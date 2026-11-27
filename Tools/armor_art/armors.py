"""The 5-tier Act I armor designs (Common, Rare, Epic, Trophy, Legendary) with tailored coats/haori,
high collars, layered lapels, riveted shoulder pauldrons, belted sashes, and dynamic split coat tails.

Reference-pixel frame (KageModel.cs): x right = back, x left = front, y down.
  Head:       x 799..899, y 116..225 (pivot 852, 205)
  Scarf_Wrap: B.Chest, order 100..103, x 818..968, y 203..254
  Torso:      B.Chest, order 44, x 807..953, y 226..425
  Belt:       B.Chest, order 72..77, x 805..954, y 366..397
  Coat:       B.Hips,  order 64 (front flap y 398..740), Coat_Side order 33, Coat_Back order 19
  ArmNU:      order 104..106, pivot (905, 268), x 884..950, y 248..380
  ArmNL:      order 108..109, pivot (926, 375), x 900..942, y 385..446
  HandN:      order 112,      pivot (914, 442), x 877..932, y 440..490
  ArmFU:      order 4..5,     pivot (862, 272), swordLayer=True
  ArmFL:      order 8..9,     pivot (822, 385), swordLayer=True
  LegNL:      order 56..58,   pivot (835, 680), x 811..863, y 662..766
  LegFL:      order 24..25,   pivot (955, 680), x 931..984, y 662..766

Strict non-overlapping layering & rigid-over-cloth hierarchy:
  B.Chest (45..98, > Torso 44, < Scarf_Wrap 100):
    45 CapeLining, 46 CapeOuter / HalfMantle, 47 AgemakiTassel, 48 CollarWing,
    50 HighCollar, 51 HighCollarFront, 52 Gorget, 53 Do, 54 ChestRope, 55 BreastPlate,
    56 HaoriBody / JacketSide, 57 HaoriLapelN / JacketLapel, 58 HaoriLapelF / JacketFront,
    60 ChestCrest / Seal / CoreRing, 61 Primary Glow, 62 Core Glow,
    78 Obi, 80 ObiBuckle / RopeKnot, 81 ObiGlow.
  B.Hips (under-cloth -> outer flowing tails -> rigid Kusazuri -> Belt/Obi):
    19 Coat_Back (trimmed under-cloth), 33 Coat_Side (trimmed under-cloth),
    36 HaoriTailBack (rear split tail, behind LegN 52 & Coat 64),
    64 Coat (trimmed front under-tunic),
    65 HaoriTailFront (Legendary front split coat panel, cleanly beneath Kusazuri),
    66 HaoriTailNear (near split coat tail, pinned to rear waist seam x 898..946, beneath Kusazuri),
    67 WaistRibbonA / SashTail (pinned to rear hip seam, beneath Kusazuri),
    68..71 Kusazuri1..4 (rigid waist tassets, unique order per panel, ALWAYS over hip cloth;
           Ronin RopeTail at 71 hangs cleanly from RopeKnot bottom seam y 400).
  B.ArmNU: 107 (rigid ArmSleeveN + ElbowFinN + SodeTier4..1 + Crest merged in strict painter order by ArmorVisuals;
           pauldron glows strictly above via underSlot -1 / 0).
  B.ArmNL: 110 KoteN, 111 KoteNGlow (< HandN 112).
  B.HandN: 113 TekkoN.
  B.ArmFU: 6 Far pauldron (swordLayer=True).
  B.ArmFL: 10 Far kote (swordLayer=True).
  B.LegNL: 59 SuneateN (underSlot -1) & SuneateNGlow (underSlot 0), strictly < FootN 60.
  B.LegFL: 26 SuneateF.
  B.Head:  79 HaloRay (not 81: the chest's ObiGlow holds that slot), 82 HaloFrame, 83 HaloGlow/HaloCore (< Head_Hair 84, < Eye 96).
"""
import math
from kit import *

ALL = []


def armor(aid, look, tier, summary, palette, retones_coat=True, aura='none'):
    a = Armor(aid, look, tier, summary, palette, retones_coat=retones_coat, aura=aura)
    ALL.append(a)
    return a


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
    - Legendary front split panel at order 65 (pinned to front waist seam x 796..824, strictly UNDER rigid Kusazuri 68..71!)
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

    # Ornate Legendary long coats also add a flared front-edge warlord coat panel strictly under Kusazuri (order 65 < 68..71)
    if length == 'long':
        front_tail = rounded(
            (798, 400, 1), (824, 400, 1), (820, 520), (808, 658, 1),
            (784, 682, 1), (768, 650, 1), (782, 524), (794, 434, 1)
        )
        f_ch = sample([(810, 400), (798, 532), (788, 662)], 5)
        ft = a.cloth('HaoriTailFront', 'Hips', 65, front_tail, coat_tone, [f_ch], feel, sd=4.6, hd=2.2, rim=True, seam=1.8)
        ft.add(band(curve(798, 404, 784, 524, 772, 648), 3.6), trim_tone, 2)
        ft.add(band([(772, 646), (784, 672), (806, 652)], 4.0), trim_tone, 2)
        if glow_tone and glow > 1.0:
            ft.add(diamond(792, 620, 4.8, 8.5), trim_tone, 2)
            ft.add(diamond(792, 620, 2.6, 4.8), glow_tone, 2)
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


# ================================================================================================
# TIER 1: COMMON (#9CA3AF) — Ronin Armor
# ================================================================================================

a = armor(
    'ronin_yoroi', 'ronin', 'common',
    'Weathered vagabond ronin attire: fitted boiled-leather hara-ate under an open tattered travel vest with standing cloth collar, diagonal hemp rope, riveted shoulder guard, split fluttering waist sashes and leather bracers.',
    {
        'rim': '#D2683F',
        'coat': ('#110B0A', '#1C1311', '#31231F'),
        'cloth': ('#4E2117', '#7E3624', '#C45E38'),
        'leather': ('#261914', '#462F24', '#6E4C3B'),
        'rope': ('#544029', '#9A7852', '#CFAF82'),
        'iron': ('#1F2229', '#3B404C', '#676F80'),
        'ink': ('#0B0808', '#171212', '#261E1E'),
    },
    retones_coat=True,
)
add_high_collar(a, tone='cloth', trim_tone='rope', height='short', rivets=False, order=50)
do = a.cel('Do', 'Chest', 53, rounded(
    (820, 256, 1), (866, 248), (936, 260, 1),
    (948, 306), (948, 360, 1), (882, 366), (810, 360, 1), (808, 308)
), 'leather', sd=6.5, hd=2.6, seam=2.0)
do.add(band([(814, 294), (878, 298), (944, 294)], 2.8), 'ink', 1)
do.add(band([(812, 330), (878, 334), (944, 330)], 2.8), 'ink', 1)
do.add(band([(848, 254), (850, 362)], 3.0), 'ink', 0)
a.cel('ChestRope', 'Chest', 54, rounded(
    (878, 252, 1), (898, 258, 1), (864, 314), (834, 366, 1), (812, 360, 1), (842, 306)
), 'cloth', sd=4.0, hd=1.8, rim=True, seam=1.7).add(
    band([(886, 258), (854, 310), (824, 360)], 4.8), 'rope', 1
).add(
    band([(889, 261), (857, 313), (827, 361)], 2.0), 'rope', 2
)
add_haori_lapels(a, coat_tone='coat', lapel_tone='cloth', trim_tone='rope', style='vest', order=56)
a.cel('ChestClasp', 'Chest', 60, disc(852, 310, 8.5), 'iron', sd=2.4, hd=1.4, rim=False, seam=1.5).add(
    disc(852, 310, 4.0), 'rope', 2
)
obi = a.cel('Obi', 'Chest', 78, rounded(
    (803, 366, 1), (878, 368), (955, 370, 1),
    (955, 397, 1), (878, 399), (803, 396, 1)
), 'rope', sd=4.0, hd=2.0, rim=False, seam=1.7)
for rx in range(814, 948, 16):
    obi.add(band([(rx, 368), (rx + 9, 396)], 2.6), 'ink', 1)
