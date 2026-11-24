import numpy as np, os
from scipy.io import wavfile
SR = 48000; out = r"C:\Ashwin\Unity\Bystander\Assets\Resources\Audio"; dec = r"C:\Ashwin\Unity\Bystander\SoundReview\decoded"
def load(p):
    sr, x = wavfile.read(p); x = x / 32768.0
    return x.mean(axis=1) if x.ndim == 2 else x
def save(name, x, peak):
    x = x * (peak / np.abs(x).max()); wavfile.write(os.path.join(out, name + ".wav"), SR, (np.clip(x, -1, 1) * 32767).astype(np.int16))
# siren loop: the whole 18.5 s siren, tail crossfaded into the head (equal power) so it repeats without a gap
s = load(os.path.join(dec, "Police siren.wav")); n = int(2.0 * SR); t = np.linspace(0, np.pi / 2, n)
loop = np.concatenate([s[-n:] * np.cos(t) + s[:n] * np.sin(t), s[n:-n]]); save("siren_loop", loop, 0.8); print("siren_loop %.1fs" % (len(loop) / SR))
# punches (synthesized, no recording was provided): a body thump, a slap of skin and a short air burst, five different ones
rng = np.random.default_rng(21)
for k in range(5):
    m = int(0.45 * SR); t = np.arange(m) / SR; y = np.zeros(m)
    f0 = rng.uniform(70, 110); f = f0 * (1 + 1.2 * np.exp(-t / 0.02)); ph = 2 * np.pi * np.cumsum(f) / SR
    y += np.sin(ph) * np.exp(-t / rng.uniform(0.05, 0.09))                                      # the thump of the fist into the body
    nz = rng.standard_normal(m); lp = np.zeros(m); a = rng.uniform(0.15, 0.35)
    for i in range(1, m): lp[i] = lp[i - 1] + a * (nz[i] - lp[i - 1])                           # low-passed noise: the dull slap of flesh
    y += 0.9 * lp * np.exp(-t / rng.uniform(0.02, 0.04))
    tick = np.diff(rng.standard_normal(m), prepend=0) * np.exp(-t / 0.003); y += 0.18 * tick    # the sharp edge of the contact
    y[:int(0.002 * SR)] *= np.linspace(0, 1, int(0.002 * SR)); y[-int(0.08 * SR):] *= np.linspace(1, 0, int(0.08 * SR))
    save("punch" if k == 0 else "punch_%d" % (k + 1), y, 0.9)
print("punch x5")
