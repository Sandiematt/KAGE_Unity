"""The helmet art kit (SAN-72 tier redesign): shapes in Kage's head frame, the tier palettes, a preview renderer that paints a
helmet the way FighterRig does (OpponentKit.Cel: seam, shadow, lit edge, base; the world-fixed back-light rim; draw orders), and
the C# export.

Head frame: u in head radii towards the face, v in head radii down, origin at the head centre (reference px 862, 170, radius 57),
the frame the SAN-72 helmet drawings were already authored in. Reference px: x to Kage's back (the reference faces left), y down.
"""
import math, re, os

CX, CY, R = 862.0, 170.0, 57.0

def px(u, v):
    return (CX - u * R, CY + v * R)

def uv(x, y):
    return ((CX - x) / R, (y - CY) / R)

# ------------------------------------------------------------------------------------------------ colours

def hexc(h, a=1.0):
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), int(round(a * 255)))

# Tier palettes. Every colour a helmet paints comes from its tier's palette (HelmetVisualTests checks it): a tone is
# (shade, mid, lit), painted by OpponentKit.Cel; "rim" is the tier's back-light rim on the helmet's outline.
PALETTES = {
    'common': {
        'rim': '#8e8e96',
        'cloth': ('#26262b', '#3f3f46', '#626269'),
        'wrap': ('#34343a', '#55555d', '#7e7e87'),
        'steel': ('#4c4f55', '#868990', '#c4c7cc'),
        'straw': ('#3b3833', '#5e5a52', '#8a857a'),
        'ink': ('#141316', '#1d1c20', '#2a292e'),
        'gauze': ('#6a6a72', '#9a9aa2', '#c6c6cc'),
    },
    'epic': {
        'rim': '#9a62f2',
        'shell': ('#0b0911', '#17131f', '#2a2338'),
        'plate': ('#1a1428', '#2a2140', '#43375e'),
        'blade': ('#3a1c6c', '#7340c8', '#ad7cf6'),
        'wrap': ('#43246f', '#7347b2', '#a479e0'),
        'glow': ('#7a3ad6', '#b47bff', '#ead4ff'),
    },
    'legendary': {
        'rim': '#e8952c',
        'shell': ('#0b0909', '#1a1516', '#352c2e'),
        'gold': ('#7a5418', '#cf9833', '#ffd77d'),
        'red': ('#4c0a0e', '#97181e', '#d8393c'),
        'wrap': ('#550b10', '#9b171d', '#d9403f'),
        'glow': ('#b86a12', '#ffb340', '#fff0c4'),
    },
    'trophy': {
        'rim': '#e3bc55',
        'shell': ('#a8a296', '#e4dfd5', '#ffffff'),
        'gold': ('#86601e', '#d3a43a', '#f8dc86'),
        'ribbon': ('#b3ada1', '#ece8df', '#ffffff'),
        'wrap': ('#aaa498', '#e2ded4', '#fbfaf6'),
        'ink': ('#141316', '#24222a', '#3a3742'),
        'glass': ('#4f8299', '#93c6db', '#e2f8ff'),
        'straw': ('#9c8e70', '#d6c8a4', '#f3e9cc'),
    },
    'god': {
        'rim': '#ff3a2e',
        'shell': ('#060405', '#130e10', '#2b1f23'),
        'plate': ('#1c1215', '#2c1b20', '#4a2c33'),
        'fire': ('#6a0507', '#d2161b', '#ff6a3d'),
        'wrap': ('#5a0709', '#b3141a', '#ee433a'),
        'glow': ('#ff2a1c', '#ff7a3a', '#ffd9a0'),
    },
}

TIER_NAMES = {'common': 'COMMON', 'epic': 'EPIC', 'trophy': 'TROPHY', 'legendary': 'LEGENDARY', 'god': 'GOD MODE'}
TIER_COLOURS = {'common': '#c9c9cf', 'epic': '#b469ff', 'trophy': '#f2c23c', 'legendary': '#ff8a1f', 'god': '#ff2f2f'}
TIER_ORDER = ['common', 'epic', 'trophy', 'legendary', 'god']

