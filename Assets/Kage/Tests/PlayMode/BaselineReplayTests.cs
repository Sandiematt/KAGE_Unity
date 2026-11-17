using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The baseline replay: every current Kage move against the training bag and against the AI, with
    /// hit frames, damage, hit levels and per-frame positions. Later combat changes are compared
    /// against it; a change that is meant to alter the fight re-records it
    /// (KAGE_RECORD_BASELINES=1) and the diff of Baselines/*.txt goes up for review.
    /// </summary>
    public class BaselineReplayTests
    {
        static IEnumerable<MoveScenario> Scenarios() => KageMoveScenarios.All();

        [Test, TestCaseSource(nameof(Scenarios))]
        public void ReplayMatchesTheBaseline(MoveScenario scenario)
        {
            string actual;
            using (var f = scenario.Play(KageMoveScenarios.Seed))
                actual = BaselineReplay.Format(scenario.name, KageMoveScenarios.Seed, f.Log.Lines(), f.Frames);

            if (BaselineReplay.Recording)
            {
                BaselineReplay.Write(scenario.name, actual);
                return;
            }
            string path = BaselineReplay.PathOf(scenario.name);
            Assert.IsTrue(File.Exists(path), $"No baseline for {scenario.name}. Record it by running the PlayMode tests with KAGE_RECORD_BASELINES=1.");
            string diff = BaselineReplay.Compare(File.ReadAllText(path), actual);
            if (diff != null)
            {
                string dump = Path.Combine(Application.temporaryCachePath, scenario.name + ".actual.txt");
                File.WriteAllText(dump, actual);
                Assert.Fail($"{scenario.name} differs from its baseline at {diff}\n(full run written to {dump})");
            }
        }

        [Test, TestCaseSource(nameof(Scenarios))]
        public void TheLegacySwitchReplaysTheOldBaseline(MoveScenario scenario)
        {
            string actual;
            Tuning.Override(new CombatTuning { legacyHitResolution = true });
            try
            {
                using var f = scenario.Play(KageMoveScenarios.Seed);
                actual = BaselineReplay.Format(scenario.name, KageMoveScenarios.Seed, f.Log.Lines(), f.Frames);
            }
            finally { Tuning.ClearOverrides(); }

            string path = BaselineReplay.LegacyPathOf(scenario.name);
            Assert.IsTrue(File.Exists(path), $"No legacy baseline for {scenario.name}.");
            string diff = BaselineReplay.Compare(File.ReadAllText(path), actual);
            if (diff != null)
            {
                string dump = Path.Combine(Application.temporaryCachePath, scenario.name + ".legacy.actual.txt");
                File.WriteAllText(dump, actual);
                Assert.Fail($"{scenario.name} with the legacy switch differs from the old baseline at {diff}\n(full run written to {dump})");
            }
        }

        [Test]
        public void TheReplayStartsEveryKageMove()
        {
            var started = new HashSet<string>();
            foreach (var s in KageMoveScenarios.All())
            {
                using var f = s.Play(KageMoveScenarios.Seed);
                foreach (var e in f.Log.All)
                    if (e.SourceId == "kage" && e.Type is CombatEventType.MoveStarted or CombatEventType.RageArt) started.Add(e.MoveId);
                // a failed grab turns the throw into its whiff without starting a new move
                if (f.Frames.Any(l => l.Contains(" throwWhiff:"))) started.Add("throwWhiff");
            }
            var missing = KageMoveScenarios.AllKageMoves.Where(m => !started.Contains(m)).ToList();
            CollectionAssert.IsEmpty(missing, "moves the replay never starts");
        }

        [Test]
        public void TheReplayLandsHitsOnTheBagAndTheAi()
        {
            int bagHits = 0, aiHits = 0, aiConnects = 0;
            foreach (var s in KageMoveScenarios.All())
            {
                using var f = s.Play(KageMoveScenarios.Seed);
                foreach (var e in f.Log.All)
                {
                    if (e.SourceId != "kage") continue;
                    if (e.Type == CombatEventType.Hit && e.TargetId == "bag") bagHits++;
                    if (e.Type == CombatEventType.Hit && e.TargetId == "ronin") aiHits++;
                    if (e.Type is CombatEventType.Hit or CombatEventType.Blocked && e.TargetId == "ronin") aiConnects++;
                }
            }
            // a sanity floor: the scripts are placed to connect, not to whiff (a guarded hit connects too:
            // since SAN-65 the AI's guard is judged on the frame it is hit, so it blocks a few more of them)
            Assert.GreaterOrEqual(bagHits, 15);
            Assert.GreaterOrEqual(aiHits, 12);
            Assert.GreaterOrEqual(aiConnects, 20);
        }

        [Test]
        public void TheBaselineFormatToleratesOnlyTinyPositionDifferences()
        {
            string a = BaselineReplay.Format("t", 1, new[] { "0 FightStarted ->- -#-1" }, new[] { "1 kage 0.10000 0.02000 Idle - hp=100" });
            string near = BaselineReplay.Format("t", 1, new[] { "0 FightStarted ->- -#-1" }, new[] { "1 kage 0.10005 0.02000 Idle - hp=100" });
            string far = BaselineReplay.Format("t", 1, new[] { "0 FightStarted ->- -#-1" }, new[] { "1 kage 0.10100 0.02000 Idle - hp=100" });
            string otherEvent = BaselineReplay.Format("t", 1, new[] { "0 FightStarted ->- -#-1 " }, new[] { "1 kage 0.10000 0.02000 Idle - hp=100" });

            Assert.IsNull(BaselineReplay.Compare(a, near));
            Assert.IsNotNull(BaselineReplay.Compare(a, far));
            Assert.IsNotNull(BaselineReplay.Compare(a, otherEvent));
        }
    }
}
