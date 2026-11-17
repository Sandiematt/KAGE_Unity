# ADR-0007: Projectile, hazard, physics layers and effect cues

- Status: Accepted (2026-11-17, SAN-111)
- Affects: SAN-65, SAN-68, SAN-75 (builds projectile, hazard and cues), SAN-82, SAN-87, SAN-88, SAN-91, SAN-92, SAN-93, SAN-99, SAN-104, SAN-106, SAN-107

## Context

The only projectile is `Crescent`: a one-off `MonoBehaviour` that moves in `Update` with `Time.deltaTime`, hit-tests `owner.opponent` and `owner.extraTarget` with `Overlap`, and calls `Vfx.I` directly. Kunai, the Chainthrower's lash, Raikon's Thunder Swing wave and Skyfall, and the shock areas of Storm Break, Garan's Storm Fist and Shiden's Vanguard Break all need the same thing. Physics layers are numbers in `KageGame` (8 Fighter, 9 Ragdoll, 10 Ground, 11 Prop), set up in code, with no names in `TagManager.asset`.

## Decision

### Physics layers

Layer names go into `ProjectSettings/TagManager.asset`, and the collision matrix into `Physics2DSettings.asset` (SAN-68). Code reads them from one static class, `PhysicsLayers` (`Scripts/Core/PhysicsLayers.cs`), which replaces the constants in `KageGame`.

| # | Name | What is on it | Collides with | Added by |
|---|---|---|---|---|
| 8 | `Fighter` | the fighter's body box (ground contact only) | Ground | exists |
| 9 | `Ragdoll` | KO ragdoll bodies | Ground | exists |
| 10 | `Ground` | floor, walls | Fighter, Ragdoll, Prop, Push, Projectile | exists |
| 11 | `Prop` | training bag and other props | Ground | exists |
| 12 | `Hurtbox` | hurt-circle triggers on bones (fighters and props) | nothing (queried by casts only) | SAN-65 |
| 13 | `Push` | fighter push colliders | Push, Ground | SAN-68 |
| 14 | `PushPassThrough` | push colliders during pass-through frames | Ground | SAN-68 |
| 15 | `Projectile` | projectile bodies | Ground | SAN-75 |
| 16 | `Hazard` | hazard areas | nothing (queried by overlap only) | SAN-75 |

- Every hit test (melee sweeps, projectiles, hazards) casts against `Hurtbox` only, skips the owner, and skips the owner's team (ADR-0002).
- A `Hurtbox` component on each hurt-circle collider carries its owner (`IHittable`) and its `HitZone`. Fighters and the training bag use the same component (SAN-65).
- The ragdoll is unchanged.

### Projectile

```csharp
public sealed class ProjectileDef
{
    public string id;              // event MoveId: "crescent", "kunai", "chainthrower.lash", "thunderSwing.wave"
    public HitWindow hit;          // damage, level, knockback, stun, hitstop
    public float speed;            // m/s along the facing
    public float gravityScale;     // 0 = straight line
    public bool groundWave;        // slides along the floor (Thunder Swing)
    public float radius;           // hit radius, metres
    public int lifeFrames;
    public int maxHits = 1;        // targets it can hit before it is spent
    public string flightCue, hitCue; // effect cue ids
}

public sealed class Projectile : MonoBehaviour, IHasOwner
{
    public static Projectile Spawn(FightContext ctx, Fighter owner, ProjectileDef def, Vector2 pos, float dir);
    public Fighter Owner { get; }
}
```

- Projectiles move on the fight loop (ADR-0002, step 6), never in `Update`. Each frame a projectile circle-casts from its previous to its current position against `Hurtbox`. It keeps its own hit log, so one projectile hits a target at most once.
- A hit resolves like a melee window: `target.ReceiveHit(info)`, then `owner.ResolveExternal(target, info, result)`, which publishes `Hit` or `Blocked` with `MoveId = def.id` (ADR-0003). Guard rules apply through `hit.level`. A parried projectile is destroyed, and its owner isn't staggered (ADR-0004). Projectiles never clash.
- Projectiles advance at the fight clock's world scale (frozen by Crimson Moon, slowed by a perfect dodge).
- The Crescent keeps its damage, speed (8.2 m/s), life (1.15 s = 69 frames), radius and hit data. Its layered mesh look moves into its flight cue (SAN-75).

### Hazard

```csharp
public sealed class HazardDef
{
    public string id;               // event MoveId: "skyfall", "stormBreak.shock", "garan.stormFist.shock"
    public int telegraphFrames;     // ground mark showing, no hit
    public int strikeFrames;        // active frames
    public Vector2 size;            // box on the floor, metres (width, height)
    public HitWindow hit;           // level Unblockable unless set otherwise
    public string telegraphCue, strikeCue;
}

public sealed class Hazard : MonoBehaviour
{
    public static Hazard Spawn(FightContext ctx, Fighter owner, HazardDef def, Vector2 groundPoint);
}
```

- A hazard shows its telegraph for `telegraphFrames`, then for `strikeFrames` checks a box overlap against `Hurtbox` each frame. It hits each target at most once. A fighter who has left the box isn't hit.
- Hazards tick on the fight loop and scale with world time like projectiles.

### Effect cues

```csharp
public static class EffectCues
{
    public static void Play(string cueId, Vector2 pos, in CueArgs args = default);  // no-op when Vfx.I is null
    public static void Register(string cueId, CuePlayer player);                   // a placeholder or, later, the real effect
}
public struct CueArgs { public Fighter owner; public Vector2 dir; public float scale; public Color? tint; }
public delegate void CuePlayer(Vector2 pos, in CueArgs args);
```

- Every new effect call goes through a cue (spec). Existing `Vfx` calls in Kage's moves may stay until the VFX stage.
- **Cue ids:** the JS VFX manifest id where the JS game has that effect (`E:\PROJECTS\KAGE\KAGE\vfx\vfx_manifest.json`), for example `VFX_Raikon_Thunder_Wave`, `VFX_Raikon_Sky_Strike`, `VFX_Raikon_Redline_Guard`, `VFX_Rage_Crimson_Crescent`, `VFX_Takashi_Kunai`, `VFX_Anime_Ground_Crack`, `VFX_Anime_Impact`. Effects the JS manifest doesn't have get a `VFX_Kage_<Name>` id: `VFX_Kage_Parry`, `VFX_Kage_Clash`, `VFX_Kage_GuardBreak`, `VFX_Kage_PerfectDodge`, `VFX_Kage_Crescent`.
- Each feature registers its placeholder from its own file (a `[RuntimeInitializeOnLoadMethod]` that calls `EffectCues.Register`), so no ticket edits a shared cue table. An unregistered id plays a generic placeholder (`Vfx.Star`) and logs one warning.
- SAN-75 creates `EffectCues`. Tickets that land later just register their cues.

## Consequences

- Kunai, the lash, Thunder Swing, Skyfall and every shock area are data plus a spawn call. Their hits go through the same hurtbox path and the same defence order as melee hits.
- Projectiles and hazards are deterministic in the harness because they run on the fight loop.
- The layer matrix becomes project settings that the owner can see. SAN-65 (Hurtbox), SAN-68 (Push layers and the whole matrix) and SAN-75 (Projectile, Hazard) edit `TagManager.asset` and `Physics2DSettings.asset`, each in a different wave. Until SAN-68 lands, SAN-65 keeps setting its layer's collisions in code, as `KageGame` does today.
- Garan's Storm Fist and Shiden's Vanguard Break use hazards, so those sub-bosses depend on SAN-75.