a.cel('RopeKnot', 'Chest', 80, rounded(
    (812, 365), (836, 363), (842, 382), (834, 401), (812, 399), (806, 382)
), 'rope', sd=3.2, hd=1.6, rim=True, seam=1.6)
# Rear cloth sash pinned cleanly behind Kusazuri (order 67 < Kusazuri 68..69); rope tail hangs directly from RopeKnot bottom seam (y 400)
sash_pts = tine(curve(906, 400, 938, 462, 964, 536), 17.0, 5.0, n=12, w_mid=20.0)
a.cloth('SashTail', 'Hips', 67, sash_pts, 'cloth', [sample(curve(906, 400, 938, 462, 964, 536), 4)], 'Strip', sd=3.2, hd=1.6, rim=True).add(
    band(curve(906, 404, 936, 464, 960, 528), 2.8), 'rope', 2
)
add_kusazuri(a, panels=2, depth=64.0, tone='leather', trim_tone='rope', lace_tone='cloth', rows=2, order=68)
tail_pts = tine(curve(824, 400, 816, 448, 804, 496), 11.5, 4.5, n=12)
a.cloth('RopeTail', 'Hips', 71, tail_pts, 'rope', [sample(curve(824, 400, 816, 448, 804, 496), 4)], 'Tassel', sd=2.8, hd=1.4, rim=True)
add_arm_sleeve(a, 'leather', 'rope')
sode = a.cel('Sode', 'ArmNU', 107, rounded(
    (874, 256, 1), (924, 238), (960, 260, 1), (960, 306),
    (954, 342, 1), (938, 322, 1), (922, 354, 1), (908, 326, 1), (892, 344, 1), (882, 298)
), 'cloth', sd=5.5, hd=2.4, rim=True, seam=1.8)
sode.add(band([(882, 268), (922, 256), (954, 272)], 5.0), 'leather', 1)
sode.add(band([(886, 298), (920, 310), (952, 298)], 3.8), 'rope', 1)
add_rivets(sode, [(894, 265), (922, 258), (946, 268)], 'iron', r=2.4)
add_kote(a, tone='leather', trim_tone='iron', strap_tone='rope', with_tekko=False, ornate=False)
add_suneate(a, tone='leather', trim_tone='rope', strap_tone='cloth', ornate=False)


# ================================================================================================
# TIER 2: RARE (#38BDF8) — Yami Armor & Shinsen Armor
# ================================================================================================

# ---- Yami Armor (Rare, 750c) ----
a = armor(
    'yami_yoroi', 'yami', 'rare',
    'Shadow-runner tactical shinobi gear: short matte-indigo tactical jacket with angled standing collar and crisp lapels over a beveled gunmetal breastplate, riveted twin-lame sode, split agile coat tails and iron kote.',
    {
        'rim': '#A86BFF',
        'coat': ('#09070F', '#130D1E', '#241B38'),
        'cloth': ('#371563', '#5E24A0', '#B67CFF'),
        'steel': ('#1F2433', '#3E475F', '#7484A6'),
        'plate': ('#131020', '#251E3B', '#483B6E'),
        'accent': ('#4E228F', '#9D5BFF', '#D9B8FF'),
        'ink': ('#07060C', '#120E1D', '#221B33'),
    },
    retones_coat=True,
)
add_high_collar(a, tone='plate', trim_tone='accent', lining_tone='cloth', height='short', rivets=True, order=50)
do = a.cel('Do', 'Chest', 53, rounded(
    (818, 256, 1), (868, 246), (938, 258, 1),
    (950, 304), (948, 360, 1), (880, 366), (808, 358, 1), (808, 304)
), 'plate', sd=6.0, hd=2.6, seam=1.9)
breast = a.cel('BreastPlate', 'Chest', 55, rounded(
    (814, 260, 1), (850, 256), (878, 266, 1),
    (880, 306), (864, 336, 1), (836, 340), (812, 326, 1), (810, 294)
), 'steel', sd=4.8, hd=2.6, rim=True, seam=1.8)
breast.add(band([(816, 322), (838, 334), (862, 330), (876, 304)], 3.2), 'accent', 2)
breast.add(band([(842, 260), (844, 334)], 2.4), 'ink', 0)
add_haori_lapels(a, coat_tone='coat', lapel_tone='plate', trim_tone='accent', style='tactical', rivets=True, order=56)
a.cel('HexBuckle', 'Chest', 60, rounded(
    (836, 290, 1), (848, 285, 1), (858, 295, 1), (854, 308, 1), (842, 313, 1), (832, 303, 1)
), 'steel', sd=2.4, hd=1.6, rim=False, seam=1.5).add(disc(845, 299, 4.0), 'accent', 2)
add_far_sode(a, 'steel', 'accent')
add_arm_sleeve(a, 'plate', 'accent')
s2 = a.cel('SodeLower', 'ArmNU', 107, rounded(
    (894, 296, 1), (954, 292, 1), (960, 338, 1), (928, 348), (896, 340, 1)
), 'plate', sd=4.5, hd=2.0, rim=True, seam=1.8)
s2.add(band([(898, 336), (928, 343), (957, 334)], 3.0), 'accent', 2)
add_rivets(s2, [(908, 320), (928, 324), (948, 318)], 'steel', r=2.0)
s1 = a.cel('SodeUpper', 'ArmNU', 107, rounded(
    (872, 258, 1), (924, 236), (960, 258, 1), (960, 304, 1), (924, 316), (882, 304, 1)
), 'steel', sd=5.2, hd=2.6, rim=True, seam=1.8)
s1.add(band([(884, 298), (924, 310), (957, 299)], 3.2), 'accent', 2)
s1.add(band([(880, 260), (924, 243), (954, 260)], 2.4), 'steel', 2)
add_rivets(s1, [(892, 276), (924, 268), (950, 276)], 'accent', r=2.2)
add_obi(a, sash_tone='plate', trim_tone='accent', buckle_tone='steel', style='rect')
add_split_coat_tails(a, coat_tone='plate', lining_tone='cloth', trim_tone='accent', length='short', feel='Strip', rivets=True)
add_kusazuri(a, panels=2, depth=76.0, tone='plate', trim_tone='accent', lace_tone='steel', rows=3, order=68)
add_kote(a, tone='steel', trim_tone='accent', strap_tone='ink', with_tekko=True, ornate=False)
add_suneate(a, tone='steel', trim_tone='accent', strap_tone='plate', ornate=False)


# ---- Shinsen Armor (Rare, 1200c) ----
a = armor(
    'shinsen_yoroi', 'shinsen', 'rare',
    'Mountain shrine battle-haori: crisp white-and-crimson tailored haori jacket with stiff collar and layered lapels over a laced maru-do, riveted 3-tier silver sode, split ceremonial haori tails and steel kote.',
    {
        'rim': '#FF5A62',
        'coat': ('#B8B0C2', '#EBE6EE', '#FFFFFF'),
        'cloth': ('#7A1218', '#C8202C', '#FF6464'),
        'silver': ('#6E7282', '#C8C5D4', '#FAF9FF'),
        'lacquer': ('#14121A', '#262230', '#453F54'),
        'enamel': ('#ABA4B6', '#EAE5EF', '#FFFFFF'),
        'ink': ('#0A080C', '#1A1620', '#2E2838'),
    },
    retones_coat=True,
)
add_high_collar(a, tone='enamel', trim_tone='cloth', lining_tone='silver', height='mid', rivets=True, order=50)
do = a.cel('Do', 'Chest', 53, rounded(
    (818, 256, 1), (868, 246), (940, 258, 1),
    (950, 306), (948, 360, 1), (880, 366), (808, 358, 1), (808, 306)
), 'lacquer', sd=6.0, hd=2.6, seam=1.9)
do.add(rounded((822, 256, 1), (836, 254, 1), (842, 356, 1), (828, 356, 1)), 'enamel', 2)
for k in range(4):
    yy = 268.0 + k * 21.0
    do.add(band([(842, yy), (874, yy + 14)], 2.8), 'cloth', 1)
    do.add(band([(874, yy), (842, yy + 14)], 2.8), 'cloth', 1)
