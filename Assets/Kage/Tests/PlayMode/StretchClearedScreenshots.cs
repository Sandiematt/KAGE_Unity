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
    /// SAN-67 review images of the Stretch cleared brush banner (the JS <c>fx.banner</c>): the banner held at moments of its run, the old
    /// big-letter announcer for the before image, and the Fallen banner, in Screenshots/review/SAN-67/stretch_cleared/unity. Needs a GPU:
    /// run without -nographics, with <c>-testFilter StretchClearedScreenshots</c>.
    /// </summary>
    [Category("Screenshots")]
    public class StretchClearedScreenshots
    {
        string dir, shotDir;
        GameFlow flow;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "kage_stretch_cleared_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            shotDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-67", "stretch_cleared", "unity"));
            Directory.CreateDirectory(shotDir);
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

        IEnumerator StartRoad()
        {
            var save = GameRules.SetDevMode(GameRules.DefaultSave(false), true);
            new SaveStore(dir).Save(save);
            flow = GameFlow.Create(dir, false);
            for (int i = 0; i < 6; i++) yield return null;
            flow.Controller.OpenCampaign();
            flow.Controller.SelectStage("act1.stretch1");
            flow.Controller.BeginLoadout();
            for (int i = 0; i < 10; i++) yield return null;
            flow.Controller.StartStage();
            float end = Time.realtimeSinceStartup + 10f;
            while ((flow.World == null || flow.World.phase != StageWorld.Phase.Fight) && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(StageWorld.Phase.Fight, flow.World.phase);
            yield return new WaitForSecondsRealtime(0.3f);
        }

        [UnityTest]
        public IEnumerator Capture_the_stretch_cleared_banner()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            yield return StartRoad();
            var hud = flow.World.Hud;

            flow.World.ForceKnockOut(true);
            Assert.IsTrue(hud.InkBannerShowing);
            Assert.AreEqual("Stretch cleared", hud.InkBannerTitle);
            // Kage takes his victory pose behind it; then the stage is held (the pause stops its clock, so it does not move on to the results)
            yield return new WaitForSecondsRealtime(2.4f);
            flow.World.Paused = true;
            hud.HoldInkBanner(0.7f);
            yield return null;
            yield return Shot("after");

            // the strip: the band paints, holds, fades
            string strip = Path.Combine(shotDir, "strip");
            if (Directory.Exists(strip)) Directory.Delete(strip, true);
            Directory.CreateDirectory(strip);
            string still = shotDir;
            shotDir = strip;
            foreach (float t in new[] { 0.04f, 0.1f, 0.18f, 0.3f, 0.7f, 1.3f, 1.42f, 1.5f })
            {
                hud.HoldInkBanner(t);
                yield return null;
                yield return Shot("f" + Mathf.RoundToInt(t * 1000f).ToString("D5"));
            }
            shotDir = still;

            // before: the old big announcer
            hud.HideInkBanner();
            hud.Announce("STRETCH CLEARED", Palette.TrimBright, 3.2f);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("before");
        }

        [UnityTest]
        public IEnumerator Capture_the_fallen_banner()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            yield return StartRoad();
            var hud = flow.World.Hud;
            flow.World.ForceKnockOut(false);
            Assert.IsTrue(hud.InkBannerShowing);
            yield return new WaitForSecondsRealtime(0.3f);
            flow.World.Paused = true;
            hud.HoldInkBanner(0.7f);
            yield return null;
            yield return Shot("fallen");
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
