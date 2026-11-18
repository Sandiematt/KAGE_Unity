using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The campaign flow in play (SAN-67): title -> campaign map -> loadout -> stage -> results -> map, pause, settings and the
    /// dev-mode confirm, driven by the same menu commands the keyboard and gamepad produce. Fights are real (the stage world
    /// runs on the real clock); the test only decides when one ends.
    /// </summary>
    public class GameFlowTests
    {
        string dir;
        GameFlow flow;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "kage_flow_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (flow != null) Object.DestroyImmediate(flow.gameObject);
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.DestroyImmediate(cam.gameObject);
            Time.timeScale = 1f;
            GameFeel.paused = false;
            try { Directory.Delete(dir, true); } catch { }
        }

        GameFlow StartFlow(bool devMode)
        {
            new SaveStore(dir).Save(devMode ? GameRules.SetDevMode(GameRules.DefaultSave(false), true) : GameRules.DefaultSave(false));
            flow = GameFlow.Create(dir);
            return flow;
        }

        string Focus { get { return flow.Active.Nav.Focus != null ? flow.Active.Nav.Focus.key : null; } }

        IEnumerator Until(System.Func<bool> cond, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("timed out waiting for " + what);
                yield return null;
            }
        }

        // ---- title and the road to a stage ----

        [UnityTest]
        public IEnumerator The_title_comes_first_and_the_keys_reach_the_campaign_map()
        {
            StartFlow(devMode: false);
            yield return null;
            Assert.AreEqual(FlowScreen.Title, flow.Controller.Screen);
            Assert.IsTrue(flow.Panel(FlowScreen.Title).Visible);
            Assert.AreEqual("continue", Focus);

            flow.Send(MenuCommand.Left);
            Assert.AreEqual("armory-tile", Focus, "left from the Continue card reaches the Armory tile under it (SAN-90)");
            flow.Send(MenuCommand.Left);
            Assert.IsTrue(Focus == "settings" || Focus == "armory" || Focus == "campaign", "and left again reaches the menu rows");
            flow.Active.Nav.SetFocus("campaign");
            flow.Send(MenuCommand.Up);
            Assert.AreEqual("play", Focus);
            flow.Send(MenuCommand.Down);
            flow.Send(MenuCommand.Submit);
            Assert.AreEqual(FlowScreen.Campaign, flow.Controller.Screen);
            Assert.IsTrue(flow.Panel(FlowScreen.Campaign).Visible);
            Assert.IsFalse(flow.Panel(FlowScreen.Title).Visible);

            flow.Send(MenuCommand.Back);
            Assert.AreEqual(FlowScreen.Title, flow.Controller.Screen);
        }

        [UnityTest]
        public IEnumerator A_locked_stage_has_no_fight_button_and_an_open_one_does()
        {
            StartFlow(devMode: false);
            yield return null;
            flow.Controller.OpenCampaign();
            var nav = flow.Panel(FlowScreen.Campaign).Nav;
            Assert.IsTrue(nav.Items.Single(i => i.key == "fight").interactable, "the Prologue is open on a fresh save");

            flow.Controller.SelectStage("act1.stretch1");
            nav = flow.Panel(FlowScreen.Campaign).Nav;
            Assert.IsFalse(nav.Items.Single(i => i.key == "fight").interactable, "Act I waits for the Prologue");
            nav.SetFocus("node:act1.stretch1");
            flow.Send(MenuCommand.Submit);          // the locked stage is selected: Enter must not open its loadout
            Assert.AreEqual(FlowScreen.Campaign, flow.Controller.Screen, "a locked stage can't be started");
            Assert.AreEqual("act1.stretch1", flow.Controller.SelectedStage);
        }

        [UnityTest]
        public IEnumerator Dev_mode_has_every_stage_open_on_the_map()
        {
            StartFlow(devMode: true);
            yield return null;
            flow.Controller.OpenCampaign();
            flow.Controller.SelectStage("act1.boss");
            Assert.IsTrue(flow.Panel(FlowScreen.Campaign).Nav.Items.Single(i => i.key == "fight").interactable);
        }

        [UnityTest]
        public IEnumerator Choosing_a_stage_starts_it_and_finishing_it_returns_to_the_map()
        {
            StartFlow(devMode: false);
            yield return null;
            flow.Controller.OpenCampaign();
            Assert.AreEqual("node:prologue.1", Focus);
            flow.Send(MenuCommand.Submit);          // the stage is already selected: this opens its loadout
            Assert.AreEqual(FlowScreen.Loadout, flow.Controller.Screen);
            Assert.IsNotNull(flow.World, "the two fighters stand behind the loadout");
            Assert.AreEqual(StageWorld.Phase.Tableau, flow.World.phase);
            Assert.AreEqual("fight", Focus);

            flow.Send(MenuCommand.Submit);
            Assert.AreEqual(FlowScreen.Stage, flow.Controller.Screen);
            Assert.AreEqual("prologue.1", flow.Controller.StageId);
            yield return Until(() => flow.World.phase == StageWorld.Phase.Fight, 6f, "the fight to begin");

            flow.World.ForceKnockOut(foeLoses: true);
            yield return Until(() => flow.Controller.Screen == FlowScreen.Results, 8f, "the results");
            Assert.IsTrue(flow.Controller.LastOutcome.Win);

            flow.Send(MenuCommand.Submit);          // Continue
            Assert.AreEqual(FlowScreen.Campaign, flow.Controller.Screen);
            Assert.IsNull(flow.World, "the fight is gone once back on the map");
            Assert.IsTrue(GameRules.IsStageComplete(flow.Controller.Save, "prologue.1"));
            Assert.AreEqual("act1.stretch1", flow.Controller.SelectedStage);

            var saved = new SaveStore(dir).Load();
            Assert.IsTrue(GameRules.IsStageComplete(saved, "prologue.1"), "the result was written to the save file");
        }

        [UnityTest]
        public IEnumerator A_lost_fight_goes_to_the_results_and_can_be_retried()
        {
            StartFlow(devMode: false);
            yield return null;
            flow.Controller.OpenCampaign();
            flow.Controller.BeginLoadout();
            flow.Controller.StartStage();
            yield return Until(() => flow.World.phase == StageWorld.Phase.Fight, 6f, "the fight to begin");
            flow.World.ForceKnockOut(foeLoses: false);
            yield return Until(() => flow.Controller.Screen == FlowScreen.Results, 8f, "the results");
            Assert.IsFalse(flow.Controller.LastOutcome.Win);
            Assert.AreEqual("retry", Focus);
            var before = flow.World;
            flow.Send(MenuCommand.Submit);
            Assert.AreEqual(FlowScreen.Stage, flow.Controller.Screen);
            Assert.AreNotSame(before, flow.World, "a retry builds a fresh fight");
            Assert.IsFalse(GameRules.IsStageComplete(flow.Controller.Save, "prologue.1"));
        }

        [UnityTest]
        public IEnumerator The_results_screen_shows_what_the_rules_paid_and_replay_starts_the_stage_again()
        {
            StartFlow(devMode: false);
            yield return null;
            yield return IntoAFight();
            flow.World.ForceKnockOut(foeLoses: true);
            yield return Until(() => flow.Controller.Screen == FlowScreen.Results, 8f, "the results");
            var screen = (ResultsScreen)flow.Panel(FlowScreen.Results);
            var o = flow.Controller.LastOutcome;
            Assert.IsTrue(screen.Visible);
            Assert.AreEqual("Victory", screen.View.Title);
            Assert.AreEqual(o.Rating.ToString(), screen.View.Rating);
            Assert.AreEqual(o.Coins, screen.View.Coins);
            Assert.AreEqual(o.Xp, screen.View.XpGain);
            Assert.AreEqual("continue", Focus);

            flow.Send(MenuCommand.Right);
            Assert.AreEqual("retry", Focus);
            var before = flow.World;
            flow.Send(MenuCommand.Submit);          // Replay
            Assert.AreEqual(FlowScreen.Stage, flow.Controller.Screen);
            Assert.AreNotSame(before, flow.World);
        }

        [UnityTest]
        public IEnumerator A_lost_fight_shows_the_defeat_state_with_no_gems_and_a_way_back_to_the_map()
        {
            StartFlow(devMode: false);
            yield return null;
            int gems = flow.Controller.Save.gems;
            yield return IntoAFight();
            flow.World.ForceKnockOut(foeLoses: false);
            yield return Until(() => flow.Controller.Screen == FlowScreen.Results, 8f, "the results");
            var v = ((ResultsScreen)flow.Panel(FlowScreen.Results)).View;
            Assert.AreEqual("Defeated", v.Title);
            Assert.IsFalse(v.ShowRewards);
            Assert.AreEqual(gems, flow.Controller.Save.gems);
            Assert.AreEqual("retry", Focus);

            flow.Send(MenuCommand.Right);
            Assert.AreEqual("armory", Focus, "Upgrade sits between Retry and Map (JS)");
            flow.Send(MenuCommand.Right);
            Assert.AreEqual("continue", Focus);
            flow.Send(MenuCommand.Submit);          // Map
            Assert.AreEqual(FlowScreen.Campaign, flow.Controller.Screen);
            Assert.IsFalse(GameRules.IsStageComplete(flow.Controller.Save, "prologue.1"));
        }

        // ---- pause ----

        IEnumerator IntoAFight()
        {
            flow.Controller.OpenCampaign();
            flow.Controller.BeginLoadout();
            flow.Controller.StartStage();
            yield return Until(() => flow.World != null && flow.World.phase == StageWorld.Phase.Fight, 6f, "the fight to begin");
        }

        [UnityTest]
        public IEnumerator Pause_stops_the_fight_and_resume_carries_on()
        {
            StartFlow(devMode: false);
            yield return null;
            yield return IntoAFight();
            var world = flow.World;
            yield return new WaitForSeconds(0.2f);

            flow.Send(MenuCommand.Pause);
            Assert.IsTrue(flow.Controller.Paused);
            Assert.IsTrue(flow.PauseMenu.Visible);
            Assert.AreEqual("resume", Focus);
            yield return null;
            yield return null;
            int frame = world.Fight.Clock.Frame;
            Assert.AreEqual(0f, Time.timeScale, "game time stands still");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(frame, world.Fight.Clock.Frame, "no fight frames run while paused");

            flow.Send(MenuCommand.Submit);          // Resume
            Assert.IsFalse(flow.Controller.Paused);
            Assert.IsFalse(flow.PauseMenu.Visible);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.Greater(world.Fight.Clock.Frame, frame, "the fight runs again");
            Assert.AreSame(world, flow.World, "resume keeps the same fight");
        }

        [UnityTest]
        public IEnumerator Retry_from_the_pause_menu_starts_the_stage_again()
        {
            StartFlow(devMode: false);
            yield return null;
            yield return IntoAFight();
            var before = flow.World;
            int attempt = flow.Controller.Attempt;

            flow.Send(MenuCommand.Pause);
            flow.Send(MenuCommand.Down);
            Assert.AreEqual("retry", Focus);
            flow.Send(MenuCommand.Submit);

            Assert.IsFalse(flow.Controller.Paused);
            Assert.IsFalse(flow.PauseMenu.Visible);
            Assert.Greater(flow.Controller.Attempt, attempt);
            Assert.AreNotSame(before, flow.World);
            Assert.AreEqual(StageWorld.Phase.Intro, flow.World.phase);
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator Quit_from_the_pause_menu_returns_to_the_map_with_the_stage_not_cleared()
        {
            StartFlow(devMode: false);
            yield return null;
            yield return IntoAFight();

            flow.Send(MenuCommand.Pause);
            flow.Send(MenuCommand.Down);
            flow.Send(MenuCommand.Down);
            Assert.AreEqual("quit", Focus);
            flow.Send(MenuCommand.Submit);

            Assert.AreEqual(FlowScreen.Campaign, flow.Controller.Screen);
            Assert.IsNull(flow.World);
            Assert.IsFalse(flow.PauseMenu.Visible);
            Assert.IsFalse(GameRules.IsStageComplete(flow.Controller.Save, "prologue.1"));
            yield return null;
            Assert.AreEqual(1f, Time.timeScale, "game time is back to normal");
        }

        [UnityTest]
        public IEnumerator Escape_pauses_and_a_second_press_resumes()
        {
            StartFlow(devMode: false);
            yield return null;
            yield return IntoAFight();
            flow.Send(MenuCommand.Pause);
            Assert.IsTrue(flow.Controller.Paused);
            flow.Send(MenuCommand.Back);
            Assert.IsFalse(flow.Controller.Paused);
        }

        // ---- settings and dev mode ----

        [UnityTest]
        public IEnumerator Turning_dev_mode_off_in_settings_asks_first_and_only_a_yes_replaces_the_save()
        {
            StartFlow(devMode: true);
            yield return null;
            flow.Controller.OpenSettings();
            Assert.AreEqual("devmode:0", Focus, "dev mode is on, so the On option has the focus");
            flow.Send(MenuCommand.Right);
            Assert.AreEqual("devmode:1", Focus);
            flow.Send(MenuCommand.Submit);          // Off

            Assert.IsTrue(flow.Controller.DevOffPending);
            Assert.IsTrue(flow.Confirm.Visible, "the question is on screen");
            Assert.IsTrue(flow.Controller.Save.devMode, "nothing changed yet");
            Assert.IsTrue(new SaveStore(dir).Load().devMode, "nothing was written yet");
            Assert.AreEqual("cancel", Focus, "the safe answer has the focus");

            flow.Send(MenuCommand.Submit);          // Cancel
            Assert.IsFalse(flow.Confirm.Visible);
            Assert.IsTrue(flow.Controller.Save.devMode);

            flow.Controller.SetDevMode(false);
            flow.Send(MenuCommand.Left);
            Assert.AreEqual("yes", Focus);
            flow.Send(MenuCommand.Submit);          // Yes
            Assert.IsFalse(flow.Confirm.Visible);
            Assert.IsFalse(flow.Controller.Save.devMode);
            Assert.AreEqual(250, flow.Controller.Save.coins);
            var onDisk = new SaveStore(dir).Load();
            Assert.IsFalse(onDisk.devMode);
            Assert.AreEqual(250, onDisk.coins);
        }

        [UnityTest]
        public IEnumerator A_setting_picked_on_the_settings_screen_is_saved()
        {
            StartFlow(devMode: false);
            yield return null;
            flow.Controller.OpenSettings();
            var shakeHalf = flow.Active.Nav.Items.Single(i => i.key == "shake:1");
            flow.Active.Nav.FocusOn(shakeHalf);
            flow.Send(MenuCommand.Submit);
            Assert.AreEqual("half", flow.Controller.Save.settings.shake);
            Assert.AreEqual("half", new SaveStore(dir).Load().settings.shake);
        }
    }
}
