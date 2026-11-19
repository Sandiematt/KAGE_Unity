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
    /// The SAN-91 review sheet: a parry. Top row: Kage presses Guard three frames before the Ronin's kick lands, the kick is
    /// parried (gold flash, star, sparks, "PARRY"), the fight freezes briefly, and the Ronin reels and is pushed back. Bottom
    /// row: the same frames with Guard held since well before the kick (an ordinary block). Writes
    /// Screenshots/review/SAN-91/parry_sheet.png and its tiles in .../tiles/. Needs a GPU: skips itself under -nographics.
    /// Run with <c>-testFilter ParryScreenshots</c>. The effects run on rendered frames at a fixed 1/60 s
    /// (Time.captureDeltaTime), one fight frame per rendered frame.
    /// </summary>
    [Category("Screenshots")]
    public class ParryScreenshots
    {
        const int T = 360;
        const int RoninPress = 14;
        /// <summary>Tiles: frames after the kick's landing frame (0 is the frame it would land on).</summary>
        static readonly int[] Offsets = { -2, 0, 3, 14, 34 };

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-91"));

        [UnityTest]
        public IEnumerator RenderTheParrySheet()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("needs a GPU: run without -nographics"); yield break; }
            string tiles = Path.Combine(Root, "tiles");
            Directory.CreateDirectory(tiles);

            int lands;
            using (var r = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f))
            {
                r.RoninInput.Add(RoninPress).Add(1, buttons: "K").Add(600);
                r.KageInput.Add(600);
                r.RunUntil(() => r.Log.Count(CombatEventType.Hit) > 0, 120);
                lands = r.Events(CombatEventType.Hit).Single().Frame;
            }

            var rows = new List<List<Color32[]>>();
            float prevCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                foreach (int pressAt in new[] { lands - 3, lands - 15 })
                {
                    var row = new List<Color32[]>();
                    var vfxGo = new GameObject("ReviewVfx");
                    vfxGo.AddComponent<Vfx>();
                    var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f);
                    try
                    {
                        f.RoninInput.Add(RoninPress).Add(1, buttons: "K").Add(600);
                        f.KageInput.Add(pressAt).Add(600, buttons: "G");
                        int last = lands + Offsets[Offsets.Length - 1];
                        for (int frame = 0; frame <= last; frame++)
                        {
                            f.Run(1);
                            yield return null;
                            int at = Array.IndexOf(Offsets, frame - lands);
                            if (at >= 0)
                            {
                                var px = Render(new Vector2((f.Kage.Pos.x + f.Ronin.Pos.x) * 0.5f, 0.95f), 1.35f, T, T);
                                SavePng(Path.Combine(tiles, $"{(pressAt == lands - 3 ? "parry" : "block")}_{at}_frame{frame - lands:+0;-0;0}.png"), px, T, T);
                                row.Add(px);
                            }
                        }
                        Debug.Log($"[SAN-91 shots] press at {pressAt - lands:+0;-0;0}: parried {f.Log.Count(CombatEventType.Parried)}, blocked {f.Log.Count(CombatEventType.Blocked)}, Ronin {f.Ronin.state} posture {f.Ronin.Posture:0.#}");
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
            File.WriteAllBytes(Path.Combine(Root, "parry_sheet.png"), sheet.EncodeToPNG());
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
