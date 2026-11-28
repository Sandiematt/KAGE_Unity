"""The five-tier armor catalogue (Common, Rare, Epic, Trophy, Legendary) with tailored coats/haori,
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
    65 HaoriTailFront (embroidered front coat panel, cleanly beneath Kusazuri),
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
from components import (add_high_collar, add_haori_lapels, add_split_coat_tails, add_arm_sleeve,
                        add_kote, add_suneate, add_kusazuri, add_far_sode, add_obi, add_rivets)
from tailoring import tailor_act_one
from trophies import build_trophies

ALL = []


def armor(aid, look, tier, summary, palette, retones_coat=True, aura='none'):
    a = Armor(aid, look, tier, summary, palette, retones_coat=retones_coat, aura=aura)
    ALL.append(a)
    return a


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


tailor_act_one(ALL)
build_trophies(armor)
