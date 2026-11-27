"""The 5-tier helmet designs (Common, Rare, Epic, Trophy, Legendary).
Head frame: u in head radii towards the face, v down, origin at the head centre.
Kage's head runs from u -0.65 (back) to 1.1 (nose), v -0.95 (crown) to 0.96 (chin);
his eye is u 0.30..0.60, v -0.12..0.21 and always stays visible.
Orders: 83 soft pieces behind the head, 85..95 on the head (over hair at 84, under eye at 96).
Kage's flowing crimson scarf (order 100..103) stays visible below every helmet.

5-Tier Roster:
  COMMON     Crimson Scarf (bare Kage), Kuro Zukin
  RARE       Sugegasa, Hachigane, Kasumi Veil
  EPIC       Tetsu Kabuto, Oni Menpo, Hoshi Kabuto
  TROPHY     Shika Kabuto, Sumi Eboshi, Hisame Zukin
  LEGENDARY  Raijin Kabuto, Utsuro Kabuto, Shikko Zukin
"""
import math
from kit import *

ALL = []


def helmet(hid, look, tier, hair, summary, eye='kept'):
    h = Helmet(hid, look, tier, hair, summary, eye=eye)
    ALL.append(h)
    return h


def tail(h, name, root, mid, end, w0, w1, tone, feel='Strip', order=83, w_mid=None, sd=3.0, hd=1.6, chains=1, glow=1.0):
    """A soft strip on spring chains (knot tails, ribbons, plumes, flames): trails and takes the wind in combat."""
    spine = curve(root[0], root[1], mid[0], mid[1], end[0], end[1])
    out = tine(spine, w0, w1, n=18, w_mid=w_mid)
    if chains == 1:
        ch = [sample(spine, 5)]
    else:
        ch = []
        for k in (-0.25, 0.25):
            off = [(p[0], p[1]) for p in sample(spine, 5)]
            a, b = off[0], off[1]
            d = norm((b[0] - a[0], b[1] - a[1]))
            n_ = (-d[1], d[0])
            ch.append([(p[0] + n_[0] * w0 * k, p[1] + n_[1] * w0 * k) for p in off])
    return h.cloth(name, order, out, tone, ch, feel, sd, hd, glow=glow)


def star(cu, cv, ro, ri, n=5, rot=-90.0):
    o = []
    for i in range(2 * n):
        a = math.radians(rot + i * 180.0 / n)
        r = ro if i % 2 == 0 else ri
        o.append((cu + math.cos(a) * r, cv + math.sin(a) * r))
    return o


def ring(cu, cv, ro, ri, a0=0.0, a1=360.0, n=40):
    """An annulus (or arc of one) as one simple polygon."""
    if abs(a1 - a0) >= 359.9:
        a0, a1 = a0 + 0.6, a0 + 359.4
    outer = [(cu + math.cos(math.radians(a0 + (a1 - a0) * i / n)) * ro, cv + math.sin(math.radians(a0 + (a1 - a0) * i / n)) * ro) for i in range(n + 1)]
    inner = [(cu + math.cos(math.radians(a1 - (a1 - a0) * i / n)) * ri, cv + math.sin(math.radians(a1 - (a1 - a0) * i / n)) * ri) for i in range(n + 1)]
    return outer + inner


def eye_layers(h, shape, halo_k=1.42, outer_k=1.18, core_k=0.58, halo_alpha=0.45, halo_glow=2.2, outer_glow=3.0, eye_glow=3.8, core_glow=4.2, with_core=True):
    """Builds concentric eye glow overlays matching the exact sharp pointed silhouette of the eye (no circular blobs)."""
    cu = sum(p[0] for p in shape) / len(shape)
    cv = sum(p[1] for p in shape) / len(shape)
    c = (cu, cv)
    h.flat('EyeHalo', 94, transform(shape, k=halo_k, about=c), 'glow', which=0, alpha=halo_alpha, glow=halo_glow)
    h.flat('EyeOuter', 95, transform(shape, k=outer_k, about=c), 'glow', which=0 if with_core else 1, glow=outer_glow)
    h.flat('Eye', 96, shape, 'glow', which=1 if with_core else 2, glow=eye_glow)
    if with_core:
        h.flat('EyeCore', 97, transform(shape, k=core_k, about=c), 'glow', which=2, glow=core_glow)


def emblem(h, name, cu, cv, r, ring_tone, face_tone, mark=None, mark_tone=None, order=93, glow=1.0, halo=None):
    if halo:
        h.flat(name + 'Halo', order - 1, disc(cu, cv, r * 1.55), halo[0], which=halo[1], alpha=halo[2], glow=halo[3])
    p = h.cel(name, order, disc(cu, cv, r), ring_tone, sd=2.4, hd=1.2, rim=False, glow=glow, seam=1.8)
    p.add(disc(cu, cv, r * 0.66), face_tone, 1)
    if mark is not None:
        p.add(mark, mark_tone or ring_tone, 2)
    return p


# ================================================================================================ COMMON (#9CA3AF)
# Crimson Scarf (shiro_maki) is bare Kage (spiky hair + glowing white eye + crimson scarf).
# Kuro Zukin: matte charcoal shinobi hood, crisp cloth folds, tied back knot + tails, crimson scarf exposed.

h = helmet('kuro_zukin', 'ronin_hood', 'common', 'hidden',
           'Matte charcoal shinobi hood wrapped cleanly to the jaw with a sharp eye slit, layered cloth folds and fluttering back tails.')
tail(h, 'TailA', (-1.06, -0.46), (-1.78, -0.78), (-2.55, -0.48), 0.24, 0.06, 'wrap', feel='Tails', w_mid=0.28)
tail(h, 'TailB', (-1.02, -0.32), (-1.68, -0.16), (-2.32, 0.20), 0.22, 0.05, 'wrap', feel='Tails', w_mid=0.25)
hood = h.cel('Hood', 86, rounded(
    (1.06, -0.21, 1), (0.12, -0.36, 1), (-0.18, -0.12), (0.04, 0.33, 1),
    (1.06, 0.25, 1), (0.92, 0.68), (0.48, 1.01, 1), (-0.32, 0.88),
    (-1.04, 0.53, 1), (-0.95, -0.11), (-1.07, -0.57, 1),
    (-0.68, -1.00), (0.06, -1.10), (0.75, -0.93), (0.98, -0.55)), 'cloth')
hood.add([(-0.97, 0.37), (-0.17, 0.61), (0.94, 0.36), (0.08, 0.75)], 'ink', 1)
hood.add([(-0.86, -0.61), (-0.45, -0.91), (0.40, -0.96), (-0.39, -0.80)], 'cloth', 2)
h.cel('Band', 88, rounded((-1.05, -0.52, 1), (-0.32, -0.70), (0.54, -0.60),
    (1.04, -0.33, 1), (0.14, -0.48), (-1.04, -0.35, 1)), 'wrap', rim=False)
