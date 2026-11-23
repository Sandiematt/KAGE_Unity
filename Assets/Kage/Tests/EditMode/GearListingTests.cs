using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// What the Armory lists (SAN-85, SAN-108): only gear that exists in Unity. An item is listed when it is drawn on Kage in fights and
    /// the player can get it in the Prologue or Act I; gear planned for a later act, and enemy-only or retired gear, stays in the
    /// tables, the save and the rules but is not shown. There is no Trophies section: an Act I reward armour sits in the one armour
    /// list, after the shop armours, once the save owns it. The expected ids are the JS tables' (the shop and the Act I rewards in
    /// <c>10-data.js</c>), written out here and not read back from the code under test.
    /// </summary>
    public class GearListingTests
    {
        static SaveData Fresh() { return GameRules.DefaultSave(false); }

        // dev mode owns every weapon, helmet and armour in the tables, later acts included
        static SaveData Dev() { return GameRules.SetDevMode(GameRules.DefaultSave(false), true); }

        static string[] Weapons(SaveData s) { return ArmoryView.Items(ArmoryTab.Weapons, s); }
        static string[] Helmets(SaveData s) { return ArmoryView.Items(ArmoryTab.Helmets, s); }
        static string[] Armours(SaveData s) { return ArmoryView.Items(ArmoryTab.Armor, s); }

        static readonly string[] ShopArmours =
            { "tachibana_yoroi", "ronin_yoroi", "yami_yoroi", "shinsen_yoroi", "raiko_yoroi", "kagehira_yoroi", "yurei_yoroi", "mugen_yoroi" };

        // ---- weapons ----

        [Test]
        public void Weapons_list_the_shop_weapons_and_nothing_else()
        {
            CollectionAssert.AreEqual(
                new[] { "kagetsu", "tsubame", "shizuka", "onikiri", "mizuchi", "yamigama", "iwakudaki" },
                Weapons(Dev()));
            CollectionAssert.AreEqual(Weapons(Dev()), Weapons(Fresh()), "what is owned does not change what is listed");
        }

        [TestCase("shinsen")]
        [TestCase("raiko_no_tachi")]
        [TestCase("kagehira_yurei")]
        [TestCase("mugen_no_ha")]
        public void A_withheld_boss_weapon_is_not_listed_even_when_dev_mode_owns_it(string id)
        {
            Assert.IsNotNull(GameRules.Item(GearKind.Weapons, id), "the table row stays for later acts");
            Assert.IsTrue(GameRules.IsWithheld(GearKind.Weapons, id));
            Assert.IsTrue(GameRules.Owns(Dev(), GearKind.Weapons, id), "and so does the rules' ownership");
            CollectionAssert.DoesNotContain(Weapons(Dev()), id);
            Assert.AreEqual(ArmoryClass.LaterAct, ArmoryListing.Classify(GearKind.Weapons, id));
        }

        // ---- helmets ----

        [Test]
        public void Helmets_list_the_starter_the_ten_shop_helmets_and_the_three_Act_I_rewards()
        {
            var shop = new[] { "kuro_zukin", "sugegasa", "hachigane", "kasumi_men", "tetsu_kabuto", "oni_menpo", "hoshi_kabuto", "shika_kabuto" };
            var rewards = new[] { "sumi_eboshi", "hisame_zukin" };
            var legendary = new[] { "utsuro_kabuto", "shikko_zukin", "raijin_kabuto" };
            var listed = Helmets(Dev());
            Assert.AreEqual(14, listed.Length);
            Assert.AreEqual("shiro_maki", listed[0], "the starter first");
            CollectionAssert.AreEquivalent(new[] { "shiro_maki" }.Concat(shop).Concat(rewards).Concat(legendary).ToArray(), listed);
            foreach (var r in rewards)
                foreach (var s in shop)
                    Assert.Less(System.Array.IndexOf(listed, s), System.Array.IndexOf(listed, r), r + " after " + s);
            CollectionAssert.AreEquivalent(legendary, listed.Skip(listed.Length - 3).ToArray(), "the legendary helmets last");
        }

        [TestCase("hakobi_gasa")]
        [TestCase("utsushi_men")]
        [TestCase("deshi_tenugui")]
        [TestCase("mokushi_kabuto")]
        [TestCase("kasa")]
        public void A_later_act_helmet_with_no_drawing_is_not_listed_but_stays_in_the_tables(string id)
        {
            Assert.IsNotNull(JsTables.Helmet(id), "the JS-derived row stays");
            Assert.IsFalse(HelmetVisuals.Has(id), "it has no drawing on Kage");
            CollectionAssert.DoesNotContain(Helmets(Dev()), id);
            Assert.AreEqual(ArmoryClass.LaterAct, ArmoryListing.Classify(GearKind.Helmets, id));
        }

        // ---- armour: no Trophies section ----

        [Test]
        public void A_fresh_save_lists_the_eight_shop_armours_and_no_trophy_row()
        {
            CollectionAssert.AreEqual(ShopArmours, Armours(Fresh()));
        }

        [Test]
        public void An_Act_I_reward_armour_joins_the_main_list_after_the_shop_armours_once_it_is_owned()
        {
            var s = Fresh();
            s.owned.armors["ikazuchi_do"] = 1;
            CollectionAssert.AreEqual(ShopArmours.Concat(new[] { "ikazuchi_do" }).ToArray(), Armours(s));
            s.owned.armors["shiden_yoroi"] = 1;
            CollectionAssert.AreEqual(ShopArmours.Concat(new[] { "ikazuchi_do", "shiden_yoroi" }).ToArray(), Armours(s));
        }

        [Test]
        public void An_owned_reward_armour_can_be_selected_and_equipped_from_the_main_list()
        {
            var s = Fresh();
            s.owned.armors["shiden_yoroi"] = 1;
            var a = new ArmoryController(s, null, null);
            a.SelectTab(ArmoryTab.Armor);
            a.Select("shiden_yoroi");
            Assert.AreEqual("shiden_yoroi", a.SelectedId);
            Assert.IsTrue(a.View.Equip.Enabled);
            Assert.IsTrue(a.Equip());
            Assert.AreEqual("shiden_yoroi", s.equipped.armor);
            // worn, it opens selected on the armour tab with the shop armours around it
            a.SelectTab(ArmoryTab.Weapons);
            a.SelectTab(ArmoryTab.Armor);
            Assert.AreEqual("shiden_yoroi", a.SelectedId);
            Assert.AreEqual(9, a.View.Tiles.Length);
        }

        [Test]
        public void Dev_mode_lists_the_two_Act_I_reward_armours_and_none_of_the_eight_later_ones()
        {
            var listed = Armours(Dev());
            CollectionAssert.AreEqual(ShopArmours.Concat(new[] { "ikazuchi_do", "shiden_yoroi" }).ToArray(), listed);
            foreach (var id in new[] { "shirabe_do", "kasane_haori", "tetsumon_do", "kudamaki_do", "mei_en_garb", "kysmei_robe", "kurokumo_yoroi", "shinsei_yoroi" })
            {
                CollectionAssert.DoesNotContain(listed, id);
                Assert.IsNotNull(JsTables.Armor(id), "the table row stays for later acts: " + id);
                Assert.IsTrue(ArmorVisuals.Has(id), "and so does its drawing, which a later act turns back on: " + id);
                Assert.AreEqual(ArmoryClass.LaterAct, ArmoryListing.Classify(GearKind.Armors, id), id);
            }
        }

        [Test]
        public void The_armour_view_never_shows_a_reward_armour_the_save_does_not_own()
        {
            var s = Dev();
            s.owned.armors.Remove("ikazuchi_do");
            s.owned.armors.Remove("shiden_yoroi");
            var v = ArmoryView.Build(s, ArmoryTab.Armor);
            CollectionAssert.AreEqual(ShopArmours, v.Tiles.Select(t => t.Id).ToArray());
            Assert.IsTrue(v.Tiles.All(t => !t.Locked && t.LockLabel == null), "no locked Trophy card");
        }

        // ---- the classification ----

        [Test]
        public void Enemy_only_and_retired_gear_is_a_placeholder_and_never_listed()
        {
            foreach (var id in new[] { "kawa_do", "hai_haori", "kasumi_shroud", "soryo_e", "kozane_do", "raiun_mantle", "shikko_garb", "o_sode", "kurogane_do", "goka_yoroi" })
                Assert.AreEqual(ArmoryClass.Placeholder, ArmoryListing.Classify(GearKind.Armors, id), id);
            Assert.AreEqual(ArmoryClass.Placeholder, ArmoryListing.Classify(GearKind.Weapons, "shadow_daggers"));
            Assert.AreEqual(ArmoryClass.Placeholder, ArmoryListing.Classify(GearKind.Weapons, "no_such_weapon"));
            Assert.IsFalse(ArmoryListing.Listed(GearKind.Weapons, "fists"), "bare hands are what you fight with, not an item");
        }

        [Test]
        public void Every_listed_item_is_drawn_on_Kage_and_the_player_can_get_it_in_Act_I()
        {
            foreach (var kind in new[] { GearKind.Weapons, GearKind.Helmets, GearKind.Armors })
            {
                var tab = kind == GearKind.Weapons ? ArmoryTab.Weapons : kind == GearKind.Helmets ? ArmoryTab.Helmets : ArmoryTab.Armor;
                foreach (var id in ArmoryView.Items(tab, Dev()))
                {
                    var it = GameRules.Item(kind, id);
                    Assert.IsTrue(ArmoryListing.Drawn(it), id + " is drawn");
                    bool shop = !it.Exclusive;
                    bool reward = GameRules.Stages.Any(st => st.Reward != null &&
                        st.Reward.Weapons.Concat(st.Reward.Helmets).Concat(st.Reward.Armors).Contains(id));
                    Assert.IsTrue(shop || reward, id + " is sold or rewarded in the Prologue or Act I");
                }
            }
        }

        [Test]
        public void The_Armory_lists_thirty_one_of_the_forty_eight_items_a_player_could_hold()
        {
            int all = 0, listed = 0;
            foreach (var kind in new[] { GearKind.Weapons, GearKind.Helmets, GearKind.Armors })
                foreach (var it in ArmoryListing.Candidates(kind).Where(i => !i.Enemy && i.Id != "fists"))
                {
                    all++;
                    if (ArmoryListing.Listed(kind, it.Id)) listed++;
                }
            // 11 weapons + 19 helmets + 18 armours (the shop and the trophies) before; 7 + 14 + 10 now (the four boss blades are withheld)
            Assert.AreEqual(48, all);
            Assert.AreEqual(31, listed);
        }
    }
}
