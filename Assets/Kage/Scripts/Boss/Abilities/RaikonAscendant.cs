using System.Collections.Generic;
using Kage.Rules;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// Raikon, the Lightning Ascendant: the three-phase boss fight (docs/raikon-ascendant-vfx.md has every frame and the VFX brief). Grounded samurai
    /// martial arts that take on more crimson lightning each phase:
    /// <list type="bullet">
    /// <item><b>Phase 1, Pure Samurai</b> (100–65 %): Kesa-Giri, Pommel Check → Front Kick, Crimson Flash (iaijutsu), Low Sweep → Rising Cut, Iron Guard
    /// Counter. No lightning projectiles; only a faint static on the blade.</item>
    /// <item><b>Phase 2, Storm Blade</b> (65–30 %, opened by Storm Kindling): the phase 1 kit upgraded (Kesa-Giri's third cut lays a delayed floor spark
    /// line; Crimson Flash leaves a trailing arc) plus Raijin Arc, Thunder Step, Sky-Splitter, Static Palm and Tempest Wheel.</item>
    /// <item><b>Phase 3, Lightning Ascendant</b> (30–0 %, opened by Ascension): everything, plus Blink Flurry, Heavens' Verdict, Ascendant Rush and
    /// Afterimage Feint; once at 12 % the Final Overload.</item>
    /// </list>
    /// The expansion (RaikonAscendant.Expansion.cs) adds three moves a phase (Sheathe Cross, Tsuba Press → Overhead Cleave, Whirlwind Feint;
    /// Fulmination Drag, Arc Anchor, Gale Sever; Tempest Cage, Zenith Guillotine, Mirage Severance), their cross-phase upgrades and nine strings.
    /// <para>
    /// <b>The three-colour telegraph</b> (never mixed): the <i>steel glint</i> (<see cref="SteelCue"/>) marks a strike that can be blocked and parried;
    /// the <i>red eye flash</i> (<see cref="EyeCue"/>) a high-speed iaijutsu or blink that can't be guarded but can be parried
    /// (<see cref="HitWindow.unguardable"/>); the <i>crimson-white aura</i> (<see cref="AuraCue"/>) an unblockable lightning attack.
    /// </para>
    /// <para>
    /// Every attack is an ordinary <see cref="MoveDef"/> (ADR-0008): an ability slot, a counter answer, an on-contact follow-up, a <see cref="MoveDef.then"/>
    /// link or a phase's <see cref="PhaseDef.enterMove"/>. Raikon's skeleton, hurt circles, scale and painted blade line (ADR-0005) are untouched: every
    /// blade window is <see cref="HitPoint.Blade"/> on <see cref="RaikonPartSet.PaintedBladeLength"/>. Damage is in JS units times
    /// <see cref="GaranAbilities.UnityPerJsDamage"/> and fixed (the tachi's multiplier does not count), as for his SAN-107 abilities.
    /// </para>
    /// </summary>
    public static partial class RaikonAscendant
    {
        // ---- ability ids (the CharacterDef.abilities slots after the four SAN-107 ones) ----
        public const string KesaGiriId = "kesaGiri", PommelKickId = "pommelKick", CrimsonFlashId = "crimsonFlash", LowSweepId = "lowSweep", IronGuardId = "ironGuard";
        public const string RaijinArcId = "raijinArc", ThunderStepId = "thunderStep", SkySplitterId = "skySplitter", StaticPalmId = "staticPalm", TempestWheelId = "tempestWheel";
        public const string BlinkFlurryId = "blinkFlurry", HeavensVerdictId = "heavensVerdict", AscendantRushId = "ascendantRush", AfterimageFeintId = "afterimageFeint";
        // ---- the expansion (2026-11-27): three more a phase (RaikonAscendant.Expansion.cs) ----
        public const string SheatheCrossId = "sheatheCross", TsubaPressId = "tsubaPress", WhirlwindFeintId = "whirlwindFeint";
        public const string FulminationDragId = "fulminationDrag", ArcAnchorId = "arcAnchor", GaleSeverId = "galeSever";
        public const string TempestCageId = "tempestCage", ZenithGuillotineId = "zenithGuillotine", MirageSeveranceId = "mirageSeverance";

        public static readonly string[] Phase1 = { KesaGiriId, PommelKickId, CrimsonFlashId, LowSweepId, IronGuardId, SheatheCrossId, TsubaPressId, WhirlwindFeintId };
        public static readonly string[] Phase2Adds = { RaijinArcId, ThunderStepId, SkySplitterId, StaticPalmId, TempestWheelId, FulminationDragId, ArcAnchorId, GaleSeverId };
        public static readonly string[] Phase3Adds = { BlinkFlurryId, HeavensVerdictId, AscendantRushId, AfterimageFeintId, TempestCageId, ZenithGuillotineId, MirageSeveranceId };

        /// <summary>Every new ability id, in slot order.</summary>
        public static IEnumerable<string> AllIds
        {
            get { foreach (var s in Phase1) yield return s; foreach (var s in Phase2Adds) yield return s; foreach (var s in Phase3Adds) yield return s; }
        }

        /// <summary>Health fractions that open phase 2, phase 3 and the Final Overload.</summary>
        public const float Phase2At = 0.65f, Phase3At = 0.30f, OverloadAt = 0.12f;

        // ---- the phases' wind (FighterRig.wind on both rigs), in Raikon's frame: -x blows into his face, so coat, hair and tassels stream behind him ----
        public static Wind Calm => Wind.Calm;
        public static Wind ModerateGust => Wind.Blowing(new Vector2(-2.2f, 0.25f), 2.6f, 71, 0.7f);
        public static Wind StormGale => Wind.Blowing(new Vector2(-4.6f, 0.45f), 5.4f, 113, 1.2f);
        /// <summary>Frames a phase shift takes to bring the wind up to the new phase's.</summary>
        public const int WindRampFrames = 70;

        // ---- moves (built by Register; public for tests) ----
        public static MoveDef KesaGiri, KesaGiriStorm, PommelKick, CrimsonFlash, CrimsonFlashStorm, LowSweep, RisingCut, IronGuard, IronGuardSlash;
        public static MoveDef RaijinArc, ThunderStep, SkySplitter, StaticPalm, TempestWheel;
        public static MoveDef BlinkFlurry, BlinkFlurry2, BlinkFlurry3, HeavensVerdict, AscendantRush, AfterimageFeint;
        public static MoveDef StormKindling, Ascension, FinalOverload;

        // ---- hazards and the wave ----
        public static HazardDef FloorSparkLine, TrailArc, VerticalBolt, SkyBolt, FlurrySlash, FlurrySlam, DecoyPop, PlantShock, SpiralKnock;
        public static ProjectileDef Wave;

        static float _scale = -1f;

        static int Dmg(double js) => Mathf.Max(1, Mathf.RoundToInt((float)js * GaranAbilities.UnityPerJsDamage));

        /// <summary>Builds the moves for a Raikon of build scale <paramref name="scale"/> and registers the abilities. Idempotent.</summary>
        public static void Register(float scale)
        {
            Build(scale);
            Reg(KesaGiriId, KesaGiri, 3.0f, 0.9f, 2.4f, 0.5f).move.variantFor = f => PhaseOf(f) >= 1 ? KesaGiriStorm : null;
            Reg(PommelKickId, PommelKick, 4.0f, 0f, 1.15f, 0.55f);
            Reg(CrimsonFlashId, CrimsonFlash, 6.0f, 3.0f, 9.5f, 0.45f).move.variantFor = f => PhaseOf(f) >= 1 ? CrimsonFlashStorm : null;
            Reg(LowSweepId, LowSweep, 3.5f, 0.8f, 2.2f, 0.45f);
            var iron = Reg(IronGuardId, IronGuard, 5.0f, 0f, 2.1f, 0.7f);
            iron.reactive = true;
            iron.when = (self, opp) => opp != null && opp.IsAttacking && opp.LightAttacksWithin(SpamWindowFrames) >= SpamCount && Facing(self, opp);

            Reg(RaijinArcId, RaijinArc, 5.0f, 2.5f, 9.5f, 0.45f);
            Reg(ThunderStepId, ThunderStep, 5.0f, 1.5f, 5.5f, 0.4f);
            var sky = Reg(SkySplitterId, SkySplitter, 4.0f, 0f, 2.4f, 0.8f);
            sky.reactive = true;
            sky.when = (self, opp) => opp != null && !opp.Grounded && opp.Pos.y - self.Pos.y > AntiAirHeight && opp.state is not (FState.Launched or FState.Thrown or FState.KO) && Facing(self, opp);
            Reg(StaticPalmId, StaticPalm, 5.0f, 0f, 1.3f, 0.5f);
            Reg(TempestWheelId, TempestWheel, 5.5f, 1.0f, 3.0f, 0.4f);

            Reg(BlinkFlurryId, BlinkFlurry, 8.0f, 2.5f, 9.5f, 0.4f);
            Reg(HeavensVerdictId, HeavensVerdict, 9.0f, 3.0f, 11f, 0.4f);
            Reg(AscendantRushId, AscendantRush, 7.0f, 0f, 1.6f, 0.5f);
            Reg(AfterimageFeintId, AfterimageFeint, FeintCooldown, 0f, 99f, 1f).escapeAfterHits = FeintAfterHits;
            RegisterExpansion();

            // a phase shows off its own kit: its new abilities come up more often than the old ones
            foreach (var id in Phase2Adds) AbilityCatalog.Get(id).weight = Phase2Weight;
            foreach (var id in Phase3Adds) AbilityCatalog.Get(id).weight = Phase3Weight;
            foreach (var id in ExpansionIds) AbilityCatalog.Get(id).weight *= ExpansionWeight;       // the expansion's moves show in a playtest
            foreach (var (from, next) in Strings) Chain(from, next);
        }

        // ---------------------------------------------------------------------------------------------- the boss strings
        /// <summary>How much more often a phase's new abilities are picked than phase 1's (<see cref="AbilityDef.weight"/>).</summary>
        public const float Phase2Weight = 1.5f, Phase3Weight = 2f;
        /// <summary>The expansion's nine moves come up half again as often as their phase's older ones, so they show in a playtest.</summary>
        public const float ExpansionWeight = 1.5f;

        /// <summary>The expansion's nine ability ids (RaikonAscendant.Expansion.cs).</summary>
        public static readonly string[] ExpansionIds =
        {
            SheatheCrossId, TsubaPressId, WhirlwindFeintId, FulminationDragId, ArcAnchorId, GaleSeverId, TempestCageId, ZenithGuillotineId, MirageSeveranceId,
        };

        /// <summary>
        /// Raikon's strings (<see cref="AbilityDef.chain"/>): what he may go straight into when an ability ends, in order of preference; the first one the
        /// phase enables, off cooldown and in its distance band is taken. Each one has a reason: the push kick throws Kage out to the iaijutsu's range,
        /// the Raijin wave makes him jump and the Crimson Flash takes him on landing, the blink puts Raikon at Kage's back for the Kesa-Giri, and so on.
        /// </summary>
        public static readonly (string from, string[] next)[] Strings =
        {
            (PommelKickId, new[] { CrimsonFlashId, RaijinArcId, LowSweepId }),
            (KesaGiriId, new[] { PommelKickId, ThunderStepId }),
            (LowSweepId, new[] { KesaGiriId, TempestWheelId }),
            (CrimsonFlashId, new[] { BlinkFlurryId, ThunderStepId }),
            (RaijinArcId, new[] { CrimsonFlashId, ThunderStepId }),
            (ThunderStepId, new[] { KesaGiriId, StaticPalmId }),
            (TempestWheelId, new[] { StaticPalmId, PommelKickId }),
            (StaticPalmId, new[] { RaijinArcId, CrimsonFlashId }),
            (BlinkFlurryId, new[] { HeavensVerdictId, ThunderStepId }),
            (HeavensVerdictId, new[] { CrimsonFlashId, AscendantRushId }),
            (AscendantRushId, new[] { ThunderStepId, BlinkFlurryId }),
            (AfterimageFeintId, new[] { AscendantRushId, KesaGiriId }),
            // the expansion's strings (docs/raikon-ascendant-vfx.md, "Boss strings")
            (SheatheCrossId, new[] { PommelKickId }),
            (TsubaPressId, new[] { LowSweepId }),                       // only when the shove whiffed (AbilityDef.chainIf)
            (WhirlwindFeintId, new[] { KesaGiriId }),
            (FulminationDragId, new[] { SkySplitterId, TempestWheelId }),
            (ArcAnchorId, new[] { StaticPalmId, ThunderStepId }),
            (GaleSeverId, new[] { RaijinArcId, CrimsonFlashId }),
            (TempestCageId, new[] { HeavensVerdictId, BlinkFlurryId }),
            (ZenithGuillotineId, new[] { AscendantRushId, StaticPalmId }),
            (MirageSeveranceId, new[] { CrimsonFlashId, BlinkFlurryId }),
        };

        /// <summary>The chance he follows a string, by phase: phase 1 sometimes, phase 3 nearly always.</summary>
        public static float ChainChance(Fighter f) => PhaseOf(f) switch { 0 => 0.45f, 1 => 0.7f, 2 => 0.85f, _ => 0.92f };

        /// <summary>The beat in frames between the end of one ability and the next of a string, by phase: readable in phase 1, a snap in phase 3.</summary>
        public static int ChainBeat(Fighter f) => PhaseOf(f) switch { 0 => 14, 1 => 9, 2 => 5, _ => 3 };

        static void Chain(string from, string[] next)
        {
            var a = AbilityCatalog.Get(from);
            if (a == null) return;
            a.chain = next;
            a.chainChance = ChainChance;
            a.chainBeat = ChainBeat;
        }

        static AbilityDef Reg(string id, MoveDef m, float cdSeconds, float near, float far, float chance)
        {
            var d = new AbilityDef { id = id, move = m, cooldownFrames = Mathf.RoundToInt(cdSeconds * 60f), range = new Vector2(near, far), chance = chance };
            AbilityCatalog.Register(d);
            return d;
        }

        /// <summary>The boss phase index of <paramref name="f"/> (0 without a phase controller).</summary>
        public static int PhaseOf(Fighter f) => f?.Phases != null ? f.Phases.Index : 0;

        static bool Facing(Fighter self, Fighter opp) => Mathf.Sign(opp.Pos.x - self.Pos.x) == self.facing;

        // ---- reaction triggers ----
        /// <summary>Iron Guard Counter answers a foe that started this many light attacks within <see cref="SpamWindowFrames"/>.</summary>
        public const int SpamCount = 3, SpamWindowFrames = 75;
        /// <summary>Sky-Splitter answers a foe in the air at least this high above him (metres, feet to feet).</summary>
        public const float AntiAirHeight = 0.3f;
        /// <summary>Afterimage Feint: out of a combo of this many hits; cooldown in seconds (the brief: 10–12 s).</summary>
        public const int FeintAfterHits = 4;
        public const float FeintCooldown = 11f;

        // ---------------------------------------------------------------------------------------------- poses
        static Pose P() => new Pose();
        static Pose Hold(Pose p, float x, float y, float blade) => p.HandF(x, y).Blade(blade).Grip();
        static Pose Carry(Pose p, float x = 0.1f, float y = 1.0f) => p.HandF(x, y).Blade(158f).HandN(0.2f, 1.24f, 10f);
        static Pose Lunge(float hx = 0.1f, float hy = -0.12f, float feetN = 0.4f, float feetF = -0.26f) => P().Hip(hx, hy, -12f).Chest(-24f).Head(12f).Feet(feetN, feetF);
        static Pose Ready() => Stance();
        static Pose Base(float hx = -0.04f, float hy = -0.12f, float rot = 6f) => P().Hip(hx, hy, rot).Chest(8f).Head(-4f).Feet(0.26f, -0.3f);
        static Pose Raised() => Hold(Base(), 0.0f, 1.5f, 140f);
        static Pose OverheadHigh() => Jodan(P().Hip(-0.02f, -0.04f, 6f).Chest(14f).Head(-12f).Feet(0.3f, -0.3f)).HandF(0.02f, 1.78f).Blade(106f);
        static Pose Slam() => Hold(P().Hip(0.12f, -0.42f, -10f).Chest(-44f).Head(22f).Feet(0.48f, -0.32f), 0.6f, 0.7f, -44f);
        static Pose Stuck() => Hold(P().Hip(0.12f, -0.44f, -10f).Chest(-46f).Head(24f).Feet(0.48f, -0.32f), 0.62f, 0.55f, -72f);
        static Pose CallSky() => Hold(P().Hip(-0.02f, -0.04f, 4f).Chest(12f).Head(-16f).Feet(0.28f, -0.3f), 0.08f, 1.8f, 98f);
        static Pose CallDown() => Hold(P().Hip(0.1f, -0.2f, -12f).Chest(-30f).Head(16f).Feet(0.44f, -0.28f), 0.5f, 0.9f, -40f);
        static Pose IaiCrouch(float deep = 0f) => Hold(P().Hip(-0.06f, -0.34f - deep, 6f).Chest(-18f - deep * 40f).Head(16f).Feet(0.34f, -0.36f), -0.12f, 0.82f - deep, -172f);
        static Pose IaiDash() => Hold(P().Hip(0.1f, -0.24f, -16f).Chest(-30f).Head(22f).Feet(0.5f, -0.4f), 0.62f, 1.02f, 4f);
        static Pose IaiEnd() => Hold(Lunge(0.1f, -0.16f, 0.44f, -0.3f), 0.52f, 1.18f, 22f);
        static Pose Kneel() => Hold(P().Hip(0.04f, -0.5f, -6f).Chest(-40f).Head(30f).FootN(0.4f, 0.13f).FootF(-0.3f, 0.15f, -60f), 0.5f, 0.45f, -70f);
        static Pose AirRaised() => Hold(P().Hip(0f, 0.02f, 0f).Chest(8f).Head(-8f).LegN(60f, -90f).LegF(40f, -100f), 0.1f, 1.65f, 110f);
        static Pose Chamber() => Carry(P().Hip(0.02f, -0.07f, 10f).Chest(8f).Head(-4f).LegN(98f, -118f, 10f).FootF(-0.16f, 0.13f));
        static Pose PushOut() => Carry(P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(96f, -4f, 40f).FootF(-0.14f, 0.13f), 0.08f, 1.02f);
        static Pose FlyingKick() => P().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(70f, -4f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).HandF(-0.15f, 1.1f).Blade(-170f);

        static readonly HitPoint[] Blade = { HitPoint.Blade };
        static readonly HitPoint[] Hilt = { HitPoint.Hilt };
        static readonly HitPoint[] FootN = { HitPoint.FootN, HitPoint.ShinN };
        static readonly HitPoint[] FootTip = { HitPoint.FootN };
        static readonly HitPoint[] Palm = { HitPoint.Fist };

        /// <summary>The blade windows' radius: a fat arc round the painted blade line (as Thunder Swing's).</summary>
        public const float BladeRadius = 0.28f;

        /// <summary>Cue <paramref name="cue"/> on every <paramref name="step"/>-th frame from <paramref name="from"/> to <paramref name="to"/>.</summary>
        static MoveDef Every(this MoveDef m, string cue, int from, int to, int step)
        {
            for (int f = from; f <= to; f += step) m.Cue(f, cue);
            return m;
        }

        /// <summary>The move's damage is the authored (JS) number on every window.</summary>
        static MoveDef Fixed(this MoveDef m)
        {
            foreach (var w in m.hits) w.fixedDamage = true;
            return m;
        }

        // ---------------------------------------------------------------------------------------------- building
        static void Build(float scale)
        {
            if (Mathf.Approximately(_scale, scale) && KesaGiri != null) return;
            _scale = scale;
            BuildHazards();

            // ==================================================================== PHASE 1: PURE SAMURAI
            // Kesa-Giri: a diagonal down cut, a reverse rising cut, then a delayed overhead cleave. 0.35 s to the first, 0.45 s held before the third.
            KesaGiri = Kesa(false);
            KesaGiriStorm = Kesa(true);

            // Pommel Check → Front Kick: the hilt (a fast stagger) then a push kick that throws Kage back to tachi range.
            PommelKick = new MoveDef("raikon.pommelKick", "Pommel Check", 52)
                .Key(6, Hold(Base(0f, -0.1f, 4f), 0.08f, 1.0f, 172f), Ease.OutQuad)
                .Key(12, Hold(Lunge(0.1f, -0.1f, 0.38f, -0.18f), 0.58f, 1.18f, 176f), Ease.OutExpo)
                .Key(16, Hold(Lunge(0.1f, -0.1f, 0.38f, -0.18f), 0.57f, 1.18f, 176f))
                .Key(20, Chamber(), Ease.InOutQuad)
                .Key(26, PushOut(), Ease.OutExpo)
                .Key(31, PushOut())
                .Key(52, Ready(), Ease.InOutQuad)
                .Move(4, 12, 3.0f)
                .Hit(11, 15, Hilt, Dmg(5), HitLevel.Mid, new Vector2(0.6f, 0.2f), stun: 26, hitstop: 7, shake: 0.18f, radius: 0.13f)
                .Hit(24, 29, FootTip, Dmg(8), HitLevel.Mid, new Vector2(6.5f, 1.2f), stun: 26, hitstop: 8, shake: 0.3f, radius: 0.13f)
                .Cue(2, SteelHandCue).Cue(17, SteelFootCue)
                .Fx(23, 29, FxKind.TrailFootN)
                .Impulse(26, new Vector2(2.5f, 0.5f))
                .Sword().Fixed();

            // Crimson Flash: a low iaijutsu crouch with two red eye glints (0.55 s), then a dash-slash across ~60 % of the arena; 0.8 s open after.
            CrimsonFlash = Flash(false);
            CrimsonFlashStorm = Flash(true);

            // Low Sweep → Rising Cut: a low cut with sparks off the floor (beats a standing guard), then a rising launch cut on hit or block.
            RisingCut = new MoveDef("raikon.risingCut", "Rising Cut", 40)
                .Key(4, Hold(P().Hip(0.04f, -0.36f, -4f).Chest(-26f).Head(14f).FootN(0.32f, 0.13f).FootF(-0.3f, 0.15f, -30f), 0.5f, 0.56f, -10f))
                .Key(9, Hold(P().Hip(0.08f, -0.06f, -2f).Chest(4f).Head(-6f).Feet(0.36f, -0.24f), 0.48f, 1.55f, 78f), Ease.OutExpo)
                .Key(15, Hold(P().Hip(0.08f, -0.06f, -2f).Chest(4f).Head(-6f).Feet(0.36f, -0.24f), 0.46f, 1.6f, 84f))
                .Key(40, Ready(), Ease.InOutQuad)
                .Hit(5, 10, Blade, Dmg(9), HitLevel.Mid, new Vector2(1.6f, 5.8f), stun: 30, hitstop: 9, shake: 0.3f, launch: true, radius: BladeRadius)
                .Fx(3, 11, FxKind.TrailBlade).Impulse(6, new Vector2(-1f, -3.5f))
                .Sword().Fixed();
            LowSweep = new MoveDef("raikon.lowSweep", "Low Sweep", 53)
                .Key(10, Hold(P().Hip(-0.04f, -0.4f, 0f).Chest(-26f).Head(14f).FootN(0.22f, 0.13f).FootF(-0.3f, 0.15f, -30f), -0.1f, 0.66f, 176f), Ease.InOutQuad)
                .Key(22, Hold(P().Hip(-0.06f, -0.42f, 2f).Chest(-28f).Head(16f).FootN(0.22f, 0.13f).FootF(-0.3f, 0.15f, -30f), -0.14f, 0.62f, 178f))
                .Key(26, Hold(P().Hip(0.06f, -0.42f, -6f).Chest(-32f).Head(18f).FootN(0.32f, 0.13f).FootF(-0.3f, 0.15f, -30f), 0.55f, 0.5f, -4f), Ease.OutExpo)
                .Key(32, Hold(P().Hip(0.06f, -0.42f, -6f).Chest(-32f).Head(18f).FootN(0.32f, 0.13f).FootF(-0.3f, 0.15f, -30f), 0.5f, 0.52f, -2f))
                .Key(53, Ready(), Ease.InOutQuad)
                .Move(19, 26, 3.5f)
                .Hit(23, 27, Blade, Dmg(8), HitLevel.Low, new Vector2(1.2f, 0f), stun: 26, hitstop: 7, shake: 0.2f, radius: BladeRadius)
                .Cue(9, SteelCue).Cue(24, GroundSparkCue)
                .Fx(21, 28, FxKind.TrailBlade)
                .Sword().Fixed();
            LowSweep.onContact = RisingCut;

            // Iron Guard Counter: a vertical blade guard held 0.5 s; one light blow from the front is turned aside and answered with a sharp slash.
            IronGuardSlash = new MoveDef("raikon.ironGuardSlash", "Iron Guard Counter", 30)
                .Key(3, Raised())
                .Key(8, Hold(Lunge(), 0.5f, 1.0f, -30f), Ease.OutExpo)
                .Key(14, Hold(Lunge(), 0.46f, 0.95f, -40f))
                .Key(30, Ready(), Ease.InOutQuad)
                .Move(4, 8, 4f)
                .Hit(5, 9, Blade, Dmg(11), HitLevel.Mid, new Vector2(4f, 1f), stun: 24, hitstop: 9, shake: 0.3f, radius: BladeRadius)
                .Cue(0, SteelCue).Fx(4, 10, FxKind.TrailBlade)
                .Sword().Fixed();
            IronGuard = new MoveDef("raikon.ironGuard", "Iron Guard", 42)
                .Key(4, Hold(P().Hip(-0.03f, -0.12f, -2f).Chest(-2f).Head(-4f).Feet(0.22f, -0.3f), 0.3f, 1.2f, 90f), Ease.OutExpo)
                .Key(34, Hold(P().Hip(-0.03f, -0.13f, -2f).Chest(-2f).Head(-4f).Feet(0.22f, -0.3f), 0.3f, 1.19f, 92f))
                .Key(42, Ready(), Ease.InOutQuad)
                .Cue(1, SteelCue)
                .Sword();
            IronGuard.counterFrom = 4;
            IronGuard.counterTo = 34;
            IronGuard.counterFrontOnly = true;
            IronGuard.counterLightOnly = true;
            IronGuard.counterAnswer = IronGuardSlash;

            // ==================================================================== PHASE 2: STORM BLADE
            // Raijin Arc: 0.45 s raised to the sky (the aura: the wave is unblockable), the slam, a ground-skimming wave (jump it); 0.55 s open.
            RaijinArc = new MoveDef("raikon.raijinArc", "Raijin Arc", 61)
                .Key(10, CallSky(), Ease.OutQuad)
                .Key(26, Hold(P().Hip(-0.02f, -0.02f, 6f).Chest(14f).Head(-18f).Feet(0.28f, -0.3f), 0.06f, 1.86f, 96f))
                .Key(28, Slam(), Ease.InCubic)
                .Key(40, Slam())
                .Key(61, Ready(), Ease.InOutQuad)
                .Armour(0, 26)
                .Hit(27, 31, Blade, Dmg(11), HitLevel.Overhead, new Vector2(3f, 1.5f), stun: 24, hitstop: 10, shake: 0.45f, heavy: true, radius: BladeRadius)
                .Throw(28, Wave, new Vector2(0.9f * scale / 1.24f, Wave.radius))
                .Every(AuraCue, 3, 24, 4).Cue(28, SlamCue)
                .Fx(28, 28, FxKind.Dust).Fx(28, 28, FxKind.Shockwave).Fx(25, 30, FxKind.TrailBlade)
                .Impulse(6, new Vector2(-1.5f, 3.5f)).Impulse(28, new Vector2(3f, -2.5f))
                .Sword().Fixed();

            // Thunder Step: 0.3 s of boot static and an eye flash, a 0.18 s blink behind Kage (ghosts), a chambered push kick, a horizontal slash
            // (upright throughout: the old spinning back-kick is a straight kick now).
            // Parrying the kick reels him, so the slash never comes.
            ThunderStep = new MoveDef("raikon.thunderStep", "Thunder Step", 78)
                .Key(8, Carry(P().Hip(-0.02f, -0.18f, 2f).Chest(-10f).Head(8f).Feet(0.26f, -0.3f)), Ease.OutQuad)
                .Key(17, Carry(P().Hip(-0.03f, -0.22f, 2f).Chest(-14f).Head(10f).Feet(0.28f, -0.32f)))
                .Key(24, Carry(DashFwd()), Ease.OutExpo)
                .Key(29, Chamber())
                .Key(33, PushOut(), Ease.OutExpo)
                .Key(37, PushOut())
                .Key(41, Raised(), Ease.InOutQuad)
                .Key(46, Hold(Lunge(), 0.52f, 1.12f, 0f), Ease.OutExpo)
                .Key(52, Hold(Lunge(), 0.48f, 1.16f, 12f))
                .Key(78, Ready(), Ease.InOutQuad)
                .Vault(18, 29, 1.0f, 0f, 0.7f, 0.4f)          // a flat blink along the floor: no hop
                .Invuln(17, 30).NoPush(17, 31)
                .Hit(32, 36, FootN, Dmg(7), HitLevel.Mid, new Vector2(1.5f, 0.5f), stun: 26, hitstop: 7, shake: 0.2f, radius: 0.13f)
                .Hit(44, 48, Blade, Dmg(11), HitLevel.Mid, new Vector2(4.5f, 1f), stun: 26, hitstop: 9, shake: 0.32f, radius: BladeRadius)
                .Cue(2, EyeCue).Every(BootStaticCue, 3, 15, 3).Every(GhostCue, 18, 30, 2).Cue(28, SteelFootCue).Cue(38, SteelCue)
                .Fx(31, 36, FxKind.TrailFootN).Fx(43, 49, FxKind.TrailBlade)
                .Impulse(18, new Vector2(-7f, 2f)).Impulse(29, new Vector2(4f, 1f)).Impulse(32, new Vector2(-3f, 2f))
                .Sword().Fixed();

            // Sky-Splitter (anti-air): a 0.3 s crouch with the aura, an electrified rising uppercut off the floor and a thin vertical bolt; 0.65 s landing.
            SkySplitter = new MoveDef("raikon.skySplitter", "Sky-Splitter", 81)
                .Key(6, Hold(P().Hip(-0.02f, -0.34f, 2f).Chest(-22f).Head(14f).Feet(0.3f, -0.3f), 0.2f, 0.7f, -150f), Ease.OutQuad)
                .Key(17, Hold(P().Hip(-0.03f, -0.38f, 2f).Chest(-24f).Head(16f).Feet(0.3f, -0.32f), 0.18f, 0.66f, -154f))
                .Key(22, Hold(P().Hip(0.06f, 0.02f, -6f).Chest(14f).Head(-14f).FootN(0.3f, 0.13f).FootF(-0.2f, 0.2f, -20f), 0.4f, 1.8f, 84f), Ease.OutExpo)
                .Key(34, Hold(P().Hip(0.06f, 0f, -6f).Chest(12f).Head(-12f).FootN(0.3f, 0.13f).FootF(-0.2f, 0.2f, -20f), 0.38f, 1.78f, 86f))
                .Key(46, Hold(LandDeep(), 0.4f, 1.0f, 40f), Ease.InOutQuad)
                .Key(81, Ready(), Ease.InOutQuad)
                .Move(18, 19, 1.5f, 5.2f, true)
                .Hit(18, 26, Blade, Dmg(11), HitLevel.Mid, new Vector2(1.4f, 6f), stun: 30, hitstop: 10, shake: 0.35f, launch: true, radius: 0.32f)
                .Hazard(16, VerticalBolt, 0.75f)
                .Cue(2, AuraCue).Cue(7, AuraCue).Cue(12, AuraCue)
                .Fx(17, 27, FxKind.TrailBlade)
                .Impulse(18, new Vector2(0f, -4f)).Impulse(44, new Vector2(0f, -3f))
                .Sword().Fixed();

            // Static Palm (unblockable): a 0.42 s crimson-white charge in the free palm, then a guard-breaking thrust with heavy knockback.
            int palmDmg = Dmg(13);
            StaticPalm = new MoveDef("raikon.staticPalm", "Static Palm", 61)
                .Key(8, Carry(Base(-0.06f, -0.16f, 6f)).HandN(-0.12f, 1.1f, 0f), Ease.OutQuad)
                .Key(24, Carry(Base(-0.08f, -0.18f, 8f)).HandN(-0.16f, 1.08f, 0f))
                .Key(28, Carry(Lunge(0.16f, -0.16f, 0.46f, -0.32f).Chest(-26f)).HandN(0.85f, 1.15f, -80f), Ease.OutExpo)
                .Key(34, Carry(Lunge(0.16f, -0.16f, 0.46f, -0.32f).Chest(-26f)).HandN(0.82f, 1.14f, -80f))
                .Key(61, Ready(), Ease.InOutQuad)
                .Move(22, 27, 5f)
                .Hit(25, 29, Palm, palmDmg, HitLevel.Unblockable, new Vector2(8f, 2.5f), stun: 30, hitstop: 12, shake: 0.55f, knockdown: true, heavy: true, radius: 0.16f,
                    postureMul: HisameAbilities.PostureMul(110, palmDmg))
                .Every(AuraCue, 2, 22, 4).Every(PalmChargeCue, 3, 24, 3).Cue(25, PalmReleaseCue)
                .Impulse(25, new Vector2(3f, 1f))
                .Fixed();

            // Tempest Wheel: a 0.4 s tell, a lunging two-kick advance, then an electrified grounded blade sweep: a flat cut forward through
            // Kage on a step-in and the backhand cut back (no whole-body spin; the wheel cue is the blade's arc, not his body turning).
            TempestWheel = new MoveDef("raikon.tempestWheel", "Tempest Wheel", 90)
                .Key(10, Carry(P().Hip(-0.04f, -0.12f, -10f).Chest(-20f).Head(10f).Feet(0.3f, -0.3f)), Ease.OutQuad)
                .Key(22, Carry(P().Hip(-0.06f, -0.14f, -16f).Chest(-28f).Head(14f).Feet(0.3f, -0.32f)))
                .Key(27, PushOut(), Ease.OutExpo)
                .Key(31, Chamber(), Ease.InOutQuad)
                .Key(36, PushOut(), Ease.OutExpo)
                .Key(42, Hold(P().Hip(-0.04f, -0.14f, 6f).Chest(8f).Head(-4f).Feet(0.3f, -0.3f), -0.1f, 1.2f, 168f), Ease.InOutQuad)
                .Key(48, Hold(P().Hip(0.12f, -0.14f, -14f).Chest(-24f).Head(12f).Feet(0.42f, -0.28f), 0.62f, 1.1f, 2f), Ease.OutExpo)
                .Key(51, Hold(P().Hip(0.12f, -0.15f, -14f).Chest(-24f).Head(12f).Feet(0.42f, -0.28f), 0.56f, 1.02f, -14f))
                .Key(56, Hold(P().Hip(0.06f, -0.14f, -4f).Chest(-8f).Head(4f).Feet(0.4f, -0.28f), 0.4f, 1.18f, 30f), Ease.OutExpo)
                .Key(60, Hold(P().Hip(0.04f, -0.14f, -2f).Chest(-6f).Head(3f).Feet(0.38f, -0.28f), 0.34f, 1.22f, 44f))
                .Key(90, Ready(), Ease.InOutQuad)
                .Move(22, 30, 5.5f).Move(42, 56, 2f)
                .Hit(24, 28, FootTip, Dmg(6), HitLevel.Mid, new Vector2(1.2f, 0.3f), stun: 22, hitstop: 6, shake: 0.15f, radius: 0.13f)
                .Hit(34, 38, FootTip, Dmg(6), HitLevel.Mid, new Vector2(1.2f, 0.3f), stun: 22, hitstop: 6, shake: 0.15f, radius: 0.13f)
                .Hit(46, 58, Blade, Dmg(10), HitLevel.Mid, new Vector2(4.5f, 1.2f), stun: 26, hitstop: 9, shake: 0.32f, radius: 0.4f)
                .Cue(10, SteelCue).Cue(38, SteelCue).Cue(44, WheelCue).Cue(50, WheelCue).Cue(56, WheelCue)
                .Fx(23, 28, FxKind.TrailFootN).Fx(33, 38, FxKind.TrailFootN).Fx(44, 59, FxKind.TrailBlade)
                .Impulse(44, new Vector2(-4f, 2f)).Impulse(52, new Vector2(4f, 2f))
                .Sword().Fixed();

            // ==================================================================== PHASE 3: LIGHTNING ASCENDANT
            BuildFlurry(scale);

            // Heavens' Verdict: three sky bolts on rings that track Kage (0.55 s each); between the second and third he sprints in with a flying kick.
            HeavensVerdict = new MoveDef("raikon.heavensVerdict", "Heavens' Verdict", 125)
                .Key(10, CallSky(), Ease.OutQuad)
                .Key(58, CallSky())
                .Key(64, Carry(DashFwd()), Ease.OutExpo)
                .Key(74, FlyingKick(), Ease.OutExpo)
                .Key(88, FlyingKick())
                .Key(96, CallSky(), Ease.InOutQuad)
                .Key(104, CallDown(), Ease.InCubic)
                .Key(125, Ready(), Ease.InOutQuad)
                .Move(60, 72, 8f).Move(73, 74, 3f, 4.2f, true).Move(75, 84, 6f)
                .Hit(76, 86, FootTip, Dmg(9), HitLevel.Mid, new Vector2(3.5f, 1.5f), stun: 26, hitstop: 8, shake: 0.25f, radius: 0.13f)
                .HazardOnTarget(2, SkyBolt).HazardOnTargetAlso(36, SkyBolt).HazardOnTargetAlso(70, SkyBolt)
                .Every(AuraCue, 2, 58, 6).Every(AuraCue, 92, 100, 4).Cue(68, SteelFootCue)
                .Fx(74, 86, FxKind.TrailFootN)
                .Impulse(60, new Vector2(-5f, 1f)).Impulse(73, new Vector2(-2f, -3f))
                .Sword().Fixed();

            // Ascendant Rush: 0.35 s stance, then elbow, low sweep, rising lightning kick, air blade cut, heavy slam. A parry on any link (the
            // rising kick's steel glint marks the easiest) reels him and ends the chain.
            AscendantRush = new MoveDef("raikon.ascendantRush", "Ascendant Rush", 106)
                .Key(8, Carry(P().Hip(-0.04f, -0.16f, 2f).Chest(-8f).Head(6f).Feet(0.3f, -0.32f)), Ease.OutQuad)
                .Key(20, Carry(P().Hip(-0.05f, -0.18f, 4f).Chest(-10f).Head(6f).Feet(0.3f, -0.32f)))
                .Key(23, Carry(P().Hip(0.12f, -0.1f, -20f).Chest(-30f).Head(12f).Feet(0.38f, -0.24f)).HandN(0.4f, 1.25f, -90f), Ease.OutExpo)
                .Key(28, Carry(P().Hip(0f, -0.36f, 4f).Chest(-14f).Head(10f).LegN(80f, -100f, 10f).FootF(-0.26f, 0.15f, -25f), 0.14f, 0.88f), Ease.InOutQuad)
                .Key(32, Carry(P().Hip(0.06f, -0.34f, 8f).Chest(-8f).Head(8f).LegN(92f, -4f, 20f).FootF(-0.26f, 0.15f, -25f), 0.12f, 0.9f), Ease.OutExpo)
                .Key(37, Carry(P().Hip(0f, -0.06f, 14f).Chest(10f).Head(-6f).LegN(140f, -90f, 10f).FootF(-0.14f, 0.13f)), Ease.InOutQuad)
                .Key(41, Carry(P().Hip(0.04f, -0.02f, 24f).Chest(20f).Head(-12f).LegN(165f, -8f, 20f).FootF(-0.12f, 0.14f)), Ease.OutExpo)
                .Key(45, Hold(P().Hip(0f, 0.02f, 6f).Chest(8f).Head(-6f).LegN(60f, -90f).LegF(40f, -100f), -0.05f, 1.4f, 160f), Ease.InOutQuad)
                .Key(50, Hold(P().Hip(0f, 0.02f, -12f).Chest(-20f).Head(10f).LegN(60f, -90f).LegF(40f, -100f), 0.58f, 1.16f, 0f), Ease.OutExpo)
                .Key(55, Hold(P().Hip(0f, 0.02f, -10f).Chest(-16f).Head(8f).LegN(60f, -90f).LegF(40f, -100f), 0.5f, 1.06f, -20f))
                .Key(60, AirRaised(), Ease.InOutQuad)
                .Key(64, Slam(), Ease.InCubic)
                .Key(76, Slam())
                .Key(106, Ready(), Ease.InOutQuad)
                .Move(19, 24, 3.5f).Move(44, 45, 1.2f, 6.2f, true).Move(58, 59, 0f, -6f, true)
                .Hit(21, 24, Palm, Dmg(5), HitLevel.Mid, new Vector2(0.8f, 0.2f), stun: 22, hitstop: 5, shake: 0.12f, radius: 0.2f)
                .Hit(30, 33, FootN, Dmg(5), HitLevel.Low, new Vector2(0.6f, 0f), stun: 22, hitstop: 5, shake: 0.12f, radius: 0.12f)
                .Hit(39, 43, FootTip, Dmg(6), HitLevel.Mid, new Vector2(0.8f, 5.2f), stun: 28, hitstop: 7, shake: 0.2f, launch: true, radius: 0.13f)
                .Hit(48, 56, Blade, Dmg(6), HitLevel.Mid, new Vector2(1f, 1.5f), stun: 26, hitstop: 6, shake: 0.18f, radius: 0.32f)
                .Hit(62, 67, Blade, Dmg(11), HitLevel.Overhead, new Vector2(4f, 2.5f), stun: 30, hitstop: 12, shake: 0.5f, knockdown: true, heavy: true, radius: BladeRadius)
                .Cue(6, SteelCue).Cue(33, SteelFootCue).Cue(40, FootArcCue).Cue(48, WheelCue).Cue(54, WheelCue).Cue(64, SlamCue)
                .Fx(29, 33, FxKind.TrailFootN).Fx(38, 43, FxKind.TrailFootN).Fx(46, 57, FxKind.TrailBlade).Fx(61, 67, FxKind.TrailBlade)
                .Fx(64, 64, FxKind.Dust).Fx(64, 64, FxKind.Shockwave)
                .Impulse(44, new Vector2(0f, -4f)).Impulse(64, new Vector2(3f, -3f))
                .Sword().Fixed();

            // Afterimage Feint (escape, phase 3): a decoy that pops for a short stun where he stood, a blink behind Kage, one quick slash.
            AfterimageFeint = new MoveDef("raikon.afterimageFeint", "Afterimage Feint", 50)
                .Key(10, Carry(DashFwd()), Ease.OutExpo)
                .Key(16, Raised(), Ease.InOutQuad)
                .Key(24, Hold(Lunge(), 0.5f, 1.0f, -30f), Ease.OutExpo)
                .Key(30, Hold(Lunge(), 0.46f, 0.95f, -40f))
                .Key(50, Ready(), Ease.InOutQuad)
                .Hazard(0, DecoyPop, 0f)
                .Vault(2, 12, 0.9f, 0f, 0.7f, 0.4f)           // a flat blink along the floor: no hop
                .Invuln(0, 20).NoPush(0, 14)
                .Hit(22, 26, Blade, Dmg(10), HitLevel.Mid, new Vector2(4f, 1f), stun: 26, hitstop: 9, shake: 0.3f, radius: BladeRadius)
                .Cue(0, DecoyCue).Every(GhostCue, 2, 12, 2).Cue(16, SteelCue)
                .Fx(21, 27, FxKind.TrailBlade)
                .Impulse(2, new Vector2(-7f, 2f))
                .Sword().Fixed();

            // ==================================================================== THE EXPANSION (three more moves a phase)
            BuildExpansion(scale);

            // ==================================================================== PHASE SHIFTS
            // Storm Kindling (into phase 2): the tachi planted in the floor, a zero-damage knockback shockwave, a sky bolt that ignites the blade.
            StormKindling = new MoveDef("raikon.stormKindling", "Storm Kindling", 84)
                .Key(10, Hold(P().Hip(0.04f, -0.36f, -4f).Chest(-30f).Head(16f).Feet(0.36f, -0.34f), 0.42f, 0.62f, -88f), Ease.InCubic)
                .Key(30, Hold(P().Hip(0.04f, -0.34f, -4f).Chest(-24f).Head(4f).Feet(0.36f, -0.34f), 0.42f, 0.64f, -88f))
                .Key(76, Hold(P().Hip(0.03f, -0.3f, -2f).Chest(-18f).Head(-4f).Feet(0.34f, -0.32f), 0.42f, 0.66f, -88f))
                .Key(84, Ready(), Ease.InOutQuad)
                .Invuln(0, 84).NoPush(0, 84)
                .Hazard(14, PlantShock, 0f)
                .Cue(6, GatherKindleCue).Cue(30, BladeIgniteCue).Every(BladeArcCue, 34, 76, 5)
                .Impulse(14, new Vector2(-3f, 5f)).Impulse(30, new Vector2(0f, 6f))
                .Sword();

            // Ascension (into phase 3): down on one knee, then a crimson spiral burst with a zero-damage knockback; the eye trail is lit from here.
            Ascension = new MoveDef("raikon.ascension", "Ascension", 80)
                .Key(12, Hold(P().Hip(0f, -0.5f, 0f).Chest(-24f).Head(20f).FootN(0.36f, 0.13f).FootF(-0.3f, 0.15f, -60f), 0.4f, 0.6f, -80f), Ease.InCubic)
                .Key(22, Hold(P().Hip(0f, -0.3f, 4f).Chest(16f).Head(-20f).Feet(0.32f, -0.3f), 0.3f, 1.0f, -110f), Ease.OutExpo)
                .Key(70, Hold(P().Hip(0f, -0.28f, 4f).Chest(14f).Head(-18f).Feet(0.32f, -0.3f), 0.3f, 1.02f, -108f))
                .Key(80, Ready(), Ease.InOutQuad)
                .Invuln(0, 80).NoPush(0, 80)
                .Hazard(20, SpiralKnock, 0f)
                .Cue(2, GatherAscendCue).Every(AuraCue, 2, 18, 4).Every(AuraCue, 30, 66, 8)
                .Impulse(20, new Vector2(-5f, 7f))
                .Sword();

            // Final Overload (once, at 12 %): 0.8 s inward pull and a dim screen, twin Raijin Arcs, two sky bolts, a full-arena unblockable iaijutsu
            // thrust, then he kneels exhausted for 1.5 s.
            FinalOverload = new MoveDef("raikon.finalOverload", "Final Overload", 220)
                .Key(8, Hold(P().Hip(0f, -0.1f, 4f).Chest(14f).Head(-16f).Feet(0.32f, -0.32f), 0.1f, 1.7f, 100f), Ease.OutQuad)
                .Key(46, Hold(P().Hip(0f, -0.08f, 6f).Chest(16f).Head(-18f).Feet(0.32f, -0.32f), 0.08f, 1.78f, 98f))
                .Key(48, Slam(), Ease.InCubic)
                .Key(60, Slam())
                .Key(72, IaiCrouch(), Ease.InOutQuad)
                .Key(108, IaiCrouch(0.04f))
                .Key(112, IaiDash(), Ease.OutExpo)
                .Key(124, IaiEnd())
                .Key(134, Kneel(), Ease.InOutQuad)
                .Key(214, Kneel())
                .Key(220, Ready(), Ease.InOutQuad)
                .Invuln(0, 48).Armour(0, 124)
                .Pull(0, 48, OverloadPull)
                .Hit(48, 51, Blade, Dmg(10), HitLevel.Overhead, new Vector2(3f, 1.5f), stun: 24, hitstop: 10, shake: 0.5f, heavy: true, radius: BladeRadius)
                .Throw(48, Wave, new Vector2(0.9f, Wave.radius)).ThrowBack(48, Wave, new Vector2(0.9f, Wave.radius))
                .HazardOnTarget(50, SkyBolt).HazardOnTargetAlso(66, SkyBolt)
                .Move(110, 123, 54f).NoPush(109, 125)
                .Hit(111, 124, Blade, Dmg(18), HitLevel.Unblockable, new Vector2(7f, 2f), stun: 34, hitstop: 14, shake: 0.7f, knockdown: true, heavy: true, radius: 0.34f)
                .Cue(0, DimCue).Cue(0, GatherOverloadCue).Every(InwardCue, 0, 44, 6).Every(AuraCue, 2, 46, 4).Cue(48, ReleaseCue).Cue(48, SlamCue)
                .Every(AuraCue, 84, 106, 4).Cue(100, EyeCue).Every(GhostCue, 110, 124, 2).Cue(124, IaiSliceCue).Cue(124, FinisherCue)
                .Fx(48, 48, FxKind.Dust).Fx(48, 48, FxKind.Shockwave).Fx(110, 124, FxKind.TrailBlade).Fx(134, 134, FxKind.Dust)
                .Impulse(0, new Vector2(0f, 4f)).Impulse(48, new Vector2(3f, -3f)).Impulse(110, new Vector2(-9f, 2f)).Impulse(124, new Vector2(6f, 1f)).Impulse(134, new Vector2(0f, -3f))
                .Sword().Fixed();
        }

        /// <summary>Final Overload's pull on Kage, m/s: less than his run, so running or dashing away beats it.</summary>
        public const float OverloadPull = 4f;

        static MoveDef Kesa(bool storm)
        {
            var m = new MoveDef(storm ? "raikon.kesaGiri.storm" : "raikon.kesaGiri", "Kesa-Giri", 103)
                .Key(8, Hold(Base(), 0.0f, 1.55f, 130f), Ease.OutQuad)
                .Key(20, Hold(Base(-0.06f, -0.14f, 8f), -0.02f, 1.6f, 134f))
                .Key(24, Hold(Lunge(0.12f, -0.14f, 0.42f, -0.28f), 0.5f, 0.95f, -40f), Ease.OutExpo)
                .Key(31, Hold(Lunge(0.12f, -0.16f, 0.42f, -0.28f), 0.44f, 0.8f, -60f), Ease.InOutQuad)
                .Key(38, Hold(Lunge(0.14f, -0.1f, 0.44f, -0.28f), 0.5f, 1.45f, 60f), Ease.OutExpo)
                .Key(50, OverheadHigh(), Ease.InOutQuad)
                .Key(61, OverheadHigh())
                .Key(65, Slam(), Ease.InCubic)
                .Key(78, Slam())
                .Key(103, Ready(), Ease.InOutQuad)
                .Move(19, 24, 2.5f).Move(33, 38, 2f).Move(60, 64, 2f)
                .Hit(21, 25, Blade, Dmg(9), HitLevel.Mid, new Vector2(1.2f, 0.4f), stun: 26, hitstop: 7, shake: 0.2f, radius: BladeRadius)
                .Hit(35, 39, Blade, Dmg(9), HitLevel.Mid, new Vector2(1.2f, 0.8f), stun: 26, hitstop: 7, shake: 0.2f, radius: BladeRadius)
                .Hit(63, 67, Blade, Dmg(14), HitLevel.Overhead, new Vector2(4f, 2f), stun: 30, hitstop: 11, shake: 0.45f, knockdown: true, heavy: true, radius: BladeRadius)
                .Cue(9, SteelCue).Cue(26, SteelCue).Cue(51, SteelCue).Cue(65, SlamCue)
                .Fx(20, 26, FxKind.TrailBlade).Fx(34, 40, FxKind.TrailBlade).Fx(62, 68, FxKind.TrailBlade).Fx(65, 65, FxKind.Dust)
                .Impulse(24, new Vector2(2f, -1f)).Impulse(38, new Vector2(1.5f, 2f)).Impulse(65, new Vector2(3f, -2.5f))
                .Sword().Fixed();
            if (storm) m.HazardAlso(63, FloorSparkLine, 1.6f).Every(BladeArcCue, 40, 60, 5);
            else m.Cue(52, BladeStaticCue);
            return m;
        }

        static MoveDef Flash(bool storm)
        {
            var m = new MoveDef(storm ? "raikon.crimsonFlash.storm" : "raikon.crimsonFlash", "Crimson Flash", 93)
                .Key(10, IaiCrouch(), Ease.OutQuad)
                .Key(32, IaiCrouch(0.04f))
                .Key(36, IaiDash(), Ease.OutExpo)
                .Key(45, Hold(P().Hip(0.1f, -0.24f, -16f).Chest(-30f).Head(22f).Feet(0.5f, -0.4f), 0.6f, 1.1f, 14f))
                .Key(52, IaiEnd(), Ease.OutQuad)
                .Key(93, Ready(), Ease.InOutQuad)
                .Move(33, 44, FlashSpeed).NoPush(32, 46)
                .Hit(34, 45, Blade, Dmg(14), HitLevel.Mid, new Vector2(5f, 1.5f), stun: 28, hitstop: 10, shake: 0.4f, heavy: true, radius: 0.32f).Unguardable()
                .Cue(4, EyeCue).Cue(22, EyeCue).Every(GhostCue, 33, 45, 2).Cue(45, IaiSliceCue)
                .Fx(32, 46, FxKind.TrailBlade)
                .Impulse(33, new Vector2(-8f, 1.5f)).Impulse(45, new Vector2(5f, 1f))
                .Sword().Fixed();
            if (storm) m.HazardAlso(45, TrailArc, -FlashDistance * 0.5f);
            return m;
        }

        /// <summary>Crimson Flash's dash: 12 frames at this speed, about 8.4 m (60 % of a 14 m arena; the stage clamps it).</summary>
        public const float FlashSpeed = 42f;
        public static float FlashDistance => FlashSpeed * 12f / 60f;

        static void BuildFlurry(float scale)
        {
            // the third link: a leap over to come down in front of Kage, a diving slam, the blade stuck in the floor for 1.1 s
            BlinkFlurry3 = new MoveDef("raikon.blinkFlurry3", "Blink Flurry", 94)
                .Key(4, AirRaised(), Ease.OutQuad)
                .Key(20, AirRaised())
                .Key(24, Slam(), Ease.InCubic)
                .Key(30, Stuck())
                .Key(88, Stuck())
                .Key(94, Ready(), Ease.InOutQuad)
                .Vault(6, 24, -0.6f, 2.2f, 0f, 0.4f)
                .Hit(20, 26, Blade, Dmg(13), HitLevel.Overhead, new Vector2(3f, 2f), stun: 30, hitstop: 12, shake: 0.5f, knockdown: true, heavy: true, radius: BladeRadius)
                .HazardAlso(24, FlurrySlam, 0.6f)
                .Cue(0, EyeCue).Cue(10, SteelCue).Every(GhostCue, 6, 22, 2).Cue(24, SlamCue)
                .Fx(19, 26, FxKind.TrailBlade).Fx(24, 24, FxKind.Dust).Fx(24, 24, FxKind.Shockwave)
                .Impulse(6, new Vector2(0f, -5f)).Impulse(24, new Vector2(3f, -3f))
                .Sword().Fixed();
            // the second link: the reverse mid dash back through him
            BlinkFlurry2 = new MoveDef("raikon.blinkFlurry2", "Blink Flurry", 16)
                .Key(3, IaiDash(), Ease.OutExpo)
                .Key(14, IaiEnd())
                .Vault(3, 13, 1.4f, 0f, 0.8f, 0.4f).NoPush(2, 15)
                .Hit(4, 13, Blade, Dmg(8), HitLevel.Mid, new Vector2(1f, 0.3f), stun: 34, hitstop: 6, shake: 0.2f, radius: 0.3f).Unguardable()
                .HazardOnTargetAlso(3, FlurrySlash)
                .Cue(0, EyeCue).Every(GhostCue, 3, 13, 2)
                .Fx(2, 14, FxKind.TrailBlade)
                .Impulse(3, new Vector2(-7f, 1f))
                .Then(BlinkFlurry3)
                .Sword().Fixed();
            // the first link: 0.55 s with three rhythmic eye flashes, then a low-mid dash through him
            BlinkFlurry = new MoveDef("raikon.blinkFlurry", "Blink Flurry", 46)
                .Key(8, IaiCrouch(), Ease.OutQuad)
                .Key(32, IaiCrouch(0.05f))
                .Key(37, Hold(P().Hip(0.1f, -0.34f, -16f).Chest(-32f).Head(24f).Feet(0.5f, -0.42f), 0.62f, 0.7f, -6f), Ease.OutExpo)
                .Key(45, Hold(P().Hip(0.1f, -0.32f, -16f).Chest(-30f).Head(22f).Feet(0.5f, -0.42f), 0.6f, 0.74f, 4f))
                .Vault(33, 43, 1.4f, 0f, 0.8f, 0.4f).NoPush(32, 45)
                .Hit(34, 43, Blade, Dmg(8), HitLevel.Low, new Vector2(1f, 0f), stun: 34, hitstop: 6, shake: 0.2f, radius: 0.3f).Unguardable()
                .HazardOnTargetAlso(33, FlurrySlash)
                .Cue(3, EyeCue).Cue(14, EyeCue).Cue(25, EyeCue).Every(GhostCue, 33, 43, 2)
                .Fx(32, 44, FxKind.TrailBlade)
                .Impulse(33, new Vector2(-7f, 1f))
                .Then(BlinkFlurry2)
                .Sword().Fixed();
        }

        static void BuildHazards()
        {
            // Kesa-Giri's storm upgrade: a floor spark line ahead of the third cut that strikes 0.35 s after it
            FloorSparkLine = new HazardDef
            {
                id = "raikon.kesaGiri.sparkLine", telegraphFrames = 21, strikeFrames = 6, size = new Vector2(2.6f, 0.5f),
                hit = new HitWindow { dmg = Dmg(6), level = HitLevel.Unblockable, kb = new Vector2(1.5f, 3.5f), stun = 24, hitstop = 6, shake = 0.2f },
                telegraphCue = SparkLineMarkCue, strikeCue = SparkLineStrikeCue,
            };
            // Crimson Flash's storm upgrade: the arc left along the dash path, struck 0.25 s after he stops (a back-dash lands in it)
            TrailArc = new HazardDef
            {
                id = "raikon.crimsonFlash.trail", telegraphFrames = 15, strikeFrames = 5, size = new Vector2(FlashDistance, 1.1f),
                hit = new HitWindow { dmg = Dmg(6), level = HitLevel.Unblockable, kb = new Vector2(2f, 2f), stun = 24, hitstop = 6, shake = 0.25f },
                telegraphCue = TrailArcMarkCue, strikeCue = TrailArcStrikeCue,
            };
            VerticalBolt = new HazardDef
            {
                id = "raikon.skySplitter.bolt", telegraphFrames = 2, strikeFrames = 6, size = new Vector2(0.55f, 4.2f),
                hit = new HitWindow { dmg = Dmg(7), level = HitLevel.Unblockable, kb = new Vector2(1.5f, 4.5f), stun = 24, hitstop = 8, shake = 0.3f, launch = true },
                telegraphCue = null, strikeCue = VerticalBoltCue,
            };
            // Heavens' Verdict and Final Overload: a ring under Kage that follows him for 20 frames, locks, and is struck 0.55 s after it opened
            SkyBolt = new HazardDef
            {
                id = "raikon.skyBolt", telegraphFrames = 33, followFrames = 20, strikeFrames = 3, size = new Vector2(1.2f, 8f),
                hit = new HitWindow { dmg = Dmg(9), level = HitLevel.Unblockable, kb = new Vector2(2f, 4.5f), stun = 26, hitstop = 10, shake = 0.5f, knockdown = true, heavy = true },
                telegraphCue = BoltMarkCue, strikeCue = SkyBoltCue,
            };
            // Blink Flurry: each dash's slash hangs where Kage stood and detonates 0.3 s after the dash went through (10 frames of dash + 18)
            FlurrySlash = new HazardDef
            {
                id = "raikon.blinkFlurry.slash", telegraphFrames = 28, strikeFrames = 4, size = new Vector2(1.4f, 1.8f),
                hit = new HitWindow { dmg = Dmg(5), level = HitLevel.Unblockable, kb = new Vector2(1f, 3f), stun = 22, hitstop = 6, shake = 0.2f },
                telegraphCue = SlashMarkCue, strikeCue = SlashDetonateCue,
            };
            FlurrySlam = new HazardDef
            {
                id = "raikon.blinkFlurry.slam", telegraphFrames = 18, strikeFrames = 4, size = new Vector2(2.4f, 0.7f),
                hit = new HitWindow { dmg = Dmg(6), level = HitLevel.Unblockable, kb = new Vector2(2f, 3.5f), stun = 24, hitstop = 6, shake = 0.3f },
                telegraphCue = SparkLineMarkCue, strikeCue = SparkLineStrikeCue,
            };
            // Afterimage Feint: the decoy pops 14 frames after it stands, a short stun round it (no knockback to speak of)
            DecoyPop = new HazardDef
            {
                id = "raikon.afterimageFeint.decoy", telegraphFrames = 14, strikeFrames = 3, size = new Vector2(1.5f, 2.3f),
                hit = new HitWindow { dmg = Dmg(2), level = HitLevel.Unblockable, kb = new Vector2(0.3f, 0f), stun = 40, hitstop = 6, shake = 0.2f },
                telegraphCue = null, strikeCue = null,
            };
            // the phase shifts: knockback only (no damage), wide enough to clear him whatever range Kage was at
            PlantShock = new HazardDef
            {
                id = "raikon.stormKindling.shock", telegraphFrames = 0, strikeFrames = 4, size = new Vector2(5.2f, 2.4f),
                hit = new HitWindow { dmg = 0, level = HitLevel.Unblockable, kb = new Vector2(7f, 3.5f), stun = 30, hitstop = 8, shake = 0.5f, knockdown = true },
                telegraphCue = null, strikeCue = PlantShockCue,
            };
            SpiralKnock = new HazardDef
            {
                id = "raikon.ascension.burst", telegraphFrames = 0, strikeFrames = 4, size = new Vector2(5.6f, 3f),
                hit = new HitWindow { dmg = 0, level = HitLevel.Unblockable, kb = new Vector2(7.5f, 3f), stun = 30, hitstop = 10, shake = 0.6f, knockdown = true },
                telegraphCue = null, strikeCue = SpiralBurstCue,
            };
            var w = JsTables.Projectile("thunderwave");
            Wave = new ProjectileDef
            {
                id = "raikon.raijinArc.wave", speed = (float)JsUnits.PxToMetres(w.Speed) * 1.15f, groundWave = true, radius = (float)JsUnits.PxToMetres(w.R),
                lifeFrames = Mathf.RoundToInt((float)(w.Life * 60.0)), maxHits = 1,
                hit = new HitWindow
                {
                    dmg = Dmg(w.Dmg), level = HitLevel.Unblockable, kb = new Vector2((float)JsUnits.PxToMetres(w.Kb), (float)JsUnits.PxToMetres(-w.Up)),
                    stun = Mathf.RoundToInt((float)(w.Hs * 60.0)), hitstop = 8, shake = 0.3f, knockdown = w.Kd, heavy = true,
                },
                flightCue = RaikonAbilities.ThunderWaveCue,
            };
        }

        // ---------------------------------------------------------------------------------------------- the boss
        /// <summary>
        /// Raikon's boss data: the JS name and title, and the Lightning Ascendant's phases in place of the JS three (ADR-0008 lets a boss file set its
        /// own): Pure Samurai, Storm Blade at 65 %, Lightning Ascendant at 30 % and the Final Overload at 12 %.
        /// </summary>
        public static BossDef Boss()
        {
            Register(_scale > 0f ? _scale : 1.24f);
            var js = BossDef.FromJs(Raikon.Id);
            var b = new BossDef { id = Raikon.Id, name = js?.name ?? "Raikon", title = js?.title ?? "the Lightning Ascendant", artCard = js?.artCard };
            var p1 = new List<string>(Phase1);
            var p2 = new List<string>(p1); p2.AddRange(Phase2Adds);
            var p3 = new List<string>(p2); p3.AddRange(Phase3Adds);
            b.phases.Add(new PhaseDef { name = "Pure Samurai", at = 1f, abilities = p1.ToArray(), wind = Calm, onEnter = RaikonStorm.Enter });
            b.phases.Add(new PhaseDef
            {
                name = "Storm Blade", at = Phase2At, abilities = p2.ToArray(), enterMove = StormKindling,
                wind = ModerateGust, windRampFrames = WindRampFrames, onEnter = RaikonStorm.Enter,
            });
            b.phases.Add(new PhaseDef
            {
                name = "Lightning Ascendant", at = Phase3At, abilities = p3.ToArray(), enterMove = Ascension, aggressive = true,
                wind = StormGale, windRampFrames = WindRampFrames, onEnter = RaikonStorm.Enter,
            });
            b.phases.Add(new PhaseDef
            {
                name = "Final Overload", at = OverloadAt, abilities = p3.ToArray(), enterMove = FinalOverload, aggressive = true,
                wind = StormGale, onEnter = RaikonStorm.Enter,
            });
            b.Validate();
            return b;
        }
    }
}
