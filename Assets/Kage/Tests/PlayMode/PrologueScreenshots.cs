using System;
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
    /// The SAN-96 review sheet: the Prologue on its road, HUD included. The title card; each of the six prompts (move, jump, attack, guard,
    /// parry, dodge) as it shows; the tutorial Hollow's encounter; the Hollow Shade's encounter; the stage cleared. A scripted Kage
    /// (<see cref="PrologueBot"/>) is held at each moment. Writes Screenshots/review/SAN-96/prologue_sheet.png and its tiles in .../tiles/.
    /// Needs a GPU: skips itself under -nographics. Run with <c>-testFilter PrologueScreenshots</c>.
    /// </summary>
    [Category("Screenshots")]
    public class PrologueScreenshots
    {
        const int W = 960, H = 540;

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-96"));

        [UnityTest]
        public IEnumerator RenderThePrologueSheet()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("needs a GPU: run without -nographics"); yield break; }
            string tiles = Path.Combine(Root, "tiles");
            Directory.CreateDirectory(tiles);

            var camGo = new GameObject("ReviewCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.aspect = (float)W / H;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var rig = camGo.AddComponent<CameraRig>();
            rig.cam = cam;
            rig.lookAhead = 2.0f;

            var bot = new PrologueBot(0.9f);
            var f = new StretchFight(96, StretchData.FromId("prologue.1"), rig: rig, tutorial: true, kageInput: sf => { bot.Fight = sf; return bot; });
            var hudGo = new GameObject("HUD", typeof(RectTransform));
            var hud = hudGo.AddComponent<Hud>();
            hud.Build(cam);
            var hint = hudGo.transform.Find("Hint");
            if (hint != null) hint.gameObject.SetActive(false);        // the dev arena's key hint
            var canvas = hudGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            hud.Bind(f.Kage);
            hud.SetNames("KAGE  影", "");
            hud.SetFormat(FightFormat.Stretch);
            f.Ctx.Hud = hud;
            f.Ctx.Camera = rig;

            var shots = new List<Color32[]>();
            var notes = new List<string>();
            float prevCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                rig.SetBounds(f.Session);
                rig.Frame(f.Ctx, f.Kage);
                rig.Snap();
                void Step()
                {
                    f.Run(1);
                    GameFeel.trauma = GameFeel.punch = 0f;
                    GameFeel.Tick();
                    rig.Follow(1f / 60f);
                    var foe = f.Kage.opponent != null && f.Kage.opponent.state != FState.KO ? f.Kage.opponent : null;
                    hud.SetPrompt(hud.TitleCardShowing ? null : f.Tutorial.NowView(), f.Tutorial.DoneView(), f.Kage.Pos + Vector2.up * 2.3f);
                    hud.Tick(f.Kage, foe, 0f, 1, false);
                    hud.SetMode(f.Session.HudText);
                }
                // rendered frames matter to the HUD (its fades and the slip's drift run on rendered time)
                IEnumerator Settle(int n) { for (int i = 0; i < n; i++) { Step(); yield return null; } }
                IEnumerator Shoot(string name, string note)
                {
                    yield return null;
                    cam.Render();
                    var px = Read(rt);
                    SavePng(Path.Combine(tiles, $"{shots.Count}_{name}.png"), px, W, H);
                    shots.Add(px);
                    notes.Add($"{name}: Kage x {f.Kage.Pos.x:0.00}, {note}");
                }
                bool Showing(string step) => f.Tutorial.Current != null && f.Tutorial.Current.Step == step;
                IEnumerator ToPrompt(string step, int settle)
                {
                    int guard = 0;
                    while (!Showing(step) && guard++ < 5000) Step();
                    Assert.IsTrue(Showing(step), step + " prompt shows");
                    bot.Override = (self, p) => new FrameInput();      // held where the prompt shows
                    yield return Settle(settle);
                }

                // 1. the title card, over the start of the road
                hud.TitleCard("Prologue · Stretch", f.Session.Data.Name, null, 5f);
                yield return Settle(24);
                yield return Shoot("title_card", "plain title card, no story scene");
                hud.HideTitleCard();

                // 2-3. move and jump
                bot.Override = (self, p) => new FrameInput();
                yield return Settle(40);
                yield return Shoot("prompt_move", "move: A / ← and D / →");
                bot.Override = null;
                yield return ToPrompt("jump", 30);
                yield return Shoot("prompt_jump", "jump");
                bot.Override = null;
                yield return ToPrompt("attack", 30);
                yield return Shoot("prompt_attack", "attack");
                bot.Override = null;

                // 4-6. guard, parry, dodge against the practice partner, as its attack winds up
                foreach (var step in new[] { "guard", "parry", "dodge" })
                {
                    yield return ToPrompt(step, 20);
                    int g = 0;
                    var partner = f.Tutorial.Partner;
                    while (g++ < 1200 && !(partner.InAttackMove && partner.moveFrame >= 3)) Step();
                    yield return Settle(1);
                    yield return Shoot("prompt_" + step, $"{step}: the practice partner's attack winds up");
                    bot.Override = null;
                    int done = f.Tutorial.Done;
                    g = 0;
                    while (f.Tutorial.Done == done && g++ < 1200) Step();
                    yield return Settle(18);
                    if (step == "dodge") yield return Shoot("prompt_dodge_done", "the dodge is done: crossed out, the world slowed");
                }

                // 7. the tutorial Hollow
                int guardN = 0;
                while (f.Session.StretchWave < 1 && guardN++ < 4000) Step();
                yield return Settle(30);
                yield return Shoot("encounter_tutorial_hollow", $"gate 1 shut: {f.Session.HudText}");
                // 8. the Hollow Shade
                guardN = 0;
                while (f.Session.StretchWave < 2 && guardN++ < 8000) Step();
                yield return Settle(30);
                yield return Shoot("encounter_hollow_shade", $"gate 2 shut: {f.Session.HudText}");
                // 9. cleared
                guardN = 0;
                while (!f.Session.Cleared && !f.Session.Failed && guardN++ < 8000) Step();
                yield return Settle(40);
                yield return Shoot("stage_cleared", $"stage cleared: {f.Session.HudText}");
                foreach (var n in notes) Debug.Log("[SAN-96 shots] " + n);
                Assert.IsTrue(f.Session.Cleared, "the run clears the Prologue");
            }
            finally
            {
                Time.captureDeltaTime = prevCapture;
                f.Dispose();
                UnityEngine.Object.DestroyImmediate(hudGo);
                UnityEngine.Object.DestroyImmediate(camGo);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                GameFeel.trauma = GameFeel.punch = 0f;
            }

            const int cols = 3;
            int rows = (shots.Count + cols - 1) / cols;
            var sheet = new Texture2D(W * cols, H * rows, TextureFormat.RGBA32, false);
            for (int i = 0; i < shots.Count; i++)
                sheet.SetPixels32((i % cols) * W, (rows - 1 - i / cols) * H, W, H, shots[i]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Root, "prologue_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        static Color32[] Read(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            UnityEngine.Object.DestroyImmediate(tex);
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
