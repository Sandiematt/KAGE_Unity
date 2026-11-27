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
    /// The review images for Raikon's fight (SAN-107), written to Screenshots/review/SAN-107/: Raikon beside Kage; each ability with the effects live
    /// (Thunder Swing: the wind-up and the wave on the floor; its late second wave in the last phase; Storm Break: the wind-up over the ground mark and
    /// the shock striking; Skyfall: the mark under Kage and the bolt; Redline Counter: the stance and the punish); his hit line on the painted tachi;
    /// and the dev arena against him: his arena with the boss bar in each of the three phases. Needs a GPU: it skips itself under -nographics. Run:
    /// <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter RaikonFightScreenshots</c> (no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class RaikonFightScreenshots
    {
        const int W = 800, H = 600, BigW = 1600, BigH = 900;
        GameObject _game;

        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-107"));
        static CharacterDef Def(string id) => CharacterCatalog.Get(id);

        [TearDown]
        public void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            KageGame.NextFoeId = null;
            foreach (var cam in Object.FindObjectsByType<Camera>()) Object.DestroyImmediate(cam.gameObject);
        }

        static bool In(Fighter s, string id) => s.state == FState.Attack && s.move != null && s.move.id == id;

        /// <summary>A scripted Raikon pressing <paramref name="slot"/> on frame 2; Kage scripted by <paramref name="kage"/>.</summary>
        static HeadlessFight Ability(int slot, float kageX, float raikonX, System.Action<ScriptedInput> kage = null, float health = 1f)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, raikonX, foeDef: Def(Raikon.Id));
            f.Kage.maxHealth = f.Kage.health = 1e6f;
            f.Ronin.health = f.Ronin.maxHealth * health;
            f.RoninInput.Add(2).Add(1, ability: slot).Add(600);
            if (kage != null) kage(f.KageInput); else f.KageInput.Add(600);
            return f;
        }

        static Vector2 Mid(HeadlessFight f, float up = 1.3f) => (f.Kage.Pos + f.Ronin.Pos) * 0.5f + new Vector2(0f, up);

        [Test]
        public void RenderTheCharacterSheet()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -1.0f, 1.0f, foeDef: Def(Raikon.Id));
            f.KageInput.Add(300);
            f.RoninInput.Add(300);
            f.Run(30);
            Save("raikon_beside_kage", Render(Mid(f, 1.5f), 2.0f), W, H);
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "raikon_beside_kage.png")));
        }

        /// <summary>Raikon alone, close, with his hit line drawn on the painted tachi: the cut's blade samples as sparks along the line.</summary>
        [UnityTest]
        public IEnumerator RenderTheHitLineOnTheBlade()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            var f = Ability(1, -3.0f, 0.8f);
            for (int i = 0; i < 60 && !(In(f.Ronin, RaikonAbilities.MoveSwing) && f.Ronin.moveFrame >= f.Ronin.move.hits[0].from + 2); i++) f.Run(1);
            var line = new GameObject("HitLine");
            try
            {
                int n = 12;
                for (int i = 0; i <= n; i++)
                {
                    var p = f.Ronin.rig.World(B.Sword, Vector2.Lerp(f.Ronin.BladeBaseLocal, f.Ronin.BladeTipLocal, i / (float)n));
                    var dot = new GameObject("Dot" + i);
                    dot.transform.SetParent(line.transform, false);
                    dot.transform.position = new Vector3(p.x, p.y, -1f);
                    var r = MeshKit.MakeRenderer("Dot", dot.transform, MeshKit.Disc(Vector2.zero, i == n ? 0.05f : 0.03f, Color.cyan, Color.cyan), MeshKit.Alpha, Vfx.Order + 90);
                }
                Save("raikon_hit_line_on_blade", Render(f.Ronin.Pos + new Vector2(-0.6f, 1.4f), 1.7f), W, H);
            }
            finally { Object.DestroyImmediate(line); f.Dispose(); }
            yield return null;
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "raikon_hit_line_on_blade.png")));
        }

        /// <summary>His abilities with the effects live (a <see cref="Vfx"/> and real frames), so the glints, ground marks, wave and bolt show.</summary>
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
                // Thunder Swing: the blade drawn back (the glint), then the cut with the wave racing along the floor
                var f = Ability(1, -3.4f, 1.0f);
                bool wind = false, wave = false;
                for (int i = 0; i < 140 && !wave; i++)
                {
                    f.Run(1);
                    yield return null;
                    if (!In(f.Ronin, RaikonAbilities.MoveSwing)) continue;
                    var m = f.Ronin.move;
                    if (!wind && f.Ronin.moveFrame >= m.hits[0].from - 5) { wind = true; Shot("thunder_swing_windup", Mid(f, 1.4f), 2.9f, saved); }
                    if (f.Ctx.Projectiles.Count > 0 && f.Ctx.Projectiles[0].Position.x < f.Ronin.Pos.x - 1.8f) { wave = true; Shot("thunder_swing_wave", Mid(f, 1.4f), 2.9f, saved); }
                }
                f.Dispose();
                vfxGo = FreshVfx(vfxGo);

                // the last phase: the second wave follows late (the blade drawn back again)
                f = Ability(1, -3.4f, 1.0f, null, 0.3f);
                bool second = false;
                for (int i = 0; i < 200 && !second; i++)
                {
                    f.Run(1);
                    yield return null;
                    if (!In(f.Ronin, RaikonAbilities.MoveSwing2)) continue;
                    var m = f.Ronin.move;
                    if (f.Ronin.moveFrame >= m.hits[1].from - 4) { second = true; Shot("thunder_swing_second_wave_charge", Mid(f, 1.4f), 2.9f, saved); }
                }
                f.Dispose();
                vfxGo = FreshVfx(vfxGo);

                // Storm Break: the blade raised over the ground mark, then driven into the floor and the shock striking Kage
                f = Ability(2, -1.9f, 1.9f);
                bool raised = false, shock = false;
                for (int i = 0; i < 100 && !shock; i++)
                {
                    f.Run(1);
                    yield return null;
                    if (!In(f.Ronin, RaikonAbilities.MoveBreak)) continue;
                    var m = f.Ronin.move;
                    if (!raised && f.Ronin.moveFrame >= m.hits[0].from - 6) { raised = true; Shot("storm_break_windup", Mid(f, 1.4f), 2.6f, saved); }
                    if (f.Ronin.moveFrame >= m.hits[0].from + 3) { shock = true; Shot("storm_break_shock", Mid(f, 1.4f), 2.6f, saved); }
                }
                f.Dispose();
                vfxGo = FreshVfx(vfxGo);

                // Skyfall: the mark under Kage as the blade is called to the sky, then the bolt
                f = Ability(3, -5f, 3.5f);
                bool mark = false, bolt = false;
                for (int i = 0; i < 100 && !bolt; i++)
                {
                    f.Run(1);
                    yield return null;
                    if (!In(f.Ronin, RaikonAbilities.MoveSkyfall)) continue;
                    var m = f.Ronin.move;
                    int strike = m.hazard.telegraphFrames + 1;
                    if (!mark && f.Ronin.moveFrame >= strike - 14) { mark = true; Shot("skyfall_mark", Mid(f, 1.5f), 4.6f, saved); }
                    if (f.Ronin.moveFrame >= strike + 1) { bolt = true; Shot("skyfall_bolt", Mid(f, 2.2f), 4.6f, saved); }
                }
                f.Dispose();
                vfxGo = FreshVfx(vfxGo);

                // Redline Counter (phase two): the stance, then the punish answering a jab into it
                f = Ability(4, -0.4f, 0.4f, k => k.Add(24).Add(1, buttons: "P").Add(600), 0.65f);
                bool stance = false, punish = false;
                for (int i = 0; i < 100 && !punish; i++)
                {
                    f.Run(1);
                    yield return null;
                    if (!stance && In(f.Ronin, RaikonAbilities.MoveRedline) && f.Ronin.moveFrame >= f.Ronin.move.counterFrom + 4) { stance = true; Shot("redline_stance", Mid(f, 1.3f), 1.9f, saved); }
                    if (In(f.Ronin, RaikonAbilities.MovePunish) && f.Ronin.moveFrame >= f.Ronin.move.hits[0].from + 2) { punish = true; Shot("redline_punish", Mid(f, 1.3f), 2.0f, saved); }
                }
                f.Dispose();
            }
            finally
            {
                Object.DestroyImmediate(vfxGo);
                Time.captureDeltaTime = prevCapture;
            }
            CollectionAssert.IsSubsetOf(new[] { "thunder_swing_windup", "thunder_swing_wave", "thunder_swing_second_wave_charge", "storm_break_windup", "storm_break_shock",
                "skyfall_mark", "skyfall_bolt", "redline_stance", "redline_punish" }, saved);
            Assert.AreEqual(9, saved.Count);
        }

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
        public IEnumerator RenderTheArenaInAllThreePhases()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            KageGame.NextFoeId = Raikon.Id;
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            game.enemy.health = game.enemy.maxHealth * 0.8f;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(0, game.Hud.Boss.PhaseIndex);
            yield return Composite("raikon_arena_phase1");

            game.enemy.health = game.enemy.maxHealth * 0.55f;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(1, game.Hud.Boss.PhaseIndex);
            yield return Composite("raikon_arena_phase2");

            game.enemy.health = game.enemy.maxHealth * 0.25f;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(2, game.Hud.Boss.PhaseIndex);
            yield return Composite("raikon_arena_phase3");
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "raikon_arena_phase3.png")));
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
