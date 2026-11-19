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
    /// The SAN-83 review sheet: Kage against four Footmen (the dev arena's F6 group fight) in the arena, seen through the
    /// fight camera framing Kage and the engaged foes (<see cref="CameraRig.Frame"/>). A red mark over a foe's head: engaged
    /// (may attack); a grey one: waiting (keeps its distance and circles). Six moments of one AI-driven fight, left to right,
    /// top to bottom. Writes Screenshots/review/SAN-83/group_fight_sheet.png and its tiles in .../tiles/. Needs a GPU: skips
    /// itself under -nographics. Run with <c>-testFilter MultiTargetScreenshots</c>.
    /// </summary>
    [Category("Screenshots")]
    public class MultiTargetScreenshots
    {
        const int W = 640, H = 360;
        static readonly int[] Shots = { 20, 140, 260, 380, 520, 700 };
        static readonly float[] FourFootmen = { 1.3f, -2.4f, 2.8f, -3.8f };

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-83"));

        [UnityTest]
        public IEnumerator RenderTheGroupFightSheet()
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
            var arena = new GameObject("ReviewArena");
            ArenaBuilder.Build(arena.transform, rig);

            var kage = CharacterCatalog.Get(KageCharacter.Id);
            kage.damageMul = 1.5f;
            kage.healthMul = 20f;
            var f = new GroupFight(83, 0f, FourFootmen, kageAi: true, kageDef: kage, aiLevel: 0.9f);
            var marks = new List<(GameObject engaged, GameObject waiting)>();
            foreach (var _ in f.Foes)
                marks.Add((Mark(new Color(0.95f, 0.2f, 0.18f, 1f)), Mark(new Color(0.6f, 0.6f, 0.62f, 1f))));
            var shots = new List<Color32[]>();
            float prevCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                rig.Frame(f.Ctx, f.Kage);
                rig.Snap();
                for (int frame = 0; frame <= Shots[Shots.Length - 1]; frame++)
                {
                    f.Run(1);
                    GameFeel.trauma = GameFeel.punch = 0f;
                    rig.Follow(1f / 60f);
                    for (int i = 0; i < f.Foes.Count; i++)
                    {
                        var foe = f.Foes[i];
                        bool standing = foe.state != FState.KO;
                        bool engaged = f.Ctx.Engagement.IsEngaged(foe);
                        var head = (Vector3)(Vector2)foe.rig.Bone(B.Head).position + new Vector3(0f, 0.45f, 0f);
                        marks[i].engaged.SetActive(standing && engaged);
                        marks[i].waiting.SetActive(standing && !engaged);
                        marks[i].engaged.transform.position = head;
                        marks[i].waiting.transform.position = head;
                    }
                    yield return null;
                    int at = Array.IndexOf(Shots, frame);
                    if (at < 0) continue;
                    var px = Render(cam);
                    SavePng(Path.Combine(tiles, $"group_{at}_frame{frame}.png"), px, W, H);
                    shots.Add(px);
                    string engagedIds = string.Join(",", f.Ctx.Engagement.Engaged.Select(e => e.FighterId));
                    Debug.Log($"[SAN-83 shots] frame {frame}: Kage x {f.Kage.Pos.x:0.00} target {f.Kage.opponent?.FighterId ?? "-"}, engaged {engagedIds}, " +
                              $"attacking {f.Attacking()}, KOs {f.Log.Count(CombatEventType.KnockedOut)}, camera x {cam.transform.position.x:0.00} size {cam.orthographicSize:0.00}");
                }
            }
            finally
            {
                Time.captureDeltaTime = prevCapture;
                f.Dispose();
                foreach (var (a, b) in marks) { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
                UnityEngine.Object.DestroyImmediate(arena);
                UnityEngine.Object.DestroyImmediate(camGo);
            }

            const int cols = 3;
            int rows = (shots.Count + cols - 1) / cols;
            var sheet = new Texture2D(W * cols, H * rows, TextureFormat.RGBA32, false);
            for (int i = 0; i < shots.Count; i++)
                sheet.SetPixels32((i % cols) * W, (rows - 1 - i / cols) * H, W, H, shots[i]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Root, "group_fight_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        static GameObject Mark(Color c)
        {
            var go = new GameObject("EngagementMark");
            MeshKit.MakeRenderer("Disc", go.transform, MeshKit.Disc(Vector2.zero, 0.11f, c, c, 24, 1f, 1f), MeshKit.Alpha, 4000);
            return go;
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
