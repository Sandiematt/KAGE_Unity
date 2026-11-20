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
