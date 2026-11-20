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
    /// SAN-67 rework review images: the new screens (splash, results, records, trophies, profile, toast, the pause menu's controls) to
    /// Screenshots/review/SAN-67/unity, and frame strips of the animations to Screenshots/review/SAN-67/animations/unity/&lt;name&gt;. The strips run on a
    /// frozen UI clock stepped by hand, so every frame is at an exact time. Needs a GPU: run without -nographics, with
    /// <c>-testFilter UiMotionScreenshots</c>.
    /// </summary>
    [Category("Screenshots")]
    public class UiMotionScreenshots
    {
        string dir, outDir, stripDir;
        GameFlow flow;
        string shotDir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "kage_motion_shots_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-67"));
            outDir = Path.Combine(root, "unity");
            stripDir = Path.Combine(root, "animations", "unity");
            Directory.CreateDirectory(outDir);
            Directory.CreateDirectory(stripDir);
            shotDir = outDir;
            UiClock.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            UiClock.Reset();
            if (flow != null) Object.DestroyImmediate(flow.gameObject);
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.DestroyImmediate(cam.gameObject);
            Time.timeScale = 1f;
            GameFeel.paused = false;
            try { Directory.Delete(dir, true); } catch { }
        }

        void Start(bool dev, System.Action<SaveData> tweak = null, bool splash = false)
        {
            if (flow != null) Object.DestroyImmediate(flow.gameObject);
            var save = dev ? GameRules.SetDevMode(GameRules.DefaultSave(false), true) : GameRules.DefaultSave(false);
            if (tweak != null) tweak(save);
            new SaveStore(dir).Save(save);
            flow = GameFlow.Create(dir, splash);
        }

        IEnumerator Settle(int frames = 6) { for (int i = 0; i < frames; i++) yield return null; }

        // ---------------- stills ----------------

        [UnityTest]
        public IEnumerator Capture_the_new_screens()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            UiClock.Skip = true;

            // the splash on its finished card
            Start(true, null, true);
            yield return Settle();
            flow.Splash.ShowAt(SplashTimeline.FinishedAt);
            yield return Settle(2);
            yield return Shot("splash");
            flow.Splash.Dismiss();

            // the three info overlays and the toast
            Start(true);
            yield return Settle();
            flow.OpenInfo(InfoView.Records);
            yield return Settle();
            yield return Shot("profile_records");
            flow.OpenInfo(InfoView.Trophies);
            yield return Settle();
            yield return Shot("profile_trophies");
            flow.OpenInfo(InfoView.Profile);
            yield return Settle();
            yield return Shot("profile_loadout");
            flow.CloseInfo();
            UiClock.Skip = false;
            flow.Controller.Toast("Acquired", "Onikiri");
            flow.Controller.Toast("Level up", "Kage reaches level 2");
            yield return new WaitForSecondsRealtime(0.9f);
            yield return Shot("toast");
            UiClock.Skip = true;

            // the results: a win with a trophy and a loss
            Start(false, s => { GameRules.MarkStageCleared(s, "prologue.1"); GameRules.MarkStageCleared(s, "act1.stretch1"); GameRules.MarkStageCleared(s, "act1.stretch2"); });
            yield return Settle();
            yield return ToResults("act1.sub1", win: true);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("results_win");
            Start(true);
            yield return Settle();
            yield return ToResults("act1.boss", win: false);
            yield return new WaitForSecondsRealtime(0.4f);
            yield return Shot("results_loss");

            // the pause menu with its Controls button, and the guide over it
            Start(true);
            yield return Settle();
            flow.Controller.OpenCampaign();
            flow.Controller.SelectStage("prologue.1");
            flow.Controller.BeginLoadout();
            yield return Settle(10);
            flow.Controller.StartStage();
            yield return new WaitForSecondsRealtime(2.2f);
            flow.Send(MenuCommand.Pause);
            yield return Settle(4);
            yield return Shot("pause_menu");
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
            flow.World.ForceKnockOut(win);
            end = Time.realtimeSinceStartup + 10f;
            while (flow.Controller.Screen != FlowScreen.Results && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(FlowScreen.Results, flow.Controller.Screen);
        }

        // ---------------- strips ----------------

        float clock = 100f;

        IEnumerator Strip(string name, float[] times, System.Action action)
        {
            // a base time later than now by 10 s: whatever was built before is long finished when the clock is frozen here
            clock = Mathf.Max(clock, Time.unscaledTime) + 10f;
            float t0 = clock;
            UiClock.Freeze(t0);
            action();
            string d = Path.Combine(stripDir, name);
            if (Directory.Exists(d)) Directory.Delete(d, true);
            Directory.CreateDirectory(d);
            shotDir = d;
            foreach (float t in times)
            {
                UiClock.Freeze(t0 + t);
                yield return null;
                yield return null;
                yield return Shot("f" + Mathf.RoundToInt(t * 1000f).ToString("D5"));
            }
            shotDir = outDir;
            UiClock.Unfreeze();
        }

        [UnityTest]
        public IEnumerator Capture_animation_strips()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");

            // the opening splash, frame by frame
            Start(true, null, true);
            yield return Settle();
            {
                string d = Path.Combine(stripDir, "splash");
                if (Directory.Exists(d)) Directory.Delete(d, true);
                Directory.CreateDirectory(d);
                shotDir = d;
                foreach (int ms in new[] { 0, 300, 700, 1100, 1500, 1900, 2300, 2700, 3100, 3600 })
                {
                    flow.Splash.ShowAt(ms / 1000f);
                    yield return null;
                    yield return Shot("f" + ms.ToString("D5"));
                }
                shotDir = outDir;
                flow.Splash.Dismiss();
            }

            // the title opening (the splash is off: the flow opens on the title)
            yield return Strip("title_reveal", new[] { 0f, 0.12f, 0.24f, 0.4f, 0.6f, 0.9f }, () => Start(true));
            yield return Settle();

            // the wipe from the title to the map
            yield return Strip("wipe", new[] { 0f, 0.1f, 0.2f, 0.24f, 0.34f, 0.45f }, () => flow.Controller.OpenCampaign());
            // the map's own entrance after the wipe
            Start(false);
            yield return Settle();
            yield return Strip("campaign_open", new[] { 0.2f, 0.4f, 0.55f, 0.7f, 0.9f, 1.3f }, () => flow.Controller.OpenCampaign());

            // the pre-fight, from the map
            Start(true);
            yield return Settle();
            flow.Controller.OpenCampaign();
            flow.Controller.SelectStage("prologue.1");
            yield return new WaitForSecondsRealtime(0.7f);
            yield return Strip("loadout_open", new[] { 0.24f, 0.4f, 0.55f, 0.75f, 1.0f, 1.4f }, () => flow.Controller.BeginLoadout());

            // the Armory, from the title
            Start(false, s =>
            {
                s.coins = 1500; s.gems = 30; s.owned.weapons["kagetsu"] = 3; s.owned.weapons["tsubame"] = 1; s.owned.helmets["kuro_zukin"] = 2; s.owned.armors["ronin_yoroi"] = 1;
            });
            yield return Settle();
            yield return Strip("armory_open", new[] { 0.24f, 0.4f, 0.55f, 0.75f, 1.0f, 1.5f }, () => flow.Controller.OpenArmory());

            // the controls guide's card pop
            Start(true);
            yield return Settle();
            yield return Strip("controls_open", new[] { 0f, 0.08f, 0.16f, 0.24f, 0.35f, 0.5f }, () => flow.OpenControls());

            // a toast
            Start(true);
            yield return Settle();
            yield return Strip("toast", new[] { 0f, 0.12f, 0.25f, 0.5f, 2.62f, 2.8f }, () => flow.Controller.Toast("Acquired", "Onikiri"));

            // the results, from a won stage
            Start(false, s => { GameRules.MarkStageCleared(s, "prologue.1"); GameRules.MarkStageCleared(s, "act1.stretch1"); GameRules.MarkStageCleared(s, "act1.stretch2"); });
            yield return Settle();
            flow.Controller.OpenCampaign();
            flow.Controller.SelectStage("act1.sub1");
            flow.Controller.BeginLoadout();
            yield return Settle(10);
            flow.Controller.StartStage();
            yield return new WaitForSecondsRealtime(0.5f);
            var win = new FightResult { win = true, hpLeft = 1.0, time = 48, maxCombo = 8, flawless = true };
            yield return Strip("results_open", new[] { 0.24f, 0.5f, 0.8f, 1.1f, 1.6f, 2.4f }, () => flow.Controller.FinishStage(win));
        }

        // ---------------- capture (as in FlowScreenshots) ----------------

        IEnumerator Shot(string name)
        {
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
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
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
                float a = Mathf.Clamp01(1f - ((w.r - f.r) + (w.g - f.g) + (w.b - f.b)) / 3f);
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
