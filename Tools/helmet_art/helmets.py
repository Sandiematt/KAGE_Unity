"""The SAN-72 helmet designs, by tier (docs/research/helmet-design.md). Head frame: u head radii to the face, v down, origin at the
head centre. Kage's head runs from u -0.65 (back) to 1.1 (nose), v -0.95 (crown) to 0.96 (chin); his eye is u 0.30..0.60,
v -0.12..0.21 and must stay on bare black face. Orders: 83 soft pieces behind the head, 85..95 on the head (over the hair at 84,
under the eye at 96), 103 the neck wrap on the chest (over the scarf wrap, under the near arm).

Tier language (the owner's reference, docs/reference/helmet_tiers.png, and the research note):
  COMMON     grey cloth, plain steel and straw; low and close to the skull; one soft accent; no glow.
  EPIC       a dark angular shell; violet blades and fins; one glowing violet ring or line; violet wrap.
  TROPHY     white enamel and gold; ceremonial: laurel, a star medallion, long white ribbons; white wrap.
  LEGENDARY  black lacquer kabuto; gold crest (antlers, crescent); a red and gold mon; red lacing and tassel; red wrap.
  GOD MODE   a black demonic shell; glowing red horns and flames; a red glow round the eye; a burning emblem; red wrap.
Each tier step adds silhouette height, pieces and light; inside a tier the helmets differ in their main shape.

The starter, Shiro Maki ("Crimson Scarf"), has no design: its look is Kage's bare hair and his own crimson scarf.
"""
import math
from kit import *

ALL = []


def helmet(hid, look, tier, hair, summary):
    h = Helmet(hid, look, tier, hair, summary)
    ALL.append(h)
    return h


def U(x, y):
    """Reference px to head frame (for pieces measured on Kage's reference: the neck wrap round the scarf)."""
    return uv(x, y)


def ref_pts(*xy):
    return [uv(xy[i], xy[i + 1]) for i in range(0, len(xy), 2)]


def tail(h, name, root, mid, end, w0, w1, tone, feel='Strip', order=83, w_mid=None, sd=3.0, hd=1.6, chains=1):
    """A soft strip on chains (knot tails, ribbons, tassels, drapes): it trails, sags and takes the wind in the game."""
    spine = curve(root[0], root[1], mid[0], mid[1], end[0], end[1])
    out = tine(spine, w0, w1, n=18, w_mid=w_mid)
    if chains == 1:
        ch = [sample(spine, 5)]
    else:
        ch = []
        for k in (-0.25, 0.25):
            off = [(p[0], p[1]) for p in sample(spine, 5)]
            # two chains either side of the spine, a quarter of the root width out
            a, b = off[0], off[1]
            d = norm((b[0] - a[0], b[1] - a[1])); n_ = (-d[1], d[0])
            ch.append([(p[0] + n_[0] * w0 * k, p[1] + n_[1] * w0 * k) for p in off])
    return h.cloth(name, order, out, tone, ch, feel, sd, hd)


def cowl(h, tone, top=0.0, folds=2, flap=1.0, back=944):
    """The tier-coloured neck wrap, wound round the neck over the scarf wrap's front (its crimson knot and tails still show behind):
    an upper band tucked under the jaw, a lower band, creases, and a loose end hanging over the chest on a chain (it swings)."""
    t = top
    pts = ref_pts(812, 258, 813, 240, 822, 226, 836, 215 - t, 864, 205 - t, 900, 199 - t, back - 8, 196 - t, back + 2, 203 - t,
                  back + 6, 220, back, 238, 920, 247, 896, 249, 872, 255, 850, 262, 828, 266)
    sharp = {0, 1, 2}
    out = rounded(*[(p[0], p[1], 1) if i in sharp else p for i, p in enumerate(pts)])
    fl = [band(curve(*U(815, 238), *U(868, 222), *U(back + 2, 214)), 0.07)]
    if folds >= 2: fl.append(band(curve(*U(824, 254), *U(880, 238), *U(back, 232)), 0.06))
    if folds >= 3: fl.append(band(curve(*U(846, 214 - t * 0.6), *U(890, 206 - t * 0.6), *U(back - 6, 204 - t * 0.6)), 0.045))
    for (x0, y0, x1, y1) in ((838, 218 - t, 848, 236), (874, 206 - t, 878, 226), (906, 202 - t, 902, 222), (862, 240, 868, 258)):
        fl.append(band(curve(*U(x0, y0), *U((x0 + x1) / 2 + 3, (y0 + y1) / 2), *U(x1, y1)), 0.035))
    w = h.wrap('Wrap', out, tone, folds=fl)
    if flap > 0:
        L = 64 * flap
        spine = [U(840, 248), U(846, 248 + L * 0.4), U(852, 248 + L * 0.75), U(858, 248 + L)]
        fo = tine(spine, 30 / R, 0.0, n=14, w_mid=26 / R)
        f = h.cloth('WrapEnd', 102, fo, tone, [spine], 'Strip', sd=4, hd=1.8)
        f.bone = 'Chest'
        f.add(band(curve(*U(838, 256), *U(846, 248 + L * 0.5), *U(854, 248 + L * 0.85)), 0.03), tone, 0)
    return w


def star(cu, cv, ro, ri, n=5, rot=-90.0):
    o = []
    for i in range(2 * n):
        a = math.radians(rot + i * 180.0 / n)
        r = ro if i % 2 == 0 else ri
        o.append((cu + math.cos(a) * r, cv + math.sin(a) * r))
    return o


def ring(cu, cv, ro, ri, a0=0.0, a1=360.0, n=40):
    """An annulus (or an arc of one) as one simple polygon."""
    if abs(a1 - a0) >= 359.9:
        # a full ring is cut open by a hair so it stays one simple outline
        a0, a1 = a0 + 0.6, a0 + 359.4
    outer = [(cu + math.cos(math.radians(a0 + (a1 - a0) * i / n)) * ro, cv + math.sin(math.radians(a0 + (a1 - a0) * i / n)) * ro) for i in range(n + 1)]
    inner = [(cu + math.cos(math.radians(a1 - (a1 - a0) * i / n)) * ri, cv + math.sin(math.radians(a1 - (a1 - a0) * i / n)) * ri) for i in range(n + 1)]
    return outer + inner


def leaf(cu, cv, length, width, deg):
    """A pointed leaf (laurel, flame tongue) from its stem at (cu, cv) along deg."""
    a = math.radians(deg)
    tip = (cu + math.cos(a) * length, cv + math.sin(a) * length)
    mid = (cu + math.cos(a) * length * 0.5, cv + math.sin(a) * length * 0.5)
    return tine([(cu, cv), mid, tip], width * 0.35, 0.0, n=10, w_mid=width)


