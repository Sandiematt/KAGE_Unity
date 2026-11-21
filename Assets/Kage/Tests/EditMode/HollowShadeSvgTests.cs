using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Hollow Shade redrawn from the owner's painting and its SVG trace (SAN-76 SVG redesign): the cut the game reads is fresh
    /// for the SVG, the painting, the config and the tool; every cut polygon is closed, simple, triangulates whole and is bigger than
    /// a speck; every rigid part rides the bone its piece belongs to; the cape, coat tails, shoulder strips and hip plates hang on
    /// spring chains with their presets and bend in the wind on a real rig; the blade is drawn along the hit line; and the Shade is
    /// still the lean Hollow beside the Husk.
    /// </summary>
    public class HollowShadeSvgTests
    {
        const string Id = "hollow_shade";

        static SvgRigData Data => SvgRigData.Load(HollowShadePartSet.Resource);

        static RigDef Rig(string id = Id)
        {
            var c = CharacterCatalog.Get(id);
            return CharacterCatalog.PartSet(c.partSet).Build(c);
        }

        static PartDef Part(RigDef r, string name)
        {
            var p = r.parts.SingleOrDefault(q => q.name == name);
            Assert.IsNotNull(p, "no part " + name);
            return p;
        }

        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        [Test]
        public void TheCutIsFreshForTheSvgThePaintingTheConfigAndTheTool()
        {
            var d = Data;
            Assert.IsNotEmpty(d.sources);
            foreach (var (_, path) in d.sources)
                Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, path)), "missing input " + path);
            Assert.IsTrue(d.sources.Any(s => s.path.EndsWith("Tools/hollow_art/shade/original/shade_cloaked_miniboss.svg")), "cut from the owner's SVG");
            Assert.IsTrue(d.sources.Any(s => s.path.EndsWith("Tools/hollow_art/shade/original/shade_ref.png")), "silhouette from the owner's painting");
            Assert.AreEqual(d.hash, d.InputHash(ProjectRoot),
                "the cut is stale: run  python Tools/svg_art/svg_rig.py Tools/hollow_art/shade/rig.json");
        }

        [Test]
        public void EveryCutPolygonIsClosedSimpleAndBiggerThanASpeck()
        {
            var d = Data;
            Assert.Greater(d.speck, 0f);
            int n = 0;
            foreach (var r in d.regions.Values)
                foreach (var (tone, pts) in r.polys)
                {
                    string what = r.name + " " + tone + " #" + n++;
                    Assert.IsTrue(d.tones.Contains(tone), what + ": unknown tone");
                    Assert.GreaterOrEqual(pts.Length, 3, what);
                    float area = Mathf.Abs(MeshKit.SignedArea(pts));
                    Assert.GreaterOrEqual(area, d.speck * 0.5f, what + ": a speck");
                    for (int i = 0; i < pts.Length; i++)
                        Assert.Greater((pts[i] - pts[(i + 1) % pts.Length]).magnitude, 0.01f, what + ": repeated point " + i);
                    Assert.IsFalse(SmoothShapeTests.SelfCrosses(pts, out int a, out int b), what + ": edge " + a + " crosses edge " + b);
                    var tris = MeshKit.Triangulate(pts);
                    float sum = 0f;
                    for (int i = 0; i < tris.Count; i += 3) sum += Mathf.Abs(MeshKit.SignedArea(new[] { pts[tris[i]], pts[tris[i + 1]], pts[tris[i + 2]] }));
                    Assert.AreEqual(area, sum, area * 0.01f + 0.05f, what + ": the triangles do not cover the shape");
                }
            Assert.Greater(n, 150, "the painting's shading planes, not a few blocks");
        }

        static readonly (string part, B bone)[] Bones =
        {
            ("Spike_1", B.Head), ("Spike_2", B.Head), ("Spike_3", B.Head), ("Head", B.Head), ("Crack", B.Head), ("Eye", B.Head),
            ("Neck", B.Neck), ("Torso", B.Chest), ("Cape", B.Chest), ("Flaps", B.Chest),
            ("Pelvis", B.Hips), ("Saya", B.Hips), ("Plates", B.Hips), ("Tails", B.Hips),
            ("ArmN_Upper", B.ArmNU), ("ArmN_Fore", B.ArmNL), ("HandN", B.HandN),
            ("ArmF_Upper", B.ArmFU), ("ArmF_Fore", B.ArmFL), ("HandF", B.HandF), ("Hilt", B.Sword), ("Blade", B.Sword),
            ("LegN_Thigh", B.LegNU), ("LegN_Shin", B.LegNL), ("LegN_Boot", B.FootN),
            ("LegF_Thigh", B.LegFU), ("LegF_Shin", B.LegFL), ("LegF_Boot", B.FootF),
        };

        [Test]
        public void EveryPartRidesTheBoneItsPieceBelongsTo()
        {
            var rig = Rig();
            foreach (var (part, bone) in Bones) Assert.AreEqual(bone, Part(rig, part).bone, part);
            CollectionAssert.AreEquivalent(Bones.Select(b => b.part), rig.parts.Select(p => p.name), "exactly these parts");
            // the far arm and everything on the sword bone move to the front in sword techniques, as Kage's do
            foreach (var p in rig.parts.Where(q => q.bone is B.ArmFU or B.ArmFL or B.HandF or B.Sword))
                Assert.IsTrue(p.swordLayer, p.name + " is on the sword layer");
            // drawn where its bone is: each limb part's outline holds its bone's pivot or lies within a hand's width of it
            foreach (var name in new[] { "LegN_Thigh", "LegN_Shin", "LegN_Boot", "ArmN_Upper", "ArmN_Fore", "Head", "Torso" })
            {
                var p = Part(rig, name);
                var o = p.rimPts ?? p.polys.OrderByDescending(q => Mathf.Abs(MeshKit.SignedArea(q.pts))).First().pts;
                Vector2 pivot = rig.pivot[(int)p.bone];
                float near = o.Min(v => (v - pivot).magnitude);
                Assert.IsTrue(Inside(o, pivot) || near < 0.12f, name + " is drawn away from its bone (" + near + ")");
            }
        }

        [Test]
        public void TheClothAndThePlatesHangOnSpringChainsWithTheirPresets()
        {
            var rig = Rig();
            foreach (var (name, feel) in new[] { ("Cape", SoftPresets.Cape), ("Tails", SoftPresets.Tails), ("Flaps", SoftPresets.Strip), ("Plates", SoftPresets.Plates) })
            {
                var p = Part(rig, name);
                Assert.AreEqual(SoftKind.Chain, p.soft, name);
                Assert.GreaterOrEqual(p.chains.Count, 2, name + ": two or more chains, so it bends and twists");
                foreach (var ch in p.chains) Assert.GreaterOrEqual(ch.Length, 3, name);
                Assert.AreEqual(feel, p.chainFeel, name + " uses its preset");
            }
            foreach (var p in rig.parts.Where(q => q.soft == SoftKind.Chain))
                Assert.IsNull(p.rimPts, p.name + ": a soft part carries its painted rim, not a fixed one that would not bend");
            Assert.IsFalse(rig.scarf);
            Assert.IsFalse(rig.softCoat, "no Kage coat cloth");
        }

        [Test]
        public void ItIsDrawnInTheHollowPaletteFromTheTrace()
        {
            var rig = Rig();
            var palette = HollowArt.Style.tones.Values.ToList();
            var far = palette.Select(c => HollowArt.Style.far(c)).ToList();
            foreach (var p in rig.parts)
            {
                Assert.IsNotNull(p.polys, p.name + " is cut from the trace");
                Assert.IsNotEmpty(p.polys, p.name);
                foreach (var (_, col) in p.polys)
                    Assert.IsTrue(palette.Contains(col) || far.Contains(col), p.name + " has a colour outside the Hollow palette: " + col);
            }
            Assert.AreEqual(HollowPalette.Rim, rig.rimColor, "the Hollows' violet back-light");
            Assert.AreEqual(2.6f, Part(rig, "Eye").glow, 1e-6f, "the cold white eye glows");
            Assert.AreEqual(PartRole.Eye, Part(rig, "Eye").role);
            Assert.Greater(Part(rig, "Crack").glow, 1.2f, "the crack in the mask glows");
            Assert.IsTrue(Part(rig, "Eye").polys.All(q => q.col == HollowArt.Style.tones["White"]), "the eye is the painting's white");
            var spikes = rig.parts.Where(p => p.name.StartsWith("Spike_")).ToList();
            Assert.AreEqual(3, spikes.Count, "a crown of three spikes");
            Assert.IsTrue(spikes.All(p => p.jigglePivot.HasValue && p.jiggle > 0f), "the spikes sway on their own pivots");
        }

        [Test]
        public void TheBladeIsDrawnAlongTheHitLine()
        {
            var rig = Rig();
            Vector2 axis = (rig.bladeTipLocal - rig.bladeBaseLocal).normalized;
            Vector2 sword = rig.pivot[(int)B.Sword];
            float tip = Part(rig, "Blade").polys.SelectMany(q => q.pts).Max(v => Vector2.Dot(v - sword, axis));
            Assert.AreEqual(Vector2.Dot(rig.bladeTipLocal, axis), tip, 0.04f, "the painted point is where the blade hits");
        }

        static bool Inside(Vector2[] poly, Vector2 q)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > q.y) != (poly[j].y > q.y) && q.x < (poly[j].x - poly[i].x) * (q.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }

        static float Width(PartDef p)
        {
            var o = p.rimPts ?? p.polys.SelectMany(q => q.pts).ToArray();
            return o.Max(v => v.x) - o.Min(v => v.x);
        }

        [Test]
        public void TheShadeIsLeanBesideTheHusk()
        {
            var shade = Rig();
            var husk = Rig("hollow_husk");
            Assert.Less(Width(Part(shade, "Torso")), Width(Part(husk, "Torso")) * 0.8f, "a narrow trunk");
            Assert.Less(Width(Part(shade, "ArmN_Upper")), Width(Part(husk, "ArmN_Sleeve")) * 0.8f, "thin arms");
            Assert.Less(Width(Part(shade, "LegN_Thigh")), Width(Part(husk, "LegN_Hakama")) * 0.85f, "thin legs");
        }

        // ---- on a real rig: the limbs stay joined in every pose ----

        static readonly (string a, string b, B joint)[] Joints =
        {
            ("LegN_Thigh", "LegN_Shin", B.LegNL), ("LegN_Shin", "LegN_Boot", B.FootN),
            ("LegF_Thigh", "LegF_Shin", B.LegFL), ("LegF_Shin", "LegF_Boot", B.FootF),
            ("ArmN_Upper", "ArmN_Fore", B.ArmNL), ("ArmN_Fore", "HandN", B.HandN),
            ("ArmF_Upper", "ArmF_Fore", B.ArmFL), ("ArmF_Fore", "HandF", B.HandF),
        };

        static readonly (string name, System.Func<Pose> make)[] Poses =
        {
            ("bind", PoseLib.Ref), ("stance", PoseLib.Stance), ("crouch", PoseLib.Crouch), ("guard", PoseLib.Block),
            ("hurt high", PoseLib.HurtHigh), ("hurt body", PoseLib.HurtBody), ("deep landing", PoseLib.LandDeep),
            ("dash", PoseLib.DashFwd), ("tumble", PoseLib.Tumble), ("lying", PoseLib.Lie),
        };

        /// <summary>The world triangles a part's renderer draws.</summary>
        static Vector2[] Triangles(GameObject rig, string part)
        {
            var mf = rig.GetComponentsInChildren<MeshFilter>().First(m => m.name == part);
            var v = mf.sharedMesh.vertices;
            var t = mf.sharedMesh.triangles;
            var r = new Vector2[t.Length];
            for (int i = 0; i < t.Length; i++) r[i] = mf.transform.TransformPoint(v[t[i]]);
            return r;
        }

        static bool Covered(Vector2[] tris, Vector2 q)
        {
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector2 a = tris[i], b = tris[i + 1], c = tris[i + 2];
                float d1 = (b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x);
                float d2 = (c.x - b.x) * (q.y - b.y) - (c.y - b.y) * (q.x - b.x);
                float d3 = (a.x - c.x) * (q.y - c.y) - (a.y - c.y) * (q.x - c.x);
                if (!((d1 < 0 || d2 < 0 || d3 < 0) && (d1 > 0 || d2 > 0 || d3 > 0))) return true;
            }
            return false;
        }

        [Test]
        public void NeighbouringLimbPiecesOverlapAtEveryJointInEveryPose()
        {
            var go = new GameObject("ShadeRig");
            try
            {
                var rig = go.AddComponent<FighterRig>();
                rig.Build(Rig(), 1000);
                const float R = 0.025f;      // 10 reference px round the joint: no background may show there
                var gaps = new System.Collections.Generic.List<string>();
                foreach (var (pose, make) in Poses)
                {
                    rig.Apply(make());
                    foreach (var (a, b, joint) in Joints)
                    {
                        var ta = Triangles(go, a);
                        var tb = Triangles(go, b);
                        Vector2 j = rig.Bone(joint).position;
                        for (int k = 0; k <= 16; k++)
                        {
                            Vector2 q = k == 16 ? j : j + R * new Vector2(Mathf.Cos(k * Mathf.PI / 8f), Mathf.Sin(k * Mathf.PI / 8f));
                            if (!Covered(ta, q) && !Covered(tb, q)) { gaps.Add($"{pose}: {a} / {b} (sample {k})"); break; }
                        }
                    }
                }
                Assert.IsEmpty(gaps, "background shows at a joint:\n" + string.Join("\n", gaps));
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- on a real rig: the soft parts in the wind ----

        static Vector2 Mean(GameObject go, string part)
        {
            var v = go.GetComponentsInChildren<MeshFilter>().First(m => m.name == part).sharedMesh.vertices;
            Vector2 s = Vector2.zero;
            foreach (var p in v) s += (Vector2)p;
            return s / v.Length;
        }

        static GameObject Run(Wind wind, int frames = 150)
        {
            var go = new GameObject("ShadeRig");
            var rig = go.AddComponent<FighterRig>();
            rig.Build(Rig(), 1000);
            rig.wind = wind;
            rig.Apply(PoseLib.Stance());
            rig.ResetSoft();
            for (int i = 0; i < frames; i++)
            {
                rig.Apply(PoseLib.Stance());
                rig.Secondary(1f / 60f, Vector2.zero);
            }
            return go;
        }

        [Test]
        public void TheCapeTailsAndPlatesBendInTheWindAndCalmIsRepeatable()
        {
            var calm = Run(Wind.Calm);
            var calm2 = Run(Wind.Calm);
            var blow = Run(Wind.Blowing(new Vector2(-8f, 0f)));     // a head wind (the fighter faces right): it streams the cloth further back
            try
            {
                foreach (var part in new[] { "Cape", "Tails", "Flaps", "Plates" })
                    Assert.AreEqual(Mean(calm, part), Mean(calm2, part), part + ": the same every run");
                float cape = Mean(calm, "Cape").x - Mean(blow, "Cape").x;
                float tails = Mean(calm, "Tails").x - Mean(blow, "Tails").x;
                float plates = Mean(calm, "Plates").x - Mean(blow, "Plates").x;
                Assert.Greater(cape, 0.02f, "the cape streams back");
                Assert.Greater(tails, 0.015f, "the coat tails stream back");
                Assert.Greater(plates, 0.002f, "the plates stir");
                Assert.Less(plates, cape, "the heavy plates move less than the cape");
            }
            finally
            {
                Object.DestroyImmediate(calm); Object.DestroyImmediate(calm2); Object.DestroyImmediate(blow);
            }
        }
    }
}
