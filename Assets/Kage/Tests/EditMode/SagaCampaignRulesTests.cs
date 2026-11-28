using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The Prologue and Act I as one campaign (SAN-109), on plain save objects: which world each stage is, which foes it plays and that
    /// every one exists, that the stages open strictly in order after the Prologue (all open in dev mode), and that clearing them in order
    /// through the flow grants the JS rewards and saves them.
    /// </summary>
    public class SagaCampaignRulesTests
    {
        static readonly string[] Order =
        {
            "prologue.1", "act1.stretch1", "act1.sub1", "act1.stretch2", "act1.sub2", "act1.stretch3", "act1.sub3", "act1.stretch4", "act1.sub4", "act1.boss",
        };

        string dir;

        [SetUp] public void SetUp() { dir = Path.Combine(Path.GetTempPath(), "kage_saga_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); }
        [TearDown] public void TearDown() { try { Directory.Delete(dir, true); } catch { } }

        // ---------------- stage -> world ----------------

        [Test]
        public void The_campaign_is_the_Prologue_then_Act_I_in_JS_order()
        {
            CollectionAssert.AreEqual(Order, GameRules.Stages.Select(s => s.Id).ToArray());
        }

        [TestCase("prologue.1", StageWorldKind.Road)]
        [TestCase("act1.stretch1", StageWorldKind.Road)]
        [TestCase("act1.stretch2", StageWorldKind.Road)]
        [TestCase("act1.stretch3", StageWorldKind.Road)]
        [TestCase("act1.stretch4", StageWorldKind.Road)]
        [TestCase("act1.sub1", StageWorldKind.SubBoss)]
        [TestCase("act1.sub2", StageWorldKind.SubBoss)]
        [TestCase("act1.sub3", StageWorldKind.SubBoss)]
        [TestCase("act1.sub4", StageWorldKind.SubBoss)]
        [TestCase("act1.boss", StageWorldKind.Boss)]
        public void A_stage_is_played_in_the_world_its_kind_says(string stage, StageWorldKind kind)
        {
            Assert.AreEqual(kind, GameRules.WorldKindOf(stage));
        }

        [Test]
        public void A_stage_the_campaign_does_not_have_has_no_world()
        {
            Assert.Throws<ArgumentException>(() => GameRules.WorldKindOf("act9.nothing"));
        }

        [TestCase("act1.sub1", "rooftops")]
        [TestCase("act1.sub2", "districts")]
        [TestCase("act1.sub3", "districts")]
        [TestCase("act1.sub4", "districts")]
        [TestCase("act1.boss", "rooftops", Description = "Raikon's stage names no env: the chapter's (JS st.env || ch.env)")]
        public void A_boss_stage_is_fought_in_the_JS_environment(string stage, string env)
        {
            Assert.AreEqual(env, GameRules.BossEnvOf(stage));
        }

        // ---------------- foes ----------------

        [TestCase("prologue.1", "hollow_tutorial", "hollow_shade")]
        [TestCase("act1.stretch1", "order_footman", "order_runner", "hollow_shade")]
        [TestCase("act1.stretch2", "order_footman", "order_chainthrower", "order_runner")]
        [TestCase("act1.stretch3", "order_shieldbearer", "order_footman", "order_runner", "hollow_flicker", "order_chainthrower", "hollow_shade")]
        [TestCase("act1.stretch4", "order_footman", "order_shieldbearer", "order_chainthrower", "order_runner", "order_ironclad", "hollow_husk")]
        [TestCase("act1.sub1", "kuroe")]
        [TestCase("act1.sub2", "garan")]
        [TestCase("act1.sub3", "hisame")]
        [TestCase("act1.sub4", "shiden")]
        [TestCase("act1.boss", "raikon")]
        public void A_stage_plays_the_squad_the_JS_data_names(string stage, params string[] foes)
        {
            CollectionAssert.AreEquivalent(foes, GameRules.StageFoeIds(stage).ToArray());
        }

        [Test]
        public void Every_foe_of_every_stage_is_a_character_of_the_catalog()
        {
            foreach (var st in GameRules.Stages)
                foreach (var id in GameRules.StageFoeIds(st.Id))
                {
                    Assert.IsNotNull(CharacterCatalog.Get(id), $"{st.Id}: foe '{id}' has no character");
                    Assert.IsNotNull(StretchSession.ResolveFoe(id), $"{st.Id}: foe '{id}' does not resolve");
                }
        }

        [Test]
        public void A_foe_id_with_no_character_is_an_error_not_a_Footman()
        {
            var e = Assert.Throws<InvalidOperationException>(() => StretchSession.ResolveFoe("no_such_foe"));
            StringAssert.Contains("no_such_foe", e.Message);
        }

        // ---------------- order and unlocks ----------------

        [Test]
        public void On_a_fresh_save_only_the_Prologue_is_open_and_each_clear_opens_the_next()
        {
            var save = GameRules.DefaultSave(false);
            for (int i = 0; i < Order.Length; i++)
            {
                for (int j = 0; j < Order.Length; j++)
                    Assert.AreEqual(j <= i, GameRules.IsStageUnlocked(save, Order[j]), $"before clearing {Order[i]}: {Order[j]}");
                var o = GameRules.CompleteStage(save, Order[i], Win());
                Assert.IsTrue(o.Win && o.FirstClear && !o.Rejected, Order[i]);
                Assert.AreEqual(i + 1 < Order.Length ? Order[i + 1] : null, o.Next, "the frontier after " + Order[i]);
            }
        }

        [Test]
        public void A_stage_cannot_be_cleared_out_of_order()
        {
            var save = GameRules.DefaultSave(false);
            var o = GameRules.CompleteStage(save, "act1.boss", Win());
            Assert.IsTrue(o.Rejected, "the boss is locked");
            Assert.IsFalse(GameRules.IsStageComplete(save, "act1.boss"));
            Assert.AreEqual(250, save.coins, "nothing earned");
        }

        [Test]
        public void With_dev_mode_on_every_stage_is_open()
        {
            var save = GameRules.SetDevMode(GameRules.DefaultSave(false), true);
            foreach (var id in Order) Assert.IsTrue(GameRules.IsStageUnlocked(save, id), id);
        }

        // ---------------- rewards, through the flow, saved ----------------

        static FightResult Win() => new FightResult { win = true, hpLeft = 0.8, time = 40, maxCombo = 4 };

        [Test]
        public void Clearing_the_whole_campaign_through_the_flow_grants_the_JS_rewards_and_saves_them()
        {
            var store = new SaveStore(dir);
            var flow = new FlowController(GameRules.DefaultSave(false), s => store.Save(s));
            flow.OpenCampaign();
            foreach (var id in Order)
            {
                Assert.AreEqual(id, flow.SelectedStage, "the map lands on the stage that just opened");
                Assert.IsTrue(flow.BeginLoadout(), id + " opens its loadout");
                Assert.IsTrue(flow.StartStage());
                flow.FinishStage(Win());
                Assert.AreEqual(FlowScreen.Results, flow.Screen);
                Assert.IsTrue(flow.LastOutcome.Win && flow.LastOutcome.FirstClear, id);
                var onDisk = store.Load();
                Assert.IsTrue(GameRules.IsStageComplete(onDisk, id), id + " is on disk the moment the results show");
                flow.ContinueFromResults();
            }
            var saved = store.Load();

            // the JS rewards (ADR-0010 tables): a helmet, an armour, a helmet, an armour, then Raikō no Tachi and the Raijin Kabuto
            Assert.IsTrue(GameRules.Owns(saved, GearKind.Armors, "ikazuchi_do"), "Garan");
            Assert.IsTrue(GameRules.Owns(saved, GearKind.Armors, "shiden_yoroi"), "Shiden");
            Assert.IsFalse(GameRules.Owns(saved, GearKind.Weapons, "raiko_no_tachi"), "Raikon's blade is withheld for now");

            // coins and gems of ten B-rated first clears at chapter tier 1: 115 per stretch (five), 255 per sub-boss (four), 395 for the boss
            Assert.AreEqual(250 + 5 * 115 + 4 * 255 + 395, saved.coins, "coins");
            Assert.AreEqual(5 + 4 * 2 + 5, saved.gems, "gems: 2 per sub-boss, 5 for the boss, on the 5 to start");
            Assert.AreEqual(10, saved.cleared.Count(kv => kv.Value));
            Assert.AreEqual(1, saved.defeated["act1.boss"]);
        }
    }
}