def flame(root, c1, tip, w, bend=0.0):
    """A flame tongue: a tapering curved strip, widest a third of the way up."""
    spine = curve(root[0], root[1], c1[0], c1[1], tip[0], tip[1])
    return tine(spine, w * 0.8, 0.0, n=16, w_mid=w * 1.15)


def emblem(h, name, cu, cv, r, ring_tone, face_tone, mark=None, mark_tone=None, order=93, glow=1.0, halo=None):
    """A round medallion at the temple (the owner's tier pattern): a cel ring, a flat face, a mark; a soft halo for the glowing tiers."""
    if halo:
        h.flat(name + 'Halo', order - 1, disc(cu, cv, r * 1.55), halo[0], which=halo[1], alpha=halo[2], glow=halo[3])
    p = h.cel(name, order, disc(cu, cv, r), ring_tone, sd=2.4, hd=1.2, rim=False, glow=glow)
    p.add(disc(cu, cv, r * 0.66), face_tone, 1)
    if mark is not None: p.add(mark, mark_tone or ring_tone, 2)
    return p


def plain_cap_back(u0=-0.7):
    return u0

# ================================================================================================ COMMON
# Grey cloth and plain steel: matte, low, close to the skull; one soft accent (knot tails, ties); no glow.

h = helmet('kuro_zukin', 'ronin_hood', 'common', 'hidden',
           'Ash-grey shinobi hood wrapped to the jaw with an eye slit, a cord knotted at the back with two trailing tails, grey neck wrap.')
hood = h.cel('Hood', 86, rounded(
    (1.08, -0.27, 1), (0.42, -0.25), (0.16, -0.22, 1), (0.10, 0.04), (0.18, 0.29, 1), (0.66, 0.31), (1.12, 0.30, 1),
    (1.06, 0.62), (0.82, 0.92), (0.42, 1.06), (-0.22, 0.98), (-0.95, 0.66), (-1.18, -0.05), (-0.92, -0.86), (-0.12, -1.24),
    (0.66, -1.1), (1.04, -0.64)), 'cloth', sd=7, hd=2.6)
hood.add(band(curve(0.5, -1.02, -0.2, -0.95, -0.72, -0.42), 0.05), 'cloth', 2)
hood.add(band(curve(-0.35, 0.42, -0.62, 0.2, -0.86, -0.32), 0.05), 'cloth', 0)
hood.add(band(curve(0.25, 0.55, 0.6, 0.62, 0.98, 0.5), 0.05), 'cloth', 0)
hood.add(band(curve(0.1, 0.78, 0.5, 0.86, 0.86, 0.78), 0.04), 'cloth', 2)
h.cel('Cord', 88, band(curve(1.03, -0.56, 0.0, -0.84, -1.06, -0.44), 0.13), 'wrap', sd=1.5, hd=1.0, rim=False)
h.cel('Knot', 89, rounded((-0.92, -0.62), (-1.24, -0.68), (-1.32, -0.36), (-1.04, -0.24)), 'wrap', sd=3, hd=1.4)
tail(h, 'TailA', (-1.16, -0.5), (-1.9, -0.78), (-2.7, -0.52), 0.24, 0.06, 'wrap', w_mid=0.27)
tail(h, 'TailB', (-1.12, -0.36), (-1.8, -0.24), (-2.45, 0.12), 0.22, 0.05, 'wrap', w_mid=0.25)
cowl(h, 'wrap', top=3, folds=3)

# ------------------------------------------------------------------------------------------------ Sugegasa: the wide straw hat
h = helmet('sugegasa', 'straw_kasa', 'common', 'hidden',
           'A wide conical straw kasa over a grey head cloth, a chin cord, two cord tails at the nape, grey neck wrap.')
cap = h.cel('HeadCloth', 85, rounded(
    (1.02, -0.3, 1), (0.6, -0.36), (0.2, -0.3, 1), (0.14, 0.1), (0.26, 0.5, 1), (0.3, 0.78), (0.0, 0.96), (-0.5, 0.78),
    (-0.92, 0.36), (-1.04, -0.3), (-0.6, -0.98), (0.3, -1.06), (0.9, -0.7)), 'cloth', sd=6, hd=2.2)
cap.add(band(curve(-0.3, 0.6, -0.7, 0.3, -0.86, -0.2), 0.05), 'cloth', 0)
cap.add(band(curve(0.18, 0.62, 0.0, 0.8, -0.4, 0.82), 0.04), 'cloth', 2)
# the chin cord from under the brim, down the cheek behind the eye, under the jaw
h.cel('ChinCord', 87, band(curve(0.0, -0.42, 0.06, 0.4, 0.46, 0.92), 0.07), 'straw', sd=1.0, hd=0.6, rim=False)
kasa = h.cel('Kasa', 92, rounded(
    (-2.3, -0.42, 1), (-1.2, -1.02), (-0.08, -1.78, 1), (1.1, -1.0), (2.28, -0.24, 1), (2.24, -0.1, 1), (1.0, -0.3), (0.0, -0.44),
    (-1.1, -0.48), (-2.26, -0.3, 1)), 'straw', sd=8, hd=3.0)
for k in (-1.7, -1.05, -0.45, 0.2, 0.85, 1.55):
    t = (k + 2.3) / 4.58
    bu = -2.28 + 4.54 * t
    bv = -0.36 + 0.18 * t
    kasa.add(band([(-0.08, -1.7), (bu * 0.55 - 0.04, -1.7 + (bv + 1.7) * 0.55), (bu, bv - 0.06)], 0.035), 'straw', 0 if k < -0.2 else 2)
kasa.add(band(curve(-2.24, -0.34, 0.0, -0.5, 2.22, -0.18), 0.05), 'straw', 0)
kasa.add(band(curve(-1.2, -0.92, -0.08, -1.28, 1.04, -0.92), 0.03), 'straw', 0)
h.cel('Apex', 93, disc(-0.08, -1.74, 0.1), 'steel', sd=1.6, hd=0.8, rim=False)
tail(h, 'CordA', (-0.82, 0.1), (-1.3, 0.3), (-1.9, 0.62), 0.09, 0.07, 'straw', feel='Tassel', w_mid=0.09)
tail(h, 'CordB', (-0.8, 0.2), (-1.2, 0.48), (-1.6, 0.9), 0.08, 0.06, 'straw', feel='Tassel', w_mid=0.08)
h.cel('CordKnot', 88, disc(-0.84, 0.14, 0.11), 'straw', sd=1.6, hd=0.8, rim=False)
cowl(h, 'wrap', top=0, folds=2)

