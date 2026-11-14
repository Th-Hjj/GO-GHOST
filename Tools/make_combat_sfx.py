"""Synthesize the combat feedback cues loaded by SfxSet from Resources/CombatSfx.

    python Tools/make_combat_sfx.py

EnemyHit    a player's bullet removed enemy health: a short, bright, crisp snap with no low end,
            so it never reads as the player's own hurt cue.
RuleTrigger a rule's action takes over: a rising intake, a drop, then a falling tone
            that smears into two echoes. Original synthesis, standard library only.

Both are 48 kHz mono 16-bit, like the Final Boss cues. Change the numbers and run again.
"""
import math
import os
import random
import struct
import wave

RATE = 48000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', '_Game', 'Levels', 'L01_Duel',
                   'Resources', 'CombatSfx')
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


def finish(samples, peak, fade=.012):
    n = len(samples)
    edge = int(fade * RATE)
    for i in range(min(edge, n)):
        samples[n - 1 - i] *= i / edge
    for i in range(min(24, n)):
        samples[i] *= i / 24
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


def enemy_hit():
    rng = random.Random(7)
    n = int(.10 * RATE)
    noise = high_pass(high_pass([rng.uniform(-1, 1) for _ in range(n)], 5200), 5200)
    out = []
    partials = [0.0, 0.0, 0.0]
    for i in range(n):
        t = i / RATE
        # The partials start a little sharp and settle, which gives the snap its edge.
        bend = 1 + .05 * math.exp(-t / .004)
        for k, pitch in enumerate((2950, 4420, 6180)):
            partials[k] += TAU * pitch * bend / RATE
        snap = noise[i] * math.exp(-t / .0028) * .85
        tink = (math.sin(partials[0]) * math.exp(-t / .016) * .50
                + math.sin(partials[1]) * math.exp(-t / .011) * .32
                + math.sin(partials[2]) * math.exp(-t / .007) * .18)
        body = math.sin(TAU * 1150 * t) * math.exp(-t / .008) * .16
        out.append(snap + tink + body)
    save('EnemyHit', finish(high_pass(out, 700), .90, fade=.008))


def rule_trigger():
    rng = random.Random(11)
    length, intake = .62, .085
    n = int(length * RATE)
    noise = high_pass([rng.uniform(-1, 1) for _ in range(n)], 3200)
    dry = [0.0] * n
    rise = drop = tone_a = tone_b = 0.0
    for i in range(n):
        t = i / RATE
        if t < intake:
            # Intake: a tone and air rising into the drop.
            k = t / intake
            rise += TAU * (320 * (1500 / 320) ** k) / RATE
            dry[i] = (math.sin(rise) * .42 + noise[i] * .30) * k ** 3
            continue
        u = t - intake
        drop += TAU * (52 + 125 * math.exp(-u / .030)) / RATE
        sub = math.sin(drop) * math.exp(-u / .110) * .85
        tick = noise[i] * math.exp(-u / .004) * .40
        # The falling tone: two slightly detuned voices with a slowing flutter.
        pitch = 330 + 1250 * math.exp(-u / .090)
        tone_a += TAU * pitch * 1.007 / RATE
        tone_b += TAU * pitch * .993 / RATE
        voice = sum(math.sin(p) + .35 * math.sin(2 * p) + .20 * math.sin(3 * p) for p in (tone_a, tone_b)) * .5
        flutter = 1 - .45 * (.5 + .5 * math.sin(TAU * 34 * math.exp(-u / .25) * u))
        dry[i] = sub + tick + voice * flutter * math.exp(-u / .160) * .42
    # A slow comb over the whole cue, then two fading repeats of everything after the drop.
    out = list(dry)
    for i in range(n):
        delay = (.0012 + .0010 * math.sin(TAU * 3 * i / RATE)) * RATE
        j = i - delay
        if j >= 1:
            lo = int(j)
            out[i] += .55 * (dry[lo] + (dry[lo + 1] - dry[lo]) * (j - lo) if lo + 1 < n else dry[lo])
    start = int(intake * RATE)
    for taps, gain in ((int(.072 * RATE), .36), (int(.144 * RATE), .15)):
        for i in range(start + taps, n):
            out[i] += dry[i - taps] * gain
    save('RuleTrigger', finish([math.tanh(1.2 * x) for x in out], .90, fade=.05))


if __name__ == '__main__':
    enemy_hit()
    rule_trigger()
