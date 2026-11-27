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
    /// The review images for Kuroe (SAN-103), written to Screenshots/review/SAN-103/: Kuroe beside Kage; Kuroe beside the Order Footman and
    /// the Hollow Shade (she must read apart from both); Censure raised and its punish; and the dev arena against her: her rooftops arena
    /// with the boss bar in phase one, then in phase two. Needs a GPU: it skips itself under -nographics. Run:
    /// <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter KuroeScreenshots</c> (no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class KuroeScreenshots
    {
        const int W = 800, H = 600, BigW = 1600, BigH = 900;
        GameObject _game;

        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-103"));
        static CharacterDef Def(string id) => CharacterCatalog.Get(id);

        [TearDown]
        public void TearDown()
        {
            if (_game != null) Object.DestroyImmediate(_game);
            KageGame.NextFoeId = null;
            foreach (var cam in Object.FindObjectsByType<Camera>()) Object.DestroyImmediate(cam.gameObject);
        }

        [Test]
        public void RenderTheCharacterSheet()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);

            // Kuroe beside Kage, idle
            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.8f, 0.8f, foeDef: Def(Kuroe.Id)))
            {
                f.KageInput.Add(300);
                f.RoninInput.Add(300);
                f.Run(30);
                Save("kuroe_beside_kage", Render((f.Kage.Pos + f.Ronin.Pos) * 0.5f + new Vector2(0f, 1.15f), 1.5f), W, H);
            }

            // Kuroe, the Footman and the Shade side by side: three close-ups on one sheet
            var line = new List<Color32[]>();
            foreach (var id in new[] { Kuroe.Id, "order_footman", "hollow_shade" })
                using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -4f, 0.3f, foeDef: Def(id)))
                {
                    f.KageInput.Add(300);
                    f.RoninInput.Add(300);
                    f.Run(30);
                    line.Add(Render(f.Ronin.Pos + new Vector2(0f, 1.15f), 1.5f));
                }
            var sheet = new Texture2D(W * 3, H, TextureFormat.RGBA32, false);
            for (int i = 0; i < line.Count; i++) sheet.SetPixels32(i * W, 0, W, H, line[i]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "kuroe_vs_footman_vs_shade.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            // Censure: raised, then Kage's jab is turned aside and the punish comes
            int up;
            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f, foeDef: Def(Kuroe.Id)))
            {
                f.Kage.maxHealth = f.Kage.health = 1e6f;
                f.RoninInput.Add(2).Add(1, ability: 1).Add(600);
                f.KageInput.Add(600);
                f.RunUntil(() => f.Ronin.state == FState.Attack && f.Ronin.move != null && f.Ronin.moveFrame >= 12, 60);
                up = f.Frames.Count;
                Save("censure_raised", Render((f.Kage.Pos + f.Ronin.Pos) * 0.5f + new Vector2(0f, 1.1f), 1.4f), W, H);
            }
            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f, foeDef: Def(Kuroe.Id)))
            {
                f.Kage.maxHealth = f.Kage.health = 1e6f;
                f.RoninInput.Add(2).Add(1, ability: 1).Add(600);
                f.KageInput.Add(up).Add(1, buttons: "P").Add(600);
                f.RunUntil(() => f.Log.Count(CombatEventType.Countered) > 0, 60);
                f.Run(2);
                Save("censure_counter", Render((f.Kage.Pos + f.Ronin.Pos) * 0.5f + new Vector2(0f, 1.1f), 1.4f), W, H);
                f.RunUntil(() => f.Ronin.move != null && f.Ronin.move.id == "kuroe.censurePunish" && f.Ronin.moveFrame >= 11, 60);
                Save("censure_punish", Render((f.Kage.Pos + f.Ronin.Pos) * 0.5f + new Vector2(0f, 1.1f), 1.4f), W, H);
            }
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "censure_punish.png")));
        }

        [UnityTest]
        public IEnumerator RenderTheArenaAndPhaseTwo()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            KageGame.NextFoeId = Kuroe.Id;
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            game.enemy.health = game.enemy.maxHealth * 0.72f;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(0, game.Hud.Boss.PhaseIndex);
            yield return Composite("kuroe_arena_phase1");

            game.enemy.health = game.enemy.maxHealth * 0.45f;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            yield return null;
            Assert.AreEqual(1, game.Hud.Boss.PhaseIndex);
            yield return Composite("kuroe_arena_phase2");
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "kuroe_arena_phase2.png")));
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
