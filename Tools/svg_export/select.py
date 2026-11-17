import sys
sys.path.insert(0, sys.argv[2])
from svgparse import parse, cls
from PIL import Image, ImageDraw
ps = parse(sys.argv[1])
def cen(p):
    b = p['bbox']; return ((b[0]+b[2])/2, (b[1]+b[3])/2)
def inbox(c, box): return box[0] <= c[0] <= box[2] and box[1] <= c[1] <= box[3]
def head_sel(i, p):
    b = p['bbox']; c = cen(p); k = cls(p['rgb'])
    if i in (0,): return False
    if b[0] < 770 or b[2] > 1000 or b[1] < 45: return False
    if b[3] > 226: return False
    if k in ('red', 'dred') and b[1] > 196: return False      # scarf top
    if k in ('red', 'dred') and b[3] > 212 and c[0] > 840: return False
    return True
def coat_sel(i, p):
    b = p['bbox']; c = cen(p); k = cls(p['rgb'])
    if b[1] < 398 or b[3] > 700 or b[0] < 783 or b[2] > 1068: return False
    if inbox(c, (755, 460, 808, 535)): return False            # sword hilt
    if inbox(c, (878, 418, 942, 456)): return False            # wristband
    if inbox(c, (800, 652, 885, 700)): return False            # knee strap
    if inbox(c, (850, 440, 935, 505)) and k not in ('red', 'dred'): return False   # fist
    if k in ('red', 'dred', 'white'): return True
    if k in ('dark', 'grey') and inbox(c, (868, 540, 1062, 672)): return True
    return False
if __name__ == '__main__':
    H = [(i, p) for i, p in enumerate(ps) if head_sel(i, p)]
    C = [(i, p) for i, p in enumerate(ps) if coat_sel(i, p)]
    print('head', len(H), 'pts', sum(len(p['pts']) for _, p in H)); print('coat', len(C), 'pts', sum(len(p['pts']) for _, p in C))
    for name, G, box, sc in (('head', H, (760, 40, 1010, 240), 4), ('coat', C, (770, 380, 1080, 710), 3)):
        w, h = int((box[2]-box[0])*sc), int((box[3]-box[1])*sc)
        im = Image.new('RGB', (w, h), (120, 100, 105)); d = ImageDraw.Draw(im)
        for i, p in G:
            col = p['rgb']
            d.polygon([((x-box[0])*sc, (y-box[1])*sc) for x, y in p['pts']], fill=col)
        im.save(sys.argv[3] + f'/sel_{name}.png')
    from collections import Counter
    print(Counter(cls(p['rgb']) for _, p in H)); print(Counter(cls(p['rgb']) for _, p in C))
