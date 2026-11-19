using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Hollow Flicker (SAN-101): a catalog entry of character data and its own part set on Kage's skeleton, in the twin family
    /// with the agile profile, clearly not the Shade. How it fights is in HollowFlickerFightTests.
    /// </summary>
    public class HollowFlickerTests
    {
        const string Id = "hollow_flicker";

        [Test]
        public void TheFlickerIsACatalogEntryWithTheJsStats()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.IsNotNull(d, "no catalog entry 'hollow_flicker'");
            Assert.AreEqual("Hollow Flicker", d.displayName);
            Assert.AreEqual("Memory echo", d.title);
            Assert.AreEqual(140f, d.jsHealth);
            Assert.AreEqual(140f * 100f / 232f, d.MaxHealth(), 1e-3, "through the shared health scale");
            Assert.AreEqual(0.02f, d.defense, 1e-6);
            Assert.AreEqual(0.8f, d.damageMul, 1e-6);
            Assert.AreEqual(1.18f, d.speedMul, 1e-6);
            Assert.AreEqual(0.94f, d.bodyScale, 1e-6);
            Assert.AreEqual(CharacterFlags.None, d.flags);
            Assert.IsFalse(d.IsPlayer);
        }

        [Test]
        public void HeUsesTheTwinSetAndTheAgileProfile()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.AreEqual("agile", d.aiProfile);
            Assert.AreEqual("shadow_daggers", d.weaponId);
            Assert.AreEqual("twin", d.family);
            Assert.AreEqual(WeaponFamilies.Twin, WeaponFamilies.FamilyIdOf(d.weaponId));
            Assert.AreEqual("agile", AiProfiles.Get(d.aiProfile).id);
            Assert.AreEqual(1f, d.moveRate, 1e-6, "no slowed attacks: the set as authored");
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
        public void ItIsAHollowDrawnApartFromTheShade()
        {
            var d = CharacterCatalog.Get(Id);
            var shade = CharacterCatalog.Get("hollow_shade");
            var rig = CharacterCatalog.PartSet(d.partSet).Build(d);
            var srig = CharacterCatalog.PartSet(shade.partSet).Build(shade);
            var names = rig.parts.Select(p => p.name).ToHashSet();
            var snames = srig.parts.Select(p => p.name).ToHashSet();
            Assert.IsTrue(names.Contains("Hood") && names.Contains("Tail_Far") && names.Contains("Tail_Near") && names.Contains("Visor") && names.Contains("Chest_Wraps") && names.Contains("Tatter_Long"),
                "the beak hood with two tails, a visor, a wrapped chest and one long tatter");
            Assert.IsFalse(names.Overlaps(new[] { "Shard_1", "Shard_2", "Haori", "Mask", "Saya", "Skirt_F1" }), "nothing of the Shade's crown, haori, skirt or saya");
            Assert.Less(names.Intersect(snames).Count(), snames.Count * 0.6, "well under 60% of the Shade's part names");
            // it reads as a Hollow: the shared palette's rim, cloth and violet glow, and Hollow effect colours
            Assert.AreEqual(HollowPalette.Rim, rig.rimColor);
            Assert.AreEqual(HollowPalette.Cloth, rig.bodyColor);
            Assert.AreEqual(shade.colors.main, d.colors.main);
            Assert.IsTrue(rig.parts.Any(p => p.color == HollowPalette.Violet || (p.polys != null && p.polys.Any(q => q.col == HollowPalette.Violet))), "violet glow along the breaks");
            Assert.AreNotEqual(CharacterCatalog.Get("kage").colors.main, d.colors.main);
            foreach (var p in rig.parts)
            {
                Assert.AreNotEqual(Palette.Trim, p.color, p.name);
                Assert.AreNotEqual(Palette.TrimDeep, p.color, p.name);
            }
        }
    }
}
