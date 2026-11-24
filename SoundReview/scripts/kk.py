import numpy as np, os, sys
from scipy.io import wavfile
from scipy.signal import butter, sosfilt, resample_poly
from kokoro_onnx import Kokoro
d = r"C:\Users\ashwi\AppData\Local\Temp\claude\d62b932dd5cff743\067fdf1e-250f-4f36-8c6d-c5a1ddfde654\scratchpad\kokoro"
k = Kokoro(os.path.join(d, "kokoro-v1.0.onnx"), os.path.join(d, "voices-v1.0.bin"))
print(sorted(k.get_voices())[:60])
out = r"C:\Ashwin\Unity\Bystander\SoundReview\dialogue_v2"; os.makedirs(out, exist_ok=True); SR = 48000
rng = np.random.default_rng(9)
def band(x, lo, hi): return sosfilt(butter(4, [lo, hi], btype="band", fs=SR, output="sos"), x)
def trim(x, thr=0.03, keep=0.07):
    i = np.where(np.abs(x) > np.abs(x).max() * thr)[0]; return x[max(0, i[0] - int(keep * SR)): i[-1] + int(keep * SR)]
def gen(voice, text, speed, name, peak=0.8, age=False):
    s, sr = k.create(text, voice=voice, speed=speed, lang="en-us")
    x = resample_poly(s, SR, sr) if sr != SR else s
    x = trim(x)
    if age:                                                          # only light touches: a little unsteadiness and a dry, close, band-limited sound; the voice itself stays natural
        t = np.arange(len(x)) / SR; pos = np.cumsum(1 + 0.004 * np.sin(2 * np.pi * 5.2 * t)); pos = pos[pos < len(x) - 1]; x = np.interp(pos, np.arange(len(x)), x)
    x = band(x, 80, 9000)
    x = x * (peak / np.abs(x).max()); n = int(0.01 * SR); x[:n] *= np.linspace(0, 1, n); m = int(0.12 * SR); x[-m:] *= np.linspace(1, 0, m)
    wavfile.write(os.path.join(out, name + ".wav"), SR, (x * 32767).astype(np.int16)); print(name, "%.2fs" % (len(x) / SR))
for v in ["am_onyx", "bm_george", "am_michael", "am_adam"]:
    gen(v, "Don't... bother.", 0.78, "oldman_dont_bother__" + v, age=True)
gen("bm_george", "Thank you.", 0.8, "oldman_thanks__bm_george", 0.6, age=True)
gen("am_onyx", "Thank you.", 0.8, "oldman_thanks__am_onyx", 0.6, age=True)
for v in ["af_nicole", "af_sarah", "af_heart"]:
    gen(v, "Thank you.", 0.82, "woman_thanks__" + v, 0.7)
