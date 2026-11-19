using NUnit.Framework;
using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The road camera (SAN-89): <see cref="CameraRig"/> framing Kage (<see cref="CameraRig.Frame"/>) with the session's limits
    /// (<see cref="CameraRig.SetBounds"/>) follows him down the road and stays within them. On a closed gate the limits are the gate
    /// span: the arena's left edge to the gate, less than a screenful past it. Checked through the camera's own projection on every
    /// frame of a whole run, on the fixed 1/60 s follow step.
    /// </summary>
    public class StretchCameraTests
    {
        StretchFight _fight;
        GameObject _camGo;

        [TearDown]
        public void TearDown()
        {
            _fight?.Dispose();
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            GameFeel.trauma = GameFeel.punch = 0f;
        }

        CameraRig Rig(StretchFight f)
        {
            _camGo = new GameObject("TestCamera");
            var cam = _camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.aspect = 16f / 9f;
            cam.enabled = false;        // never rendered; only its projection is used
            var rig = _camGo.AddComponent<CameraRig>();
            rig.cam = cam;
            rig.lookAhead = 2.0f;
            rig.SetBounds(f.Session);
            rig.Frame(f.Ctx, f.Kage);
            rig.Snap();
            return rig;
        }

        static void AssertOnScreen(Camera cam, Fighter fighter, string what)
        {
            Vector2 p = fighter.Pos;
            foreach (var corner in new[] { p + new Vector2(-0.35f, 0f), p + new Vector2(0.35f, 0f), p + new Vector2(-0.35f, 1.8f), p + new Vector2(0.35f, 1.8f) })
            {
                Vector3 v = cam.WorldToViewportPoint(corner);
                Assert.That(v.x, Is.InRange(0f, 1f), $"{what}: {fighter.FighterId} x {p.x:0.00} (camera x {cam.transform.position.x:0.00}, size {cam.orthographicSize:0.00})");
                Assert.That(v.y, Is.InRange(0f, 1f), $"{what}: {fighter.FighterId} y {p.y:0.00}");
            }
        }

        [Test]
        public void The_camera_follows_Kage_along_the_whole_road_and_stays_within_its_bounds()
        {
            _fight = new StretchFight(89, StretchData.DevFootmen());
            var f = _fight;
            var rig = Rig(f);
            var cam = rig.cam;
            var s = f.Session;
            const int Settle = 90;      // the follow's easing after the limits change
            float lastL = s.CameraLeft, lastR = s.CameraRight;
            int sinceChange = 0, checkedFrames = 0, closedChecked = 0, narrowChecked = 0;
            float maxCamX = float.MinValue, minCamX = float.MaxValue;
            bool done = f.RunUntil(() => s.Cleared || s.Failed, 14000, () =>
            {
                GameFeel.trauma = GameFeel.punch = 0f;
                rig.Follow(1f / 60f);
                if (!Mathf.Approximately(lastL, s.CameraLeft) || !Mathf.Approximately(lastR, s.CameraRight)) { sinceChange = 0; lastL = s.CameraLeft; lastR = s.CameraRight; }
                else sinceChange++;

                AssertOnScreen(cam, f.Kage, $"frame {f.Ctx.Clock.Frame}");
                float cx = cam.transform.position.x;
                float half = cam.orthographicSize * cam.aspect;
                maxCamX = Mathf.Max(maxCamX, cx); minCamX = Mathf.Min(minCamX, cx);
                if (sinceChange < Settle) return;
                checkedFrames++;
                float l = s.CameraLeft, r = s.CameraRight;
                if (r - l <= 2f * half + 0.05f)
                {
                    narrowChecked++;
                    Assert.AreEqual((l + r) * 0.5f, cx, 0.35f, $"frame {f.Ctx.Clock.Frame}: bounds narrower than the view: centred on them");
                }
                else
                {
                    Assert.GreaterOrEqual(cx - half, l - 0.2f, $"frame {f.Ctx.Clock.Frame}: the view's left edge past the bound");
                    Assert.LessOrEqual(cx + half, r + 0.2f, $"frame {f.Ctx.Clock.Frame}: the view's right edge past the bound");
                }
                if (s.GateClosed)
                {
                    closedChecked++;
                    Assert.GreaterOrEqual(cx + half, f.Kage.Pos.x - 1f, "on a closed gate the view still reaches Kage");
                }
            });
            Assert.IsTrue(done && s.Cleared);
            Assert.Greater(checkedFrames, 2000);
            Assert.Greater(closedChecked, 800, "frames on a closed gate were checked");
            Assert.Greater(maxCamX - minCamX, 15f, "the camera travelled down the road");
            Assert.GreaterOrEqual(maxCamX, (float)f.Session.Data.Length - 9f, "and to the end of it");
        }

        [Test]
        public void On_a_closed_gate_the_camera_never_shows_past_the_gate_span()
        {
            _fight = new StretchFight(89, StretchData.DevFootmen());
            var f = _fight;
            var rig = Rig(f);
            var cam = rig.cam;
            var enc = f.Session.Data.Encounters[0];
            float arenaL = (float)f.Session.Data.ArenaLeft(0), gate = (float)enc.Gate;
            Assert.IsTrue(f.RunUntil(() => f.Session.GateClosed, 1500, () => rig.Follow(1f / 60f)));
            int n = 0;
            f.RunUntil(() => !f.Session.GateClosed || f.Session.Failed, 4000, () =>
            {
                GameFeel.trauma = GameFeel.punch = 0f;
                rig.Follow(1f / 60f);
                if (++n < 90) return;
                float half = cam.orthographicSize * cam.aspect;
                float cx = cam.transform.position.x;
                if (gate - arenaL + 2f * StretchSession.CameraMargin > 2f * half)
                {
                    Assert.GreaterOrEqual(cx - half, arenaL - StretchSession.CameraMargin - 0.2f);
                    Assert.LessOrEqual(cx + half, gate + StretchSession.CameraMargin + 0.2f);
                }
            });
            Assert.Greater(n, 300, "a real fight at the gate");
        }

        [Test]
        public void A_camera_override_still_wins_over_the_road_framing()
        {
            _fight = new StretchFight(89, StretchData.DevFootmen());
            var rig = Rig(_fight);
            rig.useOverride = true;
            rig.overrideCenter = new Vector2(12f, 1.5f);
            rig.overrideSize = 2.2f;
            _fight.Run(30, () => rig.Follow(1f / 60f));
            for (int i = 0; i < 200; i++) rig.Follow(1f / 60f);
            Assert.AreEqual(12f, rig.transform.position.x, 0.05f, "a rage art's framing (SAN-82) is not clamped away or replaced");
            Assert.AreEqual(2.2f, rig.cam.orthographicSize, 0.05f);
        }

        [Test]
        public void The_camera_looks_ahead_of_Kage_while_he_walks_with_nobody_to_fight()
        {
            _fight = new StretchFight(89, StretchData.DevFootmen());
            var f = _fight;
            var rig = Rig(f);
            f.Run(100, () => rig.Follow(1f / 60f));
            Assert.AreEqual(StretchState.Walking, f.Session.State);
            Assert.Greater(rig.transform.position.x, f.Kage.Pos.x + 0.8f, "the view is ahead of him, down the road");
        }
    }
}