SEAM = '#060509'
FACE = '#110d0f'  # Palette.Body: Kage's face, left bare round the eye

# ------------------------------------------------------------------------------------------------ paths (head frame)

class Path:
    """A canvas-style path in the head frame, flattened to points (about one per 4 reference px)."""
    STEP = 4.0 / R

    def __init__(self, u=None, v=None):
        self.p = []
        if u is not None: self.p.append((u, v))

    def to(self, u, v):
        self.p.append((u, v)); return self

    def q(self, cu, cv, u, v):
        u0, v0 = self.p[-1]
        n = max(2, min(48, int(math.ceil((math.hypot(cu - u0, cv - v0) + math.hypot(u - cu, v - cv)) / self.STEP))))
        for i in range(1, n + 1):
            t = i / n
            self.p.append(((1 - t) ** 2 * u0 + 2 * (1 - t) * t * cu + t * t * u, (1 - t) ** 2 * v0 + 2 * (1 - t) * t * cv + t * t * v))
        return self

    def c(self, c1u, c1v, c2u, c2v, u, v):
        u0, v0 = self.p[-1]
        L = math.hypot(c1u - u0, c1v - v0) + math.hypot(c2u - c1u, c2v - c1v) + math.hypot(u - c2u, v - c2v)
        n = max(2, min(64, int(math.ceil(L / self.STEP))))
        for i in range(1, n + 1):
            t = i / n; s = 1 - t
            self.p.append((s ** 3 * u0 + 3 * s * s * t * c1u + 3 * s * t * t * c2u + t ** 3 * u,
                           s ** 3 * v0 + 3 * s * s * t * c1v + 3 * s * t * t * c2v + t ** 3 * v))
        return self

    def arc(self, cu, cv, r, a0, a1):
        """Angles in degrees, v down (increasing angle turns clockwise on screen)."""
        n = max(2, min(64, int(math.ceil(abs(math.radians(a1 - a0)) * r / self.STEP))))
        for i in range(0 if not self.p else 1, n + 1):
            a = math.radians(a0 + (a1 - a0) * i / n)
            self.p.append((cu + math.cos(a) * r, cv + math.sin(a) * r))
        return self

    def pts(self):
        return clean(self.p)


def rounded(*pts):
    """JS orderShape: (u, v) or (u, v, 1) points; a 1 is a sharp corner, others round through the edge midpoints."""
    P = [(p[0], p[1], len(p) > 2 and p[2]) for p in pts]
    n = len(P)
    def X(i): return P[i % n]
    s0 = X(0)
    path = Path()
    if s0[2]: path.to(s0[0], s0[1])
    else: path.to((X(-1)[0] + s0[0]) / 2, (X(-1)[1] + s0[1]) / 2)
    for i in range(1 if s0[2] else 0, n):
        a, b = X(i), X(i + 1)
        if a[2]: path.to(a[0], a[1])
        else: path.q(a[0], a[1], (a[0] + b[0]) / 2, (a[1] + b[1]) / 2)
    return path.pts()


def clean(pts):
    eps = 0.1 / R
    o = []
    for p in pts:
        if not o or math.hypot(p[0] - o[-1][0], p[1] - o[-1][1]) > eps: o.append(p)
    while len(o) > 1 and math.hypot(o[0][0] - o[-1][0], o[0][1] - o[-1][1]) <= eps: o.pop()
    return o


def ellipse(cu, cv, ru, rv, deg=0.0, n=None):
    n = n or max(12, int(2 * math.pi * max(ru, rv) / Path.STEP))
    a = math.radians(deg); ca, sa = math.cos(a), math.sin(a)
    o = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x, y = math.cos(t) * ru, math.sin(t) * rv
        o.append((cu + x * ca - y * sa, cv + x * sa + y * ca))
    return o


def disc(cu, cv, r): return ellipse(cu, cv, r, r)


