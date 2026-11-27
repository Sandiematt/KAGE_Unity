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
    /// The review images for Shiden (SAN-106), written to Screenshots/review/SAN-106/: Shiden beside Kage; Shiden in a line-up with Kuroe,
    /// Garan and Hisame (he must read apart, from Kuroe above all, the other spear user); the charging thrust (the wind-up, the run with the
    /// point out); Vanguard Break (the wind-up over its ground mark, the shockwave striking); the Braced Spear (set, the thrust meeting a rush);
    /// Hold the Line (raised, the punish); and the dev arena against him: his districts arena with the boss bar in phase one, then in phase
    /// two. Needs a GPU: it skips itself under -nographics. Run:
    /// <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter ShidenScreenshots</c> (no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class ShidenScreenshots
    {
        const int W = 800, H = 600, BigW = 1600, BigH = 900;
        GameObject _game;

        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-106"));
        static CharacterDef Def(string id) => CharacterCatalog.Get(id);

        [TearDown]
        public void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            KageGame.NextFoeId = null;
            foreach (var cam in Object.FindObjectsByType<Camera>()) Object.DestroyImmediate(cam.gameObject);
        }

        static bool In(Fighter s, string id) => s.state == FState.Attack && s.move != null && s.move.id == id;

        /// <summary>A scripted Shiden pressing <paramref name="slot"/> on frame 2; Kage scripted by <paramref name="kage"/>.</summary>
        static HeadlessFight Ability(int slot, float kageX, float shidenX, System.Action<ScriptedInput> kage = null, bool phaseTwo = false)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, shidenX, foeDef: Def(Shiden.Id));
            f.Kage.maxHealth = f.Kage.health = 1e6f;
            if (phaseTwo) f.Ronin.health = f.Ronin.maxHealth * 0.5f;
            f.RoninInput.Add(2).Add(1, ability: slot).Add(600);
            if (kage != null) kage(f.KageInput); else f.KageInput.Add(600);
            return f;
        }

        static Vector2 Mid(HeadlessFight f, float up = 1.2f) => (f.Kage.Pos + f.Ronin.Pos) * 0.5f + new Vector2(0f, up);

        [Test]
        public void RenderTheCharacterSheet()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);

            // Shiden beside Kage, idle
            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.9f, 0.9f, foeDef: Def(Shiden.Id)))
            {
                f.KageInput.Add(300);
                f.RoninInput.Add(300);
                f.Run(30);
                Save("shiden_beside_kage", Render(Mid(f, 1.35f), 1.75f), W, H);
            }

            // Shiden, Kuroe, Garan and Hisame side by side: four close-ups on one sheet
            var line = new List<Color32[]>();
            foreach (var id in new[] { Shiden.Id, Kuroe.Id, Garan.Id, Hisame.Id })
                using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -4f, 0.3f, foeDef: Def(id)))
                {
                    f.KageInput.Add(300);
                    f.RoninInput.Add(300);
                    f.Run(30);
                    line.Add(Render(f.Ronin.Pos + new Vector2(0f, 1.4f), 1.8f));
                }
            var sheet = new Texture2D(W * line.Count, H, TextureFormat.RGBA32, false);
            for (int i = 0; i < line.Count; i++) sheet.SetPixels32(i * W, 0, W, H, line[i]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "shiden_vs_kuroe_vs_garan_vs_hisame.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "shiden_vs_kuroe_vs_garan_vs_hisame.png")));
        }

        /// <summary>His abilities with the effects live (a <see cref="Vfx"/> and real frames), so the glints, ground marks and shocks show.</summary>
        [UnityTest]
        public IEnumerator RenderTheAbilities()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            float prevCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            var vfxGo = new GameObject("ReviewVfx");
            vfxGo.AddComponent<Vfx>();
            var saved = new List<string>();
            try
            {
                // the charging thrust: the wind-up (drawn back low, the glint), then the run with the point out, about to meet Kage's guard
                var f = Ability(4, -2.6f, 2.6f, k => k.Add(600, buttons: "G"));
                bool wind = false, run = false;
                for (int i = 0; i < 160 && !run; i++)
                {
                    f.Run(1);
                    yield return null;
                    if (!In(f.Ronin, "shiden.chargingThrust")) continue;
                    var m = f.Ronin.move;
                    if (!wind && f.Ronin.moveFrame >= m.hits[0].from - 8) { wind = true; Shot("charge_windup", Mid(f, 1.3f), 2.4f, saved); }
                    if (f.Ronin.Pos.x - f.Kage.Pos.x < 3.0f) { run = true; Shot("charge_thrust", Mid(f, 1.3f), 2.4f, saved); }
                }
                f.Dispose();
                vfxGo = FreshVfx(vfxGo);

                // Vanguard Break: the yari raised over the ground mark, then the spear in the ground and the shockwave striking Kage
                f = Ability(1, -1.6f, 1.4f);
                bool raised = false, shock = false;
                for (int i = 0; i < 90 && !shock; i++)
                {
                    f.Run(1);
                    yield return null;
                    if (!In(f.Ronin, "shiden.vanguardBreak")) continue;
                    var m = f.Ronin.move;
                    if (!raised && f.Ronin.moveFrame >= m.hits[0].from - 6) { raised = true; Shot("vanguard_break_windup", Mid(f, 1.3f), 2.2f, saved); }
                    if (f.Ronin.moveFrame >= m.hits[0].from + 3) { shock = true; Shot("vanguard_break_shockwave", Mid(f, 1.3f), 2.2f, saved); }
                }
                f.Dispose();
                vfxGo = FreshVfx(vfxGo);

                // the Braced Spear: set low and level (the glint), then the thrust meeting Kage as he walks onto the point
                f = Ability(2, -1.7f, 1.3f, k => k.Add(40).Add(200, fwd: 1));
                bool set = false, thrust = false;
                for (int i = 0; i < 160 && !thrust; i++)
                {
                    f.Run(1);
                    yield return null;
                    if (!set && In(f.Ronin, "shiden.brace") && f.Ronin.moveFrame >= f.Ronin.move.braceFrom + 4) { set = true; Shot("brace_set", Mid(f, 1.2f), 2.0f, saved); }
                    if (In(f.Ronin, "shiden.braceThrust") && f.Ronin.moveFrame >= 3) { thrust = true; Shot("brace_thrust", Mid(f, 1.2f), 2.0f, saved); }
                }
                f.Dispose();
                vfxGo = FreshVfx(vfxGo);

                // Hold the Line (phase two): the shaft raised, then the punish answering a jab into it
                f = Ability(3, -0.37f, 0.37f, k => k.Add(12).Add(1, buttons: "P").Add(600), phaseTwo: true);
                bool guard = false, punish = false;
                for (int i = 0; i < 90 && !punish; i++)
                {
                    f.Run(1);
                    yield return null;
                    if (!guard && In(f.Ronin, "shiden.holdTheLine") && f.Ronin.moveFrame >= f.Ronin.move.counterFrom + 6) { guard = true; Shot("vanguard_guard_raised", Mid(f, 1.2f), 1.8f, saved); }
                    if (In(f.Ronin, "shiden.holdTheLinePunish") && f.Ronin.moveFrame >= f.Ronin.move.hits[0].from + 2) { punish = true; Shot("vanguard_guard_punish", Mid(f, 1.2f), 1.9f, saved); }
                }
                f.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(vfxGo);
                Time.captureDeltaTime = prevCapture;
            }
            CollectionAssert.IsSubsetOf(new[] { "charge_windup", "charge_thrust", "vanguard_break_windup", "vanguard_break_shockwave", "brace_set", "brace_thrust",
                "vanguard_guard_raised", "vanguard_guard_punish" }, saved);
        }

        /// <summary>A new effects layer for the next scene, so nothing of the last one shows in it.</summary>
        static GameObject FreshVfx(GameObject old)
        {
            Object.DestroyImmediate(old);
            var go = new GameObject("ReviewVfx");
            go.AddComponent<Vfx>();
            return go;
        }

        static void Shot(string name, Vector2 centre, float size, List<string> saved)
        {
            Save(name, Render(centre, size), W, H);
            saved.Add(name);
        }

        [UnityTest]
        public IEnumerator RenderTheArenaAndPhaseTwo()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            KageGame.NextFoeId = Shiden.Id;
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            game.enemy.health = game.enemy.maxHealth * 0.72f;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(0, game.Hud.Boss.PhaseIndex);
            yield return Composite("shiden_arena_phase1");

            game.enemy.health = game.enemy.maxHealth * 0.45f;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(1, game.Hud.Boss.PhaseIndex);
            yield return Composite("shiden_arena_phase2");
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "shiden_arena_phase2.png")));
        }

        // ---------------------------------------------------------------- capture

        static void Save(string name, Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static Color32[] Render(Vector2 centre, float orthoSize)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.36f, 0.34f, 0.33f, 1f);       // a mid grey: dark costumes read against it
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(centre.x, centre.y, -10f);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            return px;
        }

        /// <summary>The game camera's view with the HUD composited over it (the SAN-102 boss bar capture).</summary>
        IEnumerator Composite(string name)
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
            foreach (var c in Object.FindObjectsByType<Canvas>())
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

            var world = RenderCam(main);
            cam.backgroundColor = Color.black;
            var onBlack = RenderCam(cam);
            cam.backgroundColor = Color.white;
            var onWhite = RenderCam(cam);
            var outPx = new Color32[BigW * BigH];
            for (int i = 0; i < outPx.Length; i++)
            {
                Color f = onBlack[i], w = onWhite[i];
                float a = Mathf.Clamp01(1f - ((w.r - f.r) + (w.g - f.g) + (w.b - f.b)) / 3f);
                Color under = world[i];
                outPx[i] = new Color(f.r + (1f - a) * under.r, f.g + (1f - a) * under.g, f.b + (1f - a) * under.b, 1f);
            }
            Save(name, outPx, BigW, BigH);

            foreach (var e in canvases) { e.c.renderMode = e.mode; e.c.worldCamera = e.cam; }
            foreach (var l in layers) if (l.t != null) l.t.gameObject.layer = l.layer;
            Object.DestroyImmediate(camGo);
        }

        static Color[] RenderCam(Camera cam)
        {
            var rt = new RenderTexture(BigW, BigH, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(BigW, BigH, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, BigW, BigH), 0, 0);
            RenderTexture.active = prev;
            var px = tex.GetPixels();
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
            return px;
        }
    }
}
