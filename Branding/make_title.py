# renders the BYSTANDER title (Jost ExtraLight, wide spacing, BY and ER faded) as white-on-black and black-on-white PNGs
from PIL import Image, ImageDraw, ImageFont
import os
here = os.path.dirname(os.path.abspath(__file__))
font = ImageFont.truetype(os.path.join(here, "Jost-ExtraLight.ttf"), 600)
word, track = "BYSTANDER", 0.35 * 600
def render(name, bg, fg, fade):
    W, H = 6000, 1600
    img = Image.new("RGB", (W, H), bg); d = ImageDraw.Draw(img)
    widths = [font.getlength(c) for c in word]
    total = sum(widths) + track * (len(word) - 1)
    x = (W - total) / 2
    for i, c in enumerate(word):
        a = 0.3 if i in (0, 1, 7, 8) else 1.0
        col = tuple(int(bg[k] + (fg[k] - bg[k]) * a) for k in range(3))
        d.text((x, H / 2), c, font=font, fill=col, anchor="lm"); x += widths[i] + track
    img.save(os.path.join(here, name))
render("title_white_on_black.png", (0, 0, 0), (255, 255, 255), 0.3)
render("title_black_on_white.png", (255, 255, 255), (0, 0, 0), 0.3)
