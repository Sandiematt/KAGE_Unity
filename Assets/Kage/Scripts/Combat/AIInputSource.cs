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
    /// </summary>
    public class AIInputSource : IInputSource
    {
        public float level = 0.6f;     // 0 easy … 1 hard
        public bool passive;           // training: just stands and guards sometimes
        /// <summary>How often a chosen attack is actually thrown (<see cref="CharacterDef.attackRate"/>): 1 is the profile as is, 0.3 holds back seven times in ten.</summary>
        public float attackRate = 1f;

        /// <summary>Takes the character's AI knobs (ADR-0005): <see cref="CharacterDef.attackRate"/>. Called by <see cref="FightBuilder.Spawn(CharacterDef,int,IInputSource)"/>.</summary>
        public void Configure(CharacterDef def) { if (def != null) attackRate = def.attackRate; }

        struct Step { public int frames; public int fwd, dy; public Btn btn; public bool tap; public int tapDir; }
        readonly Queue<Step> _plan = new();
        Step _cur;
        int _curLeft, _think, _guardFrames;
        bool _guardLow;
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

            // knocked down: mash for a quick kip-up
            if (self.state == FState.Down) { if (R() < 0.08f * level) o.p = true; return o; }
            if (self.state is FState.Hitstun or FState.Launched or FState.Thrown or FState.KO) { _plan.Clear(); _curLeft = 0; return o; }

            // react to the opponent's technique: guard high/low
            if (!passive && _guardFrames <= 0 && opp.IsAttacking && opp.move != null && !self.IsAttacking && self.Grounded)
            {
                var m = opp.move;
                int firstHit = 999; HitLevel lvl = HitLevel.Mid;
                foreach (var w in m.hits) if (w.from >= opp.moveFrame && w.from < firstHit) { firstHit = w.from; lvl = w.level; }
                float reach = m.swordFront ? 1.6f : 1.15f;
                if (m.projectileFrame > 0) reach = 9f;
                if (firstHit < 999 && dist < reach && R() < Mathf.Lerp(0.18f, 0.75f, level))
                {
                    _guardFrames = (firstHit - opp.moveFrame) + 10;
                    _guardLow = lvl == HitLevel.Low;
                    _plan.Clear(); _curLeft = 0;
                    if (lvl == HitLevel.Unblockable && R() < level) { _guardFrames = 0; Enqueue(1, 0, 0, Btn.None, tap: true, tapDir: -1); }
                }
            }
            if (_guardFrames > 0)
            {
                _guardFrames--;
                o.guard = true;                 // the same Guard input the player has; Down makes it a low guard
                o.dy = _guardLow ? -1 : 0;
                if (_guardFrames == 0 && self.state == FState.Blockstun) Punish(dist);
                return o;
            }
            if (passive) return o;

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
            if (self.IsAttacking && self.moveHit && self.move.chain.Count > 0 && self.moveFrame >= self.move.cancelFrom - 3 && R() < Mathf.Lerp(0.25f, 0.85f, level) * attackRate)
            {
                var keys = new List<Btn>(self.move.chain.Keys);
                Btn b = keys[N(keys.Count)];
                Press(ref o, b);
                return o;
            }

            if (_curLeft <= 0)
            {
                if (_plan.Count > 0) { _cur = _plan.Dequeue(); _curLeft = _cur.frames; }
                else if (--_think <= 0 && !self.Busy) { Decide(self, opp, dist); Restrain(dist); _think = Mathf.RoundToInt(Mathf.Lerp(16f, 5f, level)) + N(6); }
            }
            if (_curLeft > 0)
            {
                bool first = _curLeft == _cur.frames;
                _curLeft--;
                o.dx = (int)(_cur.fwd * face);
                o.dy = _cur.dy;
                if (first && _cur.btn != Btn.None) Press(ref o, _cur.btn);
                if (first && _cur.tap)
                {
                    bool right = _cur.tapDir * face > 0;
                    if (right) o.tapRight2 = true; else o.tapLeft2 = true;
                    o.dx = 0;
                }
            }
            return o;
        }

        static void Press(ref FrameInput o, Btn b)
        {
            switch (b) { case Btn.P: o.p = true; break; case Btn.K: o.k = true; break; case Btn.S: o.s = true; break; case Btn.X: o.x = true; break; }
        }

        void Enqueue(int frames, int fwd, int dy, Btn b, bool tap = false, int tapDir = 0)
        {
            _plan.Enqueue(new Step { frames = frames, fwd = fwd, dy = dy, btn = b, tap = tap, tapDir = tapDir });
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
            if (R() > level * attackRate) return;
            if (dist < 0.85f) Enqueue(2, 0, 0, Btn.P);
            else if (dist < 1.3f) Enqueue(2, 0, 0, Btn.S);
        }

        void Decide(Fighter self, Fighter opp, float dist)
        {
            float r = R();
            bool oppDown = opp.state == FState.Down;
            bool cornered = self.AtWall && Mathf.Sign(opp.Pos.x - self.Pos.x) != Mathf.Sign(-self.Pos.x);
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
