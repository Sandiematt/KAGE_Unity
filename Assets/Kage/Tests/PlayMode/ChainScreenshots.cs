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
    /// The SAN-87 review sheet: Kage with Yamigama (the chain sickle) in the stances and at the strike frame of every move,
    /// the chain extended mid-lash, and the pull dragging a Ronin in, frame by frame. Writes
    /// Screenshots/review/SAN-87/chain_sheet.png, chain_lash.png, chain_pull.png and every tile to .../chain_tiles/.
    /// Needs a GPU: skips itself under -nographics (<c>-testFilter ChainScreenshots</c>, no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class ChainScreenshots
    {
        const int T = 360;
        const int Cols = 6;

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-87"));

        static CharacterDef Kage(string weapon) => SpearFamilyFightTests.KageWith(weapon);

        [Test]
        public void RenderTheChainSheet()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Root, "chain_tiles");
            Directory.CreateDirectory(tiles);

            var rows = new[]
            {
                new (string, Func<Color32[]>)[]
                {
                    ("idle", () => Stance(s => s.Add(40), 40, "yamigama")),
                    ("walk", () => Stance(s => s.Add(18, fwd: 1), 18, "yamigama")),
                    ("crouch", () => Stance(s => s.Add(40, dy: -1), 40, "yamigama")),
                    ("katana_idle_for_scale", () => Stance(s => s.Add(40), 40, "kagetsu")),
                    ("chain_hook_idle", () => Stance(s => s.Add(40), 40, "chain_hook")),
                    ("guard_back", () => Stance(s => s.Add(20, fwd: -1), 20, "yamigama")),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("reap_f5_windup", () => Move("chain.reap", 5)), ("reap_f9", () => Move("chain.reap", 9)),
                    ("reapUp_f5_windup", () => Move("chain.reapUp", 5)), ("reapUp_f10", () => Move("chain.reapUp", 10)),
                    ("graveHook_f5_windup", () => Move("chain.graveHook", 5)), ("graveHook_f11", () => Move("chain.graveHook", 11)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("crescentLift_f6", () => Move("chain.crescentLift", 6)), ("crescentLift_f10", () => Move("chain.crescentLift", 10)),
                    ("jab_f7", () => Move("chain.jab", 7)), ("hilt_f9", () => Move("chain.hilt", 9)),
                    ("crouchJab_f7", () => Move("chain.crouchJab", 7)), ("groundHook_f10", () => Move("chain.groundHook", 10)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("kick1_f8", () => Move("chain.kick1", 8)), ("kick2_f8", () => Move("chain.kick2", 8)),
                    ("pushKick_f8", () => Move("chain.pushKick", 8)), ("backKick_f9", () => Move("chain.backKick", 9)),
                    ("sweep_f9", () => Move("chain.sweep", 9)), ("airJab_f7", () => Move("chain.airJab", 7, true)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("airKick_f10", () => Move("chain.airKick", 10, true)), ("airReap_f5", () => Move("chain.airReap", 5, true)),
                    ("airReap_f9", () => Move("chain.airReap", 9, true)), ("shadowFall_f5", () => Move("chain.shadowFall", 5, true)),
                    ("shadowFall_f12", () => Move("chain.shadowFall", 12, true)), ("pull_f5_windup", () => Move("chain.pull", 5)),
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
            File.WriteAllBytes(Path.Combine(Root, "chain_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        /// <summary>The lash frame by frame, wide enough for the whole chain (the hand, the chain, the sickle and a foe 1.9 m away).</summary>
        [Test]
        public void RenderTheLash()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Root);
            int[] frames = { 4, 6, 8, 10, 14, 20 };
            const int W = 560, H = 360;
            var sheet = new Texture2D(W * 3, H * 2, TextureFormat.RGBA32, false);
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, 0f, 1.9f, 0f, kageDef: Kage("yamigama"));
            f.RoninInput.Add(1200);
            f.Run(10);
            f.Kage.StartMove(((ChainMoves)f.Kage.moves).lash);
            for (int i = 0; i < frames.Length; i++)
            {
                f.RunUntil(() => f.Kage.state != FState.Attack || f.Kage.moveFrame >= frames[i], 60);
                ChainVisual.LineOf(f.Kage.rig)?.Refresh();
                var px = Render(new Vector2(0.95f, 1.0f), 1.0f, W, H);
                sheet.SetPixels32((i % 3) * W, (1 - i / 3) * H, W, H, px);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Root, "chain_lash.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        /// <summary>The pull: the chain goes out, the sickle hooks the foe, and he slides in through the knockback.</summary>
        [Test]
        public void RenderThePull()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Root);
            const int W = 560, H = 360;
            var sheet = new Texture2D(W * 3, H * 2, TextureFormat.RGBA32, false);
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, 0f, 1.9f, 0f, kageDef: Kage("yamigama"));
            f.RoninInput.Add(1200);
            f.Run(10);
            f.Kage.StartMove(((ChainMoves)f.Kage.moves).pull);
            int hitFrame = -1;
            var shots = new[] { 8, 0, 6, 14, 24, 40 };      // move frames; 0 = the first frame the foe is in hitstun
            for (int i = 0; i < shots.Length; i++)
            {
                if (shots[i] == 0)
                {
                    f.RunUntil(() => f.Ronin.state == FState.Hitstun, 60);
                    hitFrame = f.Ctx.Clock.Frame;
                }
                else if (hitFrame < 0) f.RunUntil(() => f.Kage.state != FState.Attack || f.Kage.moveFrame >= shots[i], 60);
                else { int target = hitFrame + shots[i]; f.RunUntil(() => f.Ctx.Clock.Frame >= target, 80); }
                ChainVisual.LineOf(f.Kage.rig)?.Refresh();
                var px = Render(new Vector2(0.95f, 1.0f), 1.0f, W, H);
                sheet.SetPixels32((i % 3) * W, (1 - i / 3) * H, W, H, px);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Root, "chain_pull.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        static Color32[] Stance(Action<ScriptedInput> script, int frames, string weapon)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: Kage(weapon));
            f.Run(2);
            script(f.KageInput);
            f.Run(frames);
            ChainVisual.LineOf(f.Kage.rig)?.Refresh();
            return Render(new Vector2(f.Kage.Pos.x + 0.15f, 0.95f), 1.15f, T, T);
        }

        static Color32[] Move(string id, int frame, bool air = false, string weapon = "yamigama")
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: Kage(weapon));
            f.Run(2);
            if (air) { f.KageInput.Add(10, dy: 1); f.Run(10); }
            f.Kage.StartMove(f.Kage.moves.All.Single(m => m.id == id));
            f.RunUntil(() => f.Kage.state == FState.Attack && f.Kage.move != null && f.Kage.move.id == id && f.Kage.moveFrame >= frame, 80);
            ChainVisual.LineOf(f.Kage.rig)?.Refresh();
            return Render(new Vector2(f.Kage.Pos.x + 0.35f, Mathf.Max(1.1f, f.Kage.rig.Bone(B.Hips).position.y + 0.4f)), 1.5f, T, T);
        }

        static Color32[] Render(Vector2 centre, float halfHeight, int w, int h)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = halfHeight;
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
