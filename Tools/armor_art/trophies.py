"""The eight later-act rewards. Each carries its boss's device in the armour itself."""
from kit import band, box, diamond, disc, poly, sample
from components import (add_high_collar, add_haori_lapels, add_split_coat_tails, add_kusazuri,
                        add_obi, add_kote, add_suneate, add_arm_sleeve, add_far_sode)
from tailoring import crest, front_panel, POINT, SLANT, SPLIT, SQUARE


def build_trophies(make):
    # Plate, silk and binding are shadow / local / light swatches. Kage keeps his red scarf.
    designs = [
        ('shirabe_do', 'operative_vest', 'eye', 'Shirabe courier vest with a watchful eye and sealed field dispatches.',
         '152232 30475A 57758B', '273642 52716C 98B4A4', '73633B B99961 E8CF94',
         'vest', 'short', 154, 36, 12, SLANT, 2),
        ('kasane_haori', 'ledger_coat', 'pages', 'Ledger haori with inked folios, a wax seal and a squared bookcloth sleeve.',
         '222236 464660 757490', '615649 A89577 F1E1BD', '442636 914252 D58489',
         'haori', 'mid', 239, 45, -8, SQUARE, 2),
        ('tetsumon_do', 'gate_plate', 'gate', 'Iron gate cuirass with bronze hinges, crossbar and a riveted tower shoulder.',
         '222E34 42575A 7D9090', '283432 48564A 86917C', '73522C AF8546 EAD295',
         'vest', 'mid', 175, 54, 0, SQUARE, 3),
        ('kudamaki_do', 'conduit_harness', 'coil', 'Copper-wound induction harness with a glass reservoir and coil housing.',
         '153335 295955 548F84', '2B233E 60517A A794CB', '66402E BF7C50 F0BF86',
         'tactical', 'short', 177, 34, 21, POINT, 2),
        ('mei_en_garb', 'mei_en', 'blossom', 'Plum flower armour with overlapping petal plates and a flowering silk sash.',
         '3E2033 763F59 BB758B', '652F53 A4547D F6BECF', '78603E C2A270 F9E3AD',
         'haori', 'mid', 250, 39, -12, SLANT, 2),
        ('kysmei_robe', 'kysmei', 'shard', 'Witness robe with a fractured crystal collar and a shard-cut, embroidered train.',
         '231C3B 4E3B69 8165A0', '352039 783657 C87894', '594B88 A999DA E2DDFF',
         'haori', 'mid', 259, 40, 15, SPLIT, 3),
        ('kurokumo_yoroi', 'tyrant', 'cinder', 'Scorched charcoal lamellar with a saw-toothed mantle and a banked ember at its heart.',
         '252830 40414A 6D6D74', '441F1F 84392D E37C4F', '723E30 CD7552 FFD39A',
         'warlord', 'mid', 218, 49, 18, SPLIT, 3),
        ('shinsei_yoroi', 'shinsei', 'fan', 'Court armour with peacock silk, an embroidered fan and gilt fan ribs.',
         '322033 644158 976F81', '15363E 2E686F 8FBDB5', '765633 C3A561 F8E5AA',
         'warlord', 'mid', 253, 48, -5, POINT, 3),
    ]
    def tones(swatches):
        return tuple('#' + color for color in swatches.split())

    for aid, look, mark, summary, plate, cloth, metal, cut, length, drop, width, sweep, hem, count in designs:
        palette = dict(plate=tones(plate), silk=tones(cloth), metal=tones(metal),
                       ink=('#101016', '#20232B', '#424754'), rim=tones(plate)[2])
        a = make(aid, look, 'trophy', summary, palette, retones_coat=False)
        add_high_collar(a, 'plate', 'metal', 'silk', height='tall' if mark == 'shard' else 'short')
        chest = a.cel('Do', 'Chest', 53, poly(823, 253, 861, 244, 926, 250, 945, 268, 950, 317,
                                          947, 362, 874, 370, 808, 357, 811, 306), 'plate')
        chest.add(poly(820, 267, 851, 252, 872, 252, 830, 351, 813, 350), 'silk', 1)
        add_haori_lapels(a, 'silk', 'plate', 'metal', style=cut)
        add_far_sode(a, 'plate', 'metal')
        add_arm_sleeve(a, 'silk', 'metal')
        shoulder = a.cel('Sode', 'ArmNU', 107, SHOULDERS[mark], 'plate', rim=False)
        shoulder.add(band([(890, 258), (917, 249), (947, 256)], 3.6), 'metal', 2)
        crest(shoulder, mark, 918, 278, 22, 'silk', 'metal')
        chest_device(a, mark)
        add_obi(a, 'silk', 'metal', 'plate', style='bar' if mark in ('gate', 'pages') else 'rect')
        add_split_coat_tails(a, 'plate', 'silk', 'metal', length=length, notched=hem == SPLIT)
        front_panel(a, mark, 'plate', 'silk', 'metal', drop, width, sweep, hem)
        add_kusazuri(a, count, 67 if length == 'short' else 82, 'plate', 'metal', 'silk', rows=count,
                pointed=hem in (SPLIT, POINT), staggered=hem == SLANT)
        add_kote(a, 'plate', 'metal', 'silk', with_tekko=mark in ('gate', 'cinder', 'coil'), heavy=mark == 'gate')
        add_suneate(a, 'plate', 'metal', 'silk')


