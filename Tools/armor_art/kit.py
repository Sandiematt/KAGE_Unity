"""The armor art kit (5-tier armor redesign): shapes in Kage's reference-pixel frame, per-armor cel palettes,
a full-body preview renderer that paints an armor the way FighterRig does (OpponentKit.Cel: seam, shadow, lit edge,
base; the world-fixed back-light rim; draw orders), and the C# export to ArmorDesigns.g.cs.

Reference pixel frame (same as KageModel.cs):
  x increases toward Kage's back (Kage faces left in ref px; KageModel.P mirrors to face right in Unity).
  y increases downward (ground y = 815, head ~116..225, chest ~248..395, belt ~366..397, hips ~398..545, knees ~680, feet ~815).
"""
import math, re, os

SEAM = '#060509'

# ------------------------------------------------------------------------------------------------ colours

def hexc(h, a=1.0):
    h = h.lstrip('#')
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), int(round(a * 255)))


def mix_hex(h0, h1, t):
    c0, c1 = hexc(h0), hexc(h1)
    r = int(round(c0[0] + (c1[0] - c0[0]) * t))
    g = int(round(c0[1] + (c1[1] - c0[1]) * t))
    b = int(round(c0[2] + (c1[2] - c0[2]) * t))
    return f'#{r:02x}{g:02x}{b:02x}'

# ------------------------------------------------------------------------------------------------ geometry (ref px)

class Path:
    """A canvas-style path in reference pixels, flattened to points (~1 point per 4.5 ref px)."""
    STEP = 4.5

    def __init__(self, x=None, y=None):
        self.p = []
        if x is not None:
            self.p.append((float(x), float(y)))

    def to(self, x, y):
        self.p.append((float(x), float(y)))
        return self

    def q(self, cx, cy, x, y):
        x0, y0 = self.p[-1]
        L = math.hypot(cx - x0, cy - y0) + math.hypot(x - cx, y - cy)
        n = max(2, min(48, int(math.ceil(L / self.STEP))))
        for i in range(1, n + 1):
            t = i / n
            s = 1.0 - t
            self.p.append((s * s * x0 + 2 * s * t * cx + t * t * x,
                           s * s * y0 + 2 * s * t * cy + t * t * y))
        return self

    def c(self, c1x, c1y, c2x, c2y, x, y):
        x0, y0 = self.p[-1]
        L = math.hypot(c1x - x0, c1y - y0) + math.hypot(c2x - c1x, c2y - c1y) + math.hypot(x - c2x, y - c2y)
        n = max(2, min(64, int(math.ceil(L / self.STEP))))
        for i in range(1, n + 1):
            t = i / n
            s = 1.0 - t
            self.p.append((s ** 3 * x0 + 3 * s * s * t * c1x + 3 * s * t * t * c2x + t ** 3 * x,
                           s ** 3 * y0 + 3 * s * s * t * c1y + 3 * s * t * t * c2y + t ** 3 * y))
        return self

    def pts(self):
        return clean(self.p)


def clean(pts):
    eps = 0.25
    o = []
    for p in pts:
        if not o or math.hypot(p[0] - o[-1][0], p[1] - o[-1][1]) > eps:
            o.append((float(p[0]), float(p[1])))
    while len(o) > 1 and math.hypot(o[0][0] - o[-1][0], o[0][1] - o[-1][1]) <= eps:
        o.pop()
    return o


def rounded(*pts):
    """Smooth closed shape: (x, y) rounds through edge midpoints; (x, y, 1) keeps a crisp sharp corner."""
    P = [(float(p[0]), float(p[1]), len(p) > 2 and bool(p[2])) for p in pts]
    n = len(P)
    def X(i): return P[i % n]
    s0 = X(0)
    path = Path()
    if s0[2]:
        path.to(s0[0], s0[1])
    else:
        path.to((X(-1)[0] + s0[0]) * 0.5, (X(-1)[1] + s0[1]) * 0.5)
    for i in range(1 if s0[2] else 0, n):
        a, b = X(i), X(i + 1)
        if a[2]:
            path.to(a[0], a[1])
        else:
            path.q(a[0], a[1], (a[0] + b[0]) * 0.5, (a[1] + b[1]) * 0.5)
    return path.pts()


def poly(*xy):
    return clean([(xy[i], xy[i + 1]) for i in range(0, len(xy), 2)])


