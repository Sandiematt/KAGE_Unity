using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kage.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Armour visuals (SAN-85, ADR-0005 gear layering). Seam: the equipped armour id on Kage's <see cref="CharacterDef"/> ->
    /// the rig his part set builds (<c>KagePartSet.Build</c>). The independent source of truth for "unchanged" is
    /// <c>KageModel.Build(Look.Kage)</c>, Kage's own drawing; for "the face stays clear" it is the eye polygon of that same rig.
    /// </summary>
    public class ArmorVisualTests
    {
        // the armours with a drawing: the 7 shop armours after the starter, and every trophy (SAN-85 rework: all 10, each its own look)
        static readonly string[] ShopDrawn = { "ronin_yoroi", "yami_yoroi", "shinsen_yoroi", "raiko_yoroi", "kagehira_yoroi", "yurei_yoroi", "mugen_yoroi" };
        static readonly string[] TrophyDrawn =
        {
            "ikazuchi_do", "shiden_yoroi", "shirabe_do", "kasane_haori", "tetsumon_do", "kudamaki_do", "mei_en_garb", "kysmei_robe", "kurokumo_yoroi", "shinsei_yoroi",
        };
        static readonly string[] Drawn = ShopDrawn.Concat(TrophyDrawn).ToArray();

        // Kage's own look (the starter, whose plain coat is the design itself), retired armours, and bad ids
        static readonly string[] NoDrawing = { "tachibana_yoroi", "kozane_do", "kawa_do", "no_such_armor", "", null };

        static readonly string[] Helmets =
        {
            null, "shiro_maki", "kuro_zukin", "sugegasa", "hachigane", "kasumi_men", "tetsu_kabuto", "oni_menpo", "hoshi_kabuto",
            "shika_kabuto", "utsuro_kabuto", "shikko_zukin", "sumi_eboshi", "hisame_zukin", "raijin_kabuto",
        };

        static RigDef Kage(string armor, string helmet = null)
        {
            var def = KageCharacter.Def();
            def.armor = armor;
            def.helmet = helmet;
            return new KagePartSet().Build(def);
        }

        static RigDef Bare() => KageModel.Build(KageModel.Look.Kage);

        static string Fingerprint(PartDef p)
        {
            var sb = new StringBuilder();
            sb.Append(p.name).Append('|').Append(p.bone).Append('|').Append(p.order).Append('|').Append(p.soft).Append('|').Append(p.color).Append('|').Append(p.rim).Append('|').Append(p.rimColor).Append('|').Append(p.glow);
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

        static IEnumerable<Vector2[]> Polygons(PartDef p)
        {
            if (p.pts != null) yield return p.pts;
            if (p.polys != null) foreach (var (pts, _) in p.polys) yield return pts;
        }

        static List<PartDef> ArmourParts(RigDef d) => d.parts.Where(p => p.name.StartsWith("Armor_")).ToList();

        static bool Contains(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        // ---------------------------------------------------------------- no drawing -> Kage unchanged

        [Test]
        public void KageWithNoArmourDrawingIsDrawnExactlyAsBefore()
        {
            var bare = Bare();
            foreach (var id in NoDrawing)
            {
                var d = Kage(id);
                CollectionAssert.AreEqual(Fingerprints(bare), Fingerprints(d), $"armour '{id}' has no drawing, so Kage must be unchanged");
                Assert.AreEqual(bare.rimColor, d.rimColor, id);
                Assert.AreEqual(bare.scarfColor, d.scarfColor, id);
                Assert.AreEqual(bare.scarfBack, d.scarfBack, id);
                Assert.IsTrue(d.scarf, id);
                Assert.IsFalse(ArmorVisuals.Has(id), id);
            }
        }

        [Test]
        public void TheSevenShopArmoursAndEveryTrophyHaveADrawing()
        {
            CollectionAssert.AreEquivalent(Drawn, ArmorVisuals.Ids.ToArray());
            foreach (var id in Drawn)
            {
                var row = JsTables.Armor(id);
                Assert.IsNotNull(row, id);
                Assert.IsFalse(row.Retired, id);
            }
        }

        // ---------------------------------------------------------------- a drawing -> the body changes

        [Test]
        public void EachArmourWithADrawingChangesKagesBodyAndAddsItsOwnParts()
        {
            var bare = Fingerprints(Bare());
            var seen = new HashSet<string>();
            foreach (var id in Drawn)
            {
                var d = Kage(id);
                var fp = Fingerprints(d);
                CollectionAssert.AreNotEqual(bare, fp, id);
                Assert.IsNotEmpty(ArmourParts(d), $"{id} added no parts");
                Assert.IsTrue(ArmorVisuals.Has(id), id);
                Assert.IsTrue(seen.Add(string.Join("\n", fp)), $"{id} looks the same as another armour");
                Assert.IsTrue(ArmourParts(d).Any(p => p.bone == B.Chest), $"{id} has nothing on the chest");
            }
        }

        [Test]
        public void EveryShopArmourRetonesTheCoatAndScarfAndTheTrophiesLeaveKagesCrimson()
        {
            var bare = Bare();
            string Coat(RigDef d) => string.Join("\n", d.parts.Where(p => p.soft == SoftKind.Coat).Select(p => string.Join(",", p.polys.Select(q => q.col.ToString()))));
            foreach (var id in ShopDrawn)
            {
                var d = Kage(id);
                Assert.AreNotEqual(Coat(bare), Coat(d), $"{id} coat");
                Assert.AreNotEqual(bare.scarfColor, d.scarfColor, $"{id} scarf");
                Assert.AreNotEqual(bare.rimColor, d.rimColor, $"{id} rim");
                Assert.AreNotEqual(string.Join(",", bare.parts.Single(p => p.name == "Head_Hair").polys.Select(q => q.col)),
                                   string.Join(",", d.parts.Single(p => p.name == "Head_Hair").polys.Select(q => q.col)), $"{id} hair strokes");
            }
            foreach (var id in TrophyDrawn)
            {
                var d = Kage(id);
                Assert.AreEqual(bare.scarfColor, d.scarfColor, $"{id} keeps his scarf");
                Assert.AreEqual(bare.scarfBack, d.scarfBack, $"{id} keeps his scarf");
            }
        }

        // ---------------------------------------------------------------- skeleton, scarf and coat behave as before

        [Test]
        public void ArmourNeverTouchesTheSkeletonHurtCirclesScarfRigOrCoatPatches()
        {
            var bare = Bare();
            foreach (var id in Drawn)
            {
                var d = Kage(id);
                for (int i = 0; i < (int)B.COUNT; i++)
                {
                    Assert.AreEqual(bare.pivot[i], d.pivot[i], $"{id} pivot {(B)i}");
                    Assert.AreEqual(bare.parent[i], d.parent[i], $"{id} parent {(B)i}");
                }
                Assert.AreEqual(bare.hurt.Count, d.hurt.Count, id);
                for (int i = 0; i < bare.hurt.Count; i++)
                {
                    Assert.AreEqual(bare.hurt[i].bone, d.hurt[i].bone, id);
                    Assert.AreEqual(bare.hurt[i].local, d.hurt[i].local, id);
                    Assert.AreEqual(bare.hurt[i].radius, d.hurt[i].radius, id);
                }
                Assert.IsTrue(d.scarf, id);
                Assert.AreEqual(bare.scarfAnchorA, d.scarfAnchorA, id);
                Assert.AreEqual(bare.scarfAnchorB, d.scarfAnchorB, id);
                Assert.IsTrue(d.softCoat, id);
                Assert.IsTrue(d.coatTail, id);
                Assert.AreEqual(new[] { bare.coatF0, bare.coatF1, bare.coatB0, bare.coatB1, bare.tailF0, bare.tailF1, bare.tailB0, bare.tailB1 },
                                new[] { d.coatF0, d.coatF1, d.coatB0, d.coatB1, d.tailF0, d.tailF1, d.tailB0, d.tailB1 }, id);
                Assert.AreEqual(bare.hairCentre, d.hairCentre, id);
                Assert.AreEqual(bare.fistLocal, d.fistLocal, id);
                Assert.AreEqual(bare.hiltLocal, d.hiltLocal, id);
            }
        }

        [Test]
        public void TheCoatKeepsItsThreeClothPanelsAndEveryVertexStaysFinite()
        {
            var bare = Bare();
            foreach (var id in Drawn)
            {
                var d = Kage(id);
                foreach (var name in new[] { "Coat", "Coat_Side", "Coat_Back" })
                {
                    var b = bare.parts.Single(p => p.name == name);
                    var p = d.parts.Single(q => q.name == name);
                    Assert.AreEqual(SoftKind.Coat, p.soft, $"{id}/{name}");
                    Assert.AreEqual(b.clothLayer, p.clothLayer, $"{id}/{name}");
                    Assert.AreEqual(b.bone, p.bone, $"{id}/{name}");
                    Assert.AreEqual(b.order, p.order, $"{id}/{name}");
                }
                foreach (var part in d.parts)
                    foreach (var poly in Polygons(part))
                        foreach (var v in poly)
                            Assert.IsTrue(!float.IsNaN(v.x) && !float.IsNaN(v.y) && Mathf.Abs(v.x) < 4f && Mathf.Abs(v.y) < 4f, $"{id}/{part.name}");
            }
        }

        [Test]
        public void ArmourPiecesSitOnTheBodyTheyDressInFrontOfItAndBehindTheScarfWrap()
        {
            var bare = Bare();
            int torso = bare.parts.Single(p => p.name == "Torso").order;
            int wrap = bare.parts.Single(p => p.name == "Scarf_Wrap").order;
            int upper = bare.parts.Single(p => p.name == "ArmN_Upper").order;
            int hair = bare.parts.Single(p => p.name == "Head_Hair").order;
            foreach (var id in Drawn)
            {
                foreach (var p in ArmourParts(Kage(id)))
                {
                    switch (p.bone)
                    {
                        case B.Chest: Assert.Greater(p.order, torso, $"{id}/{p.name}"); Assert.Less(p.order, wrap, $"{id}/{p.name}"); break;
                        case B.ArmNU: Assert.Greater(p.order, upper, $"{id}/{p.name}"); break;
                        case B.Head: Assert.Less(p.order, hair, $"{id}/{p.name} would cover the hair and face"); break;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- helmets and armour together

        [Test]
        public void EveryHelmetAndArmourCombinationBuildsAndNothingCoversTheFace()
        {
            var bare = Bare();
            var eye = bare.parts.First(p => p.name == "Eye");
            Vector2 eyeCentre = eye.pts.Aggregate(Vector2.zero, (a, v) => a + v) / eye.pts.Length;
            int hair = bare.parts.Single(p => p.name == "Head_Hair").order;
            foreach (var helmet in Helmets)
                foreach (var armor in Drawn.Concat(new[] { "tachibana_yoroi" }))
                {
                    var d = Kage(armor, helmet);
                    Assert.IsNotNull(d.parts.Find(p => p.name == "Eye"), $"{helmet}+{armor}");
                    foreach (var p in ArmourParts(d))
                    {
                        if (p.order <= hair) continue;                  // under the head: cannot cover the face
                        foreach (var poly in Polygons(p))
                            Assert.IsFalse(Contains(poly, eyeCentre), $"{armor}/{p.name} covers the eye (with {helmet})");
                    }
                    // the eye is still the topmost thing on the head: nothing of the armour is drawn over it
                    int eyeOrder = d.parts.Find(p => p.name == "Eye").order;
                    foreach (var p in ArmourParts(d).Where(p => p.bone == B.Head)) Assert.Less(p.order, eyeOrder, $"{armor}/{p.name}");
                }
        }

        [Test]
        public void ArmourAndHelmetLayerIndependentlyOnTheSameRig()
        {
            var bare = Bare();
            foreach (var armor in Drawn)
            {
                int armourOnly = Kage(armor).parts.Count - bare.parts.Count;
                int helmetOnly = Kage(null, "tetsu_kabuto").parts.Count - bare.parts.Count;
                Assert.Greater(armourOnly, 0, armor);
                Assert.AreEqual(bare.parts.Count + armourOnly + helmetOnly, Kage(armor, "tetsu_kabuto").parts.Count, armor);
            }
        }

        // ---------------------------------------------------------------- the equipped armour on Kage's data

        [Test]
        public void KagesCharacterDataCarriesTheArmourEquippedInTheSave()
        {
            var save = GameRules.DefaultSave(false);
            Assert.AreEqual("tachibana_yoroi", KageCharacter.Def(save).armor);
            save.owned.armors["raiko_yoroi"] = 1;
            Assert.IsTrue(GameRules.EquipItem(save, GearKind.Armors, "raiko_yoroi"));
            var def = KageCharacter.Def(save);
            Assert.AreEqual("raiko_yoroi", def.armor);
            Assert.IsNotEmpty(ArmourParts(new KagePartSet().Build(def)));
        }

        [Test]
        public void TheDrawingIsDeterministic()
        {
            foreach (var id in Drawn)
                CollectionAssert.AreEqual(Fingerprints(Kage(id)), Fingerprints(Kage(id)), id);
        }
    }
}
