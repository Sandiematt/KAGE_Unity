"""Body-fitted armour components shared by shop sets and boss rewards."""
from kit import *

# ------------------------------------------------------------------------------------------------ reusable armor builders

def add_rivets(piece, pts, tone, r=2.3, which=2):
    """Crisp metallic rivets along a pauldron lame, collar, or armored lapel."""
    for rx, ry in pts:
        piece.add(disc(rx, ry, r + 0.9, n=8), 'ink', 0)
        piece.add(disc(rx, ry, r, n=8), tone, which)


def add_high_collar(a, tone, trim_tone, lining_tone=None, height='mid', spiked=False, rivets=False, order=50):
    """Tailored standing high collar (eri) flaring cleanly above/behind the scarf wrap & framing the front throat line (B.Chest)."""
    if height == 'tall':
        if spiked:
            back_c = rounded(
                (906, 248, 1), (946, 198, 1), (994, 174, 1), (996, 214, 1),
                (976, 244, 1), (956, 264, 1), (912, 264, 1)
            )
            trim_spine = [(912, 246), (948, 203), (988, 181), (990, 214)]
            riv_pts = [(948, 224), (968, 206), (976, 226)]
        else:
            back_c = rounded(
                (906, 248, 1), (944, 196, 1), (986, 180, 1), (988, 222, 1),
                (964, 262, 1), (912, 264, 1)
            )
            trim_spine = [(912, 246), (946, 201), (980, 186), (982, 222)]
            riv_pts = [(946, 226), (966, 208), (970, 232)]
        front_c = rounded(
            (798, 224, 1), (824, 218, 1), (834, 246, 1), (818, 260, 1), (802, 254, 1)
        )
        fc = a.cel('HighCollarFront', 'Chest', order + 1, front_c, tone, sd=3.0, hd=1.8, rim=True, seam=1.6)
        fc.add(band([(801, 226), (822, 222), (830, 246)], 3.0), trim_tone, 2)
    elif height == 'mid':
        back_c = rounded(
            (906, 248, 1), (942, 206, 1), (978, 196, 1), (980, 232, 1),
            (958, 262, 1), (912, 264, 1)
        )
        trim_spine = [(912, 246), (944, 210), (973, 201), (974, 232)]
        riv_pts = [(946, 230), (964, 218)]
        front_c = rounded(
            (802, 230, 1), (824, 224, 1), (832, 248, 1), (816, 260, 1), (804, 254, 1)
        )
        fc = a.cel('HighCollarFront', 'Chest', order + 1, front_c, tone, sd=2.8, hd=1.6, rim=True, seam=1.5)
        fc.add(band([(805, 232), (822, 228), (829, 248)], 2.6), trim_tone, 2)
    else:
        back_c = rounded(
            (910, 250, 1), (940, 212, 1), (968, 208, 1), (970, 236, 1),
            (952, 262, 1), (916, 264, 1)
        )
        trim_spine = [(916, 248), (942, 216), (964, 213), (965, 236)]
        riv_pts = [(948, 230)]

    c = a.cel('HighCollar', 'Chest', order, back_c, tone, sd=4.2, hd=2.2, rim=True, seam=1.7)
    if lining_tone:
        c.add(band(trim_spine, 5.2), lining_tone, 1)
    c.add(band(trim_spine, 3.0), trim_tone, 2)
    if rivets:
        add_rivets(c, riv_pts, trim_tone, r=2.1)
    return c