# ------------------------------------------------------------------------------------------------ Hachigane: the brow plate band
h = helmet('hachigane', 'storm_band', 'common', 'kept',
           'His spiky hair kept over a grey cloth band with a riveted steel brow plate, long knot tails, a grey half mask and wrap.')
h.cel('Band', 86, rounded(
    (1.06, -0.62, 1), (0.2, -0.78), (-0.66, -0.7), (-1.06, -0.62, 1), (-1.08, -0.3, 1), (-0.62, -0.36), (0.2, -0.42), (1.08, -0.3, 1)),
    'cloth', sd=3, hd=1.4)
plate = h.cel('Plate', 88, rounded((0.3, -0.8, 1), (0.7, -0.84), (1.12, -0.72, 1), (1.14, -0.26, 1), (0.7, -0.3), (0.3, -0.34, 1)),
              'steel', sd=4, hd=2.0)
for (pu, pv) in ((0.4, -0.72), (1.04, -0.64), (0.4, -0.42), (1.05, -0.36)):
    plate.add(disc(pu, pv, 0.04), 'steel', 0)
plate.add(band([(0.56, -0.66), (0.7, -0.5), (0.84, -0.66), (0.95, -0.44)], 0.04), 'steel', 0)
h.cel('Knot', 89, rounded((-0.96, -0.68), (-1.3, -0.74), (-1.34, -0.3), (-1.0, -0.24)), 'cloth', sd=3, hd=1.4)
tail(h, 'TailA', (-1.2, -0.56), (-2.1, -0.9), (-3.0, -0.7), 0.26, 0.08, 'cloth', feel='Tails', w_mid=0.3)
tail(h, 'TailB', (-1.18, -0.4), (-2.0, -0.32), (-2.7, 0.05), 0.24, 0.07, 'cloth', feel='Tails', w_mid=0.27)
# the half mask over the nose and mouth, up to just under the eye
mask = h.cel('HalfMask', 90, rounded(
    (0.2, 0.28, 1), (0.7, 0.3), (1.16, 0.32, 1), (1.08, 0.66), (0.82, 0.96), (0.4, 1.06), (0.0, 1.0), (-0.24, 0.8, 1), (0.06, 0.52)),
    'wrap', sd=5, hd=2.0)
mask.add(band(curve(0.24, 0.62, 0.66, 0.66, 1.04, 0.56), 0.04), 'wrap', 0)
mask.add(band(curve(0.12, 0.84, 0.5, 0.92, 0.86, 0.82), 0.04), 'wrap', 0)
cowl(h, 'wrap', top=2, folds=2)

# ------------------------------------------------------------------------------------------------ Kasumi Veil: the mist veil
h = helmet('kasumi_men', 'veil', 'common', 'hidden',
           'A peaked grey cowl with a long drape down the back, a pale gauze veil from under the eye to a point on the chest, a thin steel circlet.')
cowlp = h.cel('Cowl', 86, rounded(
    (1.14, -0.22, 1), (0.94, -0.62), (0.6, -1.08), (0.0, -1.42, 1), (-0.6, -1.12), (-1.08, -0.56), (-1.18, 0.24), (-0.9, 0.9),
    (-0.3, 1.12), (0.12, 0.86), (0.1, 0.3), (0.2, -0.2, 1)), 'cloth', sd=7, hd=2.6)
cowlp.add(band(curve(0.0, -1.34, -0.46, -0.9, -0.84, -0.06), 0.05), 'cloth', 2)
cowlp.add(band(curve(-0.3, 0.84, -0.76, 0.44, -0.96, -0.2), 0.05), 'cloth', 0)
cowlp.add(band(curve(0.86, -0.62, 0.5, -0.42, 0.24, -0.2), 0.045), 'cloth', 0)
cowlp.add(band(curve(0.1, -1.3, 0.6, -0.96, 1.08, -0.3), 0.04), 'cloth', 2)
h.cel('Circlet', 88, band(curve(1.1, -0.44, 0.0, -0.76, -1.16, -0.34), 0.07), 'steel', sd=1.0, hd=0.6, rim=False)
h.cel('Bead', 89, disc(0.02, -0.64, 0.1), 'steel', sd=1.6, hd=0.8, rim=False)
# the long back drape of the cowl, on chains: it trails behind him and takes the wind
dr = rounded((-0.4, -1.0, 1), (-1.2, -0.5), (-1.7, 0.4), (-2.3, 1.4, 1), (-1.8, 1.4), (-1.3, 1.7, 1), (-0.9, 1.1), (-0.5, 0.8, 1), (-0.9, 0.0))
d = h.cloth('Drape', 83, dr, 'cloth', [[(-0.7, -0.7), (-1.2, -0.1), (-1.6, 0.6), (-2.0, 1.3)], [(-0.6, 0.0), (-0.9, 0.5), (-1.1, 1.0), (-1.3, 1.55)]], 'Cape', sd=6, hd=2.2)
d.add(band(curve(-0.8, -0.3, -1.2, 0.4, -1.6, 1.2), 0.04), 'cloth', 0)
# the veil: a soft gauze triangle from the cheek under the eye over the mouth and the scarf, on two chains (drawn over the scarf
# wrap, at 103, below the eye so it never covers it)
veil_out = rounded((0.16, 0.36, 1), (0.7, 0.35), (1.2, 0.38, 1), (1.22, 0.9), (1.06, 1.5, 1), (0.86, 1.34), (0.66, 1.8, 1), (0.46, 1.38), (0.24, 1.56, 1), (0.08, 1.1), (0.06, 0.6))
v = h.cloth('Veil', 103, veil_out, 'gauze', [[(0.36, 0.4), (0.38, 0.8), (0.42, 1.2), (0.5, 1.6)], [(0.98, 0.4), (1.02, 0.8), (0.96, 1.2), (0.8, 1.6)]], 'Strip', sd=5, hd=2.0)
v.add(band(curve(0.3, 0.6, 0.4, 1.0, 0.5, 1.5), 0.035), 'gauze', 0)
v.add(band(curve(0.82, 0.5, 0.9, 1.0, 0.7, 1.6), 0.035), 'gauze', 0)
v.add(band(curve(0.2, 0.42, 0.7, 0.4, 1.18, 0.44), 0.045), 'steel', 1)
for k in range(3):
    v.add(band(curve(0.1 + 0.05 * k, 0.7 + 0.24 * k, 0.6, 0.66 + 0.24 * k, 1.16 - 0.06 * k, 0.74 + 0.24 * k), 0.025), 'gauze', 2, 0.7)

# ================================================================================================ EPIC
# A dark, sleek, angular shell; violet blade fins; one glowing violet ring; violet wrap.