def box(x0, y0, x1, y1, r=0.0):
    if r <= 0.0:
        return clean([(x0, y0), (x1, y0), (x1, y1), (x0, y1)])
    return rounded(
        (x0 + r, y0, 1), (x1 - r, y0, 1), (x1, y0), (x1, y0 + r, 1),
        (x1, y1 - r, 1), (x1, y1), (x1 - r, y1, 1), (x0 + r, y1, 1),
        (x0, y1), (x0, y1 - r, 1), (x0, y0 + r, 1), (x0, y0)
    )


def ellipse(cx, cy, rx, ry, deg=0.0, n=None):
    n = n or max(12, int(2 * math.pi * max(rx, ry) / Path.STEP))
    a = math.radians(deg)
    ca, sa = math.cos(a), math.sin(a)
    o = []
    for i in range(n):
        t = 2 * math.pi * i / n
        x, y = math.cos(t) * rx, math.sin(t) * ry
        o.append((cx + x * ca - y * sa, cy + x * sa + y * ca))
    return clean(o)


def disc(cx, cy, r, n=None):
    return ellipse(cx, cy, r, r, 0.0, n or max(10, int(2 * math.pi * r / 3.5)))


def diamond(cx, cy, rx, ry):
    return clean([(cx, cy - ry), (cx + rx, cy), (cx, cy + ry), (cx - rx, cy)])


def star(cx, cy, ro, ri, n=4, rot=-90.0):
    o = []
    for i in range(2 * n):
        a = math.radians(rot + i * 180.0 / n)
        r = ro if i % 2 == 0 else ri
        o.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
    return clean(o)


def ring(cx, cy, ro, ri, a0=0.0, a1=360.0, n=36):
    if abs(a1 - a0) >= 359.9:
        a0, a1 = a0 + 0.6, a0 + 359.4
    outer = [(cx + math.cos(math.radians(a0 + (a1 - a0) * i / n)) * ro,
              cy + math.sin(math.radians(a0 + (a1 - a0) * i / n)) * ro) for i in range(n + 1)]
    inner = [(cx + math.cos(math.radians(a1 - (a1 - a0) * i / n)) * ri,
              cy + math.sin(math.radians(a1 - (a1 - a0) * i / n)) * ri) for i in range(n + 1)]
    return clean(outer + inner)


def curve(x0, y0, cx, cy, x1, y1):
    return Path(x0, y0).q(cx, cy, x1, y1).pts()


def ccurve(x0, y0, c1x, c1y, c2x, c2y, x1, y1):
    return Path(x0, y0).c(c1x, c1y, c2x, c2y, x1, y1).pts()


def sample(path_pts, n):
    cum = [0.0]
    for i in range(1, len(path_pts)):
        cum.append(cum[-1] + math.hypot(path_pts[i][0] - path_pts[i - 1][0], path_pts[i][1] - path_pts[i - 1][1]))
    L = cum[-1]
    if L < 1e-6:
        return [path_pts[0]] * (n + 1)
    o, j = [], 0
    for k in range(n + 1):
        s = L * k / n
        while j < len(cum) - 2 and cum[j + 1] < s:
            j += 1
        seg = max(1e-9, cum[j + 1] - cum[j])
        t = (s - cum[j]) / seg
        a, b = path_pts[j], path_pts[j + 1]
        o.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return o


def tine(spine, w0, w1, n=14, tip=True, w_mid=None):
    s = sample(spine, n)
    left, right = [], []
    for i, p in enumerate(s):
        a, b = s[max(0, i - 1)], s[min(len(s) - 1, i + 1)]
        d = (b[0] - a[0], b[1] - a[1])
        L = math.hypot(*d) or 1.0
        nrm = (-d[1] / L, d[0] / L)
        t = i / n
        w = w0 + (w1 - w0) * t
        if w_mid is not None:
            w += (w_mid - (w0 + w1) * 0.5) * math.sin(math.pi * t)
        w *= 0.5
        left.append((p[0] + nrm[0] * w, p[1] + nrm[1] * w))
        right.append((p[0] - nrm[0] * w, p[1] - nrm[1] * w))
    if tip and w1 <= 1e-6:
        return clean(left[:-1] + [s[-1]] + right[-2::-1])
    return clean(left + right[::-1])


def band(spine, w):
    return tine(spine, w, w, n=max(4, len(spine)), tip=False)


def ribbon_strip(p0, p1, w):
    return band([p0, p1], w)


