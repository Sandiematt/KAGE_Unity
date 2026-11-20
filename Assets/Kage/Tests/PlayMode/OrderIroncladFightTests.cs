using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static Kage.Tests.ProfileAiTestKit;

namespace Kage.Tests
{
    /// <summary>
    /// The Order Ironclad in a fight (SAN-100): spawned from its catalog entry with the JS stats, the cleaver set, the Armour flag and
    /// twice Kage's mass; light hits land without a flinch (from the front and from behind), heavy hits and a guard-break stagger it, and
    /// the same hit pushes it back much less than a Footman; a stretch resolves it to the real character; a bot fight against it ends in
    /// a knock-out without a softlock. Each fight is disposed before the next is built.
    /// </summary>
    public class OrderIroncladFightTests
    {
        const string Id = "order_ironclad";
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Tuning.ClearOverrides();
        }

        HeadlessFight Duel(string foe = Id, float kageX = -0.37f, float foeX = 0.37f, Foe mode = Foe.Scripted)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, mode, kageX, foeX, foeDef: CharacterCatalog.Get(foe));
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
        public void ItIsSpawnedFromCharacterDataWithTheCleaverSetAndTheJsStats()
        {
            var foe = Duel().Ronin;
            Assert.AreEqual(Id, foe.FighterId);
            Assert.AreEqual(280f * 1.35f * 100f / 232f, foe.maxHealth, 1e-3);
            Assert.AreEqual(0.14f, foe.Defense, 1e-6);
            Assert.AreEqual(1.15f, foe.DamageMul, 1e-6);
            Assert.AreEqual(0.9f, foe.SpeedMul, 1e-6);
            Assert.AreEqual("cleaver", foe.Family.id);
            Assert.AreEqual("raider_cleaver", foe.WeaponId);
            Assert.IsTrue(foe.rig.FamilyPartCount > 0, "the cleaver is drawn");
            Assert.IsTrue(foe.rig.BaseSwordHidden, "no katana drawn over it");
            Assert.AreEqual(2f, foe.Mass, 1e-6);
            Assert.IsTrue(foe.Has(CharacterFlags.Armour));
            Assert.AreEqual(1.14f, foe.transform.localScale.x, 1e-4, "the JS build scale");
            Assert.IsFalse(foe.isPlayer);
        }

        // ---------------------------------------------------------------- armour

        [Test]
        public void KagesLightBlowsHurtItButNeverFlinchIt()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            for (int i = 0; i < 4; i++) f.KageInput.Add(1, buttons: "P").Add(24);
            bool everStunned = false;
            for (int i = 0; i < 120; i++) { f.Run(1); everStunned |= f.Ronin.state == FState.Hitstun; }
            Assert.GreaterOrEqual(f.Events(CombatEventType.Hit).Count(e => e.Target == f.Ronin), 2, "the jabs landed");
            Assert.Less(f.Ronin.health, f.Ronin.maxHealth, "and cost it health");
            Assert.IsFalse(everStunned, "without once putting it in hitstun");
        }

        [Test]
        public void ALightBlowFromBehindDoesNotFlinchItEither()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.Run(4);
            f.Ronin.SetFacing(1f, true);
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f));
            Assert.AreEqual(FState.Idle, f.Ronin.state, "its armour is all round, unlike a shield's");
        }

        [Test]
        public void AHeavyBlowFlinchesIt()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.Run(4);
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f, heavy: true, dmg: 9));
            Assert.AreEqual(FState.Hitstun, f.Ronin.state);
        }

        [Test]
        public void AGuardBreakStaggersIt()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.Run(4);
            f.Ronin.AddPosture(f.Ronin.PostureMax, f.Kage, "test.break");
            Assert.IsTrue(f.Ronin.GuardBroken);
            Assert.AreEqual(FState.Hitstun, f.Ronin.state, "armour doesn't stop a guard-break");
            f.Run(1);
            Assert.AreEqual(1, f.Events(CombatEventType.GuardBroken).Count());
        }

        // ---------------------------------------------------------------- mass

        /// <summary>How far a hit pushes the foe in 40 frames after it lands, in metres.</summary>
        float Travel(string foe, bool heavy)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -1.3f, 0.37f, foeDef: CharacterCatalog.Get(foe));   // out of push range: a hit between frames would be undone by the push settling
            f.RoninInput.Add(600);
            f.Ronin.maxHealth = f.Ronin.health = 1e5f;
            f.Run(4);
            float x0 = f.Ronin.Pos.x;
            f.Ronin.ReceiveHit(Incoming(f.Kage, +3f, heavy: heavy, dmg: heavy ? 9 : 6));
            f.Run(40);
            return f.Ronin.Pos.x - x0;
        }

        [Test]
        public void TheSameHeavyBlowPushesItBackMuchLessThanAFootman()
        {
            float footman = Travel("order_footman", true), iron = Travel(Id, true);
            Debug.Log($"SAN-100 heavy blow, 40 frames: Footman {footman:0.000} m, Ironclad {iron:0.000} m ({iron / footman:0.00} of it)");
            Assert.Greater(footman, 0.2f, "a Footman is knocked back");
            Assert.Greater(iron, 0f, "the Ironclad does move");
            Assert.Less(iron, footman * 0.65f, $"twice the mass: {iron:0.000} m against {footman:0.000} m");
        }

        [Test]
        public void ALightBlowDoesNotMoveItAtAllWhereAFootmanIsPushed()
        {
            float footman = Travel("order_footman", false), iron = Travel(Id, false);
            Debug.Log($"SAN-100 light blow, 40 frames: Footman {footman:0.000} m, Ironclad {iron:0.000} m");
            Assert.Greater(footman, 0.1f);
            Assert.Less(iron, footman * 0.25f, "armour stops the knockback with the flinch");
        }

        // ---------------------------------------------------------------- the rest of the ticket

        [Test]
        public void StretchWavesResolveTheIroncladToTheRealCharacter()
        {
            var def = StretchSession.ResolveFoe(Id);
            Assert.AreEqual(Id, def.id);
        }

        [Test]
        public void TheDevArenaHotkeyCyclesToTheIronclad()
        {
            CollectionAssert.Contains(KageGame.DevFoeIds(), Id);
        }

        [Test]
        public void ABotFightAgainstTheIroncladEndsInAKnockOutWithoutASoftlock()
        {
            const int Cap = 9000, MaxQuiet = 600;
            var f = Duel(Id, -0.8f, 0.8f, Foe.Ai);
            f.KageInput.fallback = new KageBot();
            f.KageInput.Add(1);
            f.RunUntil(() => f.Kage.state == FState.KO || f.Ronin.state == FState.KO, Cap);
            Assert.IsTrue(f.Kage.state == FState.KO || f.Ronin.state == FState.KO, $"no knock-out in {Cap} frames (Kage {f.Kage.health:0.0}, Ironclad {f.Ronin.health:0.0})");
            int last = 0, quiet = 0;
            foreach (var e in f.Log.All.Where(e => e.Type is CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Parried or CombatEventType.Clashed))
            { quiet = Mathf.Max(quiet, e.Frame - last); last = e.Frame; }
            Assert.Less(quiet, MaxQuiet, $"{quiet} frames without a hit, block or parry");
            Assert.Greater(f.Events(CombatEventType.Hit).Count() + f.Events(CombatEventType.Blocked).Count(), 3, "the fighters did meet");
            Debug.Log($"SAN-100 bot fight vs Ironclad ended after {f.Frames.Count} frames, Kage {f.Kage.health:0} Ironclad {f.Ronin.health:0}");
        }
    }
}