def epic_ring(h, cu=0.02, cv=-0.02, r=0.22):
    h.flat('RingHalo', 92, disc(cu, cv, r * 1.6), 'glow', which=0, alpha=0.28, glow=1.6)
    h.cel('Ring', 93, ring(cu, cv, r, r * 0.64), 'shell', sd=1.6, hd=1.0, rim=False)
    h.flat('RingGlow', 94, ring(cu, cv, r * 0.9, r * 0.74), 'glow', which=1, glow=2.6)
    h.flat('RingCore', 94, disc(cu, cv, r * 0.3), 'glow', which=2, glow=2.2)

# ------------------------------------------------------------------------------------------------ Tetsu Kabuto: the blade-finned helm
h = helmet('tetsu_kabuto', 'iron_kabuto', 'epic', 'hidden',
           'A sleek black helm with a pointed brow, four violet blade fins swept back off the crown, a glowing violet ring at the ear, violet wrap.')
for i, (r0, c, t, w) in enumerate((((-0.1, -1.0), (-1.2, -1.9), (-2.5, -1.9), 0.42), ((-0.4, -0.9), (-1.5, -1.4), (-2.7, -1.0), 0.38),
                                    ((-0.6, -0.6), (-1.6, -0.7), (-2.6, -0.2), 0.34), ((-0.7, -0.3), (-1.4, -0.1), (-2.2, 0.4), 0.28))):
    p = h.cel('Fin%d' % i, 85, tine(curve(r0[0], r0[1], c[0], c[1], t[0], t[1]), w, 0.0, n=18, w_mid=w * 0.9), 'blade', sd=5, hd=2.0)
    p.add(band(curve(r0[0] - 0.1, r0[1], c[0], c[1] + 0.03, t[0] * 0.8 + r0[0] * 0.2, t[1] * 0.8 + r0[1] * 0.2), 0.035), 'blade', 2)
shell = h.cel('Shell', 87, rounded(
    (1.3, -0.26, 1), (0.92, -0.34), (0.56, -0.24, 1), (0.22, -0.18), (0.16, 0.3, 1), (0.42, 0.62), (1.0, 0.7, 1), (0.66, 0.96),
    (0.1, 1.04, 1), (-0.6, 0.78), (-1.02, 0.24, 1), (-1.04, -0.5), (-0.5, -1.16), (0.3, -1.22), (0.9, -0.86)), 'shell', sd=7, hd=2.6)
shell.add(band([(0.5, -1.12), (0.1, -0.7), (-0.6, -0.56), (-0.98, -0.6)], 0.04), 'plate', 2)
shell.add(band([(0.2, 0.3), (-0.2, 0.36), (-0.6, 0.62)], 0.04), 'plate', 2)
shell.add(band([(0.5, -0.32), (0.86, -0.42), (1.16, -0.38)], 0.035), 'plate', 2)
h.flat('Seam', 89, band([(0.3, -0.98), (-0.08, -0.72), (-0.7, -0.62)], 0.035), 'glow', which=1, glow=2.0)
h.flat('Jaw', 89, band([(0.22, 0.42), (0.5, 0.64), (0.9, 0.7)], 0.03), 'glow', which=1, glow=2.0)
epic_ring(h, 0.0, -0.06, 0.22)
cowl(h, 'wrap', top=4, folds=3)

# ------------------------------------------------------------------------------------------------ Oni Menpo: the violet oni
h = helmet('oni_menpo', 'oni_mask', 'epic', 'hidden',
           'A black skull cap with two violet horns hooking forward, an angular oni mask on the jaw with glowing violet fangs, violet wrap.')
cap = h.cel('Cap', 86, rounded(
    (1.1, -0.36, 1), (0.7, -0.4), (0.3, -0.3, 1), (0.1, 0.1), (-0.3, 0.5, 1), (-0.8, 0.5), (-1.08, -0.1, 1), (-0.9, -0.84),
    (-0.2, -1.22), (0.6, -1.08), (1.02, -0.7)), 'shell', sd=7, hd=2.6)
cap.add(band([(0.98, -0.5), (0.3, -0.52), (-0.5, -0.36), (-1.0, -0.12)], 0.05), 'plate', 2)
for i, (r0, c, t, w) in enumerate((((-0.1, -1.0), (-0.3, -2.0), (0.5, -2.4), 0.36), ((0.4, -0.96), (0.5, -1.8), (1.2, -2.0), 0.3))):
    p = h.cel('Horn%d' % i, 88 + i, tine(curve(r0[0], r0[1], c[0], c[1], t[0], t[1]), w, 0.0, n=18), 'blade', sd=5, hd=2.0)
    p.add(band(curve(r0[0] + 0.08, r0[1] - 0.1, c[0] + 0.1, c[1], t[0] * 0.85 + r0[0] * 0.15, t[1] * 0.85 + r0[1] * 0.15), 0.035), 'glow', 1)
menpo = h.cel('Menpo', 90, rounded(
    (0.2, 0.26, 1), (0.6, 0.3, 1), (0.8, 0.22, 1), (1.24, 0.34, 1), (1.1, 0.56, 1), (1.2, 0.72, 1), (0.98, 0.98, 1), (0.6, 1.12, 1),
    (0.1, 1.06, 1), (-0.3, 0.7, 1), (0.0, 0.5, 1)), 'plate', sd=5, hd=2.2)
menpo.add(band([(0.3, 0.4), (0.62, 0.46), (0.9, 0.36)], 0.035), 'plate', 2)
h.flat('Mouth', 91, rounded((0.56, 0.72, 1), (1.12, 0.66, 1), (1.04, 0.8, 1), (0.6, 0.84, 1)), 'glow', which=1, glow=2.4)
for (fu, fv, fd) in ((0.7, 0.86, -1), (0.92, 0.84, -1), (0.76, 0.62, 1), (0.98, 0.6, 1)):
    h.flat('Fang', 92, [(fu - 0.05, fv), (fu + 0.05, fv), (fu, fv + 0.16 * fd)], 'shell', which=2)
h.flat('CheekLine', 92, band([(0.06, 0.56), (0.36, 0.8), (0.5, 1.0)], 0.03), 'glow', which=1, glow=2.0)
cowl(h, 'wrap', top=2, folds=2)

# ------------------------------------------------------------------------------------------------ Hoshi Kabuto: the star sentinel
h = helmet('hoshi_kabuto', 'sentinel_helm', 'epic', 'hidden',
           'A faceted black bowl studded with glowing violet star rivets, a tall bladed crest along the crown, a long visor, a violet cord, violet wrap.')
crest = h.cel('Crest', 85, rounded(
    (0.62, -1.08, 1), (0.5, -1.9), (0.0, -2.36, 1), (-0.5, -2.3), (-1.1, -2.1, 1), (-0.9, -1.96, 1), (-1.6, -1.9, 1), (-1.3, -1.7, 1), (-2.2, -1.6, 1),
    (-1.2, -1.3), (-0.8, -0.86, 1)), 'blade', sd=6, hd=2.2)