def plate_lame(x0, y0, x1, y1, h, bow=4.0, flare=2.0):
    """A curved samurai armor lame (horizontal plate strip) from (x0,y0) to (x1,y1) of height h."""
    mx, my = (x0 + x1) * 0.5, (y0 + y1) * 0.5
    top = curve(x0, y0, mx, my - bow, x1, y1)
    bot = curve(x1 + flare, y1 + h, mx, my + h + bow * 0.65, x0 - flare, y0 + h)
    return clean(top + bot)


def lame_lacing(x0, y0, x1, y1, h, count=4, w=2.2):
    """Vertical odoshi silk lacing pairs across a lame."""
    out = []
    for i in range(count):
        t = (i + 0.5) / count
        tx = x0 + (x1 - x0) * t
        ty = y0 + (y1 - y0) * t
        out.append(band([(tx, ty + 1.5), (tx, ty + h - 1.5)], w))
    return out


# ------------------------------------------------------------------------------------------------ design model

class Piece:
    def __init__(self, name, kind, bone, order, outline, tone, sd=0.0, hd=0.0, rim=True, glow=1.0,
                 chains=None, feel=None, alpha=1.0, seam=1.6, which=1, sword_layer=False):
        self.name = name
        self.kind = kind
        self.bone = bone
        self.order = order
        self.outline = clean(outline)
        self.tone = tone
        self.sd = sd
        self.hd = hd
        self.rim = rim
        self.glow = glow
        self.chains = chains
        self.feel = feel
        self.alpha = alpha
        self.seam = seam
        self.which = which
        self.sword_layer = sword_layer
        self.detail = []   # (outline, tone, which 0/1/2, alpha)

    def add(self, outline, tone, which=1, alpha=1.0):
        c = clean(outline)
        if len(c) >= 3:
            self.detail.append((c, tone, which, alpha))
        return self


class Armor:
    def __init__(self, aid, look, tier, summary, palette, retones_coat=True, aura='none'):
        self.id = aid
        self.look = look
        self.tier = tier          # common, rare, epic, trophy, legendary
        self.summary = summary
        self.palette = palette    # dict: 'rim': '#...', tone_name: (shade, mid, lit)
        self.retones_coat = retones_coat
        self.aura = aura          # none, spirit, void
        self.pieces = []

    def _name(self, name):
        taken = {p.name for p in self.pieces}
        if name not in taken:
            return name
        k = 2
        while name + str(k) in taken:
            k += 1
        return name + str(k)

    def cel(self, name, bone, order, outline, tone, sd=4.8, hd=2.2, rim=True, glow=1.0, seam=1.7, sword_layer=False):
        p = Piece(self._name(name), 'cel', bone, order, outline, tone, sd, hd, rim, glow,
                  seam=seam, sword_layer=sword_layer)
        self.pieces.append(p)
        return p

    def flat(self, name, bone, order, outline, tone, which=1, alpha=1.0, glow=1.0, sword_layer=False):
        p = Piece(self._name(name), 'flat', bone, order, outline, tone, alpha=alpha, glow=glow,
                  rim=False, which=which, sword_layer=sword_layer)
        self.pieces.append(p)
        return p

    def cloth(self, name, bone, order, outline, tone, chains, feel, sd=4.2, hd=2.0, rim=True, glow=1.0, seam=1.7):
        p = Piece(self._name(name), 'cloth', bone, order, outline, tone, sd, hd, rim, glow,
                  chains=chains, feel=feel, seam=seam)
        self.pieces.append(p)
        return p


# ------------------------------------------------------------------------------------------------ cel geometry

def signed_area(pts):
    a = 0.0
    for i in range(len(pts)):
        x0, y0 = pts[i]
        x1, y1 = pts[(i + 1) % len(pts)]
        a += x0 * y1 - x1 * y0
    return a * 0.5


def norm(v):
    L = math.hypot(*v)
    return (0.0, 0.0) if L < 1e-12 else (v[0] / L, v[1] / L)


def grow(pts, d):
    n = len(pts)
    sign = 1.0 if signed_area(pts) > 0 else -1.0
    o = []
    for i in range(n):
        p0, p1, p2 = pts[(i - 1) % n], pts[i], pts[(i + 1) % n]
        e0 = norm((p1[0] - p0[0], p1[1] - p0[1]))
        e1 = norm((p2[0] - p1[0], p2[1] - p1[1]))
        n0 = (e0[1] * sign, -e0[0] * sign)
        n1 = (e1[1] * sign, -e1[0] * sign)
        m = (n0[0] + n1[0], n0[1] + n1[1])
        ml = math.hypot(*m)
        m = n0 if ml < 1e-9 else (m[0] / ml, m[1] / ml)
        cos = max(0.35, m[0] * n0[0] + m[1] * n0[1])
        o.append((p1[0] + m[0] * d / cos, p1[1] + m[1] * d / cos))
    return o


