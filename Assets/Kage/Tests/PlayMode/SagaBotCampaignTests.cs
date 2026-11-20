using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The headless bot run of the whole campaign (SAN-109): every Prologue and Act I stage, in order, played by the scripted bots the
    /// stage tickets left: the <see cref="PrologueBot"/> on the Prologue road, the <see cref="RoadWalker"/> (walks, fights with the AI)
    /// on a stretch, and each sub-boss's JS-tip bot. Kage is the starter loadout of a fresh save (not the boosted Kage the road tests
    /// use), the foes are the real ones at the levels the stage world gives them (<see cref="StageWorld.FoeLevel"/>), health and damage
    /// untouched. Each stage's result goes through the flow (<see cref="FlowController.FinishStage"/>) into a save on disk, so the run also
    /// shows that stages open in order and the rewards land. Time and result per stage are logged; a stage no bot clears is reported with
    /// its numbers (tuning is SAN-110) and never made easier.
    /// </summary>
    public class SagaBotCampaignTests
    {
        const int RoadCap = 60 * 900, BossCap = 60 * 300, RoadQuiet = 60 * 60, BossQuiet = 600;

        static readonly string[] Order =
        {
            "prologue.1", "act1.stretch1", "act1.sub1", "act1.stretch2", "act1.sub2", "act1.stretch3", "act1.sub3", "act1.stretch4", "act1.sub4", "act1.boss",
        };

        sealed class Run
        {
            public string Stage, Bot, Note = "";
            public bool Ended, Won;
            public int Frames, Quiet;
            public double HpLeft;
            public FightResult Result;
            public string Line =>
                $"{Stage,-14} {Bot,-16} {(Ended ? (Won ? "CLEARED" : "LOST   ") : "NO END ")} {Frames / 60.0,7:0.0} s  Kage hp {HpLeft * 100,5:0}%  quietest gap {Quiet / 60.0:0.0} s  {Note}";
        }

        static bool Combat(CombatEvent e) => e.Type is CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Parried or CombatEventType.Clashed or CombatEventType.Countered;

        static int Quietest(IEnumerable<CombatEvent> log, int frames)
        {
            int last = 0, quiet = 0;
            foreach (var e in log.Where(Combat)) { quiet = Mathf.Max(quiet, e.Frame - last); last = e.Frame; }
            return Mathf.Max(quiet, frames - last);
        }

        static CharacterDef KageDef() => KageCharacter.Def(GameRules.DefaultSave(false));

        // ---------------- one stage, one bot ----------------

        static Run PlayRoad(string stage, int seed)
        {
            var road = StretchData.FromId(stage);
            bool prologue = stage == PrologueAdditions.StageId;
            var bot = new PrologueBot(0.9f);
            using var f = new StretchFight(seed, road, KageDef(), foeLevel: StageWorld.FoeLevel(StageWorldKind.Road), tutorial: prologue,
                kageInput: prologue ? sf => { bot.Fight = sf; return bot; } : null);
            using var stats = new FightStatsRecorder(f.Ctx.Events, f.Kage, stretch: true);
            stats.MarkFightStart(0);
            bool Over() => f.Session.Cleared || f.Session.Failed || f.Kage.state == FState.KO;
            f.RunUntil(Over, RoadCap);
            var r = new Run { Stage = stage, Bot = prologue ? "PrologueBot" : "RoadWalker", Ended = Over(), Frames = f.Frames.Count, Won = f.Session.Cleared };
            r.HpLeft = f.Kage.health / f.Kage.maxHealth;
            r.Quiet = Quietest(f.Log.All, r.Frames);
            r.Result = stats.Result();
            r.Note = $"waves {f.Session.StretchWave}/{f.Session.TotalWaves}, {f.Session.Spawned.Count} foes, {f.Events(CombatEventType.Hit).Count(e => e.Target == f.Kage)} hits taken, {f.Events(CombatEventType.Parried).Count()} parries";
            return r;
        }

        static IInputSource BossBot(string foeId)
        {
            switch (foeId)
            {
                case "kuroe": return new KuroeBotFightTests.CensureAwareBot();
                case "garan": return new GaranBotFightTests.StormwiseBot();
                case "hisame": return new HisameBotFightTests.RainwiseBot();
                case "shiden": return new ShidenBotFightTests.SpearwiseBot();
                default: return new ProfileAiTestKit.KageBot();       // Raikon's own JS-tip bot is SAN-107's
            }
        }

        static Run PlayBoss(string stage, int seed)
        {
            var st = GameRules.Stage(stage);
            var kind = GameRules.WorldKindOf(stage);
            var bot = BossBot(st.Enemy);
            using var f = new HeadlessFight(seed, Foe.Ai, -1.2f, 1.2f, foeDef: CharacterCatalog.Get(st.Enemy), kageDef: KageDef());
            f.Ai.level = StageWorld.FoeLevel(kind);
            using var stats = new FightStatsRecorder(f.Ctx.Events, f.Kage);
            stats.MarkFightStart(0);
            f.KageInput.fallback = bot;
            f.KageInput.Add(1);
            bool Over() => f.Kage.state == FState.KO || f.Ronin.state == FState.KO;
            f.RunUntil(Over, BossCap);
            var r = new Run { Stage = stage, Bot = bot.GetType().Name, Ended = Over(), Frames = f.Frames.Count, Won = f.Ronin.state == FState.KO };
            r.HpLeft = f.Kage.health / f.Kage.maxHealth;
            r.Quiet = Quietest(f.Log.All, r.Frames);
            r.Result = stats.Result();
            r.Note = $"{st.Foe} {f.Ronin.health:0}/{f.Ronin.maxHealth:0} hp left, phases {f.Events(CombatEventType.BossPhaseChanged).Count()}";
            return r;
        }

        // ---------------- the campaign ----------------

        [Test]
        public void The_bots_play_the_Prologue_and_Act_I_in_order_through_the_flow_and_nothing_softlocks()
        {
            var dir = Path.Combine(Path.GetTempPath(), "kage_saga_bots_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var store = new SaveStore(dir);
                var flow = new FlowController(GameRules.DefaultSave(false), s => store.Save(s));
                flow.OpenCampaign();
                var runs = new List<Run>();
                foreach (var id in Order)
                {
                    Assert.AreEqual(id, flow.SelectedStage, "the map lands on the next stage in order");
                    Assert.IsTrue(flow.BeginLoadout(), id + " is open");
                    flow.StartStage();
                    var kind = GameRules.WorldKindOf(id);
                    var run = kind == StageWorldKind.Road ? PlayRoad(id, 109) : PlayBoss(id, 109);
                    runs.Add(run);
                    Debug.Log("SAN-109 bot run: " + run.Line);
                    // an end that is a result, or a stop: either way the stage hands the flow its result
                    flow.FinishStage(run.Result);
                    if (!run.Won) break;        // a lost stage stays the frontier: the stages after it are still locked, which is the order working
                    flow.ContinueFromResults();
                }

                var sb = new StringBuilder("SAN-109 campaign bot run (fresh save, starter loadout, real foes):\n");
                foreach (var r in runs) sb.AppendLine("  " + r.Line);
                sb.AppendLine($"  stages cleared in order: {runs.Count(r => r.Won)}/{Order.Length}");
                Debug.Log(sb.ToString());

                foreach (var r in runs)
                {
                    Assert.IsTrue(r.Ended, $"{r.Stage}: no end in the cap, a softlock: {r.Line}");
                    Assert.Less(r.Quiet, r.Stage.Contains("sub") || r.Stage.EndsWith("boss") ? BossQuiet : RoadQuiet, $"{r.Stage}: nothing happened for too long: {r.Line}");
                }
                var saved = store.Load();
                for (int i = 0; i < runs.Count; i++)
                    Assert.AreEqual(runs[i].Won, GameRules.IsStageComplete(saved, Order[i]), Order[i] + " on disk");
                Assert.AreEqual(Order.Length, runs.Count(r => r.Won), "every stage cleared by its bot, in order:\n" + sb);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
    }
}
