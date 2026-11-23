using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Opponent brain that drives a fighter through the same input interface as the player.
    /// Utility-style choice by range every few frames: approach, poke, combo, throw, sweep,
    /// jump-in, roll, retreat; reads the player's active move to guard high/low with the Guard input (ADR-0004: walking
    /// backward doesn't guard); punishes after
    /// blocking; extends combos on hit; mashes to kip-up.
    /// In a group fight a foe the engagement manager hasn't engaged waits its turn instead (SAN-83, <see cref="WaitTurn"/>):
    /// it keeps <see cref="EngagementTuning.waitDistance"/> from its target and circles, guards as usual, and never attacks.
    /// </summary>
    public partial class AIInputSource : IInputSource
    {
        public float level = 0.6f;     // 0 easy … 1 hard: the stage's difficulty; the profile's own numbers at the default 0.6
        public bool passive;           // training: just stands and guards sometimes
        /// <summary>How often a chosen attack is actually thrown (<see cref="CharacterDef.attackRate"/>): 1 is the profile as is, 0.3 holds back seven times in ten.</summary>
        public float attackRate = 1f;

        /// <summary>
        /// Takes the character's AI knobs (ADR-0005): its <see cref="CharacterDef.aiProfile"/> (<see cref="AiProfiles"/>), <see cref="CharacterDef.attackRate"/>
        /// and the parry rate (the character's own when it sets one, else the profile's). Called by <see cref="FightBuilder.Spawn(CharacterDef,int,IInputSource)"/>.
        /// </summary>
        public void Configure(CharacterDef def)
        {
            if (def == null) return;
            profile = AiProfiles.Get(def.aiProfile);
            attackRate = def.attackRate;
            parryRate = def.parryRate > 0f ? def.parryRate : (profile != null ? profile.parryRate : 0f);
        }

        /// <summary>
        /// The share of the attacks this AI guards that it tries to parry (<see cref="CharacterDef.parryRate"/>, SAN-91): 0 never, 1 every one it guards.
        /// With a <see cref="profile"/> (SAN-95) it is the chance per attack, the JS <c>parry</c> number, and it comes from the profile unless the character sets its own.
        /// </summary>
        public float parryRate;
        /// <summary>True while the guard being held is a parry attempt. A foe's parry only happens then (see Fighter.Parry.cs), so a plain guard that goes up by chance inside the window is only a block.</summary>
        public bool ParryAttempt { get; private set; }
        bool _parryTry;
        int _parryWait;

        struct Step { public int frames; public int fwd, dy; public Btn btn; public bool tap; public int tapDir; public int ability; public bool dash; }
        readonly Queue<Step> _plan = new();
        Step _cur;
        int _curLeft, _think, _guardFrames;
        bool _guardLow, _waited;
        FightRandom _rng;

        /// <summary>Every AI choice rolls on the fight's "ai/&lt;FighterId&gt;" stream (ADR-0002).</summary>
        float R() => _rng.Value();
        int N(int maxExclusive) => _rng.Range(0, maxExclusive);

        public void Sample() { }

        public FrameInput Poll(Fighter self)
        {
            _rng ??= self.ctx != null ? self.ctx.Random.Stream("ai/" + self.FighterId) : new FightRandom(1234);
            var opp = self.opponent;
            var o = new FrameInput();
            if (opp == null) return o;
            float face = self.facing;
            float dist = Mathf.Abs(opp.Pos.x - self.Pos.x);
            bool waiting = self.ctx != null && !self.ctx.Engagement.MayAttack(self);   // not engaged: no attacks (SAN-83)

            // knocked down: mash for a quick kip-up (a waiting foe gets up in its own time: a mashed button would attack)
            if (self.state == FState.Down) { if (!waiting && R() < 0.08f * level) o.p = true; return o; }
            if (self.state is FState.Hitstun or FState.Launched or FState.Thrown or FState.KO) { _plan.Clear(); _curLeft = 0; _parryTry = ParryAttempt = false; return o; }

            // react to the opponent's technique: guard high/low (a profile answers once per attack on its own rates, AIInputSource.Profile.cs)
            if (Profiled) { if (!passive) ReactProfiled(self, opp, dist); }
            else if (!passive && _guardFrames <= 0 && opp.IsAttacking && opp.move != null && !self.IsAttacking && self.Grounded)
            {
                var m = opp.move;
                int firstHit = 999; HitLevel lvl = HitLevel.Mid;
                foreach (var w in m.hits) if (w.from >= opp.moveFrame && w.from < firstHit) { firstHit = w.from; lvl = w.level; }
                float reach = m.swordFront ? 1.6f : 1.15f;
                if (m.projectileFrame > 0) reach = 9f;
                if (firstHit < 999 && dist < reach && R() < Mathf.Lerp(0.18f, 0.75f, level))
                {
                    int ahead = firstHit - opp.moveFrame;
                    _guardFrames = ahead + 10;
                    _guardLow = lvl == HitLevel.Low;
                    // a parry attempt holds the press back until the blow is half a window away (SAN-91); no extra roll when the rate is 0
                    _parryTry = parryRate > 0f && lvl != HitLevel.Unblockable && R() < parryRate;
                    _parryWait = _parryTry ? Mathf.Max(0, ahead - Tuning.Get<ParryTuning>().windowFrames / 2) : 0;
                    _plan.Clear(); _curLeft = 0;
                    if (lvl == HitLevel.Unblockable && R() < level) { _guardFrames = 0; Enqueue(1, 0, 0, Btn.None, tap: true, tapDir: -1); }
                }
            }
            if (_guardFrames > 0)
            {
                _guardFrames--;
                o.guard = _parryWait <= 0;      // the same Guard input the player has; Down makes it a low guard
                if (_parryWait > 0) _parryWait--;
                o.dy = _guardLow ? -1 : 0;
                ParryAttempt = _parryTry;
                if (_guardFrames == 0) { _parryTry = false; ParryAttempt = false; }
                if (_guardFrames == 0 && self.state == FState.Blockstun && !waiting) Punish(dist);
                return o;
            }
            if (passive) return o;
            if (waiting) return WaitTurn(self, opp, dist, o);
            if (_waited) { _waited = false; _plan.Clear(); _curLeft = 0; _think = 0; }   // just engaged: decide afresh

            // chase a launch: jump-cancel the launcher and follow with an air attack
            if (self.IsAttacking && self.moveHit && self.move.jumpCancelFrom >= 0 && self.moveFrame >= self.move.jumpCancelFrom - 2 && _plan.Count == 0)
            {
                if (R() < Mathf.Lerp(0.3f, 0.9f, level))
                {
                    o.dy = 1;
                    o.dx = (int)face;
                    Enqueue(6 + N(4), 1, 0, Btn.None);
                    Enqueue(1, 0, 0, R() < 0.5f ? Btn.K : Btn.S);
                    return o;
                }
            }

            // continue combos on confirmed hits
            if (self.IsAttacking && self.moveHit && self.move.chain.Count > 0 && self.moveFrame >= self.move.cancelFrom - 3 && ChainRoll())
            {
                var keys = new List<Btn>(self.move.chain.Keys);
                Btn b = keys[N(keys.Count)];
                Press(ref o, b);
                return o;
            }

            if (_curLeft <= 0)
            {
                if (_plan.Count > 0) { _cur = _plan.Dequeue(); _curLeft = _cur.frames; }
                else if (Profiled && !self.Busy && PunishOpening(self, opp, dist)) { }
                else if (--_think <= 0 && !self.Busy)
                {
                    if (Profiled) DecideProfiled(self, opp, dist); else Decide(self, opp, dist);
                    Restrain(dist);
                    _think = Mathf.RoundToInt(Mathf.Lerp(16f, 5f, level) * ThinkMul) + N(6);
                }
            }
            if (_curLeft > 0)
            {
                bool first = _curLeft == _cur.frames;
                _curLeft--;
                o.dx = (int)(_cur.fwd * face);
                o.dy = _cur.dy;
                if (first && _cur.btn != Btn.None) Press(ref o, _cur.btn);
                if (first && _cur.ability > 0) o.ability = _cur.ability;      // an ability slot is pressed like a button (SAN-95)
                if (first && _cur.dash) o.dash = true;
                if (first && _cur.tap)
                {
                    bool right = _cur.tapDir * face > 0;
                    if (right) o.tapRight2 = true; else o.tapLeft2 = true;
                    o.dx = 0;
                }
            }
            return o;
        }

        /// <summary>
        /// A waiting foe's turn (the JS AIController.waitTurn): too near its target, it backs away; too far, it walks in;
        /// at its distance it circles (a walking step in or out, <see cref="EngagementTuning.circleChance"/>) or holds.
        /// Movement only: it never presses an attack button.
        /// </summary>
        FrameInput WaitTurn(Fighter self, Fighter opp, float dist, FrameInput o)
        {
            if (!_waited) { _waited = true; _plan.Clear(); _curLeft = 0; }
            if (_curLeft <= 0)
            {
                if (_plan.Count > 0) { _cur = _plan.Dequeue(); _curLeft = _cur.frames; }
                else if (!self.Busy) PlanWait(self, opp, dist);
            }
            if (_curLeft > 0)
            {
                _curLeft--;
                o.dx = (int)(_cur.fwd * self.facing);
            }
            return o;
        }

        void PlanWait(Fighter self, Fighter opp, float dist)
        {
            var t = Tuning.Get<EngagementTuning>();
            bool wallBehind = WallBehind(self, opp);
            if (dist < t.waitDistance - t.retreatInside && !wallBehind) Enqueue(t.stepFrames, -1, 0, Btn.None);
            else if (dist > t.waitDistance + t.approachBeyond) Enqueue(t.stepFrames, 1, 0, Btn.None);
            else if (R() < t.circleChance)
            {
                int dir = R() < 0.5f ? 1 : -1;
                if (dir < 0 && wallBehind) dir = 1;
                Enqueue(_rng.Range(t.circleStepMin, t.circleStepMax + 1), dir, 0, Btn.None);
            }
            else Enqueue(_rng.Range(t.holdMin, t.holdMax + 1), 0, 0, Btn.None);
        }

        /// <summary>
        /// True when <paramref name="self"/> stands at a wall on the side away from <paramref name="opp"/>, so a step back walks into it.
        /// The wall comes from the fight's bounds (<see cref="Fighter.WallSide"/>), not the sign of x, which only said it in the dev arena (SAN-116).
        /// </summary>
        public static bool WallBehind(Fighter self, Fighter opp)
        {
            int wall = self.WallSide;
            return wall != 0 && wall == -Mathf.Sign(opp.Pos.x - self.Pos.x);
        }

        /// <summary>
        /// The "cornered" test that holds back a backflip dodge: at a wall, with <paramref name="opp"/> not on the side the stage opens to.
        /// The wall comes from the fight's bounds (<see cref="Fighter.WallSide"/>), as for <see cref="WallBehind"/> (SAN-116).
        /// </summary>
        public static bool Cornered(Fighter self, Fighter opp)
        {
            int wall = self.WallSide;
            return wall != 0 && Mathf.Sign(opp.Pos.x - self.Pos.x) != -wall;
        }

        static void Press(ref FrameInput o, Btn b)
        {
            switch (b) { case Btn.P: o.p = true; break; case Btn.K: o.k = true; break; case Btn.S: o.s = true; break; case Btn.X: o.x = true; break; }
        }

        void Enqueue(int frames, int fwd, int dy, Btn b, bool tap = false, int tapDir = 0, int ability = 0, bool dash = false)
        {
            _plan.Enqueue(new Step { frames = frames, fwd = fwd, dy = dy, btn = b, tap = tap, tapDir = tapDir, ability = ability, dash = dash });
        }

        /// <summary>
        /// A character with an attack rate under 1 throws only that share of the attacks <see cref="Decide"/> picked: the rest
        /// of the plan becomes a short approach (when far) or a pause.
        /// </summary>
        void Restrain(float dist)
        {
            if (attackRate >= 1f || _plan.Count == 0) return;
            bool attacks = false;
            foreach (var st in _plan) if (st.btn != Btn.None) { attacks = true; break; }
            if (!attacks || R() < attackRate) return;
            _plan.Clear();
            Enqueue(dist > 1.25f ? 10 + N(12) : 30 + N(30), dist > 1.25f ? 1 : 0, 0, Btn.None);
        }

        void Punish(float dist)
        {
            if (R() > PunishChance()) return;
            QuickAttack(dist);
        }

        void Decide(Fighter self, Fighter opp, float dist)
        {
            float r = R();
            bool oppDown = opp.state == FState.Down;
            bool cornered = Cornered(self, opp);
            if (oppDown)
            {
                if (dist < 1.0f && r < 0.5f) { Enqueue(2, 0, -1, Btn.P); return; }
                Enqueue(18 + N(12), dist > 1.3f ? 1 : -1, 0, Btn.None);
                return;
            }
            if (self.meter >= 35f && dist > 2.0f && r < 0.25f * level + 0.05f && self.moves.Select(Btn.X, 0, 0, false, self)?.projectileFrame >= 0) { Enqueue(2, 0, 0, Btn.X); return; }     // the ranged Special (a family whose Special is a melee move, like the fists, never throws it at range)
            if (dist > 2.2f)
            {
                if (r < 0.62f) Enqueue(14 + N(16), 1, 0, Btn.None);
                else if (r < 0.75f) { Enqueue(1, 1, 1, Btn.None); Enqueue(14, 1, 0, Btn.None); Enqueue(2, 0, 0, Btn.K); }
                else if (r < 0.85f) Enqueue(1, 0, 0, Btn.None, tap: true, tapDir: 1);
                else Enqueue(18, 0, 0, Btn.None);
                return;
            }
            if (dist > 1.25f)
            {
                if (r < 0.35f) Enqueue(10 + N(10), 1, 0, Btn.None);
                else if (r < 0.5f) Enqueue(2, 1, 0, Btn.S);                       // shadow step
                else if (r < 0.62f) { Enqueue(1, 1, 1, Btn.None); Enqueue(12, 1, 0, Btn.None); Enqueue(2, 0, 0, Btn.K); }
                else if (r < 0.72f) Enqueue(1, 0, 0, Btn.None, tap: true, tapDir: 1);
                else if (r < 0.82f) Enqueue(12, -1, 0, Btn.None);
                else Enqueue(14, 0, 0, Btn.None);
                return;
            }
            if (dist > 0.82f)
            {
                if (r < 0.2f) Enqueue(2, 0, 0, Btn.S);
                else if (r < 0.38f) Enqueue(2, 0, 0, Btn.K);
                else if (r < 0.52f) Enqueue(2, 0, -1, Btn.K);
                else if (r < 0.62f) Enqueue(2, 1, 0, Btn.K);
                else if (r < 0.7f) Enqueue(2, 0, -1, Btn.S);
                else if (r < 0.8f) Enqueue(8, 1, 0, Btn.None);
                else if (r < 0.88f && !cornered) Enqueue(1, 0, 0, Btn.None, tap: true, tapDir: -1);
                else Enqueue(10, -1, 0, Btn.None);
                return;
            }
            if (r < 0.3f) Enqueue(2, 0, 0, Btn.P);
            else if (r < 0.48f) Enqueue(2, 1, 0, Btn.P);                          // throw
            else if (r < 0.62f) Enqueue(2, 0, -1, Btn.K);
            else if (r < 0.74f) Enqueue(2, 0, 0, Btn.K);
            else if (r < 0.82f) Enqueue(2, 0, -1, Btn.P);
            else if (r < 0.9f && !cornered) Enqueue(1, 0, 0, Btn.None, tap: true, tapDir: -1);
            else Enqueue(12, -1, 0, Btn.None);
        }
    }
}
