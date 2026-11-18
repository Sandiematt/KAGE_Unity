using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The SAN-86 review sheet: Kage's guard stance high and low (the blade held straight, SAN-74), a jab blocked by the
    /// guard, a guard-break and the guaranteed critical that follows, and the HUD with both posture bars. Writes
    /// Screenshots/review/SAN-86/guard_sheet.png, its tiles in .../tiles/, and hud_posture.png and hud_guard_break.png.
    /// Needs a GPU: skips itself under -nographics. Run with <c>-testFilter GuardScreenshots</c> without -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class GuardScreenshots
    {
        const int T = 360;

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-86"));

        [Test]
        public void RenderTheGuardSheet()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Root, "tiles");
            Directory.CreateDirectory(tiles);

            var row1 = new (string, Func<Color32[]>)[]
            {
                ("kage_idle", () => Solo(s => s.Add(40), 40)),
                ("kage_guard_high", () => Solo(s => s.Add(40, buttons: "G"), 40)),
                ("kage_guard_low", () => Solo(s => s.Add(40, dy: -1, buttons: "G"), 40)),
                ("kage_guard_close", () => Solo(s => s.Add(40, buttons: "G"), 40, close: true)),
                ("kage_low_guard_close", () => Solo(s => s.Add(40, dy: -1, buttons: "G"), 40, close: true)),
            };
            var row2 = new (string, Func<Color32[]>)[]
            {
                ("jab_blocked", () => Duel(f => f.Events(CombatEventType.Blocked).Any(), 2)),
                ("guard_break_f0", () => Duel(f => f.Kage.GuardBroken, 0, breakIt: true)),
                ("guard_break_f8", () => Duel(f => f.Kage.GuardBroken, 8, breakIt: true)),
                ("guard_break_crit_hit", () => Duel(f => f.Events(CombatEventType.Hit).Any(e => e.TargetId == "kage"), 1, breakIt: true)),
                ("guard_break_recovered", () => Duel(f => f.Events(CombatEventType.GuardBroken).Any() && !f.Kage.GuardBroken && f.Kage.state == FState.Idle, 4, breakIt: true, noCross: true)),
            };
            var rows = new[] { row1, row2 };
            var sheet = new Texture2D(T * 5, T * rows.Length, TextureFormat.RGBA32, false);
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r].Length; c++)
                {
                    var (name, make) = rows[r][c];
                    var px = make();
                    SavePng(Path.Combine(tiles, name + ".png"), px, T, T);
                    sheet.SetPixels32(c * T, (rows.Length - 1 - r) * T, T, T, px);
                }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Root, "guard_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);

            HudShot("hud_posture", broken: false);
            HudShot("hud_guard_break", broken: true);
        }

        /// <summary>Kage alone, in the stance the script leaves him in.</summary>
        static Color32[] Solo(Action<ScriptedInput> script, int frames, bool close = false)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f);
            f.Run(2);
            script(f.KageInput);
            f.Run(frames);
            return close
                ? Render((Vector2)f.Kage.rig.World(B.HandF, Vector2.zero) + new Vector2(0.1f, 0.05f), 0.6f, T, T)
                : Render(new Vector2(f.Kage.Pos.x + 0.15f, 0.95f), 1.15f, T, T);
        }

        /// <summary>Kage guarding against the Ronin's jab, then the cross; with <paramref name="breakIt"/> his bar is tiny, so the jab breaks it.</summary>
        static Color32[] Duel(Func<HeadlessFight, bool> until, int after, bool breakIt = false, bool noCross = false)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f);
            if (breakIt) f.Kage.PostureMax = 1f;
            f.KageInput.Add(300, buttons: "G");
            f.RoninInput.Add(4).Add(1, buttons: "P");
            if (!noCross) f.RoninInput.Add(7).Add(1, buttons: "P");
            f.RoninInput.Add(300);
            f.RunUntil(() => until(f), 200);
            if (after > 0) f.Run(after);
            return Render(new Vector2((f.Kage.Pos.x + f.Ronin.Pos.x) * 0.5f, 0.95f), 1.15f, T, T);
        }

        /// <summary>The fight HUD over a duel: both posture bars, Kage's filled by guarded hits (or broken).</summary>
        static void HudShot(string name, bool broken)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f);
            if (broken) f.Kage.PostureMax = 12f;
            f.KageInput.Add(70, buttons: "G").Add(1, buttons: "P").Add(300);
            f.RoninInput.Add(4).Add(1, buttons: "P").Add(7).Add(1, buttons: "P").Add(9).Add(1, buttons: "P").Add(400);
            if (broken) f.RunUntil(() => f.Kage.GuardBroken, 120).Run(3);
            else f.Run(90);

            const int W = 1600, H = 900;
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
                hud.SetNames("KAGE", "RONIN");
                hud.Tick(f.Kage, f.Ronin, 60f, 1, true);
                var canvas = hudGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                hud.Tick(f.Kage, f.Ronin, 60f, 1, true);
                Canvas.ForceUpdateCanvases();

                cam.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                RenderTexture.active = prev;
                tex.Apply();
                File.WriteAllBytes(Path.Combine(Root, name + ".png"), tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                Debug.Log($"[SAN-86 shots] {name}: Kage posture {f.Kage.Posture:0.##}/{f.Kage.PostureMax} broken={f.Kage.GuardBroken}, Ronin {f.Ronin.Posture:0.##}/{f.Ronin.PostureMax}");
            }
            finally
            {
                cam.targetTexture = null;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(hudGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        static Color32[] Render(Vector2 centre, float size, int w, int h)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.13f, 0.14f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(centre.x, centre.y, -10f);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(go);
            return px;
        }

        static void SavePng(string path, Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
