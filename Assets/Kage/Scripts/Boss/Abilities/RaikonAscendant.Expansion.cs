using System.Collections.Generic;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// The Lightning Ascendant's expansion (2026-11-27, docs/raikon-ascendant-vfx.md): three more moves a phase, four cross-phase upgrades and nine
    /// strings, each true to its phase's identity:
    /// <list type="bullet">
    /// <item><b>Phase 1</b> (pure steel, momentum, dust, air shear, embers; no lightning, no blinks): Sheathe Cross, Tsuba Press → Overhead Cleave,
    /// Whirlwind Feint.</item>
    /// <item><b>Phase 2</b> (crackling tachi, ground lightning, static tethers, short blinks): Fulmination Drag, Arc Anchor, Gale Sever; and the phase 1
    /// upgrades: Sheathe Cross's Static Cross, the cleave's Slam Thunder and V of spark lines.</item>
    /// <item><b>Phase 3</b> (the full storm: cloud-called strikes, delayed detonations, afterimage and decoy mix-ups): Tempest Cage, Zenith Guillotine,
    /// Mirage Severance; and the phase 2 upgrades: the drag's gouge pillars, Gale Sever's X-crescent and its residue.</item>
    /// </list>
    /// The final phase (3b) inherits all of it by rule: its ability list is phase 3's, every upgrade switches on a phase index "from" (≥), and the strings'
    /// chance and beat read the phase index. Blinks, the ring lock and the vanish run as <see cref="MoveDef.steps"/>; everything a player sees is a cue.
    /// </summary>
    public static partial class RaikonAscendant
    {
        // ---- moves (built by BuildExpansion; public for tests) ----
        public static MoveDef SheatheCross, SheatheCrossStorm, TsubaPress, TsubaPressStorm, OverheadCleave, OverheadCleaveStorm, WhirlwindFeint;
        public static MoveDef FulminationDrag, FulminationDragStorm, ArcAnchor, ArcAnchorRecall, GaleSever, GaleSeverStorm;
        public static MoveDef TempestCage, ZenithGuillotine, MirageSeverance;

        // ---- their hazards and projectiles ----
        public static HazardDef StaticCross, CleaveVee, GougePillar, RadialSlash, StaticResidue, CageSpear, MirageBurst;
        public static ProjectileDef AnchorBlade, VacuumBlade, LowCrescent, XCrescent;

        /// <summary>Every hazard of the expansion (for the cue and data tests).</summary>
        public static IEnumerable<HazardDef> ExpansionHazards
        {
            get { yield return StaticCross; yield return CleaveVee; yield return GougePillar; yield return RadialSlash; yield return StaticResidue; yield return CageSpear; yield return MirageBurst; }
        }

        /// <summary>Every projectile of the expansion.</summary>
        public static IEnumerable<ProjectileDef> ExpansionProjectiles
        {
            get { yield return AnchorBlade; yield return VacuumBlade; yield return LowCrescent; yield return XCrescent; }
        }

        // ---- numbers the tests and the cues share ----
        /// <summary>Arc Anchor: the thrown tachi's speed (m/s) and flight (frames): 9 m.</summary>
        public const float AnchorSpeed = 30f;
        public const int AnchorFlightFrames = 18, AnchorThrowFrame = 22, AnchorBlinkFrame = 50;
        /// <summary>Tempest Cage: the ring's radius (5 m across), where the spears stand from its centre, and where he stands to run a chord.</summary>
        public const float CageRadius = 2.5f, CageSpearAt = 1.8f, CageChordAt = 1.75f;
        /// <summary>Zenith Guillotine: how high over the locked ring he reappears, and how fast he drops (lands about 4 frames later).</summary>
        public const float ZenithDropHeight = 2.6f, ZenithDropSpeed = 40f;

        static void RegisterExpansion()
        {
            Reg(SheatheCrossId, SheatheCross, 4.0f, 0.6f, 2.4f, 0.5f).move.variantFor = f => PhaseOf(f) >= 1 ? SheatheCrossStorm : null;
            var press = Reg(TsubaPressId, TsubaPress, 4.5f, 0.3f, 1.6f, 0.5f);
            press.move.variantFor = f => PhaseOf(f) >= 1 ? TsubaPressStorm : null;
            press.chainIf = ended => ended == TsubaPress || ended == TsubaPressStorm;    // the shove whiffed: the press itself ended, not the cleave
            Reg(WhirlwindFeintId, WhirlwindFeint, 4.5f, 1.0f, 2.6f, 0.45f);

            Reg(FulminationDragId, FulminationDrag, 6.0f, 2.0f, 6.5f, 0.45f).move.variantFor = f => PhaseOf(f) >= 2 ? FulminationDragStorm : null;
            Reg(ArcAnchorId, ArcAnchor, 7.0f, 3.0f, 9.0f, 0.4f);
            Reg(GaleSeverId, GaleSever, 5.5f, 2.0f, 7.5f, 0.45f).move.variantFor = f => PhaseOf(f) >= 2 ? GaleSeverStorm : null;

            Reg(TempestCageId, TempestCage, 9.0f, 0.8f, 4.5f, 0.4f);
            Reg(ZenithGuillotineId, ZenithGuillotine, 8.0f, 1.5f, 8.0f, 0.4f);
            Reg(MirageSeveranceId, MirageSeverance, 8.0f, 1.2f, 5.0f, 0.4f);
        }

        // ---------------------------------------------------------------------------------------------- per-fight state of the frame actions
        /// <summary>What a Raikon's frame actions remember between their frames: the thrown blade, the cage's ring, the zenith's lock.</summary>
        public sealed class ExpansionState
        {
            public bool anchorEnded, anchorDeflected;
            public Vector2 anchorAt, blinkFrom;
            public float cageCentre, cageSide;
            public bool cageLocked;
            public float zenithX, zenithGround;
            public bool zenithLocked;
        }

        static readonly Dictionary<Fighter, ExpansionState> States = new();

        /// <summary>The expansion's state of <paramref name="f"/> (made on first use).</summary>
        public static ExpansionState StateOf(Fighter f)
        {
            if (f == null) return new ExpansionState();
            if (!States.TryGetValue(f, out var s))
            {
                if (States.Count > 16) States.Clear();       // fights come and go (tests): never more than a handful alive
                s = States[f] = new ExpansionState();
            }
            return s;
        }

        // ---------------------------------------------------------------------------------------------- building
        static void BuildExpansion(float scale)
        {
            BuildExpansionHazards();

            // ==================================================================== PHASE 1: PURE SAMURAI
            // Sheathe Cross: a backstep that sheathes the blade (dust off the front foot), the scabbard mouth glints, then a fast horizontal draw at f24
            // and the reverse draw at f35 crossing it: an X of pale arcs. It punishes chasing the backstep. Phase 2 leaves the X hanging as a Static Cross.
            SheatheCross = Sheathe(false);
            SheatheCrossStorm = Sheathe(true);

            // Tsuba Press → Overhead Cleave: an advancing hilt shove (a point of damage, the posture of a heavy guarded blow); on hit or block the delayed
            // heavy cleave follows (f46 from the press's start for a f15 contact). A whiffed shove recovers, and the string drops into Low Sweep.
            OverheadCleave = Cleave(false);
            OverheadCleaveStorm = Cleave(true);
            TsubaPress = Press(false);
            TsubaPressStorm = Press(true);

            // Whirlwind Feint: a wide horizontal wind-up with a steel glint (the bait), broken off at f15 into a skid round the guard (the eye flash: the
            // truth), then a quick unguardable thrust at f29.
            WhirlwindFeint = new MoveDef("raikon.whirlwindFeint", "Whirlwind Feint", 64)
                .Key(8, Hold(P().Hip(-0.06f, -0.16f, 8f).Chest(14f).Head(-6f).Feet(0.3f, -0.32f), -0.35f, 1.3f, 170f), Ease.OutQuad)
                .Key(14, Hold(P().Hip(-0.08f, -0.17f, 9f).Chest(16f).Head(-7f).Feet(0.3f, -0.34f), -0.4f, 1.32f, 176f))
                .Key(18, Hold(P().Hip(0.04f, -0.3f, -6f).Chest(-14f).Head(10f).Feet(0.46f, -0.36f), 0.02f, 1.0f, 2f), Ease.OutExpo)
                .Key(25, Hold(P().Hip(0.06f, -0.32f, -8f).Chest(-18f).Head(12f).Feet(0.48f, -0.38f), 0.0f, 0.98f, 0f))
                .Key(29, Hold(Lunge(0.16f, -0.18f, 0.5f, -0.34f).Chest(-24f), 0.72f, 1.12f, 0f), Ease.OutExpo)
                .Key(33, Hold(Lunge(0.16f, -0.18f, 0.5f, -0.34f).Chest(-24f), 0.7f, 1.12f, 0f))
                .Key(64, Ready(), Ease.InOutQuad)
                .Move(15, 24, 5.4f).Move(27, 30, 4f)
                .Hit(29, 32, Blade, Dmg(11), HitLevel.Mid, new Vector2(3f, 0.8f), stun: 26, hitstop: 9, shake: 0.3f, radius: 0.26f).Unguardable()
                .Cue(2, SteelCue).Cue(15, EyeCue).Cue(15, FeintSmearCue).Cue(15, SkidCue).Cue(21, SkidCue).Cue(29, NeedleCue)
                .Fx(28, 33, FxKind.TrailBlade)
                .Impulse(15, new Vector2(-3f, 1f)).Impulse(29, new Vector2(-4f, 1f))
                .Sword().Fixed();

            // ==================================================================== PHASE 2: STORM BLADE
            // Fulmination Drag: the blade tip dropped to the floor behind him, a rush of up to 6 m dragging it (a rooster tail of sparks and a glowing gouge),
            // then an upward flick: an unblockable launch. Phase 3: the gouge detonates in three rising pillars, each 0.35 s after he passed it.
            FulminationDrag = Drag(false);
            FulminationDragStorm = Drag(true);

            // Arc Anchor: the tachi hurled spinning at f22 (9 m; a hit pins Kage 0.6 s), a static tether from his hand to it; at f50 he blinks to the hilt
            // for a radial extraction slash. A parry knocks the blade down and cancels the blink: he recalls it instead, open.
            ArcAnchorRecall = new MoveDef("raikon.arcAnchor.recall", "Arc Anchor", 40)
                .Key(6, P().Hip(-0.04f, -0.16f, 6f).Chest(4f).Head(-4f).Feet(0.3f, -0.32f).HandF(0.55f, 1.2f).HandN(0.2f, 1.2f, 10f), Ease.OutQuad)
                .Key(16, Hold(Base(), 0.1f, 1.1f, 120f), Ease.OutExpo)
                .Key(40, Ready(), Ease.InOutQuad)
                .Sheath(0, 14)
                .Cue(0, AnchorRecallCue)
                .Impulse(14, new Vector2(-1.5f, 1f))
                .Sword();
            ArcAnchor = new MoveDef("raikon.arcAnchor", "Arc Anchor", 90)
                .Key(8, Hold(P().Hip(-0.06f, -0.14f, 8f).Chest(10f).Head(-6f).Feet(0.3f, -0.34f), -0.3f, 1.45f, 170f), Ease.OutQuad)
                .Key(20, Hold(P().Hip(-0.08f, -0.16f, 10f).Chest(14f).Head(-8f).Feet(0.32f, -0.36f), -0.38f, 1.5f, 175f))
                .Key(24, Hold(Lunge(0.12f, -0.14f, 0.44f, -0.3f).Chest(-20f), 0.7f, 1.25f, 0f), Ease.OutExpo)
                .Key(32, Hold(Lunge(0.1f, -0.14f, 0.42f, -0.3f).Chest(-16f), 0.66f, 1.24f, 0f))
                .Key(42, P().Hip(0f, -0.14f, 2f).Chest(-4f).Head(-2f).Feet(0.3f, -0.3f).HandF(0.5f, 1.2f).HandN(0.2f, 1.2f, 10f), Ease.InOutQuad)
                .Key(49, P().Hip(-0.02f, -0.2f, 4f).Chest(-8f).Head(2f).Feet(0.32f, -0.32f).HandF(0.46f, 1.12f).HandN(0.2f, 1.18f, 10f))
                .Key(51, Hold(P().Hip(0.02f, -0.3f, -4f).Chest(-22f).Head(12f).Feet(0.38f, -0.34f), 0.2f, 0.7f, -120f), Ease.OutExpo)
                .Key(55, Hold(P().Hip(0f, -0.22f, 2f).Chest(-8f).Head(4f).Feet(0.4f, -0.36f), -0.3f, 1.1f, 175f), Ease.OutExpo)
                .Key(60, Hold(P().Hip(0.02f, -0.2f, 0f).Chest(-6f).Head(2f).Feet(0.4f, -0.34f), 0.5f, 1.15f, 10f), Ease.OutExpo)
                .Key(90, Ready(), Ease.InOutQuad)
                .Sheath(AnchorThrowFrame, AnchorBlinkFrame)
                .Throw(AnchorThrowFrame, AnchorBlade, new Vector2(0.7f, 1.25f))
                .Invuln(AnchorBlinkFrame - 2, AnchorBlinkFrame + 2).NoPush(AnchorBlinkFrame - 2, AnchorBlinkFrame + 8)
                .Step(AnchorThrowFrame - 1, f => { var s = StateOf(f); s.anchorEnded = false; s.anchorDeflected = false; })
                .Step(AnchorBlinkFrame, AnchorArrive)
                .Cue(6, SteelCue).Every(AuraCue, 34, 46, 6).Cue(AnchorBlinkFrame, AnchorArriveCue)
                .Fx(52, 60, FxKind.TrailBlade)
                .Impulse(AnchorThrowFrame, new Vector2(3f, 1f)).Impulse(AnchorBlinkFrame, new Vector2(-6f, 2f)).Impulse(AnchorBlinkFrame + 3, new Vector2(4f, 2f))
                .Sword().Fixed();

            // Gale Sever: a wide crescent swing that sends a vertical vacuum blade (parry it), then a low horizontal crescent along the floor (jump it).
            // Phase 3: a third, delayed X-crescent that leaves burning static residue on the floor for 1.2 s.
            GaleSever = Gale(false);
            GaleSeverStorm = Gale(true);

            // ==================================================================== PHASE 3: LIGHTNING ASCENDANT
            // Tempest Cage: the hilt slammed down calls four small clouds over a 5 m ring locked on Kage; four lightning spears fall at f30 (the ring's sides
            // zap and bounce him back in); he blinks to his side of the ring and runs a chord across at f44, turns at the far spears, and runs the
            // unblockable final cross back at f58. Jumping out of the ring before f30 escapes it: the chords stay inside.
            TempestCage = new MoveDef("raikon.tempestCage", "Tempest Cage", 100)
                .Key(8, Hold(P().Hip(-0.02f, -0.06f, 4f).Chest(10f).Head(-12f).Feet(0.3f, -0.3f), 0.1f, 1.75f, -90f), Ease.OutQuad)
                .Key(14, Hold(P().Hip(-0.03f, -0.05f, 5f).Chest(12f).Head(-14f).Feet(0.3f, -0.3f), 0.08f, 1.82f, -90f))
                .Key(17, Hold(P().Hip(0.04f, -0.4f, -4f).Chest(-26f).Head(14f).Feet(0.38f, -0.34f), 0.3f, 0.5f, -90f), Ease.InCubic)
                .Key(30, Hold(P().Hip(0.04f, -0.38f, -4f).Chest(-22f).Head(6f).Feet(0.38f, -0.34f), 0.3f, 0.52f, -90f))
                .Key(38, IaiCrouch(), Ease.InOutQuad)
                .Key(43, IaiCrouch(0.03f))
                .Key(46, IaiDash(), Ease.OutExpo)
                .Key(50, IaiEnd())
                .Key(55, IaiCrouch(0.02f), Ease.InOutQuad)
                .Key(57, IaiCrouch(0.04f))
                .Key(60, IaiDash(), Ease.OutExpo)
                .Key(64, IaiEnd())
                .Key(100, Ready(), Ease.InOutQuad)
                .Move(44, 50, CageChordAt * 2f * 60f / 7f).Move(58, 64, CageChordAt * 2f * 60f / 7f)
                .Invuln(38, 43).NoPush(38, 66)
                .Hit(44, 50, Blade, Dmg(8), HitLevel.Mid, new Vector2(2f, 1f), stun: 26, hitstop: 7, shake: 0.25f, radius: 0.32f)
                .Hit(58, 64, Blade, Dmg(12), HitLevel.Unblockable, new Vector2(4.5f, 2.5f), stun: 32, hitstop: 12, shake: 0.55f, knockdown: true, heavy: true, radius: 0.34f)
                .Step(15, LockCage).Step(30, DropSpears).Step(40, f => EnterChord(f, true)).Step(54, f => EnterChord(f, false))
                .Cue(14, AuraCue).Cue(16, HiltSlamCue).Cue(16, CageCloudsCue).Cue(30, CageCue).Cue(42, AuraCue)
                .Every(GhostCue, 44, 50, 2).Every(GhostCue, 58, 64, 2).Cue(50, CageSliceCue).Cue(64, CageSliceCue).Cue(64, CageShatterCue)
                .Fx(43, 51, FxKind.TrailBlade).Fx(57, 65, FxKind.TrailBlade)
                .Impulse(16, new Vector2(2f, -3f)).Impulse(40, new Vector2(-6f, 2f)).Impulse(44, new Vector2(-8f, 1.5f)).Impulse(54, new Vector2(-5f, 2f)).Impulse(58, new Vector2(-8f, 1.5f))
                .Sword().Fixed();

            // Zenith Guillotine: a crouch and a spring, then gone upward (invulnerable f14–34) while a cloud and a crackling ring follow Kage; the ring locks
            // at f30, he reappears over it at f34 and drops blade-first (f38: parry it), and the landing discharges a ground wave each way at f52 (jump it).
            ZenithGuillotine = new MoveDef("raikon.zenithGuillotine", "Zenith Guillotine", 90)
                .Key(6, Hold(P().Hip(-0.02f, -0.36f, 4f).Chest(-20f).Head(12f).Feet(0.32f, -0.34f), 0.1f, 1.0f, 100f), Ease.OutQuad)
                .Key(13, Hold(P().Hip(0f, -0.02f, 2f).Chest(10f).Head(-14f).Feet(0.28f, -0.3f), 0.06f, 1.85f, 92f), Ease.OutExpo)
                .Key(34, Hold(P().Hip(0f, 0.02f, -6f).Chest(-14f).Head(10f).LegN(40f, -80f).LegF(30f, -90f), 0.3f, 0.9f, -88f))
                .Key(38, Slam(), Ease.InCubic)
                .Key(50, Slam())
                .Key(53, Hold(P().Hip(0.02f, -0.3f, -4f).Chest(-10f).Head(-6f).Feet(0.4f, -0.36f), 0.4f, 0.6f, -80f), Ease.OutExpo)
                .Key(90, Ready(), Ease.InOutQuad)
                .Vanish(14, 33).Invuln(14, 34).NoPush(14, 38)
                .Hit(37, 41, Blade, Dmg(14), HitLevel.Overhead, new Vector2(3.5f, 2f), stun: 30, hitstop: 12, shake: 0.5f, knockdown: true, heavy: true, radius: 0.34f).Unguardable()
                .Throw(52, Wave, new Vector2(0.9f, Wave.radius)).ThrowBack(52, Wave, new Vector2(0.9f, Wave.radius))
                .Step(14, ZenithRise).Step(30, ZenithLock).Step(34, ZenithDrop)
                .Cue(4, EyeCue).Cue(14, ZenithColumnCue).Cue(14, ZenithRingCue).Cue(24, EyeCloudCue).Cue(34, MeteorCue).Cue(38, SlamCue)
                .Cue(40, AuraCue).Cue(46, AuraCue).Cue(52, DischargeCue)
                .Fx(36, 42, FxKind.TrailBlade).Fx(38, 38, FxKind.Dust).Fx(38, 38, FxKind.Shockwave)
                .Impulse(13, new Vector2(0f, 5f)).Impulse(34, new Vector2(0f, 4f)).Impulse(38, new Vector2(3f, -3f)).Impulse(52, new Vector2(0f, 3f))
                .Sword().Fixed();

            // Mirage Severance: he splits into two silhouettes at f16; the fake dashes in at f26 (no damage) and dissolves into red ash, while the real one
            // blinks behind Kage and cuts at f36, a slash that detonates 0.25 s later. Only the real one keeps the dim red eye trail.
            MirageSeverance = new MoveDef("raikon.mirageSeverance", "Mirage Severance", 73)
                .Key(8, Hold(P().Hip(-0.02f, -0.2f, 4f).Chest(-10f).Head(8f).Feet(0.3f, -0.34f), -0.05f, 0.9f, -168f), Ease.OutQuad)
                .Key(16, Hold(P().Hip(-0.03f, -0.22f, 4f).Chest(-12f).Head(9f).Feet(0.3f, -0.34f), -0.06f, 0.88f, -170f))
                .Key(26, IaiCrouch(), Ease.InOutQuad)
                .Key(34, IaiCrouch(0.03f))
                .Key(37, Hold(Lunge(0.12f, -0.18f, 0.46f, -0.32f), 0.62f, 1.05f, -20f), Ease.OutExpo)
                .Key(41, Hold(Lunge(0.12f, -0.18f, 0.46f, -0.32f), 0.55f, 0.95f, -36f))
                .Key(73, Ready(), Ease.InOutQuad)
                .Vault(26, 34, 1.1f, 0f, 0.7f, 0.4f)            // a flat blink along the floor: no hop
                .Invuln(16, 34).NoPush(16, 36)
                .Hit(36, 39, Blade, Dmg(10), HitLevel.Mid, new Vector2(3.5f, 1f), stun: 26, hitstop: 9, shake: 0.3f, radius: BladeRadius).Unguardable()
                .HazardAlso(36, MirageBurst, 0.9f)
                .Cue(10, EyeCue).Cue(20, EyeCue).Cue(16, MirageSplitCue).Every(GhostCue, 26, 34, 2)
                .Fx(35, 40, FxKind.TrailBlade)
                .Impulse(16, new Vector2(0f, 2f)).Impulse(26, new Vector2(-7f, 2f)).Impulse(36, new Vector2(4f, 1f))
                .Sword().Fixed();
        }

        static MoveDef Sheathe(bool storm)
        {
            var m = new MoveDef(storm ? "raikon.sheatheCross.storm" : "raikon.sheatheCross", "Sheathe Cross", 72)
                .Key(6, Hold(Base(-0.08f, -0.16f, 8f), -0.05f, 0.95f, -150f), Ease.OutQuad)
                .Key(12, Hold(P().Hip(-0.06f, -0.26f, 6f).Chest(-14f).Head(10f).Feet(0.32f, -0.34f), 0.02f, 0.9f, -165f), Ease.InOutQuad)
                .Key(22, Hold(P().Hip(-0.08f, -0.3f, 6f).Chest(-18f).Head(12f).Feet(0.32f, -0.36f), 0.0f, 0.88f, -165f))
                .Key(26, Hold(Lunge(0.14f, -0.16f, 0.46f, -0.3f), 0.62f, 1.2f, 8f), Ease.OutExpo)
                .Key(31, Hold(Lunge(0.14f, -0.16f, 0.46f, -0.3f), 0.58f, 1.25f, 20f))
                .Key(37, Hold(Lunge(0.1f, -0.14f, 0.42f, -0.28f).Chest(-20f), 0.1f, 1.0f, 172f), Ease.OutExpo)
                .Key(42, Hold(Lunge(0.1f, -0.14f, 0.42f, -0.28f).Chest(-18f), 0.06f, 1.02f, 176f))
                .Key(72, Ready(), Ease.InOutQuad)
                .Move(2, 12, -4.5f).Move(19, 23, 8.4f)
                .Sheath(8, 23)
                .Hit(24, 27, Blade, Dmg(8), HitLevel.Mid, new Vector2(1.5f, 0.5f), stun: 24, hitstop: 7, shake: 0.2f, radius: BladeRadius)
                .Hit(35, 38, Blade, Dmg(10), HitLevel.Mid, new Vector2(3.5f, 1f), stun: 26, hitstop: 9, shake: 0.3f, radius: BladeRadius)
                .Cue(2, BackstepDustCue).Cue(12, SteelSayaCue).Cue(22, SteelHandCue).Cue(24, DrawArcCue).Cue(35, DrawArcReverseCue)
                .Fx(23, 28, FxKind.TrailBlade).Fx(34, 39, FxKind.TrailBlade)
                .Impulse(2, new Vector2(3f, 1f)).Impulse(24, new Vector2(-4f, 1f)).Impulse(35, new Vector2(3f, 1.5f))
                .Sword().Fixed();
            if (storm) m.HazardAlso(35, StaticCross, StaticCrossAhead);
            return m;
        }

        /// <summary>Where Sheathe Cross's two draws cross, metres ahead of his feet at chest height (the Static Cross stands there).</summary>
        public const float StaticCrossAhead = 1.0f, StaticCrossHeight = 1.07f;

        static MoveDef Press(bool storm)
        {
            var m = new MoveDef(storm ? "raikon.tsubaPress.storm" : "raikon.tsubaPress", "Tsuba Press", 50)
                .Key(6, Hold(Base(0f, -0.12f, 4f), 0.2f, 1.1f, 150f), Ease.OutQuad)
                .Key(14, Hold(Lunge(0.12f, -0.1f, 0.42f, -0.2f), 0.52f, 1.2f, 140f), Ease.OutExpo)
                .Key(19, Hold(Lunge(0.12f, -0.1f, 0.42f, -0.2f), 0.5f, 1.2f, 142f))
                .Key(50, Ready(), Ease.InOutQuad)
                .Move(6, 15, 4.5f)
                .Hit(15, 18, Hilt, 1, HitLevel.Mid, new Vector2(0.5f, 0f), stun: 22, hitstop: 7, shake: 0.2f, radius: 0.16f,
                    postureMul: HisameAbilities.PostureMul(TsubaPostureGb, 1))
                .Cue(3, SteelHandCue).Cue(13, AirConeCue)
                .Impulse(15, new Vector2(2f, 0.5f))
                .Sword().Fixed();
            m.onContact = storm ? OverheadCleaveStorm : OverheadCleave;
            return m;
        }

        /// <summary>Tsuba Press's posture cost on a guard, in JS guard-break units (Static Palm's is 110): a heavy blow's worth for a point of damage.</summary>
        public const double TsubaPostureGb = 95;

        static MoveDef Cleave(bool storm)
        {
            var m = new MoveDef(storm ? "raikon.overheadCleave.storm" : "raikon.overheadCleave", "Overhead Cleave", 70)
                .Key(6, Raised(), Ease.InOutQuad)
                .Key(16, OverheadHigh(), Ease.InOutQuad)
                .Key(27, OverheadHigh())
                .Key(31, Slam(), Ease.InCubic)
                .Key(44, Slam())
                .Key(70, Ready(), Ease.InOutQuad)
                .Move(26, 30, 2f)
                .Hit(30, 34, Blade, Dmg(15), HitLevel.Overhead, new Vector2(4f, 2f), stun: 30, hitstop: 12, shake: 0.5f, knockdown: true, heavy: true, radius: BladeRadius)
                .Cue(0, RingSparkCue).Cue(12, SteelCue).Cue(29, WindSplitCue).Cue(31, SlamCue)
                .Fx(28, 35, FxKind.TrailBlade).Fx(31, 31, FxKind.Dust)
                .Impulse(4, new Vector2(-1f, 3f)).Impulse(31, new Vector2(3f, -2.5f))
                .Sword().Fixed();
            if (storm) m.HazardAlso(31, CleaveVee, CleaveVeeAhead);
            return m;
        }

        /// <summary>The cleave's V of spark lines: its centre, metres ahead of his feet (it covers 0.3–3.1 m).</summary>
        public const float CleaveVeeAhead = 1.7f;

        static MoveDef Drag(bool storm)
        {
            var dragging = Hold(Lunge(0.12f, -0.28f, 0.5f, -0.4f).Chest(-34f), 0.08f, 0.6f, -156f);
            var m = new MoveDef(storm ? "raikon.fulminationDrag.storm" : "raikon.fulminationDrag", "Fulmination Drag", 80)
                .Key(10, Hold(P().Hip(0.04f, -0.3f, -10f).Chest(-28f).Head(16f).Feet(0.42f, -0.34f), 0.12f, 0.62f, -152f), Ease.OutQuad)
                .Key(17, Hold(P().Hip(0.04f, -0.32f, -10f).Chest(-30f).Head(16f).Feet(0.42f, -0.36f), 0.1f, 0.6f, -154f))
                .Key(20, dragging, Ease.OutExpo)
                .Key(34, Hold(Lunge(0.12f, -0.28f, 0.5f, -0.4f).Chest(-34f), 0.08f, 0.6f, -156f))
                .Key(38, Hold(P().Hip(0.06f, -0.36f, -6f).Chest(-30f).Head(16f).Feet(0.44f, -0.34f), 0.3f, 0.5f, -60f), Ease.InOutQuad)
                .Key(41, Hold(P().Hip(0.06f, -0.04f, -4f).Chest(12f).Head(-12f).FootN(0.34f, 0.13f).FootF(-0.2f, 0.18f, -20f), 0.45f, 1.7f, 80f), Ease.OutExpo)
                .Key(46, Hold(P().Hip(0.06f, -0.05f, -4f).Chest(10f).Head(-10f).FootN(0.34f, 0.13f).FootF(-0.2f, 0.18f, -20f), 0.42f, 1.68f, 84f))
                .Key(80, Ready(), Ease.InOutQuad)
                .Move(18, 34, DragSpeed)
                .Hit(40, 44, Blade, Dmg(12), HitLevel.Unblockable, new Vector2(1.4f, 6.2f), stun: 30, hitstop: 10, shake: 0.4f, launch: true, radius: 0.32f)
                .Every(AuraCue, 4, 34, 6).Every(RoosterCue, 18, 34, 2).Cue(40, NeedleFanCue)
                .Fx(39, 45, FxKind.TrailBlade)
                .Impulse(18, new Vector2(-6f, 1f)).Impulse(40, new Vector2(0f, -4f))
                .Sword().Fixed();
            if (storm) m.HazardAlso(22, GougePillar, -0.4f).HazardAlso(28, GougePillar, -0.4f).HazardAlso(34, GougePillar, -0.4f);
            return m;
        }

        /// <summary>Fulmination Drag's rush: 17 frames at this speed, about 6 m (it stops at Kage's body).</summary>
        public const float DragSpeed = 21f;

        static MoveDef Gale(bool storm)
        {
            var m = new MoveDef(storm ? "raikon.galeSever.storm" : "raikon.galeSever", "Gale Sever", storm ? 88 : 72)
                .Key(8, Hold(P().Hip(-0.06f, -0.14f, 10f).Chest(16f).Head(-8f).Feet(0.32f, -0.34f), -0.2f, 1.7f, 130f), Ease.OutQuad)
                .Key(18, Hold(P().Hip(-0.08f, -0.15f, 11f).Chest(18f).Head(-9f).Feet(0.32f, -0.36f), -0.24f, 1.74f, 134f))
                .Key(22, Hold(Lunge(0.12f, -0.18f, 0.46f, -0.32f), 0.6f, 0.8f, -50f), Ease.OutExpo)
                .Key(27, Hold(Lunge(0.12f, -0.18f, 0.46f, -0.32f), 0.5f, 0.7f, -70f))
                .Key(32, Hold(P().Hip(-0.02f, -0.34f, 4f).Chest(-20f).Head(12f).Feet(0.36f, -0.36f), -0.2f, 0.55f, -170f), Ease.InOutQuad)
                .Key(36, Hold(P().Hip(-0.03f, -0.35f, 4f).Chest(-21f).Head(12f).Feet(0.36f, -0.36f), -0.22f, 0.54f, -172f))
                .Key(40, Hold(P().Hip(0.08f, -0.38f, -6f).Chest(-28f).Head(16f).Feet(0.46f, -0.36f), 0.62f, 0.4f, -6f), Ease.OutExpo)
                .Key(44, Hold(P().Hip(0.08f, -0.38f, -6f).Chest(-28f).Head(16f).Feet(0.46f, -0.36f), 0.58f, 0.42f, -10f));
            if (storm)
                m.Key(50, Hold(P().Hip(-0.06f, -0.16f, 8f).Chest(12f).Head(-6f).Feet(0.32f, -0.34f), -0.25f, 1.4f, 150f), Ease.InOutQuad)
                 .Key(55, Hold(Lunge(0.12f, -0.16f, 0.46f, -0.32f), 0.62f, 1.0f, -20f), Ease.OutExpo)
                 .Key(59, Hold(Lunge(0.12f, -0.16f, 0.46f, -0.32f), 0.58f, 0.96f, -28f));
            m.Key(m.total, Ready(), Ease.InOutQuad)
                .Hit(20, 24, Blade, Dmg(9), HitLevel.Mid, new Vector2(2f, 0.8f), stun: 26, hitstop: 8, shake: 0.3f, radius: BladeRadius).Unguardable()
                .Throw(20, VacuumBlade, new Vector2(0.9f, 1.0f))
                .ThrowAlso(38, LowCrescent, new Vector2(0.9f, 0.3f))
                .Cue(4, EyeCue).Cue(24, EyeCue)
                .Fx(19, 25, FxKind.TrailBlade).Fx(37, 42, FxKind.TrailBlade)
                .Impulse(20, new Vector2(3f, 1f)).Impulse(38, new Vector2(2.5f, -1f))
                .Sword().Fixed();
            if (storm)
                m.ThrowAlso(54, XCrescent, new Vector2(0.9f, 1.0f)).HazardAlso(54, StaticResidue, ResidueAhead)
                 .Cue(40, EyeCue).Fx(53, 58, FxKind.TrailBlade).Impulse(54, new Vector2(3f, 1f));
            return m;
        }

        /// <summary>Gale Sever's residue: its centre ahead of his feet (it covers 0.55–5.05 m), and how long it burns.</summary>
        public const float ResidueAhead = 2.8f;
        public const int ResidueFrames = 72;

        static void BuildExpansionHazards()
        {
            // Sheathe Cross (P2): the X where the draws met hangs 0.4 s, crackling, then snaps once
            StaticCross = new HazardDef
            {
                id = "raikon.sheatheCross.staticCross", telegraphFrames = 24, strikeFrames = 4, size = new Vector2(1.2f, 1.8f),
                hit = new HitWindow { dmg = Dmg(5), level = HitLevel.Unblockable, kb = new Vector2(1.5f, 2.5f), stun = 22, hitstop = 7, shake = 0.22f },
                telegraphCue = StaticCrossMarkCue, strikeCue = StaticCrossSnapCue,
            };
            // the cleave (P2): two diagonal floor spark lines in a V ahead of the blade, erupting 0.3 s later
            CleaveVee = new HazardDef
            {
                id = "raikon.overheadCleave.vee", telegraphFrames = 18, strikeFrames = 5, size = new Vector2(2.8f, 0.6f),
                hit = new HitWindow { dmg = Dmg(6), level = HitLevel.Unblockable, kb = new Vector2(1.5f, 3.5f), stun = 24, hitstop = 6, shake = 0.22f },
                telegraphCue = VeeMarkCue, strikeCue = VeeStrikeCue,
            };
            // Fulmination Drag (P3): a pillar rising out of the gouge 0.35 s after he passed
            GougePillar = new HazardDef
            {
                id = "raikon.fulminationDrag.pillar", telegraphFrames = 21, strikeFrames = 5, size = new Vector2(0.9f, 3.0f),
                hit = new HitWindow { dmg = Dmg(6), level = HitLevel.Unblockable, kb = new Vector2(1f, 4.5f), stun = 24, hitstop = 8, shake = 0.3f, launch = true },
                telegraphCue = GougeMarkCue, strikeCue = GougePillarCue,
            };
            // Arc Anchor: the radial extraction slash round him where he arrives (laid by the blink, strikes 2 frames later)
            RadialSlash = new HazardDef
            {
                id = "raikon.arcAnchor.radial", telegraphFrames = 2, strikeFrames = 4, size = new Vector2(2.8f, 2.2f),
                hit = new HitWindow { dmg = Dmg(11), level = HitLevel.Unblockable, kb = new Vector2(3.5f, 3f), stun = 28, hitstop = 10, shake = 0.45f, knockdown = true, heavy = true },
                telegraphCue = null, strikeCue = RadialSlashCue,
            };
            // Gale Sever (P3): the X-crescent's burning residue on the floor, 1.2 s, one light zap
            StaticResidue = new HazardDef
            {
                id = "raikon.galeSever.residue", telegraphFrames = 10, strikeFrames = ResidueFrames - 10, size = new Vector2(4.5f, 0.35f),
                hit = new HitWindow { dmg = Dmg(3), level = HitLevel.Unblockable, kb = new Vector2(0.8f, 1.6f), stun = 16, hitstop = 4, shake = 0.1f },
                telegraphCue = ResidueCue, strikeCue = null,
            };
            // Tempest Cage: a side of the ring (two spears): it lands at f30 and zaps once whoever touches it until the second chord, knocking them away
            // from it (back in, from inside)
            CageSpear = new HazardDef
            {
                id = "raikon.tempestCage.spear", telegraphFrames = 0, strikeFrames = 32, size = new Vector2(0.55f, 2.4f),
                hit = new HitWindow { dmg = Dmg(7), level = HitLevel.Unblockable, kb = new Vector2(3.2f, 1.8f), stun = 20, hitstop = 8, shake = 0.3f },
                telegraphCue = null, strikeCue = null,
            };
            // Mirage Severance: the slash's mark detonates 0.25 s after the cut
            MirageBurst = new HazardDef
            {
                id = "raikon.mirageSeverance.burst", telegraphFrames = 15, strikeFrames = 4, size = new Vector2(1.4f, 2.0f),
                hit = new HitWindow { dmg = Dmg(5), level = HitLevel.Unblockable, kb = new Vector2(1.2f, 3f), stun = 22, hitstop = 6, shake = 0.25f },
                telegraphCue = MirageMarkCue, strikeCue = MirageBurstCue,
            };

            // Arc Anchor's tachi: blockable and parryable (a parry knocks it down), a hit pins Kage for 0.6 s
            AnchorBlade = new ProjectileDef
            {
                id = "raikon.arcAnchor.blade", speed = AnchorSpeed, lifeFrames = AnchorFlightFrames, radius = 0.3f, maxHits = 1,
                hit = new HitWindow { dmg = Dmg(8), level = HitLevel.Mid, kb = new Vector2(0.4f, 0f), stun = 36, hitstop = 8, shake = 0.3f, fixedDamage = true },
                flightCue = AnchorFlightCue, endCue = AnchorLandCue,
                onEnd = (p, at) =>
                {
                    var s = StateOf(p.Owner);
                    s.anchorEnded = true;
                    s.anchorDeflected = p.Deflected;
                    s.anchorAt = at;
                },
            };
            // Gale Sever: the vertical vacuum blade (parry it), the low crescent (jump it), the X-crescent (P3)
            VacuumBlade = new ProjectileDef
            {
                id = "raikon.galeSever.vacuum", speed = 16f, lifeFrames = 40, radius = 0.3f, maxHits = 1,
                samples = new[] { new Vector2(0f, -0.5f), Vector2.zero, new Vector2(0f, 0.5f) },
                hit = new HitWindow { dmg = Dmg(7), level = HitLevel.Mid, unguardable = true, kb = new Vector2(2.5f, 1f), stun = 24, hitstop = 7, shake = 0.25f, fixedDamage = true },
                flightCue = VacuumBladeCue, endCue = CrescentEndCue,
            };
            LowCrescent = new ProjectileDef
            {
                id = "raikon.galeSever.low", speed = 13f, lifeFrames = 46, radius = 0.25f, maxHits = 1, groundWave = true,
                samples = new[] { new Vector2(-0.45f, 0f), Vector2.zero, new Vector2(0.45f, 0f) },
                hit = new HitWindow { dmg = Dmg(6), level = HitLevel.Low, unguardable = true, kb = new Vector2(1.5f, 2f), stun = 22, hitstop = 6, shake = 0.2f, fixedDamage = true },
                flightCue = LowCrescentCue, endCue = CrescentEndCue,
            };
            XCrescent = new ProjectileDef
            {
                id = "raikon.galeSever.x", speed = 14f, lifeFrames = 44, radius = 0.32f, maxHits = 1,
                samples = new[] { new Vector2(0f, -0.45f), Vector2.zero, new Vector2(0f, 0.45f) },
                hit = new HitWindow { dmg = Dmg(8), level = HitLevel.Mid, unguardable = true, kb = new Vector2(2.5f, 1.5f), stun = 24, hitstop = 7, shake = 0.25f, fixedDamage = true },
                flightCue = XCrescentCue, endCue = CrescentEndCue,
            };
        }

        // ---------------------------------------------------------------------------------------------- frame actions
        /// <summary>
        /// Arc Anchor at f50: with the blade down (it hit, was blocked, or flew its 9 m) he blinks to its hilt, facing Kage, and lays the radial
        /// extraction slash round himself; a deflected blade (or none) cancels the blink: he recalls it instead, open.
        /// </summary>
        static void AnchorArrive(Fighter f)
        {
            var s = StateOf(f);
            if (!s.anchorEnded || s.anchorDeflected) { f.StartMove(ArcAnchorRecall); return; }
            s.blinkFrom = f.Pos;
            float back = Mathf.Sign(f.Pos.x - s.anchorAt.x);                 // the hilt is the blade's end nearer to where it came from
            if (back == 0f) back = -f.facing;
            float faceX = f.opponent != null ? f.opponent.Pos.x : s.anchorAt.x - back;
            f.Warp(new Vector2(s.anchorAt.x + back * 0.6f, f.Pos.y), faceX);
            f.LayHazard(RadialSlash, f.Pos.x);
        }

        /// <summary>Tempest Cage at f15: the ring locks on Kage, kept inside the stage, and remembers which side of it Raikon stands on.</summary>
        static void LockCage(Fighter f)
        {
            var s = StateOf(f);
            float c = f.opponent != null ? f.opponent.Pos.x : f.Pos.x + f.facing * 2f;
            float lo = f.StageLeft + CageRadius * 0.8f, hi = f.StageRight - CageRadius * 0.8f;
            s.cageCentre = lo < hi ? Mathf.Clamp(c, lo, hi) : (f.StageLeft + f.StageRight) * 0.5f;
            float side = Mathf.Sign(f.Pos.x - s.cageCentre);
            s.cageSide = side != 0f ? side : -f.facing;
            s.cageLocked = true;
        }

        /// <summary>Tempest Cage at f30: the two sides of the ring (two spears each) land.</summary>
        static void DropSpears(Fighter f)
        {
            var s = StateOf(f);
            if (!s.cageLocked) LockCage(f);
            f.LayHazard(CageSpear, s.cageCentre - CageSpearAt);
            f.LayHazard(CageSpear, s.cageCentre + CageSpearAt);
        }

        /// <summary>Tempest Cage: a blink to a chord's end at the spears, facing across the ring: his own side first (f40), then the side he ran to (f54).</summary>
        static void EnterChord(Fighter f, bool first)
        {
            var s = StateOf(f);
            if (!s.cageLocked) LockCage(f);
            float side = first ? s.cageSide : Mathf.Sign(f.Pos.x - s.cageCentre);
            if (side == 0f) side = -s.cageSide;
            s.blinkFrom = f.Pos;
            f.Warp(new Vector2(s.cageCentre + side * CageChordAt, f.Pos.y), s.cageCentre);
        }

        /// <summary>Zenith Guillotine at f14: gone upward; the ring follows Kage until the lock.</summary>
        static void ZenithRise(Fighter f)
        {
            var s = StateOf(f);
            s.zenithGround = f.Pos.y;
            s.zenithLocked = false;
            s.zenithX = f.opponent != null ? f.opponent.Pos.x : f.Pos.x + f.facing * 2f;
        }

        /// <summary>Zenith Guillotine at f30: the ring locks where Kage stands.</summary>
        static void ZenithLock(Fighter f)
        {
            var s = StateOf(f);
            float x = f.opponent != null ? f.opponent.Pos.x : s.zenithX;
            s.zenithX = Mathf.Clamp(x, f.StageLeft, f.StageRight);
            s.zenithLocked = true;
        }

        /// <summary>Zenith Guillotine at f34: back in sight over the lock, on his own side of it, facing it, and falling fast (he lands about f38).</summary>
        static void ZenithDrop(Fighter f)
        {
            var s = StateOf(f);
            if (!s.zenithLocked) ZenithLock(f);
            float side = Mathf.Sign(f.Pos.x - s.zenithX);
            if (side == 0f) side = -f.facing;
            s.blinkFrom = f.Pos;
            f.Warp(new Vector2(s.zenithX + side * ZenithLandOffset, s.zenithGround + ZenithDropHeight), s.zenithX);
            f.rb.linearVelocity = new Vector2(0f, -ZenithDropSpeed);
        }

        /// <summary>How far from the locked point he lands, metres: his blade line reaches it.</summary>
        public const float ZenithLandOffset = 0.6f;
    }
}
