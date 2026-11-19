using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// A headless bot fight against Kuroe (SAN-103, the spec's "a scripted bot beats each sub-boss without softlocks, phases trigger,
    /// time-to-kill is logged"): Kage is a scripted bot with no randomness that follows the JS tip (it holds its blade while Censure is up),
    /// Kuroe is her real AI with real health on both sides, a fixed seed. The fight must end in a knock-out inside a generous cap, never go
    /// quiet for long, and open phase two on the way. The time-to-kill is logged against the spec's 60 to 90 s for a sub-boss.
    /// </summary>
    public class KuroeBotFightTests
    {
        const int Seed = 103, Cap = 60 * 240, MaxQuiet = 600;

        /// <summary>
        /// The SAN-95 Kage bot (walk in, cycle punch, punch, kick, weapon, kick, weapon) with two rules from the JS tip and the parry lesson:
        /// it holds its blade while a counter window is up, and it holds Guard while the foe's blow is coming (from the start of her attack to
        /// the end of its last hit window) when she is near. No randomness.
        /// </summary>
        sealed class CensureAwareBot : IInputSource
        {
            readonly ProfileAiTestKit.KageBot _bot = new();
            public int Waited, Guarded;
            public void Sample() { }
            public FrameInput Poll(Fighter self)
            {
                var opp = self.opponent;
                if (opp == null) return default;
                var m = opp.state == FState.Attack ? opp.move : null;
                if (m != null && m.counterFrom >= 0 && opp.moveFrame <= m.counterTo) { Waited++; return default; }
                if (m != null && m.hits.Count > 0 && opp.moveFrame <= m.hits.Max(h => h.to) && Mathf.Abs(opp.Pos.x - self.Pos.x) < 2.8f)
                {
                    Guarded++;
                    return new FrameInput { guard = true };
                }
                return _bot.Poll(self);
            }
        }

        static bool Combat(CombatEvent e) => e.Type is CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Parried or CombatEventType.Clashed or CombatEventType.Countered;

        [Test]
        public void A_Kage_bot_beats_Kuroe_through_both_phases_without_a_softlock()
        {
            using var f = new HeadlessFight(Seed, Foe.Ai, -1.2f, 1.2f, foeDef: CharacterCatalog.Get(Kuroe.Id));
            var bot = new CensureAwareBot();
            f.KageInput.fallback = bot;
            f.KageInput.Add(1);
            f.RunUntil(() => f.Kage.state == FState.KO || f.Ronin.state == FState.KO, Cap);

            int frames = f.Frames.Count;
            bool ended = f.Kage.state == FState.KO || f.Ronin.state == FState.KO;
            int censures = 0;
            for (int i = 0; i < f.Frames.Count; i++)
                if (f.Frames[i].Contains("kuroe.censure:") && (i == 0 || !f.Frames[i - 1].Contains("kuroe.censure:"))) censures++;
            Debug.Log($"SAN-103 bot fight vs Kuroe: {(ended ? (f.Ronin.state == FState.KO ? "Kuroe knocked out" : "Kage knocked out") : "NOT ended")} " +
                      $"after {frames} frames = time-to-kill {frames / 60f:0.0} s (spec: 60-90 s for a sub-boss); Kage {f.Kage.health:0.0}/{f.Kage.maxHealth:0}, " +
                      $"Kuroe {f.Ronin.health:0.0}/{f.Ronin.maxHealth:0}; phases {f.Events(CombatEventType.BossPhaseChanged).Count()}, Censures raised {censures}, " +
                      $"countered {f.Events(CombatEventType.Countered).Count()}, bot waited {bot.Waited} and guarded {bot.Guarded} frames; {f.Events(CombatEventType.Hit).Count()} hits, " +
                      $"{f.Events(CombatEventType.Blocked).Count()} blocks, {f.Events(CombatEventType.Parried).Count()} parries");

            Assert.IsTrue(ended, $"no knock-out in {Cap} frames (Kage {f.Kage.health:0.0}, Kuroe {f.Ronin.health:0.0})");
            Assert.AreEqual(FState.KO, f.Ronin.state, "the bot beats her");
            CollectionAssert.AreEqual(new[] { 0, 1 }, f.Events(CombatEventType.BossPhaseChanged).Select(e => e.Index).ToArray(), "both phases opened, in order");
            Assert.Greater(censures, 0, "she used Censure");

            int last = 0, quiet = 0;
            foreach (var e in f.Log.All.Where(Combat)) { quiet = Mathf.Max(quiet, e.Frame - last); last = e.Frame; }
            Assert.Less(quiet, MaxQuiet, $"{quiet} frames without a hit, block, parry or counter");
        }
    }
}