h.cel('Knot', 89, [(-0.94, -0.51), (-1.16, -0.60), (-1.26, -0.42), (-1.08, -0.26)], 'wrap')

# ================================================================================================ RARE (#38BDF8)
# Woven straw Sugegasa, brushed steel Hachigane, armored steel Kasumi Veil.

# ------------------------------------------------------------------------------------------------ Sugegasa
h = helmet('sugegasa', 'straw_kasa', 'rare', 'hidden',
           'Wide conical woven golden-straw kasa with crisp radial ribs, a broad shadow facet, chin cord and hanging brim tassels.')
# Hanging straw tassel cords behind the head & down from the brim
tail(h, 'CordBack', (-1.55, -0.34), (-1.58, 0.25), (-1.15, 0.82), 0.075, 0.055, 'straw', feel='Tassel', order=83, w_mid=0.07)
tail(h, 'CordFront', (1.10, -0.24), (1.12, -0.12), (1.09, 0.02), 0.065, 0.03, 'straw', feel='Tassel', order=85, w_mid=0.06)
# Thin chin cord hugging the dark silhouette cheek & jaw
h.cel('ChinCord', 88, band(curve(-0.22, -0.44, -0.06, 0.42, 0.48, 0.94), 0.065), 'straw', sd=1.0, hd=0.6, rim=False, seam=1.2)
h.cel('UnderBrim', 89, rounded(
    (-2.32, -0.36, 1), (-1.15, -0.52), (0.0, -0.54), (1.15, -0.40), (2.28, -0.14, 1),
    (2.14, -0.04, 1), (0.95, -0.24), (-0.08, -0.34), (-1.15, -0.36), (-2.24, -0.24, 1)), 'straw_dark', sd=4.0, hd=1.0, rim=False, seam=1.8)
kasa = h.cel('Kasa', 91, rounded((-2.36, -0.36, 1), (-1.33, -0.74), (-0.58, -1.52, 1),
    (-0.30, -1.69, 1), (0.73, -1.05), (2.34, -0.12, 1), (2.18, -0.07, 1),
    (0.58, -0.35), (-0.75, -0.47), (-2.29, -0.26, 1)), 'straw')
kasa.add([(-2.13, -0.39), (-0.32, -1.60), (-0.86, -0.51)], 'straw', 0)
for x, y in ((-0.50, -0.53), (0.18, -0.49), (1.44, -0.29)):
    kasa.add([(-0.32, -1.54), (x,y), (x+0.08,y+0.01)], 'straw_dark', 2)
kasa.add([(-1.37, -0.72), (0.55, -0.58), (1.53, -0.39), (-1.47, -0.65)], 'straw_dark', 1)
h.cel('Apex', 93, [(-0.83, -1.25), (-0.53, -1.58), (-0.30, -1.70),
    (0.10, -1.43), (-0.35, -1.49)], 'straw_dark', rim=False)


# ------------------------------------------------------------------------------------------------ Hachigane
h = helmet('hachigane', 'storm_band', 'rare', 'kept',
           'Kage keeps his spiky hair over a slate ninja headband with a riveted brushed-steel brow plate, incised lightning slash and trailing knot tails.')
tail(h, 'TailA', (-1.12, -0.52), (-2.05, -0.88), (-2.95, -0.62), 0.25, 0.07, 'cloth', feel='Tails', order=83, w_mid=0.30)
tail(h, 'TailB', (-1.08, -0.36), (-1.92, -0.24), (-2.60, 0.14), 0.22, 0.06, 'cloth', feel='Tails', order=83, w_mid=0.26)
h.cel('Band', 86, rounded(
    (1.08, -0.62, 1), (0.20, -0.76), (-0.66, -0.68), (-1.04, -0.58, 1), (-1.06, -0.28, 1), (-0.60, -0.34), (0.20, -0.40), (1.08, -0.28, 1)),
    'cloth', sd=3.5, hd=1.6, seam=2.0)
h.cel('Knot', 87, rounded((-0.94, -0.64), (-1.26, -0.70), (-1.30, -0.28), (-0.98, -0.22)), 'cloth', sd=3.0, hd=1.4, seam=1.8)
plate = h.cel('Plate', 89, [(-0.46, -0.58), (0.41, -0.78), (0.91, -0.70),
    (1.16, -0.41), (1.06, -0.25), (0.42, -0.43), (-0.28, -0.37)], 'steel')
plate.add([(-0.30, -0.55), (0.43, -0.71), (0.94, -0.63), (0.40, -0.62)], 'steel', 2)
plate.add([(0.39, -0.54), (0.77, -0.63), (0.66, -0.54), (0.91, -0.52),
    (0.55, -0.44), (0.64, -0.52)], 'ink', 1)
plate.add(disc(-0.16, -0.49, 0.047), 'ink', 0)

# ------------------------------------------------------------------------------------------------ Kasumi Veil
h = helmet('kasumi_men', 'veil', 'rare', 'hidden',
           'Armored gunmetal-steel hood with hinged riveted cheek plate, layered iron-cloth lower face veil and sweeping back cloth.')
tail(h, 'BackDrape', (-0.96, 0.18), (-1.58, 0.48), (-2.18, 0.92), 0.34, 0.10, 'cloth', feel='Cape', order=83, w_mid=0.38)
dome = h.cel('Dome', 86, rounded(
    (1.12, -0.32, 1), (0.94, -0.84), (0.42, -1.24), (-0.18, -1.40, 1), (-0.76, -1.10), (-1.18, -0.44), (-1.46, 0.30, 1),
    (-1.02, 0.60), (-0.52, 0.64), (-0.16, 0.34), (0.14, -0.22, 1)), 'steel', sd=7.5, hd=3.0, seam=2.2)
dome.add(band(curve(-0.18, -1.34, -0.62, -0.86, -1.04, 0.08), 0.05), 'steel', 2)
dome.add(band(curve(-0.14, -1.28, 0.42, -0.96, 0.98, -0.42), 0.045), 'steel', 2)
dome.add(band(curve(-0.24, -0.52, 0.36, -0.54, 1.06, -0.40), 0.048), 'ink', 1)
# Brow plate above the eye slit
brow = h.cel('Brow', 88, rounded(
    (0.14, -0.44, 1), (0.68, -0.46), (1.16, -0.34, 1), (1.12, -0.18, 1), (0.66, -0.24), (0.16, -0.24, 1)), 'steel', sd=3.0, hd=1.8, seam=1.8)
brow.add(band([(0.24, -0.32), (0.72, -0.34), (1.08, -0.26)], 0.032), 'steel', 2)
# Layered lower face armor-veil mask wrapping nose & jaw
mask = h.cel('Mask', 90, rounded(
    (0.16, 0.24, 1), (0.68, 0.26), (1.18, 0.28, 1), (1.14, 0.56), (0.86, 0.96), (0.48, 1.18, 1), (0.04, 0.96), (-0.30, 0.58, 1), (0.0, 0.36)),
    'cloth', sd=5.5, hd=2.4, seam=2.0)
