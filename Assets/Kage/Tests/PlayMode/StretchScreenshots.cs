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
    /// The SAN-89 review sheet: the test stretch of Footmen on its road, seen through the stretch camera (<see cref="CameraRig"/> with the
    /// session's limits). Six moments, left to right, top to bottom: the whole road with both gates open; the gate shut and the first
    /// wave in; the second wave; Kage against the shut gate (the camera stops at the gate span); the start of the road (the camera
    /// stops at its left end); the stage cleared at the end. Writes Screenshots/review/SAN-89/stretch_sheet.png and its tiles in .../tiles/.
    /// Needs a GPU: skips itself under -nographics. Run with <c>-testFilter StretchScreenshots</c>.
    /// </summary>
    [Category("Screenshots")]
    public class StretchScreenshots
    {
        const int W = 640, H = 360;

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-89"));

        sealed class WorldWalk : IInputSource
        {
            public int dx = 1;
            public void Sample() { }
            public FrameInput Poll(Fighter self) => new FrameInput { dx = dx };
        }

        [UnityTest]
        public IEnumerator RenderTheStretchSheet()
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
            var rig = camGo.AddComponent<CameraRig>();
            rig.cam = cam;
            rig.lookAhead = 2.0f;

            var stretch = StretchData.DevFootmen();
            var f = new StretchFight(89, stretch, rig: rig);
            var shots = new List<Color32[]>();
            var notes = new List<string>();
            float prevCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                rig.SetBounds(f.Session);
                rig.Frame(f.Ctx, f.Kage);
                rig.Snap();
                void Step() { f.Run(1); GameFeel.trauma = GameFeel.punch = 0f; rig.Follow(1f / 60f); }

                IEnumerator Shoot(string name, string note)
                {
                    yield return null;
                    var px = Render(cam);
                    SavePng(Path.Combine(tiles, $"{shots.Count}_{name}.png"), px, W, H);
                    shots.Add(px);
                    notes.Add($"{name}: Kage x {f.Kage.Pos.x:0.00}, camera x {cam.transform.position.x:0.00} size {cam.orthographicSize:0.00}, {note}");
                }

                // 1. the whole road, both gates open (an override framing, as a rage art or a title card would use)
                float len = (float)stretch.Length;
                rig.useOverride = true;
                rig.overrideCenter = new Vector2(len * 0.5f, 4.4f);
                rig.overrideSize = len * 0.5f / cam.aspect + 0.4f;
                rig.Follow(10f);
                yield return Shoot("road_overview", "the road, both gates open");
                rig.useOverride = false;

                // 5. the start of the road: the camera does not show past its left end (taken now, Kage just placed)
                for (int i = 0; i < 90; i++) Step();
                // Kage has walked on; stand him at the start for the picture
                f.Kage.ResetForRound((float)stretch.Start, 1f);
                f.Kage.BeginFight();
                f.Walker.Session = null;
                var still = new WorldWalk { dx = 0 };
                var keep = f.Kage.input;
                f.Kage.input = still;
                for (int i = 0; i < 90; i++) Step();
                rig.Snap();
                yield return Shoot("road_start", "the camera stops at the road's left end");
                f.Kage.input = keep;
                f.Walker.Session = f.Session;

                // 2. gate shut, first wave in
                while (f.Log.Count(CombatEventType.WaveStarted) < 1) Step();
                for (int i = 0; i < 70; i++) Step();
                yield return Shoot("gate_wave1", $"gate shut, wave 1 ({f.Session.WaveNumber}/{f.Session.WaveCount}), foes {f.Session.FoesLeft}, {f.Session.HudText}");

                // 4. Kage against the shut gate: the camera stops at the gate span
                float gate = (float)stretch.Encounters[0].Gate;
                var mid = f.Kage.input;
                f.Kage.input = new WorldWalk { dx = 1 };
                f.Kage.ResetForRound(gate - 1.5f, 1f);
                f.Kage.BeginFight();
                for (int i = 0; i < 150; i++) Step();
                yield return Shoot("gate_bounds", $"Kage held by the gate at {gate:0.00}; camera right edge {cam.transform.position.x + cam.orthographicSize * cam.aspect:0.00}");
                f.Kage.input = mid;

                // 3. the second wave
                while (f.Log.Count(CombatEventType.WaveStarted) < 2 && !f.Session.Failed) Step();
                for (int i = 0; i < 90; i++) Step();
                yield return Shoot("gate_wave2", $"wave 2 of the first encounter: {f.Session.FoesLeft} foes, {f.Session.HudText}");

                // 6. cleared at the end
                while (!f.Session.Cleared && !f.Session.Failed && f.Ctx.Clock.Frame < 14000) Step();
                for (int i = 0; i < 40; i++) Step();
                yield return Shoot("stage_cleared", $"stage cleared: {f.Session.HudText}");
                foreach (var n in notes) Debug.Log("[SAN-89 shots] " + n);
                Assert.IsTrue(f.Session.Cleared, "the run clears the stretch");
            }
            finally
            {
                Time.captureDeltaTime = prevCapture;
                f.Dispose();
                UnityEngine.Object.DestroyImmediate(camGo);
            }

            const int cols = 3;
            int rows = (shots.Count + cols - 1) / cols;
            var sheet = new Texture2D(W * cols, H * rows, TextureFormat.RGBA32, false);
            for (int i = 0; i < shots.Count; i++)
                sheet.SetPixels32((i % cols) * W, (rows - 1 - i / cols) * H, W, H, shots[i]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Root, "stretch_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        static Color32[] Render(Camera cam)
        {
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
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);
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
