using System.Linq;
using Kage.Rules;
using NUnit.Framework;

namespace Kage.Tests
{
    /// <summary>
    /// The owner's weapon tiers (2026-11-23): one common weapon, Mumei, the plain starting katana; rare from Kagetsu; then epic; and the
    /// last two legendary (the rainbow tier). The tables are never edited: Mumei is a Unity row and the tiers are a Unity layer
    /// (<see cref="GameRules.WeaponTiers"/>), as the legendary helmets are.
    /// </summary>
    public class WeaponTierTests
    {
        static string Tier(string id) => GameRules.Item(GearKind.Weapons, id).Tier;

        [Test]
        public void One_common_then_rare_from_Kagetsu_then_epic_and_the_last_two_legendary()
        {
            Assert.AreEqual("common", Tier("mumei"));
            Assert.AreEqual("rare", Tier("kagetsu"));
            Assert.AreEqual("rare", Tier("tsubame"));
            Assert.AreEqual("epic", Tier("shizuka"));
            Assert.AreEqual("epic", Tier("onikiri"));
            Assert.AreEqual("epic", Tier("mizuchi"));
            Assert.AreEqual(JsTables.Legendary.Id, Tier("yamigama"));
            Assert.AreEqual(JsTables.Legendary.Id, Tier("iwakudaki"));
            var listed = JsTables.AllWeapons.Select(w => w.Id).Where(id => ArmoryListing.Listed(GearKind.Weapons, id)).ToList();
            Assert.AreEqual(1, listed.Count(id => Tier(id) == "common"), "there is only one common weapon: " + string.Join(", ", listed));
            Assert.AreEqual(2, listed.Count(id => Tier(id) == JsTables.Legendary.Id), "the last two are legendary");
        }

        [Test]
        public void The_tiers_climb_with_the_price()
        {
            var shop = new[] { "mumei", "kagetsu", "tsubame", "shizuka", "onikiri", "mizuchi", "yamigama", "iwakudaki" };
            for (int i = 1; i < shop.Length; i++)
            {
                var a = GameRules.Item(GearKind.Weapons, shop[i - 1]);
                var b = GameRules.Item(GearKind.Weapons, shop[i]);
                Assert.LessOrEqual(a.Price, b.Price, shop[i - 1] + " costs no more than " + shop[i]);
                Assert.LessOrEqual(JsTables.Tier(a.Tier).Rank, JsTables.Tier(b.Tier).Rank, shop[i - 1] + " is no higher a tier than " + shop[i]);
            }
            Assert.AreEqual(0, GameRules.Item(GearKind.Weapons, "mumei").Price, "the starter is free");
            Assert.Greater(GameRules.Item(GearKind.Weapons, "kagetsu").Price, 0, "Kagetsu is sold now");
        }

        [Test]
        public void Mumei_is_a_plain_katana_measured_x1_against_Kagetsu()
        {
            var m = JsTables.Weapon("mumei");
            Assert.IsNotNull(m, "a Unity weapon row, found by id like a JS row");
            Assert.IsNull(System.Array.Find(JsTables.Weapons, w => w.Id == "mumei"), "the generated JS table is untouched");
            CollectionAssert.Contains(JsTables.AllWeapons, m);
            Assert.AreEqual("katana", WeaponFamilies.FamilyIdOf("mumei"));
            var c = WeaponStats.Combat("mumei", 1);
            Assert.AreEqual(1f, c.dmg, 1e-6);
            Assert.AreEqual(1f, c.speed, 1e-6);
            Assert.AreEqual(1f, c.reach, 1e-6);
            Assert.IsTrue(ArmoryListing.Listed(GearKind.Weapons, "mumei"), "the Armory lists it");
            Assert.IsTrue(GearArt.HasRender("mumei"), "with its own tile render");
        }

        [Test]
        public void A_new_save_starts_with_Mumei_and_every_save_always_owns_it()
        {
            var fresh = GameRules.DefaultSave(false);
            Assert.AreEqual("mumei", GameRules.StarterWeaponId);
            Assert.AreEqual("mumei", fresh.equipped.weapon);
            Assert.IsTrue(GameRules.Owns(fresh, GearKind.Weapons, "mumei"));
            Assert.IsFalse(GameRules.Owns(fresh, GearKind.Weapons, "kagetsu"), "Kagetsu must be bought now");
            // an older save that started with Kagetsu keeps it, and is given Mumei
            var old = GameRules.DefaultSave(false);
            old.owned.weapons.Clear();
            old.owned.weapons["kagetsu"] = 3;
            old.equipped.weapon = "kagetsu";
            GameRules.NormalizeSave(old);
            Assert.AreEqual("kagetsu", old.equipped.weapon);
            Assert.AreEqual(3, old.owned.weapons["kagetsu"]);
            Assert.IsTrue(GameRules.Owns(old, GearKind.Weapons, "mumei"));
            Assert.AreEqual("mumei", KageCharacter.Def(fresh).weaponId, "a fresh Kage fights with Mumei");
        }

        [Test]
        public void The_reference_Kage_of_the_catalog_keeps_Kagetsu()
        {
            Assert.AreEqual("kagetsu", CharacterCatalog.Get(KageCharacter.Id).weaponId, "fight tests, the baseline replay and the dev arena measure against Kagetsu, x1");
        }

        [Test]
        public void The_legendary_weapons_have_a_rage_art_and_Armory_words_of_their_own()
        {
            Assert.IsTrue(ArmoryText.RageArt("iwakudaki", out var hammerArt, out _));
            Assert.AreEqual("Earthsplitter", hammerArt);
            Assert.IsTrue(ArmoryText.RageArt("mumei", out var plainArt, out _));
            Assert.AreEqual("Thousand Shadows", plainArt);
            Assert.AreEqual("無銘", ArmoryText.Kanji(ArmoryTab.Weapons, "mumei"));
        }
    }
}
