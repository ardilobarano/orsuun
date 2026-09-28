"""The river's and the town's sounds (ElevenLabs Sound Effects v2, flow "Orsuun river sounds", 28 Sep 2026): docs/concept/sounds/*.mp3
decoded with macOS afconvert, the chosen takes trimmed and levelled into client/Assets/Orsuun/Resources/Audio as mono
16-bit WAVs: RiverWater, RiverBirds, RiverFire (seamless loops, levelled to about -30 dBFS with a soft limit on the
peaks), RiverReel (a loop while a fish is fought), RiverCast, RiverSplash (the float landing), RiverPlop (a bite) and
TownMarket (the town square's murmur, a loop). The town's theme is music (Content/Music/MusicTown.mp3, copied as made).
The maps' ambience loops (Amb*, one per kind of map) go to client/Assets/Orsuun/Content/Ambience (downloaded art) at
22.05 kHz. Run with /usr/bin/python3 (numpy)."""
import os
import subprocess
import tempfile
import wave
import numpy as np

ROOT = os.path.join(os.path.dirname(__file__), '..', '..')
SRC = os.path.join(ROOT, 'docs', 'concept', 'sounds')
OUT = os.path.join(ROOT, 'client', 'Assets', 'Orsuun', 'Resources', 'Audio')
AMBIENCE = os.path.join(ROOT, 'client', 'Assets', 'Orsuun', 'Content', 'Ambience')
RATE = 32000
AMBIENCE_RATE = 22050

# name: (take, loop, trim from, trim to (s), level: rms dBFS for loops, peak dBFS for one-shots)
SOUNDS = {
    'RiverWater': ('water-a', True, 0, None, -30),
    'RiverBirds': ('birds-a', True, 0, None, -34),
    'RiverFire': ('fire-a', True, 0, None, -33),
    'RiverReel': ('reel-a', True, 0, None, -24),
    'RiverCast': ('cast-a', False, 0.1, 0.62, -1),
    'RiverSplash': ('splash-a', False, 0, 0.55, -2),
    'RiverPlop': ('splash-b', False, 0, 0.55, -1),
    'TownMarket': ('town-market', True, 0, None, -32),
}

# The maps' ambience (Content/Ambience): all loops, levelled alike.
AMBIENT = ['Steppe', 'Mountain', 'Salt', 'Cinder', 'Whisper', 'Birch', 'Swamp', 'Graves', 'Bazaar', 'Deep']


def decode(take, rate=RATE):
    path = os.path.join(tempfile.mkdtemp(), take + '.wav')
    subprocess.run(['afconvert', '-f', 'WAVE', '-d', 'LEI16@%d' % rate, '-c', '1', os.path.join(SRC, take + '.mp3'), path], check=True)
    w = wave.open(path)
    x = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64) / 32768
    return x


def soft_limit(x, ceiling=0.89):
    """Leaves everything under the ceiling alone and bends what is over it smoothly toward full scale."""
    y = x.copy()
    over = np.abs(x) > ceiling
    head = 1 - ceiling
    y[over] = np.sign(x[over]) * (ceiling + head * np.tanh((np.abs(x[over]) - ceiling) / head))
    return y


def write(path, x, rate):
    w = wave.open(path, 'wb')
    w.setnchannels(1)
    w.setsampwidth(2)
    w.setframerate(rate)
    w.writeframes((np.clip(x, -1, 1) * 32767).astype(np.int16).tobytes())
    w.close()
    name = os.path.splitext(os.path.basename(path))[0]
    print('%-12s %5.2fs peak %5.1f dBFS rms %5.1f dBFS' % (name, len(x) / rate, 20 * np.log10(np.abs(x).max()), 20 * np.log10(np.sqrt((x ** 2).mean()))))


def level_loop(x, level):
    rms = np.sqrt((x ** 2).mean())
    return soft_limit(x * 10 ** (level / 20) / max(rms, 1e-9))


def main():
    for name, (take, loop, t0, t1, level) in SOUNDS.items():
        x = decode(take)
        a, b = int(t0 * RATE), (int(t1 * RATE) if t1 else len(x))
        x = x[a:b]
        if loop:
            x = level_loop(x, level)
        else:
            x = x * 10 ** (level / 20) / max(np.abs(x).max(), 1e-9)
            # A short fade at each end so a trimmed one-shot starts and stops without a click.
            n = int(0.004 * RATE)
            x[:n] *= np.linspace(0, 1, n)
            m = int(0.06 * RATE)
            x[-m:] *= np.linspace(1, 0, m)
        write(os.path.join(OUT, name + '.wav'), x, RATE)
    os.makedirs(AMBIENCE, exist_ok=True)
    for kind in AMBIENT:
        take = 'amb-' + kind.lower()
        if not os.path.exists(os.path.join(SRC, take + '.mp3')):
            continue
        write(os.path.join(AMBIENCE, 'Amb' + kind + '.wav'), level_loop(decode(take, AMBIENCE_RATE), -32), AMBIENCE_RATE)


if __name__ == '__main__':
    main()
