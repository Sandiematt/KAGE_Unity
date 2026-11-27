"""Paints the RAGE COUNTERED combat callout (Resources/UI/Img/callout_rage_countered.png).

The other callouts are hand-brushed wordmarks; this one matches the red ones (PARRIED, GUARD BROKEN): a coral-to-blood gradient
fill with dry-brush streaks, a thin deep red rim, ragged brush edges, an italic lean, a tapered brush swoosh under the word and a
little ink splatter. Two staggered lines like GUARD BROKEN. Lettering is the project's own Permanent Marker (marker_400.ttf),
condensed and roughened so it reads as brushed rather than as a font.

    python Tools/callout_art/make_rage_countered.py

Deterministic (seeded), painted at 3x and filtered down.
"""
import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FONT = os.path.join(ROOT, "Assets", "Kage", "Resources", "UI", "Fonts", "marker_400.ttf")
OUT = os.path.join(ROOT, "Assets", "Kage", "Resources", "UI", "Img", "callout_rage_countered.png")

SS = 3                      # supersampling
W, H = 820 * SS, 260 * SS   # working canvas (cropped to the ink at the end)
LEAN = 0.24                 # italic shear
CONDENSE = 0.8              # letters drawn narrower than the font, like the brushed callouts

# the PARRIED palette (sampled from callout_parried.png)
TOP = np.array((255, 150, 126), float)
MID = np.array((238, 98, 84), float)
LOW = np.array((190, 30, 38), float)
RIM = np.array((150, 18, 28), float)
SWOOSH = np.array((200, 32, 40), float)
LIT = np.array((255, 196, 178), float)

rng = np.random.default_rng(4127)
prng = random.Random(4127)


def line_mask(text, size):
    f = ImageFont.truetype(FONT, size)
    l, t, r, b = f.getbbox(text)
    m = Image.new("L", (r + 20, b + 20), 0)
    ImageDraw.Draw(m).text((10 - l, 10 - t), text, font=f, fill=255)
    return m.resize((int(m.width * CONDENSE), m.height), Image.LANCZOS)


def text_mask():
    m = Image.new("L", (W, H), 0)
    m.paste(line_mask("RAGE", 100 * SS), (30 * SS, 8 * SS))
    m.paste(line_mask("COUNTERED", 112 * SS), (84 * SS, 104 * SS))
    return m


def shear(img):
    return img.transform(img.size, Image.AFFINE, (1, LEAN, -LEAN * H, 0, 1, 0), resample=Image.BICUBIC)


def stretched_noise(cell_x, cell_y, blur):
    """Smooth noise stretched along x (the brush's travel), 0..1."""
    sw, sh = W // cell_x + 2, H // cell_y + 2
    small = Image.fromarray((rng.random((sh, sw)) * 255).astype(np.uint8))
    big = small.resize((W, H), Image.BICUBIC).filter(ImageFilter.GaussianBlur(blur))
    return np.asarray(shear(big), float) / 255.0


def roughen(mask, amount=0.2):
    """Brush edges: the soft edge nudged in and out by noise stretched along the stroke, then cut back to hard."""
    soft = np.asarray(mask.filter(ImageFilter.GaussianBlur(1.4 * SS)), float) / 255.0
    n = stretched_noise(30 * SS, 4 * SS, 1.0 * SS) - 0.5
    return np.clip((soft + n * amount - 0.5) * 7.0 + 0.5, 0.0, 1.0)


def gradient(alpha):
    """Coral at the top of each line to blood red at its foot, by row."""
    rows = np.where(alpha.max(axis=1) > 0.5)[0]
    y0, y1 = rows[0], rows[-1]
    split = y0 + (y1 - y0) * 0.44
    y = np.arange(H, dtype=float)
    t = np.where(y < split, (y - y0) / max(1.0, split - y0), (y - split) / max(1.0, y1 - split))
    t = np.clip(t, 0.0, 1.0)[:, None]
    a = np.clip(t / 0.45, 0, 1)
    b = np.clip((t - 0.45) / 0.55, 0, 1)
    col = np.where(t < 0.45, TOP + (MID - TOP) * a, MID + (LOW - MID) * b)
    return np.repeat(col[:, None, :], W, axis=1).reshape(H, W, 3)


