import numpy as np, os
from scipy.io import wavfile
from scipy.signal import resample_poly
src = open(r"C:\Users\ashwi\AppData\Local\Temp\claude\d62b932dd5cff743\067fdf1e-250f-4f36-8c6d-c5a1ddfde654\scratchpad\medley6.py").read()
exec(src[:src.index("Lc = np.zeros(N)")])            # the instruments, scale and helpers of the approved draft
OUTD = r"C:\Ashwin\Unity\Bystander\Assets\Resources\Audio"; GSR = 32000
rngg = np.random.default_rng(5)
def reverb_short(x):
    n = int(1.6 * SR); t = np.arange(n) / SR; ir = rngg.standard_normal(n) * np.exp(-t / 0.45); ir = lp(ir, 5000)
    ir[:int(0.012 * SR)] *= np.linspace(0, 1, int(0.012 * SR)); ir /= np.abs(ir).sum() * 0.35; return fftconvolve(x, ir)[:len(x)]
def save(name, x, peak=0.85):
    x = x / np.abs(x).max() * peak; y = resample_poly(x, GSR, SR); fade = int(0.05 * GSR); y[-fade:] *= np.linspace(1, 0, fade)
    wavfile.write(os.path.join(OUTD, name + ".wav"), GSR, (y * 32767).astype(np.int16))
def nm(inst, deg): return "cn_%s_%s%d" % (inst, "m" if deg < 0 else "d", abs(deg))
need = {"harp": [3, 5, 7, 8, 10], "vibes": [3, 5, 6, 8, 9, 10], "musicbox": [11], "piano": [6, 7, 8, 10, 11, 13], "flute": [11, 13, 15, 16, 17],
        "strings": list(range(-5, 6)), "choir": list(range(-3, 8))}
cnt = 0
import sys
for inst, degs in need.items():
    for d in degs:
        y = render(inst, d); y = np.concatenate([y, np.zeros(int(0.8 * SR))]); y = y + 0.4 * reverb_short(y)                  # a little room baked in (the game has no reverb of its own)
        save(nm(inst, d), y[:int(3.0 * SR)]); cnt += 1
for d in range(14, 21):
    y = glock(midi(scale_note(d))); y = np.concatenate([y, np.zeros(int(0.8 * SR))]); y = y + 0.4 * reverb_short(y); save(nm("glock", d), y[:int(3.0 * SR)]); cnt += 1
def pad_chord(notes, length, fade=2.5):
    n = int(length * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for m_ in notes:
        for det in (-0.07, 0.0, 0.08): y += (2 * ((midi(m_) * 2 ** (det / 12) * t) % 1) - 1) * 0.12
    return lp(y, 700) * np.minimum(1, t / fade) * np.minimum(1, (length - t) / fade)
chords = [[47, 54, 59, 62, 66], [43, 50, 55, 59, 62], [50, 57, 62, 66, 69], [45, 52, 57, 61, 64]]
# the pad (Bm - G - D - A), 70 s mono, fades in over 5 s and out from 63 s
padx = np.zeros(N)
for i in range(8):
    seg = pad_chord(chords[i % 4], 11.0); s0 = int(i * 9.0 * SR); m = min(len(seg), N - s0)
    if m > 0: padx[s0:s0 + m] += seg[:m]
padx *= np.minimum(1, np.arange(N) / (SR * 5)); e = np.ones(N); i0 = int(63.0 * SR); e[i0:] = np.linspace(1, 0, N - i0) ** 1.5; padx *= e
save("cards_pad", padx, 0.6); print(cnt + 1, "files written")
