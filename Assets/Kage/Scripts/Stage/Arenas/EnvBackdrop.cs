using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// A boss arena's backdrop painted in code from an <see cref="EnvLook"/> (SAN-103): the placeholder until the stage art pass. Flat meshes
    /// from <see cref="MeshKit"/> in parallax layers on the <see cref="CameraRig"/>, back to front: the sky and its stars (fixed to the camera),
    /// the moon and clouds, a far city of blocks and pagoda roofs with a few lit windows, a haze band, a string of paper lanterns, near tiled
    /// roof ridges, and the tiled roof the fighters stand on. It follows the layout of the JS environment it is named after
    /// (<c>ENVS.rooftops</c>: sky, far, lanterns, mid, floor), so every env that shares that layout (the districts) is one more
    /// <see cref="EnvLook"/>, not more code. Visual only: the physics is the dev arena's (<see cref="ArenaBuilder.BuildPhysics"/>).
    /// Seeded by the env id, so an arena looks the same every time.
    /// </summary>
    public static class EnvBackdrop
    {
        /// <summary>Sorting orders, back to front; all behind the fighters, like the shrine's.</summary>
        const int OrderSky = -3000, OrderStars = -2995, OrderHalo = -2990, OrderMoon = -2985, OrderCloud = -2980, OrderFar = -2900, OrderWindows = -2895,
            OrderFog = -2700, OrderLanterns = -2650, OrderRoofs = -2600, OrderWash = -2550, OrderFloor = -1200;

        /// <summary>Paints the backdrop under <paramref name="root"/> (a child named "Backdrop_&lt;env&gt;") and registers its parallax layers.</summary>
        public static Transform Build(Transform root, CameraRig cam, EnvLook look)
        {
            var top = new GameObject("Backdrop_" + look.id).transform;
            top.SetParent(root, false);
            int seed = Seed(look.id);

            // ---------- sky and stars: fixed to the camera ----------
            var sky = Group(top, "Sky", new Vector2(0f, 1.4f));
            var skyMesh = new Mesh { name = "SkyGradient" };
            float[] ys = { -4f, 0.2f, 2.6f, 9f };
            Color[] cs = { look.skyLow, look.skyLow, look.skyMid, look.skyTop };
            var sv = new List<Vector3>(); var sc = new List<Color>(); var st = new List<int>();
            for (int i = 0; i < ys.Length; i++)
            {
                sv.Add(new Vector3(-30f, ys[i] - 1.4f)); sv.Add(new Vector3(30f, ys[i] - 1.4f));
                sc.Add(MeshKit.L(cs[i])); sc.Add(MeshKit.L(cs[i]));
                if (i > 0) { int k = i * 2; st.AddRange(new[] { k - 2, k, k - 1, k - 1, k, k + 1 }); }
            }
            skyMesh.SetVertices(sv); skyMesh.SetColors(sc); skyMesh.SetTriangles(st, 0); skyMesh.RecalculateBounds();
            MeshKit.MakeRenderer("SkyMesh", sky, skyMesh, MeshKit.Alpha, OrderSky);
            var stars = new List<(Vector2[], Color)>();
            for (int i = 0; i < 90; i++)
            {
                Vector2 c = new(Mathf.Lerp(-12f, 12f, H(seed, i * 3)), Mathf.Lerp(0.6f, 4.6f, H(seed, i * 3 + 1)));
                float s = 0.012f + H(seed, i * 3 + 2) * 0.012f;
                stars.Add((Rect(c.x - s, c.y - s, c.x + s, c.y + s), look.star.WithA(look.star.a * (0.3f + H(seed, i * 7) * 0.7f))));
            }
            MeshKit.MakeRenderer("Stars", sky, MeshKit.PolyMulti(stars, Vector2.zero, "Stars"), MeshKit.Alpha, OrderStars);
            cam.AddParallax(sky, 1f, 1f);

            // ---------- moon and clouds ----------
            var moon = Group(top, "Moon", new Vector2(2.6f, 3.1f));
            MeshKit.MakeRenderer("Halo", moon, MeshKit.Quad(4.6f, 4.6f, look.moonHalo), MeshKit.SoftAdditive, OrderHalo);
            MeshKit.MakeRenderer("Disc", moon, MeshKit.Disc(Vector2.zero, 0.62f, look.moon, look.moon, 48), MeshKit.Alpha, OrderMoon);
            var marks = Color.Lerp(look.moon, look.skyMid, 0.25f);
            MeshKit.MakeRenderer("Mark", moon, MeshKit.Disc(new Vector2(-0.17f, 0.13f), 0.12f, marks, marks, 16), MeshKit.Alpha, OrderMoon + 1);
            MeshKit.MakeRenderer("Mark", moon, MeshKit.Disc(new Vector2(0.2f, -0.16f), 0.08f, marks, marks, 16), MeshKit.Alpha, OrderMoon + 1);
            var clouds = new List<(Vector2[], Color)>();
            for (int i = 0; i < 6; i++)
            {
                Vector2 c = new(Mathf.Lerp(-7f, 7f, H(seed, 100 + i)) - 2.6f, Mathf.Lerp(-0.9f, 0.9f, H(seed, 110 + i)));
                clouds.Add((Ellipse(c, 1.4f + H(seed, 120 + i) * 2.2f, 0.12f, 20), look.cloud));
            }
            MeshKit.MakeRenderer("Clouds", moon, MeshKit.PolyMulti(clouds, Vector2.zero, "Clouds"), MeshKit.Alpha, OrderCloud);
            cam.AddParallax(moon, 0.94f, 0.9f);

            // ---------- the far city: blocks, pagoda roofs, a few gold windows ----------
            var far = Group(top, "FarCity", Vector2.zero);
            var blocks = new List<(Vector2[], Color)>();
            var windows = new List<(Vector2[], Color)>();
            int n = 0;
            for (float x = -20f; x < 20f; n++)
            {
                float w = 0.35f + H(seed, 200 + n) * 0.8f, h = 0.75f + H(seed, 300 + n) * 1.45f, b = 0.15f;
                blocks.Add((Rect(x, b - 1f, x + w, b + h), look.far));
                if (H(seed, 400 + n) < 0.45f)
                    for (int t = 0; t < 3; t++)
                    {
                        float ty = b + h + t * 0.22f, ww = w + 0.26f - t * 0.12f, cx = x + w * 0.5f;
                        blocks.Add((Roof(cx, ty, ww * 0.5f + 0.08f), look.far));
                        if (t < 2) blocks.Add((Rect(cx - ww * 0.32f, ty, cx + ww * 0.32f, ty + 0.2f), look.far));
                    }
                for (int k = 0; k < 6; k++)
                    if (H(seed, 500 + n * 6 + k) < 0.3f)
                    {
                        float wx = x + 0.05f + H(seed, 600 + n * 6 + k) * (w - 0.1f), wy = b + 0.15f + H(seed, 700 + n * 6 + k) * h * 0.75f;
                        windows.Add((Rect(wx, wy, wx + 0.026f, wy + 0.036f), look.farWindow.WithA(look.farWindow.a * (0.5f + H(seed, 800 + n) * 0.5f))));
                    }
                x += w + 0.07f + H(seed, 900 + n) * 0.26f;
            }
            MeshKit.MakeRenderer("Blocks", far, MeshKit.PolyMulti(blocks, Vector2.zero, "FarCity"), MeshKit.Alpha, OrderFar);
            MeshKit.MakeRenderer("Windows", far, MeshKit.PolyMulti(windows, Vector2.zero, "FarWindows"), MeshKit.Alpha, OrderWindows);
            cam.AddParallax(far, 0.82f, 0.6f);

            // ---------- haze ----------
            var fog = Group(top, "Haze", Vector2.zero);
            MeshKit.MakeRenderer("Band", fog, MeshKit.PolyGradient(new[] { new Vector2(-40f, -0.4f), new Vector2(40f, -0.4f), new Vector2(40f, 1.5f), new Vector2(-40f, 1.5f) },
                look.fog, look.fog.WithA(0f)), MeshKit.Alpha, OrderFog);
            cam.AddParallax(fog, 0.6f, 0.3f);

            // ---------- a string of paper lanterns ----------
            var lanterns = Group(top, "Lanterns", Vector2.zero);
            var lamp = new List<(Vector2[], Color)>();
            var glow = new List<(Vector2[], Color)>();
            Vector2 prev = default;
            for (int i = 0; i < 12; i++)
            {
                Vector2 p = new(-14f + i * 2.6f + H(seed, 1000 + i) * 0.5f, 2.55f + H(seed, 1010 + i) * 0.5f);
                if (i > 0) lamp.Add((Sag(prev + Vector2.up * 0.25f, p + Vector2.up * 0.25f, 0.28f, 0.012f), look.rope));
                lamp.Add((Rect(p.x - 0.005f, p.y + 0.1f, p.x + 0.005f, p.y + 0.25f), look.rope));
                lamp.Add((Ellipse(p, 0.07f, 0.1f, 14), look.lantern));
                lamp.Add((Rect(p.x - 0.045f, p.y + 0.09f, p.x + 0.045f, p.y + 0.115f), look.roofs));
                lamp.Add((Rect(p.x - 0.045f, p.y - 0.115f, p.x + 0.045f, p.y - 0.09f), look.roofs));
                glow.Add((Ellipse(p, 0.22f, 0.22f, 16), look.lanternGlow));
                prev = p;
            }
            MeshKit.MakeRenderer("Glow", lanterns, MeshKit.PolyMulti(glow, Vector2.zero, "LanternGlow"), MeshKit.Additive, OrderLanterns - 1);
            MeshKit.MakeRenderer("String", lanterns, MeshKit.PolyMulti(lamp, Vector2.zero, "Lanterns"), MeshKit.Alpha, OrderLanterns);
            cam.AddParallax(lanterns, 0.4f, 0.15f);

            // ---------- near roof ridges ----------
            var roofs = Group(top, "Roofs", Vector2.zero);
            var ridges = new List<(Vector2[], Color)>();
            n = 0;
            for (float x = -18f; x < 18f; n++)
            {
                float w = 1.4f + H(seed, 1100 + n) * 1.8f, topY = 0.32f + H(seed, 1200 + n) * 0.28f;
                ridges.Add((Ridge(x, x + w, topY), look.roofs));
                for (float k = x + 0.1f; k < x + w - 0.05f; k += 0.08f)
                {
                    float u = (k - x) / w, y = topY - 0.04f - 0.3f * (2f * u - 1f) * (2f * u - 1f) * 0.6f;
                    ridges.Add((Line(new Vector2(k, y), new Vector2(k - 0.05f, y - 0.32f), 0.006f), look.roofLines));
                }
                x += w + 0.5f + H(seed, 1300 + n) * 0.5f;
            }
            MeshKit.MakeRenderer("Ridges", roofs, MeshKit.PolyMulti(ridges, Vector2.zero, "Roofs"), MeshKit.Alpha, OrderRoofs);
            cam.AddParallax(roofs, 0.55f, 0.2f);

            if (look.wash.a > 0f)
            {
                var wash = Group(top, "Wash", new Vector2(0f, 1.4f));
                MeshKit.MakeRenderer("Wash", wash, MeshKit.Quad(80f, 30f, look.wash), MeshKit.Alpha, OrderWash);
                cam.AddParallax(wash, 1f, 1f);
            }

            // ---------- the roof the fight is on ----------
            var floor = Group(top, "Floor", Vector2.zero);
            MeshKit.MakeRenderer("Tiles", floor, MeshKit.PolyGradient(new[] { new Vector2(-40f, -6f), new Vector2(40f, -6f), new Vector2(40f, 0f), new Vector2(-40f, 0f) },
                look.floorBottom, look.floorTop), MeshKit.Alpha, OrderFloor);
            var seams = new List<(Vector2[], Color)>();
            for (float x = -30f; x <= 30f; x += 0.36f) seams.Add((Line(new Vector2(x, 0f), new Vector2(x * 1.35f, -3f), 0.01f), look.floorLines));
            float dy = 0.12f;
            for (float y = -0.12f; y > -4f; dy += 0.035f, y -= dy) seams.Add((Rect(-40f, y - 0.005f, 40f, y + 0.005f), look.floorLines));
            seams.Add((Rect(-40f, -0.012f, 40f, 0.012f), look.floorEdge));
            MeshKit.MakeRenderer("Seams", floor, MeshKit.PolyMulti(seams, Vector2.zero, "FloorSeams"), MeshKit.Alpha, OrderFloor + 1);
            return top;
        }

        // ------------------------------------------------------------------------------------ shapes

        static Transform Group(Transform parent, string name, Vector2 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = at;
            return t;
        }

        static int Seed(string id)
        {
            int s = 17;
            foreach (char ch in id) s = s * 31 + ch;
            return s & 0x7fff;
        }

        static float H(int seed, int i) => KMath.Hash(seed * 7919 + i);

        static Vector2[] Rect(float x0, float y0, float x1, float y1) => new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) };

        static Vector2[] Ellipse(Vector2 c, float rx, float ry, int seg)
        {
            var p = new Vector2[seg];
            for (int i = 0; i < seg; i++) { float a = i * Mathf.PI * 2f / seg; p[i] = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry); }
            return p;
        }

        /// <summary>A curved pagoda roof tier centred on <paramref name="cx"/>, its eaves at <paramref name="y"/>, <paramref name="half"/> wide each side.</summary>
        static Vector2[] Roof(float cx, float y, float half)
        {
            var p = new List<Vector2>();
            for (int i = 0; i <= 10; i++) { float u = i / 10f * 2f - 1f; p.Add(new Vector2(cx + u * half, y + 0.03f + 0.17f * (1f - u * u) + 0.05f * Mathf.Pow(Mathf.Abs(u), 6f))); }
            p.Add(new Vector2(cx + half * 0.85f, y - 0.01f));
            p.Add(new Vector2(cx - half * 0.85f, y - 0.01f));
            return p.ToArray();
        }

        /// <summary>A low curved roof ridge from x0 to x1 with its crest at <paramref name="topY"/>, down to below the floor.</summary>
        static Vector2[] Ridge(float x0, float x1, float topY)
        {
            var p = new List<Vector2> { new(x0 - 0.2f, -1f) };
            for (int i = 0; i <= 12; i++)
            {
                float u = i / 12f, x = Mathf.Lerp(x0 - 0.2f, x1 + 0.2f, u), k = 2f * u - 1f;
                p.Add(new Vector2(x, topY - 0.18f * k * k + 0.03f * Mathf.Pow(Mathf.Abs(k), 8f)));
            }
            p.Add(new Vector2(x1 + 0.2f, -1f));
            return p.ToArray();
        }

        /// <summary>A rope hanging from a to b, <paramref name="sag"/> metres low in the middle, <paramref name="w"/> thick.</summary>
        static Vector2[] Sag(Vector2 a, Vector2 b, float sag, float w)
        {
            var top = new List<Vector2>();
            var bottom = new List<Vector2>();
            for (int i = 0; i <= 10; i++)
            {
                float u = i / 10f;
                Vector2 q = Vector2.Lerp(a, b, u) + Vector2.down * (sag * 4f * u * (1f - u));
                top.Add(q + Vector2.up * w);
                bottom.Insert(0, q - Vector2.up * w);
            }
            top.AddRange(bottom);
            return top.ToArray();
        }

        static Vector2[] Line(Vector2 a, Vector2 b, float w)
        {
            Vector2 n = Vector2.Perpendicular((b - a).normalized) * w;
            return new[] { a + n, b + n, b - n, a - n };
        }
    }
}
