"""Raikon's rig-part regions (SAN-71).

The owner's Raikon SVG is a 7-colour trace whose first colour (#010000) is both the background and the figure's
black, so the figure's outline is not in the file. This tool finds it and writes the region of every rig part:

  silhouette = every non-black colour, closed by a few pixels (bridges the black outlines), its enclosed black filled
               (except the cape's torn holes), plus the black areas listed in BLACK (face, neck, ...), grown by the
               outline width;
  part       = its rough polygon (PARTS) inside the silhouette, minus the rough polygons of the parts listed in its
               "minus" (what covers it in the painting, so nothing is drawn twice);
  fill       = a rough polygon drawn as one flat palette colour (FILLS): what the painting hides behind a part
               that moves (the torso behind the arms, the legs under the skirt).

Run:  python Tools/raikon_art/regions.py
Writes Assets/Kage/Scripts/Character/PartSets/RaikonRegions.g.cs and Screenshots/review/SAN-71/regions.png.
Coordinates are SVG pixels (976 x 1230, x right, y down, Raikon facing left).
"""
import os, re, sys
import numpy as np
import cv2

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
SVG = os.path.join(HERE, 'original', 'Raikon.svg')
OUT_CS = os.path.join(ROOT, 'Assets', 'Kage', 'Scripts', 'Character', 'PartSets', 'RaikonRegions.g.cs')
OUT_PNG = os.path.join(ROOT, 'Screenshots', 'review', 'SAN-71', 'regions.png')
S = 4               # raster supersampling
W, H = 976, 1230
CLOSE = 5           # px: black gaps narrower than about twice this between colours are the figure's own lines
OUTLINE = 2         # px: black outline kept around the figure
EPS = 0.35          # px: polygon simplification

# ---- rough part regions (SVG px). name: (polygon, minus) ----
PARTS = {
    # head bone
    'Hat': ([(186, 238), (300, 190), (420, 132), (468, 108), (500, 96), (540, 100), (575, 128), (640, 160), (668, 178),
             (660, 198), (560, 212), (450, 226), (330, 250), (196, 250)], ['Ponytail', 'Tassel1', 'Tassel2', 'Tassel3', 'Head']),
    'Tassel1': ([(274, 246), (312, 246), (322, 350), (268, 350)], []),
    'Tassel2': ([(344, 240), (386, 240), (396, 350), (340, 350)], []),
    'Tassel3': ([(606, 190), (648, 190), (658, 268), (600, 268)], []),
    'Ponytail': ([(462, 104), (462, 40), (520, 10), (600, 12), (700, 30), (800, 70), (812, 160), (796, 240), (812, 330),
                  (816, 400), (796, 470), (720, 455), (690, 390), (650, 330), (612, 280), (590, 230), (575, 175), (560, 135),
                  (530, 110)], ['Tassel3']),
    'Head': ([(396, 202), (470, 194), (600, 196), (604, 244), (528, 262), (506, 300), (470, 330), (440, 334), (424, 310),
              (446, 300), (450, 288), (436, 276), (432, 264), (420, 258), (404, 250), (414, 230)], ['Eye']),
    'Eye': ([(398, 206), (432, 206), (432, 232), (398, 232)], []),
    # chest bone
    'Fur': ([(392, 300), (420, 255), (470, 232), (560, 236), (630, 262), (700, 330), (700, 420), (660, 470), (600, 480),
             (560, 440), (500, 400), (440, 380), (400, 360)], ['SodeN', 'Head', 'Torso']),
    'Torso': ([(330, 330), (430, 318), (470, 330), (494, 340), (500, 432), (520, 470), (528, 506), (512, 560), (400, 566),
               (330, 566), (318, 450)], ['SodeN', 'Rope', 'FarForearm', 'Fist']),
    'Rope': ([(372, 500), (520, 498), (526, 530), (510, 566), (380, 570), (366, 540)], ['ArmN_Upper', 'ArmN_Fore']),
    'Cape': ([(556, 430), (640, 420), (700, 470), (760, 560), (830, 640), (866, 760), (866, 900), (830, 1020), (770, 1090),
              (690, 1080), (650, 1000), (612, 900), (594, 800), (586, 700), (590, 620), (580, 560), (560, 500)],
             ['SodeN', 'ArmN_Upper', 'ArmN_Fore', 'GripHand', 'Skirt', 'Fur', 'Tachi_Hilt']),
    # hips bone
    'Skirt': ([(326, 556), (520, 552), (586, 640), (606, 720), (664, 800), (724, 880), (740, 1000), (744, 1084),
               (660, 1086), (560, 1032), (520, 1000), (470, 994), (400, 1004), (330, 1064), (296, 1052), (256, 1000),
               (266, 900), (300, 760), (316, 650)],
              ['ArmN_Fore', 'GripHand', 'FarForearm', 'Fist', 'Tachi_Blade', 'Tachi_Hilt', 'Rope', 'ShinN', 'BootN']),
    # the painted near arm: shoulder plate, sleeve with the elbow wrap, gauntlet
    'SodeN': ([(490, 334), (600, 328), (616, 380), (626, 440), (632, 492), (560, 496), (520, 470), (494, 440)], []),
    'ArmN_Upper': ([(512, 470), (590, 470), (590, 552), (512, 552)], ['SodeN']),
    'ArmN_Fore': ([(508, 546), (580, 546), (578, 632), (508, 632)], ['Tachi_Hilt']),
    # the painted hands: the near one grips the tachi (drawn on the sword hand), the far one is a fist (the free hand)
    'GripHand': ([(500, 622), (580, 622), (580, 690), (500, 690)], []),
    'Fist': ([(318, 626), (370, 626), (372, 704), (318, 704)], []),
    # the painted far forearm, in front of the torso (not drawn: the far arm is drawn from the near one)
    'FarForearm': ([(366, 572), (404, 566), (402, 626), (372, 642), (344, 634)], []),
    # tachi: hilt (under the gripping hand) and blade
    # (the blade runs from the tsuba at (495, 703) to the tip at (108, 1000); the flames lick up from its far half)
    'Tachi_Hilt': ([(604, 598), (618, 622), (506, 716), (480, 692)], ['GripHand']),
    'Tachi_Blade': ([(502, 679), (478, 697), (454, 715), (430, 734), (407, 752), (383, 770), (359, 789), (329, 799),
                     (296, 805), (269, 820), (245, 838), (222, 856), (198, 874), (174, 893), (150, 911), (126, 929),
                     (108, 954), (92, 982), (85, 993), (105, 1020), (115, 1012), (139, 994), (162, 976), (186, 958),
                     (210, 939), (234, 921), (258, 903), (281, 885), (305, 866), (329, 848), (353, 830), (377, 812),
                     (400, 793), (424, 775), (448, 757), (472, 738), (496, 720), (519, 702)], ['Tachi_Hilt']),
    # near leg (the rear leg with the buckle)
    'ShinN': ([(462, 958), (554, 958), (556, 1100), (480, 1100)], []),
    'BootN': ([(470, 1088), (556, 1088), (558, 1198), (396, 1198), (396, 1168), (430, 1140), (470, 1128)], []),
}

