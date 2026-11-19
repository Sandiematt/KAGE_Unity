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
    /// The SAN-82 review sheets: Crimson Moon (the freeze with the cut-in band and the camera closing in, the three cuts, the
    /// step back and sheathing, the giant crescent and the finisher, the foe thrown) and Thousand Shadows (the charge, the cuts,
    /// the finisher) with a Mizuchi Kage. Written to Screenshots/review/SAN-82/ (crimson_moon_sheet.png, thousand_shadows_sheet.png
    /// and the tiles). Needs a GPU: skips itself under -nographics. Run with <c>-testFilter RageArtScreenshots</c>. The effects run
    /// on rendered frames at a fixed 1/60 s, one fight frame per rendered frame.
    /// </summary>
    [Category("Screenshots")]
    public class RageArtScreenshots
    {
        const int W = 640, H = 360, Cols = 4;

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-82"));

        [UnityTest]
        public IEnumerator RenderTheCrimsonMoonSheet()
        {
            yield return Sheet("crimson_moon", null, new[] { 14, 28, 34, 45, 57, 70, 84, 100 }, new[] { "freeze and cut-in", "band leaving, camera in", "cut 1", "cut 2", "cut 3", "step back, sheathe", "giant crescent", "foe thrown" });
        }

        [UnityTest]
        public IEnumerator RenderTheThousandShadowsSheet()
        {
            yield return Sheet("thousand_shadows", "mizuchi", new[] { 6, 12, 22, 34, 52, 70, 85, 100 }, new[] { "charge", "vanish", "cut 1", "cut 3", "cut 5", "Kage returns", "finisher", "after" });
        }

        IEnumerator Sheet(string name, string weapon, int[] ticks, string[] labels)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("needs a GPU: run without -nographics"); yield break; }
            string tiles = Path.Combine(Root, "tiles");
            Directory.CreateDirectory(tiles);

            var objects = new List<GameObject>();
            var vfxGo = new GameObject("ReviewVfx");
            vfxGo.AddComponent<Vfx>();
            objects.Add(vfxGo);
            var camGo = new GameObject("ShotCam");
            objects.Add(camGo);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2.3f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.13f, 0.14f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var rig = camGo.AddComponent<CameraRig>();
            rig.cam = cam;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.position = new Vector3(0f, -0.35f, 5f);
            floor.transform.localScale = new Vector3(40f, 0.7f, 1f);
            var mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.08f, 0.06f, 0.07f, 1f) };
            floor.GetComponent<Renderer>().sharedMaterial = mat;
            objects.Add(floor);

            var kageDef = weapon == null ? null : SpearFamilyFightTests.KageWith(weapon);
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.6f, 0.6f, 100f, kageDef: kageDef);
            var hudGo = new GameObject("HUD", typeof(RectTransform));
            objects.Add(hudGo);
            var hud = hudGo.AddComponent<Hud>();
            hud.Build(cam);
            var canvas = hudGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            hud.Bind(f.Kage);
            hud.SetNames("KAGE  影", "RONIN");
            f.Ctx.Camera = rig;
            f.Ctx.Hud = hud;
            rig.targets.Add(f.Kage.rig.Bone(B.Hips));
            rig.targets.Add(f.Ronin.rig.Bone(B.Hips));
            rig.Snap();

            f.RoninInput.Add(2400);
            f.KageInput.Add(4).Add(1, dy: -1, buttons: "X").Add(2400);

            var shots = new List<Color32[]>();
            float prevCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                int next = 0;
                for (int i = 0; i < 400 && next < ticks.Length; i++)
                {
                    f.Run(1);
                    GameFeel.trauma = 0f;
                    GameFeel.Tick();
                    rig.Follow(1f / 60f);
                    hud.Tick(f.Kage, f.Ronin, 99f, 1, true);
                    yield return null;
                    if (f.Kage.state == FState.Ultimate && f.Kage.RageArtFrame >= ticks[next])
                    {
                        cam.Render();
                        var px = Read(rt);
                        SavePng(Path.Combine(tiles, $"{name}_{next}_tick{ticks[next]}.png"), px, W, H);
                        shots.Add(px);
                        Debug.Log($"[SAN-82 shots] {name} {labels[next]}: art frame {f.Kage.RageArtFrame}, camera {cam.orthographicSize:0.00} at {cam.transform.position.x:0.00}, override {rig.useOverride}, Ronin {f.Ronin.state} hp {f.Ronin.health:0.#}, world {f.Ctx.Clock.WorldScale}");
                        next++;
                    }
                }
                Assert.AreEqual(ticks.Length, shots.Count, "every tile was captured");
                Debug.Log($"[SAN-82 shots] {name}: Kage {f.Kage.state}, camera override {rig.useOverride}, world {f.Ctx.Clock.WorldScale}");
            }
            finally
            {
                Time.captureDeltaTime = prevCapture;
                f.Dispose();
                foreach (var o in objects) if (o != null) Object.DestroyImmediate(o);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(mat);
                GameFeel.trauma = GameFeel.punch = 0f;
            }

            int rows = (shots.Count + Cols - 1) / Cols;
            var sheet = new Texture2D(W * Cols, H * rows, TextureFormat.RGBA32, false);
            for (int k = 0; k < shots.Count; k++)
                sheet.SetPixels32((k % Cols) * W, (rows - 1 - k / Cols) * H, W, H, shots[k]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Root, name + "_sheet.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        static Color32[] Read(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return px;
        }

        static void SavePng(string path, Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
