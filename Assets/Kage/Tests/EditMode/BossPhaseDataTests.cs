using System;
using NUnit.Framework;

namespace Kage.Tests
{
    /// <summary>
    /// Boss phase data (SAN-102, ADR-0008): <see cref="BossDef"/> read from the generated JS tables (names, thresholds, ability lists, the
    /// aggressive, berserk and second-wave flags), its validation, and the <see cref="BossCatalog"/>.
    /// </summary>
    public class BossPhaseDataTests
    {
        [Test]
        public void Raikon_has_three_phases_from_the_JS_table()
        {
            var b = BossDef.FromJs("raikon");
            Assert.AreEqual("raikon", b.id);
            Assert.AreEqual("Raikon", b.name);
            Assert.IsNotEmpty(b.title);
            Assert.AreEqual(3, b.Count);
            CollectionAssert.AreEqual(new[] { 1f, 0.66f, 0.33f }, new[] { b.phases[0].at, b.phases[1].at, b.phases[2].at });
            CollectionAssert.AreEqual(new[] { "The Storm Gathers", "Redline", "Lightning Ascendant" }, new[] { b.phases[0].name, b.phases[1].name, b.phases[2].name });
            CollectionAssert.AreEqual(new[] { "thunderSwing", "stormBreak", "skyfall" }, b.phases[0].abilities);
            CollectionAssert.Contains(b.phases[1].abilities, "redlineCounter");
            Assert.IsFalse(b.phases[1].aggressive);
            Assert.IsTrue(b.phases[2].aggressive);
            Assert.AreEqual(0.7f, b.phases[2].secondWave, 1e-6);
            Assert.AreEqual(0f, b.phases[1].secondWave);
        }

        [Test]
        public void The_sub_bosses_have_two_phases_with_their_own_flags()
        {
            var kuroe = BossDef.FromJs("kuroe");
            Assert.AreEqual(2, kuroe.Count);
            Assert.AreEqual(0.5f, kuroe.phases[1].at);
            Assert.IsTrue(kuroe.phases[1].aggressive);
            var hisame = BossDef.FromJs("hisame");
            Assert.IsTrue(hisame.phases[1].berserk);
            Assert.IsFalse(hisame.phases[0].berserk);
        }

        [Test]
        public void An_enemy_with_no_phases_or_an_unknown_id_is_not_a_boss()
        {
            Assert.IsNull(BossDef.FromJs("order_footman"));
            Assert.IsNull(BossDef.FromJs("nobody"));
        }

        [Test]
        public void A_phase_changes_nothing_by_default()
        {
            var p = new PhaseDef();
            Assert.AreEqual(1f, p.at);
            Assert.AreEqual(1f, p.speedMul);
            Assert.AreEqual(1f, p.moveRateMul);
            Assert.AreEqual(1f, p.damageMul);
            Assert.AreEqual(CharacterFlags.None, p.addFlags);
            Assert.AreEqual(CharacterFlags.None, p.removeFlags);
            Assert.IsNull(p.aiProfile);
            Assert.IsNull(p.abilities, "null: every ability of the character");
            Assert.IsFalse(p.aggressive);
        }

        static BossDef Boss(params float[] at)
        {
            var b = new BossDef { id = "t", name = "T" };
            foreach (var a in at) b.phases.Add(new PhaseDef { name = "p", at = a });
            return b;
        }

        [Test]
        public void Thresholds_must_start_at_one_and_fall_strictly()
        {
            Assert.DoesNotThrow(() => Boss(1f).Validate());
            Assert.DoesNotThrow(() => Boss(1f, 0.5f, 0.1f).Validate());
            Assert.Throws<InvalidOperationException>(() => Boss().Validate(), "no phases");
            Assert.Throws<InvalidOperationException>(() => Boss(0.9f).Validate(), "must open at 1");
            Assert.Throws<InvalidOperationException>(() => Boss(1f, 1f).Validate(), "equal");
            Assert.Throws<InvalidOperationException>(() => Boss(1f, 0.3f, 0.5f).Validate(), "rising");
            Assert.Throws<InvalidOperationException>(() => Boss(1f, 0f).Validate(), "zero is never reached");
        }

        [Test]
        public void The_catalog_registers_replaces_and_falls_back_to_the_JS_data()
        {
            Assert.AreEqual(2, BossCatalog.Get(FootmanBoss.Id).Count, "the test boss is registered");
            Assert.AreEqual(3, BossCatalog.Get("raikon").Count, "an unregistered JS boss uses the plain table");
            Assert.IsNull(BossCatalog.Get("order_footman"));
            Assert.IsNull(BossCatalog.Get(null));
            try
            {
                var tuned = BossDef.FromJs("raikon");
                tuned.phases[2].speedMul = 1.2f;
                BossCatalog.Register(tuned);
                Assert.AreEqual(1.2f, BossCatalog.Get("raikon").phases[2].speedMul);
            }
            finally { BossCatalog.Unregister("raikon"); }
            Assert.AreEqual(1f, BossCatalog.Get("raikon").phases[2].speedMul, "back to the table");
            Assert.Throws<InvalidOperationException>(() => BossCatalog.Register(Boss(0.5f)), "an invalid boss is refused");
        }

        [Test]
        public void A_character_made_from_a_JS_boss_entry_points_at_its_boss_data()
        {
            Assert.AreEqual("raikon", CharacterDef.FromJs("raikon").bossId);
            Assert.AreEqual("kuroe", CharacterDef.FromJs("kuroe").bossId);
            Assert.IsNull(CharacterDef.FromJs("order_footman").bossId);
            Assert.AreEqual(FootmanBoss.Id, CharacterCatalog.Get(FootmanBoss.Id).bossId);
        }

        [Test]
        public void The_test_boss_is_a_Footman_variant_in_the_dev_arena_cycle()
        {
            var d = CharacterCatalog.Get(FootmanBoss.Id);
            var footman = CharacterCatalog.Get("order_footman");
            Assert.AreEqual(footman.jsHealth, d.jsHealth);
            Assert.AreEqual(footman.partSet, d.partSet);
            Assert.AreEqual(footman.moveRate, d.moveRate);
            CollectionAssert.AreEqual(new[] { FootmanBoss.CleaveId, FootmanBoss.RushId }, d.abilities, "the ability slots, in order");
            CollectionAssert.Contains(KageGame.DevFoeIds(), FootmanBoss.Id, "selectable with F5");
        }

        [Test]
        public void The_formats_are_the_JS_ones()
        {
            Assert.AreEqual(2, FightFormat.Duel.RoundsToWin);
            Assert.AreEqual(60f, FightFormat.Duel.Timer, "the dev arena's clock");
            Assert.IsTrue(FightFormat.Duel.HasClock);
            Assert.AreEqual(1, FightFormat.Boss.RoundsToWin);
            Assert.IsFalse(FightFormat.Boss.HasClock);
            Assert.AreSame(FightFormat.Boss, FightFormat.For(CharacterCatalog.Get(FootmanBoss.Id)));
            Assert.AreSame(FightFormat.Duel, FightFormat.For(CharacterCatalog.Get("order_footman")));
            Assert.AreSame(FightFormat.Duel, FightFormat.For(null));
        }
    }
}
