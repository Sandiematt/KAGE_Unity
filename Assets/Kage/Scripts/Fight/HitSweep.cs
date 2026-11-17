using UnityEngine;

namespace Kage
{
    /// <summary>
    /// One hit point's path in one frame (ADR-0002, step 3): from its position on the window's
    /// previous active frame to its position on this frame. On a window's first active frame (or the
    /// first frame after a gap) <see cref="from"/> equals <see cref="to"/>. The fighter tick records
    /// them; the fight loop's clash and hit phases read them.
    /// </summary>
    public struct HitSweep
    {
        public Fighter owner;
        public MoveDef move;
        public HitWindow window;
        public HitPoint point;
        /// <summary>Index of this sample among the window's sample points (a blade has five, a foot two).</summary>
        public int sample;
        public Vector2 from, to;
        public float radius;
    }
}
