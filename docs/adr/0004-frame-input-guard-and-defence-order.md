# ADR-0004: Frame input with Guard, and the defence resolution order

- Status: Accepted (2026-11-17, SAN-111)
- Affects: SAN-86 (builds Guard), SAN-88, SAN-82, SAN-91, SAN-92, SAN-93, SAN-95, SAN-96, SAN-80, SAN-98, SAN-102

## Context

`FrameInput` has `dx, dy, p, k, s, x, tapLeft2, tapRight2, jump, dash, dashHeld`. Guarding is "hold back" (`_guardHeld = fwd < 0`). The spec adds a Guard button, parry, posture and guard-break, perfect dodge and clash, plus kunai on Special + Down and ability slots for boss AI. Four defence tickets (SAN-86, SAN-91, SAN-92, SAN-93) can run close together and all touch how an incoming hit is resolved.

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
