# Synthesizes the thud of the old man hitting the floor (no voice): a low body thump (a sine dropping 95 to 42 Hz, fast decay), a short low-passed noise burst
# for the cloth and the floor, and a faint second, smaller settle. Writes Assets/Resources/Audio/oldman_fall.wav (mono 48 kHz 16 bit) and a copy in SoundReview.
import numpy as np, scipy.io.wavfile as wf, scipy.signal as ss
SR = 48000; rng = np.random.default_rng(11); n = int(0.7 * SR); t = np.arange(n) / SR
def thump(t0, a, f0=95., f1=42.):
    tt = np.clip(t - t0, 0, None); on = (t >= t0)
    f = f1 + (f0 - f1) * np.exp(-tt / 0.05); ph = 2 * np.pi * np.cumsum(f) / SR
    return a * on * np.sin(ph) * np.exp(-tt / 0.11) * np.minimum(1, tt / 0.004)
def burst(t0, a, fc):
    tt = np.clip(t - t0, 0, None); on = (t >= t0)
    x = ss.sosfilt(ss.butter(3, fc, "low", fs=SR, output="sos"), rng.standard_normal(n))
    return a * on * x * np.exp(-tt / 0.05) * np.minimum(1, tt / 0.002)
y = thump(0.0, 1.0) + burst(0.0, 1.6, 700) + burst(0.0, 0.5, 2500) * 0.4 + thump(0.17, 0.25, 70., 40.) + burst(0.17, 0.3, 500)
y = ss.sosfilt(ss.butter(2, 30, "high", fs=SR, output="sos"), y)
y *= 0.85 / np.abs(y).max()
y16 = (y * 32767).astype(np.int16)
wf.write("Assets/Resources/Audio/oldman_fall.wav", SR, y16); wf.write("SoundReview/oldman_fall_thud_draft.wav", SR, y16)
print("written", n / SR, "s, peak", np.abs(y).max(), "rms", np.sqrt((y ** 2).mean()))
