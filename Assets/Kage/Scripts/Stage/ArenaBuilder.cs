using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// "Crimson Moon Shrine": an original stage in Kage's palette. Black silhouettes read against
    /// a blood-orange horizon and a pale moon; parallax layers (sky, moon, mountains, pagoda,
    /// torii, pines, lanterns, foreground grass), drifting leaves and embers. Physics: a static
    /// ground and two walls on the Ground layer.
    /// </summary>
    public static class ArenaBuilder
    {
        static Transform _root;

        static MeshRenderer Poly(string name, IList<Vector2> pts, Color c, int order, Transform parent = null, Material mat = null)
        {
            return MeshKit.MakeRenderer(name, parent != null ? parent : _root, MeshKit.Poly(pts, c, name), mat != null ? mat : MeshKit.Alpha, order);
        }

        static Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(_root, false);
            return t;
        }

        public static void Build(Transform root, CameraRig camRig)
        {
            _root = root;

            // ---------- physics ----------
            var ground = new GameObject("Ground");
            ground.transform.SetParent(root, false);
            ground.layer = KageGame.LayerGround;
            var gb = ground.AddComponent<BoxCollider2D>();
            gb.size = new Vector2(60f, 4f);
            gb.offset = new Vector2(0f, -2f);
            foreach (float sx in new[] { -1f, 1f })
            {
                var wall = new GameObject(sx < 0 ? "WallL" : "WallR");
                wall.transform.SetParent(root, false);
                wall.layer = KageGame.LayerGround;
                var wb = wall.AddComponent<BoxCollider2D>();
                wb.size = new Vector2(2f, 30f);
                wb.offset = new Vector2(sx * 8.9f, 12f);
            }

            // ---------- sky (follows the camera) ----------
            var sky = Group("Sky");
            var skyMesh = new Mesh { name = "SkyGradient" };
            float[] ys = { -6f, -0.6f, 0.9f, 2.4f, 4.2f, 7.5f, 14f };
            Color[] cs =
            {
                KMath.Hex("#7A2620"), KMath.Hex("#C9553A"), KMath.Hex("#9A3229"), KMath.Hex("#5A1A1E"),
                KMath.Hex("#2E0F14"), KMath.Hex("#160A0D"), KMath.Hex("#0B0507"),
            };
            var sv = new List<Vector3>(); var sc = new List<Color>(); var st = new List<int>();
            for (int i = 0; i < ys.Length; i++)
            {
                sv.Add(new Vector3(-30f, ys[i] - 1.4f)); sv.Add(new Vector3(30f, ys[i] - 1.4f));
                sc.Add(MeshKit.L(cs[i])); sc.Add(MeshKit.L(cs[i]));
                if (i > 0) { int k = i * 2; st.AddRange(new[] { k - 2, k, k - 1, k - 1, k, k + 1 }); }
            }
            skyMesh.SetVertices(sv); skyMesh.SetColors(sc); skyMesh.SetTriangles(st, 0); skyMesh.RecalculateBounds();
            MeshKit.MakeRenderer("SkyMesh", sky, skyMesh, MeshKit.Alpha, -3000);
            sky.position = new Vector3(0f, 1.4f, 0f);
            camRig.AddParallax(sky, 1f, 1f);

            // ---------- moon + halo + cloud wisps ----------
            var moon = Group("Moon");
            moon.position = new Vector3(2.4f, 3.7f, 0f);
            var halo = MeshKit.MakeRenderer("Halo", moon, MeshKit.Quad(9f, 9f, KMath.Hex("#E2603A").WithA(0.32f)), MeshKit.SoftAdditive, -2990);
            var moonMat = MeshKit.MakeMat(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, "Moon", null);
            moonMat.SetVector("_Color", new Vector4(1.12f, 1.08f, 1.05f, 1f));
            MeshKit.MakeRenderer("Disc", moon, MeshKit.Disc(Vector2.zero, 1.55f, KMath.Hex("#F6E6D8"), KMath.Hex("#EBC7B0"), 64), moonMat, -2985);
            // craters: faint darker blots
            MeshKit.MakeRenderer("Crater", moon, MeshKit.Disc(new Vector2(-0.45f, 0.35f), 0.32f, KMath.Hex("#E2BFAE").WithA(0.6f), KMath.Hex("#E2BFAE").WithA(0f)), MeshKit.Alpha, -2984);
            MeshKit.MakeRenderer("Crater", moon, MeshKit.Disc(new Vector2(0.5f, -0.2f), 0.45f, KMath.Hex("#E2BFAE").WithA(0.5f), KMath.Hex("#E2BFAE").WithA(0f)), MeshKit.Alpha, -2984);
            Color wisp = KMath.Hex("#2A0C10").WithA(0.78f);
            Poly("Wisp", Cloud(new Vector2(-0.8f, -0.55f), 2.6f, 0.16f, 3), wisp, -2980, moon);
            Poly("Wisp", Cloud(new Vector2(0.9f, -0.95f), 3.2f, 0.12f, 5), wisp, -2980, moon);
            Poly("Wisp", Cloud(new Vector2(-2.6f, 0.4f), 2.0f, 0.1f, 9), wisp.WithA(0.5f), -2980, moon);
            camRig.AddParallax(moon, 0.94f, 0.9f);

            // ---------- mountains ----------
            var far = Group("MountainsFar");
            Poly("Range", Ridge(-34f, 34f, 0.55f, 0.9f, 1.5f, 11), KMath.Hex("#5C2126"), -2900, far);
            camRig.AddParallax(far, 0.8f, 0.6f);
            var mid = Group("MountainsMid");
            Poly("Range", Ridge(-30f, 30f, 0.9f, 0.6f, 1.1f, 23), KMath.Hex("#3A141A"), -2850, mid);
            camRig.AddParallax(mid, 0.62f, 0.45f);

            // ---------- pagoda on the far hill ----------
            var pagoda = Group("Pagoda");
            pagoda.position = new Vector3(-4.6f, 0.95f, 0f);
            BuildPagoda(pagoda, KMath.Hex("#22090E"), -2800);
            camRig.AddParallax(pagoda, 0.5f, 0.35f);

            // ---------- horizon fog ----------
            var fog = Group("Fog");
            MeshKit.MakeRenderer("FogBand", fog, MeshKit.PolyGradient(new[] { new Vector2(-40, -0.4f), new Vector2(40, -0.4f), new Vector2(40, 1.3f), new Vector2(-40, 1.3f) },
                KMath.Hex("#D45A3C").WithA(0.32f), KMath.Hex("#D45A3C").WithA(0f)), MeshKit.Additive, -2700);
            camRig.AddParallax(fog, 0.4f, 0.2f);

            // ---------- torii gate ----------
            var torii = Group("Torii");
            torii.position = new Vector3(4.6f, 0f, 0f);
            BuildTorii(torii, KMath.Hex("#170709"), -2600);
            camRig.AddParallax(torii, 0.28f, 0.1f);

            // ---------- pines ----------
            var pines = Group("Pines");
            BuildPine(pines, new Vector2(-8.2f, 0f), 4.6f, KMath.Hex("#12060A"), -2500, 3);
            BuildPine(pines, new Vector2(9.0f, 0f), 5.2f, KMath.Hex("#12060A"), -2500, 7);
            BuildPine(pines, new Vector2(-12.5f, 0f), 3.8f, KMath.Hex("#1A080C"), -2510, 11);
            camRig.AddParallax(pines, 0.15f, 0.05f);

            // ---------- ground ----------
            var floor = Group("Floor");
            Poly("Earth", new[] { new Vector2(-40, 0.0f), new Vector2(40, 0.0f), new Vector2(40, -9f), new Vector2(-40, -9f) }, KMath.Hex("#0E0709"), -1200, floor);
            MeshKit.MakeRenderer("FloorLight", floor, MeshKit.PolyGradient(new[] { new Vector2(-40, -0.5f), new Vector2(40, -0.5f), new Vector2(40, 0.02f), new Vector2(-40, 0.02f) },
                KMath.Hex("#0E0709").WithA(0f), KMath.Hex("#4A1A1D")), MeshKit.Alpha, -1190);
            Poly("Grass", GrassEdge(-30f, 30f, 0.0f, 0.12f, 5), KMath.Hex("#0E0709"), -1180, floor);

            // stone lanterns in the fighting plane (behind the fighters)
            BuildLantern(floor, new Vector2(-6.4f, 0f), -1100);
            BuildLantern(floor, new Vector2(6.6f, 0f), -1100);

            // ---------- foreground ----------
            var fg = Group("Foreground");
            Poly("FgGrassL", GrassEdge(-16f, -4.2f, -0.35f, 0.55f, 13), KMath.Hex("#050203"), 6000, fg);
            Poly("FgGrassR", GrassEdge(4.4f, 16f, -0.35f, 0.6f, 17), KMath.Hex("#050203"), 6000, fg);
            camRig.AddParallax(fg, -0.25f, 0f);

            BuildBranch(camRig.transform, 5900);

            // ---------- ambient particles ----------
            BuildLeaves(camRig.transform);
        }

        // =======================================================================================
        static List<Vector2> Ridge(float x0, float x1, float baseY, float amp, float step, int seed)
        {
            var pts = new List<Vector2> { new(x0, -2f) };
            for (float x = x0; x <= x1; x += step)
            {
                float n = Mathf.PerlinNoise(x * 0.18f + seed, seed * 0.37f);
                float peak = Mathf.Pow(n, 1.6f) * amp * 2.2f + Mathf.PerlinNoise(x * 0.9f, seed) * amp * 0.25f;
                pts.Add(new Vector2(x, baseY + peak));
            }
            pts.Add(new Vector2(x1, -2f));
            return pts;
        }

        static List<Vector2> Cloud(Vector2 c, float len, float h, int seed)
        {
            var pts = new List<Vector2>();
            const int n = 18;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float x = c.x - len / 2 + len * t;
                float w = Mathf.Sin(t * Mathf.PI) * h * (0.7f + 0.6f * Mathf.PerlinNoise(t * 4f, seed));
                pts.Add(new Vector2(x, c.y + w));
            }
            for (int i = n; i >= 0; i--)
            {
                float t = i / (float)n;
                float x = c.x - len / 2 + len * t;
                float w = Mathf.Sin(t * Mathf.PI) * h * 0.35f;
                pts.Add(new Vector2(x, c.y - w));
            }
            return pts;
        }

        static List<Vector2> GrassEdge(float x0, float x1, float y, float h, int seed)
        {
            var pts = new List<Vector2> { new(x0, y - 1.5f) };
            int i = 0;
            for (float x = x0; x < x1; x += 0.07f, i++)
            {
                float hh = h * (0.25f + Mathf.PerlinNoise(x * 1.7f, seed) * 0.9f) * (i % 3 == 0 ? 1f : 0.45f);
                pts.Add(new Vector2(x, y));
                pts.Add(new Vector2(x + 0.035f + Mathf.Sin(x * 3f) * 0.02f, y + hh));
            }
            pts.Add(new Vector2(x1, y));
            pts.Add(new Vector2(x1, y - 1.5f));
            return pts;
        }

        static void BuildPagoda(Transform t, Color c, int order)
        {
            Poly("Hill", new[] { new Vector2(-3.5f, -1.2f), new Vector2(-1.4f, 0.1f), new Vector2(0f, 0.22f), new Vector2(1.6f, 0.05f), new Vector2(3.8f, -1.2f) }, c, order, t);
            float y = 0.2f, w = 1.1f;
            for (int tier = 0; tier < 4; tier++)
            {
                float bh = 0.42f - tier * 0.05f;
                Poly("Wall", new[] { new Vector2(-w * 0.55f, y), new Vector2(w * 0.55f, y), new Vector2(w * 0.5f, y + bh), new Vector2(-w * 0.5f, y + bh) }, c, order, t);
                y += bh;
                float rw = w * 1.05f;
                Poly("Roof", new[]
                {
                    new Vector2(-rw, y + 0.08f), new Vector2(-rw * 0.75f, y - 0.02f), new Vector2(rw * 0.75f, y - 0.02f), new Vector2(rw, y + 0.08f),
                    new Vector2(rw * 0.55f, y + 0.12f), new Vector2(0f, y + 0.2f), new Vector2(-rw * 0.55f, y + 0.12f),
                }, c, order, t);
                y += 0.16f;
                w *= 0.8f;
            }
            Poly("Spire", new[] { new Vector2(-0.03f, y), new Vector2(0.03f, y), new Vector2(0.012f, y + 0.6f), new Vector2(-0.012f, y + 0.6f) }, c, order, t);
        }

        static void BuildTorii(Transform t, Color c, int order)
        {
            float half = 1.25f, h = 3.3f;
            foreach (float s in new[] { -1f, 1f })
            {
                Poly("Pillar", new[] { new Vector2(s * half - 0.11f, 0f), new Vector2(s * half + 0.11f, 0f), new Vector2(s * half + 0.085f, h), new Vector2(s * half - 0.085f, h) }, c, order, t);
                Poly("Base", new[] { new Vector2(s * half - 0.17f, 0f), new Vector2(s * half + 0.17f, 0f), new Vector2(s * half + 0.15f, 0.22f), new Vector2(s * half - 0.15f, 0.22f) }, c, order, t);
            }
            Poly("Nuki", new[] { new Vector2(-half - 0.35f, h - 0.62f), new Vector2(half + 0.35f, h - 0.62f), new Vector2(half + 0.35f, h - 0.48f), new Vector2(-half - 0.35f, h - 0.48f) }, c, order, t);
            Poly("Gakuzuka", new[] { new Vector2(-0.07f, h - 0.5f), new Vector2(0.07f, h - 0.5f), new Vector2(0.07f, h), new Vector2(-0.07f, h) }, c, order, t);
            var kasagi = new List<Vector2>();
            for (int i = 0; i <= 16; i++)
            {
                float u = i / 16f, x = Mathf.Lerp(-half - 0.75f, half + 0.75f, u);
                float lift = Mathf.Pow(Mathf.Abs(u * 2f - 1f), 3f) * 0.32f;
                kasagi.Add(new Vector2(x, h + 0.02f + lift));
            }
            for (int i = 16; i >= 0; i--)
            {
                float u = i / 16f, x = Mathf.Lerp(-half - 0.62f, half + 0.62f, u);
                float lift = Mathf.Pow(Mathf.Abs(u * 2f - 1f), 3f) * 0.38f;
                kasagi.Add(new Vector2(x, h + 0.26f + lift));
            }
            Poly("Kasagi", kasagi, c, order, t);
        }

        static void BuildPine(Transform parent, Vector2 at, float height, Color c, int order, int seed)
        {
            var t = new GameObject("Pine").transform;
            t.SetParent(parent, false);
            t.position = at;
            float lean = (KMath.Hash(seed) - 0.5f) * 0.8f;
            Poly("Trunk", new[] { new Vector2(-0.16f, 0f), new Vector2(0.16f, 0f), new Vector2(0.06f + lean, height), new Vector2(-0.04f + lean, height) }, c, order, t);
            for (int i = 0; i < 5; i++)
            {
                float y = height * (0.45f + i * 0.13f);
                float w = (1.6f - i * 0.22f) * (0.8f + 0.4f * KMath.Hash(seed * 7 + i));
                float side = i % 2 == 0 ? -1f : 1f;
                Vector2 cc = new Vector2(lean * (y / height) + side * w * 0.25f, y);
                Poly("Canopy", Cloud(cc, w * 1.6f, 0.38f, seed + i), c, order, t);
                Poly("Branch", new[] { new Vector2(lean * (y / height), y - 0.25f), new Vector2(cc.x, y - 0.02f), new Vector2(cc.x, y + 0.04f), new Vector2(lean * (y / height), y - 0.15f) }, c, order, t);
            }
        }

        static void BuildLantern(Transform parent, Vector2 at, int order)
        {
            var t = new GameObject("Lantern").transform;
            t.SetParent(parent, false);
            t.position = at;
            Color stone = KMath.Hex("#0C0608");
            Poly("Base", new[] { new Vector2(-0.24f, 0f), new Vector2(0.24f, 0f), new Vector2(0.2f, 0.12f), new Vector2(-0.2f, 0.12f) }, stone, order, t);
            Poly("Pole", new[] { new Vector2(-0.07f, 0.1f), new Vector2(0.07f, 0.1f), new Vector2(0.06f, 0.72f), new Vector2(-0.06f, 0.72f) }, stone, order, t);
            Poly("Seat", new[] { new Vector2(-0.2f, 0.7f), new Vector2(0.2f, 0.7f), new Vector2(0.17f, 0.8f), new Vector2(-0.17f, 0.8f) }, stone, order, t);
            Poly("Box", new[] { new Vector2(-0.15f, 0.8f), new Vector2(0.15f, 0.8f), new Vector2(0.15f, 1.06f), new Vector2(-0.15f, 1.06f) }, stone, order, t);
            var glowMat = MeshKit.MakeMat(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, "LanternGlow", null);
            glowMat.SetVector("_Color", new Vector4(2.2f, 1.6f, 1.1f, 1f));
            Poly("Window", new[] { new Vector2(-0.08f, 0.85f), new Vector2(0.08f, 0.85f), new Vector2(0.08f, 1.0f), new Vector2(-0.08f, 1.0f) }, KMath.Hex("#FF8A3C"), order + 1, t, glowMat);
            MeshKit.MakeRenderer("Glow", t, MeshKit.Quad(1.6f, 1.6f, KMath.Hex("#FF7A30").WithA(0.35f)), MeshKit.SoftAdditive, order + 2).transform.localPosition = new Vector3(0f, 0.93f, 0f);
            Poly("Roof", new[] { new Vector2(-0.34f, 1.06f), new Vector2(0.34f, 1.06f), new Vector2(0.22f, 1.16f), new Vector2(0.05f, 1.24f), new Vector2(-0.05f, 1.24f), new Vector2(-0.22f, 1.16f) }, stone, order, t);
            Poly("Tip", new[] { new Vector2(-0.04f, 1.22f), new Vector2(0.04f, 1.22f), new Vector2(0.02f, 1.34f), new Vector2(-0.02f, 1.34f) }, stone, order, t);
        }

        static void BuildBranch(Transform cam, int order)
        {
            var t = new GameObject("MapleBranch").transform;
            t.SetParent(cam, false);
            t.localPosition = new Vector3(-4.3f, 2.3f, 10f);
            Color wood = KMath.Hex("#050203");
            Poly("Bough", new[] { new Vector2(-1.2f, 0.6f), new Vector2(0.4f, 0.1f), new Vector2(1.6f, -0.25f), new Vector2(1.62f, -0.2f), new Vector2(0.45f, 0.18f), new Vector2(-1.2f, 0.75f) }, wood, order, t);
            Poly("Twig", new[] { new Vector2(0.4f, 0.12f), new Vector2(0.9f, -0.35f), new Vector2(0.93f, -0.33f), new Vector2(0.44f, 0.16f) }, wood, order, t);
            var leafCol = new[] { KMath.Hex("#8C1E22"), KMath.Hex("#B3282B"), KMath.Hex("#5E1418") };
            for (int i = 0; i < 16; i++)
            {
                float u = i / 15f;
                Vector2 c = Vector2.Lerp(new Vector2(-0.8f, 0.5f), new Vector2(1.55f, -0.25f), u) + new Vector2(KMath.Hash(i * 3) * 0.3f - 0.15f, KMath.Hash(i * 5) * 0.3f - 0.22f);
                if (i % 4 == 1) c = new Vector2(0.9f, -0.38f) + new Vector2(KMath.Hash(i) * 0.2f, -KMath.Hash(i + 9) * 0.2f);
                Poly("Leaf", MapleLeaf(c, 0.13f + KMath.Hash(i * 11) * 0.06f, KMath.Hash(i * 13) * 360f), leafCol[i % 3], order + 1 + (i % 2), t);
            }
        }

        public static List<Vector2> MapleLeaf(Vector2 c, float r, float rot)
        {
            var pts = new List<Vector2>();
            int lobes = 5;
            for (int i = 0; i < lobes * 2; i++)
            {
                float a = (rot + i * 180f / lobes) * Mathf.Deg2Rad;
                float rr = i % 2 == 0 ? r : r * 0.45f;
                pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr);
            }
            return pts;
        }

        static void BuildLeaves(Transform cam)
        {
            var go = new GameObject("FallingLeaves");
            go.transform.SetParent(cam, false);
            go.transform.localPosition = new Vector3(0f, 4.2f, 10f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(KMath.Hex("#7E1A1E"), KMath.Hex("#C23A33"));
            main.gravityModifier = 0.025f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            var em = ps.emission;
            em.rateOverTime = 5f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 0.2f, 0.1f);
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 0.35f;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.6f, -0.2f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.3f, -0.15f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = MeshKit.MakeMat(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, "Leaf", LeafTexture());
            r.sortingOrder = 5800;
            ps.Play();

            var eg = new GameObject("Embers");
            eg.transform.SetParent(cam, false);
            eg.transform.localPosition = new Vector3(0f, -2.4f, 10f);
            var eps = eg.AddComponent<ParticleSystem>();
            eps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var em2 = eps.main;
            em2.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            em2.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            em2.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
            em2.startColor = new ParticleSystem.MinMaxGradient(KMath.Hex("#FF6A3A"), KMath.Hex("#FFB070"));
            em2.simulationSpace = ParticleSystemSimulationSpace.World;
            em2.gravityModifier = -0.02f;
            var ee = eps.emission;
            ee.rateOverTime = 7f;
            var es = eps.shape;
            es.shapeType = ParticleSystemShapeType.Box;
            es.scale = new Vector3(16f, 0.5f, 0.1f);
            var en = eps.noise;
            en.enabled = true;
            en.strength = 0.35f;
            en.frequency = 0.6f;
            var ecol = eps.colorOverLifetime;
            ecol.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0f, 1f) });
            ecol.color = g;
            var er = eg.GetComponent<ParticleSystemRenderer>();
            var em3 = MeshKit.MakeMat(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One, "Ember", MeshKit.SoftDot);
            em3.SetVector("_Color", new Vector4(2.2f, 2.2f, 2.2f, 1f));
            er.sharedMaterial = em3;
            er.sortingOrder = 5700;
            eps.Play();
        }

        static Texture2D LeafTexture()
        {
            const int n = 32;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "LeafTex", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Atan2(v, u), r = Mathf.Sqrt(u * u + v * v);
                float lobe = 0.62f + 0.3f * Mathf.Pow(Mathf.Abs(Mathf.Cos(a * 2.5f)), 3f);
                float alpha = Mathf.Clamp01((lobe - r) * 12f);
                if (Mathf.Abs(u) < 0.05f && v < 0f && v > -0.95f) alpha = Mathf.Max(alpha, 0.9f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