crest.add(band(curve(0.4, -1.2, 0.2, -1.9, -0.4, -2.1), 0.04), 'blade', 2)
crest.add(band(curve(-0.2, -1.2, -0.6, -1.6, -1.4, -1.7), 0.035), 'blade', 0)
h.flat('CrestEdge', 86, band(curve(0.56, -1.1, 0.44, -1.9, -0.05, -2.28), 0.035), 'glow', which=1, glow=2.2)
shik = h.cel('Shikoro', 86, rounded(
    (-0.2, -0.6, 1), (-0.9, -0.5, 1), (-1.5, 0.0, 1), (-1.7, 0.66, 1), (-0.9, 0.84, 1), (-0.4, 0.4, 1), (-0.1, 0.0, 1)), 'plate', sd=6, hd=2.2)
for k in (0.25, 0.5, 0.75):
    shik.add(band([(-0.2 - 0.15 * k, -0.5 + 0.9 * k), (-0.9 - 0.3 * k, -0.4 + 0.86 * k), (-1.5 - 0.2 * k, 0.0 + 0.66 * k)], 0.035), 'shell', 0)
bowl = h.cel('Bowl', 87, rounded(
    (1.12, -0.36, 1), (0.9, -0.98, 1), (0.3, -1.3, 1), (-0.5, -1.2, 1), (-1.1, -0.7, 1), (-1.16, -0.32, 1), (0.1, -0.44, 1)), 'shell', sd=7, hd=2.6)
bowl.add(band([(0.3, -1.26), (0.2, -0.9), (0.24, -0.46)], 0.03), 'plate', 2)
bowl.add(band([(-0.5, -1.16), (-0.42, -0.8), (-0.4, -0.42)], 0.03), 'plate', 2)
for (su, sv) in ((0.7, -0.92), (0.66, -0.62), (0.0, -1.06), (-0.04, -0.72), (-0.74, -0.88), (-0.8, -0.56)):
    h.flat('Star', 89, star(su, sv, 0.08, 0.035), 'glow', which=1, glow=2.4)
h.cel('Visor', 88, rounded((0.0, -0.5, 1), (0.9, -0.5, 1), (1.6, -0.36, 1), (1.5, -0.22, 1), (0.8, -0.3, 1), (0.0, -0.3, 1)), 'plate', sd=3, hd=1.4)
h.flat('VisorLine', 89, band([(0.2, -0.36), (0.9, -0.38), (1.5, -0.3)], 0.03), 'glow', which=1, glow=2.0)
h.cel('Fuki', 90, rounded((0.24, -0.46, 1), (0.36, -0.1, 1), (0.1, 0.28, 1), (-0.3, 0.2, 1), (-0.2, -0.4, 1)), 'shell', sd=4, hd=1.8)
epic_ring(h, 0.02, -0.08, 0.17)
tail(h, 'Cord', (-1.2, -0.4), (-1.8, -0.2), (-2.3, 0.5), 0.14, 0.1, 'wrap', feel='Tassel', w_mid=0.14)
cowl(h, 'wrap', top=1, folds=2)

# ================================================================================================ TROPHY
# White enamel and gold, ceremonial: laurel, star medallions, long white ribbons; white wrap.

def laurel(h, name, root, end, side=1, n=6, order=92, size=0.3):
    """A gold laurel branch along a curve: a stem and paired leaves pointing to its end."""
    spine = curve(root[0], root[1], (root[0] + end[0]) / 2 + 0.1 * side, (root[1] + end[1]) / 2 - 0.25, end[0], end[1])
    p = h.cel(name, order, tine(spine, 0.06, 0.03, n=12, tip=False), 'gold', sd=1.0, hd=0.6, rim=False)
    s = sample(spine, n)
    for i in range(1, n + 1):
        a, b = s[i - 1], s[i]
        deg = math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))
        k = size * (1.0 - 0.35 * i / n)
        h.cel(name + 'L%d' % i, order, leaf(b[0], b[1], k, k * 0.42, deg - 38), 'gold', sd=1.6, hd=0.8, rim=False)
        h.cel(name + 'R%d' % i, order, leaf(b[0], b[1], k, k * 0.42, deg + 38), 'gold', sd=1.6, hd=0.8, rim=False)
    return p

# ------------------------------------------------------------------------------------------------ Hisame Zukin: the ceremonial hood
h = helmet('hisame_zukin', 'rain_hood', 'trophy', 'hidden',
           'A white enamel hood edged in gold, a gold laurel over the brow set with a pale rain-drop gem, a gold star medallion, white ribbons, white wrap.')
for i, (r0, c, t, w) in enumerate((((-0.9, -0.5), (-1.8, -0.9), (-2.9, -0.5), 0.3), ((-0.9, -0.3), (-1.7, -0.1), (-2.6, 0.5), 0.26))):
    tail(h, 'Ribbon%d' % i, r0, c, t, w, w * 0.8, 'ribbon', feel='Tails', w_mid=w * 1.05)
hood = h.cel('Hood', 86, rounded(
    (1.14, -0.3, 1), (0.6, -0.3), (0.2, -0.2, 1), (0.14, 0.3), (0.3, 0.62, 1), (0.2, 0.96), (-0.4, 0.9), (-0.98, 0.4), (-1.1, -0.4),
    (-0.6, -1.12), (0.3, -1.24), (0.96, -0.82)), 'shell', sd=7, hd=2.6)
hood.add(band(curve(1.1, -0.36, 0.4, -0.34, 0.22, -0.16), 0.06), 'gold', 1)
hood.add(band(curve(0.18, -0.1, 0.12, 0.3, 0.28, 0.6), 0.06), 'gold', 1)
hood.add(band(curve(0.3, -1.18, -0.5, -0.9, -0.9, -0.2), 0.04), 'shell', 0)
laurel(h, 'Laurel', (0.0, -0.5), (0.9, -1.1), side=1, n=6, size=0.3)
h.cel('Gem', 93, rounded((0.9, -0.62), (1.02, -0.5, 1), (0.9, -0.3, 1), (0.78, -0.5, 1)), 'glass', sd=1.6, hd=0.8, rim=False, glow=1.4)
emblem(h, 'Medal', -0.18, -0.02, 0.2, 'gold', 'ribbon', mark=star(-0.18, -0.02, 0.11, 0.05), mark_tone='gold')
cowl(h, 'wrap', top=3, folds=3)

