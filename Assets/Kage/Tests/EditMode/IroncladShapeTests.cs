using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Order Ironclad redrawn with smooth shapes (SAN-100 rework): every part's outline is a closed, simple polygon that
    /// triangulates whole; every part rides the bone its piece moves with (art never moves a joint, ADR-0005); the body, armour
    /// and head are curves, not quads; the kusazuri are plates on spring chains; and the figure is still the bulkiest of the Order.
    /// </summary>
    public class IroncladShapeTests
    {
        static RigDef Rig(string id = "order_ironclad")
        {
            var c = CharacterCatalog.Get(id);
            return CharacterCatalog.PartSet(c.partSet).Build(c);
        }

        static Vector2[] Outline(PartDef p) =>
            p.pts ?? p.rimPts ?? p.polys.OrderByDescending(q => Mathf.Abs(MeshKit.SignedArea(q.pts))).First().pts;

        [Test]
        public void EveryPartOutlineIsClosedSimpleAndTriangulatesWhole()
        {
            foreach (var p in Rig().parts)
                SmoothShapeTests.AssertGoodOutline(Outline(p), p.name);
        }

        [Test]
        public void EveryPaintedPolygonHasAnArea()
        {
            foreach (var p in Rig().parts)
                if (p.polys != null)
                    foreach (var (pts, _) in p.polys)
                    {
                        Assert.GreaterOrEqual(pts.Length, 3, p.name);
                        Assert.Greater(Mathf.Abs(MeshKit.SignedArea(pts)), 1e-8f, p.name + " has a flat polygon");
                    }
        }

        static readonly (string prefix, B bone)[] Bones =
        {
            ("ArmF_Sleeve", B.ArmFU), ("ArmF_Sode", B.ArmFU), ("ArmF_Kote", B.ArmFL), ("ArmF_ForeSleeve", B.ArmFL), ("HandF", B.HandF),
            ("ArmN_Sleeve", B.ArmNU), ("ArmN_Sode", B.ArmNU), ("ArmN_Kote", B.ArmNL), ("ArmN_ForeSleeve", B.ArmNL), ("ArmN_Cuff", B.ArmNL), ("HandN", B.HandN),
            ("LegF_Hakama", B.LegFU), ("LegF_Shin", B.LegFL), ("LegF_Suneate", B.LegFL), ("FootF", B.FootF),
            ("LegN_Hakama", B.LegNU), ("LegN_Shin", B.LegNL), ("LegN_Suneate", B.LegNL), ("FootN", B.FootN),
            ("Torso", B.Chest), ("Do", B.Chest), ("Gorget", B.Chest), ("Sash", B.Chest),
            ("Pelvis", B.Hips), ("Kusazuri_", B.Hips), ("Neck", B.Neck),
            ("Face", B.Head), ("Hood", B.Head), ("Horn_", B.Head), ("Throat", B.Head), ("Shikoro", B.Head), ("Kabuto", B.Head), ("Eye", B.Head),
        };

        [Test]
        public void EveryPartRidesTheBoneItsPieceMovesWith()
        {
            var rig = Rig();
            foreach (var p in rig.parts)
            {
                var hit = Bones.Where(b => p.name.StartsWith(b.prefix)).ToArray();
                Assert.IsNotEmpty(hit, "an unexpected part " + p.name);
                Assert.AreEqual(hit[0].bone, p.bone, p.name);
            }
            foreach (var (prefix, _) in Bones)
                Assert.IsTrue(rig.parts.Any(p => p.name.StartsWith(prefix)), "no " + prefix);
            var kage = KageModel.Skeleton("K");
            for (int i = 0; i < (int)B.COUNT; i++)
            {
                Assert.AreEqual(kage.pivot[i], rig.pivot[i], "pivot of " + (B)i);
                Assert.AreEqual(kage.parent[i], rig.parent[i], "parent of " + (B)i);
            }
        }

        [Test]
        public void TheBodyArmourAndHeadAreDrawnInCurvesNotBlocks()
        {
            var rig = Rig();
            foreach (var name in new[] { "Torso", "Do", "Gorget", "ArmN_Sleeve", "ArmN_Sode", "ArmN_Kote", "HandN_Fist", "LegN_Hakama", "LegN_Shin", "LegN_Suneate",
                                         "Pelvis", "Face", "Hood", "Shikoro", "Kabuto", "Horn_Near", "Kusazuri_F2" })
            {
                var p = rig.parts.Single(q => q.name == name);
                var o = Outline(p);
                Assert.GreaterOrEqual(o.Length, 20, name + " is a curve, not a box");
                int gentle = 0;
                for (int i = 0; i < o.Length; i++)
                {
                    Vector2 a = o[(i + o.Length - 1) % o.Length], b = o[i], c = o[(i + 1) % o.Length];
                    if (Vector2.Angle(b - a, c - b) < 35f) gentle++;
                }
                Assert.Greater(gentle, o.Length * 0.75f, name + " has too many hard corners");
            }
        }

        [Test]
        public void TheKusazuriArePlatesOnSpringChains()
        {
            var rig = Rig();
            foreach (var name in new[] { "Kusazuri_B1", "Kusazuri_B2", "Kusazuri_F1", "Kusazuri_F2" })
            {
                var p = rig.parts.Single(q => q.name == name);
                Assert.AreEqual(SoftKind.Chain, p.soft, name);
                Assert.IsNotEmpty(p.chains, name);
                foreach (var ch in p.chains) Assert.GreaterOrEqual(ch.Length, 2, name);
            }
            Assert.IsFalse(rig.softCoat, "no Kage coat cloth");
        }

        [Test]
        public void TheShouldersCarryAPlateEachAndTheHeadHasTwoHorns()
        {
            var rig = Rig();
            Assert.IsTrue(rig.parts.Any(p => p.name == "ArmN_Sode") && rig.parts.Any(p => p.name == "ArmF_Sode"));
            Assert.IsTrue(rig.parts.Any(p => p.name == "Horn_Near") && rig.parts.Any(p => p.name == "Horn_Far"));
        }

        static float Width(string id, string part)
        {
            var p = Rig(id).parts.Single(q => q.name == part);
            var pts = Outline(p);
            return pts.Max(v => v.x) - pts.Min(v => v.x);
        }

        [Test]
        public void TheDoAndTrunkAreStillWiderThanTheFootmans()
        {
            Assert.Greater(Width("order_ironclad", "Do"), Width("order_footman", "Do") * 1.15f);
            Assert.Greater(Width("order_ironclad", "Torso"), Width("order_footman", "Torso") * 1.1f);
        }
    }
}
