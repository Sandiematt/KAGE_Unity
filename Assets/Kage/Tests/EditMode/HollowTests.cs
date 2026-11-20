using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Hollow Shade and the tutorial Hollow (SAN-76): catalog entries made of character data and part sets on Kage's
    /// 20-bone skeleton, sharing the Hollow palette, fighting with the katana family. How they fight is in HollowFightTests.
    /// </summary>
    public class HollowTests
    {
        static readonly string[] Ids = { "hollow_shade", "hollow_tutorial" };

        [Test]
        public void TheShadeIsACatalogEntryWithTheJsStats()
        {
            var d = CharacterCatalog.Get("hollow_shade");
            Assert.IsNotNull(d, "no catalog entry 'hollow_shade'");
            Assert.AreEqual("Hollow Shade", d.displayName);
            Assert.AreEqual("Memory echo", d.title);
            Assert.AreEqual(160f, d.jsHealth);
            Assert.AreEqual(160f * 1.4f * 100f / 232f, d.MaxHealth(), 1e-3);
            Assert.AreEqual(0.03f, d.defense, 1e-6);
            Assert.AreEqual(0.85f, d.damageMul, 1e-6);
            Assert.AreEqual("regular", d.aiProfile);
            Assert.AreEqual(CharacterFlags.None, d.flags);
            Assert.IsFalse(d.IsPlayer);
            Assert.AreEqual(1f, d.attackRate, 1e-6, "the Shade attacks as often as the regular profile does");
        }

        [Test]
        public void TheTutorialHollowIsGentleSlowAndRare()
        {
            var d = CharacterCatalog.Get("hollow_tutorial");
            Assert.IsNotNull(d, "no catalog entry 'hollow_tutorial'");
            Assert.AreEqual(95f, d.jsHealth);
            Assert.AreEqual(95f * 100f / 232f, d.MaxHealth(), 1e-3);
            Assert.AreEqual(0f, d.defense, 1e-6);
            Assert.AreEqual(0.3f, d.damageMul, 1e-6);
            Assert.AreEqual("regular", d.aiProfile);
            Assert.Less(d.moveRate, 0.9f, "slower than the Footman");
            Assert.Less(d.attackRate, 0.5f, "attacks rarely");
            Assert.Greater(d.attackRate, 0f, "but does attack");
        }

        [Test]
        public void BothUseTheKatanaFamily()
        {
            foreach (var id in Ids)
            {
                var d = CharacterCatalog.Get(id);
                Assert.AreEqual("katana", d.family, id);
                Assert.AreEqual("cutter_blade", d.weaponId, id);
                Assert.AreEqual(WeaponFamilies.Katana, WeaponFamilies.FamilyIdOf(d.weaponId), id);
            }
        }

        [Test]
        public void BothAreRegisteredWithTheirOwnPartSets()
        {
            foreach (var id in Ids)
            {
                var d = CharacterCatalog.Get(id);
                Assert.AreEqual(id, d.partSet);
                var set = CharacterCatalog.PartSet(d.partSet);
                Assert.IsNotNull(set, id);
                Assert.AreEqual(id, set.Id);
                var rig = set.Build(d);
                Assert.IsNotEmpty(rig.parts, id);
                Assert.IsFalse(rig.scarf, id);
                Assert.IsFalse(rig.softCoat, id);
                foreach (var p in rig.parts)
                {
                    bool drawn = p.pts != null ? p.pts.Length >= 3 : p.polys != null && p.polys.Count > 0;
                    Assert.IsTrue(drawn, id + " part " + p.name + " has no shape");
                    if (p.pts != null) Assert.Greater(Mathf.Abs(MeshKit.SignedArea(p.pts)), 1e-6f, id + " part " + p.name + " is flat");
                }
                Assert.AreEqual(rig.parts.Count, rig.parts.Select(p => p.name).Distinct().Count(), id + ": part names are unique");
            }
            Assert.AreNotEqual(CharacterCatalog.Get("hollow_shade").partSet, CharacterCatalog.Get("hollow_tutorial").partSet);
        }

        [Test]
        public void ArtNeverMovesTheGameplaySkeleton()
        {
            var kage = KageModel.Build(KageModel.Look.Kage);
            foreach (var id in Ids)
            {
                var d = CharacterCatalog.Get(id);
                var rig = CharacterCatalog.PartSet(d.partSet).Build(d);
                for (int i = 0; i < (int)B.COUNT; i++)
                {
                    Assert.AreEqual(kage.parent[i], rig.parent[i], id + " parent of " + (B)i);
                    Assert.AreEqual(kage.pivot[i].x, rig.pivot[i].x, 1e-6f, id + " pivot x of " + (B)i);
                    Assert.AreEqual(kage.pivot[i].y, rig.pivot[i].y, 1e-6f, id + " pivot y of " + (B)i);
                }
                Assert.AreEqual(kage.hurt.Count, rig.hurt.Count, id);
                Assert.AreEqual(kage.bladeBaseLocal, rig.bladeBaseLocal, id);
                Assert.AreEqual(kage.bladeTipLocal, rig.bladeTipLocal, id);
            }
        }

        [Test]
        public void BothShareTheHollowPaletteAndAreNotOrderOrKage()
        {
            var kage = CharacterCatalog.Get("kage");
            var footman = CharacterCatalog.Get("order_footman");
            foreach (var id in Ids)
            {
                var d = CharacterCatalog.Get(id);
                Assert.AreEqual(HollowPalette.Glow, d.colors.main, id + " effect colour is the Hollow violet");
                Assert.AreNotEqual(kage.colors.main, d.colors.main, id);
                Assert.AreNotEqual(footman.colors.main, d.colors.main, id);
                var rig = CharacterCatalog.PartSet(d.partSet).Build(d);
                Assert.AreEqual(HollowPalette.Rim, rig.rimColor, id);
                Assert.AreNotEqual(OrderPalette.Rim, rig.rimColor, id);
                Assert.Greater(rig.bodyColor.b, rig.bodyColor.g, id + ": violet-leaning cloth");
                foreach (var p in rig.parts)
                {
                    Assert.AreNotEqual(Palette.Trim, p.color, p.name);
                    Assert.AreNotEqual(OrderPalette.Lacing, p.color, p.name);
                }
            }
        }

        [Test]
        public void TheTwoHollowsLookDifferentFromEachOther()
        {
            var shade = CharacterCatalog.PartSet("hollow_shade").Build(CharacterCatalog.Get("hollow_shade"));
            var tut = CharacterCatalog.PartSet("hollow_tutorial").Build(CharacterCatalog.Get("hollow_tutorial"));
            CollectionAssert.AreNotEquivalent(shade.parts.Select(p => p.name), tut.parts.Select(p => p.name));
        }

        [Test]
        public void TheDefaultAttackRateIsOne()
        {
            Assert.AreEqual(1f, CharacterCatalog.Get("order_footman").attackRate, 1e-6);
            Assert.AreEqual(1f, CharacterCatalog.Get("ronin").attackRate, 1e-6);
        }
    }
}