def add_haori_lapels(a, coat_tone, lapel_tone, trim_tone, style='haori', rivets=False, order=56, side_shift=0.0):
    """Tailored open coat / haori / jacket front & layered folded lapels worn OVER the chest do (B.Chest, order 56..58).
    Tightly bounded between the gorget/collar line (y 254..260) and obi seam (y 366) so zero cloth bleeds past armor edges.
    """
    sx = side_shift
    if style == 'vest':
        side_p = rounded(
            (886 + sx, 256, 1), (942, 260, 1), (950, 312), (948, 366, 1),
            (876 + sx, 366, 1), (878 + sx, 312)
        )
        sp = a.cel('JacketSide', 'Chest', order, side_p, coat_tone, sd=4.8, hd=2.0, rim=False, seam=1.7)
        sp.add(band([(886 + sx, 260), (880 + sx, 312), (878 + sx, 364)], 4.2), lapel_tone, 1)
        sp.add(band([(883 + sx, 260), (877 + sx, 312), (875 + sx, 364)], 2.0), trim_tone, 2)
        front_e = rounded(
            (806, 262, 1), (822, 258, 1), (819, 310), (820, 362, 1), (805, 360, 1), (804, 308)
        )
        fe = a.cel('JacketFrontEdge', 'Chest', order + 1, front_e, coat_tone, sd=3.0, hd=1.4, rim=False, seam=1.6)
        fe.add(band([(819, 262), (817, 310), (818, 358)], 2.6), trim_tone, 2)
        return sp

    if style == 'tactical':
        side_p = rounded(
            (882 + sx, 256, 1), (944, 260, 1), (952, 310), (950, 366, 1),
            (874 + sx, 366, 1), (868 + sx, 326, 1), (876 + sx, 286)
        )
        sp = a.cel('JacketBody', 'Chest', order, side_p, coat_tone, sd=5.0, hd=2.2, rim=False, seam=1.7)
        lapel = rounded(
            (878 + sx, 256, 1), (898 + sx, 258, 1), (886 + sx, 310), (888 + sx, 366, 1),
            (868 + sx, 366, 1), (862 + sx, 320, 1), (870 + sx, 282)
        )
        lp = a.cel('JacketLapel', 'Chest', order + 1, lapel, lapel_tone, sd=3.6, hd=2.0, rim=True, seam=1.6)
        lp.add(band([(875 + sx, 258), (866 + sx, 318), (870 + sx, 364)], 3.0), trim_tone, 2)
        if rivets:
            add_rivets(lp, [(884 + sx, 276), (877 + sx, 316), (879 + sx, 350)], trim_tone, r=2.0)
        front_e = rounded(
            (804, 260, 1), (820, 258, 1), (817, 312), (822, 364, 1), (804, 360, 1), (802, 308)
        )
        fe = a.cel('JacketFrontLapel', 'Chest', order + 2, front_e, lapel_tone, sd=2.8, hd=1.4, rim=False, seam=1.5)
        fe.add(band([(818, 260), (815, 312), (820, 360)], 2.6), trim_tone, 2)
        return lp

    is_warlord = style == 'warlord'
    side_p = rounded(
        (882 + sx, 256, 1), (948, 260, 1), (956, 310), (954, 366, 1),
        (878 + sx, 366, 1), (872 + sx, 314)
    )
    sp = a.cel('HaoriBody', 'Chest', order, side_p, coat_tone, sd=5.4, hd=2.4, rim=False, seam=1.8)
    sp.add(band([(892 + sx * 0.5, 356), (924, 360), (950, 356)], 3.2), trim_tone, 2)
    if is_warlord:
        sp.add(band([(894 + sx * 0.5, 286), (926, 290), (950, 284)], 2.6), trim_tone, 2)
        sp.add(band([(892 + sx * 0.5, 322), (926, 326), (952, 320)], 2.6), trim_tone, 2)

    lapel = rounded(
        (876 + sx, 254, 1), (902 + sx, 258, 1), (896 + sx, 292, 1), (888 + sx, 328), (892 + sx, 366, 1),
        (868 + sx, 366, 1), (862 + sx, 324, 1), (868 + sx, 284)
    )
    lp = a.cel('HaoriLapelN', 'Chest', order + 1, lapel, lapel_tone, sd=3.8, hd=2.2, rim=True, seam=1.7)
    lp.add(band([(874 + sx, 256), (866 + sx, 286), (864 + sx, 324), (870 + sx, 364)], 3.4), trim_tone, 2)
    lp.add(band([(898 + sx, 260), (892 + sx, 292), (886 + sx, 328), (890 + sx, 364)], 2.4), trim_tone, 1)
    if rivets:
        add_rivets(lp, [(884 + sx, 274), (879 + sx, 308), (878 + sx, 344)], trim_tone, r=2.1)
    if is_warlord:
        lp.add(diamond(882 + sx, 288, 4.2, 6.5), trim_tone, 2)
        lp.add(diamond(878 + sx, 336, 4.0, 6.0), trim_tone, 2)

    front_l = rounded(
        (802, 258, 1), (820, 256, 1), (816, 304), (822, 364, 1), (802, 362, 1), (800, 308)
    )
    fl = a.cel('HaoriLapelF', 'Chest', order + 2, front_l, lapel_tone, sd=3.2, hd=1.8, rim=True, seam=1.6)
    fl.add(band([(817, 258), (814, 304), (819, 360)], 3.0), trim_tone, 2)
    return lp


