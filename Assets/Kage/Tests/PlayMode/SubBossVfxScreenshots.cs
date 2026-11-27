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
    /// Review sheets for the four sub-boss looks (docs/act1-subbosses-vfx.md), written to Screenshots/review/subboss-vfx/: one sheet per boss,
    /// a row per signature move, columns = the weapon trail mid-swing, the contact (hit or guard), eight frames after it, and a late frame for
    /// the residue. Rendered with a live <see cref="Vfx"/> on real frames against a dark rooftop grey, so the black ink must read too. Needs a GPU:
    /// it skips itself under -nographics. Run: <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter SubBossVfxScreenshots</c>.
    /// </summary>
    [Category("Screenshots")]
    public class SubBossVfxScreenshots
    {
        const int W = 480, H = 360, Cols = 4;

        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "subboss-vfx"));

        struct Scene
        {
            public string ability; public bool phaseTwo, guard, ranged;
            public Scene(string ability, bool phaseTwo = false, bool guard = false, bool ranged = false)
            { this.ability = ability; this.phaseTwo = phaseTwo; this.guard = guard; this.ranged = ranged; }
        }

        static readonly (string boss, Scene[] scenes)[] Boards =
        {
            (Kuroe.Id, new[]
            {
                new Scene(KuroeAbilities.SumiNidanId), new Scene(KuroeAbilities.SumiNidanId, phaseTwo: true), new Scene(KuroeAbilities.SumiBaraiId, guard: true),
                new Scene(KuroeAbilities.BokusekiWaveId, ranged: true), new Scene(KuroeAbilities.ThunderRedactionId, phaseTwo: true, ranged: true),
            }),
            (Garan.Id, new[]
            {
                new Scene(GaranAbilities.GedanComboId), new Scene(GaranAbilities.TsuppariId, guard: true), new Scene(GaranAbilities.ShikoStompId),
                new Scene(GaranAbilities.StormFistId, ranged: true), new Scene(GaranAbilities.BrokenPalmId, phaseTwo: true),
            }),
            (Hisame.Id, new[]
            {
                new Scene(HisameAbilities.KasumiMaiId), new Scene(HisameAbilities.RainGustId, ranged: true), new Scene(HisameAbilities.LightningCutId, guard: true),
                new Scene(HisameAbilities.InazumaStepId, phaseTwo: true, ranged: true), new Scene(HisameAbilities.SmokeBombId, phaseTwo: true, ranged: true),
            }),
            (Shiden.Id, new[]
            {
                new Scene(ShidenAbilities.MakiOtoshiId), new Scene(ShidenAbilities.SojutsuSweepId, guard: true), new Scene(ShidenAbilities.VanguardBreakId, ranged: true),
                new Scene(ShidenAbilities.RaijinLanceId, phaseTwo: true, ranged: true), new Scene(ShidenAbilities.ChargeId, ranged: true),
            }),
        };

        [UnityTest]
        public IEnumerator RenderEachSubBossLook()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            float prevCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            var written = new List<string>();
            try
            {
                foreach (var (boss, scenes) in Boards)
                {
                    var sheet = new Texture2D(W * Cols, H * scenes.Length, TextureFormat.RGBA32, false);
                    var bg = new Color32[W * Cols * H * scenes.Length];
                    for (int k = 0; k < bg.Length; k++) bg[k] = new Color32(26, 26, 30, 255);       // an empty tile: no such frame (darker than the stage)
                    sheet.SetPixels32(bg);
                    for (int row = 0; row < scenes.Length; row++)
                    {
                        var shots = new Color32[Cols][];
                        yield return Play(boss, scenes[row], shots);
                        for (int c = 0; c < Cols; c++)
                            if (shots[c] != null) sheet.SetPixels32(c * W, (scenes.Length - 1 - row) * H, W, H, shots[c]);
                    }
                    sheet.Apply();
                    string path = Path.Combine(Dir, boss + "_look.png");
                    File.WriteAllBytes(path, sheet.EncodeToPNG());
                    Object.DestroyImmediate(sheet);
                    written.Add(path);
                }
            }
            finally { Time.captureDeltaTime = prevCapture; }
            foreach (var p in written) Assert.IsTrue(File.Exists(p), p);
        }

        [Test]
        public void RenderSubBossHeightComparison()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Requires a graphics device");
            const int width = 720, height = 480;
            var sheet = new Texture2D(width * 2, height * 2, TextureFormat.RGBA32, false);
            try
            {
                for (int i = 0; i < Boards.Length; i++)
                {
                    string id = Boards[i].boss;
                    using var fight = new HeadlessFight(52, Foe.Scripted, -1.35f, 1.35f, foeDef: CharacterCatalog.Get(id));
                    fight.KageInput.Add(40);
                    fight.RoninInput.Add(40);
                    fight.Run(30);
                    var camera = fight.Root.AddComponent<CameraRig>();
                    camera.enabled = false;
                    EnvBackdrop.Build(fight.Root.transform, camera, EnvLook.For(BossArena.EnvFor(id)));
                    var pixels = Render(new Vector2(0f, 1.55f), 1.72f, width, height, true);
                    sheet.SetPixels32(i % 2 * width, (1 - i / 2) * height, width, height, pixels);
                    float headHeight = fight.Ronin.rig.World(B.Head, Vector2.zero).y - fight.Ronin.Pos.y;
                    float kageHeight = fight.Kage.rig.World(B.Head, Vector2.zero).y - fight.Kage.Pos.y;
                    Debug.Log($"{id} idle head height relative to Kage: {headHeight / kageHeight:F2}");
                    Assert.Greater(headHeight / kageHeight, 1.14f, id);
                }
                sheet.Apply();
                Directory.CreateDirectory(Dir);
                File.WriteAllBytes(Path.Combine(Dir, "boss_height_comparison.png"), sheet.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(sheet); }
        }

        [Test]
        public void RenderArticulatedBossPoses()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "Requires a graphics device");
            const int columns = 5, tileW = 720, tileH = 560;
            var board = new Texture2D(tileW * columns, tileH * Boards.Length, TextureFormat.RGBA32, false);
            try
            {
                for (int row = 0; row < Boards.Length; row++)
                {
                    string id = Boards[row].boss;
                    using var fight = new HeadlessFight(52, Foe.Scripted, -6f, 2f, foeDef: CharacterCatalog.Get(id));
                    var boss = fight.Ronin;
                    var stageCamera = fight.Root.AddComponent<CameraRig>();
                    stageCamera.enabled = false;
                    EnvBackdrop.Build(fight.Root.transform, stageCamera, EnvLook.For(BossArena.EnvFor(id)));
                    fight.KageInput.Add(240);
                    fight.RoninInput.Add(28).Add(11, fwd: 1).Add(6, fwd: 1, dash: true)
                        .Add(55).Add(6, buttons: "G").Add(8).Add(1, buttons: "S").Add(100);

                    void Capture(int col)
                    {
                        var bounds = new Bounds(boss.Pos, Vector3.zero);
                        foreach (var renderer in boss.rig.Renderers)
                            if (renderer.enabled) bounds.Encapsulate(renderer.bounds);
                        float size = Mathf.Max(1.16f, bounds.extents.y, bounds.extents.x * tileH / tileW) + 0.10f;
                        board.SetPixels32(col * tileW, (Boards.Length - row - 1) * tileH, tileW, tileH,
                            Render(bounds.center, size, tileW, tileH, true));
                    }
                    fight.Run(28);
                    Assert.AreEqual(FState.Idle, boss.state, id);
                    Capture(0);
                    fight.Run(11);
                    Assert.AreEqual(FState.Walk, boss.state, id);
                    Capture(1);
                    fight.Run(6);
                    Assert.AreEqual(FState.Dash, boss.state, id);
                    Capture(2);
                    fight.Run(59);
                    Assert.IsTrue(boss.InParryWindow, id);
                    Capture(3);
                    fight.Run(11);
                    Assert.IsTrue(boss.IsAttacking, id);
                    fight.RunUntil(() => boss.moveFrame >= boss.move.hits[0].from, 60);
                    Assert.IsTrue(boss.IsAttacking, id);
                    Capture(4);
                }
                board.Apply();
                Directory.CreateDirectory(Dir);
                File.WriteAllBytes(Path.Combine(Dir, "boss_poses.png"), board.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(board); }
        }

        static IEnumerator Play(string boss, Scene s, Color32[][] shots)
        {
            var vfxGo = new GameObject("ReviewVfx");
            vfxGo.AddComponent<Vfx>();
            var def = CharacterCatalog.Get(boss);
            int slot = def.abilities.IndexOf(s.ability) + 1;
            Assert.Greater(slot, 0, boss + " has " + s.ability);
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, s.ranged ? -2.3f : -0.6f, s.ranged ? 1.4f : 0.9f, foeDef: def);
            f.Kage.maxHealth = f.Kage.health = 1e6f;
            if (s.phaseTwo) f.Ronin.health = f.Ronin.maxHealth * 0.5f;
            f.RoninInput.Add(s.phaseTwo ? 40 : 2).Add(1, ability: slot).Add(600);
            if (s.guard) f.KageInput.Add(600, buttons: "G"); else f.KageInput.Add(600);

            int contact = -1, trailShot = -1, started = -1;
            bool wasStunned = false;
            for (int i = 0; i < 170; i++)
            {
                f.Run(1);
                yield return null;
                Vector2 centre = (f.Kage.Pos + f.Ronin.Pos) * 0.5f + new Vector2(0f, 1.1f);
                float size = s.ranged ? 2.3f : 1.8f;
                var r = f.Ronin;
                if (started < 0 && r.state == FState.Attack && r.move != null) started = i;
                if (trailShot < 0 && r.state == FState.Attack && r.move != null)
                {
                    foreach (var e in r.move.fx)
                    {
                        if (e.kind is not (FxKind.TrailBlade or FxKind.TrailBladeN or FxKind.TrailFist or FxKind.TrailFootN or FxKind.TrailFootF)) continue;
                        if (r.moveFrame >= e.from + Mathf.Max(2, (e.to - e.from) / 2)) { trailShot = i; shots[0] = Render(centre, size); }
                        break;
                    }
                }
                bool stunned = f.Kage.state is FState.Hitstun or FState.Blockstun or FState.Launched or FState.Down;
                if (contact < 0 && stunned && !wasStunned && started >= 0) { contact = i; shots[1] = Render(centre, size); }
                wasStunned = stunned;
                if (contact >= 0 && i == contact + 8) shots[2] = Render(centre, size);
                if (contact >= 0 && i == contact + 30) { shots[3] = Render(centre, size); break; }
                // no contact (a whiffed or delayed move): keep a timed view of what it drew
                if (contact < 0 && started >= 0 && i == started + 60) shots[2] = Render(centre, size);
                if (contact < 0 && started >= 0 && i == started + 90) shots[3] = Render(centre, size);
            }
            f.Dispose();
            Object.DestroyImmediate(vfxGo);
            yield return null;
        }

        static Color32[] Render(Vector2 centre, float orthoSize, int width = W, int height = H, bool smooth = false)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.2f, 0.23f, 1f);       // a dark rooftop grey: the ink must read on it, the light must not blow out
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3(centre.x, centre.y, -10f);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = smooth;
                data.antialiasing = smooth ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
            }
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
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
