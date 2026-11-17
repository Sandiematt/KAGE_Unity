using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// Renders Kage and the Ronin from a headless fight (idle, walking, mid-attack) to
    /// Screenshots/review/SAN-66/&lt;label&gt;, label from KAGE_SHOT_LABEL (default "after"). When a "before" set
    /// exists, every "after" image must match it pixel for pixel: building fighters from character data
    /// must not change how they look (SAN-66). The very first run after a fresh Library import can differ by a
    /// pixel (kage_walk), so render the before set in a warm session. Needs a GPU: it skips itself under -nographics. Run:
    /// <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter CharacterScreenshots</c> (no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class CharacterScreenshots
    {
        const int W = 800, H = 600;

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-66"));

        static string Label
        {
            get
            {
                var l = System.Environment.GetEnvironmentVariable("KAGE_SHOT_LABEL");
                return string.IsNullOrEmpty(l) ? "after" : l;
            }
        }

        [Test]
        public void KageAndTheRoninLookAsBefore()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string outDir = Path.Combine(Root, Label);
            Directory.CreateDirectory(outDir);

            var shots = new List<string>();
            // (name, frames to run, Kage's script, foe)
            Capture(shots, outDir, "kage_idle", 30, s => s.Add(30), Foe.None, 0f);
            Capture(shots, outDir, "kage_walk", 24, s => s.Add(24, fwd: 1), Foe.None, -0.6f);
            Capture(shots, outDir, "kage_jab", 7, s => s.Add(1, buttons: "P"), Foe.None, 0f);
            Capture(shots, outDir, "kage_kick", 9, s => s.Add(1, buttons: "K"), Foe.None, 0f);
            Capture(shots, outDir, "kage_iai", 14, s => s.Add(1, buttons: "S"), Foe.None, 0f);
            Capture(shots, outDir, "kage_vs_ronin", 40, s => s.Add(40), Foe.Scripted, -0.9f);
            if (Label != "before")
            {
                // not compared: a second fighter from another data entry (a recoloured Kage), new in SAN-66
                var variant = CharacterCatalog.Get(KageCharacter.Id);
                variant.id = "test_variant";
                variant.aiProfile = "regular";
                variant.colors = new FighterColors(new Color(0.25f, 0.8f, 1f), Color.white, new Color(0.08f, 0.3f, 0.55f));
                Capture(new List<string>(), outDir, "data_variant_vs_kage", 40, s => s.Add(40), Foe.Scripted, -0.9f, variant);
            }

            string before = Path.Combine(Root, "before");
            if (Label == "before" || !Directory.Exists(before)) return;
            var differ = new List<string>();
            foreach (var name in shots)
            {
                string a = Path.Combine(before, name + ".png"), b = Path.Combine(outDir, name + ".png");
                if (!File.Exists(a)) { differ.Add(name + " (no before image)"); continue; }
                int d = DifferentPixels(File.ReadAllBytes(a), File.ReadAllBytes(b));
                if (d != 0) differ.Add($"{name} ({d} pixels)");
            }
            CollectionAssert.IsEmpty(differ, "images that changed since the before set");
        }

        static void Capture(List<string> shots, string dir, string name, int frames, System.Action<ScriptedInput> kage, Foe foe, float kageX, CharacterDef foeDef = null)
        {
            using (var f = new HeadlessFight(KageMoveScenarios.Seed, foe, kageX, 0.9f, foeDef: foeDef))
            {
                kage(f.KageInput);
                f.RoninInput?.Add(600);
                f.Run(frames);
                var px = Render(f.Kage.Pos + new Vector2(foe == Foe.None ? 0f : 0.9f, 0.95f));
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            shots.Add(name);
        }

        static Color32[] Render(Vector2 centre)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1.3f;
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
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            return px;
        }

        static int DifferentPixels(byte[] pngA, byte[] pngB)
        {
            var a = new Texture2D(2, 2);
            var b = new Texture2D(2, 2);
            a.LoadImage(pngA);
            b.LoadImage(pngB);
            if (a.width != b.width || a.height != b.height) return int.MaxValue;
            var pa = a.GetPixels32();
            var pb = b.GetPixels32();
            int n = 0;
            for (int i = 0; i < pa.Length; i++)
                if (pa[i].r != pb[i].r || pa[i].g != pb[i].g || pa[i].b != pb[i].b || pa[i].a != pb[i].a) n++;
            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            return n;
        }
    }
}
