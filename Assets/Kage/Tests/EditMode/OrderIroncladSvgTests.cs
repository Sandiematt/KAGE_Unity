using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Order Ironclad redrawn from the owner's painting and its SVG trace (SAN-100 SVG redesign, the SAN-76 pipeline in the
    /// Order's style; a copy of HollowHuskSvgTests): the cut the game reads is fresh for the SVG, the painting, the config and the
    /// tool; every cut polygon is closed, simple, triangulates whole and is bigger than a speck; every rigid part rides the bone its
    /// piece belongs to and the limb pieces overlap at every joint in ten poses; the kusazuri, the rope belt's tassel and the hakama
    /// hang on spring chains with their presets, trail the body when it moves and bend in the wind on a real rig; the painted
    /// cleaver ends on the cleaver family's hit line and the family's own drawing leaves it alone; and the Ironclad stays the
    /// heaviest Order foe beside the Footman.
    /// </summary>
    public class OrderIroncladSvgTests
    {
        const string Id = "order_ironclad";

        static SvgRigData Data => SvgRigData.Load(OrderIroncladPartSet.Resource);

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
            Assert.IsTrue(d.sources.Any(s => s.path.EndsWith("Tools/order_art/ironclad/original/ironclad_horned_samurai.svg")), "cut from the owner's SVG");
            Assert.IsTrue(d.sources.Any(s => s.path.EndsWith("Tools/order_art/ironclad/original/ironclad_ref.png")), "silhouette from the owner's painting");
            Assert.AreEqual(d.hash, d.InputHash(ProjectRoot),
                "the cut is stale: run  python Tools/svg_art/svg_rig.py Tools/order_art/ironclad/rig.json");
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
            Assert.Greater(n, 300, "the painting's lacing and lacquer planes, not a few blocks");
        }

        static readonly (string part, B bone)[] Bones =
        {
            ("Horn_Near", B.Head), ("Horn_Far", B.Head), ("Kabuto", B.Head), ("Shikoro", B.Head), ("Face", B.Head), ("Eye", B.Head), ("Throat", B.Head),
            ("Do", B.Chest),
            ("Pelvis", B.Hips), ("Belt", B.Hips), ("Tassel", B.Hips), ("Kusazuri_Front", B.Hips), ("Kusazuri_Back", B.Hips), ("Hakama", B.Hips), ("Wakizashi", B.Hips),
            ("ArmN_Upper", B.ArmNU), ("Sode_N", B.ArmNU), ("ArmN_Fore", B.ArmNL), ("HandN", B.HandN),
            ("ArmF_Upper", B.ArmFU), ("Sode_F", B.ArmFU), ("ArmF_Fore", B.ArmFL), ("HandF", B.HandF),
            ("Cleaver_Handle", B.Sword), ("Cleaver_Blade", B.Sword),
            ("LegN_Thigh", B.LegNU), ("LegN_Shin", B.LegNL), ("LegN_Foot", B.FootN),
            ("LegF_Thigh", B.LegFU), ("LegF_Shin", B.LegFL), ("LegF_Foot", B.FootF),
        };

        [Test]
        public void EveryPartRidesTheBoneItsPieceBelongsTo()
        {
            var rig = Rig();
            foreach (var (part, bone) in Bones) Assert.AreEqual(bone, Part(rig, part).bone, part);
            CollectionAssert.AreEquivalent(Bones.Select(b => b.part), rig.parts.Select(p => p.name), "exactly these parts");
            foreach (var p in rig.parts.Where(q => q.bone is B.ArmFU or B.ArmFL or B.HandF or B.Sword))
                Assert.IsTrue(p.swordLayer, p.name + " is on the sword layer");
            foreach (var name in new[] { "LegN_Thigh", "LegN_Shin", "LegN_Foot", "ArmN_Upper", "ArmN_Fore", "HandN", "HandF", "Face", "Do" })
            {
                var p = Part(rig, name);
                var o = p.polys.SelectMany(q => q.pts).ToArray();
                Vector2 pivot = rig.pivot[(int)p.bone];
                bool inside = p.polys.Any(q => Inside(q.pts, pivot));
                float near = o.Min(v => (v - pivot).magnitude);
                Assert.IsTrue(inside || near < 0.12f, name + " is drawn away from its bone (" + near + ")");
            }
            // the rope belt sits at the waist and crosses the trunk; the sode hangs over the shoulder
            var belt = Part(rig, "Belt").polys.SelectMany(q => q.pts).ToArray();
            Vector2 hips = rig.pivot[(int)B.Hips], chest = rig.pivot[(int)B.Chest];
            Assert.IsTrue(belt.Min(v => v.y) < chest.y && belt.Max(v => v.y) > hips.y - 0.1f, "the belt is at the waist");
            Assert.IsTrue(belt.Min(v => v.x) < hips.x && belt.Max(v => v.x) > hips.x, "the belt crosses the trunk");
            var sode = Part(rig, "Sode_N").polys.SelectMany(q => q.pts).ToArray();
            Vector2 sh = rig.pivot[(int)B.ArmNU];
            Assert.Greater(sode.Max(v => v.y), sh.y - 0.05f, "the sode reaches up to the shoulder");
            Assert.Greater(sode.Min(v => v.y), rig.pivot[(int)B.ArmNL].y - 0.05f, "and hangs no lower than the elbow");
        }

        static readonly (string name, ChainFeel feel)[] Soft =
        {
            ("Kusazuri_Front", SoftPresets.Plates), ("Kusazuri_Back", SoftPresets.Plates), ("Tassel", SoftPresets.Tassel), ("Hakama", SoftPresets.Tails),
        };

        [Test]
        public void TheKusazuriTasselAndHakamaHangOnSpringChainsWithTheirPresets()
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
                "the rigid pieces (kabuto, horns, face, dō, sode, arms, hands, belt, legs, boots, cleaver) do not hang");
            Assert.IsFalse(rig.scarf);
            Assert.IsFalse(rig.softCoat, "no Kage coat cloth");
        }

        [Test]
        public void ItIsDrawnInTheOrderPaletteWithCrimsonLacingAndAWhiteEye()
        {
            var rig = Rig();
            var style = OrderArt.Style;
            var palette = style.tones.Values.ToList();
            var far = palette.Select(c => style.far(c)).ToList();
            foreach (var p in rig.parts)
            {
                Assert.IsNotNull(p.polys, p.name + " is cut from the trace");
                Assert.IsNotEmpty(p.polys, p.name);
                foreach (var (_, col) in p.polys)
                    Assert.IsTrue(palette.Contains(col) || far.Contains(col), p.name + " has a colour outside the Order palette: " + col);
            }
            Assert.AreEqual(OrderArt.Rim, rig.rimColor, "the painting's crimson back-light");
            Assert.Greater(OrderArt.Rim.r, OrderArt.Rim.b * 2f, "crimson");
            Assert.AreEqual(OrderArt.EyeGlow, Part(rig, "Eye").glow, 1e-6f, "the white eye glows");
            Assert.AreEqual(PartRole.Eye, Part(rig, "Eye").role);
            Assert.IsTrue(Part(rig, "Eye").polys.All(q => q.col == style.tones["White"] || q.col == style.tones["Steel"]), "the eye is the painting's white");
            var crimson = new[] { "CrimsonDeep", "Crimson", "CrimsonLit" }.Select(t => style.tones[t]).ToList();
            foreach (var laced in new[] { "Do", "Sode_N", "Shikoro", "Kusazuri_Front", "Kusazuri_Back" })
                Assert.Greater(Part(rig, laced).polys.Count(q => crimson.Contains(q.col)), 5, laced + " is laced in crimson");
            Assert.IsTrue(Part(rig, "Tassel").polys.All(q => crimson.Contains(q.col)), "the rope and tassel are the painting's crimson rope");
            Assert.Greater(Part(rig, "Kabuto").polys.Count(q => crimson.Contains(q.col)), 3, "the crimson crest disc and brim band");
            Assert.Greater(Part(rig, "Face").polys.Count(q => q.col == style.tones["Ink"]), 0, "a black face");
        }

        [Test]
        public void ABladeMapNarrowsTheWeaponAcrossByItsOptionalFactorAndKeepsItsPoint()
        {
            var data = SvgRigData.Parse("svgrig 1\nfit 0.8 100 100 880 815\n");
            var rig = OpponentKit.Skeleton("MAP");
            var plain = SvgRig.Map(rig, data, "blade 100 100 300 100 1".Split(' '), "plain");
            var one = SvgRig.Map(rig, data, "blade 100 100 300 100 1 1".Split(' '), "one");
            var thin = SvgRig.Map(rig, data, "blade 100 100 300 100 1 0.5".Split(' '), "thin");
            Vector2 pivot = rig.pivot[(int)B.Sword], axis = KMath.Dir(rig.bladeBindAngle);
            Assert.AreEqual(pivot, thin(new Vector2(100, 100)), "the grip on the sword bone");
            Assert.AreEqual(0f, (thin(new Vector2(300, 100)) - (pivot + axis)).magnitude, 1e-5f, "the point len units out along the blade");
            var q = new Vector2(200, 140);       // half way out, 40 px off the axis
            Assert.AreEqual(plain(q), one(q), "1 is the default");
            Vector2 across = new(-axis.y, axis.x);
            Assert.AreEqual(Vector2.Dot(plain(q) - pivot, axis), Vector2.Dot(thin(q) - pivot, axis), 1e-5f, "the same place along the blade");
            Assert.AreEqual(Vector2.Dot(plain(q) - pivot, across) * 0.5f, Vector2.Dot(thin(q) - pivot, across), 1e-5f, "half as wide");
        }

        // ---- the cleaver ----

        /// <summary>The sword-local end of the cleaver family's Blade hit points for the Ironclad's own weapon at level 1.</summary>
        static Vector2 HitTip(RigDef rig)
        {
            var d = CharacterCatalog.Get(Id);
            float reach = CleaverMoves.BladeLength * WeaponStats.Combat(d.weaponId, 1).reach;
            Vector2 dir = (rig.bladeTipLocal - rig.bladeBaseLocal).normalized;
            return rig.bladeBaseLocal + dir * reach;
        }

        [Test]
        public void ThePaintedCleaverEndsOnTheCleaverFamilysHitLine()
        {
            var rig = Rig();
            Vector2 sword = rig.pivot[(int)B.Sword], tip = HitTip(rig);
            Assert.AreEqual(0.979f, tip.magnitude, 0.005f, "the raider cleaver reaches as far as the family's reference (Onikiri, 0.9 m past the guard)");
            Vector2 axis = tip.normalized;
            var blade = Part(rig, "Cleaver_Blade").polys.Select(q => q.pts.Select(v => v - sword).ToArray()).ToList();
            float far = blade.SelectMany(p => p).Max(v => Vector2.Dot(v, axis));
            Assert.AreEqual(tip.magnitude, far, 0.03f, "the painted front edge is where the blade hits");
            Assert.IsTrue(blade.Any(p => Inside(p, tip * 0.98f)), "the hit line's end lies on the painted blade");
            Assert.IsTrue(blade.Any(p => Inside(p, tip * 0.5f)), "and so does its middle");
            float width = blade.SelectMany(p => p).Max(v => Vector2.Dot(v, new Vector2(-axis.y, axis.x))) - blade.SelectMany(p => p).Min(v => Vector2.Dot(v, new Vector2(-axis.y, axis.x)));
            Assert.That(width, Is.InRange(0.2f, 0.4f), "a broad cleaver, not a slab across the screen");
            var handle = Part(rig, "Cleaver_Handle").polys.SelectMany(q => q.pts).Select(v => Vector2.Dot(v - sword, axis)).ToArray();
            Assert.Less(handle.Min(), Pose.HeavySpacing - 0.04f, "the handle runs back past the near hand's grip");
        }

        [Test]
        public void TheCleaverFamilyLeavesThePaintedCleaverAloneAndStillDrawsItsOwnForOthers()
        {
            var visual = new CleaverVisual();
            var go = new GameObject("IroncladRig");
            var kage = new GameObject("KageRig");
            try
            {
                var rig = go.AddComponent<FighterRig>();
                rig.Build(Rig(), 1000);
                visual.Attach(rig, "raider_cleaver");
                Assert.AreEqual(0, rig.FamilyPartCount, "the painted cleaver is the Ironclad's weapon: nothing drawn over it");
                Assert.IsFalse(rig.BaseSwordHidden, "and it stays shown");
                visual.Attach(rig, "raider_cleaver");
                Assert.AreEqual(0, rig.FamilyPartCount, "idempotent");

                var krig = kage.AddComponent<FighterRig>();
                krig.Build(KageModel.Build(KageModel.Look.Kage), 1000);
                visual.Attach(krig, "onikiri");
                Assert.Greater(krig.FamilyPartCount, 5, "every other cleaver wielder still gets the family's drawing");
                Assert.IsTrue(krig.BaseSwordHidden, "over a hidden katana");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(kage); }
        }

        // ---- the heaviest Order foe ----

        static float Width(PartDef p)
        {
            var o = p.polys.SelectMany(q => q.pts).ToArray();
            return o.Max(v => v.x) - o.Min(v => v.x);
        }

        static float Width(RigDef r, params string[] names)
        {
            var o = r.parts.Where(p => names.Contains(p.name)).SelectMany(p => p.pts ?? p.polys.SelectMany(q => q.pts).ToArray()).ToArray();
            return o.Max(v => v.x) - o.Min(v => v.x);
        }

        [Test]
        public void TheIroncladIsBulkierThanTheFootman()
        {
            var iron = Rig();
            var foot = Rig("order_footman");
            float si = CharacterCatalog.Get(Id).bodyScale, sf = CharacterCatalog.Get("order_footman").bodyScale;
            Assert.Greater(Width(Part(iron, "Do")) * si, Width(foot, "Do") * sf * 1.3f, "a dō a third wider on the screen");
            Assert.Greater(Width(Part(iron, "LegN_Thigh")) * si, Width(foot, foot.parts.Where(p => p.bone == B.LegNU).Select(p => p.name).ToArray()) * sf * 1.2f, "heavy thighs");
            Assert.Greater(Width(Part(iron, "LegN_Shin")) * si, Width(foot, foot.parts.Where(p => p.bone == B.LegNL).Select(p => p.name).ToArray()) * sf * 1.2f, "heavy boots");
        }

        // ---- on a real rig: the limbs stay joined in every pose ----

        static readonly (string a, string b, B joint)[] Joints =
        {
            ("LegN_Thigh", "LegN_Shin", B.LegNL), ("LegN_Shin", "LegN_Foot", B.FootN),
            ("LegF_Thigh", "LegF_Shin", B.LegFL), ("LegF_Shin", "LegF_Foot", B.FootF),
            ("ArmN_Upper", "ArmN_Fore", B.ArmNL), ("ArmN_Fore", "HandN", B.HandN),
            ("ArmF_Upper", "ArmF_Fore", B.ArmFL), ("ArmF_Fore", "HandF", B.HandF),
            ("Do", "ArmN_Upper", B.ArmNU), ("Pelvis", "LegN_Thigh", B.LegNU),
        };

        static readonly (string name, System.Func<Pose> make)[] Poses =
        {
            ("bind", PoseLib.Ref), ("stance", PoseLib.Stance), ("crouch", PoseLib.Crouch), ("guard", PoseLib.Block),
            ("hurt high", PoseLib.HurtHigh), ("hurt body", PoseLib.HurtBody), ("deep landing", PoseLib.LandDeep),
            ("dash", PoseLib.DashFwd), ("tumble", PoseLib.Tumble), ("lying", PoseLib.Lie),
        };

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
            var go = new GameObject("IroncladRig");
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

        static GameObject Run(Wind wind, int frames = 150, float speed = 0f, int walkFrames = 0)
        {
            var go = new GameObject("IroncladRig");
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
        public void TheSoftPartsSwingWithAWalkAndAStopThePlatesStiffest()
        {
            var still = Run(Wind.Calm, 40);
            var moved = Run(Wind.Calm, 40, speed: 3f, walkFrames: 30);
            try
            {
                float Moved(string part)
                {
                    var a = still.GetComponentsInChildren<MeshFilter>().First(m => m.name == part).sharedMesh.vertices;
                    var b = moved.GetComponentsInChildren<MeshFilter>().First(m => m.name == part).sharedMesh.vertices;
                    return a.Select((v, i) => (v - b[i]).magnitude).Max();
                }
                Assert.Greater(Moved("Hakama"), 0.02f, "the hakama hem swings with the walk and the stop");
                Assert.Greater(Moved("Tassel"), 0.01f, "the tassel swings");
                Assert.Greater(Moved("Kusazuri_Back"), 0f, "the lames swing");
                Assert.Less(Moved("Kusazuri_Back"), Moved("Hakama"), "the lacquered lames swing less than the cloth");
            }
            finally { Object.DestroyImmediate(still); Object.DestroyImmediate(moved); }
        }

        [Test]
        public void TheClothAndRopeBendInTheWindThePlatesOnlyStirAndCalmIsRepeatable()
        {
            var calm = Run(Wind.Calm);
            var calm2 = Run(Wind.Calm);
            var blow = Run(Wind.Blowing(new Vector2(-8f, 0f)));     // a head wind (the fighter faces right): it streams the cloth back
            try
            {
                foreach (var (part, _) in Soft)
                    Assert.AreEqual(Mean(calm, part), Mean(calm2, part), part + ": the same every run");
                float hakama = Mean(calm, "Hakama").x - Mean(blow, "Hakama").x;
                float tassel = Mean(calm, "Tassel").x - Mean(blow, "Tassel").x;
                float plates = Mean(calm, "Kusazuri_Back").x - Mean(blow, "Kusazuri_Back").x;
                Assert.Greater(hakama, 0.01f, "the hakama streams back");
                Assert.Greater(tassel, 0.005f, "the rope and tassel stream back");
                Assert.Greater(plates, 0f, "the lames stir");
                Assert.Less(plates, hakama, "the lacquered lames move less than the cloth");
            }
            finally { Object.DestroyImmediate(calm); Object.DestroyImmediate(calm2); Object.DestroyImmediate(blow); }
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
