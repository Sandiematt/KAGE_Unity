# Helmet design: rarity tiers on a silhouette (SAN-72)

Research behind the SAN-72 tier redesign of Kage's 14 Act I helmets. The owner's reference is `docs/reference/helmet_tiers.png`:
five tiers in side view, COMMON, EPIC, TROPHY, LEGENDARY and GOD MODE. The owner asked for every helmet to be redesigned in its
tier's language, not for the five pictures to be copied. The designs are in `Tools/helmet_art/helmets.py`.

## What the sources say

**Shadow Fight 2 and 3: the silhouette is the gear.** In Shadow Fight 2 every fighter is a black silhouette. Equipment and its
enchantment glow are the only colour, so a helm reads from its outline first. The SF2 helm line runs from plain shapes (Light Helm,
Kendo Mask, Conical Hat, Snake Mask) to segmented, crested and masked late helms (Lobster Helm, Mantis Helmet, Legionnaire Helm,
Dawn Helm, Master's Helm). "Super" helms come pre-enchanted, and the set helms of the late chapters carry the set's colours and
glow. Shadow Fight 3 grades gear Common, Rare, Epic, Legendary and Unique. Its three factions each have a material language: Legion
(heavy plate and steel), Dynasty (lacquer, gold and red, an Asian court look) and Heralds (dark, mystical, glowing shadow energy).
Kage is a black silhouette with one white eye, the same situation as a Shadow Fight hero, so the helmet has to carry the tier on
its own.

The Shadow Fight wiki pages refused automated reading. The SF2 helm names come from the wiki and NamuWiki search snippets. The SF3
rarity names and faction materials come from search results and from memory of the games, and should be checked against the games
before anyone quotes them.

**Rarity in games is colour first, never colour alone.** The grey, green, blue, purple and orange ladder (Diablo II, World of
Warcraft) is the convention players already read. Design write-ups add that the tier should show without colour too: shape and
complexity grow by tier, more elements and more motion at the top, and glow and particles come last as a secondary cue. One of them
warns that if everything glows, nothing reads.

**Japanese helmets give real shapes to build on.**
- *Zunari* (head-shaped, three to five plates), *momonari* (peach) and *suji bachi* (a multi-plate bowl with raised ribs) are the
  plain-to-rich ladder of the bowl. *Hoshi bachi* / *ko-boshi* bowls are studded with rivets ("stars"). *Eboshi-nari* kabuto copy
  the court cap, sometimes very tall (*naga eboshi*). *Zukin-nari* kabuto copy a hood.
- The parts: the *shikoro* (neck guard of laced lames), the *fukigaeshi* (the turned-back flaps beside the face, where the
  family *mon* sits), the *mabizashi* (the visor), the *maedate* (the front crest: crescents, *kuwagata* horns, antlers, flames).
- *Kawari kabuto* ("unusual helmets") of the Sengoku period turned the helmet into a sculpture: crescent moons, flames, dragons, and
  the demon (*shikami*) with horns as a fore-crest. Fudō Myō-ō engulfed in flames is one Met maedate. This is the source for the top
  tiers: the higher the rank, the larger and stranger the crest.

## Principles and how the designs use them

| Principle | Where it shows |
|---|---|
| The tier reads from the outline before the colour | COMMON stays inside or close to the skull (hoods, a band, a flat kasa). EPIC adds swept blades. TROPHY adds height (laurel, the tall eboshi) and long ribbons. LEGENDARY adds a big crest (antlers, crescent). GOD MODE adds horns, flames and a broken fire halo, the biggest outline. |
| One colour family per tier, matching the owner's sheet and the common rarity ladder | `PALETTES` in `Tools/helmet_art/kit.py`: grey cloth and steel; black shell and violet; white enamel and gold; black lacquer, gold and red; black shell and burning red. The back-light rim takes the tier colour too (grey, violet, gold, amber, red), so even a black helmet is edged in its tier. |
| Light is the last step and is rationed | COMMON has no glow. EPIC has one violet ring at the ear, thin violet seam lines and star rivets. TROPHY has only a pale glass gem. LEGENDARY is lit metal with no glow. GOD MODE glows (horns, flames, the emblem's ring, a red light behind the eye). A test checks it. |
| Motion grows with the tier | Every helmet has a soft piece on the game's spring chains (knot tails, cord tails, a drape and a veil, a violet cord, white ribbons, red tassels and cords, fire tails, and the wrap's loose end). The higher tiers have longer and more of them, so they trail further and take the wind more. |
| The face stays Kage's | His black face and white eye are never covered. Every face opening leaves the eye on bare black. The god-mode glow sits behind the eye (the eye is drawn on top). Hair is hidden under every hood, cap, hat and kabuto (the owner disliked it poking through). It stays only under the brow band, the shinobi *hitai-ate* look. |
| The neck wrap ties the set together | As in the owner's sheet, each helmet but the veil winds a tier-coloured wrap round his neck over the front of the crimson scarf wrap. The scarf's knot and its two crimson tails still stream behind: the scarf stays his signature. |
| Variety inside a tier comes from a different main shape | COMMON: a hood, a hat, a band, a veil. EPIC: a fin-swept helm, horns over a jaw mask, a crested star helm. TROPHY: a hood under a laurel, a tall court cap. LEGENDARY: antlers on a suji bachi, a crescent on a zukin-nari. GOD MODE: a flame crest under a fire halo, great horns with lightning. |

## The 14 helmets

| Helmet | JS tier | Design tier | Design (Japanese type it builds on) |
|---|---|---|---|
| Shiro Maki (Crimson Scarf) | starter | Common | No design: his bare spiky hair and the crimson scarf. That look is the starter's name, and it is the baseline the other tiers are measured against. |
| Kuro Zukin | common, 300 | Common | An ash-grey shinobi hood with an eye slit, a cord knotted at the back with two tails (*zukin*). |
| Sugegasa | common, 450 | Common | A wide conical straw *sugegasa* over a grey head cloth, a chin cord, two cord tails at the nape. |
| Hachigane | common, 600 | Common | A grey cloth band with a riveted steel brow plate (*hachigane*), long knot tails, a grey half mask. His hair stays. |
| Kasumi Veil | common, 750 | Common | A peaked grey cowl with a long back drape and a pale gauze veil from under the eye to the chest. |
| Tetsu Kabuto | rare, 1200 | Epic | A sleek black helm with a pointed brow, four violet blade fins swept back, a glowing violet ear ring. |
| Oni Menpō | rare, 12 gems | Epic | A black skull cap with two violet horns hooking forward and an angular oni *menpō* with glowing fangs. |
| Hoshi Kabuto | rare, 1600 | Epic | A faceted black *hoshi bachi* with glowing violet star rivets, a tall bladed crest, a long visor, a violet cord. |
| Hisame Zukin | trophy (Hisame) | Trophy | A white enamel hood edged in gold, a gold laurel with a pale rain-drop gem, a star medallion, white ribbons. |
| Sumi Eboshi | trophy (Kuroe) | Trophy | A tall white lacquer *eboshi* swept back, one black ink stroke across it, a gold band and star medallion, white ribbons. |
| Shika Kabuto | epic, 2300 | Legendary | A black *suji bachi* laced in red, a gold band and visor edge, branching gold antlers, a red and gold mon, a red tassel. |
| Shikkō Zukin | epic, 2900 | Legendary | A tall *zukin-nari* kabuto whose peak curls back, a great gold crescent maedate, a red mon, red cords. |
| Utsuro Kabuto | epic, 25 gems | God Mode | A black demonic helm under a broken ring of red fire, flames streaming off the crown, jagged shards, a red-lit eye. |
| Raijin Kabuto | trophy (Raikon) | God Mode | A horned war helm: two great burning horns, a lightning crest, a fanged jaw guard, the thunder-drum emblem. |

The tiers follow price and standing. The four cheapest shop helmets are COMMON. The three rare ones are EPIC. Two of the three epics
are LEGENDARY. The two premium helmets at the top, the 25-gem Utsuro and the Act I boss's own Raijin (the highest health in Act I),
are GOD MODE. Hisame's and Kuroe's rewards are TROPHY.

## Sources

- [Shadow Fight Wiki: Helm (SF2)](https://shadowfight.fandom.com/wiki/Helm_(SF2)) and [Equipment (SF2)](https://shadowfight.fandom.com/wiki/Equipment_(SF2)) (search snippets: shop helms, super helms, set helms)
- [NamuWiki: Shadow Fight 2 equipment](https://en.namu.wiki/w/%EC%84%80%EB%8F%84%EC%9A%B0%20%ED%8C%8C%EC%9D%B4%ED%8A%B8%202/%EC%9E%A5%EB%B9%84) (search snippets: helms by chapter)
- [Shadow Fight 2, Wikipedia](https://en.wikipedia.org/wiki/Shadow_Fight_2) (silhouette art style)
- [TalkAndroid: Shadow Fight 3 gameplay guide](https://www.talkandroid.com/39694-shadow-fight-3-gameplay-guide/) (SF3 rarities)
- [Kabuto, Wikipedia](https://en.wikipedia.org/wiki/Kabuto); [The shapes of samurai helmets](https://www.giuseppepiva.com/?p=8084); [Eboshi kabuto](https://samuraihistoryculture.substack.com/p/eboshi-kabuto-samurai-helmets); [Zunari kabuto](https://samuraihistoryculture.substack.com/p/zunari-kabuto-samurai-helmets)
- [Kabuto: the helmet of the samurai (Samurai Museum)](https://samuraimuseum.de/en/?p=52682); [Met: Helmet (Zukinnari Kabuto)](https://www.metmuseum.org/toah/works-of-art/36.25.81); [Met: Kawari-kabuto in the shape of a wave](https://metmuseum.org/en/art/collection/search/775457)
- [TV Tropes: Color-Coded Item Tiers](https://tvtropes.org/pmwiki/pmwiki.php/Main/ColorCodedItemTiers); [GameSpot: how colour-coded loot conquered gaming](https://gamespot.com/articles/how-color-coded-loot-conquered-gaming/1100-6473027/); [Handling item rarity and visual tiers](https://app.studyraid.com/en/read/100671/4507956/handling-item-rarity-and-visual-tiers)
