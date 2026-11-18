using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The review sheets for the Hollows (SAN-76): each Hollow beside Kage in idle, walk, attack and hit, close-ups of both heads,
    /// and one sheet with the Order Footman, the Hollow Shade and the tutorial Hollow side by side. Written to
    /// Screenshots/review/SAN-76/. Needs a GPU: it skips itself under -nographics. Run:
    /// <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter HollowScreenshots</c> (no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class HollowScreenshots
    {
        const int W = 800, H = 600;

        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-76"));

        [TestCase("hollow_shade")]
        [TestCase("hollow_tutorial")]
        public void RenderTheReviewSheet(string id)
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            var shots = new List<Color32[]>
            {
                Shot(id, "idle", 30, -0.75f, 0.75f, (k, r) => { k.Add(60); r.Add(60); }),
                Shot(id, "walk", 22, -1.5f, 1.5f, (k, r) => { k.Add(40, fwd: 1); r.Add(40, fwd: 1); }),
                Shot(id, "attack", 15, -1.15f, 1.15f, (k, r) => { k.Add(1, buttons: "S").Add(60); r.Add(1, buttons: "S").Add(60); }),
                Shot(id, "hit", 17, -0.5f, 0.5f, (k, r) => { k.Add(1, buttons: "S").Add(60); r.Add(60); }),
            };
            var sheet = new Texture2D(W * 2, H * 2, TextureFormat.RGBA32, false);
            for (int i = 0; i < shots.Count; i++) sheet.SetPixels32((i % 2) * W, (1 - i / 2) * H, W, H, shots[i]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "kage_vs_" + id + "_sheet.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: CharacterCatalog.Get(id));
            f.KageInput.Add(300);
            f.RoninInput.Add(300);
            f.Run(20);
            Save(id + "_closeup", Render(f.Ronin.Pos + new Vector2(0f, 1.0f), 1.05f));
            Save(id + "_head", Render(f.Ronin.Pos + new Vector2(-0.05f, 1.62f), 0.38f));
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "kage_vs_" + id + "_sheet.png")));
        }

        [Test]
        public void RenderTheFootmanShadeAndTutorialHollowSideBySide()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            var ids = new[] { "order_footman", "hollow_shade", "hollow_tutorial" };
            var sheet = new Texture2D(W * 3, H, TextureFormat.RGBA32, false);
            for (int i = 0; i < ids.Length; i++)
            {
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: CharacterCatalog.Get(ids[i]));
                f.KageInput.Add(300);
                f.RoninInput.Add(300);
                f.Run(20);
                sheet.SetPixels32(i * W, 0, W, H, Render(f.Ronin.Pos + new Vector2(0f, 1.0f), 1.05f));
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "footman_shade_tutorial_sheet.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Assert.IsTrue(File.Exists(Path.Combine(Dir, "footman_shade_tutorial_sheet.png")));
        }

        Color32[] Shot(string id, string name, int frames, float kageX, float foeX, System.Action<ScriptedInput, ScriptedInput> script)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, foeX, foeDef: CharacterCatalog.Get(id));
            script(f.KageInput, f.RoninInput);
            f.Run(frames);
            if (name == "hit") f.Ronin.rig.Secondary(0.2f, Vector2.zero);
            var px = Render((f.Kage.Pos + f.Ronin.Pos) * 0.5f + new Vector2(0f, 0.95f), 1.3f);
            Save("kage_vs_" + id + "_" + name, px);
            return px;
        }

        static void Save(string name, Color32[] px)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static Color32[] Render(Vector2 centre, float orthoSize)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
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
    }
}
