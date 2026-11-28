# ADR-0011: The screenplay is canon, and story plays inside the stages

- Status: Accepted (2026-11-28, owner grilling session)
- Supersedes: spec story 81 ("a plain title card at the start of each stage instead of story scenes"), and the story and stage part of ADR-0010's `Campaign` export

## Context

The Unity port followed the JS game's Act I: four roads and four sub-bosses set on "rooftops", "districts" and "wasteland", with Raikon on the rooftops, and a title card in place of every story scene. The owner then wrote a screenplay for the Prologue and Act I (`KAGE MATERIALS/STORY/PROLOGUE& ACT 1.pdf`). It sets the act in different places: Hoshimura, the Outer Districts, the Ruined Military Archive, and a stone bridge in the rain before iron gates. It also has a full spoken story. The Story Bible and the JS data each disagree with it in places. For example, the bible has no sub-bosses, and its Raikon was betrayed by the Order rather than serving Takashi.

## Decision

- **The screenplay is canon** for the Prologue and Act I. Where the Story Bible or the JS stage data disagree with it, the screenplay wins. The bible only fills gaps, and the stage names, locations and clue placements are rewritten to match the screenplay.
- **The structure stays.** Act I keeps its 9 stages (4 roads, 4 sub-bosses, Raikon) and is re-themed as the screenplay's journey. Prologue: Hoshimura. Road 1 and Kuroe: the Outer Districts. Road 2 and Garan: Checkpoint Row. Road 3 and Hisame: the archive outskirts (the Burned Registry). Road 4 and Shiden: the Ruined Military Archive. Raikon: the Gate Bridge. The rooftops are gone. Stage names: The Ruined Village; The Outer Districts; Checkpoint Row; The Burned Registry; The Ruined Military Archive; Raikon — The Gate Bridge. Sub-boss stages show the sub-boss's own name and title (for example "Kuroe, the Ink Censor"), never "Sub-boss I". The chapters are titled "Prologue — The Man With No Past" and "Act I — The Missing Brother".
- **Story plays inside the stage.** A stage may open and close with a story scene. Every speaker Kage talks to stands in the stage as a character; the fighters are held still and the scene can be skipped. A scene plays only the first time a stage is cleared. The screenplay's video placeholders are captioned black cards until the videos exist.
- **Sub-bosses and the boss talk during the fight.** Fight lines appear as popups above the speaker and never pause the fight. Voice comes later.
- **Sub-bosses get story the screenplay doesn't give them.** Each has a conversation before and after its fight. Kage asks about Rem; the sub-boss reveals only the way to Raikon. They never give away what the screenplay saves for later: that Rem is alive, that it was a duel, what Takashi's evacuation order said, or anything about Akari and the City of Echoes.
- **Story and stage data are Unity's own.** The whole stage table moves into Unity-owned data, with today's values unchanged at the move: the stage list and order, names, kinds, locations, road lengths, encounters and waves, rewards, clues, and every line of dialogue. The JS export stops writing its `Campaign` table. Everything else in the export (weapons, gear, enemies, boss abilities, AI profiles) stays as ADR-0010 describes.
- **The Survivor guides Kage through Act I.** He waits at the start of each road with a guiding line and comments after each sub-boss. He says goodbye at the open gate after Raikon's last scene, because the City of Echoes is further than he can go.
- **Backgrounds stay painted in code**, as layered parallax silhouettes in the bible's flat style. Each location has its own look, and the colour moves across the act from violet (Echo) through slate (the Order) and gold (Takashi) to red (Raikon).

## Considered options

- Keep title cards only (spec story 81): rejected. The owner wants the screenplay's story told in the game.
- Cut Act I to the screenplay's three places: rejected. The four sub-bosses are already built and give the act its difficulty curve.
- Painted image backgrounds: deferred to a later art pass. Code-painted layers match how every character is built today.
