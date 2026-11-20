"""Builds the review images: side-by-side stills (JS left, Unity right) and animation frame strips (JS row over Unity row).

  python Tools/js_ui_capture/compose.py --js <dir of JS stills> --unity <dir of Unity stills> --out <side_by_side dir> name=jsname[,unityname] ...
  python Tools/js_ui_capture/compose.py --strips --js <jsanim dir> --unity <unity animations dir> --out <animations dir>
"""
import argparse, os, glob, re
from PIL import Image, ImageDraw, ImageFont

ap = argparse.ArgumentParser()
ap.add_argument('--js'); ap.add_argument('--unity'); ap.add_argument('--out'); ap.add_argument('--strips', action='store_true')
ap.add_argument('names', nargs='*')
a = ap.parse_args()
os.makedirs(a.out, exist_ok=True)
try:
    font = ImageFont.truetype('arial.ttf', 15)
except Exception:
    font = ImageFont.load_default()

def label(img, text):
    d = ImageDraw.Draw(img); d.text((8, 6), text, fill=(220, 214, 200), font=font)

def find(d, n):
    for ext in ('png', 'jpg'):
        p = os.path.join(d, n + '.' + ext)
        if os.path.exists(p): return p
    return None

if not a.strips:
    for spec in a.names:
        name, _, rest = spec.partition('=')
        jn, _, un = (rest or name).partition(',')
        un = un or jn
        pj, pu = find(a.js, jn), find(a.unity, un)
        if not pj or not pu:
            print('missing', name, pj, pu); continue
        W, H = 800, 450
        sheet = Image.new('RGB', (W * 2 + 10, H + 28), (14, 12, 11))
        sheet.paste(Image.open(pj).convert('RGB').resize((W, H), Image.LANCZOS), (0, 28))
        sheet.paste(Image.open(pu).convert('RGB').resize((W, H), Image.LANCZOS), (W + 10, 28))
        label(sheet, 'JSgame: ' + jn)
        d = ImageDraw.Draw(sheet); d.text((W + 18, 6), 'Unity: ' + un, fill=(220, 214, 200), font=font)
        sheet.save(os.path.join(a.out, name + '.png'))
        print('ok', name)
else:
    CW, CH = 400, 225
    for ud in sorted(glob.glob(os.path.join(a.unity, '*'))):
        if not os.path.isdir(ud): continue
        name = os.path.basename(ud)
        ufr = sorted(glob.glob(os.path.join(ud, 'f*.png')))
        jfr = sorted(glob.glob(os.path.join(a.js, name, 'f*.png')))
        if not ufr: continue
        ut = [int(re.search(r'f(\d+)', os.path.basename(f)).group(1)) for f in ufr]
        jt = [int(re.search(r'f(\d+)', os.path.basename(f)).group(1)) for f in jfr]
        n = len(ufr)
        sheet = Image.new('RGB', (CW * n, CH * 2 + 56), (14, 12, 11))
        for i, (f, t) in enumerate(zip(ufr, ut)):
            sheet.paste(Image.open(f).convert('RGB').resize((CW, CH), Image.LANCZOS), (i * CW, 28 + CH + 28))
            d = ImageDraw.Draw(sheet); d.text((i * CW + 6, 28 + CH + 8), 'Unity %d ms' % t, fill=(220, 214, 200), font=font)
            if jfr:
                j = min(range(len(jt)), key=lambda k: abs(jt[k] - t))
                sheet.paste(Image.open(jfr[j]).convert('RGB').resize((CW, CH), Image.LANCZOS), (i * CW, 28))
                d.text((i * CW + 6, 8), 'JS %d ms' % jt[j], fill=(220, 214, 200), font=font)
        sheet.save(os.path.join(a.out, name + '.png'))
        print('ok', name, n, 'frames')
