using System.Collections.Generic;
using UnityEngine;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The road of a stretch, built in code (SAN-89): the floor Kage walks (a ground collider the length of the road and two walls
    /// that follow the fight's bounds, <see cref="StageBounds"/>), and for the screen a night road in the dev arena's palette with
    /// simple parallax (sky, moon, two mountain ridges, a pine line and a torii line at their own speeds), props standing on the
    /// road (lanterns, fence posts) and a gate at each encounter that is shut while its encounter is on. Everything is meshes and
    /// colliders made here from <see cref="MeshKit"/>; no imported art. <see cref="BuildPhysics"/> alone is the headless harness's floor.
    /// </summary>
    public sealed class StretchRoad
    {
        /// <summary>The wall's inner face stands this far past the bound: the push collider's half width, as in the dev arena (7.6 to 7.9 m).</summary>
        public const float WallGap = 0.3f;

        public readonly Transform Root;
        public readonly StretchData Data;
        readonly StageBounds _bounds;
        Transform _wallL, _wallR;
        readonly List<GameObject> _barriers = new();
        readonly List<GameObject> _beams = new();

        StretchRoad(Transform root, StretchData data, StageBounds bounds)
        {
            Root = root;
            Data = data;
            _bounds = bounds;
        }

        /// <summary>The floor only: ground along the road and the two walls at the fight's bounds. Follows <paramref name="bounds"/> as it changes.</summary>
        public static StretchRoad BuildPhysics(Transform parent, StretchData data, StageBounds bounds)
        {
            var root = new GameObject("Road").transform;
            root.SetParent(parent, false);
            var r = new StretchRoad(root, data, bounds);
            r.MakePhysics();
            return r;
        }

        /// <summary>The floor and everything on screen: the road for the dev arena and the campaign. <paramref name="cam"/> carries the parallax layers.</summary>
        public static StretchRoad Build(Transform parent, StretchData data, StageBounds bounds, CameraRig cam)
        {
            var r = BuildPhysics(parent, data, bounds);
            r.MakeScenery(cam);
            return r;
        }

        void MakeSolid(string name, Transform parent, Vector2 size, Vector2 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = KageGame.LayerGround;
            var b = go.AddComponent<BoxCollider2D>();
            b.size = size;
            b.offset = offset;
        }

        void MakePhysics()
        {
            float len = (float)Data.Length;
            var ground = new GameObject("Ground");
            ground.transform.SetParent(Root, false);
            ground.transform.localPosition = new Vector3(len * 0.5f, 0f, 0f);
            ground.layer = KageGame.LayerGround;
            var gb = ground.AddComponent<BoxCollider2D>();
            gb.size = new Vector2(len + 40f, 4f);
            gb.offset = new Vector2(0f, -2f);
            _wallL = Wall("WallL");
            _wallR = Wall("WallR");
            PlaceWalls();
            _bounds.Changed += PlaceWalls;
        }

        Transform Wall(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.layer = KageGame.LayerGround;
            var b = go.AddComponent<BoxCollider2D>();
            b.size = new Vector2(2f, 30f);
            b.offset = new Vector2(0f, 12f);
            return go.transform;
        }

        void PlaceWalls()
        {
            if (_wallL == null || _wallR == null) return;
            _wallL.position = new Vector3(_bounds.Left - WallGap - 1f, 0f, 0f);
            _wallR.position = new Vector3(_bounds.Right + WallGap + 1f, 0f, 0f);
        }

        /// <summary>Removes the road's hooks (the walls stop following the bounds). The objects go with the road's parent.</summary>
        public void Dispose()
        {
            _bounds.Changed -= PlaceWalls;
        }

        // ---------------- gates ----------------

        /// <summary>Whether the gate of encounter <paramref name="i"/> is drawn shut. Bind it to <see cref="StretchSession.GateChanged"/>.</summary>
        public void SetGate(int i, bool closed)
        {
            if (i < 0 || i >= _barriers.Count) return;
            _barriers[i].SetActive(closed);
            _beams[i].SetActive(closed);
        }

        /// <summary>The gate is drawn from the session: shut while its encounter is on.</summary>
        public void Bind(StretchSession session)
        {
            session.GateChanged += SetGate;
            for (int i = 0; i < _barriers.Count; i++) SetGate(i, session.State == StretchState.Encounter && session.EncounterNumber == i + 1);
        }

        // ---------------- scenery ----------------

        void MakeScenery(CameraRig cam)
        {
            float len = (float)Data.Length;
            var scenery = new GameObject("Scenery").transform;
            scenery.SetParent(Root, false);

            // sky and moon follow the camera
            var sky = Group(scenery, "Sky");
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
            cam.AddParallax(sky, 1f, 1f);

            var moon = Group(scenery, "Moon");
            moon.position = new Vector3(2.4f, 3.7f, 0f);
            MeshKit.MakeRenderer("Halo", moon, MeshKit.Quad(9f, 9f, KMath.Hex("#E2603A").WithA(0.32f)), MeshKit.SoftAdditive, -2990);
            var moonMat = MeshKit.MakeMat(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, "Moon", null);
            moonMat.SetVector("_Color", new Vector4(1.12f, 1.08f, 1.05f, 1f));
            MeshKit.MakeRenderer("Disc", moon, MeshKit.Disc(Vector2.zero, 1.55f, KMath.Hex("#F6E6D8"), KMath.Hex("#EBC7B0"), 64), moonMat, -2985);
            cam.AddParallax(moon, 0.94f, 0.9f);

            // mountains: two ridges at 0.8 and 0.62 of the camera's speed
            var far = Group(scenery, "MountainsFar");
            Poly(far, "Range", Ridge(-34f, 34f, 0.55f, 0.9f, 1.5f, 11), KMath.Hex("#5C2126"), -2900);
            cam.AddParallax(far, 0.8f, 0.6f);
            var mid = Group(scenery, "MountainsMid");
            Poly(mid, "Range", Ridge(-30f, 30f, 0.9f, 0.6f, 1.1f, 23), KMath.Hex("#3A141A"), -2850);
            cam.AddParallax(mid, 0.62f, 0.45f);

            // a layer that moves at factor f against the camera needs content over [-8, len * (1 - f) + 8]
            var tori = Group(scenery, "ToriiLine");
            for (float u = -4f; u < len * 0.7f + 10f; u += 13f) Torii(tori, new Vector2(u, 0f), 2.6f, KMath.Hex("#170709"), -2600);
            cam.AddParallax(tori, 0.3f, 0.1f);
            var pines = Group(scenery, "PineLine");
            int seed = 3;
            for (float u = -8f; u < len * 0.5f + 10f; u += 3.1f) { Pine(pines, new Vector2(u + Hash(seed) * 1.4f, 0f), 1.9f + Hash(seed + 1) * 1.5f, KMath.Hex("#12060A"), -2500, seed); seed += 2; }
            cam.AddParallax(pines, 0.5f, 0.2f);

            // the ground the fighters stand on
            var floor = Group(scenery, "Floor");
            float x0 = -20f, x1 = len + 20f;
            Poly(floor, "Earth", new[] { new Vector2(x0, 0f), new Vector2(x1, 0f), new Vector2(x1, -9f), new Vector2(x0, -9f) }, KMath.Hex("#0E0709"), -1200);
            MeshKit.MakeRenderer("FloorLight", floor, MeshKit.PolyGradient(new[] { new Vector2(x0, -0.5f), new Vector2(x1, -0.5f), new Vector2(x1, 0.02f), new Vector2(x0, 0.02f) },
                KMath.Hex("#0E0709").WithA(0f), KMath.Hex("#4A1A1D")), MeshKit.Alpha, -1190);
            Poly(floor, "Grass", GrassEdge(x0, x1, 0f, 0.12f, 5), KMath.Hex("#0E0709"), -1180);

            // on the road: a stone lantern and a fence post now and then, behind the fighters
            for (float x = 3f; x < len; x += 6.5f) Lantern(floor, new Vector2(x + Hash((int)x) * 1.5f, 0f), -1100);
            for (float x = 6f; x < len; x += 4.7f) Fence(floor, new Vector2(x, 0f), -1110, (int)x);

            // gates: two posts and a shut barrier of red light, one for each encounter
            for (int i = 0; i < Data.Encounters.Length; i++)
            {
                var g = Group(floor, "Gate" + (i + 1));
                g.position = new Vector3((float)Data.Encounters[i].Gate + 0.45f, 0f, 0f);
                Poly(g, "PostNear", Post(-0.28f, 0f, 0.16f, 3.3f), KMath.Hex("#0C0507"), -1050);
                Poly(g, "PostFar", Post(0.28f, 0f, 0.16f, 3.3f), KMath.Hex("#0C0507"), -1050);
                Poly(g, "Lintel", new[] { new Vector2(-0.5f, 3.1f), new Vector2(0.5f, 3.1f), new Vector2(0.56f, 3.3f), new Vector2(-0.56f, 3.3f) }, KMath.Hex("#0C0507"), -1050);
                var barrier = new GameObject("Barrier");
                barrier.transform.SetParent(g, false);
                barrier.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                MeshKit.MakeRenderer("Light", barrier.transform, MeshKit.Quad(0.5f, 3.2f, KMath.Hex("#E2603A").WithA(0.28f)), MeshKit.SoftAdditive, -1000);
                MeshKit.MakeRenderer("Core", barrier.transform, MeshKit.Quad(0.07f, 3.1f, KMath.Hex("#FF8A66").WithA(0.9f)), MeshKit.Additive, -999);
                var beam = new GameObject("Beams");
                beam.transform.SetParent(g, false);
                for (int b = 0; b < 3; b++)
                    MeshKit.MakeRenderer("Beam", beam.transform, MeshKit.Quad(0.62f, 0.05f, KMath.Hex("#D5444B").WithA(0.85f)), MeshKit.Additive, -998).transform.localPosition = new Vector3(0f, 0.6f + b * 1.0f, 0f);
                _barriers.Add(barrier);
                _beams.Add(beam);
                barrier.SetActive(false);
                beam.SetActive(false);
            }

            // foreground blades, passing faster than the camera
            var fg = Group(scenery, "Foreground");
            int gs = 13;
            for (float u = -10f; u < len * 1.25f + 12f; u += 11.5f)
            {
                Poly(fg, "FgGrass", GrassEdge(u, u + 6.5f, -0.35f, 0.55f, gs), KMath.Hex("#050203"), 6000);
                gs += 4;
            }
            cam.AddParallax(fg, -0.25f, 0f);
        }

        // ---------------- little shapes ----------------

        static float Hash(int n) => KMath.Hash(n);

        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static MeshRenderer Poly(Transform parent, string name, IList<Vector2> pts, Color c, int order) =>
            MeshKit.MakeRenderer(name, parent, MeshKit.Poly(pts, c, name), MeshKit.Alpha, order);

        static Vector2[] Post(float cx, float y, float halfW, float h) =>
            new[] { new Vector2(cx - halfW, y), new Vector2(cx + halfW, y), new Vector2(cx + halfW, y + h), new Vector2(cx - halfW, y + h) };

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

        static List<Vector2> GrassEdge(float x0, float x1, float y, float h, int seed)
        {
            var pts = new List<Vector2> { new(x0, y - 9f) };
            int n = Mathf.Max(2, Mathf.CeilToInt((x1 - x0) / 0.22f));
            for (int i = 0; i <= n; i++)
            {
                float x = Mathf.Lerp(x0, x1, i / (float)n);
                pts.Add(new Vector2(x, y + (i % 2 == 0 ? 0.35f : 1f) * h * (0.4f + 0.9f * Mathf.PerlinNoise(x * 1.7f, seed))));
            }
            pts.Add(new Vector2(x1, y - 9f));
            return pts;
        }

        static void Pine(Transform parent, Vector2 at, float height, Color c, int order, int seed)
        {
            Poly(parent, "Trunk", Post(at.x, at.y, 0.08f * height * 0.3f, height * 0.25f), c, order);
            for (int tier = 0; tier < 4; tier++)
            {
                float t = tier / 4f;
                float baseY = at.y + height * (0.18f + t * 0.2f);
                float w = height * (0.34f - t * 0.06f);
                Poly(parent, "Tier", new[] { new Vector2(at.x - w, baseY), new Vector2(at.x + w, baseY), new Vector2(at.x, baseY + height * 0.34f) }, c, order);
            }
        }

        static void Torii(Transform parent, Vector2 at, float h, Color c, int order)
        {
            float w = h * 0.55f;
            Poly(parent, "PostL", Post(at.x - w, at.y, 0.1f, h), c, order);
            Poly(parent, "PostR", Post(at.x + w, at.y, 0.1f, h), c, order);
            Poly(parent, "Kasagi", new[] { new Vector2(at.x - w - 0.5f, at.y + h), new Vector2(at.x + w + 0.5f, at.y + h), new Vector2(at.x + w + 0.7f, at.y + h + 0.2f), new Vector2(at.x - w - 0.7f, at.y + h + 0.2f) }, c, order);
            Poly(parent, "Nuki", new[] { new Vector2(at.x - w - 0.1f, at.y + h * 0.78f), new Vector2(at.x + w + 0.1f, at.y + h * 0.78f), new Vector2(at.x + w + 0.1f, at.y + h * 0.86f), new Vector2(at.x - w - 0.1f, at.y + h * 0.86f) }, c, order);
        }

        static void Lantern(Transform parent, Vector2 at, int order)
        {
            var c = KMath.Hex("#1A0B0E");
            Poly(parent, "Base", Post(at.x, at.y, 0.13f, 0.3f), c, order);
            Poly(parent, "Stem", Post(at.x, at.y + 0.3f, 0.07f, 0.45f), c, order);
            Poly(parent, "Box", Post(at.x, at.y + 0.75f, 0.17f, 0.3f), c, order);
            Poly(parent, "Roof", new[] { new Vector2(at.x - 0.28f, at.y + 1.05f), new Vector2(at.x + 0.28f, at.y + 1.05f), new Vector2(at.x, at.y + 1.3f) }, c, order);
            MeshKit.MakeRenderer("Glow", parent, MeshKit.Disc(at + new Vector2(0f, 0.9f), 0.34f, KMath.Hex("#F6A64A").WithA(0.55f), KMath.Hex("#F6A64A").WithA(0f), 24), MeshKit.SoftAdditive, order + 1);
        }

        static void Fence(Transform parent, Vector2 at, int order, int seed)
        {
            var c = KMath.Hex("#140809");
            float h = 0.7f + Hash(seed) * 0.25f;
            Poly(parent, "Post", Post(at.x, at.y, 0.05f, h), c, order);
            Poly(parent, "Rail", new[] { new Vector2(at.x - 0.9f, at.y + h * 0.55f), new Vector2(at.x + 0.9f, at.y + h * 0.6f), new Vector2(at.x + 0.9f, at.y + h * 0.66f), new Vector2(at.x - 0.9f, at.y + h * 0.61f) }, c, order);
        }
    }
}
