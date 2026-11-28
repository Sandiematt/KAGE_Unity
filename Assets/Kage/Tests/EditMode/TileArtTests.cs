using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // The Armory shows the item itself (SAN-67 rework): a drawn sword on every weapon tile, a drawn helmet on every helmet tile, the armour on
    // every armour card, and on the Weapons tab the JS illustration of the blade instead of Kage holding it. The pictures are the JS drawings.
    public class TileArtTests
    {
        static SaveData Save()
        {
            return GameRules.SetDevMode(GameRules.DefaultSave(false), true);
        }

        [Test]
        public void Every_weapon_tile_shows_a_drawn_sword_that_exists()
        {
            var v = ArmoryView.Build(Save(), ArmoryTab.Weapons);
            Assert.AreEqual(8, v.Tiles.Length, "Mumei and the seven shop weapons");
            foreach (var t in v.Tiles)
            {
                Assert.IsNotNull(t.Art, t.Id);
                Assert.IsTrue(t.Art.Source == ArtSource.WeaponRender || t.Art.Source == ArtSource.WeaponIcon, t.Id);
                Assert.IsNotNull(t.Art.Texture, "the picture of " + t.Id + " (" + t.Art.Key + ") is in Resources/UI/Img");
            }
        }

        [Test]
        public void The_six_listed_shop_swords_use_the_design_renders()
        {
            var v = ArmoryView.Build(Save(), ArmoryTab.Weapons);
            foreach (var id in new[] { "mumei", "kagetsu", "tsubame", "shizuka", "onikiri", "mizuchi", "yamigama", "iwakudaki" })
            {
                var t = v.Tiles.Single(x => x.Id == id);
                Assert.AreEqual(ArtSource.WeaponRender, t.Art.Source, id);
                Assert.AreEqual("ui_w_" + id, t.Art.Key);
            }
        }

        [Test]
        public void EveryHairstyleTileHasItsInkEmblem()
        {
            var v = ArmoryView.Build(Save(), ArmoryTab.Hairstyles);
            Assert.AreEqual(6, v.Tiles.Length);
            foreach (var t in v.Tiles)
            {
                Assert.AreEqual(ArtSource.HairstylePortrait, t.Art.Source, t.Id);
                Assert.AreEqual("hair_" + t.Id, t.Art.Key);
                Assert.IsNotNull(t.Art.Texture, "the hairstyle portrait of " + t.Id);
            }
        }

        [Test]
        public void Every_armour_card_shows_the_armour()
        {
            var v = ArmoryView.Build(Save(), ArmoryTab.Armor);
            Assert.AreEqual(10, v.Tiles.Length, "the shop armours and the two Act I rewards dev mode owns");
            foreach (var t in v.Tiles)
            {
                Assert.AreEqual(ArtSource.ArmorIcon, t.Art.Source, t.Id);
                Assert.AreEqual("icon_a_" + t.Id, t.Art.Key);
                Assert.IsNotNull(t.Art.Texture, "the armour drawing of " + t.Id);
            }
        }

        [Test]
        public void The_Weapons_tab_previews_the_JS_illustration_of_the_selected_blade_and_the_other_tabs_a_live_Kage()
        {
            var save = Save();
            var w = ArmoryView.Build(save, ArmoryTab.Weapons, "onikiri");
            Assert.AreEqual(ArmoryPreview.Illustration, w.Preview);
            Assert.AreEqual("big_w_onikiri", w.PreviewKey);
            Assert.IsNotNull(Ui.Tex(w.PreviewKey), "the illustration exists");
            Assert.IsNotNull(Ui.Tex(GearArt.GhostKey), "and so does the ghost behind it");
            Assert.AreEqual(ArmoryPreview.LiveKage, ArmoryView.Build(save, ArmoryTab.Hairstyles, "low_ponytail").Preview);
            Assert.IsNull(ArmoryView.Build(save, ArmoryTab.Hairstyles, "low_ponytail").PreviewKey);
            Assert.AreEqual(ArmoryPreview.LiveKage, ArmoryView.Build(save, ArmoryTab.Armor).Preview);
        }

        [Test]
        public void Every_weapon_has_its_illustration()
        {
            foreach (var t in ArmoryView.Build(Save(), ArmoryTab.Weapons).Tiles)
                Assert.IsNotNull(Ui.Tex(GearArt.PreviewKey(t.Id)), "the big drawing of " + t.Id);
        }

        [Test]
        public void A_trophy_won_on_the_results_screen_is_shown_as_the_item_itself()
        {
            var save = GameRules.DefaultSave(false);
            foreach (var st in GameRules.Stages)
            {
                if (st.Id == "act1.sub2") break;
                GameRules.MarkStageCleared(save, st.Id);
            }
            var r = new FightResult { win = true, hpLeft = 0.8, time = 50, maxCombo = 5 };
            var v = ResultsView.Build(save, GameRules.CompleteStage(save, "act1.sub2", r), r);
            Assert.IsTrue(v.Items.Count > 0, "the first sub-boss drops a trophy");
            Assert.AreEqual(v.Items.Count, v.ItemArt.Count, "one picture per item");
            Assert.AreEqual(ArtSource.ArmorIcon, v.ItemArt[0].Source, "Ikazuchi-do is armor");
            Assert.AreEqual("icon_a_ikazuchi_do", v.ItemArt[0].Key);
            Assert.IsNotNull(v.ItemArt[0].Texture);
        }

        [Test]
        public void An_item_the_player_does_not_own_still_shows_its_picture()
        {
            var v = ArmoryView.Build(GameRules.DefaultSave(false), ArmoryTab.Weapons);
            var locked = v.Tiles.First(t => !t.Owned);
            Assert.IsNotNull(locked.Art.Texture, "a tile that is not owned is the same drawing, shown dim by the screen");
        }
    }
}
