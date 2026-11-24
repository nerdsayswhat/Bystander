import numpy as np, os
from scipy.io import wavfile
from scipy.signal import butter, sosfilt
SR = 48000; raw = r"C:\Ashwin\Unity\Bystander\SoundReview\dialogue_raw"; out = r"C:\Ashwin\Unity\Bystander\SoundReview\dialogue"; os.makedirs(out, exist_ok=True)
rng = np.random.default_rng(3)
def load(f):
    sr, x = wavfile.read(os.path.join(raw, f)); assert sr == SR, sr; return x / 32768.0
def trim(x, thr=0.03, keep=0.06):
    e = np.abs(x); i = np.where(e > e.max() * thr)[0]; return x[max(0, i[0] - int(keep * SR)): i[-1] + int(keep * SR)]
def wobble(x, depth, hz):                                       # a slightly unsteady pitch, as a tired older voice
    t = np.arange(len(x)) / SR; pos = np.cumsum(1 + depth * np.sin(2 * np.pi * hz * t + rng.uniform(0, 6)))
    pos = pos[pos < len(x) - 1]; return np.interp(pos, np.arange(len(x)), x)
def band(x, lo, hi): return sosfilt(butter(4, [lo, hi], btype="band", fs=SR, output="sos"), x)
def breath(n, level):
    nz = band(rng.standard_normal(n), 1500, 7000); t = np.arange(n) / SR
    return nz * level * (0.5 + 0.5 * np.sin(2 * np.pi * 2.2 * t) ** 2)
def finish(x, name, peak=0.85):
    x = x * (peak / np.abs(x).max()); n = int(0.01 * SR); x[:n] *= np.linspace(0, 1, n); m = int(0.08 * SR); x[-m:] *= np.linspace(1, 0, m)
    wavfile.write(os.path.join(out, name + ".wav"), SR, (x * 32767).astype(np.int16)); print(name, "%.2fs" % (len(x) / SR))
def old(x, rough, tremor):
    x = trim(x); x = wobble(x, 0.012, 5.3)
    t = np.arange(len(x)) / SR; x = x * (1 - tremor + tremor * np.sin(2 * np.pi * 5.8 * t) ** 2)          # shaky level
    x = x + rough * x * rng.standard_normal(len(x)) * 0.35                                                  # a rough, dry edge
    x = band(x, 90, 5500) + breath(len(x), 0.012) * np.abs(x).max()                                         # dry and close, a little breath
    return x
finish(old(load("old_dont_1.wav"), 0.5, 0.18), "oldman_dont_bother")
finish(old(load("old_dont_2.wav"), 0.6, 0.22), "oldman_dont_bother_2")
finish(old(load("old_dont_3.wav"), 0.4, 0.14), "oldman_dont_bother_3")
finish(old(load("old_thanks.wav"), 0.5, 0.2) * 0.8, "oldman_thanks", 0.7)
w = trim(load("woman_thanks.wav")); w = wobble(w, 0.006, 4.8); w = band(w, 120, 7500) + breath(len(w), 0.03) * np.abs(w).max(); finish(w, "woman_thanks", 0.75)
