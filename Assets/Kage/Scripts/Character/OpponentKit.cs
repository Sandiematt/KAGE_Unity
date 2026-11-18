using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The opponent art pipeline (ADR-0005, SAN-69): small helpers for drawing a new character in code on Kage's
    /// 20-bone skeleton. A part set (Character/PartSets/&lt;Name&gt;PartSet.cs) starts from <see cref="Skeleton"/>, then adds
    /// polygons with <see cref="Add"/>, <see cref="AddMulti"/> and <see cref="AddSwinging"/>. Nothing here moves a
    /// gameplay joint: pivots, parents, hurt circles and the hit points belong to the skeleton and the part set only
    /// adds drawn parts on its bones.
    /// <para>
    /// <b>Frame.</b> Art is authored in the reference pixels Kage's own parts use (x towards the left, y down, 400 px
    /// to a unit, the character facing left; <see cref="P"/> mirrors it to face right). Bone pivots in these pixels:
    /// Hips (878,520), Chest (878,395), Neck (852,222), Head (852,205), near arm shoulder (905,268), elbow (926,375),
    /// hand (914,442), far arm (the katana arm) shoulder (862,272), elbow (822,385), hand (797,455), sword grip (787,478),
    /// near / front leg hip (848,545), knee (835,680), ankle (835,763), far / rear leg hip (905,545), knee (955,680),
    /// ankle (955,763), ground at y = 815. The front of the body is low x.
    /// </para>
    /// <para>
    /// <b>Draw order.</b> A part's <c>slot</c> is a depth slot, back to front (see <see cref="Slot"/>); <c>sub</c> orders parts inside
    /// one slot (0 to 3). Every part gets a rim outline in the rig's rim colour unless it says otherwise. A part on the
    /// sword bone should be a <c>swordLayer</c> part (it moves to the front during sword techniques), and so should the
    /// far arm's parts.
    /// </para>
    /// <para>
    /// <b>Attaching.</b> A part rides the one bone it is added to: put each piece on the bone it moves with (sleeves on the
    /// upper arm, forearm guards on the lower arm, hakama on the thigh and shin) and overlap neighbouring pieces at the
    /// joints so a bent limb shows no gap. Hanging pieces (plates, tails, ears) use <see cref="AddSwinging"/>: its own pivot
    /// with the fighter's spring motion. Soft cloth (coat, hair, scarf) is Kage's own rig and is not part of this kit.
    /// </para>
    /// </summary>
    public static class OpponentKit
    {
        /// <summary>The depth slots Kage's own parts use, back to front. A part set uses the same numbers so Kage and foes stack alike.</summary>
        public static class Slot
        {
            public const int FarArm = 1, FarLeg = 5, FarFoot = 7, Pelvis = 8, BackPlates = 9, Torso = 11, TorsoTrim = 12,
                NearLeg = 13, NearFoot = 15, FrontPlates = 16, Belt = 18, BeltTrim = 19, Neck = 20, Head = 21, HeadTop = 23,
                Face = 24, NearArm = 26, NearHand = 28;
        }

        /// <summary>Reference pixels to bind space, facing right.</summary>
        public static Vector2 P(float x, float y) => KageModel.P(x, y);

        /// <summary>A polygon from x,y pairs in reference pixels.</summary>
        public static Vector2[] V(params float[] xy)
        {
            var r = new Vector2[xy.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = P(xy[i * 2], xy[i * 2 + 1]);
            return r;
        }

        /// <summary>
        /// A new rig with Kage's gameplay skeleton (pivots, parents, hurt circles, fist, hilt, foot and blade hit points) and
        /// no parts, scarf or cloth: the part set draws everything on it.
        /// </summary>
        public static RigDef Skeleton(string name) => KageModel.Skeleton(name);

        // ------------------------------------------------------------------------------------ shapes

        /// <summary>A tapered limb piece from (ax,ay) to (bx,by), <paramref name="wa"/> wide at a and <paramref name="wb"/> at b, with round ends.</summary>
        public static Vector2[] Tube(float ax, float ay, float bx, float by, float wa, float wb, int cap = 4)
        {
            Vector2 a = new(ax, ay), b = new(bx, by);
            Vector2 dir = (b - a).normalized, n = new Vector2(-dir.y, dir.x);
            var px = new List<Vector2>();
            px.Add(a + n * wa * 0.5f);
            px.Add(b + n * wb * 0.5f);
            for (int i = 1; i < cap; i++)      // round end at b
            {
                float t = i / (float)cap * Mathf.PI;
                px.Add(b + (n * Mathf.Cos(t) + dir * Mathf.Sin(t)) * wb * 0.5f);
            }
            px.Add(b - n * wb * 0.5f);
            px.Add(a - n * wa * 0.5f);
            for (int i = 1; i < cap; i++)      // round end at a
            {
                float t = i / (float)cap * Mathf.PI;
                px.Add(a + (-n * Mathf.Cos(t) - dir * Mathf.Sin(t)) * wa * 0.5f);
            }
            return ToBind(px);
        }

        /// <summary>An ellipse centred on (cx,cy), radii rx and ry, turned by <paramref name="deg"/> degrees.</summary>
        public static Vector2[] Ellipse(float cx, float cy, float rx, float ry, float deg = 0f, int n = 18)
        {
            var px = new List<Vector2>();
            float s = Mathf.Sin(deg * Mathf.Deg2Rad), c = Mathf.Cos(deg * Mathf.Deg2Rad);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n * Mathf.PI * 2f;
                float x = Mathf.Cos(t) * rx, y = Mathf.Sin(t) * ry;
                px.Add(new Vector2(cx + x * c - y * s, cy + x * s + y * c));
            }
            return ToBind(px);
        }

        /// <summary>A stroke of the given width (pixels) along an open polyline of x,y pairs, tapered a little at both ends.</summary>
        public static Vector2[] Band(float width, params float[] xy)
        {
            var c = new List<Vector2>();
            for (int i = 0; i + 3 < xy.Length; i += 2)
            {
                Vector2 a = new(xy[i], xy[i + 1]), b = new(xy[i + 2], xy[i + 3]);
                int n = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 10f));
                for (int k = 0; k < n; k++) c.Add(Vector2.Lerp(a, b, k / (float)n));
            }
            c.Add(new Vector2(xy[xy.Length - 2], xy[xy.Length - 1]));
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            for (int i = 0; i < c.Count; i++)
            {
                Vector2 t = (c[Mathf.Min(c.Count - 1, i + 1)] - c[Mathf.Max(0, i - 1)]).normalized;
                Vector2 n = new Vector2(-t.y, t.x);
                float u = i / (float)Mathf.Max(1, c.Count - 1);
                float w = width * 0.5f * Mathf.Lerp(0.6f, 1f, Mathf.Min(1f, Mathf.Min(u, 1f - u) * 6f));
                left.Add(c[i] + n * w);
                right.Add(c[i] - n * w);
            }
            right.Reverse();
            left.AddRange(right);
            return ToBind(left);
        }

        /// <summary>
        /// A polygon laid along the sword's own axis (x along the blade from the grip, y across it, in units), turned into the
        /// rig's bind pose. The blade's hit line is <c>bladeBaseLocal</c> (0.08, 0) to <c>bladeTipLocal</c> (0.80, 0.03) in this frame.
        /// </summary>
        public static Vector2[] AlongSword(RigDef d, params float[] xy)
        {
            var r = new Vector2[xy.Length / 2];
            Vector2 grip = d.pivot[(int)B.Sword];
            for (int i = 0; i < r.Length; i++) r[i] = grip + KMath.Rot(new Vector2(xy[i * 2], xy[i * 2 + 1]), d.bladeBindAngle);
            return r;
        }

        static Vector2[] ToBind(List<Vector2> px)
        {
            var r = new Vector2[px.Count];
            for (int i = 0; i < r.Length; i++) r[i] = P(px[i].x, px[i].y);
            return r;
        }

        // ------------------------------------------------------------------------------------ adding parts

        /// <summary>Adds one flat-coloured polygon to a bone. Returns the part so the caller can adjust it.</summary>
        public static PartDef Add(RigDef d, string name, B bone, int slot, Color color, Vector2[] pts, int sub = 0,
            PartRole role = PartRole.Body, bool rim = true, Color? rimColor = null, float glow = 1f, bool swordLayer = false)
        {
            var p = new PartDef
            {
                name = name, bone = bone, order = slot * 4 + sub, color = color, role = role, pts = pts,
                rim = rim, rimColor = rimColor, glow = glow, swordLayer = swordLayer,
            };
            d.parts.Add(p);
            return p;
        }

        /// <summary>
        /// Adds one part painted from several polygons in order (a plate with its lacing, a sleeve with its cuff): one mesh, one
        /// draw slot, no rim of its own. <paramref name="outline"/> (optional) is the silhouette, drawn first in the rim colour
        /// and shifted towards the back light, so the part still has the rig's rim light.
        /// </summary>
        public static PartDef AddMulti(RigDef d, string name, B bone, int slot, List<(Vector2[] pts, Color col)> polys, int sub = 0,
            Vector2[] outline = null, PartRole role = PartRole.Body, bool swordLayer = false)
        {
            if (outline != null)
            {
                var all = new List<(Vector2[] pts, Color col)> { (Shift(MeshKit.Grow(outline, 0.0045f), new Vector2(-0.0055f, 0.0055f)), d.rimColor) };
                all.AddRange(polys);
                polys = all;
            }
            var p = new PartDef { name = name, bone = bone, order = slot * 4 + sub, role = role, polys = polys, rim = false, swordLayer = swordLayer };
            d.parts.Add(p);
            return p;
        }

        /// <summary>
        /// Adds a hanging piece (a skirt plate, a hood tail, an ear): a flat polygon on a bone with its own pivot at
        /// (<paramref name="pivotX"/>, <paramref name="pivotY"/>) pixels, swung by the fighter's spring motion. <paramref name="swing"/>
        /// is how much (0.3 a little, 1 a lot).
        /// </summary>
        public static PartDef AddSwinging(RigDef d, string name, B bone, int slot, Color color, Vector2[] pts, float pivotX, float pivotY,
            float swing, int sub = 0, PartRole role = PartRole.Body, bool rim = true)
        {
            var p = Add(d, name, bone, slot, color, pts, sub, role, rim);
            p.jigglePivot = P(pivotX, pivotY);
            p.jiggle = swing;
            return p;
        }

        /// <summary>A polygon list helper: the shape and its colour.</summary>
        public static (Vector2[] pts, Color col) Poly(Vector2[] pts, Color col) => (pts, col);

        static Vector2[] Shift(Vector2[] pts, Vector2 by)
        {
            var r = new Vector2[pts.Length];
            for (int i = 0; i < r.Length; i++) r[i] = pts[i] + by;
            return r;
        }

        /// <summary>The same colour a little lighter or darker (k above 1 lightens), alpha kept: for shading a flat part.</summary>
        public static Color Shade(Color c, float k) => new(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);
    }
}
