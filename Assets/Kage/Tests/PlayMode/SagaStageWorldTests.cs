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
    /// The stage worlds of the campaign (SAN-109) through the real flow: each stage is built from its data as the world its kind says
    /// (a road with its session, HUD layout and title card; a sub-boss or the boss in a boss-format fight with the boss bar), a stage's end
    /// reaches the rules through <see cref="FlowController.FinishStage"/>, and a clear opens the next stage and is on disk. Fights run on
    /// the real clock, so the test decides when one ends (<see cref="StageWorld.ForceKnockOut"/>); the full fights are the bot run's.
    /// </summary>
    public class SagaStageWorldTests
    {
        string dir;
        GameFlow flow;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "kage_saga_world_" + System.Guid.NewGuid().ToString("N"));
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
            new SaveStore(dir).Save(GameRules.SetDevMode(GameRules.DefaultSave(false), devMode));
            flow = GameFlow.Create(dir);
            return flow;
        }

        IEnumerator Until(System.Func<bool> cond, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("timed out waiting for " + what);
                yield return null;
            }
        }

        /// <summary>Opens the loadout of a stage from the map (the stage must be open).</summary>
        void ToLoadout(string stage)
        {
            if (flow.Controller.Screen == FlowScreen.Title) flow.Controller.OpenCampaign();
            Assert.IsTrue(flow.Controller.SelectStage(stage), stage);
            Assert.IsTrue(flow.Controller.BeginLoadout(), stage + " is open");
        }

        // ---------------- every stage builds the world its kind says ----------------

        [UnityTest]
        public IEnumerator Every_stage_builds_its_world_from_its_data([ValueSource(nameof(StageIds))] string stage)
        {
            StartFlow(devMode: true);
            yield return null;
            ToLoadout(stage);
            var w = flow.World;
            Assert.IsNotNull(w, "the world stands behind the loadout");
            Assert.AreEqual(StageWorld.Phase.Tableau, w.phase);
            Assert.AreEqual(GameRules.WorldKindOf(stage), w.Kind);
            var st = GameRules.Stage(stage);

            if (w.Kind == StageWorldKind.Road)
            {
                Assert.IsNotNull(w.Stretch, "the road's session");
                Assert.IsNotNull(w.Road, "the road");
                Assert.IsNull(w.Match);
                var data = StretchData.FromId(stage);
                Assert.AreSame(data.Id, w.Stretch.Data.Id);
                Assert.AreEqual(data.Encounters.Length, w.Stretch.EncounterCount);
                Assert.AreEqual(data.TotalWaves, w.Stretch.TotalWaves);
                Assert.AreEqual(data.Prompts.Length > 0, w.Tutorial != null, "only the Prologue teaches");
                Assert.IsFalse(w.Stretch.Foes.Any(), "no foe stands before the first gate");
            }
            else
            {
                Assert.IsNotNull(w.Match, "the boss fight's match");
                Assert.IsNull(w.Stretch);
                Assert.AreSame(FightFormat.Boss, w.Match.Format, "one round, no clock");
                Assert.AreEqual(st.Enemy, w.Foe.Character.id, "the stage's boss");
            }

            // the stage starts: the title card then the fight, with the stage's own HUD
            flow.Controller.StartStage();
            yield return Until(() => flow.World.phase == StageWorld.Phase.Fight, 6f, "the fight to begin");
            w = flow.World;
            if (w.Kind == StageWorldKind.Road)
                Assert.IsTrue(w.Hud.gameObject.activeSelf);
            else
                Assert.IsNotNull(w.Hud.Boss, "the boss bar of " + st.Enemy + " when it has phases");
        }

        static string[] StageIds() => GameRules.Stages.Select(s => s.Id).ToArray();

        // ---------------- the end of a stage goes through the rules ----------------

        [UnityTest]
        public IEnumerator A_fresh_save_plays_the_stages_in_order_and_every_clear_is_saved()
        {
            StartFlow(devMode: false);
            yield return null;
            flow.Controller.OpenCampaign();
            var store = new SaveStore(dir);
            foreach (var st in GameRules.Stages)
            {
                Assert.AreEqual(st.Id, flow.Controller.SelectedStage, "the map lands on the stage that just opened");
                var later = GameRules.Stages.SkipWhile(s => s.Id != st.Id).Skip(1).FirstOrDefault();
                if (later != null) Assert.IsFalse(GameRules.IsStageUnlocked(flow.Controller.Save, later.Id), later.Id + " waits for " + st.Id);
                ToLoadout(st.Id);
                flow.Controller.StartStage();
                yield return Until(() => flow.World != null && flow.World.phase == StageWorld.Phase.Fight, 6f, st.Id + " to begin");
                flow.World.ForceKnockOut(foeLoses: true);
                yield return Until(() => flow.Controller.Screen == FlowScreen.Results, StageWorld.OverSeconds + 3f, st.Id + " results");
                Assert.IsTrue(flow.Controller.LastOutcome.Win && flow.Controller.LastOutcome.FirstClear, st.Id);
                Assert.IsTrue(GameRules.IsStageComplete(store.Load(), st.Id), st.Id + " is saved when the results show");
                if (later != null) Assert.IsTrue(GameRules.IsStageUnlocked(store.Load(), later.Id), later.Id + " opened");
                flow.Controller.ContinueFromResults();
            }
            Assert.IsNull(flow.Controller.Save.cleared.Where(kv => !kv.Value).Select(kv => kv.Key).FirstOrDefault());
            Assert.IsFalse(GameRules.Owns(store.Load(), GearKind.Weapons, "raiko_no_tachi"), "Raikō no Tachi is withheld for now");
            Assert.IsTrue(GameRules.Owns(store.Load(), GearKind.Helmets, "raijin_kabuto"), "the boss's helmet");
        }

        [UnityTest]
        public IEnumerator A_lost_stage_stays_the_frontier_with_nothing_unlocked([ValueSource(nameof(OneOfEach))] string stage)
        {
            StartFlow(devMode: true);
            yield return null;
            ToLoadout(stage);
            flow.Controller.StartStage();
            yield return Until(() => flow.World != null && flow.World.phase == StageWorld.Phase.Fight, 6f, "the fight to begin");
            flow.World.ForceKnockOut(foeLoses: false);
            yield return Until(() => flow.Controller.Screen == FlowScreen.Results, StageWorld.OverSeconds + 3f, "the results");
            Assert.IsFalse(flow.Controller.LastOutcome.Win);
            Assert.IsFalse(GameRules.IsStageComplete(flow.Controller.Save, stage), "a loss clears nothing");
        }

        static string[] OneOfEach() => new[] { "prologue.1", "act1.sub1", "act1.boss" };
    }
}
