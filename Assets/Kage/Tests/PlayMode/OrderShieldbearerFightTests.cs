using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static Kage.PoseLib;
using static Kage.Tests.ProfileAiTestKit;

namespace Kage.Tests
{
    /// <summary>
    /// The Order Shieldbearer in a fight (SAN-98): spawned from his catalog entry with the JS stats, the spear set and his shield flag;
    /// Kage's blows from the front hurt him without flinching him, from behind and after a guard-break they flinch him; a stretch
    /// resolves him to the real character; a scripted Kage defeats him; a bot fight against him ends in a knock-out without a softlock.
    /// Each fight is disposed before the next is built.
    /// </summary>
    public class OrderShieldbearerFightTests
    {
        const string Id = "order_shieldbearer";
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Tuning.ClearOverrides();
        }

        HeadlessFight Duel(float kageX = -0.37f, float foeX = 0.37f, Foe mode = Foe.Scripted)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, mode, kageX, foeX, foeDef: CharacterCatalog.Get(Id));
            _fights.Add(f);
            return f;
        }

        static HitInfo Incoming(Fighter attacker, float kbX, bool heavy = false, int dmg = 6) => new()
        {
            attacker = attacker,
            w = new HitWindow { dmg = dmg, level = HitLevel.Mid, kb = new Vector2(Mathf.Abs(kbX), 0f), stun = 16, heavy = heavy },
            dmg = dmg, kb = new Vector2(kbX, 0f), zone = HitZone.Body, contact = new Vector2(0.3f, 1f), moveId = "test.hit", window = -1,
        };

        [Test]
        public void HeIsSpawnedFromCharacterDataWithTheSpearSetAndTheJsStats()
        {
            var foe = Duel().Ronin;
            Assert.AreEqual(Id, foe.FighterId);
            Assert.AreEqual(220f * 1.1f * 100f / 232f, foe.maxHealth, 1e-3);
            Assert.AreEqual(0.12f, foe.Defense, 1e-6);
            Assert.AreEqual(1f, foe.DamageMul, 1e-6);
            Assert.AreEqual(1f, foe.SpeedMul, 1e-6);
            Assert.AreEqual("spear", foe.Family.id);
            Assert.AreEqual("sentinel_spear", foe.WeaponId);
            Assert.IsTrue(foe.rig.FamilyPartCount > 0, "the leaf-headed spear is drawn");
            Assert.IsTrue(foe.rig.BaseSwordHidden, "no katana drawn over it");
            Assert.IsTrue(foe.Has(CharacterFlags.FrontalArmour));
            Assert.IsFalse(foe.Has(CharacterFlags.Armour), "heavy hits and light hits from behind still flinch him: no all-round armour");
            Assert.IsFalse(foe.isPlayer);
        }

        // ---------------------------------------------------------------- frontal armour on the real foe

        [Test]
        public void KagesLightBlowsFromTheFrontHurtHimButNeverFlinchHim()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            for (int i = 0; i < 4; i++) f.KageInput.Add(1, buttons: "P").Add(24);
            bool everStunned = false;
            for (int i = 0; i < 120; i++) { f.Run(1); everStunned |= f.Ronin.state == FState.Hitstun; }
            Assert.GreaterOrEqual(f.Events(CombatEventType.Hit).Count(e => e.Target == f.Ronin), 2, "the jabs landed");
            Assert.Less(f.Ronin.health, f.Ronin.maxHealth, "and cost him health");
            Assert.IsFalse(everStunned, "without once putting him in hitstun");
        }

        [Test]
        public void ABlowFromBehindFlinchesHim()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.Run(2);
            // a fighter in a move doesn't turn to face its opponent: he is busy, with his back to Kage
            f.Ronin.StartMove(new MoveDef("test.plain", "Plain", 90).Key(10, Stance().Chest(-12f)).Key(90, Stance(), Ease.InOutQuad));
            f.Ronin.SetFacing(1f, true);
            f.KageInput.Add(1, buttons: "P").Add(24);
            bool stunned = false;
            for (int i = 0; i < 60; i++) { f.Run(1); stunned |= f.Ronin.state == FState.Hitstun; }
            Assert.GreaterOrEqual(f.Events(CombatEventType.Hit).Count(e => e.Target == f.Ronin), 1, "the jab landed");
            Assert.IsTrue(stunned, "from behind the shield does nothing");
        }

        [Test]
        public void AHeavyBlowFromTheFrontFlinchesHim()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.Run(4);
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f, heavy: true, dmg: 9));
            Assert.AreEqual(FState.Hitstun, f.Ronin.state);
        }

        [Test]
        public void AfterAGuardBreakABlowFromTheFrontIsACriticalAndFlinchesHim()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.Run(4);
            f.Ronin.AddPosture(f.Ronin.PostureMax, f.Kage, "test.break");
            Assert.IsTrue(f.Ronin.GuardBroken);
            Assert.AreEqual(FState.Hitstun, f.Ronin.state, "the break itself staggers him: the shield doesn't stop it");
            f.Ronin.rb.linearVelocity = Vector2.zero;
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f));
            Assert.IsTrue(f.Ronin.LastHitCrit);
            Assert.Greater(f.Ronin.rb.linearVelocity.x * 1f, 0.5f, "knocked along although it came from the front");
        }

        [Test]
        public void HeCanBeThrownBecauseTheShieldIsNotACharacterWideArmour()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.KageInput.Add(4).Add(1, fwd: 1, buttons: "P");
            f.RunUntil(() => f.Ronin.state == FState.Thrown, 60);
            Assert.AreEqual(FState.Thrown, f.Ronin.state, "Block + Punch: the JS tip, 'grab him', still works");
        }

        // ---------------------------------------------------------------- the rest of the ticket

        [Test]
        public void KageDefeatsTheShieldbearerWithScriptedInput()
        {
            var f = Duel();
            f.RoninInput.Add(3000);
            for (int i = 0; i < 60; i++) f.KageInput.Add(1, buttons: "S").Add(45).Add(10, fwd: 1);
            f.RunUntil(() => f.Ronin.state == FState.KO, 3600);
            Assert.AreEqual(FState.KO, f.Ronin.state, $"the Shieldbearer is still on {f.Ronin.health:0.0} health");
            Assert.AreEqual(Id, f.Events(CombatEventType.KnockedOut).Single().TargetId);
            Assert.IsNotNull(f.Ronin.FallBody(B.Hips), "a ragdoll");
        }

        [Test]
        public void StretchWavesResolveTheShieldbearerToTheRealCharacter()
        {
            var def = StretchSession.ResolveFoe(Id);
            Assert.AreEqual(Id, def.id);
            Assert.IsTrue(def.flags.HasFlag(CharacterFlags.FrontalArmour));
        }

        [Test]
        public void TheDevArenaHotkeyCyclesToTheShieldbearer()
        {
            CollectionAssert.Contains(KageGame.DevFoeIds(), Id);
        }

        [Test]
        public void ABotFightAgainstTheShieldbearerEndsInAKnockOutWithoutASoftlock()
        {
            const int Cap = 9000, MaxQuiet = 600;
            var f = Duel(-0.8f, 0.8f, Foe.Ai);
            f.KageInput.fallback = new KageBot();
            f.KageInput.Add(1);
            f.RunUntil(() => f.Kage.state == FState.KO || f.Ronin.state == FState.KO, Cap);
            Assert.IsTrue(f.Kage.state == FState.KO || f.Ronin.state == FState.KO, $"no knock-out in {Cap} frames (Kage {f.Kage.health:0.0}, Shieldbearer {f.Ronin.health:0.0})");
            int last = 0, quiet = 0;
            foreach (var e in f.Log.All.Where(e => e.Type is CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Parried or CombatEventType.Clashed))
            { quiet = Mathf.Max(quiet, e.Frame - last); last = e.Frame; }
            Assert.Less(quiet, MaxQuiet, $"{quiet} frames without a hit, block or parry");
            Assert.Greater(f.Events(CombatEventType.Hit).Count() + f.Events(CombatEventType.Blocked).Count(), 3, "the fighters did meet");
            Debug.Log($"SAN-98 bot fight vs Shieldbearer ended after {f.Frames.Count} frames, Kage {f.Kage.health:0} Shieldbearer {f.Ronin.health:0}");
        }
    }
}
