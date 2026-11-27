using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The review renders of SAN-109 (Screenshots/review/SAN-109/unity, 1600 x 900): the title card that opens each Prologue and Act I stage,
    /// and a sheet of the campaign map at three points of progress (a fresh save, part way through Act I, and dev mode with every stage open).
    /// Needs a GPU: it skips itself under -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class SagaScreenshots
    {
        string dir, outDir;
        GameFlow flow;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "kage_saga_shots_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-109", "unity"));
            Directory.CreateDirectory(outDir);
        }

        [TearDown]
        public void TearDown()
        {
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
            tweak?.Invoke(save);
            new SaveStore(dir).Save(save);
            flow = GameFlow.Create(dir);
        }

        IEnumerator Settle(int frames = 6)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Capture_the_title_card_of_every_stage()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Start(true);
            yield return Settle();
            flow.Controller.OpenCampaign();
            foreach (var st in GameRules.Stages)
            {
                flow.Controller.SelectStage(st.Id);
                Assert.IsTrue(flow.Controller.BeginLoadout(), st.Id);
                yield return Settle(10);
                flow.Controller.StartStage();
                yield return new WaitForSecondsRealtime(0.7f);       // the card is at full strength from 0.25 s to 1.1 s
                yield return Shot("titlecard_" + st.Id.Replace('.', '_'));
                flow.Controller.Pause();
                yield return Settle(2);
                flow.Controller.QuitToMap();
                yield return Settle(2);
            }
        }

        [UnityTest]
        public IEnumerator Capture_the_campaign_map_with_stages_unlocked()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            var names = new List<string>();

            Start(false);
            yield return Settle();
            flow.Controller.OpenCampaign();
            yield return Settle();
            yield return Shot("map_1_fresh_save");
            names.Add("map_1_fresh_save");

            Start(false, s => { foreach (var id in new[] { "prologue.1", "act1.stretch1", "act1.sub1", "act1.stretch2" }) GameRules.MarkStageCleared(s, id); });
            yield return Settle();
            flow.Controller.OpenCampaign();
            yield return Settle();
            yield return Shot("map_2_act1_in_progress");
            names.Add("map_2_act1_in_progress");

            Start(true);
            yield return Settle();
            flow.Controller.OpenCampaign();
            yield return Settle();
            yield return Shot("map_3_dev_mode_all_open");
            names.Add("map_3_dev_mode_all_open");

            // the sheet: the three maps stacked, each at half size
            const int W = 800, H = 450;
            var sheet = new Texture2D(W, H * names.Count, TextureFormat.RGBA32, false);
            for (int i = 0; i < names.Count; i++)
            {
                var t = new Texture2D(2, 2);
                t.LoadImage(File.ReadAllBytes(Path.Combine(outDir, names[i] + ".png")));
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++) px[y * W + x] = t.GetPixelBilinear((x + 0.5f) / W, (y + 0.5f) / H);
                sheet.SetPixels(0, (names.Count - 1 - i) * H, W, H, px);
                Object.Destroy(t);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(outDir, "campaign_map_sheet.png"), sheet.EncodeToPNG());
            Object.Destroy(sheet);
        }

        // ---------------- capture (as FlowScreenshots) ----------------

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
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());

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
