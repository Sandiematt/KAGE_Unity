import re, sys, json, math
def parse(path):
    txt = open(path, encoding='utf-8').read()
    out = []
    for m in re.finditer(r'<path d="([^"]+)" fill="([^"]+)" transform="translate\(([-\d.]+),([-\d.]+)\)"', txt):
        d, fill, tx, ty = m.group(1), m.group(2), float(m.group(3)), float(m.group(4))
        toks = re.findall(r'[MCLZ]|-?\d+\.?\d*', d)
        i = 0; subs = []; cur = []; pos = (0, 0)
        while i < len(toks):
            t = toks[i]
            if t == 'M':
                if cur: subs.append(cur)
                pos = (float(toks[i+1]), float(toks[i+2])); cur = [pos]; i += 3
            elif t == 'L':
                pos = (float(toks[i+1]), float(toks[i+2])); cur.append(pos); i += 3
            elif t == 'C':
                p1 = (float(toks[i+1]), float(toks[i+2])); p2 = (float(toks[i+3]), float(toks[i+4])); p3 = (float(toks[i+5]), float(toks[i+6]))
                p0 = pos
                n = max(2, min(8, int((abs(p3[0]-p0[0]) + abs(p3[1]-p0[1])) / 6) + 2))
                for k in range(1, n + 1):
                    u = k / n; a = (1-u)**3; b = 3*(1-u)**2*u; c = 3*(1-u)*u*u; e = u**3
                    cur.append((a*p0[0]+b*p1[0]+c*p2[0]+e*p3[0], a*p0[1]+b*p1[1]+c*p2[1]+e*p3[1]))
                pos = p3; i += 7
            elif t == 'Z':
                i += 1
            else:
                i += 1
        if cur: subs.append(cur)
        h = fill.lstrip('#'); rgb = tuple(int(h[k:k+2], 16) for k in (0, 2, 4))
        for s in subs:
            pts = [(x + tx, y + ty) for x, y in s]
            xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
            area = abs(sum(pts[k][0]*pts[(k+1) % len(pts)][1] - pts[(k+1) % len(pts)][0]*pts[k][1] for k in range(len(pts)))) / 2
            out.append(dict(rgb=rgb, pts=pts, bbox=(min(xs), min(ys), max(xs), max(ys)), area=area))
    return out
def cls(rgb):
    r, g, b = rgb
    if r > 150 and g < 140 and b < 140 and r - g > 60: return 'red'
    if r > 90 and r - g > 40: return 'dred'
    if r > 170 and g > 150: return 'white'
    if r < 20 and g < 20: return 'black'
    if abs(r-29) < 10 and abs(g-24) < 10: return 'bg'
    if r < 70: return 'dark'
    return 'grey'
if __name__ == '__main__':
    ps = parse(sys.argv[1])
    print(len(ps), 'subpaths')
    from collections import Counter
    c = Counter(cls(p['rgb']) for p in ps); print(c)
    for i, p in enumerate(ps):
        if p['area'] > 1500:
            print(i, cls(p['rgb']), p['rgb'], 'area', int(p['area']), 'bbox', tuple(int(v) for v in p['bbox']), 'n', len(p['pts']))
