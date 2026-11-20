"""SVG trace -> rig regions (SAN-76 SVG redesign). Generic: everything about one character is in its config JSON.

    python Tools/svg_art/svg_rig.py Tools/hollow_art/shade/rig.json

Reads the owner's colour trace (any SVG of filled paths, painted in file order: VTracer's stacked or cutout output) and,
optionally, the painting it was traced from, and writes one text resource the game builds the rig from
(SvgRigData / SvgRig in Assets/Kage/Scripts/Character/SvgRig/). Steps:

  1. raster  : every path filled in paint order on a supersampled grid (labels = path index);
  2. tones   : each trace colour snapped to the nearest named tone of the config (CIELAB), so hundreds of trace
               colours become a few readable shading planes; the game paints each tone in its style's colour;
  3. figure  : the silhouette: the trace's non-background colours, plus (if the config names the painting) every
               pixel of the painting that differs from its background, so dark cloth the trace merged with the
               canvas (a cape's black inside) is kept; such pixels take the config's fill tone;
  4. specks  : islands of a tone smaller than `speck` px^2 take the tone around them (no trace specks);
  5. regions : each region = its rough polygon(s) & figure [& tones] - the regions listed in its `minus`;
  6. polygons: per region, a base layer (the region's outline in the base tone) and then every island of every
               other tone, holes filled, painted outermost first (an island inside another's hole is painted
               after it), so no polygon has a hole and neighbours never leave a crack (the base shows through a
               seam). Holes that open onto the outside are slit open instead. Outlines are traced half a sample
               outside each island, simplified by `eps` px and checked simple; soft regions are resampled so no
               edge is longer than `resample` px (they bend smoothly on their chains);
  7. output  : regions + the config's rig parts (bone, order, map, soft preset, chains) + an FNV-1a hash of every
               input (SVG, config, painting and this tool), which the EditMode test recomputes (stale = red).

Previews go to the config's `preview` folder: regions.png (each region tinted, magenta = figure no region draws),
tones.png (the cleaned tone image) and rebuilt.png (the polygons painted back, which should match tones.png).
Coordinates are SVG px (x right, y down). Every loop over floats has a fixed cap.
"""
import json, os, re, sys
import numpy as np
import cv2

TOOL_VERSION = 1
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))

# ---------------------------------------------------------------------------------------------------- SVG paths
NUM = r'[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?'


def parse_path(d, steps=8):
    """Closed subpaths (lists of (x, y)) of an SVG path's d: M L H V C S Q T Z, absolute and relative (no arcs)."""
    toks = re.findall(r'[MLHVCSQTZmlhvcsqtz]|' + NUM, d)
    subs, cur = [], []
    pos = np.zeros(2); start = np.zeros(2); ctrl = None
    i, cmd = 0, None

    def take(n):
        nonlocal i
        v = [float(t) for t in toks[i:i + n]]
        i += n
        return v

    def bez(pts):
        p = np.array(pts)
        out = []
        for k in range(1, steps + 1):
            t = k / steps
            if len(p) == 4:
                a = 1 - t
                out.append(a**3 * p[0] + 3 * a * a * t * p[1] + 3 * a * t * t * p[2] + t**3 * p[3])
            else:
                a = 1 - t
                out.append(a * a * p[0] + 2 * a * t * p[1] + t * t * p[2])
        return out

    guard = 0
    while i < len(toks) and guard < 10 ** 7:
        guard += 1
        t = toks[i]
        if t.isalpha():
            cmd = t; i += 1
            if cmd in 'Zz':
                if len(cur) >= 3: subs.append(cur)
                cur = []; pos = start.copy(); ctrl = None
            continue
        rel = cmd.islower(); c = cmd.upper()
        base = pos if rel else np.zeros(2)
        if c == 'M':
            x, y = take(2); pos = base + (x, y); start = pos.copy()
            if len(cur) >= 3: subs.append(cur)
            cur = [pos.copy()]; cmd = 'l' if rel else 'L'; ctrl = None
        elif c == 'L':
            x, y = take(2); pos = base + (x, y); cur.append(pos.copy()); ctrl = None
        elif c == 'H':
            (x,) = take(1); pos = np.array([(pos[0] if rel else 0) + x, pos[1]]); cur.append(pos.copy()); ctrl = None
        elif c == 'V':
            (y,) = take(1); pos = np.array([pos[0], (pos[1] if rel else 0) + y]); cur.append(pos.copy()); ctrl = None
        elif c == 'C':
            v = take(6); p1 = base + v[0:2]; p2 = base + v[2:4]; p3 = base + v[4:6]
            cur += bez([pos, p1, p2, p3]); ctrl = p2; pos = p3
        elif c == 'S':
            v = take(4); p1 = 2 * pos - ctrl if ctrl is not None else pos; p2 = base + v[0:2]; p3 = base + v[2:4]
            cur += bez([pos, p1, p2, p3]); ctrl = p2; pos = p3
        elif c == 'Q':
            v = take(4); p1 = base + v[0:2]; p2 = base + v[2:4]
            cur += bez([pos, p1, p2]); ctrl = p1; pos = p2
        elif c == 'T':
            v = take(2); p1 = 2 * pos - ctrl if ctrl is not None else pos; p2 = base + v[0:2]
            cur += bez([pos, p1, p2]); ctrl = p1; pos = p2
        else:
            raise ValueError('unsupported path command ' + cmd)
    if len(cur) >= 3: subs.append(cur)
    return [np.array(s) for s in subs]