# ------------------------------------------------------------------------------------------------ Sumi Eboshi: the ink court cap
h = helmet('sumi_eboshi', 'sumi_eboshi', 'trophy', 'hidden',
           'A tall white lacquer court cap swept back, crossed by one black ink stroke, a gold brow band and star medallion, two long white ribbons.')
tail(h, 'RibbonA', (-1.0, -1.2), (-1.8, -1.5), (-2.8, -1.2), 0.22, 0.2, 'ribbon', feel='Tails', w_mid=0.24)
tail(h, 'RibbonB', (-1.0, -0.9), (-1.7, -0.8), (-2.5, -0.3), 0.2, 0.18, 'ribbon', feel='Tails', w_mid=0.22)
eb = h.cel('Cap', 87, rounded(
    (1.06, -0.34, 1), (0.96, -1.2), (0.5, -2.0), (-0.2, -2.62), (-0.9, -2.86), (-1.18, -2.4), (-1.12, -1.5), (-1.08, -0.8), (-1.04, -0.24, 1),
    (0.0, -0.5)), 'shell', sd=8, hd=3.0)
eb.add(band(curve(0.1, -0.6, 0.1, -1.7, -0.7, -2.6), 0.04), 'shell', 0)
eb.add(band(curve(-0.5, -0.5, -0.7, -1.5, -0.98, -2.5), 0.04), 'shell', 0)
stroke = tine(curve(-1.02, -1.3, -0.1, -1.98, 0.86, -1.3), 0.14, 0.02, n=16, w_mid=0.24)
eb.add(stroke, 'ink', 1)
eb.add(band(curve(-0.84, -1.36, -0.1, -1.8, 0.56, -1.44), 0.03), 'ink', 2)
h.cel('Band', 88, band(curve(-1.02, -0.36, 0.0, -0.62, 1.06, -0.4), 0.16), 'gold', sd=2.0, hd=1.0, rim=False)
h.cel('Crease', 89, band(curve(-0.96, -2.7, -0.5, -2.4, -0.06, -2.5), 0.05), 'gold', sd=0.8, hd=0.4, rim=False)
emblem(h, 'Medal', -0.1, -0.24, 0.19, 'gold', 'ink', mark=star(-0.1, -0.24, 0.1, 0.045), mark_tone='gold')
h.cel('ChinCord', 86, band(curve(-0.1, -0.1, -0.04, 0.6, 0.44, 0.94), 0.06), 'gold', sd=1.0, hd=0.6, rim=False)
cowl(h, 'wrap', top=1, folds=2)

# ================================================================================================ LEGENDARY
# Black lacquer kabuto; a gold crest; a red and gold mon; red lacing; a red tassel; red wrap.

def kabuto(h, lace='red', back=-1.7, bowl_top=-1.28):
    shik = h.cel('Shikoro', 86, rounded(
        (-0.1, -0.56, 1), (-0.9, -0.52, 1), (-1.4, -0.1, 1), (back, 0.66, 1), (-0.9, 0.86, 1), (-0.36, 0.42, 1), (-0.1, 0.0, 1)), 'shell', sd=6, hd=2.2)
    for k in (0.25, 0.5, 0.75):
        shik.add(band([(-0.16 - 0.12 * k, -0.48 + 0.92 * k), (-0.9 - 0.12 * k, -0.42 + 0.86 * k), (-1.4 + (back + 1.4) * k, -0.04 + 0.7 * k)], 0.05), 'gold', 0)
        shik.add(band([(-0.18 - 0.12 * k, -0.43 + 0.92 * k), (-0.9 - 0.12 * k, -0.37 + 0.86 * k), (-1.4 + (back + 1.4) * k, 0.01 + 0.7 * k)], 0.045), lace, 1)
    bowl = h.cel('Bowl', 87, rounded(
        (1.1, -0.36, 1), (0.98, -1.0), (0.2, bowl_top - 0.12), (-0.7, -1.16), (-1.14, -0.36, 1), (0.0, -0.5)), 'shell', sd=7, hd=2.6)
    for x in (-0.6, -0.2, 0.2, 0.6):
        bowl.add(band(curve(0.15, bowl_top + 0.02, x * 1.05, -1.0, x * 1.2, -0.52), 0.03), 'shell', 2)
    h.cel('Koshimaki', 88, band(curve(-1.14, -0.44, 0.0, -0.62, 1.1, -0.42), 0.13), 'gold', sd=1.6, hd=0.8, rim=False)
    h.cel('Visor', 89, rounded((0.2, -0.52, 1), (1.0, -0.5, 1), (1.58, -0.34, 1), (1.48, -0.2, 1), (0.9, -0.3, 1), (0.2, -0.32, 1)), 'shell', sd=3, hd=1.4)
    h.cel('VisorEdge', 90, band([(0.24, -0.34), (0.9, -0.33), (1.5, -0.24)], 0.05), 'gold', sd=0.8, hd=0.4, rim=False)
    fuki = h.cel('Fuki', 90, rounded((0.26, -0.5, 1), (0.42, -0.14, 1), (0.2, 0.3, 1), (-0.36, 0.24, 1), (-0.26, -0.44, 1)), 'shell', sd=4, hd=1.8)
    fuki.add(band([(0.4, -0.14), (0.2, 0.28), (-0.34, 0.22)], 0.05), 'gold', 1)
    return bowl


def mon(h, cu, cv, r, kind):
    p = emblem(h, 'Mon', cu, cv, r, 'gold', 'red', order=92)
    if kind == 'tomoe':
        for k in range(3):
            a = math.radians(k * 120 - 90)
            c = (cu + math.cos(a) * r * 0.28, cv + math.sin(a) * r * 0.28)
            p.add(tine(curve(c[0], c[1], cu + math.cos(a + 1.2) * r * 0.5, cv + math.sin(a + 1.2) * r * 0.5, cu + math.cos(a + 2.0) * r * 0.48, cv + math.sin(a + 2.0) * r * 0.48), r * 0.32, 0.0, n=8), 'gold', 2)
    elif kind == 'crescent':
        p.add(ring(cu, cv, r * 0.46, r * 0.3, 110, 400, 24), 'gold', 2)
    else:
        p.add(star(cu, cv, r * 0.5, r * 0.22, 4, -90), 'gold', 2)
    return p

# ------------------------------------------------------------------------------------------------ Shika Kabuto: the gold antlers
h = helmet('shika_kabuto', 'sage_antlers', 'legendary', 'hidden',
           'A black lacquer kabuto laced in red, a gold brow band and visor edge, branching gold antlers, a red and gold mon, a red tassel, red wrap.')
