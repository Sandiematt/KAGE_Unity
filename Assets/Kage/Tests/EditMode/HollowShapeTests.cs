using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The tutorial Hollow redrawn with smooth shapes (SAN-76 rework, on the SAN-112 kit): every part's outline is a closed,
    /// simple polygon that triangulates whole; every part rides the bone its piece moves with (art never moves a joint, ADR-0005);
    /// the body is curves, not boxes; the hanging cloth is on spring chains; and it reads as a straw-stuffed training husk with a
    /// sack hood, a plank over the eyes, crossed ropes and a short rag skirt, apart from the Shade. (The Shade was redrawn again
    /// from the owner's painting in the SAN-76 SVG redesign: its own tests are <see cref="HollowShadeSvgTests"/>.)
    /// </summary>
    public class HollowShapeTests
    {
        const string Tutorial = "hollow_tutorial";

        static RigDef Rig(string id)
        {
            var c = CharacterCatalog.Get(id);
            return CharacterCatalog.PartSet(c.partSet).Build(c);
        }

        /// <summary>The silhouette of a part: its flat polygon, its cel outline, or (cloth) its largest painted polygon.</summary>
        static Vector2[] Outline(PartDef p) =>
            p.pts ?? p.rimPts ?? p.polys.OrderByDescending(q => Mathf.Abs(MeshKit.SignedArea(q.pts))).First().pts;

        static PartDef Part(RigDef r, string name)
        {
            var p = r.parts.SingleOrDefault(q => q.name == name);
            Assert.IsNotNull(p, "no part " + name);
            return p;
        }

        /// <summary>Lowest point of a part in reference px (y grows down).</summary>
        static float LowestRefY(PartDef p) => Outline(p).Max(v => KageModel.OriginYRef - v.y * KageModel.RefScale);

        static float Width(PartDef p) { var o = Outline(p); return o.Max(v => v.x) - o.Min(v => v.x); }

        [TestCase(Tutorial)]
        public void EveryPartOutlineIsClosedSimpleAndTriangulatesWhole(string id)
        {
            foreach (var p in Rig(id).parts)
                SmoothShapeTests.AssertGoodOutline(Outline(p), id + " " + p.name);
        }

        [TestCase(Tutorial)]
        public void EveryPaintedPolygonHasAnArea(string id)
        {
            foreach (var p in Rig(id).parts)
                if (p.polys != null)
                    foreach (var (pts, _) in p.polys)
                    {
                        Assert.GreaterOrEqual(pts.Length, 3, p.name);
                        Assert.Greater(Mathf.Abs(MeshKit.SignedArea(pts)), 1e-8f, id + " " + p.name + " has a flat polygon");
                    }
        }

        static readonly (string prefix, B bone)[] Body =
        {
            ("ArmF_Sleeve", B.ArmFU), ("ArmF_Kote", B.ArmFL), ("ArmF_Forearm", B.ArmFL), ("HandF", B.HandF),
            ("ArmN_Sleeve", B.ArmNU), ("ArmN_Kote", B.ArmNL), ("ArmN_Forearm", B.ArmNL), ("HandN", B.HandN),
            ("LegF_Hakama", B.LegFU), ("LegF_Shin", B.LegFL), ("LegF_Wrap", B.LegFL), ("LegF_Foot", B.FootF),
            ("LegN_Hakama", B.LegNU), ("LegN_Shin", B.LegNL), ("LegN_Wrap", B.LegNL), ("LegN_Foot", B.FootN),
            ("Torso", B.Chest), ("Sash", B.Chest), ("Pelvis", B.Hips), ("Robe_", B.Hips), ("Neck", B.Neck), ("Face", B.Head), ("Eye", B.Head),
            ("Blade", B.Sword), ("Tsuka", B.Sword), ("Tsuba", B.Sword), ("Habaki", B.Sword),
        };

        static readonly (string prefix, B bone)[] TutorialOwn =
        {
            ("ArmF_Straw", B.ArmFL), ("ArmN_Straw", B.ArmNL), ("Rope_Cross", B.Chest),
            ("Straw_", B.Head), ("Sack", B.Head), ("Throat_Rope", B.Head), ("Plank", B.Head),
        };

        [TestCase(Tutorial)]
        public void EveryPartRidesTheBoneItsPieceMovesWith(string id)
        {
            var rig = Rig(id);
            var table = Body.Concat(TutorialOwn).ToArray();
            foreach (var p in rig.parts)
            {
                var hit = table.Where(b => p.name.StartsWith(b.prefix)).ToArray();
                Assert.IsNotEmpty(hit, id + ": an unexpected part " + p.name);
                Assert.AreEqual(hit[0].bone, p.bone, id + " " + p.name);
            }
            foreach (var (prefix, _) in table)
                Assert.IsTrue(rig.parts.Any(p => p.name.StartsWith(prefix)), id + ": no " + prefix);
            var kage = KageModel.Skeleton("K");
            for (int i = 0; i < (int)B.COUNT; i++)
            {
                Assert.AreEqual(kage.pivot[i], rig.pivot[i], "pivot of " + (B)i);
                Assert.AreEqual(kage.parent[i], rig.parent[i], "parent of " + (B)i);
            }
        }

        [TestCase(Tutorial, "Sack")]
        public void TheBodyIsDrawnInCurvesNotBlocks(string id, string hood)
        {
            var rig = Rig(id);
            // (the neck, a short capsule mostly under the hood, and the sash, a band with eased corners and straight sides, have fewer
            // points; they are checked as cel parts below)
            foreach (var name in new[] { "Torso", "Pelvis", "ArmN_Sleeve", "ArmN_Forearm", "ArmN_Kote", "HandN_Fist", "LegN_Hakama", "LegN_Shin", "LegN_Wrap",
                         "Face", hood, "Robe_Back", "Robe_Flap" })
            {
                var o = Outline(Part(rig, name));
                Assert.GreaterOrEqual(o.Length, 24, id + " " + name + " is a curve, not a box");
                int gentle = 0;
                for (int i = 0; i < o.Length; i++)
                {
                    Vector2 a = o[(i + o.Length - 1) % o.Length], b = o[i], c = o[(i + 1) % o.Length];
                    if (Vector2.Angle(b - a, c - b) < 35f) gentle++;
                }
                Assert.Greater(gentle, o.Length * 0.7f, id + " " + name + " has too many hard corners");
            }
            foreach (var p in rig.parts.Where(q => q.bone != B.Sword && q.name != "Eye"))
                Assert.IsTrue(p.rimPts != null || p.polys != null, id + " " + p.name + " is a flat block, not a cel part");
        }

        [TestCase(Tutorial, new[] { "Robe_Back", "Robe_Flap", "Sash_Tail", "Sack_Tail" })]
        public void TheHangingClothIsOnChains(string id, string[] names)
        {
            var rig = Rig(id);
            foreach (var name in names)
            {
                var p = Part(rig, name);
                Assert.AreEqual(SoftKind.Chain, p.soft, id + " " + name);
                Assert.IsNotEmpty(p.chains, id + " " + name);
                foreach (var ch in p.chains) Assert.GreaterOrEqual(ch.Length, 2, id + " " + name);
            }
            Assert.IsFalse(rig.softCoat, "no Kage coat cloth");
        }

        [Test]
        public void TheTutorialHollowsRagSkirtIsShort()
        {
            // the training husk's rag skirt stops above the knees (680)
            Assert.Less(LowestRefY(Part(Rig(Tutorial), "Robe_Back")), 640f, "the tutorial Hollow's rag skirt is short");
        }

        [Test]
        public void TheTutorialHollowIsAStrawStuffedTrainingHusk()
        {
            var tut = Rig(Tutorial);
            var names = tut.parts.Select(p => p.name).ToHashSet();
            Assert.IsFalse(names.Overlaps(new[] { "Saya", "Shard_1", "Mask", "Cowl", "Chest_Rip" }), "no saya, shards, half-mask, cowl or rip");
            Assert.GreaterOrEqual(tut.parts.Count(p => p.name.StartsWith("Straw_")), 5, "straw bursting from the crown");
            bool Has(PartDef p, Color c) => p.polys != null && p.polys.Any(q => q.col == c);
            Assert.IsTrue(tut.parts.Where(p => p.name.StartsWith("Straw_") || p.name.EndsWith("_Straw")).All(p => Has(p, HollowPalette.Straw)), "straw coloured straw");
            Assert.IsTrue(Has(Part(tut, "Sack"), HollowPalette.Sack), "a sack hood");
            Assert.IsTrue(Has(Part(tut, "Plank"), HollowPalette.Wood), "a plank over the eyes");
            Assert.IsTrue(Has(Part(tut, "Rope_Cross"), HollowPalette.Rope), "ropes crossed over the chest");
            Assert.IsTrue(Has(Part(tut, "Sash"), HollowPalette.Rope), "a rope sash");
        }
    }
}
