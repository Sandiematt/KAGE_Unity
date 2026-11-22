using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The SAN-118 review tiles: every katana sword move at its wind-up, impact, follow-through and recovery keys, the stances,
    /// guards, Kasumi, the run, and frame strips of three signature cuts. Kage is posed straight from the move set (the move
    /// evaluated from chūdan, or from Mirror Guard's Kasumi for the counter), so root motion is left out and every tile is framed
    /// alike. Writes Screenshots/review/SAN-118/tiles/&lt;phase&gt;/, the phase from the KAGE_REVIEW_PHASE environment variable
    /// ("before" on the old keys, "after" on the new; default "after"); a script outside Unity sets the two phases side by side.
    /// Needs a GPU: skips itself under -nographics. Run with <c>-testFilter SwordReviewScreenshots</c> without -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class SwordReviewScreenshots
    {
        const int T = 320;

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-118", "tiles"));

        /// <summary>Move id, then the frames shown: wind-up, impact, follow-through, recovery (zanshin).</summary>
        static readonly (string id, int[] frames)[] Keys =
        {
            ("iai", new[] { 4, 9, 14, 21 }),
            ("risingMoon", new[] { 3, 7, 13, 20 }),
            ("cleave", new[] { 14, 20, 26, 36 }),
            ("shadowStep", new[] { 3, 8, 14, 26 }),
            ("lowReap", new[] { 6, 10, 14, 21 }),
            ("crescent", new[] { 9, 13, 18, 25 }),
            ("counterSlash", new[] { 1, 3, 10, 18 }),
            ("cross", new[] { 5, 9, 13, 20 }),
            ("airSlash", new[] { 3, 8, 13, 24 }),
        };

        static readonly string[] Strips = { "iai", "cleave", "crescent" };

        [Test]
        public void RenderTheSwordReviewTiles()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string phase = Environment.GetEnvironmentVariable("KAGE_REVIEW_PHASE");
            if (string.IsNullOrEmpty(phase)) phase = "after";
            string dir = Path.Combine(Root, phase);
            Directory.CreateDirectory(dir);

            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f);
            f.Run(2);
            var k = f.Kage;
            var set = (KatanaMoves)new MoveSets().Get(WeaponFamilies.Katana);
            var byId = set.All.Where(m => m != null).Distinct().ToDictionary(m => m.id);

            Pose StartOf(MoveDef m)
            {
                var start = PoseLib.Stance();
                if (m != set.counterSlash) return start;
                var held = new Pose();
                set.counterStance.Evaluate(k.def, 15f, start, held);
                return held.Clone();
            }

            void Shot(string name, Pose p, float lookAhead = 0.35f)
            {
                // settle the cloth and hair on the pose before the shot
                for (int i = 0; i < 12; i++) { k.rig.Apply(p); k.rig.Secondary(1f / 60f, Vector2.zero); }
                SavePng(Path.Combine(dir, name + ".png"), Render(new Vector2(k.Pos.x + lookAhead, 0.98f)));
            }

            Pose At(MoveDef m, float frame)
            {
                var p = new Pose();
                m.Evaluate(k.def, frame, StartOf(m), p);
                return p;
            }

            // stances, guards, Kasumi, the run and the walk
            Shot("stance_chudan", PoseLib.Stance());
            Shot("stance_gedan_crouch", PoseLib.Crouch());
            Shot("guard_high", PoseLib.Block());
            Shot("guard_low", PoseLib.CrouchBlock());
            Shot("mirrorGuard_kasumi_f15", At(set.counterStance, 15f));
            var gait = new Pose();
            for (int i = 0; i < 4; i++) Shot($"run_phase{i}", PoseLib.RunCycle(i / 4f, gait).Clone(), 0.2f);
            Shot("walk_phase0", PoseLib.WalkCycle(0f, 1f, gait).Clone(), 0.2f);

            foreach (var (id, frames) in Keys)
                foreach (int fr in frames)
                    Shot($"{id}_f{fr:00}", At(byId[id], fr));

            foreach (var id in Strips)
            {
                var m = byId[id];
                for (int fr = 0; fr <= m.total; fr += 2) Shot($"strip_{id}_f{fr:00}", At(m, fr));
            }
        }

        // ---------------------------------------------------------------------------------------
        // SAN-118 cleave rework: Kage Cleave on the ground (no leap of its own) and from a jump the player makes (Down + S in the
        // air). Played live in a fight, so the jump and the root motion show; a fixed camera and a floor line. A tile's border is
        // red while the cut's hit window is live. Writes Screenshots/review/SAN-118/cleave/: the tiles and one strip per version.

        const float CleaveView = 1.85f;
        const int StripCols = 12;

        static string CleaveDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-118", "cleave"));

        [Test]
        public void RenderTheCleaveStrips()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(CleaveDir);

            // on the ground: the chain's third hit (Iai, Rising Moon, Cleave) started straight from chūdan
            var ground = new List<Color32[]>();
            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f))
            {
                f.Run(2);
                Vector2 centre = new(f.Kage.Pos.x + 0.45f, 1.6f);
                var cleave = ((KatanaMoves)f.Kage.moves).cleave;
                f.Kage.StartMove(cleave);
                for (int tick = 0; tick <= cleave.total + 2; tick++)     // capped by the move's length
                {
                    if (tick % 2 == 0) ground.Add(CleaveTile(f, centre, $"ground_t{tick:00}"));
                    f.Run(1);
                }
            }
            SaveStrip(Path.Combine(CleaveDir, "san118_cleave_ground_strip.png"), ground);

            // from a jump: hold up to jump, then Down + S on the way up; the cut falls past the apex and Kage lands
            var air = new List<Color32[]>();
            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f))
            {
                f.Run(2);
                Vector2 centre = new(f.Kage.Pos.x + 0.45f, 1.6f);
                f.KageInput.Add(12, dy: 1).Add(1, dy: -1, buttons: "S");
                bool started = false;
                int after = 0;
                for (int tick = 0; tick < 120; tick++)                    // capped: a jump and a landing take well under 120 frames
                {
                    if (f.Kage.move != null && f.Kage.move.id == "airCleave") started = true;
                    if (started && f.Kage.Grounded && f.Kage.state != FState.Attack) after++;
                    if (after > 12) break;
                    if (tick % 2 == 0) air.Add(CleaveTile(f, centre, $"air_t{tick:00}"));
                    f.Run(1);
                }
                Assert.IsTrue(started, "Down + S in the air starts the air Kage Cleave");
            }
            SaveStrip(Path.Combine(CleaveDir, "san118_cleave_air_strip.png"), air);
        }

        static Color32[] CleaveTile(HeadlessFight f, Vector2 centre, string name)
        {
            var k = f.Kage;
            var px = Render(centre, CleaveView);
            // the floor: world y 0
            int floor = Mathf.RoundToInt(T * 0.5f + (0f - centre.y) / CleaveView * T * 0.5f);
            if (floor >= 0 && floor < T)
                for (int x = 0; x < T; x++) px[floor * T + x] = new Color32(150, 140, 120, 255);
            bool live = k.state == FState.Attack && k.move != null && k.move.hits.Any(h => k.moveFrame >= h.from && k.moveFrame <= h.to);
            if (live)
            {
                var red = new Color32(220, 60, 60, 255);
                for (int i = 0; i < T; i++)
                    for (int b = 0; b < 4; b++)
                    {
                        px[b * T + i] = red; px[(T - 1 - b) * T + i] = red; px[i * T + b] = red; px[i * T + T - 1 - b] = red;
                    }
            }
            string label = k.state == FState.Attack && k.move != null ? $"{k.move.id}_f{k.moveFrame:00}" : k.state.ToString();
            SavePng(Path.Combine(CleaveDir, $"{name}_{label}.png"), px);
            return px;
        }

        static void SaveStrip(string path, List<Color32[]> tiles)
        {
            int cols = Mathf.Min(StripCols, tiles.Count), rows = (tiles.Count + StripCols - 1) / StripCols;
            var sheet = new Texture2D(cols * T, rows * T, TextureFormat.RGBA32, false);
            var bg = new Color32[cols * T * rows * T];
            for (int i = 0; i < bg.Length; i++) bg[i] = new Color32(30, 26, 28, 255);
            sheet.SetPixels32(bg);
            for (int i = 0; i < tiles.Count; i++)
                sheet.SetPixels32(i % StripCols * T, (rows - 1 - i / StripCols) * T, T, T, tiles[i]);
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        static Color32[] Render(Vector2 centre, float view = 1.15f)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = view;
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

        static void SavePng(string path, Color32[] px)
        {
            var tex = new Texture2D(T, T, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