def sample(path_pts, n):
    """n+1 evenly spaced points along a polyline."""
    cum = [0.0]
    for i in range(1, len(path_pts)):
        cum.append(cum[-1] + math.hypot(path_pts[i][0] - path_pts[i - 1][0], path_pts[i][1] - path_pts[i - 1][1]))
    L = cum[-1]
    o, j = [], 0
    for k in range(n + 1):
        s = L * k / n
        while j < len(cum) - 2 and cum[j + 1] < s: j += 1
        seg = max(1e-9, cum[j + 1] - cum[j])
        t = (s - cum[j]) / seg
        a, b = path_pts[j], path_pts[j + 1]
        o.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return o


def tine(spine, w0, w1, n=16, tip=True, w_mid=None):
    """A tapered strip along a spine polyline (head frame): width w0 at the root, w1 at the end (0 for a point); w_mid swells it."""
    s = sample(spine, n)
    left, right = [], []
    for i, p in enumerate(s):
        a, b = s[max(0, i - 1)], s[min(len(s) - 1, i + 1)]
        d = (b[0] - a[0], b[1] - a[1]); L = math.hypot(*d) or 1
        nrm = (-d[1] / L, d[0] / L)
        t = i / n
        w = w0 + (w1 - w0) * t
        if w_mid is not None: w += (w_mid - (w0 + w1) / 2) * math.sin(math.pi * t)
        w *= 0.5
        left.append((p[0] + nrm[0] * w, p[1] + nrm[1] * w))
        right.append((p[0] - nrm[0] * w, p[1] - nrm[1] * w))
    if tip and w1 <= 1e-6:
        return clean(left[:-1] + [s[-1]] + right[-2::-1])
    return clean(left + right[::-1])


def band(spine, w):
    return tine(spine, w, w, n=max(4, len(spine)), tip=False)


def curve(u0, v0, cu, cv, u1, v1):
    return Path(u0, v0).q(cu, cv, u1, v1).pts()


def ccurve(u0, v0, c1u, c1v, c2u, c2v, u1, v1):
    return Path(u0, v0).c(c1u, c1v, c2u, c2v, u1, v1).pts()


def mirror_v(pts, axis_v):
    return [(p[0], 2 * axis_v - p[1]) for p in pts]


def transform(pts, du=0.0, dv=0.0, k=1.0, deg=0.0, about=(0.0, 0.0)):
    a = math.radians(deg); ca, sa = math.cos(a), math.sin(a)
    o = []
    for (x, y) in pts:
        x, y = x - about[0], y - about[1]
        x, y = x * k, y * k
        o.append((about[0] + x * ca - y * sa + du, about[1] + x * sa + y * ca + dv))
    return o

# ------------------------------------------------------------------------------------------------ the design model

class Piece:
    def __init__(self, name, kind, order, outline, tone, sd=0.0, hd=0.0, rim=True, glow=1.0, bone='Head', chains=None, feel=None,
                 alpha=1.0, seam=1.6, which=None):
        self.name, self.kind, self.order, self.outline = name, kind, order, outline
        self.tone, self.sd, self.hd, self.rim, self.glow, self.bone = tone, sd, hd, rim, glow, bone
        self.chains, self.feel, self.alpha, self.seam, self.which = chains, feel, alpha, seam, which
        self.detail = []   # (outline, tone, which 0/1/2, alpha)

    def add(self, outline, tone, which=1, alpha=1.0):
        self.detail.append((outline, tone, which, alpha)); return self


