using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Order Shieldbearer (SAN-98): a catalog entry of character data and its own part set on Kage's skeleton, in the spear family
    /// with the defensive profile and the frontal-armour flag, with a shield you can see. How the flag fights is in
    /// FrontalArmourTests, how he fights in OrderShieldbearerFightTests.
    /// </summary>
    public class OrderShieldbearerTests
    {
        const string Id = "order_shieldbearer";

        [Test]
        public void TheShieldbearerIsACatalogEntryWithTheJsStats()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.IsNotNull(d, "no catalog entry 'order_shieldbearer'");
            Assert.AreEqual("Order Shieldbearer", d.displayName);
            Assert.AreEqual("Order of Silence", d.title);
            Assert.AreEqual(220f, d.jsHealth);
            Assert.AreEqual(220f * 1.1f * 100f / 232f, d.MaxHealth(), 1e-3, "through the shared health scale");
            Assert.AreEqual(0.12f, d.defense, 1e-6);
            Assert.AreEqual(1f, d.damageMul, 1e-6);
            Assert.AreEqual(1f, d.speedMul, 1e-6);
            Assert.AreEqual(1.06f, d.bodyScale, 1e-6);
            Assert.IsFalse(d.IsPlayer);
        }

        [Test]
        public void HeUsesTheSpearSetAndTheDefensiveProfile()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.AreEqual("defensive", d.aiProfile);
            Assert.AreEqual("sentinel_spear", d.weaponId);
            Assert.AreEqual("spear", d.family);
            Assert.AreEqual(WeaponFamilies.Spear, WeaponFamilies.FamilyIdOf(d.weaponId));
            Assert.AreEqual("defensive", AiProfiles.Get(d.aiProfile).id);
            Assert.AreEqual(1f, d.moveRate, 1e-6, "no slowed attacks: the set as authored");
        }

        [Test]
        public void HisShieldIsADataFlagAndNothingElse()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.AreEqual(CharacterFlags.FrontalArmour, d.flags, "frontal armour, and neither Armour nor NoGrab: a throw takes him");
            foreach (var other in new[] { "order_footman", "order_runner", "order_chainthrower", "hollow_shade", "kuroe" })
                Assert.IsFalse(CharacterCatalog.Get(other).flags.HasFlag(CharacterFlags.FrontalArmour), other + " has no shield");
        }

        [Test]
        public void HisPartSetIsRegisteredAndKeepsKagesSkeleton()
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

        [Test]
        public void HeCarriesAVisibleShieldOnTheNearForearmAndNoSwordOfHisOwn()
        {
            var d = CharacterCatalog.Get(Id);
            var rig = CharacterCatalog.PartSet(d.partSet).Build(d);
            var tate = rig.parts.SingleOrDefault(p => p.name == "Tate");
            Assert.IsNotNull(tate, "a part named Tate: the shield");
            Assert.AreEqual(B.ArmNL, tate.bone, "strapped to the near forearm, so it follows the arm");
            Assert.IsNotNull(tate.polys);
            Assert.GreaterOrEqual(tate.polys.Count, 8, "planks, bands, rivets and the mark, not a flat plate");
            var all = tate.polys.SelectMany(q => q.pts).ToArray();
            float w = all.Max(v => v.x) - all.Min(v => v.x), h = all.Max(v => v.y) - all.Min(v => v.y);
            Assert.Greater(h, w * 2.5f, "a tall shield");
            Assert.Greater(h, 0.8f, "taller than three quarters of a metre");
            Assert.IsFalse(rig.parts.Any(p => p.bone == B.Sword), "the spear is the family's, not the part set's");
            var near = rig.parts.Where(p => p.bone == B.HandN).Select(p => p.order).DefaultIfEmpty(int.MaxValue).Min();
            Assert.Less(tate.order, near, "the near hand is drawn over the shield");
        }

        [Test]
        public void HeIsDrawnApartFromTheFootmanAndKage()
        {
            var d = CharacterCatalog.Get(Id);
            var footman = CharacterCatalog.Get("order_footman");
            var rig = CharacterCatalog.PartSet(d.partSet).Build(d);
            var frig = CharacterCatalog.PartSet(footman.partSet).Build(footman);
            var names = rig.parts.Select(p => p.name).ToHashSet();
            var fnames = frig.parts.Select(p => p.name).ToHashSet();
            Assert.IsTrue(names.Contains("Maedate") && names.Contains("Kabuto") && names.Contains("Shikoro"), "a kabuto with a crescent crest and a flared shikoro");
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
