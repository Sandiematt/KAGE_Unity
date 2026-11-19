using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// <see cref="FightSession"/> (SAN-102, ADR-0008): the rounds and the clock of an arena fight. A boss fight is one round with no clock, a
    /// duel is best of three on a clock, and a stage's end publishes StageCleared or StageFailed. The session is fed time by the test, so it is
    /// exact; the fighters are real (the harness) so a knockout is a real knockout.
    /// </summary>
    public class BossPhaseSessionTests
    {
        const float Dt = 1f / 60f;
        readonly List<HeadlessFight> _fights = new();
        readonly List<GroupFight> _groups = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            foreach (var g in _groups) g.Dispose();
            _fights.Clear();
            _groups.Clear();
        }

        sealed class Rig
        {
            public HeadlessFight F;
            public FightSession S;
            public readonly List<string> Log = new();
            public int Starting, Began;
            public RoundOutcome? Last;

            public void Advance(float seconds) { for (float t = 0; t < seconds; t += Dt) S.Update(Dt, Dt); }
            public void Knock(Fighter loser)
            {
                var hit = new HitInfo
                {
                    attacker = loser == F.Kage ? F.Ronin : F.Kage,
                    w = new HitWindow { dmg = 9999, level = HitLevel.Mid, kb = new Vector2(1f, 0f), stun = 16, heavy = true },
                    dmg = 9999, kb = new Vector2(1f, 0f), zone = HitZone.Body, contact = loser.Pos + Vector2.up, moveId = "test.hit", window = -1,
                };
                loser.ReceiveHit(hit);
            }
        }

        Rig Make(FightFormat format, string stageId = null, string foeId = "order_footman")
        {
            var r = new Rig { F = new HeadlessFight(61, Foe.Scripted, -0.9f, 0.9f, foeDef: CharacterCatalog.Get(foeId)) };
            _fights.Add(r.F);
            r.F.RoninInput.Add(100000);
            r.F.KageInput.Add(100000);
            r.S = new FightSession(r.F.Ctx, format, r.F.Kage, new[] { r.F.Ronin }, stageId);
            r.F.Kage.onKO += r.S.OnKnockOut;
            r.F.Ronin.onKO += r.S.OnKnockOut;
            r.S.RoundStarting += () => r.Starting++;
            r.S.FightBegan += () => r.Began++;
            r.S.RoundEnded += o => r.Last = o;
            r.S.Restart();
            return r;
        }

        static int Events(Rig r, CombatEventType t) => r.F.Events(t).Count();

        // ---- a boss fight: one round, no clock ----

        [Test]
        public void A_boss_fight_has_one_round_and_no_clock()
        {
            var r = Make(FightFormat.Boss);
            Assert.AreEqual(FightSession.Stage.Intro, r.S.State);
            r.Advance(FightSession.IntroTime + 0.1f);
            Assert.AreEqual(FightSession.Stage.Fight, r.S.State);
            Assert.AreEqual(1, r.Began);
            Assert.AreEqual(0f, r.S.TimeLeft);
            r.Advance(600f);        // ten minutes
            Assert.AreEqual(FightSession.Stage.Fight, r.S.State, "no time-out loss in a boss fight");
            Assert.AreEqual(1, r.S.Round);
            Assert.AreEqual(0f, r.S.TimeLeft);
        }

        [Test]
        public void Knocking_out_the_boss_wins_the_match_after_one_round()
        {
            var r = Make(FightFormat.Boss, "test.stage");
            r.Advance(2f);
            r.Knock(r.F.Ronin);
            Assert.AreEqual(FightSession.Stage.RoundOver, r.S.State);
            Assert.AreEqual(RoundEnd.KnockOut, r.Last.Value.Reason);
            Assert.AreSame(r.F.Kage, r.Last.Value.Winner);
            r.Advance(FightSession.RoundOverTime + 0.1f);
            Assert.AreEqual(FightSession.Stage.MatchOver, r.S.State);
            Assert.IsTrue(r.S.Over);
            Assert.AreSame(r.F.Kage, r.S.MatchWinner);
            Assert.AreEqual(1, r.S.Round, "no second round");
            Assert.AreEqual(1, r.Starting, "and none started");
            Assert.AreEqual(1, r.F.Kage.roundsWon);
        }

        [Test]
        public void A_cleared_stage_publishes_StageCleared_once()
        {
            var r = Make(FightFormat.Boss, "act1.boss");
            r.Advance(2f);
            r.Knock(r.F.Ronin);
            r.Advance(10f);
            var cleared = r.F.Events(CombatEventType.StageCleared).ToList();
            Assert.AreEqual(1, cleared.Count);
            Assert.AreEqual("act1.boss", cleared[0].Name);
            Assert.AreEqual(0, Events(r, CombatEventType.StageFailed));
        }

        [Test]
        public void Losing_the_boss_fight_publishes_StageFailed()
        {
            var r = Make(FightFormat.Boss, "act1.boss");
            r.Advance(2f);
            r.Knock(r.F.Kage);
            r.Advance(10f);
            Assert.AreSame(r.F.Ronin, r.S.MatchWinner);
            Assert.AreEqual(1, Events(r, CombatEventType.StageFailed));
            Assert.AreEqual(0, Events(r, CombatEventType.StageCleared));
            Assert.AreEqual("act1.boss", r.F.Events(CombatEventType.StageFailed).First().Name);
        }

        [Test]
        public void A_fight_with_no_stage_id_publishes_no_stage_event()
        {
            var r = Make(FightFormat.Boss, null);
            r.Advance(2f);
            r.Knock(r.F.Ronin);
            r.Advance(10f);
            Assert.AreEqual(0, Events(r, CombatEventType.StageCleared) + Events(r, CombatEventType.StageFailed), "the dev arena has no stage");
        }

        // ---- a duel: best of three on a clock, as the dev arena always was ----

        [Test]
        public void A_duel_runs_a_60_second_clock_and_starts_a_second_round_after_the_first()
        {
            var r = Make(FightFormat.Duel);
            Assert.AreEqual(60f, r.S.TimeLeft);
            r.Advance(FightSession.IntroTime + 0.1f);
            Assert.AreEqual(FightSession.Stage.Fight, r.S.State);
            r.Advance(10f);
            Assert.AreEqual(50f, r.S.TimeLeft, 0.2f, "the clock runs in the fight");
            r.Knock(r.F.Ronin);
            Assert.AreEqual(1, r.F.Kage.roundsWon);
            r.Advance(FightSession.RoundOverTime + 0.1f);
            Assert.AreEqual(FightSession.Stage.Intro, r.S.State, "not over: Kage needs two rounds");
            Assert.AreEqual(2, r.S.Round);
            Assert.AreEqual(2, r.Starting, "the arena is asked to place the fighters again");
            Assert.AreEqual(60f, r.S.TimeLeft);
        }

        [Test]
        public void The_clock_runs_out_and_the_fighter_with_more_health_takes_the_round()
        {
            var r = Make(FightFormat.Duel);
            r.F.Ronin.health = r.F.Ronin.maxHealth * 0.5f;
            r.Advance(FightSession.IntroTime + 0.1f);
            r.Advance(60.5f);
            Assert.AreEqual(FightSession.Stage.RoundOver, r.S.State);
            Assert.AreEqual(RoundEnd.TimeUp, r.Last.Value.Reason);
            Assert.AreSame(r.F.Kage, r.Last.Value.Winner);
            Assert.AreEqual(1, r.F.Kage.roundsWon);
        }

        [Test]
        public void Equal_health_at_time_up_is_a_draw_with_no_round_won()
        {
            var r = Make(FightFormat.Duel);
            r.F.Kage.maxHealth = r.F.Kage.health = 100f;
            r.F.Ronin.maxHealth = r.F.Ronin.health = 100f;
            r.Advance(FightSession.IntroTime + 0.1f);
            r.Advance(60.5f);
            Assert.AreEqual(RoundEnd.Draw, r.Last.Value.Reason);
            Assert.IsNull(r.Last.Value.Winner);
            Assert.AreEqual(0, r.F.Kage.roundsWon + r.F.Ronin.roundsWon);
            r.Advance(FightSession.RoundOverTime + 0.1f);
            Assert.AreEqual(2, r.S.Round, "the next round follows");
        }

        [Test]
        public void Two_rounds_win_a_duel()
        {
            var r = Make(FightFormat.Duel);
            r.Advance(2f);
            r.Knock(r.F.Ronin);
            r.Advance(FightSession.RoundOverTime + 0.1f);
            Assert.AreEqual(2, r.S.Round);
            r.F.Ronin.ResetForRound(0.9f, -1f);       // what the arena's RoundStarting handler does
            r.Advance(2f);
            r.Knock(r.F.Ronin);
            r.Advance(FightSession.RoundOverTime + 0.1f);
            Assert.IsTrue(r.S.Over);
            Assert.AreSame(r.F.Kage, r.S.MatchWinner);
            Assert.AreEqual(2, r.F.Kage.roundsWon);
        }

        [Test]
        public void Restart_clears_the_rounds_and_begins_round_one_again()
        {
            var r = Make(FightFormat.Duel);
            r.Advance(2f);
            r.Knock(r.F.Ronin);
            r.Advance(FightSession.RoundOverTime + 0.1f);
            r.S.Restart();
            Assert.AreEqual(1, r.S.Round);
            Assert.AreEqual(0, r.F.Kage.roundsWon);
            Assert.AreEqual(FightSession.Stage.Intro, r.S.State);
            Assert.IsNull(r.S.MatchWinner);
        }

        [Test]
        public void The_intro_beat_comes_at_a_third_of_a_second()
        {
            var r = Make(FightFormat.Duel);
            int beats = 0;
            r.S.IntroBeat += () => beats++;
            r.Advance(0.3f);
            Assert.AreEqual(0, beats);
            r.Advance(0.1f);
            Assert.AreEqual(1, beats);
            r.Advance(2f);
            Assert.AreEqual(1, beats, "once per round");
        }

        // ---- a group: the round goes on while a foe stands ----

        [Test]
        public void With_a_group_the_round_ends_only_when_every_foe_is_down()
        {
            var g = new GroupFight(61, 0f, new[] { 1.3f, -2.4f }, foesStill: true);
            _groups.Add(g);
            var s = new FightSession(g.Ctx, FightFormat.Duel, g.Kage, g.Foes);
            g.Kage.onKO += s.OnKnockOut;
            foreach (var f in g.Foes) f.onKO += s.OnKnockOut;
            RoundOutcome? last = null;
            s.RoundEnded += o => last = o;
            s.Restart();
            for (float t = 0; t < 2f; t += Dt) s.Update(Dt, Dt);

            HitInfo Lethal(Fighter victim) => new()
            {
                attacker = g.Kage, w = new HitWindow { dmg = 9999, level = HitLevel.Mid, kb = new Vector2(1f, 0f), stun = 16, heavy = true },
                dmg = 9999, kb = new Vector2(1f, 0f), zone = HitZone.Body, contact = victim.Pos + Vector2.up, moveId = "test.hit", window = -1,
            };
            g.Foes[0].ReceiveHit(Lethal(g.Foes[0]));
            Assert.AreEqual(FightSession.Stage.Fight, s.State, "one foe is still standing");
            Assert.IsNull(last);
            g.Foes[1].ReceiveHit(Lethal(g.Foes[1]));
            Assert.AreEqual(FightSession.Stage.RoundOver, s.State);
            Assert.AreSame(g.Kage, last.Value.Winner);
            Assert.AreEqual(1, g.Kage.roundsWon);
        }
    }
}