class Helmet:
    def __init__(self, hid, look, tier, hair, summary):
        self.id, self.look, self.tier, self.hair, self.summary = hid, look, tier, hair, summary
        self.pieces = []

    def _name(self, name):
        """Piece names are part names in the rig: a repeated name gets a number."""
        taken = {p.name for p in self.pieces}
        if name not in taken: return name
        k = 2
        while name + str(k) in taken: k += 1
        return name + str(k)

    def cel(self, name, order, outline, tone, sd=5.0, hd=2.4, rim=True, glow=1.0, seam=1.6):
        p = Piece(self._name(name), 'cel', order, outline, tone, sd, hd, rim, glow, seam=seam); self.pieces.append(p); return p

    def flat(self, name, order, outline, tone, which=1, alpha=1.0, glow=1.0):
        p = Piece(self._name(name), 'flat', order, outline, tone, alpha=alpha, glow=glow, rim=False, which=which); self.pieces.append(p); return p

    def cloth(self, name, order, outline, tone, chains, feel, sd=4.0, hd=2.0, rim=True, glow=1.0):
        p = Piece(self._name(name), 'cloth', order, outline, tone, sd, hd, rim, glow, chains=chains, feel=feel); self.pieces.append(p); return p

    def wrap(self, name, outline, tone, sd=6.0, hd=2.6, folds=()):
        """The tier-coloured neck wrap: on the chest, over the scarf wrap (order 103), outline in head-frame numbers at bind."""
        p = Piece(self._name(name), 'cel', 103, outline, tone, sd, hd, True, bone='Chest')
        for f in folds: p.add(f, tone, 0)
        self.pieces.append(p); return p

    def colours(self):
        pal = PALETTES[self.tier]
        def col(tone, which, alpha=1.0):
            c = pal[tone][which]
            return hexc(c, alpha)
        return pal, col

# ------------------------------------------------------------------------------------------------ cel geometry (ports of OpponentKit / MeshKit)

def signed_area(pts):
    a = 0.0
    for i in range(len(pts)):
        x0, y0 = pts[i]; x1, y1 = pts[(i + 1) % len(pts)]
        a += x0 * y1 - x1 * y0
    return a / 2


def grow(pts, d):
    n = len(pts); sign = 1.0 if signed_area(pts) > 0 else -1.0
    o = []
    for i in range(n):
        p0, p1, p2 = pts[(i - 1) % n], pts[i], pts[(i + 1) % n]
        e0 = norm((p1[0] - p0[0], p1[1] - p0[1])); e1 = norm((p2[0] - p1[0], p2[1] - p1[1]))
        n0 = (e0[1] * sign, -e0[0] * sign); n1 = (e1[1] * sign, -e1[0] * sign)
        m = (n0[0] + n1[0], n0[1] + n1[1]); ml = math.hypot(*m)
        m = n0 if ml < 1e-9 else (m[0] / ml, m[1] / ml)
        cos = max(0.35, m[0] * n0[0] + m[1] * n0[1])
        o.append((p1[0] + m[0] * d / cos, p1[1] + m[1] * d / cos))
    return o


def norm(v):
    L = math.hypot(*v)
    return (0.0, 0.0) if L < 1e-12 else (v[0] / L, v[1] / L)


def depth(pts, i, d):
    n = len(pts); o = pts[i]; best = None
    for k in range(n):
        k1 = (k + 1) % n
        if k == i or k1 == i: continue
        a = pts[k]; e = (pts[k1][0] - a[0], pts[k1][1] - a[1])
        den = d[0] * e[1] - d[1] * e[0]
        if abs(den) < 1e-12: continue
        w = (a[0] - o[0], a[1] - o[1])
        t = (w[0] * e[1] - w[1] * e[0]) / den
        s = (w[0] * d[1] - w[1] * d[0]) / den
        if t > 1e-6 and 0 <= s <= 1 and (best is None or t < best): best = t
    return 0.0 if best is None else best


def inset(pts, sdir, to_shade, to_light):
    n = len(pts); sign = 1.0 if signed_area(pts) > 0 else -1.0
    o = []
    for i in range(n):
        p0, p1, p2 = pts[(i - 1) % n], pts[i], pts[(i + 1) % n]
        e0 = norm((p1[0] - p0[0], p1[1] - p0[1])); e1 = norm((p2[0] - p1[0], p2[1] - p1[1]))
        n0 = (e0[1] * sign, -e0[0] * sign); n1 = (e1[1] * sign, -e1[0] * sign)
        m = (n0[0] + n1[0], n0[1] + n1[1]); ml = math.hypot(*m)
        m = n0 if ml < 1e-10 else (m[0] / ml, m[1] / ml)
        k = m[0] * sdir[0] + m[1] * sdir[1]
        amt = to_shade * max(0, k) + to_light * max(0, -k)
        if amt <= 0: o.append(p1); continue
        amt = min(amt, 0.45 * depth(pts, i, (-m[0], -m[1])))
        o.append((p1[0] - m[0] * amt, p1[1] - m[1] * amt))
    return o