def depth(pts, i, d):
    n = len(pts)
    o = pts[i]
    best = None
    for k in range(n):
        k1 = (k + 1) % n
        if k == i or k1 == i:
            continue
        a = pts[k]
        e = (pts[k1][0] - a[0], pts[k1][1] - a[1])
        den = d[0] * e[1] - d[1] * e[0]
        if abs(den) < 1e-12:
            continue
        w = (a[0] - o[0], a[1] - o[1])
        t = (w[0] * e[1] - w[1] * e[0]) / den
        s = (w[0] * d[1] - w[1] * d[0]) / den
        if t > 1e-6 and 0 <= s <= 1 and (best is None or t < best):
            best = t
    return 0.0 if best is None else best


def inset(pts, sdir, to_shade, to_light):
    n = len(pts)
    sign = 1.0 if signed_area(pts) > 0 else -1.0
    o = []
    for i in range(n):
        p0, p1, p2 = pts[(i - 1) % n], pts[i], pts[(i + 1) % n]
        e0 = norm((p1[0] - p0[0], p1[1] - p0[1]))
        e1 = norm((p2[0] - p1[0], p2[1] - p1[1]))
        n0 = (e0[1] * sign, -e0[0] * sign)
        n1 = (e1[1] * sign, -e1[0] * sign)
        m = (n0[0] + n1[0], n0[1] + n1[1])
        ml = math.hypot(*m)
        m = n0 if ml < 1e-10 else (m[0] / ml, m[1] / ml)
        k = m[0] * sdir[0] + m[1] * sdir[1]
        amt = to_shade * max(0, k) + to_light * max(0, -k)
        if amt <= 0:
            o.append(p1)
            continue
        amt = min(amt, 0.45 * depth(pts, i, (-m[0], -m[1])))
        o.append((p1[0] - m[0] * amt, p1[1] - m[1] * amt))
    return o


SHADE_REF = norm((0.55, 0.835))
RIM_SHIFT_REF = (2.2, -2.2)
RIM_GROW = 1.8
SEAM_COL = hexc(SEAM)


def cel_polys(ref, tone, sd, hd, seam=1.6):
    shade, mid, lit = tone
    out = []
    if seam > 0:
        out.append((grow(ref, seam), SEAM_COL))
    out.append((ref, shade))
    if hd > 0:
        out.append((inset(ref, SHADE_REF, sd, 0), lit))
        out.append((inset(ref, SHADE_REF, sd, hd), mid))
    else:
        out.append((inset(ref, SHADE_REF, sd, 0), mid))
    return out


# ------------------------------------------------------------------------------------------------ Kage full-body preview

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, '..', '..'))


def _kage_svg_hair():
    src = open(os.path.join(ROOT, 'Assets', 'Kage', 'Scripts', 'Character', 'KageSvg.cs'), encoding='utf-8').read()
    hair_block = src.split('Hair =')[1].split('};')[0]
    polys, eye = [], None
    for m in re.finditer(r'new SvgPoly\((\d+),(\d+),(\d+),(\d+),(\d+), new float\[\]\{([^}]*)\}\)', hair_block):
        r, g, b, role = int(m.group(1)), int(m.group(2)), int(m.group(3)), int(m.group(4))
        xy = [float(t.rstrip('f')) for t in m.group(6).split(',')]
        pts = [(xy[i], xy[i + 1]) for i in range(0, len(xy), 2)]
        if role == 6:
            eye = pts
            continue
        if role >= 4:
            tb = (0xF0, 0x6F, 0x6E)
            r, g, b = [int(round(c + (t - c) * 0.18)) for c, t in zip((r, g, b), tb)]
        polys.append((pts, (r, g, b, 255)))
    return polys, eye


_HAIR_CACHE = None

def kage_svg_hair():
    global _HAIR_CACHE
    if _HAIR_CACHE is None:
        _HAIR_CACHE = _kage_svg_hair()
    return _HAIR_CACHE


def V(*xy):
    return [(xy[i], xy[i + 1]) for i in range(0, len(xy), 2)]


BODY = hexc('#110D0F')
FAR = hexc('#0B080A')
RIM = hexc('#E65C5E')
TRIM = hexc('#D5444B')
TRIM_DEEP = hexc('#731F23')
TRIM_BRIGHT = hexc('#F06F6E')
COAT_MAIN = hexc('#1A161B')
COAT_SIDE = hexc('#151116')
COAT_BACK = hexc('#120A0C')
SILVER = hexc('#B8BDC6')

