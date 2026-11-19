using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Who may attack (SAN-83): at most <see cref="EngagementTuning.maxAttackers"/> foes are <b>engaged</b> at once; the
    /// others wait at a distance and circle (<see cref="AIInputSource"/>). Foes are the fighters not on Kage's team (team 0);
    /// Kage's side is never limited. One per fight (<see cref="FightContext.Engagement"/>), updated by the fight loop at the
    /// start of every frame, after targets are picked and before input (ADR-0002).
    /// <para>The rules, in order, each frame:</para>
    /// <list type="number">
    /// <item>An engaged foe that is knocked out, gone (inactive) or has no target leaves its place.</item>
    /// <item>Free places go to the waiting foes nearest to their targets (ties: fighter order). So when an engaged foe is
    /// defeated, the nearest waiting foe takes its place on the next frame.</item>
    /// <item>The engaged foe farthest from its target gives its place to the nearest waiting foe when that one is
    /// <see cref="EngagementTuning.swapMargin"/> nearer, but only while it is not committed to an attack
    /// (<see cref="Fighter.CommittedToAttack"/>).</item>
    /// </list>
    /// A foe that isn't engaged can't attack: its attack buttons are dropped at the input phase
    /// (<see cref="Fighter.PollInput"/>), whatever drives it. Together with rule 3 this means every foe in an attack move is
    /// engaged, so no more than maxAttackers foes are ever <see cref="Fighter.InAttackMove"/> on the same frame.
    /// With one foe it is always engaged and nothing changes.
    /// </summary>
    public sealed class EngagementManager
    {
        readonly List<Fighter> _engaged = new();

        /// <summary>The engaged foes, in the order they took their places.</summary>
        public IReadOnlyList<Fighter> Engaged => _engaged;

        public int MaxAttackers => Mathf.Max(0, Tuning.Get<EngagementTuning>().maxAttackers);

        /// <summary>True for a fighter whose attacks the manager limits: a foe (any team but Kage's).</summary>
        public static bool Limits(Fighter f) => f != null && f.team != 0;

        public bool IsEngaged(Fighter f) => _engaged.Contains(f);

        /// <summary>True when <paramref name="f"/> may attack this frame: Kage's side always, a foe only when engaged.</summary>
        public bool MayAttack(Fighter f) => !Limits(f) || _engaged.Contains(f);

        /// <summary>The foes waiting their turn: in the fight, not engaged.</summary>
        public void Waiting(List<Fighter> fighters, List<Fighter> into)
        {
            into.Clear();
            foreach (var f in fighters) if (Limits(f) && InFight(f) && !_engaged.Contains(f)) into.Add(f);
        }

        /// <summary>The fight loop, each frame: applies the three rules above to <paramref name="fighters"/> (tick order).</summary>
        public void Update(List<Fighter> fighters)
        {
            for (int i = _engaged.Count - 1; i >= 0; i--)
                if (!InFight(_engaged[i]) || !fighters.Contains(_engaged[i])) _engaged.RemoveAt(i);

            int max = MaxAttackers;
            while (_engaged.Count > max) _engaged.RemoveAt(_engaged.Count - 1);
            while (_engaged.Count < max)
            {
                var w = NearestWaiting(fighters);
                if (w == null) break;
                _engaged.Add(w);
            }

            var near = NearestWaiting(fighters);
            if (near == null || _engaged.Count == 0) return;
            int far = -1;
            float farDist = float.MinValue;
            for (int i = 0; i < _engaged.Count; i++)
            {
                var e = _engaged[i];
                if (e.CommittedToAttack) continue;
                float d = Distance(e);
                if (d > farDist) { farDist = d; far = i; }
            }
            if (far >= 0 && farDist - Distance(near) > Tuning.Get<EngagementTuning>().swapMargin) _engaged[far] = near;
        }

        Fighter NearestWaiting(List<Fighter> fighters)
        {
            Fighter best = null;
            float bestDist = float.MaxValue;
            foreach (var f in fighters)
            {
                if (!Limits(f) || !InFight(f) || _engaged.Contains(f)) continue;
                float d = Distance(f);
                if (d < bestDist) { bestDist = d; best = f; }
            }
            return best;
        }

        /// <summary>Taking part: built, active, not knocked out, with a target.</summary>
        static bool InFight(Fighter f) => f != null && f.isActiveAndEnabled && f.rb != null && f.state != FState.KO && f.opponent != null;

        /// <summary>Horizontal distance from a foe to its target.</summary>
        public static float Distance(Fighter f) => Mathf.Abs(f.opponent.Pos.x - f.Pos.x);
    }
}