mask.add(band(curve(0.16, 0.46, 0.66, 0.50, 1.10, 0.42), 0.045), 'ink', 1)
mask.add(band(curve(0.06, 0.72, 0.56, 0.78, 0.96, 0.68), 0.045), 'ink', 1)
mask.add(band(curve(0.24, 0.32, 0.68, 0.34, 1.08, 0.32), 0.035), 'steel', 2)
# Hinged steel ear/cheek armor plate with rivets
cheek = h.cel('CheekPlate', 92, rounded((-0.76, -0.36, 1), (-0.30, -0.66),
    (0.18, -0.55, 1), (0.04, -0.08, 1), (-0.50, 0.50, 1), (-0.39, 0.05, 1),
    (-0.80, 0.21, 1)), 'steel')
cheek.add([(-0.66, -0.31), (0.09, -0.49), (-0.53, 0.29), (-0.17, -0.26)], 'steel', 0)
cheek.add(disc(-0.29, -0.42, 0.06), 'ink', 0)

# ================================================================================================ EPIC (#A855F7)
# Forged iron Tetsu Kabuto with twin horns & stern iron face mask, dark Oni Menpo with fierce iron horns & fanged snarl,
# Hoshi Kabuto with faceted studded bowl & lush sweeping arcane-violet horsehair plume.

# ------------------------------------------------------------------------------------------------ Tetsu Kabuto
h = helmet('tetsu_kabuto', 'iron_kabuto', 'epic', 'hidden',
           'Riveted forged-iron samurai kabuto with swept twin iron blades, brass brow stud, tiered iron shikoro and stern iron menpo mask.')
tail(h, 'Cord', (-1.05, -0.10), (-1.68, 0.16), (-2.18, 0.72), 0.14, 0.07, 'wrap', feel='Tassel', order=83, w_mid=0.15)
# Tiered riveted iron shikoro neck guard
shik = h.cel('Shikoro', 86, rounded(
    (-0.18, -0.56, 1), (-0.98, -0.46, 1), (-1.48, 0.08, 1), (-1.64, 0.66, 1), (-0.90, 0.80, 1), (-0.34, 0.38, 1), (-0.10, -0.04, 1)),
    'steel', sd=6.5, hd=2.6, seam=2.0)
for k in (0.26, 0.52, 0.78):
    shik.add(band([(-0.22 - 0.12 * k, -0.46 + 0.90 * k), (-0.96 - 0.18 * k, -0.36 + 0.86 * k), (-1.44 - 0.16 * k, 0.10 + 0.54 * k)], 0.048), 'iron_dark', 0)
    shik.add(band([(-0.24 - 0.12 * k, -0.40 + 0.90 * k), (-0.98 - 0.18 * k, -0.30 + 0.86 * k), (-1.44 - 0.16 * k, 0.16 + 0.54 * k)], 0.032), 'steel', 2)
# Far horn (behind bowl)
h.cel('HornFar', 85, tine(curve(0.46, -0.96, -0.37, -1.44, -0.70, -2.06), 0.22, 0.0, n=14), 'steel', sd=3.0, hd=1.8, rim=False, seam=1.8)
# Domed ribbed iron bowl
bowl = h.cel('Bowl', 87, rounded(
    (1.12, -0.36, 1), (0.98, -0.94), (0.36, -1.32), (-0.42, -1.28), (-1.04, -0.84), (-1.16, -0.34, 1), (0.0, -0.50)),
    'steel', sd=7.5, hd=3.2, seam=2.2)
for x in (-0.58, -0.14, 0.32, 0.72):
    bowl.add(band(curve(0.16, -1.24, x * 0.96, -0.92, x * 1.10, -0.48), 0.038), 'iron_dark', 0)
    bowl.add(band(curve(0.19, -1.24, x * 0.96 + 0.04, -0.92, x * 1.10 + 0.04, -0.48), 0.025), 'steel', 2)
# Brow rim & curved iron visor
h.cel('BrowRim', 88, band(curve(-1.14, -0.42, 0.0, -0.58, 1.10, -0.40), 0.13), 'iron_dark', sd=2.0, hd=1.2, rim=False, seam=1.8)
visor = h.cel('Visor', 89, rounded(
    (0.06, -0.52, 1), (0.92, -0.50, 1), (1.46, -0.28, 1), (1.36, -0.14, 1), (0.82, -0.26, 1), (0.08, -0.30, 1)),
    'steel', sd=3.5, hd=2.0, seam=2.0)
visor.add(band([(0.22, -0.34), (0.86, -0.34), (1.34, -0.20)], 0.035), 'steel', 2)
# Front crescent horn & gold/brass stud with subtle arcane purple glint
h.cel('HornNear', 91, tine(curve(0.92, -0.78, 0.34, -1.08, 0.16, -1.90), 0.24, 0.0, n=14), 'steel', sd=3.2, hd=2.0, seam=1.8)
h.cel('Stud', 92, disc(0.92, -0.68, 0.15), 'gold', sd=2.2, hd=1.2, rim=False, seam=1.6)
h.flat('ArcaneGem', 93, disc(0.92, -0.68, 0.075), 'glow', which=2, glow=2.4)
# Stern sculpted iron face mask (menpo)
menpo = h.cel('Menpo', 90, rounded(
    (0.18, 0.24, 1), (0.64, 0.28), (1.16, 0.30, 1), (1.10, 0.56, 1), (1.16, 0.70, 1), (0.96, 0.98, 1), (0.56, 1.10, 1), (0.10, 1.00, 1), (-0.22, 0.62, 1)),
    'steel', sd=5.5, hd=2.4, seam=2.0)
menpo.add(band([(0.28, 0.42), (0.68, 0.48), (1.02, 0.40)], 0.04), 'iron_dark', 0)
menpo.add(band([(0.54, 0.72), (0.82, 0.68), (1.04, 0.74)], 0.048), 'iron_dark', 0)
menpo.add(band([(0.34, 0.88), (0.68, 0.92), (0.92, 0.84)], 0.035), 'steel', 2)
# Ear guard (fukigaeshi) with arcane glow ring
fuki = h.cel('Fuki', 91, rounded((0.20, -0.48, 1), (0.34, -0.06, 1), (0.08, 0.30, 1), (-0.34, 0.24, 1), (-0.24, -0.42, 1)), 'iron_dark', sd=4.0, hd=1.8, seam=1.8)
h.flat('EarGlow', 92, ring(-0.04, -0.08, 0.14, 0.08), 'glow', which=1, glow=2.4)

