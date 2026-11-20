using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The pacing report (SAN-110): time-to-kill for every foe type, every stretch wave and every sub-boss (Raikon: <see cref="PacingRaikonReportTests"/>),
    /// with the gear a player can realistically own at that point (<see cref="PacingKit.Loadouts"/>), measured with a survival-helped Kage
    /// (<see cref="PacingKit.Helped"/>: real damage, 20 times the health) and, alongside, how often the same bot wins with its real health.
    /// Each test logs a table and asserts that every row is inside its target band, except the rows named in <see cref="Exceptions"/> (each with its reason).
    /// </summary>
    public class PacingReportTests
    {
        const int FoeSeeds = 5, WaveSeeds = 3, BossSeeds = 3, FirstSeed = 110;

        /// <summary>Rows allowed outside their band, and why. Keep each reason in sync with ADR-0010's SAN-110 notes.</summary>
        internal static readonly Dictionary<string, string> Exceptions = new Dictionary<string, string>
        {
            { "hollow_tutorial", "the Prologue's practice Hollow: a one-hit-style tutorial dummy (JS hp 95, damage x0.3), short on purpose" },
            { "prologue.1 mean wave", "the Prologue is the tutorial road: two easy Hollows, not a stretch wave of the Order" },
        };

        static void AssertBands(IEnumerable<PacingKit.Row> rows)
        {
            foreach (var r in rows)
            {
                if (r.InBand) continue;
                Assert.IsTrue(Exceptions.ContainsKey(r.Name), $"{r.Name} is outside {r.Target}: {r.Line()}");
            }
        }

        // ---------------- regular foes ----------------

        [Test, Timeout(1800000)]
        public void PacingReport_regular_foes_take_15_to_25_s()
        {
            var loadouts = PacingKit.Loadouts();
            var rows = new List<PacingKit.Row>();
            var seen = new HashSet<string>();
            foreach (var l in loadouts)
            {
                foreach (var foe in GameRules.StageFoeIds(l.Stage))
                {
                    if (GameRules.WorldKindOf(l.Stage) != StageWorldKind.Road || !seen.Add(foe)) continue;
                    var row = new PacingKit.Row { Name = foe, Target = PacingKit.Regular, Gear = $"first met at {l.Stage}; " + l.Text };
                    for (int i = 0; i < FoeSeeds; i++)
                    {
                        var h = PacingKit.OneFoe(foe, FirstSeed + i, PacingKit.Helped(l.Kage), StageWorld.FoeLevel(StageWorldKind.Road));
                        row.HelpedRuns++;
                        if (h.Won) row.Ttk.Add(h.Seconds);
                        var r = PacingKit.OneFoe(foe, FirstSeed + i, l.Kage.Clone(), StageWorld.FoeLevel(StageWorldKind.Road));
                        row.RealRuns++;
                        if (r.Won) { row.RealWins++; row.WonSeeds.Add(FirstSeed + i); }
                    }
                    rows.Add(row);
                }
            }
            Debug.Log(PacingKit.Table($"SAN-110 pacing: regular foes, one on one, AI Kage level {PacingKit.KageAiLevel}, foe level {StageWorld.FoeLevel(StageWorldKind.Road)}, {FoeSeeds} seeds, helped Kage x{PacingKit.SurvivalHelp} health", rows));
            AssertBands(rows);
        }

        // ---------------- stretch waves ----------------

        [Test, Timeout(1800000)]
        public void PacingReport_each_stretch_wave_takes_about_45_s()
        {
            var rows = new List<PacingKit.Row>();
            var loadouts = PacingKit.Loadouts();
            var stretchRows = new List<string>();
            var means = new List<PacingKit.Row>();
            foreach (var l in loadouts.Where(x => GameRules.WorldKindOf(x.Stage) == StageWorldKind.Road))
            {
                var road = StretchData.FromId(l.Stage);
                var names = road.Encounters.SelectMany((e, ei) => e.Waves.Select((w, wi) => $"{l.Stage} e{ei + 1}w{wi + 1} [{string.Join(",", w.Select(s => s.Replace("order_", "").Replace("hollow_", "h_")))}]")).ToList();
                var waveRows = names.Select(n => new PacingKit.Row { Name = n, Target = PacingKit.Wave, Gear = l.Text }).ToList();
                var whole = new PacingKit.Row { Name = l.Stage + " whole road", Target = new PacingKit.Band(0, 99999), Gear = l.Text };
                for (int i = 0; i < WaveSeeds; i++)
                {
                    var h = PacingKit.Stretch(l.Stage, FirstSeed + i, PacingKit.Helped(l.Kage));
                    whole.HelpedRuns++;
                    if (h.Won) whole.Ttk.Add(h.Seconds);
                    for (int w = 0; w < waveRows.Count; w++)
                    {
                        waveRows[w].HelpedRuns++;
                        if (h.WaveFrames.TryGetValue(w + 1, out var frames)) waveRows[w].Ttk.Add(frames / 60.0);
                    }
                    var r = PacingKit.Stretch(l.Stage, FirstSeed + i, l.Kage.Clone());
                    whole.RealRuns++; if (r.Won) { whole.RealWins++; whole.WonSeeds.Add(FirstSeed + i); }
                    foreach (var wr in waveRows) { wr.RealRuns++; if (r.Won) wr.RealWins++; }     // a wave row's "wins" is the whole road's
                }
                rows.AddRange(waveRows);
                stretchRows.Add(whole.Line());
                var mean = new PacingKit.Row { Name = l.Stage + " mean wave", Target = PacingKit.Wave, Gear = l.Text };
                mean.Ttk.Add(waveRows.Where(w => w.Ttk.Count > 0).Average(w => w.Mean));
                mean.HelpedRuns = 1;
                means.Add(mean);
            }
            Debug.Log(PacingKit.Table($"SAN-110 pacing: stretch waves (time a wave's foes are alive, spawn to last knock-out), RoadWalker AI Kage, foe level {StageWorld.FoeLevel(StageWorldKind.Road)}, {WaveSeeds} seeds; 'bot wins' is the whole road's", rows)
                + "  mean wave per stretch (what the 45 s target is asserted on; a single wave scales with its foe count):\n    " + string.Join("\n    ", means.Select(m => m.Line()))
                + "\n  whole roads (helped time = to the end of the stretch):\n    " + string.Join("\n    ", stretchRows));
            AssertBands(means);
        }

        // ---------------- sub-bosses ----------------

        [Test, Timeout(1800000)]
        public void PacingReport_each_sub_boss_takes_60_to_90_s()
        {
            var rows = new List<PacingKit.Row>();
            foreach (var l in PacingKit.Loadouts().Where(x => GameRules.WorldKindOf(x.Stage) == StageWorldKind.SubBoss))
            {
                var row = new PacingKit.Row { Name = GameRules.Stage(l.Stage).Enemy, Target = PacingKit.SubBoss, Gear = l.Stage + "; " + l.Text };
                for (int i = 0; i < BossSeeds; i++)
                {
                    var h = PacingKit.Boss(l.Stage, FirstSeed + i, PacingKit.Helped(l.Kage));
                    row.HelpedRuns++;
                    if (h.Won) row.Ttk.Add(h.Seconds);
                    var r = PacingKit.Boss(l.Stage, FirstSeed + i, l.Kage.Clone());
                    row.RealRuns++;
                    if (r.Won) { row.RealWins++; row.WonSeeds.Add(FirstSeed + i); }
                }
                rows.Add(row);
            }
            Debug.Log(PacingKit.Table($"SAN-110 pacing: sub-bosses, each one's JS-tip bot, foe level {StageWorld.FoeLevel(StageWorldKind.SubBoss)}, {BossSeeds} seeds, helped Kage x{PacingKit.SurvivalHelp} health", rows));
            AssertBands(rows);
        }

        // ---------------- Raikon ----------------

        [Test, Timeout(1800000)]
        public void PacingReport_Raikon_takes_2_to_3_minutes()
        {
            var l = PacingKit.LoadoutOf("act1.boss");
            var row = new PacingKit.Row { Name = "raikon", Target = PacingKit.Raikon, Gear = "act1.boss; " + l.Text };
            for (int i = 0; i < BossSeeds; i++)
            {
                var h = PacingKit.Boss("act1.boss", FirstSeed + i, PacingKit.Helped(l.Kage));
                row.HelpedRuns++;
                if (h.Won) row.Ttk.Add(h.Seconds);
                var r = PacingKit.Boss("act1.boss", FirstSeed + i, l.Kage.Clone());
                row.RealRuns++;
                if (r.Won) { row.RealWins++; row.WonSeeds.Add(FirstSeed + i); }
            }
            Debug.Log(PacingKit.Table($"SAN-110 pacing: Raikon, the StormBot (SAN-107), foe level {StageWorld.FoeLevel(StageWorldKind.Boss)}, {BossSeeds} seeds, helped Kage x{PacingKit.SurvivalHelp} health", new[] { row }));
            AssertBands(new[] { row });
        }
    }
}
