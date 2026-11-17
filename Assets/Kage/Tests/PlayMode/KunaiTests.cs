using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kage.Tests
{
    /// <summary>
    /// The kunai (SAN-88, ADR-0004): Special + Down throws a kunai from a per-fight count; with Rage full
    /// the same input is the rage art; with none left it does nothing. It is the generic projectile.
    /// </summary>
    public class KunaiTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        HeadlessFight Duel(bool guard = false, float meter = 0f)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, HeadlessFight.KageX, 0.6f, meter);
            f.RoninInput.Add(1200, fwd: guard ? -1 : 0);
            _fights.Add(f);
            return f;
        }

        static void SpecialDown(HeadlessFight f, int gap = 30) { f.KageInput.Add(1, dy: -1, buttons: "X"); f.KageInput.Add(gap); }

        static IEnumerable<Projectile> Kunai(HeadlessFight f) => f.Ctx.Projectiles.Where(p => p.Def.id == "kunai");

        [Test]
        public void KageStartsAFightWithFiveKunai()
        {
            var f = Duel();
            Assert.AreEqual(5, f.Kage.KunaiCount);
            Assert.AreEqual(5, f.Kage.KunaiMax);
        }

        [Test]
        public void SpecialAndDownThrowsAKunaiAndTheCountGoesDown()
        {
            var f = Duel();
            f.KageInput.Add(1, dy: -1, buttons: "X");
            f.RunUntil(() => Kunai(f).Any(), 40);

            var k = Kunai(f).Single();
            Assert.AreSame(f.Kage, k.Owner);
            Assert.AreEqual(4, f.Kage.KunaiCount);
            Assert.AreEqual(1.15f, k.Position.y, 1e-3f, "thrown from chest height (JS 132 px)");
            float x0 = k.Position.x;
            f.Run(1);
            Assert.AreEqual(x0 + 10.87f / 60f, k.Position.x, 5e-3f, "1250 px/s");
        }

        [Test]
        public void ThePlainSpecialIsStillTheCrescent()
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, HeadlessFight.KageX, 6f, 35f);    // the foe far off, so the Crescent stays in flight
            _fights.Add(f);
            f.RoninInput.Add(1200);
            f.KageInput.Add(1, buttons: "X");
            f.RunUntil(() => f.Ctx.Projectiles.Count > 0, 40);
            Assert.AreEqual("crescent", f.Ctx.Projectiles[0].Def.id);
            Assert.AreEqual(5, f.Kage.KunaiCount);
        }

        [Test]
        public void AKunaiHitsThroughTheHurtboxForThreeDamage()
        {
            var f = Duel();
            SpecialDown(f);
            f.Run(60);
            var hit = f.Events(CombatEventType.Hit).Single();
            Assert.AreEqual("kunai", hit.MoveId);
            Assert.AreEqual(3f, hit.Damage);
            Assert.AreEqual(97f, f.Ronin.health);
            Assert.AreEqual(0, Kunai(f).Count(), "spent on its one hit");
        }

        [Test]
        public void AFoeHoldingBackGuardsAKunai()
        {
            var f = Duel(guard: true);
            SpecialDown(f);
            f.Run(60);
            var blocked = f.Events(CombatEventType.Blocked).Single();
            Assert.AreEqual("kunai", blocked.MoveId);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count());
            Assert.AreEqual(100f, f.Ronin.health, "no chip from a kunai");
        }

        [Test]
        public void ThrowingUntilEmptyHitsFiveTimesThenTheInputDoesNothing()
        {
            var f = Duel();
            for (int i = 0; i < 7; i++) SpecialDown(f, 40);
            // keep the foe alive and standing so every kunai can land
            for (int i = 0; i < 7 * 41; i++)
            {
                f.Run(1);
                f.Ronin.health = 100f;
            }
            Assert.AreEqual(5, f.Events(CombatEventType.Hit).Count(e => e.MoveId == "kunai" && e.Target == f.Ronin));
            Assert.AreEqual(0, f.Kage.KunaiCount);
            Assert.AreEqual(0, f.Ctx.Projectiles.Count, "the sixth and seventh presses threw nothing");
        }

        [Test]
        public void AnEmptyPressIsConsumedAndDoesNotFallBackToTheCrescent()
        {
            var f = Duel(meter: 35f);
            f.Kage.SetKunaiCount(0);
            SpecialDown(f);
            f.Run(40);
            Assert.AreEqual(0, f.Ctx.Projectiles.Count);
            Assert.AreEqual(35f, f.Kage.meter, "no Crescent was paid for");
            Assert.AreEqual(FState.Idle, f.Kage.state);
        }

        [Test]
        public void TheCountResetsEachFight()
        {
            var a = Duel();
            SpecialDown(a);
            a.Run(40);
            Assert.AreEqual(4, a.Kage.KunaiCount);
            a.Dispose();
            _fights.Clear();
            var b = Duel();
            Assert.AreEqual(5, b.Kage.KunaiCount);
        }

        [Test]
        public void ResetKunaiRestoresTheStartingCountForTheNextMatch()
        {
            var f = Duel();
            f.Kage.SetKunaiCount(1);
            f.Kage.ResetKunai();
            Assert.AreEqual(5, f.Kage.KunaiCount);
        }

        [Test]
        public void TheStartingCountComesFromData()
        {
            var f = Duel();
            f.Kage.kunaiStart = 7;
            f.Kage.ResetKunai();
            Assert.AreEqual(7, f.Kage.KunaiCount);
            Assert.AreEqual(7, f.Kage.KunaiMax);
        }

        [Test]
        public void WithRageFullTheSameInputIsTheRageArtAndNoKunaiIsSpent()
        {
            var f = Duel(meter: 100f);
            f.KageInput.Add(1, dy: -1, buttons: "X");
            f.Run(30);
            Assert.AreEqual(1, f.Events(CombatEventType.RageArt).Count());
            Assert.AreEqual(5, f.Kage.KunaiCount);
            Assert.AreEqual(0, f.Ctx.Projectiles.Count);
        }

        [Test]
        public void AFighterWhoCannotThrowKunaiDoesNothing()
        {
            var f = Duel();
            f.Kage.canThrowKunai = false;
            SpecialDown(f);
            f.Run(40);
            Assert.AreEqual(0, f.Ctx.Projectiles.Count);
            Assert.AreEqual(5, f.Kage.KunaiCount);
        }
    }

    /// <summary>The real input source: Special + Down from keyboard and gamepad.</summary>
    public class KunaiInputTests : InputTestFixture
    {
        static FrameInput PollAfter(System.Action press)
        {
            var src = new PlayerInputSource();
            press();
            src.Sample();
            return src.Poll(null);
        }

        [Test]
        public void KeyboardSpecialPlusDownIsXWithDyBelowZero()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            var inp = PollAfter(() => { Press(kb.sKey); Press(kb.iKey); });
            Assert.IsTrue(inp.x);
            Assert.AreEqual(-1, inp.dy);
        }

        [Test]
        public void GamepadSpecialPlusDownIsXWithDyBelowZero()
        {
            var gp = InputSystem.AddDevice<Gamepad>();
            var inp = PollAfter(() => { Set(gp.leftStick, new Vector2(0f, -1f)); Press(gp.buttonEast); });
            Assert.IsTrue(inp.x);
            Assert.AreEqual(-1, inp.dy);
        }
    }
}
