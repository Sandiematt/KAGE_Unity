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
    /// The review images for the JS-style HUD (SAN-108), written to Screenshots/review/SAN-108/unity/: a duel, a stretch at its first gate and
    /// a boss fight in the dev arena, each with the HUD over the world (1600 x 900), and the duel HUD with a combo over the harness duel.
    /// The JS reference images are in ../js and the side-by-side sheets in ../side_by_side. Needs a GPU: it skips itself under -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class FightHudScreenshots
    {
        const int W = 1600, H = 900;
        GameObject _game;

        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-108", "unity"));

        [TearDown]
        public void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            KageGame.NextFoeId = null;
            KageGame.NextStretchId = null;
            GameFeel.paused = false;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.DestroyImmediate(cam.gameObject);
        }

        sealed class WorldWalk : IInputSource
        {
            public void Sample() { }
            public FrameInput Poll(Fighter self) => new FrameInput { dx = 1 };
        }

        KageGame Start()
        {
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            game.Hud.DevText = false;
            return game;
        }

        IEnumerator UntilFight(KageGame game)
        {
            float end = Time.realtimeSinceStartup + 10f;
            while (game.Session.State != FightSession.Stage.Fight && Time.realtimeSinceStartup < end) yield return null;
            Assert.AreEqual(FightSession.Stage.Fight, game.Session.State);
            // the fight announcer ("FIGHT!") is on screen for a moment
            yield return new WaitForSecondsRealtime(1.2f);
        }

        [UnityTest]
        public IEnumerator A_duel()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            KageGame.NextFoeId = "order_footman";
            var game = Start();
            yield return UntilFight(game);
            GameFeel.paused = true;      // freeze the fight, the HUD keeps running on real time
            game.player.health = game.player.maxHealth * 0.72f;
            game.player.meter = 100f;
            game.enemy.health = game.enemy.maxHealth * 0.55f;
            game.player.roundsWon = 1;
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Shot("duel");
        }

        [UnityTest]
        public IEnumerator A_stretch_at_its_first_gate()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            KageGame.NextStretchId = "act1.stretch1";
            var game = Start();
            yield return null;
            game.player.input = new WorldWalk();
            float end = Time.realtimeSinceStartup + 25f;
            while (game.Stretch.State != StretchState.Encounter && Time.realtimeSinceStartup < end) yield return null;
            end = Time.realtimeSinceStartup + 10f;
            while (game.Stretch.StretchWave < 1 && Time.realtimeSinceStartup < end) yield return null;
            game.player.input = new PlayerInputSource();
            game.player.meter = 48f;
            yield return new WaitForSecondsRealtime(1.0f);
            GameFeel.paused = true;
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(HudLayout.Stretch, game.Hud.Model.Layout);
            yield return Shot("stretch");
        }

        // SAN-108: on the road, before any gate, the label reads "ENCOUNTERS 0/3". It used to run over Kage's health number; it is centred now.
        [UnityTest]
        public IEnumerator A_road_walking_before_the_first_gate()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            KageGame.NextStretchId = "act1.stretch1";
            var game = Start();
            yield return null;
            yield return new WaitForSecondsRealtime(4.0f);
            Assert.AreEqual(StretchState.Walking, game.Stretch.State, "still on the road, before the first gate");
            Assert.AreEqual(HudLayout.Stretch, game.Hud.Model.Layout);
            StringAssert.StartsWith("ENCOUNTERS 0/", game.Hud.Model.Road.Label);
            GameFeel.paused = true;
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("road_encounters");
        }

        [UnityTest]
        public IEnumerator A_boss_fight()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            KageGame.NextFoeId = "raikon";
            var game = Start();
            yield return UntilFight(game);
            game.player.meter = 100f;
            game.enemy.health = game.enemy.maxHealth * 0.62f;
            float end = Time.realtimeSinceStartup + 4f;
            while (game.Hud.Boss != null && game.Hud.Boss.PhaseIndex < 1 && Time.realtimeSinceStartup < end) yield return null;
            GameFeel.paused = true;
            yield return new WaitForSecondsRealtime(2.6f);
            Assert.AreEqual(HudLayout.Boss, game.Hud.Model.Layout);
            yield return Shot("boss");
        }

        // ---------------- the combo, over the harness duel ----------------

        [UnityTest]
        public IEnumerator The_duel_HUD_with_a_combo_and_rage_ready()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f);
            f.Run(30);
            var camGo = new GameObject("HudShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1.6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.13f, 0.14f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3((f.Kage.Pos.x + f.Ronin.Pos.x) * 0.5f, 1.25f, -10f);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var hudGo = new GameObject("HUD", typeof(RectTransform));
            try
            {
                var hud = hudGo.AddComponent<Hud>();
                hud.Build(cam);
                hud.DevText = false;
                hud.SetNames("KAGE", "RONIN");
                var canvas = hudGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                f.Kage.meter = 100f;
                f.Kage.comboCount = 7;
                f.Kage.roundsWon = 1;
                f.Ronin.health = f.Ronin.maxHealth * 0.4f;
                for (int i = 0; i < 40; i++)
                {
                    hud.Tick(f.Kage, f.Ronin, 47.2f, 2, true);
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                }
                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                RenderTexture.active = prev;
                tex.Apply();
                File.WriteAllBytes(Path.Combine(Dir, "duel_combo.png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(hudGo);
                Object.DestroyImmediate(camGo);
            }
        }

        // ---------------- capture (as BossPhaseScreenshots: the world, then the UI on black and white for its alpha) ----------------

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
                foreach (var t in c.GetComponentsInChildren<Transform>(true)) { layers.Add((t, t.gameObject.layer)); t.gameObject.layer = 5; }
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();
            yield return null;

            var world = Render(main, W, H);
            cam.backgroundColor = Color.black;
            var onBlack = Render(cam, W, H);
            cam.backgroundColor = Color.white;
            var onWhite = Render(cam, W, H);
            var outPx = new Color32[W * H];
            for (int i = 0; i < outPx.Length; i++)
            {
                Color fg = onBlack[i], w = onWhite[i];
                float a = Mathf.Clamp01(1f - ((w.r - fg.r) + (w.g - fg.g) + (w.b - fg.b)) / 3f);
                Color under = world[i];
                outPx[i] = new Color(fg.r + (1f - a) * under.r, fg.g + (1f - a) * under.g, fg.b + (1f - a) * under.b, 1f);
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(outPx);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());

            foreach (var e in canvases) { e.c.renderMode = e.mode; e.c.worldCamera = e.cam; }
            foreach (var l in layers) if (l.t != null) l.t.gameObject.layer = l.layer;
            Object.DestroyImmediate(camGo);
            Object.Destroy(tex);
            Assert.IsTrue(File.Exists(Path.Combine(Dir, name + ".png")));
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
