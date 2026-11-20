using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using static Kage.OpponentKit;

namespace Kage.Tests
{
    /// <summary>
    /// The smooth-shape tool of the opponent art kit (SAN-112, <c>OpponentKit.Smooth.cs</c>): curves are tessellated into closed,
    /// simple, non-degenerate polygons the rig can triangulate, sharp corners stay sharp, limbs have round ends at the asked
    /// width, clipping and cel insets stay inside the outline.
    /// </summary>
    public class SmoothShapeTests
    {
        const float Px = 1f / KageModel.RefScale;

        /// <summary>A closed outline is usable as a part: at least three points, no repeated point, no edge crossing another, an area, and ear clipping covers exactly that area.</summary>
        public static void AssertGoodOutline(Vector2[] pts, string what)
        {
            Assert.GreaterOrEqual(pts.Length, 3, what + ": too few points");
            for (int i = 0; i < pts.Length; i++)
                Assert.Greater((pts[i] - pts[(i + 1) % pts.Length]).magnitude, 0.05f * Px, what + ": repeated point " + i);
            Assert.Greater(Mathf.Abs(MeshKit.SignedArea(pts)), 4f * Px * Px, what + ": no area");
            Assert.IsFalse(SelfCrosses(pts, out int a, out int b), what + ": edge " + a + " crosses edge " + b);
            var tris = MeshKit.Triangulate(pts);
            float sum = 0f;
            for (int i = 0; i < tris.Count; i += 3) sum += Mathf.Abs(MeshKit.SignedArea(new[] { pts[tris[i]], pts[tris[i + 1]], pts[tris[i + 2]] }));
            Assert.AreEqual(Mathf.Abs(MeshKit.SignedArea(pts)), sum, Mathf.Abs(MeshKit.SignedArea(pts)) * 0.01f, what + ": triangulation does not cover the shape");
        }

        public static bool SelfCrosses(Vector2[] p, out int ea, out int eb)
        {
            int n = p.Length;
            for (int i = 0; i < n; i++)
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue;
                    if (Cross(p[i], p[(i + 1) % n], p[j], p[(j + 1) % n])) { ea = i; eb = j; return true; }
                }
            ea = eb = -1;
            return false;
        }

