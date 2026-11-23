using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The katana family's drawing. Kage's two shop katanas are drawn as the Armory shows them (the JS <c>kb_kagetsu</c> and
    /// <c>kb_shizuka</c> of src/js/21-characters/kage/weapons.js) over his hidden base katana: Kagetsu's back-swept spine
    /// teeth, moon bite and hooked kissaki, crimson hamon and edge light, bone-white tsuka and crescent tsuba; Shizuka's
    /// needle blade, winged guard, crescent pommel ring and violet ridge light. Every other katana is the part set's own
    /// (attaching just shows it). Only a rig built on Kage's model (it has his <c>Kashira</c>) takes the shop drawings.
    /// </summary>
    public sealed class KatanaVisual : IWeaponVisual
    {
        /// <summary>The JS origin (the guard) in the sword frame, and metres per JS unit: Kagetsu's 82-unit blade ends at the katana's tip, 0.80.</summary>
        const float Origin = 0.05f, M = 0.75f / 82f;

        static readonly Color Gold = KMath.Hex("#C9A45C"), GoldHi = KMath.Hex("#F2DCA4");

        public void Attach(FighterRig rig, string weaponId)
        {
            rig.ClearFamilyParts();                  // idempotent: never add the parts twice
            rig.SetSwordVisible(true);
            if (!rig.HasOwnPart("Kashira")) return;
            switch (weaponId)
            {
                case "kagetsu": rig.SetBaseSwordHidden(true); Kagetsu(Pen(rig)); break;
                case "shizuka": rig.SetBaseSwordHidden(true); Shizuka(Pen(rig)); break;
            }
        }

        public void Detach(FighterRig rig) => rig.ClearFamilyParts();

        static BladePen Pen(FighterRig rig) => new(rig, "Katana", B.Sword, new Vector2(Origin, 0f), Vector2.right, M);

        static Vector2 V(float x, float y) => new(x, y);

        /// <summary>JS <c>kbTassel</c> at rest: cords hanging from (x, y).</summary>
        static void Tassel(BladePen p, float x, float y, float len, Color c, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float o = (i - 1) * 1.1f, a = 2.2f + i * 0.12f;
                Vector2 p0 = V(x, y), c1 = V(x - len * 0.35f, y + o + len * 0.2f), p1 = V(x + Mathf.Cos(a) * len, y + o + Mathf.Sin(a) * len);
                var pts = new List<Vector2>();
                for (int k = 0; k <= 6; k++)
                {
                    float t = k / 6f;
                    pts.Add((1 - t) * (1 - t) * p0 + 2 * (1 - t) * t * c1 + t * t * p1);
                }
                p.Stroke("Tassel", 3, c, PartRole.Trim, pts, 1.1f);
            }
        }

        static void Outline(BladePen p, string name, int sub, Color c, IList<Vector2> pts, float w)
        {
            var loop = new List<Vector2>(pts) { pts[0] };
            p.Stroke(name, sub, c, PartRole.Metal, loop, w);
        }

        static List<Vector2> EllipsePts(float cx, float cy, float rx, float ry, float a0 = 0f, float a1 = Mathf.PI * 2f, int seg = 18)
        {
            var r = new List<Vector2>();
            bool full = Mathf.Abs(a1 - a0) >= Mathf.PI * 2f - 1e-4f;
            int n = full ? seg : seg + 1;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)seg);
                r.Add(V(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
            }
            return r;
        }

        // #7 Kagetsu, "Shadow Moon"
        static void Kagetsu(BladePen p)
        {
            const float L = 82f, hl = 0.31f;
            Color steel = Palette.Steel, band = KMath.Hex("#2b2d33"), glow = KMath.Hex("#ff2238"), hot = KMath.Hex("#ffc2c6");

            p.Grip("Tsuka", 0, -L * hl, -1.6f, 2.3f, KMath.Hex("#d9cfb8"), KMath.Hex("#2a2620"), 5);   // bone-white tsuka
            p.Ellipse("Kashira", 1, Gold, PartRole.Metal, -L * hl, 0f, 2.6f, 2.6f, true);
            p.Ellipse("Moon", 2, glow, PartRole.Trim, -L * hl, 0f, 1f, 1f, false, 1.5f, 10);
            Tassel(p, -L * hl - 1.5f, 0f, 9f, KMath.Hex("#d8462f"), 2);
            // crescent-moon tsuba: a gold oval with a crimson crescent inlaid
            p.Ellipse("Tsuba", 2, Gold, PartRole.Metal, -0.7f, 0f, 2.3f, 6.6f, true);
            var cres = EllipsePts(-0.7f, 0f, 1.3f, 4.8f, -Mathf.PI / 2f, Mathf.PI / 2f, 10);
            cres.AddRange(EllipsePts(-0.7f, 0f, 0.45f, 4.8f, Mathf.PI / 2f, -Mathf.PI / 2f, 10));
            p.Fill("TsubaMoon", 3, glow, PartRole.Trim, cres, false, 1.4f);

            float Cy(float u) => -0.05f * u * u * L;
            float W(float u) => Mathf.Lerp(2.5f, 1.9f, u);
            var spine = new List<Vector2> { V(0.015f, Cy(0) - W(0)) };
            // three teeth: rise, sweep back toward the hilt, drop back to the spine
            foreach (var (u, h, b) in new[] { (0.17f, 3.2f, 0.075f), (0.35f, 2.7f, 0.07f), (0.52f, 2.1f, 0.06f) })
            {
                spine.Add(V(u - b, Cy(u - b) - W(u - b)));
                spine.Add(V(u - b * 0.85f, Cy(u) - W(u) - h));
                spine.Add(V(u, Cy(u) - W(u)));
            }
            spine.Add(V(0.82f, Cy(0.82f) - W(0.82f)));
            spine.Add(V(0.95f, Cy(0.95f) - W(0.95f) * 0.7f));
            spine.Add(V(1f, Cy(1) + 0.2f));
            var edge = new List<Vector2>
            {
                V(0.015f, Cy(0) + W(0)), V(0.3f, Cy(0.3f) + W(0.3f)), V(0.6f, Cy(0.6f) + W(0.6f)), V(0.78f, Cy(0.78f) + W(0.78f)),
                V(0.82f, Cy(0.82f) + W(0.82f) * 0.2f), V(0.86f, Cy(0.86f) - W(0.86f) * 0.15f), V(0.9f, Cy(0.9f) + W(0.9f) * 0.2f),   // the moon bite
                V(0.93f, Cy(0.93f) + W(0.93f) * 1.9f), V(0.955f, Cy(0.955f) + W(0.955f) * 0.9f), V(1f, Cy(1) + 0.2f),                 // the hook
            };
            edge.RemoveAt(edge.Count - 1);        // the tip is the spine's last point already
            var blade = BladePen.Outline(L, spine, edge);
            p.Fill("Blade", 0, steel, PartRole.Metal, blade, true);
            // the dark spine band carrying the teeth (the blade above its centre line, the whole of the curved tip)
            p.Fill("Band", 1, band, PartRole.Metal, BladePen.ClipBelowY(blade, -0.05f));
            // the crimson hamon wave
            var ham = new List<Vector2>();
            for (int i = 0; i <= 24; i++) { float u = 0.04f + 0.74f * i / 24f; ham.Add(V(u * L, Cy(u) + W(u) * (0.15f + 0.22f * Mathf.Sin(i * 1.7f)))); }
            p.Stroke("Hamon", 2, new Color(1f, 40f / 255f, 60f / 255f, 0.7f), PartRole.Trim, ham, 0.7f, 1.3f);
            // the edge light, and its run round the hook
            var eg = new List<Vector2>();
            for (int i = 0; i <= 12; i++) { float u = 0.04f + 0.74f * i / 12f; eg.Add(V(u * L, Cy(u) + W(u) * 0.82f)); }
            p.Glow("Edge", eg, 0.65f, glow, hot, 1, 3);
            p.Glow("Hook", new[] { V(0.9f * L, Cy(0.9f) + W(0.9f) * 0.3f), V(0.93f * L, Cy(0.93f) + W(0.93f) * 1.7f), V(L, Cy(1) + 0.2f) }, 0.5f, glow, hot, 2, 3);
            // habaki (just past the tsuba) and the red moon mark
            p.Fill("Habaki", 2, Gold, PartRole.Metal, new[] { V(1.7f, -W(0)), V(3.6f, -W(0)), V(3.6f, W(0)), V(1.7f, W(0)) });
            float mx = 0.075f * L, my = Cy(0.075f);
            var moon = EllipsePts(mx, my, 1.15f, 1.15f, Mathf.PI * 0.3f, Mathf.PI * 1.7f, 10);
            moon.AddRange(EllipsePts(mx + 0.55f, my - 0.25f, 0.85f, 0.85f, Mathf.PI * 1.45f, Mathf.PI * 0.55f, 8));
            p.Fill("RedMoon", 3, glow, PartRole.Trim, moon, false, 1.5f);
        }

        // #10 Shizuka, "Silence"
        static void Shizuka(BladePen p)
        {
            const float L = 88f, hl = 0.3f;
            Color steel = Palette.Steel, lo = KMath.Hex("#6c7179"), glow = KMath.Hex("#a46bff"), hot = KMath.Hex("#eadcff");

            p.Grip("Tsuka", 0, -L * hl, -1.5f, 1.9f, KMath.Hex("#2f2738"), Gold, 6);
            // crescent pommel ring: a gold ring around an off-centre hole (one outline, bridged to the hole)
            float px = -L * hl - 2.2f;
            var ring = EllipsePts(px, 0f, 3f, 3f, 0f, Mathf.PI * 2f, 20);
            ring.Add(ring[0]);
            var hole = EllipsePts(px - 0.7f, -0.4f, 1.9f, 1.9f, 0f, -Mathf.PI * 2f, 16);
            hole.Add(hole[0]);
            ring.AddRange(hole);
            p.Fill("Pommel", 1, Gold, PartRole.Metal, ring);
            Tassel(p, -L * hl - 4.5f, 0.8f, 8f, KMath.Hex("#9c78d8"), 3);
            // the winged guard: a diamond boss with two forward-swept wings, each with a spur
            var gd = new[]
            {
                V(2.4f, 0), V(0.6f, -2.2f), V(2.6f, -5.4f), V(0.8f, -4.6f), V(-0.6f, -8.2f), V(-1.6f, -4.2f), V(-2.6f, -2.4f),
                V(-2.6f, 2.4f), V(-1.6f, 4.2f), V(-0.6f, 8.2f), V(0.8f, 4.6f), V(2.6f, 5.4f), V(0.6f, 2.2f),
            };
            p.Fill("Guard", 1, Gold, PartRole.Metal, gd, true);
            Outline(p, "GuardHi", 2, GoldHi, gd, 0.6f);
            p.Ellipse("Gem", 3, glow, PartRole.Trim, 0f, 0f, 1.1f, 1.1f, false, 1.5f, 10);

            float W(float u) => Mathf.Lerp(1.75f, 0.35f, Mathf.Pow(u, 1.4f));
            var spine = new List<Vector2> { V(0.03f, -W(0)), V(0.1f, -W(0.1f) - 0.5f), V(0.13f, -W(0.13f)) };   // a small ricasso shoulder
            for (int i = 1; i <= 8; i++) { float u = 0.13f + 0.82f * i / 8f; spine.Add(V(u, -W(u))); }
            spine.Add(V(1f, 0f));
            var edge = new List<Vector2> { V(0.03f, W(0)), V(0.1f, W(0.1f) + 0.5f), V(0.13f, W(0.13f)) };
            for (int i = 1; i <= 8; i++) { float u = 0.13f + 0.82f * i / 8f; edge.Add(V(u, W(u))); }
            var blade = BladePen.Outline(L, spine, edge);
            p.Fill("Blade", 0, steel, PartRole.Metal, blade, true);
            p.Fill("SpineFlat", 1, lo, PartRole.Metal, BladePen.ClipBelowY(blade, 0f));       // the spine flat in shadow
            p.Glow("Ridge", new[] { V(0.15f * L, 0f), V(0.97f * L, 0f) }, 0.5f, glow, hot, 2, 3);   // the violet ridge light
            p.Ellipse("Ricasso", 3, KMath.Hex("#d6d9dd"), PartRole.Metal, 0.1f * L, 0f, 0.7f, 0.7f, false, 1f, 8);
        }
    }
}