# Kage's full base body parts at bind pose (matching KageModel.cs orders)
KAGE_BODY = [
    ('ArmF_Upper', 4, V(845,262, 882,268, 866,330, 842,392, 806,382, 826,322), FAR, RIM),
    ('ArmF_Fore', 8, V(806,382, 842,390, 816,440, 806,462, 786,454, 796,420), FAR, RIM),
    ('HandF', 12, V(780,450, 810,452, 814,478, 802,494, 780,490, 772,470), FAR, RIM),
    ('Coat_Back', 19, V(870,398, 953,398, 976,450, 1004,522, 1034,600, 1060,672, 1082,726, 1046,734, 1000,746, 950,750, 906,744, 890,660, 880,560, 874,470), COAT_BACK, RIM),
    ('LegF_Thigh', 20, V(875,540, 940,540, 968,610, 982,676, 940,676, 912,630, 886,590), FAR, RIM),
    ('LegF_Shin', 24, V(934,668, 980,668, 979,720, 976,766, 938,766, 935,720), FAR, RIM),
    ('FootF_Boot', 28, V(934,752, 978,752, 988,790, 994,816, 904,816, 900,804, 916,792, 934,782), FAR, None),
    ('Pelvis', 32, V(812,392, 952,392, 946,566, 816,566), BODY, None),
    ('Coat_Side', 33, V(876,398, 953,398, 966,444, 986,512, 1008,590, 1030,664, 1056,726, 1010,738, 964,744, 928,740, 916,690, 904,630, 892,560, 882,470), COAT_SIDE, RIM),
    ('Torso', 44, V(834,226, 870,214, 908,220, 932,240, 946,268, 950,310, 951,355, 953,425, 880,428, 808,425, 807,360, 812,322, 818,290, 824,260), BODY, RIM),
    ('Lapel', 48, V(838,238, 850,236, 860,300, 862,398, 850,398, 846,300), TRIM, None),
    ('Collar', 49, V(822,250, 842,232, 853,262, 838,292), TRIM_DEEP, None),
    ('LegN_Thigh', 52, V(812,540, 884,540, 878,600, 866,650, 858,676, 816,676, 806,630, 806,580), BODY, RIM),
    ('LegN_Shin', 56, V(814,668, 860,668, 858,720, 854,766, 818,766, 814,720), BODY, RIM),
    ('LegN_KneeStrap', 57, V(811,662, 863,662, 862,690, 812,690), TRIM, None),
    ('FootN_Boot', 60, V(814,752, 858,752, 862,790, 865,816, 774,816, 770,804, 788,792, 812,782), BODY, None),
    ('Coat', 64, V(806,398, 890,398, 892,470, 892,560, 882,620, 864,680, 848,738, 816,744, 780,736, 790,650, 798,560, 802,470), COAT_MAIN, RIM),
    ('Belt', 72, V(805,366, 953,370, 954,397, 805,395), hexc('#B8323A'), None),
    ('Buckle', 76, V(808,363, 834,363, 834,399, 808,399), SILVER, None),
    ('Neck', 80, V(838,200, 868,200, 872,240, 842,240), BODY, None),
    ('Scarf_Wrap', 100, V(818,232, 836,221, 860,213, 890,207, 920,203, 946,205, 958,217, 955,236, 940,244, 912,240, 884,242, 856,248, 834,254, 820,250), TRIM, TRIM_BRIGHT),
    ('Scarf_Fold', 101, V(830,240, 858,232, 890,226, 925,222, 948,224, 946,231, 924,229, 890,233, 858,239, 832,247), TRIM_DEEP, None),
    ('Scarf_Knot', 102, V(942,204, 960,207, 968,220, 962,236, 948,234, 944,220), TRIM, None),
    ('ArmN_Upper', 104, V(884,258, 924,248, 944,286, 950,334, 950,372, 904,380, 897,334, 888,296), BODY, RIM),
    ('ArmN_Cuff', 105, V(901,352, 950,350, 952,390, 903,392), TRIM, None),
    ('ArmN_Fore', 108, V(905,385, 942,385, 934,420, 930,446, 902,446, 900,415), BODY, RIM),
    ('ArmN_Wrist', 109, V(900,430, 932,430, 932,447, 900,447), TRIM, None),
    ('HandN_Fist', 112, V(888,442, 926,440, 932,462, 924,486, 898,490, 880,480, 877,458), BODY, RIM),
]


