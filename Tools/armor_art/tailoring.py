"""Silk cuts and clan marks, in Kage's bind-pose reference pixels."""
import math
from kit import band, diamond, disc, ellipse, poly, ring, sample, star


def crest(piece, mark, cx, cy, size, silk, metal):
    """Scale one cut-paper emblem for a shoulder badge or the woven coat repeat."""
    def stamp(points, tone=metal, value=2):
        piece.add([(cx + x * size, cy + y * size) for x, y in points], tone, value)

    def cut(*coords):
        stamp(poly(*coords))

    if mark == 'reed':
        cut(-.7, .68, -.46, -.15, -.58, -.84, -.18, -.26, -.22, .35)
        cut(-.36, .68, .06, -.35, .78, -.72, .38, .04)
    elif mark == 'moon':
        stamp(ring(0, 0, 1, .72, 38, 285, 18))
        cut(-.25, .34, .86, -.62, .43, .21)
    elif mark == 'peaks':
        for x, y in [(-.5, .03), (.35, -.23)]:
            stamp([(x-.55, y+.45), (x, y-.46), (x+.55, y+.45), (x, y+.03)])
    elif mark in ('thunder', 'fork'):
        cut(.1, -1, .76, -1, .22, -.18, .7, -.23, -.48, .99, -.17, .21, -.68, .28)
        if mark == 'fork':
            cut(-.96, .06, -.45, -.82, -.13, -.82, -.58, .0)
    elif mark in ('flame', 'cinder'):
        cut(-.7, .43, -.87, -.14, -.46, .02, -.42, -.68, -.12, -.28, .38, -1,
            .2, -.17, .75, -.48, .58, .33, .12, .84, -.3, .97)
        stamp(poly(-.38, .54, -.19, .04, -.02, .3, .35, -.09, .15, .52, -.18, .78), silk, 1)
    elif mark == 'fault':
        cut(-.79, -.82, -.12, -.47, .05, -.58, .79, .23, .08, -.08, -.08, .14)
        cut(-.74, -.22, -.12, .25, .04, .12, .63, .84, -.1, .49, -.27, .61)
    elif mark == 'crane':
        cut(-1, -.72, -.33, -.42, -.1, -.04, .27, -.61, .62, -.78, .45, -.4, .96, -.48,
            .48, .04, .03, .33, -.31, .84, -.25, .26, -.72, .05)
        stamp(poly(-.83, -.55, -.38, -.28, -.12, .06, -.46, -.08), silk, 1)
    elif mark == 'eclipse':
        stamp(ring(0, 0, .92, .69, -70, 220, 20))
        stamp(star(.05, 0, .56, .17))
        stamp(diamond(-.63, -.65, .17, .29))
    elif mark == 'eye':
        cut(-1, .04, -.4, -.5, .27, -.53, 1, -.06, .38, .5, -.35, .48)
        stamp(disc(0, 0, .37, n=12), silk, 0)
        stamp(diamond(0, 0, .13, .28))
    elif mark == 'pages':
        for x, y in [(-.29, -.23), (.15, .12)]:
            stamp(poly(x-.52, y-.61, x+.5, y-.47, x+.4, y+.64, x-.62, y+.49), silk, 2)
            for offset in [-.20, .12]:
                stamp(poly(x-.31, y+offset, x+.27, y+offset+.09, x+.25, y+offset+.17, x-.32, y+offset+.08), metal, 0)
    elif mark == 'gate':
        cut(-.91, -.66, .9, -.66, .9, -.38, -.91, -.38)
        cut(-.62, -.37, -.35, -.37, -.35, .85, -.62, .85)
        cut(.33, -.37, .62, -.37, .62, .85, .33, .85)
        cut(-.82, -.07, .82, -.07, .82, .16, -.82, .16)
    elif mark == 'coil':
        for radius, start in [(1, 72), (.64, -96)]:
            stamp(ring(0, 0, radius, radius-.18, start, start+274, 18))
        stamp(diamond(0, 0, .21, .32))
    elif mark == 'blossom':
        for i in range(5):
            angle = i * 72 - 90
            theta = math.radians(angle)
            stamp(ellipse(.55*math.cos(theta), .55*math.sin(theta), .45, .26, angle, 10), silk, 2)
        stamp(disc(0, 0, .2, n=8))
    elif mark == 'shard':
        for x, y, w, h in [(-.55, .02, .29, .55), (.04, -.16, .35, .94), (.62, .1, .22, .56)]:
            stamp(diamond(x, y, w, h))
            stamp([(x, y-h), (x+w, y), (x, y+h)], silk, 1)
    elif mark == 'fan':
        cut(-.94, -.28, -.53, -.77, .14, -.86, .79, -.52, .96, -.13, .05, .79)
        for x in [-.55, 0, .56]:
            stamp(band([(x, -.53), (.04, .56)], .085), silk, 0)
    else:
        raise ValueError(mark)


def front_panel(armor, mark, field, lining, metal, length, width, sweep, hem):
    """Pattern cut on the bias. The seam, embroidery and spring share a centreline."""
    def at(u, v):
        return (850 + sweep * v + u * width * (.58 + .42 * v), 398 + length * v)

    perimeter = [(-1, 0), (1, 0), (1, .54), *hem, (-1, .48)]
    cloth = armor.cloth('HaoriTailFront', 'Hips', 65, [at(u, v) for u, v in perimeter], field,
                        [sample([at(0, 0), at(0, 1)], 5)], 'Haori', sd=4, hd=1.8, rim=False)
    cloth.add([at(u, v) for u, v in [(-1, .04), (-.64, .08), (-.8, .52), (-.69, .8), (-1, .83)]], lining, 0)
    cloth.add(band([at(u * .90, v - .034) for u, v in hem], 4.5), metal, 1)
    x, y = at(.04, .64)
    crest(cloth, mark, x, y, width * .57, lining, metal)
    if armor.tier == 'legendary':
        for u, v, r in [(-.38, .47, 4), (.38, .78, 4), (.36, .53, 2.7)]:
            x, y = at(u, v)
            cloth.add(star(x, y, r, r*.36), metal, 2)
    return cloth


