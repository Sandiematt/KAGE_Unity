using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The review images of the Hollow Shade redrawn from the owner's painting and SVG (SAN-76 SVG redesign), in
    /// Screenshots/review/SAN-76/svg/: the owner's painting beside the Unity bind pose and fight idle (side_by_side.png), the Shade
    /// beside Kage in idle, walk, attack and hit (kage_vs_hollow_shade_sheet.png) with a close-up and its head, the soft parts in
    /// motion and in the wind (soft_motion.png), and the line-up of every Order and Hollow foe (each tile also saved alone in
    /// lineup/, so the other characters can be compared pixel for pixel before and after). Needs a GPU: it skips itself under
    /// -nographics.
    /// Run with <c>-testFilter ShadeSvgScreenshots</c> and no -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class ShadeSvgScreenshots
    {
        const int W = 800, H = 600;
        static readonly string[] LineUp =
        {
            "order_footman", "order_runner", "order_shieldbearer", "order_chainthrower", "order_ironclad",
            "hollow_shade", "hollow_tutorial", "hollow_flicker", "hollow_husk",
        };
        /// <summary>Characters off the sheet whose soft parts share the code this ticket touches: rendered alone for the pixel check.</summary>
        static readonly string[] Others = { "raikon", "kage" };

        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-76", "svg"));
        static string Painting => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Tools", "hollow_art", "shade", "original", "shade_ref.png"));
        const string Id = "hollow_shade";

        [Test]
        public void RenderTheShadeReviewImages()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Dir, "tiles");
            Directory.CreateDirectory(tiles);
            SideBySide(tiles);

            var shots = new List<Color32[]>
            {
                Pair("idle", 30, -0.75f, 0.75f, (k, r) => { k.Add(60); r.Add(60); }),
                Pair("walk", 22, -1.5f, 1.5f, (k, r) => { k.Add(40, fwd: 1); r.Add(40, fwd: 1); }),
                Pair("attack", 15, -1.15f, 1.15f, (k, r) => { k.Add(1, buttons: "S").Add(60); r.Add(1, buttons: "S").Add(60); }),
                Pair("hit", 17, -0.5f, 0.5f, (k, r) => { k.Add(1, buttons: "S").Add(60); r.Add(60); }),
            };
            var sheet = new Texture2D(W * 2, H * 2, TextureFormat.RGBA32, false);
            for (int i = 0; i < shots.Count; i++) sheet.SetPixels32((i % 2) * W, (1 - i / 2) * H, W, H, shots[i]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "kage_vs_hollow_shade_sheet.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: CharacterCatalog.Get(Id)))
            {
                f.KageInput.Add(300);
                f.RoninInput.Add(300);
                f.Run(20);
                Save(Path.Combine(Dir, "hollow_shade_closeup.png"), Render(f.Ronin.Pos + new Vector2(0f, 1.0f), 1.05f), W, H);
                Save(Path.Combine(Dir, "hollow_shade_head.png"), Render(f.Ronin.Pos + new Vector2(-0.05f, 1.62f), 0.38f), W, H);
            }
            SoftMotion(tiles);
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "side_by_side.png")));
        }

        static Color32[] Pair(string name, int frames, float kageX, float foeX, System.Action<ScriptedInput, ScriptedInput> script)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, foeX, foeDef: CharacterCatalog.Get(Id));
            script(f.KageInput, f.RoninInput);
            f.Run(frames);
            if (name == "hit") f.Ronin.rig.Secondary(0.2f, Vector2.zero);
            var px = Render((f.Kage.Pos + f.Ronin.Pos) * 0.5f + new Vector2(0f, 0.95f), 1.3f);
            Save(Path.Combine(Dir, "tiles", "kage_vs_hollow_shade_" + name + ".png"), px, W, H);
            return px;
        }

        // ---- the painting, the bind pose and the fight idle ----

        static void SideBySide(string tiles)
        {
            const int w = 560, h = 600;
            var painting = new Texture2D(2, 2);
            painting.LoadImage(File.ReadAllBytes(Painting));
            var left = FitCrop(painting, new Rect(240, 170, 960, 690), w, h);
            Object.DestroyImmediate(painting);

            // bind pose: the rig straight from the part set, facing left like the painting
            var go = new GameObject("ShadeBind");
            var rig = go.AddComponent<FighterRig>();
            rig.Build(CharacterCatalog.PartSet(Id).Build(CharacterCatalog.Get(Id)), 1000);
            rig.SetFacing(-1f, true);
            var bind = Render(new Vector2(-0.2f, 1.0f), 1.25f, w, h);
            Object.DestroyImmediate(go);

            // the fight idle, facing left (the foe faces Kage, who stands to its left)
            Color32[] idle;
            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: CharacterCatalog.Get(Id)))
            {
                f.KageInput.Add(300);
                f.RoninInput.Add(300);
                f.Run(40);
                idle = Render(f.Ronin.Pos + new Vector2(-0.2f, 1.0f), 1.25f, w, h);
            }
            Save(Path.Combine(tiles, "side_painting.png"), left, w, h);
            Save(Path.Combine(tiles, "side_unity_bind.png"), bind, w, h);
            Save(Path.Combine(tiles, "side_unity_idle.png"), idle, w, h);
            var sheet = new Texture2D(w * 3, h, TextureFormat.RGBA32, false);
            sheet.SetPixels32(0, 0, w, h, left);
            sheet.SetPixels32(w, 0, w, h, bind);
            sheet.SetPixels32(w * 2, 0, w, h, idle);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "side_by_side.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        /// <summary>A rectangle of the painting (its pixels, y down) scaled to fit a w x h tile.</summary>
        static Color32[] FitCrop(Texture2D src, Rect crop, int w, int h)
        {
            var px = new Color32[w * h];
            float scale = Mathf.Min(w / crop.width, h / crop.height);
            float ox = (w - crop.width * scale) * 0.5f, oy = (h - crop.height * scale) * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sx = crop.x + (x - ox) / scale, syDown = crop.y + (h - 1 - y - oy) / scale;
                    float sy = src.height - 1 - syDown;
                    px[y * w + x] = sx >= 0 && sy >= 0 && sx < src.width && sy < src.height
                        ? (Color32)src.GetPixelBilinear(sx / src.width, sy / src.height)
                        : new Color32(0, 0, 0, 255);
                }
            return px;
        }

        // ---- the soft parts: through a move, and in the wind ----

        const int T = 400;

        static void SoftMotion(string tiles)
        {
            var rows = new (string name, System.Func<Color32[]> make)[3][];
            for (int r = 0; r < 3; r++) rows[r] = new (string, System.Func<Color32[]>)[6];
            for (int c = 0; c < 6; c++)
            {
                int frame = 6 + c * 8;
                rows[0][c] = ($"move_f{frame:00}", () => Soft(frame, Wind.Calm, s => s.Add(20, fwd: 1).Add(6).Add(1, buttons: "S").Add(60)));
                int wf = 20 + c * 12;
                // the Shade faces left: a wind to +x blows at its face and streams the cape back; to -x it blows from behind
                rows[1][c] = ($"headwind_f{wf:00}", () => Soft(wf, Wind.Blowing(new Vector2(3f, 0f), 6f, seed: 76), s => s.Add(200)));
                rows[2][c] = ($"tailwind_f{wf:00}", () => Soft(wf, Wind.Blowing(new Vector2(-3f, 0.5f), 6f, seed: 77), s => s.Add(200)));
            }
            var sheet = new Texture2D(T * 6, T * 3, TextureFormat.RGBA32, false);
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 6; c++)
                {
                    var (name, make) = rows[r][c];
                    var px = make();
                    Save(Path.Combine(tiles, "soft_" + name + ".png"), px, T, T);
                    sheet.SetPixels32(c * T, (2 - r) * T, T, T, px);
                }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "soft_motion.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        static Color32[] Soft(int frames, Wind wind, System.Action<ScriptedInput> script)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3.5f, 0.6f, foeDef: CharacterCatalog.Get(Id));
            f.KageInput.Add(300);
            f.Ronin.rig.wind = wind;
            script(f.RoninInput);
            f.Run(frames);
            return Render(f.Ronin.Pos + new Vector2(-0.1f, 1.05f), 1.3f, T, T);
        }

        [Test]
        public void RenderTheLineUp()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Dir, "lineup");
            Directory.CreateDirectory(tiles);
            var line = new Texture2D(W * LineUp.Length, H, TextureFormat.RGBA32, false);
            for (int i = 0; i < LineUp.Length; i++)
            {
                var px = Solo(LineUp[i], 20);
                Save(Path.Combine(tiles, LineUp[i] + ".png"), px, W, H);
                line.SetPixels32(i * W, 0, W, H, px);
            }
            line.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "lineup_sheet.png"), line.EncodeToPNG());
            Object.DestroyImmediate(line);
            // the soft parts after a walk and a stop: chains and cloth in motion, for the pixel check of the characters off the sheet
            foreach (var id in Others)
                Save(Path.Combine(tiles, id + "_motion.png"), Moving(id), W, H);
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "lineup_sheet.png")));
        }

        static Color32[] Solo(string id, int frames)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: CharacterCatalog.Get(id));
            f.KageInput.Add(300);
            f.RoninInput.Add(300);
            f.Run(frames);
            return Render(f.Ronin.Pos + new Vector2(0f, 1.0f), 1.05f);
        }

        static Color32[] Moving(string id)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: id == "kage" ? null : CharacterCatalog.Get(id));
            f.KageInput.Add(300);
            f.RoninInput.Add(24, fwd: 1).Add(300);
            f.Run(34);
            var who = id == "kage" ? f.Kage : f.Ronin;
            return Render(who.Pos + new Vector2(0f, 1.1f), 1.3f);
        }

        internal static void Save(string path, Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        internal static Color32[] Render(Vector2 centre, float orthoSize, int w = W, int h = H)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
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
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            return px;
        }
    }
}
