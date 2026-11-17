# ADR-0006: The weapon family contract

- Status: Accepted (2026-11-17, SAN-111)
- Affects: SAN-70 (builds it), SAN-77 to SAN-82, SAN-84, SAN-87, SAN-69, SAN-93, every foe and boss ticket

## Context

Today there is one `MoveSet` (built in `KageMoves.cs`) shared by Kage and the Ronin. It owns every button: punches (`jab`, `cross` = Pommel Strike, `uppercut`), kicks, katana techniques, the Crescent, and body moves (`roll`, `backflip`, `getUp`, `throwGrab`). `MoveSet.Select(Btn, fwd, dy, air, self)` picks a move. The spec adds six player-quality families plus the heavy tachi, picked by the equipped weapon, with JS upgrade multipliers and a per-character move rate. Enemies use the same sets.

## Decision

### A family is a full move set

```csharp
public abstract class MoveSet
{
    // body moves, built once and shared by every family (references, not copies)
    public MoveDef roll, backflip, getUp, getUpFront, throwGrab, throwWhiff;
    public MoveDef counterSlash;        // katana family only (Mirror Guard); null elsewhere

    // Punch, Kick, Weapon and plain Special, by direction (relative to facing) and context
    public abstract MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self);

    // a named ability move for foes and bosses (ADR-0008); null when the family has none
    public virtual MoveDef Ability(string abilityId) => null;

    // every move of the set, for "each move lands on the training bag" tests
    public abstract IEnumerable<MoveDef> All { get; }
}

public sealed class WeaponFamilyDef
{
    public string id;               // "katana", "twin", "spear", "chain", "cleaver", "fists", "tachi"
    public string referenceWeapon;  // JS weapon id whose stats count as ×1 for this family
    public Func<MoveSet> build;     // built once per run, shared by every fighter of the family
    public GripPose grip;           // OneHand, TwoHand, Dual, Polearm, HeavyTwoHand
    public float bladeLength;       // metres from guard to tip; 0 for fists
    public string rageArt;          // "crimsonMoon" for katana, "thousandShadows" for every other family
    public IWeaponVisual visual;    // draws the weapon on the hand bones
}

public interface IWeaponVisual
{
    void Attach(FighterRig rig, string weaponId);  // e.g. Tsubame vs Shadow Daggers vs Mist Fans
    void Detach(FighterRig rig);
}
```

- A family covers **every button**: Punch, Kick, Weapon and Special, on the ground, crouching, forward, back and in the air, with chains. Families share moves by reference through builder helpers, never by copying a `MoveDef`.
- Today's `MoveSet` becomes `KatanaMoves : MoveSet` and is the katana family unchanged. Kage with Kagetsu at level 1 must replay the baseline exactly (SAN-70).
- `Special + Down` is not part of `Select`: ADR-0004 handles it (rage art, kunai).
- **Move ids:** today's katana ids (`jab`, `iai`, `cleave`, ...) stay as they are, because the baseline replay uses them. New families use `<family>.<move>` (`twin.crossSlash`). Character abilities use `<characterId>.<ability>` (`raikon.thunderSwing`).
- **Clash eligibility:** a hit window can clash only if it uses `HitPoint.Blade`. `Blade` means the weapon's line for every family: the sword, the spear shaft, the cleaver, the sickle blade. Fist, hilt, foot, shin and knee points never clash. The fists family has no `Blade` points.
- Kage's katana drawing stays in his part set. Other families hide the parts on the `Sword` bone and attach their own weapon parts there, plus `HandN` for a second blade (twin blades). `bladeLength` sets the `Blade` hit points, which are sampled from guard to tip as today.

### Families and weapons

| Family | Reference weapon | Player weapons | Foe weapons (JS id) | Rage art | Ticket |
|---|---|---|---|---|---|
| `katana` | `kagetsu` | Kagetsu, Shizuka | `cutter_blade` (Footman, Shade, tutorial Hollow) | Crimson Moon | SAN-70 |
| `fists` | `fists` | Bare Hands | `iron_fists` (Garan) | Thousand Shadows | SAN-70, SAN-80 |
| `twin` | `tsubame` | Tsubame | `shadow_daggers` (Runner, Flicker), `mist_fans` (Hisame) | Thousand Shadows | SAN-77 |
| `spear` | `mizuchi` | Mizuchi | `sentinel_spear` (Shieldbearer), `temple_staff` (Kuroe), `vanguard_yari` (Shiden) | Thousand Shadows | SAN-78 |
| `cleaver` | `onikiri` | Onikiri | `raider_cleaver` (Ironclad) | Thousand Shadows | SAN-79 |
| `chain` | `yamigama` | Yamigama | `chain_hook` (Chainthrower) | Thousand Shadows | SAN-87 |
| `tachi` | `raiko_no_tachi` | Raikō no Tachi | `raikon_tachi` (Raikon) | Thousand Shadows | SAN-81 |

