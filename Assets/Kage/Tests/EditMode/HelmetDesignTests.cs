using System.Collections.Generic;
using System.Linq;
using Kage.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The SAN-72 tier redesign's data (<see cref="HelmetDesigns"/>, generated from <c>Tools/helmet_art/helmets.py</c>): every Act I helmet
    /// but the starter has a design in one of the five tiers of the owner's reference, each tier has its palette, the shapes are
    /// drawable, the soft pieces hang on chains, and the tier language holds (no glow on common gear, glow on epic and god mode).
    /// </summary>
    public class HelmetDesignTests
    {
        // the 13 designed Act I helmets and their tiers (the starter Shiro Maki is Kage's bare hair and crimson scarf: no design)
        static readonly (string id, HelmetTier tier)[] Tiers =
        {
            ("kuro_zukin", HelmetTier.Common), ("sugegasa", HelmetTier.Common), ("hachigane", HelmetTier.Common), ("kasumi_men", HelmetTier.Common),
            ("tetsu_kabuto", HelmetTier.Epic), ("oni_menpo", HelmetTier.Epic), ("hoshi_kabuto", HelmetTier.Epic),
            ("hisame_zukin", HelmetTier.Trophy), ("sumi_eboshi", HelmetTier.Trophy),
            ("shika_kabuto", HelmetTier.Legendary), ("shikko_zukin", HelmetTier.Legendary),
            ("utsuro_kabuto", HelmetTier.GodMode), ("raijin_kabuto", HelmetTier.GodMode),
        };

        static IEnumerable<Vector2> Points(float[] xy)
        {
            for (int i = 0; i + 1 < xy.Length; i += 2) yield return new Vector2(xy[i], xy[i + 1]);
        }

        static IEnumerable<(string what, float[] xy)> Shapes(HelmetDesign d)
        {
            foreach (var p in d.pieces)
            {
                yield return ($"{d.id}/{p.name}", p.outline);
                if (p.detail != null)
                    for (int i = 0; i < p.detail.Length; i++) yield return ($"{d.id}/{p.name}/detail{i}", p.detail[i].pts);
            }
        }

        [Test]
        public void EveryAct1HelmetButTheStarterHasADesign()
        {
            Assert.AreEqual(13, HelmetDesigns.All.Count());
            CollectionAssert.AreEquivalent(Tiers.Select(t => t.id), HelmetDesigns.All.Select(d => d.id));
            foreach (var d in HelmetDesigns.All)
            {
                Assert.IsNotNull(JsTables.Helmet(d.id), $"{d.id} is a JS helmet");
                Assert.AreEqual(JsTables.Helmet(d.id).Look, d.look, $"{d.id} is named for its JS look");
                Assert.IsNotEmpty(d.pieces, d.id);
                Assert.AreEqual(d.pieces.Length, d.pieces.Select(p => p.name).Distinct().Count(), $"{d.id}: piece names are unique");
            }
        }

        [Test]
        public void TheStarterAndLaterActHelmetsHaveNoDesignAndFallBack()
        {
            foreach (var id in new[] { "shiro_maki", "hakobi_gasa", "utsushi_men", "deshi_tenugui", "mokushi_kabuto", "kasa", "nope", "", null })
            {
                Assert.IsFalse(HelmetDesigns.Has(id), id ?? "null");
                Assert.IsNull(HelmetDesigns.Get(id), id ?? "null");
                Assert.AreEqual(HairUnderHelmet.Kept, HelmetVisuals.HairUnder(id), id ?? "null");
            }
        }

        [Test]
        public void EachHelmetIsInItsTier()
        {
            foreach (var (id, tier) in Tiers) Assert.AreEqual(tier, HelmetDesigns.Get(id).tier, id);
            foreach (var t in HelmetTiers.All) Assert.IsTrue(HelmetDesigns.All.Any(d => d.tier == t), $"no helmet in {t}");
            // the order of the owner's sheet, and its labels
            CollectionAssert.AreEqual(new[] { "COMMON", "EPIC", "TROPHY", "LEGENDARY", "GOD MODE" }, HelmetTiers.All.Select(HelmetTiers.Label));
        }

        [Test]
        public void EveryTierHasItsOwnPaletteAndEveryToneADesignUsesIsInIt()
        {
            var rims = new HashSet<Color>();
            foreach (var t in HelmetTiers.All)
            {
                var pal = HelmetTiers.Palette(t);
                Assert.IsNotNull(pal, t.ToString());
                Assert.IsTrue(pal.Has("wrap"), $"{t}: the neck wrap tone");
                Assert.IsTrue(rims.Add(pal.rim), $"{t}: its rim light is another tier's");
            }
            foreach (var d in HelmetDesigns.All)
            {
                var pal = HelmetTiers.Palette(d.tier);
                foreach (var p in d.pieces)
                {
                    Assert.IsTrue(pal.Has(p.tone), $"{d.id}/{p.name}: tone {p.tone} is not in the {d.tier} palette");
                    if (p.detail != null) foreach (var x in p.detail) Assert.IsTrue(pal.Has(x.tone), $"{d.id}/{p.name}: detail tone {x.tone}");
                    Assert.That(p.which, Is.InRange(0, 2), $"{d.id}/{p.name}");
                }
            }
        }

        [Test]
        public void TheTierLanguageHolds()
        {
            float Glow(HelmetDesign d) => d.pieces.Max(p => p.glow);
            foreach (var d in HelmetDesigns.All)
            {
                switch (d.tier)
                {
                    case HelmetTier.Common: Assert.AreEqual(1f, Glow(d), $"{d.id}: common gear does not glow"); break;
                    case HelmetTier.Epic:
                    case HelmetTier.GodMode: Assert.Greater(Glow(d), 2f, $"{d.id}: {d.tier} gear glows"); break;
                    case HelmetTier.Trophy:
                        Assert.IsTrue(d.pieces.Any(p => p.tone == "gold") && d.pieces.Any(p => p.tone == "ribbon"), $"{d.id}: gold and white ribbons"); break;
                    case HelmetTier.Legendary:
                        Assert.IsTrue(d.pieces.Any(p => p.tone == "gold") && d.pieces.Any(p => p.tone == "red"), $"{d.id}: a gold crest and red cords"); break;
                }
            }
            // the god-mode helmets light his eye from behind
            foreach (var d in HelmetDesigns.All.Where(d => d.tier == HelmetTier.GodMode))
                Assert.IsTrue(d.pieces.Any(p => p.name == "EyeGlow"), d.id);
            // a tier up is more drawn: common helmets have fewer pieces on average than god-mode ones
            float Mean(HelmetTier t) => (float)HelmetDesigns.All.Where(d => d.tier == t).Average(d => d.pieces.Length);
            Assert.Less(Mean(HelmetTier.Common), Mean(HelmetTier.GodMode));
        }

        [Test]
        public void EveryShapeIsAClosedOutlineThatTriangulatesWithoutGaps()
        {
            foreach (var d in HelmetDesigns.All)
                foreach (var (what, xy) in Shapes(d))
                {
                    Assert.AreEqual(0, xy.Length % 2, what);
                    var pts = Points(xy).ToList();
                    Assert.GreaterOrEqual(pts.Count, 3, what);
                    float area = Mathf.Abs(MeshKit.SignedArea(pts));
                    Assert.Greater(area, 3f, $"{what}: no area");
                    var tris = MeshKit.Triangulate(pts);
                    float sum = 0f;
                    for (int i = 0; i + 2 < tris.Count; i += 3)
                        sum += Mathf.Abs(MeshKit.SignedArea(new[] { pts[tris[i]], pts[tris[i + 1]], pts[tris[i + 2]] }));
                    Assert.AreEqual(area, sum, area * 0.01f + 0.5f, $"{what}: the triangles do not tile the outline (a crossing edge)");
                }
        }

        [Test]
        public void SoftPiecesHangOnChainsWithAKnownFeel()
        {
            foreach (var d in HelmetDesigns.All)
            {
                Assert.IsTrue(d.pieces.Any(p => p.kind == PieceKind.Cloth), $"{d.id} has a soft piece");
                foreach (var p in d.pieces)
                {
                    if (p.kind != PieceKind.Cloth) { Assert.IsNull(p.chains, $"{d.id}/{p.name}"); continue; }
                    Assert.DoesNotThrow(() => SoftPresets.Get(p.feel), $"{d.id}/{p.name}");
                    Assert.IsNotEmpty(p.chains, $"{d.id}/{p.name}");
                    var outline = Points(p.outline).ToList();
                    float size = Mathf.Sqrt(Mathf.Abs(MeshKit.SignedArea(outline)));
                    foreach (var c in p.chains)
                    {
                        var ch = Points(c).ToList();
                        Assert.GreaterOrEqual(ch.Count, 3, $"{d.id}/{p.name}: a chain needs a few links");
                        // the chain runs along the piece: its root is near the outline
                        float near = outline.Min(v => (v - ch[0]).magnitude);
                        Assert.Less(near, size, $"{d.id}/{p.name}: the chain's root is far from the piece");
                    }
                }
            }
        }

        [Test]
        public void PiecesSitOnTheHeadOrTheNeckWrapAtTheirOrders()
        {
            foreach (var d in HelmetDesigns.All)
                foreach (var p in d.pieces)
                {
                    if (p.bone == B.Chest)
                    {
                        // the neck wrap and its loose end: over the scarf wrap (100), under the near arm (104)
                        Assert.That(p.order, Is.InRange(102, 103), $"{d.id}/{p.name}");
                        StringAssert.StartsWith("Wrap", p.name);
                        continue;
                    }
                    Assert.AreEqual(B.Head, p.bone, $"{d.id}/{p.name}");
                    if (p.order >= HelmetVisuals.EyeOrder)
                        Assert.AreEqual(PieceKind.Cloth, p.kind, $"{d.id}/{p.name}: only a face veil hangs in front of the scarf");
                    else
                        Assert.That(p.order, Is.InRange(83, 95), $"{d.id}/{p.name}: between behind the head and under the eye");
                }
        }

        [TestCase("kuro_zukin", HairUnderHelmet.Hidden)]
        [TestCase("sugegasa", HairUnderHelmet.Hidden)]
        [TestCase("hachigane", HairUnderHelmet.Kept)]
        [TestCase("kasumi_men", HairUnderHelmet.Hidden)]
        [TestCase("tetsu_kabuto", HairUnderHelmet.Hidden)]
        [TestCase("oni_menpo", HairUnderHelmet.Hidden)]
        [TestCase("hoshi_kabuto", HairUnderHelmet.Hidden)]
        [TestCase("hisame_zukin", HairUnderHelmet.Hidden)]
        [TestCase("sumi_eboshi", HairUnderHelmet.Hidden)]
        [TestCase("shika_kabuto", HairUnderHelmet.Hidden)]
        [TestCase("shikko_zukin", HairUnderHelmet.Hidden)]
        [TestCase("utsuro_kabuto", HairUnderHelmet.Hidden)]
        [TestCase("raijin_kabuto", HairUnderHelmet.Hidden)]
        public void TheHairRuleOfEachHelmet(string id, HairUnderHelmet hair)
        {
            Assert.AreEqual(hair, HelmetVisuals.HairUnder(id), id);
        }
    }
}
