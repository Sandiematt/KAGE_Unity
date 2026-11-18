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
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.DestroyImmediate(cam.gameObject);
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
        public IEnumerator The_title_menu_opens_the_Armory_with_Kage_standing_in_it_and_Back_returns()
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
            Assert.AreEqual(1, flow.ArmoryWorld.Fighters.Count);
            Assert.AreEqual("kagetsu", Preview.Character.weaponId);
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
        public IEnumerator Moving_across_the_tiles_selects_each_item_and_Kage_takes_up_the_weapon()
        {
            StartFlow(false, s => { s.owned.weapons["tsubame"] = 1; });
            yield return null;
            flow.Controller.OpenArmory();
            yield return null;
            Assert.AreEqual("tile:kagetsu", Focus);
            flow.Send(MenuCommand.Right);
            Assert.AreEqual("tile:tsubame", Focus);
            Assert.AreEqual("tsubame", Armory.SelectedId, "browsing selects, like pointing at it");
            yield return Until(() => Preview != null && Preview.Character.weaponId == "tsubame", 3f, "the preview to take up Tsubame");
            Assert.AreEqual(FightBuilderFamily("tsubame"), Preview.Character.family);
            Assert.AreEqual("kagetsu", flow.Controller.Save.equipped.weapon, "only equipping changes the save");
        }

        static string FightBuilderFamily(string weapon) { return WeaponFamilies.FamilyIdOf(weapon); }

        [UnityTest]
        public IEnumerator Every_family_of_weapon_can_be_previewed_on_Kage()
        {
            StartFlow(true);
            yield return null;
            flow.Controller.OpenArmory();
            yield return null;
            foreach (var id in new[] { "kagetsu", "tsubame", "shizuka", "onikiri", "mizuchi", "yamigama", "iwakudaki" })
            {
                Armory.Select(id);
                string want = id;
                yield return Until(() => Preview != null && Preview.Character.weaponId == want, 3f, "the preview of " + id);
                Assert.AreEqual(WeaponFamilies.FamilyIdOf(id), Preview.Character.family, id);
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
            Assert.AreEqual(8, ids.Length);
            yield return Until(() => flow.ArmoryWorld.Fighters.Count == 8, 4f, "the eight armour cards");
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(ids[i], flow.ArmoryWorld.Fighters[i].Character.armor, "card " + i);
                Assert.AreEqual("shiro_maki", flow.ArmoryWorld.Fighters[i].Character.helmet);
            }
            // picking another card keeps the same fighters
            int builds = flow.ArmoryWorld.Builds;
            Armory.Select("ronin_yoroi");
            yield return null;
            Assert.AreEqual(builds, flow.ArmoryWorld.Builds);
            // the trophies are a second row
            Armory.SetLine(ArmorLine.Trophy);
            yield return Until(() => flow.ArmoryWorld.Fighters.Count == 10, 4f, "the ten trophies");
            Assert.AreEqual("ikazuchi_do", flow.ArmoryWorld.Fighters[0].Character.armor);
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
            Assert.AreEqual("tsubame", Armory.SelectedId, "the d-pad moves across the tiles");

            Press(pad.buttonEast);
            yield return null;
            Release(pad.buttonEast);
            yield return null;
            Assert.AreEqual(FlowScreen.Title, flow.Controller.Screen, "East goes back");
        }
    }
}
