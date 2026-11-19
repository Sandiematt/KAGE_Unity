using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The geometry of a weapon clash (SAN-93, ADR-0004 step 1): do two blades' paths cross within one frame? Pure math on
    /// <see cref="HitSweep"/> samples, so it is the same on every machine and every run.
    /// <para>
    /// A blade is the run of one window's Blade (or BladeN) samples, base to tip. Each sample moved from <c>from</c> (its
    /// place on the window's previous active frame) to <c>to</c> (this frame's), so the blade swept a strip of quads. The
    /// strip's outline is three sets of segments: the blade at the end of the frame (the <c>to</c> points joined), each
    /// sample's own path, and the blade at the start of the frame (the <c>from</c> points joined). Two blades clash when any
    /// segment of one intersects any segment of the other, touching included. That catches a blade swung through a still
    /// one, two swings that cross, and two blades pointed at each other along one line (spear against spear).
    /// </para>
    /// </summary>
    public static class ClashGeometry
    {
        /// <summary>One blade of one window in a frame's sweeps: <c>sweeps[start..end)</c>, base to tip.</summary>
        public struct BladeRun
        {
            public Fighter owner;
            public MoveDef move;
            public HitWindow window;
            public int start, end;
        }

        /// <summary>
        /// Splits a frame's sweeps into blades: each run of consecutive <see cref="HitPoint.Blade"/> or <see cref="HitPoint.BladeN"/>
        /// samples of one owner's one window and one point kind. A twin blade window gives two runs (the far blade, the near blade);
        /// fist, hilt, foot, shin and knee samples give none, so those attacks can never clash. Appends to <paramref name="into"/>.
        /// </summary>
        public static void CollectBlades(IReadOnlyList<HitSweep> sweeps, List<BladeRun> into)
        {
            int i = 0;
            while (i < sweeps.Count)
            {
                var s = sweeps[i];
                if (s.point is not (HitPoint.Blade or HitPoint.BladeN)) { i++; continue; }
                int j = i + 1;
                while (j < sweeps.Count && sweeps[j].owner == s.owner && sweeps[j].window == s.window && sweeps[j].point == s.point) j++;
                into.Add(new BladeRun { owner = s.owner, move = s.move, window = s.window, start = i, end = j });
                i = j;
            }
        }

        /// <summary>Segments shorter than this (m) are points and are skipped: a blade that didn't move has no path segments, only its line.</summary>
        const float MinLength = 1e-6f;
        /// <summary>Relative tolerance for the parallel and collinear tests.</summary>
        const float Eps = 1e-6f;

        /// <summary>
        /// True when segment a0-a1 meets segment b0-b1, ends included. <paramref name="point"/> is where: the crossing, or the
        /// middle of the shared stretch when the segments overlap along one line. A segment shorter than a micrometre is a
        /// point and never crosses.
        /// </summary>
        public static bool SegmentsCross(Vector2 a0, Vector2 a1, Vector2 b0, Vector2 b1, out Vector2 point)
        {
            point = default;
            Vector2 r = a1 - a0, s = b1 - b0;
            float rr = r.sqrMagnitude, ss = s.sqrMagnitude;
            if (rr < MinLength * MinLength || ss < MinLength * MinLength) return false;
            float denom = Cross(r, s);
            Vector2 qp = b0 - a0;
            if (Mathf.Abs(denom) <= Eps * Mathf.Sqrt(rr * ss))
            {
                // parallel: they meet only when on one line and their stretches overlap
                if (Mathf.Abs(Cross(qp, r)) > Eps * Mathf.Sqrt(rr)) return false;
                float t0 = Vector2.Dot(qp, r) / rr;
                float t1 = t0 + Vector2.Dot(s, r) / rr;
                float lo = Mathf.Max(0f, Mathf.Min(t0, t1)), hi = Mathf.Min(1f, Mathf.Max(t0, t1));
                if (lo > hi) return false;
                point = a0 + r * ((lo + hi) * 0.5f);
                return true;
            }
            float t = Cross(qp, s) / denom;
            float u = Cross(qp, r) / denom;
            const float slack = 1e-6f;
            if (t < -slack || t > 1f + slack || u < -slack || u > 1f + slack) return false;
            point = a0 + r * Mathf.Clamp01(t);
            return true;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <summary>
        /// True when blade A (<c>sweeps[aStart..aEnd)</c>) and blade B (<c>sweeps[bStart..bEnd)</c>) cross this frame.
        /// <paramref name="contact"/> is the first crossing found, in a fixed segment order (A's end line, its paths, its start
        /// line, against B's in the same order), so the same frame always gives the same point.
        /// </summary>
        public static bool BladesCross(IReadOnlyList<HitSweep> sweeps, int aStart, int aEnd, int bStart, int bEnd, out Vector2 contact)
        {
            contact = default;
            int na = Count(aStart, aEnd), nb = Count(bStart, bEnd);
            for (int a = 0; a < na; a++)
            {
                Segment(sweeps, aStart, aEnd, a, out var a0, out var a1);
                for (int b = 0; b < nb; b++)
                {
                    Segment(sweeps, bStart, bEnd, b, out var b0, out var b1);
                    if (SegmentsCross(a0, a1, b0, b1, out contact)) return true;
                }
            }
            return false;
        }

        /// <summary>A blade of n samples has 3n - 2 segments (n - 1 each for the end and start lines, n paths); a single sample has just its path.</summary>
        static int Count(int start, int end) => end - start <= 1 ? 1 : 3 * (end - start) - 2;

        /// <summary>Segment <paramref name="index"/> of the blade's outline: first the end line, then the paths, then the start line.</summary>
        static void Segment(IReadOnlyList<HitSweep> sweeps, int start, int end, int index, out Vector2 p0, out Vector2 p1)
        {
            int n = end - start;
            if (n <= 1) { p0 = sweeps[start].from; p1 = sweeps[start].to; return; }
            if (index < n - 1) { p0 = sweeps[start + index].to; p1 = sweeps[start + index + 1].to; return; }
            index -= n - 1;
            if (index < n) { p0 = sweeps[start + index].from; p1 = sweeps[start + index].to; return; }
            index -= n;
            p0 = sweeps[start + index].from; p1 = sweeps[start + index + 1].from;
        }
    }
}