add_haori_lapels(a, coat_tone='enamel', lapel_tone='cloth', trim_tone='silver', style='tactical', rivets=True, order=56)
a.cel('ChestCrest', 'Chest', 60, disc(836, 304, 11.5), 'silver', sd=2.8, hd=1.8, rim=False, seam=1.6).add(
    disc(836, 304, 6.2), 'cloth', 1
).add(disc(836, 304, 2.8), 'silver', 2)
add_far_sode(a, 'silver', 'cloth')
add_arm_sleeve(a, 'lacquer', 'silver')
s3 = a.cel('SodeTier3', 'ArmNU', 107, rounded(
    (898, 312, 1), (962, 308, 1), (966, 344, 1), (930, 350), (902, 344, 1)
), 'silver', sd=4.2, hd=2.0, rim=True, seam=1.7)
s3.add(band([(904, 338), (932, 343), (962, 338)], 2.8), 'cloth', 1)
add_rivets(s3, [(914, 326), (934, 329), (954, 325)], 'silver', r=1.9)
s2 = a.cel('SodeTier2', 'ArmNU', 107, rounded(
    (888, 282, 1), (960, 278, 1), (964, 316, 1), (928, 322), (892, 316, 1)
), 'silver', sd=4.5, hd=2.2, rim=True, seam=1.7)
s2.add(band([(894, 310), (928, 315), (960, 310)], 2.8), 'cloth', 1)
add_rivets(s2, [(904, 296), (928, 299), (952, 295)], 'silver', r=2.0)
s1 = a.cel('SodeTier1', 'ArmNU', 107, rounded(
    (872, 256, 1), (924, 232), (960, 254, 1), (962, 290, 1), (924, 296), (880, 290, 1)
), 'silver', sd=5.2, hd=2.8, rim=True, seam=1.8)
s1.add(band([(880, 256), (924, 238), (956, 256)], 3.2), 'silver', 2)
s1.add(band([(884, 283), (924, 289), (958, 283)], 2.8), 'cloth', 1)
add_rivets(s1, [(892, 266), (924, 256), (950, 266)], 'cloth', r=2.2)
add_obi(a, sash_tone='cloth', trim_tone='silver', buckle_tone='silver', style='crest')
add_split_coat_tails(a, coat_tone='enamel', lining_tone='cloth', trim_tone='silver', length='short', feel='Haori', rivets=False)
add_kusazuri(a, panels=3, depth=82.0, tone='enamel', trim_tone='cloth', lace_tone='cloth', rows=3, order=68)
add_kote(a, tone='silver', trim_tone='cloth', strap_tone='lacquer', with_tekko=True, ornate=True)
add_suneate(a, tone='silver', trim_tone='cloth', strap_tone='lacquer', ornate=True)


# ================================================================================================
# TIER 3: EPIC (#A855F7) — Raikō Armor & Kagehira Armor
# ================================================================================================

# ---- Raikō Armor (Epic, 1700c) ----
a = armor(
    'raiko_yoroi', 'raiko', 'epic',
    'Thunder vanguard battle-haori & o-yoroi: structured obsidian-gold haori with standing collar, layered lapels and gilded split coat tails worn over a segmented cuirass with glowing storm-wheel crest and heavily riveted storm-wing pauldrons.',
    {
        'rim': '#FFC43A',
        'coat': ('#0B0805', '#15100A', '#292014'),
        'cloth': ('#5E3E0A', '#B88018', '#FFE08A'),
        'lacquer': ('#16120B', '#2B2315', '#4E4026'),
        'gold': ('#7A5412', '#DDA228', '#FFF0A0'),
        'glow': ('#D97706', '#FFC83D', '#FFFBE0'),
        'ink': ('#080604', '#14100B', '#261F15'),
    },
    retones_coat=True,
)
add_high_collar(a, tone='lacquer', trim_tone='gold', lining_tone='cloth', height='mid', rivets=True, order=50)
a.cel('Gorget', 'Chest', 52, rounded(
    (814, 248, 1), (866, 242), (920, 248, 1), (914, 274, 1), (864, 280, 1), (812, 270, 1)
), 'lacquer', sd=3.5, hd=1.8, rim=False, seam=1.6).add(
    band([(814, 267), (864, 276), (912, 269)], 3.2), 'gold', 2
)
do = a.cel('Do', 'Chest', 53, rounded(
    (816, 258, 1), (868, 248), (944, 260, 1),
    (954, 308), (952, 362, 1), (880, 368), (806, 358, 1), (806, 306)
), 'lacquer', sd=6.5, hd=2.8, seam=2.0)
for y_lame in (284, 308, 332, 352):
    do.add(band([(810, y_lame), (880, y_lame + 4), (948, y_lame - 2)], 2.8), 'ink', 0)
    do.add(band([(812, y_lame - 3), (880, y_lame + 1), (946, y_lame - 5)], 2.0), 'gold', 1)
front_p = a.cel('BreastPlate', 'Chest', 55, rounded(
    (814, 262, 1), (854, 258), (882, 268, 1),
    (884, 314), (870, 348, 1), (838, 352), (810, 338, 1), (808, 300)
), 'lacquer', sd=4.8, hd=2.4, rim=True, seam=1.8)
front_p.add(band([(812, 336), (838, 348), (868, 344), (880, 314)], 3.4), 'gold', 2)
front_p.add(band([(852, 262), (854, 344)], 2.4), 'ink', 0)
add_haori_lapels(a, coat_tone='coat', lapel_tone='cloth', trim_tone='gold', style='haori', rivets=True, order=56)
a.cel('StormMon', 'Chest', 60, disc(838, 302, 14.0), 'gold', sd=2.8, hd=1.8, rim=False, seam=1.6).add(
    disc(838, 302, 9.5), 'lacquer', 0
)
a.flat('StormMonGlow', 'Chest', 61, star(838, 302, 8.5, 3.6, n=4, rot=-45.0), 'glow', which=2, glow=2.0)
add_far_sode(a, 'lacquer', 'gold')
add_arm_sleeve(a, 'lacquer', 'gold', elbow_fin=True)
s3 = a.cel('SodeTier3', 'ArmNU', 107, rounded(
    (892, 314, 1), (968, 304, 1), (978, 346, 1), (930, 356), (896, 346, 1)
), 'lacquer', sd=4.6, hd=2.2, rim=True, seam=1.8)
s3.add(band([(898, 342), (932, 350), (974, 340)], 3.5), 'gold', 2)
add_rivets(s3, [(910, 330), (934, 334), (960, 326)], 'gold', r=2.2)
s2 = a.cel('SodeTier2', 'ArmNU', 107, rounded(
    (882, 282, 1), (966, 272, 1), (976, 316, 1), (928, 326), (886, 316, 1)
), 'lacquer', sd=4.8, hd=2.4, rim=True, seam=1.8)
s2.add(band([(888, 312), (928, 320), (972, 310)], 3.2), 'gold', 2)
add_rivets(s2, [(902, 298), (928, 302), (958, 294)], 'gold', r=2.3)
s1 = a.cel('SodeTier1', 'ArmNU', 107, rounded(
    (866, 254, 1), (924, 224, 1), (982, 232, 1), (970, 288, 1), (924, 298), (874, 288, 1)
), 'lacquer', sd=5.4, hd=2.8, rim=True, seam=1.9)
s1.add(band([(872, 256), (924, 230), (976, 236)], 3.8), 'gold', 2)
s1.add(diamond(924, 264, 9.5, 6.2), 'gold', 2)
add_rivets(s1, [(888, 270), (960, 256)], 'gold', r=2.4)
a.flat('SodeGlow', 'ArmNU', 107, diamond(924, 264, 4.8, 3.4), 'glow', which=2, glow=1.9)
add_obi(a, sash_tone='cloth', trim_tone='gold', buckle_tone='gold', style='hex', glow_tone='glow', glow=1.8)
add_split_coat_tails(a, coat_tone='lacquer', lining_tone='cloth', trim_tone='gold', length='mid', notched=False, feel='Haori', rivets=True)
add_kusazuri(a, panels=3, depth=90.0, tone='lacquer', trim_tone='gold', lace_tone='cloth', rows=3, order=68, ornate=True, staggered=True, glow_tone='glow', glow=1.7)
add_kote(a, tone='lacquer', trim_tone='gold', strap_tone='cloth', with_tekko=True, ornate=True, glow_tone='glow', glow=1.7)
add_suneate(a, tone='lacquer', trim_tone='gold', strap_tone='cloth', ornate=True, glow_tone='glow', glow=1.7)


