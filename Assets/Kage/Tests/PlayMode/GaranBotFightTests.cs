using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// A headless bot fight against Garan (SAN-104, the spec's "a scripted bot beats each sub-boss without softlocks, phases trigger,
    /// time-to-kill is logged"): Kage is a scripted bot with no randomness that follows the JS tips for Garan, Garan is his real AI with real
    /// health on both sides, a fixed seed. The fight must end in a knock-out inside a generous cap, never go quiet for long, and open phase
    /// two on the way. The time-to-kill is logged against the spec's 60 to 90 s for a sub-boss.
    /// </summary>
    public class GaranBotFightTests
    {
        const int Seed = 104, Cap = 60 * 240, MaxQuiet = 600;

        /// <summary>
        /// The SAN-95 Kage bot (walk in, cycle its buttons) with the JS tips for Garan, reading only his move and its frame:
        /// <list type="bullet">
        /// <item>Storm Fist ("dash away or behind him, then hit the long recovery") and Tsuppari: through the wind-up or the crouch, close in
        /// and roll through him to his back, where the slam, the shock and the palms all go the other way, and hit him there; with no time
        /// to get round, dash away and keep walking back.</item>
        /// <item>The charge: wait out the tell, roll through him as he comes; he meets the wall.</item>
        /// <item>His other blows: hold Guard while one is coming when he is near (as the Kuroe bot), low against a low blow. Guard is never
        /// held otherwise, so the clinch (which takes only a guard) seldom comes.</item>
        /// </list>
        /// </summary>
        sealed class StormwiseBot : IInputSource
        {
            readonly ProfileAiTestKit.KageBot _bot = new();
            MoveDef _rolledFor;
            public int Rolled, Dashed, SteppedBack, Guarded;
            public void Sample() { }

            public FrameInput Poll(Fighter self)
            {
                var opp = self.opponent;
                if (opp == null) return default;
                var m = opp.state == FState.Attack ? opp.move : null;
                float dist = Mathf.Abs(opp.Pos.x - self.Pos.x);
                int toward = opp.Pos.x > self.Pos.x ? 1 : -1;
                bool behind = Mathf.Sign(self.Pos.x - opp.Pos.x) == -Mathf.Sign(opp.facing);
                bool storm = m != null && m.id == "garan.stormFist" && opp.moveFrame < m.hits[0].from + 4;
                bool palms = m != null && m.id == "garan.tsuppari" && opp.moveFrame <= m.hits[m.hits.Count - 1].to;
                if (storm || palms)
                {
                    if (behind) return _bot.Poll(self);                         // his back, with the blow going the other way: free hits
                    if (_rolledFor != m && !self.Busy)
                    {
                        if (dist < 1.05f) return Roll(m, toward);               // through him, to his back
                        int time = m.hits[0].from - opp.moveFrame;
                        if (time > 12) return new FrameInput { dx = toward };   // time to close in for the roll
                        return DashAway(m, toward);                            // no time: out of it
                    }
                    SteppedBack++;
                    return new FrameInput { dx = -toward };
                }
                if (m != null && m.id == "garan.charge")
                {
                    if (_rolledFor != m && opp.moveFrame >= m.hits[0].from && dist < 2.2f && !self.Busy) return Roll(m, toward);
                    return default;
                }
                if (m != null && m.hits.Count > 0 && opp.moveFrame <= m.hits.Max(h => h.to) && dist < 2.8f)
                {
                    Guarded++;
                    var next = m.hits.Where(h => h.to >= opp.moveFrame).OrderBy(h => h.from).FirstOrDefault();
                    return new FrameInput { guard = true, dy = next != null && next.level == HitLevel.Low ? -1 : 0 };     // a low blow: the low guard
                }
                return _bot.Poll(self);
            }

            FrameInput DashAway(MoveDef m, int toward)
            {
                _rolledFor = m;
                Dashed++;
                return new FrameInput { dash = true, dx = -toward };
            }

            FrameInput Roll(MoveDef m, int toward)
            {
                _rolledFor = m;
                Rolled++;
                return toward > 0 ? new FrameInput { tapRight2 = true } : new FrameInput { tapLeft2 = true };
            }
        }

        static bool Combat(CombatEvent e) => e.Type is CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Parried or CombatEventType.Clashed or CombatEventType.Countered;

        [Test]
        public void A_Kage_bot_beats_Garan_through_both_phases_without_a_softlock()
        {
            using var f = new HeadlessFight(Seed, Foe.Ai, -1.2f, 1.2f, foeDef: CharacterCatalog.Get(Garan.Id));
            var bot = new StormwiseBot();
            f.KageInput.fallback = bot;
            f.KageInput.Add(1);
            f.RunUntil(() => f.Kage.state == FState.KO || f.Ronin.state == FState.KO, Cap);

            int frames = f.Frames.Count;
            bool ended = f.Kage.state == FState.KO || f.Ronin.state == FState.KO;
            int Count(string id)
            {
                int n = 0;
                for (int i = 0; i < f.Frames.Count; i++)
                    if (f.Frames[i].Contains(id + ":") && (i == 0 || !f.Frames[i - 1].Contains(id + ":"))) n++;
                return n;
            }
            int Hits(string id) => f.Events(CombatEventType.Hit).Count(e => e.Source == f.Ronin && e.MoveId == id);
            Debug.Log($"SAN-104 bot fight vs Garan: {(ended ? (f.Ronin.state == FState.KO ? "Garan knocked out" : "Kage knocked out") : "NOT ended")} " +
                      $"after {frames} frames = time-to-kill {frames / 60f:0.0} s (spec: 60-90 s for a sub-boss); Kage {f.Kage.health:0.0}/{f.Kage.maxHealth:0}, " +
                      $"Garan {f.Ronin.health:0.0}/{f.Ronin.maxHealth:0}; phases {f.Events(CombatEventType.BossPhaseChanged).Count()}; " +
                      $"Storm Fists {Count("garan.stormFist")} (slam hits {Hits("garan.stormFist")}, shock hits {Hits(GaranAbilities.ShockId)}), " +
                      $"Tsuppari {Count("garan.tsuppari")} (palm hits {Hits("garan.tsuppari")}), charges {Count("garan.charge")} (hits {Hits("garan.charge")}), " +
                      $"clinches {Count("garan.clinch") + Count("garan.clinchWhiff")} (throws {Hits("garan.clinch")}); bot rolled {bot.Rolled}, dashed away {bot.Dashed}, stepped back {bot.SteppedBack} " +
                      $"and guarded {bot.Guarded} frames; {f.Events(CombatEventType.Hit).Count()} hits, {f.Events(CombatEventType.Blocked).Count()} blocks, " +
                      $"{f.Events(CombatEventType.GuardBroken).Count()} guard-breaks; Kage landed {f.Events(CombatEventType.Hit).Count(e => e.Source == f.Kage)} " +
                      $"for {f.Events(CombatEventType.Hit).Where(e => e.Source == f.Kage).Sum(e => e.Damage):0}; Garan's hits by move: " +
                      string.Join(", ", f.Events(CombatEventType.Hit).Where(e => e.Source == f.Ronin).GroupBy(e => e.MoveId).Select(g => $"{g.Key} {g.Count()}x{g.Sum(e => e.Damage):0}")));

            Assert.IsTrue(ended, $"no knock-out in {Cap} frames (Kage {f.Kage.health:0.0}, Garan {f.Ronin.health:0.0})");
            Assert.AreEqual(FState.KO, f.Ronin.state, "the bot beats him");
            CollectionAssert.AreEqual(new[] { 0, 1 }, f.Events(CombatEventType.BossPhaseChanged).Select(e => e.Index).ToArray(), "both phases opened, in order");
            Assert.Greater(Count("garan.stormFist") + Count("garan.tsuppari"), 0, "he used his abilities");

            int last = 0, quiet = 0;
            foreach (var e in f.Log.All.Where(Combat)) { quiet = Mathf.Max(quiet, e.Frame - last); last = e.Frame; }
            Assert.Less(quiet, MaxQuiet, $"{quiet} frames without a hit, block, parry or counter");
        }
    }
}
