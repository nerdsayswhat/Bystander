import numpy as np
from scipy.io import wavfile
SR = 48000; rng = np.random.default_rng(5); n = int(0.9 * SR); y = np.zeros(n)
def thump(t0, a, f0, dec):
    i0 = int(t0 * SR); m = int(0.25 * SR); t = np.arange(m) / SR
    f = f0 * (1 + 1.5 * np.exp(-t / 0.015))                                    # a rubber ball: a quick downward pitch drop
    ph = 2 * np.pi * np.cumsum(f) / SR
    s = np.sin(ph) * np.exp(-t / dec) + 0.35 * np.sin(2.1 * ph) * np.exp(-t / (dec * 0.5))
    tick = rng.standard_normal(m) * np.exp(-t / 0.004); tick = np.diff(tick, prepend=0)   # the surface contact: a tiny high-passed tick
    y[i0:i0 + m] += a * (s + 0.12 * tick)[:max(0, min(m, n - i0))]
thump(0.02, 0.5, 120, 0.05)       # picked up off the ground
thump(0.38, 0.35, 150, 0.04)      # a small bounce in the hand
thump(0.52, 0.16, 170, 0.03)
y *= 0.9 / np.abs(y).max(); y[-int(0.05 * SR):] *= np.linspace(1, 0, int(0.05 * SR))
wavfile.write(r"C:\Ashwin\Unity\Bystander\SoundReview\ball_pickup.wav", SR, (y * 32767).astype(np.int16)); print("ok")
