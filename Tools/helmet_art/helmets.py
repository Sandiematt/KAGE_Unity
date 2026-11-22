"""The SAN-72 helmet designs, by tier (docs/research/helmet-design.md). Head frame: u head radii to the face, v down, origin at the
head centre. Kage's head runs from u -0.65 (back) to 1.1 (nose), v -0.95 (crown) to 0.96 (chin); his eye is u 0.30..0.60,
v -0.12..0.21 and must stay on bare black face. Orders: 83 soft pieces behind the head, 85..95 on the head (over the hair at 84,
under the eye at 96), 103 the neck wrap on the chest (over the scarf wrap, under the near arm).
"""
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

def tail(h, name, root, mid, end, w0, w1, tone, feel='Strip', order=83, w_mid=None, sd=3.0, hd=1.6):
    spine = curve(root[0], root[1], mid[0], mid[1], end[0], end[1])
    out = tine(spine, w0, w1, n=18, w_mid=w_mid)
    chain = sample(spine, 5)
    return h.cloth(name, order, out, tone, [chain], feel, sd, hd)

def cowl(h, tone, top=0.0, folds=2, lift_front=0.0, back=940):
    """The tier-coloured neck wrap over the scarf wrap's front (its crimson knot and tails still show behind): two wound bands,
    the upper one tucked under the jaw, stepping out at the front where they overlap."""
    t = top
    pts = ref_pts(811, 241, 816, 233, 822, 226 - lift_front, 838, 214 - t, 866, 205 - t, 900, 199 - t, back - 8, 197 - t, back, 204 - t,
                  back + 2, 222, back - 4, 238, 920, 246, 884, 247, 852, 254, 828, 260, 814, 256)
    sharp = {0, 1, 14}
    out = rounded(*[(p[0], p[1], 1) if i in sharp else p for i, p in enumerate(pts)])
    fl = [band(curve(*U(814, 238), *U(866, 224), *U(back, 214)), 0.075)]
    if folds >= 2: fl.append(band(curve(*U(826, 250), *U(880, 236), *U(back - 2, 230)), 0.06))
    if folds >= 3: fl.append(band(curve(*U(846, 214 - t * 0.6), *U(890, 206 - t * 0.6), *U(back - 6, 204 - t * 0.6)), 0.05))
    return h.wrap('Wrap', out, tone, folds=fl)

# ================================================================================================ COMMON
# Grey cloth and plain steel: matte, low, close to the skull; one soft accent (knot tails, ties); no glow.

h = helmet('kuro_zukin', 'ash_hood', 'common', 'hidden',
           'Ash-grey shinobi hood wrapped to the jaw with an eye slit, a cord knotted at the back with two trailing tails, grey neck wrap.')
hood = h.cel('Hood', 86, rounded(
    (1.08, -0.27, 1), (0.42, -0.25), (0.16, -0.22, 1), (0.10, 0.04), (0.18, 0.29, 1), (0.66, 0.31), (1.12, 0.30, 1),
    (1.06, 0.62), (0.82, 0.92), (0.42, 1.06), (-0.22, 0.98), (-0.95, 0.66), (-1.18, -0.05), (-0.92, -0.86), (-0.12, -1.24),
    (0.66, -1.1), (1.04, -0.64)), 'cloth', sd=7, hd=2.6)
hood.add(band(curve(0.5, -1.02, -0.2, -0.95, -0.72, -0.42), 0.05), 'cloth', 2)
hood.add(band(curve(-0.35, 0.42, -0.62, 0.2, -0.86, -0.32), 0.05), 'cloth', 0)
hood.add(band(curve(0.25, 0.55, 0.6, 0.62, 0.98, 0.5), 0.05), 'cloth', 0)
hood.add(band(curve(0.1, 0.78, 0.5, 0.86, 0.86, 0.78), 0.04), 'cloth', 2)
cord = h.cel('Cord', 88, band(curve(1.03, -0.56, 0.0, -0.84, -1.06, -0.44), 0.13), 'wrap', sd=1.5, hd=1.0, rim=False)
h.cel('Knot', 89, rounded((-0.92, -0.62), (-1.24, -0.68), (-1.32, -0.36), (-1.04, -0.24)), 'wrap', sd=3, hd=1.4)
tail(h, 'TailA', (-1.16, -0.5), (-1.9, -0.78), (-2.7, -0.52), 0.24, 0.06, 'wrap', w_mid=0.27)
tail(h, 'TailB', (-1.12, -0.36), (-1.8, -0.24), (-2.45, 0.12), 0.22, 0.05, 'wrap', w_mid=0.25)
cowl(h, 'wrap', top=3, folds=3)
