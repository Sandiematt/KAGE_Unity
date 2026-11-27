using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Comprehensive end-to-end verification of the 4-weapon overhaul: Mizuchi (Spear), Yamigama (Chain Sickle),
    /// Hammer (Iwakudaki), and Onikiri (Heavy Cleaver). Verifies all 7 distinct attack patterns per weapon,
    /// full-body 20-bone rig integrity (no 360-degree body flips, no NaNs, no hyperextended/detached arms),
    /// sliding-grip spear shafts, dynamic curved-to-taut kusarigama chain physics, hammer ground-impact shocks
    /// and counterbalance lean, Onikiri spark-drag cues and one/two-hand grips, directional wind gusts,
    /// ClashGeometry blade clashes, and two-way contact-matched parrying.
    /// </summary>
    public class WeaponOverhaulTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            GameFeel.trauma = GameFeel.punch = 0f;
        }

        static CharacterDef KageWith(string weapon) => SpearFamilyFightTests.KageWith(weapon);

        static CharacterDef FoeWith(string weapon)
        {
            var d = CharacterCatalog.Get(Ronin.Id);
            d.weaponId = weapon;
            return d;
        }

        HeadlessFight Alone(string weapon)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: KageWith(weapon));
            _fights.Add(f);
            return f;
        }

        HeadlessFight Duel(float gap, string kageWeapon, string foeWeapon)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -gap * 0.5f, gap * 0.5f, 0f, foeDef: FoeWith(foeWeapon), kageDef: KageWith(kageWeapon));
            _fights.Add(f);
            return f;
        }

        static List<string> Started(HeadlessFight f, string id = "kage") =>
            f.Events(CombatEventType.MoveStarted).Where(e => e.SourceId == id).Select(e => e.MoveId).ToList();

        static float SegmentGap(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
        {
            if (ClashGeometry.SegmentsCross(a0, a1, b0, b1, out _)) return 0f;
            ParryContactSolver.ClosestPoints(a0, a1, b0, b1, out var p, out var q);
            return (p - q).magnitude;
        }

        // =====================================================================================
        //  1. RIG INTEGRITY & FULL-BODY KINETICS ACROSS ALL 4 WEAPONS
        // =====================================================================================

        [TestCase("mizuchi")]
        [TestCase("yamigama")]
        [TestCase("iwakudaki")]
        [TestCase("onikiri")]
        public void Every_attack_maintains_upright_silhouette_and_valid_limb_integrity(string weapon)
        {
            var f = Alone(weapon);
            var d = f.Kage.def;
            float maxArmF = (d.BindLocal(B.ArmFL).magnitude + d.BindLocal(B.HandF).magnitude) * 1.03f;
            float maxArmN = (d.BindLocal(B.ArmNL).magnitude + d.BindLocal(B.HandN).magnitude) * 1.03f;
            var moves = f.Kage.moves.All.Where(m => m != null && m.hits.Count > 0 && !m.isThrow).Distinct().ToList();
            Assert.GreaterOrEqual(moves.Count, 12, $"{weapon} move count");

            var issues = new List<string>();
            foreach (var m in moves)
            {
                f.Kage.ResetForRound(0f, 1f);
                f.Kage.BeginFight();
                f.Run(10);
                if (m.airborne) { f.KageInput.Add(8, dy: 1); f.Run(8); }
                f.Kage.StartMove(m);
                for (int step = 0; step < m.total + 2 && f.Kage.state == FState.Attack && f.Kage.move == m; step++)
                {
                    f.Run(1);
                    var cur = f.Kage.rig.current;
                    float hipLean = Mathf.Abs(KMath.Wrap(cur.a[(int)B.Hips]));
                    if (hipLean > 35f) issues.Add($"{weapon}/{m.id} f{f.Kage.moveFrame}: hip lean {hipLean:0.0} deg");

                    for (int b = 0; b < (int)B.COUNT; b++)
                    {
                        Vector3 p = f.Kage.rig.bones[b].position;
                        if (float.IsNaN(p.x) || float.IsNaN(p.y)) issues.Add($"{weapon}/{m.id} f{f.Kage.moveFrame}: NaN bone {(B)b}");
                    }
                    float lenF = ((Vector2)f.Kage.rig.Bone(B.HandF).position - (Vector2)f.Kage.rig.Bone(B.ArmFU).position).magnitude;
                    float lenN = ((Vector2)f.Kage.rig.Bone(B.HandN).position - (Vector2)f.Kage.rig.Bone(B.ArmNU).position).magnitude;
                    if (lenF > maxArmF) issues.Add($"{weapon}/{m.id} f{f.Kage.moveFrame}: lead arm hyperextended {lenF:0.000} > {maxArmF:0.000}");
                    if (lenN > maxArmN) issues.Add($"{weapon}/{m.id} f{f.Kage.moveFrame}: rear arm hyperextended {lenN:0.000} > {maxArmN:0.000}");
                }
            }
            Assert.IsEmpty(issues, string.Join("\n", issues.Take(25)));
        }

        // =====================================================================================
        //  2. MIZUCHI (SPEAR): ALL 7 PATTERNS, SLIDING SHAFT, LINEAR GUSTS
        // =====================================================================================

        [Test]
        public void Mizuchi_executes_all_7_techniques_with_sliding_shaft_and_linear_gusts()
        {
            var f = Alone("mizuchi");
            f.Run(15);

            // Light 1 -> Light 2 -> Light 3 combo (Needle Thrust -> Twin Fang Flurry -> Crescent Whip-Cleave)
            f.KageInput.Add(1, buttons: "S").Add(12).Add(1, buttons: "S").Add(19).Add(1, buttons: "S");
            float minSlide = 0f, maxSlide = 0f;
            float peakGustX = 0f;
            for (int i = 0; i < 150; i++)
            {
                f.Run(1);
                if (f.Kage.move?.id == "spear.thrust")
                {
                    minSlide = Mathf.Min(minSlide, f.Kage.rig.current.shaftSlide);
                    maxSlide = Mathf.Max(maxSlide, f.Kage.rig.current.shaftSlide);
                }
                peakGustX = Mathf.Max(peakGustX, f.Kage.rig.ActiveGust.x);
            }
            CollectionAssert.AreEqual(new[] { "spear.thrust", "spear.twinFang", "spear.crescentCleave" }, Started(f));
            Assert.Less(minSlide, -0.15f, "Needle Thrust pulls shaft back across hip during anticipation");
            Assert.Greater(maxSlide, 0.25f, "Needle Thrust slides shaft forward cleanly through lead hand on snap");
            Assert.Greater(peakGustX, 4f, "Spear thrusts fire sharp directional forward gusts");

            // Heavy (Dragon Piercer), Low (Riverbed Sweep), Up/Anti-Air (Rising Serpent), Dash (Vaulting Skewer)
            var set = (SpearMoves)f.Kage.moves;
            Assert.AreSame(set.lunge, set.Select(Btn.S, 1, 0, false, f.Kage));
            Assert.AreSame(set.sweep, set.Select(Btn.S, 0, -1, false, f.Kage));
            Assert.AreSame(set.vault, set.Select(Btn.S, 0, 1, false, f.Kage));

            f.Kage.ResetForRound(0f, 1f);
            f.Kage.BeginFight();
            f.Run(10);
            f.KageInput.Add(1, fwd: 1, dash: true).Add(5, fwd: 1).Add(1, buttons: "S");
            f.Run(45);
            CollectionAssert.Contains(Started(f), "spear.dashSkewer", "Dash + S triggers Vaulting Skewer");
        }

        // =====================================================================================
        //  3. YAMIGAMA (CHAIN SICKLE): ALL 7 PATTERNS, DYNAMIC CURVED-TO-TAUT CHAIN, NO TORSO SPIN
        // =====================================================================================

        [Test]
        public void Yamigama_sickle_stays_in_hand_while_chain_curves_in_windup_and_snaps_taut_on_lash()
        {
            var f = Alone("yamigama");
            f.Run(15);
            var line = ChainVisual.LineOf(f.Kage.rig);
            var sickleAnchor = f.Kage.rig.Bone(B.Sword).Find("Chain_Sickle");
            Assert.IsNotNull(line);
            Assert.IsNotNull(sickleAnchor);

            // Iron Whip Lash: windup bows dynamically (> 5 cm deviation), peak goes taut (< 2.5 cm), recovery returns to 0
            f.Kage.StartMove(((ChainMoves)f.Kage.moves).lash);
            float windupDev = 0f, peakDev = float.MaxValue, peakDist = 0f;
            for (int i = 0; i < 40 && f.Kage.state == FState.Attack; i++)
            {
                f.Run(1);
                line.Refresh();
                Assert.Less(sickleAnchor.localPosition.magnitude, 1e-4f, "sickle stays firmly gripped in lead hand");
                if (f.Kage.moveFrame <= 6) windupDev = Mathf.Max(windupDev, line.CurveDeviation);
                if (f.Kage.moveFrame >= 9 && f.Kage.moveFrame <= 13)
                {
                    peakDev = Mathf.Min(peakDev, line.CurveDeviation);
                    peakDist = Mathf.Max(peakDist, line.HeadDistance);
                }
            }
            Assert.Greater(windupDev, 0.05f, "chain curves cleanly during windup (never a rigid stick)");
            Assert.Less(peakDev, 0.03f, "chain snaps taut at active whip peak");
            Assert.Greater(peakDist, 1.3f, "iron fundo reaches mid-range");

            // Cyclone Chain Arc: overhead/side chain twirl keeps torso facing forward (no 360 body spin)
            f.Kage.ResetForRound(0f, 1f);
            f.Kage.BeginFight();
            f.Run(10);
            f.Kage.StartMove(((ChainMoves)f.Kage.moves).cyclone);
            float maxHipSpin = 0f, cycloneCurve = 0f, cyclonePeak = 0f;
            for (int i = 0; i < 55 && f.Kage.state == FState.Attack; i++)
            {
                f.Run(1);
                line.Refresh();
                maxHipSpin = Mathf.Max(maxHipSpin, Mathf.Abs(KMath.Wrap(f.Kage.rig.current.a[(int)B.Hips])));
                if (f.Kage.moveFrame <= 16) cycloneCurve = Mathf.Max(cycloneCurve, line.CurveDeviation);
                if (f.Kage.moveFrame >= 19 && f.Kage.moveFrame <= 25) cyclonePeak = Mathf.Max(cyclonePeak, line.HeadDistance);
            }
            Assert.LessOrEqual(maxHipSpin, 20f, "Cyclone Chain Arc twirls chain while torso stays facing forward");
            Assert.Greater(cycloneCurve, 0.06f, "Cyclone twirl visibly curves the chain overhead");
            Assert.Greater(cyclonePeak, 1.4f, "Cyclone unleashes long-range sweeping arc");
        }

        // =====================================================================================
        //  4. HAMMER (IWAKUDAKI): ALL 7 PATTERNS, COUNTERBALANCE, GROUND IMPACT SHOCK
        // =====================================================================================

        [Test]
        public void Hammer_executes_all_7_techniques_with_counterbalance_and_ground_shock_impacts()
        {
            var seenCues = new List<string>();
            void OnCue(string id, Vector2 pos, in CueArgs a) => seenCues.Add(id);
            EffectCues.Played += OnCue;
            try
            {
                var f = Alone("iwakudaki");
                f.Run(15);
                var set = (HammerMoves)f.Kage.moves;

                // 3-hit combo: Crushing Overhead Smash -> Earth-Scoop Uppercut -> Battering Haymaker
                f.KageInput.Add(1, buttons: "S").Add(38).Add(1, buttons: "S").Add(34).Add(1, buttons: "S");
                float maxLeanBack = 0f, maxLiftY = 0f, minGustY = 0f;
                for (int i = 0; i < 200; i++)
                {
                    f.Run(1);
                    if (f.Kage.move?.id == HammerMoves.SmashId && f.Kage.moveFrame <= 24)
                    {
                        maxLeanBack = Mathf.Max(maxLeanBack, f.Kage.rig.current.a[(int)B.Chest]);
                        maxLiftY = Mathf.Max(maxLiftY, f.Kage.rig.Bone(B.FootN).position.y);
                    }
                    minGustY = Mathf.Min(minGustY, f.Kage.rig.ActiveGust.y);
                }
                CollectionAssert.AreEqual(new[] { HammerMoves.SmashId, HammerMoves.BreakId, HammerMoves.HaymakerId }, Started(f));
                Assert.Greater(maxLeanBack, 18f, "Crushing Overhead Smash counterbalances back while hauling hammer high");
                Assert.Greater(maxLiftY, 0.17f, "Lead foot lifts slightly during overhead haul");
                Assert.Less(minGustY, -4f, "Hammer slam triggers heavy downward shock gust");
                CollectionAssert.Contains(seenCues, WeaponCues.HammerGroundImpactCue, "Ground impact VFX cue fires on slam");

                // Heavy (Meteor Sunder), Low (Shin-Breaker Drag), Up/Anti-Air (Rising Anvil), Dash (Iron Ram)
                Assert.AreSame(set.meteorSunder, set.Select(Btn.S, 1, 0, false, f.Kage));
                Assert.AreSame(set.shinBreaker, set.Select(Btn.S, 0, -1, false, f.Kage));
                Assert.AreSame(set.risingAnvil, set.Select(Btn.S, 0, 1, false, f.Kage));

                f.Kage.ResetForRound(0f, 1f);
                f.Kage.BeginFight();
                f.Run(10);
                f.KageInput.Add(1, fwd: 1, dash: true).Add(5, fwd: 1).Add(1, buttons: "S");
                f.Run(55);
                CollectionAssert.Contains(Started(f), HammerMoves.IronRamId, "Dash + S triggers Iron Ram");
            }
            finally { EffectCues.Played -= OnCue; }
        }

        // =====================================================================================
        //  5. ONIKIRI (HEAVY CLEAVER): ALL 7 PATTERNS, SPARK-DRAG CUES, ONE/TWO-HAND GRIPS
        // =====================================================================================

        [Test]
        public void Onikiri_executes_all_7_techniques_and_spawns_spark_drag_cues()
        {
            var seenCues = new List<string>();
            void OnCue(string id, Vector2 pos, in CueArgs a) => seenCues.Add(id);
            EffectCues.Played += OnCue;
            try
            {
                var f = Alone("onikiri");
                f.Run(15);
                var set = (CleaverMoves)f.Kage.moves;

                // 3-hit Light combo: Savage Diagonal Hack -> Butcher's Backhand Rip -> Executioner's Guillotine
                f.KageInput.Add(1, buttons: "P").Add(15).Add(1, buttons: "P").Add(18).Add(1, buttons: "P");
                bool oneHandSweep = false, twoHandGuillotine = false;
                for (int i = 0; i < 160; i++)
                {
                    f.Run(1);
                    if (f.Kage.move?.id == "cleaver.hack2" && f.Kage.moveFrame == 13 && !f.Kage.rig.current.grip) oneHandSweep = true;
                    if (f.Kage.move?.id == "cleaver.overhead" && f.Kage.moveFrame == 24 && f.Kage.rig.current.grip) twoHandGuillotine = true;
                }
                CollectionAssert.AreEqual(new[] { "cleaver.hack", "cleaver.hack2", "cleaver.overhead" }, Started(f));
                Assert.IsTrue(oneHandSweep, "Butcher's Backhand Rip swings one-handed with counterweight arm");
                Assert.IsTrue(twoHandGuillotine, "Executioner's Guillotine locks braced two-handed grip");

                // Heavy (Spark-Drag Uppercut): drags blade tip along ground spawning sparks before vertical uppercut
                f.Kage.ResetForRound(0f, 1f);
                f.Kage.BeginFight();
                f.Run(10);
                f.KageInput.Add(1, dy: -1, buttons: "S");
                f.Run(70);
                int sparkCount = seenCues.Count(c => c == WeaponCues.OnikiriSparkDragCue);
                Assert.GreaterOrEqual(sparkCount, 3, "Spark-Drag Uppercut spawns repeated ground-drag sparks during windup");

                // Low (Ankle Hew), Up/Anti-Air (Reaving Sky-Hack), Dash (Lunging Meat-Hook Cleave)
                Assert.AreSame(set.lowHack, set.Select(Btn.P, 0, -1, false, f.Kage));
                Assert.AreSame(set.sweep, set.Select(Btn.S, 0, 1, false, f.Kage));

                f.Kage.ResetForRound(0f, 1f);
                f.Kage.BeginFight();
                f.Run(10);
                f.KageInput.Add(1, fwd: 1, dash: true).Add(5, fwd: 1).Add(1, buttons: "S");
                f.Run(60);
                CollectionAssert.Contains(Started(f), "cleaver.charge", "Dash + S triggers Lunging Meat-Hook Cleave");
            }
            finally { EffectCues.Played -= OnCue; }
        }

        // =====================================================================================
        //  6. TWO-WAY CONTACT-MATCHED PARRYING ACROSS ALL 4 OVERHAULED WEAPONS
        // =====================================================================================

        [TestCase("mizuchi", "S", 0, 0, 1.65f, 7)]       // Mizuchi Needle Thrust (Thrust)
        [TestCase("mizuchi", "S", 0, -1, 1.55f, 8)]      // Mizuchi Riverbed Sweep (Low)
        [TestCase("yamigama", "S", 0, 0, 0.95f, 4)]      // Yamigama Reaping Hook (Mid sickle)
        [TestCase("yamigama", "S", 1, 0, 1.65f, 5)]      // Yamigama Iron Whip Lash (Mid chain whip)
        [TestCase("yamigama", "S", 0, -1, 1.45f, 6)]     // Yamigama Ankle Snare Lash (Low chain whip)
        [TestCase("onikiri", "P", 0, 0, 1.15f, 8)]       // Onikiri Savage Diagonal Hack (High/Overhead)
        [TestCase("onikiri", "S", 0, 0, 1.15f, 24)]      // Onikiri Executioner's Guillotine (Overhead)
        [TestCase("onikiri", "S", 0, -1, 1.15f, 24)]     // Onikiri Spark-Drag Uppercut (Upward)
        public void Kage_parries_overhauled_weapon_strikes_blade_on_blade(string foeWeapon, string btn, int fwd, int dy, float gap, int guardDelay)
        {
            var f = Duel(gap, "kagetsu", foeWeapon);
            f.RoninInput.Add(2).Add(1, fwd: fwd, dy: dy, buttons: btn).Add(200);
            f.KageInput.Add(2 + guardDelay).Add(12, buttons: "G").Add(200);
            f.RunUntil(() => f.Events(CombatEventType.Parried).Any(), 80);

            var parries = f.Events(CombatEventType.Parried).ToList();
            Assert.IsNotEmpty(parries, $"Kage parries {foeWeapon} ({btn} fwd={fwd} dy={dy})");
            var sol = f.Kage.LastParrySolution;
            Assert.IsTrue(sol.valid, "valid contact-matched parry solution");
            Assert.Less(sol.contactGap, 0.03f, $"{foeWeapon}: steel-on-steel gap {sol.contactGap * 100f:0.0} cm");
            Assert.GreaterOrEqual(sol.crossAngleDeg, ParryContactSolver.MinCross, $"{foeWeapon}: cross angle {sol.crossAngleDeg:0} deg");
            Assert.LessOrEqual(Mathf.Abs(KMath.Wrap(f.Kage.rig.current.a[(int)B.Hips])), 22f, "parrier hips upright");
        }

        [TestCase("mizuchi", "S", 0, 0, 1.65f, 7)]       // Mizuchi Needle Thrust parried by foe
        [TestCase("yamigama", "S", 1, 0, 1.65f, 5)]      // Yamigama Iron Whip Lash parried by foe
        [TestCase("iwakudaki", "S", 0, 0, 1.15f, 22)]    // Hammer Crushing Overhead Smash parried by foe
        [TestCase("onikiri", "S", 0, 0, 1.15f, 24)]      // Onikiri Executioner's Guillotine parried by foe
        public void Foes_parry_Kages_overhauled_weapons_blade_on_blade(string kageWeapon, string btn, int fwd, int dy, float gap, int guardDelay)
        {
            var f = Duel(gap, kageWeapon, "kagetsu");
            f.KageInput.Add(2).Add(1, fwd: fwd, dy: dy, buttons: btn).Add(200);
            f.RoninInput.Add(2 + guardDelay).Add(12, buttons: "G").Add(200);
            f.RunUntil(() => f.Events(CombatEventType.Parried).Any(), 80);

            var parries = f.Events(CombatEventType.Parried).ToList();
            Assert.IsNotEmpty(parries, $"Foe parries Kage's {kageWeapon} ({btn} fwd={fwd} dy={dy})");
            var sol = f.Ronin.LastParrySolution;
            Assert.IsTrue(sol.valid, "valid foe contact-matched parry solution");
            Assert.Less(sol.contactGap, 0.03f, $"{kageWeapon}: foe parry gap {sol.contactGap * 100f:0.0} cm");
            Assert.GreaterOrEqual(sol.crossAngleDeg, ParryContactSolver.MinCross, $"{kageWeapon}: cross angle {sol.crossAngleDeg:0} deg");
            Assert.LessOrEqual(Mathf.Abs(KMath.Wrap(f.Ronin.rig.current.a[(int)B.Hips])), 22f, "foe parrier hips upright");
        }
    }
}
