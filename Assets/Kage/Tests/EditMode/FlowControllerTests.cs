using System.Collections.Generic;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // The campaign flow (SAN-67): title -> campaign map -> pre-fight loadout -> stage -> results -> map, with pause,
    // settings and the dev-mode switch. Tested through FlowController, the pure state machine under GameFlow: no scene,
    // no UI, plain save objects.
    public class FlowControllerTests
    {
        const string Prologue = "prologue.1", Stretch1 = "act1.stretch1", Sub1 = "act1.sub1";

        List<SaveData> persisted;

        FlowController NewFlow(bool devMode = false)
        {
            persisted = new List<SaveData>();
            return new FlowController(GameRules.DefaultSave(devMode), s => persisted.Add(s));
        }

        static FightResult Win() { return new FightResult { win = true, hpLeft = 0.8, time = 40, maxCombo = 4 }; }
        static FightResult Loss() { return new FightResult { win = false, hpLeft = 0, time = 30 }; }

        // ---- the road from the title to a stage and back ----

        [Test]
        public void Starts_on_the_title_screen()
        {
            Assert.AreEqual(FlowScreen.Title, NewFlow().Screen);
        }

        [Test]
        public void Campaign_opens_from_the_title_and_back_returns_to_the_title()
        {
            var f = NewFlow();
            f.OpenCampaign();
            Assert.AreEqual(FlowScreen.Campaign, f.Screen);
            f.Back();
            Assert.AreEqual(FlowScreen.Title, f.Screen);
        }

        [Test]
        public void Choosing_an_unlocked_stage_on_the_map_opens_its_loadout()
        {
            var f = NewFlow();
            f.OpenCampaign();
            f.SelectStage(Prologue);
            Assert.IsTrue(f.BeginLoadout());
            Assert.AreEqual(FlowScreen.Loadout, f.Screen);
            Assert.AreEqual(Prologue, f.StageId);
        }

        [Test]
        public void A_locked_stage_can_be_looked_at_but_not_started()
        {
            var f = NewFlow();
            f.OpenCampaign();
            f.SelectStage(Stretch1);
            Assert.AreEqual(Stretch1, f.SelectedStage);
            Assert.IsFalse(f.BeginLoadout());
            Assert.AreEqual(FlowScreen.Campaign, f.Screen);
        }

        [Test]
        public void Selecting_an_unknown_stage_changes_nothing()
        {
            var f = NewFlow();
            f.OpenCampaign();
            f.SelectStage(Prologue);
            Assert.IsFalse(f.SelectStage("act9.nope"));
            Assert.AreEqual(Prologue, f.SelectedStage);
        }

        [Test]
        public void The_map_starts_with_the_story_frontier_selected()
        {
            var f = NewFlow();
            f.OpenCampaign();
            Assert.AreEqual(Prologue, f.SelectedStage);
            GameRules.MarkStageCleared(f.Save, Prologue);
            f.Back();
            f.OpenCampaign();
            Assert.AreEqual(Stretch1, f.SelectedStage);
        }

        [Test]
        public void Back_from_the_loadout_returns_to_the_map_with_the_stage_still_selected()
        {
            var f = NewFlow();
            f.OpenCampaign();
            f.SelectStage(Prologue);
            f.BeginLoadout();
            f.Back();
            Assert.AreEqual(FlowScreen.Campaign, f.Screen);
            Assert.AreEqual(Prologue, f.SelectedStage);
        }

        [Test]
        public void Fight_in_the_loadout_starts_the_stage()
        {
            var f = NewFlow();
            f.OpenCampaign();
            f.SelectStage(Prologue);
            f.BeginLoadout();
            int attempt = f.Attempt;
            Assert.IsTrue(f.StartStage());
            Assert.AreEqual(FlowScreen.Stage, f.Screen);
            Assert.AreEqual(Prologue, f.StageId);
            Assert.IsFalse(f.Paused);
            Assert.Greater(f.Attempt, attempt, "the host builds a fresh fight for each attempt");
        }

        [Test]
        public void Continue_goes_straight_to_the_loadout_of_the_next_stage()
        {
            var f = NewFlow();
            GameRules.MarkStageCleared(f.Save, Prologue);
            Assert.IsTrue(f.Continue());
            Assert.AreEqual(FlowScreen.Loadout, f.Screen);
            Assert.AreEqual(Stretch1, f.StageId);
        }

        [Test]
        public void Finishing_a_won_stage_shows_the_results_then_returns_to_the_map_with_the_stage_cleared()
        {
            var f = NewFlow();
            f.OpenCampaign();
            f.SelectStage(Prologue);
            f.BeginLoadout();
            f.StartStage();

            f.FinishStage(Win());
            Assert.AreEqual(FlowScreen.Results, f.Screen);
            Assert.IsTrue(f.LastOutcome.Win);
            Assert.IsTrue(f.LastOutcome.FirstClear);
            Assert.IsTrue(GameRules.IsStageComplete(f.Save, Prologue));

            f.ContinueFromResults();
            Assert.AreEqual(FlowScreen.Campaign, f.Screen);
            Assert.AreEqual(Stretch1, f.SelectedStage, "the map lands on the stage that just opened");
        }

        [Test]
        public void A_won_stage_pays_coins_and_is_written_to_the_save()
        {
            var f = NewFlow();
            int before = f.Save.coins;
            f.OpenCampaign();
            f.SelectStage(Prologue);
            f.BeginLoadout();
            f.StartStage();
            persisted.Clear();
            f.FinishStage(Win());
            Assert.Greater(f.Save.coins, before);
            Assert.IsNotEmpty(persisted, "the save is written after a stage result");
            Assert.AreSame(f.Save, persisted[persisted.Count - 1]);
        }

        [Test]
        public void A_lost_stage_clears_nothing_and_offers_a_retry()
        {
            var f = NewFlow();
            f.OpenCampaign();
            f.SelectStage(Prologue);
            f.BeginLoadout();
            f.StartStage();
            f.FinishStage(Loss());
            Assert.AreEqual(FlowScreen.Results, f.Screen);
            Assert.IsFalse(f.LastOutcome.Win);
            Assert.IsFalse(GameRules.IsStageComplete(f.Save, Prologue));

            int attempt = f.Attempt;
            f.RetryFromResults();
            Assert.AreEqual(FlowScreen.Stage, f.Screen);
            Assert.AreEqual(Prologue, f.StageId);
            Assert.Greater(f.Attempt, attempt);
        }

        [Test]
        public void Finishing_with_no_stage_in_play_is_ignored()
        {
            var f = NewFlow();
            f.FinishStage(Win());
            Assert.AreEqual(FlowScreen.Title, f.Screen);
            Assert.IsNull(f.LastOutcome);
        }

        // ---- pause ----

        FlowController InStage()
        {
            var f = NewFlow();
            f.OpenCampaign();
            f.SelectStage(Prologue);
            f.BeginLoadout();
            f.StartStage();
            return f;
        }

        [Test]
        public void Pause_stops_the_fight_and_resume_carries_on_with_the_same_fight()
        {
            var f = InStage();
            int attempt = f.Attempt;
            f.Pause();
            Assert.IsTrue(f.Paused);
            Assert.AreEqual(FlowScreen.Stage, f.Screen);
            f.Resume();
            Assert.IsFalse(f.Paused);
            Assert.AreEqual(attempt, f.Attempt, "resume does not rebuild the fight");
        }

        [Test]
        public void Back_toggles_the_pause_inside_a_stage()
        {
            var f = InStage();
            f.Back();
            Assert.IsTrue(f.Paused);
            f.Back();
            Assert.IsFalse(f.Paused);
            Assert.AreEqual(FlowScreen.Stage, f.Screen);
        }

        [Test]
        public void Retry_from_the_pause_starts_the_stage_again_unpaused()
        {
            var f = InStage();
            int attempt = f.Attempt;
            f.Pause();
            f.Retry();
            Assert.IsFalse(f.Paused);
            Assert.AreEqual(FlowScreen.Stage, f.Screen);
            Assert.AreEqual(Prologue, f.StageId);
            Assert.Greater(f.Attempt, attempt);
        }

        [Test]
        public void Quit_from_the_pause_returns_to_the_map_without_clearing_the_stage()
        {
            var f = InStage();
            f.Pause();
            f.QuitToMap();
            Assert.AreEqual(FlowScreen.Campaign, f.Screen);
            Assert.IsFalse(f.Paused);
            Assert.AreEqual(Prologue, f.SelectedStage);
            Assert.IsFalse(GameRules.IsStageComplete(f.Save, Prologue));
        }

        [Test]
        public void Pause_actions_only_work_while_paused_in_a_stage()
        {
            var f = NewFlow();
            f.Pause();
            Assert.IsFalse(f.Paused, "no pause on the title");
            f.OpenCampaign();
            f.Retry();
            f.QuitToMap();
            Assert.AreEqual(FlowScreen.Campaign, f.Screen);

            var g = InStage();
            int attempt = g.Attempt;
            g.Retry();     // not paused: ignored
            Assert.AreEqual(attempt, g.Attempt);
        }

        [Test]
        public void A_finished_stage_is_never_paused_afterwards()
        {
            var f = InStage();
            f.Pause();
            f.FinishStage(Win());
            Assert.IsFalse(f.Paused);
        }

        // ---- settings ----

        [Test]
        public void Settings_open_from_the_title_and_back_returns_to_it()
        {
            var f = NewFlow();
            f.OpenSettings();
            Assert.AreEqual(FlowScreen.Settings, f.Screen);
            f.Back();
            Assert.AreEqual(FlowScreen.Title, f.Screen);
        }

        [Test]
        public void A_setting_change_is_saved()
        {
            var f = NewFlow();
            f.OpenSettings();
            persisted.Clear();
            f.UpdateSettings(s => s.shake = "half");
            Assert.AreEqual("half", f.Save.settings.shake);
            Assert.AreEqual(1, persisted.Count);
        }

        // ---- the loadout ----

        [Test]
        public void Choosing_an_owned_weapon_equips_and_saves_it()
        {
            var f = NewFlow(devMode: true);
            GameRules.ApplyDevUnlock(f.Save);
            f.OpenCampaign();
            f.SelectStage(Prologue);
            f.BeginLoadout();
            persisted.Clear();
            Assert.IsTrue(f.EquipWeapon("tsubame"));
            Assert.AreEqual("tsubame", f.Save.equipped.weapon);
            Assert.AreEqual(1, persisted.Count);
        }

        [Test]
        public void A_weapon_that_is_not_owned_cannot_be_equipped()
        {
            var f = NewFlow();
            Assert.IsFalse(f.EquipWeapon("tsubame"));
            Assert.AreEqual("kagetsu", f.Save.equipped.weapon);
        }

        // ---- dev mode ----

        [Test]
        public void Turning_dev_mode_on_unlocks_everything_at_once()
        {
            var f = NewFlow();
            f.SetDevMode(true);
            Assert.IsTrue(f.Save.devMode);
            Assert.IsTrue(GameRules.IsStageUnlocked(f.Save, "act1.boss"));
            Assert.IsFalse(f.DevOffPending);
            Assert.IsNotEmpty(persisted);
        }

        [Test]
        public void Turning_dev_mode_off_asks_first_and_changes_nothing_until_confirmed()
        {
            var f = NewFlow(devMode: true);
            GameRules.ApplyDevUnlock(f.Save);
            var before = f.Save;
            persisted.Clear();

            f.SetDevMode(false);

            Assert.IsTrue(f.DevOffPending);
            Assert.AreSame(before, f.Save, "the dev save is still the save in play");
            Assert.IsTrue(f.Save.devMode);
            Assert.IsEmpty(persisted, "nothing was written");
        }

        [Test]
        public void Confirming_dev_mode_off_replaces_the_save_with_a_fresh_one_and_writes_it()
        {
            var f = NewFlow(devMode: true);
            GameRules.ApplyDevUnlock(f.Save);
            GameRules.MarkStageCleared(f.Save, Prologue);
            persisted.Clear();
            f.SetDevMode(false);

            f.ConfirmDevOff();

            Assert.IsFalse(f.DevOffPending);
            Assert.IsFalse(f.Save.devMode);
            Assert.AreEqual(250, f.Save.coins, "the real JS economy from a fresh start");
            Assert.IsFalse(GameRules.IsStageComplete(f.Save, Prologue));
            Assert.IsFalse(GameRules.IsStageUnlocked(f.Save, Stretch1));
            Assert.AreEqual(1, persisted.Count);
            Assert.AreSame(f.Save, persisted[0]);
        }

        [Test]
        public void Cancelling_dev_mode_off_keeps_the_dev_save()
        {
            var f = NewFlow(devMode: true);
            GameRules.ApplyDevUnlock(f.Save);
            var before = f.Save;
            f.SetDevMode(false);
            persisted.Clear();

            f.CancelDevOff();

            Assert.IsFalse(f.DevOffPending);
            Assert.AreSame(before, f.Save);
            Assert.IsTrue(f.Save.devMode);
            Assert.AreEqual(99999, f.Save.coins);
            Assert.IsEmpty(persisted);
        }

        [Test]
        public void Back_cancels_a_pending_dev_off_before_it_leaves_the_screen()
        {
            var f = NewFlow(devMode: true);
            f.OpenSettings();
            f.SetDevMode(false);
            f.Back();
            Assert.IsFalse(f.DevOffPending);
            Assert.IsTrue(f.Save.devMode);
            Assert.AreEqual(FlowScreen.Settings, f.Screen, "the first Back closes the question only");
        }

        [Test]
        public void Confirming_with_nothing_pending_does_not_touch_the_save()
        {
            var f = NewFlow(devMode: true);
            var before = f.Save;
            f.ConfirmDevOff();
            Assert.AreSame(before, f.Save);
            Assert.IsTrue(f.Save.devMode);
        }

        [Test]
        public void Dev_mode_off_while_already_off_asks_nothing()
        {
            var f = NewFlow(devMode: false);
            f.SetDevMode(false);
            Assert.IsFalse(f.DevOffPending);
        }
    }
}
