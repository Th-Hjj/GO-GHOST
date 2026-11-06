"""Offline, non-destructive line rendering. Times are seconds in the source take."""

import gzip
import hashlib
import re
import subprocess
import sys
import wave
from pathlib import Path

import numpy as np
import parselmouth as pm

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import render_happy_voice

RATE = render_happy_voice.RATE

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
BANK = ROOT / "Assets/StreamingAssets/HandlerVoice"
CACHE = ROOT / "Library/VoiceStudio"


def read(path):
    try:
        with wave.open(str(path)) as w:
            if w.getsampwidth() != 2 or w.getframerate() != RATE or w.getnchannels() not in (1, 2):
                raise ValueError("Convert format.")
            return np.frombuffer(w.readframes(w.getnframes()), "<i2").reshape(-1, w.getnchannels()) / 32768
    except (wave.Error, ValueError, EOFError):
        import imageio_ffmpeg

        data = subprocess.check_output(
            [
                imageio_ffmpeg.get_ffmpeg_exe(),
                "-nostdin",
                "-v",
                "error",
                "-i",
                str(path),
                "-vn",
                "-ac",
                "2",
                "-ar",
                str(RATE),
                "-f",
                "s16le",
                "-",
            ]
        )
        return np.frombuffer(data, "<i2").reshape(-1, 2) / 32768


def write(path, x):
    if len(x) == 0 or not np.all(np.isfinite(x)):
        raise ValueError("Empty or invalid audio.")
    if np.max(np.abs(x)) > 1:
        x = x * (0.99 / np.max(np.abs(x)))
    with wave.open(str(path), "wb") as w:
        w.setparams((x.shape[1], 2, RATE, 0, "NONE", "not compressed"))
        w.writeframes(np.clip(np.rint(x * 32768), -32768, 32767).astype("<i2").tobytes())


