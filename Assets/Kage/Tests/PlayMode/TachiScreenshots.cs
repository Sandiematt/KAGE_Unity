using System;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The SAN-81 review sheet: Raikō no Tachi's heavy two-handed grip in the stances and the key frames of the tachi set,
    /// next to Kage's katana for scale, plus Raikon's copy. Writes Screenshots/review/SAN-81/san81_sheet.png,
    /// every tile to .../san81_tiles/ and the grip close-up. Needs a GPU: skips itself under -nographics. Run with
    /// <c>-testFilter TachiScreenshots</c> without -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class TachiScreenshots
    {
        const int W = 560, H = 420, Cols = 4;
        const float Half = 1.6f;                  // orthographic half-height (metres)

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-81"));

        const string Tachi = "raiko_no_tachi";

        delegate MoveDef Pick(TachiMoves s);

        static CharacterDef KageWith(string weapon) => SpearFamilyFightTests.KageWith(weapon);

        [Test]
        public void RenderTheTachiSheet()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Root, "san81_tiles");
            Directory.CreateDirectory(tiles);

            var rows = new[]
            {
                new (string, Func<Color32[]>)[]
                {
                    ("tachi_idle", () => Idle(Tachi, 40)),
                    ("tachi_walk", () => Idle(Tachi, 18, fwd: 1)),
                    ("tachi_crouch", () => Idle(Tachi, 40, dy: -1)),
                    ("tachi_low_guard", () => Idle(Tachi, 40, dy: -1, buttons: "G")),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("cut1_f11_windup", () => Move(s => s.cut1, 11)),
                    ("cut1_f13_sweeping", () => Move(s => s.cut1, 13)),
                    ("cut1_f16_through", () => Move(s => s.cut1, 16)),
                    ("katana_idle_for_scale", () => Idle("kagetsu", 40)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("cut2_f11_raised", () => Move(s => s.cut2, 11)),
                    ("cut2_f14_falling", () => Move(s => s.cut2, 14)),
                    ("cut2_f18_through", () => Move(s => s.cut2, 18)),
                    ("guard_f16", () => Move(s => s.guard, 16)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("overhead_f14_lifted", () => Move(s => s.overhead, 14)),
                    ("overhead_f19_falling", () => Move(s => s.overhead, 19)),
                    ("overhead_f24_landed", () => Move(s => s.overhead, 24)),
                    ("overhead_f34_hold", () => Move(s => s.overhead, 34)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("thrust_f10_drawn", () => Move(s => s.thrust, 10)),
                    ("thrust_f17_extended", () => Move(s => s.thrust, 17)),
                    ("sweep_f12_wound", () => Move(s => s.sweep, 12)),
                    ("sweep_f19_low", () => Move(s => s.sweep, 19)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("pommel_f11", () => Move(s => s.pommel, 11)),
                    ("hiltRise_f12", () => Move(s => s.hiltRise, 12)),
                    ("stepPommel_f11", () => Move(s => s.stepPommel, 11)),
                    ("lowPommel_f9", () => Move(s => s.lowPommel, 9)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("kick_f12", () => Move(s => s.kick, 12)),
                    ("pushKick_f12", () => Move(s => s.pushKick, 12)),
                    ("lowKick_f10", () => Move(s => s.lowKick, 10)),
                    ("heelDrop_f12", () => Move(s => s.heelDrop, 12)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("airHilt_f10", () => Move(s => s.airHilt, 10, air: true)),
                    ("airKick_f12", () => Move(s => s.airKick, 12, air: true)),
                    ("airCleave_f8_raised", () => Move(s => s.airCleave, 8, air: true)),
                    ("airCleave_f14_dive", () => Move(s => s.airCleave, 14, air: true)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("look_raikon_tachi", () => Idle("raikon_tachi", 40)),
                    ("groundStab_f17", () => Move(s => s.groundStab, 17)),
                    ("grip_closeup", () => Idle(Tachi, 40, closeUp: true)),
                    ("katana_cut_for_scale", () => KatanaIai(10)),
                },
            };

            rows = rows.Append(new (string, Func<Color32[]>)[]
            {
                ("raikon_idle_with_tachi_set", () => RaikonMove(null, 40)),
                ("raikon_cut1_f13", () => RaikonMove(s => s.cut1, 13)),
                ("raikon_overhead_f20", () => RaikonMove(s => s.overhead, 20)),
                ("raikon_thrust_f17", () => RaikonMove(s => s.thrust, 17)),
            }).ToArray();

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
            File.WriteAllBytes(Path.Combine(Root, "san81_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        /// <summary>Raikon (a Ronin slot, facing left) in his idle or in a tachi move; the image is mirrored back so he faces right like the others.</summary>
        static Color32[] RaikonMove(Pick pick, int frame)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3.5f, 0.6f, 0f, foeDef: CharacterCatalog.Get("raikon"));
            f.RoninInput.Add(600);
            f.Run(2);
            var r = f.Ronin;
            if (pick == null) f.Run(frame);
            else
            {
                var m = pick((TachiMoves)r.moves);
                r.StartMove(m);
                f.RunUntil(() => r.state == FState.Attack && r.move == m && r.moveFrame >= frame, 80);
            }
            var px = Render(new Vector2(r.Pos.x - 0.35f, Mathf.Max(1.25f, r.rig.Bone(B.Hips).position.y + 0.4f)), Half);
            for (int y = 0; y < H; y++) System.Array.Reverse(px, y * W, W);
            return px;
        }

        static Color32[] KatanaIai(int frame)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: KageWith("kagetsu"));
            f.Run(2);
            var m = ((KatanaMoves)f.Kage.moves).iai;
            f.Kage.StartMove(m);
            f.RunUntil(() => f.Kage.state == FState.Attack && f.Kage.move == m && f.Kage.moveFrame >= frame, 80);
            return Frame(f);
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
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: KageWith(Tachi));
            f.Run(2);
            if (air) { f.KageInput.Add(10, fwd: 1, dy: 1); f.Run(10); }
            var m = pick((TachiMoves)f.Kage.moves);
            f.Kage.StartMove(m);
            f.RunUntil(() => f.Kage.state == FState.Attack && f.Kage.move == m && f.Kage.moveFrame >= frame, 80);
            return Frame(f);
        }

        static Color32[] Frame(HeadlessFight f) =>
            Render(new Vector2(f.Kage.Pos.x + 0.35f, Mathf.Max(1.25f, f.Kage.rig.Bone(B.Hips).position.y + 0.4f)), Half);

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
