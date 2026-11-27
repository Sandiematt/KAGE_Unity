using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Fighting a group (Fighter.Counter.cs, docs/combat-turn-and-counter.md): a parry or a group-fight block of a blow from behind turns
    /// Kage round; Guard + a fresh Back press turns him to the foe behind; the foe he parried or turned to stays his target for a
    /// while; and his first clean blow on a foe reeling from his parry is a counter hit. Hits use the <see cref="IHittable.ReceiveHit"/>
    /// seam (as ParryTests does), so no move's reach or timing is typed here. Numbers come from <see cref="ParryTuning"/>.
    /// </summary>
    public class TurnAndCounterTests
    {
        readonly List<GroupFight> _fights = new();
        static ParryTuning T => Tuning.Get<ParryTuning>();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Tuning.ClearOverrides();
        }

        /// <summary>Kage at 0 facing right; foes that stand still (their scripted input aside) at <paramref name="foeXs"/>.</summary>
        GroupFight Still(params float[] foeXs)
        {
            var f = new GroupFight(KageMoveScenarios.Seed, 0f, foeXs, foesStill: true);
            _fights.Add(f);
            return f;
        }

        /// <summary>A plain mid blow from <paramref name="from"/> on <paramref name="to"/>, pushing <paramref name="to"/> away from it.</summary>
        static HitInfo Blow(Fighter from, Fighter to, int dmg = 6) => new()
        {
            attacker = from,
            w = new HitWindow { dmg = dmg, level = HitLevel.Mid, kb = new Vector2(2f, 0f), stun = 16 },
            dmg = dmg, kb = new Vector2(Mathf.Sign(to.Pos.x - from.Pos.x) * 2f, 0f), zone = HitZone.Body,
            contact = Vector2.Lerp(from.Pos, to.Pos, 0.5f) + Vector2.up, moveId = "test.hit", window = 0,
        };

        /// <summary>Frames until <paramref name="who"/> leaves hit-stun (frames frozen by hit-stop don't tick it, so they're passed over).</summary>
        static int StunFrames(GroupFight f, Fighter who)
        {
            int n = 0;
            while (who.state == FState.Hitstun && n < 400) { f.Next(); n++; }
            return n;
        }

        // ---------------------------------------------------------------- turning to a blow from behind

        [Test]
        public void A_parry_of_a_blow_from_behind_turns_Kage_round_and_keeps_that_foe_his_target()
        {
            // in front at 1.0; behind at 3.0, which the nearest-in-front rule alone would never pick, before or after he turns
            var f = Still(1.0f, -3.0f);
            var front = f.Foes[0];
            var back = f.Foes[1];
            f.KageInput.Add(1200, buttons: "G");
            f.Run(2);
            Assert.AreSame(front, f.Kage.opponent);
            Assert.AreEqual(1f, f.Kage.facing);

            Assert.AreEqual(HitResult.Parried, f.Kage.ReceiveHit(Blow(back, f.Kage)));
            Assert.AreEqual(-1f, f.Kage.facing, "he turned round to the blow");
            Assert.AreSame(back, f.Kage.Focus);

            for (int i = 0; i < T.parryFocusFrames - 2; i++)
            {
                f.Next();
                Assert.AreSame(back, f.Kage.opponent, $"own frame {i}: the parried foe stays his target");
                Assert.AreEqual(-1f, f.Kage.facing, $"own frame {i}: and he keeps facing it");
            }
            for (int i = 0; i < 30; i++) f.Next();
            Assert.IsNull(f.Kage.Focus, "the focus runs out");
            Assert.AreSame(front, f.Kage.opponent, "then the nearest-in-front rule has him again");
        }

        [Test]
        public void With_turnToBlow_off_a_parry_from_behind_does_not_turn_him()
        {
            Tuning.Override(new ParryTuning { turnToBlow = false });
            var f = Still(1.0f, -3.0f);
            f.KageInput.Add(1200, buttons: "G");
            f.Run(2);
            Assert.AreEqual(HitResult.Parried, f.Kage.ReceiveHit(Blow(f.Foes[1], f.Kage)));
            Assert.AreEqual(1f, f.Kage.facing);
        }

        [Test]
        public void In_a_group_fight_a_blocked_blow_from_behind_turns_the_guard_round()
        {
            var f = Still(1.0f, -3.0f);
            f.KageInput.Add(1200, buttons: "G");
            f.Run(T.windowFrames + 6);                     // past the parry window: a plain guard
            Assert.IsFalse(f.Kage.InParryWindow);

            Assert.AreEqual(HitResult.Blocked, f.Kage.ReceiveHit(Blow(f.Foes[1], f.Kage)));
            Assert.AreEqual(-1f, f.Kage.facing);
            Assert.AreSame(f.Foes[1], f.Kage.Focus);
        }

        [Test]
        public void A_blocked_blow_from_the_front_changes_nothing()
        {
            var f = Still(1.0f, -3.0f);
            f.KageInput.Add(1200, buttons: "G");
            f.Run(T.windowFrames + 6);
            Assert.AreEqual(HitResult.Blocked, f.Kage.ReceiveHit(Blow(f.Foes[0], f.Kage)));
            Assert.AreEqual(1f, f.Kage.facing);
            Assert.IsNull(f.Kage.Focus);
        }

        // ---------------------------------------------------------------- turning by hand

        [Test]
        public void Guard_and_a_fresh_Back_press_turns_Kage_to_the_nearest_foe_behind()
        {
            var f = Still(1.2f, -4.0f, -3.2f);
            f.KageInput.Add(6, buttons: "G").Add(1, fwd: -1, buttons: "G").Add(1200, buttons: "G");
            f.Run(6);
            Assert.AreEqual(1f, f.Kage.facing);
            Assert.AreSame(f.Foes[0], f.Kage.opponent);

            f.Run(1);
            Assert.AreEqual(-1f, f.Kage.facing, "he turned round");
            Assert.AreSame(f.Foes[2], f.Kage.Focus, "to the nearest foe behind, not the first in the list");
            for (int i = 0; i < T.turnFocusFrames - 2; i++)
            {
                f.Next();
                Assert.AreSame(f.Foes[2], f.Kage.opponent, $"own frame {i}");
                Assert.AreEqual(-1f, f.Kage.facing, $"own frame {i}");
            }
        }

        [Test]
        public void Guard_and_Back_with_nobody_behind_does_not_turn()
        {
            var f = Still(1.2f, 2.5f);
            f.KageInput.Add(6, buttons: "G").Add(1, fwd: -1, buttons: "G").Add(1200, buttons: "G");
            f.Run(20);
            Assert.AreEqual(1f, f.Kage.facing);
            Assert.IsNull(f.Kage.Focus);
        }

        [Test]
        public void Back_held_before_Guard_is_pressed_does_not_turn()
        {
            // walking backward and then guarding is a retreat, not a turn: only a fresh Back press inside the guard turns
            var f = Still(1.2f, -4.0f);
            f.KageInput.Add(10, fwd: -1).Add(30, fwd: -1, buttons: "G");
            f.Run(40);
            Assert.AreEqual(1f, f.Kage.facing);
            Assert.AreSame(f.Foes[0], f.Kage.opponent);
        }

        // ---------------------------------------------------------------- the counter hit

        [Test]
        public void The_first_clean_blow_on_a_foe_Kage_parried_is_a_counter_hit()
        {
            var f = Still(1.2f);
            var foe = f.Foes[0];
            f.KageInput.Add(1200, buttons: "G");
            f.Run(2);
            Assert.AreEqual(HitResult.Parried, f.Kage.ReceiveHit(Blow(foe, f.Kage)));
            Assert.IsTrue(foe.CounterOpenFor(f.Kage));

            var hit = Blow(f.Kage, foe, 6);
            var res = foe.ReceiveHit(hit);
            Assert.AreEqual(HitResult.Hit, res);
            Assert.IsTrue(foe.LastHitCounter);
            int counterDmg = Mathf.RoundToInt(6 * T.counterDamageMul);
            Assert.AreEqual(counterDmg, foe.LastDamageTaken, 1e-3f, "damage × counterDamageMul");
            Assert.IsFalse(foe.CounterOpenFor(f.Kage), "the first blow spends the opening");

            f.Kage.ResolveExternal(foe, hit, res);
            var hitEvent = f.Events(CombatEventType.Hit).Single(e => e.Source == f.Kage);
            var counter = f.Events(CombatEventType.CounterHit).Single();
            Assert.AreSame(f.Kage, counter.Source);
            Assert.AreSame(foe, counter.Target);
            Assert.AreEqual(counterDmg, counter.Damage, 1e-3f);
            Assert.AreEqual(counterDmg, hitEvent.Damage, 1e-3f, "the Hit carries the counter's damage");
            Assert.AreEqual(1, f.Kage.CounterHitsLanded);

            Assert.AreEqual(HitResult.Hit, foe.ReceiveHit(Blow(f.Kage, foe, 6)));
            Assert.IsFalse(foe.LastHitCounter, "the next blow is a plain hit");
            Assert.AreEqual(6f, foe.LastDamageTaken, 1e-3f);
        }

        [Test]
        public void A_counter_hit_stuns_longer_and_takes_more_posture()
        {
            int stun = 0, plainStun = 0;
            float posture = 0f, plainPosture = 0f;
            foreach (bool counter in new[] { true, false })
            {
                var f = Still(1.2f);
                var foe = f.Foes[0];
                f.KageInput.Add(1200, buttons: "G");
                f.Run(2);
                f.Kage.ReceiveHit(Blow(foe, f.Kage));
                if (!counter) foe.ReceiveHit(Blow(f.Kage, foe, 1));        // spend the opening on a light tap
                float before = foe.Posture;
                Assert.AreEqual(HitResult.Hit, foe.ReceiveHit(Blow(f.Kage, foe, 6)));
                Assert.AreEqual(counter, foe.LastHitCounter);
                if (counter) { posture = foe.Posture - before; stun = StunFrames(f, foe); }
                else { plainPosture = foe.Posture - before; plainStun = StunFrames(f, foe); }
            }
            Assert.That(stun - plainStun, Is.InRange(T.counterStunBonus - 1, T.counterStunBonus + 1), "counterStunBonus more frames of hit-stun");
            Assert.Greater(posture, plainPosture, "more posture");
        }

        [Test]
        public void The_counter_opening_lasts_the_stagger_plus_the_grace_frames()
        {
            var f = Still(1.2f);
            var foe = f.Foes[0];
            f.KageInput.Add(1200, buttons: "G");
            f.Run(2);
            f.Kage.ReceiveHit(Blow(foe, f.Kage));
            Assert.AreEqual(T.regularStaggerFrames + T.counterGraceFrames, foe.CounterOpenLeft, "a footman is a regular foe");
            for (int i = 0; i < T.regularStaggerFrames + T.counterGraceFrames; i++) f.Next();
            Assert.IsFalse(foe.CounterOpenFor(f.Kage), "closed");
            Assert.AreEqual(HitResult.Hit, foe.ReceiveHit(Blow(f.Kage, foe, 6)));
            Assert.IsFalse(foe.LastHitCounter);
        }

        [Test]
        public void Only_the_parrier_scores_the_counter_hit()
        {
            var f = Still(1.2f, 2.0f);
            f.KageInput.Add(1200, buttons: "G");
            f.Run(2);
            f.Kage.ReceiveHit(Blow(f.Foes[0], f.Kage));
            Assert.IsFalse(f.Foes[0].CounterOpenFor(f.Foes[1]));
            Assert.IsTrue(f.Foes[0].CounterOpenFor(f.Kage));
        }

        [Test]
        public void A_foe_that_parries_Kage_scores_no_counter_hit_unless_foesCounterHit()
        {
            foreach (bool foesToo in new[] { false, true })
            {
                Tuning.Override(new ParryTuning { foesCounterHit = foesToo });
                var f = Still(1.2f);
                var foe = f.Foes[0];
                f.FoeInputs[0].Add(1200, buttons: "G");
                f.Run(2);
                Assert.AreEqual(HitResult.Parried, foe.ReceiveHit(Blow(f.Kage, foe)), "the scripted foe parries");
                Assert.AreEqual(foesToo, f.Kage.CounterOpenFor(foe));
                f.Dispose();
                _fights.Remove(f);
            }
        }
    }
}