# in reference px the cel shadow falls to the back (+x) and down (+y): OpponentKit.ShadeDir (-0.55, -0.835) in bind space
SHADE_REF = norm((0.55, 0.835))
RIM_SHIFT_REF = (2.2, -2.2)   # FighterRig.lightDir (-0.0055, 0.0055) units, facing right, in reference px
RIM_GROW = 1.8                # PartDef.rimGrow 0.0045 units
SEAM_COL = hexc(SEAM)


def to_ref(pts): return [px(u, v) for (u, v) in pts]


def cel_polys(ref, tone, sd, hd, seam=1.6):
    shade, mid, lit = tone
    out = []
    if seam > 0: out.append((grow(ref, seam), SEAM_COL))
    out.append((ref, shade))
    if hd > 0:
        out.append((inset(ref, SHADE_REF, sd, 0), lit))
        out.append((inset(ref, SHADE_REF, sd, hd), mid))
    else:
        out.append((inset(ref, SHADE_REF, sd, 0), mid))
    return out

# ------------------------------------------------------------------------------------------------ Kage, for the previews

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))


def kage_svg_hair():
    src = open(os.path.join(ROOT, 'Assets', 'Kage', 'Scripts', 'Character', 'KageSvg.cs'), encoding='utf-8').read()
    hair_block = src.split('Hair =')[1].split('};')[0]
    polys, eye = [], None
    for m in re.finditer(r'new SvgPoly\((\d+),(\d+),(\d+),(\d+),(\d+), new float\[\]\{([^}]*)\}\)', hair_block):
        r, g, b, role = int(m.group(1)), int(m.group(2)), int(m.group(3)), int(m.group(4))
        xy = [float(t.rstrip('f')) for t in m.group(6).split(',')]
        pts = [(xy[i], xy[i + 1]) for i in range(0, len(xy), 2)]
        if role == 6: eye = pts; continue
        if role >= 4:
            tb = (0xF0, 0x6F, 0x6E)
            r, g, b = [int(round(c + (t - c) * 0.18)) for c, t in zip((r, g, b), tb)]
        polys.append((pts, (r, g, b, 255)))
    return polys, eye


HEAD_PLAIN = [(813, 146), (805, 160), (806, 178), (799, 191), (806, 197), (809, 205), (818, 214), (832, 223), (852, 225),
              (872, 216), (890, 198), (899, 172), (896, 146), (882, 126), (858, 116), (833, 121)]

BODY = hexc('#110D0F'); RIM = hexc('#E65C5E'); TRIM = hexc('#D5444B'); TRIM_DEEP = hexc('#731F23'); TRIM_BRIGHT = hexc('#F06F6E')

def V(*xy): return [(xy[i], xy[i + 1]) for i in range(0, len(xy), 2)]

# (name, order, pts, colour, rim colour or None) of the Kage parts round the head, at bind
KAGE_CONTEXT = [
    ('Torso', 44, V(834,226, 870,214, 908,220, 932,240, 946,268, 950,310, 951,355, 953,425, 880,428, 808,425, 807,360, 812,322, 818,290, 824,260), BODY, RIM),
    ('Lapel', 48, V(838,238, 850,236, 860,300, 862,398, 850,398, 846,300), TRIM, None),
    ('Collar', 49, V(822,250, 842,232, 853,262, 838,292), TRIM_DEEP, None),
    ('Neck', 80, V(838,200, 868,200, 872,240, 842,240), BODY, None),
    ('Scarf_Wrap', 100, V(818,232, 836,221, 860,213, 890,207, 920,203, 946,205, 958,217, 955,236, 940,244, 912,240, 884,242, 856,248, 834,254, 820,250), TRIM, TRIM_BRIGHT),
    ('Scarf_Fold', 101, V(830,240, 858,232, 890,226, 925,222, 948,224, 946,231, 924,229, 890,233, 858,239, 832,247), TRIM_DEEP, None),
    ('Scarf_Knot', 102, V(942,204, 960,207, 968,220, 962,236, 948,234, 944,220), TRIM, None),
    ('Scarf_KnotShade', 103, V(950,214, 962,218, 960,230, 952,228), TRIM_DEEP, None),
    ('ArmN_Upper', 104, V(884,258, 924,248, 944,286, 950,334, 950,372, 904,380, 897,334, 888,296), BODY, RIM),
]