# ------------------------------------------------------------------------------------------------ Oni Menpo
h = helmet('oni_menpo', 'oni_mask', 'epic', 'hidden',
           'Dark forged iron oni helm with twin curving demon horns, tiered iron neck plates, fierce arcane-lit oni eye and fanged face guard.',
           eye='replaced')
tail(h, 'Cord', (-1.02, -0.08), (-1.62, 0.18), (-2.12, 0.72), 0.14, 0.07, 'wrap', feel='Tassel', order=83, w_mid=0.15)
shik = h.cel('Shikoro', 86, rounded(
    (-0.20, -0.52, 1), (-0.98, -0.44, 1), (-1.42, 0.10, 1), (-1.52, 0.64, 1), (-0.84, 0.76, 1), (-0.30, 0.36, 1)),
    'iron_dark', sd=6.0, hd=2.4, seam=2.0)
for k in (0.28, 0.56, 0.82):
    shik.add(band([(-0.24 - 0.10 * k, -0.42 + 0.84 * k), (-0.94 - 0.16 * k, -0.34 + 0.80 * k), (-1.38 - 0.12 * k, 0.12 + 0.48 * k)], 0.045), 'shell', 0)
    shik.add(band([(-0.26 - 0.10 * k, -0.37 + 0.84 * k), (-0.96 - 0.16 * k, -0.29 + 0.80 * k), (-1.38 - 0.12 * k, 0.17 + 0.48 * k)], 0.028), 'steel', 1)
# Twin fierce curved oni horns
for i, (r0, c, t, w, o) in enumerate((
        ((0.06, -1.04), (-0.80, -1.46), (-0.94, -2.26), 0.42, 85),
        ((0.52, -0.94), (1.16, -1.26), (1.20, -2.04), 0.38, 91))):
    p = h.cel('Horn%d' % i, o, tine(curve(r0[0], r0[1], c[0], c[1], t[0], t[1]), w, 0.0, n=16), 'iron_dark', sd=4.5, hd=2.2, seam=2.0)
    p.add(band(curve(r0[0] + 0.05, r0[1] - 0.08, c[0] + 0.06, c[1], t[0] * 0.82 + r0[0] * 0.18, t[1] * 0.82 + r0[1] * 0.18), 0.036), 'plume', 2)
# Sculpted dark iron skull cap & angry brow ridge
cap = h.cel('Cap', 87, rounded(
    (1.16, -0.30, 1), (0.86, -0.38), (0.48, -0.24, 1), (0.14, -0.20, 1), (0.04, 0.24), (-0.34, 0.48, 1), (-0.86, 0.46),
    (-1.12, -0.14, 1), (-0.92, -0.84), (-0.18, -1.24), (0.64, -1.10), (1.08, -0.68)), 'iron_dark', sd=7.5, hd=3.0, seam=2.2)
cap.add(band([(1.06, -0.44), (0.42, -0.50), (-0.42, -0.38), (-1.02, -0.16)], 0.055), 'steel', 1)
cap.add(band([(0.24, -1.16), (0.14, -0.80), (0.16, -0.46)], 0.035), 'steel', 1)
# Sculpted fanged iron Oni face mask
menpo = h.cel('Menpo', 90, rounded(
    (0.18, 0.22, 1), (0.64, 0.28, 1), (0.90, 0.18, 1), (1.26, 0.32, 1), (1.14, 0.56, 1), (1.24, 0.72, 1), (1.02, 1.00, 1),
    (0.60, 1.14, 1), (0.08, 1.06, 1), (-0.30, 0.66, 1), (0.0, 0.44, 1)), 'iron_dark', sd=5.5, hd=2.6, seam=2.0)
menpo.add(band([(0.24, 0.36), (0.64, 0.44), (0.98, 0.32)], 0.042), 'steel', 2)
menpo.add(band([(0.12, 0.64), (0.42, 0.82), (0.56, 1.04)], 0.036), 'steel', 1)
# Snarling mouth opening with arcane purple energy & sharp fangs
h.flat('Mouth', 91, rounded((0.52, 0.68, 1), (1.14, 0.62, 1), (1.08, 0.84, 1), (0.56, 0.86, 1)), 'glow', which=1, glow=2.5)
for (fu, fv, fd) in ((0.68, 0.86, -1), (1.00, 0.82, -1)):
    h.flat('Fang', 92, [(fu - 0.048, fv), (fu + 0.048, fv), (fu, fv + 0.34 * fd)], 'steel', which=2)
h.flat('BrowGlow', 92, band([(0.22, -0.30), (0.64, -0.34), (1.06, -0.30)], 0.032), 'glow', which=1, glow=2.2)
# Custom fierce narrowed pointed arcane-violet Oni eye (exact matching pointed layers, no circles)
oni_eye_pts = rounded((0.22, -0.05, 1), (0.50, -0.04), (0.76, 0.05, 1), (0.52, 0.12), (0.34, 0.08))
eye_layers(h, oni_eye_pts, halo_k=1.38, outer_k=1.16, halo_alpha=0.42, halo_glow=2.0, outer_glow=2.8, eye_glow=3.5, with_core=False)

# ------------------------------------------------------------------------------------------------ Hoshi Kabuto
h = helmet('hoshi_kabuto', 'sentinel_helm', 'epic', 'hidden',
           'Studded obsidian-iron sentinel kabuto crowned with a sweeping arcane-violet horsehair plume, pointed visor and layered purple collar.')
# Lush sweeping arcane-violet plume (4 cel layers + flowing soft plume tip on chains!)
tail(h, 'PlumeTail', (-0.82, -1.18), (-1.75, -1.42), (-2.62, -0.96), 0.46, 0.04, 'plume', feel='Tails', order=83, w_mid=0.52)
for i, (r0, c, t, w) in enumerate((
        ((0.42, -1.08), (-0.68, -2.12), (-2.32, -2.18), 0.65),
        ((0.06, -1.06), (-1.12, -1.60), (-2.54, -1.65), 0.48),
        ((-0.44, -0.84), (-1.42, -1.12), (-2.42, -0.44), 0.44))):
    pl = h.cel('Plume%d' % i, 85, tine(curve(r0[0], r0[1], c[0], c[1], t[0], t[1]), w, 0.0, n=18, w_mid=w * 0.96), 'plume', sd=5.5, hd=2.6, seam=2.0)
    pl.add(band(curve(r0[0] - 0.06, r0[1] - 0.04, c[0], c[1] + 0.05, t[0] * 0.8 + r0[0] * 0.2, t[1] * 0.8 + r0[1] * 0.2), 0.042), 'glow', 2)
# Layered purple collar folds tucked neatly on the head/neck behind & above the crimson scarf
collar = h.cel('Collar', 86, rounded(
    (-0.24, 0.46, 1), (0.36, 0.72), (0.56, 0.94, 1), (0.04, 1.06), (-0.78, 0.94), (-1.46, 0.56, 1), (-1.28, -0.24), (-0.64, -0.46)),
    'wrap', sd=5.5, hd=2.4, seam=2.0)
