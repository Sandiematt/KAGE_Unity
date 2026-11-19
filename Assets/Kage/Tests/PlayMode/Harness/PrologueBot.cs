using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// A scripted Kage for the whole Prologue (SAN-96). While a tutorial prompt shows he does what it asks: walks, jumps, swings the
    /// sword, holds Guard through the practice partner's attack, presses Guard just before its blow lands (the parry), dashes just before
    /// it lands (the perfect dodge). Between prompts he walks right. When the prompts are done or an encounter is on he is the
    /// <see cref="RoadWalker"/> (walks the road, fights with the AI). The timing numbers are leads in the partner's move frames before
    /// its first hit window opens.
    /// </summary>
    public sealed class PrologueBot : IInputSource
    {
        public StretchFight Fight;
        public readonly RoadWalker Walker;

        /// <summary>Move frames before the partner's first hit window at which the parry press and the dodge dash are made.</summary>
        public int ParryLead = 2, DodgeLead = 2;

        /// <summary>When set, replaces what the bot does for the prompt on show (a test that does the wrong thing, or nothing).</summary>
        public System.Func<Fighter, StretchPrompt, FrameInput?> Override;

        bool _armed = true;
        int _guardHold;

        public PrologueBot(float aiLevel) { Walker = new RoadWalker(aiLevel); }

        public void Sample() { Walker.Sample(); }

        public FrameInput Poll(Fighter self)
        {
            var f = Fight;
            var t = f?.Tutorial;
            Walker.Session = f?.Session;
            if (t == null || t.Finished || f.Session.State != StretchState.Walking) return Walker.Poll(self);
            var cur = t.Current;
            if (cur == null) return new FrameInput { dx = 1 };
            if (Override != null)
            {
                var o = Override(self, cur);
                if (o.HasValue) return o.Value;
            }
            var partner = t.Partner;
            bool swinging = partner != null && partner.InAttackMove;
            if (!swinging) _armed = true;
            int frame = swinging ? partner.moveFrame : 0;
            int hitAt = swinging && partner.move.hits.Count > 0 ? partner.move.hits[0].from : int.MaxValue;
            if (_guardHold > 0) { _guardHold--; return new FrameInput { guard = true }; }
            switch (cur.Step)
            {
                case PromptStep.Move: return new FrameInput { dx = 1 };
                case PromptStep.Jump: return new FrameInput { jump = self.Grounded };
                case PromptStep.Attack: return new FrameInput { s = self.state is FState.Idle or FState.Walk or FState.Run };
                case PromptStep.Guard:
                    return swinging ? new FrameInput { guard = true, guardPressed = _armed && (_armed = false) } : default;
                case PromptStep.Parry:
                    if (swinging && _armed && frame >= hitAt - ParryLead)
                    {
                        _armed = false;
                        _guardHold = 6;
                        return new FrameInput { guard = true, guardPressed = true };
                    }
                    return default;
                case PromptStep.Dodge:
                    if (swinging && _armed && frame >= hitAt - DodgeLead)
                    {
                        _armed = false;
                        return new FrameInput { dash = true };
                    }
                    return default;
            }
            return default;
        }
    }
}