        static bool Cross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Orient(c, d, a), d2 = Orient(c, d, b), d3 = Orient(a, b, c), d4 = Orient(a, b, d);
            return d1 * d2 < 0f && d3 * d4 < 0f;
        }

        static float Orient(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        static bool Inside(Vector2[] poly, Vector2 q)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > q.y) != (poly[j].y > q.y) && q.x < (poly[j].x - poly[i].x) * (q.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }

        [Test]
        public void ARoundedSquareIsASmoothClosedOutlineInsideItsCorners()
        {
            var pts = RoundedAll(Frame.Ref, 0, 0, 100, 0, 100, 100, 0, 100);
            AssertGoodOutline(pts, "rounded square");
            Assert.Greater(pts.Length, 20, "the corners are curves, not four points");
            foreach (var p in pts)
            {
                Assert.That(p.x, Is.InRange(P(100, 0).x - 1e-4f, P(0, 0).x + 1e-4f));
                Assert.That(p.y, Is.InRange(P(0, 100).y - 1e-4f, P(0, 0).y + 1e-4f));
            }
            // a rounded corner cuts in: the outline passes the corner's midpoint-quadratic, (25, 25) px from the corner at the middle
            Assert.IsFalse(Inside(pts, P(3, 3)), "the corner is cut off");
            Assert.IsTrue(Inside(pts, P(50, 50)));
        }

        [Test]
        public void SharpPointsAreKeptExactly()
        {
            var pts = Rounded(Frame.Ref, 0, 0, 1, 100, 0, 1, 50, 80, 1);
            Assert.AreEqual(3, pts.Length);
            CollectionAssert.AreEqual(new[] { P(0, 0), P(100, 0), P(50, 80) }, pts);
        }

        [Test]
        public void ACapsuleIsATaperedLimbWithRoundEndsAtTheAskedWidth()
        {
            var pts = Capsule(0, 0, 0, 200, 30, 20, 0f);
            AssertGoodOutline(pts, "capsule");
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            Assert.AreEqual(60f * Px, maxX - minX, 1f * Px, "30 px each side at the wide end");
            Assert.AreEqual(250f * Px, maxY - minY, 1f * Px, "round ends: 30 px beyond a, 20 px beyond b");
            var bulged = Capsule(0, 0, 0, 200, 30, 20, 10f, 0.5f);
            float w = 0f;
            foreach (var p in bulged) if (Mathf.Abs(p.y - P(0, 100).y) < 6f * Px) w = Mathf.Max(w, Mathf.Abs(p.x - P(0, 0).x));
            Assert.Greater(w, 30f * Px, "the swelling pushes the middle out past both radii");
        }

        [Test]
        public void CurvesAreTessellatedFinely()
        {
            var o = new Outline().MoveTo(0, 0).CubicTo(100, -80, 200, 80, 300, 0).QuadTo(150, 200, 0, 0);
            var pts = o.Bind();
            AssertGoodOutline(pts, "cubic and quadratic");
            for (int i = 0; i < pts.Length; i++)
                Assert.Less((pts[i] - pts[(i + 1) % pts.Length]).magnitude, Outline.Step * 2.5f * Px, "segment " + i);
            Assert.AreEqual(P(300, 0), o.Bind()[System.Array.FindIndex(pts, p => (p - P(300, 0)).magnitude < 1e-5f)], "passes its end points");
        }

        [Test]
        public void ArcsAndOvalsAreRound()
        {
            var oval = Oval(0, 0, 40, 30, 20f);
            AssertGoodOutline(oval, "oval");
            Assert.AreEqual(Mathf.PI * 40f * 30f * Px * Px, Mathf.Abs(MeshKit.SignedArea(oval)), Mathf.PI * 40f * 30f * Px * Px * 0.02f);
        }

        [Test]
        public void ClippingToAConvexShapeKeepsOnlyTheInside()
        {
            var square = V(0, 0, 100, 0, 100, 100, 0, 100);
            var half = V(50, -10, 200, -10, 200, 200, 50, 200);
            var c = ClipConvex(square, half);
            AssertGoodOutline(c, "clipped");
            Assert.AreEqual(50f * 100f * Px * Px, Mathf.Abs(MeshKit.SignedArea(c)), 1e-6f);
            Assert.AreEqual(0, ClipConvex(square, V(300, 0, 400, 0, 400, 100)).Length, "no overlap, no shape");
        }

        [Test]
        public void ClippingTwoConcaveShapesSeenFromInsideBothKeepsTheirOverlapAndBothNotches()
        {
            // a square with a notch cut into its right side (a face's bridge), clipped to a region with a zigzag bottom edge
            // (a mask broken along a crack): both are concave, both are seen whole from the centre (SAN-76 rework)
            var notched = V(0, 0, 100, 0, 100, 40, 80, 50, 100, 60, 100, 100, 0, 100);
            var zig = V(-20, -20, 120, -20, 120, 70, 90, 80, 60, 60, 30, 80, 0, 60, -20, 70);
            var c = ClipStar(notched, zig, P(50, 45));
            AssertGoodOutline(c, "star clip");
            // the area: the square above y = 60, less the notch (a triangle of 20 x 20 / 2 = 200), plus the zigzag's teeth below 60
            // (three triangles 30 wide and 20 deep over 0..90: 3 x 300 = 900) and the start of the next (a 10-wide trapezoid between
            // y 60 and the edge from 80 to 76.7: (20 + 16.7) / 2 * 10, about 183)
            float want = 100f * 60f - 200f + 900f + 183f;
            Assert.AreEqual(want * Px * Px, Mathf.Abs(MeshKit.SignedArea(c)), want * Px * Px * 0.03f);
            // the notch is still cut in, the teeth still hang
            Assert.IsFalse(Contains(c, P(95, 50)), "the notch is kept");
            Assert.IsTrue(Contains(c, P(30, 75)), "a tooth hangs below 60");
            Assert.IsFalse(Contains(c, P(60, 70)), "between the teeth is cut away");
        }

        static bool Contains(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        [Test]
        public void CelShadingPaintsInsideTheOutlineWithTheShadowAwayFromTheLight()
        {
            var shape = Capsule(0, 0, 0, 200, 40, 30, 6f, 0.4f);
            var tone = new Tone(Color.black, Color.gray, Color.white);
            var polys = Cel(shape, tone, 20f, 4f);
            Assert.AreEqual(4, polys.Count, "seam, shadow, lit edge, base");
            Assert.AreEqual(Seam, polys[0].col);
            foreach (var (pts, col) in polys) AssertGoodOutline(pts, "cel layer " + col);
            for (int k = 2; k < polys.Count; k++)
                foreach (var p in polys[k].pts)
                    Assert.IsTrue(Inside(shape, p) || MinDist(shape, p) < 0.2f * Px, "layer " + k + " leaves the outline");
            // the base layer is pulled in on the shadow side and not on the far side
            Vector2 c = Centroid(polys[3].pts) - Centroid(shape);
            Assert.Less(Vector2.Dot(c, ShadeDir), 0f, "the shadow strip lies towards ShadeDir");
        }

        [Test]
        public void ATornPanelHasSmoothSidesAndAToothedHem()
        {
            var panel = TornPanel(new float[] { 0, 0, -5, 60, -10, 120 }, new float[] { 80, 0, 85, 60, 90, 120 }, 5, 20f, 1);
            AssertGoodOutline(panel, "torn panel");
            int below = 0;
            foreach (var p in panel) if (p.y < P(0, 125).y) below++;
            Assert.GreaterOrEqual(below, 4, "teeth hang below the hem line");
        }

        static float MinDist(Vector2[] poly, Vector2 q)
        {
            float best = float.MaxValue;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], ab = poly[(i + 1) % poly.Length] - a;
                float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-12f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - q).magnitude);
            }
            return best;
        }

        static Vector2 Centroid(Vector2[] p)
        {
            Vector2 s = Vector2.zero;
            foreach (var q in p) s += q;
            return s / p.Length;
        }
    }
}
