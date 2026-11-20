using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Hollow Husk redrawn with smooth shapes (SAN-112 rework): every part's outline is a closed, simple polygon that
    /// triangulates whole; every part rides the bone its piece moves with (art never moves a joint, ADR-0005); the body is
    /// curves, not quads; the robe, mantle and shide are cloth on spring chains.
    /// </summary>
    public class HollowHuskShapeTests
    {
        static RigDef Rig()
        {
            var c = CharacterCatalog.Get("hollow_husk");
            return CharacterCatalog.PartSet(c.partSet).Build(c);
        }

        /// <summary>The silhouette of a part: its flat polygon, its cel outline, or (cloth) its largest painted polygon.</summary>
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
            ("ArmF_Sleeve", B.ArmFU), ("ArmF_Cuff", B.ArmFU), ("ArmF_Forearm", B.ArmFL), ("HandF", B.HandF),
            ("ArmN_Sleeve", B.ArmNU), ("ArmN_Cuff", B.ArmNU), ("ArmN_Forearm", B.ArmNL), ("HandN", B.HandN),
            ("LegF_Hakama", B.LegFU), ("LegF_Shin", B.LegFL), ("LegF_Wrap", B.LegFL), ("LegF_Foot", B.FootF),
            ("LegN_Hakama", B.LegNU), ("LegN_Shin", B.LegNL), ("LegN_Wrap", B.LegNL), ("LegN_Foot", B.FootN),
            ("Torso", B.Chest), ("Chest_", B.Chest), ("Shimenawa", B.Chest), ("Shide_", B.Chest), ("Mantle", B.Chest),
            ("Pelvis", B.Hips), ("Robe_", B.Hips), ("Neck", B.Neck),
            ("Face", B.Head), ("Cowl", B.Head), ("Mask", B.Head), ("Horn_", B.Head), ("Eye", B.Head),
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
        public void TheBodyIsDrawnInCurvesNotBlocks()
        {
            var rig = Rig();
            foreach (var name in new[] { "Torso", "ArmN_Sleeve", "ArmN_Forearm", "HandN_Fist", "LegN_Hakama", "LegN_Shin", "Cowl", "Face", "Pelvis", "Mantle", "Robe_Flap" })
            {
                var p = rig.parts.Single(q => q.name == name);
                var o = Outline(p);
                Assert.GreaterOrEqual(o.Length, 30, name + " is a curve, not a box");
                // most of the outline turns gently: no run of hard corners
                int gentle = 0;
                for (int i = 0; i < o.Length; i++)
                {
                    Vector2 a = o[(i + o.Length - 1) % o.Length], b = o[i], c = o[(i + 1) % o.Length];
                    if (Vector2.Angle(b - a, c - b) < 35f) gentle++;
                }
                Assert.Greater(gentle, o.Length * 0.8f, name + " has too many hard corners");
            }
        }

        [Test]
        public void TheRobeMantleShideAndLooseEndAreCloth()
        {
            var rig = Rig();
            foreach (var name in new[] { "Robe_Back", "Robe_Flap", "Mantle", "Shide_1", "Shide_2", "Shide_3", "HandN_LooseEnd" })
            {
                var p = rig.parts.Single(q => q.name == name);
                Assert.AreEqual(SoftKind.Chain, p.soft, name);
                Assert.IsNotEmpty(p.chains, name);
                foreach (var ch in p.chains) Assert.GreaterOrEqual(ch.Length, 2, name);
            }
            Assert.IsFalse(rig.softCoat, "no Kage coat cloth");
        }
    }
}