# ---- Kagehira Armor (Epic, 2400c) ----
a = armor(
    'kagehira_yoroi', 'kagehira', 'epic',
    'Crimson blademaster battle-haori & lamellar: structured blood-crimson haori with tall gold-trimmed collar, sharp folded lapels and notched split tails over laced kozane-do, riveted winged o-sode and flowing agemaki cord.',
    {
        'rim': '#FF3E44',
        'coat': ('#0C0506', '#18080A', '#2F1115'),
        'cloth': ('#7E101A', '#D01C26', '#FF6A5A'),
        'lacquer': ('#1A0709', '#381014', '#661E24'),
        'iron': ('#18141A', '#302935', '#584E63'),
        'gold': ('#7E5218', '#D8962C', '#FDE078'),
        'glow': ('#DC2626', '#FF4D4D', '#FFD4B0'),
        'ink': ('#070304', '#14080A', '#261013'),
    },
    retones_coat=True,
)
# Flowing crimson agemaki silk tassel pinned cleanly to rear shoulder armor seam (x 956, y 266)
tassel_sp = curve(956, 266, 996, 334, 1022, 418)
a.cloth('AgemakiTassel', 'Chest', 47, tine(tassel_sp, 14.0, 4.0, n=12, w_mid=16.0), 'cloth', [sample(tassel_sp, 4)], 'Tassel', sd=3.2, hd=1.6, rim=True).add(
    band(curve(956, 272, 994, 338, 1018, 410), 2.8), 'gold', 2
)
add_high_collar(a, tone='cloth', trim_tone='gold', lining_tone='lacquer', height='mid', rivets=True, order=50)
a.cel('Gorget', 'Chest', 52, rounded(
    (812, 246, 1), (866, 240), (924, 246, 1), (920, 274, 1), (864, 282, 1), (812, 270, 1)
), 'lacquer', sd=4.0, hd=2.0, rim=True, seam=1.7).add(
    band([(814, 266), (864, 277), (918, 269)], 3.4), 'gold', 2
)
do = a.cel('Do', 'Chest', 53, rounded(
    (814, 258, 1), (868, 248), (946, 260, 1),
    (956, 308), (952, 364, 1), (880, 370), (804, 360, 1), (804, 306)
), 'lacquer', sd=6.5, hd=2.8, seam=2.0)
for y_l in (276, 296, 316, 336, 354):
    do.add(band([(808, y_l), (878, y_l + 4), (948, y_l - 2)], 2.4), 'ink', 0)
    for lx in range(818, 942, 18):
        do.add(band([(lx, y_l - 13), (lx + 1, y_l - 2)], 2.8), 'cloth', 2)
add_haori_lapels(a, coat_tone='coat', lapel_tone='cloth', trim_tone='gold', style='haori', rivets=True, order=56)
crest = a.cel('ChestCrest', 'Chest', 60, rounded(
    (820, 274, 1), (838, 264, 1), (858, 276, 1), (852, 326, 1), (836, 344, 1), (818, 324, 1)
), 'iron', sd=3.6, hd=2.0, rim=True, seam=1.7)
crest.add(band([(821, 322), (836, 340), (850, 324)], 3.2), 'gold', 2)
a.flat('ChestGlow', 'Chest', 61, diamond(837, 302, 7.2, 12.5), 'glow', which=2, glow=2.1)
add_far_sode(a, 'lacquer', 'gold')
add_arm_sleeve(a, 'lacquer', 'gold', elbow_fin=True)
s3 = a.cel('SodeTier3', 'ArmNU', 107, rounded(
    (890, 316, 1), (968, 306, 1), (982, 348, 1), (932, 358), (894, 348, 1)
), 'lacquer', sd=4.8, hd=2.2, rim=True, seam=1.8)
s3.add(band([(896, 344), (934, 352), (978, 342)], 3.4), 'gold', 2)
add_rivets(s3, [(910, 330), (936, 334), (964, 326)], 'gold', r=2.2)
s2 = a.cel('SodeTier2', 'ArmNU', 107, rounded(
    (880, 282, 1), (966, 272, 1), (980, 318, 1), (930, 328), (884, 318, 1)
), 'lacquer', sd=5.0, hd=2.4, rim=True, seam=1.8)
s2.add(band([(886, 314), (930, 322), (976, 312)], 3.2), 'gold', 2)
add_rivets(s2, [(900, 298), (930, 302), (960, 294)], 'gold', r=2.3)
s1 = a.cel('SodeTier1', 'ArmNU', 107, rounded(
    (862, 256, 1), (922, 220, 1), (980, 236, 1), (972, 288, 1), (924, 298), (872, 288, 1)
), 'lacquer', sd=5.6, hd=2.8, rim=True, seam=1.9)
s1.add(band([(868, 258), (922, 226), (976, 240)], 3.8), 'gold', 2)
s1.add(band([(876, 284), (926, 292), (968, 282)], 3.0), 'cloth', 2)
add_rivets(s1, [(888, 266), (924, 244), (958, 266)], 'gold', r=2.4)
a.flat('SodeGlow', 'ArmNU', 107, band([(906, 258), (944, 252), (966, 260)], 3.0), 'glow', which=2, glow=2.0)
add_obi(a, sash_tone='cloth', trim_tone='gold', buckle_tone='gold', style='crest', glow_tone='glow', glow=1.9)
add_split_coat_tails(a, coat_tone='cloth', lining_tone='lacquer', trim_tone='gold', length='mid', notched=True, feel='Haori', rivets=True)
add_kusazuri(a, panels=4, depth=94.0, tone='lacquer', trim_tone='gold', lace_tone='cloth', rows=4, order=68, ornate=True, pointed=True, glow_tone='glow', glow=1.9)
add_kote(a, tone='lacquer', trim_tone='gold', strap_tone='cloth', with_tekko=True, ornate=True, bladed=True, glow_tone='glow', glow=1.9)
add_suneate(a, tone='lacquer', trim_tone='gold', strap_tone='cloth', ornate=True, glow_tone='glow', glow=1.8)


# ================================================================================================
# TIER 4: TROPHY (#EAB308) — Ikazuchi-dō (Garan) & Shiden Yoroi (Shiden)
# ================================================================================================

# ---- Ikazuchi-dō (Trophy, Garan) ----
a = armor(
    'ikazuchi_do', 'half_do', 'trophy',
    'Garan trophy war-surcoat & iron cuirass: jagged high riveted collar and battle-scarred iron surcoat lapels with heavy split tails over a seared half-do, massive riveted spiked o-sode and brawler gauntlets.',
    {
        'rim': '#EAB308',
        'iron': ('#1A1E25', '#343B47', '#677285'),
        'iron_dark': ('#101217', '#20242D', '#3E4554'),
        'gold': ('#7C5414', '#D49A2A', '#FCE07A'),
        'lace': ('#5A1C16', '#A8382B', '#E0604A'),
        'glow': ('#DC2626', '#FF6A3D', '#FFF0B0'),
        'ink': ('#07080A', '#12151B', '#222732'),
    },
    retones_coat=False,
)
add_high_collar(a, tone='iron_dark', trim_tone='gold', lining_tone='lace', height='mid', rivets=True, order=50)
# Place the dark war-surcoat behind the seared iron half-do so the signature jagged half-do border reads crisply on top!
add_haori_lapels(a, coat_tone='iron_dark', lapel_tone='iron_dark', trim_tone='gold', style='haori', rivets=True, order=53, side_shift=14.0)
do = a.cel('Do', 'Chest', 56, rounded(
    (814, 256, 1), (866, 248), (904, 258, 1),
    (888, 276, 1), (906, 294, 1), (886, 314, 1), (904, 336, 1), (888, 356, 1),
    (844, 362), (808, 352, 1), (806, 304)
), 'iron', sd=6.5, hd=3.0, rim=True, seam=2.0)
for yl in (282, 308, 334):
    do.add(band([(812, yl), (886, yl - 4)], 3.0), 'lace', 1)
    do.add(band([(812, yl + 3.2), (884, yl - 0.8)], 1.6), 'ink', 0)
