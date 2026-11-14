"""Synthesize the title's ambience and the opening's cues, loaded by DuelUI from Resources/TitleSfx.

    python Tools/make_title_sfx.py

TitleWind      loop, 12 s. Wind over a rooftop: low band noise that swells and thins, with a faint higher whistle.
TitleHum       loop, 8 s. The city across the valley: a low drone that beats slowly, with a little rumble.
TitleLight1-5  a building group switching on: a relay knock and a short note, each group a step higher.
TitleLogo      the logo landing: three short pieces of broken grit and nothing else.

The revolver core lights in silence: a rising tone was tried and removed (Thomas, 2026-11-14).

The loops repeat without a seam: every slow change completes whole cycles, and the noise is cross-faded
end to start. All are 48 kHz mono 16-bit. Change the numbers and run again. Standard library only.
"""
import math
import os
import random
import struct
import wave

RATE = 48000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', '_Game', 'Levels', 'L01_Duel',
                   'Resources', 'TitleSfx')
TAU = 2 * math.pi


def high_pass(samples, cutoff):
    rc = 1 / (TAU * cutoff)
    a = rc / (rc + 1 / RATE)
    out, prev_in, prev_out = [], 0.0, 0.0
    for x in samples:
        prev_out = a * (prev_out + x - prev_in)
        prev_in = x
        out.append(prev_out)
    return out


def low_pass(samples, cutoff):
    a = (1 / RATE) / (1 / (TAU * cutoff) + 1 / RATE)
    out, prev = [], 0.0
    for x in samples:
        prev += a * (x - prev)
        out.append(prev)
    return out


def normalise(samples, peak):
    top = max(abs(x) for x in samples) or 1
    return [x * peak / top for x in samples]


def finish(samples, peak, fade=.02):
    n = len(samples)
    edge = int(fade * RATE)
    for i in range(min(edge, n)):
        samples[n - 1 - i] *= i / edge
    for i in range(min(32, n)):
        samples[i] *= i / 32
    return normalise(samples, peak)


def seamless(samples, length, overlap):
    """Fold the extra tail back over the start with an equal-power cross-fade."""
    out = samples[:length]
    for i in range(overlap):
        k = i / overlap
        out[i] = samples[length + i] * math.cos(k * math.pi / 2) + samples[i] * math.sin(k * math.pi / 2)
    return out


def save(name, samples):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + '.wav')
    with wave.open(path, 'wb') as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, x)) * 32767)) for x in samples))
    print(name, round(len(samples) / RATE, 3), 's')


def title_wind():
    seconds, extra = 12, 1.5
    length, overlap = seconds * RATE, int(extra * RATE)
    rng = random.Random(21)
    raw = [rng.uniform(-1, 1) for _ in range(length + overlap)]
    body = seamless(low_pass(low_pass(high_pass(raw, 90), 520), 520), length, overlap)
    whistle = seamless(low_pass(high_pass(raw[::-1], 900), 1700), length, overlap)
    out = []
    for i in range(length):
        t = i / RATE
        # Gusts: whole cycles in twelve seconds, so the swell closes on itself.
        gust = .62 + .24 * math.sin(TAU * t / 12 + .6) + .14 * math.sin(TAU * 3 * t / 12 + 2.1) + .08 * math.sin(TAU * 5 * t / 12)
        thin = max(0, math.sin(TAU * 2 * t / 12 + 1.2)) ** 2
        out.append(body[i] * gust + whistle[i] * .16 * thin * gust)
    save('TitleWind', normalise(out, .5))


def title_hum():
    seconds, extra = 8, 1.0
    length, overlap = seconds * RATE, int(extra * RATE)
    rng = random.Random(8)
    rumble = seamless(low_pass(low_pass([rng.uniform(-1, 1) for _ in range(length + overlap)], 150), 150), length, overlap)
    rumble = normalise(rumble, 1)
    out = []
    for i in range(length):
        t = i / RATE
        # Every partial completes whole cycles in eight seconds. The detuned pairs beat every few seconds.
        drone = (math.sin(TAU * 55 * t) + math.sin(TAU * 55.25 * t) * .8
                 + math.sin(TAU * 82.5 * t) * .45 + math.sin(TAU * 110.375 * t) * .30
                 + math.sin(TAU * 165 * t + 1.0) * .10 + math.sin(TAU * 220.125 * t) * .05)
        out.append(drone * .30 + rumble[i] * .22)
    save('TitleHum', normalise(out, .5))


def title_light(index, pitch):
    n = int(.26 * RATE)
    rng = random.Random(30 + index)
    noise = low_pass(high_pass([rng.uniform(-1, 1) for _ in range(n)], 1500), 6500)
    out, knock = [], 0.0
    for i in range(n):
        t = i / RATE
        edge = noise[i] * math.exp(-t / .004) * .55
        knock += TAU * (140 + 260 * math.exp(-t / .006)) / RATE
        relay = math.sin(knock) * math.exp(-t / .020) * .55
        note = (math.sin(TAU * pitch * t) + math.sin(TAU * pitch * 2 * t) * .25) * math.exp(-t / .075) * .45 * min(1, t / .004)
        out.append(edge + relay + note)
    save('TitleLight%d' % index, finish(out, .6))


def title_logo():
    # Only the broken tail: three short pieces of grit, like the logo's own slip. The hard edge and the
    # falling thump that used to lead it were removed (Thomas, 2026-11-14).
    n = int(.19 * RATE)
    rng = random.Random(12)
    full = int(.62 * RATE)
    # The same noise as before with its top taken down twice: 4200 Hz was too sharp, so it now sits around 2 kHz.
    grit = low_pass(low_pass(high_pass([rng.uniform(-1, 1) for _ in range(full)][::-1], 420), 2300), 2300)
    pieces = ((0, .03), (.07, .09), (.14, .155))
    lead = int(.07 * RATE)                              # the same grit the pieces were first cut from
    out = []
    for i in range(n):
        t = i / RATE
        gate = 0.0
        for start, end in pieces:
            if start <= t < end:
                gate = min(1, (t - start) / .001, (end - t) / .001)   # a millisecond at each cut, so it does not click
        out.append(grit[lead + i] * gate)
    save('TitleLogo', normalise(out, .5))


if __name__ == '__main__':
    title_wind()
    title_hum()
    # A minor pentatonic climb; the tower, last, lands an octave above the first.
    for index, pitch in enumerate((220.0, 261.63, 293.66, 329.63, 440.0), 1):
        title_light(index, pitch)
    title_logo()
