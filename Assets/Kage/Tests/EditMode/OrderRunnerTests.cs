using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The Order Runner (SAN-97): a catalog entry of character data and its own part set on Kage's skeleton, in the twin family
    /// with the agile profile, clearly not the Footman. How he fights is in OrderRunnerFightTests.
    /// </summary>
    public class OrderRunnerTests
    {
        const string Id = "order_runner";

        [Test]
        public void TheRunnerIsACatalogEntryWithTheJsStats()
        {
            var d = CharacterCatalog.Get(Id);
            Assert.IsNotNull(d, "no catalog entry 'order_runner'");
            Assert.AreEqual("Order Runner", d.displayName);
            Assert.AreEqual("Order of Silence", d.title);
            Assert.AreEqual(150f, d.jsHealth);
            Assert.AreEqual(150f * 1.35f * 100f / 232f, d.MaxHealth(), 1e-3, "through the shared health scale");
            Assert.AreEqual(0.03f, d.defense, 1e-6);
            Assert.AreEqual(0.85f, d.damageMul, 1e-6);
            Assert.AreEqual(1.12f, d.speedMul, 1e-6);
            Assert.AreEqual(0.97f, d.bodyScale, 1e-6);
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
        public void HeIsDrawnApartFromTheFootmanAndKage()
        {
            var d = CharacterCatalog.Get(Id);
            var footman = CharacterCatalog.Get("order_footman");
            var rig = CharacterCatalog.PartSet(d.partSet).Build(d);
            var frig = CharacterCatalog.PartSet(footman.partSet).Build(footman);
            var names = rig.parts.Select(p => p.name).ToHashSet();
            var fnames = frig.parts.Select(p => p.name).ToHashSet();
            // his own head and no fox ears, no sode, no saya
            Assert.IsTrue(names.Contains("Hood_Peak") && names.Contains("Tail_Far") && names.Contains("Tail_Near"), "the peaked hood with two tails");
            Assert.IsFalse(names.Overlaps(new[] { "Ear_Far", "Ear_Near", "Mask", "ArmN_Sode", "ArmF_Sode", "Saya" }), "nothing of the Footman's head or sode");
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
