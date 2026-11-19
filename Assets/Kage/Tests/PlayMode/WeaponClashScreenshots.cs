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

namespace Kage.Tests
{
    /// <summary>
    /// The SAN-93 review sheet: a weapon clash. Top row: Kage's and the Ronin's Iai Draws meet (2 frames before, the frame of the
    /// clash with its flare and sparks, then the recoil). Bottom row: the same frames for a blade against a punch, which is no
    /// clash (the cut just lands). Writes Screenshots/review/SAN-93/clash_sheet.png and its tiles in .../tiles/.
    /// Needs a GPU: skips itself under -nographics. Run with <c>-testFilter WeaponClashScreenshots</c>.
    /// The effects run on rendered frames at a fixed 1/60 s (Time.captureDeltaTime), one fight frame per rendered frame, and
    /// each rendered frame waits 1/60 s of real time because the sparks and rings count unscaled time.
    /// </summary>
    [Category("Screenshots")]
    public class WeaponClashScreenshots
    {
        const int T = 360;
        static readonly int[] Offsets = { -2, 0, 3, 8, 16 };

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-93"));

        static CharacterDef Foe()
        {
            var d = CharacterCatalog.Get(global::Kage.Ronin.Id);
            d.weaponId = "kagetsu";
            return d;
        }

        [UnityTest]
        public IEnumerator RenderTheClashSheet()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("needs a GPU: run without -nographics"); yield break; }
            string tiles = Path.Combine(Root, "tiles");
            Directory.CreateDirectory(tiles);

            int clashFrame;
            using (var r = new HeadlessFight(KageMoveScenarios.Seed, global::Kage.Tests.Foe.Scripted, -0.9f, 0.9f, 0f, foeDef: Foe()))
            {
                r.KageInput.Add(1).Add(1, buttons: "S").Add(600);
                r.RoninInput.Add(1).Add(1, buttons: "S").Add(600);
                r.RunUntil(() => r.Log.Count(CombatEventType.Clashed) > 0, 120);
                clashFrame = r.Events(CombatEventType.Clashed).Single().Frame;
            }

            var rows = new List<List<Color32[]>>();
            float prevCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                foreach (bool clash in new[] { true, false })
                {
                    var row = new List<Color32[]>();
                    var vfxGo = new GameObject("ReviewVfx");
                    vfxGo.AddComponent<Vfx>();
                    var f = new HeadlessFight(KageMoveScenarios.Seed, global::Kage.Tests.Foe.Scripted, clash ? -0.9f : -0.5f, clash ? 0.9f : 0.5f, 0f, foeDef: Foe());
                    try
                    {
                        f.KageInput.Add(1).Add(1, buttons: "S").Add(600);
                        f.RoninInput.Add(1).Add(1, buttons: clash ? "S" : "P").Add(600);
                        int last = clashFrame + Offsets[Offsets.Length - 1];
                        for (int frame = 0; frame <= last; frame++)
                        {
                            f.Run(1);
                            yield return null;
                            yield return new WaitForSecondsRealtime(1f / 60f);     // the sparks and rings run on real time: give each frame its 1/60 s
                            int at = Array.IndexOf(Offsets, frame - clashFrame);
                            if (at >= 0)
                            {
                                var px = Render(new Vector2((f.Kage.Pos.x + f.Ronin.Pos.x) * 0.5f, 1.0f), 1.5f, T, T);
                                SavePng(Path.Combine(tiles, $"{(clash ? "clash" : "blade_vs_fist")}_{at}_frame{frame - clashFrame:+0;-0;0}.png"), px, T, T);
                                row.Add(px);
                            }
                        }
                        Debug.Log($"[SAN-93 shots] clash={clash}: clashes {f.Log.Count(CombatEventType.Clashed)}, hits {f.Log.Count(CombatEventType.Hit)}, Kage {f.Kage.Pos.x:0.00} {f.Kage.state}, Ronin {f.Ronin.Pos.x:0.00} {f.Ronin.state}");
                    }
                    finally
                    {
                        f.Dispose();
                        UnityEngine.Object.DestroyImmediate(vfxGo);
                    }
                    rows.Add(row);
                }
            }
            finally { Time.captureDeltaTime = prevCapture; }

            var sheet = new Texture2D(T * Offsets.Length, T * rows.Count, TextureFormat.RGBA32, false);
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < rows[r].Count; c++)
                    sheet.SetPixels32(c * T, (rows.Count - 1 - r) * T, T, T, rows[r][c]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Root, "clash_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
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
