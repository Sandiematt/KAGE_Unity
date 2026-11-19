using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Multiple targets (SAN-83, ADR-0002): the target list, the "nearest in front" rule that picks
    /// <see cref="opponent"/> (the current target) from it every frame, and what the engagement manager reads.
    /// </summary>
    public partial class Fighter
    {
        /// <summary>
        /// Every fighter this one can fight: the active fighters of other teams, in fighter (tick) order. Refreshed every
        /// frame by <see cref="PickTarget"/>. With one foe it has one entry, the opponent.
        /// </summary>
        public readonly List<Fighter> Targets = new();

        /// <summary>
        /// Fight loop, first thing in a frame (before the engagement manager and input): refreshes <see cref="Targets"/> and
        /// picks <see cref="opponent"/> by the nearest-in-front rule (<see cref="EngagementTuning.behindPenalty"/>):
        /// <list type="bullet">
        /// <item>Fighters still standing (not knocked out) come first; only when every one is out is a knocked-out one the target,
        /// so with one foe the target is always that foe, as before.</item>
        /// <item>Of those, the lowest horizontal distance wins, where a fighter behind (against <see cref="facing"/>) counts
        /// <c>behindPenalty</c> farther and the current target <c>stickiness</c> nearer. Ties: fighter order.</item>
        /// </list>
        /// Move selection, the AI and facing all read <see cref="opponent"/>, so a fighter turns (in neutral) to a new target
        /// behind it. The target is held while a throw or the rage art holds it. No target leaves <see cref="opponent"/> null.
        /// </summary>
        public void PickTarget()
        {
            if (ctx == null) return;
            Targets.Clear();
            foreach (var o in ctx.Fighters)
                if (o != null && o != this && o.team != team && o.isActiveAndEnabled && o.def != null) Targets.Add(o);

            bool held = state is FState.Ultimate or FState.Thrown || _victim != null;
            if (held && opponent != null && Targets.Contains(opponent)) return;
            if (Targets.Count <= 1) { opponent = Targets.Count == 1 ? Targets[0] : null; return; }

            var t = Tuning.Get<EngagementTuning>();
            bool anyStanding = false;
            foreach (var o in Targets) if (o.state != FState.KO) { anyStanding = true; break; }
            Fighter best = null;
            float bestScore = float.MaxValue;
            Vector2 p = Pos;
            foreach (var o in Targets)
            {
                if (anyStanding && o.state == FState.KO) continue;
                float dx = o.Pos.x - p.x;
                float score = Mathf.Abs(dx);
                if (dx * facing < 0f) score += t.behindPenalty;
                if (o == opponent) score -= t.stickiness;
                if (score < bestScore) { bestScore = score; best = o; }
            }
            opponent = best;
        }

        /// <summary>
        /// In an attack move: a move with a hit window, a throw, a projectile or a counter window (not a roll, a backflip,
        /// a dash or getting up). This is what "attacking" means for the engagement rule's limit (SAN-83).
        /// </summary>
        public bool InAttackMove => state == FState.Attack && move != null &&
                                    (move.hits.Count > 0 || move.isThrow || move.projectileFrame >= 0 || move.counterFrom >= 0);

        /// <summary>
        /// Committed to an attack: in an attack move, in the rage art, or with an attack button still buffered (it may start
        /// a move in the next frames). An engaged foe keeps its place while committed.
        /// </summary>
        public bool CommittedToAttack => InAttackMove || state == FState.Ultimate || (_buffer != Btn.None && _bufferAge <= 10);

        /// <summary>
        /// Input phase: a foe the engagement manager hasn't engaged can't attack, so its attack buttons are dropped
        /// (everything else, the Guard input included, goes through).
        /// </summary>
        void HoldBackIfWaiting(ref FrameInput inp)
        {
            if (ctx == null || ctx.Engagement.MayAttack(this)) return;
            inp.p = inp.k = inp.s = inp.x = false;
        }
    }
}