# ------------------------------------------------------------------------------------------------ preview renderer

def draw_list(helmet, bare=False, scarf_tails=True):
    """Everything to paint, as (sort key, polygon in ref px, rgba), in FighterRig's sorting: a part's main mesh at order * 3 + 2,
    its rim at (order - order % 4) * 3."""
    items = []
    seq = [0]
    def add(sort, pts, col):
        seq[0] += 1
        items.append((sort, seq[0], pts, col))
    hair, eye = kage_svg_hair()
    for name, order, pts, col, rim in KAGE_CONTEXT:
        if rim: add((order - order % 4) * 3, [(x + RIM_SHIFT_REF[0], y + RIM_SHIFT_REF[1]) for (x, y) in grow(pts, RIM_GROW)], rim)
        add(order * 3 + 2, pts, col)
    if scarf_tails:
        # the scarf ribbons stream back behind everything (ScarfRibbon at base order + 3)
        add(-1, [(944, 210), (1010, 214), (1080, 232), (1120, 250), (1080, 246), (1010, 236), (950, 232)], TRIM_DEEP)
        add(0, [(946, 214), (1000, 222), (1060, 246), (1100, 268), (1060, 262), (1000, 244), (948, 230)], TRIM)
    hide = helmet is not None and helmet.hair == 'hidden' and not bare
    if hide:
        add(84 * 3, [(x + RIM_SHIFT_REF[0], y + RIM_SHIFT_REF[1]) for (x, y) in grow(HEAD_PLAIN, RIM_GROW)], RIM)
        add(84 * 3 + 2, HEAD_PLAIN, BODY)
    else:
        for pts, col in hair: add(84 * 3 + 2, pts, col)
    add(96 * 3 + 2, eye, hexc('#F4F1EE'))
    if helmet is not None and not bare:
        pal = PALETTES[helmet.tier]
        rimc = hexc(pal['rim'])
        for p in helmet.pieces:
            ref = to_ref(p.outline)
            o = p.order
            tone = tuple(hexc(c) for c in pal[p.tone]) if p.tone in pal else None
            if p.kind == 'flat':
                c = pal[p.tone][p.which if p.which is not None else 1] if p.tone in pal else p.tone
                add(o * 3 + 2, ref, hexc(c, p.alpha))
                continue
            if p.kind == 'cel' and p.rim:
                add((o - o % 4) * 3, [(x + RIM_SHIFT_REF[0], y + RIM_SHIFT_REF[1]) for (x, y) in grow(ref, RIM_GROW)], rimc)
            if p.kind == 'cloth' and p.rim:
                add(o * 3 + 2, [(x + RIM_SHIFT_REF[0], y + RIM_SHIFT_REF[1]) for (x, y) in grow(ref, RIM_GROW)], rimc)
            for poly, col in cel_polys(ref, tone, p.sd, p.hd, p.seam):
                add(o * 3 + 2, poly, col)
            for d, dt, which, alpha in p.detail:
                add(o * 3 + 2, to_ref(d), hexc(pal[dt][which], alpha))
    items.sort(key=lambda t: (t[0], t[1]))
    return items


