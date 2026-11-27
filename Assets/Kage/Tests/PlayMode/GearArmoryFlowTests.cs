using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The Armory in play (SAN-90): reached from the title and the loadout and the results, driven by the same menu commands the
    /// keyboard and gamepad produce, with a live Kage wearing what is selected, and every purchase saved to the file. (The name sorts
    /// after the character screenshots on purpose: it runs fights.)
    /// </summary>
    public class GearArmoryFlowTests : InputTestFixture
    {
        string dir;
        GameFlow flow;

        public override void Setup()
        {
            base.Setup();
            dir = Path.Combine(Path.GetTempPath(), "kage_armory_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        public override void TearDown()
        {
            if (flow != null) Object.DestroyImmediate(flow.gameObject);
            foreach (var cam in Object.FindObjectsByType<Camera>()) Object.DestroyImmediate(cam.gameObject);
            Time.timeScale = 1f;
            GameFeel.paused = false;
            try { Directory.Delete(dir, true); } catch { }
            base.TearDown();
        }

        GameFlow StartFlow(bool dev, System.Action<SaveData> tweak = null)
        {
            var save = dev ? GameRules.SetDevMode(GameRules.DefaultSave(false), true) : GameRules.DefaultSave(false);
            if (tweak != null) tweak(save);
            new SaveStore(dir).Save(save);
            flow = GameFlow.Create(dir);
            return flow;
        }

        string Focus { get { return flow.Active.Nav.Focus != null ? flow.Active.Nav.Focus.key : null; } }

        ArmoryController Armory { get { return flow.Controller.Armory; } }

        IEnumerator Until(System.Func<bool> cond, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("timed out waiting for " + what);
                yield return null;
            }
        }

        Fighter Preview { get { return flow.ArmoryWorld.Fighters.Count > 0 ? flow.ArmoryWorld.Fighters[0] : null; } }

        // ---- the way in and out ----

        [UnityTest]
        public IEnumerator The_title_menu_opens_the_Armory_with_the_blade_on_show_and_Back_returns()
        {
            StartFlow(false);
            yield return null;
            flow.Active.Nav.SetFocus("campaign");
            flow.Send(MenuCommand.Down);
            Assert.AreEqual("armory", Focus, "the Armory row is a real menu row now");
            flow.Send(MenuCommand.Submit);
            Assert.AreEqual(FlowScreen.Armory, flow.Controller.Screen);
            Assert.IsTrue(flow.Panel(FlowScreen.Armory).Visible);
            Assert.IsFalse(flow.Panel(FlowScreen.Title).Visible);
            Assert.IsNotNull(flow.ArmoryWorld);
            // SAN-67 rework: the Weapons tab shows the JS illustration of the blade, not Kage holding it
            Assert.AreEqual(0, flow.ArmoryWorld.Fighters.Count, "no fighter on the Weapons tab");
            Assert.AreEqual(ArmoryPreview.Illustration, Armory.View.Preview);
            Assert.AreEqual("big_w_kagetsu", Armory.View.PreviewKey);
            Assert.AreEqual("tile:kagetsu", Focus, "the worn weapon has the focus");

            flow.Send(MenuCommand.Back);
            Assert.AreEqual(FlowScreen.Title, flow.Controller.Screen);
            Assert.IsNull(flow.ArmoryWorld, "the Armory's world is put away");
        }

        [UnityTest]
        public IEnumerator The_title_s_Armory_tile_opens_it_too()
        {
            StartFlow(false);
            yield return null;
            flow.Active.Nav.SetFocus("armory-tile");
            flow.Send(MenuCommand.Submit);
            Assert.AreEqual(FlowScreen.Armory, flow.Controller.Screen);
        }

        // ---- browsing ----

        [UnityTest]
        public IEnumerator Moving_across_the_tiles_only_highlights_and_confirm_selects_the_item_and_shows_its_blade()
        {
            StartFlow(false, s => { s.owned.weapons["tsubame"] = 1; });
            yield return null;
            flow.Controller.OpenArmory();
            yield return null;
            Assert.AreEqual("tile:kagetsu", Focus);
            flow.Send(MenuCommand.Right);
            Assert.AreEqual("tile:tsubame", Focus);
            // SAN-108: focus (arrows, stick, hover) only highlights; the selection changes on click or confirm
            Assert.AreEqual("kagetsu", Armory.SelectedId, "moving the focus selects nothing");
            Assert.AreEqual("big_w_kagetsu", Armory.View.PreviewKey, "the preview still shows the selected blade");
            flow.Send(MenuCommand.Submit);
            Assert.AreEqual("tsubame", Armory.SelectedId, "confirm selects the focused tile");
            Assert.AreEqual("big_w_tsubame", Armory.View.PreviewKey, "the preview shows the selected blade");
            Assert.IsNotNull(Ui.Tex(Armory.View.PreviewKey));
            Assert.AreEqual("kagetsu", flow.Controller.Save.equipped.weapon, "only equipping changes the save");
        }

        [UnityTest]
        public IEnumerator Every_weapon_has_its_illustration_on_the_preview()
        {
            StartFlow(true);
            yield return null;
            flow.Controller.OpenArmory();
            yield return null;
            foreach (var id in new[] { "kagetsu", "tsubame", "shizuka", "onikiri", "mizuchi", "yamigama" })
            {
                Armory.Select(id);
                Assert.AreEqual("big_w_" + id, Armory.View.PreviewKey, id);
                Assert.IsNotNull(Ui.Tex(Armory.View.PreviewKey), id);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Q_and_E_turn_the_tabs_and_the_preview_wears_the_selected_helmet()
        {
            StartFlow(true);
            yield return null;
            flow.Controller.OpenArmory();
            yield return null;
            flow.Send(MenuCommand.Next);
            Assert.AreEqual(ArmoryTab.Helmets, Armory.Tab);
            Assert.AreEqual("tile:shiro_maki", Focus, "the focus goes to the new tab's worn item");
            Armory.Select("oni_menpo");
            yield return Until(() => Preview != null && Preview.Character.helmet == "oni_menpo", 3f, "the preview to wear Oni Menpo");
            Assert.AreEqual("tachibana_yoroi", Preview.Character.armor);
            Assert.AreEqual("shiro_maki", flow.Controller.Save.equipped.helmet, "not equipped yet");
            flow.Send(MenuCommand.Prev);
            Assert.AreEqual(ArmoryTab.Weapons, Armory.Tab);
        }

        [UnityTest]
        public IEnumerator The_Armor_tab_stands_one_Kage_in_each_armour_of_the_row()
        {
            StartFlow(true);
            yield return null;
            flow.Controller.OpenArmory();
            yield return null;
            flow.Send(MenuCommand.Next);
            flow.Send(MenuCommand.Next);
            Assert.AreEqual(ArmoryTab.Armor, Armory.Tab);
            var ids = Armory.View.Tiles.Select(t => t.Id).ToArray();
            // dev mode owns the eight shop armours and the two Act I rewards (SAN-108: one list, no Trophies row)
            Assert.AreEqual(10, ids.Length);
            yield return Until(() => flow.ArmoryWorld.Fighters.Count == 10, 4f, "the ten armour cards");
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(ids[i], flow.ArmoryWorld.Fighters[i].Character.armor, "card " + i);
                Assert.AreEqual("shiro_maki", flow.ArmoryWorld.Fighters[i].Character.helmet);
                Assert.AreEqual("fists", flow.ArmoryWorld.Fighters[i].Character.weaponId, "armor card " + i + " must not equip a weapon");
                Assert.IsFalse(flow.ArmoryWorld.Fighters[i].rig.SwordVisible, "armor card " + i + " must hide any weapon mesh");
            }
            // picking another card keeps the same fighters
            int builds = flow.ArmoryWorld.Builds;
            Armory.Select("ronin_yoroi");
            yield return null;
            Assert.AreEqual(builds, flow.ArmoryWorld.Builds);
            // five-tier rarity progression: Trophy rewards precede the two Legendary armours, which carry ArmorAura
            Assert.AreEqual("ikazuchi_do", flow.ArmoryWorld.Fighters[6].Character.armor);
            Assert.AreEqual("shiden_yoroi", flow.ArmoryWorld.Fighters[7].Character.armor);
            Assert.AreEqual("yurei_yoroi", flow.ArmoryWorld.Fighters[8].Character.armor);
            Assert.AreEqual("mugen_yoroi", flow.ArmoryWorld.Fighters[9].Character.armor);
            Assert.IsNotNull(flow.ArmoryWorld.Fighters[8].GetComponent<ArmorAura>());
            Assert.IsNotNull(flow.ArmoryWorld.Fighters[9].GetComponent<ArmorAura>());
        }

        // ---- buying, upgrading, equipping ----

        [UnityTest]
        public IEnumerator Buy_spends_the_coins_and_the_save_file_has_the_new_weapon()
        {
            StartFlow(false, s => s.coins = 1500);
            yield return null;
            flow.Controller.OpenArmory();
            yield return null;
            Armory.Select("onikiri");
            flow.Active.Nav.SetFocus("primary");
            Assert.AreEqual("primary", Focus);
            flow.Send(MenuCommand.Submit);
            Assert.AreEqual(100, flow.Controller.Save.coins);
            Assert.AreEqual(1, flow.Controller.Save.owned.weapons["onikiri"]);
            var onDisk = new SaveStore(dir).Load();
            Assert.AreEqual(100, onDisk.coins);
            Assert.IsTrue(onDisk.owned.weapons.ContainsKey("onikiri"));
            Assert.AreEqual("Upgrade", Armory.View.Primary.Label);
        }

        [UnityTest]
        public IEnumerator A_button_that_cannot_be_afforded_cannot_be_pressed()
        {
            StartFlow(false);   // 250 coins
            yield return null;
            flow.Controller.OpenArmory();
            yield return null;
            Armory.Select("onikiri");
            var primary = flow.Active.Nav.Items.First(i => i.key == "primary");
            Assert.IsFalse(primary.interactable);
            flow.Active.Nav.FocusOn(primary);
            flow.Send(MenuCommand.Submit);
            Assert.AreEqual(250, flow.Controller.Save.coins);
        }

        [UnityTest]
        public IEnumerator Equipping_dresses_Kage_for_the_fights_and_the_Upgrade_shows_the_next_level_first()
        {
            StartFlow(false, s => { s.coins = 5000; s.owned.helmets["kuro_zukin"] = 2; });
            yield return null;
            flow.Controller.OpenArmory();
            yield return null;
            flow.Send(MenuCommand.Next);
            Armory.Select("kuro_zukin");
            flow.Active.Nav.SetFocus("equip");
            flow.Send(MenuCommand.Submit);
            Assert.AreEqual("kuro_zukin", flow.Controller.Save.equipped.helmet);
            Assert.AreEqual("kuro_zukin", new SaveStore(dir).Load().equipped.helmet);
            Assert.AreEqual("Equipped", Armory.View.Equip.Label);

            var v = Armory.View;
            Assert.IsTrue(v.Stats.Any(s => !string.IsNullOrEmpty(s.Preview)), "an upgradable helmet previews what the next level adds");
        }

        // ---- the other ways in ----

        [UnityTest]
        public IEnumerator Change_gear_on_the_loadout_opens_the_Armory_and_the_fight_is_built_from_the_new_gear()
        {
            StartFlow(false, s => { s.coins = 2000; });
            yield return null;
            flow.Controller.OpenCampaign();
            flow.Controller.BeginLoadout();
            yield return null;
            Assert.AreEqual("kagetsu", flow.World.Player.Character.weaponId);
            flow.Active.Nav.SetFocus("gear");
            flow.Send(MenuCommand.Submit);
            Assert.AreEqual(FlowScreen.Armory, flow.Controller.Screen);
            Assert.IsNull(flow.World, "the tableau is put away while the Armory is open");
            Armory.Select("tsubame");
            Assert.IsTrue(Armory.Buy());
            Assert.IsTrue(Armory.Equip());
            flow.Send(MenuCommand.Back);
            Assert.AreEqual(FlowScreen.Loadout, flow.Controller.Screen);
            Assert.IsNotNull(flow.World);
            Assert.AreEqual("tsubame", flow.World.Player.Character.weaponId, "the tableau is built again from the new loadout");
            flow.Controller.StartStage();
            yield return Until(() => flow.World.phase == StageWorld.Phase.Fight, 6f, "the fight to begin");
            Assert.AreEqual("tsubame", flow.World.Player.Character.weaponId);
        }

        [UnityTest]
        public IEnumerator Upgrade_on_the_defeat_results_opens_the_Armory_and_Back_returns_to_the_results()
        {
            StartFlow(false);
            yield return null;
            flow.Controller.OpenCampaign();
            flow.Controller.BeginLoadout();
            flow.Controller.StartStage();
            yield return Until(() => flow.World.phase == StageWorld.Phase.Fight, 6f, "the fight to begin");
            flow.World.ForceKnockOut(foeLoses: false);
            yield return Until(() => flow.Controller.Screen == FlowScreen.Results, 8f, "the results");
            flow.Send(MenuCommand.Right);
            Assert.AreEqual("armory", Focus);
            flow.Send(MenuCommand.Submit);
            Assert.AreEqual(FlowScreen.Armory, flow.Controller.Screen);
            flow.Send(MenuCommand.Back);
            Assert.AreEqual(FlowScreen.Results, flow.Controller.Screen);
            Assert.IsTrue(flow.Panel(FlowScreen.Results).Visible);
            Assert.IsNotNull(flow.World, "the results have fighters behind them again");
        }

        // ---- the gamepad ----

        [UnityTest]
        public IEnumerator The_gamepad_turns_tabs_moves_and_goes_back()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            StartFlow(false, s => s.owned.weapons["tsubame"] = 1);
            yield return null;
            flow.Controller.OpenArmory();
            yield return null;

            Press(pad.rightShoulder);
            yield return null;
            Release(pad.rightShoulder);
            yield return null;
            Assert.AreEqual(ArmoryTab.Helmets, Armory.Tab, "the right shoulder button turns to the next tab");

            Press(pad.leftShoulder);
            yield return null;
            Release(pad.leftShoulder);
            yield return null;
            Assert.AreEqual(ArmoryTab.Weapons, Armory.Tab);

            Press(pad.dpad.right);
            yield return null;
            Release(pad.dpad.right);
            yield return null;
            Assert.AreEqual("tile:tsubame", Focus, "the d-pad moves the focus across the tiles");
            Assert.AreEqual("kagetsu", Armory.SelectedId, "without selecting");
            Press(pad.buttonSouth);
            yield return null;
            Release(pad.buttonSouth);
            yield return null;
            Assert.AreEqual("tsubame", Armory.SelectedId, "A selects the focused tile");

            Press(pad.buttonEast);
            yield return null;
            Release(pad.buttonEast);
            yield return null;
            Assert.AreEqual(FlowScreen.Title, flow.Controller.Screen, "East goes back");
        }
    }
}
