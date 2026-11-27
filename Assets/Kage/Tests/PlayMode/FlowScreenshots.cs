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
    /// Renders every campaign-flow screen to Screenshots/review/SAN-67/unity (1600 x 900) for the owner's side-by-side review with the
    /// JS screens. Needs a GPU: it skips itself under -nographics. Run:
    /// <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter FlowScreenshots</c> (no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class FlowScreenshots
    {
        string dir, outDir;
        GameFlow flow;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "kage_shots_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-67", "unity"));
            Directory.CreateDirectory(outDir);
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

        void Start(bool dev, System.Action<SaveData> tweak = null)
        {
            if (flow != null) Object.DestroyImmediate(flow.gameObject);
            var save = dev ? GameRules.SetDevMode(GameRules.DefaultSave(false), true) : GameRules.DefaultSave(false);
            if (tweak != null) tweak(save);
            new SaveStore(dir).Save(save);
            flow = GameFlow.Create(dir);
        }

        IEnumerator Settle(int frames = 6)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_every_screen()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");

            // title (a fresh install is in dev mode)
            Start(true);
            yield return Settle();
            yield return Shot("title");

            // campaign: the Prologue on a fresh non-dev save (Act I locked), then Act I in dev mode
            Start(false);
            yield return Settle();
            flow.Controller.OpenCampaign();
            yield return Settle();
            yield return Shot("campaign_prologue_fresh_save");
            flow.Controller.SelectStage("act1.stretch1");
            yield return Settle();
            yield return Shot("campaign_act1_locked");

            Start(false, s => { GameRules.MarkStageCleared(s, "prologue.1"); GameRules.MarkStageCleared(s, "act1.stretch1"); });
            yield return Settle();
            flow.Controller.OpenCampaign();
            yield return Settle();
            yield return Shot("campaign_act1_progress");

            Start(true);
            yield return Settle();
            flow.Controller.OpenCampaign();
            flow.Controller.SelectStage("act1.boss");
            yield return Settle();
            yield return Shot("campaign_act1_boss_dev");

            // loadout
            flow.Controller.BeginLoadout();
            yield return Settle(20);
            yield return Shot("loadout_raikon");
            flow.Controller.Back();
            flow.Controller.SelectStage("prologue.1");
            flow.Controller.BeginLoadout();
            yield return Settle(20);
            yield return Shot("loadout_prologue");

            // stage: title card, fight, pause
            flow.Controller.StartStage();
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Shot("stage_title_card");
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Shot("stage_fight");
            flow.Send(MenuCommand.Pause);
            yield return Settle(4);
            yield return Shot("pause");
            flow.Send(MenuCommand.Pause);

            // results
            flow.World.ForceKnockOut(true);
            yield return new WaitForSecondsRealtime(StageWorld.OverSeconds + 0.5f);
            yield return Settle(4);
            yield return Shot("results_win");

            // settings and the dev-mode question
            flow.Controller.ContinueFromResults();
            flow.Controller.Back();
            flow.Controller.OpenSettings();
            yield return Settle();
            yield return Shot("settings");
            flow.Controller.SetDevMode(false);
            yield return Settle();
            yield return Shot("settings_dev_off_confirm");
            flow.Controller.CancelDevOff();
            flow.OpenControls();
            yield return Settle();
            yield return Shot("controls_guide");
        }

        /// <summary>SAN-73: the results screen, a win with gear and a loss, to Screenshots/review/SAN-73/unity.</summary>
        [UnityTest]
        public IEnumerator Capture_results_screens()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            shotDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-73", "unity"));
            Directory.CreateDirectory(shotDir);

            // a win on the first sub-boss: S rating, flawless, a trophy
            Start(false, s => { GameRules.MarkStageCleared(s, "prologue.1"); GameRules.MarkStageCleared(s, "act1.stretch1"); GameRules.MarkStageCleared(s, "act1.stretch2"); s.xp = 150; });
            yield return Settle();
            yield return ToResults("act1.sub1", win: true);
            yield return new WaitForSecondsRealtime(2.4f);
            yield return Shot("results_win");

            // a loss on Raikon's stage
            Start(true);
            yield return Settle();
            yield return ToResults("act1.boss", win: false);
            yield return new WaitForSecondsRealtime(2.4f);
            yield return Shot("results_loss");
            shotDir = null;
        }

        IEnumerator ToResults(string stage, bool win)
        {
            flow.Controller.OpenCampaign();
            flow.Controller.SelectStage(stage);
            flow.Controller.BeginLoadout();
            yield return Settle(10);
            flow.Controller.StartStage();
            float end = Time.realtimeSinceStartup + 8f;
            while (flow.World.phase != StageWorld.Phase.Fight && Time.realtimeSinceStartup < end) yield return null;
            if (win) flow.World.ForceKnockOut(true); else flow.World.ForceKnockOut(false);
            end = Time.realtimeSinceStartup + 10f;
            while (flow.Controller.Screen != FlowScreen.Results && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(FlowScreen.Results, flow.Controller.Screen);
        }

        // ---------------- capture ----------------

        string shotDir;

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
                // a canvas is drawn by the camera whose mask holds its layer: the HUD's is on the default layer, so borrow the UI layer
                foreach (var t in c.GetComponentsInChildren<Transform>(true)) { layers.Add((t, t.gameObject.layer)); t.gameObject.layer = 5; }
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();
            yield return null;

            // the world (with its post-processing), then the UI twice, on black and on white: the difference gives the UI's alpha
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
                Color o = new Color(f.r + (1f - a) * under.r, f.g + (1f - a) * under.g, f.b + (1f - a) * under.b, 1f);
                outPx[i] = o;
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(outPx);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(shotDir ?? outDir, name + ".png"), tex.EncodeToPNG());

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
