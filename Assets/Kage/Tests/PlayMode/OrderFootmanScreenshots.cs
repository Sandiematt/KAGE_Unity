using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage.Tests
{
    /// <summary>
    /// The review sheet for the Order Footman (SAN-69): Kage and the Footman side by side, from a headless fight, in idle,
    /// walk, attack and hit, plus a close-up of the Footman's head and body and his K.O. ragdoll. Written to
    /// Screenshots/review/SAN-69/. Needs a GPU: it skips itself under -nographics. Run:
    /// <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter OrderFootmanScreenshots</c> (no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class OrderFootmanScreenshots
    {
        const int W = 800, H = 600;

        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "SAN-69"));

        static CharacterDef Footman() => CharacterCatalog.Get("order_footman");

        [Test]
        public void RenderTheReviewSheet()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            var shots = new List<Color32[]>();

            // idle, walk and attack: both fighters do the same thing, so the two builds compare pose for pose; hit: Kage cuts the Footman
            shots.Add(Shot("idle", 30, -0.75f, 0.75f, (k, r) => { k.Add(60); r.Add(60); }));
            shots.Add(Shot("walk", 22, -1.5f, 1.5f, (k, r) => { k.Add(40, fwd: 1); r.Add(40, fwd: 1); }));
            shots.Add(Shot("attack", 15, -1.15f, 1.15f, (k, r) => { k.Add(1, buttons: "S").Add(60); r.Add(1, buttons: "S").Add(60); }));
            shots.Add(Shot("hit", 17, -0.5f, 0.5f, (k, r) => { k.Add(1, buttons: "S").Add(60); r.Add(60); }));      // Kage's iai cuts the Footman

            var sheet = new Texture2D(W * 2, H * 2, TextureFormat.RGBA32, false);
            for (int i = 0; i < shots.Count; i++)
            {
                var px = shots[i];
                int ox = (i % 2) * W, oy = (1 - i / 2) * H;
                sheet.SetPixels32(ox, oy, W, H, px);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(Dir, "kage_vs_footman_sheet.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            // the Footman alone, close up
            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: Footman()))
            {
                f.KageInput.Add(300);
                f.RoninInput.Add(300);
                f.Run(20);
                Save("footman_closeup", Render(f.Ronin.Pos + new Vector2(0f, 1.0f), 1.05f));
                Save("footman_head", Render(f.Ronin.Pos + new Vector2(-0.05f, 1.62f), 0.38f));
            }

            // the K.O. ragdoll
            using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.4f, 0.4f, foeDef: Footman()))
            {
                f.Ronin.health = 1f;
                f.RoninInput.Add(300);
                f.KageInput.Add(1, dy: -1, buttons: "K");
                f.RunUntil(() => f.Ronin.state == FState.KO, 60);
                f.Run(14);
                Save("footman_ko_air", Render(f.Ronin.FallBody(B.Hips).position, 1.3f));
                f.Run(130);
                Save("footman_ko", Render(f.Ronin.FallBody(B.Hips).position + new Vector2(0f, 0.2f), 1.3f));
            }

            Assert.IsTrue(File.Exists(Path.Combine(Dir, "kage_vs_footman_sheet.png")));
        }

        Color32[] Shot(string name, int frames, float kageX, float footX, System.Action<ScriptedInput, ScriptedInput> script)
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, footX, foeDef: Footman());
            script(f.KageInput, f.RoninInput);
            f.Run(frames);
            if (name == "hit")      // the hit flash only fades on rendered frames: let it, so the review shows the fighters' colours
            {
                f.Ronin.rig.Secondary(0.2f, Vector2.zero);
            }
            var px = Render((f.Kage.Pos + f.Ronin.Pos) * 0.5f + new Vector2(0f, 0.95f), 1.3f);
            Save("kage_vs_footman_" + name, px);
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
