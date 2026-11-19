# ADR-0004: Frame input with Guard, and the defence resolution order

- Status: Accepted (2026-11-17, SAN-111)
- Affects: SAN-86 (builds Guard), SAN-88, SAN-82, SAN-91, SAN-92, SAN-93, SAN-95, SAN-96, SAN-80, SAN-98, SAN-102

## Context

`FrameInput` had `dx, dy, p, k, s, x, tapLeft2, tapRight2, jump, dash, dashHeld`, and guarding was "hold back" (`_guardHeld = fwd < 0`) until SAN-86. The spec adds a Guard button, parry, posture and guard-break, perfect dodge and clash, plus kunai on Special + Down and ability slots for boss AI. Four defence tickets (SAN-86, SAN-91, SAN-92, SAN-93) can run close together and all touch how an incoming hit is resolved.

Today Special + Down with a full meter triggers Thousand Shadows (`TryBuffered`: `X && dy < 0 && canUltimate && meter >= 100`). SAN-88 puts the kunai on Special + Down too.

## Decision

### Buttons

| Button | Field | Action |
|---|---|---|
| Punch | `p` | as today |
| Kick | `k` | as today |
| Weapon | `s` | the weapon family's weapon moves |
| Special | `x` | the family's Special move (Crescent for the katana family), kunai, rage art |
| Guard | `guard`, `guardPressed` | new |
| Dash | `dash`, `dashHeld` | as today |
| Jump / Down | `jump`, `dy` | as today |

### New fields

```csharp
public struct FrameInput
{
    // ... existing fields unchanged ...
    public bool guard;        // Guard held at poll time, or pressed since the last poll (SAN-86)
    public bool guardPressed; // Guard went down since the last poll (SAN-86)
    public int ability;       // 0 = none; n = the n-th entry of the character's ability list (SAN-95, ADR-0008)
}
```

- `FrameInput.Any` stays `p || k || s || x`. Guard and abilities don't count as "any button" (kip-up mashing).
- `PlayerInputSource` latches `guardPressed` between physics steps, like the other buttons.
- The fighter treats a Guard press as `guardPressed || (guard && !guardLastFrame)`, so AI and scripted input only need to set `guard`.
- `ScriptedInput.Add(..., buttons: "G")` means Guard is held for the whole step, pressed on its first frame.
- `AIInputSource` guards by setting `guard` (and `dy = -1` for a low guard) instead of holding back (SAN-86).
- Player abilities: the player never sets `ability`.

### Key bindings

Bindings move into one table, `KeyBindings` (`Scripts/Combat/KeyBindings.cs`, created by SAN-86): action → keyboard keys and gamepad controls. `PlayerInputSource` reads it, and tutorial prompts read `KeyBindings.Label(GameAction action, InputDeviceKind device)` to show the bound key (SAN-96). SAN-86 picks the Guard key and button and documents them in the move list. A touch input source later implements `IInputSource` and fills the same fields.

### Guard rules

- Holding Guard on the ground, in a neutral state or in blockstun, puts the fighter in guard. While guarding, horizontal input is ignored. Guard + Down is a low guard.
- Walking backward no longer guards.
- The existing level rules apply unchanged (`Fighter.ReceiveHit` today): a Low hit needs a low guard, an Overhead needs a standing guard, High and Mid are blocked by either, and Unblockable is never guarded.
- Dash, roll, jump and attacks are allowed straight out of guard. An attack ends the guard.
- No guarding in the air (as today).

### Special + Down precedence

`Fighter.TrySpecialDown()` (in `Fighter.Specials.cs`) decides, in this order:

1. If the fighter can use a rage art (Kage), Rage is full (100) and the fighter is grounded: the weapon family's rage art (ADR-0006). Crimson Moon for the katana family, Thousand Shadows for every other family.
2. Else, if the fighter has kunai left: throw a kunai (SAN-88).
3. Else: nothing happens, and the press is consumed.

This keeps today's Thousand Shadows input, and matches the JS game, where Special triggers the rage art when Rage is full and throws a kunai otherwise. A plain Special (no Down) is the family's Special move, or nothing if the family has none.

### Defence resolution order

For one hit (a melee sweep, a projectile or a hazard) that reaches a defender, checks run in this order. The first one that resolves the hit wins.

| # | Check | Result | Where | Ticket |
|---|---|---|---|---|
| 1 | **Clash**: two blade sweeps from fighters of different teams cross in the same frame. Melee blade windows only. | no damage, both recoil, `Clashed` | `FightLoop.ResolveClashes`, before any hit | SAN-93 |
| 2 | **Perfect dodge**: the defender is the player, in a dash or roll started within the dodge window, and the hit would have landed. Checked before invulnerability. | no hit, `PerfectDodged` | `Fighter.PreHitCheck` | SAN-92 |
| 3 | **Not hittable**: invulnerable frames, thrown, KO, intro, and so on (`HittableBy`) | no contact, nothing published | `Fighter.Reaction` | as today |
| 4 | **Counter window** (`MoveDef.counterFrom..counterTo`; not Unblockable) | `Countered` | `Fighter.Specials` | as today |
| 5 | **Parry**: a Guard press within the parry window before this hit, not locked out, not Unblockable | attacker staggers and takes posture; `Parried` | `Fighter.ParryCheck` | SAN-91 |
| 6 | **Guard**: guard rules above | chip damage, posture += damage × factor; `Blocked`; a full posture bar then publishes `GuardBroken` | `Fighter.Guard` / `Fighter.Posture` | SAN-86 |
| 7 | **Hit**: armour check (move armour frames, Armour flag for light hits, frontal armour for hits from the front), then damage, a little posture, launch or hitstun, KO | `Hit` (and `KnockedOut`) | `Fighter.Reaction`, `Fighter.Armour` | as today; SAN-80, SAN-98, SAN-102 |

