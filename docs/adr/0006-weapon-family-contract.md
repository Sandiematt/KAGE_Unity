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

## Notes from the implementation (SAN-70)

- Files: `Combat/Families/MoveSet.cs` (the abstract `MoveSet` and `MoveSets`, the per-fight cache), `WeaponFamilyDef.cs` (`WeaponFamilyDef`, `GripPose`, `IWeaponVisual`), `WeaponFamilies.cs` (registry and the JS weapon → family map), `WeaponStats.cs`, `KatanaMoves.cs` (was `KageMoves.cs`, renamed with git), `KatanaVisual.cs`, `FistsMoves.cs`, `FistsVisual.cs`; `Combat/DamageCalc.cs`; `Combat/Fighter.Weapon.cs` (`EquipWeapon`, `Family`, `WeaponId`, `WeaponLevel`, `Weapon`, `PlaybackRate`, `BladeTipLocal`).
- `build` is `Func<MoveSets, MoveSet>`, not `Func<MoveSet>`: a fight's `FightContext.MoveSets` builds each family's set on first use, and a family reaches other families' sets of the same fight through it, so it can share moves by reference (the fists set holds the katana set's punches and kicks). A set is built once per fight, as `MoveSet.Build()` was per `FightBuilder`, because key poses cache their resolved angles.
- Registration: `WeaponFamilies.Register()` has one `Add(<Family>Moves.Family)` line per family; the family's `referenceWeapon` must match the table above or registration throws. `WeaponFamilies.FamilyIdOf(weaponId)` maps any JS weapon (through its JS `base`; `raiko_no_tachi` and `raikon_tachi` → `tachi`, Iwakudaki → `cleaver`) whether or not the family is registered.
- `GripPose` gained `Fists` (bare hands). The stance's `Pose.Grip()` is honoured for `TwoHand`, `HeavyTwoHand` and `Polearm`; every other grip, a broken sword and the fists family let the near hand go (one rule, `Fighter.GripHeld`). The grip spacing is still the katana's (`Pose.GripSpacing`); a polearm that holds its hands further apart needs its own spacing (SAN-78).
- Bare hands (fists family or a broken sword) share one path: `MoveSet.BareHanded(m)` gives a stand-in (the katana set maps the Pommel Strike to `crossFist`), other blade moves are dropped, and the held stances show both fists up.
- **Reference rule as built:** `WeaponStats.Combat` = `GameRules.WeaponMul` (the JS port) except that a family's reference weapon is measured against itself. That only changes Raikō no Tachi (×1 at level 1). `raikon_tachi` (Raikon's foe weapon, family `tachi`, JS base Onikiri) keeps its JS multiplier 64/82 = ×0.78 rather than being measured against Raikō no Tachi (which would be ×0.34); Iwakudaki and the Gate kanabō keep theirs (×1 against Iwakudaki). SAN-81/SAN-107 tune the tachi with that in mind.
- **Where the weapon multiplier counts:** every window of the fighter's move set (punches and kicks included, since the family owns every button), blade projectiles (`sword` windows: the Crescent) and the rage art. Not throws, kunai or hazards. The character's damage multiplier and the defender's defence count everywhere (`DamageCalc.For`). Kage's starter defence (0.03) is applied from now on: no baseline hit changes, since every one rounds back to the same whole number.
- **Fractional playback as built:** the move time advances by `PlaybackRate` (= character move rate × weapon speed) each frame and `moveFrame` is its whole part. A hit window or a root motion is on when a whole frame passed this frame lies in it, or, when no whole frame passed (rate below 1), when the current frame does; so at rate 0.5 a window is on for twice as many frames, and a move's hit sweeps stay continuous (sweep continuity counts ticks, not move frames). One-frame triggers fire on the frame that passes them. Root-motion x velocity is multiplied by the rate; a `setY` jump impulse is not (the arc stays the authored one, only the poses play slower or faster). Hitstun, blockstun and hitstop given to the target are untouched. Blend times of state poses are not scaled.
- Reach: the Blade hit points run from the guard to `bladeLength × reach` along the drawn blade. Within a millimetre of the drawn blade they use it exactly, so Kagetsu at level 1 and the katana foes hit from the same points as before. The drawn katana itself doesn't grow with reach.
- `HitWindow.postureMul`, `MoveDef.armorFrom/armorTo` (`Armour(from, to)`, `Armoured(f)`) are data only; posture (SAN-86) and the armour check (SAN-80) apply them.
- Crimson Moon: `WeaponFamilyDef.rageArt` is set (`crimsonMoon` / `thousandShadows`) but `TrySpecialDown` still starts Thousand Shadows for every family until SAN-82 reads it.
- Loadout: `CharacterDef.weaponLevel` is new; `KageCharacter.Def(save)` fills `weaponId`, `weaponLevel` and `family` from the save's equipped weapon. SAN-84 only has to pass the loadout's weapon and level.
- Dev arena: F8 cycles the player's weapon through `WeaponFamilies.PlayableWeapons()` (player weapons of registered families, JS table order: fists, kagetsu, shizuka, shinsen, mugen_no_ha), Shift+F8 raises its level (wraps at the max).