def read_svg(path):
    s = open(path, encoding='utf-8').read()
    m = re.search(r'<svg[^>]*\bwidth="([\d.]+)"[^>]*\bheight="([\d.]+)"', s)
    size = (float(m.group(1)), float(m.group(2)))
    paths = []
    for tag in re.findall(r'<path\b[^>]*>', s):
        d = re.search(r'\bd="([^"]*)"', tag)
        f = re.search(r'\bfill="(#[0-9A-Fa-f]{6})"', tag)
        if d and f: paths.append((d.group(1), f.group(1).upper()))
    return size, paths


# ---------------------------------------------------------------------------------------------------- helpers
def lab(hexes):
    rgb = np.array([[int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16)] for h in hexes], np.float32) / 255.0
    return cv2.cvtColor(rgb[None, :, ::-1].copy(), cv2.COLOR_BGR2LAB)[0]


def disk(r):
    r = max(1, int(round(r)))
    return cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * r + 1, 2 * r + 1))


def fill_holes(m):
    inv = (1 - m).astype(np.uint8)
    n, cc = cv2.connectedComponents(np.pad(inv, 1, constant_values=1), connectivity=4)
    outside = cc[0, 0]
    return np.where(cc[1:-1, 1:-1] == outside, 0, 1).astype(np.uint8) | m


def fnv(h, data):
    for v in data:
        h ^= v
        h = (h * 1099511628211) & 0xFFFFFFFFFFFFFFFF
    return h


def input_hash(files):
    """FNV-1a 64 over every input, in order: text files by character with '\\r' dropped (the same on a CRLF
    checkout), binary files by byte. SvgRigData.InputHash in C# computes the same."""
    h = 1469598103934665603
    for kind, path in files:
        if kind == 'binary':
            h = fnv(h, open(path, 'rb').read())
        else:
            h = fnv(h, (ord(c) for c in open(path, encoding='utf-8', newline='').read() if c != '\r'))
    return h


def poly_area(p):
    x, y = p[:, 0], p[:, 1]
    return 0.5 * float(np.dot(x, np.roll(y, -1)) - np.dot(y, np.roll(x, -1)))


def self_crosses(p):
    """True when two non-adjacent edges of the closed polygon cross (touching does not count)."""
    n = len(p)
    if n < 4: return False
    a = p; b = np.roll(p, -1, axis=0)
    def orient(p0, p1, q):
        return (p1[..., 0] - p0[..., 0]) * (q[..., 1] - p0[..., 1]) - (p1[..., 1] - p0[..., 1]) * (q[..., 0] - p0[..., 0])
    A = a[:, None, :]; B = b[:, None, :]; C = a[None, :, :]; D = b[None, :, :]
    d1 = orient(C, D, A); d2 = orient(C, D, B); d3 = orient(A, B, C); d4 = orient(A, B, D)
    hit = (d1 * d2 < 0) & (d3 * d4 < 0)
    idx = np.arange(n)
    adj = (np.abs(idx[:, None] - idx[None, :]) <= 1) | ((idx[:, None] == 0) & (idx[None, :] == n - 1)) | ((idx[:, None] == n - 1) & (idx[None, :] == 0))
    return bool((hit & ~adj).any())