def add_split_coat_tails(a, coat_tone, lining_tone, trim_tone, length='mid', notched=False,
                         feel='Haori', glow_tone=None, glow=1.0, rivets=False):
    """Dynamic multi-layered split coat / haori tails pinned strictly to the lower Obi / rear Kusazuri armor seams (B.Hips):
    - Back split tail at order 36 (over trimmed Coat_Side 33, behind LegN 52 & Coat 64)
    - Near outer split tail at order 66 (pinned to rear waist seam x 898..946, strictly UNDER rigid Kusazuri 68..71!)
    """
    if length == 'short':
        back_pts = rounded(
            (928, 399, 1), (960, 400, 1), (1004, 482), (1034, 578, 1),
            (1004, 594, 1), (984, 566, 1), (958, 588, 1), (944, 496), (924, 434, 1)
        )
        b_ch = sample([(944, 400), (972, 488), (1000, 576)], 4)
        near_pts = rounded(
            (898, 399, 1), (944, 400, 1), (968, 476), (990, 566, 1),
            (958, 584, 1), (936, 558, 1), (912, 574, 1), (904, 486), (896, 430, 1)
        )
        n_ch = sample([(922, 400), (940, 486), (958, 566)], 4)
        b_hem = [(962, 580), (984, 560), (1004, 586), (1028, 572)]
        n_hem = [(916, 566), (936, 552), (958, 576), (984, 560)]
        n_edge = curve(942, 404, 964, 480, 984, 558)
        riv_pts = [(926, 542), (958, 556)]
    elif length == 'mid':
        if notched:
            back_pts = rounded(
                (928, 399, 1), (962, 400, 1), (1016, 504), (1062, 646, 1),
                (1030, 672, 1), (1008, 636, 1), (980, 668, 1), (958, 634, 1),
                (948, 520), (924, 436, 1)
            )
            near_pts = rounded(
                (898, 399, 1), (944, 400, 1), (980, 500), (1014, 634, 1),
                (980, 660, 1), (958, 626, 1), (928, 654, 1), (910, 620, 1),
                (902, 508), (896, 432, 1)
            )
            b_hem = [(962, 630), (980, 658), (1008, 628), (1030, 662), (1056, 640)]
            n_hem = [(914, 616), (928, 646), (958, 618), (980, 650), (1008, 628)]
        else:
            back_pts = rounded(
                (928, 399, 1), (962, 400, 1), (1016, 506), (1060, 648, 1),
                (1018, 668, 1), (962, 656, 1), (948, 524), (924, 436, 1)
            )
            near_pts = rounded(
                (898, 399, 1), (944, 400, 1), (978, 502), (1010, 634, 1),
                (966, 656, 1), (912, 642, 1), (902, 508), (896, 432, 1)
            )
            b_hem = [(966, 648), (1016, 658), (1054, 642)]
            n_hem = [(916, 634), (964, 646), (1004, 628)]
        b_ch = sample([(945, 400), (980, 524), (1014, 648)], 5)
        n_ch = sample([(922, 400), (946, 520), (968, 636)], 5)
        n_edge = curve(940, 404, 972, 506, 1002, 624)
        riv_pts = [(928, 620), (962, 632), (992, 618)]
    else:
        back_pts = rounded(
            (928, 399, 1), (964, 400, 1), (1026, 520), (1084, 686, 1),
            (1050, 720, 1), (1024, 678, 1), (994, 718, 1), (966, 678, 1),
            (952, 540), (924, 438, 1)
        )
        near_pts = rounded(
            (898, 399, 1), (946, 400, 1), (988, 514), (1032, 672, 1),
            (996, 704, 1), (970, 664, 1), (938, 700, 1), (914, 660, 1),
            (904, 528), (896, 434, 1)
        )
        b_ch = sample([(946, 400), (988, 544), (1030, 692)], 5)
        n_ch = sample([(922, 400), (950, 538), (978, 678)], 5)
        b_hem = [(970, 672), (994, 708), (1024, 670), (1050, 710), (1076, 680)]
        n_hem = [(918, 654), (938, 690), (970, 656), (996, 694), (1024, 664)]
        n_edge = curve(942, 404, 982, 520, 1022, 660)
        riv_pts = [(932, 656), (968, 650), (1004, 662)]

    bt = a.cloth('HaoriTailBack', 'Hips', 36, back_pts, lining_tone, [b_ch], feel, sd=5.0, hd=2.0, rim=True, seam=1.8)
    bt.add(band(b_hem, 4.2), trim_tone, 2)

    nt = a.cloth('HaoriTailNear', 'Hips', 66, near_pts, coat_tone, [n_ch], feel, sd=5.4, hd=2.4, rim=True, seam=1.8)
    nt.add(band(n_edge, 3.6), trim_tone, 2)
    nt.add(band(n_hem, 4.4), trim_tone, 2)
    if lining_tone != coat_tone:
        nt.add(band([(x - 6.0, y - 8.0) for (x, y) in n_hem], 2.6), lining_tone, 1)
    if rivets:
        add_rivets(nt, riv_pts, trim_tone, r=2.2)
    if glow_tone and glow > 1.0:
        cx_g = sum(p[0] for p in n_hem) / len(n_hem)
        cy_g = sum(p[1] for p in n_hem) / len(n_hem) - 34.0
        nt.add(diamond(cx_g, cy_g, 6.5, 11.0), trim_tone, 2)
        nt.add(diamond(cx_g, cy_g, 3.6, 6.5), glow_tone, 2)

    return bt, nt


