"""Master a dry 24 kHz mono PCM16 Heart take as Happy's approved v0.8 stereo voice."""
import argparse
from pathlib import Path
import wave

import numpy as np


RATE = 24000
TAIL = 15600


def rms(signal):
    return np.sqrt(np.mean(signal * signal))


def match_level(signal, reference):
    peak = np.max(np.abs(signal))
    if peak == 0:
        return signal
    return signal * min(rms(reference) / rms(signal), .91 / peak)


def electronic_layer(dry):
    size, hop = 1024, 128
    padded = np.pad(dry, size)
    output = np.zeros_like(padded)
    weights = np.zeros_like(padded)
    window = np.sqrt(np.hanning(size))
    frequency = np.fft.rfftfreq(size, 1 / RATE)
    edges = np.geomspace(85, 10500, 26)
    filters = np.stack([np.maximum(0, np.minimum((frequency-l)/(m-l), (h-frequency)/(h-m)))
                        for l, m, h in zip(edges, edges[1:], edges[2:])])
    time = np.arange(len(padded)) / RATE
    carrier = sum(np.sin(2*np.pi*k*182*time + .15*k*k)/k**.38 for k in range(1, 57))
    treble = np.clip((frequency - 3300) / 1700, 0, 1)
    band_sum = np.maximum(filters.sum(axis=0), .02)
    for start in range(0, len(padded) - size + 1, hop):
        frame = slice(start, start + size)
        speech = np.fft.rfft(padded[frame] * window)
        excitation = np.fft.rfft(carrier[frame] * window)
        envelope = np.sqrt((filters @ np.abs(speech)**2) /
                           (filters @ np.abs(excitation)**2 + 1e-7))
        voiced = excitation * (envelope @ filters) / band_sum
        spectrum = (1 - treble)*voiced + treble*speech
        output[frame] += window * np.fft.irfft(spectrum)
        weights[frame] += window * window
    region = slice(size, size + len(dry))
    return output[region] / np.maximum(weights[region], 1e-6)


def diffuse(signal, seed, near):
    length = 1536 if near else 16384
    time = np.arange(length) / RATE
    noise = np.random.default_rng(seed).normal(size=length)
    freq = np.fft.rfftfreq(length, 1 / RATE)
    shape = (1 - np.exp(-(freq/700)**2)) * np.exp(-(freq/4900)**4)
    impulse = np.fft.irfft(np.fft.rfft(noise) * shape)
    onset = np.maximum(time - (.0015 if near else .009), 0)
    impulse *= (1 - np.exp(-onset/(.002 if near else .022))) * np.exp(-onset/(.009 if near else .108))
    impulse *= np.clip((time[-1] - time) / .012, 0, 1)
    size = 1 << (len(signal) + length - 2).bit_length()
    return np.fft.irfft(np.fft.rfft(signal, size) * np.fft.rfft(impulse, size), size)[:len(signal)]


def master(dry, electronic=.15, near_db=0, space_db=0):
    if not np.any(dry):
        return np.zeros((len(dry) + TAIL, 2), dtype='<i2')
    vocoded = match_level(electronic_layer(dry), dry)
    full_b = match_level(.40*dry + .65*vocoded, dry)
    # The audition used a PCM16 B stem and the RMS of its v0.4 listening master.
    full_b = np.rint(full_b * 32767) / 32768
    previous = .65*full_b + .35*dry
    target = rms(np.rint(previous * (rms(full_b)/rms(previous)) * 32768) / 32768)
    direct = electronic*full_b + (1-electronic)*dry
    direct = np.pad(direct * (target/rms(direct)), (0, TAIL))
    direct_level = rms(direct)

    def reflection(seed, near, db):
        signal = diffuse(direct, seed, near)
        return signal * direct_level / max(rms(signal), 1e-15) * 10**(db/20)

    mid = direct + reflection(501, False, -12.5+space_db)
    side = reflection(502, False, -13+space_db)
    mid += reflection(801, True, -9+near_db)
    side += reflection(802, True, -6.5+near_db)
    stereo = np.column_stack((mid + side, mid - side))
    stereo *= min(target/rms(stereo[:len(dry)]), .98/np.max(np.abs(stereo)))
    return np.rint(stereo * 32767).astype('<i2')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('dry', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    if args.dry.resolve() == args.output.resolve():
        parser.error('Choose a separate output file to preserve the dry take.')
    with wave.open(str(args.dry)) as source:
        if (source.getframerate(), source.getnchannels(), source.getsampwidth()) != (RATE, 1, 2):
            parser.error('Input must be a dry mono 24 kHz PCM16 WAV.')
        samples = np.frombuffer(source.readframes(source.getnframes()), dtype='<i2')
    if not len(samples):
        parser.error('Empty input.')
    stereo = master(samples.astype(float) / 32768)
    with wave.open(str(args.output), 'wb') as dest:
        dest.setparams((2, 2, RATE, 0, 'NONE', 'not compressed'))
        dest.writeframes(stereo.tobytes())


if __name__ == '__main__':
    main()