# flat palette fills: name: (polygon, colour index)
FILLS = {
    'TorsoFill': ([(392, 330), (470, 320), (560, 372), (604, 450), (596, 540), (520, 580), (396, 584), (360, 520), (370, 400)], 2),
    'HipsFill': ([(384, 556), (560, 556), (600, 700), (604, 950), (560, 1000), (420, 1004), (380, 900)], 2),
    'ThighN': ([(470, 780), (548, 780), (548, 1004), (482, 1004)], 2),
    'GripFill': ([(600, 604), (610, 622), (506, 712), (494, 694)], 1),
}

# what a moving part hides of the part behind it, painted back by continuing that part's colours down its columns
# (the skirt's panels and cords run straight down): name: (part behind, part in front)
PATCHES = {
    'SkirtUnderBlade': ('Skirt', 'Tachi_Blade'),
}

# black areas that belong to the figure (the trace merges them with the background)
BLACK = [
    [(396, 202), (470, 194), (520, 200), (528, 262), (506, 300), (470, 330), (440, 334), (424, 310), (446, 300),
     (450, 288), (436, 276), (432, 264), (420, 258), (404, 250), (414, 230)],                  # face under the hat
    [(430, 300), (500, 290), (510, 345), (440, 350)],                                          # neck
]
# rough area where enclosed black holes stay holes (the cape's tears); elsewhere enclosed black is the figure's
TORN = [(600, 560), (850, 560), (850, 1100), (600, 1100)]


def parse(d):
    toks = re.findall(r'[MCZ]|-?\d*\.?\d+(?:e-?\d+)?', d)
    subs, cur, i, cmd = [], None, 0, None
    while i < len(toks):
        t = toks[i]
        if t in 'MCZ':
            cmd = t; i += 1
            if t == 'Z' and cur:
                subs.append(cur); cur = None
            continue
        if cmd == 'M':
            cur = [(float(toks[i]), float(toks[i + 1]))]; i += 2; cmd = 'L'
        elif cmd == 'C':
            p0 = cur[-1]
            p1, p2, p3 = [(float(toks[i + k]), float(toks[i + k + 1])) for k in (0, 2, 4)]
            i += 6
            for k in range(1, 7):
                t_ = k / 6; a = 1 - t_
                cur.append((a**3*p0[0] + 3*a*a*t_*p1[0] + 3*a*t_*t_*p2[0] + t_**3*p3[0],
                            a**3*p0[1] + 3*a*a*t_*p1[1] + 3*a*t_*t_*p2[1] + t_**3*p3[1]))
        else:
            raise ValueError(cmd)
    if cur: subs.append(cur)
    return subs


