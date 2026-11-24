import numpy as np, os
from scipy.io import wavfile
d = r"C:\Users\ashwi\AppData\Local\Temp\dl"; out = r"C:\Ashwin\Unity\Bystander\Assets\Resources\Audio"; SR = 48000
def go(n, name, peak):
    sr, x = wavfile.read(os.path.join(d, n + ".wav")); x = x / 32768.0; x = x.mean(axis=1) if x.ndim == 2 else x
    i = np.where(np.abs(x) > np.abs(x).max() * 0.04)[0]; x = x[max(0, i[0] - int(0.08 * SR)): i[-1] + int(0.15 * SR)].copy()
    x *= peak / np.abs(x).max(); a = int(0.01 * SR); b = int(0.1 * SR); x[:a] *= np.linspace(0, 1, a); x[-b:] *= np.linspace(1, 0, b)
    wavfile.write(os.path.join(out, name + ".wav"), SR, (x * 32767).astype(np.int16)); print(name, "%.2fs" % (len(x) / SR))
go("dont", "oldman_dont_bother", 0.85); go("oldthanks", "oldman_thanks", 0.7); go("lady", "woman_thanks", 0.8)
