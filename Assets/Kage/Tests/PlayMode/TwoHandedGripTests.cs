using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The two-handed grip (SAN-113): Kage holds the katana with both hands, a punch lets go with the
    /// punching hand only and re-grips afterwards. Judged on the posed rig: where the near (punching)
    /// fist is against the sword's handle (tsuka).
    /// </summary>
    public class TwoHandedGripTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        HeadlessFight Alone()
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None);
            _fights.Add(f);
            return f;
        }

        /// <summary>The handle runs from the pommel (kashira, 0.17 behind the sword pivot) to the guard (tsuba, 0.03 ahead).</summary>
        public static float NearFistToHandle(Fighter k)
        {
            var d = k.def;
            Vector2 a = k.rig.World(B.Sword, KMath.Rot(new Vector2(-0.17f, 0f), d.bladeBindAngle));
            Vector2 b = k.rig.World(B.Sword, KMath.Rot(new Vector2(0.03f, 0f), d.bladeBindAngle));
            Vector2 fist = k.rig.World(B.HandN, d.fistLocal);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(fist - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - fist).magnitude;
        }

        /// <summary>A fist wrapped round the handle: its centre within a fist's half-width of the handle's axis.</summary>
        const float OnGrip = 0.035f;

        [Test]
        public void IdleHoldsTheKatanaWithBothHands()
        {
            var f = Alone();
            f.Run(30);
            Assert.AreEqual(FState.Idle, f.Kage.state);
            Assert.Less(NearFistToHandle(f.Kage), OnGrip);
        }

        [Test]
        public void WalkingKeepsBothHandsOnTheGrip()
        {
            var f = Alone();
            f.KageInput.Add(60, fwd: 1);
            for (int i = 0; i < 60; i++)
            {
                f.Run(1);
                if (i > 12) Assert.Less(NearFistToHandle(f.Kage), OnGrip, $"frame {i} ({f.Kage.state})");
            }
        }

        [Test]
        public void AJabLetsGoWithThePunchingHandAndReGripsAfterwards()
        {
            var f = Alone();
            f.Run(20);
            f.KageInput.Add(1, buttons: "P");
            f.RunUntil(() => f.Kage.state == FState.Attack && f.Kage.moveFrame == 8, 20);
            Assert.AreEqual("jab", f.Kage.move.id);
            Assert.Greater(NearFistToHandle(f.Kage), 0.2f, "the punching fist is out, off the handle");
            Assert.Less(Vector2.Distance(f.Kage.rig.World(B.Sword, Vector2.zero), f.Kage.rig.World(B.HandF, Vector2.zero)), 0.1f,
                "the other hand keeps the sword");
            f.RunUntil(() => f.Kage.state == FState.Idle, 40);
            f.Run(12);
            Assert.Less(NearFistToHandle(f.Kage), OnGrip, "re-gripped with both hands");
        }

        [Test]
        public void EveryFrameOfAKatanaSwingKeepsBothHandsOnTheGrip()
        {
            foreach (var (btn, dy, fwd) in new[] { ("S", 0, 0), ("S", -1, 0) })
            {
                var f = Alone();
                f.Run(20);
                f.KageInput.Add(1, dy: dy, fwd: fwd, buttons: btn);
                f.Run(1);
                string id = f.Kage.move?.id;
                Assert.IsNotNull(id);
                while (f.Kage.state == FState.Attack)
                {
                    Assert.Less(NearFistToHandle(f.Kage), 0.06f, $"{id} frame {f.Kage.moveFrame}");
                    f.Run(1);
                }
                f.Dispose();
                _fights.Remove(f);
            }
        }
    }
}
