using System.Collections.Generic;
using System.IO;
using Kage.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The review sheets of SAN-72: Kage in each of the 14 helmets Act I can give him (the starter, the 10 shop helmets, the 3
    /// Act I rewards), head close-ups in idle, walk, attack and hit poses, and a full-body idle sheet, written to
    /// Screenshots/review/SAN-72/. Each cell is labelled with the JS name and id. Needs a GPU: it skips itself under
    /// -nographics. Run <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter HelmetScreenshots</c>
    /// (no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class HelmetScreenshots
    {
        // ticket order: the starter, the 10 shop helmets in the JS table's order, the 3 Act I rewards
        public static readonly string[] Act1Helmets =
        {
            "shiro_maki", "kuro_zukin", "sugegasa", "hachigane", "kasumi_men", "tetsu_kabuto", "oni_menpo", "hoshi_kabuto",
            "shika_kabuto", "utsuro_kabuto", "shikko_zukin", "sumi_eboshi", "hisame_zukin", "raijin_kabuto",
        };

        const int Cell = 340, Cols = 5;
        static readonly Color Back = new(0.16f, 0.13f, 0.14f, 1f);

        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-72"));

        static CharacterDef KageWearing(string helmet)
        {
            var def = CharacterCatalog.Get(KageCharacter.Id);
            def.helmet = helmet;
            return def;
        }

        [Test]
        public void SheetOfAll14Helmets()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Assert.AreEqual(14, Act1Helmets.Length);
            Directory.CreateDirectory(OutDir);

            var head = new Dictionary<string, List<Color32[]>>();
            var body = new List<Color32[]>();
            foreach (var pose in new[] { "idle", "walk", "attack", "hit" }) head[pose] = new List<Color32[]>();

            foreach (var id in Act1Helmets)
            {
                string label = $"{JsTables.Helmet(id).Name}  ({id})";
                foreach (var pose in new[] { "idle", "walk", "attack", "hit" })
                {
                    using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, -0.2f, kageDef: KageWearing(id));
                    switch (pose)
                    {
                        case "idle": f.Run(30); break;
                        case "walk": f.KageInput.Add(30, fwd: 1); f.Run(24); break;
                        case "attack": f.KageInput.Add(1, buttons: "S"); f.Run(14); break;
                        case "hit": f.Run(20); f.Kage.rig.Apply(PoseLib.HurtHigh()); break;
                    }
                    Vector2 c = f.Kage.rig.Bone(B.Head).position;
                    head[pose].Add(Render(c + new Vector2(0.0f, 0.1f), 0.68f, label + "  " + pose));
                    if (pose == "idle")
                    {
                        File.WriteAllBytes(Path.Combine(OutDir, $"helmet_{id}.png"), Png(Render(c + new Vector2(0.05f, 0.05f), 0.5f, label, 800, 800), 800, 800));
                        body.Add(Render(f.Kage.Pos + new Vector2(0f, 0.95f), 1.25f, label));
                    }
                }
            }

            foreach (var pose in head.Keys) Sheet($"helmets_{pose}.png", head[pose]);
            Sheet("helmets_fullbody_idle.png", body);
            foreach (var f in new[] { "helmets_idle.png", "helmets_walk.png", "helmets_attack.png", "helmets_hit.png", "helmets_fullbody_idle.png" })
                FileAssert.Exists(Path.Combine(OutDir, f));
        }

        static void Sheet(string name, List<Color32[]> cells)
        {
            int rows = (cells.Count + Cols - 1) / Cols;
            var tex = new Texture2D(Cols * Cell, rows * Cell, TextureFormat.RGBA32, false);
            var bg = new Color32[tex.width * tex.height];
            for (int i = 0; i < bg.Length; i++) bg[i] = Back;
            tex.SetPixels32(bg);
            for (int i = 0; i < cells.Count; i++)
            {
                int cx = (i % Cols) * Cell, cy = (rows - 1 - i / Cols) * Cell;
                tex.SetPixels32(cx, cy, Cell, Cell, cells[i]);
            }
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, name), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static byte[] Png(Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            var b = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            return b;
        }

        static Color32[] Render(Vector2 centre, float halfHeight, string label, int w = Cell, int h = Cell)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = halfHeight;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Back;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(centre.x, centre.y, -10f);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }

            // the caption, along the bottom edge
            var tgo = new GameObject("Label");
            tgo.transform.position = new Vector3(centre.x - halfHeight + 0.02f * halfHeight, centre.y - halfHeight + 0.09f * halfHeight, 0f);
            var tm = tgo.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.text = label;
            tm.characterSize = 0.0175f * halfHeight;
            tm.fontSize = 64;
            tm.color = new Color(0.93f, 0.9f, 0.85f, 1f);
            var mr = tgo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = 10000;

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
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tgo);
            Object.DestroyImmediate(go);
            return px;
        }
    }
}