# Different constructions around the same arm socket, from paper shingles to a fan of silk.
SHOULDERS = {
    'eye': poly(884, 256, 912, 246, 944, 252, 950, 275, 941, 304, 916, 318, 889, 303),
    'pages': poly(882, 257, 913, 245, 951, 253, 958, 305, 940, 302, 941, 321, 891, 309),
    'gate': poly(878, 252, 941, 242, 958, 253, 961, 329, 951, 339, 889, 336, 883, 322),
    'coil': poly(883, 258, 905, 244, 935, 244, 953, 263, 953, 293, 933, 312, 903, 309, 887, 293),
    'blossom': poly(883, 254, 911, 244, 942, 250, 958, 265, 954, 288, 943, 288, 948, 303, 923, 321, 899, 308, 887, 287),
    'shard': poly(884, 258, 910, 241, 934, 247, 953, 232, 947, 260, 962, 257, 949, 298, 926, 322, 925, 307, 907, 316, 889, 289),
    'cinder': poly(878, 256, 897, 240, 901, 224, 914, 243, 937, 232, 951, 215, 950, 246, 967, 240, 961, 273, 969, 296, 954, 292, 958, 319, 931, 339, 891, 320),
    'fan': poly(876, 264, 892, 247, 917, 238, 940, 240, 961, 254, 969, 272, 963, 287, 947, 300, 924, 321, 898, 305),
}


def chest_device(a, mark):
    """Boss relics sit between the lapels or hang from the back shoulder seam."""
    if mark == 'coil':
        tank = a.cel('GlassReservoir', 'Chest', 47, poly(942, 252, 962, 245, 981, 252, 986, 317, 976, 330, 955, 327), 'metal')
        tank.add(poly(952, 260, 974, 257, 979, 308, 960, 313), 'silk', 2)
        for y in (266, 281, 296):
            tank.add(band([(956, y+5), (978, y)], 4.2), 'plate', 1)
    elif mark in ('cinder', 'shard', 'fan'):
        tip = {'cinder': (1033, 485), 'shard': (1012, 456), 'fan': (1008, 394)}[mark]
        x, y = tip
        p = a.cloth('HalfMantle', 'Chest', 46, poly(939, 248, 956, 243, x-14, y-82, x, y,
                    x-23, y-17, x-35, y+8, 952, 355), 'silk',
                    [sample([(948, 248), (962, 330), (x-18, y-8)], 4)], 'Haori', rim=False)
        p.add(band([(953, 253), (x-18, y-76), (x-5, y-9)], 4), 'metal', 1)
    if mark == 'gate':
        bar = a.cel('Crossbar', 'Chest', 60, box(809, 295, 885, 315, 3), 'ink')
        for x in [821, 863]:
            bar.add(box(x, 290, x+10, 320), 'metal', 1)
            bar.add(disc(x+5, 299, 2.5, n=8), 'metal', 2)
    elif mark == 'pages':
        p = a.cel('Ledger', 'Chest', 60, poly(820, 272, 862, 278, 858, 334, 813, 328), 'silk')
        for y in [286, 297, 308]:
            p.add(band([(823, y), (850, y+4)], 2.6), 'ink', 0)
    elif mark == 'blossom':
        p = a.cel('PetalLames', 'Chest', 60, poly(817, 268, 855, 261, 868, 275, 854, 301, 821, 320, 810, 312), 'silk')
        p.add(band([(813, 309), (824, 313), (853, 294), (863, 275)], 3.8), 'metal', 2)
    elif mark == 'eye':
        p = a.cel('DispatchCase', 'Chest', 60, poly(817, 286, 856, 279, 864, 316, 823, 325), 'silk')
        p.add(poly(819, 288, 855, 282, 843, 302), 'metal', 1)
    else:
        p = a.cel('RelicMount', 'Chest', 60, diamond(837, 299, 23, 32), 'silk', rim=False)
        crest(p, mark, 837, 298, 16, 'plate', 'metal')
    a.flat('RelicSeal', 'Chest', 61, diamond(837, 310, 4, 6), 'metal', which=2, glow=1.85)
