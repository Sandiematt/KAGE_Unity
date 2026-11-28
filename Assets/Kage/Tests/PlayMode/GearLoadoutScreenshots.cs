using System.Collections.Generic;
using System.IO;
using Kage.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The review sheet of SAN-84: Kage built from three saves through the real spawn path (<see cref="KageCharacter.Def(SaveData)"/>), so what
    /// he wears follows the loadout. Each cell is labelled with the equipped gear and levels and what the rules give for it (health in JS
    /// units and in Unity health, damage reduction, damage and speed). Written to Screenshots/review/SAN-84/. Needs a GPU: it skips
    /// itself under -nographics. Run <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter GearLoadoutScreenshots</c>
    /// (no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class GearLoadoutScreenshots
    {
        const int W = 560, H = 760;
        static readonly Color Back = new(0.16f, 0.13f, 0.14f, 1f);

        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-84"));

        static SaveData Save(string weapon, int wl, string hairstyle, string armor, int al, int level)
        {
            var s = GameRules.DefaultSave(false);
            s.level = level;
            s.owned.weapons[weapon] = wl;
            s.owned.armors[armor] = al;
            s.equipped.weapon = weapon;
            s.equipped.hairstyle = hairstyle;
            s.equipped.armor = armor;
            return s;
        }

        static string Label(SaveData s)
        {
            var d = KageCharacter.Def(s);
            var e = s.equipped;
            string Name(JsGear g, string id) => g != null ? g.Name : id;
            return $"{JsTables.Weapon(e.weapon).Name} L{d.weaponLevel}   player level {s.level}\n"
                 + $"{Hairstyles.Name(e.hairstyle)}   {Name(JsTables.Armor(e.armor), e.armor)} L{s.owned.armors[e.armor]}\n"
                 + $"health {d.jsHealth:0} JS = {d.MaxHealth():0.0}   defence {d.defense * 100:0.#} %\n"
                 + $"damage x{d.damageMul:0.###}   speed x{d.speedMul:0.##}";
        }

        [Test]
        public void SheetOfKageBuiltFromThreeSaves()
        {
            Assume.That(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(OutDir);
            var saves = new[]
            {
                Save("kagetsu", 1, "spike", "tachibana_yoroi", 1, 1),
                Save("shizuka", 6, "messy_layers", "ronin_yoroi", 4, 4),
                Save("kagetsu", 8, "undercut", "mugen_yoroi", 5, 11),
            };
            var cells = new List<Color32[]>();
            foreach (var s in saves)
            {
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, -0.2f, kageDef: KageCharacter.Def(s));
                f.Run(30);
                cells.Add(Render(new Vector2(f.Kage.rig.Bone(B.Chest).position.x - 0.12f, f.Kage.Pos.y + 1.0f), 1.25f, Label(s)));
            }
            var tex = new Texture2D(cells.Count * W, H, TextureFormat.RGBA32, false);
            for (int i = 0; i < cells.Count; i++) tex.SetPixels32(i * W, 0, W, H, cells[i]);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(OutDir, "kage_from_save_loadouts.png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            FileAssert.Exists(Path.Combine(OutDir, "kage_from_save_loadouts.png"));
        }

        static Color32[] Render(Vector2 centre, float halfHeight, string label)
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

            var tgo = new GameObject("Label");
            float halfWidth = halfHeight * W / H;
            tgo.transform.position = new Vector3(centre.x - halfWidth + 0.03f * halfWidth, centre.y - halfHeight + 0.27f * halfHeight, 0f);
            var tm = tgo.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = font;
            tm.text = label;
            tm.characterSize = 0.0085f * halfHeight;
            tm.fontSize = 64;
            tm.color = new Color(0.93f, 0.9f, 0.85f, 1f);
            var mr = tgo.GetComponent<MeshRenderer>();
            mr.sharedMaterial = font.material;
            mr.sortingOrder = 10000;

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
            Object.DestroyImmediate(tgo);
            Object.DestroyImmediate(go);
            return px;
        }
    }
}
