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

        static SaveData SaveWith(string weapon = "kagetsu", int weaponLevel = 1, string helmet = "shiro_maki", int helmetLevel = 1,
            string armor = "tachibana_yoroi", int armorLevel = 1, int level = 1)
        {
            var s = GameRules.DefaultSave(false);
            s.level = level;
            s.owned.weapons[weapon] = weaponLevel;
            s.owned.helmets[helmet] = helmetLevel;
            s.owned.armors[armor] = armorLevel;
            s.equipped.weapon = weapon;
            s.equipped.helmet = helmet;
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
            Assert.AreEqual(0.03f, d.defense, 1e-6f, "starter helmet 0.01 + starter armour 0.02");
            Assert.AreEqual(1f, d.damageMul);
            Assert.AreEqual(1f, d.speedMul);
            Assert.IsTrue(d.perks.IsNone);
            Assert.AreEqual("kagetsu", d.weaponId);
            Assert.AreEqual(1, d.weaponLevel);
            Assert.AreEqual(cat.jsHealth, d.jsHealth);
            Assert.AreEqual(cat.defense, d.defense);
            Assert.AreEqual(cat.damageMul, d.damageMul);
            Assert.AreEqual(cat.speedMul, d.speedMul);
            Assert.AreEqual(cat.weaponId, d.weaponId);
            Assert.AreEqual(cat.helmet, d.helmet);
            Assert.AreEqual(cat.armor, d.armor);
        }

        [Test]
        public void ANullSaveIsAFreshSave()
        {
            var d = KageCharacter.Def(null);
            Assert.AreEqual(232f, d.jsHealth);
            Assert.AreEqual("shiro_maki", d.helmet);
        }

        // ---- health and defence ----

        [Test]
        public void UpgradedGearGivesTheJsHealthThroughTheHealthScale()
        {
            // Tetsu Kabuto L5: 14 hp x 1.4 = 19.6 -> 20; Mugen Armor L5: 55 x 1.4 = 77; 220 + 20 + 77 = 317
            var d = KageCharacter.Def(SaveWith(helmet: "tetsu_kabuto", helmetLevel: 5, armor: "mugen_yoroi", armorLevel: 5));
            Assert.AreEqual(317f, d.jsHealth);
            Assert.AreEqual(317f * Scale, d.MaxHealth(), 1e-3);
        }

        [Test]
        public void PlayerLevelAddsFourJsHealthPerLevel()
        {
            var d = KageCharacter.Def(SaveWith(level: 11));
            Assert.AreEqual(220f + 40f + 4f + 8f, d.jsHealth);
        }

        [Test]
        public void DefenceIsHelmetPlusArmourAndGrowsPerLevel()
        {
            // Tetsu Kabuto L5: 0.09 + 4 x 0.012 = 0.138; Mugen Armor L5: 0.16 + 4 x 0.012 = 0.208
            var d = KageCharacter.Def(SaveWith(helmet: "tetsu_kabuto", helmetLevel: 5, armor: "mugen_yoroi", armorLevel: 5));
            Assert.AreEqual(0.346f, d.defense, 1e-5f);
        }

        [Test]
        public void DefenceIsCappedAtHalf()
        {
            // Raijin Kabuto (legendary: the best helmet stats, 0.2 / 40) L5 0.248 + Shinsei Yoroi L5 0.308 = 0.556
            var d = KageCharacter.Def(SaveWith(helmet: "raijin_kabuto", helmetLevel: 5, armor: "shinsei_yoroi", armorLevel: 5));
            Assert.AreEqual(0.5f, d.defense, 1e-6f);
            Assert.AreEqual(220f + 56f + 140f, d.jsHealth);
        }

        [Test]
        public void HealthAndDefenceMatchTheRulesPortForSeveralLoadouts()
        {
            var loadouts = new[]
            {
                SaveWith(),
                SaveWith("kagetsu", 8, "hachigane", 3, "yami_yoroi", 2, level: 4),
                SaveWith("onikiri", 5, "oni_menpo", 5, "kagehira_yoroi", 5, level: 9),
                SaveWith("shizuka", 2, "kuro_zukin", 1, "ronin_yoroi", 5),
                SaveWith("kagetsu", 1, "raijin_kabuto", 5, "shinsei_yoroi", 5, level: 20),
            };
            foreach (var s in loadouts)
            {
                var d = KageCharacter.Def(s);
                var r = GameRules.PlayerCombatStats(s);
                Assert.AreEqual(r.Hp, d.jsHealth, s.equipped.helmet + "/" + s.equipped.armor);
                Assert.AreEqual((float)r.Defense, d.defense, 1e-6f, s.equipped.helmet + "/" + s.equipped.armor);
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
        public void PerksAreSummedOverHelmetAndArmour()
        {
            // Mugen Armor: dmg 0.07, rage 0.2, stunRes 0.2; Tetsu Kabuto: stunRes 0.3
            var d = KageCharacter.Def(SaveWith(helmet: "tetsu_kabuto", armor: "mugen_yoroi"));
            Assert.AreEqual(0.5f, d.perks.stunRes, 1e-6f);
            Assert.AreEqual(0.2f, d.perks.rage, 1e-6f);
            Assert.AreEqual(0.07f, d.perks.dmg, 1e-6f);
            Assert.AreEqual(0f, d.perks.dash);
        }

        [Test]
        public void SpeedAndDamagePerksBecomeTheCharactersMultipliers()
        {
            // Yami Armor: speed 0.04, dash 0.1; Raijin Kabuto: stunRes 0.3, dmg 0.05
            var d = KageCharacter.Def(SaveWith(helmet: "raijin_kabuto", armor: "yami_yoroi"));
            Assert.AreEqual(1.04f, d.speedMul, 1e-6f);
            Assert.AreEqual(1.05f, d.damageMul, 1e-6f);
            Assert.AreEqual(0.1f, d.perks.dash, 1e-6f);
        }

        [Test]
        public void PlayerLevelRaisesDamageByOnePointTwoPercentPerLevel()
        {
            Assert.AreEqual(1.12f, KageCharacter.Def(SaveWith(level: 11)).damageMul, 1e-6f);
            // Raijin Kabuto's 5 % on top: 1.12 x 1.05
            Assert.AreEqual(1.176f, KageCharacter.Def(SaveWith(helmet: "raijin_kabuto", level: 11)).damageMul, 1e-5f);
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
            // Sugegasa: ammo 2; Raiko Armor: speed 0.04, kunai 0.4
            var d = KageCharacter.Def(SaveWith(helmet: "sugegasa", armor: "raiko_yoroi"));
            Assert.AreEqual(7, d.perks.KunaiStart);
            Assert.AreEqual(1.4f, d.perks.KunaiMul, 1e-6f);
        }
    }
}
