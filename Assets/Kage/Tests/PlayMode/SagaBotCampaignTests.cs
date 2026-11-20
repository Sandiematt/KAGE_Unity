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
    /// on a stretch, and each sub-boss's JS-tip bot. The foes are the real ones at the levels the stage world gives them
    /// (<see cref="StageWorld.FoeLevel"/>), health and damage untouched, and are never made easier (tuning is SAN-110). Two runs:
    /// <list type="bullet">
    /// <item><b>Starter Kage</b>, every stage on its own, three seeds each: the loadout of a fresh save. This is the honest table: it logs
    /// how often each stage's bot clears it and how long it takes, and asserts only that nothing softlocks.</item>
    /// <item><b>The campaign in order through the flow</b> (<see cref="FlowController.FinishStage"/> into a save on disk): the Prologue and the
    /// sub-bosses with the starter Kage, the stretches with the boosted Kage the road tests use (<see cref="StretchFight.StrongKage"/>:
    /// damage x4, health x20), because the stretch bot is the AI playing Kage and a group of regular foes beats the starter Kage (see the
    /// first table). It shows that stages open in order, every stage's world is playable to its end and the rewards land.</item>
    /// </list>
    /// </summary>
    public class SagaBotCampaignTests
    {
        const int Seeds = 3, RoadCap = 60 * 900, BossCap = 60 * 300, RoadQuiet = 60 * 60, BossQuiet = 600;

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
                $"{Stage,-14} {Bot,-18} {(Ended ? (Won ? "CLEARED" : "LOST   ") : "NO END ")} {Frames / 60.0,7:0.0} s  Kage hp {HpLeft * 100,4:0}%  quietest gap {Quiet / 60.0:0.0} s  {Note}";
        }

        static bool Combat(CombatEvent e) => e.Type is CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Parried or CombatEventType.Clashed or CombatEventType.Countered;

        static int Quietest(IEnumerable<CombatEvent> log, int frames)
        {
            int last = 0, quiet = 0;
            foreach (var e in log.Where(Combat)) { quiet = Mathf.Max(quiet, e.Frame - last); last = e.Frame; }
            return Mathf.Max(quiet, frames - last);
        }

        static CharacterDef KageDef(bool boosted) => boosted ? StretchFight.StrongKage() : KageCharacter.Def(GameRules.DefaultSave(false));

        // ---------------- one stage, one bot ----------------

        static Run PlayRoad(string stage, int seed, bool boosted)
        {
            var road = StretchData.FromId(stage);
            bool prologue = stage == PrologueAdditions.StageId;
            var bot = new PrologueBot(0.9f);
            using var f = new StretchFight(seed, road, KageDef(boosted), foeLevel: StageWorld.FoeLevel(StageWorldKind.Road), tutorial: prologue,
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

        internal static IInputSource BossBot(string foeId)
        {
            switch (foeId)
            {
                case "kuroe": return new KuroeBotFightTests.CensureAwareBot();
                case "garan": return new GaranBotFightTests.StormwiseBot();
                case "hisame": return new HisameBotFightTests.RainwiseBot();
                case "shiden": return new ShidenBotFightTests.SpearwiseBot();
                case "raikon": return new RaikonFightBotTests.StormBot();
                default: return new ProfileAiTestKit.KageBot();
            }
        }

        static Run PlayBoss(string stage, int seed, float? level = null)
        {
            var st = GameRules.Stage(stage);
            var kind = GameRules.WorldKindOf(stage);
            var bot = BossBot(st.Enemy);
            using var f = new HeadlessFight(seed, Foe.Ai, -1.2f, 1.2f, foeDef: CharacterCatalog.Get(st.Enemy), kageDef: KageDef(false));
            if (bot is RaikonFightBotTests.StormBot storm) storm.Fight = f;       // Raikon's bot reads his waves and hazards from the fight
            f.Ai.level = level ?? StageWorld.FoeLevel(kind);
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

        static bool IsFight(string stage) => GameRules.WorldKindOf(stage) != StageWorldKind.Road;

        static Run Play(string stage, int seed, bool boostRoads) =>
            IsFight(stage) ? PlayBoss(stage, seed) : PlayRoad(stage, seed, boostRoads && stage != PrologueAdditions.StageId);

        static void AssertNoSoftlock(Run r)
        {
            Assert.IsTrue(r.Ended, $"{r.Stage}: no end in the cap, a softlock: {r.Line}");
            Assert.Less(r.Quiet, IsFight(r.Stage) ? BossQuiet : RoadQuiet, $"{r.Stage}: nothing happened for too long: {r.Line}");
        }

        // ---------------- the honest table: each stage on its own ----------------

        static string Row(string label, string id, List<Run> runs)
        {
            var won = runs.Where(r => r.Won).ToList();
            var shown = won.Count > 0 ? won : runs;
            return $"  {id,-14} {runs[0].Bot,-18} {label,-26} cleared {won.Count}/{runs.Count}  mean time {shown.Average(r => r.Frames) / 60.0,6:0.0} s ({(won.Count > 0 ? "wins" : "losses")})  " +
                   $"Kage hp left {runs.Average(r => r.HpLeft) * 100:0}%  | seed 109: {runs[0].Note}";
        }

        [Test, Timeout(900000)]
        public void Each_stage_ends_for_its_bot_and_the_results_are_logged()
        {
            var sb = new StringBuilder($"SAN-109 bot results per stage, real foes at the stage world's levels, seeds 109-{108 + Seeds}:\n");
            List<Run> Many(Func<int, Run> play)
            {
                var runs = new List<Run>();
                for (int seed = 109; seed < 109 + Seeds; seed++) { var r = play(seed); AssertNoSoftlock(r); runs.Add(r); }
                return runs;
            }
            foreach (var id in Order)
            {
                sb.AppendLine(Row("starter Kage", id, Many(seed => Play(id, seed, boostRoads: false))));
                if (!IsFight(id) && id != PrologueAdditions.StageId) sb.AppendLine(Row("boosted Kage (x4 dmg, x20 hp)", id, Many(seed => Play(id, seed, boostRoads: true))));
            }
            Debug.Log(sb.ToString());
        }

        // ---------------- the campaign in order, through the flow ----------------

        const int Attempts = 8;

        [Test, Timeout(900000)]
        public void The_bots_play_the_Prologue_and_Act_I_in_order_through_the_flow_retrying_a_lost_stage_and_nothing_softlocks()
        {
            var dir = Path.Combine(Path.GetTempPath(), "kage_saga_bots_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var store = new SaveStore(dir);
                var flow = new FlowController(GameRules.DefaultSave(false), s => store.Save(s));
                flow.OpenCampaign();
                var runs = new List<Run>();
                var attempts = new Dictionary<string, int>();
                foreach (var id in Order)
                {
                    Assert.AreEqual(id, flow.SelectedStage, "the map lands on the next stage in order");
                    Assert.IsTrue(flow.BeginLoadout(), id + " is open");
                    flow.StartStage();
                    Run run = null;
                    for (int attempt = 1; attempt <= Attempts; attempt++)
                    {
                        run = Play(id, 108 + attempt, boostRoads: true);
                        attempts[id] = attempt;
                        Debug.Log($"SAN-109 bot run (attempt {attempt}): " + run.Line);
                        AssertNoSoftlock(run);
                        flow.FinishStage(run.Result);        // the stage hands the flow its result, win or loss
                        Assert.AreEqual(run.Won, GameRules.IsStageComplete(store.Load(), id), id + ": the save on disk follows the result");
                        if (run.Won) break;
                        Assert.IsFalse(GameRules.IsStageComplete(flow.Save, id), "a loss clears nothing");
                        flow.RetryFromResults();             // as a player would
                    }
                    runs.Add(run);
                    if (!run.Won) break;                     // the stages after a stage no bot clears stay locked, which is the order working
                    flow.ContinueFromResults();
                }

                var sb = new StringBuilder("SAN-109 campaign bot run in order, a lost stage retried up to " + Attempts + " times (Prologue and sub-bosses: starter Kage; stretches: boosted Kage):\n");
                foreach (var r in runs) sb.AppendLine($"  {r.Line}  [attempts: {attempts[r.Stage]}]");
                sb.AppendLine($"  stages cleared in order: {runs.Count(r => r.Won)}/{Order.Length}");
                Debug.Log(sb.ToString());

                var saved = store.Load();
                for (int i = 0; i < runs.Count; i++)
                    Assert.AreEqual(runs[i].Won, GameRules.IsStageComplete(saved, Order[i]), Order[i] + " on disk");
                // Raikon's own JS-tip bot (SAN-107) clears him on a retry (1 of 3 first attempts on the seeds of the table above): every stage clears, in order
                Assert.AreEqual(Order.Length, runs.Count(r => r.Won), "every stage cleared by its bot, in order:\n" + sb);
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
    }
}