def swoosh(bbox):
    """A tapered brush stroke under COUNTERED with ragged edges, and a thinner echo under it."""
    layer = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(layer)
    x0, x1 = bbox[0] - 6 * SS, bbox[2] + 30 * SS
    base = bbox[3] + 2 * SS

    def stroke(xa, xb, y, thick, rise, seed):
        r = random.Random(seed)
        top, bot = [], []
        n = 120
        for i in range(n + 1):
            t = i / n
            x = xa + (xb - xa) * t
            cy = y - rise * t - 5 * SS * math.sin(t * math.pi)
            w = thick * (min(1.0, t / 0.25) ** 0.6) * ((1.0 - t) ** 0.5)
            w *= 0.8 + 0.4 * r.random()
            top.append((x, cy - w * 0.4))
            bot.append((x, cy + w * 0.6))
        d.polygon(top + bot[::-1], fill=255)

    stroke(x0, x1, base, 15 * SS, 16 * SS, 11)
    stroke(x0 + 90 * SS, x1 - 60 * SS, base + 12 * SS, 5 * SS, 10 * SS, 12)
    return roughen(layer, 0.12)


def splatter(bbox):
    layer = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(layer)
    spots = [(bbox[2] + 6 * SS, bbox[3] - 16 * SS, 34 * SS, 26 * SS, 6, 4.5),
             (bbox[0] + 24 * SS, bbox[3] + 10 * SS, 30 * SS, 8 * SS, 3, 2.5),
             (bbox[2] - 150 * SS, bbox[1] + 18 * SS, 40 * SS, 16 * SS, 3, 2.5)]
    for cx, cy, sx, sy, n, rmax in spots:
        for _ in range(n):
            x = cx + prng.uniform(-sx, sx)
            y = cy + prng.uniform(-sy, sy)
            r = prng.uniform(1.2, rmax) * SS
            d.ellipse((x - r * 1.3, y - r, x + r * 1.3, y + r), fill=255)
    return np.asarray(layer, float) / 255.0


def main():
    text_img = shear(text_mask())
    letters = roughen(text_img)
    bbox = Image.fromarray((letters * 255).astype(np.uint8)).getbbox()

    # the letters: gradient, dry-brush streaks (darker drags along the stroke), a faint lit top edge
    fill = gradient(letters)
    streak = np.clip((stretched_noise(90 * SS, 6 * SS, 1.5 * SS) - 0.5) * 2.2, 0, 1)[:, :, None]
    fill = fill + (LOW - fill) * streak * 0.32
    above = np.zeros_like(letters)
    above[4 * SS:, :] = letters[:-4 * SS, :]
    edge = np.clip(letters - above, 0, 1)
    edge = np.asarray(Image.fromarray((edge * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.9 * SS)), float)[:, :, None] / 255.0
    fill = fill + (LIT - fill) * edge * 0.3

    # the rim: a thin deep red border round the letters only
    rim_img = Image.fromarray((letters * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(2 * (2 * SS) + 1))
    rim = np.asarray(rim_img.filter(ImageFilter.GaussianBlur(0.5 * SS)), float) / 255.0

    ink = np.maximum(swoosh(bbox), splatter(bbox))

    rgb = np.zeros((H, W, 3))
    a = np.zeros((H, W))
    for col, cov in ((np.broadcast_to(SWOOSH, (H, W, 3)), ink), (np.broadcast_to(RIM, (H, W, 3)), rim), (fill, letters)):
        c = cov[:, :, None]
        rgb = rgb * (1 - c) + col * c
        a = a * (1 - cov) + cov
    out = Image.fromarray(np.dstack([np.clip(rgb, 0, 255), np.clip(a * 255, 0, 255)]).astype(np.uint8), "RGBA")

    box = out.getbbox()
    pad = 6 * SS
    out = out.crop((max(0, box[0] - pad), max(0, box[1] - pad), min(W, box[2] + pad), min(H, box[3] + pad)))
    out = out.resize((out.width // SS, out.height // SS), Image.LANCZOS)
    out.save(OUT)
    print(OUT, out.size)


if __name__ == "__main__":
    main()
