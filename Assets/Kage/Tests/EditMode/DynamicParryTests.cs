using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The contact-matched parry (<see cref="ParryContactSolver"/>, <see cref="DynamicParryController"/>) and the two reference kata (Crown
    /// Wheel, Poised Fang). The solver is judged on the posed skeleton (<see cref="PoseFK"/>) against real blows from the move sets at their
    /// hit frames: the blades touch (under 3 cm), cross at 35° or more, the arm stays inside its reach, both hands stay on the grip, the hips
    /// stay upright and the point stays off the floor, facing either way and at any body scale. Then real headless fights, both ways: Kage
    /// parrying every family and tier, foes parrying Kage, where the blades stay locked through the hit-stop, the event and the cue sit on
    /// the blades' crossing, the parried weapon recoils along the clash normal and the parrier settles back into its guard.
    /// </summary>
    public class DynamicParryTests
    {
        // ================================================================================== the pure solver

        static RigDef _rig;
        static RigDef Rig => _rig ??= KageModel.Skeleton("dynamic-parry");
        static KatanaMoves Katana => (KatanaMoves)new MoveSets().Get(WeaponFamilies.Katana);

        /// <summary>Steel on steel: the two blades' segments within this (metres).</summary>
        const float Gap = 0.03f;

        static float ArmMin => Mathf.Abs(Rig.BindLocal(B.ArmFL).magnitude - Rig.BindLocal(B.HandF).magnitude) + 0.045f;
        static float ArmMax => (Rig.BindLocal(B.ArmFL).magnitude + Rig.BindLocal(B.HandF).magnitude) * 0.955f;

        static Vector2 TipLocal(float len) => Rig.bladeBaseLocal + (Rig.bladeTipLocal - Rig.bladeBaseLocal).normalized * len;

        static DefenderBladeSpec Spec(RootFrame frame, bool crouch = false) => new()
        {
            rigDef = Rig, frame = frame, bladeBaseLocal = Rig.bladeBaseLocal, bladeTipLocal = Rig.bladeTipLocal,
            gripHeld = true, gripSpacing = Pose.GripSpacing, gripPommel = Pose.GripPommel, crouchHeld = crouch,
            reach = (Rig.bladeTipLocal - Rig.bladeBaseLocal).magnitude, guardPlaneX = new ParryTuning().guardPlaneX,
        };

        /// <summary>The pose a katana move starts from: chūdan, or the Mirror Guard's Kasumi for the counter thrust.</summary>
        static Pose StartOf(KatanaMoves s, MoveDef m)
        {
            var start = PoseLib.Stance();
            if (m != s.counterSlash) return start;
            var held = new Pose();
            s.counterStance.Evaluate(Rig, 15f, start, held);
            return held.Clone();
        }

        static Vector2 BladePoint(MoveDef m, float frame, Pose start, float along)
        {
            var p = new Pose();
            m.Evaluate(Rig, frame, start, p);
            var fk = new PoseFK(Rig).Solve(p);
            return fk.Point(B.Sword, Vector2.Lerp(Rig.bladeBaseLocal, TipLocal(KatanaMoves.BladeLength), along));
        }

        /// <summary>
        /// A katana blow at <paramref name="frame"/> by an attacker facing left, placed so its blade's monouchi lies on the defender (standing at
        /// the origin facing right, at <paramref name="defScale"/>), as on the frame a real hit lands; <paramref name="air"/> lifts the attacker.
        /// </summary>
        static StrikeGeometry Blow(string moveId, float frame, float air = 0f, float defScale = 1f, bool mirror = false)
        {
            var s = Katana;
            var m = s.All.First(x => x != null && x.id == moveId);
            var start = StartOf(s, m);
            Vector2 mono = BladePoint(m, frame, start, 0.85f);
            float ax = mono.x + 0.08f * defScale;
            var at = RootFrame.Of(new Vector2(ax, air), -1f, 1f);
            Vector2 b0 = at.ToWorld(BladePoint(m, frame, start, 0f)), b1 = at.ToWorld(BladePoint(m, frame, start, 1f));
            Vector2 swing = at.ToWorld(BladePoint(m, frame + 0.25f, start, 1f)) - at.ToWorld(BladePoint(m, frame - 0.25f, start, 1f));
            // the hurt circle it touches: the defender's guard posed, the circle nearest the blade
            var guard = new PoseFK(Rig).Solve(PoseLib.Block());
            Vector2 contact = b1;
            float best = float.MaxValue;
            foreach (var h in Rig.hurt)
            {
                Vector2 c = guard.Point(h.bone, h.local) * defScale;
                ParryContactSolver.ClosestPoints(b0, b1, c, c, out var onBlade, out _);
                float d = (onBlade - c).magnitude;
                if (d < best) { best = d; contact = onBlade; }
            }
            var g = new StrikeGeometry
            {
                baseWorld = b0, tipWorld = b1, swingDirWorld = swing.normalized, rawContactWorld = contact,
                level = m.hits.Count > 0 ? m.hits[0].level : HitLevel.Mid, point = HitPoint.Blade, isBlade = true, hasSegment = true,
            };
            if (!mirror) return g;
            static Vector2 M(Vector2 v) => new(-v.x, v.y);
            g.baseWorld = M(g.baseWorld); g.tipWorld = M(g.tipWorld); g.swingDirWorld = M(g.swingDirWorld); g.rawContactWorld = M(g.rawContactWorld);
            return g;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>How far <paramref name="p"/> is inside the deepest of the fighter's body hurt circles (negative: outside all of them).</summary>
        static float InsideBody(Fighter f, Vector2 p)
        {
            float scale = Mathf.Abs(f.transform.localScale.y), worst = float.NegativeInfinity;
            foreach (var h in f.def.hurt)
            {
                if (h.zone != HitZone.Body) continue;
                Vector2 c = f.rig.World(h.bone, h.local);
                worst = Mathf.Max(worst, h.radius * scale - (p - c).magnitude);
            }
            return worst;
        }

        static float SegmentGap(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1)
        {
            if (ClashGeometry.SegmentsCross(a0, a1, b0, b1, out _)) return 0f;
            ParryContactSolver.ClosestPoints(a0, a1, b0, b1, out var p, out var q);
            return (p - q).magnitude;
        }

        /// <summary>Everything a solved parry must hold on the posed skeleton.</summary>
        static void AssertSound(string what, in StrikeGeometry g, in ParryContactSolution sol, Pose pose, RootFrame frame)
        {
            Assert.IsTrue(sol.valid, $"{what}: solved");
            Assert.IsFalse(sol.clampedToReach, $"{what}: in reach");
            var k = new PoseFK(Rig).Solve(pose);
            Vector2 d0 = frame.ToWorld(k.Point(B.Sword, Rig.bladeBaseLocal)), d1 = frame.ToWorld(k.Point(B.Sword, Rig.bladeTipLocal));
            float gap = SegmentGap(d0, d1, g.baseWorld, g.tipWorld);
            Assert.Less(gap, Gap, $"{what}: steel on steel ({gap * 100f:0.0} cm apart)");
            Assert.Less(SegmentGap(d0, d1, sol.clashWorld, sol.clashWorld), Gap, $"{what}: the clash point is on the defender's blade");
            Assert.Less(SegmentGap(g.baseWorld, g.tipWorld, sol.clashWorld, sol.clashWorld), Gap, $"{what}: and on the incoming blade");
            Assert.GreaterOrEqual(sol.crossAngleDeg, ParryContactSolver.MinCross, $"{what}: the blades cross at {sol.crossAngleDeg:0} degrees");
            float reach = (k.pos[(int)B.HandF] - k.pos[(int)B.ArmFU]).magnitude;
            Assert.That(reach, Is.InRange(ArmMin - 0.002f, ArmMax + 0.002f), $"{what}: the arm neither locked straight nor folded ({reach:0.000} m)");
            Assert.LessOrEqual(k.NearFistToHandle(), 0.045f, $"{what}: both hands on the grip");
            Assert.LessOrEqual(Mathf.Abs(KMath.Wrap(pose.a[(int)B.Hips])), 20f, $"{what}: the hips upright");
            Assert.GreaterOrEqual(Mathf.Min(d0.y, d1.y), frame.origin.y + 0.04f * frame.Scale, $"{what}: the blade off the floor");
            Assert.AreEqual(1f, sol.clashNormalWorld.magnitude, 1e-3f, $"{what}: a unit clash normal");
        }

        static IEnumerable<TestCaseData> KatanaBlows()
        {
            yield return new TestCaseData("cleave", 20f, 0f).SetName("Solver_meets_the_overhead_cleave");
            yield return new TestCaseData("tameCut", 28f, 0f).SetName("Solver_meets_the_Poised_Fang_kiri_oroshi");
            yield return new TestCaseData("crownWheel", 11f, 0f).SetName("Solver_meets_the_Crown_Wheel_low_beat");
            yield return new TestCaseData("crownWheel", 27f, 0f).SetName("Solver_meets_the_Crown_Wheel_level_beat");
            yield return new TestCaseData("shadowStep", 9f, 0f).SetName("Solver_meets_the_horizontal_Shadow_Step");
            yield return new TestCaseData("risingMoon", 7f, 0f).SetName("Solver_meets_the_rising_Rising_Moon");
            yield return new TestCaseData("iai", 9f, 0f).SetName("Solver_meets_the_rising_Iai_Draw");
            yield return new TestCaseData("lowReap", 10.5f, 0f).SetName("Solver_meets_the_Low_Reap_sweep");
            yield return new TestCaseData("counterSlash", 3f, 0f).SetName("Solver_meets_the_Kage_Counter_thrust");
            yield return new TestCaseData("airCleave", 12f, 0.45f).SetName("Solver_meets_the_air_cleave");
            yield return new TestCaseData("airSlash", 6f, 0.45f).SetName("Solver_meets_the_air_Crescent_Cut");
        }

        [TestCaseSource(nameof(KatanaBlows))]
        public void Solver_meets_every_height_of_blow_blade_on_blade(string moveId, float frame, float air)
        {
            var g = Blow(moveId, frame, air);
            var frameDef = RootFrame.Of(Vector2.zero, 1f, 1f);
            var pose = new Pose();
            var sol = ParryContactSolver.Solve(g, Spec(frameDef), PoseLib.Block(), pose, new PoseFK(Rig));
            AssertSound($"{moveId} f{frame}", g, sol, pose, frameDef);
            Assert.Greater(sol.clashRoot.x, 0.08f, $"{moveId}: met in front of the body, not on it");
        }

        [Test]
        public void Solver_meets_the_long_spear_thrust()
        {
            var spear = (SpearMoves)new MoveSets().Get(WeaponFamilies.Spear);
            var m = spear.thrust;
            var start = PoseLib.Stance();
            Vector2 SpearPoint(float f, float along)
            {
                var p = new Pose();
                m.Evaluate(Rig, f, start, p);
                var fk = new PoseFK(Rig).Solve(p);
                return fk.Point(B.Sword, Vector2.Lerp(Rig.bladeBaseLocal, TipLocal(SpearMoves.BladeLength), along));
            }
            const float frame = 10f;
            Vector2 mono = SpearPoint(frame, 0.85f);
            var at = RootFrame.Of(new Vector2(mono.x + 0.08f, 0f), -1f, 1f);
            Vector2 b0 = at.ToWorld(SpearPoint(frame, 0f)), b1 = at.ToWorld(SpearPoint(frame, 1f));
            Vector2 swing = at.ToWorld(SpearPoint(frame + 0.25f, 1f)) - at.ToWorld(SpearPoint(frame - 0.25f, 1f));
            var g = new StrikeGeometry
            {
                baseWorld = b0, tipWorld = b1, swingDirWorld = swing.normalized, rawContactWorld = at.ToWorld(mono),
                level = HitLevel.Mid, point = HitPoint.Blade, isBlade = true, hasSegment = true,
            };
            var frameDef = RootFrame.Of(Vector2.zero, 1f, 1f);
            var pose = new Pose();
            var sol = ParryContactSolver.Solve(g, Spec(frameDef), PoseLib.Block(), pose, new PoseFK(Rig));
            AssertSound("spear.thrust f10", g, sol, pose, frameDef);
            Assert.GreaterOrEqual(sol.crossAngleDeg, 50f, "a thrust is beaten aside cleanly");
            Assert.Greater(sol.clashRoot.x, 0.08f, "met in front of the body");
        }

        [Test]
        public void Solver_meets_the_low_leg_sweep_and_forearm_jab()
        {
            var s = Katana;
            var start = PoseLib.Stance();
            var frameDef = RootFrame.Of(Vector2.zero, 1f, 1f);

            StrikeGeometry LimbBlow(MoveDef m, float frame, HitPoint point, HitLevel lvl)
            {
                (Vector2 a, Vector2 b) Seg(float f)
                {
                    var p = new Pose();
                    m.Evaluate(Rig, f, start, p);
                    var fk = new PoseFK(Rig).Solve(p);
                    return point == HitPoint.Fist
                        ? (fk.pos[(int)B.ArmNL], fk.Point(B.HandN, Rig.fistLocal))
                        : (fk.pos[(int)B.LegNL], fk.Point(B.FootN, Rig.footTipLocal));
                }
                var (la, lb) = Seg(frame);
                var at = RootFrame.Of(new Vector2(lb.x + 0.06f, 0f), -1f, 1f);
                var (_, nextB) = Seg(frame + 0.25f);
                var (_, prevB) = Seg(frame - 0.25f);
                Vector2 swing = at.ToWorld(nextB) - at.ToWorld(prevB);
                Vector2 b0 = at.ToWorld(la), b1 = at.ToWorld(lb);
                return new StrikeGeometry
                {
                    baseWorld = b0, tipWorld = b1, swingDirWorld = swing.sqrMagnitude > 1e-8f ? swing.normalized : Vector2.left,
                    rawContactWorld = Vector2.Lerp(b0, b1, 0.8f), level = lvl, point = point, isBlade = false, hasSegment = true,
                };
            }

            var sweep = LimbBlow(s.sweep, 11f, HitPoint.FootN, HitLevel.Low);
            var sweepPose = new Pose();
            var sweepSol = ParryContactSolver.Solve(sweep, Spec(frameDef), PoseLib.Block(), sweepPose, new PoseFK(Rig));
            AssertSound("sweep f11", sweep, sweepSol, sweepPose, frameDef);

            var jab = LimbBlow(s.jab, 7f, HitPoint.Fist, HitLevel.High);
            var jabPose = new Pose();
            var jabSol = ParryContactSolver.Solve(jab, Spec(frameDef), PoseLib.Block(), jabPose, new PoseFK(Rig));
            AssertSound("jab f7", jab, jabSol, jabPose, frameDef);
        }

        [Test]
        public void A_low_blow_braces_the_stance_low_and_an_overhead_one_upright_with_the_hips_never_turning()
        {
            var frame = RootFrame.Of(Vector2.zero, 1f, 1f);
            var low = ParryContactSolver.Solve(Blow("lowReap", 10.5f), Spec(frame), PoseLib.Block(), new Pose(), new PoseFK(Rig));
            var high = ParryContactSolver.Solve(Blow("cleave", 20f), Spec(frame), PoseLib.Block(), new Pose(), new PoseFK(Rig));
            Assert.Less(low.strikeY, ParryContactSolver.LowY);
            Assert.Less(low.hipOffsetDelta.y, 0f, "a low blow sinks the stance");
            Assert.Greater(high.strikeY, ParryContactSolver.HighY);
            Assert.GreaterOrEqual(high.hipOffsetDelta.y, 0f, "an overhead braces upright");
            Assert.AreEqual(0f, low.hipRotDelta);
            Assert.AreEqual(0f, high.hipRotDelta);
            Assert.Less(low.clashRoot.y, high.clashRoot.y, "the low parry meets lower than the overhead one");
        }

        [TestCase("iai", 9f)]
        [TestCase("cleave", 20f)]
        [TestCase("lowReap", 10.5f)]
        public void Facing_left_solves_the_mirror_of_facing_right(string moveId, float frame)
        {
            var right = RootFrame.Of(Vector2.zero, 1f, 1f);
            var left = RootFrame.Of(Vector2.zero, -1f, 1f);
            var a = ParryContactSolver.Solve(Blow(moveId, frame), Spec(right), PoseLib.Block(), new Pose(), new PoseFK(Rig));
            var b = ParryContactSolver.Solve(Blow(moveId, frame, mirror: true), Spec(left), PoseLib.Block(), new Pose(), new PoseFK(Rig));
            Assert.AreEqual(a.clashRoot.x, b.clashRoot.x, 1e-3f);
            Assert.AreEqual(a.clashRoot.y, b.clashRoot.y, 1e-3f);
            Assert.AreEqual(a.defenderBladeAngleRoot, b.defenderBladeAngleRoot, 1e-2f);
            Assert.AreEqual(-a.clashWorld.x, b.clashWorld.x, 1e-3f, "the world clash mirrored");
            Assert.AreEqual(-a.clashNormalWorld.x, b.clashNormalWorld.x, 1e-3f, "and its normal");
        }

        [Test]
        public void A_scaled_up_defender_solves_the_same_parry_in_its_own_root_space()
        {
            const float scale = 1.24f;      // Raikon
            var unit = RootFrame.Of(Vector2.zero, 1f, 1f);
            var big = RootFrame.Of(Vector2.zero, 1f, scale);
            var g = Blow("iai", 9f);
            var gBig = g;
            gBig.baseWorld *= scale; gBig.tipWorld *= scale; gBig.rawContactWorld *= scale;
            var pose = new Pose();
            var a = ParryContactSolver.Solve(g, Spec(unit), PoseLib.Block(), new Pose(), new PoseFK(Rig));
            var b = ParryContactSolver.Solve(gBig, Spec(big), PoseLib.Block(), pose, new PoseFK(Rig));
            Assert.AreEqual(a.clashRoot.x, b.clashRoot.x, 1e-3f);
            Assert.AreEqual(a.clashRoot.y, b.clashRoot.y, 1e-3f);
            var k = new PoseFK(Rig).Solve(pose);
            float gap = SegmentGap(big.ToWorld(k.Point(B.Sword, Rig.bladeBaseLocal)), big.ToWorld(k.Point(B.Sword, Rig.bladeTipLocal)), gBig.baseWorld, gBig.tipWorld);
            Assert.Less(gap, Gap * scale, "steel on steel at 1.24x");
        }

        [Test]
        public void A_blow_out_of_reach_is_met_at_the_nearest_point_the_arm_can_reach_without_breaking_it()
        {
            var frame = RootFrame.Of(Vector2.zero, 1f, 1f);
            var g = new StrikeGeometry
            {
                baseWorld = new Vector2(3.4f, 1.3f), tipWorld = new Vector2(2.6f, 1.1f), swingDirWorld = Vector2.left,
                rawContactWorld = new Vector2(2.6f, 1.1f), level = HitLevel.Mid, point = HitPoint.Blade, isBlade = true, hasSegment = true,
            };
            var pose = new Pose();
            var sol = ParryContactSolver.Solve(g, Spec(frame), PoseLib.Block(), pose, new PoseFK(Rig));
            Assert.IsTrue(sol.valid);
            Assert.IsTrue(sol.clampedToReach, "nothing could reach it");
            var k = new PoseFK(Rig).Solve(pose);
            float reach = (k.pos[(int)B.HandF] - k.pos[(int)B.ArmFU]).magnitude;
            Assert.That(reach, Is.InRange(ArmMin - 0.002f, ArmMax + 0.002f), "the arm stays attached and unbroken");
            foreach (var p in k.pos) Assert.IsFalse(float.IsNaN(p.x) || float.IsNaN(p.y), "no joint lost");
            Assert.Greater(sol.clashRoot.x, k.pos[(int)B.ArmFU].x, "it reaches out towards the blow");
            var guard = new PoseFK(Rig).Solve(PoseLib.Block());
            float Side(PoseFK f) => Mathf.Sign(Cross(f.pos[(int)B.HandF] - f.pos[(int)B.ArmFU], f.pos[(int)B.ArmFL] - f.pos[(int)B.ArmFU]));
            Assert.AreEqual(Side(guard), Side(k), "the elbow bends the way it does in the guard, never backwards");
        }

        [Test]
        public void A_synthetic_blow_with_no_segment_is_still_parried_blade_on_blade()
        {
            var frame = RootFrame.Of(Vector2.zero, 1f, 1f);
            foreach (var level in new[] { HitLevel.High, HitLevel.Mid, HitLevel.Low, HitLevel.Overhead })
            {
                var g = new StrikeGeometry { rawContactWorld = new Vector2(0.05f, level == HitLevel.Low ? 0.4f : 1.1f), level = level };
                var sol = ParryContactSolver.Solve(g, Spec(frame), PoseLib.Block(), new Pose(), new PoseFK(Rig));
                Assert.IsTrue(sol.valid, level.ToString());
                Assert.Less(sol.contactGap, Gap, $"{level}: the made-up blade is met");
            }
        }

        [Test]
        public void Twin_blades_meet_with_the_far_blade_and_cross_the_near_one_under_it()
        {
            var frame = RootFrame.Of(Vector2.zero, 1f, 1f);
            var spec = Spec(frame);
            spec.dual = true;
            spec.gripHeld = false;
            var pose = new Pose();
            var g = Blow("iai", 9f);
            var sol = ParryContactSolver.Solve(g, spec, PoseLib.Block(), pose, new PoseFK(Rig));
            Assert.IsTrue(sol.valid);
            Assert.IsTrue(pose.dual && pose.hasSwNAbs, "the near blade is posed");
            Assert.GreaterOrEqual(ParryContactSolver.CrossAngle(sol.nearBladeAngleRoot, sol.defenderBladeAngleRoot), 30f, "the two blades cross");
            Assert.Less(sol.contactGap, Gap);
        }

        [Test]
        public void Bare_hands_meet_the_blow_on_the_forearm()
        {
            var frame = RootFrame.Of(Vector2.zero, 1f, 1f);
            var spec = Spec(frame);
            spec.bareHanded = true;
            spec.gripHeld = false;
            var guard = PoseLib.FistGuard(PoseLib.Block());
            var pose = new Pose();
            var g = Blow("iai", 9f);
            var sol = ParryContactSolver.Solve(g, spec, guard, pose, new PoseFK(Rig));
            Assert.IsTrue(sol.valid);
            var k = new PoseFK(Rig).Solve(pose);
            Vector2 elbow = k.pos[(int)B.ArmFL], fist = k.Point(B.HandF, Rig.BindLocal(B.Sword));
            Assert.Less(SegmentGap(elbow, fist, g.baseWorld, g.tipWorld), Gap, "the forearm on the blade");
        }

        [Test]
        public void The_solver_is_deterministic()
        {
            var frame = RootFrame.Of(new Vector2(0.3f, 0f), 1f, 1.06f);
            var g = Blow("shadowStep", 9f);
            var a = ParryContactSolver.Solve(g, Spec(frame), PoseLib.Block(), new Pose(), new PoseFK(Rig));
            var b = ParryContactSolver.Solve(g, Spec(frame), PoseLib.Block(), new Pose(), new PoseFK(Rig));
            Assert.AreEqual(a.clashWorld, b.clashWorld);
            Assert.AreEqual(a.wristTargetRoot, b.wristTargetRoot);
            Assert.AreEqual(a.defenderBladeAngleRoot, b.defenderBladeAngleRoot);
        }

        // ================================================================================== the reference kata

        /// <summary>A bare fighter for a Select (the player: no AI, no opponent).</summary>
        static Fighter Bare(GameObject root) => root.AddComponent<Fighter>();

        [Test]
        public void The_reference_kata_chain_from_the_katana_strings_and_Back_P()
        {
            var k = Katana;
            var root = new GameObject("Select");
            try
            {
            var self = Bare(root);
            Assert.AreSame(k.crownWheel, k.iai.chain[Btn.P], "S, P");
            Assert.AreSame(k.crownWheel, k.lowReap.chain[Btn.S], "Down + S, S");
            Assert.AreSame(k.crownWheel, k.cross.chain[Btn.S], "P, P, S");
            Assert.AreSame(k.tameCut, k.risingMoon.chain[Btn.P], "S, S, P");
            Assert.AreSame(k.tameCut, k.kick2.chain[Btn.S], "K, K, S");
            Assert.AreSame(k.tameCut, k.crownWheel.chain[Btn.S], "Crown Wheel, S");
            Assert.LessOrEqual(k.lowReap.cancelFrom, 14);
            Assert.AreSame(k.crownWheel, k.Select(Btn.S, 0, -1, false, self), "S + L (Down + Weapon) is Crown Wheel directly");
            Assert.AreSame(k.tameCut, k.Select(Btn.P, -1, 0, false, self), "Back + J (Back + Punch) is Poised Fang directly");
            Assert.AreSame(k.jab, k.Select(Btn.P, 0, 0, false, self), "neutral P is still the jab");
            Assert.AreSame(k.jab, k.BareHanded(k.tameCut), "bare-handed, Back + P stays the jab");
            Assert.IsTrue(k.crownWheel.profiledOnly && k.tameCut.profiledOnly);
            CollectionAssert.Contains(k.All.ToList(), k.crownWheel);
            CollectionAssert.Contains(k.All.ToList(), k.tameCut);

            var t = (TachiMoves)new MoveSets().Get(WeaponFamilies.Tachi);
            Assert.AreSame(t.crownWheel, t.cut1.chain[Btn.P]);
            Assert.AreSame(t.crownWheel, t.sweep.chain[Btn.S]);
            Assert.AreSame(t.sweep, t.lowKick.chain[Btn.S]);
            Assert.AreSame(t.tameCut, t.cut2.chain[Btn.P]);
            Assert.AreSame(t.tameCut, t.crownWheel.chain[Btn.S]);
            Assert.AreSame(t.crownWheel, t.Select(Btn.S, 0, -1, false, self), "Tachi S + L is Tempest Coil directly");
            Assert.AreSame(t.tameCut, t.Select(Btn.P, -1, 0, false, self));
            CollectionAssert.Contains(t.All.ToList(), t.crownWheel);
            CollectionAssert.Contains(t.All.ToList(), t.tameCut);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void The_fists_keep_their_jab_on_Back_P_and_their_kick_string()
        {
            var sets = new MoveSets();
            var fists = sets.Get(WeaponFamilies.Fists);
            var k = (KatanaMoves)sets.Get(WeaponFamilies.Katana);
            var root = new GameObject("Select");
            try { Assert.AreSame(k.jab, fists.Select(Btn.P, -1, 0, false, Bare(root))); }
            finally { Object.DestroyImmediate(root); }
            Assert.IsNull(fists.BareHanded(k.tameCut), "the Switch Kick's Weapon chain plays nothing bare-handed");
        }

        [Test]
        public void A_legacy_AI_keeps_to_its_old_moves_and_a_profiled_one_throws_the_kata()
        {
            var k = Katana;
            var root = new GameObject("LegacyAi");
            try
            {
                var f = root.AddComponent<Fighter>();
                f.input = new AIInputSource();
                Assert.AreSame(k.jab, k.Select(Btn.P, -1, 0, false, f), "legacy: Back + P is the jab it always was");
                Assert.AreSame(k.lowReap, k.Select(Btn.S, 0, -1, false, f), "legacy: Down + Weapon stays Low Reap");
                f.input = new AIInputSource { profile = new AiProfile() };
                Assert.AreSame(k.tameCut, k.Select(Btn.P, -1, 0, false, f), "profiled: the Poised Fang");
                Assert.AreSame(k.crownWheel, k.Select(Btn.S, 0, -1, false, f), "profiled: Crown Wheel on Down + Weapon");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Crown_Wheel_is_a_low_then_mid_two_beat_and_Poised_Fang_a_held_heavy_overhead()
        {
            var k = Katana;
            Assert.AreEqual(2, k.crownWheel.hits.Count);
            Assert.AreEqual(HitLevel.Low, k.crownWheel.hits[0].level);
            Assert.AreEqual(HitLevel.Mid, k.crownWheel.hits[1].level);
            Assert.IsTrue(k.crownWheel.hits[1].heavy);
            Assert.Less(k.crownWheel.hits[0].to, k.crownWheel.hits[1].from, "two beats");
            Assert.AreEqual(1, k.tameCut.hits.Count);
            Assert.AreEqual(HitLevel.Overhead, k.tameCut.hits[0].level);
            Assert.IsTrue(k.tameCut.hits[0].heavy);
            Assert.AreEqual(1.45f, k.tameCut.hits[0].postureMul, 1e-6f);
            // the tame: the blade held behind the crown before the cut (the pause baits an early parry)
            var start = PoseLib.Stance();
            float held = 0;
            for (int f = 17; f <= 24; f++)
            {
                var fk = new PoseFK(Rig);
                var p = new Pose();
                k.tameCut.Evaluate(Rig, f, start, p);
                if (fk.Solve(p).BladeAngle > 130f) held++;
            }
            Assert.GreaterOrEqual(held, 6, "held poised behind the crown for most of frames 17-24");
            foreach (var m in new[] { k.crownWheel, k.tameCut })
                foreach (var key in m.keys) Assert.LessOrEqual(Mathf.Abs(key.pose.a[(int)B.Hips]), 12f, $"{m.id} key {key.frame}: upright");
        }

        // ================================================================================== real headless fights, both ways

        sealed class Puppet : IInputSource
        {
            public FrameInput next;
            public void Sample() { }
            public FrameInput Poll(Fighter self) => next;
        }

        sealed class Duel
        {
            public FightLoop loop;
            public FightContext ctx;
            public Fighter kage, foe;
            public Puppet kageIn, foeIn;
        }

        GameObject _root;
        SimulationMode2D _simMode;
        readonly List<(string id, Vector2 pos, Vector2 dir)> _cues = new();

        void OnCue(string id, Vector2 pos, in CueArgs a) => _cues.Add((id, pos, a.dir));

        [SetUp]
        public void SetUp()
        {
            _simMode = Physics2D.simulationMode;
            _cues.Clear();
            EffectCues.Played += OnCue;
        }

        [TearDown]
        public void TearDown()
        {
            EffectCues.Played -= OnCue;
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
            GameFeel.ManualClock = null;
            Physics2D.simulationMode = _simMode;
            Tuning.ClearOverrides();
        }

        static ParryTuning T => Tuning.Get<ParryTuning>();

        Duel Fight(string foeId, float dist)
        {
            if (_root != null) Object.DestroyImmediate(_root);
            _root = new GameObject("DynamicParry");
            var b = new FightBuilder(_root.transform, 1337, manualClock: true);
            b.AddGround();
            var kageIn = new Puppet();
            var foeIn = new Puppet();
            var kage = b.Spawn(CharacterCatalog.Get(KageCharacter.Id), 0, kageIn);
            var foe = b.Spawn(CharacterCatalog.Get(foeId), 1, foeIn);
            var ctx = b.Build();
            ctx.Begin();
            kage.ResetForRound(-dist * 0.5f, 1f); kage.BeginFight();
            foe.ResetForRound(dist * 0.5f, -1f); foe.BeginFight();
            b.Loop.StepHeadless();
            return new Duel { loop = b.Loop, ctx = ctx, kage = kage, foe = foe, kageIn = kageIn, foeIn = foeIn };
        }

        static MoveDef MoveOf(Fighter f, string id)
        {
            if (id == null) return f.moves.Select(Btn.S, 0, 0, false, f);
            return f.moves.All.First(m => m != null && m.id == id);
        }

        /// <summary>The first window of <paramref name="m"/> a parry can stop.</summary>
        static int FirstParryable(MoveDef m)
        {
            for (int i = 0; i < m.hits.Count; i++) if (m.hits[i].level != HitLevel.Unblockable) return i;
            return -1;
        }

        /// <summary>
        /// The attacker plays <paramref name="m"/>; with <paramref name="parry"/> the defender presses Guard a few frames before the move time
        /// <paramref name="pressBefore"/> (the blow's contact; window <paramref name="window"/>'s first frame when below 0). Runs until that window
        /// resolves on the defender (or the move ends) and returns its event; <paramref name="hitTime"/> is the attacker's move time then.
        /// </summary>
        static CombatEvent? Exchange(Duel d, Fighter attacker, MoveDef m, int window, bool parry, CombatEventLog log, out float hitTime, float pressBefore = -1f)
        {
            hitTime = -1f;
            var defender = attacker == d.kage ? d.foe : d.kage;
            var defIn = defender == d.kage ? d.kageIn : d.foeIn;
            var w = m.hits[window];
            float pressAt = (pressBefore >= 0f ? pressBefore : w.from) - 3.5f;
            attacker.StartMove(m);
            bool pressed = false;
            int seen = log.All.Count;
            for (int i = 0; i < 400; i++)
            {
                if (parry && !pressed && attacker.IsAttacking && attacker.move == m && attacker.MoveTime >= pressAt) pressed = true;
                defIn.next = new FrameInput { guard = pressed };
                float before = attacker.MoveTime;
                d.loop.StepHeadless();
                for (int e = seen; e < log.All.Count; e++)
                {
                    var ev = log.All[e];
                    if (ev.Source == attacker && ev.Target == defender && ev.MoveId == m.id && ev.Window == window)
                    {
                        hitTime = attacker.IsAttacking && attacker.move == m ? attacker.MoveTime : before;
                        return ev;
                    }
                }
                seen = log.All.Count;
                if (!attacker.IsAttacking || attacker.move != m) return null;
            }
            return null;
        }

        static CombatEvent? Exchange(Duel d, Fighter attacker, MoveDef m, int window, bool parry, CombatEventLog log) =>
            Exchange(d, attacker, m, window, parry, log, out _);

        /// <summary>Farthest first: a blow is tried at its own fighting distance, where its blade (not its hilt) does the cutting.</summary>
        static readonly float[] Spacings = { 2.0f, 1.8f, 1.6f, 1.4f, 1.2f, 1.0f, 0.8f, 0.65f };

        /// <summary>Window <paramref name="window"/> lands on an unguarding defender at <paramref name="dist"/>; <paramref name="hitTime"/> is the attacker's move time then.</summary>
        bool Lands(string foeId, bool kageAttacks, string moveId, float dist, out int window, out float hitTime)
        {
            var d = Fight(foeId, dist);
            var attacker = kageAttacks ? d.kage : d.foe;
            var m = MoveOf(attacker, moveId);
            window = FirstParryable(m);
            Assert.GreaterOrEqual(window, 0, $"{m.id} has a window a parry can stop");
            using var log = new CombatEventLog(d.ctx.Events);
            var ev = Exchange(d, attacker, m, window, false, log, out hitTime);
            return ev.HasValue && ev.Value.Type is CombatEventType.Hit or CombatEventType.Blocked;
        }

        /// <summary>
        /// A fighting distance for the blow: a step (0.2 m) inside the farthest spacing at which window <paramref name="window"/> lands on an
        /// unguarding defender, so its blade (not its hilt) cuts, and a guard's lean doesn't take the defender out of reach. <paramref name="hitTime"/>
        /// is the attacker's move time at the contact there, so the parry's Guard press can be timed on the real blow.
        /// </summary>
        float Reach(string foeId, bool kageAttacks, string moveId, out int window, out float hitTime)
        {
            window = -1;
            hitTime = -1f;
            foreach (float far in Spacings)
            {
                if (!Lands(foeId, kageAttacks, moveId, far, out window, out float farTime)) continue;
                float near = Mathf.Max(0.65f, far - 0.2f);
                if (near < far && Lands(foeId, kageAttacks, moveId, near, out window, out hitTime)) return near;
                hitTime = farTime;
                return far;
            }
            Assert.Fail($"{moveId ?? "the plain Weapon move"} of {(kageAttacks ? "Kage" : foeId)} never reached at any spacing");
            return 0f;
        }

        float Reach(string foeId, bool kageAttacks, string moveId, out int window) => Reach(foeId, kageAttacks, moveId, out window, out _);

        /// <summary>The striking segments of a fighter as posed now (its blades, fists and shins): the parried one is among them.</summary>
        static IEnumerable<(Vector2 a, Vector2 b)> Segments(Fighter f)
        {
            var r = f.rig;
            if (f.HasBlade) yield return (r.World(B.Sword, f.BladeBaseLocal), r.World(B.Sword, f.BladeTipLocal));
            if (f.Dual) yield return (r.World(B.HandN, f.NearBladeBaseLocal), r.World(B.HandN, f.NearBladeTipLocal));
            yield return (r.World(B.ArmNL, Vector2.zero), r.World(B.HandN, f.def.fistLocal));
            yield return (r.World(B.ArmFL, Vector2.zero), r.World(B.HandF, f.def.BindLocal(B.Sword)));
            yield return (r.World(B.ArmFL, Vector2.zero), r.World(B.HandF, f.def.hiltLocal));
            yield return (r.World(B.LegNL, Vector2.zero), r.World(B.FootN, f.def.footTipLocal));
            yield return (r.World(B.LegFL, Vector2.zero), r.World(B.FootF, f.def.footTipLocal));
        }

        static (Vector2 a, Vector2 b) DefenderBlade(Fighter f) =>
            (f.rig.World(B.Sword, f.BladeBaseLocal), f.rig.World(B.Sword, f.BladeTipLocal));

        static float LockGap(Fighter defender, Fighter attacker)
        {
            var (d0, d1) = DefenderBlade(defender);
            return Segments(attacker).Min(s => SegmentGap(d0, d1, s.a, s.b));
        }

        static Vector2 AttackerTip(Fighter f) => f.HasBlade ? (Vector2)f.rig.World(B.Sword, f.BladeTipLocal) : (Vector2)f.rig.World(B.HandF, f.def.BindLocal(B.Sword));

        /// <summary>
        /// A full parry exchange at a spacing the blow reaches, judged end to end: the event and the cue on the blades' crossing, the blades
        /// locked through the hit-stop, the parried weapon recoiling along the clash normal and the parrier settling back into its guard.
        /// </summary>
        void ParryExchange(string foeId, bool kageDefends, string moveId)
        {
            CharacterCatalog.Get(foeId);
            float dist = Reach(foeId, !kageDefends, moveId, out int window, out float hitTime);
            var d = Fight(foeId, dist);
            var attacker = kageDefends ? d.foe : d.kage;
            var defender = kageDefends ? d.kage : d.foe;
            var m = MoveOf(attacker, moveId);
            string who = $"{(kageDefends ? "Kage parries" : foeId + " parries")} {m.id}@{dist}";
            _cues.Clear();
            using var log = new CombatEventLog(d.ctx.Events);
            var ev = Exchange(d, attacker, m, window, true, log, out _, hitTime);
            Assert.IsTrue(ev.HasValue, $"{who}: the blow resolved");
            Assert.AreEqual(CombatEventType.Parried, ev.Value.Type, $"{who}: parried");

            // the clash point: on the blades, in front of the parrier, not on its hurt circle; the cue plays there along the normal
            var sol = defender.LastParrySolution;
            Assert.IsTrue(sol.valid, $"{who}: solved");
            Assert.IsTrue(defender.HasParryContact);
            Assert.AreEqual(defender.LastParryContact.x, ev.Value.Contact.x, 1e-4f, $"{who}: the event carries the clash point");
            Assert.AreEqual(defender.LastParryContact.y, ev.Value.Contact.y, 1e-4f);
            string cue = kageDefends ? Fighter.ParryCue : Fighter.FoeParryCue;
            var played = _cues.Where(c => c.id == cue).ToList();
            Assert.AreEqual(1, played.Count, $"{who}: one {cue}");
            Assert.Less((played[0].pos - sol.clashWorld).magnitude, 1e-4f, $"{who}: the cue sits on the clash point");
            Assert.AreEqual(1f, played[0].dir.magnitude, 1e-3f, $"{who}: aimed along the clash normal");
            Assert.Less(InsideBody(defender, ev.Value.Contact), 0f, $"{who}: met off the body, outside its body hurt circles");
            // met in front of the parrier, or (a blow from above) over its head: a roof parry
            var headHurt = defender.def.hurt.First(h => h.zone == HitZone.Head);
            float crown = defender.rig.World(headHurt.bone, headHurt.local).y + headHurt.radius * Mathf.Abs(defender.transform.localScale.y);
            float ahead = (ev.Value.Contact.x - defender.Pos.x) * defender.facing;
            Assert.IsTrue(ahead > 0.05f || ev.Value.Contact.y > crown,
                $"{who}: met in front of the parrier or over its head (ahead {ahead:0.000} m, height {ev.Value.Contact.y:0.00} against the crown at {crown:0.00}; " +
                $"clash root {sol.clashRoot}, blade {sol.defenderBladeAngleRoot:0}°, blow read at {sol.strikeY:0.00}, segment {sol.strikeBaseRoot}->{sol.strikeTipRoot})");
            if (!sol.clampedToReach) Assert.GreaterOrEqual(sol.crossAngleDeg, ParryContactSolver.MinCross, $"{who}: a real cross");

            // locked steel on steel through the hit-stop
            Assert.AreEqual(DynamicParryController.Role.Defender, defender.ParryController.Mode, $"{who}: the parrier's blade on the clash point");
            Assert.AreEqual(DynamicParryController.Role.Attacker, attacker.ParryController.Mode, $"{who}: the attacker's weapon locked");
            int frozen = 0;
            for (int i = 0; i < 6 && frozen < 3; i++) if (!d.loop.StepHeadless()) frozen++;
            Assert.AreEqual(3, frozen, $"{who}: the hit-stop holds");
            Assert.AreEqual(1f, defender.ParryController.Weight, 1e-3f, $"{who}: snapped fully in by the third held frame");
            float gap = LockGap(defender, attacker);
            if (!sol.clampedToReach) Assert.Less(gap, Gap * Mathf.Max(1f, defender.transform.localScale.y), $"{who}: steel on steel in the hit-stop ({gap * 100f:0.0} cm)");
            Vector2 tipHeld = AttackerTip(attacker);

            // after the hit-stop: the parried weapon kicks back along the normal
            int guard = 0;
            while (!d.loop.StepHeadless() && guard++ < 40) { }
            d.loop.StepHeadless();
            Vector2 tipAfter = AttackerTip(attacker);
            Assert.Greater(Vector2.Dot(tipAfter - tipHeld, sol.clashNormalWorld), 0.02f, $"{who}: the parried weapon recoils along the clash normal");

            // the parrier settles back into its guard
            for (int i = 0; i < T.contactHoldFrames + T.contactRecoverFrames + 4; i++) d.loop.StepHeadless();
            Assert.AreEqual(DynamicParryController.Role.None, defender.ParryController.Mode, $"{who}: back in its own pose");
            Assert.AreEqual(0f, defender.ParryController.Weight, $"{who}: the overlay gone");
        }

        [TestCase("order_footman", "iai")]
        [TestCase("order_footman", "cleave")]
        [TestCase("order_footman", "crownWheel")]
        [TestCase("order_footman", "tameCut")]
        [TestCase("order_runner", null)]
        [TestCase("order_shieldbearer", null)]
        [TestCase(OrderIronclad.Id, null)]
        [TestCase("order_chainthrower", null)]
        [TestCase(Garan.Id, null)]
        [TestCase(Hisame.Id, null)]
        [TestCase(Kuroe.Id, null)]
        [TestCase(Shiden.Id, null)]
        [TestCase(Raikon.Id, "tachi.tameCut")]
        [TestCase(Raikon.Id, "tachi.crownWheel")]
        public void Kage_parries_blade_on_blade(string foeId, string moveId) => ParryExchange(foeId, true, moveId);

        [TestCase("order_footman", "iai")]
        [TestCase("order_footman", "risingMoon")]
        [TestCase("order_footman", "cleave")]
        [TestCase("order_footman", "lowReap")]
        [TestCase("order_footman", "crownWheel")]
        [TestCase("order_footman", "tameCut")]
        [TestCase(Kuroe.Id, "iai")]
        [TestCase(Hisame.Id, "cleave")]
        [TestCase(Raikon.Id, "tameCut")]
        public void A_foe_parries_Kage_blade_on_blade(string foeId, string moveId) => ParryExchange(foeId, false, moveId);

        [Test]
        public void An_answer_cancels_the_parry_pose_at_once()
        {
            float dist = Reach("order_footman", false, "iai", out int window, out float hitTime);
            var d = Fight("order_footman", dist);
            var m = MoveOf(d.foe, "iai");
            using var log = new CombatEventLog(d.ctx.Events);
            var ev = Exchange(d, d.foe, m, window, true, log, out _, hitTime);
            Assert.AreEqual(CombatEventType.Parried, ev?.Type);
            int guard = 0;
            while (!d.loop.StepHeadless() && guard++ < 40) { }
            Assert.AreEqual(DynamicParryController.Role.Defender, d.kage.ParryController.Mode, "still holding the clash after the hit-stop");
            d.kageIn.next = new FrameInput { guard = false, s = true };
            d.loop.StepHeadless();
            d.kageIn.next = default;
            d.loop.StepHeadless();
            Assert.AreEqual(FState.Attack, d.kage.state, "the answer comes out with no lag");
            Assert.AreEqual(DynamicParryController.Role.None, d.kage.ParryController.Mode, "and the clash pose gives way to it");
        }

        [Test]
        public void Kages_Crown_Wheel_lands_low_then_mid_on_an_open_foe()
        {
            float dist = Reach("order_footman", true, "crownWheel", out _);
            var d = Fight("order_footman", dist);
            var m = MoveOf(d.kage, "crownWheel");
            using var log = new CombatEventLog(d.ctx.Events);
            d.kage.StartMove(m);
            for (int i = 0; i < 200 && d.kage.IsAttacking && d.kage.move == m; i++) d.loop.StepHeadless();
            var hits = log.All.Where(e => e.Type == CombatEventType.Hit && e.Source == d.kage && e.MoveId == m.id).ToList();
            Assert.AreEqual(2, hits.Count, "both beats land");
            Assert.AreEqual(HitLevel.Low, hits[0].Level);
            Assert.AreEqual(HitLevel.Mid, hits[1].Level);
        }

        [Test]
        public void Kages_Poised_Fang_lands_as_a_heavy_overhead()
        {
            float dist = Reach("order_footman", true, "tameCut", out _);
            var d = Fight("order_footman", dist);
            var m = MoveOf(d.kage, "tameCut");
            using var log = new CombatEventLog(d.ctx.Events);
            var ev = Exchange(d, d.kage, m, 0, false, log);
            Assert.AreEqual(CombatEventType.Hit, ev?.Type);
            Assert.AreEqual(HitLevel.Overhead, ev.Value.Level);
        }

        [Test]
        public void Anticipation_tracks_an_incoming_blow_in_front_with_arms_only_and_ignores_one_behind()
        {
            var d = Fight("order_footman", 1.5f);
            // baseline guard without any incoming attack (same 6 idle + 4 guard timeline as below)
            for (int i = 0; i < 6; i++) d.loop.StepHeadless();
            d.kageIn.next = new FrameInput { guard = true };
            for (int i = 0; i < 4; i++) d.loop.StepHeadless();
            Vector2 baseHips = d.kage.rig.World(B.Hips, Vector2.zero);
            Vector2 baseChest = d.kage.rig.World(B.Chest, Vector2.zero);
            Vector2 baseHead = d.kage.rig.World(B.Head, Vector2.zero);
            Assert.AreEqual(0f, d.kage.ParryController.AnticipationWeight, 1e-4f, "no anticipation when the foe is idle");

            // fresh fight: foe starts overhead cleave in front while Kage opens his parry window
            d = Fight("order_footman", 1.5f);
            d.foe.StartMove(MoveOf(d.foe, "cleave"));
            for (int i = 0; i < 6; i++) d.loop.StepHeadless();
            d.kageIn.next = new FrameInput { guard = true };
            for (int i = 0; i < 4; i++) d.loop.StepHeadless();
            Assert.Greater(d.kage.ParryController.AnticipationWeight, 0.05f, "anticipation engages while a foe attacks in front");
            Assert.Less(((Vector2)d.kage.rig.World(B.Hips, Vector2.zero) - baseHips).magnitude, 1e-3f, "hips unchanged (hurtboxes preserved)");
            Assert.Less(((Vector2)d.kage.rig.World(B.Chest, Vector2.zero) - baseChest).magnitude, 1e-3f, "chest unchanged");
            Assert.Less(((Vector2)d.kage.rig.World(B.Head, Vector2.zero) - baseHead).magnitude, 1e-3f, "head unchanged");
            float armReach = ((Vector2)d.kage.rig.World(B.HandF, Vector2.zero) - (Vector2)d.kage.rig.World(B.ArmFU, Vector2.zero)).magnitude;
            Assert.That(armReach, Is.InRange(ArmMin - 0.005f, ArmMax + 0.005f), "anticipated arm stays inside anatomical reach");

            // foe attacking behind Kage while his parry window is open: anticipation ignores it
            d.kage.ParryController.Reset();
            d.kage.SetFacing(-1f, true);
            for (int i = 0; i < 4; i++) d.kage.PoseOutput(0f, Time.fixedDeltaTime, 1f + i * Time.fixedDeltaTime, Time.fixedDeltaTime);
            Assert.IsTrue(d.kage.InParryWindow, "parry window still open");
            Assert.AreEqual(0f, d.kage.ParryController.AnticipationWeight, 1e-4f, "ignores a foe swinging behind the defender");
        }

        [Test]
        public void Kage_parries_Crown_Wheel_second_beat_mid()
        {
            float dist = -1f, hitTime = -1f;
            foreach (float cand in new[] { 1.95f, 1.85f, 1.75f, 1.65f, 1.55f, 1.45f, 1.35f })
            {
                var probe = Fight("order_footman", cand);
                var cw = MoveOf(probe.foe, "crownWheel");
                using var probeLog = new CombatEventLog(probe.ctx.Events);
                probe.foe.StartMove(cw);
                for (int i = 0; i < 120 && probe.foe.IsAttacking && probe.foe.move == cw; i++)
                {
                    float before = probe.foe.MoveTime;
                    probe.loop.StepHeadless();
                    if (hitTime < 0f && probeLog.All.Any(e => e.MoveId == cw.id && e.Window == 1)) hitTime = before;
                }
                bool hitBeat0 = probeLog.All.Any(e => e.MoveId == cw.id && e.Window == 0);
                bool hitBeat1 = probeLog.All.Any(e => e.MoveId == cw.id && e.Window == 1);
                if (!hitBeat0 && hitBeat1) { dist = cand; break; }
                hitTime = -1f;
            }
            Assert.Greater(dist, 0f, "found a mid-range spacing where Beat 1 whiffs and Beat 2 lunges in");

            var d = Fight("order_footman", dist);
            var m = MoveOf(d.foe, "crownWheel");
            _cues.Clear();
            using var log = new CombatEventLog(d.ctx.Events);
            var ev = Exchange(d, d.foe, m, 1, true, log, out _, hitTime);
            Assert.IsTrue(ev.HasValue && ev.Value.Type == CombatEventType.Parried, "Kage parries Crown Wheel Beat 2");
            Assert.AreEqual(HitLevel.Mid, ev.Value.Level);
            var sol = d.kage.LastParrySolution;
            Assert.IsTrue(sol.valid);
            Assert.AreEqual(d.kage.LastParryContact.x, ev.Value.Contact.x, 1e-4f);
            Assert.AreEqual(d.kage.LastParryContact.y, ev.Value.Contact.y, 1e-4f);
            int frozen = 0;
            for (int i = 0; i < 6 && frozen < 3; i++) if (!d.loop.StepHeadless()) frozen++;
            Assert.AreEqual(3, frozen);
            if (!sol.clampedToReach) Assert.Less(LockGap(d.kage, d.foe), Gap, "steel on steel on Beat 2 during hit-stop");
        }

        [Test]
        public void A_foe_parries_Kages_aerial_cleave()
        {
            float dist = -1f, hitTime = -1f;
            foreach (float cand in new[] { 1.4f, 1.25f, 1.1f, 0.95f, 0.8f })
            {
                var probe = Fight("order_footman", cand);
                var ac = MoveOf(probe.kage, "airCleave");
                Vector2 airPos = new(-cand * 0.5f, 0.72f);
                probe.kage.rb.position = airPos;
                probe.kage.transform.position = airPos;
                probe.kage.rb.linearVelocity = new Vector2(0f, 2.2f);
                using var probeLog = new CombatEventLog(probe.ctx.Events);
                var pev = Exchange(probe, probe.kage, ac, 0, false, probeLog, out hitTime);
                if (pev.HasValue && pev.Value.Type is CombatEventType.Hit or CombatEventType.Blocked) { dist = cand; break; }
            }
            Assert.Greater(dist, 0f, "found an aerial spacing where airCleave reaches");

            var d = Fight("order_footman", dist);
            var m = MoveOf(d.kage, "airCleave");
            Vector2 startAir = new(-dist * 0.5f, 0.72f);
            d.kage.rb.position = startAir;
            d.kage.transform.position = startAir;
            d.kage.rb.linearVelocity = new Vector2(0f, 2.2f);
            _cues.Clear();
            using var log = new CombatEventLog(d.ctx.Events);
            var ev = Exchange(d, d.kage, m, 0, true, log, out _, hitTime);
            Assert.IsTrue(ev.HasValue && ev.Value.Type == CombatEventType.Parried, "Footman parries Kage's airCleave");
            var sol = d.foe.LastParrySolution;
            Assert.IsTrue(sol.valid);
            Assert.AreEqual(d.foe.LastParryContact.x, ev.Value.Contact.x, 1e-4f);
            Assert.AreEqual(d.foe.LastParryContact.y, ev.Value.Contact.y, 1e-4f);
            var played = _cues.Where(c => c.id == Fighter.FoeParryCue).ToList();
            Assert.AreEqual(1, played.Count);
            Assert.Less((played[0].pos - sol.clashWorld).magnitude, 1e-4f);
            int frozen = 0;
            for (int i = 0; i < 6 && frozen < 3; i++) if (!d.loop.StepHeadless()) frozen++;
            Assert.AreEqual(3, frozen);
            if (!sol.clampedToReach) Assert.Less(LockGap(d.foe, d.kage), Gap, "steel on steel against aerial cleave during hit-stop");
        }

        [Test]
        public void A_parry_that_breaks_the_attackers_guard_anchors_GuardBroken_at_the_blade_crossing()
        {
            float dist = Reach("order_footman", false, "iai", out int window, out float hitTime);
            var d = Fight("order_footman", dist);
            d.foe.AddPosture(d.foe.PostureMax * 0.85f, d.kage);
            d.foe.FlushGuardBreak();
            var m = MoveOf(d.foe, "iai");
            using var log = new CombatEventLog(d.ctx.Events);
            var ev = Exchange(d, d.foe, m, window, true, log, out _, hitTime);
            Assert.AreEqual(CombatEventType.Parried, ev?.Type);
            var broken = log.All.Where(e => e.Type == CombatEventType.GuardBroken && e.Target == d.foe).ToList();
            Assert.AreEqual(1, broken.Count, "the parry filled the foe's posture bar and broke its guard");
            Assert.AreEqual(d.kage.LastParryContact.x, broken[0].Contact.x, 1e-4f, "GuardBroken anchors at the blade crossing");
            Assert.AreEqual(d.kage.LastParryContact.y, broken[0].Contact.y, 1e-4f);
        }
    }
}
