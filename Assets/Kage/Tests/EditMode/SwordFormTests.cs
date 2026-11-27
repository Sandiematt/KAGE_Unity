using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Kage's swordwork reads as real two-handed kenjutsu (SAN-118, docs/research/sword-mechanics.md). Judged on the posed
    /// skeleton (<see cref="PoseFK"/>) at every half frame of every katana move: both hands stay on the tsuka, a cut travels
    /// edge first (hasuji), a thrust travels along its own blade, Kasumi is held at head height, and the run carries the
    /// sword in both hands.
    /// </summary>
    public class SwordFormTests
    {
        static RigDef _rig;
        static RigDef Rig => _rig ??= KageModel.Skeleton("san118");
        static KatanaMoves Set => (KatanaMoves)new MoveSets().Get(WeaponFamilies.Katana);

        /// <summary>A fist wrapped round the handle: its centre within a fist's half-width of the handle's axis (TwoHandedGripTests' OnGrip).</summary>
        const float OnGrip = 0.035f;

        /// <summary>Hard cap on half-frame steps through a move (no move is longer than 200 frames).</summary>
        const int MaxSteps = 400;

        /// <summary>The pose a move starts from: chūdan, or Mirror Guard's Kasumi for the counter it answers with.</summary>
        static Pose StartOf(KatanaMoves s, MoveDef m)
        {
            var start = PoseLib.Stance();
            if (m != s.counterSlash) return start;
            var held = new Pose();
            s.counterStance.Evaluate(Rig, 15f, start, held);
            return held.Clone();
        }

        static PoseFK At(KatanaMoves s, MoveDef m, float frame)
        {
            var p = new Pose();
            m.Evaluate(Rig, frame, StartOf(s, m), p);
            return new PoseFK(Rig).Solve(p);
        }

        /// <summary>Every two-handed sword move of the katana set: the moves drawn with the sword in front (Pommel Strike holds it in both hands too).</summary>
        static IEnumerable<MoveDef> TwoHanded(KatanaMoves s) => s.All.Where(m => m != null && m.swordFront).Distinct();

        [Test]
        public void EveryFrameOfEveryTwoHandedKatanaMoveKeepsBothHandsOnTheGrip()
        {
            var s = Set;
            var bad = new List<string>();
            int moves = 0;
            foreach (var m in TwoHanded(s))
            {
                moves++;
                float worst = 0f; float worstAt = 0f;
                for (int i = 0; i <= Mathf.Min(m.total * 2, MaxSteps); i++)
                {
                    float d = At(s, m, i * 0.5f).NearFistToHandle();
                    if (d > worst) { worst = d; worstAt = i * 0.5f; }
                }
                if (worst > OnGrip) bad.Add($"{m.id}: near fist {worst * 100f:0.0} cm off the handle at frame {worstAt}");
            }
            Assert.GreaterOrEqual(moves, 9, "the katana's sword moves");
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        /// <summary>The frames a move's blade is live: its hit windows, or its blade trail when it has none (the Crescent's cut throws a projectile).</summary>
        static IEnumerable<(int from, int to)> LiveFrames(MoveDef m)
        {
            var w = m.hits.Where(h => h.sword).Select(h => (h.from, h.to)).ToList();
            if (w.Count == 0) w = m.fx.Where(f => f.kind == FxKind.TrailBlade).Select(f => (f.from, f.to)).ToList();
            return w;
        }

        /// <summary>The monouchi: the striking part of the blade, three quarters of the way to the tip.</summary>
        static Vector2 Monouchi(PoseFK k) => k.OnBlade(0.08f + 0.72f * 0.75f);

        /// <summary>The cuts (every katana blade move but the thrust and the pommel strike).</summary>
        static string[] Cuts => new[] { "iai", "risingMoon", "cleave", "shadowStep", "lowReap", "crescent", "airSlash", "airCleave", "crownWheel", "tameCut" };

        [Test]
        public void EveryCutTravelsEdgeFirstThroughItsLiveFrames()
        {
            var s = Set;
            var bad = new List<string>();
            foreach (var id in Cuts)
            {
                var m = s.All.First(x => x != null && x.id == id);
                int judged = 0;
                foreach (var (from, to) in LiveFrames(m))
                    for (int i = 0; i <= Mathf.Min((to - from) * 4, MaxSteps); i++)
                    {
                        float f = from + i * 0.25f;
                        var k = At(s, m, f);
                        Vector2 travel = Monouchi(At(s, m, f + 0.125f)) - Monouchi(At(s, m, f - 0.125f));
                        if (travel.magnitude < 0.004f) continue;                 // all but still (a held strike)
                        Vector2 dir = travel.normalized;
                        if (Mathf.Abs(Vector2.Dot(dir, KMath.Dir(k.BladeAngle))) > 0.8f) continue;   // a draw along the blade
                        judged++;
                        float off = Vector2.Angle(dir, k.EdgeNormal);
                        if (off > 55f) bad.Add($"{id} frame {f:0.00}: the edge is {off:0} degrees off the cut's travel");
                    }
                if (judged == 0) bad.Add($"{id}: no frame of its live window cuts (the blade never sweeps)");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad.Take(40)));
        }

        [Test]
        public void TheKageCounterThrustTravelsAlongItsBlade()
        {
            var s = Set;
            var m = s.counterSlash;
            int judged = 0;
            var bad = new List<string>();
            foreach (var (from, to) in LiveFrames(m))
                for (int i = 0; i <= Mathf.Min((to - from) * 4, MaxSteps); i++)
                {
                    float f = from + i * 0.25f;
                    var k = At(s, m, f);
                    Vector2 tip0 = At(s, m, f - 0.125f).OnBlade(0.8f), tip1 = At(s, m, f + 0.125f).OnBlade(0.8f);
                    if ((tip1 - tip0).magnitude < 0.004f) continue;
                    judged++;
                    float off = Vector2.Angle(tip1 - tip0, KMath.Dir(k.BladeAngle));
                    if (off > 30f) bad.Add($"frame {f:0.00}: the point travels {off:0} degrees off the blade's line");
                }
            Assert.Greater(judged, 2, "the thrust moves through its live frames");
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }

        [Test]
        public void MirrorGuardHoldsKasumiAtHeadHeightTheBladeLevelEdgeUp()
        {
            var s = Set;
            var k = At(s, s.counterStance, 15f);
            var head = Rig.hurt.First(h => h.zone == HitZone.Head);
            float headY = k.Point(head.bone, head.local).y;
            float farFist = k.Point(B.HandF, Rig.hiltLocal).y, nearFist = k.NearFist.y;
            Assert.That(farFist, Is.EqualTo(headY).Within(0.15f), $"the lead fist at head height ({headY:0.00})");
            Assert.That(nearFist, Is.EqualTo(headY).Within(0.15f), $"the rear fist at head height ({headY:0.00})");
            Assert.That(Mathf.Abs(KMath.Wrap(k.BladeAngle)), Is.LessThan(15f), "the blade level, pointing at the opponent");
            Assert.Greater(k.EdgeNormal.y, 0.8f, "the edge up");
            Assert.Less(k.NearFistToHandle(), OnGrip, "both hands on the grip");
        }

        [Test]
        public void TheRunCarriesTheKatanaInBothHands()
        {
            var run = new Pose();
            var bad = new List<string>();
            for (int i = 0; i <= 40; i++)
            {
                float ph = i / 40f;
                PoseLib.RunCycle(ph, run);
                float d = new PoseFK(Rig).Solve(run).NearFistToHandle();
                if (d > OnGrip) bad.Add($"phase {ph:0.000}: near fist {d * 100f:0.0} cm off the handle");
            }
            Assert.IsEmpty(bad, string.Join("\n", bad));
        }
    }
}
