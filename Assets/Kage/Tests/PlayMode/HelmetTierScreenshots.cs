using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// SAN-72 tier redesign: the review tiles in Screenshots/review/SAN-72/tiers/tiles/ (every Act I helmet on the game's real rig, head
    /// close-ups in idle, walk, attack, hit and a head wind), the Armory's helmet tile pictures (Resources/UI/Img/icon_h_&lt;id&gt;.png),
    /// and the pixel check of everything the helmets must not change. The tier sheet and the pose sheets are laid out from the tiles by
    /// <c>python Tools/helmet_art/build.py sheets Screenshots/review/SAN-72/tiers</c>. Needs a GPU: it skips itself under -nographics.
    /// Run <c>-testFilter HelmetTierScreenshots</c> without -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class HelmetTierScreenshots
    {
        const int W = 800, H = 600;
        const int Tile = 420, IconW = 320, IconH = 320;
        static readonly Color Back = new(0.10f, 0.11f, 0.14f, 1f);

        // the 14 Act I helmets in 5-tier order (Common, Rare, Epic, Trophy, Legendary)
        static readonly string[] Act1 =
        {
            "shiro_maki", "kuro_zukin",
            "sugegasa", "hachigane", "kasumi_men",
            "tetsu_kabuto", "oni_menpo", "hoshi_kabuto",
            "shika_kabuto", "sumi_eboshi", "hisame_zukin",
            "raijin_kabuto", "utsuro_kabuto", "shikko_zukin",
        };

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static CharacterDef Wearing(string helmet, bool bareHands = false)
        {
            var def = CharacterCatalog.Get(KageCharacter.Id);
            def.helmet = helmet;
            if (bareHands) def.weaponId = "fists";
            return def;
        }

        /// <summary>Kage in a pose: idle, a walk, a slash, the high hurt pose, and standing in a head wind that blows his soft pieces back.</summary>
        static HeadlessFight Posed(string helmet, string pose, bool bareHands = false)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, -0.2f, kageDef: Wearing(helmet, bareHands));
            switch (pose)
            {
                case "idle": f.Run(30); break;
                case "walk": f.KageInput.Add(30, fwd: 1); f.Run(24); break;
                case "attack": f.KageInput.Add(1, buttons: "S"); f.Run(14); break;
                case "hit": f.Run(20); f.Kage.rig.Apply(PoseLib.HurtHigh()); break;
                case "wind": f.Kage.rig.wind = Wind.Blowing(new Vector2(-5f, 0f), 2f); f.Run(60); break;
            }
            return f;
        }

        [Test]
        public void ReviewTilesAndArmoryPictures()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string tiles = Path.Combine(Root, "Screenshots", "review", "SAN-72", "tiers", "tiles");
            string icons = Path.Combine(Application.dataPath, "Kage", "Resources", "UI", "Img");
            Directory.CreateDirectory(tiles);

            foreach (var id in Act1)
            {
                foreach (var pose in new[] { "idle", "walk", "attack", "hit", "wind" })
                {
                    using var f = Posed(id, pose);
                    Vector2 head = f.Kage.rig.Bone(B.Head).position;
                    Save(Path.Combine(tiles, $"{id}_{pose}.png"), Render(head + new Vector2(-0.08f, 0.14f), 0.56f, Tile, Tile, Back), Tile, Tile);
                    if (pose == "walk")
                        Save(Path.Combine(tiles, $"{id}_body.png"), Render(f.Kage.Pos + new Vector2(0f, 1.0f), 1.2f, Tile, Tile, Back), Tile, Tile);
                }
                // Square side-profile bust portrait for the 5-tier Helmet Selection cards (no weapon intruding into the bust)
                using (var portrait = Posed(id, "idle", bareHands: true))
                {
                    Vector2 head = portrait.Kage.rig.Bone(B.Head).position;
                    Save(Path.Combine(icons, $"icon_h_{id}.png"), Icon(head + new Vector2(-0.04f, 0.10f)), IconW, IconH);
                }
            }
            FileAssert.Exists(Path.Combine(tiles, "raijin_kabuto_wind.png"));
            FileAssert.Exists(Path.Combine(icons, "icon_h_raijin_kabuto.png"));
        }

        /// <summary>An Armory helmet bust portrait: Kage's dark silhouette, glowing eye, crimson scarf and helmet on transparent alpha.</summary>
        static Color32[] Icon(Vector2 centre)
        {
            var black = Render(centre, 0.50f, IconW, IconH, Color.black);
            var white = Render(centre, 0.50f, IconW, IconH, Color.white);
            var o = new Color32[black.Length];
            for (int i = 0; i < o.Length; i++)
            {
                float cov = 1f - ((white[i].r - black[i].r) + (white[i].g - black[i].g) + (white[i].b - black[i].b)) / (3f * 255f);
                cov = Mathf.Clamp01(cov);
                if (cov < 0.004f) { o[i] = new Color32(0, 0, 0, 0); continue; }
                byte C(byte b) => (byte)Mathf.Clamp(Mathf.RoundToInt(b / cov), 0, 255);
                o[i] = new Color32(C(black[i].r), C(black[i].g), C(black[i].b), (byte)Mathf.RoundToInt(cov * 255f));
            }
            return o;
        }

        static void Save(string path, Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static Color32[] Render(Vector2 centre, float orthoSize, int w, int h, Color bg)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(centre.x, centre.y, -10f);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
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

        /// <summary>
        /// Kage with no helmet (and in the starter, which draws nothing) and every other character of the catalog, each tile alone,
        /// written to the folder named by the KAGE_PIXEL_DIR environment variable, so a run before the change and one after can be
        /// compared pixel for pixel. Skipped when the variable is not set (the review run does not need it).
        /// </summary>
        [Test]
        public void PixelCheckOfKageBareHeadedAndTheOtherCharacters()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string dir = Environment.GetEnvironmentVariable("KAGE_PIXEL_DIR");
            Assume.That(!string.IsNullOrEmpty(dir), "set KAGE_PIXEL_DIR to write the pixel check");
            Directory.CreateDirectory(dir);

            foreach (var helmet in new[] { null, "shiro_maki" })
            {
                string tag = helmet ?? "none";
                var def = CharacterCatalog.Get(KageCharacter.Id);
                def.helmet = helmet;
                using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, -0.2f, kageDef: def))
                {
                    f.Run(30);
                    Vector2 c = f.Kage.rig.Bone(B.Head).position;
                    HuskSvgScreenshots.Save(Path.Combine(dir, $"kage_{tag}_head.png"), HuskSvgScreenshots.Render(c + new Vector2(0f, 0.1f), 0.5f), W, H);
                    HuskSvgScreenshots.Save(Path.Combine(dir, $"kage_{tag}_body.png"), HuskSvgScreenshots.Render(f.Kage.Pos + new Vector2(0f, 1.1f), 1.25f), W, H);
                }
                using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, -0.2f, kageDef: def))
                {
                    f.KageInput.Add(24, fwd: 1).Add(300);
                    f.Run(34);
                    HuskSvgScreenshots.Save(Path.Combine(dir, $"kage_{tag}_motion.png"), HuskSvgScreenshots.Render(f.Kage.Pos + new Vector2(0f, 1.1f), 1.3f), W, H);
                }
            }

            foreach (var armor in ArmorVisuals.Ids)
            {
                var def = CharacterCatalog.Get(KageCharacter.Id);
                def.armor = armor;
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, -0.2f, kageDef: def);
                f.KageInput.Add(24, fwd: 1).Add(300);
                f.Run(34);
                HuskSvgScreenshots.Save(Path.Combine(dir, $"armor_{armor}.png"), HuskSvgScreenshots.Render(f.Kage.Pos + new Vector2(0f, 1.1f), 1.3f), W, H);
            }

            foreach (var id in CharacterCatalog.Ids)
            {
                if (id == KageCharacter.Id) continue;
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: CharacterCatalog.Get(id));
                f.KageInput.Add(300);
                f.RoninInput.Add(24, fwd: 1).Add(300);
                f.Run(34);
                HuskSvgScreenshots.Save(Path.Combine(dir, $"char_{id}.png"), HuskSvgScreenshots.Render(f.Ronin.Pos + new Vector2(0f, 1.1f), 1.3f), W, H);
            }
            Assert.IsTrue(File.Exists(Path.Combine(dir, "kage_none_head.png")));
        }
    }
}
