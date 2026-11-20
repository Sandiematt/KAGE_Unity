using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Order Footman (SAN-69): a catalog entry made of character data and a part set on Kage's 20-bone skeleton.
    /// Seams: the catalog (character data, JS stats, weapon family) and the part set's rig (skeleton, hurt circles,
    /// palette). How he fights is in OrderFootmanFightTests.
    /// </summary>
    public class OrderFootmanTests
    {
        const string Id = "order_footman";

        [Test]
        public void TheFootmanIsACatalogEntryWithTheJsStats()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.IsNotNull(d, "no catalog entry 'order_footman'");
            Assert.AreEqual(Id, d.id);
            Assert.AreEqual("Order Footman", d.displayName);
            Assert.AreEqual("Order of Silence", d.title);
            Assert.AreEqual(170f, d.jsHealth);
            Assert.AreEqual(170f * 1.65f * 100f / 232f, d.MaxHealth(), 1e-3, "through the shared health scale");
            Assert.AreEqual(0.054f, d.defense, 1e-6);
            Assert.AreEqual(0.9f, d.damageMul, 1e-6);
            Assert.AreEqual(1f, d.speedMul, 1e-6);
            Assert.AreEqual("regular", d.aiProfile);
            Assert.AreEqual(CharacterFlags.None, d.flags);
            Assert.IsFalse(d.IsPlayer);
        }

        [Test]
        public void HeFightsWithTheKatanaSetAtAMoveRateOfNinetyPercent()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.AreEqual(0.9f, d.moveRate, 1e-6);
            Assert.AreEqual("cutter_blade", d.weaponId);
            Assert.AreEqual("katana", d.family);
            Assert.AreEqual(WeaponFamilies.Katana, WeaponFamilies.FamilyIdOf(d.weaponId), "the JS weapon's family is the katana");
        }

        [Test]
        public void HisPartSetIsRegisteredAndDrawsAnOrderSoldier()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.AreEqual("order_footman", d.partSet);
            var set = CharacterCatalog.PartSet(d.partSet);
            Assert.IsNotNull(set);
            Assert.AreEqual(d.partSet, set.Id);

            var rig = set.Build(d);
            Assert.IsNotEmpty(rig.parts);
            Assert.IsFalse(rig.scarf, "Kage's scarf is not an Order soldier's");
            Assert.IsFalse(rig.softCoat, "no coat cloth");
            foreach (var p in rig.parts)
            {
                bool drawn = p.pts != null ? p.pts.Length >= 3 : p.polys != null && p.polys.Count > 0;
                Assert.IsTrue(drawn, "part " + p.name + " has no shape");
                if (p.pts != null) Assert.Greater(Mathf.Abs(MeshKit.SignedArea(p.pts)), 1e-6f, "part " + p.name + " is flat");
            }
            Assert.AreEqual(rig.parts.Count, rig.parts.Select(p => p.name).Distinct().Count(), "part names are unique");
        }

        [Test]
        public void ArtNeverMovesTheGameplaySkeleton()
        {
            var d = CharacterCatalog.Get(Id);
            var rig = CharacterCatalog.PartSet(d.partSet).Build(d);
            var kage = KageModel.Build(KageModel.Look.Kage);

            for (int i = 0; i < (int)B.COUNT; i++)
            {
                Assert.AreEqual(kage.parent[i], rig.parent[i], "parent of " + (B)i);
                Assert.AreEqual(kage.pivot[i].x, rig.pivot[i].x, 1e-6f, "pivot x of " + (B)i);
                Assert.AreEqual(kage.pivot[i].y, rig.pivot[i].y, 1e-6f, "pivot y of " + (B)i);
            }
            Assert.AreEqual(kage.hurt.Count, rig.hurt.Count);
            for (int i = 0; i < kage.hurt.Count; i++)
            {
                Assert.AreEqual(kage.hurt[i].bone, rig.hurt[i].bone);
                Assert.AreEqual(kage.hurt[i].local, rig.hurt[i].local);
                Assert.AreEqual(kage.hurt[i].radius, rig.hurt[i].radius);
                Assert.AreEqual(kage.hurt[i].zone, rig.hurt[i].zone);
            }
            // the hit points: fist, hilt, foot and the blade's line
            Assert.AreEqual(kage.fistLocal, rig.fistLocal);
            Assert.AreEqual(kage.hiltLocal, rig.hiltLocal);
            Assert.AreEqual(kage.footTipLocal, rig.footTipLocal);
            Assert.AreEqual(kage.heelLocal, rig.heelLocal);
            Assert.AreEqual(kage.bladeBindAngle, rig.bladeBindAngle);
            Assert.AreEqual(kage.bladeBaseLocal, rig.bladeBaseLocal);
            Assert.AreEqual(kage.bladeTipLocal, rig.bladeTipLocal);
        }

        [Test]
        public void HisPaletteIsNotKagesCrimson()
        {
            var d = CharacterCatalog.Get(Id);
            var kage = CharacterCatalog.Get("kage");
            Assert.AreNotEqual(kage.colors.main, d.colors.main);
            Assert.AreNotEqual(kage.colors.dark, d.colors.dark);

            // no drawn part may be Kage's crimson trim, and the rim light is not his red
            var rig = CharacterCatalog.PartSet(d.partSet).Build(d);
            Assert.AreNotEqual(Palette.Rim, rig.rimColor);
            foreach (var p in rig.parts)
            {
                Assert.AreNotEqual(Palette.Trim, p.color, p.name);
                Assert.AreNotEqual(Palette.TrimDeep, p.color, p.name);
            }
            // cool slate overall: the part set's body tone leans blue, not Kage's warm black
            Assert.Greater(rig.bodyColor.b, rig.bodyColor.r);
        }
    }
}
