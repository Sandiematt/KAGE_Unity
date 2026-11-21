using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // What the Armory shows (SAN-90): the listing, the selected item's tier, level, stats, perks and lock hint, the stat
    // preview of the next level and what the buttons say. Expected numbers are the ones the JS Armory shows for the same
    // saves (read off its screenshots in Screenshots/review/SAN-90/js), not recomputed from the rules.
    public class ArmoryViewTests
    {
        // the JS review save: 1500 coins, 30 gems, Kagetsu level 3, Tsubame, Kuro Zukin level 2, Ronin Armor
        static SaveData MidGame()
        {
            var s = GameRules.DefaultSave(false);
            s.coins = 1500;
            s.gems = 30;
            s.owned.weapons["kagetsu"] = 3;
            s.owned.weapons["tsubame"] = 1;
            s.owned.helmets["kuro_zukin"] = 2;
            s.owned.armors["ronin_yoroi"] = 1;
            return s;
        }

        static ArmoryView View(SaveData save, ArmoryTab tab, string id = null)
        {
            return ArmoryView.Build(save, tab, id);
        }

        // ---- the listing ----

        [Test]
        public void Weapons_are_listed_cheapest_tier_first_and_the_boss_weapon_last_without_enemy_gear_fists_or_later_act_weapons()
        {
            var v = View(MidGame(), ArmoryTab.Weapons);
            CollectionAssert.AreEqual(
                new[] { "kagetsu", "tsubame", "shizuka", "onikiri", "mizuchi", "yamigama", "raiko_no_tachi" },
                v.Tiles.Select(t => t.Id).ToArray());
        }

        [Test]
        public void The_armor_tab_is_one_list_with_the_shop_armours_first_and_an_owned_reward_after_them()
        {
            var save = MidGame();
            var v = View(save, ArmoryTab.Armor);
            Assert.AreEqual(8, v.Tiles.Length);
            Assert.AreEqual("tachibana_yoroi", v.Tiles[0].Id);
            Assert.AreEqual("mugen_yoroi", v.Tiles[7].Id);
            save.owned.armors["ikazuchi_do"] = 1;
            var withReward = View(save, ArmoryTab.Armor);
            Assert.AreEqual(9, withReward.Tiles.Length);
            Assert.AreEqual("ikazuchi_do", withReward.Tiles[8].Id);
        }

        [Test]
        public void The_armor_tab_opens_on_the_armour_worn_even_a_reward_armour()
        {
            var save = MidGame();
            save.owned.armors["ikazuchi_do"] = 1;
            save.equipped.armor = "ikazuchi_do";
            var v = View(save, ArmoryTab.Armor);
            Assert.AreEqual("ikazuchi_do", v.SelectedId);
            Assert.IsTrue(v.Tiles.Single(t => t.Id == "ikazuchi_do").Equipped);
        }

        [Test]
        public void The_selection_starts_on_what_is_equipped_and_falls_back_to_the_first_item()
        {
            var save = MidGame();
            save.equipped.weapon = "tsubame";
            Assert.AreEqual("tsubame", View(save, ArmoryTab.Weapons).SelectedId);
            Assert.AreEqual("kuro_zukin", View(save, ArmoryTab.Helmets, "kuro_zukin").SelectedId);
            // a worn armour the Armory does not list (a later-act one dev mode wore): the list's first item is selected
            save.equipped.armor = "shirabe_do";
            Assert.AreEqual("tachibana_yoroi", View(save, ArmoryTab.Armor).SelectedId);
            Assert.AreEqual("tsubame", View(save, ArmoryTab.Weapons, "no_such_weapon").SelectedId, "an id the row does not hold falls back to what is worn");
        }

        [Test]
        public void Tiles_say_what_is_owned_equipped_locked_and_what_the_rest_cost()
        {
            var v = View(MidGame(), ArmoryTab.Weapons, "onikiri");
            var kagetsu = v.Tiles[0];
            Assert.IsTrue(kagetsu.Owned);
            Assert.IsTrue(kagetsu.Equipped);
            var onikiri = v.Tiles.Single(t => t.Id == "onikiri");
            Assert.IsFalse(onikiri.Owned);
            Assert.IsTrue(onikiri.Selected);
            Assert.AreEqual(1400, onikiri.Coins);
            Assert.AreEqual(0, onikiri.Gems);
            var raiko = v.Tiles.Single(t => t.Id == "raiko_no_tachi");
            Assert.IsTrue(raiko.Locked);
            Assert.AreEqual("Boss weapon", raiko.LockLabel);
            var helmets = View(MidGame(), ArmoryTab.Helmets);
            Assert.AreEqual(12, helmets.Tiles.Single(t => t.Id == "oni_menpo").Gems);
        }

        // ---- the selected weapon ----

        [Test]
        public void A_weapon_owned_at_level_3_shows_its_stats_and_what_the_next_level_adds()
        {
            var v = View(MidGame(), ArmoryTab.Weapons, "kagetsu");
            Assert.AreEqual("Common · Katana", v.TierLine);
            Assert.AreEqual("common", v.TierId);
            Assert.AreEqual("Kagetsu", v.Name);
            Assert.AreEqual(3, v.Level);
            Assert.AreEqual(8, v.MaxLevel);
            Assert.AreEqual("3 / 8", v.LevelText);
            Assert.IsTrue(v.Equipped);
            CollectionAssert.AreEqual(new[] { "Damage", "Speed", "Reach" }, v.Stats.Select(s => s.Label).ToArray());
            CollectionAssert.AreEqual(new[] { "70", "67", "60" }, v.Stats.Select(s => s.Value).ToArray());
            CollectionAssert.AreEqual(new[] { "+4", "+2", "+1" }, v.Stats.Select(s => s.Preview).ToArray());
            Assert.AreEqual(70 / 1.8, v.Stats[0].Pct, 0.05);
            Assert.AreEqual(4 / 1.8, v.Stats[0].PreviewPct, 0.05);
        }

        [Test]
        public void An_unowned_weapon_shows_level_one_stats_and_no_preview()
        {
            var v = View(MidGame(), ArmoryTab.Weapons, "onikiri");
            Assert.AreEqual("Rare · Cleaver", v.TierLine);
            Assert.AreEqual("Not owned", v.LevelText);
            CollectionAssert.AreEqual(new[] { "82", "38", "54" }, v.Stats.Select(s => s.Value).ToArray());
            Assert.IsTrue(v.Stats.All(s => string.IsNullOrEmpty(s.Preview)));
        }

        [Test]
        public void A_weapon_that_climbs_a_tier_with_levels_shows_the_tier_it_has_reached()
        {
            var save = MidGame();
            save.owned.weapons["kagetsu"] = 4;
            Assert.AreEqual("Rare · Katana", View(save, ArmoryTab.Weapons, "kagetsu").TierLine);
            save.owned.weapons["kagetsu"] = 7;
            Assert.AreEqual("Epic · Katana", View(save, ArmoryTab.Weapons, "kagetsu").TierLine);
        }

        [Test]
        public void A_weapon_names_its_rage_art()
        {
            var onikiri = View(MidGame(), ArmoryTab.Weapons, "onikiri");
            var box = onikiri.Boxes.Single(b => !b.Warn);
            Assert.AreEqual("RAGE ART · Hell Splitter", box.Title);
            StringAssert.StartsWith("A demon leap", box.Text);
        }

        // ---- the buttons ----

        [Test]
        public void An_unowned_item_offers_Buy_with_its_price_and_is_disabled_when_the_coins_are_short()
        {
            var save = MidGame();
            var v = View(save, ArmoryTab.Weapons, "onikiri");
            Assert.AreEqual(ArmoryAction.Buy, v.Primary.Action);
            Assert.AreEqual("Buy", v.Primary.Label);
            Assert.AreEqual(1400, v.Primary.Coins);
            Assert.IsTrue(v.Primary.Enabled);
            Assert.IsFalse(v.Equip.Enabled, "an item you don't own can't be equipped");
            save.coins = 1399;
            Assert.IsFalse(View(save, ArmoryTab.Weapons, "onikiri").Primary.Enabled);
        }

        [Test]
        public void A_gem_item_checks_the_gems_and_the_Mugen_Armor_needs_both()
        {
            var save = MidGame();
            var oni = View(save, ArmoryTab.Helmets, "oni_menpo");
            Assert.AreEqual(12, oni.Primary.Gems);
            Assert.AreEqual(0, oni.Primary.Coins);
            Assert.IsTrue(oni.Primary.Enabled);
            save.gems = 11;
            Assert.IsFalse(View(save, ArmoryTab.Helmets, "oni_menpo").Primary.Enabled);

            save.coins = 4200; save.gems = 24;
            var mugen = View(save, ArmoryTab.Armor, "mugen_yoroi");
            Assert.AreEqual(4200, mugen.Primary.Coins);
            Assert.AreEqual(25, mugen.Primary.Gems);
            Assert.IsFalse(mugen.Primary.Enabled, "one gem short");
            save.gems = 25;
            Assert.IsTrue(View(save, ArmoryTab.Armor, "mugen_yoroi").Primary.Enabled);
        }

        [Test]
        public void An_owned_item_offers_Upgrade_at_the_JS_cost_and_Mastered_at_its_cap()
        {
            var save = MidGame();
            var v = View(save, ArmoryTab.Weapons, "kagetsu");
            Assert.AreEqual(ArmoryAction.Upgrade, v.Primary.Action);
            Assert.AreEqual(630, v.Primary.Coins);
            Assert.IsTrue(v.Primary.Enabled);
            save.coins = 629;
            Assert.IsFalse(View(save, ArmoryTab.Weapons, "kagetsu").Primary.Enabled);
            save.owned.weapons["kagetsu"] = 8;
            var m = View(save, ArmoryTab.Weapons, "kagetsu");
            Assert.AreEqual("Mastered", m.Primary.Label);
            Assert.AreEqual(ArmoryAction.None, m.Primary.Action);
            Assert.IsFalse(m.Primary.Enabled);
            Assert.IsTrue(m.Stats.All(s => string.IsNullOrEmpty(s.Preview)), "nothing left to preview at the cap");
        }

        [Test]
        public void Equip_is_offered_for_owned_gear_and_reads_Equipped_for_what_is_worn()
        {
            var save = MidGame();
            Assert.IsTrue(View(save, ArmoryTab.Weapons, "tsubame").Equip.Enabled);
            Assert.AreEqual("Equip", View(save, ArmoryTab.Weapons, "tsubame").Equip.Label);
            var worn = View(save, ArmoryTab.Weapons, "kagetsu");
            Assert.AreEqual("Equipped", worn.Equip.Label);
            Assert.IsFalse(worn.Equip.Enabled);
        }

        // ---- locked items ----

        [Test]
        public void A_locked_item_says_how_to_unlock_it_and_cannot_be_bought()
        {
            var v = View(MidGame(), ArmoryTab.Weapons, "raiko_no_tachi");
            Assert.AreEqual("Boss weapon · Odachi", v.TierLine);
            Assert.AreEqual("Locked", v.Primary.Label);
            Assert.AreEqual(ArmoryAction.None, v.Primary.Action);
            Assert.IsFalse(v.Primary.Enabled);
            Assert.IsFalse(v.Equip.Enabled);
            var warn = v.Boxes.Single(b => b.Warn);
            Assert.AreEqual("LOCKED", warn.Title);
            Assert.AreEqual("Defeat Raikon", warn.Text);

            var trophy = View(MidGame(), ArmoryTab.Helmets, "sumi_eboshi");
            Assert.AreEqual("Defeat Kuroe, the Ink Censor", trophy.Boxes.Single(b => b.Warn).Text);
        }

        [Test]
        public void A_locked_item_that_the_save_owns_is_not_locked_any_more()
        {
            var save = MidGame();
            save.owned.armors["ikazuchi_do"] = 1;
            var v = View(save, ArmoryTab.Armor, "ikazuchi_do");
            Assert.IsFalse(v.Boxes.Any(b => b.Warn));
            Assert.AreEqual("Mastered", v.Primary.Label, "trophies stay at level 1");
            Assert.IsTrue(v.Equip.Enabled);
        }

        // ---- helmets and armour ----

        [Test]
        public void Armour_shows_defence_health_and_mobility_with_the_level_preview_and_its_perks()
        {
            var v = View(MidGame(), ArmoryTab.Armor, "ronin_yoroi");
            Assert.AreEqual("Common", v.TierLine);
            Assert.AreEqual("1 / 5", v.LevelText);
            CollectionAssert.AreEqual(new[] { "Defense", "Health", "Mobility" }, v.Stats.Select(s => s.Label).ToArray());
            CollectionAssert.AreEqual(new[] { "5%", "+15", "100" }, v.Stats.Select(s => s.Value).ToArray());
            CollectionAssert.AreEqual(new[] { "+1", "+2", "" }, v.Stats.Select(s => s.Preview ?? "").ToArray());
            var perks = v.Boxes.Single();
            Assert.AreEqual("PERKS", perks.Title);
            Assert.AreEqual("+10% dash", perks.Text);
            Assert.AreEqual(ArmoryAction.Upgrade, v.Primary.Action);
            Assert.AreEqual(260, v.Primary.Coins);
        }

        [Test]
        public void A_helmet_with_the_rage_perk_and_an_unowned_state()
        {
            var v = View(MidGame(), ArmoryTab.Helmets, "oni_menpo");
            Assert.AreEqual("Rare", v.TierLine);
            CollectionAssert.AreEqual(new[] { "8%", "+14", "100" }, v.Stats.Select(s => s.Value).ToArray());
            Assert.AreEqual("+20% rage gain", v.Boxes.Single().Text);
        }

        [Test]
        public void Perks_read_the_way_the_JS_Armory_words_them()
        {
            var save = GameRules.SetDevMode(GameRules.DefaultSave(false), true);
            Assert.AreEqual("+2 kunai", View(save, ArmoryTab.Helmets, "sugegasa").Boxes.Single().Text);
            Assert.AreEqual("+12% dash", View(save, ArmoryTab.Helmets, "kuro_zukin").Boxes.Single().Text);
            Assert.AreEqual("20% stun resist · +4% damage", View(save, ArmoryTab.Armor, "ikazuchi_do").Boxes.Single().Text);
        }

        [Test]
        public void An_armour_without_perks_says_so_on_the_armor_tab_only()
        {
            var v = View(MidGame(), ArmoryTab.Armor, "tachibana_yoroi");
            Assert.AreEqual("None: the plain coat he left home in", v.Boxes.Single().Text);
            Assert.IsFalse(View(MidGame(), ArmoryTab.Helmets, "shiro_maki").Boxes.Any());
        }

        // ---- the Kage preview ----

        [Test]
        public void The_preview_loadout_is_the_equipped_gear_with_the_selection_swapped_in()
        {
            var save = MidGame();
            var w = ArmoryView.PreviewSave(save, ArmoryTab.Weapons, "tsubame");
            Assert.AreEqual("tsubame", w.equipped.weapon);
            Assert.AreEqual("shiro_maki", w.equipped.helmet);
            Assert.AreEqual("kagetsu", save.equipped.weapon, "the real save is not touched");
            var h = ArmoryView.PreviewSave(save, ArmoryTab.Helmets, "kuro_zukin");
            Assert.AreEqual("kuro_zukin", h.equipped.helmet);
            Assert.AreEqual("kagetsu", h.equipped.weapon);
            Assert.AreEqual("kuro_zukin", KageCharacter.Def(h).helmet);
            Assert.AreEqual(2, h.owned.helmets["kuro_zukin"]);
            // the armour cards wear the Crimson Scarf, as the JS cards do
            var a = ArmoryView.PreviewSave(save, ArmoryTab.Armor, "ronin_yoroi");
            Assert.AreEqual("ronin_yoroi", KageCharacter.Def(a).armor);
            Assert.AreEqual("shiro_maki", KageCharacter.Def(a).helmet);
        }

        [Test]
        public void An_unowned_weapon_previews_at_level_one_and_an_owned_one_at_its_level()
        {
            var save = MidGame();
            Assert.AreEqual(1, KageCharacter.Def(ArmoryView.PreviewSave(save, ArmoryTab.Weapons, "onikiri")).weaponLevel);
            Assert.AreEqual(3, KageCharacter.Def(ArmoryView.PreviewSave(save, ArmoryTab.Weapons, "kagetsu")).weaponLevel);
        }
    }
}