def draw_list(armor):
    items = []
    seq = [0]
    def add(sort, pts, col):
        seq[0] += 1
        items.append((sort, seq[0], pts, col))

    pal = armor.palette if armor else None
    rim_col = hexc(pal['rim']) if (armor and armor.retones_coat) else RIM
    scarf_col = hexc(pal['cloth'][1]) if (armor and armor.retones_coat and 'cloth' in pal) else TRIM
    scarf_lo = hexc(pal['cloth'][0]) if (armor and armor.retones_coat and 'cloth' in pal) else TRIM_DEEP
    coat_col = hexc(pal['coat'][1]) if (armor and armor.retones_coat and 'coat' in pal) else COAT_MAIN
    coat_lo = hexc(pal['coat'][0]) if (armor and armor.retones_coat and 'coat' in pal) else COAT_BACK

    # Scarf tails behind
    add(-1, [(944, 210), (1010, 214), (1080, 232), (1120, 250), (1080, 246), (1010, 236), (950, 232)], scarf_lo)
    add(0, [(946, 214), (1000, 222), (1060, 246), (1100, 268), (1060, 262), (1000, 244), (948, 230)], scarf_col)

    # Mirror ArmorVisuals.cs: suppress completely covered base straps/belt/lapel & trim base coat under tailored haori/capes
    covered_base = {'Lapel', 'Collar', 'Belt', 'Buckle', 'ArmN_Cuff', 'ArmN_Wrist', 'LegN_KneeStrap'}
    trimmed_coat = None
    if armor is not None:
        if armor.tier == 'legendary':
            trimmed_coat = {
                'Coat': V(808,462, 874,462, 876,556, 864,640, 846,714, 816,718, 796,712, 802,630, 806,544),
                'Coat_Side': V(894,454, 944,454, 956,522, 968,616, 976,712, 948,718, 922,714, 908,622, 898,530),
                'Coat_Back': V(894,454, 946,454, 962,526, 976,620, 988,716, 958,722, 924,718, 908,626, 898,532),
            }
        elif armor.tier in ('epic', 'trophy'):
            trimmed_coat = {
                'Coat': V(804,444, 878,444, 880,548, 866,640, 846,722, 816,728, 792,720, 800,632, 804,536),
                'Coat_Side': V(890,436, 946,436, 960,516, 976,618, 988,720, 956,726, 924,722, 906,622, 896,526),
                'Coat_Back': V(890,436, 948,436, 966,520, 986,622, 1000,724, 966,730, 926,726, 908,626, 896,528),
            }
        elif armor.tier == 'rare':
            trimmed_coat = {
                'Coat': V(806,424, 882,424, 884,548, 868,648, 848,732, 816,738, 788,730, 796,640, 802,530),
                'Coat_Side': V(886,420, 948,420, 970,512, 994,620, 1016,724, 976,734, 932,732, 908,624, 892,516),
                'Coat_Back': V(884,420, 950,420, 978,518, 1008,626, 1034,726, 990,738, 936,736, 910,628, 892,518),
            }
        else:
            trimmed_coat = {
                'Coat': V(806,418, 886,418, 888,554, 866,668, 848,736, 816,742, 782,734, 792,646, 800,530),
            }

    for name, order, pts, col, rim in KAGE_BODY:
        if armor is not None and name in covered_base:
            continue
        if trimmed_coat and name in trimmed_coat:
            pts = trimmed_coat[name]
        c = col
        if armor and armor.retones_coat:
            if name in ('Coat', 'Coat_Side'):
                c = coat_col
            elif name == 'Coat_Back':
                c = coat_lo
            elif name in ('Scarf_Wrap', 'Scarf_Knot', 'Lapel', 'ArmN_Cuff', 'ArmN_Wrist', 'LegN_KneeStrap', 'Belt'):
                c = scarf_col
            elif name in ('Scarf_Fold', 'Collar'):
                c = scarf_lo
        if rim is not None:
            add((order - order % 4) * 3, [(x + RIM_SHIFT_REF[0], y + RIM_SHIFT_REF[1]) for (x, y) in grow(pts, RIM_GROW)], rim_col)
        add(order * 3 + 2, pts, c)

    hair, eye = kage_svg_hair()
    for pts, col in hair:
        add(84 * 3 + 2, pts, col)
    add(96 * 3 + 2, eye, hexc('#F4F1EE'))

    if armor is not None:
        arim = hexc(pal['rim'])
        for p in armor.pieces:
            ref = p.outline
            o = p.order
            tone = tuple(hexc(c) for c in pal[p.tone]) if p.tone in pal else None
            if p.kind == 'flat':
                c = pal[p.tone][p.which]
                add(o * 3 + 2, ref, hexc(c, p.alpha))
                continue
            if p.kind == 'cel' and p.rim:
                # Pauldron cel tiers merge into a single ordered mesh with per-tier rims right under each tier
                rim_sort = o * 3 + 1 if p.bone == 'ArmNU' else (o - o % 4) * 3
                add(rim_sort, [(x + RIM_SHIFT_REF[0], y + RIM_SHIFT_REF[1]) for (x, y) in grow(ref, RIM_GROW)], arim)
            if p.kind == 'cloth' and p.rim:
                add(o * 3 + 2, [(x + RIM_SHIFT_REF[0], y + RIM_SHIFT_REF[1]) for (x, y) in grow(ref, RIM_GROW)], arim)
            for pg, col in cel_polys(ref, tone, p.sd, p.hd, p.seam):
                add(o * 3 + 2, pg, col)
            for d, dt, which, alpha in p.detail:
                add(o * 3 + 2, d, hexc(pal[dt][which], alpha))

    items.sort(key=lambda t: (t[0], t[1]))
    return items