for k in (0.25, 0.55, 0.82):
    collar.add(band(curve(-1.32 + 0.16 * k, 0.10 + 0.50 * k, -0.66 + 0.20 * k, 0.34 + 0.48 * k, 0.34 * k, 0.54 + 0.42 * k), 0.048), 'shell', 0)
# Faceted studded obsidian-iron bowl
bowl = h.cel('Bowl', 88, rounded(
    (1.10, -0.34, 1), (0.94, -0.96), (0.30, -1.32), (-0.48, -1.24), (-1.10, -0.76), (-1.18, -0.30, 1), (0.08, -0.46)),
    'shell', sd=7.5, hd=2.8, seam=2.2)
for x in (-0.54, -0.06, 0.40):
    bowl.add(band(curve(0.10, -1.26, x, -0.90, x * 1.08, -0.46), 0.036), 'steel', 1)
# Raised glowing arcane star rivets (hoshi)
for (su, sv) in ((0.68, -0.92), (0.64, -0.60), (0.02, -1.06), (-0.02, -0.72), (-0.70, -0.88), (-0.76, -0.54)):
    h.flat('Hoshi', 90, star(su, sv, 0.085, 0.038), 'glow', which=2, glow=2.5)
# Sweeping visor + glowing brow line
h.cel('Visor', 89, rounded(
    (0.02, -0.50, 1), (0.92, -0.50, 1), (1.58, -0.34, 1), (1.46, -0.20, 1), (0.82, -0.28, 1), (0.02, -0.30, 1)), 'iron_dark', sd=3.2, hd=1.8, seam=2.0)
h.flat('VisorGlow', 90, band([(0.20, -0.36), (0.88, -0.36), (1.46, -0.26)], 0.036), 'glow', which=1, glow=2.4)
# Ornate ear plate
h.cel('Fuki', 91, rounded((0.24, -0.48, 1), (0.36, -0.08, 1), (0.10, 0.28, 1), (-0.30, 0.20, 1), (-0.22, -0.42, 1)), 'shell', sd=4.0, hd=1.8, seam=1.8)
h.flat('EarGlow', 92, ring(0.02, -0.08, 0.14, 0.08), 'glow', which=1, glow=2.4)


# ================================================================================================ TROPHY (#EAB308)
# Ceremonial ivory-white enamel, warm imperial gold, branching stag antlers, filigree scrolls & white ribbons.

# ------------------------------------------------------------------------------------------------ Shika Kabuto
h = helmet('shika_kabuto', 'sage_antlers', 'trophy', 'hidden',
           'Ceremonial white-enamel peaked kabuto with tall branching golden-bronze stag antlers, red-and-gold laced shikoro and ivory ribbons.')
tail(h, 'Ribbon', (-1.04, -0.62), (-1.86, -0.82), (-2.72, -0.48), 0.24, 0.16, 'ribbon', feel='Tails', order=83, w_mid=0.26)
# Red-and-gold laced tiered shikoro at the back
shik = h.cel('Shikoro', 86, rounded(
    (-0.18, -0.50, 1), (-0.98, -0.42, 1), (-1.54, 0.14, 1), (-1.78, 0.72, 1), (-0.86, 0.88, 1), (-0.28, 0.42, 1), (-0.08, -0.04, 1)),
    'crimson', sd=6.0, hd=2.4, seam=2.0)
for k in (0.24, 0.50, 0.76):
    shik.add(band([(-0.22 - 0.10 * k, -0.40 + 0.90 * k), (-0.96 - 0.20 * k, -0.32 + 0.88 * k), (-1.52 - 0.22 * k, 0.16 + 0.54 * k)], 0.052), 'gold', 2)
    shik.add(band([(-0.24 - 0.10 * k, -0.34 + 0.90 * k), (-0.98 - 0.20 * k, -0.26 + 0.88 * k), (-1.52 - 0.22 * k, 0.22 + 0.54 * k)], 0.036), 'ink', 0)
# Crisp ivory cloth collar fold resting cleanly over the crimson scarf
neck = h.cel('NeckFold', 87, rounded(
    (0.06, 0.52, 1), (0.64, 0.78), (0.86, 0.98, 1), (0.28, 1.12), (-0.58, 0.96), (-1.18, 0.62, 1), (-0.72, 0.44)),
    'wrap', sd=4.5, hd=2.0, seam=1.8)
neck.add(band(curve(-0.98, 0.66, -0.24, 0.86, 0.68, 0.92), 0.042), 'enamel', 0)
# Towering branching golden-bronze stag antlers (breaking high above the top frame!)
for side, (ox, k, o) in enumerate(((-0.46, 0.88, 85), (0.14, 0.98, 92))):
    def T(*q):
        return [(ox + q[i] * k + (q[i+1] + 1.14) * (0.54 if side == 0 else 0.18), -1.14 + (q[i + 1] + 1.14) * k) for i in range(0, len(q), 2)]
    h.cel('Antler%d' % side, o, tine(T(0.18, -1.14, 0.40, -1.92, 0.24, -2.64, 0.58, -3.16), 0.24 * k, 0.05, n=16), 'antler', sd=3.2, hd=1.8, rim=side == 1, seam=1.8)
    for j, q in enumerate((
            (0.36, -1.82, 0.86, -2.14, 1.14, -1.98),
            (0.28, -2.38, -0.10, -2.76, -0.24, -3.06),
            (0.42, -2.82, 0.84, -3.04, 0.96, -3.22))):
        h.cel('Antler%dT%d' % (side, j), o, tine(T(*q), 0.14 * k, 0.0, n=12), 'antler', sd=2.2, hd=1.2, rim=side == 1, seam=1.5)
# Tall peaked ivory-white enamel kabuto crown with dark lacquer trim stripes
bowl = h.cel('Crown', 88, rounded(
    (1.10, -0.34, 1), (1.02, -1.04), (0.53, -1.42), (-0.03, -1.64, 1), (-0.60, -1.44), (-0.88, -1.08), (-1.12, -0.34, 1), (0.04, -0.52)),
    'enamel', sd=8.0, hd=3.0, seam=2.2)
bowl.add(band(curve(-0.88, -1.02, 0.04, -1.24, 0.96, -0.96), 0.075), 'ink', 0)
bowl.add([(-0.03, -1.60), (0.11, -1.38), (0.42, -0.61), (0.30, -0.60)], 'enamel', 0)

