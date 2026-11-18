using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The SAN-71 review images for the owner: Unity Raikon next to the owner's painting (raikon_side_by_side.png), his
    /// poses on the shared skeleton (raikon_poses.png: idle, walk, attacks, guard) and his soft parts in motion
    /// (raikon_soft_motion.png), in Screenshots/review/SAN-71/ with every tile in .../tiles/. Needs a GPU: skips itself
    /// under -nographics. Run with <c>-testFilter RaikonScreenshots</c> without -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class RaikonScreenshots
    {
        const int T = 400;          // pose tile (px)
        const float Ortho = 1.5f;   // half the tile height in metres (Raikon is about 2.6 m to the top of his ponytail)

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-71"));
        static string Painting => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "raikon_art", "original", "Raikon.png"));
        static readonly Color Bg = new(0.16f, 0.13f, 0.14f, 1f);

        [Test]
        public void RenderTheRaikonReviewSheets()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Root, "tiles");
            Directory.CreateDirectory(tiles);

            SideBySide(tiles);

            var poses = new (string, Func<Color32[]>)[][]
            {
                new (string, Func<Color32[]>)[]
                {
                    ("idle", () => Shot(s => s.Add(40), 40)),
                    ("walk_f10", () => Shot(s => s.Add(30, fwd: 1), 10)),
                    ("walk_f20", () => Shot(s => s.Add(30, fwd: 1), 20)),
                    ("crouch", () => Shot(s => s.Add(40, dy: -1), 40)),
                    ("high_guard", () => Shot(s => s.Add(40), 40, pose: PoseLib.Block())),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("jab_f8", () => Shot(s => s.Add(1, buttons: "P"), 0, f => RunMove(f, "jab", 8))),
                    ("kick_f9", () => Shot(s => s.Add(1, buttons: "K"), 9)),
                    ("iai_f6", () => Shot(s => s.Add(1, buttons: "S"), 0, f => RunMove(f, "iai", 6))),
                    ("iai_f10", () => Shot(s => s.Add(1, buttons: "S"), 0, f => RunMove(f, "iai", 10))),
                    ("lowReap_f10", () => Shot(s => s.Add(1, dy: -1, buttons: "S"), 0, f => RunMove(f, "lowReap", 10))),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("risingMoon_f9", () => Shot(s => s.Add(1, buttons: "S"), 0, f => RunMove(f, "iai", 13, "risingMoon", 9))),
                    ("cleave_f14", () => Shot(s => s.Add(1, buttons: "S"), 0, f => Cleave(f, 14))),
                    ("cleave_f22", () => Shot(s => s.Add(1, buttons: "S"), 0, f => Cleave(f, 22))),
                    ("jump", () => Shot(s => s.Add(1, dy: 1).Add(30), 14)),
                    ("facing_left_idle", () => Shot(s => s.Add(40), 40, facingLeft: true)),
                },
            };
            Sheet(Path.Combine(Root, "raikon_poses.png"), tiles, poses);

            // the soft parts in motion: a walk, a stop and a swing; every 6th frame
            var motion = new (string, Func<Color32[]>)[2][];
            for (int r = 0; r < 2; r++)
            {
                motion[r] = new (string, Func<Color32[]>)[5];
                for (int c = 0; c < 5; c++)
                {
                    int frame = 6 + (r * 5 + c) * 6;
                    motion[r][c] = ($"motion_f{frame:00}", () => Shot(s => s.Add(26, fwd: 1).Add(8).Add(1, buttons: "S").Add(40), frame));
                }
            }
            Sheet(Path.Combine(Root, "raikon_soft_motion.png"), tiles, motion);
        }

        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The painting, Raikon in the skeleton's bind pose (closest to the painting) and in his idle stance.</summary>
        static void SideBySide(string tiles)
        {
            const int w = 560, h = 700;
            var painting = new Texture2D(2, 2);
            painting.LoadImage(File.ReadAllBytes(Painting));
            var left = Fit(painting, w, h);
            UnityEngine.Object.DestroyImmediate(painting);

            // bind pose: the rig straight from the part set, facing left like the painting
            var go = new GameObject("RaikonBind");
            go.transform.localScale = new Vector3(1.24f, 1.24f, 1f);
            var rig = go.AddComponent<FighterRig>();
            rig.Build(CharacterCatalog.PartSet("raikon").Build(CharacterCatalog.Get("raikon")), 1000);
            rig.SetFacing(-1f, true);
            var bind = Render(new Vector2(0f, 1.42f), w, h, 1.5f);
            UnityEngine.Object.DestroyImmediate(go);

            Color32[] idle;
            using (var f = Fight())
            {
                f.Kage.ResetForRound(0f, -1f);
                f.KageInput.Add(60);
                f.Run(60);
                idle = Render(new Vector2(f.Kage.Pos.x, 1.42f), w, h, 1.5f);
            }

            SavePng(Path.Combine(tiles, "side_painting.png"), left, w, h);
            SavePng(Path.Combine(tiles, "side_unity_bind.png"), bind, w, h);
            SavePng(Path.Combine(tiles, "side_unity_idle.png"), idle, w, h);
            var sheet = new Texture2D(w * 3, h, TextureFormat.RGBA32, false);
            sheet.SetPixels32(0, 0, w, h, left);
            sheet.SetPixels32(w, 0, w, h, bind);
            sheet.SetPixels32(w * 2, 0, w, h, idle);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Root, "raikon_side_by_side.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        static HeadlessFight Fight() => new(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: CharacterCatalog.Get(Raikon.Id));

        static void Sheet(string path, string tiles, (string name, Func<Color32[]> make)[][] rows)
        {
            int cols = 0;
            foreach (var r in rows) cols = Mathf.Max(cols, r.Length);
            var sheet = new Texture2D(T * cols, T * rows.Length, TextureFormat.RGBA32, false);
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r].Length; c++)
                {
                    var (name, make) = rows[r][c];
                    var px = make();
                    SavePng(Path.Combine(tiles, name + ".png"), px, T, T);
                    sheet.SetPixels32(c * T, (rows.Length - 1 - r) * T, T, T, px);
                }
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        static void Cleave(HeadlessFight f, int frame)
        {
            RunMove(f, "iai", 13, "risingMoon", 13);
            f.KageInput.Add(1, buttons: "S");
            f.RunUntil(() => f.Kage.move != null && f.Kage.move.id == "cleave" && f.Kage.moveFrame >= frame, 60);
        }

        static void RunMove(HeadlessFight f, string id, int frame, string then = null, int thenFrame = 0)
        {
            f.RunUntil(() => f.Kage.move != null && f.Kage.move.id == id && f.Kage.moveFrame >= frame, 80);
            if (then == null) return;
            f.KageInput.Add(1, buttons: "S");
            f.RunUntil(() => f.Kage.move != null && f.Kage.move.id == then && f.Kage.moveFrame >= thenFrame, 40);
        }

        static Color32[] Shot(Action<ScriptedInput> script, int frames, Action<HeadlessFight> until = null, Pose pose = null, bool facingLeft = false)
        {
            using var f = Fight();
            if (facingLeft) f.Kage.ResetForRound(0f, -1f);
            f.Run(2);
            script(f.KageInput);
            if (frames > 0) f.Run(frames);
            until?.Invoke(f);
            if (pose != null) f.Kage.rig.Apply(pose);
            return Render(new Vector2(f.Kage.Pos.x + (facingLeft ? -0.15f : 0.15f), Mathf.Max(1.3f, f.Kage.rig.Bone(B.Hips).position.y + 0.55f)), T, T, Ortho);
        }

        /// <summary>The painting scaled to fit a w x h tile on black (its own background).</summary>
        static Color32[] Fit(Texture2D src, int w, int h)
        {
            var px = new Color32[w * h];
            float scale = Mathf.Min(w / (float)src.width, h / (float)src.height);
            float ox = (w - src.width * scale) * 0.5f, oy = (h - src.height * scale) * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sx = (x - ox) / scale, sy = (y - oy) / scale;
                    px[y * w + x] = sx >= 0 && sy >= 0 && sx < src.width && sy < src.height
                        ? (Color32)src.GetPixelBilinear(sx / src.width, sy / src.height)
                        : new Color32(0, 0, 0, 255);
                }
            return px;
        }

        static Color32[] Render(Vector2 centre, int w, int h, float ortho)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ortho;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Bg;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(centre.x, centre.y, -10f);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
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