# Hems listed from the back edge to the front edge.
SLANT = [(1, .78), (-.62, 1), (-1, .94)]
SPLIT = [(1, .88), (.42, .99), (.08, .83), (-.56, 1), (-1, .88)]
POINT = [(1, .82), (-.15, 1), (-1, .84)]
SQUARE = [(1, .92), (.74, 1), (-.85, .96), (-1, .89)]


def fit_shoulder(armor, width, lift):
    """Grade all overlapping plates and their inlays together about the upper-arm seam."""
    def fit(pts):
        return [(916 + (x - 916) * width, 265 + (y - 265) * lift) for x, y in pts]

    for p in armor.pieces:
        if p.bone != 'ArmNU' or not p.name.startswith('Sode'):
            continue
        p.outline = fit(p.outline)
        p.detail = [(fit(pts), tone, value, alpha) for pts, tone, value, alpha in p.detail]


def tailor_act_one(armors):
    # Cloth colours, hem proportions and shoulder ease are selected per garment.
    cuts = [
        ('ronin', 'reed', 'leather', 'cloth', 'rope', 147, 32, 4, SLANT, .76, .78),
        ('yami', 'moon', 'plate', 'cloth', 'accent', 180, 35, 12, POINT, .79, .88),
        ('shinsen', 'peaks', 'enamel', 'lacquer', 'cloth', 190, 46, -8, SQUARE, .87, .90),
        ('raiko', 'thunder', 'lacquer', 'cloth', 'gold', 228, 45, 16, SLANT, .91, .91),
        ('kagehira', 'flame', 'lacquer', 'cloth', 'gold', 234, 42, -7, SPLIT, .93, .90),
        ('half_do', 'fault', 'iron_dark', 'lace', 'glow', 189, 48, 9, SLANT, .92, .93),
        ('vanguard_plate', 'fork', 'steel', 'violet', 'silver', 217, 42, -4, POINT, .91, .94),
        ('yurei', 'crane', 'indigo', 'enamel', 'gold', 272, 51, -15, SPLIT, .94, .94),
        ('mugen', 'eclipse', 'obsidian', 'violet', 'gold', 290, 52, 6, POINT, .97, .93),
    ]
    designs = {a.look: a for a in armors}
    for look, mark, field, lining, metal, length, width, sweep, hem, sx, sy in cuts:
        a = designs[look]
        cap_name = {'ronin': 'Sode', 'yami': 'SodeUpper', 'yurei': 'SodeTier2',
                    'mugen': 'SodeTier2'}.get(look, 'SodeTier1')
        cap = next(p for p in a.pieces if p.name == cap_name)
        # Reserve a clean field on the crown; the articulated lames retain their edge binding.
        cap.detail.clear()
        if look == 'ronin':
            # A repaired leather patch on a strapped shoulder, with three coarse stitches.
            cap.add(poly(884, 259, 912, 247, 951, 258, 945, 293, 907, 306, 886, 291), 'leather', 1)
            for x in (896, 906, 916):
                cap.add(band([(x, 286), (x+6, 291)], 2.4), 'rope', 1)
        elif look == 'raiko':
            # Lightning conductor forged into the top plate, hooked toward the rear.
            cap.add(poly(879, 262, 901, 242, 921, 242, 939, 218, 935, 240, 968, 230,
                         951, 253, 923, 262, 905, 278), 'gold', 1)
            cap.add(poly(897, 253, 920, 247, 934, 230, 923, 254, 905, 262), 'gold', 2)
        elif look == 'kagehira':
            cap.add(poly(880, 259, 909, 248, 923, 232, 924, 243, 954, 221, 948, 242,
                         975, 229, 957, 257, 922, 270, 891, 273), 'iron', 1)
            cap.add(poly(909, 259, 932, 244, 927, 252, 955, 237, 941, 255, 923, 262), 'gold', 2)
        else:
            cap.add(poly(884, 259, 911, 246, 943, 243, 952, 255, 922, 284, 894, 282), field, 0)
            crest(cap, mark, 921, 264, 15, lining, metal)
        if look == 'yurei':
            cape = next(p for p in a.pieces if p.name == 'CapeOuter')
            # Flight feathers follow the cape's existing spring field and fold with its hem.
            for x, y, span in [(966, 343, 74), (972, 402, 83), (980, 467, 88)]:
                cape.add(poly(x, y, x+15, y+20, x+span, y+123, x+span-25, y+107,
                              x+span-36, y+105, x+16, y+53), 'cloth', 0)
                cape.add(poly(x+6, y+12, x+span-8, y+116, x+span-33, y+91), 'gold', 2)
        elif look == 'mugen':
            cape = next(p for p in a.pieces if p.name == 'CapeOuter')
            for cy, r in [(534, 39), (546, 26)]:
                cape.add(ring(1020, cy, r, r-4, 12, 261, 21), 'violet', 2)
            cape.add(star(1021, 530, 25, 6), 'gold', 1)
        fit_shoulder(a, sx, sy)
        front_panel(a, mark, field, lining, metal, length, width, sweep, hem)
