using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// A headless bot fight against Shiden (SAN-106, the spec's "a scripted bot beats each sub-boss without softlocks, phases trigger,
    /// time-to-kill is logged"): Kage is a scripted bot with no randomness that follows the JS tips for Shiden, Shiden is his real AI with real
    /// health on both sides, a fixed seed. The fight must end in a knock-out inside a generous cap, never go quiet for long, and open phase
    /// two on the way. The time-to-kill is logged against the spec's 60 to 90 s for a sub-boss.
    /// </summary>
    public class ShidenBotFightTests
    {
        const int Seed = 106, Cap = 60 * 240, MaxQuiet = 600;

        /// <summary>
        /// The SAN-95 Kage bot (walk in, cycle its buttons) with the JS tips for Shiden, reading only his move, its frame and its windows:
        /// <list type="bullet">
        /// <item>Vanguard Break ("step back from the shockwave"): through the wind-up, back-dash out of the slam's reach (it crushes a guard),
        /// then guard the shockwave (it can be blocked); from behind him, strike his back. The long recovery is the bot's ordinary
        /// walk-in-and-strike.</item>
        /// <item>The Braced Spear ("beat it by not rushing: wait it out"): stand still while the spear is set; once he lifts it, dash in and
        /// strike the opening.</item>
        /// <item>Hold the Line ("do not strike into it"): hold the blade while the guard is up (as the Kuroe bot does with Censure).</item>
        /// <item>The charging thrust: hold Guard from the wind-up until it stops.</item>
        /// <item>His other blows: hold Guard while one is coming when he is within the spear's reach.</item>
        /// <item>Outside the spear's reach and with nothing coming, dash in to close the distance (he fights at the end of the point).</item>
        /// </list>
        /// </summary>
        internal sealed class SpearwiseBot : IInputSource
        {
            readonly ProfileAiTestKit.KageBot _bot = new();
            public int GuardedShock, BackedOff, WaitedBrace, HeldBlade, GuardedCharge, Guarded;
            int _tick, _dashAt = -999;
            public void Sample() { }

            /// <summary>A dash press at most once per 45 frames, from neutral only: a dash buffered on a later frame would go wherever the stick points then.</summary>
            bool Dash(Fighter self)
            {
                if (_tick - _dashAt < 45 || self.Busy || self.state is not (FState.Idle or FState.Walk or FState.Run)) return false;
                _dashAt = _tick;
                return true;
            }

            public FrameInput Poll(Fighter self)
            {
                _tick++;
                var opp = self.opponent;
                if (opp == null) return default;
                var m = opp.state == FState.Attack ? opp.move : null;
                float dist = Mathf.Abs(opp.Pos.x - self.Pos.x);
                int toward = opp.Pos.x > self.Pos.x ? 1 : -1;
                bool behind = Mathf.Sign(self.Pos.x - opp.Pos.x) == -Mathf.Sign(opp.facing);
                if (m != null && m.id == "shiden.vanguardBreak" && opp.moveFrame <= m.hits[0].to + 18)
                {
                    if (behind) return _bot.Poll(self);                         // his back, with the slam and the shock going the other way
                    bool wallBehind = Mathf.Abs(self.Pos.x) > 6.9f && Mathf.Sign(self.Pos.x) == -toward;
                    if (dist < 2.7f && !wallBehind && opp.moveFrame < m.hits[0].from - 6)
                    {
                        BackedOff++;     // the JS tip: step back from the shockwave (out of the slam's reach: back-dashes, then walks)
                        return new FrameInput { dx = -toward, dash = Dash(self) };
                    }
                    if (dist > 4.6f) return default;
                    GuardedShock++;      // then guard the shock (it can be blocked; only the slam crushes a guard)
                    return new FrameInput { guard = true };
                }
                if (m != null && m.id == "shiden.brace")
                {
                    if (opp.moveFrame <= m.braceTo) { WaitedBrace++; return default; }     // the JS tip: don't rush the point, wait it out
                    if (dist > 1.0f) return new FrameInput { dx = toward, dash = dist > 1.8f && Dash(self) };   // then in fast at the opening
                }
                if (m != null && m.counterTo >= 0 && opp.moveFrame >= m.counterFrom - 2 && opp.moveFrame <= m.counterTo)
                {
                    HeldBlade++;
                    return default;
                }
                if (m != null && m.id == "shiden.chargingThrust")
                {
                    GuardedCharge++;
                    return new FrameInput { guard = true };
                }
                if (m != null && m.hits.Count > 0 && opp.moveFrame <= m.hits.Max(h => h.to) && dist < 3.2f)
                {
                    Guarded++;
                    return new FrameInput { guard = true };
                }
                if (dist > 2.2f && !self.Busy && (m == null || m.hits.Count == 0)) return new FrameInput { dx = toward, dash = Dash(self) };   // close the spear's distance fast
                return _bot.Poll(self);
            }
        }

        static bool Combat(CombatEvent e) => e.Type is CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Parried or CombatEventType.Clashed or CombatEventType.Countered;

        [Test]
        public void A_Kage_bot_beats_Shiden_through_both_phases_without_a_softlock()
        {
            using var f = new HeadlessFight(Seed, Foe.Ai, -1.2f, 1.2f, foeDef: CharacterCatalog.Get(Shiden.Id));
            var bot = new SpearwiseBot();
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
            int Blocks(string id) => f.Events(CombatEventType.Blocked).Count(e => e.Source == f.Ronin && e.MoveId == id);
            Debug.Log($"SAN-106 bot fight vs Shiden: {(ended ? (f.Ronin.state == FState.KO ? "Shiden knocked out" : "Kage knocked out") : "NOT ended")} " +
                      $"after {frames} frames = time-to-kill {frames / 60f:0.0} s (spec: 60-90 s for a sub-boss); Kage {f.Kage.health:0.0}/{f.Kage.maxHealth:0}, " +
                      $"Shiden {f.Ronin.health:0.0}/{f.Ronin.maxHealth:0}; phases {f.Events(CombatEventType.BossPhaseChanged).Count()}; " +
                      $"Vanguard Breaks {Count("shiden.vanguardBreak")} (slam hits {Hits("shiden.vanguardBreak")}, shock hits {Hits(ShidenAbilities.ShockId)}), " +
                      $"Braces {Count("shiden.brace")} (thrusts {Count("shiden.braceThrust")}, hits {Hits("shiden.braceThrust")}), " +
                      $"Hold the Line {Count("shiden.holdTheLine")} (punish hits {Hits("shiden.holdTheLinePunish")}), " +
                      $"charging thrusts {Count("shiden.chargingThrust")} (hits {Hits("shiden.chargingThrust")}, blocked {Blocks("shiden.chargingThrust")}); " +
                      $"bot backed off {bot.BackedOff}, guarded the shock {bot.GuardedShock}, waited the brace {bot.WaitedBrace}, held the blade {bot.HeldBlade}, guarded the charge {bot.GuardedCharge}, other blows {bot.Guarded} frames; " +
                      $"{f.Events(CombatEventType.Hit).Count()} hits, {f.Events(CombatEventType.Blocked).Count()} blocks, " +
                      $"{f.Events(CombatEventType.Parried).Count(e => e.Target == f.Ronin)} of his parries, {f.Events(CombatEventType.Countered).Count()} counters, " +
                      $"{f.Events(CombatEventType.GuardBroken).Count()} guard-breaks; " +
                      $"Kage landed {f.Events(CombatEventType.Hit).Count(e => e.Source == f.Kage)} for {f.Events(CombatEventType.Hit).Where(e => e.Source == f.Kage).Sum(e => e.Damage):0}; " +
                      "Shiden's hits by move: " +
                      string.Join(", ", f.Events(CombatEventType.Hit).Where(e => e.Source == f.Ronin).GroupBy(e => e.MoveId).Select(g => $"{g.Key} {g.Count()}x{g.Sum(e => e.Damage):0}")));

            Assert.IsTrue(ended, $"no knock-out in {Cap} frames (Kage {f.Kage.health:0.0}, Shiden {f.Ronin.health:0.0})");
            Assert.AreEqual(FState.KO, f.Ronin.state, "the bot beats him");
            CollectionAssert.AreEqual(new[] { 0, 1 }, f.Events(CombatEventType.BossPhaseChanged).Select(e => e.Index).ToArray(), "both phases opened, in order");
            Assert.Greater(Count("shiden.vanguardBreak") + Count("shiden.brace") + Count("shiden.holdTheLine") + Count("shiden.chargingThrust"), 0, "he used his abilities");

            int last = 0, quiet = 0;
            foreach (var e in f.Log.All.Where(Combat)) { quiet = Mathf.Max(quiet, e.Frame - last); last = e.Frame; }
            Assert.Less(quiet, MaxQuiet, $"{quiet} frames without a hit, block, parry or counter");
        }
    }
}
