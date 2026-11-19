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
    /// The review images for Hisame (SAN-105), written to Screenshots/review/SAN-105/: Hisame beside Kage; Hisame in a line-up with Kuroe and
    /// Garan (she must read apart); Lightning Cut (the low tell with its sparks, the dash cut); Kocho (the start, mid-arc over Kage, the glint
    /// as she lands at his back, the cut); Smoke Bomb (in the air, the burst); and the dev arena against her: her districts arena with the
    /// boss bar in phase one, then in phase two. Needs a GPU: it skips itself under -nographics. Run:
    /// <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter HisameScreenshots</c> (no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class HisameScreenshots
    {
        const int W = 800, H = 600, BigW = 1600, BigH = 900;
        GameObject _game;

        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-105"));
        static CharacterDef Def(string id) => CharacterCatalog.Get(id);

        [TearDown]
        public void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            KageGame.NextFoeId = null;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.DestroyImmediate(cam.gameObject);
        }

        static bool In(Fighter g, string id) => g.state == FState.Attack && g.move != null && g.move.id == id;

        /// <summary>A scripted Hisame pressing <paramref name="slot"/> on frame 2; Kage scripted by <paramref name="kage"/>.</summary>
        static HeadlessFight Ability(int slot, float kageX, float hisameX, System.Action<ScriptedInput> kage = null, bool phaseTwo = false)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, hisameX, foeDef: Def(Hisame.Id));
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

            // Hisame beside Kage, idle
            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.9f, 0.9f, foeDef: Def(Hisame.Id)))
            {
                f.KageInput.Add(300);
                f.RoninInput.Add(300);
                f.Run(30);
                Save("hisame_beside_kage", Render(Mid(f, 1.25f), 1.6f), W, H);
            }

            // Hisame, Kuroe and Garan side by side: three close-ups on one sheet
            var line = new List<Color32[]>();
            foreach (var id in new[] { Hisame.Id, Kuroe.Id, Garan.Id })
                using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -4f, 0.3f, foeDef: Def(id)))
                {
                    f.KageInput.Add(300);
                    f.RoninInput.Add(300);
                    f.Run(30);
                    line.Add(Render(f.Ronin.Pos + new Vector2(0f, 1.25f), 1.6f));
                }
            var sheet = new Texture2D(W * 3, H, TextureFormat.RGBA32, false);
            for (int i = 0; i < line.Count; i++) sheet.SetPixels32(i * W, 0, W, H, line[i]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "hisame_vs_kuroe_vs_garan.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "hisame_vs_kuroe_vs_garan.png")));
        }

        /// <summary>Her three abilities with the effects live (a <see cref="Vfx"/> and real frames), so the sparks, glints and smoke show.</summary>
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
                // Lightning Cut: the low tell (sparks on the fans), then the dash cut
                var f = Ability(1, -0.4f, 1.0f, k => k.Add(600, buttons: "G"));
                bool tell = false, cut = false;
                for (int i = 0; i < 90 && !cut; i++)
                {
                    f.Run(1);
                    yield return null;
                    if (!tell && In(f.Ronin, "hisame.lightningCut") && f.Ronin.moveFrame >= 26) { tell = true; Shot("lightning_cut_tell", Mid(f, 1.1f), 1.6f, saved); }
                    if (In(f.Ronin, "hisame.lightningCut") && f.Ronin.moveFrame >= f.Ronin.move.hits[0].from + 2) { cut = true; Shot("lightning_cut_dash", Mid(f, 1.1f), 1.7f, saved); }
                }
                f.Dispose();
                vfxGo = FreshVfx(vfxGo);

                // Kocho: the start (fans spread), mid-arc over Kage, the glint as she lands at his back, the cut
                f = Ability(2, -0.6f, 0.8f);
                bool start = false, arc = false, glint = false, kcut = false;
                Vector2 frame = new(-0.3f, 1.6f);
                for (int i = 0; i < 90 && !kcut; i++)
                {
                    f.Run(1);
                    yield return null;
                    if (!In(f.Ronin, "hisame.kocho")) continue;
                    var m = f.Ronin.move;
                    if (!start && f.Ronin.moveFrame >= m.vaultFrom - 1) { start = true; Shot("kocho_start", frame, 2.0f, saved); }
                    if (!arc && f.Ronin.moveFrame >= (m.vaultFrom + m.vaultTo) / 2) { arc = true; Shot("kocho_mid_arc", frame, 2.0f, saved); }
                    if (!glint && f.Ronin.moveFrame >= m.vaultTo) { glint = true; Shot("kocho_glint", frame, 2.0f, saved); }
                    if (f.Ronin.moveFrame >= m.hits[0].from + 1) { kcut = true; Shot("kocho_cut", frame, 2.0f, saved); }
                }
                f.Dispose();
                vfxGo = FreshVfx(vfxGo);

                // Smoke Bomb (phase two): the bomb in the air, then the burst on Kage
                f = Ability(3, -2.4f, 1.2f, phaseTwo: true);
                bool flight = false, burst = false;
                int after = -1;
                for (int i = 0; i < 120 && !burst; i++)
                {
                    f.Run(1);
                    yield return null;
                    if (!flight && f.Ctx.Projectiles.Count > 0 && f.Ctx.Projectiles[0].Age >= 20) { flight = true; Shot("smoke_bomb_flight", Mid(f, 1.3f), 2.4f, saved); }
                    if (flight && f.Ctx.Projectiles.Count == 0 && after < 0) after = i;
                    if (after >= 0 && i >= after + 6) { burst = true; Shot("smoke_bomb_burst", Mid(f, 1.3f), 2.4f, saved); }
                }
                f.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(vfxGo);
                Time.captureDeltaTime = prevCapture;
            }
            CollectionAssert.IsSubsetOf(new[] { "lightning_cut_tell", "lightning_cut_dash", "kocho_start", "kocho_mid_arc", "kocho_glint", "kocho_cut",
                "smoke_bomb_flight", "smoke_bomb_burst" }, saved);
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
            KageGame.NextFoeId = Hisame.Id;
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            game.enemy.health = game.enemy.maxHealth * 0.72f;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(0, game.Hud.Boss.PhaseIndex);
            yield return Composite("hisame_arena_phase1");

            game.enemy.health = game.enemy.maxHealth * 0.45f;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(1, game.Hud.Boss.PhaseIndex);
            yield return Composite("hisame_arena_phase2");
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "hisame_arena_phase2.png")));
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
