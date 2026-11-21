using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Hollow Husk redrawn from the owner's SVG (SAN-112 SVG redesign, the SAN-76 pipeline; a copy of HollowShadeSvgTests): the
    /// cut the game reads is fresh for the SVG, its render, the config and the tool; every cut polygon is closed, simple, triangulates
    /// whole and is bigger than a speck; every rigid part rides the bone its piece belongs to and the limb pieces overlap at every
    /// joint in ten poses; the cloak, mantle, tassets, shide and bandage ends hang on spring chains with their presets, trail the body
    /// when it moves and bend in the wind on a real rig; and the Husk stays the heavy Hollow beside the Shade.
    /// </summary>
    public class HollowHuskSvgTests
    {
        const string Id = "hollow_husk";

        static SvgRigData Data => SvgRigData.Load(HollowHuskPartSet.Resource);

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
        public void TheCutIsFreshForTheSvgItsRenderTheConfigAndTheTool()
        {
            var d = Data;
            Assert.IsNotEmpty(d.sources);
            foreach (var (_, path) in d.sources)
                Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, path)), "missing input " + path);
            Assert.IsTrue(d.sources.Any(s => s.path.EndsWith("Tools/hollow_art/husk/original/husk_hooded_warrior.svg")), "cut from the owner's SVG");
            Assert.IsTrue(d.sources.Any(s => s.path.EndsWith("Tools/hollow_art/husk/original/husk_ref.png")), "silhouette from the SVG's render");
            Assert.AreEqual(d.hash, d.InputHash(ProjectRoot),
                "the cut is stale: run  python Tools/svg_art/svg_rig.py Tools/hollow_art/husk/rig.json");
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
            Assert.Greater(n, 150, "the trace's shading planes, not a few blocks");
        }

        static readonly (string part, B bone)[] Bones =
        {
            ("Horn_1", B.Head), ("Horn_2", B.Head), ("Hood", B.Head), ("Mask", B.Head), ("Face", B.Head), ("Eye", B.Head),
            ("Torso", B.Chest), ("Mantle", B.Chest),
            ("Pelvis", B.Hips), ("Shimenawa", B.Hips), ("Shide_1", B.Hips), ("Shide_2", B.Hips), ("Shide_3", B.Hips), ("Plates", B.Hips), ("Cloak", B.Hips),
            ("ArmN_Upper", B.ArmNU), ("ArmN_Fore", B.ArmNL), ("Wraps", B.ArmNL), ("HandN", B.HandN),
            ("ArmF_Upper", B.ArmFU), ("ArmF_Fore", B.ArmFL), ("HandF", B.HandF),
            ("LegN_Thigh", B.LegNU), ("LegN_Shin", B.LegNL), ("LegN_Foot", B.FootN),
            ("LegF_Thigh", B.LegFU), ("LegF_Shin", B.LegFL), ("LegF_Foot", B.FootF),
        };

        [Test]
        public void EveryPartRidesTheBoneItsPieceBelongsTo()
        {
            var rig = Rig();
            foreach (var (part, bone) in Bones) Assert.AreEqual(bone, Part(rig, part).bone, part);
            CollectionAssert.AreEquivalent(Bones.Select(b => b.part), rig.parts.Select(p => p.name), "exactly these parts");
            Assert.IsFalse(rig.parts.Any(p => p.bone == B.Sword), "no blade: the fists are the weapon");
            // the far arm moves to the front in techniques, as Kage's does
            foreach (var p in rig.parts.Where(q => q.bone is B.ArmFU or B.ArmFL or B.HandF))
                Assert.IsTrue(p.swordLayer, p.name + " is on the sword layer");
            // drawn where its bone is: each limb part's outline holds its bone's pivot or lies within a hand's width of it
            foreach (var name in new[] { "LegN_Thigh", "LegN_Shin", "LegN_Foot", "ArmN_Upper", "ArmN_Fore", "HandN", "Hood", "Torso" })
            {
                var p = Part(rig, name);
                var o = p.polys.SelectMany(q => q.pts).ToArray();
                Vector2 pivot = rig.pivot[(int)p.bone];
                bool inside = p.polys.Any(q => Inside(q.pts, pivot));
                float near = o.Min(v => (v - pivot).magnitude);
                Assert.IsTrue(inside || near < 0.12f, name + " is drawn away from its bone (" + near + ")");
            }
            // the rope belt sits at the waist: between the chest and hips pivots, across the trunk
            var belt = Part(rig, "Shimenawa").polys.SelectMany(q => q.pts).ToArray();
            float waist = (rig.pivot[(int)B.Chest].y + rig.pivot[(int)B.Hips].y) * 0.5f;
            Assert.IsTrue(belt.Min(v => v.y) < waist && belt.Max(v => v.y) > rig.pivot[(int)B.Hips].y - 0.1f, "the belt is at the waist");
            Assert.IsTrue(belt.Min(v => v.x) < rig.pivot[(int)B.Hips].x && belt.Max(v => v.x) > rig.pivot[(int)B.Hips].x, "the belt crosses the trunk");
        }

        static readonly (string name, ChainFeel feel)[] Soft =
        {
            ("Cloak", SoftPresets.Cape), ("Mantle", SoftPresets.Cape), ("Plates", SoftPresets.Plates), ("Wraps", SoftPresets.Strip),
            ("Shide_1", SoftPresets.Tassel), ("Shide_2", SoftPresets.Tassel), ("Shide_3", SoftPresets.Tassel),
        };

        [Test]
        public void TheClothPaperAndBandageEndsHangOnSpringChainsWithTheirPresets()
        {
            var rig = Rig();
            foreach (var (name, feel) in Soft)
            {
                var p = Part(rig, name);
                Assert.AreEqual(SoftKind.Chain, p.soft, name);
                Assert.GreaterOrEqual(p.chains.Count, 2, name + ": two or more chains, so it bends and twists");
                foreach (var ch in p.chains) Assert.GreaterOrEqual(ch.Length, 3, name);
                Assert.AreEqual(feel, p.chainFeel, name + " uses its preset");
            }
            foreach (var p in rig.parts.Where(q => q.soft == SoftKind.Chain))
                Assert.IsNull(p.rimPts, p.name + ": a soft part carries its painted rim, not a fixed one that would not bend");
            CollectionAssert.AreEquivalent(Soft.Select(s => s.name), rig.parts.Where(p => p.soft == SoftKind.Chain).Select(p => p.name),
                "the rigid pieces (hood, horns, mask, torso, pauldrons, belt, limbs, fists) do not hang");
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
            Assert.AreEqual(HollowArt.EyeGlow, Part(rig, "Eye").glow, 1e-6f, "the cold white eye glows");
            Assert.AreEqual(PartRole.Eye, Part(rig, "Eye").role);
            Assert.IsTrue(Part(rig, "Eye").polys.All(q => q.col == HollowArt.Style.tones["White"] || q.col == HollowArt.Style.tones["Steel"]), "the eye is the trace's white");
            var rope = new[] { "RopeDark", "Rope", "RopeLit" }.Select(t => HollowArt.Style.tones[t]).ToList();
            Assert.Greater(Part(rig, "Shimenawa").polys.Count(q => rope.Contains(q.col)), 10, "the shimenawa is the trace's twisted rope");
            for (int i = 1; i <= 3; i++)
                Assert.IsTrue(Part(rig, "Shide_" + i).polys.Any(q => q.col == HollowArt.Style.tones["White"] || q.col == HollowArt.Style.tones["Steel"]), "white paper shide " + i);
        }

        [Test]
        public void ASegMapWidensAPieceAcrossByItsOptionalFactorAndKeepsItAlongTheBone()
        {
            var data = SvgRigData.Parse("svgrig 1\nfit 0.8 100 100 880 815\n");
            var rig = OpponentKit.Skeleton("MAP");
            var plain = SvgRig.Map(rig, data, "seg 100 100 100 300 LegNU LegNL".Split(' '), "plain");
            var one = SvgRig.Map(rig, data, "seg 100 100 100 300 LegNU LegNL 1".Split(' '), "one");
            var wide = SvgRig.Map(rig, data, "seg 100 100 100 300 LegNU LegNL 1.5".Split(' '), "wide");
            Vector2 a = rig.pivot[(int)B.LegNU], b = rig.pivot[(int)B.LegNL], along = (b - a).normalized, across = new Vector2(-along.y, along.x);
            Assert.AreEqual(a, plain(new Vector2(100, 100)), "the drawn joints land on the pivots");
            Assert.AreEqual(b, plain(new Vector2(100, 300)));
            var q = new Vector2(130, 200);       // half way down, 30 px off the line
            Assert.AreEqual(plain(q), one(q), "1 is the default");
            Assert.AreEqual(Vector2.Dot(plain(q) - a, along), Vector2.Dot(wide(q) - a, along), 1e-5f, "the same place along the bone");
            Assert.AreEqual(Vector2.Dot(plain(q) - a, across) * 1.5f, Vector2.Dot(wide(q) - a, across), 1e-5f, "half as wide again across it");
            Assert.AreEqual(30f * 0.8f / KageModel.RefScale * 1.5f, Mathf.Abs(Vector2.Dot(wide(q) - a, across)), 1e-5f, "the fit's scale times the factor");
        }

        // ---- the heavy Hollow ----

        static float Width(PartDef p)
        {
            var o = p.polys.SelectMany(q => q.pts).ToArray();
            return o.Max(v => v.x) - o.Min(v => v.x);
        }

        [Test]
        public void TheHuskIsBulkierThanTheShade()
        {
            var husk = Rig();
            var shade = Rig("hollow_shade");
            Assert.Greater(Width(Part(husk, "Torso")), Width(Part(shade, "Torso")) * 1.25f, "a broad trunk");
            Assert.Greater(Width(Part(husk, "ArmN_Upper")), Width(Part(shade, "ArmN_Upper")) * 1.25f, "thick arms");
            Assert.Greater(Width(Part(husk, "LegN_Thigh")), Width(Part(shade, "LegN_Thigh")) * 1.2f, "thick legs");
            Assert.Greater(Width(Part(husk, "HandN")), Width(Part(shade, "HandN")) * 1.5f, "huge fists");
        }

        // ---- on a real rig: the limbs stay joined in every pose ----

        static readonly (string a, string b, B joint)[] Joints =
        {
            ("LegN_Thigh", "LegN_Shin", B.LegNL), ("LegN_Shin", "LegN_Foot", B.FootN),
            ("LegF_Thigh", "LegF_Shin", B.LegFL), ("LegF_Shin", "LegF_Foot", B.FootF),
            ("ArmN_Upper", "ArmN_Fore", B.ArmNL), ("ArmN_Fore", "HandN", B.HandN),
            ("ArmF_Upper", "ArmF_Fore", B.ArmFL), ("ArmF_Fore", "HandF", B.HandF),
            ("Torso", "ArmN_Upper", B.ArmNU), ("Pelvis", "LegN_Thigh", B.LegNU),
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
            var go = new GameObject("HuskRig");
            try
            {
                var rig = go.AddComponent<FighterRig>();
                rig.Build(Rig(), 1000);
                const float R = 0.025f;      // 10 reference px round the joint: no background may show there
                var gaps = new List<string>();
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

        // ---- on a real rig: the soft parts with the body's motion and in the wind ----

        static Vector2 Mean(GameObject go, string part)
        {
            var v = go.GetComponentsInChildren<MeshFilter>().First(m => m.name == part).sharedMesh.vertices;
            Vector2 s = Vector2.zero;
            foreach (var p in v) s += (Vector2)p;
            return s / v.Length;
        }

        /// <summary>The rig in its stance for a number of frames, standing (speed 0) or walking forward (facing right) at a speed
        /// for the first <paramref name="walkFrames"/> frames and then standing.</summary>
        static GameObject Run(Wind wind, int frames = 150, float speed = 0f, int walkFrames = 0)
        {
            var go = new GameObject("HuskRig");
            var rig = go.AddComponent<FighterRig>();
            rig.Build(Rig(), 1000);
            rig.wind = wind;
            rig.Apply(PoseLib.Stance());
            rig.ResetSoft();
            const float dt = 1f / 60f;
            for (int i = 0; i < frames; i++)
            {
                float v = i < walkFrames ? speed : 0f;
                go.transform.position += new Vector3(v * dt, 0f, 0f);
                rig.Apply(PoseLib.Stance());
                rig.Secondary(dt, new Vector2(v, 0f));
            }
            return go;
        }

        [Test]
        public void TheSoftPartsSwingWithAWalkAndAStop()
        {
            var still = Run(Wind.Calm, 40);
            var moved = Run(Wind.Calm, 40, speed: 3f, walkFrames: 30);     // a walk, then a stop: the cloth swings on
            try
            {
                // how far the part's furthest point (a hem, a tip) is from where it hangs standing
                float Moved(string part)
                {
                    var a = still.GetComponentsInChildren<MeshFilter>().First(m => m.name == part).sharedMesh.vertices;
                    var b = moved.GetComponentsInChildren<MeshFilter>().First(m => m.name == part).sharedMesh.vertices;
                    return a.Select((v, i) => (v - b[i]).magnitude).Max();
                }
                Assert.Greater(Moved("Cloak"), 0.03f, "the cloak's hem swings with the walk and the stop");
                Assert.Greater(Moved("Mantle"), 0.01f, "the mantle's torn tips swing");
                Assert.Greater(Moved("Wraps"), 0.0015f, "the short bandage ends swing");
                Assert.Greater(Moved("Plates"), 0f, "the tassets swing");
                Assert.Less(Moved("Plates"), Moved("Cloak"), "the heavy tassets swing less than the cloak");
            }
            finally { Object.DestroyImmediate(still); Object.DestroyImmediate(moved); }
        }

        [Test]
        public void TheCloakMantleAndTassetsBendInTheWindAndCalmIsRepeatable()
        {
            var calm = Run(Wind.Calm);
            var calm2 = Run(Wind.Calm);
            var blow = Run(Wind.Blowing(new Vector2(-8f, 0f)));     // a head wind (the fighter faces right): it streams the cloth further back
            try
            {
                foreach (var (part, _) in Soft)
                    Assert.AreEqual(Mean(calm, part), Mean(calm2, part), part + ": the same every run");
                float cloak = Mean(calm, "Cloak").x - Mean(blow, "Cloak").x;
                float mantle = Mean(calm, "Mantle").x - Mean(blow, "Mantle").x;
                float plates = Mean(calm, "Plates").x - Mean(blow, "Plates").x;
                float shide = Mean(calm, "Shide_2").x - Mean(blow, "Shide_2").x;
                Assert.Greater(cloak, 0.02f, "the cloak streams back");
                Assert.Greater(mantle, 0.005f, "the mantle's torn tips stream back");
                Assert.Greater(shide, 0.002f, "the paper shide flutter back");
                Assert.Greater(plates, 0.002f, "the tassets stir");
                Assert.Less(plates, cloak, "the heavy tassets move less than the cloak");
            }
            finally
            {
                Object.DestroyImmediate(calm); Object.DestroyImmediate(calm2); Object.DestroyImmediate(blow);
            }
        }

        static bool Inside(Vector2[] poly, Vector2 q)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > q.y) != (poly[j].y > q.y) && q.x < (poly[j].x - poly[i].x) * (q.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }
    }
}