def add_arm_sleeve(a, sleeve_tone, trim_tone, elbow_fin=False):
    """Armored upper-arm sleeve & elbow cop (hijigane) on B.ArmNU (order 107) bridging sode to kote cleanly."""
    sleeve = rounded(
        (896, 336, 1), (952, 334, 1), (956, 386, 1), (928, 392), (900, 388, 1)
    )
    p = a.cel('ArmSleeveN', 'ArmNU', 107, sleeve, sleeve_tone, sd=4.0, hd=1.8, rim=False, seam=1.6)
    p.add(band([(902, 381), (928, 386), (953, 380)], 3.2), trim_tone, 2)
    p.add(band([(900, 362), (928, 365), (953, 360)], 2.2), trim_tone, 1)
    if elbow_fin:
        fin = rounded(
            (948, 348, 1), (972, 364, 1), (956, 386, 1), (944, 372, 1)
        )
        a.cel('ElbowFinN', 'ArmNU', 107, fin, sleeve_tone, sd=3.0, hd=1.8, rim=True, seam=1.6).add(
            band([(950, 352), (968, 365), (954, 382)], 2.6), trim_tone, 2
        )


def add_kote(a, tone, trim_tone, strap_tone, with_tekko=False, ornate=False, bladed=False, heavy=False, glow_tone=None, glow=1.0):
    """Armored forearm bracers (kote) on near & far arms, plus optional armored hand/knuckle guard (tekko)."""
    if bladed:
        bracer = rounded(
            (898, 382, 1), (924, 376), (948, 384, 1),
            (954, 412, 1), (942, 424, 1), (934, 450, 1), (916, 452), (897, 446, 1), (897, 414)
        )
    elif heavy:
        bracer = rounded(
            (896, 380, 1), (924, 374), (950, 384, 1),
            (948, 418), (938, 450, 1), (916, 453), (894, 447, 1), (894, 414)
        )
    else:
        bracer = rounded(
            (900, 384, 1), (924, 378), (945, 386, 1),
            (942, 416), (934, 448, 1), (916, 450), (898, 446, 1), (898, 414)
        )
    p = a.cel('KoteN', 'ArmNL', 110, bracer, tone, sd=4.2, hd=2.0, rim=True, seam=1.7)
    p.add(band([(900, 388), (923, 383), (944, 389)], 3.2), trim_tone, 2)
    p.add(band([(898, 441), (917, 444), (935, 442)], 3.4), trim_tone, 2)
    p.add(band([(899, 408), (921, 410), (941, 407)], 2.6), strap_tone, 1)
    p.add(band([(899, 426), (919, 428), (937, 425)], 2.6), strap_tone, 1)
    if ornate:
        p.add(band([(918, 388), (916, 440)], 3.2), trim_tone, 2)
        p.add(diamond(917, 414, 4.5, 7.0), trim_tone, 2)
    if glow_tone and glow > 1.0:
        a.flat('KoteNGlow', 'ArmNL', 111, band([(917, 394), (916, 434)], 2.2), glow_tone, which=2, glow=glow)

    far_b = rounded(
        (806, 386, 1), (826, 388), (841, 396, 1),
        (828, 426), (812, 456, 1), (796, 456), (784, 449, 1), (795, 418)
    )
    fp = a.cel('KoteF', 'ArmFL', 10, far_b, tone, sd=3.6, hd=1.8, rim=False, seam=1.5, sword_layer=True)
    fp.add(band([(805, 392), (824, 395), (838, 400)], 2.8), trim_tone, 1)
    fp.add(band([(788, 444), (802, 448), (812, 448)], 3.0), trim_tone, 1)

    if with_tekko:
        if heavy:
            tekko = rounded(
                (880, 446, 1), (908, 440), (933, 446, 1),
                (937, 468, 1), (924, 484, 1), (900, 486), (878, 476, 1), (876, 460)
            )
        else:
            tekko = rounded(
                (884, 448, 1), (908, 442), (929, 448, 1),
                (932, 466), (922, 480, 1), (900, 482), (880, 474, 1), (879, 460)
            )
        tk = a.cel('TekkoN', 'HandN', 113, tekko, tone, sd=3.4, hd=1.8, rim=True, seam=1.6)
        tk.add(band([(883, 472), (904, 476), (925, 473)], 3.2), trim_tone, 2)
        if ornate or heavy:
            for kx in (890, 904, 918):
                tk.add(disc(kx, 464, 2.8 if heavy else 2.2), trim_tone, 2)


