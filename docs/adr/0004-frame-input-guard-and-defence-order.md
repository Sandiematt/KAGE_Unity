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
- **Projectiles** follow steps 2–7. They never clash. A parried projectile is destroyed, and its owner neither staggers nor takes posture.
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
| `breakFrames` | 45 | 0.75 s helpless (hitstun, `PoseLib.GuardBroken`) |
| `breakPushKept` | 0.2 | a broken fighter keeps a fifth of the block's push-back, so the punish can reach |
| `postureAfterBreak` | 0.4 | the bar left after a break (JS refills to 60 of 100) |
| `chipFactor`, `blockstunFactor`, `minBlockstun` | 0.15, 0.6, 8 | the existing chip and blockstun numbers, moved to data |

`damage` is the hit's damage after every multiplier (the `HitInfo.dmg` that reaches the defender). With 60 posture about two blocked strings break a regular guard (a jab blocked adds 6, an iai 13.5). The JS game's numbers are 100 posture, damage × 1.3 per block on JS damage (about 2.3 × Unity damage), 1 s delay, 20 a second; the Unity bar and drain keep that pace.

**Guard-break and the critical.** A full bar from a guarded hit (or from `AddPosture(amount, by, moveId)`, the public entry for a parry's or a clash's posture, SAN-91 / SAN-93) breaks the guard: the fighter goes into hitstun for `breakFrames`, `GuardBroken` is true for those frames, and the hit's result is still `Blocked`. The next hit to reach step 7 while `GuardBroken` becomes a critical if the attacker's roll didn't make it one: `dmg = max(1, round(dmg × 1.5))` on the damage that arrived (for an unmultiplied hit this equals the crit formula exactly), and the break is spent. The attacker's crit roll is still rolled (the "crit" stream is unchanged). The attacker learns of it through the defender's `LastHitCrit` / `LastHitDamage`, so the `Hit` event, hitstop and the `onHit` callback all show the critical.

**Events.** `Hit` and `Blocked` carry `Posture` (what the target's bar rose by). `GuardBroken` is published after the event of the hit that caused it, on the same frame: `Fighter.FlushGuardBreak` runs at the end of `OnHitResolved` for both the target and the attacker (a parried attacker breaks there too). A caller outside hit resolution (the clash) calls `FlushGuardBreak` after publishing its own event; any break still pending is flushed at the start of the fighter's next tick.

**Baseline replay re-recorded.** All 27 `_vs_ai` scenarios (and their legacy copies under `Baselines/Legacy`) changed; the 27 `_vs_bag` scenarios did not. Two causes: the posture field on `Hit` and `Blocked` lines, and the AI guarding in place instead of walking backward. A temporary patch restoring the old semantics on this code (walking back counts as Guard, no guard stance, the AI guarding by holding back) reproduced all 54 old files exactly apart from `pos=`, so the Guard input reproduces the old blocks and posture changes nothing else. With the real code every block the old AI made is still made (17 blocks before, 20 after, since the AI no longer backs out of range). The legacy copy was re-recorded once for this deliberate change; the note on `BaselineReplay.LegacyPathOf` says so.

**Tests.** `GuardPostureTests` (fight harness: the stance, the level rules at the `ReceiveHit` seam and in real fights, posture fill with `postureMul`, drain, no drain while guarding, guard-break timing, the guaranteed critical and its event order, the AI guarding through the Guard input, the HUD bars), `GuardInputTests` (keyboard O/B, S + O, gamepad RB/RT, LB/LT still dash, B still Special, the labels), `TuningTests.TheShippedGuardTuningIsTheSpecsDefenceNumbers`. Review sheet: `GuardScreenshots` → `Screenshots/review/SAN-86/`.