def render(armor, cx=900.0, cy=455.0, half=420.0, size=480, ss=2, bg=(26, 22, 26, 255), mirror=True):
    from PIL import Image, ImageDraw
    S = size * ss
    bg_rgba = bg if len(bg) == 4 else bg + (255,)
    img = Image.new('RGBA', (S, S), bg_rgba)
    scale = S / (2.0 * half)
    def tr(p):
        x = (cx - p[0]) if mirror else (p[0] - cx)
        return (S * 0.5 + x * scale, S * 0.5 + (p[1] - cy) * scale)
    for _, _, pts, col in draw_list(armor):
        if len(pts) < 3:
            continue
        q = [tr(p) for p in pts]
        if col[3] >= 255:
            ImageDraw.Draw(img).polygon(q, fill=col)
        else:
            layer = Image.new('RGBA', (S, S), (0, 0, 0, 0))
            ImageDraw.Draw(layer).polygon(q, fill=col)
            img = Image.alpha_composite(img, layer)
    return img.resize((size, size), Image.LANCZOS)


# ------------------------------------------------------------------------------------------------ geometry checks

def _cross(o, a, b):
    return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])


def _hits(a, b, c, d):
    d1, d2, d3, d4 = _cross(c, d, a), _cross(c, d, b), _cross(a, b, c), _cross(a, b, d)
    return ((d1 > 0) != (d2 > 0)) and ((d3 > 0) != (d4 > 0)) and abs(d1) > 1e-12 and abs(d2) > 1e-12 and abs(d3) > 1e-12 and abs(d4) > 1e-12


def problem(pts):
    n = len(pts)
    if n < 3:
        return 'fewer than 3 points'
    if abs(signed_area(pts)) < 4.0:
        return 'area under 4 px2'
    for i in range(n):
        a, b = pts[i], pts[(i + 1) % n]
        for j in range(i + 2, n):
            if i == 0 and j == n - 1:
                continue
            c, d = pts[j], pts[(j + 1) % n]
            if _hits(a, b, c, d):
                return 'edges %d and %d cross' % (i, j)
    return None


def triangulate(pts):
    n = len(pts)
    if n < 3:
        return []
    ccw = signed_area(pts) > 0
    idx = list(range(n)) if ccw else list(range(n - 1, -1, -1))
    tris = []
    def cr(a, b): return a[0] * b[1] - a[1] * b[0]
    def intri(p, a, b, c):
        d1 = cr((b[0] - a[0], b[1] - a[1]), (p[0] - a[0], p[1] - a[1]))
        d2 = cr((c[0] - b[0], c[1] - b[1]), (p[0] - b[0], p[1] - b[1]))
        d3 = cr((a[0] - c[0], a[1] - c[1]), (p[0] - c[0], p[1] - c[1]))
        neg = d1 < 0 or d2 < 0 or d3 < 0
        pos = d1 > 0 or d2 > 0 or d3 > 0
        return not (neg and pos)
    guard = 0
    while len(idx) > 3 and guard < 4000:
        guard += 1
        clipped = False
        for i in range(len(idx)):
            ia, ib, ic = idx[(i - 1) % len(idx)], idx[i], idx[(i + 1) % len(idx)]
            a, b, c = pts[ia], pts[ib], pts[ic]
            if cr((b[0] - a[0], b[1] - a[1]), (c[0] - b[0], c[1] - b[1])) <= 1e-9:
                continue
            if any(intri(pts[q], a, b, c) for q in idx if q not in (ia, ib, ic)):
                continue
            tris.append((ia, ib, ic))
            idx.pop(i)
            clipped = True
            break
        if not clipped:
            break
    for i in range(1, len(idx) - 1):
        tris.append((idx[0], idx[i], idx[i + 1]))
    return tris