def poly_mask(pts):
    m = np.zeros((H * S, W * S), np.uint8)
    cv2.fillPoly(m, [np.round(np.array(pts) * S).astype(np.int32)], 1)
    return m


def disk(r):
    r = int(round(r * S))
    return cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * r + 1, 2 * r + 1))


def labels():
    s = open(SVG).read()
    paths = re.findall(r'<path d="([^"]*)" fill="(#[0-9a-f]{6})"', s)
    lab = np.zeros((H * S, W * S), np.uint8)
    for ci, (d, _) in enumerate(paths):
        m = np.zeros_like(lab)
        for sp in parse(d):
            t = np.zeros_like(lab)
            cv2.fillPoly(t, [np.round(np.array(sp) * S).astype(np.int32)], 1)
            m ^= t
        lab[m == 1] = ci
    return lab, [c for _, c in paths]


def fill_holes(m):
    inv = (1 - m).astype(np.uint8)
    n, cc = cv2.connectedComponents(inv, connectivity=4)
    bg = cc[0, 0]
    return np.where(cc == bg, 0, 1).astype(np.uint8)


def silhouette(lab):
    sil = (lab != 0).astype(np.uint8)
    sil = cv2.morphologyEx(sil, cv2.MORPH_CLOSE, disk(CLOSE))
    for b in BLACK: sil |= poly_mask(b)
    filled = fill_holes(sil)
    torn = poly_mask(TORN)
    holes = filled & (1 - sil)
    # big holes inside the cape stay transparent, small ones are lines in the cloth
    n, cc, st, _ = cv2.connectedComponentsWithStats(holes, connectivity=4)
    keep_hole = np.zeros_like(sil)
    for k in range(1, n):
        if st[k, cv2.CC_STAT_AREA] > 60 * S * S:
            comp = (cc == k)
            if torn[comp].mean() > 0.5: keep_hole[comp] = 1
    sil = filled & (1 - keep_hole)
    sil = cv2.dilate(sil, disk(OUTLINE))
    return sil


