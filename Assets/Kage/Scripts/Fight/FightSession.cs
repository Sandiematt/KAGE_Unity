using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// How a fight is staged (the JS <c>fightFormat</c>, ADR-0008): how many rounds a side must win and how long a round's clock runs.
    /// <b>Duel</b>: best of three on a clock (the dev arena's 60 s; the JS campaign uses 75 s). <b>Boss</b>: one round, no clock, so no
    /// time-out loss. <b>Stretch</b>: one round, no clock (waves are SAN-89's).
    /// </summary>
    public sealed class FightFormat
    {
        public readonly string Name;
        public readonly int RoundsToWin;
        /// <summary>Seconds a round lasts; 0 = no clock.</summary>
        public readonly float Timer;

        public FightFormat(string name, int roundsToWin, float timer) { Name = name; RoundsToWin = roundsToWin; Timer = timer; }

        public bool HasClock => Timer > 0f;
        public bool HasRounds => RoundsToWin > 1;

        public static readonly FightFormat Duel = new("duel", 2, 60f);
        public static readonly FightFormat Boss = new("boss", 1, 0f);
        public static readonly FightFormat Stretch = new("stretch", 1, 0f);

        /// <summary>The format a fight against this foe uses: boss when the character has phases (<see cref="CharacterDef.bossId"/>), else the duel.</summary>
        public static FightFormat For(CharacterDef foe) => foe != null && foe.bossId != null ? Boss : Duel;
    }

    /// <summary>Why a round ended.</summary>
    public enum RoundEnd { KnockOut, TimeUp, Draw }

    /// <summary>How a round ended: <see cref="Winner"/> is null for a draw.</summary>
    public readonly struct RoundOutcome
    {
        public readonly RoundEnd Reason;
        public readonly Fighter Winner;
        public RoundOutcome(RoundEnd reason, Fighter winner) { Reason = reason; Winner = winner; }
    }

    /// <summary>
    /// The rounds and the clock of one arena fight (SAN-102, ADR-0008; pulled out of the dev arena's round logic, which no ticket owned).
    /// The owner (the dev arena) places the fighters and shows the announcements; the session decides what happens and when:
    /// <list type="bullet">
    /// <item><b>Intro</b> (1.45 s, <see cref="IntroBeat"/> at 0.35 s), then <b>Fight</b> (<see cref="FightBegan"/>: the fighters get control; the
    /// clock runs when the <see cref="FightFormat"/> has one), then <b>RoundOver</b> (the winner takes a bow after 1.3 s; after 3.6 s the next round
    /// or the match end), then <b>MatchOver</b>.</item>
    /// <item>A round ends on a knockout (<see cref="OnKnockOut"/>; with several foes the round goes on while one stands) or when the clock runs
    /// out (the one with more health, as a share of the maximum for a group, wins; equal health is a draw).</item>
    /// <item>The match ends when a side has <see cref="FightFormat.RoundsToWin"/> rounds. With a stage id, <see cref="CombatEventType.StageCleared"/>
    /// (Kage won) or <see cref="CombatEventType.StageFailed"/> is published on the fight's event stream.</item>
    /// </list>
    /// Time is passed in by the owner (<see cref="Update"/>), so the session has no clock of its own and runs the same in play and in tests.
    /// </summary>
    public sealed class FightSession
    {
        public enum Stage { Intro, Fight, RoundOver, MatchOver }

        public const float IntroBeatTime = 0.35f, IntroTime = 1.45f, BowTime = 1.3f, RoundOverTime = 3.6f;

        readonly FightContext _ctx;
        readonly Fighter _player;
        readonly IReadOnlyList<Fighter> _foes;
        readonly string _stageId;
        bool _koHandled, _beatShown;

        public FightFormat Format { get; }
        public Stage State { get; private set; } = Stage.Intro;
        /// <summary>The round being played, from 1.</summary>
        public int Round { get; private set; } = 1;
        /// <summary>Seconds left on the round's clock; 0 when the format has none.</summary>
        public float TimeLeft { get; private set; }
        /// <summary>Seconds since <see cref="State"/> began (the owner passes unscaled time).</summary>
        public float StateTime { get; private set; }
        /// <summary>The last round's winner (null: no round yet, or a draw).</summary>
        public Fighter RoundWinner { get; private set; }
        /// <summary>The match's winner, once <see cref="State"/> is <see cref="Stage.MatchOver"/>.</summary>
        public Fighter MatchWinner { get; private set; }
        public bool Over => State == Stage.MatchOver;
        public bool Group => _foes.Count > 1;
        Fighter Lead => _foes[0];

        /// <summary>A round is about to begin: place the fighters (<see cref="Round"/> is its number).</summary>
        public event Action RoundStarting;
        /// <summary>0.35 s into the intro: the "ROUND n" moment.</summary>
        public event Action IntroBeat;
        /// <summary>The intro is over: the fighters have been given control (<see cref="Fighter.BeginFight"/>).</summary>
        public event Action FightBegan;
        public event Action<RoundOutcome> RoundEnded;
        /// <summary>The match is decided: <see cref="MatchWinner"/> is set.</summary>
        public event Action<Fighter> MatchEnded;

        public FightSession(FightContext ctx, FightFormat format, Fighter player, IReadOnlyList<Fighter> foes, string stageId = null)
        {
            _ctx = ctx; Format = format; _player = player; _foes = foes; _stageId = stageId;
        }

        /// <summary>Starts again from round 1: nobody has a round won.</summary>
        public void Restart()
        {
            _player.roundsWon = 0;
            foreach (var f in _foes) f.roundsWon = 0;
            Round = 1;
            MatchWinner = null;
            StartRound();
        }

        /// <summary>Begins the intro of the current round (<see cref="Round"/> is not changed).</summary>
        public void StartRound()
        {
            _koHandled = false;
            _beatShown = false;
            RoundWinner = null;
            State = Stage.Intro;
            StateTime = 0f;
            TimeLeft = Format.Timer;
            RoundStarting?.Invoke();
        }

        /// <summary>
        /// Advances by one rendered frame: <paramref name="unscaledDt"/> drives the state timers, <paramref name="scaledDt"/> (game time, so hitstop and
        /// slow motion stretch it) the round clock.
        /// </summary>
        public void Update(float unscaledDt, float scaledDt)
        {
            StateTime += unscaledDt;
            switch (State)
            {
                case Stage.Intro:
                    if (!_beatShown && StateTime > IntroBeatTime) { _beatShown = true; IntroBeat?.Invoke(); }
                    if (StateTime > IntroTime)
                    {
                        _player.BeginFight();
                        foreach (var f in _foes) f.BeginFight();
                        State = Stage.Fight;
                        StateTime = 0f;
                        FightBegan?.Invoke();
                    }
                    break;
                case Stage.Fight:
                    if (Format.HasClock)
                    {
                        TimeLeft -= scaledDt;
                        if (TimeLeft <= 0f) TimeUp();
                    }
                    break;
                case Stage.RoundOver:
                    var w = RoundWinner;
                    if (StateTime > BowTime && w != null && w.state != FState.Victory && w.Grounded && !w.Busy) w.Win();
                    if (StateTime > RoundOverTime)
                    {
                        if (_player.roundsWon >= Format.RoundsToWin || Lead.roundsWon >= Format.RoundsToWin) EndMatch();
                        else { Round++; StartRound(); }
                    }
                    break;
            }
        }

        /// <summary>
        /// A fighter was knocked out. The round ends unless Kage is up against a group with a foe still standing. Ignored once the round is decided.
        /// </summary>
        public void OnKnockOut(Fighter loser)
        {
            if (_koHandled || State == Stage.RoundOver || State == Stage.MatchOver) return;
            if (Group && loser != _player) foreach (var f in _foes) if (f.state != FState.KO) return;     // the round goes on while a foe stands
            _koHandled = true;
            var winner = loser == _player ? Lead : _player;
            winner.roundsWon++;
            winner.controlEnabled = false;
            loser.controlEnabled = false;
            foreach (var f in _foes) f.controlEnabled = false;
            if (Group && loser == _player) foreach (var f in _foes) if (f.state != FState.KO) { winner = f; break; }   // a foe still standing takes the bow
            EndRound(new RoundOutcome(RoundEnd.KnockOut, winner));
        }

        void TimeUp()
        {
            _player.controlEnabled = false;
            Lead.controlEnabled = false;
            if (Group)
            {
                // the group's health against Kage's, as shares of the maximum
                float hp = 0f, max = 0f;
                foreach (var f in _foes) { f.controlEnabled = false; hp += f.health; max += f.maxHealth; }
                float kage = _player.health / _player.maxHealth, them = hp / Mathf.Max(1f, max);
                if (Mathf.Abs(kage - them) < 0.005f) { EndRound(new RoundOutcome(RoundEnd.Draw, null)); return; }
                var w = kage > them ? _player : Lead;
                w.roundsWon++;
                EndRound(new RoundOutcome(RoundEnd.TimeUp, w));
                return;
            }
            if (Mathf.Abs(_player.health - Lead.health) < 0.5f) { EndRound(new RoundOutcome(RoundEnd.Draw, null)); return; }
            var winner = _player.health > Lead.health ? _player : Lead;
            winner.roundsWon++;
            EndRound(new RoundOutcome(RoundEnd.TimeUp, winner));
        }

        void EndRound(RoundOutcome o)
        {
            RoundWinner = o.Winner;
            State = Stage.RoundOver;
            StateTime = 0f;
            RoundEnded?.Invoke(o);
        }

        void EndMatch()
        {
            State = Stage.MatchOver;
            StateTime = 0f;
            MatchWinner = _player.roundsWon >= Format.RoundsToWin ? _player : (Fighter)Lead;
            if (_stageId != null)
            {
                int frame = _ctx.Clock.Frame;
                _ctx.Events.Publish(MatchWinner == _player ? CombatEvent.StageCleared(frame, _stageId) : CombatEvent.StageFailed(frame, _stageId));
            }
            MatchEnded?.Invoke(MatchWinner);
        }
    }
}
