# Combat presentation in the Dev Arena

## Play through the changes

Press **F2** to switch between Training and Duel. Training gives Kage a punching bag. Duel gives him a fighter who can attack, guard, clash and reel. **F5** cycles foes, **F8** cycles weapons, **F4** toggles passive AI and **F1** opens the move list. The inputs below use the default keyboard bindings.

| Moment | How to trigger it | What to look or listen for |
| --- | --- | --- |
| Light hit, launcher and combo | Equip Kagetsu. In Training, compare a jab (**J**) with the **L, L, L** weapon chain. Try **Back + J** for Poised Fang. | The cut follows the striking part. Light contacts have a short, single ring. Rising Moon and the heavy finish have a longer slash, two rings, a hot core and more ink. Watch the blade trail brighten at contact and the kick follow the cut. |
| Hitstop vibration | In Duel, use **F4** to make the foe passive and land a heavy blow. | The struck fighter's artwork vibrates along the impact, then settles onto its skeleton. |
| Normal Parry and Perfect Parry | Use active AI in Duel. Press **Guard (O)** four to eight frames before melee contact, then try within three frames. | Both show steel meeting at the solved crossing. Perfect Parry adds larger crossed streaks, gold and white rings, a brief dark backdrop, a stronger zoom and a higher harmonic chime. A foe's parry keeps its crimson warning. |
| Counter hit | Parry a blow and immediately answer with **J** while the foe reels. | The COUNTER callout, crimson-gold accent, directional kick and low thump coincide with the answer landing. |
| Iai Counter | With Kagetsu or Shizuka, press neutral **Weapon (L)** immediately after a block or parry. | The backdrop drops away at the draw. The blade glints and the IAI COUNTER callout appears. |
| Guard-break | Use **F5** to select the Shieldbearer. Pressure its raised guard with connected weapon hits until its posture fills. The effect also plays when Kage's guard breaks. | The paintings hold at contact for nine sixtieths of a second. Shards radiate from the break, the camera kicks and a sharp crack sits over a low thump. Follow up for the existing guaranteed critical. |
| Perfect dodge | Press **Dash (Shift)** into an incoming blow just before contact. Also try a double-tap back step. | A short air-slice marks the dodge. Blue afterimages snapshot Kage at successive positions as he travels. The indigo vignette and colour split lift when the slowed world resumes. |
| Weapon clash | Use Kagetsu against a blade-wielding foe. Against the Ronin, start neutral **L** as his Iai Draw begins, about 1.8 m apart. | The blades cross, both paintings tremble, two spark fans open and a metallic ring hangs over the recoil. |
| Whiff punish | Let the foe's cut fall short. Step in and hit it during recovery. | The PUNISH callout has a cyan-white slash cross, a short zoom and a high confirm tone. |
| Recovery cancel | Land a grounded jab, then press **Shift** during recovery. Repeat with **O** held after a clean hit. Try a missed jab as a control. | A successful cancel leaves dust at the feet and one short shadow of the outgoing pose. The whiff recovers in full. |
| KO | Finish a Duel with a heavy blow. | The camera kicks along the launch impulse. The brief dark backdrop separates the KO star and rings from the arena. |

## Files in this pass

Paths in this table are relative to `Assets/Kage/Scripts/` unless otherwise indicated.

| File | Change |
| --- | --- |
| `Combat/Fighter.HitDetection.cs` | Reads confirmed hit direction and weight, selects the struck fighter's vibration, flares the connecting blade and plays impact audio. |
| `Character/FighterRig.cs` | Offsets mesh leaves and can temporarily hold their painted pose in world space. Restores local transforms before the next pose. |
| `Character/ScarfRibbon.cs` | Includes the visual offset when drawing scarf vertices and releases scarf meshes owned by temporary afterimages. |
| `Combat/Fighter.Pose.cs` | Applies visual offsets after posing, samples moving dodge ghosts and enables tip embers around heavy, critical, launcher and rage-art swings. |
| `VFX/Vfx.cs` | Draws directional hit bursts, graded shock rings, crossed steel, radial posture shards and spline trails with an ink rim. Owns transient impact meshes. |
| `VFX/SubBoss/CombatLook.cs` | Exposes a short contact flare on additive trail layers. The generic trail now shares the sub-bosses' stroke-separated Catmull–Rom sampler. |
| `Core/GameFeel.cs` | Adds a damped directional kick, unscaled bloom and vignette pulses, a capped impact frame and shared post-processing for both fight hosts. |
| `Stage/CameraRig.cs` | Adds the kick to the final camera position after computing normal framing. |
| `VFX/Vfx.RageArts.cs` | Briefly uses the existing backdrop layer for an impact tint and restores the current rage-art grade. |
| `Core/KageGame.cs` | Connects the Dev Arena audio service and updates its post-processing. |
| `UI/Flow/StageWorld.cs` | Connects the campaign fight audio service and the same post-processing. |
| `Fight/FightContext.cs` | Carries the live fight's audio service. |
| `Fight/FightBuilder.cs` | Assigns the fight context before fighter setup so the rig can skip trail construction in a manual-clock fight. |
| `Combat/Fighter.cs` | Builds trails only for live play, exposes the presentation check and clears outstanding painting and dodge effects on round reset. |
| `VFX/EffectCues.cs` | Preserves cue observation by tests and dispatches renderers only in live play. |
| `Combat/Fighter.Parry.cs` | Distinguishes the player's Perfect Parry and vibrates the two fighters at their solved crossing. |
| `Combat/Fighter.Posture.cs` | Adds the nine-frame visual hold, posture shatter, camera kick and layered break audio. The fight clock continues on its existing schedule. |
| `Combat/Fighter.PerfectDodge.cs` | Takes a bounded number of moving pose snapshots and gates the dodge grade to the exempt fighter's slowed-world window. |
| `Fight/FightLoop.Clash.cs` | Sends the two actual clash participants opposing visual impulses. |
| `VFX/ClashCues.cs` | Draws the steel cross, spreads two spark fans and plays the dedicated clash ring with a zoom and impact frame. |
| `Combat/Fighter.Counter.cs` | Adds the crimson-gold counter accent, impact frame and low audio layer. |
| `Combat/Fighter.Flow.cs` | Adds whiff-punish confirmation and a single ghost with foot dust on a successful recovery cancel. |
| `Combat/Fighter.Style.cs` | Adds the Iai Counter draw frame and uses the cancel cue for the twin blades' own dash cancel. |
| `Combat/Fighter.Reaction.cs` | Kicks along the KO impulse and adds the KO impact frame. |
| `Core/KageAudio.cs` | Synthesizes perfect-parry harmonics, shatter, air-slice, clash ring and punish confirm. Releases generated clips when a fight closes. |
| `UI/HudCallouts.cs` | Smooths the callout's fade so the previous word clears as a combo continues. |
| `Assets/Kage/Tests/PlayMode/DevArenaTests.cs` | Exercises a guard-break in the live arena and checks that hurtboxes travel while the painting holds, then rejoins its skeleton. |
| `docs/combat-presentation.md` | Dev Arena controls, observations and file-by-file handoff. |