def tiles(ref):
    tris = triangulate(ref)
    A = abs(signed_area(ref))
    S = sum(abs(signed_area([ref[a], ref[b], ref[c]])) for a, b, c in tris)
    return abs(A - S) <= A * 0.01 + 0.5, A, S


# ------------------------------------------------------------------------------------------------ C# export

def cs_floats(ref):
    return 'new float[] { ' + ', '.join(f'{x:.1f}f, {y:.1f}f' for (x, y) in ref) + ' }'


def export_cs(armors, path):
    tiers = {'common': 'Common', 'rare': 'Rare', 'epic': 'Epic', 'trophy': 'Trophy', 'legendary': 'Legendary'}
    auras = {'none': 'None', 'spirit': 'Spirit', 'void': 'Void'}
    L = []
    L.append('// GENERATED by Tools/armor_art/build.py from Tools/armor_art/armors.py (5-tier armor redesign). Do not edit by hand:')
    L.append('// change the design there, run `python Tools/armor_art/build.py check && python Tools/armor_art/build.py export`.')
    L.append('// Outlines and chains are in reference px (KageModel.P); tones name an entry of the armor palette.')
    L.append('using System.Collections.Generic;')
    L.append('')
    L.append('namespace Kage')
    L.append('{')
    L.append('    public static partial class ArmorDesigns')
    L.append('    {')
    L.append('        static readonly ArmorDesign[] Generated =')
    L.append('        {')
    for a in armors:
        L.append(f'            // {a.id} ({a.tier}): {a.summary}')
        pal_entries = []
        for k, v in a.palette.items():
            if k == 'rim':
                continue
            pal_entries.append(f'["{k}"] = new[] {{ "{v[0]}", "{v[1]}", "{v[2]}" }}')
        pal_str = f'new ArmorPalette("{a.palette["rim"]}", new Dictionary<string, string[]> {{ {", ".join(pal_entries)} }})'
        L.append(f'            new ArmorDesign("{a.id}", "{a.look}", HelmetTier.{tiers[a.tier]}, ArmorAuraKind.{auras[a.aura]}, {pal_str}, new[]')
        L.append('            {')
        for p in a.pieces:
            kind = {'cel': 'PieceKind.Cel', 'flat': 'PieceKind.Flat', 'cloth': 'PieceKind.Cloth'}[p.kind]
            parts = [
                f'name = "{p.name}"',
                f'kind = {kind}',
                f'bone = B.{p.bone}',
                f'order = {p.order}',
                f'tone = "{p.tone}"',
            ]
            if p.kind == 'flat':
                parts.append(f'which = {p.which}')
            if p.alpha != 1.0:
                parts.append(f'alpha = {p.alpha:.3f}f')
            if p.kind != 'flat':
                parts += [f'sd = {p.sd:.2f}f', f'hd = {p.hd:.2f}f', f'rim = {"true" if p.rim else "false"}']
                if abs(p.seam - 1.6) > 0.01:
                    parts.append(f'seam = {p.seam:.2f}f')
            if p.glow != 1.0:
                parts.append(f'glow = {p.glow:.2f}f')
            if p.sword_layer:
                parts.append('swordLayer = true')
            if p.feel:
                parts.append(f'feel = "{p.feel}"')
            parts.append('outline = ' + cs_floats(p.outline))
            if p.chains:
                parts.append('chains = new[] { ' + ', '.join(cs_floats(c) for c in p.chains) + ' }')
            if p.detail:
                ds = [f'new ArmorDetail("{dt}", {w}, {al:.3f}f, {cs_floats(d)})' for d, dt, w, al in p.detail]
                parts.append('detail = new[] { ' + ', '.join(ds) + ' }')
            L.append('                new ArmorPiece { ' + ', '.join(parts) + ' },')
        L.append('            }),')
    L.append('        };')
    L.append('    }')
    L.append('}')
    open(path, 'w', encoding='utf-8', newline='\n').write('\n'.join(L) + '\n')
