using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kage.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Helmet visuals (SAN-72, ADR-0005 gear layering). Seams: the equipped helmet id on Kage's
    /// <see cref="CharacterDef"/> -> the rig that his part set builds (<c>KagePartSet.Build</c>). The
    /// independent source of truth for "unchanged" is <c>KageModel.Build(Look.Kage)</c>, Kage's own drawing.
    /// </summary>
    public class HelmetVisualTests
    {
        // the 13 helmets with a design (the 14th, Shiro Maki, is Kage's bare head + crimson scarf: no extra parts). SAN-72 tier redesign.
        static readonly string[] Drawn =
        {
            "kuro_zukin", "sugegasa", "hachigane", "kasumi_men", "tetsu_kabuto", "oni_menpo", "hoshi_kabuto",
            "shika_kabuto", "utsuro_kabuto", "shikko_zukin", "raijin_kabuto", "sumi_eboshi", "hisame_zukin",
        };

        // worn with the head hair gone (every hood, cap, hat and kabuto) / hair kept under a brow band (SAN-72 tier redesign)
        static readonly string[] HairHidden =
        {
            "kuro_zukin", "sugegasa", "kasumi_men", "tetsu_kabuto", "oni_menpo", "hoshi_kabuto", "shika_kabuto", "utsuro_kabuto", "shikko_zukin",
            "raijin_kabuto", "sumi_eboshi", "hisame_zukin",
        };
        static readonly string[] HairKept = { "hachigane" };

        // later-act helmets: in the data, no drawing yet
        static readonly string[] NoDrawing = { "shiro_maki", "hakobi_gasa", "utsushi_men", "deshi_tenugui", "mokushi_kabuto", "kasa", "no_such_helmet", "", null };

        static RigDef Kage(string helmet)
        {
            var def = KageCharacter.Def();
            def.helmet = helmet;
            return new KagePartSet().Build(def);
        }

        static RigDef Bare() => KageModel.Build(KageModel.Look.Kage);

        /// <summary>A text fingerprint of everything drawn: bone, order, colours and every vertex of every part.</summary>
        static string Fingerprint(PartDef p)
        {
            var sb = new StringBuilder();
            sb.Append(p.name).Append('|').Append(p.bone).Append('|').Append(p.order).Append('|').Append(p.soft).Append('|').Append(p.color).Append('|').Append(p.rim).Append('|').Append(p.jiggle);
            if (p.pts != null) foreach (var v in p.pts) sb.Append(v.x.ToString("0.0000")).Append(',').Append(v.y.ToString("0.0000")).Append(';');
            if (p.polys != null)
                foreach (var (pts, col) in p.polys)
                {
                    sb.Append('#').Append(col);
                    foreach (var v in pts) sb.Append(v.x.ToString("0.0000")).Append(',').Append(v.y.ToString("0.0000")).Append(';');
                }
            return sb.ToString();
        }

        static List<string> Fingerprints(RigDef d) => d.parts.Select(Fingerprint).ToList();

        static IEnumerable<Vector2> Vertices(PartDef p)
        {
            if (p.pts != null) foreach (var v in p.pts) yield return v;
            if (p.polys != null) foreach (var (pts, _) in p.polys) foreach (var v in pts) yield return v;
        }

        static List<PartDef> Added(RigDef helmeted, RigDef bare)
        {
            var known = new HashSet<string>(Fingerprints(bare));
            return helmeted.parts.Where(p => !known.Contains(Fingerprint(p))).ToList();
        }

        // ---------------------------------------------------------------- no helmet drawing -> Kage unchanged

        [Test]
        public void KageWithNoHelmetDrawingIsDrawnExactlyAsBefore()
        {
            var bare = Fingerprints(Bare());
            foreach (var id in NoDrawing)
            {
                var d = Kage(id);
                CollectionAssert.AreEqual(bare, Fingerprints(d), $"helmet '{id}' has no drawing, so Kage must be unchanged");
                Assert.IsTrue(d.scarf);
            }
        }

        [Test]
        public void HelmetsOfLaterActsAreNotDrawnAndRaiseNothing()
        {
            foreach (var id in new[] { "hakobi_gasa", "utsushi_men", "deshi_tenugui", "mokushi_kabuto", "kasa", "shiro_maki" })
                Assert.IsFalse(HelmetVisuals.Has(id), id);
            // the fall back is silent: a defaulted character spawns through its part set without a log line
            Assert.DoesNotThrow(() => Kage("kasa"));
        }

        // ---------------------------------------------------------------- the 13 drawn helmets

        [Test]
        public void EveryAct1HelmetWithADrawingChangesKagesHead()
        {
            Assert.AreEqual(13, Drawn.Length);
            var bare = Bare();
            foreach (var id in Drawn)
            {
                Assert.IsTrue(HelmetVisuals.Has(id), id);
                var added = Added(Kage(id), bare);
                Assert.IsNotEmpty(added, $"{id} draws nothing");
                Assert.IsTrue(added.Any(p => p.bone == B.Head && p.name.StartsWith("Helmet_")), $"{id}: nothing on the head");
                // the head bone, or the chest for the neck wrap (it winds round the neck like the scarf wrap)
                foreach (var p in added.Where(p => p.name.StartsWith("Helmet_")))
                    Assert.IsTrue(p.bone == B.Head || (p.bone == B.Chest && p.name.Contains("_Wrap")), $"{id}: part {p.name} on {p.bone}");
            }
        }

        [Test]
        public void NoTwoHelmetsLookAlike()
        {
            var seen = new Dictionary<string, string>();
            foreach (var id in Drawn)
            {
                string key = string.Join("\n", Fingerprints(Kage(id)));
                Assert.IsFalse(seen.ContainsKey(key), $"{id} is drawn like {(seen.TryGetValue(key, out var other) ? other : "")}");
                seen[key] = id;
            }
        }

        [Test]
        public void HelmetsOnlyRedrawTheHeadAndNeverMoveAGameplayJoint()
        {
            var bare = Bare();
            foreach (var id in Drawn)
            {
                var d = Kage(id);
                for (int i = 0; i < (int)B.COUNT; i++)
                {
                    Assert.AreEqual(bare.pivot[i], d.pivot[i], $"{id}: pivot {(B)i}");
                    Assert.AreEqual(bare.parent[i], d.parent[i], $"{id}: parent {(B)i}");
                }
                Assert.AreEqual(bare.hurt.Count, d.hurt.Count, id);
                for (int i = 0; i < bare.hurt.Count; i++)
                {
                    Assert.AreEqual(bare.hurt[i].local, d.hurt[i].local, id);
                    Assert.AreEqual(bare.hurt[i].radius, d.hurt[i].radius, id);
                }
                // everything that isn't the head or its hair is exactly as before
                var body = Fingerprints(bare).Where(f => !f.StartsWith("Head_Hair|")).ToList();
                var now = Fingerprints(d).Where(f => body.Contains(f)).ToList();
                CollectionAssert.AreEquivalent(body, now, $"{id}: the body changed");
            }
        }

        [Test]
        public void HelmetArtStaysAroundTheHead()
        {
            var bare = Bare();
            Vector2 head = bare.pivot[(int)B.Head];
            foreach (var id in Drawn)
            {
                foreach (var p in Added(Kage(id), bare))
                    foreach (var v in Vertices(p))
                    {
                        Assert.That(Mathf.Abs(v.x - head.x), Is.LessThan(1.0f), $"{id}/{p.name} x");
                        Assert.That(v.y - head.y, Is.InRange(-0.45f, 0.8f), $"{id}/{p.name} y");
                    }
            }
        }

        // ---------------------------------------------------------------- hair and scarf

        [Test]
        public void HoodsCapsAndClosedKabutoCoverTheHairAndLeaveABareHead()
        {
            foreach (var id in HairHidden)
            {
                var d = Kage(id);
                Assert.IsFalse(d.parts.Any(p => p.soft == SoftKind.Hair), $"{id}: hair should be covered");
                Assert.IsTrue(d.parts.Any(p => p.name == "Head" && p.bone == B.Head), $"{id}: a bare head stands in for the hair");
                Assert.IsTrue(d.parts.Any(p => p.name == "Eye"), $"{id}: the eye stays");
            }
        }

        [Test]
        public void ABrowBandKeepsTheHairMovingUnderIt()
        {
            var bare = Bare();
            var hair = bare.parts.First(p => p.soft == SoftKind.Hair);
            foreach (var id in HairKept)
            {
                var d = Kage(id);
                var h = d.parts.FirstOrDefault(p => p.soft == SoftKind.Hair);
                Assert.IsNotNull(h, $"{id}: hair is kept");
                Assert.AreEqual(Fingerprint(hair), Fingerprint(h), $"{id}: the hair is drawn as before");
                Assert.AreEqual(bare.hairCentre, d.hairCentre, id);
                // the band is drawn over the hair, the eye over the band (the knot tails hang behind the head)
                foreach (var p in Added(d, bare).Where(p => (p.polys != null || p.pts != null) && p.soft != SoftKind.Chain && p.bone == B.Head))
                    Assert.Greater(p.order, h.order, $"{id}/{p.name} must be over the hair");
            }
        }

        [Test]
        public void TheScarfAndEyeAreTheSameUnderEveryHelmet()
        {
            var bare = Bare();
            var scarfParts = bare.parts.Where(p => p.name.StartsWith("Scarf")).Select(Fingerprint).ToList();
            foreach (var id in Drawn)
            {
                var d = Kage(id);
                Assert.IsTrue(d.scarf, id);
                Assert.AreEqual(bare.scarfColor, d.scarfColor, id);
                Assert.AreEqual(bare.scarfBack, d.scarfBack, id);
                Assert.AreEqual(bare.scarfAnchorA, d.scarfAnchorA, id);
                Assert.AreEqual(bare.scarfAnchorB, d.scarfAnchorB, id);
                CollectionAssert.AreEqual(scarfParts, d.parts.Where(p => p.name.StartsWith("Scarf")).Select(Fingerprint).ToList(), id);
                // only the neck wrap (and Kasumi's face veil) is drawn over the scarf wrap, and under the near arm
                int wrap = d.parts.First(p => p.name == "Scarf_Wrap").order;
                int arm = d.parts.First(p => p.name == "ArmN_Upper").order;
                foreach (var p in Added(d, bare).Where(p => p.order >= wrap))
                {
                    Assert.IsTrue(p.name.Contains("_Wrap") || p.name.EndsWith("_Veil"), $"{id}/{p.name} is drawn over the scarf");
                    Assert.Less(p.order, arm, $"{id}/{p.name} is drawn over the arm");
                }
                // the knot and the two tails still show: the wrap stops short of the knot
                var knot = d.parts.First(p => p.name == "Scarf_Knot");
                Vector2 knotCentre = knot.pts.Aggregate(Vector2.zero, (a, v) => a + v) / knot.pts.Length;
                foreach (var p in Added(d, bare).Where(p => p.order >= wrap))
                    foreach (var poly in Polys(p)) Assert.IsFalse(Inside(poly, knotCentre), $"{id}/{p.name} hides the scarf's knot");
            }
        }

        // ---------------------------------------------------------------- the eye, the tiers and the soft pieces

        static IEnumerable<Vector2[]> Polys(PartDef p)
        {
            if (p.pts != null) yield return p.pts;
            if (p.polys != null) foreach (var (pts, _) in p.polys) yield return pts;
        }

        static bool Inside(Vector2[] poly, Vector2 q)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > q.y) != (poly[j].y > q.y) && q.x < (poly[j].x - poly[i].x) * (q.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }

        [Test]
        public void TheWhiteEyeStaysOnTopUnderEveryHelmet()
        {
            var bare = Bare();
            var eye0 = bare.parts.First(p => p.name == "Eye");
            foreach (var id in Drawn)
            {
                var d = Kage(id);
                var eye = d.parts.First(p => p.name == "Eye");
                Assert.AreEqual(Fingerprint(eye0), Fingerprint(eye), $"{id}: the eye is drawn as before");
                Assert.AreEqual(HelmetVisuals.EyeOrder, eye.order);
                foreach (var p in Added(d, bare).Where(p => p.order >= eye.order))
                    foreach (var poly in Polys(p))
                        foreach (var v in eye.pts) Assert.IsFalse(Inside(poly, v), $"{id}/{p.name} is drawn over the eye");
            }
        }

        [Test]
        public void EveryHelmetIsPaintedInItsTiersPalette()
        {
            var bare = Bare();
            foreach (var id in Drawn)
            {
                var pal = HelmetTiers.Palette(HelmetDesigns.Get(id).tier);
                var allowed = new HashSet<Color> { pal.rim, OpponentKit.Seam };
                foreach (var t in pal.ToneNames) for (int k = 0; k < 3; k++) allowed.Add(pal.Colour(t, k));
                foreach (var p in Added(Kage(id), bare).Where(p => p.name.StartsWith("Helmet_")))
                {
                    var cols = new List<Color>();
                    if (p.pts != null) cols.Add(p.color);
                    if (p.polys != null) cols.AddRange(p.polys.Select(x => x.col));
                    if (p.rimColor.HasValue) cols.Add(p.rimColor.Value);
                    foreach (var c in cols)
                        Assert.IsTrue(allowed.Contains(new Color(c.r, c.g, c.b, 1f)), $"{id}/{p.name}: colour {c} is not in the {HelmetDesigns.Get(id).tier} palette");
                }
            }
        }

        [Test]
        public void SoftPiecesBecomeChainPartsAndGlowCarriesOver()
        {
            var bare = Bare();
            foreach (var id in Drawn)
            {
                var design = HelmetDesigns.Get(id);
                var added = Added(Kage(id), bare);
                foreach (var piece in design.pieces.Where(p => p.kind == PieceKind.Cloth))
                {
                    var part = added.Single(p => p.name == $"Helmet_{design.look}_{piece.name}");
                    Assert.AreEqual(SoftKind.Chain, part.soft, $"{id}/{piece.name}");
                    Assert.AreEqual(piece.chains.Length, part.chains.Count, $"{id}/{piece.name}");
                    Assert.Greater(part.chainFeel.drag, 0f, $"{id}/{piece.name}");
                }
                foreach (var piece in design.pieces.Where(p => p.glow > 1f))
                    Assert.AreEqual(piece.glow, added.Single(p => p.name == $"Helmet_{design.look}_{piece.name}").glow, $"{id}/{piece.name}");
            }
        }

        // ---------------------------------------------------------------- equipped helmet -> character data

        [Test]
        public void KageWearsTheHelmetTheSaveHasEquipped()
        {
            Assert.AreEqual("shiro_maki", KageCharacter.Def().helmet);
            var save = GameRules.DefaultSave(false);
            save.equipped.helmet = "tetsu_kabuto";
            Assert.AreEqual("tetsu_kabuto", KageCharacter.Def(save).helmet);
            // and the part set draws it
            Assert.AreNotEqual(string.Join("\n", Fingerprints(Bare())), string.Join("\n", Fingerprints(new KagePartSet().Build(KageCharacter.Def(save)))));
        }
    }
}
