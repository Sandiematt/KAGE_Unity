"""The SAN-72 review sheets, laid out from the tiles the game renders (HelmetTierScreenshots writes <dir>/tiles/<id>_<pose>.png):

  tier_sheet.png    the owner's layout: five tier columns (COMMON, EPIC, TROPHY, LEGENDARY, GOD MODE), every Act I helmet in its tier
  poses.png         every helmet in idle, walk, attack, hit and a head wind (rows by tier)
  pose_<pose>.png   one pose, every helmet, five to a row
  bodies.png        every helmet on the whole of Kage, mid-walk

  python Tools/helmet_art/build.py sheets Screenshots/review/SAN-72/tiers
"""
import os, re
from PIL import Image, ImageDraw, ImageFont
import kit, helmets

POSES = ['idle', 'walk', 'attack', 'hit', 'wind']
BG = (17, 13, 15, 255)
TIER_LINES = {
    'common': 'Grey cloth, plain steel, straw. No glow.',
    'epic': 'Dark angular shell, violet blades, a violet glow.',
    'trophy': 'White enamel and gold. Ceremonial.',
    'legendary': 'Black lacquer kabuto, gold crest, red mon.',
    'god': 'Demonic shell, burning red horns and flames.',
}


def names():
    src = open(os.path.join(kit.ROOT, 'Assets', 'Kage', 'Rules', 'Generated', 'JsTables.g.cs'), encoding='utf-8').read()
    i = src.index('public static readonly JsGear[] Helmets')
    blk = src[i:src.index('};', i)]
    return {m.group(1): m.group(2).encode('ascii').decode('unicode_escape') for m in re.finditer(r'Id = "([^"]+)",\s*Name = "([^"]+)"', blk)}


def order():
    """(id, tier) in the owner's tier order; the starter leads the common column."""
    out = [('shiro_maki', 'common')]
    for t in kit.TIER_ORDER:
        out += [(h.id, h.tier) for h in helmets.ALL if h.tier == t]
    return out


def font(size, bold=False):
    for f in (('arialbd.ttf' if bold else 'arial.ttf'), 'DejaVuSans.ttf'):
        try: return ImageFont.truetype(f, size)
        except Exception: pass
    return ImageFont.load_default()


def col(hexs):
    h = hexs.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), 255)


def tile(d, name, size):
    return Image.open(os.path.join(d, 'tiles', name + '.png')).convert('RGBA').resize((size, size), Image.LANCZOS)


def heading(draw, x, w, y, tier, f_big, f_small):
    c = col(kit.TIER_COLOURS[tier])
    t = kit.TIER_NAMES[tier]
    tw = draw.textlength(t, font=f_big)
    draw.text((x + (w - tw) / 2, y), t, fill=c, font=f_big)
    ly = y + 52
    cx = x + w / 2
    draw.line((x + 24, ly, cx - 14, ly), fill=c, width=2)
    draw.line((cx + 14, ly, x + w - 24, ly), fill=c, width=2)
    draw.polygon([(cx, ly - 9), (cx + 9, ly), (cx, ly + 9), (cx - 9, ly)], fill=c)
    line = TIER_LINES[tier]
    lw = draw.textlength(line, font=f_small)
    draw.text((x + (w - lw) / 2, ly + 16), line, fill=(225, 218, 210, 255), font=f_small)


def make(d):
    nm = names()
    seq = order()
    T = 300
    colw = T + 30
    tiers = kit.TIER_ORDER
    rows = max(sum(1 for _, t in seq if t == k) for k in tiers)
    top = 230
    W = colw * len(tiers) + 60
    H = top + rows * (T + 44) + 30
    sheet = Image.new('RGBA', (W, H), BG)
    dr = ImageDraw.Draw(sheet)
    dr.text((40, 30), 'KAGE', fill=(214, 40, 48, 255), font=font(64, True))
    dr.text((44, 104), 'HELMET DESIGNS  (SIDE VIEW, IN GAME)', fill=(225, 218, 210, 255), font=font(22))
    rp = 'RARITY PROGRESSION'
    dr.text((W - 40 - dr.textlength(rp, font=font(18)), 44), rp, fill=(190, 182, 176, 255), font=font(18))
    for ci, t in enumerate(tiers):
        x = 30 + ci * colw
        heading(dr, x, colw, 140, t, font(34, True), font(15))
        for ri, (hid, _) in enumerate([s for s in seq if s[1] == t]):
            y = top + ri * (T + 44)
            sheet.paste(tile(d, f'{hid}_idle', T), (x + 15, y))
            label = f'{nm.get(hid, hid)}'
            lw = dr.textlength(label, font=font(18, True))
            dr.text((x + (colw - lw) / 2, y + T + 6), label, fill=col(kit.TIER_COLOURS[t]), font=font(18, True))
            if hid == 'shiro_maki':
                s = 'starter: his own scarf'
                dr.text((x + (colw - dr.textlength(s, font=font(14))) / 2, y + T + 26), s, fill=(170, 164, 158, 255), font=font(14))
    sheet.convert('RGB').save(os.path.join(d, 'tier_sheet.png'))

    # every pose of every helmet
    P = 230
    lab = 210
    poses = Image.new('RGBA', (lab + P * len(POSES), 40 + P * len(seq)), BG)
    dr = ImageDraw.Draw(poses)
    for pi, p in enumerate(POSES):
        dr.text((lab + pi * P + 10, 10), p.upper(), fill=(225, 218, 210, 255), font=font(18, True))
    for ri, (hid, t) in enumerate(seq):
        y = 40 + ri * P
        dr.text((10, y + P / 2 - 26), nm.get(hid, hid), fill=(235, 228, 220, 255), font=font(18, True))
        dr.text((10, y + P / 2 + 2), kit.TIER_NAMES[t], fill=col(kit.TIER_COLOURS[t]), font=font(16, True))
        for pi, p in enumerate(POSES):
            poses.paste(tile(d, f'{hid}_{p}', P), (lab + pi * P, y))
    poses.convert('RGB').save(os.path.join(d, 'poses.png'))

    # one pose per sheet, and the whole body
    for p in POSES + ['body']:
        C = 5
        R_ = (len(seq) + C - 1) // C
        S = 300
        sh = Image.new('RGBA', (C * S, R_ * (S + 30)), BG)
        dr = ImageDraw.Draw(sh)
        for i, (hid, t) in enumerate(seq):
            x, y = (i % C) * S, (i // C) * (S + 30)
            sh.paste(tile(d, f'{hid}_{p}', S), (x, y))
            dr.text((x + 8, y + S + 4), f'{nm.get(hid, hid)}  ·  {kit.TIER_NAMES[t]}', fill=col(kit.TIER_COLOURS[t]), font=font(16, True))
        sh.convert('RGB').save(os.path.join(d, 'bodies.png' if p == 'body' else f'pose_{p}.png'))
    print('sheets written to', d)
