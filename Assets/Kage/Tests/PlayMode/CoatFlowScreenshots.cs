using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// Review strips of Kage's lower coat in motion: frame by frame through a walk, a run back and an idle, for his plain coat and two
    /// cut hems, written to Screenshots/review/coat-flow/. Each cell is one sampled frame of the lower body (belt to boots). Needs a
    /// GPU: it skips itself under -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class CoatFlowScreenshots
    {
        const int Cell = 300, Frames = 8, Every = 4;
        static readonly Color Back = new(0.16f, 0.13f, 0.14f, 1f);
        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "coat-flow"));

        static CharacterDef KageWearing(string armor)
        {
            var def = CharacterCatalog.Get(KageCharacter.Id);
            def.armor = armor;
            return def;
        }

        [Test]
        public void StripsOfTheCoatWalkingAndIdle()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(OutDir);
            foreach (var armor in new[] { "tachibana_yoroi", "kagehira_yoroi", "ronin_yoroi" })
            {
                var rows = new List<List<Color32[]>>();
                foreach (var move in new[] { "walk", "back", "idle" })
                {
                    var row = new List<Color32[]>();
                    using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, -0.6f, kageDef: KageWearing(armor));
                    f.Run(20);
                    if (move == "walk") f.KageInput.Add(200, fwd: 1);
                    else if (move == "back") f.KageInput.Add(200, fwd: -1);
                    f.Run(16);
                    for (int i = 0; i < Frames; i++)
                    {
                        f.Run(Every);
                        var hips = (Vector2)f.Kage.rig.Bone(B.Hips).position;
                        row.Add(Render(new Vector2(hips.x - 0.1f, f.Kage.Pos.y + 0.55f), 0.62f));
                    }
                    rows.Add(row);
                }
                Sheet($"coat_{armor}.png", rows);
                FileAssert.Exists(Path.Combine(OutDir, $"coat_{armor}.png"));
            }
        }

        static void Sheet(string name, List<List<Color32[]>> rows)
        {
            int cols = Frames;
            var tex = new Texture2D(cols * Cell, rows.Count * Cell, TextureFormat.RGBA32, false);
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < rows[r].Count; c++)
                    tex.SetPixels32(c * Cell, (rows.Count - 1 - r) * Cell, Cell, Cell, rows[r][c]);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, name), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static Color32[] Render(Vector2 centre, float halfHeight)
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
            var rt = new RenderTexture(Cell, Cell, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Cell, Cell, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
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
