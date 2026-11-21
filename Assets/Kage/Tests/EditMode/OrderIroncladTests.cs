using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Order Ironclad (SAN-100): a catalog entry of character data and its own part set on Kage's skeleton, in the cleaver family
    /// with the heavy profile, the Armour flag and about twice Kage's mass, visibly bulkier than the Footman. How it fights is in
    /// OrderIroncladFightTests.
    /// </summary>
    public class OrderIroncladTests
    {
        const string Id = "order_ironclad";

        [Test]
        public void TheIroncladIsACatalogEntryWithTheJsStats()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.IsNotNull(d, "no catalog entry 'order_ironclad'");
            Assert.AreEqual("Order Ironclad", d.displayName);
            Assert.AreEqual("Order of Silence", d.title);
            Assert.AreEqual(280f, d.jsHealth);
            Assert.AreEqual(280f * 1.35f * 100f / 232f, d.MaxHealth(), 1e-3, "through the shared health scale");
            Assert.AreEqual(0.14f, d.defense, 1e-6);
            Assert.AreEqual(1.15f, d.damageMul, 1e-6);
            Assert.AreEqual(0.9f, d.speedMul, 1e-6);
            Assert.AreEqual(1.14f, d.bodyScale, 1e-6);
            Assert.IsFalse(d.IsPlayer);
        }

        [Test]
        public void ItUsesTheCleaverSetAndTheHeavyProfile()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.AreEqual("heavy", d.aiProfile);
            Assert.AreEqual("raider_cleaver", d.weaponId);
            Assert.AreEqual("cleaver", d.family);
            Assert.AreEqual(WeaponFamilies.Cleaver, WeaponFamilies.FamilyIdOf(d.weaponId));
            Assert.AreEqual("heavy", AiProfiles.Get(d.aiProfile).id);
            Assert.AreEqual(1f, d.moveRate, 1e-6, "no slowed attacks: the set as authored; the weight is in its frames");
        }

        [Test]
        public void ItIsArmouredAndTwiceKagesMass()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.AreEqual(CharacterFlags.Armour, d.flags, "the Armour flag, a Unity addition (the JS foe has no heavyArmor), and no NoGrab");
            Assert.AreEqual(2f, d.mass, 1e-6, "about twice Kage's");
            Assert.AreEqual(1f, CharacterCatalog.Get("kage").mass);
            Assert.Greater(d.mass, CharacterCatalog.Get("order_footman").mass);
        }

        [Test]
        public void ItsPartSetIsRegisteredAndKeepsKagesSkeleton()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.AreEqual(Id, d.partSet);
            var set = CharacterCatalog.PartSet(d.partSet);
            Assert.IsNotNull(set);
            Assert.AreEqual(Id, set.Id);
            var rig = set.Build(d);
            Assert.IsNotEmpty(rig.parts);
            Assert.IsFalse(rig.scarf);
            Assert.IsFalse(rig.softCoat);
            foreach (var p in rig.parts)
            {
                bool drawn = p.pts != null ? p.pts.Length >= 3 : p.polys != null && p.polys.Count > 0;
                Assert.IsTrue(drawn, "part " + p.name + " has no shape");
                if (p.pts != null) Assert.Greater(Mathf.Abs(MeshKit.SignedArea(p.pts)), 1e-6f, "part " + p.name + " is flat");
            }
            Assert.AreEqual(rig.parts.Count, rig.parts.Select(p => p.name).Distinct().Count(), "part names are unique");
            // SAN-100 SVG redesign: the painting's cleaver is cut onto the Sword bone (CleaverVisual leaves it alone), and nothing else is
            CollectionAssert.AreEquivalent(new[] { "Cleaver_Handle", "Cleaver_Blade" }, rig.parts.Where(p => p.bone == B.Sword).Select(p => p.name),
                "the painted cleaver is the only thing on the sword bone");

            var kage = KageModel.Build(KageModel.Look.Kage);
            for (int i = 0; i < (int)B.COUNT; i++)
            {
                Assert.AreEqual(kage.parent[i], rig.parent[i], "parent of " + (B)i);
                Assert.AreEqual(kage.pivot[i], rig.pivot[i], "pivot of " + (B)i);
            }
            Assert.AreEqual(kage.hurt.Count, rig.hurt.Count);
            Assert.AreEqual(kage.bladeBaseLocal, rig.bladeBaseLocal);
            Assert.AreEqual(kage.bladeTipLocal, rig.bladeTipLocal);
            Assert.AreEqual(kage.fistLocal, rig.fistLocal);
        }

        /// <summary>Width of the armour on the trunk (the dō) in the part set's own units.</summary>
        static float DoWidth(string id)
        {
            var d = CharacterCatalog.Get(id);
            var rig = CharacterCatalog.PartSet(d.partSet).Build(d);
            var part = rig.parts.Single(p => p.name == "Do");
            // a smooth cel part (SAN-100 rework) carries its outline as rimPts; the Footman's multi-polygon dō is its plate
            var pts = part.rimPts ?? (part.polys != null && part.pts == null ? part.polys.SelectMany(q => q.pts).ToArray() : part.pts);
            return pts.Max(v => v.x) - pts.Min(v => v.x);
        }

        [Test]
        public void ItIsVisiblyBulkierThanTheFootman()
        {
            Assert.Greater(DoWidth(Id), DoWidth("order_footman") * 1.1f, "a broader dō");
            var iron = CharacterCatalog.Get(Id);
            var footman = CharacterCatalog.Get("order_footman");
            Assert.Greater(iron.bodyScale, footman.bodyScale, "and a bigger build");
            Assert.Greater(iron.bodyScale * DoWidth(Id), footman.bodyScale * DoWidth("order_footman") * 1.25f, "a quarter wider on the screen");
        }

        [Test]
        public void ItIsDrawnApartFromTheFootmanAndKage()
        {
            var d = CharacterCatalog.Get(Id);
            var footman = CharacterCatalog.Get("order_footman");
            var rig = CharacterCatalog.PartSet(d.partSet).Build(d);
            var frig = CharacterCatalog.PartSet(footman.partSet).Build(footman);
            var names = rig.parts.Select(p => p.name).ToHashSet();
            var fnames = frig.parts.Select(p => p.name).ToHashSet();
            Assert.IsTrue(names.Contains("Horn_Near") && names.Contains("Horn_Far") && names.Contains("Throat") && names.Contains("Shikoro"), "horned kabuto, broad shikoro, throat guard");
            Assert.IsFalse(names.Overlaps(new[] { "Ear_Far", "Ear_Near", "Mask", "Saya", "Hood_Tail" }), "nothing of the Footman's fox head or saya");
            Assert.Less(names.Intersect(fnames).Count(), fnames.Count * 0.75, "well under three quarters of the Footman's part names");
            Assert.AreNotEqual(CharacterCatalog.Get("kage").colors.main, d.colors.main);
            foreach (var p in rig.parts)
            {
                Assert.AreNotEqual(Palette.Trim, p.color, p.name);
                Assert.AreNotEqual(Palette.TrimDeep, p.color, p.name);
            }
            Assert.Greater(rig.bodyColor.b, rig.bodyColor.r, "Order slate, not Kage's warm black");
        }
    }
}