add_rivets(do, [(824, 268), (854, 262), (820, 344), (856, 348)], 'gold', r=2.8)
a.flat('ChestScar', 'Chest', 61, band([(858, 268), (840, 296), (852, 294), (828, 336)], 4.2), 'glow', which=1, glow=2.4)
a.flat('ChestScarCore', 'Chest', 62, band([(858, 268), (840, 296), (852, 294), (828, 336)], 1.8), 'glow', which=2, glow=3.0)
add_arm_sleeve(a, 'iron_dark', 'gold')
s3 = a.cel('SodeTier3', 'ArmNU', 107, rounded(
    (886, 314, 1), (976, 304, 1), (984, 352, 1), (930, 364), (888, 350, 1)
), 'iron_dark', sd=5.0, hd=2.2, rim=True, seam=1.9)
s3.add(band([(892, 346), (932, 356), (978, 346)], 3.6), 'gold', 2)
add_rivets(s3, [(908, 330), (936, 334), (964, 326)], 'gold', r=2.5)
s2 = a.cel('SodeTier2', 'ArmNU', 107, rounded(
    (876, 280, 1), (974, 270, 1), (982, 320, 1), (928, 330), (880, 318, 1)
), 'iron', sd=5.2, hd=2.4, rim=True, seam=1.9)
s2.add(band([(882, 312), (928, 322), (976, 312)], 3.0), 'lace', 2)
add_rivets(s2, [(898, 296), (930, 300), (962, 292)], 'gold', r=2.5)
s1 = a.cel('SodeTier1', 'ArmNU', 107, rounded(
    (856, 254, 1), (898, 224, 1), (926, 208, 1), (954, 228, 1), (984, 246, 1), (978, 288, 1), (924, 298), (868, 288, 1)
), 'iron', sd=6.0, hd=3.0, rim=True, seam=2.0)
s1.add(band([(864, 256), (926, 218), (976, 250)], 3.8), 'gold', 2)
add_rivets(s1, [(886, 270), (924, 252), (958, 266)], 'gold', r=3.0)
a.flat('SodeScar', 'ArmNU', 107, band([(904, 262), (928, 294), (916, 300), (946, 338)], 4.0), 'glow', which=1, glow=2.3)
add_obi(a, sash_tone='iron_dark', trim_tone='gold', buckle_tone='gold', style='ring', glow_tone='glow', glow=2.0)
add_split_coat_tails(a, coat_tone='iron_dark', lining_tone='lace', trim_tone='gold', length='mid', notched=True, feel='Haori', rivets=True)
add_kusazuri(a, panels=3, depth=92.0, tone='iron', trim_tone='gold', lace_tone='lace', rows=3, order=68, ornate=True)
add_kote(a, tone='iron', trim_tone='gold', strap_tone='lace', with_tekko=True, ornate=True, heavy=True, glow_tone='glow', glow=2.2)
add_suneate(a, tone='iron', trim_tone='gold', strap_tone='lace', ornate=True)


# ---- Shiden Yoroi (Trophy, Shiden) ----
a = armor(
    'shiden_yoroi', 'vanguard_plate', 'trophy',
    'Shiden trophy lieutenant haori & plate: crisp silver-trimmed officer collar, folded midnight-steel haori lapels, shoulder half-mantle and split chevron coat tails over violet-laced vanguard plate and riveted double-stacked o-sode.',
    {
        'rim': '#EAB308',
        'steel': ('#171824', '#2E3148', '#5E658A'),
        'silver': ('#7A8296', '#CFD6E6', '#FFFFFF'),
        'gold': ('#855E16', '#E0AA32', '#FEF08A'),
        'violet': ('#46247A', '#8B5CF6', '#D8B4FE'),
        'glow': ('#7C3AED', '#B784FF', '#F5EDFF'),
        'ink': ('#07070D', '#121320', '#23253B'),
    },
    retones_coat=False,
)
# Pinned strictly to the rear shoulder/collar seam (x 946..962, y 248) so it never spawns inside the torso
mantle_pts = rounded(
    (946, 248, 1), (964, 246, 1), (1002, 326), (1034, 424, 1),
    (1002, 444, 1), (978, 416, 1), (956, 434, 1), (948, 344), (944, 286, 1)
)
m_ch = sample([(954, 250), (982, 342), (1012, 426)], 4)
a.cloth('HalfMantle', 'Chest', 46, mantle_pts, 'steel', [m_ch], 'Strip', sd=4.8, hd=2.2, rim=True, seam=1.8).add(
    band([(958, 428), (978, 412), (1002, 436), (1028, 420)], 3.4), 'silver', 2
).add(
    band(curve(960, 254, 994, 338, 1026, 416), 3.0), 'violet', 2
)
add_high_collar(a, tone='steel', trim_tone='silver', lining_tone='violet', height='mid', rivets=True, order=50)
a.cel('Gorget', 'Chest', 52, rounded(
    (814, 248, 1), (868, 242), (926, 248, 1), (922, 272, 1), (866, 278, 1), (814, 268, 1)
), 'steel', sd=3.6, hd=1.8, rim=False, seam=1.6).add(
    band([(816, 265), (866, 274), (920, 268)], 3.2), 'silver', 2
)
do = a.cel('Do', 'Chest', 53, rounded(
    (816, 256, 1), (868, 246), (944, 258, 1),
    (954, 306), (950, 362, 1), (880, 368), (806, 360, 1), (806, 306)
), 'steel', sd=6.4, hd=2.8, rim=True, seam=2.0)
for yl in (278, 300, 322, 344):
    do.add(band([(810, yl), (878, yl + 3), (946, yl - 2)], 2.8), 'violet', 1)
do.add(band([(812, 354), (880, 360), (946, 354)], 3.2), 'silver', 2)
do.add(band([(854, 254), (856, 358)], 2.4), 'ink', 0)
add_haori_lapels(a, coat_tone='steel', lapel_tone='violet', trim_tone='silver', style='haori', rivets=True, order=56)
a.cel('BoltPlate', 'Chest', 60, poly(
    830, 282, 846, 282, 838, 304, 850, 302, 828, 338, 834, 312, 822, 314
), 'silver', sd=2.0, hd=1.4, rim=False, seam=1.5)
a.flat('BoltGlow', 'Chest', 61, poly(
    833, 287, 842, 287, 836, 305, 844, 304, 831, 330, 835, 311, 827, 312
), 'glow', which=2, glow=2.2)
add_far_sode(a, 'steel', 'silver')
add_arm_sleeve(a, 'steel', 'silver')
s3 = a.cel('SodeTier3', 'ArmNU', 107, rounded(
    (892, 312, 1), (968, 304, 1), (974, 346, 1), (930, 354), (896, 346, 1)
), 'steel', sd=4.6, hd=2.2, rim=True, seam=1.8)
s3.add(band([(898, 340), (932, 348), (970, 340)], 3.4), 'silver', 2)
s3.add(band([(896, 326), (932, 330), (968, 324)], 2.4), 'violet', 2)
add_rivets(s3, [(910, 319), (934, 321), (958, 317)], 'silver', r=2.1)
s2 = a.cel('SodeTier2', 'ArmNU', 107, rounded(
    (882, 280, 1), (966, 272, 1), (972, 316, 1), (928, 324), (886, 316, 1)
), 'steel', sd=4.8, hd=2.4, rim=True, seam=1.8)
s2.add(band([(888, 310), (928, 317), (968, 310)], 3.0), 'gold', 2)
add_rivets(s2, [(902, 294), (928, 298), (956, 292)], 'silver', r=2.2)
s1 = a.cel('SodeTier1', 'ArmNU', 107, rounded(
    (866, 252, 1), (926, 226, 1), (968, 250, 1), (968, 288, 1), (924, 296), (874, 288, 1)
), 'steel', sd=5.4, hd=2.8, rim=True, seam=1.9)
s1.add(band([(872, 254), (926, 232), (964, 252)], 3.6), 'silver', 2)
s1.add(band([(878, 272), (926, 266), (964, 272)], 2.6), 'violet', 2)
add_rivets(s1, [(890, 260), (926, 248), (954, 260)], 'gold', r=2.3)
add_obi(a, sash_tone='steel', trim_tone='silver', buckle_tone='gold', style='bar', glow_tone='glow', glow=2.0)
add_split_coat_tails(a, coat_tone='steel', lining_tone='violet', trim_tone='silver', length='mid', notched=True, feel='Haori', rivets=True)
add_kusazuri(a, panels=3, depth=90.0, tone='steel', trim_tone='silver', lace_tone='violet', rows=3, order=68, ornate=True, pointed=True, glow_tone='glow', glow=1.9)
add_kote(a, tone='steel', trim_tone='silver', strap_tone='violet', with_tekko=True, ornate=True, glow_tone='glow', glow=1.9)
add_suneate(a, tone='steel', trim_tone='silver', strap_tone='violet', ornate=True, glow_tone='glow', glow=1.8)