- A **guaranteed critical** after a guard-break is applied at step 7 on the defender's side: if the defender is guard-broken and the hit isn't already a critical, it becomes one (×1.5, rounded the same way as today). The attacker's crit roll doesn't change.
- **Projectiles** follow steps 2–7. They never clash. A projectile is blocked, not parried, as in the JS game; with `parryProjectiles` on in `parry.json`, a parried projectile is destroyed, and its owner neither staggers nor takes posture (SAN-91 notes).
- **Hazards** follow steps 2, 3 and 7. Their hits are Unblockable unless their data says otherwise.
- **Armour never stops damage or posture.** It only stops the flinch (hitstun, launch, knockdown).

### Hook points

So that SAN-91, SAN-92 and SAN-93 can run in parallel in their own files, the calls are declared in advance as C# partial methods (`void`, so a missing implementation compiles to nothing):

```csharp
public struct DefenceCheck
{
    public HitInfo info;      // the incoming hit, as resolved so far
    public bool resolved;     // set by a check that took the hit
    public HitResult result;  // the result to return when resolved
}

// Fighter
partial void PreHitCheck(ref DefenceCheck c);              // declared by SAN-65; implemented by SAN-92 (Fighter.PerfectDodge.cs)
partial void ParryCheck(ref DefenceCheck c);               // declared by SAN-86; implemented by SAN-91 (Fighter.Parry.cs)
partial void ArmourCheck(in HitInfo info, ref bool resistsFlinch); // declared by SAN-86; implemented in Fighter.Armour.cs

// FightLoop
partial void ResolveClashes(List<HitSweep> sweeps);        // declared by SAN-65; implemented by SAN-93 (FightLoop.Clash.cs)
```

- `HitResult` gets `Parried` and `Dodged`, both added by SAN-86 (unused until SAN-91 and SAN-92).
- `Fighter.Armour.cs` is created by whichever of SAN-80 (armour frames on a move) and SAN-102 (the Armour flag) lands first. SAN-98 adds frontal armour to the same method later. These tickets are in different waves.
- Every defence number (windows, lockout, posture factor, drain delay, guard-break time, slow-motion scale and length, meter gain, clash posture loss) is read from its own tuning file: `guard.json` (SAN-86), `parry.json` (SAN-91), `dodge.json` (SAN-92), `clash.json` (SAN-93). See ADR-0001.

## Consequences

- The defence layer is a fixed pipeline. Each new check sits in one place, and the order is the same for every attacker, projectile and hazard.
- Player and AI use the same Guard input, so the AI obeys the same rules (spec: "enemies are driven through the same input interface").
- Kunai can't be thrown while Rage is full, as in the JS game.
- Hook declarations land before their implementations. A ticket that finds a hook missing adds it in the file this ADR names.

## Notes from the implementation (SAN-80: the armour check)