tail(h, 'Tassel', (-1.0, -0.6), (-1.5, -0.2), (-1.7, 0.6), 0.12, 0.26, 'red', feel='Tassel', w_mid=0.14)
kabuto(h)
for side, (ox, k, o) in enumerate(((-0.42, 0.86, 85), (0.06, 1.0, 93))):
    tone = 'gold'
    def T(*q): return [(ox + q[i] * k, -1.0 + (q[i + 1] + 1.0) * k) for i in range(0, len(q), 2)]
    a = h.cel('Antler%d' % side, o, tine(T(0.3, -1.0, 0.5, -1.7, 0.3, -2.4, 0.6, -3.0), 0.26 * k, 0.06, n=14), tone, sd=3, hd=1.4, rim=side == 1)
    for j, q in enumerate(((0.48, -1.62, 0.9, -1.96, 1.18, -1.86), (0.34, -2.26, 0.0, -2.66, -0.12, -2.96), (0.48, -2.72, 0.86, -2.92, 0.98, -3.1))):
        h.cel('Antler%dT%d' % (side, j), o, tine(T(*q), 0.15 * k, 0.0, n=10), tone, sd=2, hd=1.0, rim=side == 1)
mon(h, 0.0, -0.06, 0.2, 'star')
cowl(h, 'wrap', top=3, folds=3)

# ------------------------------------------------------------------------------------------------ Shikko Zukin: the crescent hood-kabuto
h = helmet('shikko_zukin', 'executioner', 'legendary', 'hidden',
           'A tall hood-shaped black lacquer kabuto whose peak curls back, a great gold crescent rising off the brow, red mon, red cords, red wrap.')
tail(h, 'CordA', (-0.9, -0.2), (-1.4, 0.2), (-1.5, 0.9), 0.1, 0.22, 'red', feel='Tassel', w_mid=0.1)
tail(h, 'CordB', (-1.0, -1.6), (-1.7, -1.6), (-2.3, -1.0), 0.16, 0.12, 'red', feel='Tails', w_mid=0.18)
h.cel('Shikoro', 86, rounded((-0.1, -0.5, 1), (-1.0, -0.4, 1), (-1.5, 0.5, 1), (-0.8, 0.8, 1), (-0.3, 0.4, 1)), 'shell', sd=5, hd=2.0).add(
    band([(-0.3, -0.1), (-1.0, 0.0), (-1.3, 0.4)], 0.05), 'red', 1)
zk = h.cel('Zukin', 87, rounded(
    (1.1, -0.36, 1), (1.0, -0.9), (0.6, -1.5), (0.0, -2.1), (-0.6, -2.3), (-1.1, -2.0, 1), (-0.7, -1.8), (-0.9, -1.3), (-1.14, -0.36, 1),
    (0.0, -0.5)), 'shell', sd=8, hd=3.0)
zk.add(band(curve(0.6, -0.5, 0.4, -1.4, -0.5, -2.1), 0.035), 'gold', 1)
zk.add(band(curve(-0.4, -0.5, -0.7, -1.2, -0.9, -1.8), 0.03), 'shell', 2)
h.cel('Koshimaki', 88, band(curve(-1.14, -0.44, 0.0, -0.62, 1.1, -0.42), 0.12), 'gold', sd=1.6, hd=0.8, rim=False)
h.cel('Visor', 89, rounded((0.2, -0.52, 1), (1.0, -0.5, 1), (1.5, -0.36, 1), (1.4, -0.22, 1), (0.9, -0.3, 1), (0.2, -0.32, 1)), 'shell', sd=3, hd=1.4)
h.cel('VisorEdge', 90, band([(0.24, -0.34), (0.9, -0.33), (1.42, -0.26)], 0.05), 'gold', sd=0.8, hd=0.4, rim=False)
cres = ring(0.75, -1.25, 1.05, 0.78, 145, 350, 30)
h.cel('Crescent', 93, cres, 'gold', sd=3, hd=1.6)
h.cel('Tsunomoto', 94, disc(0.86, -0.62, 0.12), 'gold', sd=1.6, hd=0.8, rim=False)
mon(h, -0.1, -0.02, 0.2, 'crescent')
cowl(h, 'wrap', top=3, folds=3)

# ================================================================================================ GOD MODE
# A black demonic shell; glowing red horns and flames; a red glow round the eye; a burning emblem; red wrap.

def god_eye(h, u0=0.24):
    """The red glow round Kage's eye: his white eye stays on top (order 96), lit from behind."""
    h.flat('EyeHalo', 94, ellipse(0.46, 0.04, 0.3, 0.2, -18), 'glow', which=0, alpha=0.45, glow=2.0)
    h.flat('EyeGlow', 95, ellipse(0.45, 0.04, 0.2, 0.13, -18), 'glow', which=1, glow=2.8)

def god_emblem(h, cu, cv, r):
    h.flat('EmblemHalo', 91, disc(cu, cv, r * 1.6), 'glow', which=0, alpha=0.3, glow=1.8)
    p = h.cel('Emblem', 92, disc(cu, cv, r), 'plate', sd=2.0, hd=1.0, rim=False)
    p.add(disc(cu, cv, r * 0.72), 'shell', 1)
    h.flat('EmblemRing', 93, ring(cu, cv, r * 0.88, r * 0.74), 'glow', which=1, glow=2.6)
    h.flat('EmblemMark', 93, tine(curve(cu - r * 0.3, cv + r * 0.3, cu + r * 0.4, cv + r * 0.1, cu - r * 0.05, cv - r * 0.45), r * 0.24, 0.0, n=8), 'fire', which=2, glow=2.2)

# ------------------------------------------------------------------------------------------------ Utsuro Kabuto: the hollow flame
h = helmet('utsuro_kabuto', 'hollow_crest', 'god', 'hidden',
           'A black demonic helm under a broken ring of red fire, flames streaming back off the crown, jagged neck shards, a red-lit eye, a burning emblem.')
h.flat('HaloGlow', 83, ring(-0.5, -0.6, 1.9, 1.5, 150, 330, 36), 'glow', which=0, alpha=0.35, glow=1.8)
h.cel('Halo', 84, ring(-0.5, -0.6, 1.8, 1.6, 156, 318, 36), 'fire', sd=1.4, hd=0.8, rim=False, glow=1.6)
for i, (r0, c, t, w) in enumerate((((0.2, -1.1), (-0.6, -2.0), (-1.9, -2.3), 0.44), ((-0.3, -1.0), (-1.2, -1.6), (-2.4, -1.5), 0.4),
                                    ((-0.7, -0.7), (-1.6, -0.9), (-2.6, -0.5), 0.36), ((0.6, -1.0), (0.4, -1.8), (-0.4, -2.5), 0.3))):
    h.cel('Flame%d' % i, 85, flame(r0, c, t, w), 'fire', sd=4, hd=2.0, rim=False, glow=1.8)
    h.flat('FlameCore%d' % i, 85, flame(r0, ((r0[0] + c[0]) / 2, (r0[1] + c[1]) / 2), ((c[0] + t[0]) / 2, (c[1] + t[1]) / 2), w * 0.45), 'glow', which=2, glow=2.2)
