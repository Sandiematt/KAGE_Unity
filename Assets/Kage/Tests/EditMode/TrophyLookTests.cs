using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kage.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Every armour the Armory offers has its own look on the preview Kage (SAN-90 rework: "each armor should have different design
    /// like first 2 ones"). Seam: the Armory's rows (<see cref="ArmoryView.Items"/>) -> the rig the equipped armour builds. Before the
    /// rework only Ikazuchi-do and Shiden Yoroi had a drawing and the other eight trophies all rendered as Kage's plain red coat.
    /// </summary>
    public class TrophyLookTests
    {
        static string[] Trophies => ArmoryView.Items(ArmoryTab.Armor, ArmorLine.Trophy);
        static string[] Line => ArmoryView.Items(ArmoryTab.Armor, ArmorLine.Line);

        static RigDef Kage(string armor)
        {
            var def = KageCharacter.Def();
            def.armor = armor;
            return new KagePartSet().Build(def);
        }

        static string Fingerprint(RigDef d)
        {
            var sb = new StringBuilder();
            foreach (var p in d.parts)
            {
                sb.Append(p.name).Append('|').Append(p.bone).Append('|').Append(p.order).Append('|').Append(p.color).Append('|');
                if (p.pts != null) foreach (var v in p.pts) sb.Append(v.x.ToString("0.0000")).Append(',').Append(v.y.ToString("0.0000")).Append(';');
                if (p.polys != null)
                    foreach (var (pts, col) in p.polys)
                    {
                        sb.Append('#').Append(col);
                        foreach (var v in pts) sb.Append(v.x.ToString("0.0000")).Append(',').Append(v.y.ToString("0.0000")).Append(';');
                    }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        static float ArmourArea(RigDef d)
        {
            float a = 0f;
            foreach (var p in d.parts.Where(p => p.name.StartsWith("Armor_")))
                foreach (var (pts, _) in p.polys)
                {
                    float s = 0f;
                    for (int i = 0; i < pts.Length; i++) { var u = pts[i]; var v = pts[(i + 1) % pts.Length]; s += u.x * v.y - v.x * u.y; }
                    a += Mathf.Abs(s) / 2f;
                }
            return a;
        }

        [Test]
        public void TheArmoryOffersTenTrophies()
        {
            Assert.AreEqual(10, Trophies.Length);
            CollectionAssert.Contains(Trophies, "shirabe_do");
            CollectionAssert.Contains(Trophies, "kasane_haori");
            CollectionAssert.Contains(Trophies, "tetsumon_do");
            CollectionAssert.Contains(Trophies, "kudamaki_do");
            CollectionAssert.Contains(Trophies, "mei_en_garb");
            CollectionAssert.Contains(Trophies, "kysmei_robe");
        }

        [Test]
        public void EveryTrophyResolvesToADrawnLookNotKagesPlainCoat()
        {
            var bare = Fingerprint(KageModel.Build(KageModel.Look.Kage));
            foreach (var id in Trophies)
            {
                Assert.IsTrue(ArmorVisuals.Has(id), $"{id} has no look: it would render as the plain red coat");
                Assert.AreNotEqual(bare, Fingerprint(Kage(id)), $"{id} renders as Kage's own coat");
            }
        }

        [Test]
        public void EveryArmourTheArmoryOffersHasItsOwnLook()
        {
            var seen = new Dictionary<string, string>();
            foreach (var id in Line.Concat(Trophies).Where(i => JsTables.Armor(i).Tier != "starter"))
            {
                string fp = Fingerprint(Kage(id));
                Assert.IsFalse(seen.TryGetValue(fp, out var other), $"{id} looks exactly like {other}");
                seen[fp] = id;
            }
            // the starter is Kage's plain design, and is the only one
            Assert.AreEqual(1, Line.Count(i => JsTables.Armor(i).Tier == "starter"));
        }

        [Test]
        public void EveryTrophyHasPlatesOverTheChestAndEnoughDrawingToReadAtCardSize()
        {
            foreach (var id in Trophies)
            {
                var d = Kage(id);
                var armour = d.parts.Where(p => p.name.StartsWith("Armor_")).ToList();
                Assert.IsTrue(armour.Any(p => p.bone == B.Chest), $"{id} has nothing on the chest");
                Assert.GreaterOrEqual(armour.Sum(p => p.polys.Count), 12, $"{id} is a handful of shapes: not a design");
                Assert.Greater(ArmourArea(d), 0.01f, $"{id} covers almost nothing");
            }
        }

        [Test]
        public void TrophyLooksAreNamedForTheirJsLookAndKeepTheirOwnPartNames()
        {
            // the part names carry the JS look key (JS ARMORS[id].look), so a look can be found in a rig
            foreach (var id in Trophies)
            {
                string look = JsTables.Armor(id).Look;
                Assert.IsTrue(Kage(id).parts.Any(p => p.name.StartsWith($"Armor_{look}_")), $"{id}: no part is named for its JS look '{look}'");
            }
        }

        [Test]
        public void TrophiesLeaveKagesHairScarfSkeletonAndCoatClothAsTheyWere()
        {
            var bare = KageModel.Build(KageModel.Look.Kage);
            foreach (var id in Trophies)
            {
                var d = Kage(id);
                Assert.AreEqual(bare.scarfColor, d.scarfColor, id);
                Assert.AreEqual(bare.rimColor, d.rimColor, id);
                for (int i = 0; i < (int)B.COUNT; i++) Assert.AreEqual(bare.pivot[i], d.pivot[i], $"{id} pivot {(B)i}");
                CollectionAssert.AreEqual(bare.parts.Single(p => p.name == "Head_Hair").polys.Select(q => q.col), d.parts.Single(p => p.name == "Head_Hair").polys.Select(q => q.col), id);
                Assert.AreEqual(bare.scarfStrands.Count, d.scarfStrands.Count, id);
            }
        }
    }
}
