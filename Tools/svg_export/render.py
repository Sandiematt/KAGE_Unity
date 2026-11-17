import sys
sys.path.insert(0, sys.argv[2])
from svgparse import parse, cls
from PIL import Image, ImageDraw
ps = parse(sys.argv[1])
def render(box, scale, out, boost=True, labels=False):
    w, h = int((box[2]-box[0])*scale), int((box[3]-box[1])*scale)
    im = Image.new('RGB', (w, h), (29, 24, 27)); d = ImageDraw.Draw(im)
    for i, p in enumerate(ps):
        b = p['bbox']
        if b[2] < box[0] or b[0] > box[2] or b[3] < box[1] or b[1] > box[3]: continue
        col = p['rgb']
        if boost:
            c = cls(col)
            if c == 'bg': col = (95, 85, 90)
            elif c == 'black': col = (10, 8, 9)
            elif c == 'dark': col = tuple(min(255, int(v*1.6)+10) for v in col)
        d.polygon([((x-box[0])*scale, (y-box[1])*scale) for x, y in p['pts']], fill=col)
    for x in range(int(box[0]//50*50), int(box[2]), 50):
        if x >= box[0]: d.line([((x-box[0])*scale, 0), ((x-box[0])*scale, h)], fill=(0, 120, 120)); d.text(((x-box[0])*scale+2, 2), str(x), fill=(255, 255, 0))
    for y in range(int(box[1]//50*50), int(box[3]), 50):
        if y >= box[1]: d.line([(0, (y-box[1])*scale), (w, (y-box[1])*scale)], fill=(0, 120, 120)); d.text((2, (y-box[1])*scale+2), str(y), fill=(255, 255, 0))
    im.save(out)
render((760, 40, 1010, 240), 4, sys.argv[3] + '/svg_head.png')
render((760, 360, 1090, 700), 3, sys.argv[3] + '/svg_coat.png')
