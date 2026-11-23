using System.Collections.Generic;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // Buying, upgrading and equipping in the Armory (SAN-90) through the rules port: coins and gems move by the JS rules,
    // every change is saved at once, a refused action changes and saves nothing.
    public class ArmoryControllerTests
    {
        List<SaveData> saved;
        int changes;

        ArmoryController New(SaveData save)
        {
            saved = new List<SaveData>();
            changes = 0;
            return new ArmoryController(save, s => saved.Add(s), () => changes++);
        }

        static SaveData Money(int coins, int gems)
        {
            var s = GameRules.DefaultSave(false);
            s.coins = coins;
            s.gems = gems;
            return s;
        }

        [Test]
        public void Buying_takes_the_price_owns_the_item_at_level_one_and_saves()
        {
            var save = Money(1500, 30);
            var a = New(save);
            a.Select("onikiri");
            Assert.IsTrue(a.Buy());
            Assert.AreEqual(100, save.coins);
            Assert.AreEqual(1, save.owned.weapons["onikiri"]);
            Assert.AreEqual(1, saved.Count);
            Assert.AreSame(save, saved[0]);
            Assert.AreEqual("Upgrade", a.View.Primary.Label, "the same item now offers an upgrade");
        }

        [Test]
        public void Buying_with_gems_takes_the_gems_not_the_coins()
        {
            var save = Money(1500, 30);
            var a = New(save);
            a.SelectTab(ArmoryTab.Helmets);
            a.Select("oni_menpo");
            Assert.IsTrue(a.Buy());
            Assert.AreEqual(18, save.gems);
            Assert.AreEqual(1500, save.coins);
        }

        [Test]
        public void The_Mugen_Armor_takes_coins_and_gems_together()
        {
            var save = Money(5000, 30);
            var a = New(save);
            a.SelectTab(ArmoryTab.Armor);
            a.Select("mugen_yoroi");
            Assert.IsTrue(a.Buy());
            Assert.AreEqual(800, save.coins);
            Assert.AreEqual(5, save.gems);
        }

        [Test]
        public void A_purchase_that_cannot_be_afforded_changes_and_saves_nothing()
        {
            var save = Money(1399, 30);
            var a = New(save);
            a.Select("onikiri");
            int before = changes;
            Assert.IsFalse(a.Buy());
            Assert.AreEqual(1399, save.coins);
            Assert.IsFalse(save.owned.weapons.ContainsKey("onikiri"));
            Assert.AreEqual(0, saved.Count);
            Assert.AreEqual(before, changes);
        }

        [Test]
        public void Locked_gear_cannot_be_bought()
        {
            var save = Money(99999, 999);
            var a = New(save);
            a.SelectTab(ArmoryTab.Helmets);
            a.Select("sumi_eboshi");
            Assert.AreEqual("sumi_eboshi", a.SelectedId);
            Assert.IsFalse(a.Buy());
            Assert.IsFalse(save.owned.helmets.ContainsKey("sumi_eboshi"));
        }

        [Test]
        public void Upgrading_takes_the_JS_cost_and_raises_the_level_and_saves()
        {
            var save = Money(1000, 0);
            save.owned.weapons["kagetsu"] = 3;
            var a = New(save);
            Assert.IsTrue(a.Upgrade());
            Assert.AreEqual(370, save.coins, "630 coins");
            Assert.AreEqual(4, save.owned.weapons["kagetsu"]);
            Assert.AreEqual(1, saved.Count);
        }

        [Test]
        public void An_upgrade_you_cannot_pay_or_past_the_cap_does_nothing()
        {
            var save = Money(300, 0);
            var a = New(save);
            Assert.IsFalse(a.Upgrade(), "Kagetsu level 1 costs 320");
            save.coins = 99999;
            save.owned.weapons["kagetsu"] = 8;
            Assert.IsFalse(a.Upgrade());
            Assert.AreEqual(99999, save.coins);
            Assert.AreEqual(0, saved.Count);
        }

        [Test]
        public void Equipping_wears_an_owned_item_and_saves_and_refuses_one_that_is_not_owned()
        {
            var save = Money(0, 0);
            save.owned.weapons["tsubame"] = 1;
            var a = New(save);
            a.Select("tsubame");
            Assert.IsTrue(a.Equip());
            Assert.AreEqual("tsubame", save.equipped.weapon);
            Assert.AreEqual(1, saved.Count);
            a.Select("onikiri");
            Assert.IsFalse(a.Equip());
            Assert.AreEqual("tsubame", save.equipped.weapon);
            Assert.AreEqual(1, saved.Count);
        }

        [Test]
        public void Equipping_other_gear_changes_the_loadout_the_fights_are_built_from()
        {
            var save = Money(0, 0);
            save.owned.helmets["kuro_zukin"] = 2;
            save.owned.armors["ronin_yoroi"] = 1;
            string before = KageCharacter.LoadoutKey(save);
            var a = New(save);
            a.SelectTab(ArmoryTab.Helmets);
            a.Select("kuro_zukin");
            a.Equip();
            a.SelectTab(ArmoryTab.Armor);
            a.Select("ronin_yoroi");
            a.Equip();
            Assert.AreNotEqual(before, KageCharacter.LoadoutKey(save));
            var def = KageCharacter.Def(save);
            Assert.AreEqual("kuro_zukin", def.helmet);
            Assert.AreEqual("ronin_yoroi", def.armor);
        }

        [Test]
        public void An_upgrade_of_the_worn_weapon_changes_its_level_in_the_loadout()
        {
            var save = Money(1000, 0);
            var a = New(save);
            a.Upgrade();
            Assert.AreEqual(2, KageCharacter.Def(save).weaponLevel);
        }

        // ---- state: tab, selection, row ----

        [Test]
        public void Changing_tab_selects_what_is_worn_in_that_tab_and_every_change_is_announced()
        {
            var save = Money(0, 0);
            var a = New(save);
            Assert.AreEqual(ArmoryTab.Weapons, a.Tab);
            Assert.AreEqual("kagetsu", a.View.SelectedId);
            a.SelectTab(ArmoryTab.Helmets);
            Assert.AreEqual("shiro_maki", a.View.SelectedId);
            a.Select("kuro_zukin");
            Assert.AreEqual("kuro_zukin", a.View.SelectedId);
            a.SelectTab(ArmoryTab.Armor);
            Assert.AreEqual("tachibana_yoroi", a.View.SelectedId);
            Assert.AreEqual(3, changes);
        }

        [Test]
        public void Tabs_step_with_Prev_and_Next_and_stop_at_the_ends()
        {
            var a = New(Money(0, 0));
            a.StepTab(-1);
            Assert.AreEqual(ArmoryTab.Weapons, a.Tab);
            a.StepTab(1);
            Assert.AreEqual(ArmoryTab.Helmets, a.Tab);
            a.StepTab(1);
            a.StepTab(1);
            Assert.AreEqual(ArmoryTab.Armor, a.Tab);
        }

        [Test]
        public void The_Armor_tab_is_one_list_and_opens_on_the_armour_worn()
        {
            var save = Money(0, 0);
            save.owned.armors["shiden_yoroi"] = 1;
            save.equipped.armor = "shiden_yoroi";
            var a = New(save);
            a.SelectTab(ArmoryTab.Armor);
            Assert.AreEqual("shiden_yoroi", a.View.SelectedId, "a reward armour is in the list once owned");
            a.Select("tachibana_yoroi");
            Assert.AreEqual("tachibana_yoroi", a.View.SelectedId, "the shop armours are in the same list");
            Assert.AreEqual(9, a.View.Tiles.Length);
        }

        [Test]
        public void A_selection_that_is_not_in_the_tab_is_ignored()
        {
            var a = New(Money(0, 0));
            a.Select("fists");
            Assert.AreEqual("kagetsu", a.View.SelectedId);
            a.Select("raikon_tachi");
            Assert.AreEqual("kagetsu", a.View.SelectedId);
            a.Select("shinsen");
            Assert.AreEqual("kagetsu", a.View.SelectedId, "a later-act weapon is not listed, so it cannot be selected");
        }
    }
}
