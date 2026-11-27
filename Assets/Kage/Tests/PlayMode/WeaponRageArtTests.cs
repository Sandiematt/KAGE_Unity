using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The shop weapons' own rage arts (docs/weapon-styles.md) in the fight harness: Silent Moon (Shizuka), Thousand Swallows (Tsubame),
    /// Hell Splitter (Onikiri), Rising Water Dragon (Mizuchi) and Reaper's Requiem (Yamigama). Each starts on Special + Down with full
    /// Rage, freezes the world until its finisher, lands only unblockable blows on the foe it started on, throws it at the finisher, and
    /// gives the world back. Silent Moon's own beats (the owner's brief): the blade sheathed, the draw that carries Kage past the foe,
    /// the foe standing still until the blade clicks home, then falling.
    /// </summary>
    public class WeaponRageArtTests
    {
        readonly List<IDisposable> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            GameFeel.trauma = GameFeel.punch = 0f;
        }

        HeadlessFight Duel(string weapon, float meter = 100f)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.6f, 0.6f, meter, kageDef: SpearFamilyFightTests.KageWith(weapon));
            _fights.Add(f);
            f.RoninInput.Add(2400);
            return f;
        }

        static void Cast(HeadlessFight f) => f.KageInput.Add(1, dy: -1, buttons: "X").Add(2400);

        static void RunArt(HeadlessFight f, Action<int> each = null, int max = 900)
        {
            bool started = false;
            for (int i = 0; i < max; i++)
            {
                f.Run(1);
                bool active = f.Kage.state == FState.Ultimate;
                started |= active;
                if (active) each?.Invoke(f.Kage.RageArtFrame);
                if (started && !active) return;
            }
            Assert.Fail("the art never ended");
        }

        [TestCase("shizuka", Fighter.SilentMoon, 10)]
        [TestCase("tsubame", Fighter.SwallowStorm, 9)]
        [TestCase("onikiri", Fighter.HellSplitter, 1)]
        [TestCase("mizuchi", Fighter.WaterDragon, 7)]
        [TestCase("yamigama", Fighter.ReaperRequiem, 5)]
        public void Each_weapon_plays_its_own_art_on_the_foe_and_gives_the_world_back(string weapon, string art, int minHits)
        {
            var f = Duel(weapon);
            Assert.AreEqual(art, f.Kage.RageArtId);
            float before = f.Ronin.health;
            Cast(f);
            f.Run(3);
            var rage = f.Events(CombatEventType.RageArt).Single();
            Assert.AreEqual(art, rage.MoveId);
            Assert.AreEqual(art, f.Kage.ActiveRageArt);
            Assert.AreEqual(0f, f.Kage.meter, "the art spends the whole meter");
            Assert.AreEqual(0f, f.Ctx.Clock.WorldScale, "the world is held from the first frame");

            bool thrown = false;
            RunArt(f, _ => thrown |= f.Ronin.state is FState.Launched or FState.Down);
            f.Run(90);
            thrown |= f.Ronin.state is FState.Launched or FState.Down;

            var hits = f.Events(CombatEventType.Hit).Where(e => e.MoveId == art).ToList();
            Assert.GreaterOrEqual(hits.Count, minHits, "the art's blows");
            Assert.IsTrue(hits.All(e => e.Level == HitLevel.Unblockable), "every blow is unblockable");
            Assert.IsTrue(hits.All(e => e.TargetId == "ronin"), "only the foe it started on");
            Assert.Less(f.Ronin.health, before);
            Assert.IsTrue(thrown, "the finisher throws the foe");
            Assert.AreEqual(1f, f.Ctx.Clock.WorldScale, "the world runs again");
            Assert.AreNotEqual(FState.Ultimate, f.Kage.state);
            Assert.IsFalse(f.Kage.rig.Sheathed, "the blade is drawn again after the art");
            Assert.AreEqual(0f, f.Kage.meter, "the art's blows give Kage no Rage back");
        }

        [Test]
        public void Silent_Moon_sheathes_draws_past_the_foe_and_the_foe_falls_when_the_blade_clicks_home()
        {
            var f = Duel("shizuka");
            float side0 = Mathf.Sign(f.Ronin.Pos.x - f.Kage.Pos.x);
            Cast(f);
            bool sheathedBefore = false, sheathedAfter = false, pastAfterDraw = false, backTurned = false, stillUntilFall = true, fellAfter = false;
            float foeX = 0f;
            int fallFrame = -1;
            RunArt(f, a =>
            {
                if (a == 30) sheathedBefore = f.Kage.rig.Sheathed;
                if (a == 46)
                {
                    pastAfterDraw = Mathf.Sign(f.Ronin.Pos.x - f.Kage.Pos.x) == -side0;
                    backTurned = f.Kage.facing == side0;              // still facing the way he went: his back to the foe
                    foeX = f.Ronin.Pos.x;
                }
                if (a > 46 && a < 112 && Mathf.Abs(f.Ronin.Pos.x - foeX) > 0.05f) stillUntilFall = false;
                if (a == 120) sheathedAfter = f.Kage.rig.Sheathed;
                if (a >= 112 && fallFrame < 0 && f.Ronin.state is FState.Launched or FState.Down) fallFrame = a;
            });
            f.Run(90);
            fellAfter = f.Ronin.state == FState.Down || f.Ronin.state == FState.Launched || fallFrame >= 0;

            Assert.IsTrue(sheathedBefore, "the blade is in its scabbard before the draw");
            Assert.IsTrue(pastAfterDraw, "the draw carries Kage past the foe");
            Assert.IsTrue(backTurned, "his back is to the foe");
            Assert.IsTrue(stillUntilFall, "the foe stands still until the cuts open");
            Assert.IsTrue(sheathedAfter, "the blade clicks home before the fall");
            Assert.IsTrue(fellAfter, "the foe falls");
            Assert.AreEqual(112, fallFrame, "it falls the frame the cuts open");
        }

        [Test]
        public void Without_a_foe_the_press_is_spent_and_announces_the_art()
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Bag, kageMeter: 100f, kageDef: SpearFamilyFightTests.KageWith("mizuchi"));
            _fights.Add(f);
            Cast(f);
            f.Run(140);
            Assert.AreEqual(Fighter.WaterDragon, f.Events(CombatEventType.RageArt).Single().MoveId);
            Assert.AreEqual(0f, f.Kage.meter);
            Assert.AreEqual(1f, f.Ctx.Clock.WorldScale, "nothing is frozen");
            Assert.AreNotEqual(FState.Ultimate, f.Kage.state);
        }

        [Test]
        public void An_art_cut_short_gives_the_world_and_the_scabbard_back()
        {
            var f = Duel("shizuka");
            Cast(f);
            f.Run(30);
            Assert.AreEqual(FState.Ultimate, f.Kage.state);
            f.Kage.ResetForRound(-1f, 1f);
            f.Run(2);
            Assert.AreEqual(1f, f.Ctx.Clock.WorldScale);
            Assert.IsFalse(f.Kage.rig.Sheathed);
            Assert.IsNull(f.Kage.ActiveRageArt);
        }
    }
}
