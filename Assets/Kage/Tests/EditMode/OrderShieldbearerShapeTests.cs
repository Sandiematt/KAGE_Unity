using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Order Shieldbearer redrawn with smooth shapes (SAN-98 rework): every part's outline is a closed, simple polygon that
    /// triangulates whole; every armour piece rides the bone its piece moves with and sits over the body part it covers (the dō on
    /// the trunk, a sode on its upper sleeve, a kote and the tate on the forearm, the suneate on the shin, the kusazuri hung from
    /// the belt, the kabuto on the head); the body and armour are curves, not boxes; the tate stands on the near forearm.
    /// </summary>
    public class OrderShieldbearerShapeTests
    {
        static RigDef Rig()
        {
            var c = CharacterCatalog.Get("order_shieldbearer");
            return CharacterCatalog.PartSet(c.partSet).Build(c);
        }

        static PartDef Part(RigDef rig, string name)
        {
            var p = rig.parts.SingleOrDefault(q => q.name == name);
            Assert.IsNotNull(p, "no part " + name);
            return p;
        }

        /// <summary>The silhouette of a part: its flat polygon, its cel outline, or its largest painted polygon.</summary>
        static Vector2[] Outline(PartDef p) =>
            p.pts ?? p.rimPts ?? p.polys.OrderByDescending(q => Mathf.Abs(MeshKit.SignedArea(q.pts))).First().pts;

        /// <summary>A reference-pixel point (Kage's art frame, x to the back, y down) in bind space.</summary>
        static Vector2 Ref(float x, float y) => KageModel.P(x, y);

        const float Px = 1f / KageModel.RefScale;

        static Vector2 Centroid(Vector2[] pts)
        {
            float a = 0f, cx = 0f, cy = 0f;
            for (int i = 0; i < pts.Length; i++)
            {
                Vector2 p = pts[i], q = pts[(i + 1) % pts.Length];
                float k = p.x * q.y - q.x * p.y;
                a += k; cx += (p.x + q.x) * k; cy += (p.y + q.y) * k;
            }
            return new Vector2(cx, cy) / (3f * a);
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude);
            return (a + d * t - p).magnitude;
        }

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
            ("FieldCloak", B.Chest),
            ("ArmF_Sleeve", B.ArmFU), ("ArmF_Sode", B.ArmFU), ("ArmF_Kote", B.ArmFL), ("ArmF_Forearm", B.ArmFL), ("HandF", B.HandF),
            ("ArmN_Sleeve", B.ArmNU), ("ArmN_Sode", B.ArmNU), ("ArmN_Kote", B.ArmNL), ("ArmN_Forearm", B.ArmNL), ("ArmN_Cuff", B.ArmNL),
            ("HandN", B.HandN), ("Tate", B.ArmNL),
            ("LegF_Hakama", B.LegFU), ("LegF_Shin", B.LegFL), ("LegF_Suneate", B.LegFL), ("FootF", B.FootF),
            ("LegN_Hakama", B.LegNU), ("LegN_Shin", B.LegNL), ("LegN_Suneate", B.LegNL), ("FootN", B.FootN),
            ("Torso", B.Chest), ("Do", B.Chest), ("Sash", B.Chest), ("Kusazuri_", B.Hips), ("Pelvis", B.Hips), ("Neck", B.Neck),
            ("Face", B.Head), ("Hood", B.Head), ("Shikoro", B.Head), ("Kabuto", B.Head), ("Maedate", B.Head), ("Eye", B.Head),
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
        }

        /// <summary>Each armour piece lies along the bone it rides: its middle within a few px of that bone's segment.</summary>
        [Test]
        public void EachArmourPieceLiesAlongItsBone()
        {
            var rig = Rig();
            Vector2 Pv(B b) => rig.pivot[(int)b];
            var cases = new (string part, Vector2 a, Vector2 b, float px)[]
            {
                ("Do", Ref(878, 412), Pv(B.Neck), 30f),                         // the trunk, waist to neck
                ("ArmN_Sode", Pv(B.ArmNU), Pv(B.ArmNL), 32f),                   // shoulder to elbow
                ("ArmF_Sode", Pv(B.ArmFU), Pv(B.ArmFL), 32f),
                ("ArmN_Kote", Pv(B.ArmNL), Pv(B.HandN), 10f),                   // elbow to wrist
                ("ArmF_Kote", Pv(B.ArmFL), Pv(B.HandF), 10f),
                ("LegN_Suneate", Pv(B.LegNL), Ref(835, 797), 10f),              // knee to ankle
                ("LegF_Suneate", Pv(B.LegFL), Ref(955, 797), 10f),
                ("Tate", Pv(B.ArmNL), Pv(B.HandN), 40f),                        // strapped along the near forearm
            };
            foreach (var (part, a, b, px) in cases)
            {
                var c = Centroid(Outline(Part(rig, part)));
                Assert.LessOrEqual(SegDist(c, a, b), px * Px, part + " is off its bone by " + SegDist(c, a, b) / Px + " px");
            }
        }

        /// <summary>Each armour piece sits over the body it covers: the dō over the gi trunk, a sode over its sleeve, the suneate over
        /// the shin's hakama, the kabuto over the head, the kusazuri hung from the belt and over the hakama seat.</summary>
        [Test]
        public void EachArmourPieceSitsOverTheBodyItCovers()
        {
            var rig = Rig();
            void Over(string armour, string body)
            {
                var a = Outline(Part(rig, armour));
                var b = Outline(Part(rig, body));
                Assert.IsTrue(Inside(b, Centroid(a)), armour + " does not sit over " + body);
            }
            Over("Do", "Torso");
            Over("Torso", "Do");
            Over("ArmN_Sode", "ArmN_Sleeve");
            Over("ArmF_Sode", "ArmF_Sleeve");
            Over("LegN_Suneate", "LegN_Shin");
            Over("LegF_Suneate", "LegF_Shin");
            Over("Hood", "Face");
            var face = Outline(Part(rig, "Face"));
            var crown = face.OrderByDescending(v => v.y).First();
            Assert.IsTrue(Inside(Outline(Part(rig, "Kabuto")), crown + new Vector2(0f, -3f * Px)), "the kabuto does not sit on the crown of the head");

            var sash = Outline(Part(rig, "Sash"));
            float sashBottom = sash.Min(v => v.y);
            var seat = Outline(Part(rig, "Pelvis"));
            foreach (var k in rig.parts.Where(p => p.name.StartsWith("Kusazuri_")))
            {
                var o = Outline(k);
                Assert.IsTrue(o.Any(v => Inside(sash, v)), k.name + " is not tucked under the belt");
                Assert.Less(Centroid(o).y, sashBottom, k.name + " does not hang below the belt");
                var thigh = Outline(Part(rig, k.name.Contains("_B") ? "LegF_Hakama" : "LegN_Hakama"));
                Assert.IsTrue(Inside(seat, Centroid(o)) || Inside(thigh, Centroid(o)), k.name + " does not hang over the hakama");
            }
        }

        [Test]
        public void TheBodyAndArmourAreDrawnInCurvesNotBlocks()
        {
            var rig = Rig();
            foreach (var name in new[] { "Torso", "Do", "ArmN_Sleeve", "ArmN_Sode", "ArmN_Forearm", "LegN_Hakama", "LegN_Shin", "LegN_Suneate",
                         "Pelvis", "Hood", "Kabuto", "Shikoro", "Face", "Maedate", "Tate", "Kusazuri_F1" })
            {
                var o = Outline(Part(rig, name));
                Assert.GreaterOrEqual(o.Length, 24, name + " is a curve, not a box");
                int gentle = 0;
                for (int i = 0; i < o.Length; i++)
                {
                    Vector2 a = o[(i + o.Length - 1) % o.Length], b = o[i], c = o[(i + 1) % o.Length];
                    if (Vector2.Angle(b - a, c - b) < 35f) gentle++;
                }
                Assert.Greater(gentle, o.Length * 0.8f, name + " has too many hard corners");
            }
        }

        /// <summary>The kusazuri are cel parts that still swing from the belt: on a real rig each plate and its rim hang from their
        /// own pivot at the plate's top, under the hips bone.</summary>
        [Test]
        public void TheKusazuriSwingFromTheBelt()
        {
            var def = Rig();
            var go = new GameObject("ShieldbearerRig");
            try
            {
                var rig = go.AddComponent<FighterRig>();
                rig.Build(def, 0);
                var plates = def.parts.Where(p => p.name.StartsWith("Kusazuri_")).ToArray();
                Assert.GreaterOrEqual(plates.Length, 4, "two plates in front, two behind");
                foreach (var k in plates)
                {
                    Assert.IsNotNull(k.polys, k.name + " is a cel part");
                    Assert.IsTrue(k.jigglePivot.HasValue && k.jiggle > 0f, k.name + " swings");
                    float top = Outline(k).Max(v => v.y);
                    Assert.AreEqual(top, k.jigglePivot.Value.y, 12f * Px, k.name + " swings from its top");
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == k.name || r.name == k.name + "_Rim"))
                    {
                        Assert.AreEqual(k.name + "_Pivot", r.transform.parent.name, r.name + " hangs from the swing pivot");
                        Assert.AreEqual(rig.Bone(B.Hips), r.transform.parent.parent, r.name + " swings under the hips");
                    }
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheTateStandsOnTheNearForearmInFrontOfTheBody()
        {
            var rig = Rig();
            var tate = Part(rig, "Tate");
            var o = Outline(tate);
            Assert.AreEqual(B.ArmNL, tate.bone);
            Assert.IsNotNull(tate.rimPts, "the shield carries the back-light rim so it reads against the dark armour");
            // drawn over the near sleeve and the trunk, under the near hand
            Assert.Greater(tate.order, Part(rig, "ArmN_Sleeve").order);
            Assert.Greater(tate.order, Part(rig, "Do").order);
            Assert.Less(tate.order, Part(rig, "HandN_Fist").order);
            // the near hand grips it: the hand pivot is inside the shield
            Assert.IsTrue(Inside(o, rig.pivot[(int)B.HandN]), "the near hand is on the shield");
            // in front of the forearm (lower x in reference px is the front: bind x is mirrored)
            Assert.Greater(Centroid(o).x, rig.pivot[(int)B.HandN].x - 2f * Px, "the shield stands in front of the hand");
        }
    }
}