def adjust_finished_happy(x, electronic):
    """Retune a finished Happy take relative to its baked 15% treatment."""
    baseline = 0.15
    if abs(electronic - baseline) < 1e-6 or not np.any(x):
        return x

    if electronic > baseline:
        # The existing stereo returns already contain the room. Replace part of the
        # centre with the same B-layer used by the dry-source renderer, without a second reverb.
        center = x.mean(axis=1)
        vocoded = render_happy_voice.match_level(render_happy_voice.electronic_layer(center), center)
        layer = render_happy_voice.match_level(0.4 * center + 0.65 * vocoded, center)
        mix = (electronic - baseline) / (1 - baseline)
        result = x + mix * (layer - center)[:, None]
    else:
        # A mixed recording has no isolated dry stem. Attenuate the narrow
        # harmonics of Happy's steady 182 Hz carrier, leaving the broadband speech.
        size = 1 << (len(x) + RATE // 2 - 1).bit_length()
        hz = np.fft.rfftfreq(size, 1 / RATE)
        harmonic = np.rint(hz / 182)
        width = 1 - np.exp(-0.5 * ((hz - 182 * harmonic) / 10) ** 2)
        keep = np.where((harmonic >= 1) & (hz < 5000), width, 1)
        gain = 1 - (1 - electronic / baseline) * (1 - keep)
        result = np.fft.irfft(np.fft.rfft(x, size, axis=0) * gain[:, None], size, axis=0)[:len(x)]

    peak = np.max(np.abs(result))
    if peak > 0:
        result *= min(render_happy_voice.rms(x) / render_happy_voice.rms(result), (32767 / 32768) / peak)
    return result


def synth(text, boss, speed, pronunciations=""):
    import sherpa_onnx as so

    if not text.strip():
        raise ValueError("Enter the spoken text before synthesizing.")
    lex = BANK / "lexicon-us-en.txt"
    if boss:
        lex = CACHE / "lexicon-gb-en.txt"
        if not lex.exists():
            lex.parent.mkdir(parents=True, exist_ok=True)
            lex.write_bytes(gzip.decompress((HERE / "lexicon-gb-en.txt.gz").read_bytes()))
    # A user-supplied lexicon entry takes precedence over the model dictionary.
    # Preserve the original production pronunciation of the tutorial's possessive.
    extras = {"enjoyed": "ɪ n ʤ ˈ Y d"} if boss else {"body's": "b ˈ ɑ d i z", "destroyed": "d ɪ s t ɹ ˈ Y d"}
    for entry in pronunciations.splitlines():
        if not entry.strip():
            continue
        word, space, phones = entry.strip().partition(" ")
        if not space or not phones.strip():
            raise ValueError("Use one word and its Kokoro phones per line.")
        extras[word.lower()] = phones.strip()
    words = set(re.findall(r"[a-z]+(?:'[a-z]+)?", text.lower().replace("’", "'")))
    dictionary = {w: p for w, p in extras.items() if w in words}
    with lex.open(encoding="utf-8") as f:
        for row in f:
            word, _, phones = row.strip().partition(" ")
            if word in words and word not in dictionary:
                dictionary[word] = phones
    missing = words - dictionary.keys()
    if missing:
        raise ValueError("Add pronunciations for: " + ", ".join(sorted(missing)))
    # Keep the full dictionary for number expansion / punctuation normalization.
    if extras:
        extra = "\n".join(w + " " + p for w, p in sorted(extras.items()))
        name = hashlib.sha256((lex.name + extra).encode()).hexdigest()
        custom = CACHE / ("lex-" + name + ".txt")
        if not custom.exists():
            base = lex.read_text(encoding="utf-8").splitlines()
            custom.write_text(
                extra + "\n" + "\n".join(r for r in base if r.partition(" ")[0] not in extras) + "\n",
                encoding="utf-8",
            )
        lex = custom
    config = so.OfflineTtsConfig()
    k = config.model.kokoro
    k.model, k.tokens, k.lexicon = str(BANK / "model.int8.onnx"), str(BANK / "tokens.txt"), str(lex)
    k.voices = str(HERE / "author-voices.bin" if boss else BANK / "voices.bin")
    config.model.num_threads = 2
    engine = so.OfflineTts(config)
    take = engine.generate(text, sid=int(boss), speed=speed)
    if take.sample_rate != RATE or len(take.samples) == 0:
        raise ValueError("Synthesis produced no 24 kHz audio.")
    return np.asarray(take.samples, dtype=float)[:, None]


def warp(x, source, dest):
    """Stereo-linked WSOLA. A single alignment offset keeps both channels in phase."""
    if np.any(np.diff(source) <= 0) or np.any(np.diff(dest) <= 0):
        raise ValueError("Time markers must keep their order.")
    if np.allclose(source, dest, atol=1.0e-8, rtol=0):
        return x
    hop, radius = 360, 120
    length = max(1, round(dest[-1] * RATE))
    padded = np.pad(x, ((0, hop * 5), (0, 0)))
    y = np.zeros((length + 2 * hop, x.shape[1]))
    y[: 2 * hop] = padded[: 2 * hop]
    fade = (0.5 - 0.5 * np.cos(np.linspace(0, np.pi, hop)))[:, None]
    for pos in range(hop, length, hop):
        expected = round(np.interp(pos / RATE, dest, source) * RATE)
        lo, hi = max(0, expected - radius), min(len(x), expected + radius)
        block = padded[lo : hi + hop]
        overlap = y[pos : pos + hop]
        dot = sum(np.correlate(block[:, c], overlap[:, c], "valid") for c in range(x.shape[1]))
        energy = np.convolve((block * block).sum(axis=1), np.ones(hop), "valid")
        score = dot / np.sqrt(np.maximum(energy * (overlap * overlap).sum(), 1.0e-20))
        start = lo + np.argmax(score - 0.00001 * np.abs(np.arange(lo, hi + 1) - expected))
        y[pos : pos + hop] = overlap * (1 - fade) + padded[start : start + hop] * fade
        y[pos + hop : pos + 2 * hop] = padded[start + hop : start + 2 * hop]
    return y[:length]


def features(x):
    mono = x.mean(axis=1)
    step = 240
    blocks = np.pad(mono, (0, (-len(mono)) % step)).reshape(-1, step)
    amp = np.sqrt((blocks**2).mean(axis=1))
    times = (np.arange(len(amp)) + 0.5) * step / RATE
    pitch = pm.Sound(mono, sampling_frequency=RATE).to_pitch(0.01, 65, 500)
    hz = pitch.selected_array["frequency"]
    valid = hz > 0
    contour = np.zeros(len(times))
    if valid.any():
        contour = np.interp(times, pitch.xs()[valid], 12 * np.log2(hz[valid] / np.median(hz[valid])))
    return times, amp, contour


def edit(x, recipe, tempo, reference=None):
    duration = len(x) / RATE
    # Integrate local rate/gain/pitch continuously so the words stay in one performance.
    t = np.linspace(0, duration, max(2, int(duration * 200) + 1))
    dt = np.diff(t)
    speed = float(recipe["speed"]) * tempo
    scale = np.full(len(t), 1 / speed)
    pitch = np.full(len(t), float(recipe["pitch"]))
    gain = np.full(len(t), float(recipe["gain"]))
    gaps = []
    for mark in recipe["edits"] or []:
        a, b = mark["start"], mark["end"]
        if not 0 <= a < b <= duration + 0.001:
            raise ValueError("Place each selection inside the source waveform.")
        weight = np.clip(np.minimum((t - a) / 0.012, (b - t) / 0.012), 0, 1)
        scale *= 1 + weight * (mark["stretch"] - 1)
        pitch += weight * mark["pitch"]
        gain += weight * mark["gain"]
        if mark["gapMs"]:
            if abs(mark["gapMs"]) > 3000:
                raise ValueError("A single gap edit must be within ±3000 ms.")
            gaps.append((b, mark["gapMs"] / 1000))
    if reference is not None and any(recipe[k] for k in ("timing", "melody", "stress")):
        u, a, f = features(x)
        v, b, g = features(reference)

        # Match cumulative voiced energy within the utterance, ignoring recorder lead-in.
        def positions(time, amp):
            active = amp > max(0.0005, amp.max() * 0.065)
            if active.sum() < 4:
                raise ValueError("Reference and source both need audible speech.")
            i, j = np.flatnonzero(active)[[0, -1]]
            weight = np.cumsum(np.maximum(amp[i : j + 1], amp.max() * 0.03) ** 0.5)
            return np.interp(
                np.linspace(0, 1, 17), (weight - weight[0]) / (weight[-1] - weight[0]), time[i : j + 1]
            )

        src, ref = positions(u, a), positions(v, b)
        target = np.interp(t, src, ref)
        inside = (t >= src[0]) & (t <= src[-1])
        ratio = np.clip(np.gradient(target, t), 0.25, 4)
        scale[inside] *= 1 + recipe["timing"] * (ratio[inside] - 1)
        pitch[inside] += (
            recipe["melody"] * np.clip(np.interp(target, v, g) - np.interp(t, u, f), -9, 9)[inside]
        )
        db = 20 * np.log10(
            (np.interp(target, v, b) / max(b.max(), 1e-7) + 0.01)
            / (np.interp(t, u, a) / max(a.max(), 1e-7) + 0.01)
        )
        gain[inside] += recipe["stress"] * np.clip(db[inside], -10, 10)
    dest = np.r_[0, np.cumsum(dt * (scale[:-1] + scale[1:]) / 2)]
    # Resample for pitch, then WSOLA back to the independently specified timing.
    shifted = np.r_[0, np.cumsum(dt * 2 ** (-(pitch[:-1] + pitch[1:]) / 24))]
    if np.any(pitch):
        sample_t = np.interp(np.arange(round(shifted[-1] * RATE)) / RATE, shifted, t)
        x = np.column_stack([np.interp(sample_t * RATE, np.arange(len(x)), channel) for channel in x.T])
    clock = np.arange(len(x)) / RATE
    if np.any(gain):
        x = x * 10 ** (np.interp(clock, shifted, gain)[:, None] / 20)
    y = warp(x, shifted, dest)
    mapped = [
        {"start": float(np.interp(m["start"], t, dest)), "end": float(np.interp(m["end"], t, dest))}
        for m in recipe["edits"] or []
    ]
    for end, gap in sorted(gaps, reverse=True):
        boundary = float(np.interp(end, t, dest))
        at = round(boundary * RATE)
        n = round(abs(gap) * RATE)
        if n == 0:
            continue
        if gap < 0:
            width = min(144, len(y) - at - n)
            if width < 1:
                raise ValueError("Gap reduction extends beyond the take.")
            if any(boundary < m["start"] < boundary - gap for m in mapped):
                raise ValueError("Shorten the gap less, or adjust the next word boundary.")
            f = np.linspace(0, 1, width)[:, None]
            y = np.concatenate(
                (y[:at], y[at : at + width] * (1 - f) + y[at + n : at + n + width] * f, y[at + n + width :])
            )
        else:
            width = min(96, at, len(y) - at)
            if width:
                f = np.linspace(0, 1, width)[:, None]
                y[at - width : at] *= 1 - f
                y[at : at + width] *= f
            y = np.concatenate((y[:at], np.zeros((n, y.shape[1])), y[at:]))
        for mark in mapped:
            for bound in ("start", "end"):
                if mark[bound] > boundary + 1.0e-6:
                    mark[bound] = max(boundary, mark[bound] + gap)
    return y, mapped


def boss_master(dry, sub_gain, drive):
    grid = np.arange(len(dry))
    slow = np.interp(np.arange(int(len(dry) / 0.86) + 8400) * 0.86, grid, dry, right=0)

    def shift(ratio, size):
        mix = np.zeros_like(slow)
        grid = np.arange(len(slow))
        for i in range(-size, len(slow), size // 4):
            k = np.arange(max(0, -i), min(size, len(slow) - i))
            sample = i + size / 2 + (k - size / 2) * ratio
            mix[i + k] += (
                0.25 * (1 - np.cos(2 * np.pi * k / size)) * np.interp(sample, grid, slow, left=0, right=0)
            )
        return mix

    def band(x, f, q):
        omega = 2 * np.pi * f / RATE
        a = np.sin(omega) / (2 * q)
        b, c, d = a / (1 + a), 2 * np.cos(omega) / (1 + a), (a - 1) / (1 + a)
        out = np.zeros(len(x) + 2)
        delayed = np.pad(x, (2, 0))
        for i in range(len(x)):
            out[i + 2] = b * (x[i] - delayed[i]) + c * out[i + 1] + d * out[i]
        return out[2:]

    low = shift(0.68, 2400)
    sub = shift(0.49, 3360)
    deep = np.tanh(
        drive
        * (
            0.77 * low
            + sub_gain * sub
            + 0.62 * band(low, 145, 0.8)
            + 0.34 * band(slow, 3000, 0.85)
            + 0.11 * slow
        )
    )
    wet = deep.copy()
    for delay, amplitude in ((1176, 0.19), (2232, 0.10)):
        wet[delay:] += amplitude * deep[:-delay]
    return (wet * (0.87 / max(1.0e-8, np.max(np.abs(wet)))))[:, None]
