using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage.Tests
{
    /// <summary>
    /// The armour check (SAN-80, ADR-0004 step 7 and ADR-0006): during a move's armour frames
    /// (<see cref="MoveDef.armorFrom"/>..<see cref="MoveDef.armorTo"/>) a light hit (a window without
    /// <see cref="HitWindow.heavy"/>) lands its damage but doesn't flinch the fighter. Heavy hits, and light hits
    /// outside the armour frames, flinch as always. Tested at the <see cref="IHittable.ReceiveHit"/> seam with a plain
    /// armoured move, so the check is shown to be independent of any family.
    /// </summary>
    public class ArmourTests
    {
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        /// <summary>An armoured stand-in move: 40 frames, armour from frame 8 to 20, no hit window.</summary>
        static MoveDef ArmouredMove() =>
            new MoveDef("test.armoured", "Armoured", 40).Key(10, Stance().Chest(-12f)).Key(40, Stance(), Ease.InOutQuad).Armour(8, 20);

        HeadlessFight Duel()
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f);
            f.RoninInput.Add(1200);
            _fights.Add(f);
            return f;
        }

        /// <summary>Starts the armoured move and runs to <paramref name="frame"/> of it.</summary>
        static void AtFrame(HeadlessFight f, MoveDef m, int frame)
        {
            f.Run(4);
            f.Kage.StartMove(m);
            f.RunUntil(() => f.Kage.moveFrame >= frame, 60);
            Assert.AreEqual(FState.Attack, f.Kage.state);
            Assert.AreEqual(frame, f.Kage.moveFrame);
        }

        static HitInfo Incoming(Fighter attacker, bool heavy, int dmg = 6, bool knockdown = false) => new()
        {
            attacker = attacker,
            w = new HitWindow { dmg = dmg, level = HitLevel.Mid, kb = new Vector2(3f, 0f), stun = 16, heavy = heavy, knockdown = knockdown },
            dmg = dmg, kb = new Vector2(-3f, 0f), zone = HitZone.Body, contact = new Vector2(-0.3f, 1f), moveId = "test.hit", window = -1,
        };

        [Test]
        public void A_light_hit_in_the_armour_frames_lands_its_damage_but_does_not_flinch()
        {
            var f = Duel();
            var m = ArmouredMove();
            AtFrame(f, m, 12);
            float before = f.Kage.health;
            var res = f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: false));
            Assert.AreEqual(HitResult.Hit, res);
            Assert.AreEqual(before - 6f, f.Kage.health, "the damage still lands");
            Assert.AreEqual(FState.Attack, f.Kage.state, "no flinch");
            Assert.AreSame(m, f.Kage.move, "the move carries on");
            Assert.Less(Mathf.Abs(f.Kage.rb.linearVelocity.x), 0.5f, "and no knockback");
        }

        [Test]
        public void An_armoured_move_plays_out_after_a_light_hit_without_ever_leaving_the_attack_state()
        {
            var f = Duel();
            var m = ArmouredMove();
            AtFrame(f, m, 12);
            f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: false));
            int frames = 0;
            while (f.Kage.state == FState.Attack && frames < 60) { f.Run(1); frames++; }
            Assert.AreEqual(FState.Idle, f.Kage.state, "it ended by finishing, not by being flinched");
            Assert.GreaterOrEqual(f.Kage.moveFrame, 0);
            Assert.GreaterOrEqual(frames, 27, "the 40-frame move ran on to its end (28 frames left at frame 12)");
        }

        [Test]
        public void A_heavy_hit_in_the_armour_frames_flinches_as_usual()
        {
            var f = Duel();
            AtFrame(f, ArmouredMove(), 12);
            float before = f.Kage.health;
            f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: true, dmg: 9));
            Assert.AreEqual(before - 9f, f.Kage.health, "damage lands");
            Assert.AreEqual(FState.Hitstun, f.Kage.state, "but armour doesn't stop a heavy hit");
        }

        [Test]
        public void A_light_hit_with_a_knockdown_but_no_heavy_flag_is_still_light()
        {
            // the glossary's rule: heavy is the flag on the window, nothing else
            var f = Duel();
            AtFrame(f, ArmouredMove(), 12);
            f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: false, knockdown: true));
            Assert.AreEqual(FState.Attack, f.Kage.state);
        }

        [TestCase(3, TestName = "A_light_hit_before_the_armour_frames_flinches")]
        [TestCase(7, TestName = "A_light_hit_on_the_frame_before_the_armour_flinches")]
        [TestCase(21, TestName = "A_light_hit_on_the_frame_after_the_armour_flinches")]
        [TestCase(30, TestName = "A_light_hit_in_the_recovery_flinches")]
        public void A_light_hit_outside_the_armour_frames_flinches(int frame)
        {
            var f = Duel();
            AtFrame(f, ArmouredMove(), frame);
            f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: false));
            Assert.AreEqual(FState.Hitstun, f.Kage.state);
        }

        [TestCase(8, TestName = "Armour_holds_on_its_first_frame")]
        [TestCase(20, TestName = "Armour_holds_on_its_last_frame")]
        public void The_armour_frames_are_inclusive(int frame)
        {
            var f = Duel();
            AtFrame(f, ArmouredMove(), frame);
            f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: false));
            Assert.AreEqual(FState.Attack, f.Kage.state);
        }

        [Test]
        public void A_move_with_no_armour_frames_flinches_from_a_light_hit()
        {
            var f = Duel();
            var plain = new MoveDef("test.plain", "Plain", 40).Key(10, Stance().Chest(-12f)).Key(40, Stance(), Ease.InOutQuad);
            AtFrame(f, plain, 12);
            f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: false));
            Assert.AreEqual(FState.Hitstun, f.Kage.state);
        }

        [Test]
        public void An_armoured_fighter_still_dies_to_a_light_hit_that_takes_the_last_health()
        {
            var f = Duel();
            AtFrame(f, ArmouredMove(), 12);
            f.Kage.health = 3f;
            f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: false, dmg: 6));
            Assert.AreEqual(FState.KO, f.Kage.state, "armour stops the flinch, never the damage");
        }

        [Test]
        public void The_spears_guard_raise_carries_its_armour_data_and_now_honours_it()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f, kageDef: SpearFamilyFightTests.KageWith("mizuchi"));
            f.RoninInput.Add(1200);
            var guard = ((SpearMoves)f.Kage.moves).guardRaise;
            Assert.AreEqual(5, guard.armorFrom);
            f.Run(4);
            f.Kage.StartMove(guard);
            f.RunUntil(() => f.Kage.moveFrame >= 10, 40);
            f.Kage.ReceiveHit(Incoming(f.Ronin, heavy: false));
            Assert.AreEqual(FState.Attack, f.Kage.state);
            Assert.AreSame(guard, f.Kage.move);
        }
    }
}
