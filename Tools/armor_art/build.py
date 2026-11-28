"""5-tier armor art CLI: geometry checks, preview sheets, icon export, and C# codegen.

  python Tools/armor_art/build.py check                     verify every outline & detail polygon triangulates cleanly
  python Tools/armor_art/build.py preview <out dir> [ids]   render full-body & torso closeup preview sheets
  python Tools/armor_art/build.py export                    write Assets/Kage/Scripts/Character/Gear/ArmorDesigns.g.cs
  python Tools/armor_art/build.py icons                     write icon_a_<id>.png portraits to Resources/UI/Img
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit, armors


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'preview'
    if cmd == 'check':
        bad = 0
        for a in armors.ALL:
            for p in a.pieces:
                if p.tone not in a.palette:
                    bad += 1
                    print(a.id, p.name, 'missing tone', p.tone)
                shapes = [('outline', p.outline)] + [(f'detail{i}', d[0]) for i, d in enumerate(p.detail)]
                for name, pts in shapes:
                    why = kit.problem(pts)
                    if not why:
                        ref = [(round(x, 1), round(y, 1)) for (x, y) in pts]
                        ok, A, S = kit.tiles(ref)
                        if not ok:
                            why = f'triangles cover {S:.1f} of {A:.1f} px2'
                    if why:
                        bad += 1
                        print(a.id, p.name, name, why)
                for i, d in enumerate(p.detail):
                    if d[1] not in a.palette:
                        bad += 1
                        print(a.id, p.name, f'detail{i} missing tone', d[1])
        print('armors', len(armors.ALL), 'pieces', sum(len(a.pieces) for a in armors.ALL), 'problems', bad)
        sys.exit(1 if bad else 0)

    if cmd == 'export':
        out = os.path.join(kit.ROOT, 'Assets', 'Kage', 'Scripts', 'Character', 'Gear', 'ArmorDesigns.g.cs')
        kit.export_cs(armors.ALL, out)
        print('wrote', out, len(armors.ALL), 'armors')
        return

    if cmd == 'icons':
        icons_dir = os.path.join(kit.ROOT, 'Assets', 'Kage', 'Resources', 'UI', 'Img')
        os.makedirs(icons_dir, exist_ok=True)
        for a in armors.ALL:
            # Keep the whole silhouette inside the card, including Mugen's halo and the boots.
            points = [pt for _, _, polygon, _ in kit.draw_list(a) for pt in polygon]
            xs, ys = zip(*points)
            cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
            half = max(max(xs) - min(xs), max(ys) - min(ys)) * 0.53
            img = kit.render(a, cx=cx, cy=cy, half=half, size=256, ss=2, bg=(0, 0, 0, 0))
            img.save(os.path.join(icons_dir, f'icon_a_{a.id}.png'))
        print('wrote', len(armors.ALL), 'armor icons to', icons_dir)
        return

    from PIL import Image, ImageDraw, ImageFont
    out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(kit.ROOT, 'Screenshots', 'review', 'armor-redesign', 'py')
    ids = sys.argv[3:]
    os.makedirs(out, exist_ok=True)
    chosen = [a for a in armors.ALL if not ids or a.id in ids]
    try:
        font = ImageFont.truetype('arial.ttf', 15)
    except Exception:
        font = ImageFont.load_default()

    body_tiles, torso_tiles = [], []
    # Include starter baseline Kage as cell 0 when rendering all
    if not ids:
        b0 = kit.render(None, cx=920.0, cy=455.0, half=400.0, size=420)
        ImageDraw.Draw(b0).text((10, 8), 'tachibana_yoroi [starter]', fill=(220, 214, 205), font=font)
        body_tiles.append(b0)
        t0 = kit.render(None, cx=895.0, cy=365.0, half=220.0, size=420)
        ImageDraw.Draw(t0).text((10, 8), 'tachibana_yoroi [starter]', fill=(220, 214, 205), font=font)
        torso_tiles.append(t0)

    for a in chosen:
        b = kit.render(a, cx=920.0, cy=455.0, half=400.0, size=420)
        ImageDraw.Draw(b).text((10, 8), f'{a.id}  [{a.tier}]', fill=(235, 228, 215), font=font)
        b.save(os.path.join(out, a.id + '_body.png'))
        body_tiles.append(b)

        t = kit.render(a, cx=895.0, cy=365.0, half=220.0, size=420)
        ImageDraw.Draw(t).text((10, 8), f'{a.id}  [{a.tier}]', fill=(235, 228, 215), font=font)
        t.save(os.path.join(out, a.id + '_torso.png'))
        torso_tiles.append(t)

    def make_sheet(tiles, fname):
        if len(tiles) <= 1:
            return
        cols = min(5, len(tiles))
        rows = (len(tiles) + cols - 1) // cols
        sheet = Image.new('RGBA', (cols * 420, rows * 420), (18, 15, 19, 255))
        for i, im in enumerate(tiles):
            sheet.paste(im, ((i % cols) * 420, (i // cols) * 420))
        sheet.save(os.path.join(out, fname))

    make_sheet(body_tiles, '_sheet_body.png')
    make_sheet(torso_tiles, '_sheet_torso.png')
    print('ok', len(chosen), '-> ', out)


if __name__ == '__main__':
    main()
