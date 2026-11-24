import numpy as np, os, glob
from scipy.io import wavfile
SR = 48000
src = r"C:\Ashwin\Unity\Bystander\Assets\Audio\SoundEffects"; dec = r"C:\Ashwin\Unity\Bystander\SoundReview\decoded"
out = r"C:\Ashwin\Unity\Bystander\Assets\Resources\Audio"; os.makedirs(out, exist_ok=True)

def load(path, mono=True):
    sr, x = wavfile.read(path)
    if x.dtype == np.int16: x = x / 32768.0
    elif x.dtype == np.int32: x = x / 2147483648.0
    else: x = x.astype(float)
    if mono and x.ndim == 2: x = x.mean(axis=1)
    assert sr == SR, (path, sr)
    return x
def save(name, x, peak=0.9):
    x = np.asarray(x, float); p = np.abs(x).max()
    if p > 0: x = x * (peak / p)
    wavfile.write(os.path.join(out, name + ".wav"), SR, (np.clip(x, -1, 1) * 32767).astype(np.int16))
    return len(x) / SR
def fade(x, a=0.005, b=0.03):
    x = x.copy(); n1 = int(a * SR); n2 = int(b * SR)
    if n1: x[:n1] *= np.linspace(0, 1, n1)
    if n2: x[-n2:] *= np.linspace(1, 0, n2)
    return x
def env(x, w=0.01):
    n = int(w * SR); k = len(x) // n
    return np.sqrt((x[:k * n].reshape(k, n) ** 2).mean(axis=1)), n
def loopify(x, xf):
    n = int(xf * SR); head = x[:n]; tail = x[-n:]
    t = np.linspace(0, np.pi / 2, n); body = x[n:-n].copy()
    mix = tail * np.cos(t) + head * np.sin(t)          # equal-power crossfade: the tail fades into the head so the loop point is invisible
    return np.concatenate([mix, body])
def trim(x, thr=0.02, keep=0.1):
    e, n = env(x); idx = np.where(e > e.max() * thr)[0]
    return x[max(0, int((idx[0] * n) - keep * SR)): int((idx[-1] + 1) * n + keep * SR)]
log = []

# 1. barking -> single barks
x = load(os.path.join(dec, "Barking.wav")); e, n = env(x, 0.01); thr = e.max() * 0.12
starts = []; i = 3
while i < len(e) - 3:
    if e[i] > thr and e[i] > 2.0 * e[i - 3]:
        starts.append(i); i += 12
    else: i += 1
bk = []
for j, s in enumerate(starts):
    a = max(0, s * n - int(0.03 * SR)); b = starts[j + 1] * n - int(0.02 * SR) if j + 1 < len(starts) else min(len(x), s * n + int(0.7 * SR))
    b = min(b, a + int(0.9 * SR))
    seg = x[a:b]
    if len(seg) > 0.12 * SR: bk.append(fade(seg, 0.004, 0.04))
bk = bk[:11]
for k, seg in enumerate(bk): save("dog_bark" if k == 0 else "dog_bark_%d" % (k + 1), seg, 0.9)
log.append("barks found %d, kept %d (%s s)" % (len(starts), len(bk), ", ".join("%.2f" % (len(s) / SR) for s in bk)))

# 2. oranges + bag
of = [load(os.path.join(src, "OrangeFall%d.wav" % i)) for i in range(1, 8)]
for k, o in enumerate(of): save("orange_roll" if k == 0 else "orange_roll_%d" % (k + 1), fade(o, 0.001, 0.02), 0.85)
def pitch(x, r): return np.interp(np.arange(0, len(x) - 1, r), np.arange(len(x)), x)
bag = pitch(of[4], 0.72); b2 = pitch(of[6], 0.85); L = max(len(bag), int(0.03 * SR) + len(b2)); z = np.zeros(L); z[:len(bag)] += bag; z[int(0.03 * SR):int(0.03 * SR) + len(b2)] += 0.7 * b2
save("bag_hit", fade(z, 0.001, 0.05), 0.9); log.append("orange_roll x7, bag_hit from OrangeFall5 (x0.72 slower) + OrangeFall7")

# 3. can kick
ck = load(os.path.join(dec, "CanKick.wav")); e, n = env(ck, 0.01)
log.append("CanKick %.2fs, activity per 0.1s: %s" % (len(ck) / SR, " ".join("%.2f" % (e[i:i + 10].max() / e.max()) for i in range(0, len(e), 10))))
save("can_kick", fade(ck, 0.002, 0.05), 0.9)

# 4. booms (spectacle): soft-limit the clipping, mono, level
for i in range(1, 8):
    f = load(os.path.join(src, "Firework%d.wav" % i)); f = np.tanh(f * 1.2) / np.tanh(1.2)
    save("boom" if i == 1 else "boom_%d" % i, fade(f, 0.002, 0.08), 0.85)
log.append("boom x7 from Firework1-7 (mono, tanh soft limited)")

# 5. scared woman
sw = load(os.path.join(src, "Scared Woman.wav")); save("woman_scream", fade(sw, 0.002, 0.2), 0.9); log.append("woman_scream %.1fs" % (len(sw) / SR))

# 6. chains rattle: trim silence, normalise, smooth loop
ch = load(os.path.join(src, "Chains rattling.wav")); ch = trim(ch, 0.03, 0.05); ch = loopify(ch, 1.0); d = save("chain_rattle", ch, 0.9); log.append("chain_rattle looped %.1fs" % d)

# 7. ambience loops
cr = load(os.path.join(dec, "Crowd talking.wav")); cr = loopify(cr, 1.5); d = save("street_crowd", cr, 0.8); log.append("street_crowd looped %.1fs mono" % d)
pn = load(os.path.join(dec, "CrowdScaredandScreaming.wav")); pn = loopify(pn, 3.0); d = save("street_panic", pn, 0.8); log.append("street_panic looped %.1fs mono" % d)

# 8. sirens: three distant variations from different parts
sr_ = load(os.path.join(dec, "Police siren.wav")); T = len(sr_) / SR; seg = int(6.5 * SR)
for k, a in enumerate([0.0, 5.5, 11.5]):
    s = sr_[int(a * SR): int(a * SR) + seg]; save("siren" if k == 0 else "siren_%d" % (k + 1), fade(s, 1.2, 1.5), 0.8)
log.append("siren x3 (6.5 s each, 1.2/1.5 s fades) from %.1fs siren" % T)

# 9. horns, groan
for k, nm in enumerate(["Carhorn", "carHorn (1)", "carhorn (2)", "carHorn (4)"]):
    h = trim(load(os.path.join(dec, nm + ".wav")), 0.02, 0.02); save("car_horn" if k == 0 else "car_horn_%d" % (k + 1), fade(h, 0.003, 0.08), 0.85)
g = trim(load(os.path.join(dec, "person in pain.wav")), 0.02, 0.1); save("groan", fade(g, 0.05, 0.4), 0.8); log.append("car_horn x4, groan %.1fs" % (len(g) / SR))
print("\n".join(log))
print(sorted(os.listdir(out)))
