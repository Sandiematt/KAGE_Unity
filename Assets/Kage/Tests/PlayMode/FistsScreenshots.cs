using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The SAN-80 review sheet: Kage's bare hands in the stance, the armoured swing, the grapple's grab, carry and release,
    /// and the rest of the set. Writes Screenshots/review/SAN-80/san80_sheet.png and every tile to .../san80_tiles/.
    /// Needs a GPU: skips itself under -nographics. Run with <c>-testFilter FistsScreenshots</c> without -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class FistsScreenshots
    {
        const int W = 560, H = 420, Cols = 4;
        const float Half = 1.5f;

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-80"));

        delegate MoveDef Pick(FistsMoves s);

        [Test]
        public void RenderTheFistsSheet()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Root, "san80_tiles");
            Directory.CreateDirectory(tiles);

            var rows = new[]
            {
                new (string, Func<Color32[]>)[]
                {
                    ("stance_idle", () => Idle(40)),
                    ("stance_crouch", () => Idle(40, dy: -1)),
                    ("haymaker_f4_sink", () => Move(s => s.haymaker, 4)),
                    ("haymaker_f11_wound_up", () => Move(s => s.haymaker, 11)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("haymaker_f15_over_the_top", () => Move(s => s.haymaker, 15)),
                    ("haymaker_f19_through", () => Move(s => s.haymaker, 19)),
                    ("grapple_f6_reach", () => Move(s => s.grapple, 6, close: true)),
                    ("grapple_f10_grab", () => Move(s => s.grapple, 10, close: true)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("grapple_f21_lift", () => Move(s => s.grapple, 21, close: true)),
                    ("grapple_f31_carry_overhead", () => Move(s => s.grapple, 31, close: true)),
                    ("grapple_f42_slam", () => Move(s => s.grapple, 42, close: true)),
                    ("grapple_f50_after_release", () => Move(s => s.grapple, 50, close: true)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("quake_f5_raised", () => Move(s => s.quake, 5)),
                    ("quake_f11_smash", () => Move(s => s.quake, 11)),
                    ("ironGuard_f14", () => Move(s => s.ironGuard, 14)),
                    ("shadowPalm_f12", () => Move(s => s.shadowPalm, 12, meter: 60f)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("airSlam_f5_clasped", () => Move(s => s.airSlam, 5, air: true)),
                    ("airSlam_f10_driven_down", () => Move(s => s.airSlam, 11, air: true)),
                    ("jab_f8", () => Shared("jab", 8)),
                    ("crossFist_f10", () => Shared("crossFist", 10)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("uppercut_f12", () => Shared("uppercut", 12)),
                    ("lunge_f10", () => Shared("lunge", 10)),
                    ("kick1_f11", () => Shared("kick1", 11)),
                    ("sweep_f12", () => Shared("sweep", 12)),
                },
            };

            var sheet = new Texture2D(W * Cols, H * rows.Length, TextureFormat.RGBA32, false);
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r].Length; c++)
                {
                    var (name, make) = rows[r][c];
                    var px = make();
                    SavePng(Path.Combine(tiles, name + ".png"), px, W, H);
                    sheet.SetPixels32(c * W, (rows.Length - 1 - r) * H, W, H, px);
                }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Root, "san80_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        /// <summary>Kage alone (the other fighter far off) for the poses, or with a foe in grip range for the grapple.</summary>
        static HeadlessFight Duel(float meter = 0f, bool foeClose = false)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, foeClose ? 0.37f : 5.5f, meter, kageDef: SpearFamilyFightTests.KageWith("fists"));
            f.RoninInput.Add(1200);
            f.Run(2);
            return f;
        }

        static Color32[] Idle(int frames, int dy = 0)
        {
            using var f = Duel();
            f.KageInput.Add(frames, dy: dy);
            f.Run(frames);
            return Frame(f);
        }

        static Color32[] Move(Pick pick, int frame, bool air = false, float meter = 0f, bool close = false)
        {
            using var f = Duel(meter, foeClose: close);
            if (air) { f.KageInput.Add(10, fwd: 1, dy: 1); f.Run(10); }
            var m = pick((FistsMoves)f.Kage.moves);
            f.Kage.StartMove(m);
            f.RunUntil(() => f.Kage.state == FState.Attack && f.Kage.moveFrame >= frame, 100);
            return Frame(f);
        }

        static Color32[] Shared(string id, int frame)
        {
            using var f = Duel();
            var m = System.Linq.Enumerable.First(f.Kage.moves.All, x => x.id == id);
            f.Kage.StartMove(m);
            f.RunUntil(() => f.Kage.state == FState.Attack && f.Kage.moveFrame >= frame, 100);
            return Frame(f);
        }

        static Color32[] Frame(HeadlessFight f)
        {
            bool close = Mathf.Abs(f.Ronin.Pos.x - f.Kage.Pos.x) < 2.5f;
            return close ? Render(new Vector2((f.Kage.Pos.x + f.Ronin.Pos.x) * 0.5f, 1.3f), 1.7f) : Render(new Vector2(f.Kage.Pos.x + 0.55f, Mathf.Max(1.1f, f.Kage.rig.Bone(B.Hips).position.y + 0.25f)), 1.25f);
        }

        static Color32[] Render(Vector2 centre, float half)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = half;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.13f, 0.14f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(centre.x, centre.y, -10f);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }

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
