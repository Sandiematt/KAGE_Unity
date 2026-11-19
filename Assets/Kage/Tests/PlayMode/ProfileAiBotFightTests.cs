using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static Kage.Tests.ProfileAiTestKit;

namespace Kage.Tests
{
    /// <summary>
    /// A headless bot fight per AI profile (SAN-95): a scripted Kage bot (<see cref="KageBot"/>, no randomness) against the Footman
    /// with each profile, real health on both sides, a fixed seed. Each fight must end in a knock-out inside a generous frame cap and
    /// never go quiet for long: no softlock (a foe that keeps away forever, a guard that never drops, a corner nobody leaves).
    /// </summary>
    public class ProfileAiBotFightTests
    {
        const int Seed = 95, Cap = 7200, MaxQuiet = 600;

        [TearDown]
        public void TearDown() => Tuning.ClearOverrides();

        static readonly string[] Profiles = { "regular", "agile", "defensive", "ranged", "heavy", "boss", AiProfiles.LegacyId };

        static bool Combat(CombatEvent e) => e.Type is CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Parried or CombatEventType.Clashed;

        [TestCaseSource(nameof(Profiles))]
        public void AKageBotAndAFootmanWithThisProfileFightToAKnockOut(string profile)
        {
            using var f = new HeadlessFight(Seed, Foe.Ai, -0.8f, 0.8f, foeDef: Footman(profile));
            f.KageInput.fallback = new KageBot();
            f.KageInput.Add(1);
            f.RunUntil(() => f.Kage.state == FState.KO || f.Ronin.state == FState.KO, Cap);

            int frames = f.Frames.Count;
            bool ended = f.Kage.state == FState.KO || f.Ronin.state == FState.KO;
            Debug.Log($"SAN-95 bot fight vs {profile}: {(ended ? "ended" : "NOT ended")} after {frames} frames, Kage {f.Kage.health:0} foe {f.Ronin.health:0}, {f.Events(CombatEventType.Hit).Count()} hits, {f.Events(CombatEventType.Blocked).Count()} blocks, {f.Events(CombatEventType.Parried).Count()} parries");
            Assert.IsTrue(ended, $"{profile}: no knock-out in {Cap} frames (Kage {f.Kage.health:0.0}, foe {f.Ronin.health:0.0})");

            int last = 0, quiet = 0;
            foreach (var e in f.Log.All.Where(Combat)) { quiet = Mathf.Max(quiet, e.Frame - last); last = e.Frame; }
            Assert.Less(quiet, MaxQuiet, $"{profile}: {quiet} frames without a hit, block or parry");
            Assert.Greater(f.Events(CombatEventType.Hit).Count() + f.Events(CombatEventType.Blocked).Count(), 3, $"{profile}: the fighters did meet");
        }

        [Test]
        public void ABotFightIsTheSameFightUnderTheSameSeed()
        {
            string Run()
            {
                using var f = new HeadlessFight(Seed, Foe.Ai, -0.8f, 0.8f, foeDef: Footman("agile"));
                f.KageInput.fallback = new KageBot();
                f.KageInput.Add(1);
                f.Run(1500);
                return string.Join("\n", f.Log.Lines());
            }
            Assert.AreEqual(Run(), Run());
        }
    }
}
