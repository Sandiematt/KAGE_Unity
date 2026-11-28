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
    /// The review sheets of SAN-85: Kage in each of the armours Act I can give him (the starter, the 7 shop armours, the 2 Act I
    /// trophies), upper-body close-ups in idle, walk, attack, hit and K.O. ragdoll poses, a full-body idle sheet, a hairstyle and armour
    /// combination sheet, and one large picture per armour, written to Screenshots/review/SAN-85/. Each cell is labelled with the JS
    /// name and id. Needs a GPU: it skips itself under -nographics. Run
    /// <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter ArmorScreenshots</c> (no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class ArmorScreenshots
    {
        // ticket order: the starter, the 7 shop armours in the JS table's order, the 2 Act I rewards
        public static readonly string[] Act1Armors =
        {
            "tachibana_yoroi", "ronin_yoroi", "yami_yoroi", "shinsen_yoroi", "raiko_yoroi", "kagehira_yoroi", "yurei_yoroi", "mugen_yoroi",
            "ikazuchi_do", "shiden_yoroi",
        };

        static readonly string[] Poses = { "idle", "walk", "attack", "hit", "ragdoll" };
        static readonly string[] Combos = { "spike", "messy_layers", "low_ponytail", "topknot", "long_flowing", "undercut" };

        const int Cell = 400, Cols = 5;
        static readonly Color Back = new(0.16f, 0.13f, 0.14f, 1f);

        static string OutDir => System.Environment.GetEnvironmentVariable("KAGE_ARMOR_CAPTURE_DIR")
            ?? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-85"));

        static CharacterDef KageWearing(string armor, string hairstyle = null)
        {
            var def = CharacterCatalog.Get(KageCharacter.Id);
            def.armor = armor;
            def.hairstyle = hairstyle;
            return def;
        }

        static string Label(string id) => $"{JsTables.Armor(id).Name}  ({id})";

        /// <summary>A fight with Kage in the pose, ready to photograph; the caller disposes it.</summary>
        static HeadlessFight Pose(string armor, string pose, string hairstyle = null)
        {
            if (pose == "ragdoll")
            {
                var k = KageWearing(armor, hairstyle);
                var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.4f, 0.4f, kageDef: k);
                f.Kage.health = 1f;
                f.KageInput.Add(300);
                f.RoninInput.Add(1, dy: -1, buttons: "K").Add(300);
                f.Run(85);
                return f;
            }
            var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, -0.2f, kageDef: KageWearing(armor, hairstyle));
            switch (pose)
            {
                case "idle": fight.Run(30); break;
                case "walk": fight.KageInput.Add(30, fwd: 1); fight.Run(24); break;
                case "attack": fight.KageInput.Add(1, buttons: "S"); fight.Run(14); break;
                case "hit": fight.Run(20); fight.Kage.rig.Apply(PoseLib.HurtHigh()); break;
            }
            return fight;
        }

        // the whole figure (hair to boots, coat tail to the left), or just the chest and shoulders
        static Vector2 BodyCentre(HeadlessFight f) => new Vector2(f.Kage.rig.Bone(B.Chest).position.x - 0.12f, f.Kage.Pos.y + 1.05f);
        static Vector2 TorsoCentre(HeadlessFight f) => (Vector2)f.Kage.rig.Bone(B.Chest).position + new Vector2(-0.02f, 0.1f);

        [Test]
        public void SheetOfAllTheAct1Armours()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Assert.AreEqual(10, Act1Armors.Length);
            Directory.CreateDirectory(OutDir);

            var close = new Dictionary<string, List<Color32[]>>();
            var body = new List<Color32[]>();
            var torso = new List<Color32[]>();
            foreach (var pose in Poses) close[pose] = new List<Color32[]>();

            foreach (var id in Act1Armors)
            {
                string label = Label(id);
                foreach (var pose in Poses)
                {
                    using var f = Pose(id, pose);
                    close[pose].Add(Render(BodyCentre(f), 1.15f, label + "  " + pose));
                    if (pose == "idle")
                    {
                        File.WriteAllBytes(Path.Combine(OutDir, $"armor_{id}.png"), Png(Render(TorsoCentre(f), 0.62f, label, 800, 800), 800, 800));
                        torso.Add(Render(TorsoCentre(f), 0.55f, label));
                        body.Add(Render(BodyCentre(f), 1.15f, label));
                    }
                }
            }

            foreach (var pose in Poses) Sheet($"armors_{pose}.png", close[pose]);
            Sheet("armors_fullbody_idle.png", body);
            Sheet("armors_chest_closeup.png", torso);
            foreach (var f in new[] { "armors_chest_closeup.png", "armors_idle.png", "armors_walk.png", "armors_attack.png", "armors_hit.png", "armors_ragdoll.png", "armors_fullbody_idle.png" })
                FileAssert.Exists(Path.Combine(OutDir, f));
        }

        /// <summary>The trophies the Armory shows after the first two (SAN-90 rework): each gets its own look.</summary>
        public static readonly string[] OtherTrophies =
        {
            "shirabe_do", "kasane_haori", "tetsumon_do", "kudamaki_do", "mei_en_garb", "kysmei_robe", "kurokumo_yoroi", "shinsei_yoroi",
        };

        [Test]
        public void SheetOfTheOtherTrophies()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(OutDir);
            var close = new Dictionary<string, List<Color32[]>>();
            var torso = new List<Color32[]>();
            foreach (var pose in Poses) close[pose] = new List<Color32[]>();
            foreach (var id in OtherTrophies)
            {
                string label = Label(id);
                foreach (var pose in Poses)
                {
                    using var f = Pose(id, pose);
                    close[pose].Add(Render(BodyCentre(f), 1.15f, label + "  " + pose));
                    if (pose == "idle")
                    {
                        File.WriteAllBytes(Path.Combine(OutDir, $"armor_{id}.png"), Png(Render(TorsoCentre(f), 0.62f, label, 800, 800), 800, 800));
                        torso.Add(Render(TorsoCentre(f), 0.55f, label));
                    }
                }
            }
            foreach (var pose in Poses) SheetCols($"armors_trophies_{pose}.png", close[pose], 4);
            SheetCols("armors_trophies_chest_closeup.png", torso, 4);
            FileAssert.Exists(Path.Combine(OutDir, "armors_trophies_idle.png"));
            FileAssert.Exists(Path.Combine(OutDir, "armors_trophies_chest_closeup.png"));
        }

        [Test]
        public void SheetOfTheMugenRingInEveryPoseAndFacing()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(OutDir);
            var cells = new List<Color32[]>();
            foreach (var facing in new[] { 1f, -1f })
                foreach (var pose in Poses)
                {
                    using var f = Pose("mugen_yoroi", pose);
                    f.Kage.rig.SetFacing(facing, true);
                    var c = BodyCentre(f);
                    cells.Add(Render(new Vector2(c.x, c.y + 0.35f), 0.95f, $"mugen ring  {pose}  facing {(facing > 0 ? "right" : "left")}"));
                }
            SheetCols("mugen_ring_poses.png", cells, Poses.Length);
            FileAssert.Exists(Path.Combine(OutDir, "mugen_ring_poses.png"));
        }

        [Test]
        public void SheetOfHairstyleAndArmourCombinations()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(OutDir);
            var cells = new List<Color32[]>();
            foreach (var id in Act1Armors)
                foreach (var hairstyle in Combos)
                {
                    using var f = Pose(id, "idle", hairstyle);
                    cells.Add(Render((Vector2)f.Kage.rig.Bone(B.Head).position + new Vector2(0.0f, -0.2f), 0.62f, $"{id} + {hairstyle ?? "no hairstyle"}"));
                }
            SheetCols("armors_with_hairstyles.png", cells, Combos.Length);
            FileAssert.Exists(Path.Combine(OutDir, "armors_with_hairstyles.png"));
        }

        static void Sheet(string name, List<Color32[]> cells) => SheetCols(name, cells, Cols);

        static void SheetCols(string name, List<Color32[]> cells, int cols)
        {
            int rows = (cells.Count + cols - 1) / cols;
            var tex = new Texture2D(cols * Cell, rows * Cell, TextureFormat.RGBA32, false);
            var bg = new Color32[tex.width * tex.height];
            for (int i = 0; i < bg.Length; i++) bg[i] = Back;
            tex.SetPixels32(bg);
            for (int i = 0; i < cells.Count; i++)
            {
                int cx = (i % cols) * Cell, cy = (rows - 1 - i / cols) * Cell;
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