# ================================================================================================
# TIER 5: LEGENDARY (#F97316 / #EF4444) — Yūrei Armor & Mugen Armor
# ================================================================================================

# ---- Yūrei Armor (Legendary #1, 3100c) ----
a = armor(
    'yurei_yoroi', 'yurei', 'legendary',
    'Legendary Celestial Spirit Sovereign: ornate porcelain-and-indigo imperial warlord coat with towering gold-trimmed collar, brocade lapels, sweeping 2-layer warlord cape and dramatic split coat tails over royal filigree o-yoroi, riveted dragon-wing pauldrons and drifting spirit embers.',
    {
        'rim': '#A8DCFF',
        'coat': ('#8E9AC2', '#D8E0F4', '#FFFFFF'),
        'cloth': ('#30589E', '#5B98EE', '#B8E0FF'),
        'enamel': ('#8C95B4', '#DCE3F5', '#FFFFFF'),
        'indigo': ('#101830', '#1E2D59', '#3B569E'),
        'gold': ('#8A5C12', '#E5AC30', '#FFF1A8'),
        'glow': ('#38BDF8', '#7DD3FC', '#F0F9FF'),
        'ink': ('#070A14', '#11182E', '#1F2C52'),
    },
    retones_coat=True,
    aura='spirit',
)
# 1. Sweeping 2-layered Celestial Imperial Warlord Cape pinned strictly to the rear shoulder/collar seam (x >= 944)
#    CapeLining at order 45, CapeOuter at order 46 — zero Z-fighting!
cape_inner = rounded(
    (946, 248, 1), (964, 246, 1), (1020, 352), (1076, 500), (1118, 656, 1),
    (1084, 706, 1), (1056, 662, 1), (1028, 716, 1), (998, 664, 1), (976, 704, 1),
    (962, 560), (952, 410), (944, 300, 1)
)
ch_c1 = sample([(954, 250), (1000, 382), (1048, 526), (1092, 682)], 5)
ch_c2 = sample([(946, 260), (964, 405), (982, 545), (1000, 686)], 5)
ci = a.cloth('CapeLining', 'Chest', 45, cape_inner, 'indigo', [ch_c1, ch_c2], 'Cape', sd=5.5, hd=2.0, rim=False, seam=1.8)
ci.add(band(curve(962, 254, 1026, 444, 1096, 672), 4.2), 'gold', 2)

cape_outer = rounded(
    (944, 246, 1), (962, 244, 1), (1014, 340), (1066, 480), (1104, 626, 1),
    (1070, 662, 1), (1042, 620, 1), (1014, 670, 1), (986, 624, 1), (964, 654, 1),
    (954, 520), (948, 386), (942, 292, 1)
)
co = a.cloth('CapeOuter', 'Chest', 46, cape_outer, 'enamel', [ch_c1, ch_c2], 'Cape', sd=6.5, hd=3.0, rim=True, seam=1.9)
co.add(band(curve(958, 250, 1016, 426, 1080, 642), 4.8), 'gold', 2)
co.add(band(curve(950, 260, 1002, 430, 1062, 632), 2.6), 'cloth', 1)
co.add(band([(966, 642), (986, 616), (1014, 658), (1042, 612), (1070, 650), (1094, 620)], 4.0), 'gold', 2)
co.add(disc(1024, 538, 15.0), 'cloth', 1, 0.85)
co.add(disc(1024, 538, 9.0), 'enamel', 2, 0.92)
co.add(disc(1024, 538, 4.5), 'gold', 2, 0.95)
co.add(disc(1046, 566, 9.0), 'gold', 2, 0.92)

# Ornate gilded celestial back-collar wing + tall gold-trimmed imperial collar
back_plume = rounded(
    (934, 248, 1), (972, 194, 1), (1002, 180, 1), (990, 224, 1), (958, 256, 1)
)
a.cel('CollarWing', 'Chest', 48, back_plume, 'gold', sd=3.5, hd=2.2, rim=True, seam=1.7).add(
    band(curve(946, 244, 974, 206, 994, 188), 3.0), 'enamel', 2
)
add_high_collar(a, tone='indigo', trim_tone='gold', lining_tone='cloth', height='tall', spiked=False, rivets=True, order=50)
gor = a.cel('Gorget', 'Chest', 52, rounded(
    (808, 244, 1), (866, 236), (934, 240, 1), (944, 268, 1), (924, 278, 1), (864, 286, 1), (810, 272, 1)
), 'indigo', sd=4.2, hd=2.0, rim=True, seam=1.8)
gor.add(band([(812, 248), (866, 242), (930, 246)], 3.2), 'gold', 2)
gor.add(band([(812, 269), (864, 281), (922, 274)], 3.8), 'gold', 2)

# 2. Sculpted Royal O-Yoroi Cuirass + Layered Imperial Brocade Coat Lapels
do = a.cel('Do', 'Chest', 53, rounded(
    (812, 256, 1), (868, 246), (948, 258, 1),
    (958, 308), (954, 364, 1), (880, 372), (804, 362, 1), (804, 306)
), 'indigo', sd=6.8, hd=2.8, rim=True, seam=2.0)
for yl in (282, 306, 330, 352):
    do.add(band([(808, yl), (878, yl + 5), (950, yl - 2)], 2.8), 'gold', 1)
carapace = a.cel('BreastPlate', 'Chest', 55, rounded(
    (812, 260, 1), (854, 254), (888, 266, 1),
    (890, 314), (872, 352, 1), (836, 358, 1), (808, 340, 1), (806, 298)
), 'enamel', sd=5.2, hd=2.8, rim=True, seam=1.9)
carapace.add(band([(810, 336), (836, 353), (870, 347), (886, 312)], 4.0), 'gold', 2)
carapace.add(band([(814, 264), (854, 258), (884, 270)], 3.4), 'gold', 2)
carapace.add(band(curve(838, 304, 858, 282, 882, 278), 3.0), 'gold', 2)
carapace.add(band(curve(838, 304, 860, 326, 880, 332), 3.0), 'gold', 2)
carapace.add(band([(838, 262), (838, 350)], 3.0), 'gold', 2)
add_haori_lapels(a, coat_tone='enamel', lapel_tone='indigo', trim_tone='gold', style='warlord', rivets=True, order=56)
a.cel('SpiritSeal', 'Chest', 60, diamond(838, 304, 15.0, 20.0), 'gold', sd=2.8, hd=2.0, rim=False, seam=1.6).add(
    diamond(838, 304, 10.5, 14.5), 'indigo', 0
)
a.flat('SpiritSealGlow', 'Chest', 61, diamond(838, 304, 8.0, 11.5), 'glow', which=1, glow=2.8)
a.flat('SpiritSealCore', 'Chest', 62, star(838, 304, 7.0, 2.8, n=4, rot=-90.0), 'glow', which=2, glow=3.4)

