using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Boss phases in a fight (SAN-102, ADR-0008) with the test boss, a Footman variant with two phases (<see cref="FootmanBoss"/>): the
    /// controller opens a phase when health falls to its threshold, swaps the ability list, AI profile, speed, move rate, damage and flags,
    /// and publishes <see cref="CombatEventType.BossPhaseChanged"/>. Tested through the fight harness: public state and the event log only.
    /// </summary>
    public class BossPhaseTests
    {
        readonly List<HeadlessFight> _fights = new();
        readonly List<string> _bosses = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            foreach (var id in _bosses) BossCatalog.Unregister(id);
            _bosses.Clear();
            Tuning.ClearOverrides();
        }

        static CharacterDef BossDefOf(string bossId = null)
        {
            var d = CharacterCatalog.Get(FootmanBoss.Id);
            if (bossId != null) d.bossId = bossId;
            return d;
        }

        HeadlessFight Fight(CharacterDef boss = null, Foe foe = Foe.Scripted, float bossX = 0.9f)
        {
            var f = new HeadlessFight(61, foe, -0.9f, bossX, foeDef: boss ?? BossDefOf());
            f.Kage.maxHealth = f.Kage.health = 1e7f;
            if (foe == Foe.Scripted) f.RoninInput.Add(100000);
            f.KageInput.Add(100000);
            _fights.Add(f);
            return f;
        }

        static void SetHealth(Fighter f, float fraction) => f.health = f.maxHealth * fraction;

        static List<CombatEvent> Phases(HeadlessFight f) => f.Events(CombatEventType.BossPhaseChanged).ToList();

        // ---- the controller exists for bosses only ----

        [Test]
        public void A_character_with_a_boss_id_gets_a_phase_controller_and_one_without_does_not()
        {
            var f = Fight();
            Assert.IsNotNull(f.Ronin.Phases);
            Assert.AreEqual(FootmanBoss.Id, f.Ronin.Phases.Boss.id);
            Assert.IsNull(f.Kage.Phases, "the player has none");
            var plain = new HeadlessFight(61, Foe.Scripted, -0.9f, 0.9f, foeDef: CharacterCatalog.Get("order_footman"));
            _fights.Add(plain);
            Assert.IsNull(plain.Ronin.Phases, "a regular foe has none");
        }

        // ---- phase one ----

        [Test]
        public void The_first_phase_is_open_from_the_start_and_publishes_its_event_once()
        {
            var f = Fight();
            f.Run(30);
            Assert.AreEqual(0, f.Ronin.Phases.Index);
            var ev = Phases(f);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(0, ev[0].Index);
            Assert.AreEqual("Drill Order", ev[0].Name);
            Assert.AreSame(f.Ronin, ev[0].Target);
            Assert.AreEqual("footman_boss", ev[0].TargetId);
        }

        [Test]
        public void Phase_one_allows_only_its_abilities()
        {
            var f = Fight();
            f.Run(2);
            Assert.IsTrue(f.Ronin.AbilityEnabled(FootmanBoss.CleaveId));
            Assert.IsFalse(f.Ronin.AbilityEnabled(FootmanBoss.RushId));
        }

        // ---- the threshold ----

        [Test]
        public void Phase_two_opens_when_health_falls_to_half_and_not_before()
        {
            var f = Fight();
            f.Run(3);
            SetHealth(f.Ronin, 0.51f);
            f.Run(3);
            Assert.AreEqual(0, f.Ronin.Phases.Index, "51% is above the threshold");
            SetHealth(f.Ronin, 0.5f);
            f.Run(2);
            Assert.AreEqual(1, f.Ronin.Phases.Index, "exactly 50% opens it");
            var ev = Phases(f);
            Assert.AreEqual(2, ev.Count);
            Assert.AreEqual(1, ev[1].Index);
            Assert.AreEqual("Charge", ev[1].Name);
        }

        [Test]
        public void A_phase_opens_once_and_never_closes_when_health_comes_back()
        {
            var f = Fight();
            f.Run(2);
            SetHealth(f.Ronin, 0.3f);
            f.Run(3);
            SetHealth(f.Ronin, 1f);
            f.Run(3);
            Assert.AreEqual(1, f.Ronin.Phases.Index);
            Assert.AreEqual(2, Phases(f).Count, "phase one, phase two, nothing more");
        }

        [Test]
        public void A_blow_across_two_thresholds_opens_both_phases_in_order()
        {
            BossCatalog.Register(new BossDef
            {
                id = "test.three", name = "Three", title = "t",
                phases = { new PhaseDef { name = "A", at = 1f }, new PhaseDef { name = "B", at = 0.66f }, new PhaseDef { name = "C", at = 0.33f } },
            });
            _bosses.Add("test.three");
            var f = Fight(BossDefOf("test.three"));
            f.Run(2);
            SetHealth(f.Ronin, 0.2f);
            f.Run(2);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Phases(f).Select(e => e.Index).ToArray());
            CollectionAssert.AreEqual(new[] { "A", "B", "C" }, Phases(f).Select(e => e.Name).ToArray());
            Assert.AreEqual(2, f.Ronin.Phases.Index);
        }

        [Test]
        public void A_boss_with_no_health_left_opens_no_new_phase()
        {
            var f = Fight();
            f.Run(2);
            f.Ronin.health = 0f;
            f.Run(3);
            Assert.AreEqual(0, f.Ronin.Phases.Index);
        }

        // ---- what a phase changes ----

        [Test]
        public void Entering_phase_two_swaps_abilities_profile_speed_move_rate_damage_and_flags()
        {
            var f = Fight(foe: Foe.Ai);
            var def = f.Ronin.Character;
            f.Run(3);
            // phase one: the character as built
            Assert.AreEqual(def.speedMul, f.Ronin.SpeedMul, 1e-5);
            Assert.AreEqual(def.moveRate, f.Ronin.MoveRate, 1e-5);
            Assert.AreEqual(def.damageMul, f.Ronin.DamageMul, 1e-5);
            Assert.AreEqual(CharacterFlags.None, f.Ronin.Flags);
            Assert.AreEqual("regular", f.Ai.profile.id);
            Assert.IsFalse(f.Ai.aggressive);

            SetHealth(f.Ronin, 0.45f);
            f.Run(2);
            Assert.AreEqual(def.speedMul * 1.3f, f.Ronin.SpeedMul, 1e-5, "faster on its feet");
            Assert.AreEqual(def.moveRate * 1.15f, f.Ronin.MoveRate, 1e-5, "attacks play quicker");
            Assert.AreEqual(def.damageMul * 1.2f, f.Ronin.DamageMul, 1e-5);
            Assert.AreEqual(def.moveRate * 1.15f, f.Ronin.PlaybackRate, 1e-5, "and the playback rate follows");
            Assert.IsTrue(f.Ronin.Has(CharacterFlags.Armour));
            Assert.IsTrue(f.Ronin.Has(CharacterFlags.NoGrab));
            Assert.IsTrue(f.Ronin.AbilityEnabled(FootmanBoss.RushId), "the rush is allowed now");
            Assert.AreEqual("boss", f.Ai.profile.id, "the profile swapped");
            Assert.IsTrue(f.Ai.aggressive);
        }

        [Test]
        public void The_flags_a_phase_adds_are_the_real_flags_light_hits_stop_flinching_in_phase_two()
        {
            var f = Fight();
            f.Run(3);
            var hit = new HitInfo
            {
                attacker = f.Kage, w = new HitWindow { dmg = 3, level = HitLevel.Mid, kb = new Vector2(3f, 0f), stun = 16 },
                dmg = 3, kb = new Vector2(3f, 0f), zone = HitZone.Body, contact = new Vector2(0.9f, 1f), moveId = "test.hit", window = -1,
            };
            f.Ronin.ReceiveHit(hit);
            Assert.AreEqual(FState.Hitstun, f.Ronin.state, "phase one: a light hit flinches");
            f.Run(60);
            SetHealth(f.Ronin, 0.4f);
            f.Run(2);
            f.Ronin.ReceiveHit(hit);
            Assert.AreNotEqual(FState.Hitstun, f.Ronin.state, "phase two has armour");
        }

        [Test]
        public void A_new_round_starts_the_boss_from_phase_one_again()
        {
            var f = Fight();
            f.Run(2);
            SetHealth(f.Ronin, 0.3f);
            f.Run(2);
            Assert.AreEqual(1, f.Ronin.Phases.Index);
            f.Ronin.ResetForRound(0.9f, -1f);
            f.Run(2);
            Assert.AreEqual(0, f.Ronin.Phases.Index);
            Assert.AreEqual(f.Ronin.Character.speedMul, f.Ronin.SpeedMul, 1e-5);
            Assert.AreEqual(CharacterFlags.None, f.Ronin.Flags);
            Assert.IsFalse(f.Ronin.AbilityEnabled(FootmanBoss.RushId));
            CollectionAssert.AreEqual(new[] { 0, 1, 0 }, Phases(f).Select(e => e.Index).ToArray(), "phase one is announced again");
        }

        [Test]
        public void Berserk_cools_abilities_down_faster_and_a_second_wave_delay_is_exposed()
        {
            BossCatalog.Register(new BossDef
            {
                id = "test.rage", name = "Rage", title = "t",
                phases = { new PhaseDef { name = "A", at = 1f }, new PhaseDef { name = "B", at = 0.5f, berserk = true, secondWave = 0.7f } },
            });
            _bosses.Add("test.rage");
            var f = Fight(BossDefOf("test.rage"));
            f.Run(2);
            Assert.AreEqual(1f, f.Ronin.AbilityCooldownMul);
            Assert.AreEqual(0f, f.Ronin.SecondWaveDelay);
            SetHealth(f.Ronin, 0.4f);
            f.Run(2);
            Assert.AreEqual(PhaseController.BerserkCooldownMul, f.Ronin.AbilityCooldownMul, 1e-6);
            Assert.AreEqual(0.7f, f.Ronin.SecondWaveDelay, 1e-6);
            Assert.IsTrue(f.Ronin.Phases.Current.berserk);
        }

        // ---- the AI uses the phase's abilities ----

        static int Starts(HeadlessFight f, string moveId) => f.Events(CombatEventType.MoveStarted).Count(e => e.MoveId == moveId && e.SourceId == FootmanBoss.Id);

        [Test]
        public void The_AI_does_not_use_an_ability_its_phase_does_not_allow()
        {
            var f = Fight(foe: Foe.Ai, bossX: 2.4f);
            f.Run(900);
            Assert.AreEqual(0, Starts(f, "captain.rush"), "the rush is not in phase one");
        }

        [Test]
        public void The_AI_uses_the_rush_once_phase_two_allows_it()
        {
            var f = new HeadlessFight(61, Foe.Ai, -3.0f, 0.5f, foeDef: BossDefOf());      // 3.5 m apart: inside the rush's band (1.5 to 4 m)
            _fights.Add(f);
            f.Kage.maxHealth = f.Kage.health = 1e7f;
            f.KageInput.Add(100000);
            SetHealth(f.Ronin, 0.4f);
            f.Run(600);
            Assert.AreEqual(1, f.Ronin.Phases.Index);
            Assert.Greater(Starts(f, "captain.rush"), 0, "the rush is in phase two");
        }

        // ---- the harness check of the thresholds against real hits ----

        [Test]
        public void Phases_trigger_at_the_right_health_when_Kage_wears_the_boss_down()
        {
            BossCatalog.Register(new BossDef
            {
                id = "test.three", name = "Three", title = "t",
                phases = { new PhaseDef { name = "A", at = 1f }, new PhaseDef { name = "B", at = 0.66f }, new PhaseDef { name = "C", at = 0.33f } },
            });
            _bosses.Add("test.three");
            var def = BossDefOf("test.three");
            var f = new HeadlessFight(61, Foe.Scripted, -0.4f, 0.4f, foeDef: def);
            _fights.Add(f);
            f.RoninInput.Add(100000);
            f.Kage.maxHealth = f.Kage.health = 1e7f;
            f.Ronin.maxHealth = f.Ronin.health = 150f;          // a few blows per phase
            f.KageInput.fallback = new ProfileAiTestKit.KageBot();      // walks up to the boss and strings punches, kicks and cuts together

            var seen = new List<(int index, float before, float after, int frame)>();
            int last = 0;
            float prev = 1f;
            for (int i = 0; i < 2400 && f.Ronin.health > 0f && last < 2; i++)
            {
                f.Run(1);
                float now = f.Ronin.health / f.Ronin.maxHealth;
                while (last < f.Ronin.Phases.Index) seen.Add((++last, prev, now, f.Ctx.Clock.Frame));
                prev = now;
            }
            Assert.AreEqual(2, seen.Count, "both later phases opened while Kage wore the boss down");
            Assert.LessOrEqual(seen[0].after, 0.66f, "phase B opened at or below 66%");
            Assert.Greater(seen[0].before, 0.66f, "and not on the frame before");
            Assert.LessOrEqual(seen[1].after, 0.33f, "phase C opened at or below 33%");
            Assert.Greater(seen[1].before, 0.33f);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Phases(f).Select(e => e.Index).ToArray());
            foreach (var p in Phases(f).Skip(1))
            {
                var hit = f.Events(CombatEventType.Hit).Last(e => e.Target == f.Ronin && e.Frame <= p.Frame);
                Assert.LessOrEqual(p.Frame - hit.Frame, 1, "the event follows the blow that crossed the threshold by at most a frame");
            }
        }
    }
}
