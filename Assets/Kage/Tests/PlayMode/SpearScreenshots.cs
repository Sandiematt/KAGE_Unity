using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The SAN-78 review sheet: Mizuchi's two-handed polearm grip in the stances and the key frames of the spear set,
    /// next to Kage's katana for scale, plus the foes' polearm looks. Writes Screenshots/review/SAN-78/san78_sheet.png,
    /// every tile to .../san78_tiles/ and the grip close-up. Needs a GPU: skips itself under -nographics. Run with
    /// <c>-testFilter SpearScreenshots</c> without -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class SpearScreenshots
    {
        const int W = 560, H = 420, Cols = 4;
        const float Half = 1.2f;                  // orthographic half-height (metres)

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-78"));

        delegate MoveDef Pick(SpearMoves s);

        static CharacterDef KageWith(string weapon) => SpearFamilyFightTests.KageWith(weapon);

        [Test]
        public void RenderTheSpearSheet()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Root, "san78_tiles");
            Directory.CreateDirectory(tiles);

            var rows = new[]
            {
                // stances: the grip at rest, in a walk, crouched and in the low guard, with the katana for scale
                new (string, Func<Color32[]>)[]
                {
                    ("spear_idle", () => Idle("mizuchi", 40)),
                    ("spear_walk", () => Idle("mizuchi", 18, fwd: 1)),
                    ("spear_crouch", () => Idle("mizuchi", 40, dy: -1)),
                    ("spear_low_guard", () => Idle("mizuchi", 40, dy: -1, buttons: "G")),
                },
                // the weapon button
                new (string, Func<Color32[]>)[]
                {
                    ("thrust_f7_windup", () => Move(s => s.thrust, 7)),
                    ("thrust_f12_extended", () => Move(s => s.thrust, 12)),
                    ("lunge_f12", () => Move(s => s.lunge, 12)),
                    ("sweep_f12", () => Move(s => s.sweep, 12)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("guardRaise_f14", () => Move(s => s.guardRaise, 14)),
                    ("vault_f6_crouch", () => Move(s => s.vault, 5)),
                    ("vault_f13_rising", () => Move(s => s.vault, 13)),
                    ("skewer_f14", () => Move(s => s.skewer, 14)),
                },
                // the punch and kick buttons
                new (string, Func<Color32[]>)[]
                {
                    ("jab_f8", () => Move(s => s.jab, 8)),
                    ("poke2_f9", () => Move(s => s.poke2, 9)),
                    ("stepPoke_f10", () => Move(s => s.stepPoke, 10)),
                    ("lowPoke_f8", () => Move(s => s.lowPoke, 8)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("kick_f11", () => Move(s => s.kick, 11)),
                    ("pushKick_f11", () => Move(s => s.pushKick, 11)),
                    ("lowKick_f9", () => Move(s => s.lowKick, 9)),
                    ("katana_idle_for_scale", () => Idle("kagetsu", 40)),
                },
                // the air
                new (string, Func<Color32[]>)[]
                {
                    ("airPoke_f9", () => Move(s => s.airPoke, 9, air: true)),
                    ("airDive_f12", () => Move(s => s.airDive, 12, air: true)),
                    ("airSweep_f8", () => Move(s => s.airSweep, 8, air: true)),
                    ("airSweep_f14", () => Move(s => s.airSweep, 14, air: true)),
                },
                // the foes' polearms, and the grip up close
                new (string, Func<Color32[]>)[]
                {
                    ("look_sentinel_spear", () => Idle("sentinel_spear", 40)),
                    ("look_vanguard_yari", () => Idle("vanguard_yari", 40)),
                    ("look_temple_staff", () => Idle("temple_staff", 40)),
                    ("grip_closeup", () => Idle("mizuchi", 40, closeUp: true)),
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
            File.WriteAllBytes(Path.Combine(Root, "san78_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        static Color32[] Idle(string weapon, int frames, int fwd = 0, int dy = 0, bool closeUp = false, string buttons = "")
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: KageWith(weapon));
            f.Run(2);
            f.KageInput.Add(frames, fwd: fwd, dy: dy, buttons: buttons);
            f.Run(frames);
            return closeUp ? Render((Vector2)f.Kage.rig.World(B.HandF, Vector2.zero) + new Vector2(0.15f, 0.1f), 0.5f) : Frame(f);
        }

        static Color32[] Move(Pick pick, int frame, bool air = false)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: KageWith("mizuchi"));
            f.Run(2);
            if (air) { f.KageInput.Add(10, fwd: 1, dy: 1); f.Run(10); }
            var m = pick((SpearMoves)f.Kage.moves);
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