def add_suneate(a, tone, trim_tone, strap_tone, ornate=False, glow_tone=None, glow=1.0):
    """Sculpted knee guards (tateage) and shin greaves (suneate) on near & far legs."""
    shin = rounded(
        (809, 658, 1), (834, 651), (863, 660, 1),
        (862, 696), (859, 756, 1), (836, 762), (813, 756, 1), (810, 696)
    )
    p = a.cel('SuneateN', 'LegNL', 59, shin, tone, sd=4.5, hd=2.2, rim=True, seam=1.7)
    p.add(band([(811, 665), (835, 659), (860, 667)], 3.2), trim_tone, 2)
    p.add(band([(812, 690), (836, 694), (860, 690)], 2.8), strap_tone, 1)
    p.add(band([(814, 742), (836, 745), (858, 742)], 2.8), strap_tone, 1)
    p.add(band([(830, 696), (829, 754)], 2.6), trim_tone, 2)
    if ornate:
        p.add(diamond(833, 676, 6.0, 7.5), trim_tone, 2)
    if glow_tone and glow > 1.0:
        a.flat('SuneateNGlow', 'LegNL', 59, diamond(833, 676, 3.2, 4.5), glow_tone, which=2, glow=glow)

    fshin = rounded(
        (930, 660, 1), (956, 654), (984, 662, 1),
        (983, 704), (980, 758, 1), (958, 763), (936, 758, 1), (933, 702)
    )
    fp = a.cel('SuneateF', 'LegFL', 26, fshin, tone, sd=4.0, hd=1.8, rim=False, seam=1.5)
    fp.add(band([(932, 666), (956, 661), (981, 668)], 2.8), trim_tone, 1)
    fp.add(band([(934, 692), (958, 695), (981, 692)], 2.5), strap_tone, 1)
    fp.add(band([(936, 742), (958, 745), (979, 742)], 2.5), strap_tone, 1)