# 3. Grand Sculpted Riveted Winged Royal O-Sode Pauldrons + Sleeve Guard
add_far_sode(a, 'enamel', 'gold')
add_arm_sleeve(a, 'indigo', 'gold', elbow_fin=True)
s4 = a.cel('SodeTier4', 'ArmNU', 107, rounded(
    (894, 326, 1), (972, 314, 1), (982, 356, 1), (932, 366), (894, 354, 1)
), 'indigo', sd=4.8, hd=2.2, rim=True, seam=1.8)
s4.add(band([(898, 350), (934, 360), (978, 350)], 3.8), 'gold', 2)
add_rivets(s4, [(912, 338), (936, 342), (964, 334)], 'gold', r=2.2)
s3 = a.cel('SodeTier3', 'ArmNU', 107, rounded(
    (884, 294, 1), (972, 282, 1), (980, 326, 1), (930, 336), (886, 326, 1)
), 'enamel', sd=5.0, hd=2.4, rim=True, seam=1.8)
s3.add(band([(890, 320), (930, 328), (976, 318)], 3.6), 'gold', 2)
s3.add(band([(894, 306), (930, 310), (970, 300)], 2.4), 'cloth', 1)
add_rivets(s3, [(904, 308), (930, 312), (960, 304)], 'gold', r=2.3)
s2 = a.cel('SodeTier2', 'ArmNU', 107, rounded(
    (870, 260, 1), (928, 232), (974, 250, 1), (978, 292, 1), (928, 304), (874, 294, 1)
), 'enamel', sd=5.6, hd=3.0, rim=True, seam=1.9)
s2.add(band([(874, 262), (928, 238), (970, 254)], 4.0), 'gold', 2)
s2.add(band([(878, 288), (928, 297), (974, 286)], 3.4), 'gold', 2)
add_rivets(s2, [(888, 272), (962, 268)], 'gold', r=2.5)
crest_wing = a.cel('SodeWingCrest', 'ArmNU', 107, rounded(
    (876, 250, 1), (918, 214, 1), (968, 206, 1), (994, 224, 1), (964, 246, 1), (926, 242), (892, 258, 1)
), 'gold', sd=4.0, hd=2.4, rim=True, seam=1.8)
crest_wing.add(band(curve(892, 246, 934, 224, 982, 222), 3.0), 'enamel', 2)
a.flat('SodeRuneGlow', 'ArmNU', 107, diamond(926, 268, 8.0, 10.5), 'glow', which=1, glow=2.8)
a.flat('SodeRuneCore', 'ArmNU', 107, diamond(926, 268, 4.2, 5.8), 'glow', which=2, glow=3.4)

# 4. Royal Obi, Long Split Coat Tails (36/65/66), Rear Waist Ribbon (67), Rigid 4-Panel Kusazuri (68..71), Gauntlets & Greaves
add_obi(a, sash_tone='indigo', trim_tone='gold', buckle_tone='gold', style='royal', glow_tone='glow', glow=2.8)
add_split_coat_tails(a, coat_tone='enamel', lining_tone='indigo', trim_tone='gold', length='long', notched=True,
                     feel='Haori', glow_tone='glow', glow=2.6, rivets=True)
sash_r1 = tine(curve(904, 400, 934, 472, 962, 578), 14.0, 4.0, n=14, w_mid=16.0)
a.cloth('WaistRibbonA', 'Hips', 67, sash_r1, 'cloth', [sample(curve(904, 400, 934, 472, 962, 578), 4)], 'Tails', sd=3.2, hd=1.8, rim=True).add(
    band(curve(904, 404, 934, 474, 958, 570), 3.2), 'gold', 2
)
add_kusazuri(a, panels=4, depth=102.0, tone='enamel', trim_tone='gold', lace_tone='cloth', rows=4, order=68, ornate=True, pointed=True, glow_tone='glow', glow=2.6)
add_kote(a, tone='enamel', trim_tone='gold', strap_tone='indigo', with_tekko=True, ornate=True, bladed=True, glow_tone='glow', glow=2.6)
add_suneate(a, tone='enamel', trim_tone='gold', strap_tone='indigo', ornate=True, glow_tone='glow', glow=2.6)


# ---- Mugen Armor (Legendary #2, 4200c + 25 gems) ----
a = armor(
    'mugen_yoroi', 'mugen', 'legendary',
    'Legendary Dark-Electric Void Sovereign: towering gold-trimmed warlord coat and flame-cut cape with layered royal lapels and long split tails over obsidian-violet filigree o-yoroi, riveted horned pauldrons, rayed halo and crackling energy arcs.',
    {
        'rim': '#E08CFF',
        'coat': ('#08060D', '#110A1A', '#221436'),
        'cloth': ('#43166C', '#7C2CC4', '#E4A8FF'),
        'obsidian': ('#0E0818', '#1D1130', '#3C2363'),
        'gold': ('#8E5A10', '#E8AD2E', '#FFF2A6'),
        'violet': ('#581C87', '#A855F7', '#F0C4FF'),
        'glow': ('#A855F7', '#E06BFF', '#FFFFFF'),
        'ink': ('#050308', '#0E0816', '#1E1230'),
    },
    retones_coat=True,
    aura='void',
)

def halo_arc(cx, cy, r_out, r_in, a0=156.0, a1=374.0, n=32):
    out_pts = [(cx - math.cos(math.radians(a0 + (a1 - a0) * i / n)) * r_out,
                cy + math.sin(math.radians(a0 + (a1 - a0) * i / n)) * r_out) for i in range(n + 1)]
    in_pts = [(cx - math.cos(math.radians(a1 - (a1 - a0) * i / n)) * r_in,
               cy + math.sin(math.radians(a1 - (a1 - a0) * i / n)) * r_in) for i in range(n + 1)]
    return clean(out_pts + in_pts)

# 1. Majestic Sculpted Celestial Crescent Halo behind Head (B.Head, orders 79/82/83, < Head_Hair 84)
for deg_s in (210.0, 245.0, 280.0, 315.0):
    rad = math.radians(deg_s)
    dx, dy = -math.cos(rad), math.sin(rad)
    px_, py_ = -dy, dx
    base_r, tip_r = 148.0, 174.0
    spike = clean([
        (899.0 + dx * base_r + px_ * 6.5, 144.0 + dy * base_r + py_ * 6.5),
        (899.0 + dx * tip_r, 144.0 + dy * tip_r),
        (899.0 + dx * base_r - px_ * 6.5, 144.0 + dy * base_r - py_ * 6.5),
    ])
    a.cel('HaloRay', 'Head', 79, spike, 'gold', sd=2.0, hd=1.4, rim=False, seam=1.4)
a.cel('HaloFrame', 'Head', 82, halo_arc(899.0, 144.0, 152.0, 128.0), 'gold', sd=3.5, hd=2.0, rim=False, seam=1.8).add(
    halo_arc(899.0, 144.0, 148.0, 132.0), 'obsidian', 1
)
a.flat('HaloGlow', 'Head', 83, halo_arc(899.0, 144.0, 145.0, 135.0), 'glow', which=1, glow=2.6)
a.flat('HaloCore', 'Head', 83, halo_arc(899.0, 144.0, 141.5, 138.5), 'glow', which=2, glow=3.4)

# 2. Dramatic Flame-Cut Sovereign Warlord Cape pinned strictly to rear shoulder seam (x >= 944, orders 45 / 46)
cape_l = rounded(
    (946, 248, 1), (966, 246, 1), (1026, 356), (1082, 506), (1126, 668, 1),
    (1092, 718, 1), (1064, 668, 1), (1036, 728, 1), (1006, 672, 1), (978, 714, 1),
    (962, 566), (952, 412), (944, 298, 1)
)
ch_m1 = sample([(956, 250), (1006, 386), (1058, 534), (1100, 692)], 5)
ch_m2 = sample([(946, 260), (966, 412), (984, 558), (1002, 694)], 5)
cl = a.cloth('CapeLining', 'Chest', 45, cape_l, 'violet', [ch_m1, ch_m2], 'Cape', sd=5.0, hd=2.4, rim=False, seam=1.8)
cl.add(band(curve(964, 254, 1030, 444, 1104, 684), 4.4), 'gold', 2)

