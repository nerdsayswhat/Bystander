import numpy as np, os
from scipy.io import wavfile
from scipy.signal import fftconvolve, butter, sosfilt, lfilter
SR = 44100; rng = np.random.default_rng(77); OUT = r"C:\Ashwin\Unity\Bystander\SoundReview"; DUR = 70.0; N = int(DUR * SR)
def midi(m): return 440.0 * 2 ** ((m - 69) / 12)
PENT = [0, 2, 4, 7, 9]
def scale_note(deg, root=62):
    o, d = divmod(deg, 5); return root + 12 * o + PENT[d]
def env_ar(n, a, r):
    e = np.ones(n); na = int(a * SR); nr = int(r * SR)
    if na: e[:na] = np.linspace(0, 1, na) ** 1.5
    if nr: e[-nr:] *= np.linspace(1, 0, nr)
    return e
def lp(x, fc, order=2): return sosfilt(butter(order, fc, fs=SR, output="sos"), x)
def bp(x, lo, hi): return sosfilt(butter(2, [lo, hi], btype="band", fs=SR, output="sos"), x)
def musicbox(f, L=2.2):
    n = int(L * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for r, a, d in [(1, 1, 1.0), (2.0, .25, .4), (3.0, .3, .25), (5.0, .1, .12)]: y += a * np.sin(2 * np.pi * f * r * t) * np.exp(-t / d)
    return y * np.minimum(1, t / 0.002)
def piano(f, L=2.4):
    n = int(L * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for k in range(1, 11):
        fk = k * f * np.sqrt(1 + 0.0004 * k * k)
        if fk > SR / 2.2: break
        y += (1 / k ** 1.15) * np.sin(2 * np.pi * fk * t + rng.uniform(0, 6)) * np.exp(-t / (1.6 / k ** 0.55))
    y *= np.minimum(1, t / 0.004); y += 0.03 * lp(rng.standard_normal(n), 900) * np.exp(-t / 0.02)     # a soft felt hammer
    return lp(y, 5000)
def harp(f, L=2.2):                                                                                      # a smooth plucked string: a SOFT excitation (the old one was raw noise, which clicked and crackled when 100 of them overlapped)
    n = int(L * SR); d = int(round(SR / f)); exc = lp(rng.uniform(-1, 1, d), 1800); exc = exc / np.abs(exc).max(); out = np.zeros(n); b = exc.copy()
    for i in range(n):
        j = i % d; out[i] = b[j]; b[j] = 0.497 * (b[j] + b[(j + 1) % d])
    t = np.arange(n) / SR; out = lp(out, 4200) * np.minimum(1, t / 0.004)                               # no hard edge at the start
    return out * np.exp(-t / 1.1)
def flute(f, L=1.5):
    n = int(L * SR); t = np.arange(n) / SR; vib = 1 + 0.005 * np.sin(2 * np.pi * 5.1 * t) * np.minimum(1, t / 0.4); ph = 2 * np.pi * np.cumsum(f * vib) / SR
    y = np.sin(ph) + 0.14 * np.sin(2 * ph) + 0.05 * np.sin(3 * ph)
    y += 0.06 * bp(rng.standard_normal(n), min(f * 2, 6000), min(f * 5, 9000)) * np.minimum(1, t / 0.1)   # a little breath
    return y * env_ar(n, 0.12, 0.35)
def strings(f, L=2.2):                                                                                   # a bowed string section: detuned saws, slow attack, vibrato
    n = int(L * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for det in (-0.12, -0.04, 0.05, 0.13):
        ff = f * 2 ** (det / 12) * (1 + 0.004 * np.sin(2 * np.pi * 5.3 * t + rng.uniform(0, 6))); ph = 2 * np.pi * np.cumsum(ff) / SR; y += 2 * ((ph / (2 * np.pi)) % 1) - 1
    return lp(y, 2400) * env_ar(n, 0.3, 0.5) * 0.4
def choir(f, L=2.4):                                                                                     # an "oh" vowel: saws through two formants
    n = int(L * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for det in (-0.08, 0.0, 0.09):
        ff = f * 2 ** (det / 12) * (1 + 0.006 * np.sin(2 * np.pi * 5.6 * t + rng.uniform(0, 6))); ph = 2 * np.pi * np.cumsum(ff) / SR; y += 2 * ((ph / (2 * np.pi)) % 1) - 1
    y = bp(y, 380, 520) * 1.0 + bp(y, 720, 900) * 0.6 + bp(y, 2400, 3000) * 0.15
    return y * env_ar(n, 0.45, 0.6)
def vibes(f, L=2.0):                                                                                     # a mellow vibraphone: soft mallet, a slow tremolo, short bell-like partials
    n = int(L * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for r, a, d in [(1, 1, 1.0), (4.0, .35, .22), (10.0, .08, .08)]: y += a * np.sin(2 * np.pi * f * r * t) * np.exp(-t / d)
    y *= 1 + 0.18 * np.sin(2 * np.pi * 4.2 * t)
    return lp(y * np.minimum(1, t / 0.006), 4500)
INST = {"vibes": (vibes, list(range(2, 19)), 0.7), "musicbox": (musicbox, list(range(6, 16)), 0.8), "piano": (piano, list(range(0, 14)), 0.8), "harp": (harp, list(range(3, 16)), 0.8),
        "flute": (flute, list(range(7, 18)), 0.6), "strings": (strings, list(range(-5, 6)), 0.6), "choir": (choir, list(range(-3, 8)), 0.6)}
cache = {}
def render(name, deg):
    if (name, deg) not in cache:
        fn, _, g = INST[name]; y = fn(midi(scale_note(deg)))
        y = y / np.abs(y).max() * g; cache[(name, deg)] = y
    return cache[(name, deg)]
def glock(f, L=2.2):
    n = int(L * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for r, a, d in [(1, 1, 1.1), (2, .35, .55), (3, .22, .35), (4.17, .16, .22), (5.4, .09, .14)]: y += a * np.sin(2 * np.pi * f * r * t + rng.uniform(0, 6)) * np.exp(-t / d)
    return y * np.minimum(1, t / 0.002)
Lc = np.zeros(N); Rc = np.zeros(N)
def place(sig, t0, pan, gain):
    i = int(t0 * SR); m = min(len(sig), N - i)
    if m <= 0: return
    gl, gr = np.sqrt(0.5 * (1 - pan)), np.sqrt(0.5 * (1 + pan)); Lc[i:i + m] += sig[:m] * gain * gl * 1.4; Rc[i:i + m] += sig[:m] * gain * gr * 1.4
def play(name, deg, t0, vel=0.7, pan=None): place(render(name, deg), t0, rng.uniform(-0.9, 0.9) if pan is None else pan, vel)
def lines(name, t0, count, span, lo, hi, vel):                                                          # one note per card of this layer, a wandering line through the scale
    lo_, hi_ = INST[name][1][0], INST[name][1][-1]; ts = np.sort(span * rng.uniform(0, 1, count) ** 0.8); deg = (lo_ + hi_) // 2
    for tt in ts:
        deg = int(np.clip(deg + rng.choice([-2, -1, 1, 1, 2, 3]), lo_, hi_)); play(name, deg, t0 + tt, vel * rng.uniform(0.45, 1.0))
# pad
def pad_chord(notes, length, fade=2.5):
    n = int(length * SR); t = np.arange(n) / SR; y = np.zeros(n)
    for m_ in notes:
        for det in (-0.07, 0.0, 0.08): y += (2 * ((midi(m_) * 2 ** (det / 12) * t) % 1) - 1) * 0.12
    return lp(y, 700) * np.minimum(1, t / fade) * np.minimum(1, (length - t) / fade)
chords = [[47, 54, 59, 62, 66], [43, 50, 55, 59, 62], [50, 57, 62, 66, 69], [45, 52, 57, 61, 64]]
pad = np.zeros(N)
for i in range(8):
    seg = pad_chord(chords[i % 4], 11.0); s0 = int(i * 9.0 * SR); m = min(len(seg), N - s0)
    if m > 0: pad[s0:s0 + m] += seg[:m]
pad *= np.minimum(1, np.arange(N) / (SR * 5)) * 0.45
def ripple(name, t0, count, span, vel):                                                                  # layer 2: a calm, even, rising ripple (no random wandering): one note per card, evenly spaced, climbing smoothly with a gentle wave
    lo_, hi_ = INST[name][1][0] + 1, INST[name][1][-1]; step = span / count
    for k in range(count):
        u = k / (count - 1); deg = int(round(lo_ + (hi_ - lo_) * (0.1 + 0.9 * u) + 1.6 * np.sin(u * 2 * np.pi * 3)))
        deg = int(np.clip(deg, INST[name][1][0], hi_)); accent = 1.0 if k % 4 == 0 else 0.6
        play(name, deg, t0 + k * step + rng.uniform(-0.18, 0.18) * step, vel * accent * (0.5 + 0.5 * u))
# the arrangement: every layer brings a NEW instrument
play("musicbox", 11, 0.5, 2.0, 0.0)            # card 1 (Stephen): a lone music box
play("piano", 7, 12.5, 2.0, 0.2)               # card 2 (Julia): a soft piano joins
# layer 1 (author 2026-11-24: remove the 100-note run, a natural and slow pace): a harp arpeggio of 11 notes over 6.4 s on the D chord, unhurried, played with a human, slightly uneven touch
for k, (deg, dt, v) in enumerate([(5, 0.0, 0.8), (7, 0.55, 0.65), (8, 1.1, 0.7), (10, 1.75, 0.85), (8, 2.4, 0.6), (7, 3.0, 0.6), (5, 3.6, 0.7), (8, 4.3, 0.65), (7, 5.0, 0.55), (5, 5.7, 0.6), (3, 6.4, 0.5)]):
    play("harp", deg, 20.0 + dt + rng.uniform(-0.03, 0.03), v * 3.6, -0.3 if k % 2 == 0 else 0.3)
# layer 2 (author 2026-11-24: ignore the 100-notes rule here, make it blend): a slow vibraphone phrase of 13 notes over the harp, in the same scale and chords, notes left to ring
for k, (deg, dt, v) in enumerate([(3, 0.0, 0.8), (5, 0.6, 0.7), (6, 1.2, 0.75), (8, 1.9, 0.85), (6, 2.6, 0.7), (5, 3.2, 0.65), (8, 3.9, 0.8), (10, 4.7, 0.9), (9, 5.5, 0.8), (8, 6.2, 0.7), (6, 7.0, 0.7), (5, 7.8, 0.6), (3, 8.6, 0.55)]):
    play("vibes", deg, 27.0 + dt, v * 1.2, -0.25 if k % 2 == 0 else 0.25)
lines("strings", 34.0, 100, 6.5, 0, 0, 0.9)    # layer 3: strings
lines("choir", 41.0, 100, 6.5, 0, 0, 0.85)     # layer 4: a choir
for b in range(4):                              # the far bands: a fine glockenspiel shimmer
    for k in range(14): place(glock(midi(scale_note(int(rng.integers(14, 21))))), 24.0 + b * 5 + rng.uniform(0, 4.0), rng.uniform(-0.9, 0.9), 0.2)
for t0, deg in [(47.0, 13), (49.0, 11), (51.0, 10), (53.5, 8), (56.0, 10), (58.5, 8), (61.0, 6)]:   # the quiet melody: piano with a flute above it
    play("piano", deg, t0, 0.6, -0.1); play("flute", deg + 5 if deg + 5 <= 17 else 17, t0 + 0.05, 0.35, 0.15)
def reverb(x):
    ir_n = int(2.8 * SR); t = np.arange(ir_n) / SR; ir = rng.standard_normal(ir_n) * np.exp(-t / 0.7); ir = lp(ir, 5000)
    ir[:int(0.012 * SR)] *= np.linspace(0, 1, int(0.012 * SR)); ir /= np.abs(ir).sum() * 0.35; return fftconvolve(x, ir)[:len(x)]
Lw = Lc * 0.7 + reverb(Lc) * 0.5 + pad * 0.7; Rw = Rc * 0.7 + reverb(Rc) * 0.5 + pad * 0.7
env = np.ones(N); i0 = int(63.0 * SR); env[i0:] = np.linspace(1, 0, N - i0) ** 1.5; Lw *= env; Rw *= env
pk = max(np.abs(Lw).max(), np.abs(Rw).max()); out = np.stack([Lw, Rw], axis=1) * (0.8 / pk)
wavfile.write(os.path.join(OUT, "cards_medley_instruments_v6_draft.wav"), SR, (out * 32767).astype(np.int16)); print("medley ok, scale", round(0.8 / pk, 2))
# single mono notes for the game, one folder per instrument
nd = os.path.join(OUT, "card_notes_v6"); os.makedirs(nd, exist_ok=True); cnt = 0
for name, (fn, degs, g) in INST.items():
    for k, d in enumerate(degs):
        y = render(name, d).copy(); y *= 0.85 / np.abs(y).max(); y[-int(0.05 * SR):] *= np.linspace(1, 0, int(0.05 * SR))
        wavfile.write(os.path.join(nd, "%s_%02d.wav" % (name, k + 1)), SR, (y * 32767).astype(np.int16)); cnt += 1
print(cnt, "single notes")