# Gleaming imperial gold brow band, visor & crest stud
h.cel('GoldBand', 89, band(curve(-1.12, -0.44, 0.04, -0.64, 1.10, -0.42), 0.16), 'gold', sd=2.4, hd=1.4, rim=False, seam=1.8)
h.cel('Visor', 90, rounded((0.18, -0.54, 1), (1.02, -0.50, 1), (1.46, -0.32, 1), (1.34, -0.18, 1), (0.86, -0.28, 1), (0.18, -0.32, 1)), 'gold', sd=3.2, hd=1.8, seam=1.8)
emblem(h, 'Crest', 0.02, -0.06, 0.19, 'gold', 'crimson', mark=star(0.02, -0.06, 0.10, 0.045, 4, -90), mark_tone='gold', order=93)

# ------------------------------------------------------------------------------------------------ Sumi Eboshi
h = helmet('sumi_eboshi', 'sumi_eboshi', 'trophy', 'hidden',
           'Tall ceremonial ivory-lacquer court cap swept high, slashed with a bold sumi-ink brushstroke, imperial gold brow band and long white ribbons.')
tail(h, 'RibbonA', (-0.98, -1.16), (-1.82, -1.52), (-2.80, -1.18), 0.24, 0.18, 'ribbon', feel='Tails', order=83, w_mid=0.26)
tail(h, 'RibbonB', (-0.98, -0.84), (-1.72, -0.74), (-2.52, -0.24), 0.20, 0.15, 'ribbon', feel='Tails', order=83, w_mid=0.22)
eb = h.cel('Cap', 87, rounded(
    (1.06, -0.34, 1), (0.96, -1.22), (0.48, -2.04), (-0.42, -2.59), (-1.65, -2.91, 1), (-1.34, -2.32, 1), (-1.55, -2.40, 1), (-1.12, -1.50), (-1.06, -0.78),
    (-1.04, -0.24, 1), (0.0, -0.50)), 'enamel', sd=8.5, hd=3.2, seam=2.2)
eb.add(band(curve(0.10, -0.60, 0.10, -1.72, -0.70, -2.64), 0.042), 'enamel', 0)
eb.add(band(curve(-0.50, -0.52, -0.70, -1.52, -0.98, -2.52), 0.042), 'enamel', 0)
stroke = tine(curve(-1.02, -1.32, -0.10, -2.00, 0.86, -1.30), 0.16, 0.02, n=16, w_mid=0.26)
eb.add(stroke, 'ink', 0)
eb.add(band(curve(-0.84, -1.38, -0.10, -1.82, 0.56, -1.44), 0.032), 'ink', 2)
h.cel('GoldBand', 88, band(curve(-1.04, -0.36, 0.0, -0.64, 1.06, -0.40), 0.18), 'gold', sd=2.4, hd=1.4, rim=False, seam=1.8)
h.cel('GoldPeak', 89, band(curve(-1.53, -2.83, -0.93, -2.64, -0.30, -2.42), 0.06), 'gold', sd=1.0, hd=0.6, rim=False, seam=1.4)
emblem(h, 'Medal', -0.08, -0.22, 0.20, 'gold', 'ink', mark=star(-0.08, -0.22, 0.11, 0.048), mark_tone='gold', order=92)
h.cel('ChinCord', 86, band(curve(-0.08, -0.08, -0.04, 0.58, 0.44, 0.94), 0.065), 'gold', sd=1.2, hd=0.6, rim=False, seam=1.4)

# ------------------------------------------------------------------------------------------------ Hisame Zukin
h = helmet('hisame_zukin', 'rain_hood', 'trophy', 'hidden',
           'Ivory-white ceremonial cloth hood adorned with gold rain embroidery, draped white neck folds and long fluttering white ribbons.')
tail(h, 'RibbonA', (-0.88, -0.54), (-1.82, -0.96), (-2.88, -0.58), 0.30, 0.20, 'ribbon', feel='Tails', order=83, w_mid=0.32)
tail(h, 'RibbonB', (-0.86, -0.30), (-1.70, -0.12), (-2.56, 0.44), 0.26, 0.18, 'ribbon', feel='Tails', order=83, w_mid=0.28)
# Sculpted white cloth hood framing the black silhouette face & glowing white eye
hood = h.cel('Hood', 87, rounded(
    (1.16, -0.38, 1), (0.86, -0.34), (0.44, -0.26, 1), (0.20, 0.06), (0.26, 0.50), (0.58, 0.78), (0.94, 0.94, 1),
    (0.46, 1.18), (-0.34, 1.06), (-0.98, 0.56), (-1.16, -0.24), (-1.43, -0.93, 1), (-0.55, -0.99), (0.04, -1.32, 1), (0.88, -1.00), (1.14, -0.66)),
    'enamel', sd=8.0, hd=3.0, seam=2.2)
# Soft ivory cloth fold shadows
hood.add(band(curve(0.18, -1.24, -0.54, -0.92, -0.96, -0.12), 0.052), 'enamel', 0)
hood.add(band(curve(-0.88, 0.34, -0.28, 0.76, 0.62, 0.96), 0.055), 'enamel', 0)
hood.add(band(curve(-0.62, 0.68, -0.06, 0.98, 0.58, 1.08), 0.048), 'enamel', 0)
# Gold filigree trim along hood brow, face opening
hood.add(band(curve(1.12, -0.44, 0.64, -0.38, 0.36, -0.24), 0.072), 'gold', 0)
hood.add(band(curve(1.10, -0.44, 0.64, -0.38, 0.38, -0.25), 0.042), 'gold', 1)
hood.add(band(curve(0.32, -0.20, 0.16, 0.16, 0.32, 0.58), 0.068), 'gold', 0)
hood.add(band(curve(0.32, -0.18, 0.18, 0.16, 0.32, 0.56), 0.038), 'gold', 1)
# Rain embroidery: three broken, tapering strokes follow the drape.
for u, v in ((-0.67, -0.46), (-0.46, -0.37), (-0.26, -0.30)):
    hood.add([(u,v), (u+0.08,v+0.14), (u-0.10,v+0.55)], 'gold', 1)

# Sculpted draped white cloth collar fold above crimson scarf
cowl_fold = h.cel('NeckFold', 88, rounded(
    (-0.84, 0.42, 1), (-0.12, 0.64), (0.76, 0.78, 1), (0.98, 1.02, 1), (0.36, 1.22), (-0.54, 1.12), (-1.12, 0.72, 1)),
    'wrap', sd=5.0, hd=2.2, seam=2.0)
cowl_fold.add(band(curve(-0.92, 0.66, -0.16, 0.90, 0.80, 0.98), 0.048), 'enamel', 0)
# Gold brow finial ornament
h.cel('BrowFinial', 91, rounded((0.76, -0.98), (1.16, -0.84, 1), (1.06, -0.54), (0.72, -0.68)), 'gold', sd=2.4, hd=1.4, rim=False, seam=1.8)
h.flat('BrowGem', 92, disc(0.92, -0.76, 0.075), 'glow', which=2, glow=1.6)


# ================================================================================================ LEGENDARY (#F97316 / #EF4444)
# Blazing crimson-gold demonic war helms with fiery horns, roaring flames, golden filigree & glowing amber-red eyes.

