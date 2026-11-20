using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Wind for the soft parts (SAN-76 SVG redesign): <see cref="SoftChains"/> hang under gravity, swing with the body, bend with a
    /// wind vector and gusts that are the same for the same seed, and with the wind calm (the default) every character's soft parts
    /// move exactly as they did before wind existed.
    /// </summary>
    public class SoftChainWindTests
    {
        // ---- Raikon's cape on a real rig, before wind existed: the regression the calm default must keep ----

        [Test]
        public void WithTheWindCalmRaikonsCapeMovesExactlyAsBefore()
        {
            var go = new GameObject("RaikonRig");
            try
            {
                var rig = go.AddComponent<FighterRig>();
                rig.Build(CharacterCatalog.PartSet("raikon").Build(CharacterCatalog.Get("raikon")), 1000);
                rig.Apply(PoseLib.Stance());
                rig.ResetSoft();
                const float dt = 1f / 60f;
                for (int i = 0; i < 90; i++)
                {
                    float v = i < 30 ? 3f : 0f;
                    go.transform.position += new Vector3(v * dt, 0f, 0f);
                    rig.Apply(PoseLib.Stance());
                    rig.Secondary(dt, new Vector2(v, 0f));
                }
                var cape = go.GetComponentsInChildren<MeshFilter>().First(m => m.name == "Cape").sharedMesh.vertices;
                double sx = 0, sy = 0, w = 0;
                for (int i = 0; i < cape.Length; i++) { sx += cape[i].x; sy += cape[i].y; w += cape[i].x * (i % 7) - cape[i].y * (i % 5); }
                Debug.Log($"SAN-76 cape golden: n={cape.Length} sx={sx:R} sy={sy:R} w={w:R} v0={cape[0].x:R},{cape[0].y:R} vlast={cape[^1].x:R},{cape[^1].y:R}");
                Assert.AreEqual(GoldenCount, cape.Length);
                Assert.AreEqual(GoldenSx, sx, 1e-3);
                Assert.AreEqual(GoldenSy, sy, 1e-3);
                Assert.AreEqual(GoldenW, w, 1e-3);
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- one chain on a bare bone: hang, swing, bend, repeat ----

        const float Dt = 1f / 60f;

        sealed class Rope : System.IDisposable
        {
            public readonly GameObject bone = new("Bone");
            public readonly Mesh mesh;
            public readonly SoftChains chains;

            /// <param name="sideways">true: the rope is drawn straight out to the side (to see it sag); false: hanging straight down</param>
            public Rope(ChainFeel feel, bool sideways = false)
            {
                var pts = new Vector2[5];
                for (int i = 0; i < 5; i++) pts[i] = sideways ? new Vector2(i * 0.25f, 0f) : new Vector2(0f, -i * 0.25f);
                var v = new Vector3[10];
                for (int i = 0; i < 5; i++)
                {
                    Vector2 n = sideways ? new Vector2(0f, 0.04f) : new Vector2(0.04f, 0f);
                    v[i * 2] = pts[i] - n;
                    v[i * 2 + 1] = pts[i] + n;
                }
                var t = new System.Collections.Generic.List<int>();
                for (int i = 0; i < 4; i++) t.AddRange(new[] { i * 2, i * 2 + 1, i * 2 + 2, i * 2 + 1, i * 2 + 3, i * 2 + 2 });
                mesh = new Mesh { vertices = v, triangles = t.ToArray() };
                chains = new SoftChains(mesh, bone.transform, new[] { pts }, feel);
            }

            /// <summary>The tip of the rope (the mean of its last two vertices), in the bone's space.</summary>
            public Vector2 Tip { get { var v = mesh.vertices; return (v[8] + v[9]) * 0.5f; } }

            public void Run(int frames, Vector2 boneVelocity = default)
            {
                for (int i = 0; i < frames; i++)
                {
                    bone.transform.position += (Vector3)(boneVelocity * Dt);
                    chains.Tick(Dt, 1f, boneVelocity);
                }
            }

            public void Dispose() { Object.DestroyImmediate(bone); Object.DestroyImmediate(mesh); }
        }

        static ChainFeel Still(float gravity = 6f) => new() { stiffRoot = 0.12f, stiffTip = 0.02f, drag = 2f, gravity = gravity, breeze = 0f };

        [Test]
        public void ARopeDrawnSidewaysSagsUnderGravity()
        {
            using var heavy = new Rope(Still(9f), sideways: true);
            using var weightless = new Rope(Still(0f), sideways: true);
            heavy.Run(120);
            weightless.Run(120);
            // (the shape matching holds a part near its drawn shape: gravity bends it, it does not let it fall straight)
            Assert.AreEqual(0f, weightless.Tip.y, 0.003f, "with no gravity it keeps its drawn shape (bar the flutter)");
            Assert.Less(heavy.Tip.y, weightless.Tip.y - 0.008f, "the free end hangs down");
            Assert.Greater(heavy.Tip.x, 0.6f, "the links keep their length: it droops, it does not stretch to the floor");
        }

        [Test]
        public void AHangingRopeTrailsTheBodyAndSettlesWhenItStops()
        {
            using var r = new Rope(Still());
            r.Run(30, new Vector2(3f, 0f));
            float trail = r.Tip.x;
            Assert.Less(trail, -0.003f, "moving right, the tip trails to the left of the root");
            r.Run(120);
            Assert.Less(Mathf.Abs(r.Tip.x), Mathf.Abs(trail) * 0.5f, "stopped, it swings back towards rest");
        }

        [Test]
        public void AHangingRopeBendsWithTheWindVector()
        {
            using var calm = new Rope(Still());
            using var right = new Rope(Still());
            using var left = new Rope(Still());
            right.chains.wind = Wind.Blowing(new Vector2(4f, 0f));
            left.chains.wind = Wind.Blowing(new Vector2(-4f, 0f));
            calm.Run(120); right.Run(120); left.Run(120);
            Assert.AreEqual(0f, calm.Tip.x, 0.01f, "no wind, no bend");
            Assert.Greater(right.Tip.x, 0.05f, "blown to the right");
            Assert.Less(left.Tip.x, -0.05f, "blown to the left");
            Assert.AreEqual(right.Tip.x, -left.Tip.x, 0.01f, "the same bend either way");
        }

        [Test]
        public void GustsAreTheSameForTheSameSeedAndDifferForAnother()
        {
            using var a = new Rope(Still());
            using var b = new Rope(Still());
            using var c = new Rope(Still());
            a.chains.wind = Wind.Blowing(new Vector2(1f, 0f), 4f, seed: 7);
            b.chains.wind = Wind.Blowing(new Vector2(1f, 0f), 4f, seed: 7);
            c.chains.wind = Wind.Blowing(new Vector2(1f, 0f), 4f, seed: 8);
            float maxAc = 0f;
            for (int f = 0; f < 240; f++)
            {
                a.Run(1); b.Run(1); c.Run(1);
                Assert.AreEqual(a.Tip, b.Tip, "frame " + f + ": the same seed blows the same");
                maxAc = Mathf.Max(maxAc, (a.Tip - c.Tip).magnitude);
            }
            Assert.Greater(maxAc, 0.005f, "another seed gusts differently");
        }

        [Test]
        public void TheWindIsCalmUnlessSetAndCalmChangesNothing()
        {
            using var a = new Rope(Still());
            using var b = new Rope(Still());
            Assert.IsTrue(a.chains.wind.IsCalm, "a new chain feels no wind");
            b.chains.wind = Wind.Calm;
            for (int f = 0; f < 90; f++)
            {
                var v = new Vector2(f < 30 ? 3f : 0f, 0f);
                a.Run(1, v); b.Run(1, v);
                Assert.AreEqual(a.Tip, b.Tip, "frame " + f);
            }
            Assert.AreEqual(Vector2.zero, Wind.Calm.At(3.7f, 2f));
        }

        [Test]
        public void AWindBlowsTheSteadyWindPlusGustsWithinTheirStrength()
        {
            var steady = Wind.Blowing(new Vector2(2f, 0f));
            Assert.AreEqual(new Vector2(2f, 0f), steady.At(0.3f), "no gust: the steady wind");
            var gusty = Wind.Blowing(new Vector2(2f, 0f), 3f, seed: 3);
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < 600; i++)
            {
                var w = gusty.At(i * 0.05f, i * 0.01f);
                Assert.AreEqual(w, gusty.At(i * 0.05f, i * 0.01f), "a pure function of time and x");
                Assert.That(w.x, Is.InRange(2f - 1e-4f, 5f + 1e-4f), "along the wind: steady to steady + gust");
                Assert.That(Mathf.Abs(w.y), Is.LessThanOrEqualTo(0.9f + 1e-4f), "a little sideways wobble");
                lo = Mathf.Min(lo, w.x); hi = Mathf.Max(hi, w.x);
            }
            Assert.Greater(hi - lo, 1.2f, "the gusts come and go");
        }

        [Test]
        public void EverySoftPartPresetHangsAndBendsInTheWindByItsWeight()
        {
            float capeBend = 0f, platesBend = 0f;
            foreach (var name in SoftPresets.Names)
            {
                var feel = SoftPresets.Get(name);
                using var sag = new Rope(feel, sideways: true);
                sag.Run(120);
                Assert.Less(sag.Tip.y, -0.001f, name + " hangs");
                using var calm = new Rope(feel);
                using var blown = new Rope(feel);
                blown.chains.wind = Wind.Blowing(new Vector2(5f, 0f));
                calm.Run(120); blown.Run(120);
                float bend = blown.Tip.x - calm.Tip.x;
                Assert.Greater(bend, 0.02f, name + " bends with the wind");
                Assert.IsTrue(float.IsFinite(blown.Tip.x) && float.IsFinite(blown.Tip.y), name);
                if (name == "Cape") capeBend = bend;
                if (name == "Plates") platesBend = bend;
            }
            Assert.Less(platesBend, capeBend, "hanging plates are heavier than cloth: the wind moves them less");
        }

        // measured on main (963ebcf) before SoftChains had a wind input
        const int GoldenCount = 6037;
        const double GoldenSx = -2185.437089189887, GoldenSy = -1052.8597129345872, GoldenW = -4454.3849588494631;
    }
}