def add_kusazuri(a, panels, depth, tone, trim_tone, lace_tone, rows=3, order=68, ornate=False,
                 pointed=False, staggered=False, glow_tone=None, glow=1.0):
    """Rigid cel-shaded samurai waist tassets (kusazuri) on B.Hips at order 68..71:
    strictly ABOVE all hip cloth (Coat 64, HaoriTailFront 65, HaoriTailNear 66, WaistRibbon 67)
    and strictly BELOW Belt/Obi (72..80), with a unique order per panel so panels never Z-fight.
    """
    x_start, x_end = 801.0, 896.0
    step = (x_end - x_start) / panels
    gap = 3.0 if panels >= 3 else 4.0
    for i in range(panels):
        x0 = x_start + i * step + (0.0 if i == 0 else gap * 0.5)
        x1 = x_start + (i + 1) * step - (0.0 if i == panels - 1 else gap * 0.5)
        flare = (i - (panels - 1) * 0.5) * 4.0
        y0 = 398.0
        extra = (12.0 if (staggered and i == panels - 1) else (4.0 if i == 0 else 0.0))
        y1 = y0 + depth + extra
        bx0 = x0 + flare - 2.0
        bx1 = x1 + flare + 2.0
        tip_drop = 12.0 if pointed else 4.0
        shape = rounded(
            (x0, y0, 1), ((x0 + x1) * 0.5, y0 - 1.0), (x1, y0, 1),
            (bx1, y1 - (12.0 if pointed else 7.0), 1),
            ((bx0 + bx1) * 0.5, y1 + tip_drop, 1),
            (bx0, y1 - (10.0 if pointed else 6.0), 1)
        )
        cx_top = (x0 + x1) * 0.5
        cx_bot = (bx0 + bx1) * 0.5
        chain = sample([(cx_top, y0), ((cx_top + cx_bot) * 0.5, (y0 + y1) * 0.5), (cx_bot, y1)], 4)
        panel_order = min(71, order + i)
        p = a.cloth(f'Kusazuri{i+1}', 'Hips', panel_order, shape, tone, [chain], 'Plates', sd=4.0, hd=2.0, rim=True, seam=1.7)
        for r in range(1, rows + 1):
            t = r / (rows + 0.65)
            lx0 = x0 + (bx0 - x0) * t + 2.0
            lx1 = x1 + (bx1 - x1) * t - 2.0
            ly = y0 + (y1 - y0) * t
            p.add(band([(lx0, ly), (lx1, ly)], 2.2), lace_tone, 1)
            if r < rows:
                p.add(band([(lx0, ly + 2.8), (lx1, ly + 2.8)], 1.5), 'ink', 0)
        p.add(band([(bx0 + 1.5, y1 - (11.0 if pointed else 7.0)), ((bx0 + bx1) * 0.5, y1 + tip_drop - 2.5), (bx1 - 1.5, y1 - (12.0 if pointed else 8.0))], 3.2), trim_tone, 2)
        if ornate:
            p.add(diamond((cx_top + cx_bot) * 0.5, (y0 + y1) * 0.52, 4.0, 5.5), trim_tone, 2)
        if glow_tone and glow > 1.0:
            p.add(diamond((cx_top + cx_bot) * 0.5, y1 - 14.0, 2.8, 4.0), glow_tone, 2)


def add_far_sode(a, tone, trim_tone):
    """Subtle armored shoulder cap on Kage's far arm (B.ArmFU, order 6, swordLayer=True)."""
    cap = rounded(
        (838, 256, 1), (866, 248), (890, 264, 1),
        (884, 302), (862, 312, 1), (836, 304, 1), (833, 278)
    )
    p = a.cel('SodeF', 'ArmFU', 6, cap, tone, sd=3.6, hd=1.8, rim=False, seam=1.5, sword_layer=True)
    p.add(band([(838, 298), (862, 306), (883, 296)], 2.8), trim_tone, 1)


