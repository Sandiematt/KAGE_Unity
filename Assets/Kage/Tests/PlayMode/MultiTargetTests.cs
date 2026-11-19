using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Multiple targets (SAN-83, ADR-0002): a fighter's target list, the "nearest in front" rule that picks
    /// <see cref="Fighter.opponent"/> (the current target), turning to a new target, and hits on any foe in reach.
    /// Distances in the rule come from <see cref="EngagementTuning"/> (Resources/Tuning/engagement.json).
    /// </summary>
    public class MultiTargetTests
    {
        readonly List<System.IDisposable> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Tuning.ClearOverrides();
        }

        /// <summary>Kage at <paramref name="kageX"/> facing right; foes that stand still at <paramref name="foeXs"/>.</summary>
        GroupFight Still(float kageX, params float[] foeXs) => Still(kageX, null, foeXs);

        GroupFight Still(float kageX, CharacterDef kageDef, params float[] foeXs)
        {
            var f = new GroupFight(KageMoveScenarios.Seed, kageX, foeXs, foesStill: true, kageDef: kageDef);
            _fights.Add(f);
            return f;
        }

        static EngagementTuning T => Tuning.Get<EngagementTuning>();

        // ---- one foe: nothing differs ----

        [Test]
        public void With_one_foe_the_target_list_has_one_entry_and_it_is_the_opponent()
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted);
            _fights.Add(f);
            f.RoninInput.Add(600);
            f.Run(1);

            CollectionAssert.AreEqual(new[] { f.Ronin }, f.Kage.Targets);
            CollectionAssert.AreEqual(new[] { f.Kage }, f.Ronin.Targets);
            Assert.AreSame(f.Ronin, f.Kage.opponent);
            Assert.AreSame(f.Kage, f.Ronin.opponent);
            Assert.IsTrue(f.Ctx.Engagement.IsEngaged(f.Ronin), "a lone foe is always engaged");

            // behind Kage it is still his target, and he turns to it
            f.Ronin.ResetForRound(-1.6f, 1f);
            f.Ronin.BeginFight();
            f.Run(10);
            Assert.AreSame(f.Ronin, f.Kage.opponent);
            Assert.AreEqual(-1f, f.Kage.facing);
        }

        [Test]
        public void The_training_bag_fight_has_no_target()
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Bag);
            _fights.Add(f);
            f.Run(2);
            CollectionAssert.IsEmpty(f.Kage.Targets);
            Assert.IsNull(f.Kage.opponent);
            Assert.AreSame(f.Bag, f.Kage.extraTarget);
        }

        // ---- the nearest-in-front rule ----

        [Test]
        public void The_target_list_holds_every_foe_in_fighter_order()
        {
            var f = Still(0f, 1.5f, -2.5f, 3.5f, -4.5f);
            f.Run(1);
            CollectionAssert.AreEqual(f.Foes, f.Kage.Targets);
            foreach (var foe in f.Foes) CollectionAssert.AreEqual(new[] { f.Kage }, foe.Targets, "a foe's only target is Kage");
        }

        [Test]
        public void Kage_targets_the_nearest_foe_in_front_over_a_nearer_one_behind()
        {
            // behind 1.0 counts 1.0 + behindPenalty, more than the 2.0 in front
            Assume.That(1.0f + T.behindPenalty, Is.GreaterThan(2.0f));
            var f = Still(0f, 3.0f, 2.0f, -1.0f);
            f.Run(30);
            Assert.AreSame(f.Foes[1], f.Kage.opponent, "the nearest in front, not the first in the list");
            Assert.AreEqual(1f, f.Kage.facing, "he keeps facing the front");
        }

        [Test]
        public void With_nobody_in_front_Kage_turns_to_the_nearest_foe_behind()
        {
            var f = Still(0f, -3.0f, -1.5f);
            f.Run(1);
            Assert.AreSame(f.Foes[1], f.Kage.opponent);
            f.Run(10);
            Assert.AreEqual(-1f, f.Kage.facing, "he turned round to his target");
        }

        [Test]
        public void A_foe_close_behind_turns_Kage_round_and_he_stays_turned()
        {
            // 0.8 behind counts 0.8 + behindPenalty, less than the 3.5 in front
            Assume.That(0.8f + T.behindPenalty, Is.LessThan(3.5f));
            var f = Still(0f, 3.5f, -0.8f);
            f.Run(10);
            Assert.AreSame(f.Foes[1], f.Kage.opponent);
            Assert.AreEqual(-1f, f.Kage.facing);
            for (int i = 0; i < 60; i++)
            {
                f.Run(1);
                Assert.AreSame(f.Foes[1], f.Kage.opponent, $"frame {i}: the foe now in front stays the target");
            }
        }

        [Test]
        public void The_current_target_holds_against_a_foe_at_about_the_same_distance()
        {
            // no penalty behind, so only the distances count: the foe behind is 0.1 nearer, less than the stickiness
            Tuning.Override(new EngagementTuning { behindPenalty = 0f, stickiness = 0.25f });
            var f = Still(0f, 1.6f, -1.5f);
            f.Run(30);
            Assert.AreSame(f.Foes[0], f.Kage.opponent, "the current target (the first foe since the fight was built) holds");
            Assert.AreEqual(1f, f.Kage.facing);

            Tuning.Override(new EngagementTuning { behindPenalty = 0f, stickiness = 0f });
            f.Run(10);
            Assert.AreSame(f.Foes[1], f.Kage.opponent, "with no stickiness the nearer foe wins");
            Assert.AreEqual(-1f, f.Kage.facing);
        }

        // ---- hits on any foe in reach ----

        static CharacterDef KageWithSpear()
        {
            var d = CharacterCatalog.Get(KageCharacter.Id);
            d.weaponId = "mizuchi";
            return d;
        }

        void OnePokeHitsBothFoes(bool legacy)
        {
            Tuning.Override(new CombatTuning { legacyHitResolution = legacy });
            var f = Still(0f, KageWithSpear(), 0.8f, 1.55f);
            Tuning.ClearOverrides();
            f.Run(1);
            Assert.AreSame(f.Foes[0], f.Kage.opponent);
            f.KageInput.Add(1, buttons: "P").Add(60);
            f.Run(40);

            var hit = f.Events(CombatEventType.Hit).Where(e => e.Source == f.Kage).Select(e => e.Target).ToList();
            CollectionAssert.Contains(hit, f.Foes[0], "the target");
            CollectionAssert.Contains(hit, f.Foes[1], "the foe behind it, in the spear's reach too");
            Assert.AreEqual(1, f.Events(CombatEventType.MoveStarted).Count(e => e.Source == f.Kage), "one poke");
        }

        [Test]
        public void One_spear_poke_hits_every_foe_in_its_reach() => OnePokeHitsBothFoes(legacy: false);

        [Test]
        public void One_spear_poke_hits_every_foe_in_its_reach_under_the_legacy_switch() => OnePokeHitsBothFoes(legacy: true);
    }
}
