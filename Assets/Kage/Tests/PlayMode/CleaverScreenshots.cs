using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The SAN-79 review sheet: Onikiri's heavy two-handed grip in the stances and the key frames of the cleaver set,
    /// next to Kage's katana for scale, plus the foes' cleaver looks. Writes Screenshots/review/SAN-79/san79_sheet.png and
    /// every tile to .../san79_tiles/. Needs a GPU: skips itself under -nographics. Run with
    /// <c>-testFilter CleaverScreenshots</c> without -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class CleaverScreenshots
    {
        const int W = 560, H = 420, Cols = 4;
        const float Half = 1.2f;                  // orthographic half-height (metres)

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-79"));

        delegate MoveDef Pick(CleaverMoves s);

        static CharacterDef KageWith(string weapon) => SpearFamilyFightTests.KageWith(weapon);

        [Test]
        public void RenderTheCleaverSheet()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Root, "san79_tiles");
            Directory.CreateDirectory(tiles);

            var rows = new[]
            {
                new (string, Func<Color32[]>)[]
                {
                    ("cleaver_idle", () => Idle("onikiri", 40)),
                    ("cleaver_walk", () => Idle("onikiri", 18, fwd: 1)),
                    ("cleaver_crouch", () => Idle("onikiri", 40, dy: -1)),
                    ("cleaver_low_guard", () => Idle("onikiri", 40, dy: -1, buttons: "G")),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("overhead_f22_raised", () => Move(s => s.overhead, 22)),
                    ("overhead_f29_falling", () => Move(s => s.overhead, 29)),
                    ("overhead_f34_landed", () => Move(s => s.overhead, 34)),
                    ("katana_idle_for_scale", () => Idle("kagetsu", 40)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("sweep_f18_wound", () => Move(s => s.sweep, 18)),
                    ("sweep_f25_mid", () => Move(s => s.sweep, 25)),
                    ("sweep_f31_front", () => Move(s => s.sweep, 31)),
                    ("charge_f18", () => Move(s => s.charge, 18)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("slam_f12_crouch", () => Move(s => s.slam, 12)),
                    ("slam_f24_apex", () => Move(s => s.slam, 24)),
                    ("slam_f35_smash", () => Move(s => s.slam, 35)),
                    ("hew_f18", () => Move(s => s.hew, 18)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("hack_f8_raised", () => Move(s => s.hack, 8)),
                    ("hack_f14_strike", () => Move(s => s.hack, 14)),
                    ("hack2_f15", () => Move(s => s.hack2, 15)),
                    ("stepHack_f13", () => Move(s => s.stepHack, 13)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("lowHack_f12", () => Move(s => s.lowHack, 12)),
                    ("pommel_f10_low", () => Move(s => s.pommel, 10)),
                    ("pommel_f18_rising", () => Move(s => s.pommel, 18)),
                    ("kick_f13", () => Move(s => s.kick, 13)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("pushKick_f13", () => Move(s => s.pushKick, 13)),
                    ("lowKick_f14", () => Move(s => s.lowKick, 14)),
                    ("airChop_f6_raised", () => Move(s => s.airChop, 6, air: true)),
                    ("airChop_f12", () => Move(s => s.airChop, 12, air: true)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("airKick_f10", () => Move(s => s.airKick, 10, air: true)),
                    ("airPlunge_f8", () => Move(s => s.airPlunge, 8, air: true)),
                    ("airPlunge_f16", () => Move(s => s.airPlunge, 16, air: true)),
                    ("look_raider_cleaver", () => Idle("raider_cleaver", 40)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("grip_closeup", () => Idle("onikiri", 40, closeUp: true)),
                    ("blade_closeup", () => Idle("onikiri", 40, bladeUp: true)),
                    ("look_tyrant_cleaver", () => Idle("tyrant_cleaver", 40)),
                    ("look_iwakudaki", () => Idle("iwakudaki", 40)),
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
            File.WriteAllBytes(Path.Combine(Root, "san79_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        static Color32[] Idle(string weapon, int frames, int fwd = 0, int dy = 0, bool closeUp = false, bool bladeUp = false, string buttons = "")
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: KageWith(weapon));
            f.Run(2);
            f.KageInput.Add(frames, fwd: fwd, dy: dy, buttons: buttons);
            f.Run(frames);
            if (bladeUp) return Render((Vector2)f.Kage.rig.World(B.Sword, f.Kage.BladeTipLocal * 0.5f), 0.55f);
            return closeUp ? Render((Vector2)f.Kage.rig.World(B.HandF, Vector2.zero) + new Vector2(0.15f, 0.1f), 0.5f) : Frame(f);
        }

        static Color32[] Move(Pick pick, int frame, bool air = false)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: KageWith("onikiri"));
            f.Run(2);
            if (air) { f.KageInput.Add(10, fwd: 1, dy: 1); f.Run(10); }
            var m = pick((CleaverMoves)f.Kage.moves);
            f.Kage.StartMove(m);
            f.RunUntil(() => f.Kage.state == FState.Attack && f.Kage.move == m && f.Kage.moveFrame >= frame, 80);
            return Frame(f);
        }

        static Color32[] Frame(HeadlessFight f) =>
            Render(new Vector2(f.Kage.Pos.x + 0.85f, Mathf.Max(1.05f, f.Kage.rig.Bone(B.Hips).position.y + 0.3f)), Half);

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
