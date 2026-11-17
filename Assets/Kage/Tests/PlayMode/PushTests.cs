using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Physics push (SAN-68): fighters keep apart through push colliders on the Push layer; during
    /// pass-through frames a fighter's push collider moves to the PushPassThrough layer and touches nothing.
    /// The legacy switch brings back the fight loop's old manual push.
    /// </summary>
    public class PushTests
    {
        /// <summary>The old manual push kept fighters' centres this far apart.</summary>
        const float PushWidth = 0.72f;

        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Tuning.ClearOverrides();
        }

        /// <summary>Kage against a Ronin who stands still (scripted with no input) for the whole test.</summary>
        HeadlessFight StillRonin(float kageX, float roninX, bool legacy = false)
        {
            Tuning.Override(new CombatTuning { legacyHitResolution = legacy });
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, roninX);
            Tuning.ClearOverrides();
            f.RoninInput.Add(600);
            _fights.Add(f);
            return f;
        }

        static float Gap(HeadlessFight f) => f.Ronin.Pos.x - f.Kage.Pos.x;

        // ---------------------------------------------------------------------------------------
        [Test]
        public void AFighterWalkingIntoAnotherPushesItAndNeverOverlapsIt()
        {
            var f = StillRonin(-0.4f, 0.6f);
            f.KageInput.Add(120, fwd: 1);
            float minGap = float.MaxValue;
            for (int i = 0; i < 120; i++) { f.Run(1); minGap = Mathf.Min(minGap, Gap(f)); }

            Assert.GreaterOrEqual(minGap, PushWidth - 0.02f, "closest the two centres came");
            Assert.Greater(f.Ronin.Pos.x, 1.2f, "the Ronin was pushed");
        }

        [Test]
        public void PushingMovesBothFightersLikeTheOldPush()
        {
            var phys = StillRonin(-0.4f, 0.6f);
            var old = StillRonin(-0.4f, 0.6f, legacy: true);
            foreach (var f in new[] { phys, old }) f.KageInput.Add(120, fwd: 1);
            for (int i = 0; i < 120; i++)
            {
                phys.Run(1); old.Run(1);
                Assert.AreEqual(old.Kage.Pos.x, phys.Kage.Pos.x, 0.06f, $"Kage x on frame {i}");
                Assert.AreEqual(old.Ronin.Pos.x, phys.Ronin.Pos.x, 0.06f, $"Ronin x on frame {i}");
            }
        }

        [Test]
        public void PassThroughFramesLetAFighterRollThroughTheOther()
        {
            var f = StillRonin(-0.4f, 0.6f);
            f.KageInput.Add(1, tapFwd: true);
            f.Run(40);
            Assert.Greater(f.Kage.Pos.x, f.Ronin.Pos.x, "Kage ended on the far side of the Ronin");
        }

        [Test]
        public void AFighterJumpingOntoAnotherDoesNotStandOnItsHead()
        {
            var f = StillRonin(-0.4f, 0.3f);
            f.KageInput.Add(12, fwd: 1, dy: 1);
            f.Run(12);
            f.RunUntil(() => f.Kage.Grounded, 120);
            f.Run(10);
            Assert.Less(f.Kage.Pos.y, 0.05f, "Kage is back on the ground");
            Assert.GreaterOrEqual(Mathf.Abs(Gap(f)), PushWidth - 0.02f, "and beside the Ronin, not inside him");
        }

        [Test]
        public void ACorneredFighterStaysOnTheStageAndThePusherStopsAtIt()
        {
            var f = StillRonin(6.2f, 7.2f);
            f.KageInput.Add(150, fwd: 1);
            float maxRonin = float.MinValue, minGap = float.MaxValue;
            for (int i = 0; i < 150; i++) { f.Run(1); maxRonin = Mathf.Max(maxRonin, f.Ronin.Pos.x); if (i > 60) minGap = Mathf.Min(minGap, Gap(f)); }

            Assert.LessOrEqual(maxRonin, 7.6f + 0.03f, "the Ronin stays inside the stage edge");
            Assert.IsTrue(f.Ronin.AtWall, "the Ronin is in the corner");
            Assert.GreaterOrEqual(minGap, PushWidth - 0.04f, "Kage is held off by the Ronin");
        }

        [Test]
        public void PushCollidersAreOffUnderTheLegacySwitch()
        {
            var f = StillRonin(-0.4f, 0.6f, legacy: true);
            f.Run(2);
            foreach (var c in f.Kage.GetComponentsInChildren<Collider2D>())
                if (c.gameObject.layer is PhysicsLayers.Push or PhysicsLayers.PushPassThrough)
                    Assert.IsFalse(c.enabled, "a push collider is on under the legacy switch");
        }
    }
}
