using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// Picking in the Armory (SAN-108: "the thing should be applied when only clicked on weapons, helmet and armor, not when hovering").
    /// Seam: the Armory's tiles as <see cref="NavItem"/>s in a <see cref="NavGroup"/>, wired the way the screen wires them
    /// (<see cref="ArmoryTilePick"/>), over a real <see cref="ArmoryController"/>. Hover, arrows and the stick all reach a tile through
    /// <c>NavGroup.FocusOn(item, byPlayer: true)</c> or <c>Handle(direction)</c>; a mouse click is that focus plus
    /// <c>Handle(Submit)</c>, and Enter, Space, J and the gamepad's A are <c>Handle(Submit)</c>. The selection is what the detail panel,
    /// the live Kage and the weapon illustration show.
    /// </summary>
    public class TilePickTests
    {
        GameObject stage;
        NavGroup nav;
        ArmoryController armory;
        SaveData save;
        readonly List<GameObject> made = new List<GameObject>();
        int changes;

        [SetUp]
        public void SetUp()
        {
            stage = new GameObject("TileStage", typeof(RectTransform));
            made.Add(stage);
            nav = new NavGroup((RectTransform)stage.transform);
            save = GameRules.DefaultSave(false);
            save.coins = 5000;
            save.owned.weapons["tsubame"] = 1;
            save.owned.weapons["kagetsu"] = 1;
            save.equipped.weapon = "kagetsu";        // the Armory opens on the worn weapon (a new save wears Mumei)
            changes = 0;
            armory = new ArmoryController(save, null, () => changes++);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in made) if (go != null) Object.DestroyImmediate(go);
            made.Clear();
        }

        NavItem Node(string key, float x, float y)
        {
            var go = new GameObject(key, typeof(RectTransform));
            made.Add(go);
            var rt = (RectTransform)go.transform;
            rt.SetParent(stage.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(127f, 110f);
            var item = go.AddComponent<NavItem>();
            item.key = key;
            nav.Add(item);
            return item;
        }

        // the Weapons row as the screen builds it: a tile each for Kagetsu, Tsubame and Onikiri, and the Buy button after them
        NavItem[] WeaponRow()
        {
            string[] ids = { "kagetsu", "tsubame", "onikiri" };
            var row = new NavItem[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                row[i] = Node("tile:" + ids[i], i * 137f, 0f);
                ArmoryTilePick.Bind(row[i], ids[i], armory.Select);
            }
            var buy = Node("primary", 500f, 0f);
            buy.onSubmit = () => armory.Buy();
            nav.SetFocus("tile:kagetsu");
            return row;
        }

        [Test]
        public void Hovering_a_tile_only_highlights_it_the_selection_stays_put()
        {
            var row = WeaponRow();
            nav.FocusOn(row[2], true);                       // the pointer moves onto Onikiri
            Assert.AreSame(row[2], nav.Focus, "it is highlighted");
            Assert.IsTrue(row[2].Focused);
            Assert.AreEqual("kagetsu", armory.SelectedId, "but nothing was selected");
            Assert.AreEqual("kagetsu", armory.View.Name.ToLowerInvariant(), "the detail panel still shows Kagetsu");
            Assert.AreEqual("big_w_kagetsu", armory.View.PreviewKey, "and so does the illustration");
            Assert.AreEqual(0, changes, "nothing redrew");
        }

        [Test]
        public void Moving_the_focus_with_the_arrows_or_the_stick_only_highlights_each_tile()
        {
            WeaponRow();
            Assert.IsTrue(nav.Handle(MenuCommand.Right));
            Assert.AreEqual("tile:tsubame", nav.Focus.key);
            Assert.IsTrue(nav.Handle(MenuCommand.Right));
            Assert.AreEqual("tile:onikiri", nav.Focus.key);
            Assert.AreEqual("kagetsu", armory.SelectedId);
            Assert.AreEqual(0, changes);
            Assert.IsTrue(nav.Handle(MenuCommand.Left));
            Assert.AreEqual("tile:tsubame", nav.Focus.key);
            Assert.AreEqual("kagetsu", armory.SelectedId);
        }

        [Test]
        public void Restoring_the_focus_after_a_redraw_selects_nothing()
        {
            WeaponRow();
            nav.SetFocus("tile:tsubame");
            Assert.AreEqual("kagetsu", armory.SelectedId);
        }

        [Test]
        public void Confirm_on_the_focused_tile_selects_it_and_the_view_follows()
        {
            var row = WeaponRow();
            nav.FocusOn(row[1], true);
            Assert.AreEqual("kagetsu", armory.SelectedId, "focus alone does nothing");
            Assert.IsTrue(nav.Handle(MenuCommand.Submit), "Enter, Space, J and the gamepad's A");
            Assert.AreEqual("tsubame", armory.SelectedId);
            Assert.AreEqual("big_w_tsubame", armory.View.PreviewKey);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void A_click_is_the_pointer_on_the_tile_then_confirm_and_selects_it()
        {
            var row = WeaponRow();
            nav.FocusOn(row[2], true);                       // the pointer arrives: still nothing selected
            Assert.AreEqual("kagetsu", armory.SelectedId);
            nav.FocusOn(row[2], true);                       // the press lands on it, then GameFlow sends Submit
            nav.Handle(MenuCommand.Submit);
            Assert.AreEqual("onikiri", armory.SelectedId);
        }

        [Test]
        public void Buy_acts_on_the_selected_tile_not_the_one_the_pointer_is_over()
        {
            var row = WeaponRow();
            nav.FocusOn(row[2], true);                       // hovering Onikiri
            Assert.IsFalse(armory.Buy(), "the selection is Kagetsu, which is already owned");
            Assert.IsFalse(save.owned.weapons.ContainsKey("onikiri"));
            Assert.AreEqual(5000, save.coins);

            nav.Handle(MenuCommand.Submit);                  // click it: now it is selected
            Assert.IsTrue(armory.Buy());
            Assert.AreEqual(1, save.owned.weapons["onikiri"]);
            Assert.AreEqual(3600, save.coins);
        }

        [Test]
        public void Equip_acts_on_the_selection_and_the_action_buttons_stay_reachable_from_the_row()
        {
            var row = WeaponRow();
            nav.FocusOn(row[1], true);
            nav.Handle(MenuCommand.Submit);                  // select Tsubame
            Assert.AreEqual("tsubame", armory.SelectedId);
            nav.FocusOn(row[0], true);                       // hover Kagetsu again
            Assert.IsTrue(armory.Equip(), "Equip wears the selected Tsubame, not the hovered Kagetsu");
            Assert.AreEqual("tsubame", save.equipped.weapon);

            nav.SetFocus("tile:onikiri");
            Assert.IsTrue(nav.Handle(MenuCommand.Right), "the stick or arrow goes on to the buttons");
            Assert.AreEqual("primary", nav.Focus.key);
        }

        [Test]
        public void A_pointer_over_a_tile_of_another_tab_row_still_selects_nothing()
        {
            // helmets: the same wiring on another tab
            armory.SelectTab(ArmoryTab.Hairstyles);
            var a = Node("tile:spike", 0f, 0f);
            var b = Node("tile:messy_layers", 137f, 0f);
            ArmoryTilePick.Bind(a, "spike", armory.Select);
            ArmoryTilePick.Bind(b, "messy_layers", armory.Select);
            nav.SetFocus("tile:spike");
            nav.FocusOn(b, true);
            Assert.AreEqual("spike", armory.SelectedId);
            nav.Handle(MenuCommand.Submit);
            Assert.AreEqual("messy_layers", armory.SelectedId);
        }
    }
}
