using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// The review images of Raikon's thunder pass (docs/raikon-ascendant-vfx.md), written to Screenshots/review/raikon-thunder/: the real game (its
    /// camera, his storm arena, bloom) against Raikon, with his AI held still and moves started on cue, one or two frames per beat: the red aura in each
    /// phase, the backdrop storm's events, a cloud gathering and the bolt out of it (Storm Kindling, Ascension, Heavens' Verdict, the Final Overload),
    /// the lightning on a swing and a slam's thunder. Needs a GPU: it skips itself under -nographics. Run:
    /// <c>Unity.exe -batchmode -projectPath ... -runTests -testPlatform PlayMode -testFilter RaikonThunderScreenshots</c> (no -nographics).
    /// </summary>
    [Category("Screenshots")]
    public class RaikonThunderScreenshots
    {
        const int W = 1280, H = 720;
        GameObject _game;
        float _prevCapture;

        sealed class Hold : IInputSource
        {
            public void Sample() { }
            public FrameInput Poll(Fighter self) => default;
        }

        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "raikon-thunder"));

        [SetUp]
        public void SetUp() { _prevCapture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60f; }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = _prevCapture;
            if (_game != null) Object.DestroyImmediate(_game);
            KageGame.NextFoeId = null;
            foreach (var cam in Object.FindObjectsByType<Camera>()) Object.DestroyImmediate(cam.gameObject);
        }

        [UnityTest]
        public IEnumerator RenderTheThunderPass()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);
            KageGame.NextFoeId = Raikon.Id;
            _game = new GameObject("KAGE");
            var game = _game.AddComponent<KageGame>();
            yield return null;
            for (int i = 0; i < 30; i++) yield return null;
            var raikon = game.enemy;
            var kage = game.player;
            raikon.input = new Hold();
            var saved = new List<string>();
            void Keep() { kage.health = kage.maxHealth; if (raikon.health <= 0f) raikon.health = 1f; }

            // ---- phase 1: the smouldering aura, the distant storm, a swing's light lightning
            for (int i = 0; i < 40; i++) { Keep(); yield return null; }
            yield return Shot("p1_aura_idle", saved);
            raikon.GetComponent<RaikonSkyStorm>().Play(RaikonSkyStorm.StormEvent.FarBolt, 0.25f);
            for (int i = 0; i < 2; i++) { Keep(); yield return null; }
            yield return Shot("p1_far_bolt", saved);
            raikon.QueueForcedMove(RaikonAscendant.KesaGiri);
            yield return Until(raikon, RaikonAscendant.KesaGiri, 37, Keep);
            yield return Shot("p1_kesa_second_cut", saved);
            yield return Until(raikon, RaikonAscendant.KesaGiri, 66, Keep);
            yield return Shot("p1_kesa_cleave_slam", saved);
            for (int i = 0; i < 60; i++) { Keep(); yield return null; }

            // ---- phase 2: Storm Kindling's cloud and its bolt into the blade, the burning aura, the storm cleave
            raikon.health = raikon.maxHealth * 0.6f;
            yield return Until(raikon, RaikonAscendant.StormKindling, 22, Keep);
            yield return Shot("p2_kindling_cloud_gathers", saved);
            yield return Until(raikon, RaikonAscendant.StormKindling, 31, Keep);
            yield return Shot("p2_kindling_bolt_into_blade", saved);
            for (int i = 0; i < 90; i++) { Keep(); yield return null; }
            yield return Shot("p2_aura_idle", saved);
            raikon.QueueForcedMove(RaikonAscendant.KesaGiriStorm);
            yield return Until(raikon, RaikonAscendant.KesaGiriStorm, 66, Keep);
            yield return Shot("p2_kesa_storm_cleave", saved);
            for (int i = 0; i < 70; i++) { Keep(); yield return null; }

            // ---- phase 3: Ascension's storm breaking, the blazing aura, the backdrop's near strike, Heavens' Verdict out of its clouds
            raikon.health = raikon.maxHealth * 0.25f;
            yield return Until(raikon, RaikonAscendant.Ascension, 14, Keep);
            yield return Shot("p3_ascension_cloud_gathers", saved);
            yield return Until(raikon, RaikonAscendant.Ascension, 23, Keep);
            yield return Shot("p3_ascension_storm_breaks", saved);
            for (int i = 0; i < 90; i++) { Keep(); yield return null; }
            yield return Shot("p3_aura_idle", saved);
            raikon.GetComponent<RaikonSkyStorm>().Play(RaikonSkyStorm.StormEvent.NearStrike, -0.3f);
            for (int i = 0; i < 2; i++) { Keep(); yield return null; }
            yield return Shot("p3_near_strike", saved);
            raikon.QueueForcedMove(RaikonAscendant.HeavensVerdict);
            yield return Until(raikon, RaikonAscendant.HeavensVerdict, 26, Keep);
            yield return Shot("p3_verdict_cloud_over_kage", saved);
            yield return Until(raikon, RaikonAscendant.HeavensVerdict, 37, Keep);
            yield return Shot("p3_verdict_bolt", saved);
            for (int i = 0; i < 120; i++) { Keep(); yield return null; }

            // ---- the Final Overload: the great cloud over the pull, the release
            raikon.health = raikon.maxHealth * 0.1f;
            yield return Until(raikon, RaikonAscendant.FinalOverload, 40, Keep);
            yield return Shot("p4_overload_great_cloud", saved);
            yield return Until(raikon, RaikonAscendant.FinalOverload, 52, Keep);
            yield return Shot("p4_overload_release", saved);

            Assert.AreEqual(16, saved.Count, string.Join(", ", saved));
        }

        /// <summary>Steps frames until <paramref name="f"/> is in <paramref name="m"/> at move frame <paramref name="frame"/> or later (10 s at most).</summary>
        static IEnumerator Until(Fighter f, MoveDef m, int frame, System.Action each)
        {
            for (int i = 0; i < 600; i++)
            {
                if (f.state == FState.Attack && f.move == m && f.moveFrame >= frame) yield break;
                each();
                yield return null;
            }
            Assert.Fail($"{m.id} never reached frame {frame} (in {f.move?.id ?? "nothing"} at {f.moveFrame}, state {f.state})");
        }

        /// <summary>
        /// The game camera's own view (post-processing and all), rendered now into a texture. No WaitForEndOfFrame: it never fires in batchmode, and it
        /// hung the first run of this fixture.
        /// </summary>
        static IEnumerator Shot(string name, List<string> saved)
        {
            yield return null;
            var cam = Camera.main;
            Assert.IsNotNull(cam, "the game camera");
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(Dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            saved.Add(name);
        }
    }
}
