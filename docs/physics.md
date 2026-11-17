# Physics layers, materials, push and mass

How the fight's 2D physics is set up (ADR-0007, SAN-68). Nothing here is set in code: the layer
names, the collision matrix and the materials are project settings and assets, so you can see and
change them in the Editor.

## Layers

Names are in **Project Settings > Tags and Layers** (`ProjectSettings/TagManager.asset`). Code reads
the numbers from `PhysicsLayers` (`Assets/Kage/Scripts/Core/PhysicsLayers.cs`).

| # | Name | What is on it |
|---|---|---|
| 8 | `Fighter` | the fighter's body box (ground contact only) |
| 9 | `Ragdoll` | KO ragdoll bodies |
| 10 | `Ground` | floor and walls |
| 11 | `Prop` | training bag and other props |
| 12 | `Hurtbox` | hurt-circle triggers on bones (fighters and props) |
| 13 | `Push` | fighter push boxes |
| 14 | `PushPassThrough` | a push box during pass-through frames |
| 15 | `Projectile` | projectile bodies (SAN-75) |
| 16 | `Hazard` | hazard areas (SAN-75) |

## Collision matrix

In **Project Settings > Physics 2D > Layer Collision Matrix** (`ProjectSettings/Physics2DSettings.asset`).
Among the game layers (8 to 16), only these pairs collide:

| Layer | Collides with |
|---|---|
| Fighter | Ground |
| Ragdoll | Ground |
| Ground | Fighter, Ragdoll, Prop, Projectile |
| Prop | Ground |
| Hurtbox | nothing (hit casts query it) |
| Push | Push |
| PushPassThrough | nothing |
| Projectile | Ground |
| Hazard | nothing (overlap queries only) |

The built-in layers (Default, TransparentFX, Ignore Raycast, Water, UI) and the unnamed layers 17 to 31
collide with each other and with Ground, and with no other game layer.

`PhysicsSettingsTests` (EditMode) checks this table.

**Difference from ADR-0007:** the ADR lists Push as colliding with Ground. It doesn't here. The push box
is 0.72 m wide, twice the body box, so against the walls it would stop a fighter at x = 7.54, inside the
stage edge (`StageHalf` = 7.6) and before `AtWall` (7.55) can become true. Fighters still stop at the walls
through their body box and the stage clamp, and a cornered fighter still holds the other off through
the push boxes.

## Physics materials

Assets under `Assets/Kage/Resources/Physics`, read through `PhysicsMaterials`:

| Asset | Friction | Bounciness | Used by |
|---|---|---|---|
| `NoFriction` | 0 | 0 | fighter body boxes, push boxes |
| `Ragdoll` | 0.7 | 0.12 | KO ragdoll limbs |

The ground, walls and the training bag use the project default material.

## Push

Each fighter has a push box (`Fighter.Push.cs`): a frictionless box on its own body, on the `Push`
layer. It follows the old manual push's rules:

- Two fighters' centres are kept 0.72 m apart, sideways only. The box is 1.82 m tall (1.1 m push zone
  plus its own width), so whenever two boxes touch they overlap more vertically than sideways, and physics
  always separates them sideways: nobody stands on a head or gets bumped upwards.
- A fighter 1.1 m or more above another passes over it.
- During a move's pass-through frames (`MoveDef.NoPush`), and while down, thrown, KO or in the rage art,
  the box moves to `PushPassThrough` and touches nothing.
- The push is positional, as before: after a push contact a fighter gets its own horizontal speed back,
  so walking into an idle fighter pushes it at half the walking speed. How the push is shared depends on
  mass: a heavy fighter is pushed less.

The fight loop sets each push box's layer after the hit phase, and Unity's physics step that follows
does the pushing. The box is sized in world metres whatever the fighter's scale.

## Mass and knockback

`Fighter.Mass` comes from `FighterSpec.mass` (Kage and the Ronin: 1). Authored knockback
(`HitWindow.kb`) is the velocity a hit gives a fighter of `Fighter.ReferenceMass` (1, Kage), so its
impulse is kb × 1. Hitstun knockback, launches, ground hits, blockstun pushback and the attacker's
recoil off a cornered guard are applied as that impulse on a stopped body: velocity = kb / mass. Kage's
numbers are unchanged. Launches keep their minimum pop (2.8 m/s off the ground), so every launch still
leaves the ground.

A throw carries its victim on the same animated arc as before, whatever the mass (the victim is
kinematic while carried). On release the victim becomes a dynamic body again and is launched by the
throw's impulse, so a heavy victim lands closer.

The KO ragdoll is unchanged.

## Legacy switch

`legacyHitResolution` in `Assets/Kage/Resources/Tuning/combat.json` restores the old behaviour, read once
per fight: hits inside the attacker's tick (SAN-65), the fight loop's manual push with the push boxes
switched off, and knockback that ignores mass. The layer matrix and materials are the same either way.
The legacy switch replays `Tests/PlayMode/Baselines/Legacy` exactly.
