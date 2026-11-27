"""SAN-72 helmet art: previews and the C# export.

  python Tools/helmet_art/build.py preview <out dir> [ids...]   one PNG per helmet and a sheet, at bind, facing right
  python Tools/helmet_art/build.py export                       writes Assets/Kage/Scripts/Character/Gear/HelmetDesigns.g.cs
  python Tools/helmet_art/build.py sheets <dir>                 the review sheets from the game's tiles (<dir>/tiles, HelmetTierScreenshots)
  python Tools/helmet_art/build.py check                        every outline closed, simple (no self crossing) and not tiny
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit, helmets

def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'preview'
    if cmd == 'sheets':
        import sheets
        sheets.make(sys.argv[2])
        return
    if cmd == 'check':
        bad = 0
        for h in helmets.ALL:
            for p in h.pieces:
                shapes = [('outline', p.outline)] + [('detail%d' % i, d[0]) for i, d in enumerate(p.detail)]
                for name, pts in shapes:
                    why = kit.problem(pts)
                    if not why:
                        ref = [(round(x, 1), round(y, 1)) for (x, y) in kit.to_ref(pts)]
                        ok, A, S = kit.tiles(ref)
                        if not ok: why = 'triangles cover %.1f of %.1f px2' % (S, A)
                    if why:
                        bad += 1
                        print(h.id, p.name, name, why)
        print('pieces', sum(len(h.pieces) for h in helmets.ALL), 'problems', bad)
        return
    if cmd == 'export':
        out = os.path.join(kit.ROOT, 'Assets', 'Kage', 'Scripts', 'Character', 'Gear', 'HelmetDesigns.g.cs')
        kit.export_cs(helmets.ALL, out)
        print('wrote', out, len(helmets.ALL), 'helmets')
        return
    if cmd == 'icons':
        icons_dir = os.path.join(kit.ROOT, 'Assets', 'Kage', 'Resources', 'UI', 'Img')
        os.makedirs(icons_dir, exist_ok=True)
        kit.render_icon(None, size=320).save(os.path.join(icons_dir, 'icon_h_shiro_maki.png'))
        for h in helmets.ALL:
            kit.render_icon(h, size=320).save(os.path.join(icons_dir, f'icon_h_{h.id}.png'))
        print('wrote', 1 + len(helmets.ALL), 'helmet icons to', icons_dir)
        return
    from PIL import Image, ImageDraw, ImageFont
    out = sys.argv[2]
    ids = sys.argv[3:]
    os.makedirs(out, exist_ok=True)
    chosen = [h for h in helmets.ALL if not ids or h.id in ids]
    try: font = ImageFont.truetype('arial.ttf', 14)
    except Exception: font = ImageFont.load_default()
    tiles = []
    for h in chosen:
        img = kit.render(h, centre_uv=(-0.45, -0.55), half=2.75, size=360)
        d = ImageDraw.Draw(img)
        d.text((8, 6), f'{h.id}  [{h.tier}]  hair {h.hair}', fill=(235, 228, 215), font=font)
        img.save(os.path.join(out, h.id + '.png'))
        tiles.append(img)
    if len(tiles) > 1:
        cols = min(5, len(tiles)); rows = (len(tiles) + cols - 1) // cols
        sheet = Image.new('RGBA', (cols * 360, rows * 360), (20, 16, 18, 255))
        for i, t in enumerate(tiles): sheet.paste(t, ((i % cols) * 360, (i // cols) * 360))
        sheet.save(os.path.join(out, '_sheet.png'))
    print('ok', len(tiles))

if __name__ == '__main__':
    main()
