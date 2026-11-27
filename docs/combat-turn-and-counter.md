# Fighting a group: turning to the blow, and the counter hit

Code: `Assets/Kage/Scripts/Combat/Fighter.Counter.cs`. Numbers: `Resources/Tuning/parry.json` (`ParryTuning`) and
`engagement.json` (`warnBackAttacks`). Tests: `Assets/Kage/Tests/PlayMode/TurnAndCounterTests.cs`.

## Why

With two foes engaged (`EngagementTuning.maxAttackers`), one often stands behind Kage. The guard and the parry were
already all-round, but Kage kept facing the foe in front: a parry from behind left him with his back to the foe he
had just staggered, and his answer went to the wrong foe. Nor could the player turn round on purpose, since the
guard ignores left and right.

## Rules

1. **Turn to the blow.** A parry of a blow from behind turns the parrier round at once to face its attacker. A blocked
   blow from behind does too. In a group fight (more than one target) the attacker then stays the target (rule 3); in a
   1v1 the turn comes from the combat-flow rule `turnToBlowFromBehind` (`flow.json`, [combat-flow.md](combat-flow.md)),
   which also turns a fighter that reels from a blow from behind.
2. **Turn by hand.** With Guard held, a fresh press of Back (Back now, not held from before Guard) turns the fighter to
   the nearest standing foe behind it. With nobody behind, Back in the guard still does nothing.
3. **Focus.** The foe a fighter parried stays its target for `parryFocusFrames` (75); a foe it turned to for
   `turnFocusFrames` (45). `PickTarget` holds it over the nearest-in-front rule, so attacks, the riposte and facing all
   go to it. The focus drops early if that foe is down or knocked out.
4. **Counter hit.** A parried fighter is open to a counter hit from its parrier for its stagger plus
   `counterGraceFrames` (12). The parrier's first clean melee blow in that time is a counter hit:
   damage × `counterDamageMul` (1.5), `counterStunBonus` (10) more frames of hit-stun so the next blow links,
   clean-hit posture × `counterPostureMul` (2), `counterHitstopFrames` (4) more hit-stop, a "COUNTER" call-out, and a
   `CombatEventType.CounterHit` event right after the Hit (Damage = the Hit's). The first blow uses up the opening. Only
   Kage scores counter hits unless `foesCounterHit` is set.
5. **The warning.** When a foe behind Kage starts an attack in a group fight, a "!" flashes over it, so he knows to
   turn and parry.

All five are deterministic: no random numbers. Rules 1 to 4 only act after a parry, a block from behind, or Guard + Back.
The combat-flow pass ([combat-flow.md](combat-flow.md)) moves 1v1 baselines that contain a cross-up block or reel.
