using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Camera framing in a group fight (SAN-83): <see cref="CameraRig"/> framing a fight (<see cref="CameraRig.Frame"/>) keeps
    /// Kage and the engaged foes on screen, checked through the camera's own projection (WorldToViewportPoint) every frame of
    /// a long fight. The rig follows on a fixed 1/60 s step (<see cref="CameraRig.Follow"/>), one per fight frame. Shake and
    /// the zoom punch are left out (they are momentary effects, not the framing).
    /// </summary>
    public class EngagementCameraTests
    {
        static readonly float[] FourFootmen = { 1.3f, -2.4f, 2.8f, -3.8f };

        GroupFight _fight;
        GameObject _camGo;

        [TearDown]
        public void TearDown()
        {
            _fight?.Dispose();
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            GameFeel.trauma = GameFeel.punch = 0f;
        }

        CameraRig Rig(FightContext ctx, Fighter focus)
        {
            _camGo = new GameObject("TestCamera");
            var cam = _camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.aspect = 16f / 9f;
            cam.enabled = false;        // never rendered; only its projection is used
            var rig = _camGo.AddComponent<CameraRig>();
            rig.cam = cam;
            rig.Frame(ctx, focus);
            rig.Snap();
            return rig;
        }

        /// <summary>The fighter's body, feet to head and a shoulder's width each side, inside the viewport.</summary>
        static void AssertOnScreen(Camera cam, Fighter f, string what)
        {
            Vector2 p = f.Pos;
            foreach (var corner in new[] { p + new Vector2(-0.35f, 0f), p + new Vector2(0.35f, 0f), p + new Vector2(-0.35f, 1.8f), p + new Vector2(0.35f, 1.8f) })
            {
                Vector3 v = cam.WorldToViewportPoint(corner);
                Assert.That(v.x, Is.InRange(0f, 1f), $"{what}: {f.FighterId} x {p.x:0.00} (camera x {cam.transform.position.x:0.00}, size {cam.orthographicSize:0.00})");
                Assert.That(v.y, Is.InRange(0f, 1f), $"{what}: {f.FighterId} y {p.y:0.00}");
            }
        }

        [Test]
        public void The_camera_keeps_Kage_and_the_engaged_foes_on_screen()
        {
            var d = CharacterCatalog.Get(KageCharacter.Id);
            d.damageMul = 4f;
            d.healthMul = 20f;
            _fight = new GroupFight(83, 0f, FourFootmen, kageAi: true, kageDef: d, aiLevel: 0.9f);
            var rig = Rig(_fight.Ctx, _fight.Kage);
            var cam = rig.cam;
            // the camera eases towards a newly engaged foe; give it the time it takes (its follow rate is 6 a second)
            const int Settle = 30;
            var engagedFor = new Dictionary<Fighter, int>();
            int checkedFoes = 0;
            for (int i = 0; i < 3600; i++)
            {
                _fight.Run(1);
                GameFeel.trauma = GameFeel.punch = 0f;
                rig.Follow(1f / 60f);
                AssertOnScreen(cam, _fight.Kage, $"frame {i}");
                foreach (var foe in _fight.Foes)
                {
                    bool engaged = _fight.Ctx.Engagement.IsEngaged(foe);
                    engagedFor[foe] = engaged ? (engagedFor.TryGetValue(foe, out var n) ? n + 1 : 1) : 0;
                    if (engaged && engagedFor[foe] > Settle) { AssertOnScreen(cam, foe, $"frame {i}"); checkedFoes++; }
                }
            }
            Assert.Greater(checkedFoes, 3600, "engaged foes were framed for most of the fight");
            Assert.Greater(_fight.Log.Count(CombatEventType.KnockedOut), 0, "places changed hands during the fight");
        }

        [Test]
        public void With_nobody_engaged_the_camera_frames_Kage()
        {
            _fight = new GroupFight(83, 0f, FourFootmen, foesStill: true);
            foreach (var foe in _fight.Foes) foe.gameObject.SetActive(false);
            var rig = Rig(_fight.Ctx, _fight.Kage);
            _fight.Run(2);
            rig.Follow(1f / 60f);
            CollectionAssert.IsEmpty(_fight.Ctx.Engagement.Engaged);
            AssertOnScreen(rig.cam, _fight.Kage, "alone");
            Assert.AreEqual(rig.minSize, rig.cam.orthographicSize, 1e-3f, "the closest framing");
        }
    }
}
