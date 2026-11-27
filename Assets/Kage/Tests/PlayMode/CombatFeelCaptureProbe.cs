using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    public class CombatFeelCaptureProbe
    {
        [UnityTest]
        public IEnumerator CaptureConnectedCleave()
        {
            var parent = new GameObject("Presentation review");
            var game = parent.AddComponent<KageGame>();
            float capture = Time.captureDeltaTime;
            try
            {
                Time.captureDeltaTime = 1f / 60f;
                yield return null;
                var a = game.player;
                var b = game.enemy;
                a.input = new ScriptedInput();
                b.input = new ScriptedInput();
                a.ResetForRound(-0.53f, 1f);
                b.ResetForRound(0.53f, -1f);
                a.BeginFight();
                b.BeginFight();
                bool landed = false;
                a.onHit += (_, target, info, result) => { if (result == HitResult.Hit) landed = true; };
                a.StartMove(KatanaMoves.Build().cleave);
                for (int i = 0; i < 100 && !landed; i++) yield return null;
                Assert.IsTrue(landed, "Cleave must connect for the review frame");
                yield return null;
                Capture(game.Cam.cam, "kage-feel-cleave.png");
                yield return new WaitForSecondsRealtime(0.3f);
                b.AddPosture(b.PostureMax, a);
                yield return null;
                Capture(game.Cam.cam, "kage-feel-break.png");
            }
            finally
            {
                Time.captureDeltaTime = capture;
                Object.DestroyImmediate(parent);
                foreach (var camera in Object.FindObjectsByType<Camera>()) Object.DestroyImmediate(camera.gameObject);
            }
        }

        static void Capture(Camera cam, string name)
        {
            var prev = RenderTexture.active;
            var target = RenderTexture.GetTemporary(800, 450, 24, RenderTextureFormat.DefaultHDR);
            var tex = new Texture2D(800, 450, TextureFormat.RGB24, false);
            cam.targetTexture = target;
            cam.Render();
            RenderTexture.active = target;
            tex.ReadPixels(new Rect(0, 0, 800, 450), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Path.GetTempPath(), name), tex.EncodeToPNG());
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(tex);
        }
    }
}
