using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The loadout in the save becomes Kage's character data (SAN-84, ADR-0005): health and damage reduction by the JS rules
    /// through the shared health scale, weapon and level, the summed helmet and armour perks. Expected values are worked by hand
    /// from the JS data (def and hp per item, +0.012 def and +10 % hp per level, 220 base health + 4 per player level, defence
    /// capped at 0.5), so they can disagree with the code.
    /// </summary>
    public class GearLoadoutTests
    {
        const float Scale = 100f / 232f;

        static SaveData SaveWith(string weapon = "kagetsu", int weaponLevel = 1, 
            string armor = "tachibana_yoroi", int armorLevel = 1, int level = 1)
        {
            var s = GameRules.DefaultSave(false);
            s.level = level;
            s.owned.weapons[weapon] = weaponLevel;
            s.owned.armors[armor] = armorLevel;
            s.equipped.weapon = weapon;
            s.equipped.armor = armor;
            return s;
        }

        // ---- the starter loadout is the Kage the baseline was recorded with ----

        [Test]
        public void TheStarterLoadoutBuildsTheSameKageAsTheCatalogAndChangesNothing()
        {
            var d = KageCharacter.Def(GameRules.DefaultSave(false));
            var cat = CharacterCatalog.Get(KageCharacter.Id);
            Assert.AreEqual(232f, d.jsHealth);
            Assert.AreEqual(100f, d.MaxHealth(), 1e-3);
            Assert.AreEqual(0.03f, d.defense, 1e-6f, "base defense plus starter armor");
            Assert.AreEqual(1f, d.damageMul);
            Assert.AreEqual(1f, d.speedMul);
            Assert.IsTrue(d.perks.IsNone);
            Assert.AreEqual("mumei", d.weaponId, "a new save starts with Mumei (the catalog's reference Kage keeps Kagetsu, x1 like Mumei)");
            Assert.AreEqual(1, d.weaponLevel);
            Assert.AreEqual(cat.jsHealth, d.jsHealth);
            Assert.AreEqual(cat.defense, d.defense);
            Assert.AreEqual(cat.damageMul, d.damageMul);
            Assert.AreEqual(cat.speedMul, d.speedMul);
            Assert.AreEqual("kagetsu", cat.weaponId, "the catalog's reference Kage holds Kagetsu, the katana family's x1 weapon");
            Assert.AreEqual(1, cat.weaponLevel);
            Assert.AreEqual(cat.hairstyle, d.hairstyle);
            Assert.AreEqual(cat.armor, d.armor);
        }

        [Test]
        public void ANullSaveIsAFreshSave()
        {
            var d = KageCharacter.Def(null);
            Assert.AreEqual(232f, d.jsHealth);
            Assert.AreEqual("spike", d.hairstyle);
        }

        // ---- health and defence ----

        [Test]
        public void UpgradedGearGivesTheJsHealthThroughTheHealthScale()
        {
            var d = KageCharacter.Def(SaveWith(armor: "mugen_yoroi", armorLevel: 5));
            Assert.AreEqual(301f, d.jsHealth);
            Assert.AreEqual(301f * Scale, d.MaxHealth(), 1e-3);
        }

        [Test]
        public void PlayerLevelAddsFourJsHealthPerLevel()
        {
            var d = KageCharacter.Def(SaveWith(level: 11));
            Assert.AreEqual(220f + 40f + 4f + 8f, d.jsHealth);
        }

        [Test]
        public void ArmorDefenseGrowsWithItsLevel()
        {
            var d = KageCharacter.Def(SaveWith(armor: "mugen_yoroi", armorLevel: 5));
            Assert.AreEqual(0.218f, d.defense, 1e-5f);
        }

        [Test]
        public void HighTierArmorUsesItsOwnDefenseAndHealth()
        {
            var d = KageCharacter.Def(SaveWith(armor: "shinsei_yoroi", armorLevel: 5));
            Assert.AreEqual(0.318f, d.defense, 1e-6f);
            Assert.AreEqual(224f + 140f, d.jsHealth);
        }

        [Test]
        public void HealthAndDefenceMatchTheRulesPortForSeveralLoadouts()
        {
            var loadouts = new[]
            {
                SaveWith(),
                SaveWith("kagetsu", 8, "yami_yoroi", 2, level: 4),
                SaveWith("onikiri", 5, "kagehira_yoroi", 5, level: 9),
                SaveWith("shizuka", 2, "ronin_yoroi", 5),
                SaveWith("kagetsu", 1, "shinsei_yoroi", 5, level: 20),
            };
            foreach (var s in loadouts)
            {
                var d = KageCharacter.Def(s);
                var r = GameRules.PlayerCombatStats(s);
                Assert.AreEqual(r.Hp, d.jsHealth, s.equipped.hairstyle + "/" + s.equipped.armor);
                Assert.AreEqual((float)r.Defense, d.defense, 1e-6f, s.equipped.hairstyle + "/" + s.equipped.armor);
                Assert.AreEqual(r.Hp * Scale, d.MaxHealth(), 1e-3f);
                Assert.LessOrEqual(d.defense, 0.5f);
            }
        }

        // ---- weapon ----

        [Test]
        public void TheEquippedWeaponAndItsLevelReachTheCharacter()
        {
            var d = KageCharacter.Def(SaveWith("shizuka", 6));
            Assert.AreEqual("shizuka", d.weaponId);
            Assert.AreEqual(6, d.weaponLevel);
            Assert.AreEqual("katana", d.family);
        }

        // ---- perks ----

        [Test]
        public void EveryPerkInTheJsDataIsOneThisGameKnows()
        {
            var keys = new HashSet<string>(JsTables.Helmets.Concat(JsTables.Armors).Where(g => g.Perks != null).SelectMany(g => g.Perks.Keys));
            CollectionAssert.AreEquivalent(new[] { "dash", "speed", "ammo", "stunRes", "rage", "kunai", "dmg", "blockRage" }, keys);
            foreach (var k in keys) CollectionAssert.Contains(GearPerks.Keys, k);
        }

        [Test]
        public void EveryGearPieceReadsItsPerksWithoutLoss()
        {
            foreach (var g in JsTables.Helmets.Concat(JsTables.Armors).Where(g => g.Perks != null))
            {
                var p = GearPerks.FromJs(g.Perks);
                foreach (var kv in g.Perks) Assert.AreEqual((float)kv.Value, p.Get(kv.Key), 1e-6f, g.Id + " " + kv.Key);
            }
        }

        [Test]
        public void ArmorPerksReachTheCharacter()
        {
            var d = KageCharacter.Def(SaveWith(armor: "mugen_yoroi"));
            Assert.AreEqual(0.2f, d.perks.stunRes, 1e-6f);
            Assert.AreEqual(0.2f, d.perks.rage, 1e-6f);
            Assert.AreEqual(0.07f, d.perks.dmg, 1e-6f);
            Assert.AreEqual(0f, d.perks.dash);
        }

        [Test]
        public void SpeedAndDamagePerksBecomeTheCharactersMultipliers()
        {
            var d = KageCharacter.Def(SaveWith(armor: "yami_yoroi"));
            Assert.AreEqual(1.04f, d.speedMul, 1e-6f);
            Assert.AreEqual(1f, d.damageMul, 1e-6f);
            Assert.AreEqual(0.1f, d.perks.dash, 1e-6f);
        }

        [Test]
        public void PlayerLevelRaisesDamageByOnePointTwoPercentPerLevel()
        {
            Assert.AreEqual(1.12f, KageCharacter.Def(SaveWith(level: 11)).damageMul, 1e-6f);
            Assert.AreEqual(1.1984f, KageCharacter.Def(SaveWith(armor: "mugen_yoroi", level: 11)).damageMul, 1e-5f);
        }

        [Test]
        public void ADownsidePerkSlowsHim()
        {
            // Kurogane-dō (a retired enemy armour): speed -0.08, stunRes 0.25
            var d = KageCharacter.Def(SaveWith(armor: "kurogane_do"));
            Assert.AreEqual(0.92f, d.speedMul, 1e-6f);
        }

        [Test]
        public void AmmoAndKunaiPerksSetTheKunaiCountAndDamage()
        {
            var d = KageCharacter.Def(SaveWith(armor: "raiko_yoroi"));
            Assert.AreEqual(5, d.perks.KunaiStart);
            Assert.AreEqual(1.4f, d.perks.KunaiMul, 1e-6f);
        }
    }
}