h.cel('Shards', 86, rounded((-0.2, -0.5, 1), (-1.0, -0.4, 1), (-1.5, 0.2, 1), (-1.3, 0.3, 1), (-1.7, 0.7, 1), (-1.2, 0.66, 1), (-1.1, 0.9, 1),
                            (-0.7, 0.62, 1), (-0.5, 0.86, 1), (-0.3, 0.4, 1)), 'plate', sd=5, hd=2.0)
helm = h.cel('Helm', 87, rounded(
    (1.18, -0.3, 1), (0.9, -0.36, 1), (0.6, -0.2, 1), (0.24, -0.26, 1), (0.14, 0.2, 1), (0.3, 0.5, 1), (0.9, 0.56, 1), (1.12, 0.74, 1), (0.7, 1.0, 1),
    (0.1, 1.02, 1), (-0.6, 0.7, 1), (-1.06, 0.0, 1), (-0.96, -0.8, 1), (-0.3, -1.2, 1), (0.5, -1.14, 1), (1.0, -0.74, 1)), 'shell', sd=7, hd=2.6)
helm.add(band([(0.4, -1.04), (0.0, -0.66), (-0.7, -0.5)], 0.04), 'plate', 2)
helm.add(band([(0.3, 0.5), (0.0, 0.62), (-0.5, 0.6)], 0.04), 'plate', 2)
h.flat('Crack', 88, band([(0.9, -0.66), (0.5, -0.6), (0.3, -0.8), (-0.2, -0.82)], 0.035), 'glow', which=1, glow=2.6)
h.flat('Teeth', 88, [(0.36, 0.56), (0.5, 0.74), (0.6, 0.58), (0.72, 0.78), (0.84, 0.6), (0.98, 0.78), (1.06, 0.7), (0.9, 0.56)], 'glow', which=1, glow=2.2)
god_eye(h)
god_emblem(h, -0.12, -0.04, 0.19)
cowl(h, 'wrap', top=3, folds=3)

# ------------------------------------------------------------------------------------------------ Raijin Kabuto: the thunder god
h = helmet('raijin_kabuto', 'raijin_kabuto', 'god', 'hidden',
           'A black horned war helm: two great red glowing horns, a lightning crest between them, a fanged jaw guard, a red-lit eye, the thunder-drum emblem burning.')
tail(h, 'FireA', (-1.0, -0.4), (-1.9, -0.6), (-2.8, -0.2), 0.3, 0.02, 'fire', feel='Tails', w_mid=0.32)
tail(h, 'FireB', (-0.9, -0.1), (-1.7, 0.1), (-2.4, 0.6), 0.26, 0.02, 'fire', feel='Tails', w_mid=0.28)
h.cel('Shikoro', 86, rounded((-0.1, -0.56, 1), (-0.9, -0.5, 1), (-1.4, -0.1, 1), (-1.3, 0.1, 1), (-1.7, 0.5, 1), (-1.2, 0.5, 1), (-1.2, 0.8, 1),
                             (-0.7, 0.62, 1), (-0.4, 0.8, 1), (-0.2, 0.2, 1)), 'plate', sd=6, hd=2.2)
for i, (r0, c, t, w, o) in enumerate((((-0.3, -1.0), (-0.9, -2.0), (-0.2, -3.2), 0.46, 85), ((0.5, -1.0), (0.4, -2.1), (1.4, -3.0), 0.42, 93))):
    h.flat('HornGlow%d' % i, o - 1 if o > 85 else 83, tine(curve(r0[0], r0[1], c[0], c[1], t[0], t[1]), w * 1.7, 0.1, n=18), 'glow', which=0, alpha=0.32, glow=1.8)
    p = h.cel('Horn%d' % i, o, tine(curve(r0[0], r0[1], c[0], c[1], t[0], t[1]), w, 0.0, n=18), 'fire', sd=5, hd=2.4, rim=False, glow=1.7)
    h.flat('HornCore%d' % i, o, tine(curve(r0[0] + 0.05, r0[1] - 0.1, c[0] + 0.05, c[1], t[0] * 0.8 + r0[0] * 0.2, t[1] * 0.8 + r0[1] * 0.2), w * 0.3, 0.0, n=14), 'glow', which=2, glow=2.4)
bowl = h.cel('Bowl', 87, rounded(
    (1.16, -0.34, 1), (1.0, -0.9, 1), (0.5, -1.24, 1), (-0.2, -1.28, 1), (-0.8, -1.0, 1), (-1.14, -0.36, 1), (0.0, -0.5, 1)), 'shell', sd=7, hd=2.6)
bowl.add(band([(0.5, -1.2), (0.3, -0.8), (0.32, -0.46)], 0.035), 'plate', 2)
bowl.add(band([(-0.2, -1.24), (-0.3, -0.8), (-0.4, -0.44)], 0.035), 'plate', 2)
bolt = [(0.2, -1.2), (0.0, -1.7), (0.24, -1.7), (0.0, -2.3), (0.5, -1.62), (0.26, -1.62), (0.44, -1.2)]
h.flat('Bolt', 88, bolt, 'glow', which=1, glow=2.6)
h.cel('Visor', 89, rounded((0.1, -0.52, 1), (1.0, -0.5, 1), (1.62, -0.26, 1), (1.44, -0.2, 1), (0.9, -0.3, 1), (0.1, -0.32, 1)), 'shell', sd=3, hd=1.4)
h.flat('VisorLine', 90, band([(0.24, -0.36), (0.9, -0.36), (1.5, -0.24)], 0.035), 'glow', which=1, glow=2.2)
jaw = h.cel('Jaw', 90, rounded((0.2, 0.32, 1), (1.16, 0.34, 1), (1.04, 0.5, 1), (1.2, 0.64, 1), (0.98, 0.98, 1), (0.5, 1.1, 1), (0.0, 0.96, 1), (-0.3, 0.6, 1)),
             'shell', sd=5, hd=2.0)
jaw.add(band([(0.3, 0.42), (0.7, 0.46), (1.04, 0.42)], 0.03), 'plate', 2)
h.flat('Fangs', 91, [(0.5, 0.66), (0.6, 0.84), (0.7, 0.66), (0.8, 0.86), (0.9, 0.66), (1.0, 0.82), (1.08, 0.64), (0.98, 0.58), (0.56, 0.58)], 'glow', which=1, glow=2.4)
god_eye(h)
god_emblem(h, -0.1, -0.02, 0.2)
cowl(h, 'wrap', top=3, folds=3)
