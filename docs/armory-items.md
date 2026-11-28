# What the Armory lists (SAN-85, SAN-108)

The Armory has weapon, hairstyle and armor tabs. The gear lists come from `ArmoryListing` and `ArmoryView.Items`.

## The classes

- **(a) Implemented**: drawn on Kage in fights (a registered weapon family with its own drawing, an `ArmorVisuals` look, or the starter's own design), and the player can get it in the Prologue or Act I (starter, sold in the shop, or granted by an Act I stage in `JsTables.Stages`). Listed.
- **(b) Later act**: planned, with no Unity implementation yet: no drawing, no way to get it before a later act, or a stand-in for a family that is out of scope. Not listed.
- **(c) Placeholder**: enemy-only or retired JS gear that was never meant for the player. Not listed (it never was).

## One armour list, no Trophies section

The Armor tab shows one list: the starter and the seven shop armours, then the Act I reward armours (Ikazuchi-dō from Garan, Shiden Yoroi from Shiden) once the save owns them, and not before, so there is no locked "Trophy" card. A worn reward armour is selected when the tab opens and can be equipped like any other. The trophy armours' data and drawings (`ArmorLooksTrophies.cs`) stay: fights, results and the profile still draw them. The title's Trophies diamond and overlay (every boss stage and whether it has fallen) is a different feature and is unchanged.

## Weapons

| Item | Where from | Family | Class | Armory |
| --- | --- | --- | --- | --- |
| Mumei | starter (Unity's own row, 2026-11-23) | katana, common | (a) | listed |
| Kagetsu | shop 300 (was the starter) | katana, rare | (a) | listed |
| Tsubame | shop 450 | twin blades, rare | (a) | listed |
| Shizuka | shop 1100 | katana, epic | (a) | listed |
| Onikiri | shop 1400 | cleaver, epic | (a) | listed |
| Mizuchi | shop 1700 | spear, epic | (a) | listed |
| Yamigama | shop 2600 | chain sickle, legendary | (a) | listed |
| Iwakudaki | shop 3200 | hammer (its own style and smash string over the cleaver set), legendary | (a) | listed |
| Raikō no Tachi | Act I boss (Raikon) | heavy tachi | (b) withheld | removed: Raikon no longer grants it (owner, 2026-11-23) |
| Shinsen | boss weapon, "Defeat Akari" (later act) | katana | (b) | removed |
| Kagehira & Yūrei | boss weapon, "Defeat Akari" (later act) | twin blades | (b) | removed |
| Mugen no Ha | "Finish the Epilogue" | katana | (b) | removed |
| Bare Hands | what Kage fights with when no weapon is held | fists | fallback | never an item |
| 30 foe weapons (Shadow Daggers, Chain Hook, Raider Cleaver, Bokken, ...) | foes only | | (c) | never listed |

## Hairstyles

Spike, Messy Layers, Low Ponytail, Topknot (Samurai), Long Flowing and Undercut are available from the start. Selecting an ink silhouette saves the style and shows it on the live preview. Calm, Breeze and Gust adjust the preview wind. Every armor can be worn with every style. Hairstyles use the head rig in the gallery, profile and combat. Existing saves default to Spike.

## Armour

| Item | Where from | Drawing | Class | Armory |
| --- | --- | --- | --- | --- |
| Shinobi Garb (`tachibana_yoroi`) | starter | Kage's own coat | (a) | listed |
| Ronin, Yami, Shinsen, Raikō, Kagehira, Yūrei, Mugen Armor | shop 400 to 4200 (Mugen also 25 gems) | `ArmorLooks` | (a) | listed |
| Ikazuchi-dō | Act I, Garan | `ArmorLooksTrophies` | (a) | listed once owned, after the shop armours |
| Shiden Yoroi | Act I, Shiden | `ArmorLooksTrophies` | (a) | listed once owned, after the shop armours |
| Shirabe-dō | "Defeat Sayo" (later act) | drawn, kept | (b) | removed |
| Kasane Haori | "Defeat Kasane" (later act) | drawn, kept | (b) | removed |
| Tetsumon-dō | "Defeat Gonryu" (later act) | drawn, kept | (b) | removed |
| Kudamaki-dō | "Defeat Moro" (later act) | drawn, kept | (b) | removed |
| Mei-En Garb | "Defeat Akari" (later act) | drawn, kept | (b) | removed |
| Kysmei Robe | "Finish the Epilogue" | drawn, kept | (b) | removed |
| Kurokumo Yoroi | "Finish the Epilogue" | drawn, kept | (b) | removed |
| Shinsei Yoroi | "Finish the Epilogue in New Game+" | drawn, kept | (b) | removed |
| Kawa-dō, Hai Haori, Kasumi Shroud, Sōryo-e, Kozane-dō, Raiun Mantle, Shikkō Garb, Ō-sode Yoroi, Kurogane-dō, Gōka Yoroi | foes only, retired | | (c) | never listed |

## Counts

The current campaign offers eight weapons, six hairstyles and ten armors, including two reward armors.

## Withheld weapons

The owner removed four boss weapons for now (Shinsen is meant to come only after Act 2 or 3): Shinsen, Raikō no Tachi, Kagehira & Yūrei and Mugen no Ha. `GameRules.IsWithheld` names them. A stage reward never grants them, the Armory and the loadout's weapon row never list them, and `ArmoryListing.Classify` reports them as (b). The table rows, the weapon families and the foes' own copies of the blades (Raikon still fights with his tachi) are untouched. Delete an id from `WithheldWeapons` in `GameRules.Campaign.cs` to turn one back on.

## Turning an item back on

Give the item a drawing and a way to get it in a stage's `reward` (or the shop), and `ArmoryListing.Classify` reports it as (a): nothing in the Armory changes. The one explicit exception is `ArmoryListing.StandIns` (Iwakudaki, until a hammer family exists).