- The JS weapon table (ADR-0010) maps each weapon id to its family through its JS `base` and stance, as in this table.
- Weapons from later acts that dev mode owns use the family of their JS `base` (Shinsen and Mugen no Ha: katana; Kagehira & Yūrei: twin). Iwakudaki (hammer, out of scope) uses the cleaver family until a hammer family exists.
- `WeaponFamilies.cs` registers each family with one line. Each family's moves and visual live in `Combat/Families/<Family>Moves.cs` and `<Family>Visual.cs`.

### Upgrade multipliers

Combat multipliers use the JS rule: `+8 %` damage, `+2.5 %` speed, `+1.5 %` reach per level, speed capped at ×1.25 and reach at ×1.2, measured **against the family's reference weapon at level 1**.

- For every weapon with a JS `base`, the family reference *is* the JS base, so the combat multiplier equals the JS `weaponMul`.
- **Exception: Raikō no Tachi.** In the JS game its base is Onikiri, which would make it ×2.3 damage on top of a set that is already authored heavy. In Unity it has its own family, so it is measured against itself: ×1 at level 1, and it stays at level 1 (SAN-81).
- `GameRules.WeaponMul` in `Kage.Rules` stays an exact JS port, for the parity tests. Combat calls `WeaponStats.Combat(weaponId, level)` in `Kage.Runtime`, which applies the family reference.
- Kagetsu at level 1 is ×1 on everything, so the baseline doesn't change.

### Applying the multipliers

**Damage** is computed in one place, `DamageCalc` (`Scripts/Combat/DamageCalc.cs`, created by SAN-70):

```
raw = window.dmg × attacker.damageMul × weapon.dmg × (crit ? 1.5 : 1) × (1 − defender.defense)
dmg = max(1, RoundToInt(raw))     // one rounding, at the end
```

With every multiplier at 1 and defence 0 this is exactly today's damage (`RoundToInt(dmg × 1.5)` on a crit). Kage's `defense` comes from his gear (SAN-84). A foe's comes from its character data. The guaranteed critical after a guard-break is applied on the defender's side (ADR-0004).

**Speed** becomes the move rate: `rate = character.moveRate × weapon.speed`. SAN-70 changes move playback as follows:

- the fighter keeps a fractional move time and advances it by `rate` each frame; `moveFrame` is its whole part;
- a hit window is active on a frame if any whole frame passed this frame lies in `from..to`;
- one-frame triggers (`projectileFrame`, `grabFrame`, `releaseFrame`, `Fx` at `from`, a root-motion `setY` at `from`) fire once, when their frame is passed, so none is skipped at a rate above 1 or repeated below 1;
- root-motion velocities are multiplied by `rate`, so a move covers the same distance;
- hitstun, blockstun and hitstop given to the target are not scaled;
- at rate 1 playback is exactly today's.

**Reach** scales the `Blade` hit points along the blade (`bladeLength × weapon.reach`). Other hit points ignore it.

### Additions to move data (all by SAN-70, defaults keep today's behaviour)

- `HitWindow.postureMul = 1f`: multiplies the posture this window adds (Raikō no Tachi is the highest; posture itself is SAN-86).
- `MoveDef.armorFrom = -1, armorTo = -1`: armour frames. Light hits (windows without `heavy`) don't flinch the fighter during them (SAN-80 armoured swing, Garan's grapple).
- `MoveDef.Rate(float)` is not added. Move rate comes only from character and weapon data.

## Consequences

- Switching weapon swaps the whole move set, grip and blade, so switching changes how Kage fights, and every foe reads like the player family it uses.
- Kage's katana set is untouched, so the approved feel and the baseline are protected.
- Fractional playback is the only change to the move engine. It is off (rate 1) for Kage with Kagetsu.