def raijin_eye(h):
    """Fierce pointed blazing crimson-gold demon eye for Raijin Kabuto (all overlays match exact pointed eye silhouette)."""
    pts = rounded((0.18, -0.09, 1), (0.48, -0.05), (0.80, 0.05, 1), (0.58, 0.15), (0.34, 0.11))
    eye_layers(h, pts, halo_k=1.42, outer_k=1.18, core_k=0.58, halo_alpha=0.48, halo_glow=2.4, outer_glow=3.2, eye_glow=3.8, core_glow=4.2)


def utsuro_eye(h):
    """Sharp pointed hollow blazing crimson-orange demon eye for Utsuro Kabuto (all overlays match exact pointed eye silhouette)."""
    pts = rounded((0.20, -0.06, 1), (0.50, -0.07), (0.78, 0.04, 1), (0.56, 0.15), (0.32, 0.11))
    eye_layers(h, pts, halo_k=1.42, outer_k=1.18, core_k=0.56, halo_alpha=0.48, halo_glow=2.4, outer_glow=3.2, eye_glow=3.8, core_glow=4.2)


def shikko_eye(h):
    """Razor-thin pointed executioner crimson-gold slash eye for Shikko Zukin (all overlays match exact pointed eye silhouette)."""
    pts = rounded((0.14, -0.11, 1), (0.48, -0.03), (0.80, 0.06, 1), (0.56, 0.12), (0.34, 0.04))
    eye_layers(h, pts, halo_k=1.40, outer_k=1.18, core_k=0.56, halo_alpha=0.45, halo_glow=2.2, outer_glow=3.0, eye_glow=3.8, core_glow=4.2)


def forged_crest(h, name, order, outline, bevel, groove, metal):
    plate = h.cel(name, order, outline, metal, sd=4, hd=2, seam=1.7)
    plate.add(bevel, 'gold', 1)
    plate.add(groove, 'fire', 2)

# ------------------------------------------------------------------------------------------------ Raijin Kabuto
h = helmet('raijin_kabuto', 'raijin_kabuto', 'legendary', 'hidden',
           'Blazing demonic war kabuto with twin forked thunder horns, inset ember grooves, fanged demon jaw and blazing crimson-gold demon eye.',
           eye='replaced')
# Flowing flame ribbons behind
tail(h, 'FlameTailA', (-0.98, -0.56), (-1.92, -0.96), (-2.88, -0.54), 0.34, 0.02, 'fire', feel='Tails', order=83, w_mid=0.38, glow=1.9)
tail(h, 'FlameTailB', (-0.94, -0.18), (-1.78, 0.06), (-2.52, 0.58), 0.28, 0.02, 'fire', feel='Tails', order=83, w_mid=0.32, glow=1.7)
# Two lightning-shaped horn blades share the bowl; open sky separates the tips.
forged_crest(h, 'ThunderFar', 85,
    [(-0.19, -0.92), (-0.86, -1.44), (-1.08, -1.98), (-0.91, -1.89),
     (-1.24, -3.15), (-0.64, -2.22), (-0.48, -2.24), (-0.38, -1.65), (0.12, -1.09)],
    [(-1.18, -3.00), (-0.72, -2.18), (-0.86, -2.12)],
    [(-0.83, -1.84), (-0.44, -1.49), (-0.29, -1.14)], 'horn')

# Tiered crimson & gold laced shikoro
shik = h.cel('Shikoro', 86, rounded(
    (-0.14, -0.54, 1), (-0.96, -0.46, 1), (-1.46, 0.04, 1), (-1.68, 0.64, 1), (-0.92, 0.84, 1), (-0.28, 0.44, 1), (-0.12, -0.02, 1)),
    'crimson', sd=6.0, hd=2.4, seam=2.0)
for k in (0.26, 0.54, 0.80):
    shik.add(band([(-0.18 - 0.10 * k, -0.44 + 0.88 * k), (-0.94 - 0.18 * k, -0.36 + 0.84 * k), (-1.44 - 0.18 * k, 0.08 + 0.52 * k)], 0.055), 'gold', 2)
forged_crest(h, 'ThunderNear', 92,
    [(0.42, -0.74), (0.14, -1.44), (0.35, -2.06), (0.49, -1.91),
     (0.89, -3.15), (0.86, -2.19), (1.02, -2.38), (0.84, -1.63), (0.71, -0.83)],
    [(0.48, -1.36), (0.64, -1.79), (0.82, -2.74), (0.77, -1.73)],
    [(0.50, -0.92), (0.42, -1.38), (0.58, -1.72)], 'horn')

# Lacquered demonic bowl + gold ribs
bowl = h.cel('Bowl', 87, rounded(
    (1.16, -0.34, 1), (1.00, -0.94), (0.46, -1.30), (-0.28, -1.32), (-0.90, -0.98), (-1.16, -0.36, 1), (0.02, -0.50)),
    'shell', sd=7.5, hd=3.0, seam=2.2)
bowl.add(band([(0.48, -1.24), (0.28, -0.82), (0.30, -0.46)], 0.042), 'gold', 1)
bowl.add(band([(-0.24, -1.26), (-0.34, -0.82), (-0.42, -0.44)], 0.042), 'gold', 1)


h.cel('Visor', 89, rounded((0.12, -0.52, 1), (1.02, -0.50, 1), (1.60, -0.26, 1), (1.42, -0.16, 1), (0.88, -0.26, 1), (0.12, -0.30, 1)), 'crimson', sd=3.2, hd=1.6, seam=2.0)
h.cel('VisorTrim', 90, band([(0.18, -0.32), (0.88, -0.30), (1.48, -0.20)], 0.055), 'gold', sd=1.0, hd=0.6, rim=False, glow=1.4, seam=1.4)
# Snarling gilded demon jaw guard (menpo) with glowing molten fangs
jaw = h.cel('Jaw', 90, rounded(
    (0.18, 0.28, 1), (0.72, 0.30, 1), (1.18, 0.34, 1), (1.08, 0.54, 1), (1.22, 0.68, 1), (1.00, 1.02, 1), (0.50, 1.14, 1), (0.0, 0.98, 1), (-0.30, 0.58, 1)),
    'crimson', sd=5.5, hd=2.4, seam=2.0)
jaw.add(band([(0.24, 0.38), (0.72, 0.44), (1.08, 0.38)], 0.048), 'gold', 2)
h.flat('Fangs', 91, [(0.50, 0.64), (0.60, 0.86), (0.70, 0.64), (0.82, 0.88), (0.94, 0.64), (1.04, 0.82), (1.12, 0.64), (1.00, 0.58), (0.54, 0.58)], 'glow', which=2, glow=2.6)
raijin_eye(h)

