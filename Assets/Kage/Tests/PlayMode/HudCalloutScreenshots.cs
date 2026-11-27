using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// Visual review captures and live integration checks for the compact, world-anchored Japanese
    /// brush-calligraphy combat callouts, written to <c>Screenshots/review/hud_callouts/</c>.
    /// </summary>
    [Category("Screenshots")]
    public class HudCalloutScreenshots
    {
        const int W = 1600, H = 900;
        static string Dir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots", "review", "hud_callouts"));

        [UnityTest]
        public IEnumerator Render_combat_callout_review_shots()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            Directory.CreateDirectory(Dir);

            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.45f, 0.45f);
            f.Run(24);

            var camGo = new GameObject("CalloutShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 1.65f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.14f, 0.10f, 0.11f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.position = new Vector3((f.Kage.Pos.x + f.Ronin.Pos.x) * 0.5f, 1.25f, -10f);
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; }

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var hudGo = new GameObject("HUD", typeof(RectTransform));

            float prevCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            try
            {
                var hud = hudGo.AddComponent<Hud>();
                hud.Build(cam);
                hud.DevText = false;
                hud.SetNames("KAGE", "RONIN");
                hud.BindEvents(f.Ctx.Events);
                var canvas = hudGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;

                f.Kage.meter = 72f;
                f.Kage.comboCount = 5;
                f.Kage.roundsWon = 1;
                f.Ronin.health = f.Ronin.maxHealth * 0.58f;

                Vector2 bladeClash = new Vector2((f.Kage.Pos.x + f.Ronin.Pos.x) * 0.5f, 1.22f);

                void StepFrames(int n)
                {
                    for (int i = 0; i < n; i++)
                    {
                        hud.Tick(f.Kage, f.Ronin, 44.0f, 1, true);
                        Canvas.ForceUpdateCanvases();
                    }
                }

                void Capture(string file)
                {
                    Canvas.ForceUpdateCanvases();
                    cam.Render();
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    RenderTexture.active = prev;
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(Dir, file), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }

                StepFrames(20);
                yield return null;

                // 1) PARRY right where the blades meet + PERFECT PARRY gold variant
                hud.ShowCallout(CombatCalloutKind.Parry, bladeClash);
                StepFrames(10);
                yield return null;
                Capture("01_parry_at_clash.png");

                StepFrames(70);

                hud.ShowCallout(CombatCalloutKind.PerfectParry, bladeClash);
                StepFrames(10);
                yield return null;
                Capture("02_perfect_parry_at_clash.png");

                // 2) Simultaneous PARRY + GUARD BREAK right at the clash point (lifted cleanly apart)
                hud.ShowCallout(CombatCalloutKind.GuardBreak, bladeClash);
                StepFrames(10);
                yield return null;
                Capture("03_stacked_guard_break_and_parry.png");
                Assert.AreEqual(2, hud.ActiveCalloutCount);

                StepFrames(70);
                Assert.AreEqual(0, hud.ActiveCalloutCount);

                // 3) DODGE right at Kage's evade point + CLASH at crossing blades
                hud.ShowCallout(CombatCalloutKind.Dodge, f.Kage.Pos + Vector2.up * 1.15f);
                StepFrames(12);
                yield return null;
                Capture("04_dodge_at_kage.png");

                StepFrames(70);

                hud.ShowCallout(CombatCalloutKind.Clash, bladeClash);
                StepFrames(10);
                yield return null;
                Capture("05_clash_at_blades.png");

                StepFrames(70);

                // 4) COUNTER and PARRIED warning at blade contact
                hud.ShowCallout(CombatCalloutKind.Counter, bladeClash);
                StepFrames(10);
                yield return null;
                Capture("06_counter_at_contact.png");

                StepFrames(70);

                hud.ShowCallout(CombatCalloutKind.Parried, bladeClash);
                StepFrames(10);
                yield return null;
                Capture("07_parried_at_contact.png");
            }
            finally
            {
                Time.captureDeltaTime = prevCapture;
                cam.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(hudGo);
                Object.DestroyImmediate(camGo);
            }
        }

        [UnityTest]
        public IEnumerator Live_combat_events_drive_world_anchored_callouts_at_contact_points()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.4f, 0.4f);
            f.Run(10);

            var camGo = new GameObject("LiveCalloutCam");
            var cam = camGo.AddComponent<Camera>();
            var hudGo = new GameObject("HUD", typeof(RectTransform));
            try
            {
                var hud = hudGo.AddComponent<Hud>();
                hud.Build(cam);
                hud.BindEvents(f.Ctx.Events);
                hud.Tick(f.Kage, f.Ronin, 60f, 1, true);
                yield return null;

                var bladePoint = new Vector2(0.05f, 1.25f);

                // Plain hit & block must NOT trigger a callout
                f.Ctx.Events.Publish(CombatEvent.HitResolved(11, CombatEventType.Hit, f.Kage, f.Ronin, null, "jab", 0, 8f, false, HitLevel.Mid, HitZone.Body, bladePoint, Vector2.zero));
                f.Ctx.Events.Publish(CombatEvent.HitResolved(12, CombatEventType.Blocked, f.Ronin, f.Kage, null, "jab", 0, 0f, false, HitLevel.Mid, HitZone.Body, bladePoint, Vector2.zero));
                Assert.AreEqual(0, hud.ActiveCalloutCount, "ordinary hits and blocks must stay silent");

                // Core combat events produce ALL-CAPS English callouts anchored at the exact contact point
                f.Ctx.Events.Publish(CombatEvent.HitResolved(13, CombatEventType.Parried, f.Ronin, f.Kage, null, "kick1", 0, 0f, false, HitLevel.Mid, HitZone.Body, bladePoint, Vector2.zero));
                Assert.AreEqual("PARRY", hud.LatestCalloutLabel);
                Assert.AreEqual(bladePoint, hud.Model.Callouts.Entries[0].WorldPos);

                f.Ctx.Events.Publish(CombatEvent.HitResolved(14, CombatEventType.PerfectDodged, f.Ronin, f.Kage, null, "kick1", 0, 0f, false, HitLevel.Mid, HitZone.Body, bladePoint, Vector2.zero));
                Assert.AreEqual("DODGE", hud.LatestCalloutLabel);

                f.Ctx.Events.Publish(CombatEvent.CounterHit(15, f.Kage, f.Ronin, "katana1", 0, 18f, bladePoint));
                Assert.AreEqual("COUNTER", hud.LatestCalloutLabel);

                f.Ctx.Events.Publish(CombatEvent.Clashed(16, f.Kage, f.Ronin, "katana1", 0, "katana1", 10f, bladePoint));
                Assert.AreEqual("CLASH", hud.LatestCalloutLabel);
                Assert.AreEqual(bladePoint, hud.Model.Callouts.Entries[0].WorldPos);

                f.Ctx.Events.Publish(CombatEvent.GuardBroken(17, f.Kage, f.Ronin, "katana3", 0, 60f, bladePoint));
                Assert.AreEqual("GUARD BREAK", hud.LatestCalloutLabel);

                f.Ctx.Events.Publish(CombatEvent.HitResolved(18, CombatEventType.Parried, f.Kage, f.Ronin, null, "katana1", 0, 0f, false, HitLevel.Mid, HitZone.Body, bladePoint, Vector2.zero));
                Assert.AreEqual("PARRIED", hud.LatestCalloutLabel);

                f.Ctx.Events.Publish(CombatEvent.GuardBroken(19, f.Ronin, f.Kage, "kick3", 0, 60f, bladePoint));
                Assert.AreEqual("GUARD BROKEN", hud.LatestCalloutLabel);

                hud.Tick(f.Kage, f.Ronin, 60f, 1, true);
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(hudGo);
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
