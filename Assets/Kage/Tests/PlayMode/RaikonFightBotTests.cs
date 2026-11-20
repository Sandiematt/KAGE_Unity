using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// A headless bot fight against Raikon (SAN-107, the spec's "a scripted bot beats Raikon on Normal without softlocks, phases trigger at the
    /// right health, time-to-kill is logged"): Kage is a scripted bot with no randomness that follows the JS tips for Raikon, Raikon is his real
    /// AI with real health on both sides, a fixed seed. The fight must end in a knock-out inside a generous cap, never go quiet for long, and open
    /// all three phases on the way. The time-to-kill is logged against the spec's 2 to 3 minutes for Raikon.
    /// </summary>
    public class RaikonFightBotTests
    {
        const int Seed = 107, Cap = 60 * 420, MaxQuiet = 900, ParryLead = 4;

        /// <summary>
        /// The SAN-95 Kage bot (walk in, cycle its buttons) with the JS tips for Raikon, reading only his move, its frame and its windows, the waves
        /// and the hazards:
        /// <list type="bullet">
        /// <item>Thunder Swing ("jump or dash through it"): jump the wave as it reaches him; guard the cut when he is within the blade's reach; in the
        /// last phase the second wave comes late, so he lands and jumps again ("wait for it before you dodge again").</item>
        /// <item>Storm Break: through the wind-up, back-dash out of the slam's reach (it crushes a guard), then guard the shock (it can be blocked);
        /// strike the open recovery.</item>
        /// <item>Skyfall ("move off the red mark before the bolt lands"): once the mark has locked under him, step off it.</item>
        /// <item>Redline Counter ("never attack into it"): hold the blade while the stance is up; hit the spent stance.</item>
        /// <item>His other blows: hold Guard while one is coming when he is within the tachi's reach.</item>
        /// </list>
        /// </summary>
        sealed class StormBot : IInputSource
        {
            readonly ProfileAiTestKit.KageBot _bot = new();
            public HeadlessFight Fight;
            public int Jumps, BackedOff, GuardedShock, SteppedOff, HeldBlade, GuardedCut, Guarded, Punished, Hopped;
            int _tick, _dashAt = -999, _jumpAt = -999, _shockJumpAt = -999;
            public void Sample() { }

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
                if (opp == null || Fight == null) return default;
                var m = opp.state == FState.Attack ? opp.move : null;
                float dist = Mathf.Abs(opp.Pos.x - self.Pos.x);
                int toward = opp.Pos.x > self.Pos.x ? 1 : -1;

                // Skyfall: off the locked mark
                foreach (var h in Fight.Ctx.Hazards)
                {
                    if (h.Def.id != RaikonAbilities.BoltId || h.Owner != opp) continue;
                    float dx = self.Pos.x - h.Centre.x;
                    if (h.Age > h.Def.followFrames + 1 && Mathf.Abs(dx) < h.Def.size.x * 0.5f + 0.5f)
                    {
                        SteppedOff++;
                        return new FrameInput { dx = dx >= 0f ? 1 : -1, dash = Dash(self) };
                    }
                    if (h.Age > h.Def.followFrames + 1) return default;           // clear of it: stand
                }

                // Thunder Swing's wave: jump it as it reaches him
                foreach (var p in Fight.Ctx.Projectiles)
                {
                    if (p.Def.id != RaikonAbilities.WaveId || p.Owner != opp || !p.Alive) continue;
                    float d = Mathf.Abs(p.Position.x - self.Pos.x);
                    bool approaching = Mathf.Sign(self.Pos.x - p.Position.x) == p.Dir;
                    if (approaching && d < 1.7f && d > 0.5f && self.Grounded && !self.Busy && _tick - _jumpAt > 30)
                    {
                        _jumpAt = _tick;
                        Jumps++;
                    }
                    if (_tick - _jumpAt < 22) return new FrameInput { dy = 1 };      // over it: the wave runs along the floor; the jump button held, a full jump
                    if (approaching && d < 4f) return default;                     // wait for it
                }

                if (m != null && m.id == RaikonAbilities.MoveBreak && opp.moveFrame <= m.hits[0].to + 24)
                {
                    bool behind = Mathf.Sign(self.Pos.x - opp.Pos.x) == -Mathf.Sign(opp.facing);
                    if (behind) return _bot.Poll(self);
                    bool wallBehind = Mathf.Abs(self.Pos.x) > 6.9f && Mathf.Sign(self.Pos.x) == -toward;
                    if (dist < 3.2f && !wallBehind && opp.moveFrame < m.hits[0].from - 6)
                    {
                        BackedOff++;
                        return new FrameInput { dx = -toward, dash = Dash(self) };
                    }
                    if (dist > 5.2f) return default;
                    foreach (var h in Fight.Ctx.Hazards)
                    {
                        if (h.Def.id != RaikonAbilities.ShockId || h.Owner != opp) continue;
                        float hx = Mathf.Abs(self.Pos.x - h.Centre.x);
                        if (hx > h.Def.size.x * 0.5f + 0.2f) return default;                                       // out of its reach
                        if (h.Age >= h.Def.telegraphFrames - 10 && h.Age < h.Def.telegraphFrames + 8 && self.Grounded && !self.Busy) _shockJumpAt = _tick;
                    }
                    if (_tick - _shockJumpAt < 20) { Hopped++; return new FrameInput { dy = 1 }; }                // the shock is a floor box: over it
                    GuardedShock++;
                    return new FrameInput { guard = true };
                }
                if (m != null && m.counterTo >= 0 && opp.moveFrame >= m.counterFrom - 2 && opp.moveFrame <= m.counterTo)
                {
                    HeldBlade++;
                    return default;
                }
                if (m != null && m.hits.Count > 0)
                {
                    // any other blow, the cut included: a Guard press just before it lands is a parry (the JS tip: wait for the committed cut, PARRY it), and the
                    // hold that follows blocks it; the bot stands still until then
                    HitWindow next = null;
                    int lastTo = 0;
                    foreach (var w in m.hits)
                    {
                        lastTo = Mathf.Max(lastTo, w.to);
                        if (w.to >= opp.moveFrame && (next == null || w.from < next.from)) next = w;
                    }
                    if (next != null && dist < 3.2f)
                    {
                        if (opp.moveFrame >= next.from - ParryLead)
                        {
                            if (m.id == RaikonAbilities.MoveSwing || m.id == RaikonAbilities.MoveSwing2) GuardedCut++; else Guarded++;
                            return new FrameInput { guard = true };
                        }
                        return default;
                    }
                    // his blow is over and he is still recovering: the opening ("punish the follow-through")
                    if (next == null && opp.moveFrame < m.total - 6)
                    {
                        Punished++;
                        if (dist > 1.3f) return new FrameInput { dx = toward, dash = dist > 2.2f && Dash(self) };
                        return _bot.Poll(self);
                    }
                    return default;
                }
                if (m != null && m.id == RaikonAbilities.MoveSkyfall) return new FrameInput { dx = toward };
                if (opp.state is FState.Hitstun or FState.Down or FState.Launched)
                {
                    Punished++;
                    return dist > 1.3f ? new FrameInput { dx = toward, dash = dist > 2.2f && Dash(self) } : _bot.Poll(self);        // staggered or knocked down: strike
                }
                if (dist > 2.3f && !self.Busy) return new FrameInput { dx = toward };       // close to his reach and wait for him to commit
                return default;
            }
        }

        static bool Combat(CombatEvent e) => e.Type is CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Parried or CombatEventType.Clashed or CombatEventType.Countered;

        [Test]
        public void A_Kage_bot_beats_Raikon_through_all_three_phases_without_a_softlock()
        {
            using var f = new HeadlessFight(Seed, Foe.Ai, -2.5f, 2.5f, foeDef: CharacterCatalog.Get(Raikon.Id));
            var bot = new StormBot { Fight = f };
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
            Debug.Log($"SAN-107 bot fight vs Raikon: {(ended ? (f.Ronin.state == FState.KO ? "Raikon knocked out" : "Kage knocked out") : "NOT ended")} " +
                      $"after {frames} frames = time-to-kill {frames / 60f:0.0} s (spec: 2-3 minutes for Raikon); Kage {f.Kage.health:0.0}/{f.Kage.maxHealth:0}, " +
                      $"Raikon {f.Ronin.health:0.0}/{f.Ronin.maxHealth:0}; phases {f.Events(CombatEventType.BossPhaseChanged).Count()}; " +
                      $"Thunder Swings {Count(RaikonAbilities.MoveSwing)}+{Count(RaikonAbilities.MoveSwing2)} (cut hits {Hits(RaikonAbilities.MoveSwing) + Hits(RaikonAbilities.MoveSwing2)}, wave hits {Hits(RaikonAbilities.WaveId)}), " +
                      $"Storm Breaks {Count(RaikonAbilities.MoveBreak)} (slam hits {Hits(RaikonAbilities.MoveBreak)}, shock hits {Hits(RaikonAbilities.ShockId)}), " +
                      $"Skyfalls {Count(RaikonAbilities.MoveSkyfall)} (bolt hits {Hits(RaikonAbilities.BoltId)}), " +
                      $"Redlines {Count(RaikonAbilities.MoveRedline) + Count(RaikonAbilities.MoveRedlineShort)} (punish hits {Hits(RaikonAbilities.MovePunish)}); " +
                      $"bot jumped {bot.Jumps} waves, backed off {bot.BackedOff}, jumped the shock {bot.Hopped}, guarded the shock {bot.GuardedShock}, stepped off marks {bot.SteppedOff}, held the blade {bot.HeldBlade}, guarded the cut {bot.GuardedCut}, other blows {bot.Guarded} frames, punished {bot.Punished} frames; " +
                      $"{f.Events(CombatEventType.Hit).Count()} hits, {f.Events(CombatEventType.Blocked).Count()} blocks, " +
                      $"{f.Events(CombatEventType.Parried).Count(e => e.Source == f.Ronin)} of his blows parried, {f.Events(CombatEventType.Countered).Count()} counters, " +
                      $"{f.Events(CombatEventType.GuardBroken).Count()} guard-breaks; " +
                      $"Kage landed {f.Events(CombatEventType.Hit).Count(e => e.Source == f.Kage)} for {f.Events(CombatEventType.Hit).Where(e => e.Source == f.Kage).Sum(e => e.Damage):0}; " +
                      "Raikon's hits by move: " +
                      string.Join(", ", f.Events(CombatEventType.Hit).Where(e => e.Source == f.Ronin).GroupBy(e => e.MoveId).Select(g => $"{g.Key} {g.Count()}x{g.Sum(e => e.Damage):0}")));

            Assert.That(frames / 60f, Is.InRange(100f, 240f), "the spec's 2 to 3 minutes for Raikon (this Kage has no gear, so a little over two)");
            Assert.IsTrue(ended, $"no knock-out in {Cap} frames (Kage {f.Kage.health:0.0}, Raikon {f.Ronin.health:0.0})");
            Assert.AreEqual(FState.KO, f.Ronin.state, "the bot beats him");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, f.Events(CombatEventType.BossPhaseChanged).Select(e => e.Index).ToArray(), "all three phases opened, in order");
            Assert.Greater(Count(RaikonAbilities.MoveSwing) + Count(RaikonAbilities.MoveSwing2) + Count(RaikonAbilities.MoveBreak) + Count(RaikonAbilities.MoveSkyfall), 0, "he used his abilities");

            int last = 0, quiet = 0;
            foreach (var e in f.Log.All.Where(Combat)) { quiet = Mathf.Max(quiet, e.Frame - last); last = e.Frame; }
            Assert.Less(quiet, MaxQuiet, $"{quiet} frames without a hit, block, parry or counter");
        }
    }
}