# ------------------------------------------------------------------------------------------------ Utsuro Kabuto
h = helmet('utsuro_kabuto', 'hollow_crest', 'legendary', 'hidden',
           'Obsidian demonic war helm with a swept, split-vane crest, magma cracks and hollow fiery eye.',
           eye='replaced')
tail(h, 'FlameTail', (-1.04, -0.38), (-1.94, -0.62), (-2.84, -0.16), 0.32, 0.02, 'fire', feel='Tails', order=83, w_mid=0.36, glow=1.8)
# Overlapping swept vanes form the hollow crest with a deep open notch.
forged_crest(h, 'VaneFar', 84,
    [(-0.89, -0.71), (-1.48, -1.03), (-2.50, -2.02), (-1.56, -1.64),
     (-1.82, -2.08), (-0.57, -1.47), (0.09, -0.90)],
    [(-2.32, -1.92), (-1.23, -1.44), (-0.77, -1.05)],
    [(-1.59, -1.85), (-0.79, -1.42), (-0.60, -1.21)], 'crimson')
forged_crest(h, 'VaneNear', 85,
    [(0.64, -0.80), (-0.22, -1.12), (-1.03, -1.89), (-1.80, -2.80),
     (-0.79, -2.19), (-0.94, -2.51), (-0.23, -2.03), (0.34, -1.65)],
    [(-1.64, -2.64), (-0.42, -1.83), (0.45, -1.03), (-0.59, -1.83)],
    [(-0.79, -2.34), (-0.25, -1.97), (0.05, -1.60)], 'shell')

shards = h.cel('Shards', 86, rounded(
    (-0.18, -0.52, 1), (-1.00, -0.40, 1), (-1.52, 0.18, 1), (-1.30, 0.30, 1), (-1.70, 0.70, 1), (-1.20, 0.66, 1), (-1.10, 0.90, 1),
    (-0.70, 0.62, 1), (-0.50, 0.86, 1), (-0.28, 0.40, 1)), 'crimson', sd=5.5, hd=2.2, seam=2.0)
shards.add(band([(-0.42, -0.24), (-1.04, -0.14), (-1.42, 0.24)], 0.045), 'gold', 1)
helm = h.cel('Helm', 87, rounded(
    (1.20, -0.30, 1), (0.90, -0.36, 1), (0.58, -0.20, 1), (0.24, -0.26, 1), (0.14, 0.20, 1), (0.30, 0.50, 1), (0.92, 0.56, 1),
    (1.14, 0.74, 1), (0.72, 1.02, 1), (0.10, 1.04, 1), (-0.60, 0.70, 1), (-1.08, 0.0, 1), (-0.98, -0.82, 1), (-0.30, -1.24, 1),
    (0.52, -1.18, 1), (1.02, -0.74, 1)), 'shell', sd=7.5, hd=2.8, seam=2.2)
helm.add(band([(0.44, -1.06), (0.02, -0.68), (-0.70, -0.52)], 0.048), 'gold', 1)
helm.add(band([(0.30, 0.50), (0.0, 0.62), (-0.52, 0.60)], 0.042), 'gold', 1)
h.flat('Crack', 88, band([(0.92, -0.66), (0.52, -0.60), (0.30, -0.82), (-0.22, -0.84)], 0.04), 'glow', which=2, glow=2.8)
h.flat('Teeth', 88, [(0.36, 0.56), (0.50, 0.76), (0.60, 0.58), (0.72, 0.80), (0.84, 0.60), (0.98, 0.78), (1.08, 0.70), (0.90, 0.56)], 'glow', which=2, glow=2.5)
utsuro_eye(h)

# ------------------------------------------------------------------------------------------------ Shikko Zukin
h = helmet('shikko_zukin', 'executioner', 'legendary', 'hidden',
           'Tall curved obsidian-crimson executioner kabuto with a single molten-gold blade crest, razor-thin fiery eye and crimson cords.',
           eye='replaced')
tail(h, 'CordA', (-0.92, -0.18), (-1.48, 0.20), (-1.66, 0.92), 0.12, 0.22, 'crimson', feel='Tassel', order=83, w_mid=0.14)
tail(h, 'FlameCord', (-1.00, -1.58), (-1.82, -1.68), (-2.56, -1.02), 0.22, 0.05, 'fire', feel='Tails', order=83, w_mid=0.24, glow=1.8)


shik = h.cel('Shikoro', 86, rounded((-0.12, -0.50, 1), (-1.00, -0.40, 1), (-1.54, 0.52, 1), (-0.80, 0.82, 1), (-0.28, 0.40, 1)), 'shell', sd=5.5, hd=2.2, seam=2.0)
shik.add(band([(-0.30, -0.10), (-1.00, 0.0), (-1.34, 0.42)], 0.055), 'crimson', 2)
shik.add(band([(-0.28, -0.26), (-0.98, -0.18), (-1.38, 0.22)], 0.042), 'gold', 1)
zk = h.cel('Zukin', 87, rounded(
    (1.12, -0.36, 1), (1.02, -0.94), (0.62, -1.58), (0.02, -2.20), (-0.64, -2.42, 1), (-1.14, -2.08, 1), (-0.72, -1.84),
    (-0.92, -1.32), (-1.16, -0.36, 1), (0.0, -0.50)), 'shell', sd=8.5, hd=3.0, seam=2.2)
zk.add(band(curve(0.62, -0.52, 0.42, -1.44, -0.52, -2.20), 0.048), 'gold', 2)
zk.add(band(curve(-0.40, -0.50, -0.70, -1.24, -0.92, -1.86), 0.042), 'crimson', 2)
h.cel('Koshimaki', 88, band(curve(-1.16, -0.44, 0.0, -0.64, 1.12, -0.42), 0.14), 'gold', sd=2.0, hd=1.0, rim=False, seam=1.8)
h.cel('Visor', 89, rounded((0.20, -0.52, 1), (1.02, -0.50, 1), (1.54, -0.34, 1), (1.42, -0.18, 1), (0.90, -0.28, 1), (0.20, -0.30, 1)), 'shell', sd=3.2, hd=1.6, seam=2.0)
h.cel('VisorEdge', 90, band([(0.24, -0.32), (0.90, -0.31), (1.44, -0.24)], 0.055), 'gold', sd=1.0, hd=0.6, rim=False, glow=1.4, seam=1.4)
# A long single cutting edge locked into the executioner's conical crown.
forged_crest(h, 'BladeCrest', 93,
    [(0.70, -0.60), (0.42, -1.24), (-0.14, -1.97), (-1.46, -3.12),
     (-0.67, -2.85), (0.03, -2.40), (0.61, -1.88), (0.92, -1.14), (0.96, -0.66)],
    [(-1.32, -3.03), (-0.29, -2.48), (0.50, -1.86), (0.81, -1.16), (0.43, -1.80)],
    [(-0.44, -2.50), (0.38, -1.84), (0.66, -1.30)], 'gold')

shikko_eye(h)
