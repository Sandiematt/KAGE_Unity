using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// Renders the Armory to Screenshots/review/SAN-90/unity (1600 x 900) for the side-by-side review with the JS Armory, on the same
    /// mid-game save the JS screens were taken with (1500 coins, 30 gems, Kagetsu level 3, Tsubame, Kuro Zukin level 2, Ronin Armor).
    /// Needs a GPU: it skips itself under -nographics. Run <c>-testFilter GearArmoryScreenshots</c> without -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class GearArmoryScreenshots
    {
        string dir, outDir, shotDir;
        GameFlow flow;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "kage_armory_shots_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-90", "unity"));
            Directory.CreateDirectory(outDir);
            shotDir = outDir;
            UiClock.Skip = true;      // still images: every animation at its end state
        }

        [TearDown]
        public void TearDown()
        {
            UiClock.Reset();
            if (flow != null) Object.DestroyImmediate(flow.gameObject);
            foreach (var cam in Object.FindObjectsByType<Camera>()) Object.DestroyImmediate(cam.gameObject);
            Time.timeScale = 1f;
            GameFeel.paused = false;
            try { Directory.Delete(dir, true); } catch { }
        }

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

        void Start(SaveData save)
        {
            if (flow != null) Object.DestroyImmediate(flow.gameObject);
            new SaveStore(dir).Save(save);
            flow = GameFlow.Create(dir);
        }

        ArmoryController A { get { return flow.Controller.Armory; } }

        IEnumerator Show(ArmoryTab tab, string id)
        {
            A.SelectTab(tab);
            A.Select(id);
            // the fighters are built after a short pause; give them a second to stand and the cloth to settle
            yield return new WaitForSecondsRealtime(1.2f);
            for (int i = 0; i < 4; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_the_armory_like_the_JS_screens()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Start(MidGame());
            yield return null;
            flow.Controller.OpenArmory();
            yield return null;

            yield return Show(ArmoryTab.Weapons, "onikiri");
            yield return Shot("weapons_buy");
            yield return Show(ArmoryTab.Weapons, "kagetsu");
            yield return Shot("weapons_upgrade");
            yield return Show(ArmoryTab.Weapons, "raiko_no_tachi");
            yield return Shot("weapons_locked");
            yield return Show(ArmoryTab.Helmets, "oni_menpo");
            yield return Shot("helmets_buy");
            yield return Show(ArmoryTab.Armor, "ronin_yoroi");
            yield return Shot("armors_line");
            yield return Show(ArmoryTab.Armor, "mugen_yoroi");
            yield return Shot("armors_line_mugen");
            // SAN-108: no Trophies row. A reward armour the save owns stands after the shop armours in the same row.
            A.Save.owned.armors["ikazuchi_do"] = 1;
            A.Save.owned.armors["shiden_yoroi"] = 1;
            yield return Show(ArmoryTab.Armor, "ikazuchi_do");
            yield return Shot("armors_reward_owned");

            // more of the families on the preview
            foreach (var id in new[] { "tsubame", "shizuka", "mizuchi", "yamigama" })
            {
                yield return Show(ArmoryTab.Weapons, id);
                yield return Shot("weapon_" + id);
            }

            // the ways in: the title tile, the loadout's Change gear
            Start(GameRules.DefaultSave(false));
            yield return null;
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Shot("title_with_armory_tile");
            flow.Controller.OpenCampaign();
            flow.Controller.BeginLoadout();
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("loadout_change_gear");
        }

        // SAN-108: pointing at a tile only highlights it; the preview and the details change when it is clicked or confirmed.
        [UnityTest]
        public IEnumerator Capture_hover_does_not_apply_and_click_does()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            shotDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-108", "unity"));
            Directory.CreateDirectory(shotDir);
            Start(MidGame());
            yield return null;
            flow.Controller.OpenArmory();
            yield return null;

            // Weapons: Kagetsu is selected; the pointer moves onto Onikiri
            yield return Show(ArmoryTab.Weapons, "kagetsu");
            Assert.AreEqual("big_w_kagetsu", A.View.PreviewKey);
            yield return Shot("pick_weapons_1_start");
            flow.Active.Nav.FocusOn(Item("tile:onikiri"), true);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual("kagetsu", A.SelectedId, "hovering selected nothing");
            Assert.AreEqual("big_w_kagetsu", A.View.PreviewKey);
            yield return Shot("pick_weapons_2_hover_onikiri");
            flow.Send(MenuCommand.Submit);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual("onikiri", A.SelectedId, "the click selected it");
            Assert.AreEqual("big_w_onikiri", A.View.PreviewKey);
            yield return Shot("pick_weapons_3_click_onikiri");

            // Helmets: the live Kage keeps his helmet while the pointer is on Oni Menpo, and wears it after the click
            yield return Show(ArmoryTab.Helmets, "shiro_maki");
            flow.Active.Nav.FocusOn(Item("tile:oni_menpo"), true);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual("shiro_maki", A.SelectedId);
            yield return Shot("pick_helmets_1_hover_oni_menpo");
            flow.Send(MenuCommand.Submit);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual("oni_menpo", A.SelectedId);
            yield return Shot("pick_helmets_2_click_oni_menpo");

            // Armor: ten cards scroll; moving the focus to the last one scrolls the row (and the Kages in it) without selecting it
            A.Save.owned.armors["ikazuchi_do"] = 1;
            A.Save.owned.armors["shiden_yoroi"] = 1;
            yield return Show(ArmoryTab.Armor, "tachibana_yoroi");
            flow.Active.Nav.FocusOn(Item("tile:shiden_yoroi"), true);
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual("tachibana_yoroi", A.SelectedId, "focusing a card selected nothing");
            yield return Shot("pick_armor_focus_last_card");
            shotDir = outDir;
        }

        NavItem Item(string key)
        {
            foreach (var i in flow.Active.Nav.Items) if (i != null && i.key == key) return i;
            Assert.Fail("no item " + key);
            return null;
        }

        // ---------------- capture (as in FlowScreenshots) ----------------

        IEnumerator Shot(string name)
        {
            Debug.Log("[shots] " + name);
            const int W = 1600, H = 900;
            var main = Camera.main;
            var canvases = new List<(Canvas c, RenderMode mode, Camera cam)>();
            var layers = new List<(Transform t, int layer)>();
            var camGo = new GameObject("CaptureCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4.5f;
            cam.cullingMask = 1 << 5;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.depth = 100;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }
            foreach (var c in Object.FindObjectsByType<Canvas>())
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                canvases.Add((c, c.renderMode, c.worldCamera));
                foreach (var t in c.GetComponentsInChildren<Transform>(true)) { layers.Add((t, t.gameObject.layer)); t.gameObject.layer = 5; }
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
            }
            if (flow != null && flow.ArmoryWorld != null) flow.ArmoryWorld.Reframe(16f / 9f);
            Canvas.ForceUpdateCanvases();
            yield return null;

            Color[] world = null;
            if (main != null) world = Render(main, W, H);
            cam.backgroundColor = Color.black;
            var onBlack = Render(cam, W, H);
            cam.backgroundColor = Color.white;
            var onWhite = Render(cam, W, H);

            var bg = new Color(0.04f, 0.02f, 0.02f);
            var outPx = new Color32[W * H];
            for (int i = 0; i < outPx.Length; i++)
            {
                Color f = onBlack[i], w = onWhite[i];
                float a = 1f - ((w.r - f.r) + (w.g - f.g) + (w.b - f.b)) / 3f;
                a = Mathf.Clamp01(a);
                Color under = world != null ? world[i] : bg;
                outPx[i] = new Color(f.r + (1f - a) * under.r, f.g + (1f - a) * under.g, f.b + (1f - a) * under.b, 1f);
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(outPx);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(shotDir, name + ".png"), tex.EncodeToPNG());

            foreach (var e in canvases) { e.c.renderMode = e.mode; e.c.worldCamera = e.cam; }
            foreach (var l in layers) if (l.t != null) l.t.gameObject.layer = l.layer;
            Object.DestroyImmediate(camGo);
            Object.Destroy(tex);
        }

        static Color[] Render(Camera cam, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;
            var px = tex.GetPixels();
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
            return px;
        }
    }
}
