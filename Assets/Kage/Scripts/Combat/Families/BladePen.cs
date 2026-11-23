using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Draws a weapon family's parts from the JS weapon drawings (src/js/21-characters/kage/weapons.js, <c>KAGE_BLADES</c>)
    /// onto a rig: points are given in the JS canvas frame (origin at the grip, +x along the blade, -y the spine side, in
    /// the weapon's own units) and turned into the family part frame (metres, +y the spine side). Fills become polygons,
    /// canvas strokes become thin strips, and <c>kbGlow</c>'s edge light becomes a soft wide strip under a lit core and a
    /// hot centre line.
    /// </summary>
    public sealed class BladePen
    {
        readonly FighterRig rig;
        readonly string prefix;
        readonly B bone;
        readonly Vector2 origin, dir, perp;
        readonly float m;
        int n;

        /// <param name="origin">Where the JS origin lands in the family part frame.</param>
        /// <param name="dir">The JS +x direction in the family part frame (unit).</param>
        /// <param name="metresPerUnit">Metres per JS unit (the weapon's drawn length over its JS length).</param>
        public BladePen(FighterRig rig, string prefix, B bone, Vector2 origin, Vector2 dir, float metresPerUnit)
        {
            this.rig = rig; this.prefix = prefix; this.bone = bone; this.origin = origin;
            this.dir = dir.normalized; perp = new Vector2(-this.dir.y, this.dir.x); m = metresPerUnit;
        }

        /// <summary>A JS point in the family part frame (JS -y, the spine, is the frame's +y).</summary>
        public Vector2 P(float x, float y) => origin + dir * (x * m) - perp * (y * m);

        Vector2[] Map(IList<Vector2> js)
        {
            var r = new Vector2[js.Count];
            for (int i = 0; i < r.Length; i++) r[i] = P(js[i].x, js[i].y);
            return r;
        }

        void Add(string name, int sub, Color c, PartRole role, Vector2[] pts, bool rim, float glow)
            => rig.AddFamilyPart($"{prefix}_{name}_{n++}", pts, c, role, sub, rim, glow, bone);

        public void Fill(string name, int sub, Color c, PartRole role, IList<Vector2> js, bool rim = false, float glow = 1f)
            => Add(name, sub, c, role, Map(js), rim, glow);

        public void Ellipse(string name, int sub, Color c, PartRole role, float cx, float cy, float rx, float ry, bool rim = false, float glow = 1f, int seg = 18)
        {
            var pts = new Vector2[seg];
            for (int i = 0; i < seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
            }
            Fill(name, sub, c, role, pts, rim, glow);
        }

        /// <summary>A canvas stroke: a strip <paramref name="width"/> JS units wide along the polyline (mitred, the mitre clamped).</summary>
        public void Stroke(string name, int sub, Color c, PartRole role, IList<Vector2> js, float width, float glow = 1f)
        {
            var pts = Map(js);
            int k = pts.Length;
            if (k < 2) return;
            float h = width * m * 0.5f;
            var left = new Vector2[k];
            var right = new Vector2[k];
            for (int i = 0; i < k; i++)
            {
                Vector2 a = i > 0 ? (pts[i] - pts[i - 1]).normalized : (pts[1] - pts[0]).normalized;
                Vector2 b = i < k - 1 ? (pts[i + 1] - pts[i]).normalized : a;
                Vector2 t = (a + b).sqrMagnitude > 1e-6f ? (a + b).normalized : a;
                Vector2 nrm = new(-t.y, t.x);
                float cos = Mathf.Max(0.5f, Vector2.Dot(nrm, new Vector2(-a.y, a.x)));
                left[i] = pts[i] + nrm * (h / cos);
                right[i] = pts[i] - nrm * (h / cos);
            }
            var poly = new Vector2[k * 2];
            for (int i = 0; i < k; i++) { poly[i] = left[i]; poly[k * 2 - 1 - i] = right[i]; }
            Add(name, sub, c, role, poly, false, glow);
        }

        /// <summary>JS <c>kbGlow</c>: a soft wide light under the lit line and its hot core.</summary>
        public void Glow(string name, IList<Vector2> js, float width, Color glow, Color hot, int softSub, int coreSub)
        {
            Stroke(name + "Soft", softSub, new Color(glow.r, glow.g, glow.b, 0.3f), PartRole.Trim, js, width * 3.2f);
            Stroke(name, coreSub, glow, PartRole.Trim, js, width * 1.25f, 1.6f);
            Stroke(name + "Hot", coreSub, hot, PartRole.Trim, js, width * 0.5f, 1.8f);
        }

        /// <summary>JS <c>kbGrip</c>: the handle from x0 to x1 (half width hw, round ends) and n - 1 diamond wraps.</summary>
        public void Grip(string name, int sub, float x0, float x1, float hw, Color wrap, Color dia, int count, bool rim = true)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= 6; i++) { float a = Mathf.PI / 2f + i / 6f * Mathf.PI; pts.Add(new Vector2(x0 + Mathf.Cos(a) * hw, Mathf.Sin(a) * hw)); }
            for (int i = 0; i <= 6; i++) { float a = -Mathf.PI / 2f + i / 6f * Mathf.PI; pts.Add(new Vector2(x1 + Mathf.Cos(a) * hw, Mathf.Sin(a) * hw)); }
            Fill(name, sub, wrap, PartRole.Body, pts, rim);
            float d = hw * 0.45f * 1.4142f;
            for (int i = 1; i < count; i++)
            {
                float x = Mathf.Lerp(x0, x1, (i + 0.1f) / (count + 0.2f));
                Fill(name + "Wrap", sub + 1, dia, PartRole.TrimDeep, new[] { new Vector2(x - d, 0), new Vector2(x, -d), new Vector2(x + d, 0), new Vector2(x, d) });
            }
        }

        /// <summary>JS <c>kbBlade</c>: the spine from base to tip, then the edge back, both [u, v] with u in blade lengths and v in JS units.</summary>
        public static List<Vector2> Outline(float L, IList<Vector2> spine, IList<Vector2> edge)
        {
            var pts = new List<Vector2>();
            foreach (var p in spine) pts.Add(new Vector2(p.x * L, p.y));
            for (int i = edge.Count - 1; i >= 0; i--) pts.Add(new Vector2(edge[i].x * L, edge[i].y));
            return pts;
        }

        /// <summary>The part of a polygon on the side y &lt; <paramref name="yMax"/> (Sutherland-Hodgman against one line): a canvas clip to a rectangle above that line.</summary>
        public static List<Vector2> ClipBelowY(IList<Vector2> poly, float yMax)
        {
            var r = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                bool ain = a.y < yMax, bin = b.y < yMax;
                if (ain) r.Add(a);
                if (ain != bin) r.Add(Vector2.Lerp(a, b, (yMax - a.y) / (b.y - a.y)));
            }
            return r;
        }
    }
}