def render(helmet, centre_uv=(0.0, 0.05), half=1.9, size=400, ss=3, bg=(42, 34, 37), bare=False, mirror=True, scarf_tails=True):
    """The helmet on Kage's head at bind, facing right (the game's facing), as a PIL image."""
    from PIL import Image, ImageDraw
    S = size * ss
    img = Image.new('RGBA', (S, S), bg + (255,))
    cx, cy = px(*centre_uv)
    scale = S / (2 * half * R)
    def tr(p):
        x = (cx - p[0]) if mirror else (p[0] - cx)
        return (S / 2 + x * scale, S / 2 + (p[1] - cy) * scale)
    for _, _, pts, col in draw_list(helmet, bare, scarf_tails):
        if len(pts) < 3: continue
        q = [tr(p) for p in pts]
        if col[3] >= 255:
            ImageDraw.Draw(img).polygon(q, fill=col)
        else:
            layer = Image.new('RGBA', (S, S), (0, 0, 0, 0))
            ImageDraw.Draw(layer).polygon(q, fill=col)
            img = Image.alpha_composite(img, layer)
    return img.resize((size, size), Image.LANCZOS)

def _cross(o, a, b):
    return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])


def _hits(a, b, c, d):
    d1, d2, d3, d4 = _cross(c, d, a), _cross(c, d, b), _cross(a, b, c), _cross(a, b, d)
    return ((d1 > 0) != (d2 > 0)) and ((d3 > 0) != (d4 > 0)) and abs(d1) > 1e-12 and abs(d2) > 1e-12 and abs(d3) > 1e-12 and abs(d4) > 1e-12


def problem(pts):
    """Why an outline would not triangulate (None when it is fine): too few points, no area, or edges that cross."""
    n = len(pts)
    if n < 3: return 'fewer than 3 points'
    if abs(signed_area(pts)) * R * R < 4.0: return 'area under 4 px2'
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        for j in range(i + 2, n):
            if i == 0 and j == n - 1: continue
            c, d = pts[j], pts[(j + 1) % n]
            if _hits(a, b, c, d): return 'edges %d and %d cross' % (i, j)
    return None

def triangulate(pts):
    """MeshKit.Triangulate (ear clipping), to check an outline before Unity sees it."""
    n = len(pts)
    if n < 3: return []
    ccw = signed_area(pts) > 0
    idx = list(range(n)) if ccw else list(range(n - 1, -1, -1))
    tris = []
    def cr(a, b): return a[0] * b[1] - a[1] * b[0]
    def intri(p, a, b, c):
        d1 = cr((b[0] - a[0], b[1] - a[1]), (p[0] - a[0], p[1] - a[1])); d2 = cr((c[0] - b[0], c[1] - b[1]), (p[0] - b[0], p[1] - b[1]))
        d3 = cr((a[0] - c[0], a[1] - c[1]), (p[0] - c[0], p[1] - c[1]))
        neg = d1 < 0 or d2 < 0 or d3 < 0; pos = d1 > 0 or d2 > 0 or d3 > 0
        return not (neg and pos)
    guard = 0
    while len(idx) > 3 and guard < 4000:
        guard += 1
        clipped = False
        for i in range(len(idx)):
            ia, ib, ic = idx[(i - 1) % len(idx)], idx[i], idx[(i + 1) % len(idx)]
            a, b, c = pts[ia], pts[ib], pts[ic]
            if cr((b[0] - a[0], b[1] - a[1]), (c[0] - b[0], c[1] - b[1])) <= 1e-9: continue
            if any(intri(pts[q], a, b, c) for q in idx if q not in (ia, ib, ic)): continue
            tris.append((ia, ib, ic)); idx.pop(i); clipped = True; break
        if not clipped: break
    for i in range(1, len(idx) - 1): tris.append((idx[0], idx[i], idx[i + 1]))
    return tris


def tiles(ref):
    """True when MeshKit's triangulation of a ref-px outline covers exactly its area."""
    tris = triangulate(ref)
    A = abs(signed_area(ref))
    S = sum(abs(signed_area([ref[a], ref[b], ref[c]])) for a, b, c in tris)
    return abs(A - S) <= A * 0.01 + 0.5, A, S

