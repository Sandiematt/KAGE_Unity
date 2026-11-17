using NUnit.Framework;
using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    // The rating input of a fight (ADR-0003, SAN-73): FightStatsRecorder reads only the event stream and the player's public
    // state. Events are published on a real fight's stream with real fighters; nothing is stepped, so the frame is whatever
    // the test says. Expected numbers come from the JS rules (rateFight inputs, engine fightTime / damageTaken).
    public class FightStatsRecorderTests
    {
        HeadlessFight fight;
        FightStatsRecorder rec;

        [SetUp]
        public void SetUp()
        {
            fight = new HeadlessFight(73, Foe.Ai);
            rec = new FightStatsRecorder(fight.Ctx.Events, fight.Kage);
            rec.MarkFightStart(100);
        }

        [TearDown]
        public void TearDown()
        {
            rec.Dispose();
            fight.Dispose();
        }

        void Publish(int frame, CombatEventType type, Fighter source, Fighter target, float dmg = 0f, string targetId = null)
        {
            fight.Ctx.Events.Publish(CombatEvent.HitResolved(frame, type, source, target, targetId, "slash1", 0, dmg, false, HitLevel.Mid, HitZone.Body, Vector2.zero, Vector2.zero));
        }

        void KageHits(int frame, float dmg = 10f) { Publish(frame, CombatEventType.Hit, fight.Kage, fight.Ronin, dmg); }

        void RoninHits(int frame, float dmg = 5f, CombatEventType type = CombatEventType.Hit) { Publish(frame, type, fight.Ronin, fight.Kage, dmg); }

        void Knockout(int frame, Fighter winner, Fighter loser)
        {
            fight.Ctx.Events.Publish(CombatEvent.KnockedOut(frame, winner, loser, "slash1", Vector2.zero, Vector2.zero));
        }

        [Test]
        public void Hits_landed_within_a_second_and_a_tenth_chain_into_one_combo()
        {
            KageHits(110); KageHits(150); KageHits(215);   // gaps of 40 and 65 frames (JS comboT 1.1 s = 66 frames)
            Assert.AreEqual(3, rec.MaxCombo);
        }

        [Test]
        public void A_gap_longer_than_the_combo_window_starts_a_new_combo()
        {
            KageHits(110); KageHits(120); KageHits(188);   // 68 frames after the last
            KageHits(190);
            Assert.AreEqual(2, rec.MaxCombo);
        }

        [Test]
        public void Taking_a_hit_breaks_the_combo_and_the_best_combo_is_kept()
        {
            KageHits(110); KageHits(120); KageHits(130);
            RoninHits(135);
            KageHits(140);
            Assert.AreEqual(3, rec.MaxCombo);
        }

        [Test]
        public void Hits_on_the_training_bag_do_not_count()
        {
            Publish(110, CombatEventType.Hit, fight.Kage, null, 10f, "bag");
            Assert.AreEqual(0, rec.MaxCombo);
        }

        [Test]
        public void Parries_count_when_the_player_is_the_one_who_parried()
        {
            Publish(120, CombatEventType.Parried, fight.Ronin, fight.Kage);
            Publish(180, CombatEventType.Parried, fight.Kage, fight.Ronin);
            Assert.AreEqual(1, rec.Parries);
        }

        [Test]
        public void A_fight_without_damage_to_the_player_is_flawless()
        {
            KageHits(110);
            Knockout(400, fight.Kage, fight.Ronin);
            var r = rec.Result();
            Assert.IsTrue(r.win);
            Assert.IsTrue(r.flawless);
        }

        [Test]
        public void Any_damage_taken_even_chip_through_a_guard_spoils_flawless()
        {
            RoninHits(130, 2f, CombatEventType.Blocked);
            Knockout(400, fight.Kage, fight.Ronin);
            Assert.IsFalse(rec.Result().flawless);
        }

        [Test]
        public void Time_runs_from_the_fight_start_to_the_knockout_at_sixty_frames_a_second()
        {
            Knockout(100 + 45 * 60, fight.Kage, fight.Ronin);
            Assert.AreEqual(45.0, rec.Result().time, 1e-9);
        }

        [Test]
        public void Knocking_out_the_foe_is_a_win_and_being_knocked_out_is_a_loss_that_is_never_flawless()
        {
            Knockout(300, fight.Kage, fight.Ronin);
            Assert.IsTrue(rec.Result().win);

            using var other = new FightStatsRecorder(fight.Ctx.Events, fight.Kage);
            other.MarkFightStart(100);
            Knockout(500, fight.Ronin, fight.Kage);
            var r = other.Result();
            Assert.IsFalse(r.win);
            Assert.IsFalse(r.flawless);
        }

        [Test]
        public void Health_left_is_the_players_fraction_at_the_end()
        {
            fight.Kage.health = fight.Kage.maxHealth * 0.25f;
            Knockout(300, fight.Kage, fight.Ronin);
            Assert.AreEqual(0.25, rec.Result().hpLeft, 1e-6);
        }

        [Test]
        public void The_result_carries_combo_and_parries_into_the_rating()
        {
            KageHits(110); KageHits(120); KageHits(130);
            Publish(140, CombatEventType.Parried, fight.Ronin, fight.Kage);
            Knockout(100 + 30 * 60, fight.Kage, fight.Ronin);
            var r = rec.Result();
            Assert.AreEqual(3, r.maxCombo);
            Assert.AreEqual(1, r.parries);
            // JS rateFight: hpLeft 1 x 40 + (30 s < 45: 20) + min(20, 3 x 2) + 1 x 5 + flawless 20 = 91 -> S
            Assert.AreEqual(Rating.S, GameRules.RateFight(r));
        }
    }
}
