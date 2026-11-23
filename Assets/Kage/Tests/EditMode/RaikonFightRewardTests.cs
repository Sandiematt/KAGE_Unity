using System.Collections.Generic;
using Kage.Rules;
using NUnit.Framework;

namespace Kage.Tests
{
    /// <summary>
    /// Winning against Raikon grants Raikō no Tachi and Raijin Kabuto (SAN-107, ADR-0009), through the game rules and the campaign flow: the
    /// stage's JS reward (<c>act1.boss</c>) is handed over when the fight is won and not when it is lost.
    /// </summary>
    public class RaikonFightRewardTests
    {
        const string Boss = "act1.boss";

        static FlowController FlowAtTheBoss(out List<SaveData> persisted)
        {
            var list = new List<SaveData>();
            persisted = list;
            var f = new FlowController(GameRules.DefaultSave(false), s => list.Add(s));
            foreach (var st in GameRules.Stages)
            {
                if (st.Id == Boss) break;
                GameRules.MarkStageCleared(f.Save, st.Id);
            }
            f.OpenCampaign();
            f.SelectStage(Boss);
            Assert.IsTrue(f.BeginLoadout(), "the boss stage is open once the sub-bosses are down");
            Assert.IsTrue(f.StartStage());
            return f;
        }

        static FightResult Win() => new FightResult { win = true, hpLeft = 0.4, time = 150, maxCombo = 6 };

        [Test]
        public void The_stage_is_Raikons_and_its_reward_is_the_tachi_and_the_helmet()
        {
            var st = GameRules.Stage(Boss);
            Assert.AreEqual("raikon", st.Enemy);
            CollectionAssert.AreEqual(new[] { "raiko_no_tachi" }, st.Reward.Weapons);
            CollectionAssert.AreEqual(new[] { "raijin_kabuto" }, st.Reward.Helmets);
            Assert.AreEqual(RewardType.Boss, GameRules.StageRewardType(Boss));
        }

        [Test]
        public void Beating_Raikon_through_the_flow_grants_Raijin_Kabuto_but_not_the_withheld_tachi_and_saves()
        {
            var f = FlowAtTheBoss(out var persisted);
            Assert.IsFalse(f.Save.owned.weapons.ContainsKey("raiko_no_tachi"), "not owned before the fight");
            Assert.IsFalse(f.Save.owned.helmets.ContainsKey("raijin_kabuto"));
            persisted.Clear();
            f.FinishStage(Win());
            Assert.IsTrue(f.LastOutcome.Win);
            Assert.IsTrue(f.LastOutcome.FirstClear);
            Assert.IsFalse(f.Save.owned.weapons.ContainsKey("raiko_no_tachi"), "Raikō no Tachi is withheld for now");
            Assert.IsTrue(f.Save.owned.helmets.ContainsKey("raijin_kabuto"), "Raijin Kabuto");
            Assert.IsTrue(GameRules.IsStageComplete(f.Save, Boss));
            Assert.IsNotEmpty(persisted, "the save was written");
            Assert.IsTrue(GameRules.EquipItem(f.Save, GearKind.Helmets, "raijin_kabuto"));
        }

        [Test]
        public void Losing_to_Raikon_grants_nothing()
        {
            var f = FlowAtTheBoss(out _);
            f.FinishStage(new FightResult { win = false, hpLeft = 0, time = 80 });
            Assert.IsFalse(f.LastOutcome.Win);
            Assert.IsFalse(f.Save.owned.weapons.ContainsKey("raiko_no_tachi"));
            Assert.IsFalse(f.Save.owned.helmets.ContainsKey("raijin_kabuto"));
            Assert.IsFalse(GameRules.IsStageComplete(f.Save, Boss));
        }
    }
}
