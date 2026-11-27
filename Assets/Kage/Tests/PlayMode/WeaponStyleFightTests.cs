using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Kage.Tests
{
    /// <summary>
    /// The weapons' attack patterns in a fight (docs/weapon-styles.md): Shizuka's four draws, the katanas' Iai Counter out of a guard and
    /// Shizuka's silent cut after it, the twin blades' momentum, the cleaver's armoured swing, and a foe weapon left alone.
    /// </summary>
    public class WeaponStyleFightTests
    {
        readonly List<IDisposable> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            GameFeel.trauma = GameFeel.punch = 0f;
        }

        HeadlessFight Close(string weapon, Foe foe = Foe.Scripted)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, foe, -0.35f, 0.35f, kageDef: SpearFamilyFightTests.KageWith(weapon));
            _fights.Add(f);
            return f;
        }

        List<string> KageMoves(HeadlessFight f) =>
            f.Events(CombatEventType.MoveStarted).Where(e => e.SourceId == "kage").Select(e => e.MoveId).ToList();

        [Test]
        public void Shizuka_plays_the_Weapon_button_as_four_draws_ending_in_the_noto()
        {
            var f = Close("shizuka", Foe.None);
            f.KageInput.Add(1, buttons: "S").Add(9).Add(1, buttons: "S").Add(10).Add(1, buttons: "S").Add(14).Add(1, buttons: "S").Add(120);
            f.Run(160);
            CollectionAssert.AreEqual(new[] { "shizuka.nukitsuke", "shizuka.kiriage", "shizuka.tsuki", ShizukaMoves.NotoId }, KageMoves(f));
        }

        [Test]
        public void The_noto_puts_the_blade_in_its_scabbard_and_draws_it_again()
        {
            var f = Close("shizuka", Foe.None);
            f.KageInput.Add(1, buttons: "S").Add(9).Add(1, buttons: "S").Add(10).Add(1, buttons: "S").Add(14).Add(1, buttons: "S").Add(160);
            bool sheathed = false;
            for (int i = 0; i < 160; i++)
            {
                f.Run(1);
                if (f.Kage.move != null && f.Kage.move.id == ShizukaMoves.NotoId && f.Kage.moveFrame == ShizukaMoves.NotoSheathFrame + 2) sheathed = f.Kage.rig.Sheathed;
            }
            Assert.IsTrue(sheathed, "the blade is home after the noto's click");
            Assert.IsFalse(f.Kage.rig.Sheathed, "and drawn again once the move is over");
        }

        [TestCase("kagetsu", WeaponStyles.KagetsuCounterId)]
        [TestCase("shizuka", ShizukaMoves.CounterId)]
        public void A_guarded_blow_opens_the_Iai_Counter(string weapon, string counter)
        {
            var f = Close(weapon);
            f.RoninInput.Add(20).Add(1, buttons: "P").Add(400);
            f.KageInput.Add(24, buttons: "G").Add(1, buttons: "S").Add(400);
            f.Run(120);
            Assert.IsTrue(f.Events(CombatEventType.Blocked).Any(e => e.TargetId == "kage"), "precondition: Kage guards the jab");
            CollectionAssert.Contains(KageMoves(f), counter, "a Weapon press in the window plays the Iai Counter, out of the guard");
            var hit = f.Events(CombatEventType.Hit).FirstOrDefault(e => e.MoveId == counter);
            Assert.AreEqual(counter, hit.MoveId, "the counter lands");
            Assert.IsTrue(hit.Crit, "always a critical");
            if (weapon == "shizuka")
                Assert.IsTrue(f.Events(CombatEventType.Hit).Any(e => e.MoveId == counter + ".silentCut" && e.Frame > hit.Frame), "Shizuka's counter leaves a silent cut");
        }

        [Test]
        public void Without_a_guard_the_Weapon_press_is_the_ordinary_draw()
        {
            var f = Close("kagetsu");
            f.RoninInput.Add(400);
            f.KageInput.Add(1, buttons: "S").Add(200);
            f.Run(60);
            CollectionAssert.AreEqual(new[] { "iai" }, KageMoves(f).Take(1).ToList());
            Assert.IsFalse(KageMoves(f).Contains(WeaponStyles.KagetsuCounterId));
        }

        [Test]
        public void The_twin_blades_build_momentum_on_landed_blows_and_lose_it_when_they_stop()
        {
            var f = Close("tsubame");
            f.RoninInput.Add(600);
            f.KageInput.Add(1, buttons: "S").Add(7).Add(1, buttons: "S").Add(7).Add(1, buttons: "S").Add(400);
            f.Run(40);
            Assert.Greater(f.Kage.Momentum, 0f, "landed blows speed the next ones up");
            Assert.LessOrEqual(f.Kage.Momentum, WeaponStyles.Of("tsubame").tempoMax);
            f.Run(160);
            Assert.AreEqual(0f, f.Kage.Momentum, 1e-4f, "momentum drains away when the blows stop");
        }

        [Test]
        public void The_cleavers_swing_holds_through_a_light_blow()
        {
            var f = Close("onikiri");
            f.KageInput.Add(1, buttons: "S").Add(200);           // the Overhead Chop: its blow lands on frame 27
            f.RoninInput.Add(4).Add(1, buttons: "P").Add(400);   // a jab into the wind-up
            bool hitDuringSwing = false, kept = true;
            for (int i = 0; i < 40; i++)
            {
                f.Run(1);
                bool hitNow = f.Events(CombatEventType.Hit).Any(e => e.TargetId == "kage");
                if (hitNow && !hitDuringSwing) { hitDuringSwing = true; kept = f.Kage.state == FState.Attack && f.Kage.move != null && f.Kage.move.id == "cleaver.overhead"; }
            }
            Assert.IsTrue(hitDuringSwing, "precondition: the jab lands during the wind-up");
            Assert.IsTrue(kept, "the armoured swing carries on through it");
        }
    }
}
