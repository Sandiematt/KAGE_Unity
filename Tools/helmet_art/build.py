"""SAN-72 helmet art: previews and the C# export.

  python Tools/helmet_art/build.py preview <out dir> [ids...]   one PNG per helmet and a sheet, at bind, facing right
  python Tools/helmet_art/build.py export                       writes Assets/Kage/Scripts/Character/Gear/HelmetDesigns.g.cs
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import kit, helmets

def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'preview'
    if cmd == 'export':
        out = os.path.join(kit.ROOT, 'Assets', 'Kage', 'Scripts', 'Character', 'Gear', 'HelmetDesigns.g.cs')
        kit.export_cs(helmets.ALL, out)
        print('wrote', out, len(helmets.ALL), 'helmets')
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
        img = kit.render(h, size=360)
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
