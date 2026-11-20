using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// A headless bot fight against Hisame (SAN-105, the spec's "a scripted bot beats each sub-boss without softlocks, phases trigger,
    /// time-to-kill is logged"): Kage is a scripted bot with no randomness that follows the JS tips for Hisame, Hisame is her real AI with real
    /// health on both sides, a fixed seed. The fight must end in a knock-out inside a generous cap, never go quiet for long, and open phase
    /// two on the way. The time-to-kill is logged against the spec's 60 to 90 s for a sub-boss.
    /// </summary>
    public class HisameBotFightTests
    {
        const int Seed = 105, Cap = 60 * 240, MaxQuiet = 600;

        /// <summary>
        /// The SAN-95 Kage bot (walk in, cycle its buttons) with the JS tips for Hisame, reading only her move, its frame and her projectiles:
        /// <list type="bullet">
        /// <item>Lightning Cut ("block or jump it, then strike as she skids to a stop"): hold Guard from the low tell to the end of the cut;
        /// the skid after it is the bot's ordinary walk-in-and-strike.</item>
        /// <item>Kocho ("a glint first, time enough to turn and guard"): stand through the step (she can't be touched), then from the landing
        /// turn to her and hold Guard until the cut is over.</item>
        /// <item>Smoke Bomb: hold Guard while her bomb is in the air within 4 m.</item>
        /// <item>Her other blows: hold Guard while one is coming when she is near (as the Kuroe bot).</item>
        /// </list>
        /// </summary>
        internal sealed class RainwiseBot : IInputSource
        {
            readonly ProfileAiTestKit.KageBot _bot = new();
            public int GuardedCut, GuardedKocho, GuardedBomb, Guarded;
            public void Sample() { }

            public FrameInput Poll(Fighter self)
            {
                var opp = self.opponent;
                if (opp == null) return default;
                var m = opp.state == FState.Attack ? opp.move : null;
                float dist = Mathf.Abs(opp.Pos.x - self.Pos.x);
                int toward = opp.Pos.x > self.Pos.x ? 1 : -1;
                if (m != null && m.id == "hisame.kocho")
                {
                    if (opp.moveFrame < m.vaultTo) return default;                      // the step: nothing to hit, wait
                    if (opp.moveFrame <= m.hits[0].to)
                    {
                        GuardedKocho++;
                        return self.facing != toward ? new FrameInput { dx = toward } : new FrameInput { guard = true };   // turn, then guard
                    }
                }
                if (m != null && m.id == "hisame.lightningCut" && opp.moveFrame <= m.hits[0].to && dist < 4f)
                {
                    GuardedCut++;
                    return new FrameInput { guard = true };
                }
                if (self.ctx != null && self.ctx.Projectiles.Any(p => p.Owner == opp && p.Alive && Mathf.Abs(p.Position.x - self.Pos.x) < 4f))
                {
                    GuardedBomb++;
                    return new FrameInput { guard = true };
                }
                if (m != null && m.hits.Count > 0 && opp.moveFrame <= m.hits.Max(h => h.to) && dist < 2.8f)
                {
                    Guarded++;
                    return new FrameInput { guard = true };
                }
                return _bot.Poll(self);
            }
        }

        static bool Combat(CombatEvent e) => e.Type is CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Parried or CombatEventType.Clashed or CombatEventType.Countered;

        [Test]
        public void A_Kage_bot_beats_Hisame_through_both_phases_without_a_softlock()
        {
            using var f = new HeadlessFight(Seed, Foe.Ai, -1.2f, 1.2f, foeDef: CharacterCatalog.Get(Hisame.Id));
            var bot = new RainwiseBot();
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
            Debug.Log($"SAN-105 bot fight vs Hisame: {(ended ? (f.Ronin.state == FState.KO ? "Hisame knocked out" : "Kage knocked out") : "NOT ended")} " +
                      $"after {frames} frames = time-to-kill {frames / 60f:0.0} s (spec: 60-90 s for a sub-boss); Kage {f.Kage.health:0.0}/{f.Kage.maxHealth:0}, " +
                      $"Hisame {f.Ronin.health:0.0}/{f.Ronin.maxHealth:0}; phases {f.Events(CombatEventType.BossPhaseChanged).Count()}; " +
                      $"Lightning Cuts {Count("hisame.lightningCut")} (hits {Hits("hisame.lightningCut")}, blocked {Blocks("hisame.lightningCut")}), " +
                      $"Kocho {Count("hisame.kocho")} (cut hits {Hits("hisame.kocho")}, blocked {Blocks("hisame.kocho")}), " +
                      $"Smoke Bombs {Count("hisame.smokeBomb")} (hits {Hits(HisameAbilities.SmokeProjectileId)}, blocked {Blocks(HisameAbilities.SmokeProjectileId)}); " +
                      $"bot guarded the cut {bot.GuardedCut}, Kocho {bot.GuardedKocho}, bombs {bot.GuardedBomb}, other blows {bot.Guarded} frames; " +
                      $"{f.Events(CombatEventType.Hit).Count()} hits, {f.Events(CombatEventType.Blocked).Count()} blocks, " +
                      $"{f.Events(CombatEventType.Parried).Count(e => e.Target == f.Ronin)} of her parries, {f.Events(CombatEventType.GuardBroken).Count()} guard-breaks; " +
                      $"Kage landed {f.Events(CombatEventType.Hit).Count(e => e.Source == f.Kage)} for {f.Events(CombatEventType.Hit).Where(e => e.Source == f.Kage).Sum(e => e.Damage):0}; " +
                      "Hisame's hits by move: " +
                      string.Join(", ", f.Events(CombatEventType.Hit).Where(e => e.Source == f.Ronin).GroupBy(e => e.MoveId).Select(g => $"{g.Key} {g.Count()}x{g.Sum(e => e.Damage):0}")));

            Assert.IsTrue(ended, $"no knock-out in {Cap} frames (Kage {f.Kage.health:0.0}, Hisame {f.Ronin.health:0.0})");
            Assert.AreEqual(FState.KO, f.Ronin.state, "the bot beats her");
            CollectionAssert.AreEqual(new[] { 0, 1 }, f.Events(CombatEventType.BossPhaseChanged).Select(e => e.Index).ToArray(), "both phases opened, in order");
            Assert.Greater(Count("hisame.lightningCut") + Count("hisame.kocho") + Count("hisame.smokeBomb"), 0, "she used her abilities");

            int last = 0, quiet = 0;
            foreach (var e in f.Log.All.Where(Combat)) { quiet = Mathf.Max(quiet, e.Frame - last); last = e.Frame; }
            Assert.Less(quiet, MaxQuiet, $"{quiet} frames without a hit, block, parry or counter");
        }
    }
}
