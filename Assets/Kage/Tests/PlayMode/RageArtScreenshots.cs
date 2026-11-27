using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// The SAN-82 review sheets: Crimson Moon (the freeze with the cut-in band and the camera closing in, the three cuts, the
    /// step back and sheathing, the giant crescent and the finisher, the foe thrown) and Thousand Shadows (the charge, the cuts,
    /// the finisher) with a Mizuchi Kage. Written to Screenshots/review/SAN-82/ (crimson_moon_sheet.png, thousand_shadows_sheet.png
    /// and the tiles). Needs a GPU: skips itself under -nographics. Run with <c>-testFilter RageArtScreenshots</c>. The effects run
    /// on rendered frames at a fixed 1/60 s, one fight frame per rendered frame.
    /// </summary>
    [Category("Screenshots")]
    public class RageArtScreenshots
    {
        const int W = 640, H = 360, Cols = 4;
        const int GalleryW = 1280, GalleryH = 720;

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-82"));
        static string HudGalleryRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "rage-art-hud"));

        /// <summary>
        /// High-resolution (1280x720) HUD verification gallery for ALL 13 playable weapons: captures a RAGE READY HUD baseline plus
        /// BOTH the settled Cut-In band AND the settled Finisher Stamp frame for every weapon into Screenshots/review/rage-art-hud/.
        /// </summary>
        [UnityTest]
        public IEnumerator RenderAllWeaponRageArtHudGallery()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("needs a GPU: run without -nographics"); yield break; }
            string root = HudGalleryRoot;
            Directory.CreateDirectory(root);

            var weapons = new[]
            {
                "mumei", "fists", "raiko_no_tachi", "kagehira_yurei",
                "kagetsu", "shinsen", "mugen_no_ha",
                "tsubame", "shizuka", "onikiri", "mizuchi", "yamigama", "iwakudaki",
            };

            bool savedReady = false;
            foreach (var weapon in weapons)
            {
                var objects = new List<GameObject>();
                var vfxGo = new GameObject("GalleryVfx");
                vfxGo.AddComponent<Vfx>();
                objects.Add(vfxGo);
                var camGo = new GameObject("GalleryCam");
                objects.Add(camGo);
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 2.3f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.16f, 0.13f, 0.14f, 1f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }
                var rt = new RenderTexture(GalleryW, GalleryH, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                var rig = camGo.AddComponent<CameraRig>();
                rig.cam = cam;

                var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(floor.GetComponent<Collider>());
                floor.transform.position = new Vector3(0f, -0.35f, 5f);
                floor.transform.localScale = new Vector3(40f, 0.7f, 1f);
                var mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.08f, 0.06f, 0.07f, 1f) };
                floor.GetComponent<Renderer>().sharedMaterial = mat;
                objects.Add(floor);

                var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.6f, 0.6f, 100f, kageDef: SpearFamilyFightTests.KageWith(weapon));
                f.Ronin.maxHealth = f.Ronin.health = 500f;
                var hudGo = new GameObject("HUD", typeof(RectTransform));
                objects.Add(hudGo);
                var hud = hudGo.AddComponent<Hud>();
                hud.Build(cam);
                hud.DevText = false;
                var canvas = hudGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
                canvas.sortingOrder = 5000;
                hud.Bind(f.Kage);
                hud.SetNames("KAGE  影", "RONIN");
                f.Ctx.Camera = rig;
                f.Ctx.Hud = hud;
                rig.targets.Add(f.Kage.rig.Bone(B.Hips));
                rig.targets.Add(f.Ronin.rig.Bone(B.Hips));
                rig.Snap();

                f.RoninInput.Add(2400);
                f.KageInput.Add(savedReady ? 4 : 20).Add(1, dy: -1, buttons: "X").Add(2400);

                float prevCapture = Time.captureDeltaTime;
                Time.captureDeltaTime = 1f / 60f;
                bool gotCutIn = false, gotStamp = false;
                int cutTicks = -1, stampTicks = -1;
                try
                {
                    for (int i = 0; i < 420 && !(gotCutIn && gotStamp); i++)
                    {
                        f.Run(1);
                        GameFeel.trauma = 0f;
                        GameFeel.Tick();
                        rig.Follow(1f / 60f);
                        hud.Tick(f.Kage, f.Ronin, 99f, 1, true);
                        yield return null;

                        if (!savedReady && i == 15)
                        {
                            cam.Render();
                            SavePng(Path.Combine(root, "00_rage_ready_hud.png"), Read(rt), GalleryW, GalleryH);
                            savedReady = true;
                        }

                        if (!gotCutIn && hud.CutInShowing)
                        {
                            cutTicks = cutTicks < 0 ? 0 : cutTicks + 1;
                            if (cutTicks == 15)
                            {
                                cam.Render();
                                SavePng(Path.Combine(root, $"{weapon}_cutin.png"), Read(rt), GalleryW, GalleryH);
                                gotCutIn = true;
                                Debug.Log($"[HUD Gallery] {weapon} cut-in: name='{hud.CutInName}' kanji='{hud.CutInKanji}' sub='{hud.CutInCaption}'");
                            }
                        }

                        if (!gotStamp && hud.ArtStampShowing)
                        {
                            stampTicks = stampTicks < 0 ? 0 : stampTicks + 1;
                            if (stampTicks == 14)
                            {
                                cam.Render();
                                SavePng(Path.Combine(root, $"{weapon}_stamp.png"), Read(rt), GalleryW, GalleryH);
                                gotStamp = true;
                                Debug.Log($"[HUD Gallery] {weapon} stamp: word='{hud.ArtStampWord}' kanji='{hud.ArtStampKanji}' sub='{hud.ArtStampCaption}'");
                            }
                        }
                    }

                    Assert.IsTrue(gotCutIn, $"{weapon}: captured settled Cut-In HUD frame");
                    Assert.IsTrue(gotStamp, $"{weapon}: captured settled Finisher Stamp HUD frame");
                }
                finally
                {
                    Time.captureDeltaTime = prevCapture;
                    f.Dispose();
                    foreach (var o in objects) if (o != null) Object.DestroyImmediate(o);
                    rt.Release();
                    Object.DestroyImmediate(rt);
                    Object.DestroyImmediate(mat);
                    GameFeel.trauma = GameFeel.punch = 0f;
                }
            }
        }

        [UnityTest]
        public IEnumerator RenderTheCrimsonMoonSheet()
        {
            yield return Sheet("crimson_moon", null, new[] { 14, 28, 34, 45, 57, 70, 84, 100 }, new[] { "freeze and cut-in", "band leaving, camera in", "cut 1", "cut 2", "cut 3", "step back, sheathe", "giant crescent", "foe thrown" });
        }

        [UnityTest]
        public IEnumerator RenderTheThousandShadowsSheet()
        {
            yield return Sheet("thousand_shadows", "fists", new[] { 6, 12, 22, 34, 52, 70, 85, 100 }, new[] { "charge", "vanish", "cut 1", "cut 3", "cut 5", "Kage returns", "finisher", "after" });
        }

        // ---- the shop weapons' own arts (docs/weapon-styles.md): Screenshots/review/weapon-arts/ ----
        const string Arts = "weapon-arts";

        [UnityTest]
        public IEnumerator RenderTheSilentMoonSheet() => Sheet("silent_moon", "shizuka", new[] { 8, 18, 38, 45, 64, 100, 113, 132 },
            new[] { "sheathing, cut-in", "click", "stillness", "the draw: past the foe", "the lattice", "noto", "the fall: SILENCE", "fallen" }, Arts);

        [UnityTest]
        public IEnumerator RenderTheThousandSwallowsSheet() => Sheet("thousand_swallows", "tsubame", new[] { 12, 31, 46, 61, 76, 88, 94, 106 },
            new[] { "gathering", "strike 1", "strike 4", "strike 7", "step out", "RAIJIN", "after the X", "thrown" }, Arts);

        [UnityTest]
        public IEnumerator RenderTheHellSplitterSheet() => Sheet("hell_splitter", "onikiri", new[] { 10, 26, 40, 49, 53, 62, 72, 92 },
            new[] { "demon aura", "cracks", "leap", "the fall", "HELL SPLIT", "pillars 1", "pillars 2", "after" }, Arts);

        [UnityTest]
        public IEnumerator RenderTheWaterDragonSheet() => Sheet("water_dragon", "mizuchi", new[] { 12, 30, 40, 56, 72, 96, 109, 124 },
            new[] { "flood and rain", "gathering", "the dragon bursts", "arrival", "the coil", "the rise", "MIZUCHI", "after" }, Arts);

        [UnityTest]
        public IEnumerator RenderTheReapersRequiemSheet() => Sheet("reapers_requiem", "yamigama", new[] { 8, 24, 38, 52, 70, 86, 97, 116 },
            new[] { "the dark", "clones risen", "chains", "cut 1", "cut 3", "the leap", "REQUIEM", "after" }, Arts);

        [UnityTest]
        public IEnumerator RenderTheEarthsplitterSheet() => Sheet("earthsplitter", "iwakudaki", new[] { 8, 30, 46, 60, 66, 82, 98, 112 },
            new[] { "the waking", "rocks and rays", "the leap", "the plunge", "the smash: cracks", "wave 1", "wave 2", "EARTHSPLITTER" }, Arts);

        /// <summary>Earthsplitter on a foe it kills (the final blow): the screen shatters like glass a beat later.</summary>
        [UnityTest]
        public IEnumerator RenderTheEarthsplitterKillSheet() => Sheet("earthsplitter_kill", "iwakudaki", new[] { 66, 100, 109, 117, 132, 142, 152, 168 },
            new[] { "the smash: a crack", "wave 2", "the killing blow", "the web races out", "held, the cracks lit", "the break", "shards falling", "the fight behind" }, Arts,
            f => f.Ronin.health = 30f);

        /// <summary>Close-ups of Kage holding the hammer (the owner saw a gap between its shaft and its head) and the plain katana.</summary>
        [UnityTest]
        public IEnumerator RenderTheWeaponLooks()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("needs a GPU: run without -nographics"); yield break; }
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", Arts));
            Directory.CreateDirectory(root);
            foreach (var (weapon, size, stance) in new[] { ("iwakudaki", 1.1f, false), ("iwakudaki", 1.1f, true), ("mumei", 0.9f, false), ("kagetsu", 0.9f, false) })
            {
                var camGo = new GameObject("LookCam");
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = size;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.17f, 0.06f, 0.07f, 1f);
                cam.nearClipPlane = 0.1f; cam.farClipPlane = 100f;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }
                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, 0.6f, 0f, kageDef: SpearFamilyFightTests.KageWith(weapon));
                try
                {
                    if (stance) f.KageInput.Add(1, buttons: "S").Add(200); else f.KageInput.Add(200);
                    f.Run(stance ? 18 : 40);
                    for (int i = 0; i < 3; i++) yield return null;
                    Vector2 hand = f.Kage.rig.World(B.HandF, Vector2.zero);
                    camGo.transform.position = new Vector3(hand.x + 0.35f, hand.y, -10f);
                    cam.Render();
                    SavePng(Path.Combine(root, $"look_{weapon}{(stance ? "_raised" : "")}.png"), Read(rt), W, H);
                }
                finally
                {
                    f.Dispose();
                    Object.DestroyImmediate(camGo);
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
        }

        IEnumerator Sheet(string name, string weapon, int[] ticks, string[] labels, string folder = null, System.Action<HeadlessFight> setup = null)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("needs a GPU: run without -nographics"); yield break; }
            string root = folder == null ? Root : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", folder));
            string tiles = Path.Combine(root, "tiles");
            Directory.CreateDirectory(tiles);

            var objects = new List<GameObject>();
            var vfxGo = new GameObject("ReviewVfx");
            vfxGo.AddComponent<Vfx>();
            objects.Add(vfxGo);
            var camGo = new GameObject("ShotCam");
            objects.Add(camGo);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2.3f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.13f, 0.14f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var rig = camGo.AddComponent<CameraRig>();
            rig.cam = cam;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.position = new Vector3(0f, -0.35f, 5f);
            floor.transform.localScale = new Vector3(40f, 0.7f, 1f);
            var mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.08f, 0.06f, 0.07f, 1f) };
            floor.GetComponent<Renderer>().sharedMaterial = mat;
            objects.Add(floor);

            var kageDef = weapon == null ? null : SpearFamilyFightTests.KageWith(weapon);
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.6f, 0.6f, 100f, kageDef: kageDef);
            var hudGo = new GameObject("HUD", typeof(RectTransform));
            objects.Add(hudGo);
            var hud = hudGo.AddComponent<Hud>();
            hud.Build(cam);
            hud.DevText = false;
            var canvas = hudGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 5000;
            hud.Bind(f.Kage);
            hud.SetNames("KAGE  影", "RONIN");
            f.Ctx.Camera = rig;
            f.Ctx.Hud = hud;
            rig.targets.Add(f.Kage.rig.Bone(B.Hips));
            rig.targets.Add(f.Ronin.rig.Bone(B.Hips));
            rig.Snap();

            f.RoninInput.Add(2400);
            f.KageInput.Add(4).Add(1, dy: -1, buttons: "X").Add(2400);
            setup?.Invoke(f);

            var shots = new List<Color32[]>();
            float prevCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                int next = 0;
                for (int i = 0; i < 400 && next < ticks.Length; i++)
                {
                    f.Run(1);
                    GameFeel.trauma = 0f;
                    GameFeel.Tick();
                    rig.Follow(1f / 60f);
                    hud.Tick(f.Kage, f.Ronin, 99f, 1, true);
                    yield return null;
                    if (f.Kage.state == FState.Ultimate && f.Kage.RageArtFrame >= ticks[next])
                    {
                        cam.Render();
                        var px = Read(rt);
                        SavePng(Path.Combine(tiles, $"{name}_{next}_tick{ticks[next]}.png"), px, W, H);
                        shots.Add(px);
                        Debug.Log($"[SAN-82 shots] {name} {labels[next]}: art frame {f.Kage.RageArtFrame}, camera {cam.orthographicSize:0.00} at {cam.transform.position.x:0.00}, override {rig.useOverride}, Ronin {f.Ronin.state} hp {f.Ronin.health:0.#}, world {f.Ctx.Clock.WorldScale}");
                        next++;
                    }
                }
                Assert.AreEqual(ticks.Length, shots.Count, "every tile was captured");
                Debug.Log($"[SAN-82 shots] {name}: Kage {f.Kage.state}, camera override {rig.useOverride}, world {f.Ctx.Clock.WorldScale}");
            }
            finally
            {
                Time.captureDeltaTime = prevCapture;
                f.Dispose();
                foreach (var o in objects) if (o != null) Object.DestroyImmediate(o);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(mat);
                GameFeel.trauma = GameFeel.punch = 0f;
            }

            int rows = (shots.Count + Cols - 1) / Cols;
            var sheet = new Texture2D(W * Cols, H * rows, TextureFormat.RGBA32, false);
            for (int k = 0; k < shots.Count; k++)
                sheet.SetPixels32((k % Cols) * W, (rows - 1 - k / Cols) * H, W, H, shots[k]);
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(root, name + "_sheet.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }

        static Color32[] Read(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return px;
        }

        static void SavePng(string path, Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
