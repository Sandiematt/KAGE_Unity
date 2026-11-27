using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Every shop weapon's drawing on Kage's rig is whole (the owner saw "a gap in the weapon" on Iwakudaki: a corner of the hammer head
    /// came out NaN, because <c>Mathf.Sin(PI)</c> is a hair below zero in float and a negative to the power 1.6 is NaN, and the broken
    /// polygon left a wedge of the head undrawn). Each family part's mesh must have finite vertices, and its main shapes must be covered
    /// by their triangles exactly (the triangles' area is the polygon's area).
    /// </summary>
    public class WeaponDrawingTests
    {
        readonly List<GameObject> _made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        FighterRig KageRig()
        {
            var go = new GameObject("DrawingTestRig");
            _made.Add(go);
            var rig = go.AddComponent<FighterRig>();
            rig.Build(KageModel.Build(KageModel.Look.Kage), 1500);
            return rig;
        }

        static float SignedArea(Vector3[] v)
        {
            float a = 0f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i++) a += v[j].x * v[i].y - v[i].x * v[j].y;
            return a * 0.5f;
        }

        static float TriangleArea(Mesh m)
        {
            var v = m.vertices;
            var t = m.triangles;
            float a = 0f;
            for (int k = 0; k < t.Length; k += 3)
            {
                Vector3 p = v[t[k]], q = v[t[k + 1]], r = v[t[k + 2]];
                a += Mathf.Abs((q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x)) * 0.5f;
            }
            return a;
        }

        [TestCase("mumei")]
        [TestCase("kagetsu")]
        [TestCase("shizuka")]
        [TestCase("tsubame")]
        [TestCase("onikiri")]
        [TestCase("mizuchi")]
        [TestCase("temple_staff")]
        [TestCase("vanguard_yari")]
        [TestCase("yamigama")]
        [TestCase("iwakudaki")]
        public void Every_part_of_the_weapon_drawing_is_whole(string weapon)
        {
            var rig = KageRig();
            var fam = WeaponFamilies.Get(WeaponFamilies.FamilyIdOf(weapon));
            Assert.IsNotNull(fam, weapon);
            fam.visual?.Attach(rig, weapon);
            int checkedShapes = 0;
            foreach (var r in rig.Renderers)
            {
                if (r == null) continue;
                var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var v = mesh.vertices;
                foreach (var p in v)
                    Assert.IsTrue(float.IsFinite(p.x) && float.IsFinite(p.y), $"{weapon}: {r.name} has a vertex that is not a number");
                // the main shapes: the polygon's outline is its vertex list, so the triangles must cover exactly its area
                if (!(r.name.Contains("_Head_") || r.name.Contains("_Blade_") || r.name == "Saya_Body") || r.name.EndsWith("_Rim")) continue;
                float poly = Mathf.Abs(SignedArea(v)), tris = TriangleArea(mesh);
                Assert.Greater(poly, 0f, r.name);
                Assert.AreEqual(poly, tris, poly * 1e-3f, $"{weapon}: {r.name}'s triangles cover {tris:0.000000} of its {poly:0.000000}");
                checkedShapes++;
            }
            if (weapon == "iwakudaki") Assert.Greater(checkedShapes, 0, "the hammer head was checked");
        }
    }
}
