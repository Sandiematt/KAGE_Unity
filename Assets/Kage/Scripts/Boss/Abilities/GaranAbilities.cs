using Kage.Rules;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// Garan's abilities (SAN-104 + Karate · Sumō martial combos &amp; Raikon vanguard lightning, ADR-0008):
    /// <list type="bullet">
    /// <item><b>Storm Fist</b> (<c>stormFist</c>), <b>Tsuppari</b> (<c>garanTsuppari</c>), <b>Charge</b> (<c>charge</c>), and <b>Clinch</b>
    /// (<c>garan.clinch</c>): his four SAN-104 slots, upgraded with iron-storm ground cracks, Harite air-compression rings, and chest-scar arcs.</item>
    /// <item><b>Seiken · Gedan Mawashi</b> (<c>garan.gedanCombo</c>): Kyokushin + Sumō pocket combination — heavy Shiko step with a Seiken body
    /// punch, Harite guard slap, and a whipping Gedan Mawashi Geri low shin kick.</item>
    /// <item><b>Shiko Raijin Stomp</b> (<c>garan.shikoStomp</c>): ceremonial Sumō high leg lift (armoured, storm sparks on his iron suneate)
    /// into a thunderous stomp that sends a short Raikon-trained delayed floor spark line ahead.</item>
    /// <item><b>Broken Static Palm</b> (<c>garan.brokenPalm</c>, Phase 2): Garan's scarred vanguard version of Raikon's Static Palm — draws
    /// crimson-orange arcs from his burnt chest cuirass into his tekko gauntlet and lunges with a heavy electrified palm shockwave.</item>
    /// </list>
    /// </summary>
    public static class GaranAbilities
    {
        public const string StormFistId = "stormFist", TsuppariId = "garanTsuppari", ChargeId = "charge", ClinchId = "garan.clinch";
        public const string GedanComboId = "garan.gedanCombo", ShikoStompId = "garan.shikoStomp", BrokenPalmId = "garan.brokenPalm";
        public const string ShockId = "garan.stormFist.shock", ShikoSparkId = "garan.shikoStomp.spark";

        public static readonly string[] ExtraIds = { GedanComboId, ShikoStompId, BrokenPalmId };

        // ---- effect cues ----
        public const string ShockCue = "VFX_Anime_Ground_Crack";
        public const string ScarChargeCue = "VFX_Kage_GaranScarCharge";
        public const string PalmWaveCue = "VFX_Kage_GaranPalmWave";
        public const string HeavyPalmWaveCue = "VFX_Kage_GaranHeavyPalmWave";
        public const string ShikoChargeCue = "VFX_Kage_GaranShikoCharge";
        public const string ShikoStompCue = "VFX_Kage_GaranShikoStomp";
        public const string SeikenCue = "VFX_Kage_GaranSeiken";
        public const string GedanKickCue = "VFX_Kage_GaranGedanKick";
        public const string ShikoLineMarkCue = "VFX_Kage_GaranShikoLineMark";
        public const string ShikoLineStrikeCue = "VFX_Kage_GaranShikoLineStrike";

        public static readonly string[] CueIds =
        {
            ShockCue, ScarChargeCue, PalmWaveCue, HeavyPalmWaveCue, ShikoChargeCue, ShikoStompCue, SeikenCue, GedanKickCue, ShikoLineMarkCue, ShikoLineStrikeCue,
        };

        // ---- constants of the JS ability code ----
        public const double StormBreakDmg = 19;
        public const double StormBreakGb = 110;
        public const double StormBreakRecovery = 0.75;
        public const double ShockStartPx = 120;
        const double TsuppariTell = 0.5, PalmGap = 0.17, PalmWalkPx = 170, TsuppariRecovery = 0.5;
        const int Palms = 4;
        const double PalmDmg = 6, LastPalmDmg = 10, PalmGb = 24;
        public const double ChargeTell = 0.6, ChargeWallStun = 1.7;
        const double ChargeDur = 3.2, ChargeRunPx = 1050, ChargeBackPx = 40;
        const double ChargeDmg = 22, ChargeGb = 120, ChargeStop = 0.6;
        const double ThrowDmg = 16, ThrowCarry = 0.5, ThrowReachPx = 70, ThrowLiftPx = 120;
        const double ThrowVxPx = 360, ThrowVyPx = 380;
        const float ClinchChance = 0.55f;
        const double GuardLeanPerDmg = 1.3;

        static readonly HitPoint[] Fist = { HitPoint.Fist };
        static readonly HitPoint[] FarFist = { HitPoint.FistF };
        static readonly HitPoint[] BothFists = { HitPoint.Fist, HitPoint.FistF };
        static readonly HitPoint[] LowShin = { HitPoint.ShinN, HitPoint.FootN };
        static readonly HitPoint[] Body = { HitPoint.Fist, HitPoint.FistF, HitPoint.KneeN };

        public static MoveDef GedanComboMove, ShikoStompMove, BrokenPalmMove;
        public static HazardDef ShikoSparkLine;

        static int Frames(double seconds) => Mathf.RoundToInt((float)(seconds * 60.0));
        static float Metres(double px) => (float)JsUnits.PxToMetres(px);

        public static double Windup => JsTables.Difficulty("normal").Windup * JsTables.AiRole("sub").Windup;
        public static float UnityPerJsDamage => KatanaMoves.Build().jab.hits[0].dmg / (float)JsTables.Move("p1").Dmg;

        static int Dmg(double js) => Mathf.Max(1, Mathf.RoundToInt((float)js * UnityPerJsDamage));
        static float PostureMul(double gb, double jsDmg) => (float)(gb / (jsDmg * GuardLeanPerDmg));

        static AbilityDef Def(string id, MoveDef move)
        {
            var js = JsTables.BossAbility(id);
            return new AbilityDef
            {
                id = id, move = move, cooldownFrames = Frames(js.Cd),
                range = new Vector2(Metres(js.Range[0]), Metres(js.Range[1])), chance = (float)js.Chance,
            };
        }

        /// <summary>Registers Garan's abilities in the <see cref="AbilityCatalog"/> for build scale <paramref name="scale"/>. Idempotent.</summary>
        public static void Register(float scale)
        {
            RegisterCues();
            AbilityCatalog.Register(Def(StormFistId, StormFist(JsTables.BossAbility(StormFistId), scale)));
            AbilityCatalog.Register(Def(TsuppariId, Tsuppari()));
            AbilityCatalog.Register(Def(ChargeId, Charge()));
            var clinch = JsTables.FightStyle(Garan.Id).Strings;
            JsStyleString grab = null;
            foreach (var s in clinch) if (s.Seq.Length == 1 && s.Seq[0] == "grab") grab = s;
            AbilityCatalog.Register(new AbilityDef
            {
                id = ClinchId, move = Clinch(Metres(grab.Range[1]), scale), cooldownFrames = 0,
                range = new Vector2(Metres(grab.Range[0]), Metres(grab.Range[1])), chance = ClinchChance,
                when = (self, opp) => opp != null && opp.IsGuarding,
            });

            BuildMartialKit();
            System.Func<Fighter, Fighter, bool> whenOpen = (self, opp) => opp == null || !opp.IsGuarding;
            Reg(GedanComboId, GedanComboMove, 9.0f, 0f, 1.65f, 0.22f, whenOpen);
            Reg(ShikoStompId, ShikoStompMove, 10.0f, 0.65f, 2.5f, 0.20f, whenOpen);
            Reg(BrokenPalmId, BrokenPalmMove, 9.5f, 0f, 1.75f, 0.22f, whenOpen);
            Patterns();
        }

        /// <summary>
        /// Garan's strings (sub-boss II, <see cref="SubBossTier.Garan"/>): a pressure brawler who walks his strings forward. The karate pocket
        /// combo flows into the Shiko stomp or the palm barrage; a palm barrage that meets a guard turns into the clinch; the stomp sets up the
        /// Storm Fist overhead or, in Phase 2, the Static Palm; the big blows roll into the Charge once he is Stormbroken.
        /// </summary>
        static void Patterns()
        {
            var t = SubBossTier.Garan;
            t.Chain(GedanComboId, ShikoStompId, TsuppariId);
            t.Chain(TsuppariId, ClinchId, StormFistId);
            t.Chain(ShikoStompId, BrokenPalmId, StormFistId);
            t.Chain(StormFistId, ChargeId, GedanComboId);
            t.Chain(BrokenPalmId, ChargeId, TsuppariId);
        }

        static void Reg(string id, MoveDef m, float cd, float near, float far, float chance, System.Func<Fighter, Fighter, bool> when = null)
        {
            AbilityCatalog.Register(new AbilityDef { id = id, move = m, cooldownFrames = Frames(cd), range = new Vector2(near, far), chance = chance, when = when });
        }

        static Pose P() => new Pose();
        static Pose Guard() => FistGuard(Stance());

        static void BuildMartialKit()
        {
            if (GedanComboMove != null) return;

            // Shiko Stomp's floor fissure: the floor cracks along the line during the leg lift and erupts in rock and crimson arcs with the stomp (frames 24..28)
            ShikoSparkLine = new HazardDef
            {
                id = ShikoSparkId,
                telegraphFrames = 13,
                strikeFrames = 4,
                size = new Vector2(1.55f, 0.5f),
                hit = new HitWindow { dmg = Dmg(4), level = HitLevel.Mid, kb = new Vector2(1.6f, 2.4f), stun = 18, hitstop = 6, shake = 0.2f, postureMul = 0.5f },
                telegraphCue = ShikoLineMarkCue,
                strikeCue = ShikoLineStrikeCue,
            };

            // Kyokushin + Sumō Inside-Outside Blaster: Seiken body punch -> Harite guard slap -> Gedan Mawashi Geri low shin kick
            GedanComboMove = new MoveDef(GedanComboId, "Seiken · Gedan Mawashi", 72)
                .Key(8, P().Hip(-0.04f, -0.22f, 6f).Chest(10f).Head(-4f).Feet(0.36f, -0.32f).HandN(0.18f, 1.05f, 30f).HandF(0.32f, 1.18f, 40f), Ease.OutQuad)
                .Key(16, P().Hip(0.12f, -0.22f, -12f).Chest(-20f).Head(10f).Feet(0.46f, -0.3f).HandF(0.88f, 1.02f, 0f).HandN(0.2f, 1.18f, 30f), Ease.OutExpo)
                .Key(23, P().Hip(0.08f, -0.2f, -6f).Chest(-10f).Head(6f).Feet(0.42f, -0.3f).HandN(0.14f, 1.32f, 60f).HandF(0.38f, 1.1f, 20f), Ease.InOutQuad)
                .Key(29, P().Hip(0.14f, -0.18f, -14f).Chest(-22f).Head(12f).Feet(0.46f, -0.3f).HandN(0.9f, 1.28f, 85f).HandF(0.22f, 1.08f, 30f), Ease.OutExpo)
                .Key(37, P().Hip(0.04f, -0.18f, 8f).Chest(8f).Head(-4f).LegN(78f, -95f, 10f).FootF(-0.18f, 0.14f).HandN(0.34f, 1.2f, 40f).HandF(0.24f, 1.14f, 40f), Ease.InOutQuad)
                .Key(43, P().Hip(0.12f, -0.22f, 14f).Chest(14f).Head(-6f).LegN(64f, -8f, 24f).FootF(-0.16f, 0.14f).HandN(0.26f, 1.18f, 35f).HandF(0.2f, 1.12f, 35f), Ease.OutExpo)
                .Key(49, P().Hip(0.1f, -0.22f, 12f).Chest(12f).Head(-4f).LegN(60f, -12f, 20f).FootF(-0.16f, 0.14f).HandN(0.28f, 1.18f, 35f).HandF(0.2f, 1.12f, 35f))
                .Key(72, Guard(), Ease.InOutQuad)
                .Move(12, 18, 2.4f).Move(25, 30, 1.5f).Move(39, 44, 1.8f)
                .Hit(16, 20, FarFist, Dmg(5), HitLevel.Mid, new Vector2(1.1f, 0f), stun: 18, hitstop: 5, shake: 0.14f, radius: 0.16f, postureMul: 0.45f)
                .Hit(28, 32, Fist, Dmg(6), HitLevel.Mid, new Vector2(1.4f, 0.2f), stun: 20, hitstop: 6, shake: 0.18f, radius: 0.16f, postureMul: 0.45f)
                .Hit(42, 46, LowShin, Dmg(8), HitLevel.Low, new Vector2(3.0f, 0.8f), stun: 24, hitstop: 8, shake: 0.28f, heavy: true, radius: 0.15f, postureMul: 0.5f)
                .Cue(17, SeikenCue).Cue(29, PalmWaveCue).Cue(43, GedanKickCue)
                .Fx(15, 20, FxKind.TrailFist).Fx(27, 32, FxKind.TrailFist).Fx(41, 47, FxKind.TrailFootN)
                .Impulse(43, new Vector2(2.4f, -0.8f));

            // Sumō Shiko Raijin Stomp: high ceremonial leg lift with storm sparks, then a stomp that fires the floor spark line on landing
            ShikoStompMove = new MoveDef(ShikoStompId, "Shiko Raijin Stomp", 68)
                .Key(10, P().Hip(-0.04f, -0.14f, 14f).Chest(16f).Head(-8f).LegN(128f, -72f, 15f).FootF(-0.2f, 0.14f).HandN(0.32f, 1.34f, 50f).HandF(-0.08f, 1.28f, 40f), Ease.OutQuad)
                .Key(21, P().Hip(-0.05f, -0.12f, 18f).Chest(20f).Head(-10f).LegN(140f, -64f, 18f).FootF(-0.2f, 0.14f).HandN(0.34f, 1.4f, 55f).HandF(-0.1f, 1.32f, 45f))
                .Key(25, P().Hip(0.08f, -0.38f, -6f).Chest(-24f).Head(14f).Feet(0.52f, -0.32f).HandN(0.46f, 0.86f, -30f).HandF(0.36f, 0.84f, -30f), Ease.InCubic)
                .Key(38, P().Hip(0.08f, -0.38f, -6f).Chest(-22f).Head(12f).Feet(0.52f, -0.32f).HandN(0.44f, 0.88f, -30f).HandF(0.34f, 0.86f, -30f))
                .Key(68, Guard(), Ease.InOutQuad)
                .Move(21, 25, 1.5f)
                .Hit(24, 28, LowShin, Dmg(8), HitLevel.Mid, new Vector2(2.8f, 1.8f), stun: 22, hitstop: 9, shake: 0.35f, heavy: true, radius: 0.2f, postureMul: 0.6f)
                .Hazard(11, ShikoSparkLine, 1.55f)
                .Cue(6, ShikoChargeCue).Cue(13, ShikoChargeCue).Cue(19, ShikoChargeCue).Cue(25, ShikoStompCue)
                .Fx(23, 28, FxKind.TrailFootN).Fx(25, 25, FxKind.Dust)
                .Impulse(25, new Vector2(2.0f, -3.2f));

            // Broken Static Palm (Phase 2): Garan's vanguard inheritance of Raikon's Static Palm
            BrokenPalmMove = new MoveDef(BrokenPalmId, "Broken Static Palm", 66)
                .Key(8, P().Hip(-0.08f, -0.22f, 8f).Chest(14f).Head(-6f).Feet(0.34f, -0.34f).HandN(-0.14f, 1.12f, 10f).HandF(0.28f, 1.18f, 50f), Ease.OutQuad)
                .Key(24, P().Hip(-0.1f, -0.24f, 10f).Chest(16f).Head(-8f).Feet(0.34f, -0.36f).HandN(-0.18f, 1.1f, 10f).HandF(0.26f, 1.18f, 50f))
                .Key(28, P().Hip(0.18f, -0.2f, -14f).Chest(-28f).Head(14f).Feet(0.5f, -0.32f).HandN(0.94f, 1.18f, -80f).HandF(0.18f, 1.08f, 30f), Ease.OutExpo)
                .Key(38, P().Hip(0.18f, -0.2f, -14f).Chest(-26f).Head(14f).Feet(0.5f, -0.32f).HandN(0.9f, 1.16f, -80f).HandF(0.18f, 1.08f, 30f))
                .Key(66, Guard(), Ease.InOutQuad)
                .Armour(18, 26)
                .Move(23, 28, 4.0f)
                .Hit(26, 30, Fist, Dmg(10), HitLevel.Mid, new Vector2(4.8f, 1.6f), stun: 26, hitstop: 10, shake: 0.42f, knockdown: true, heavy: true, radius: 0.18f,
                    postureMul: 0.85f)
                .Cue(4, ScarChargeCue).Cue(10, ScarChargeCue).Cue(16, ScarChargeCue).Cue(22, ScarChargeCue).Cue(26, HeavyPalmWaveCue)
                .Fx(25, 31, FxKind.TrailFist).Fx(26, 26, FxKind.Dust)
                .Impulse(26, new Vector2(3.2f, 0.8f));
        }

        // ---------------------------------------------------------------------------------------------- Storm Fist
        static MoveDef StormFist(JsBossAbility js, float scale)
        {
            int land = Frames(js.Windup * Windup);
            int slam = land - 1;
            int total = land + Frames(StormBreakRecovery);
            var overhead = P().Hip(-0.05f, -0.12f, 8f).Chest(18f).Head(-12f).Feet(0.34f, -0.32f).HandN(0.08f, 2.05f, 80f).HandF(-0.04f, 2.0f, 80f);
            var down = P().Hip(0.12f, -0.42f, -10f).Chest(-48f).Head(24f).Feet(0.46f, -0.32f).HandN(0.98f, 0.14f, -70f).HandF(0.88f, 0.12f, -70f);
            double jsSlam = StormBreakDmg * js.Dmg;
            var shock = JsTables.Projectile("shock");
            double runPx = shock.Speed * shock.Life * js.ShockLife;
            var hazard = new HazardDef
            {
                id = ShockId,
                telegraphFrames = slam - 1,
                strikeFrames = Frames(shock.Life * js.ShockLife),
                size = new Vector2(Metres(runPx + 2 * shock.R), Metres(2 * shock.R)),
                hit = new HitWindow
                {
                    dmg = Dmg(shock.Dmg * js.Shock), level = shock.Unb ? HitLevel.Unblockable : HitLevel.Mid,
                    kb = new Vector2(2.2f, 3.6f), stun = 24, hitstop = 8, shake = 0.3f, knockdown = shock.Kd, heavy = true,
                },
                strikeCue = ShockCue,
            };
            var m = new MoveDef("garan.stormFist", "Storm Fist", total)
                .Key(8, overhead, Ease.OutQuad)
                .Key(land - 4, P().Hip(-0.06f, -0.1f, 10f).Chest(22f).Head(-14f).Feet(0.34f, -0.32f).HandN(0.04f, 2.1f, 80f).HandF(-0.08f, 2.05f, 80f))
                .Key(land, down, Ease.InCubic)
                .Key(land + 16, P().Hip(0.12f, -0.42f, -10f).Chest(-46f).Head(22f).Feet(0.46f, -0.32f).HandN(0.96f, 0.16f, -70f).HandF(0.86f, 0.14f, -70f))
                .Key(total, Guard(), Ease.InOutQuad)
                .Hit(slam, slam + 4, BothFists, Dmg(jsSlam), HitLevel.Overhead, new Vector2(3.3f, 4.0f), stun: 30, hitstop: 12, shake: 0.55f,
                    knockdown: true, heavy: true, radius: 0.22f, postureMul: PostureMul(StormBreakGb, jsSlam))
                .Armour(0, land)
                .Hazard(1, hazard, Metres(ShockStartPx * scale + runPx / 2))
                .Fx(2, land - 3, FxKind.ChargeDark).Fx(land - 8, land - 8, FxKind.Glint)
                .Fx(land, land, FxKind.Dust).Fx(land, land, FxKind.Shockwave).Fx(slam - 2, land + 1, FxKind.TrailFist)
                .Impulse(land, new Vector2(2.8f, -2.6f));
            for (int f = 6; f < land - 4; f += 6) m.Cue(f, ScarChargeCue);
            return m;
        }

        // ---------------------------------------------------------------------------------------------- Tsuppari
        static MoveDef Tsuppari()
        {
            int tell = Frames(TsuppariTell * Windup);
            int end = Frames(TsuppariTell * Windup + Palms * PalmGap);
            int total = Frames(TsuppariTell * Windup + Palms * PalmGap + TsuppariRecovery);
            var crouch = P().Hip(0f, -0.44f, 2f).Chest(-30f).Head(22f).Feet(0.52f, -0.34f).HandN(0.44f, 0.46f, -80f).HandF(0.36f, 0.44f, -80f);
            var m = new MoveDef("garan.tsuppari", "Tsuppari", total)
                .Key(tell - 6, crouch, Ease.OutQuad)
                .Key(tell - 1, P().Hip(0f, -0.46f, 2f).Chest(-32f).Head(24f).Feet(0.52f, -0.34f).HandN(0.46f, 0.44f, -80f).HandF(0.38f, 0.42f, -80f));
            double jsSmall = PalmDmg, jsLast = LastPalmDmg;
            for (int i = 0; i < Palms; i++)
            {
                int at = Frames(TsuppariTell * Windup + i * PalmGap);
                bool near = i % 2 == 0, last = i == Palms - 1;
                var thrust = P().Hip(0.12f, -0.2f, -10f).Chest(-22f).Head(10f).Feet(0.44f, -0.3f);
                thrust = near ? thrust.HandN(0.86f, 1.05f, 90f).HandF(0.2f, 1.12f, 30f) : thrust.HandF(0.84f, 1.05f, 90f).HandN(0.24f, 1.2f, 30f);
                var chamber = P().Hip(0.06f, -0.22f, -6f).Chest(-16f).Head(8f).Feet(0.4f, -0.3f).HandN(0.4f, 1.24f, 60f).HandF(0.34f, 1.18f, 60f);
                m.Key(at + 2, thrust, Ease.OutExpo).Key(at + 7, chamber, Ease.InOutQuad);
                double js = last ? jsLast : jsSmall;
                m.Hit(at, at + 3, near ? Fist : FarFist, Dmg(js), HitLevel.Mid, last ? new Vector2(4.2f, 0.8f) : new Vector2(1.6f, 0f),
                    stun: last ? 26 : 16, hitstop: last ? 9 : 5, shake: last ? 0.3f : 0.12f, heavy: last, radius: 0.16f, postureMul: PostureMul(PalmGb, js));
                m.Fx(at, at + 3, FxKind.TrailFist).Cue(at + 1, last ? HeavyPalmWaveCue : PalmWaveCue);
            }
            return m.Key(total, Guard(), Ease.InOutQuad)
                .Move(tell, end, Metres(PalmWalkPx))
                .Armour(0, end)
                .Fx(tell, tell, FxKind.Dust);
        }

        // ---------------------------------------------------------------------------------------------- Charge
        static MoveDef Charge()
        {
            int tell = Frames(ChargeTell), total = Frames(ChargeDur);
            var set = P().Hip(-0.08f, -0.24f, 10f).Chest(14f).Head(-8f).Feet(0.3f, -0.4f).HandN(0.14f, 1.0f, 20f).HandF(-0.02f, 0.96f, 20f);
            var runA = P().Hip(0.12f, -0.22f, -18f).Chest(-34f).Head(26f).FootN(0.5f, 0.18f).FootF(-0.42f, 0.2f).HandN(0.6f, 1.22f, 0f).HandF(0.5f, 1.1f, 0f);
            var runB = P().Hip(0.12f, -0.18f, -18f).Chest(-34f).Head(26f).FootN(-0.36f, 0.2f).FootF(0.46f, 0.18f).HandN(0.62f, 1.24f, 0f).HandF(0.52f, 1.12f, 0f);
            var m = new MoveDef("garan.charge", "Charge", total)
                .Key(10, set, Ease.OutQuad)
                .Key(tell - 1, P().Hip(-0.1f, -0.26f, 12f).Chest(18f).Head(-10f).Feet(0.3f, -0.42f).HandN(0.1f, 0.98f, 20f).HandF(-0.04f, 0.94f, 20f));
            for (int f = tell + 4, k = 0; f < total; f += 8, k++) m.Key(f, k % 2 == 0 ? runA : runB, Ease.InOutQuad);
            m.Move(0, tell - 1, -Metres(ChargeBackPx))
                .Move(tell, total, Metres(ChargeRunPx))
                .Hit(tell, total, Body, Dmg(ChargeDmg), HitLevel.Mid, new Vector2(5.0f, 3.0f), stun: 34, hitstop: 14, shake: 0.6f,
                    knockdown: true, heavy: true, radius: 0.2f, postureMul: PostureMul(ChargeGb, ChargeDmg))
                .Armour(0, total)
                .Cue(12, ScarChargeCue).Cue(24, ScarChargeCue)
                .Fx(tell, total, FxKind.AfterImage).Fx(tell, tell, FxKind.Dust);
            m.onContact = new MoveDef("garan.chargeStop", "Charge", Frames(ChargeStop))
                .Key(10, P().Hip(-0.04f, -0.24f, 8f).Chest(10f).Head(-4f).Feet(0.46f, -0.3f).HandN(0.5f, 1.1f, 10f).HandF(0.4f, 1.02f, 10f), Ease.OutQuad)
                .Key(Frames(ChargeStop), Guard(), Ease.InOutQuad)
                .Move(0, 8, 2.4f)
                .Fx(0, 0, FxKind.Dust);
            m.wallStagger = Frames(ChargeWallStun);
            return m;
        }

        // ---------------------------------------------------------------------------------------------- Clinch
        static MoveDef Clinch(float reach, float scale)
        {
            var grab = JsTables.Move("grab");
            int from = Frames(grab.T[0]);
            int to = from + Frames(grab.T[1]) - 1;
            int release = to + Frames(ThrowCarry);
            int recovery = Frames(grab.T[2]);
            var wide = P().Hip(0.08f, -0.2f, -8f).Chest(-22f).Head(12f).Feet(0.4f, -0.28f).HandN(0.78f, 1.38f, 10f).HandF(0.72f, 1.16f, 10f);
            var clamp = P().Hip(0.12f, -0.22f, -10f).Chest(-26f).Head(14f).Feet(0.42f, -0.28f).HandN(0.52f, 1.26f, -20f).HandF(0.48f, 1.14f, -20f);
            var whiff = new MoveDef("garan.clinchWhiff", "Clinch", to + recovery)
                .Key(from, wide, Ease.OutExpo).Key(to, clamp, Ease.InQuad)
                .Key(to + recovery, Guard(), Ease.InOutQuad)
                .Armour(0, to);
            var m = new MoveDef("garan.clinch", "Clinch", release + recovery)
                .Key(from, wide, Ease.OutExpo).Key(to, clamp, Ease.InQuad)
                .Key(to + 10, P().Hip(0f, -0.32f, -2f).Chest(-6f).Head(-4f).Feet(0.32f, -0.3f).HandN(0.28f, 1.72f).HandF(0.2f, 1.68f), Ease.InOutQuad)
                .Key(to + 20, P().Hip(-0.04f, -0.18f, 10f).Chest(18f).Head(-12f).Feet(0.3f, -0.3f).HandN(-0.08f, 1.96f).HandF(-0.18f, 1.9f), Ease.InOutQuad)
                .Key(release, P().Hip(-0.06f, -0.36f, -12f).Chest(-44f).Head(20f).Feet(0.26f, -0.3f).HandN(-0.52f, 0.7f).HandF(-0.46f, 0.6f), Ease.InQuad)
                .Key(release + recovery, Guard(), Ease.InOutQuad)
                .Move(1, to, 1.6f)
                .Armour(0, release)
                .Fx(release, release, FxKind.Dust).Fx(release, release, FxKind.Shockwave);
            m.isThrow = true;
            m.grabFrame = from;
            m.grabTo = to;
            m.releaseFrame = release;
            m.whiff = whiff;
            m.grabRange = reach;
            m.throwDmg = Dmg(ThrowDmg);
            m.carryFrom = Metres(ThrowReachPx * scale) * Mathf.Cos(0.2f);
            m.carryTo = Metres(ThrowReachPx * scale) * Mathf.Cos(Mathf.PI * 0.95f);
            m.carryHeight = Metres(ThrowLiftPx * scale);
            m.throwKb = new Vector2(-Metres(ThrowVxPx), Metres(ThrowVyPx));
            return m;
        }

        // ---------------------------------------------------------------------------------------------- cues
        // The look itself is in Vfx.Garan.cs (docs/act1-subbosses-vfx.md); his iron smears and concussive impacts come from GaranLook on every move.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RegisterCues()
        {
            EffectCues.Register(ShockCue, PlayShock);
            EffectCues.Register(ScarChargeCue, PlayScarCharge);
            EffectCues.Register(PalmWaveCue, (Vector2 p, in CueArgs a) => PlayPalmWave(p, a, false));
            EffectCues.Register(HeavyPalmWaveCue, (Vector2 p, in CueArgs a) => PlayPalmWave(p, a, true));
            EffectCues.Register(ShikoChargeCue, PlayShikoCharge);
            EffectCues.Register(ShikoStompCue, PlayShikoStomp);
            EffectCues.Register(SeikenCue, PlaySeiken);
            EffectCues.Register(GedanKickCue, PlayGedanKick);
            EffectCues.Register(ShikoLineMarkCue, (Vector2 p, in CueArgs a) => Vfx.I?.GaranFissureMark(p - Vector2.right * a.size.x * 0.5f, p + Vector2.right * a.size.x * 0.5f, a.seconds));
            EffectCues.Register(ShikoLineStrikeCue, (Vector2 p, in CueArgs a) => Vfx.I?.GaranFissureStrike(p - Vector2.right * a.size.x * 0.5f, p + Vector2.right * a.size.x * 0.5f));
        }

        static float Facing(in CueArgs a) => a.owner != null ? a.owner.facing : (a.dir.x >= 0f ? 1f : -1f);
        static bool Stormbroken(in CueArgs a) => a.owner?.Phases != null && a.owner.Phases.Index >= 1;
        static Vector2 Hand(in CueArgs a, B hand, Vector2 fallback) => a.owner != null && a.owner.rig != null ? a.owner.rig.World(hand, Vector2.zero) : fallback;

        /// <summary>The Storm Fist shock: a fissure races from his fists along the floor across the hazard, rock spouting at its head.</summary>
        static void PlayShock(Vector2 pos, in CueArgs a)
        {
            var v = Vfx.I;
            if (v == null) return;
            float face = Facing(a);
            float half = Mathf.Max(0.45f, a.size.x * 0.5f);
            Vector2 g = new(pos.x, 0.02f);
            v.GaranGroundSlam(new Vector2(g.x - face * half, g.y), 0.6f, Stormbroken(a));
            v.GaranFissure(g - Vector2.right * (face * half), g + Vector2.right * (face * half), Mathf.Max(0.18f, a.seconds), Stormbroken(a));
            GameFeel.Shake(0.28f);
        }

        static void PlayScarCharge(Vector2 pos, in CueArgs a)
        {
            var v = Vfx.I;
            if (v == null) return;
            Vector2 chest = a.owner != null ? a.owner.Pos + new Vector2(a.owner.facing * 0.08f, 1.15f) : pos;
            v.GaranScarCharge(chest, Hand(a, B.HandN, pos), Stormbroken(a));
        }

        static void PlayPalmWave(Vector2 pos, in CueArgs a, bool heavy) =>
            Vfx.I?.GaranPalmBlast(Hand(a, B.HandN, pos), Facing(a), heavy, Stormbroken(a));

        static void PlayShikoCharge(Vector2 pos, in CueArgs a)
        {
            var v = Vfx.I;
            if (v == null || a.owner == null || a.owner.rig == null) return;
            v.GaranShikoCharge(a.owner.rig.World(B.LegNL, Vector2.zero), a.owner.rig.World(B.FootN, Vector2.zero), a.owner.rig.World(B.FootF, Vector2.zero));
        }

        static void PlayShikoStomp(Vector2 pos, in CueArgs a)
        {
            var v = Vfx.I;
            if (v == null) return;
            Vector2 at = a.owner != null && a.owner.rig != null ? new Vector2(a.owner.rig.World(B.FootN, Vector2.zero).x, a.owner.Pos.y) : pos;
            v.GaranGroundSlam(at, 0.8f, true);
            GameFeel.Shake(0.3f);
        }

        static void PlaySeiken(Vector2 pos, in CueArgs a) => Vfx.I?.GaranSeiken(Hand(a, B.HandF, pos), Facing(a));

        static void PlayGedanKick(Vector2 pos, in CueArgs a)
        {
            float face = Facing(a);
            Vector2 ground = a.owner != null ? a.owner.Pos + Vector2.right * (face * 0.55f) : pos;
            Vfx.I?.GaranLowKick(ground, face);
        }
    }
}