- `Fighter.Armour.cs` implements `ArmourCheck`; the partial declaration is in `Fighter.Reaction.cs` beside `PreHitCheck` (SAN-86 had not landed, so SAN-80 declared it: SAN-86 must not declare it again). `Fighter.ReceiveHit` asks once, after the damage and the guard check: `bool resists = health > 0f && ResistsFlinch(info)`. A hit that takes the last health is never resisted, so armour never stops a KO.
- When a hit is resisted, the damage and the meter gain land and the result is `HitResult.Hit` (the attacker gets its hitstop, combo count and the `Hit` event as for any hit), but the fighter doesn't change state, takes no knockback, and its move carries on. A held throw victim stays held (the normal `ReleaseVictim(false)` on a hit is skipped). Heavy hits release it.
- The rule as built: `ArmourCheck` sets `resistsFlinch` when the window is **not** `heavy` and `MoveArmoured` (the fighter is in `FState.Attack` and its move's `Armoured(moveFrame)`). "Heavy" is the window's flag and nothing else: a light-flag window with `knockdown` or `launch` (a leg sweep) doesn't knock an armoured fighter down. A fighter in any other state, or outside the armour frames, flinches as before. The frames are inclusive and read the whole move frame, so a playback rate above or below 1 changes nothing about which frames are armoured.
- Extending it (SAN-102, SAN-98): add a line to `ArmourCheck`, e.g. `if (light && Has(CharacterFlags.Armour)) resistsFlinch = true;` for the boss-wide flag, and for frontal armour a test of `info.kb.x` against `facing`. `ResistsFlinch` is the single entry point and `ReceiveHit` needs no change.
- Throws (grabs) are not hits, so armour never stops a grab. The spear's Raised Guard (`Armour(5, 24)`) is honoured from now on.
- Tests: `ArmourTests` (the check at the `ReceiveHit` seam with a plain armoured move) and the swing, grip and guard in `FistsFamilyFightTests`.

## Notes from the implementation (SAN-86: the Guard button, posture and guard-break)

**Controls** (one table, `Scripts/Combat/KeyBindings.cs`; `KeyBindings.Label(GameAction, InputDeviceKind)` gives a prompt's text):

| Action | Keyboard | Gamepad |
|---|---|---|
| Move | A / D, ← / → | left stick, d-pad |
| Jump | W, ↑, Space | stick or d-pad up |
| Down (crouch, low guard) | S, ↓ | stick or d-pad down |
| Punch | J (or Z) | X / □ |
| Kick | K (or X) | A / ✕ |
| Weapon | L (or C) | Y / △ |
| Special | I (or V) | B / ○ |
| **Guard** | **O (or B)** | **RB / R1 (or RT / R2)** |
| Dash | Shift | LB / L1 (or LT / L2) |

The gamepad's RB used to be a second Special and RT a third Dash; both are Guard now. Guard sits next to the attack keys (O above L) so it can be held while the other hand moves; Down + Guard is S + O. The in-game move list (Tab, `Hud.BuildMoveList`) and the HUD hint show it.

**Input.** `FrameInput.guard` and `guardPressed` exist as decided. `PlayerInputSource` reads every button through `KeyBindings` and latches `guardPressed` between physics steps; `ScriptedInput` takes `"G"` (held for the step, pressed on its first frame); `AIInputSource` sets `guard` (and `dy = -1` for a low guard) where it used to hold back, with no horizontal input. `Fighter.ReadGuard` (Fighter.Guard.cs) turns the input into `GuardHeld` and `FramesSinceGuardPress` (0 on the press frame; the parry window reads it, SAN-91). `FrameInput.Any` still ignores Guard.

**The stance.** `Fighter.GuardStance`, called from `Neutral` after attacks, dash, roll and jump have had their chance (so all four come straight out of the guard): Guard held on the ground stands in `Idle` (or `Crouch` with Down), ignores horizontal input and slows to a stop. `IsGuarding` = Guard held, grounded, and `Idle`, `Walk`, `Crouch` or `Blockstun`, the same states as the hold-back guard. The stance pose is SAN-113's `PoseLib.Block` (blade held straight up) and `CrouchBlock`. No guarding in the air. `Fighter.GuardBlocks(level, low)` is the level rule, unchanged.

**Defence order as built** (`Fighter.ReceiveHitCore`): step 4 counter window; step 5 `TryParry` → `partial void ParryCheck(ref DefenceCheck c)` (declared in Fighter.Guard.cs; SAN-91 implements it in Fighter.Parry.cs and sets `resolved`, `result = HitResult.Parried`); step 6 `TryGuard`; step 7 `ApplyGuardBreakCrit`, damage, `CleanHitPosture`, armour, launch or hitstun, KO. `HitResult.Parried` and `HitResult.Dodged` exist; `PublishHit` maps them to the `Parried` and `PerfectDodged` events, so SAN-91 and SAN-92 only return them. `PreHitCheck` (step 2) and `FightLoop.ResolveClashes` (step 1) are unchanged from SAN-65.

**Posture** (Fighter.Posture.cs; every number in `Resources/Tuning/guard.json`, class `GuardTuning`):

| Number | Value | Meaning |
|---|---|---|
| `postureMax` (character data) | 60 (Kage, regular foes), 100 (Raikon) | the bar |
| `blockPostureFactor` | 1.5 | a guarded hit adds damage × 1.5 × `HitWindow.postureMul` |
| `hitPostureFactor` | 0.3 | a clean hit adds damage × 0.3 × postureMul, capped at the bar, never a break |
| `drainDelayFrames` | 60 | frames without a posture gain before it drains |
| `drainPerFrame` | 0.2 | 12 a second |
| `drainWhileGuarding` | false | holding Guard stops the drain (as the JS game), so turtling costs |
| `breakFrames` | 45 | 0.75 s helpless (hitstun, `PoseLib.GuardBroken`), × (1 − 0.5 stunRes) with the stunRes perk (SAN-84) |
| `breakPushKept` | 0.2 | a broken fighter keeps a fifth of the block's push-back, so the punish can reach |
| `postureAfterBreak` | 0.4 | the bar left after a break (JS refills to 60 of 100) |
| `chipFactor`, `blockstunFactor`, `minBlockstun` | 0.15, 0.6, 8 | the existing chip and blockstun numbers, moved to data |

A block also builds Rage: `Fighter.BlockRageGain(damage)` (SAN-84; JS damage × 0.4 × the Rage perk × (1 + blockRage)).

`damage` is the hit's damage after every multiplier (the `HitInfo.dmg` that reaches the defender). With 60 posture about two blocked strings break a regular guard (a jab blocked adds 6, an iai 13.5). The JS game's numbers are 100 posture, damage × 1.3 per block on JS damage (about 2.3 × Unity damage), 1 s delay, 20 a second; the Unity bar and drain keep that pace.

**Guard-break and the critical.** A full bar from a guarded hit (or from `AddPosture(amount, by, moveId)`, the public entry for a parry's or a clash's posture, SAN-91 / SAN-93) breaks the guard: the fighter goes into hitstun for `breakFrames`, `GuardBroken` is true for those frames, and the hit's result is still `Blocked`. The next hit to reach step 7 while `GuardBroken` becomes a critical if the attacker's roll didn't make it one: `dmg = max(1, round(dmg × 1.5))` on the damage that arrived (for an unmultiplied hit this equals the crit formula exactly), and the break is spent. The attacker's crit roll is still rolled (the "crit" stream is unchanged). The attacker learns of it through the defender's `LastHitCrit` / `LastHitDamage`, so the `Hit` event, hitstop and the `onHit` callback all show the critical.

**Events.** `Hit` and `Blocked` carry `Posture` (what the target's bar rose by). `GuardBroken` is published after the event of the hit that caused it, on the same frame: `Fighter.FlushGuardBreak` runs at the end of `OnHitResolved` for both the target and the attacker (a parried attacker breaks there too). A caller outside hit resolution (the clash) calls `FlushGuardBreak` after publishing its own event; any break still pending is flushed at the start of the fighter's next tick.

**Baseline replay re-recorded.** All 27 `_vs_ai` scenarios (and their legacy copies under `Baselines/Legacy`) changed; the 27 `_vs_bag` scenarios did not. Two causes: the posture field on `Hit` and `Blocked` lines, and the AI guarding in place instead of walking backward. A temporary patch restoring the old semantics on this code (walking back counts as Guard, no guard stance, the AI guarding by holding back) reproduced all 54 old files exactly apart from `pos=`, so the Guard input reproduces the old blocks and posture changes nothing else. With the real code every block the old AI made is still made (17 blocks before, 20 after, since the AI no longer backs out of range). The legacy copy was re-recorded once for this deliberate change; the note on `BaselineReplay.LegacyPathOf` says so.

**Tests.** `GuardPostureTests` (fight harness: the stance, the level rules at the `ReceiveHit` seam and in real fights, posture fill with `postureMul`, drain, no drain while guarding, guard-break timing, the guaranteed critical and its event order, the AI guarding through the Guard input, the HUD bars), `GuardInputTests` (keyboard O/B, S + O, gamepad RB/RT, LB/LT still dash, B still Special, the labels), `TuningTests.TheShippedGuardTuningIsTheSpecsDefenceNumbers`. Review sheet: `GuardScreenshots` → `Screenshots/review/SAN-86/`.


## Notes from the implementation (SAN-92: the perfect dodge)

**Where.** `Fighter.PerfectDodge.cs` implements `PreHitCheck` (step 2). It runs for every hit that found a contact on the defender's hurtboxes, before `HittableBy`: melee sweeps (`Fighter.ResolveContact`), projectiles and hazards (`ExternalHit.Deliver`). The tuning class is `DodgeTuning` (`Resources/Tuning/dodge.json`).

**How "would have landed" is detected.** The hit's sweep, cast or box touched one of Kage's hurtboxes this frame. That is the whole test of the hit: it does not ask whether he is invulnerable (a roll's and a back dash's own frames, the dash perk's), because the check sits before step 3. A move that passes over a ducking body never reaches the check (the Ronin's jab misses a forward dash's low pose; his kick reaches it, which is what the tests use).

**The window.** The defender is the player (`isPlayer`), in a dash (`FState.Dash`: ground or air, forward or back) or a dodge move (`moves.roll`, `moves.backflip`), and his `DodgeAge` (frames since it started, 0 on its first frame: the state's `stateFrame`, in his own frames, so hitstop doesn't count) is at most `PerfectDodgeWindow` = `windowFrames` (8) + `round(DashBonus × dashBonusWindowFrames)` (30, the same rate as SAN-84's dash invulnerability: a 0.1 dash perk gives 11). The boundary is inclusive: a dash started 8 frames before the hit perfect-dodges it, 9 frames before doesn't (measured with the Ronin's kick).

**What it does.** The check resolves the hit with `HitResult.Dodged`: no damage, no posture, no hitstop. The attacker logs the window (a projectile logs the target and flies on through) and publishes `PerfectDodged` (ADR-0003). Kage gains `meterGain` (12, the JS game's) × his Rage perk, the world slows to `worldScale` (0.4) for `slowFrames` (24, 0.4 s) with Kage exempt (ADR-0002, SAN-92 notes), and the `VFX_Kage_PerfectDodge` cue plays: `afterimages` (3) blue snapshots of Kage trailing back along the dodge (`afterimageSpacing` 0.18 m apart, `afterimageLife` 0.45 s), a blue screen tint (`GameFeel.Flash`) and an "EVADE" call-out. `Vfx.Ghost` gained an optional offset for the trail.

**Once per dodge.** Each `SetState` counts a state entry (`Fighter._stateEntries`, one line in `Fighter.cs`); a perfect dodge marks the entry it fired in. A second hit in the same dash or roll goes through the normal order (its invulnerability, then the hit); the next dash or roll can perfect-dodge again. So Rage is gained once per perfect dodge, and one `PerfectDodged` is published per dodge.

**Normal dodge.** Outside the window nothing changes: a roll, backflip or back dash keeps its own invulnerable frames, a forward dash is hittable (unless the dash perk makes its first frames untouchable).

**Player only.** A foe's dash or roll never resolves step 2: the check returns at once unless `isPlayer`.

**Numbers** (`dodge.json`; the JS game's were 0.3 world speed for 0.75 s, +12 Rage, 3 afterimages; the spec asks for about 8 frames and about 0.4 s, the glossary for 0.4):

| Number | Value | Meaning |
|---|---|---|
| `windowFrames` | 8 | frames from the dash's or roll's start in which a landing hit is perfect-dodged |
| `dashBonusWindowFrames` | 30 | extra frames per 1.0 of the dash perk (`round(DashBonus × 30)`) |
| `worldScale` | 0.4 | the world's speed during the slow motion |
| `slowFrames` | 24 | 0.4 s of Kage's frames |
| `meterGain` | 12 | Rage gained, × the Rage perk |
| `afterimages`, `afterimageLife`, `afterimageSpacing`, `afterimageTint` | 3, 0.45 s, 0.18 m, light blue | the afterimage trail |
| `screenTint`, `screenTintSeconds` | blue at 0.3, 0.4 s | the screen tint |

**Baseline and the legacy switch.** No baseline scenario perfect-dodges, so the baseline replay is unchanged. Under the legacy switch (hits resolved inside the attacker's tick, `Fighter.TryTarget`) step 2 never runs, as before SAN-65: the legacy path has no perfect dodge, and its baseline copy is unchanged too. The world time scale itself works on either path.

**Tests.** `PerfectDodgeTests` (fight harness: dash inside and outside the window, roll and backflip on both sides of the window's last frame, standing, walking and guarding never dodge, the dash perk's wider window, the meter gained once, one per dash and again on the next dash, the event's zero damage after an earlier hit, foes never dodge, the world slowed for its frames but not Kage, slowed walking, jumping and freezing, projectiles and hazards at their owner's scale, determinism of a fight with a perfect dodge), `WorldTimeScaleTests` (EditMode: the clock's counting), `TuningTests.TheShippedDodgeTuningIsTheSpecsPerfectDodgeNumbers`. Review sheet: `PerfectDodgeScreenshots` → `Screenshots/review/SAN-92/`.


## Notes from the implementation (SAN-91: the parry)

**Where.** `Fighter.Parry.cs` implements `ParryCheck` (step 5). The tuning class is `ParryTuning` (`Resources/Tuning/parry.json`). Small shared edits: `ReadGuard` calls `ParryTick(press)` (one line), `ResetPosture` calls `ResetParry`, `HitInfo` gained `origin` (`HitOrigin.Melee`, `Projectile`, `Hazard`; set by `Projectile.Deliver` and `Hazard.Strike`), `PublishHit` reads the parry's posture, `CharacterDef.parryRate` and two members on `AIInputSource`.

**How the window is measured.** A Guard press (`guardPressed`, or `guard` going from released to held) opens a parry window. A hit parries when the defender `IsGuarding` and the press is at most `windowFrames` (8) of the defender's own frames old, counting the press frame as 0 (`FramesSinceGuardPress`; the loop polls and ticks every fighter before it resolves hits, so a press and a hit on the same loop frame are 0 frames apart). The edge is inclusive and measured: a press 8 frames before the hit parries it, 9 frames before only blocks it (`ParryTests.The_window_edge_is_inclusive...`, with the Ronin's kick and its landing frame measured with Kage standing still). The JS game's window, `blockT < 0.15 s`, is the same frames 0 to 8. Hit-stop doesn't count (a fighter doesn't tick in it); a slowed fighter counts its own frames.

**Lockout.** A press opens a window only when parrying isn't locked out, and locks it for `lockoutFrames` (20, 0.33 s) from that press. A press inside the lockout still guards, but opens no window and doesn't end the open one. A parry spends the window (one press parries one hit) and does not clear the lockout, so mashing Guard gives at most one window in 20 frames and can't chain parries. A press that parries nothing (too early or too late) costs the same lockout: that is the "mistimed press". Tests: `A_mistimed_press_locks_out...`, `The_lockout_ends_lockoutFrames_after_the_press...` (a press on frame 20 parries, on frame 19 doesn't), `Mashing_Guard_cannot_chain_parries`, `One_press_parries_one_hit`.

**What parries.** A melee window (and the Thousand Shadows cut, which is Unblockable anyway) that isn't `Unblockable`, on a fighter who `IsGuarding` (held, grounded, neutral or blockstun; not in the air, not in hit-stun or a guard-break). The hit's level doesn't matter (the JS game has none): a standing guard parries a low sweep and a low guard an overhead. The counter window (step 4) still runs first. Hazards are never parried. Unblockable hits are never parried (`An_unblockable_hit_is_never_parried_even_with_perfect_timing`).

**Defender.** No damage, no posture, no blockstun: it keeps its guard stance and can attack at once. `HitResult.Parried`; `LastDamageTaken` and `LastPostureTaken` are 0. It gains `meterGain` (14, JS) Rage × the Rage perk, takes a hit-stop (`playerHitstopFrames` 12 when Kage parries, `foeHitstopFrames` 8 when a foe does; JS 0.2 s and 0.13 s), a shake and the `VFX_Kage_Parry` cue (ADR-0007's reserved id): a gold screen flash, a star, two rings, sparks flying back at the attacker, a "PARRY" call-out. The JS game's slow motion and "PERFECT PARRY" stamp on a player parry, and its 0.75 s iai counter window after a parry, are not built: the counter-slash out of a parry is the weapon family's business (Mirror Guard).

**Attacker.** `Fighter.GetParried`: it goes into hit-stun for the role's stagger frames, cut by the stunRes perk with `ScaleStun(frames, stunResistFactor)`, is pushed back at `pushback` (2.4 m/s, JS 260 px/s), and takes the role's share of its own posture bar through `AddPosture`, which can break its guard (the break's longer helplessness replaces the stagger, and `GuardBroken` is published right after the `Parried` event, as SAN-86 arranged). The role is the JS enemy type of the character's id (`regular`, `sub`, `boss`; a character with no JS entry, such as the Ronin, is regular), or `player` for Kage (the JS "deflect").

| Number (`parry.json`) | Value | Meaning |
|---|---|---|
| `windowFrames` | 8 | the window, frames; inclusive |
| `lockoutFrames` | 20 | from the press (the spec's 0.33 s; the JS game has none) |
| `regular/sub/boss/playerStaggerFrames` | 57, 37, 30, 28 | JS 0.95, 0.62, 0.5, 0.46 s |
| `regular/sub/boss/playerPostureShare` | 0.55, 0.34, 0.26, 0.30 | JS 55, 34, 26, 30 of a 100 bar; here a share of the fighter's own bar, so a regular foe (60) loses 33 and the second parry breaks it |
| `stunResistFactor` | 0.5 | stagger × (1 − stunRes × 0.5), as the JS `parried`; see below |
| `pushback` | 2.4 | m/s |
| `meterGain` | 14 | Rage, × the Rage perk |
| `playerHitstopFrames`, `foeHitstopFrames`, `shake` | 12, 8, 0.35 | feel |
| `flashTint`, `flashSeconds`, `sparkTint`, `sparks` | gold, 0.18 s, gold, 18 | the placeholder cue |
| `parryProjectiles` | false | see below |

The spec says a stagger of "about 0.5 s": the sub-boss (0.62), boss (0.5) and Kage (0.46) are near it; a regular foe reels the JS game's 0.95 s. Every number is data, so the owner can pull the regular foe to 0.5 s with one edit.

**The stunRes perk.** The ticket pointed at `ScaleStun(frames, HardStunResist)`. The JS game cuts a parried fighter's stun by `1 − stunRes × 0.5` (`Budo.parried`), and ADR-0005's perk table lists "guard-break, parry and deflect stun × (1 − 0.5x)", so the parry goes through `ScaleStun(frames, ParryTuning.stunResistFactor)` with the factor 0.5 (the value of `Fighter.BreakStunResist`), in data. Setting `stunResistFactor` to 1 gives the hard-stun curve (`HardStunResist`) if the owner prefers it. Test: `The_stunRes_perk_shortens_the_stagger_of_a_parried_Kage_through_ScaleStun`.

**Projectiles.** Decision, following the JS game: a projectile is blocked, never parried (`!m.proj` in `Combat.applyHit`). The decision section above and ADR-0007 said a parried projectile is destroyed and its owner is left alone; that is kept as a switch: with `parryProjectiles` true a guard press inside the window parries a projectile (`HitOrigin.Projectile`), the projectile is destroyed (`Projectile.Deliver` ends on `Parried` as on `Countered`), and the owner is neither staggered nor loses posture. The shipped data has it off. Tests: `A_projectile_is_blocked_not_parried...`, `With_parryProjectiles_on_a_parried_projectile_leaves_its_owner_alone`.

**The AI.** Decision, following the JS game (its AI profiles have a `parry` chance, and it raises the guard when the blow is under 8 frames from landing): AI foes can parry, gated by data. `CharacterDef.parryRate` (0..1, default 0 = never) is the share of the attacks the AI chooses to guard that it tries to parry; `AIInputSource.Configure` takes it from the character. A parry attempt holds the Guard press back until the blow is half a window (4 frames) from landing, then holds it as a normal guard does. A foe parries only while its AI is making an attempt (`AIInputSource.ParryAttempt`), so with a rate of 0 even a guard that happens to go up inside the window is only a block. The rate costs one random draw per guard decision only when it is above 0, so every existing fight, and the baseline replay, is unchanged at 0. SAN-95 should set the rate per profile from the JS `parry` column (regular 0.06, agile 0.05, defensive 0.16, ranged 0.04, heavy 0, boss 0.12) and per role. Not ported: the JS "predictable move" bonus, adapting to repeated moves, and Kage's recoil ending early on a fresh Guard press. A scripted foe (`ScriptedInput`) may parry whenever its script presses Guard in time; that is what the tests use. Tests: `The_AI_never_parries_with_a_parry_rate_of_zero...`, `An_AI_foe_with_a_parry_rate_of_one...`.

**Counted for the rating.** The event is `Parried` with Source = the attacker, Target = the parrier, Damage 0, Crit false, Posture = the posture the attacker lost (ADR-0003 already said so). `FightStatsRecorder` counts `Parried` where the Target is the player (it did already, from SAN-73), so a foe's parry of Kage isn't counted. Tests: `A_parry_is_published_once_and_counted_for_the_fight_rating` (real fight: the event and `Result().parries`), `A_foe_parrying_Kage_is_not_counted_for_the_rating`.

**Existing tests changed.** The guard tests that pressed Guard and took a hit within 8 frames (`GuardPostureTests`, `SwordBreakTests`) now hold Guard past the window first (`PastParry`), since pressing and being hit at once is exactly a parry now. The AI-guard test counts a block or a parry, because its input wrapper hides the AI from the fighter.

**Baseline replay.** Unchanged: no baseline scenario presses Guard, and the baseline AI has `parryRate` 0, so no parry occurs and the replay files match without re-recording.

**Tests.** `ParryTests` (fight harness and the `ReceiveHit` seam: the window edge, held guard, no guard, the defender, the attacker's stagger, push, posture and roles, guard-break order, the stunRes perk, the lockout, mashing, one press one hit, unblockable, projectiles and hazards, the event, the rating, the cue and hit-stop, the AI), `TuningTests.TheShippedParryTuningIsTheSpecsParryNumbers`. Review sheet: `ParryScreenshots` → `Screenshots/review/SAN-91/`.

## Notes from the implementation (SAN-93: the weapon clash)

**Where.** `FightLoop.Clash.cs` implements `partial void ResolveClashes(List<HitSweep>)` (phase 4, step 1 of the defence order); the geometry is `Combat/ClashGeometry.cs` (pure math), the fighter's side `Fighter.Clash.cs` (`ClashRecoil`), the numbers `ClashTuning` / `Resources/Tuning/clash.json`, the placeholder look `VFX/ClashCues.cs` (`VFX_Kage_Clash`).

**What is a blade.** A run of consecutive `HitPoint.Blade` or `HitPoint.BladeN` samples of one owner's one window (`ClashGeometry.CollectBlades`). A twin blade window gives two runs; the chain lash is part of the Blade line already (ADR-0006), so it is just a longer run. Fist, hilt, foot, shin and knee samples never make a run, so a blade passes through a fist or a foot and the hit phase treats it as an ordinary hit. A window that mixes (a fist and a blade) clashes with its blade only. Projectiles and hazards are not sweeps, so they never clash.

**When two blades cross.** Each blade swept a strip this frame: its samples ran from where the window last saw them (`HitSweep.from`) to where they are now (`to`). The strip's outline is three sets of segments: the `to` points joined, each sample's own path, the `from` points joined (on a window's first active frame `from == to`, so only the line). Two blades cross when any segment of one meets any segment of the other (`ClashGeometry.SegmentsCross`, ends included; a collinear overlap counts, which is two spears thrust at each other). That catches a swing through a still blade, two crossing swings and a head-on thrust. It is pure float math on the recorded sweeps, so it is deterministic. The clash ignores the window radius (blades are lines) and the base-to-first-sample gap (about 14 cm at the guard). Two blades that pass without their lines or paths meeting are not a clash, even if both hit: a 1.0 m iai against an iai dashes through the other body, and the cuts land as a trade (the first fighter in fight order wins, as before).

**Who clashes.** Two live fighters of different teams (`Fighter.team`), both still in the attack that recorded the blade (`state == Attack && move == run.move`). Pairs are tried in sweep order (fighter order, then window order); a clash ends both moves, so a fighter clashes at most once a frame and a third fighter's blade then meets nobody. Several foes (SAN-83) therefore clash pair by pair in fighter order. Under the world time scale (SAN-92) a fighter the slowed world doesn't advance this frame records no sweeps, so it can't clash on that frame; under the legacy switch (`ctx.LegacyHits`) the loop never calls `ResolveClashes`, so there are no clashes.

**What a clash does.**

| Number (`clash.json`) | Value | Meaning |
|---|---|---|
| `posture` | 8 | posture each fighter gains (of 60; Raikon 100), through `AddPosture(amount, by, moveId)`; a full bar breaks the guard |
| `recoilFrames` | 13 | hitstun after the clash, the move is over (JS 0.22 s) |
| `recoilSpeed` | 3.6 | m/s each is thrown away from the other, through `KnockVelocity` (mass and knockback taken count; JS 420 px/s) |
| `hitstopFrames` | 8 | the whole fight freezes (JS 0.13 s) |
| `shake`, `punch` | 0.35, 0.05 | `GameFeel.Shake` and `GameFeel.Punch` |
| `sparksPerSide`, `sparkSpeedMin/Max`, `sparkLife`, `sparkSize`, `flareSize`, `ringRadius`, `sparkTint`, `soundVolume` | 12, 3 to 9 m/s, 0.35 s, 0.06 m, 0.6 m, 0.55 m, gold #ffe2a8, 0.9 | the placeholder cue |

Neither takes damage: both are out of their moves, and the hit phase drops the sweeps of a move that is over, so neither blade hits (nor does either fighter's other windows). The recoil ignores armour (armour is for hits) and the stun-resist perk, so both fighters recoil alike. Order inside `Clash`: recoil, posture (`AddPosture`, so a break replaces the recoil's stun with the break's), the `Clashed` event, then `FlushGuardBreak` for both (so `GuardBroken` follows `Clashed` on the same frame), hit-stop, shake, the `VFX_Kage_Clash` cue at the crossing point. The sparks and rings count unscaled time, as the other hit effects, so they don't freeze in the hit-stop.

**Event.** One `Clashed` per pair (ADR-0003 notes): Source is the earlier fighter, Target the other, `Name` the target's move.

**Baseline.** No baseline scenario has two blade windows crossing, so the baseline replay is unchanged (the full PlayMode suite, including `BaselineReplayTests` and the legacy copies, passes on the unchanged files).

**Tests.** `ClashGeometryTests` (EditMode: segment cases, still blades, a blade swung through a still one, near miss, heights, order independence, which samples are blades, twin windows, mixed windows) and `WeaponClashTests` (fight harness: katana against katana and low reap against low reap, no hit and no damage, the event, recoil and its length, posture, hit-stop length, the cue, a guard-break after the clash event, a near miss, out of reach, blade against fist, kick, fists family, same team, spear against spear, the chain lash beyond the blade's reach, the near twin blade alone clashing against a spear, a clash replaying identically). The scenarios' distances and button delays were measured on the move sets as they are; a retimed move needs its scenario re-measured. Review image: `Screenshots/review/SAN-93/clash_sheet.png` (from `WeaponClashScreenshots`, run without `-nographics`).


## Notes from the implementation (SAN-95: ability slots)

**The field.** `FrameInput.ability` (int, 0 = none) exists as decided here: `n` asks for the n-th entry of `CharacterDef.abilities`, **1-based** (slot 1 is the first entry; the list's order is the slot order, ADR-0008). `ScriptedInput.Add(..., ability: n)` presses it on the step's first frame, like a button. `PlayerInputSource` never sets it. `FrameInput.Any` ignores it (kip-up mashing).

**Where it is read.** `Fighter.Abilities.cs` (new). `Fighter.Neutral` calls `TryAbility(inp)` right after `TryBuffered`, so an ability starts from the same states an attack button does (idle, walking, running, crouching, guarding; not in an attack, a dash, the air, a hit reaction or a fall), and a fighter that is busy simply ignores the request: it is not buffered. `HoldBackIfWaiting` clears `ability` along with the attack buttons, so a foe that is waiting its turn in a group fight (SAN-83) can't start one.

**What makes a request count** (`Fighter.AbilityReady(slot)`, public so an AI can ask first): the slot is in range of the list; the ability is **enabled** (`Fighter.SetEnabledAbilities(ids)`, the call a boss phase makes, null = all; ADR-0008); it is off **cooldown**; it has a move; the fighter has the Shadow the move costs. A disabled, cooling, unknown or move-less request is dropped silently, as ADR-0008 says. The cooldown starts on the frame the move starts and runs on the fight clock (`AbilityDef.cooldownFrames`, kept per fighter in `_abilityReady`).

**Which move.** `Fighter.AbilityMove(id)`: the `AbilityDef.move` written in a character file (`AbilityCatalog`, `Combat/AbilityDef.cs`), else the family's `MoveSet.Ability(def.moveId ?? id)`, else `MoveSet.Ability("<characterId>.<id>")`, so both a JS id (`thunderSwing`) and ADR-0006's full id (`raikon.thunderSwing`) find the tachi's `AddAbility` move. The move then starts with `StartMove` and is an ordinary move: hit windows, guard, the defence order and the events (`MoveStarted` carries the move's id) are the same as for any other. There is no second combat path.

**Who presses it.** A profiled `AIInputSource` (ADR-0005, SAN-95) chooses abilities at its decision points like the JS `tryAbility`: of the abilities it knows, that are ready and whose distance band `AbilityDef.range` holds, it picks one at random and rolls `AbilityDef.chance` (× `AiTuning.aggressiveAbilityMul` in an aggressive phase), then presses the slot for a frame. The legacy profile (the Ronin) has no ability slots. `AbilityDef` has the ADR-0008 fields minus the projectile and hazard (SAN-102 and the boss tickets add those) plus `move`.

**Tests.** `ProfileAiAbilityTests` (fight harness, with a test ability registered in `AbilityCatalog`): the slot maps to the move, slot 1 with no move and slots 0, 3 and 99 do nothing, the cooldown (pressed again 61 frames in is ignored, ready again at 120), a disabled ability, a busy fighter, the player side, an AI choosing it in range and off cooldown, never inside the band or at chance 0, and the legacy AI never pressing one.
