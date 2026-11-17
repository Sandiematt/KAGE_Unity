using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Mass knockback and throw release (SAN-68): knockback and launches are impulses divided by the
    /// fighter's mass (Kage = 1, so his distances don't change); a throw carries its victim on the same
    /// animated arc whatever the mass, then releases it into a physics launch. Each run disposes its
    /// fight before the next is built: push colliders and hurtboxes of a live fight would touch the next one.
    /// </summary>
    public class MassKnockbackTests
    {
        [TearDown]
        public void TearDown() => Tuning.ClearOverrides();

        /// <summary>The test dummy: the Ronin's rig and moves with three times Kage's mass (catalog entry test_heavy).</summary>
        static CharacterDef Heavy() => CharacterCatalog.Get(TestHeavy.Id);

        /// <summary>Kage against a foe who stands still; <paramref name="kage"/> scripts Kage. Dispose it.</summary>
        static HeadlessFight Fight(System.Action<ScriptedInput> kage, CharacterDef foe = null, bool legacy = false, float kageX = -0.37f, float foeX = 0.37f)
        {
            Tuning.Override(new CombatTuning { legacyHitResolution = legacy });
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, foeX, foeDef: foe);
            Tuning.ClearOverrides();
            f.RoninInput.Add(600);
            kage(f.KageInput);
            return f;
        }

        static int FirstHitOn(HeadlessFight f, Fighter target)
        {
            var hit = f.Events(CombatEventType.Hit).Where(e => e.Target == target).Select(e => (int?)e.Frame).FirstOrDefault();
            Assert.IsTrue(hit.HasValue, $"{target.FighterId} was never hit");
            return hit.Value;
        }

        static readonly System.Action<ScriptedInput> PushKick = s => s.Add(1, fwd: 1, buttons: "K");
        static readonly System.Action<ScriptedInput> CrouchPunchUppercut = s => s.Add(1, dy: -1, buttons: "P").Add(6, dy: -1).Add(1, buttons: "P");

        // ---------------------------------------------------------------------------------------
        [Test]
        public void KageAndTheRoninWeighOneAndTheHeavyDummyThree()
        {
            using (var light = Fight(_ => { }))
            {
                Assert.AreEqual(1f, light.Kage.Mass);
                Assert.AreEqual(1f, light.Ronin.Mass);
            }
            using var heavy = Fight(_ => { }, Heavy());
            Assert.AreEqual(3f, heavy.Ronin.Mass);
        }

        [Test]
        public void AHeavyDummyIsKnockedBackLessThanKageWeightByTheSameHit()
        {
            float Travel(CharacterDef foe)
            {
                using var f = Fight(PushKick, foe);
                f.RunUntil(() => f.Events(CombatEventType.Hit).Any(), 60);
                FirstHitOn(f, f.Ronin);
                float x0 = f.Ronin.Pos.x;
                f.Run(40);
                return f.Ronin.Pos.x - x0;
            }
            float light = Travel(null), heavy = Travel(Heavy());
            Assert.Greater(light, 0.2f, "the push kick knocks a mass-1 fighter back");
            Assert.Greater(heavy, 0f);
            Assert.Less(heavy, light * 0.5f, $"mass 3 travelled {heavy:0.000} m, mass 1 {light:0.000} m");
        }

        [Test]
        public void AHeavyDummyIsLaunchedLowerThanKageWeightByTheSameHit()
        {
            float Peak(CharacterDef foe)
            {
                using var f = Fight(CrouchPunchUppercut, foe);
                float peak = 0f;
                for (int i = 0; i < 70; i++) { f.Run(1); peak = Mathf.Max(peak, f.Ronin.Pos.y); }
                Assert.IsTrue(f.Events(CombatEventType.Hit).Any(e => e.Target == f.Ronin && e.MoveId == "uppercut"), "the uppercut connected");
                return peak;
            }
            float light = Peak(null), heavy = Peak(Heavy());
            Assert.Greater(light, 0.4f, "the uppercut launches a mass-1 fighter");
            Assert.Less(heavy, light * 0.5f, $"mass 3 peaked at {heavy:0.000} m, mass 1 at {light:0.000} m");
        }

        [Test]
        public void KagesKnockbackIsTheSameAsBeforeTheMassChange()
        {
            // the Ronin push-kicks a still Kage; Kage's path after the hit must match the legacy run
            List<float> KagePath(bool legacy)
            {
                Tuning.Override(new CombatTuning { legacyHitResolution = legacy });
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f);
                Tuning.ClearOverrides();
                f.KageInput.Add(600);
                f.RoninInput.Add(1, fwd: 1, buttons: "K").Add(600);
                f.RunUntil(() => f.Events(CombatEventType.Hit).Any(), 60);
                FirstHitOn(f, f.Kage);
                var xs = new List<float>();
                for (int i = 0; i < 40; i++) { f.Run(1); xs.Add(f.Kage.Pos.x); }
                return xs;
            }
            var now = KagePath(false);
            var before = KagePath(true);
            Assert.Less(now[^1] - now[0], -0.2f, "Kage was knocked back");
            for (int i = 0; i < now.Count; i++)
                Assert.AreEqual(before[i], now[i], 0.01f, $"Kage x {i} frames after the hit");
        }

        [Test]
        public void TheLegacySwitchIgnoresMass()
        {
            float Travel(CharacterDef foe)
            {
                using var f = Fight(PushKick, foe, legacy: true);
                f.RunUntil(() => f.Events(CombatEventType.Hit).Any(), 60);
                FirstHitOn(f, f.Ronin);
                float x0 = f.Ronin.Pos.x;
                f.Run(40);
                return f.Ronin.Pos.x - x0;
            }
            Assert.AreEqual(Travel(null), Travel(Heavy()), 0.02f, "under the legacy switch a heavy fighter is knocked back like a light one");
        }

        // ---- throws ------------------------------------------------------------------------------
        static readonly System.Action<ScriptedInput> Throw = s => s.Add(4).Add(1, fwd: 1, buttons: "P");

        sealed class ThrowRun
        {
            public readonly List<Vector2> carry = new();   // the victim's position on every Thrown frame
            public Vector2 release, landed;
            public FState afterRelease;
            public RigidbodyType2D bodyAfterRelease;
        }

        static ThrowRun RunThrow(CharacterDef foe, bool legacy = false)
        {
            using var f = Fight(Throw, foe, legacy, -0.38f, 0.38f);
            var r = new ThrowRun();
            var v = f.Ronin;
            f.RunUntil(() => v.state == FState.Thrown, 60);
            Assert.AreEqual(FState.Thrown, v.state, "the throw grabbed");
            while (v.state == FState.Thrown && r.carry.Count < 200) { r.carry.Add(v.Pos); f.Run(1); }
            r.release = v.Pos;
            r.afterRelease = v.state;
            r.bodyAfterRelease = v.rb.bodyType;
            f.RunUntil(() => v.state == FState.Down, 200);
            Assert.AreEqual(FState.Down, v.state, "the victim landed");
            r.landed = v.Pos;
            return r;
        }

        [Test]
        public void AThrowCarriesItsVictimOnTheSameArcAsBefore()
        {
            var now = RunThrow(null);
            var before = RunThrow(null, legacy: true);
            var heavy = RunThrow(Heavy());
            Assert.AreEqual(before.carry.Count, now.carry.Count, "frames carried");
            Assert.AreEqual(before.carry.Count, heavy.carry.Count, "frames carried (heavy)");
            for (int i = 0; i < now.carry.Count; i++)
            {
                Assert.AreEqual(before.carry[i].x, now.carry[i].x, 1e-3f, $"carry x {i}");
                Assert.AreEqual(before.carry[i].y, now.carry[i].y, 1e-3f, $"carry y {i}");
                Assert.AreEqual(now.carry[i].x, heavy.carry[i].x, 1e-3f, $"heavy carry x {i}");
                Assert.AreEqual(now.carry[i].y, heavy.carry[i].y, 1e-3f, $"heavy carry y {i}");
            }
        }

        [Test]
        public void AThrowReleasesItsVictimIntoAPhysicsLaunchThatMassShortens()
        {
            var light = RunThrow(null);
            var heavy = RunThrow(Heavy());
            Assert.AreEqual(FState.Launched, light.afterRelease);
            Assert.AreEqual(RigidbodyType2D.Dynamic, light.bodyAfterRelease, "released into the physics world");
            float l = Mathf.Abs(light.landed.x - light.release.x), h = Mathf.Abs(heavy.landed.x - heavy.release.x);
            Assert.Greater(l, 0.15f, "a mass-1 victim is flung away from the release point");
            Assert.Less(h, l * 0.6f, $"mass 3 landed {h:0.000} m from the release point, mass 1 {l:0.000} m");
        }
    }
}
