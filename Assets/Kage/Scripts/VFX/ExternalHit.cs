using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// What projectiles and hazards share (ADR-0007): who they may hit, the order they meet targets in,
    /// and delivery of one hit through the defence order (ADR-0004). Fighter melee does the same in
    /// Fighter.HitDetection; this is the same rule set for things that are not a fighter's own move.
    /// </summary>
    internal static class ExternalHit
    {
        public enum Outcome
        {
            /// <summary>Nothing happened: the target can't be hit now (invulnerable, down...). Try again next frame.</summary>
            Missed,
            /// <summary>A pre-hit check took it (perfect dodge): the hit is spent on that target, the thing flies on.</summary>
            Dodged,
            /// <summary>The target received it: a hit, a block or a counter.</summary>
            Landed,
        }

        /// <summary>Never the owner, never a fighter of the owner's team.</summary>
        public static bool CanTarget(Fighter owner, IHittable t)
        {
            if (t == null || ReferenceEquals(t, owner)) return false;
            return !(t is Fighter f && owner != null && f.team == owner.team);
        }

        /// <summary>The distinct owners of <paramref name="boxes"/> in a fixed order: fighters in tick order, then props.</summary>
        public static void OrderedTargets(FightContext ctx, List<Hurtbox> boxes, List<IHittable> into)
        {
            into.Clear();
            var order = new List<int>();
            foreach (var b in boxes)
            {
                var t = b.Owner;
                if (t == null || into.Contains(t)) continue;
                int fi = t is Fighter f ? ctx.Fighters.IndexOf(f) : -1;
                int pi = fi < 0 ? ctx.Props.IndexOf(t) : -1;
                int key = fi >= 0 ? fi : pi >= 0 ? 10000 + pi : 20000 + into.Count;
                int at = order.Count;
                while (at > 0 && order[at - 1] > key) at--;
                order.Insert(at, key);
                into.Insert(at, t);
            }
        }

        /// <summary>The circle swept with each sample point: where <paramref name="t"/> is touched, as a melee window does it.</summary>
        public static bool FindContact(IHittable t, List<(Vector2 from, Vector2 to)> samples, float radius, out HitZone zone, out Vector2 contact)
        {
            zone = HitZone.Body;
            contact = default;
            var boxes = Scratch;
            // the first sample (in order) that rests in one of its circles wins, with the lowest-index circle's zone
            foreach (var (_, to) in samples)
            {
                Hurtbox.Overlap(to, radius, boxes);
                Hurtbox best = null;
                foreach (var b in boxes) if (ReferenceEquals(b.Owner, t) && (best == null || b.Index < best.Index)) best = b;
                if (best == null) continue;
                zone = best.Zone;
                contact = Vector2.Lerp(to, best.Centre, radius / (radius + best.Radius));
                return true;
            }
            // else the earliest touch along any sample's path since last frame
            float bestFrac = float.MaxValue;
            int bestIndex = int.MaxValue;
            foreach (var (from, to) in samples)
            {
                Hurtbox.Sweep(from, to, radius, SweepScratch);
                foreach (var h in SweepScratch)
                {
                    if (!ReferenceEquals(h.box.Owner, t)) continue;
                    if (h.fraction > bestFrac || (h.fraction == bestFrac && h.box.Index >= bestIndex)) continue;
                    bestFrac = h.fraction;
                    bestIndex = h.box.Index;
                    zone = h.box.Zone;
                    contact = h.point;
                }
            }
            return bestFrac != float.MaxValue;
        }

        static readonly List<Hurtbox> Scratch = new();
        static readonly List<HurtboxHit> SweepScratch = new();

        /// <summary>
        /// One hit through the defence order: pre-hit checks (perfect dodge), whether the target can be hit
        /// now, then its own checks in ReceiveHit; the owner then publishes the result like any hit.
        /// </summary>
        public static Outcome Deliver(Fighter owner, IHittable t, HitInfo info, out HitResult result)
        {
            result = HitResult.Whiff;
            if (t is Fighter tf)
            {
                var c = new DefenceCheck { info = info };
                if (tf.PreHit(ref c))
                {
                    result = c.result;
                    owner.ResolveExternal(t, c.info, c.result);
                    return Outcome.Dodged;
                }
            }
            if (!t.HittableBy(info.w)) return Outcome.Missed;
            result = t.ReceiveHit(info);
            if (result == HitResult.Whiff) return Outcome.Missed;
            owner.ResolveExternal(t, info, result);
            return Outcome.Landed;
        }
    }
}
