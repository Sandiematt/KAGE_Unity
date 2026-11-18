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
    /// The SAN-113 review sheet for the owner (SAN-74 sign-off): Kage's two-handed stances, guards and swing
    /// frames next to the reference image's stances, plus the one-hand punch, the broken sword and a physical
    /// fall. Writes Screenshots/review/SAN-74/san113_sheet.png and every tile to .../san113_tiles/. Needs a GPU:
    /// skips itself under -nographics. Run with <c>-testFilter StanceScreenshots</c> without -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class StanceScreenshots
    {
        const int T = 360;           // tile size (px)
        const int Cols = 5;

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-74"));
        static string RefPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "reference", "two_handed_sword_stances.png"));

        // reference figures, with their caption, in the 2000 x 2000 image (x, y from the top, w, h)
        static readonly (string name, RectInt r)[] RefCrops =
        {
            ("ref_chudan", new RectInt(1460, 880, 560, 560)),
            ("ref_jodan", new RectInt(470, 720, 450, 740)),
            ("ref_gedan", new RectInt(520, 190, 460, 520)),
            ("ref_waki", new RectInt(1020, 1480, 440, 520)),
            ("ref_kasumi", new RectInt(1500, 190, 440, 520)),
        };

        [Test]
        public void RenderTheStanceSheet()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Root, "san113_tiles");
            Directory.CreateDirectory(tiles);

            var reference = new Texture2D(2, 2);
            reference.LoadImage(File.ReadAllBytes(RefPath));

            var rows = new[]
            {
                // the reference stances
                RefCrops.Select(c => (c.name, (Func<Color32[]>)(() => Crop(reference, c.r)))).ToArray(),
                // Kage in the matching stance
                new (string, Func<Color32[]>)[]
                {
                    ("kage_idle_chudan", () => Shot(s => s.Add(40), 40)),
                    ("kage_cleave_f14_jodan", () => Shot(s => s.Add(1, buttons: "S"), 0, f => Cleave(f, 14))),
                    ("kage_low_guard_gedan", () => Shot(s => s.Add(40), 40, pose: PoseLib.CrouchBlock())),
                    ("kage_iai_f6_waki", () => Shot(s => s.Add(1, buttons: "S"), 0, f => RunMove(f, "iai", 6))),
                    ("kage_mirror_guard_kasumi", () => Shot(s => s.Add(1, fwd: -1, buttons: "S"), 0, f => RunMove(f, "counterStance", 12))),
                },
                // guards and swing frames
                new (string, Func<Color32[]>)[]
                {
                    ("kage_high_guard", () => Shot(s => s.Add(40), 40, pose: PoseLib.Block())),
                    ("kage_iai_f10", () => Shot(s => s.Add(1, buttons: "S"), 0, f => RunMove(f, "iai", 10))),
                    ("kage_risingMoon_f9", () => Shot(s => s.Add(1, buttons: "S"), 0, f => RunMove(f, "iai", 13, then: "risingMoon", thenFrame: 9))),
                    ("kage_cleave_f22", () => Shot(s => s.Add(1, buttons: "S"), 0, f => Cleave(f, 22))),
                    ("kage_lowReap_f10", () => Shot(s => s.Add(1, dy: -1, buttons: "S"), 0, f => RunMove(f, "lowReap", 10))),
                },
                // walk, one-hand punch, crouch, broken sword
                new (string, Func<Color32[]>)[]
                {
                    ("kage_walk", () => Shot(s => s.Add(18, fwd: 1), 18)),
                    ("kage_jab_f8_one_hand", () => Shot(s => s.Add(1, buttons: "P"), 0, f => RunMove(f, "jab", 8))),
                    ("kage_crouch", () => Shot(s => s.Add(40, dy: -1), 40)),
                    ("kage_sword_broken_idle", () => Shot(s => s.Add(40), 40, broken: true)),
                    ("kage_sword_broken_crossFist_f9", () => Shot(s => s.Add(1, buttons: "P").Add(7).Add(1, buttons: "P"), 0, f => RunMove(f, "crossFist", 9), broken: true)),
                },
                // a physical fall: swept by the Ronin, launched, impact, lying, the kip-up
                new (string, Func<Color32[]>)[]
                {
                    ("fall_launched_f18", () => Fall(18)),
                    ("fall_launched_f26", () => Fall(26)),
                    ("fall_impact_f34", () => Fall(34)),
                    ("fall_lying_f70", () => Fall(70)),
                    ("fall_getUp_f91", () => Fall(91)),
                },
            };

            var sheet = new Texture2D(T * Cols, T * rows.Length, TextureFormat.RGBA32, false);
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r].Length; c++)
                {
                    var (name, make) = rows[r][c];
                    var px = make();
                    SavePng(Path.Combine(tiles, name + ".png"), px, T, T);
                    sheet.SetPixels32(c * T, (rows.Length - 1 - r) * T, T, T, px);
                }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Root, "san113_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            UnityEngine.Object.DestroyImmediate(reference);
        }

        /// <summary>Kage Cleave is the third katana chain hit: iai, Rising Moon, Cleave.</summary>
        static void Cleave(HeadlessFight f, int frame)
        {
            RunMove(f, "iai", 13, then: "risingMoon", thenFrame: 13);
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

        static Color32[] Shot(Action<ScriptedInput> script, int frames, Action<HeadlessFight> until = null, Pose pose = null, bool broken = false)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f);
            if (broken) f.Kage.BreakSword();
            f.Run(2);
            script(f.KageInput);
            if (frames > 0) f.Run(frames);
            until?.Invoke(f);
            if (pose != null) f.Kage.rig.Apply(pose);
            return Render(new Vector2(f.Kage.Pos.x + 0.15f, Mathf.Max(0.95f, f.Kage.rig.Bone(B.Hips).position.y + 0.25f)));
        }

        static Color32[] Fall(int frame)
        {
            var s = KageMoveScenarios.All().Single(x => x.name == "getUp_after_sweep_vs_ai");
            using var f = new HeadlessFight(KageMoveScenarios.Seed, s.foe, s.kageX, s.roninX, s.meter);
            s.ronin(f.RoninInput);
            f.Run(frame + 1);
            Vector2 c = f.Kage.FallBody(B.Hips) != null ? f.Kage.FallBody(B.Hips).position : f.Kage.Pos + Vector2.up * 0.7f;
            return Render(new Vector2(c.x, 0.95f));
        }

        static Color32[] Crop(Texture2D src, RectInt r)
        {
            // fit the crop into the tile, keeping its aspect, on the reference's green
            var px = new Color32[T * T];
            var bg = src.GetPixel(10, 10);
            float scale = Mathf.Min(T / (float)r.width, T / (float)r.height);
            float ox = (T - r.width * scale) * 0.5f, oy = (T - r.height * scale) * 0.5f;
            for (int y = 0; y < T; y++)
                for (int x = 0; x < T; x++)
                {
                    float sx = (x - ox) / scale, sy = (T - 1 - y - oy) / scale;    // sy from the crop's top
                    Color col = bg;
                    if (sx >= 0 && sy >= 0 && sx < r.width && sy < r.height)
                        col = src.GetPixelBilinear((r.x + sx) / src.width, 1f - (r.y + sy) / src.height);
                    px[y * T + x] = col;
                }
            return px;
        }

        static Color32[] Render(Vector2 centre)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1.15f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.13f, 0.14f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(centre.x, centre.y, -10f);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }

            var rt = new RenderTexture(T, T, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(T, T, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, T, T), 0, 0);
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
