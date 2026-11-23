using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Raikon redrawn on the SVG rig pipeline (SAN-71 redesign; the Husk's HollowHuskSvgTests with Raikon's table). Seams: the cut
    /// file <c>Resources/Art/SvgRig/Raikon.txt</c> (<see cref="SvgRigData"/>), the catalog's <c>raikon</c> entry → his
    /// <see cref="IPartSet.Build"/> → the <see cref="RigDef"/>, and that rig built on a real <see cref="FighterRig"/>. The independent
    /// sources of truth are the owner's files (their hash), Kage's own skeleton (<c>KageModel.Build(Look.Kage)</c>), the trace's seven
    /// colours, the hit line of SAN-107 and the ticket's split of rigid and moving parts.
    /// </summary>
    public class RaikonSvgTests
    {
        const string Id = "raikon";

        static SvgRigData Data => SvgRigData.Load(RaikonPartSet.Resource);

        static RigDef Rig()
        {
            var c = CharacterCatalog.Get(Id);
            return CharacterCatalog.PartSet(c.partSet).Build(c);
        }

        static PartDef Part(RigDef r, string name)
        {
            var p = r.parts.SingleOrDefault(q => q.name == name);
            Assert.IsNotNull(p, "no part " + name);
            return p;
        }

        static IEnumerable<Vector2> Drawn(PartDef p) => p.polys.SelectMany(q => q.pts);

        static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        // ---- the cut ----

        [Test]
        public void TheCutIsFreshForTheSvgThePaintingTheConfigAndTheTool()
        {
            var d = Data;
            foreach (var (_, path) in d.sources)
                Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, path)), "missing input " + path);
            Assert.IsTrue(d.sources.Any(s => s.path == "Tools/raikon_art/original/Raikon.svg"), "cut from the owner's SVG");
            Assert.IsTrue(d.sources.Any(s => s.path == "Tools/raikon_art/original/Raikon.png"), "silhouette, hair, fur and face tones from the owner's painting");
            Assert.IsTrue(d.sources.Any(s => s.path == "Tools/svg_art/svg_rig.py"), "by the shared SVG rig tool");
            Assert.AreEqual(d.hash, d.InputHash(ProjectRoot), "the cut is stale: run  python Tools/svg_art/svg_rig.py Tools/raikon_art/rig.json");
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
            Assert.Greater(n, 1500, "the trace's shading planes, finely cut: not a few blocks");
        }

        // ---- the character ----

        [Test]
        public void RaikonIsInTheCatalogWithHisJsBuildScaleAndTheTachi()
        {
            var def = CharacterCatalog.Get(Id);
            Assert.IsNotNull(def);
            Assert.AreEqual("raikon", def.partSet);
            Assert.AreEqual(1.24f, def.bodyScale, 1e-4f);
            Assert.AreEqual(WeaponFamilies.Tachi, def.family);
            Assert.AreEqual(RaikonPartSet.PaintedBladeLength, def.bladeLength, "his hit line is still his painted blade (SAN-107)");
        }

        [Test]
        public void ArtNeverMovesAGameplayJoint()
        {
            var kage = KageModel.Build(KageModel.Look.Kage);
            var r = Rig();
            for (int i = 0; i < (int)B.COUNT; i++)
            {
                Assert.AreEqual(kage.pivot[i], r.pivot[i], ((B)i) + " pivot");
                Assert.AreEqual(kage.parent[i], r.parent[i], ((B)i) + " parent");
            }
            Assert.AreEqual(kage.hurt.Count, r.hurt.Count);
            for (int i = 0; i < kage.hurt.Count; i++)
            {
                Assert.AreEqual(kage.hurt[i].bone, r.hurt[i].bone);
                Assert.AreEqual(kage.hurt[i].local, r.hurt[i].local);
                Assert.AreEqual(kage.hurt[i].radius, r.hurt[i].radius);
            }
            Assert.AreEqual(kage.fistLocal, r.fistLocal);
            Assert.AreEqual(kage.footTipLocal, r.footTipLocal);
            Assert.AreEqual(kage.heelLocal, r.heelLocal);
            Assert.AreEqual(kage.hiltLocal, r.hiltLocal);
            Assert.AreEqual(kage.bladeBindAngle, r.bladeBindAngle);
            Assert.AreEqual(kage.bladeBaseLocal, r.bladeBaseLocal);
            Assert.AreEqual(kage.bladeTipLocal, r.bladeTipLocal);
        }

        [Test]
        public void RaikonStandsOnTheGroundAndIsAsTallAsTheSkeletonAllows()
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var p in Rig().parts)
                foreach (var v in Drawn(p)) { lo = Mathf.Min(lo, v.y); hi = Mathf.Max(hi, v.y); }
            Assert.AreEqual(0f, lo, 0.03f, "boot soles on the ground");
            Assert.That(hi, Is.InRange(1.95f, 2.6f), "top of the hat's bow and the ponytail");
        }

        static readonly (string part, B bone)[] Bones =
        {
            ("Hat", B.Head), ("Head", B.Head), ("Eye", B.Head), ("Ponytail", B.Head), ("Ribbons", B.Head),
            ("Tassel1", B.Head), ("Tassel2", B.Head), ("Tassel3", B.Head),
            ("Torso", B.Chest), ("Fur", B.Chest), ("Cape", B.Chest), ("Rope", B.Chest), ("ChestTassel", B.Chest),
            ("Skirt", B.Hips), ("Apron", B.Hips), ("Cord", B.Hips), ("Hem", B.Hips),
            ("SodeN", B.ArmNU), ("ArmN_Upper", B.ArmNU), ("ArmN_Fore", B.ArmNL), ("HandN", B.HandN),
            ("SodeF", B.ArmFU), ("ArmF_Upper", B.ArmFU), ("ArmF_Fore", B.ArmFL), ("HandF", B.HandF),
            ("ThighN", B.LegNU), ("ShinN", B.LegNL), ("BootN", B.FootN),
            ("ThighF", B.LegFU), ("ShinF", B.LegFL), ("BootF", B.FootF),
            ("Tachi_Hilt", B.Sword), ("Tachi_Blade", B.Sword),
        };

        [Test]
        public void EveryPartRidesTheBoneItsPieceBelongsTo()
        {
            var rig = Rig();
            foreach (var (part, bone) in Bones) Assert.AreEqual(bone, Part(rig, part).bone, part);
            CollectionAssert.AreEquivalent(Bones.Select(b => b.part), rig.parts.Select(p => p.name), "exactly these parts");
            // the far (sword) arm and the tachi come to the front in techniques, as Kage's do
            foreach (var p in rig.parts.Where(q => q.bone is B.ArmFU or B.ArmFL or B.HandF or B.Sword))
                Assert.IsTrue(p.swordLayer, p.name + " is on the sword layer");
            // drawn where its bone is: each limb part holds its bone's pivot or lies within a hand's width of it
            foreach (var name in new[] { "ThighN", "ShinN", "BootN", "ArmN_Upper", "ArmN_Fore", "HandN", "HandF", "Head", "Torso", "Skirt" })
            {
                var p = Part(rig, name);
                Vector2 pivot = rig.pivot[(int)p.bone];
                bool inside = p.polys.Any(q => Inside(q.pts, pivot));
                float near = Drawn(p).Min(v => (v - pivot).magnitude);
                Assert.IsTrue(inside || near < 0.12f, name + " is drawn away from its bone (" + near + ")");
            }
            // the rope belt sits at the waist, across the trunk
            var belt = Drawn(Part(rig, "Rope")).ToArray();
            Assert.IsTrue(belt.Min(v => v.y) < rig.pivot[(int)B.Chest].y + 0.05f && belt.Max(v => v.y) > rig.pivot[(int)B.Chest].y, "the belt is at the waist");
            Assert.IsTrue(belt.Min(v => v.x) < rig.pivot[(int)B.Chest].x && belt.Max(v => v.x) > rig.pivot[(int)B.Chest].x, "the belt crosses the trunk");
        }

        // ---- rigid from the SVG, moving parts on spring chains ----

        static readonly (string name, ChainFeel feel, int chains)[] Soft =
        {
            ("Cape", SoftPresets.Cape, 3), ("Ponytail", SoftPresets.Hair, 2), ("Ribbons", SoftPresets.Strip, 3), ("Fur", SoftPresets.Fur, 6),
            ("Tassel1", SoftPresets.Tassel, 1), ("Tassel2", SoftPresets.Tassel, 1), ("Tassel3", SoftPresets.Tassel, 1),
            ("ChestTassel", SoftPresets.Tassel, 1), ("Cord", SoftPresets.Tassel, 1),
            ("Apron", SoftPresets.Tails, 3), ("Hem", SoftPresets.Tails, 3),
        };

        [Test]
        public void HairCapeTasselsFurAndHangingClothAreOnSpringChainsAndTheArmourIsRigid()
        {
            var rig = Rig();
            foreach (var (name, feel, chains) in Soft)
            {
                var p = Part(rig, name);
                Assert.AreEqual(SoftKind.Chain, p.soft, name);
                Assert.AreEqual(chains, p.chains.Count, name + ": its chains");
                foreach (var ch in p.chains) Assert.GreaterOrEqual(ch.Length, 3, name + ": root, middle and tip");
                Assert.AreEqual(feel, p.chainFeel, name + " uses its preset");
                Assert.IsNull(p.rimPts, p.name + ": a soft part carries its painted outline, not a fixed rim that would not bend");
            }
            CollectionAssert.AreEquivalent(Soft.Select(s => s.name), rig.parts.Where(p => p.soft == SoftKind.Chain).Select(p => p.name),
                "the hat, head, armour, sode, arms, hands, belt, skirt, legs, boots and tachi do not hang");
            Assert.IsFalse(rig.scarf);
            Assert.IsFalse(rig.softCoat, "no Kage coat cloth");
        }

        [Test]
        public void ItIsPaintedInTheTracesOwnSevenColours()
        {
            var rig = Rig();
            var style = RaikonArt.Style;
            var trace = new[] { "#010000", "#1E1218", "#39222A", "#8A6C73", "#D2B5B7", "#F9EEEF", "#C7181F" }.Select(KMath.Hex).ToList();
            CollectionAssert.AreEquivalent(trace, style.tones.Values, "the style is the trace's palette, colour for colour");
            var far = trace.Select(style.far).ToList();
            foreach (var p in rig.parts)
            {
                Assert.IsNotNull(p.polys, p.name + " is cut from the trace");
                Assert.IsNotEmpty(p.polys, p.name);
                var allowed = Data.parts.Single(s => s.name == p.name).far ? far : trace;
                foreach (var (_, col) in p.polys)
                    Assert.IsTrue(allowed.Contains(col), p.name + " has a colour outside its palette: " + col);
            }
            Assert.AreEqual(RaikonArt.Rim, rig.rimColor, "the crimson back-light");
            Assert.AreEqual(RaikonArt.EyeGlow, Part(rig, "Eye").glow, 1e-6f, "the red eye glows");
            Assert.AreEqual(PartRole.Eye, Part(rig, "Eye").role);
            Assert.AreEqual(RaikonArt.BladeGlow, Part(rig, "Tachi_Blade").glow, 1e-6f, "the tachi glows");
            // far parts are one step darker along the trace's palette, never lighter
            for (int i = 0; i < trace.Count; i++)
                Assert.LessOrEqual(far[i].grayscale, trace[i].grayscale + 1e-6f, "far is darker: " + trace[i]);
        }

        [Test]
        public void TheFaceHatHairAndFurCarryThePaintingsDetail()
        {
            var rig = Rig();
            var style = RaikonArt.Style;
            int Count(string part, params string[] tones) => Part(rig, part).polys.Count(q => tones.Any(t => style.tones[t] == q.col));
            // the face is the painting's: a black profile with its lit edge (the trace merges it with the canvas)
            Assert.Greater(Count("Head", "Mauve", "Pale", "Cloth", "Crimson"), 3, "the face's lit profile");
            // the hair and fur are strands, not the trace's black specks: after the base layer (the outline, in ink, cut cell by cell)
            // come the tone islands; an ink island under 30 SVG px^2 is a speck (the trace's hair had dozens)
            var ink = style.tones["Ink"];
            float speck = 30f * (0.688f / 400f) * (0.688f / 400f);
            foreach (var part in new[] { "Ponytail", "Fur" })
            {
                var polys = Part(rig, part).polys;
                int k0 = polys.FindIndex(q => q.col != ink);
                Assert.Greater(k0, 0, part + ": its outline first");
                int specks = polys.Skip(k0).Count(q => q.col == ink && Mathf.Abs(MeshKit.SignedArea(q.pts)) < speck);
                Assert.Less(specks, 10, part + " has no black specks");
                float light = polys.Where(q => q.col == style.tones["Pale"] || q.col == style.tones["White"]).Sum(q => Mathf.Abs(MeshKit.SignedArea(q.pts)));
                Assert.Greater(light, 0f, part + " is white");
            }
            Assert.Greater(Count("Hat", "Crimson"), 10, "the hat's red veins and brim");
        }

        [Test]
        public void TheFarArmIsTheSwordArmAndCarriesItsOwnShoulderPlate()
        {
            var data = Data;
            foreach (var (part, region) in new[] { ("ArmF_Upper", "ArmN_Upper"), ("SodeF", "SodeN"), ("ArmF_Fore", "ArmN_Fore"), ("ThighF", "ThighN"), ("ShinF", "ShinN"), ("BootF", "BootN") })
            {
                var spec = data.parts.Single(p => p.name == part);
                Assert.AreEqual(region, spec.region, part + " is the near piece");
                Assert.IsTrue(spec.far, part + " one step darker");
            }
            var hand = data.parts.Single(p => p.name == "HandF");
            Assert.AreEqual("FistF", hand.region, "the sword hand closes the painted far fist on the hilt");
            Assert.AreEqual("HandN", data.parts.Single(p => p.name == "HandN").region, "the near hand is the painted hand that grips the tachi");
            var rig = Rig();
            Assert.Less(Part(rig, "SodeF").order, Part(rig, "Torso").order, "the far sode is behind the trunk");
            Assert.Less(Part(rig, "ArmF_Upper").order, Part(rig, "SodeF").order + 1);
        }

        [Test]
        public void TheTachiIsThePaintedBladeOnTheSwordBoneAndItsTipEndsTheHitLine()
        {
            var rig = Rig();
            Assert.AreEqual(B.HandF, rig.parent[(int)B.Sword], "the sword bone hangs from the hand");
            Vector2 pivot = rig.pivot[(int)B.Sword];
            Vector2 baseAt = pivot + rig.bladeBaseLocal;
            Vector2 along = (rig.bladeTipLocal - rig.bladeBaseLocal).normalized;
            // the painted tip, the furthest drawn point along the blade's axis, is where the hit line ends
            float tip = Drawn(Part(rig, "Tachi_Blade")).Max(v => Vector2.Dot(v - baseAt, along));
            Assert.AreEqual(RaikonPartSet.PaintedBladeLength, tip, 0.015f, "the drawn tip on the end of the hit line");
            // and the blade lies on the hit line: its middle within the blade's breadth
            float mid = RaikonPartSet.PaintedBladeLength * 0.5f;
            Vector2 m = baseAt + along * mid;
            Assert.Less(Drawn(Part(rig, "Tachi_Blade")).Min(v => (v - m).magnitude), 0.03f, "the blade runs along the hit line");
            float hilt = Drawn(Part(rig, "Tachi_Hilt")).Min(v => Vector2.Dot(v - baseAt, along));
            Assert.Less(hilt, -0.1f, "a hilt behind the guard for both hands");
        }

        // ---- on a real rig: the limbs stay joined in every pose ----

        static readonly (string a, string b, B joint)[] Joints =
        {
            ("ThighN", "ShinN", B.LegNL), ("ShinN", "BootN", B.FootN),
            ("ThighF", "ShinF", B.LegFL), ("ShinF", "BootF", B.FootF),
            ("ArmN_Upper", "ArmN_Fore", B.ArmNL), ("ArmN_Fore", "HandN", B.HandN),
            ("ArmF_Upper", "ArmF_Fore", B.ArmFL), ("ArmF_Fore", "HandF", B.HandF),
            ("SodeN", "ArmN_Upper", B.ArmNU), ("Skirt", "ThighN", B.LegNU),
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
            var go = new GameObject("RaikonRig");
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

        static GameObject Run(Wind wind, int frames = 150, float speed = 0f, int walkFrames = 0, bool setWind = true)
        {
            var go = new GameObject("RaikonRig");
            var rig = go.AddComponent<FighterRig>();
            rig.Build(Rig(), 1000);
            if (setWind) rig.wind = wind;
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
        public void TheSoftPartsSwingWithAWalkAndAStopAndTheArmourDoesNot()
        {
            // a standing rig and a walking one stepped together; for each part, the furthest any vertex got from the standing one's
            var still = Run(Wind.Calm, 1);
            var moved = Run(Wind.Calm, 1);
            var most = new Dictionary<string, float>();
            try
            {
                var names = Bones.Select(b => b.part).ToList();
                const float dt = 1f / 60f;
                for (int i = 0; i < 60; i++)
                {
                    float v = i < 30 ? 3f : 0f;
                    moved.transform.position += new Vector3(v * dt, 0f, 0f);
                    foreach (var (go, speed) in new[] { (still, 0f), (moved, v) })
                    {
                        var rig = go.GetComponent<FighterRig>();
                        rig.Apply(PoseLib.Stance());
                        rig.Secondary(dt, new Vector2(speed, 0f));
                    }
                    foreach (var part in names)
                    {
                        var a = still.GetComponentsInChildren<MeshFilter>().First(m => m.name == part).sharedMesh.vertices;
                        var b = moved.GetComponentsInChildren<MeshFilter>().First(m => m.name == part).sharedMesh.vertices;
                        float d = a.Select((p, k) => (p - b[k]).magnitude).Max();
                        most[part] = Mathf.Max(most.TryGetValue(part, out var m0) ? m0 : 0f, d);
                    }
                }
                float Moved(string part) => most[part];
                Assert.Greater(Moved("Cape"), 0.03f, "the cape's hem swings with the walk and the stop");
                Assert.Greater(Moved("Ponytail"), 0.02f, "the ponytail swings");
                Assert.Greater(Moved("Apron"), 0.01f, "the apron panels swing");
                Assert.Greater(Moved("Hem"), 0.01f, "the coat panels swing");
                foreach (var t in new[] { "Tassel1", "Tassel2", "Tassel3", "ChestTassel", "Cord", "Ribbons" })
                    Assert.Greater(Moved(t), 0.003f, t + " swings");
                Assert.Greater(Moved("Fur"), 0f, "the fur ruffles");
                Assert.Less(Moved("Fur"), Moved("Cape"), "the stiff fur moves less than the cape");
                foreach (var r in new[] { "Hat", "Torso", "Skirt", "SodeN", "Tachi_Blade" })
                    Assert.AreEqual(0f, Moved(r), 1e-6f, r + " is rigid");
            }
            finally { Object.DestroyImmediate(still); Object.DestroyImmediate(moved); }
        }

        [Test]
        public void TheCapeHairAndRibbonsStreamInTheWindAndCalmIsRepeatableAndTheSameAsUnset()
        {
            var calm = Run(Wind.Calm);
            var calm2 = Run(Wind.Calm);
            var unset = Run(Wind.Calm, setWind: false);
            var blow = Run(Wind.Blowing(new Vector2(-8f, 0f)));     // a head wind (the fighter faces right): it streams the cloth back
            try
            {
                foreach (var (part, _, _) in Soft)
                {
                    Assert.AreEqual(Mean(calm, part), Mean(calm2, part), part + ": the same every run");
                    Assert.AreEqual(Mean(unset, part), Mean(calm, part), part + ": calm is no wind at all");
                }
                float Back(string part) => Mean(calm, part).x - Mean(blow, part).x;
                Assert.Greater(Back("Cape"), 0.02f, "the cape streams back");
                Assert.Greater(Back("Ponytail"), 0.01f, "the ponytail streams back");
                Assert.Greater(Back("Ribbons"), 0.002f, "the ribbons flutter back");
                Assert.Greater(Back("Apron"), 0.003f, "the apron lifts");
                Assert.Greater(Back("Fur"), 0f, "the fur stirs");
                Assert.Less(Back("Fur"), Back("Cape"), "the fur moves less than the cape");
            }
            finally
            {
                Object.DestroyImmediate(calm); Object.DestroyImmediate(calm2); Object.DestroyImmediate(unset); Object.DestroyImmediate(blow);
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
