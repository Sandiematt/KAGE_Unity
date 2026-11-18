using System.Collections.Generic;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // Where the Armory sits in the campaign flow (SAN-90): reached from the title, the pre-fight loadout and the results, Back
    // goes where it came from, and what is bought, upgraded or equipped there is in the save the fights are built from.
    public class ArmoryFlowTests
    {
        List<SaveData> persisted;

        FlowController NewFlow(SaveData save = null)
        {
            persisted = new List<SaveData>();
            return new FlowController(save ?? GameRules.DefaultSave(false), s => persisted.Add(s));
        }

        static FightResult Loss() { return new FightResult { win = false, hpLeft = 0, time = 30 }; }

        [Test]
        public void The_title_opens_the_Armory_and_Back_returns_to_the_title()
        {
            var f = NewFlow();
            Assert.IsTrue(f.OpenArmory());
            Assert.AreEqual(FlowScreen.Armory, f.Screen);
            Assert.IsNotNull(f.Armory);
            f.Back();
            Assert.AreEqual(FlowScreen.Title, f.Screen);
            Assert.IsNull(f.Armory);
        }

        [Test]
        public void The_loadout_opens_the_Armory_and_Back_returns_to_the_same_loadout()
        {
            var f = NewFlow();
            f.OpenCampaign();
            f.SelectStage("prologue.1");
            f.BeginLoadout();
            Assert.IsTrue(f.OpenArmory());
            Assert.AreEqual(FlowScreen.Armory, f.Screen);
            Assert.AreEqual("prologue.1", f.StageId, "the stage waits");
            f.Back();
            Assert.AreEqual(FlowScreen.Loadout, f.Screen);
            Assert.AreEqual("prologue.1", f.StageId);
            Assert.IsTrue(f.StartStage(), "the Fight button still works");
        }

        [Test]
        public void The_results_open_the_Armory_and_Back_returns_to_the_results()
        {
            var f = NewFlow();
            f.OpenCampaign();
            f.SelectStage("prologue.1");
            f.BeginLoadout();
            f.StartStage();
            f.FinishStage(Loss());
            Assert.AreEqual(FlowScreen.Results, f.Screen);
            var outcome = f.LastOutcome;
            Assert.IsTrue(f.OpenArmory());
            f.Back();
            Assert.AreEqual(FlowScreen.Results, f.Screen);
            Assert.AreSame(outcome, f.LastOutcome, "nothing was earned a second time");
            f.RetryFromResults();
            Assert.AreEqual(FlowScreen.Stage, f.Screen);
        }

        [Test]
        public void The_Armory_is_not_reachable_from_the_map_a_fight_or_settings()
        {
            var f = NewFlow();
            f.OpenCampaign();
            Assert.IsFalse(f.OpenArmory());
            f.SelectStage("prologue.1");
            f.BeginLoadout();
            f.StartStage();
            Assert.IsFalse(f.OpenArmory());
            Assert.AreEqual(FlowScreen.Stage, f.Screen);
        }

        [Test]
        public void Everything_done_in_the_Armory_is_saved_and_announced_to_the_screens()
        {
            var save = GameRules.DefaultSave(false);
            save.coins = 2000;
            var f = NewFlow(save);
            int raised = 0;
            f.Changed += () => raised++;
            f.OpenArmory();
            int afterOpen = raised;
            f.Armory.Select("tsubame");
            f.Armory.Buy();
            f.Armory.Equip();
            Assert.AreEqual("tsubame", f.Save.equipped.weapon);
            Assert.AreEqual(1550, f.Save.coins);
            Assert.AreEqual(2, persisted.Count, "one save for the purchase, one for equipping");
            Assert.Greater(raised, afterOpen);
        }

        [Test]
        public void The_weapon_equipped_in_the_Armory_is_the_one_the_loadout_screen_shows_equipped()
        {
            var save = GameRules.DefaultSave(false);
            save.coins = 2000;
            var f = NewFlow(save);
            f.OpenCampaign();
            f.SelectStage("prologue.1");
            f.BeginLoadout();
            f.OpenArmory();
            f.Armory.Select("tsubame");
            f.Armory.Buy();
            f.Armory.Equip();
            f.Back();
            Assert.AreEqual(FlowScreen.Loadout, f.Screen);
            Assert.AreEqual("tsubame", f.Save.equipped.weapon);
            Assert.AreEqual("tsubame", KageCharacter.Def(f.Save).weaponId);
        }
    }
}
