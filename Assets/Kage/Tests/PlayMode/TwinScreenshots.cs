using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The SAN-77 review sheet: Kage with Tsubame (twin blades) in the idle, walk and crouch stances and at the
    /// strike frame of every move, with the katana idle for scale. Writes Screenshots/review/SAN-77/twin_sheet.png and
    /// every tile to .../twin_tiles/. Needs a GPU: skips itself under -nographics
    /// (<c>-testFilter TwinScreenshots</c>, no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class TwinScreenshots
    {
        const int T = 360;
        const int Cols = 6;

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-77"));

        static CharacterDef Kage(string weapon)
        {
            var d = CharacterCatalog.Get(KageCharacter.Id);
            d.weaponId = weapon;
            return d;
        }

        [Test]
        public void RenderTheTwinSheet()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Root, "twin_tiles");
            Directory.CreateDirectory(tiles);

            var rows = new[]
            {
                new (string, Func<Color32[]>)[]
                {
                    ("idle", () => Stance(s => s.Add(40), 40, "tsubame")),
                    ("walk", () => Stance(s => s.Add(18, fwd: 1), 18, "tsubame")),
                    ("crouch", () => Stance(s => s.Add(40, dy: -1), 40, "tsubame")),
                    ("katana_idle_for_scale", () => Stance(s => s.Add(40), 40, "kagetsu")),
                    ("hurt_idle_back", () => Stance(s => s.Add(20, fwd: -1), 20, "tsubame")),
                    ("shadow_daggers_idle", () => Stance(s => s.Add(40), 40, "shadow_daggers")),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("fang1_f6", () => Move("twin.fang1", 6)), ("fang2_f6", () => Move("twin.fang2", 6)),
                    ("fang3_f7", () => Move("twin.fang3", 7)), ("fang4_f8", () => Move("twin.fang4", 8)),
                    ("crossSlash_f8", () => Move("twin.crossSlash", 8)), ("dashThrough_f7", () => Move("twin.dashThrough", 7)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("lowSlice_f7", () => Move("twin.lowSlice", 7)), ("jab_f5", () => Move("twin.jab", 5)),
                    ("hook_f6", () => Move("twin.hook", 6)), ("lunge_f6", () => Move("twin.lunge", 6)),
                    ("groundStab_f9", () => Move("twin.groundStab", 9)), ("kick1_f7", () => Move("twin.kick1", 7)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("kick2_f7", () => Move("twin.kick2", 7)), ("pushKick_f7", () => Move("twin.pushKick", 7)),
                    ("backKick_f8", () => Move("twin.backKick", 8)), ("sweep_f8", () => Move("twin.sweep", 8)),
                    ("airJab_f6", () => Move("twin.airJab", 6, true)), ("airKick_f10", () => Move("twin.airKick", 10, true)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("airSpin_f6", () => Move("twin.airSpin", 6, true)), ("airSpin_f12", () => Move("twin.airSpin", 12, true)),
                    ("dive_f6", () => Move("twin.dive", 6, true)), ("dive_f14", () => Move("twin.dive", 14, true)),
                    ("fang1_f3_windup", () => Move("twin.fang1", 3)), ("fang4_f4_windup", () => Move("twin.fang4", 4)),
                },
                new (string, Func<Color32[]>)[]
                {
                    ("mist_fans_idle", () => Stance(s => s.Add(40), 40, "mist_fans")),
                    ("mist_fans_fang1_f6", () => Move("twin.fang1", 6, false, "mist_fans")),
                    ("shadow_daggers_fang2_f6", () => Move("twin.fang2", 6, false, "shadow_daggers")),
                    ("fang2_f3_windup", () => Move("twin.fang2", 3)),
                    ("crossSlash_f5_windup", () => Move("twin.crossSlash", 5)),
                    ("dashThrough_f4_windup", () => Move("twin.dashThrough", 4)),
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
            File.WriteAllBytes(Path.Combine(Root, "twin_sheet.png"), sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        static Color32[] Stance(Action<ScriptedInput> script, int frames, string weapon)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: Kage(weapon));
            f.Run(2);
            script(f.KageInput);
            f.Run(frames);
            return Render(new Vector2(f.Kage.Pos.x + 0.15f, 0.95f));
        }

        static Color32[] Move(string id, int frame, bool air = false, string weapon = "tsubame")
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageDef: Kage(weapon));
            f.Run(2);
            if (air) { f.KageInput.Add(10, dy: 1); f.Run(10); }
            f.Kage.StartMove(System.Linq.Enumerable.Single(f.Kage.moves.All, m => m.id == id));
            f.RunUntil(() => f.Kage.state == FState.Attack && f.Kage.move != null && f.Kage.move.id == id && f.Kage.moveFrame >= frame, 80);
            return Render(new Vector2(f.Kage.Pos.x + 0.15f, Mathf.Max(0.95f, f.Kage.rig.Bone(B.Hips).position.y + 0.25f)));
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
