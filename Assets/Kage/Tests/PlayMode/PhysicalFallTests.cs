using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Physical falls (SAN-113): a knockdown, launch, trip or sweep throws the body as physics bodies (momentum
    /// from the hit, a tumble, the impact on the ground) instead of the canned tumble, then hands back to the
    /// authored get-up. KO keeps the full, free ragdoll. Gameplay timing doesn't change.
    /// </summary>
    public class PhysicalFallTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Tuning.ClearOverrides();
        }

        /// <summary>The baseline replay's "getUp_after_sweep_vs_ai": the Ronin sweeps Kage, Kage gets up.</summary>
        HeadlessFight SweptKage()
        {
            var s = KageMoveScenarios.All().Single(x => x.name == "getUp_after_sweep_vs_ai");
            var f = new HeadlessFight(KageMoveScenarios.Seed, s.foe, s.kageX, s.roninX, s.meter);
            s.ronin(f.RoninInput);
            _fights.Add(f);
            return f;
        }

        [Test]
        public void ASweepThrowsTheBodyAsPhysicsBodies()
        {
            var f = SweptKage();
            f.RunUntil(() => f.Kage.state == FState.Launched, 30);
            f.Run(1);
            Assert.IsTrue(f.Kage.FallingPhysically, "launched: the rig is physics bodies");
            var hips = f.Kage.FallBody(B.Hips);
            Assert.IsNotNull(hips);
            Assert.AreEqual(RigidbodyType2D.Dynamic, hips.bodyType);
            Assert.Greater(Mathf.Abs(hips.angularVelocity) + hips.linearVelocity.magnitude, 0.5f, "moving with the hit's momentum");
        }

        [Test]
        public void TheBodyHitsTheGroundAndLiesThere()
        {
            var f = SweptKage();
            f.RunUntil(() => f.Kage.state == FState.Down, 60);
            f.Run(30);
            Assert.AreEqual(FState.Down, f.Kage.state);
            Assert.IsTrue(f.Kage.FallingPhysically);
            Vector2 hips = f.Kage.FallBody(B.Hips).position;
            Vector2 chest = f.Kage.FallBody(B.Chest).position;
            Assert.Less(hips.y, 0.45f, "on the ground");
            Assert.Less(Mathf.Abs(chest.y - hips.y), 0.2f, "lying flat, not standing");
            Assert.Less(f.Kage.FallBody(B.Chest).linearVelocity.magnitude, 1f, "at rest after the impact");
        }

        [Test]
        public void TheGetUpStartsAtTheAuthoredFrameAndTheRigIsBackOnItsPose()
        {
            // the canned fall's timeline, from Baselines/getUp_after_sweep_vs_ai.txt: hit at 14, down at 32, kip-up at 85
            var f = SweptKage();
            int launched = -1, down = -1, getUp = -1;
            for (int i = 0; i < 120 && getUp < 0; i++)
            {
                f.Run(1);
                int frame = f.Ctx.Clock.Frame - 1;
                if (launched < 0 && f.Kage.state == FState.Launched) launched = frame;
                if (down < 0 && f.Kage.state == FState.Down) down = frame;
                if (getUp < 0 && f.Kage.state == FState.Attack) getUp = frame;
            }
            Assert.AreEqual(14, launched);
            Assert.AreEqual(32, down);
            Assert.AreEqual(85, getUp);
            Assert.AreEqual("getUp", f.Kage.move.id);
            Assert.IsFalse(f.Kage.FallingPhysically, "the get-up is posed again");
            Assert.AreSame(f.Kage.rig.transform, f.Kage.rig.Bone(B.Root).parent);
            Assert.IsTrue(f.Kage.rig.Bone(B.Hips).IsChildOf(f.Kage.rig.transform), "bones back under the rig");
            f.RunUntil(() => f.Kage.state == FState.Idle, 60);
            Assert.AreEqual(FState.Idle, f.Kage.state);
        }

        [Test]
        public void AKOStillUsesTheFullFreeRagdoll()
        {
            var f = SweptKage();
            f.RunUntil(() => f.Kage.state == FState.Launched, 30);
            f.Kage.health = 1f;
            f.Run(1);
            var info = new HitInfo
            {
                attacker = f.Ronin, dmg = 10, kb = new Vector2(-2f, 3f), contact = f.Kage.Pos + Vector2.up,
                w = new HitWindow { dmg = 10, level = HitLevel.Mid, kb = new Vector2(2f, 3f) }, moveId = "test", window = 0,
            };
            f.Kage.ReceiveHit(info);
            Assert.AreEqual(FState.KO, f.Kage.state);
            Assert.IsFalse(f.Kage.FallingPhysically, "KO: free ragdoll, not the tethered fall");
            Assert.IsNotNull(f.Kage.FallBody(B.Hips), "still physics bodies");
        }

        [Test]
        public void TheLegacySwitchKeepsTheCannedFall()
        {
            Tuning.Override(new CombatTuning { legacyHitResolution = true });
            var f = SweptKage();
            f.RunUntil(() => f.Kage.state == FState.Launched, 30);
            f.Run(3);
            Assert.IsFalse(f.Kage.FallingPhysically);
            Assert.IsNull(f.Kage.FallBody(B.Hips));
        }
    }
}
