using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// The review images for the boss bar (SAN-102): the dev arena against the test boss (Footman Captain) before and after the phase change
    /// at half health, written to Screenshots/review/SAN-102/. Needs a GPU: it skips itself under -nographics. Run:
    /// <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter BossPhaseScreenshots</c> (no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class BossPhaseScreenshots
    {
        const int W = 1600, H = 900;
        GameObject _game;

        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-102"));

        [TearDown]
        public void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            KageGame.NextFoeId = null;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.DestroyImmediate(cam.gameObject);
        }

        [UnityTest]
        public IEnumerator The_boss_bar_before_and_after_the_phase_change()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            KageGame.NextFoeId = FootmanBoss.Id;
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            game.enemy.health = game.enemy.maxHealth * 0.7f;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(0, game.Hud.Boss.PhaseIndex);
            yield return Shot("boss_bar_phase1");

            game.enemy.health = game.enemy.maxHealth * 0.45f;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(1, game.Hud.Boss.PhaseIndex);
            yield return Shot("boss_bar_phase2");
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "boss_bar_phase2.png")));
        }

        IEnumerator Shot(string name)
        {
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
                // a canvas is drawn by the camera whose mask holds its layer: the HUD's is on the default layer, so borrow the UI layer
                foreach (var t in c.GetComponentsInChildren<Transform>(true)) { layers.Add((t, t.gameObject.layer)); t.gameObject.layer = 5; }
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();
            yield return null;

            // the world (with its post-processing), then the UI on black and on white: the difference gives the UI's alpha
            var world = Render(main, W, H);
            cam.backgroundColor = Color.black;
            var onBlack = Render(cam, W, H);
            cam.backgroundColor = Color.white;
            var onWhite = Render(cam, W, H);
            var outPx = new Color32[W * H];
            for (int i = 0; i < outPx.Length; i++)
            {
                Color f = onBlack[i], w = onWhite[i];
                float a = Mathf.Clamp01(1f - ((w.r - f.r) + (w.g - f.g) + (w.b - f.b)) / 3f);
                Color under = world[i];
                outPx[i] = new Color(f.r + (1f - a) * under.r, f.g + (1f - a) * under.g, f.b + (1f - a) * under.b, 1f);
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(outPx);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());

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
