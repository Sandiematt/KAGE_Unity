using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Hollow Husk (SAN-112): a catalog entry of character data and its own part set on Kage's skeleton, in the fists family with the
    /// heavy profile, the Armour flag and two and a half times Kage's mass, a Hollow and visibly bulkier than the Shade. How it fights is
    /// in HollowHuskFightTests.
    /// </summary>
    public class HollowHuskTests
    {
        const string Id = "hollow_husk";

        [Test]
        public void TheHuskIsACatalogEntryWithTheJsStats()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.IsNotNull(d, "no catalog entry 'hollow_husk'");
            Assert.AreEqual("Hollow Husk", d.displayName);
            Assert.AreEqual("Memory echo", d.title);
            Assert.AreEqual(270f, d.jsHealth);
            Assert.AreEqual(270f * 1.4f * 100f / 232f, d.MaxHealth(), 1e-3, "through the shared health scale");
            Assert.AreEqual(0.1f, d.defense, 1e-6);
            Assert.AreEqual(1.15f, d.damageMul, 1e-6);
            Assert.AreEqual(0.88f, d.speedMul, 1e-6);
            Assert.AreEqual(1.16f, d.bodyScale, 1e-6);
            Assert.IsFalse(d.IsPlayer);
        }

        [Test]
        public void ItUsesTheHeavyFistsSetAndTheHeavyProfile()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.AreEqual("heavy", d.aiProfile);
            Assert.AreEqual("iron_fists", d.weaponId);
            Assert.AreEqual("fists", d.family);
            Assert.AreEqual(WeaponFamilies.Fists, WeaponFamilies.FamilyIdOf(d.weaponId));
            Assert.AreEqual("heavy", AiProfiles.Get(d.aiProfile).id);
            Assert.AreEqual(1f, d.moveRate, 1e-6, "no slowed attacks: the set as authored");
        }

        [Test]
        public void ItIsArmouredAndTwoAndAHalfTimesKagesMass()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.AreEqual(CharacterFlags.Armour, d.flags, "the Armour flag and nothing else: a throw takes it");
            Assert.AreEqual(2.5f, d.mass, 1e-6);
            Assert.AreEqual(1f, CharacterCatalog.Get("kage").mass);
            Assert.Greater(d.mass, CharacterCatalog.Get("order_ironclad")?.mass ?? 2f, "heavier than the Ironclad");
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
            Assert.IsFalse(rig.parts.Any(p => p.bone == B.Sword), "no blade: the fists are the weapon");

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

        /// <summary>Width of the trunk (the gi every part set draws as the "Torso" part) in the part set's own units.</summary>
        static float Width(string id)
        {
            var d = CharacterCatalog.Get(id);
            var rig = CharacterCatalog.PartSet(d.partSet).Build(d);
            var pts = rig.parts.Where(p => p.name == "Torso").SelectMany(p => p.pts).ToArray();
            return pts.Max(v => v.x) - pts.Min(v => v.x);
        }

        [Test]
        public void ItIsVisiblyBulkierThanTheShade()
        {
            var husk = CharacterCatalog.Get(Id);
            var shade = CharacterCatalog.Get("hollow_shade");
            Assert.Greater(husk.bodyScale, shade.bodyScale, "a bigger build");
            Assert.Greater(Width(Id), Width("hollow_shade") * 1.15f, "a broader trunk in its own right");
            Assert.Greater(husk.bodyScale * Width(Id), shade.bodyScale * Width("hollow_shade") * 1.25f, "a quarter wider on the screen");
        }

        [Test]
        public void ItReadsAsAHollowNotAnOrderSoldier()
        {
            var d = CharacterCatalog.Get(Id);
            var rig = CharacterCatalog.PartSet(d.partSet).Build(d);
            Assert.AreEqual(HollowPalette.Rim, rig.rimColor, "the Hollows' violet back-light");
            Assert.Greater(rig.bodyColor.b, rig.bodyColor.g * 1.2f, "violet-black cloth");
            Assert.AreEqual(HollowPalette.Glow, d.colors.main, "the Hollow effect colour");
            var names = rig.parts.Select(p => p.name).ToHashSet();
            Assert.IsTrue(names.Contains("Mask") && names.Contains("Eye") && names.Contains("Chest_Cracks"), "a cracked mask, a cold eye, a chest of glowing cracks");
            Assert.IsTrue(names.Contains("Mantle") && names.Contains("Shimenawa"), "the brute's mantle and rope belt");
            Assert.IsTrue(rig.parts.Any(p => p.name == "Chest_Cracks" && p.polys.Any(q => q.col == HollowPalette.Violet)), "the cracks glow violet");
            foreach (var p in rig.parts)
            {
                Assert.AreNotEqual(Palette.Trim, p.color, p.name);
                Assert.AreNotEqual(Palette.TrimDeep, p.color, p.name);
            }
        }

        [Test]
        public void ItIsDrawnApartFromTheShadeAndKage()
        {
            var d = CharacterCatalog.Get(Id);
            var shade = CharacterCatalog.Get("hollow_shade");
            var rig = CharacterCatalog.PartSet(d.partSet).Build(d);
            var srig = CharacterCatalog.PartSet(shade.partSet).Build(shade);
            var names = rig.parts.Select(p => p.name).ToHashSet();
            var snames = srig.parts.Select(p => p.name).ToHashSet();
            Assert.IsFalse(names.Overlaps(new[] { "Saya", "Shard_1", "Shard_2", "Blade", "Haori" }), "nothing of the Shade's crown, saya or blade");
            Assert.Less(names.Intersect(snames).Count(), snames.Count * 0.75, "well under three quarters of the Shade's part names");
            Assert.AreNotEqual(CharacterCatalog.Get("kage").colors.main, d.colors.main);
        }

        [Test]
        public void StretchFourEndsOnTheHuskAndTheHuskIsARealCharacter()
        {
            var data = Kage.Rules.StretchData.FromStage("act1.stretch4");
            var last = data.Encounters.Last().Waves.Last();
            CollectionAssert.Contains(last, Id, "the JS data puts the Husk in the last wave of stretch IV");
            Assert.IsNotNull(CharacterCatalog.Get(Id));
        }
    }
}