## Notes from the implementation (SAN-78)

- Files: `Combat/Families/SpearMoves.cs` (the set), `SpearVisual.cs` (the drawing), one `Add(SpearMoves.Family)` line in `WeaponFamilies.Register()`. Family `spear`: reference `mizuchi`, grip `Polearm`, `bladeLength` 1.45 m, rage art Thousand Shadows. Mizuchi, `sentinel_spear`, `temple_staff`, `vanguard_yari` (and `sage_staff`) all fight with it; their JS stats are Mizuchi's, so every combat multiplier is ×1 at level 1. The foes' weapon looks differ (`SpearVisual` by weapon id: Mizuchi's dragon collar and kris head, the Order's leaf head, Shiden's yari, a plain staff for Kuroe); the set and the hit line are the same.
- **Move list** (ids `spear.<move>`): Punch: `jab` Quick Poke, `poke2` Rising Poke (jab, P), `stepPoke` (P+forward; the shared `throwGrab` when the foe is close and can be thrown), `lowPoke` (P+down), `skewer` (P+down on a downed foe within 1.9 m, an OTG stab). Kick: `kick`, `pushKick` (K+forward), `lowKick` (K+down), `vault` Dragon Ascent (K+back, a hop with the point driven up: a launcher). Weapon: `thrust` Long Thrust, `lunge` Charging Lunge (S+forward, root motion), `sweep` Tide Sweep (S+down, Low, knockdown, OTG), `guardRaise` Raised Guard (S+back: no hit window, `Armour(5, 24)` as data for SAN-80/SAN-86). Air: `airPoke` (P), `airDive` Falling Dragon (K), `airSweep` Wave Crest (S). Special has no plain move yet (Special + Down is ADR-0004's). Chains: jab → poke2 (P) / thrust (S) / kick (K); poke2 → sweep (S) / thrust (P); stepPoke and kick → thrust (S); thrust → sweep (S); lowPoke → poke2 (P). The body moves (`roll`, `backflip`, `getUp`, `getUpFront`, `throwGrab`, `throwWhiff`) are the katana set's, by reference. Boss abilities (`Ability(id)`: Censure, Vanguard Break, ...) are for their tickets and return null for now.
- **Polearm grip:** `Pose` gained `gripSpacing` and `gripPommel` (default the katana's `GripSpacing`, `GripPommel`); `Pose.PolearmSpacing` (−0.40 m: the rear hand a forearm behind the lead hand on the shaft) and `PolearmPommel` (−0.62 m: it slides back towards the butt when the arm can't reach) are the polearm's. `Fighter.ApplyGrip` (called wherever the display pose was given `grip = false` before) sets them from the family, so authored poses and the shared stance poses need no change; the stance, walk and guard poses are the katana set's with the spear on the hands. `ApplyGrip` also lifts the shaft so the point never goes below the floor (a low guard or the sweep tilts it up instead of digging in), measured from the baked arm angles because a fighter's pose often has no absolute blade angle (`hasSwAbs` is false after the first blend from the intro pose).
- **Blade line:** for a polearm the hit line is the straight shaft axis (the katana's drawn blade has a slight curve), from 0.38 m behind the guard point to `bladeLength × reach` ahead of it, so a foe closer than the lead hand is met by the shaft and not passed by the point (`Fighter.BladeBaseLocal`). `BladeReach` (the number reach scales, 1.45 m at level 1, ×1.105 at level 8) still runs from the guard. Blade hit points are one about every 18 cm, never fewer than five (a katana still has its five); the blade trail follows `BladeTipLocal`.
- **Drawing:** `FighterRig.AddFamilyPart` / `ClearFamilyParts` add and remove a family's weapon parts on the Sword bone (axis frame: x along the weapon from the lead hand), `SetBaseSwordHidden` hides the part set's own katana while they show, and `SetSwordAlwaysFront` draws a polearm in front of the body all the time (the shaft passes the body between the hands). `SetSwordVisible(false)` (a broken weapon, the fists family) hides both. Other families' visuals use the same calls.
- **Reach** (metres from the body to the farthest hit point of any window, root motion not counted; measured by `SpearFamilyFightTests`): spear 2.25 (Long Thrust), katana 1.45, fists 0.89. The test compares the spear with every registered family, so a new family joins the comparison when it is registered.
- Test harness: `HeadlessFight` takes an optional `bagAnchor`, because a Low hit can't reach the dev arena's bag (its lowest point is about 1 m up): the landing test tries that bag and one hung low.
- Dev arena: F8 now cycles fists, Kagetsu, Mizuchi, Shizuka, Shinsen, Mugen no Ha.

## Notes from the implementation (SAN-80)

- Files: `Combat/Families/FistsMoves.cs` (rewritten), `Combat/Fighter.Armour.cs` (the armour check, see ADR-0004), `FistsVisual.cs` (comment only: Garan's gauntlets are part of his own part set, since `AddFamilyPart` only draws on the Sword bone). Family `fists`: reference `fists`, grip `Fists`, no blade, Thousand Shadows. Bare Hands and `iron_fists` (Garan) fight with the same set; their JS stats give the combat multipliers as before.
- **Move list.** Shared with the katana set by reference (their katana ids stay): Punch `jab` (P), chain `crossFist` (P), `uppercut` (P); `lunge` Stepping Punch (P+forward), or the shared `throw` Shoulder Throw when the foe is close and can be thrown; `crouchPunch` (P+down), or `groundPound` on a downed foe; Kick `kick1` Roundhouse, chain `kick2`, `axe`; `push` (K+forward), `axe` (K+back), `sweep` (K+down); air `airPunch` (P), `airKick` (K). New, `fists.<move>`: **Weapon** `haymaker` Iron Haymaker (S: heavy overhand swing, dmg 15, knockdown, `Armour(3, 22)`, window 14-20), `grapple` Iron Grip (S+forward, the heavy grapple), `quake` Quake Fist (S+down: Low, knockdown, OTG), `ironGuard` Iron Guard (S+back: no window, `Armour(5, 26)`), air `airSlam` Axe Handle (S: overhead spike, knockdown); **Special** `shadowPalm` Shadow Palm (X: 25 Rage, dmg 10, kb (7, 1.4), knockdown). Chains: jab and roundhouse into `haymaker` with Weapon (see below).
- **Chaining into a new move without copying a shared one.** A shared katana move's chain table is the katana's, so the fists reach the Haymaker through `MoveSet.BareHanded`: `FistsMoves.BareHanded` maps the Iai Draw (a jab's and a roundhouse's Weapon chain) to `haymaker`, the same way the Pommel Strike becomes `crossFist`. `Fighter.Armed` applies it to selected moves and chain targets alike.
- **The heavy grapple** reuses SAN-68's throw: grab at frame 9 if the foe is within **1.25 m** (the shoulder throw's 0.95 m) and can be thrown, else it plays `grappleWhiff` from the same move time; the victim is held and carried frames 9 to 43 (34 frames) from 0.62 m in front to 1.0 m behind on a 1.7 m arc (overhead), then released at frame 43 into a physics launch (damage 18, impulse (5, -5) behind the thrower, divided by the victim's mass; a heavy victim lands closer). The grapple is armoured from frame 2 to the release, so light hits neither stop it nor drop the victim; a heavy hit does. `MoveDef` gained throw tuning with the shoulder throw's values as defaults: `grabRange` (0.95), `carryFrom` (0.62), `carryTo` (-0.78), `carryHeight` (1.3), `throwKb` (-2.4, -3; x is forward of the thrower). `Fighter.Throw.cs` reads them instead of its constants. The throw doesn't check the NoGrab flag yet (SAN-102).
- **Armour frames** are `MoveDef.Armour(from, to)` as before; the check is ADR-0004's notes. The fists set uses them on the Haymaker, the grapple (start to release) and the Iron Guard.
- **AI Special.** `AIInputSource` now presses Special at range only when the family's plain Special is a projectile move (`Select(Btn.X)` has a `projectileFrame`), so a fists foe with Rage doesn't throw a palm at nothing. The katana is unchanged (the baseline replay holds).
- Reach: the fists set reaches 0.89 m (the Axe Kick; the new moves are within that) against the spear's 2.25 m; no move has a `Blade` point, so none clashes (tested).
- Review images: `Screenshots/review/SAN-80/` (`FistsScreenshots`).