def keyhole(outer, holes):
    """Joins each hole to its outline by a zero-width cut, so a region with holes is one polygon (the importer then
    clips by plain polygons, which is much faster than by a region with holes)."""
    outer = list(outer)
    for h in holes:
        best = None
        for j, p in enumerate(outer):
            for k, q in enumerate(h):
                d = (p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2
                if best is None or d < best[0]: best = (d, j, k)
        _, j, k = best
        outer = outer[:j + 1] + list(h[k:]) + list(h[:k + 1]) + outer[j:]
    return outer


def contours(m):
    """Polygons (SVG px) of a mask: one per island, holes joined in."""
    m = np.pad(m, 1)
    cs, hier = cv2.findContours(m, cv2.RETR_CCOMP, cv2.CHAIN_APPROX_NONE)
    if hier is None: return []
    hier = hier[0]
    def simp(c):
        a = cv2.approxPolyDP(c, EPS * S, True).reshape(-1, 2)
        return [((x - 1 + 0.5) / S, (y - 1 + 0.5) / S) for x, y in a]
    out = []
    for i, c in enumerate(cs):
        if hier[i][3] >= 0 or cv2.contourArea(c) < 4 * S * S: continue
        outer = simp(c)
        if len(outer) < 3: continue
        holes = [simp(cs[k]) for k in range(len(cs)) if hier[k][3] == i and cv2.contourArea(cs[k]) >= 4 * S * S]
        out.append(keyhole(outer, [h for h in holes if len(h) >= 3]))
    return out


def inpaint(lab, hole, source):
    """Labels for the hole pixels, each taken from the nearest source pixel straight above or below it
    (-1 where there is none within 60 px)."""
    H_, W_ = lab.shape
    out = np.full(lab.shape, -1, np.int16)
    reach = 60 * S
    ys = np.arange(H_)
    for x in np.nonzero(hole.any(axis=0))[0]:
        col_h, col_s = hole[:, x].astype(bool), source[:, x].astype(bool)
        if not col_s.any(): continue
        idx = np.where(col_s, ys, -1)
        above = np.maximum.accumulate(idx)                       # last source row at or above
        idx2 = np.where(col_s, ys, H_ * 2)
        below = np.minimum.accumulate(idx2[::-1])[::-1]          # first source row at or below
        da = np.where(above >= 0, ys - above, 10 ** 9)
        db = np.where(below < H_, below - ys, 10 ** 9)
        pick = np.where(da <= db, above, below)
        ok = col_h & (np.minimum(da, db) <= reach)
        out[ok, x] = lab[pick[ok], x]
    return out


def main():
    lab, cols = labels()
    sil = silhouette(lab)
    rough = {n: poly_mask(p) for n, (p, _) in PARTS.items()}
    result = {}
    for n, (p, minus) in PARTS.items():
        m = rough[n] & sil
        for o in minus: m &= 1 - rough[o]
        result[n] = contours(m)
    fills = [(n, layer, contours(poly_mask(p))[0]) for n, (p, layer) in FILLS.items()]
    for n, (behind, front) in PATCHES.items():
        hole = rough[front] & rough[behind] & sil
        source = rough[behind] & sil & (1 - rough[front])
        for o in PARTS[behind][1]: source &= 1 - rough[o]
        got = inpaint(lab, hole, source)
        for layer in range(len(cols)):
            for c in contours(((got == layer) & (hole == 1)).astype(np.uint8)):
                fills.append((n, layer, c))

    def arr(c): return 'new float[] { ' + ','.join(f'{v:.2f}f' for pt in c for v in pt) + ' }'
    with open(OUT_CS, 'w', newline='\n') as f:
        f.write('// GENERATED by Tools/raikon_art/regions.py from Tools/raikon_art/original/Raikon.svg — do not edit by hand.\n')
        f.write('// The region of each of Raikon\'s rig parts: polygons (x, y pairs in SVG px), combined even-odd.\n')
        f.write('namespace Kage\n{\n    public static class RaikonRegions\n    {\n')
        f.write('        public static readonly (string name, float[][] polygons)[] Parts =\n        {\n')
        for n, cs in result.items():
            f.write(f'            ("{n}", new float[][]\n            {{\n')
            for c in cs: f.write('                ' + arr(c) + ',\n')
            f.write('            }),\n')
        f.write('        };\n\n')
        f.write('        /// <summary>Fills in the trace\'s colours (layer index): flat underlays, and patches painted back where a moving part covered another.</summary>\n')
        f.write('        public static readonly (string name, int layer, float[] polygon)[] Fills =\n        {\n')
        for n, layer, c in fills:
            f.write(f'            ("{n}", {layer}, {arr(c)}),\n')
        f.write('        };\n    }\n}\n')

    # preview: the trace tinted by the part that draws each pixel; magenta = figure that no part draws
    pal = np.array([[int(c[5:7], 16), int(c[3:5], 16), int(c[1:3], 16)] for c in cols], np.uint8)
    img = pal[lab].astype(np.float32)
    covered = np.zeros_like(sil)
    rng = np.random.RandomState(5)
    names = []
    for n in PARTS:
        m = rough[n] & sil
        for o in PARTS[n][1]: m &= 1 - rough[o]
        covered |= m
        col = rng.randint(60, 255, 3).astype(np.float32)
        img[m == 1] = img[m == 1] * 0.55 + col * 0.45
        ys, xs = np.nonzero(m)
        if len(xs): names.append((n, int(xs.mean()), int(ys.mean()), tuple(int(v) for v in col)))
    for n, (p, _) in FILLS.items(): covered |= poly_mask(p) & sil
    img[(sil == 1) & (covered == 0)] = (255, 0, 255)
    img[sil == 0] = (40, 70, 40)
    img = cv2.resize(img.astype(np.uint8), (W * 2, H * 2), interpolation=cv2.INTER_AREA)
    for n, x, y, col in names:
        cv2.putText(img, n, (x * 2 // S - 30, y * 2 // S), cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 3)
        cv2.putText(img, n, (x * 2 // S - 30, y * 2 // S), cv2.FONT_HERSHEY_SIMPLEX, 0.55, col, 1)
    os.makedirs(os.path.dirname(OUT_PNG), exist_ok=True)
    cv2.imwrite(OUT_PNG, img)
    print('uncovered figure px', int(((sil == 1) & (covered == 0)).sum() / S / S))
    print('parts', len(result), 'fills', len(fills), 'points', sum(len(c) for cs in result.values() for c in cs))
    # preview of the patches alone (what shows when the tachi moves away from the skirt)
    patch = pal[lab].copy()
    for n, (behind, front) in PATCHES.items():
        hole = rough[front] & rough[behind] & sil
        source = rough[behind] & sil & (1 - rough[front])
        for o in PARTS[behind][1]: source &= 1 - rough[o]
        got = inpaint(lab, hole, source)
        patch[(hole == 1) & (got >= 0)] = pal[got[(hole == 1) & (got >= 0)]]
        patch[(hole == 1) & (got < 0)] = (255, 0, 255)
    cv2.imwrite(os.path.join(os.path.dirname(OUT_PNG), 'regions_patches.png'), cv2.resize(patch, (W, H), interpolation=cv2.INTER_AREA))


if __name__ == '__main__':
    main()
