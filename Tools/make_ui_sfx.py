"""Synthesize the interface cues loaded by DuelUI from Resources/UiSfx.

    python Tools/make_ui_sfx.py

UiHover  the pointer reaches a control: one dry, quiet tick. It is heard dozens of times a minute,
         so it has no pitch to tire of and no tail.
UiClick  a control is pressed: a hard edge over a short falling knock, then a faint high blip,
         the same family as the rule cue but a tenth of its length.

Both are 48 kHz mono 16-bit. Change the numbers and run again. Standard library only.
"""
import math
import os
import random
import struct
import wave

RATE = 48000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', '_Game', 'Levels', 'L01_Duel',
                   'Resources', 'UiSfx')
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


def finish(samples, peak, fade=.008):
    n = len(samples)
    edge = int(fade * RATE)
    for i in range(min(edge, n)):
        samples[n - 1 - i] *= i / edge
    for i in range(min(16, n)):
        samples[i] *= i / 16
    top = max(abs(x) for x in samples) or 1
    return [x * peak / top for x in samples]


def save(name, samples):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + '.wav')
    with wave.open(path, 'wb') as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(b''.join(struct.pack('<h', int(max(-1, min(1, x)) * 32767)) for x in samples))
    print(name, round(len(samples) / RATE, 3), 's')


def ui_hover():
    rng = random.Random(3)
    n = int(.032 * RATE)
    # Band-limited noise: bright enough to read as a tick, with the harsh top taken off.
    noise = low_pass(high_pass([rng.uniform(-1, 1) for _ in range(n)], 2400), 7500)
    out = []
    for i in range(n):
        t = i / RATE
        tick = noise[i] * math.exp(-t / .0035)
        ring = math.sin(TAU * 3100 * t) * math.exp(-t / .006) * .30
        out.append(tick + ring)
    save('UiHover', finish(out, .55, fade=.006))


def ui_click():
    rng = random.Random(5)
    n = int(.085 * RATE)
    noise = high_pass([rng.uniform(-1, 1) for _ in range(n)], 2000)
    out = []
    knock = 0.0
    for i in range(n):
        t = i / RATE
        edge = noise[i] * math.exp(-t / .0030) * .70
        # The knock falls from 520 Hz to 190 Hz in a few milliseconds and is gone by 40 ms.
        knock += TAU * (190 + 330 * math.exp(-t / .007)) / RATE
        body = math.sin(knock) * math.exp(-t / .016) * .90
        # The blip arrives 14 ms late, so the press reads as two close steps.
        u = t - .014
        blip = math.sin(TAU * 1760 * u) * math.exp(-u / .014) * .22 if u > 0 else 0.0
        out.append(math.tanh(1.5 * (edge + body + blip)))
    save('UiClick', finish(high_pass(out, 120), .85, fade=.012))


if __name__ == '__main__':
    ui_hover()
    ui_click()