def add_obi(a, sash_tone, trim_tone, buckle_tone, style='rect', glow_tone=None, glow=1.0):
    """Layered armored waist sash (obi) & sculpted buckle on B.Chest at order 78..81."""
    obi = rounded(
        (802, 364, 1), (878, 366), (956, 368, 1),
        (957, 400, 1), (878, 402), (802, 398, 1)
    )
    p = a.cel('Obi', 'Chest', 78, obi, sash_tone, sd=4.0, hd=1.8, rim=False, seam=1.7)
    p.add(band([(804, 370), (878, 372), (954, 374)], 2.8), trim_tone, 2)
    p.add(band([(804, 393), (878, 396), (954, 395)], 2.8), trim_tone, 2)

    if style == 'hex':
        bk = rounded((814, 360, 1), (842, 360, 1), (854, 381, 1), (842, 402, 1), (814, 402, 1), (802, 381, 1))
        b = a.cel('ObiBuckle', 'Chest', 80, bk, buckle_tone, sd=3.0, hd=2.0, rim=True, seam=1.6)
        b.add(rounded((818, 367, 1), (838, 367, 1), (846, 381, 1), (838, 395, 1), (818, 395, 1), (810, 381, 1)), sash_tone, 0)
        if glow_tone and glow > 1.0:
            a.flat('ObiGlow', 'Chest', 81, star(828, 381, 6.5, 2.8, n=4, rot=-45.0), glow_tone, which=2, glow=glow)
    elif style == 'ring':
        b = a.cel('ObiBuckle', 'Chest', 80, disc(828, 381, 20.0), buckle_tone, sd=3.2, hd=2.0, rim=True, seam=1.6)
        b.add(disc(828, 381, 12.0), sash_tone, 0)
        b.add(disc(828, 381, 5.5), buckle_tone, 2)
        if glow_tone and glow > 1.0:
            a.flat('ObiGlow', 'Chest', 81, disc(828, 381, 3.6), glow_tone, which=2, glow=glow)
    elif style == 'bar':
        bk = box(804, 362, 854, 400, 3.0)
        b = a.cel('ObiBuckle', 'Chest', 80, bk, buckle_tone, sd=3.0, hd=1.8, rim=True, seam=1.6)
        b.add(box(810, 368, 848, 394), sash_tone, 0)
        b.add(band([(812, 376), (846, 376)], 3.0), trim_tone, 2)
        b.add(band([(812, 386), (846, 386)], 3.0), trim_tone, 2)
        if glow_tone and glow > 1.0:
            a.flat('ObiGlow', 'Chest', 81, diamond(829, 381, 5.0, 5.0), glow_tone, which=2, glow=glow)
    elif style in ('crest', 'royal'):
        bk = rounded(
            (805, 360, 1), (828, 352, 1), (851, 360, 1),
            (855, 381), (849, 402, 1), (828, 410, 1), (806, 402, 1), (802, 381)
        )
        b = a.cel('ObiBuckle', 'Chest', 80, bk, buckle_tone, sd=3.4, hd=2.0, rim=True, seam=1.6)
        b.add(diamond(828, 381, 13.0, 16.0), sash_tone, 0)
        b.add(diamond(828, 381, 7.5, 9.5), buckle_tone, 2)
        if glow_tone and glow > 1.0:
            a.flat('ObiGlow', 'Chest', 81, star(828, 381, 6.2, 2.6, n=4, rot=-90.0), glow_tone, which=2, glow=glow)
    else:
        bk = box(808, 363, 840, 399, 3.0)
        b = a.cel('ObiBuckle', 'Chest', 80, bk, buckle_tone, sd=2.8, hd=1.6, rim=False, seam=1.6)
        b.add(box(814, 370, 834, 392), sash_tone, 0)
        b.add(band([(812, 381), (836, 381)], 2.8), buckle_tone, 2)