cape_o = rounded(
    (944, 246, 1), (964, 242, 1), (1018, 346), (1070, 488), (1110, 636, 1),
    (1076, 676, 1), (1050, 632, 1), (1022, 686, 1), (994, 634, 1), (968, 668, 1),
    (956, 532), (950, 390), (942, 290, 1)
)
co = a.cloth('CapeOuter', 'Chest', 46, cape_o, 'obsidian', [ch_m1, ch_m2], 'Cape', sd=6.5, hd=2.8, rim=True, seam=1.9)
co.add(band(curve(960, 250, 1020, 430, 1086, 652), 4.6), 'gold', 2)
co.add(band([(970, 656), (994, 624), (1022, 674), (1050, 622), (1076, 664), (1100, 630)], 4.0), 'gold', 2)
co.add(band([(1024, 654), (1016, 594), (1028, 548), (1014, 486)], 3.2), 'glow', 1)
co.add(band([(1068, 646), (1054, 586), (1064, 532), (1046, 470)], 2.8), 'glow', 1)

# 3. Towering Spiked Sovereign Warlord Collar & Gorget
add_high_collar(a, tone='obsidian', trim_tone='gold', lining_tone='violet', height='tall', spiked=True, rivets=True, order=50)
gor = a.cel('Gorget', 'Chest', 52, rounded(
    (808, 242, 1), (866, 234), (928, 236, 1), (954, 216, 1), (948, 268, 1), (924, 278, 1), (864, 286, 1), (810, 272, 1)
), 'obsidian', sd=4.4, hd=2.2, rim=True, seam=1.8)
gor.add(band([(812, 246), (866, 240), (928, 242), (950, 222)], 3.4), 'gold', 2)
gor.add(band([(812, 269), (864, 281), (924, 274)], 3.8), 'gold', 2)

# 4. Grand Sculpted Sovereign Cuirass + Layered Obsidian-Gold Royal Surcoat Lapels
do = a.cel('Do', 'Chest', 53, rounded(
    (810, 256, 1), (868, 246), (948, 258, 1),
    (960, 308), (956, 366, 1), (880, 374), (802, 362, 1), (802, 306)
), 'obsidian', sd=6.8, hd=2.8, rim=True, seam=2.0)
for yl in (280, 304, 328, 352):
    do.add(band([(806, yl), (878, yl + 5), (952, yl - 2)], 2.8), 'gold', 1)
    do.add(band([(808, yl + 3.2), (878, yl + 8.2), (950, yl + 1.2)], 1.8), 'violet', 1)
car = a.cel('BreastPlate', 'Chest', 55, rounded(
    (810, 260, 1), (854, 254), (890, 266, 1),
    (892, 316), (874, 354, 1), (836, 360, 1), (806, 342, 1), (804, 298)
), 'obsidian', sd=5.2, hd=2.6, rim=True, seam=1.9)
car.add(band([(808, 338), (836, 355), (872, 349), (888, 314)], 4.2), 'gold', 2)
car.add(band([(812, 264), (854, 258), (886, 270)], 3.6), 'gold', 2)
car.add(band(curve(838, 304, 860, 280, 884, 276), 3.2), 'gold', 2)
car.add(band(curve(838, 304, 862, 326, 882, 334), 3.2), 'gold', 2)
car.add(band([(838, 260), (838, 352)], 3.0), 'gold', 2)
add_haori_lapels(a, coat_tone='coat', lapel_tone='obsidian', trim_tone='gold', style='warlord', rivets=True, order=56)
a.cel('CoreRing', 'Chest', 60, disc(838, 304, 16.0), 'gold', sd=2.8, hd=2.0, rim=False, seam=1.6).add(
    disc(838, 304, 11.5), 'obsidian', 0
)
a.flat('CoreGlow', 'Chest', 61, ring(838, 304, 11.2, 6.6), 'glow', which=1, glow=2.8)
a.flat('CoreStar', 'Chest', 62, star(838, 304, 7.6, 3.0, n=4, rot=-45.0), 'glow', which=2, glow=3.6)

# 5. Massive Sculpted Riveted Horned Sovereign O-Sode Pauldrons + Sleeve Guard
add_far_sode(a, 'obsidian', 'gold')
add_arm_sleeve(a, 'obsidian', 'gold', elbow_fin=True)
s4 = a.cel('SodeTier4', 'ArmNU', 107, rounded(
    (892, 326, 1), (974, 314, 1), (986, 358, 1), (932, 368), (892, 356, 1)
), 'obsidian', sd=5.0, hd=2.4, rim=True, seam=1.9)
s4.add(band([(896, 352), (934, 362), (982, 352)], 3.8), 'gold', 2)
add_rivets(s4, [(910, 338), (936, 342), (966, 334)], 'gold', r=2.3)
s3 = a.cel('SodeTier3', 'ArmNU', 107, rounded(
    (882, 294, 1), (974, 282, 1), (984, 328, 1), (930, 338), (884, 326, 1)
), 'obsidian', sd=5.2, hd=2.6, rim=True, seam=1.9)
s3.add(band([(888, 322), (930, 330), (980, 320)], 3.6), 'gold', 2)
s3.add(band([(892, 308), (930, 312), (974, 302)], 2.6), 'violet', 2)
add_rivets(s3, [(902, 308), (930, 312), (962, 304)], 'gold', r=2.4)
s2 = a.cel('SodeTier2', 'ArmNU', 107, rounded(
    (866, 258, 1), (928, 228), (978, 246, 1), (982, 292, 1), (928, 304), (872, 294, 1)
), 'obsidian', sd=5.8, hd=3.0, rim=True, seam=2.0)
s2.add(band([(872, 260), (928, 234), (974, 250)], 4.0), 'gold', 2)
s2.add(band([(876, 288), (928, 298), (978, 286)], 3.6), 'gold', 2)
add_rivets(s2, [(886, 272), (964, 268)], 'gold', r=2.6)
horn_crest = a.cel('SodeHornCrest', 'ArmNU', 107, rounded(
    (872, 250, 1), (914, 214, 1), (956, 198, 1), (998, 206, 1), (970, 236, 1), (988, 250, 1),
    (956, 252, 1), (924, 240), (888, 258, 1)
), 'gold', sd=4.2, hd=2.4, rim=True, seam=1.8)
horn_crest.add(band(curve(892, 244, 938, 218, 988, 210), 3.2), 'obsidian', 1)
a.flat('SodeGlowA', 'ArmNU', 107, diamond(926, 268, 9.0, 11.0), 'glow', which=1, glow=2.8)
a.flat('SodeGlowCore', 'ArmNU', 107, star(926, 268, 6.5, 2.5, n=4, rot=-90.0), 'glow', which=2, glow=3.5)

# 6. Sovereign Obi, Long Split Coat Tails (36/65/66), Rear Waist Ribbon (67), Rigid 4-Panel Kusazuri (68..71), Gauntlets & Greaves
add_obi(a, sash_tone='obsidian', trim_tone='gold', buckle_tone='gold', style='royal', glow_tone='glow', glow=3.0)
add_split_coat_tails(a, coat_tone='obsidian', lining_tone='violet', trim_tone='gold', length='long', notched=True,
                     feel='Haori', glow_tone='glow', glow=2.8, rivets=True)
w_rib = tine(curve(906, 400, 938, 474, 966, 584), 15.0, 4.0, n=14, w_mid=17.0)
a.cloth('WaistRibbonA', 'Hips', 67, w_rib, 'cloth', [sample(curve(906, 400, 938, 474, 966, 584), 4)], 'Tails', sd=3.4, hd=1.8, rim=True).add(
    band(curve(906, 404, 938, 476, 962, 576), 3.2), 'gold', 2
)
add_kusazuri(a, panels=4, depth=104.0, tone='obsidian', trim_tone='gold', lace_tone='violet', rows=4, order=68, ornate=True, pointed=True, glow_tone='glow', glow=2.8)
add_kote(a, tone='obsidian', trim_tone='gold', strap_tone='violet', with_tekko=True, ornate=True, bladed=True, glow_tone='glow', glow=2.8)
add_suneate(a, tone='obsidian', trim_tone='gold', strap_tone='violet', ornate=True, glow_tone='glow', glow=2.7)
