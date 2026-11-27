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

## Current wearable roster

The armory follows the five tiers in `GameRules.HelmetTiers` and the progression in `HelmetOrder`. The art kit defines each named material as a shade, base and highlight plus a tier rim.

| Helmet ID | Tier | Materials | Side profile |
|---|---|---|---|
| shiro_maki | Common | Crimson scarf, dark hair | A high crossed fold beneath the bare head, with the knot seated between the existing ribbon anchors. |
| kuro_zukin | Common | cloth, ink, wrap | Matte charcoal shinobi hood wrapped cleanly to the jaw with a sharp eye slit, layered cloth folds and fluttering back tails. |
| sugegasa | Rare | straw, straw_dark | Wide conical woven golden-straw kasa with crisp radial ribs, a broad shadow facet, chin cord and hanging brim tassels. |
| hachigane | Rare | cloth, ink, steel | Kage keeps his spiky hair over a slate ninja headband with a riveted brushed-steel brow plate, incised lightning slash and trailing knot tails. |
| kasumi_men | Rare | cloth, ink, steel | Armored gunmetal-steel hood with hinged riveted cheek plate, layered iron-cloth lower face veil and sweeping back cloth. |
| tetsu_kabuto | Epic | glow, gold, iron_dark, steel, wrap | Riveted forged-iron samurai kabuto with swept twin iron blades, brass brow stud, tiered iron shikoro and stern iron menpo mask. |
| oni_menpo | Epic | glow, iron_dark, plume, shell, steel, wrap | Dark forged iron oni helm with twin curving demon horns, tiered iron neck plates, fierce arcane-lit oni eye and fanged face guard. |
| hoshi_kabuto | Epic | glow, iron_dark, plume, shell, steel, wrap | Studded obsidian-iron sentinel kabuto crowned with a sweeping arcane-violet horsehair plume, pointed visor and layered purple collar. |
| shika_kabuto | Trophy | antler, crimson, enamel, gold, ink, ribbon, wrap | Ceremonial white-enamel peaked kabuto with tall branching golden-bronze stag antlers, red-and-gold laced shikoro and ivory ribbons. |
| sumi_eboshi | Trophy | enamel, gold, ink, ribbon | Tall ceremonial ivory-lacquer court cap swept high, slashed with a bold sumi-ink brushstroke, imperial gold brow band and long white ribbons. |
| hisame_zukin | Trophy | enamel, glow, gold, ribbon, wrap | Ivory-white ceremonial cloth hood adorned with gold rain embroidery, draped white neck folds and long fluttering white ribbons. |
| raijin_kabuto | Legendary | crimson, fire, glow, gold, horn, shell | Blazing demonic war kabuto with twin forked thunder horns, inset ember grooves, fanged demon jaw and blazing crimson-gold demon eye. |
| utsuro_kabuto | Legendary | crimson, fire, glow, gold, shell | Obsidian demonic war helm with a swept, split-vane crest, magma cracks and hollow fiery eye. |
| shikko_zukin | Legendary | crimson, fire, glow, gold, shell | Tall curved obsidian-crimson executioner kabuto with a single molten-gold blade crest, razor-thin fiery eye and crimson cords. |

The starter uses the base rig. The thirteen other helmets use their existing head frame and cloth chains. Hakobi-gasa, Utsushi-men, Deshi Tenugui, Mokushi Kabuto and Kasa are later-act catalog records without wearable drawings in this armory.

## Sources

- [Shadow Fight Wiki: Helm (SF2)](https://shadowfight.fandom.com/wiki/Helm_(SF2)) and [Equipment (SF2)](https://shadowfight.fandom.com/wiki/Equipment_(SF2)) (search snippets: shop helms, super helms, set helms)
- [NamuWiki: Shadow Fight 2 equipment](https://en.namu.wiki/w/%EC%84%80%EB%8F%84%EC%9A%B0%20%ED%8C%8C%EC%9D%B4%ED%8A%B8%202/%EC%9E%A5%EB%B9%84) (search snippets: helms by chapter)
- [Shadow Fight 2, Wikipedia](https://en.wikipedia.org/wiki/Shadow_Fight_2) (silhouette art style)
- [TalkAndroid: Shadow Fight 3 gameplay guide](https://www.talkandroid.com/39694-shadow-fight-3-gameplay-guide/) (SF3 rarities)
- [Kabuto, Wikipedia](https://en.wikipedia.org/wiki/Kabuto); [The shapes of samurai helmets](https://www.giuseppepiva.com/?p=8084); [Eboshi kabuto](https://samuraihistoryculture.substack.com/p/eboshi-kabuto-samurai-helmets); [Zunari kabuto](https://samuraihistoryculture.substack.com/p/zunari-kabuto-samurai-helmets)
- [Kabuto: the helmet of the samurai (Samurai Museum)](https://samuraimuseum.de/en/?p=52682); [Met: Helmet (Zukinnari Kabuto)](https://www.metmuseum.org/toah/works-of-art/36.25.81); [Met: Kawari-kabuto in the shape of a wave](https://metmuseum.org/en/art/collection/search/775457)
- [TV Tropes: Color-Coded Item Tiers](https://tvtropes.org/pmwiki/pmwiki.php/Main/ColorCodedItemTiers); [GameSpot: how colour-coded loot conquered gaming](https://gamespot.com/articles/how-color-coded-loot-conquered-gaming/1100-6473027/); [Handling item rarity and visual tiers](https://app.studyraid.com/en/read/100671/4507956/handling-item-rarity-and-visual-tiers)