# ------------------------------------------------------------------------------------------------ C# export

def cs_floats(ref):
    return 'new float[] { ' + ', '.join(f'{x:.1f}f, {y:.1f}f' for (x, y) in ref) + ' }'


def export_cs(helmets, path):
    tiers = {'common': 'Common', 'epic': 'Epic', 'trophy': 'Trophy', 'legendary': 'Legendary', 'god': 'GodMode'}
    hairs = {'hidden': 'Hidden', 'kept': 'Kept'}
    L = []
    L.append('// GENERATED by Tools/helmet_art/build.py from Tools/helmet_art/helmets.py (SAN-72 tier redesign). Do not edit by hand:')
    L.append('// change the design there, run `python Tools/helmet_art/build.py export`, and re-render the tiles (HelmetTierScreenshots).')
    L.append('// Outlines and chains are in reference px (KageModel.P); tones name an entry of the tier palette (HelmetTiers.Palette).')
    L.append('// The design notes behind each helmet: docs/research/helmet-design.md.')
    L.append('namespace Kage')
    L.append('{')
    L.append('    public static partial class HelmetDesigns')
    L.append('    {')
    L.append('        static readonly HelmetDesign[] Generated =')
    L.append('        {')
    for h in helmets:
        L.append(f'            // {h.id} ({h.tier}): {h.summary}')
        L.append(f'            new HelmetDesign("{h.id}", "{h.look}", HelmetTier.{tiers[h.tier]}, HairUnderHelmet.{hairs[h.hair]}, new[]')
        L.append('            {')
        for p in h.pieces:
            ref = to_ref(p.outline)
            kind = {'cel': 'Cel', 'flat': 'Flat', 'cloth': 'Cloth'}[p.kind]
            parts = [f'name = "{p.name}"', f'kind = PieceKind.{kind}', f'bone = B.{p.bone}', f'order = {p.order}', f'tone = "{p.tone}"']
            if p.kind == 'flat': parts.append(f'which = {p.which if p.which is not None else 1}')
            if p.alpha != 1.0: parts.append(f'alpha = {p.alpha:.3f}f')
            if p.kind != 'flat':
                parts += [f'sd = {p.sd:.2f}f', f'hd = {p.hd:.2f}f', f'rim = {"true" if p.rim else "false"}']
                if p.seam != 1.6: parts.append(f'seam = {p.seam:.2f}f')
            if p.glow != 1.0: parts.append(f'glow = {p.glow:.2f}f')
            if p.feel: parts.append(f'feel = "{p.feel}"')
            parts.append('outline = ' + cs_floats(ref))
            if p.chains:
                parts.append('chains = new[] { ' + ', '.join(cs_floats(to_ref(c)) for c in p.chains) + ' }')
            if p.detail:
                ds = []
                for d, dt, which, alpha in p.detail:
                    ds.append(f'new HelmetDetail("{dt}", {which}, {alpha:.3f}f, {cs_floats(to_ref(d))})')
                parts.append('detail = new[] { ' + ', '.join(ds) + ' }')
            L.append('                new HelmetPiece { ' + ', '.join(parts) + ' },')
        L.append('            }),')
    L.append('        };')
    L.append('    }')
    L.append('')
    L.append('    public static partial class HelmetTiers')
    L.append('    {')
    L.append('        static readonly System.Collections.Generic.Dictionary<HelmetTier, HelmetPalette> Palettes = new()')
    L.append('        {')
    for t in TIER_ORDER:
        pal = PALETTES[t]
        L.append(f'            [HelmetTier.{tiers[t]}] = new HelmetPalette("{pal["rim"]}", new()')
        L.append('            {')
        for k, v in pal.items():
            if k == 'rim': continue
            L.append(f'                ["{k}"] = new[] {{ "{v[0]}", "{v[1]}", "{v[2]}" }},')
        L.append('            }),')
    L.append('        };')
    L.append('    }')
    L.append('}')
    open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(L) + '\n')