def clean(p, min_len):
    """Drops repeated points (closer than min_len) and points where the outline doubles straight back."""
    out = []
    for q in p:
        if out and np.hypot(*(q - out[-1])) < min_len: continue
        out.append(q)
    while len(out) > 3 and np.hypot(*(out[0] - out[-1])) < min_len: out.pop()
    changed, guard = True, 0
    while changed and len(out) > 3 and guard < 50:
        changed, guard = False, guard + 1
        keep = []
        n = len(out)
        for k in range(n):
            a, b, c = out[k - 1], out[k], out[(k + 1) % n]
            u, v = b - a, c - b
            nu, nv = np.hypot(*u), np.hypot(*v)
            if nu > 0 and nv > 0 and np.dot(u, v) / (nu * nv) < -0.999:
                changed = True; continue                      # a spike: the outline goes out and straight back
            keep.append(b)
        out = keep
    return np.array(out)


def resample(p, seg):
    out = []
    n = len(p)
    for k in range(n):
        a, b = p[k], p[(k + 1) % n]
        out.append(a)
        L = np.hypot(*(b - a))
        m = min(64, int(np.ceil(L / seg)))
        for j in range(1, m): out.append(a + (b - a) * (j / m))
    return np.array(out)


# ---------------------------------------------------------------------------------------------------- the cut
class Cutter:
    def __init__(self, cfg, cfg_dir):
        self.cfg = cfg
        self.S = int(cfg.get('supersample', 4))
        x0, y0, x1, y1 = cfg['crop']
        self.x0, self.y0 = x0, y0
        self.w, self.h = int((x1 - x0) * self.S), int((y1 - y0) * self.S)
        self.svg_path = os.path.normpath(os.path.join(cfg_dir, cfg['svg']))
        self.size, self.paths = read_svg(self.svg_path)
        self.painting = os.path.normpath(os.path.join(cfg_dir, cfg['painting'])) if cfg.get('painting') else None
        self.tone_names = [t[0] for t in cfg['tones']]

    def px(self, pts):
        """SVG px -> sample grid (float)."""
        p = np.asarray(pts, np.float64)
        return (p - (self.x0, self.y0)) * self.S

    def raster(self):
        lab_img = np.full((self.h, self.w), -1, np.int16)
        for k, (d, _) in enumerate(self.paths):
            subs = parse_path(d)
            if not subs: continue
            allp = np.vstack([self.px(s) for s in subs])
            bx0, by0 = np.floor(allp.min(0)).astype(int); bx1, by1 = np.ceil(allp.max(0)).astype(int) + 1
            bx0, by0 = max(bx0, 0), max(by0, 0); bx1, by1 = min(bx1, self.w), min(by1, self.h)
            if bx1 <= bx0 or by1 <= by0: continue
            m = np.zeros((by1 - by0, bx1 - bx0), np.uint8)
            for s in subs:
                t = np.zeros_like(m)
                q = np.round((self.px(s) - (bx0, by0)) * 4).astype(np.int32)
                cv2.fillPoly(t, [q], 1, lineType=cv2.LINE_8, shift=2)
                m ^= t
            view = lab_img[by0:by1, bx0:bx1]
            view[m == 1] = k
        return lab_img

    def tones(self, labels):
        """Each path's tone index (-1 for the background colours)."""
        names, refs = [], []
        for name, hexes, *_ in self.cfg["tones"]:
            for hx in (hexes if isinstance(hexes, list) else [hexes]):
                names.append(name); refs.append(hx.upper())
        ref_lab = lab(refs)
        cols = [c for _, c in self.paths]
        col_lab = lab(cols)
        bg = set(c.upper() for c in self.cfg.get('background', []))
        over = {k.upper(): v for k, v in self.cfg.get('toneOverrides', {}).items()}
        tone_of_path = np.full(len(self.paths), -1, np.int16)
        for k, c in enumerate(cols):
            if c in bg: continue
            if c in over: tone_of_path[k] = self.tone_names.index(over[c]); continue
            dist = ((ref_lab - col_lab[k]) ** 2).sum(1)
            tone_of_path[k] = self.tone_names.index(names[int(np.argmin(dist))])
        T = np.where(labels >= 0, tone_of_path[np.maximum(labels, 0)], -1).astype(np.int16)
        return T

    def figure(self, T):
        S = self.S
        sil = (T >= 0).astype(np.uint8)
        sc = self.cfg.get('silhouette', {})
        sil = cv2.morphologyEx(sil, cv2.MORPH_CLOSE, disk(sc.get('closeTrace', 1.5) * S))
        if self.painting:
            im = cv2.imread(self.painting).astype(np.int32)
            H0, W0 = im.shape[:2]
            if (W0, H0) != (int(self.size[0]), int(self.size[1])):
                im = cv2.resize(im.astype(np.uint8), (int(self.size[0]), int(self.size[1])), interpolation=cv2.INTER_AREA).astype(np.int32)
            corners = np.vstack([im[:60, :60].reshape(-1, 3), im[:60, -60:].reshape(-1, 3), im[-60:, :60].reshape(-1, 3), im[-60:, -60:].reshape(-1, 3)])
            bgc = np.median(corners, 0)
            diff = np.abs(im - bgc).sum(2)
            m = (diff > sc.get('threshold', 10)).astype(np.uint8)
            m = cv2.morphologyEx(m, cv2.MORPH_OPEN, disk(sc.get('open', 1.5)))
            m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, disk(sc.get('close', 2)))
            n, cc, st, _ = cv2.connectedComponentsWithStats(m, connectivity=8)
            keep = np.zeros_like(m)
            for k in range(1, n):
                if st[k, cv2.CC_STAT_AREA] >= sc.get('minArea', 400): keep[cc == k] = 1
            x0, y0 = self.x0, self.y0
            crop = keep[int(y0):int(y0) + self.h // S + 2, int(x0):int(x0) + self.w // S + 2].astype(np.float32)
            up = cv2.resize(crop, (crop.shape[1] * S, crop.shape[0] * S), interpolation=cv2.INTER_LINEAR)[:self.h, :self.w]
            sil |= (up > 0.5).astype(np.uint8)
        for poly in sc.get('add', []):                      # hand-marked dark cloth the painting barely parts from its canvas
            cv2.fillPoly(sil, [np.round(self.px(poly) * 4).astype(np.int32)], 1, shift=2)
        # holes: small ones are the figure's own dark lines, big ones (between the legs, in a torn hem) stay holes
        filled = fill_holes(sil)
        holes = filled & (1 - sil)
        n, cc, st, _ = cv2.connectedComponentsWithStats(holes, connectivity=4)
        big = sc.get('holeArea', 300) * S * S
        for k in range(1, n):
            if st[k, cv2.CC_STAT_AREA] < big: sil[cc == k] = 1
        for poly in sc.get('cut', []):                      # hand-marked background inside the figure
            cv2.fillPoly(sil, [np.round(self.px(poly) * 4).astype(np.int32)], 0, shift=2)
        fill = self.tone_names.index(sc.get('fill', self.tone_names[0]))
        T = T.copy()
        T[(sil == 1) & (T < 0)] = fill
        T[sil == 0] = -1
        return sil, T

    def despeck(self, T):
        S = self.S
        limit = self.cfg.get('speck', 6) * S * S
        k3 = np.ones((3, 3), np.uint8)
        for _ in range(4):                                   # fixed number of passes
            changed = 0
            for t in range(len(self.tone_names)):
                n, cc, st, _ = cv2.connectedComponentsWithStats((T == t).astype(np.uint8), connectivity=8)
                for k in range(1, n):
                    if st[k, cv2.CC_STAT_AREA] >= limit: continue
                    x, y, w, h = st[k, 0], st[k, 1], st[k, 2], st[k, 3]
                    xa, ya, xb, yb = max(x - 2, 0), max(y - 2, 0), min(x + w + 2, T.shape[1]), min(y + h + 2, T.shape[0])
                    comp = (cc[ya:yb, xa:xb] == k).astype(np.uint8)
                    ring = cv2.dilate(comp, k3) & (1 - comp)
                    around = T[ya:yb, xa:xb][ring == 1]
                    around = around[around != t]
                    if len(around) == 0: continue
                    vals, counts = np.unique(around, return_counts=True)
                    T[ya:yb, xa:xb][comp == 1] = vals[np.argmax(counts)]
                    changed += 1
            if changed == 0: break
        return T

    def poly_mask(self, polys):
        m = np.zeros((self.h, self.w), np.uint8)
        if polys and not isinstance(polys[0][0], (list, tuple)): polys = [polys]
        for p in polys:
            cv2.fillPoly(m, [np.round(self.px(p) * 4).astype(np.int32)], 1, shift=2)
        return m

    def region_masks(self, sil, T):
        regs = self.cfg['regions']
        rough = {n: self.poly_mask(r['poly']) for n, r in regs.items()}
        done, busy = {}, set()

        def get(n):
            if n in done: return done[n]
            if n in busy: raise ValueError('regions minus each other in a loop: ' + n)
            busy.add(n)
            r = regs[n]
            m = rough[n] & sil
            if 'tones' in r: m &= np.isin(T, [self.tone_names.index(t) for t in r['tones']]).astype(np.uint8)
            if 'notTones' in r: m &= (~np.isin(T, [self.tone_names.index(t) for t in r['notTones']])).astype(np.uint8)
            for o in r.get('minus', []): m &= 1 - get(o)
            for o in r.get('minusRough', []): m &= 1 - rough[o]
            busy.discard(n)
            done[n] = m
            return m

        for n in regs: get(n)
        return done, rough

    # ------------------------------------------------------------------------------------------------ polygons
    def contour(self, m, eps):
        """Outer outlines of a mask's islands (no holes inside them), half a sample outside, simplified, in SVG px."""
        S = self.S
        grown = cv2.dilate(m, np.ones((3, 3), np.uint8))      # the outline then runs through the centres of the samples just outside
        cs, _ = cv2.findContours(np.pad(grown, 1), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        out = []
        for c in cs:
            if cv2.contourArea(c) < 2: continue
            for e in (eps, eps * 0.5, eps * 0.25, 0.0):
                a = c if e == 0 else cv2.approxPolyDP(c, e * S, True)
                p = (a.reshape(-1, 2).astype(np.float64) - 1 + 0.5) / S + (self.x0, self.y0)
                p = clean(p, 0.04)
                if len(p) >= 3 and not self_crosses(p): break
            if len(p) >= 3 and not self_crosses(p): out.append(p)
        return out

    def open_holes(self, m, outside):
        """Slits every hole of the mask that contains `outside` pixels open to its edge (a vertical cut 3 samples wide from
        the hole up through the mask), so the mask's outline needs no holes; other holes are left to be filled."""
        m = m.copy()
        for _ in range(60):
            holes = fill_holes(m) & (1 - m)
            n, cc, st, cen = cv2.connectedComponentsWithStats(holes, connectivity=4)
            todo = [k for k in range(1, n) if (outside[cc == k]).any()]
            if not todo: break
            k = todo[0]
            x = int(round(cen[k][0])); ytop = st[k, cv2.CC_STAT_TOP]
            col = m[:ytop + 1, max(x - 1, 0):x + 2]
            col[:] = 0
        return m

    def layers(self, mask, T, base_tone, eps, soft_seg):
        """The region's polygons in paint order: (tone, points). base_tone None: no base layer (a cut of a few tones)."""
        out = []
        outside = (1 - mask).astype(np.uint8)
        if base_tone is not None:
            base = self.open_holes(mask, outside)
            for p in self.contour(base, eps):
                if abs(poly_area(p)) >= self.cfg.get('speck', 6) * 0.5:   # a fleck of the region cut off by a neighbour: dropped
                    out.append((base_tone, p))
        comps = []
        for t in range(len(self.tone_names)):
            tm = ((T == t) & (mask == 1)).astype(np.uint8)
            n, cc, st, _ = cv2.connectedComponentsWithStats(tm, connectivity=8)
            for k in range(1, n):
                if st[k, cv2.CC_STAT_AREA] < 2: continue
                x, y, w, h = st[k, :4]
                xa, ya, xb, yb = max(x - 2, 0), max(y - 2, 0), min(x + w + 2, T.shape[1]), min(y + h + 2, T.shape[0])
                comp = (cc[ya:yb, xa:xb] == k).astype(np.uint8)
                comp = self.open_holes(comp, outside[ya:yb, xa:xb])
                filled = fill_holes(comp)
                ys, xs = np.nonzero(comp)
                comps.append(dict(tone=t, box=(xa, ya, xb, yb), comp=comp, filled=filled, probe=(xs[0] + xa, ys[0] + ya), area=int(comp.sum())))
        # depth: how many other islands' filled outlines hold this one
        for c in comps:
            px_, py_ = c['probe']; d = 0
            for o in comps:
                if o is c: continue
                xa, ya, xb, yb = o['box']
                if xa <= px_ < xb and ya <= py_ < yb and o['filled'][py_ - ya, px_ - xa] and not o['comp'][py_ - ya, px_ - xa]: d += 1
            c['depth'] = d
        comps.sort(key=lambda c: (c['depth'], -c['area']))
        tname = self.tone_names
        for c in comps:
            if c['tone'] == base_tone and c['depth'] == 0: continue          # the base layer already paints it
            xa, ya, xb, yb = c['box']
            full = np.zeros_like(mask); full[ya:yb, xa:xb] = c['filled']
            for p in self.contour(full, eps):
                if abs(poly_area(p)) >= self.cfg.get('speck', 6) * 0.5:
                    out.append((c['tone'], p))
        if soft_seg:
            out = [(t, resample(p, soft_seg)) for t, p in out]
        return out


def fmt(p):
    return ' '.join(f'{v:.2f}' for q in p for v in q)


def main(cfg_path):
    cfg_path = os.path.abspath(cfg_path)
    cfg_dir = os.path.dirname(cfg_path)
    cfg = json.load(open(cfg_path, encoding='utf-8'))
    cut = Cutter(cfg, cfg_dir)
    labels = cut.raster()
    T = cut.tones(labels)
    sil, T = cut.figure(T)
    T = cut.despeck(T)
    masks, rough = cut.region_masks(sil, T)

    eps = cfg.get('eps', 0.45)
    soft_regions = set()
    for p in cfg['parts']:
        if p.get('soft'): soft_regions.add(p.get('region', p['name']))
    base_tone = cut.tone_names.index(cfg.get('base', cut.tone_names[0]))
    regions = {}
    for n, r in cfg['regions'].items():
        rb = cut.tone_names.index(r['base']) if 'base' in r else base_tone
        if 'tones' in r and 'base' not in r: rb = None                     # a tone cut (eye, crack): no base layer
        lay = cut.layers(masks[n], T, rb, r.get('eps', eps), cfg.get('resample', 8) if n in soft_regions else 0)
        outline = cut.contour(fill_holes(masks[n]), max(1.0, eps * 2))
        outline = max(outline, key=lambda p: abs(poly_area(p))) if outline else None
        regions[n] = (outline, lay)
        print(f'{n:16s} polys {len(lay):4d}  points {sum(len(p) for _, p in lay):6d}')

    # ---- write ----
    rel = lambda p: os.path.relpath(p, ROOT).replace('\\', '/')
    sources = [('text', cut.svg_path), ('text', cfg_path), ('text', os.path.abspath(__file__))]
    if cut.painting: sources.append(('binary', cut.painting))
    h = input_hash(sources)
    out_path = os.path.join(ROOT, cfg['out'])
    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    with open(out_path, 'w', newline='\n', encoding='utf-8') as f:
        f.write(f'# GENERATED by Tools/svg_art/svg_rig.py from {rel(cfg_path)} - do not edit by hand.\n')
        f.write(f'svgrig {TOOL_VERSION}\n')
        f.write(f'hash {h}\n')
        for kind, p in sources: f.write(f'source {kind} {rel(p)}\n')
        f.write(f'speck {cfg.get("speck", 6)}\n')
        fit = cfg['fit']
        f.write(f'fit {fit["k"]} {fit["svg"][0]} {fit["svg"][1]} {fit["ref"][0]} {fit["ref"][1]}\n')
        f.write('tones ' + ' '.join(cut.tone_names) + '\n')
        for n, (outline, lay) in regions.items():
            f.write(f'region {n}\n')
            if outline is not None: f.write('outline ' + fmt(outline) + '\n')
            for t, p in lay: f.write(f'poly {cut.tone_names[t]} {fmt(p)}\n')
            f.write('end\n')
        for p in cfg['parts']:
            f.write(f'part {p["name"]}\n')
            f.write(f'region {p.get("region", p["name"])}\n')
            f.write(f'bone {p["bone"]}\n')
            f.write(f'order {p["order"][0]} {p["order"][1] if len(p["order"]) > 1 else 0}\n')
            f.write(f'map {p["map"]}\n')
            for flag in ('rim', 'far', 'sword'):
                if p.get(flag): f.write(flag + '\n')
            if 'glow' in p: f.write(f'glow {p["glow"]}\n')
            if 'role' in p: f.write(f'role {p["role"]}\n')
            if 'swing' in p: f.write('swing ' + ' '.join(str(v) for v in p['swing']) + '\n')
            if 'soft' in p: f.write(f'soft {p["soft"]}\n')
            for ch in p.get('chains', []): f.write('chain ' + ' '.join(str(v) for v in ch) + '\n')
            f.write('end\n')
    print('wrote', rel(out_path), 'hash', h)

    # ---- previews ----
    prev = os.path.join(ROOT, cfg['preview']) if cfg.get('preview') else None
    if not prev: return
    os.makedirs(prev, exist_ok=True)
    tone_bgr = np.array([[int(c[5:7], 16), int(c[3:5], 16), int(c[1:3], 16)] for c in
                         [(t[2] if len(t) > 2 else (t[1][0] if isinstance(t[1], list) else t[1])) for t in cfg['tones']]], np.uint8)
    bgc = np.array([40, 34, 42], np.uint8)
    timg = np.where((T >= 0)[..., None], tone_bgr[np.maximum(T, 0)], bgc)
    small = lambda im: cv2.resize(im, (im.shape[1] * 2 // cut.S, im.shape[0] * 2 // cut.S), interpolation=cv2.INTER_AREA)
    cv2.imwrite(os.path.join(prev, 'tones.png'), small(timg))
    # rebuilt: every region's polygons painted back in order (parts in config order)
    rb = np.full_like(timg, bgc)
    for p in cfg['parts']:
        if p.get('far'): continue
        outline, lay = regions[p.get('region', p['name'])]
        for t, q in lay:
            cv2.fillPoly(rb, [np.round(cut.px(q) * 4).astype(np.int32)], tuple(int(v) for v in tone_bgr[t]), shift=2)
    cv2.imwrite(os.path.join(prev, 'rebuilt.png'), small(rb))
    # regions tinted
    img = timg.astype(np.float32)
    covered = np.zeros_like(sil)
    rng = np.random.RandomState(5)
    names = []
    for n in cfg['regions']:
        m = masks[n]; covered |= m
        col = rng.randint(60, 255, 3).astype(np.float32)
        img[m == 1] = img[m == 1] * 0.45 + col * 0.55
        ys, xs = np.nonzero(m)
        if len(xs): names.append((n, int(xs.mean()), int(ys.mean()), tuple(int(v) for v in col)))
    img[(sil == 1) & (covered == 0)] = (255, 0, 255)
    img = small(img.astype(np.uint8))
    for n, x, y, col in names:
        X, Y = x * 2 // cut.S - 20, y * 2 // cut.S
        cv2.putText(img, n, (X, Y), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (255, 255, 255), 2)
        cv2.putText(img, n, (X, Y), cv2.FONT_HERSHEY_SIMPLEX, 0.4, col, 1)
    cv2.imwrite(os.path.join(prev, 'regions.png'), img)
    print('figure px not in any region:', int(((sil == 1) & (covered == 0)).sum() / cut.S / cut.S))


if __name__ == '__main__':
    main(sys.argv[1])
