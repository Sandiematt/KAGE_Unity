using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The perfect dodge (SAN-92, ADR-0004 step 2) and the world time scale it builds (ADR-0002). Fight-harness runs:
    /// the Ronin kicks (scripted; his kick reaches a forward-dashing body, his jab passes over it), Kage dashes or rolls a
    /// known number of frames before the kick would land. The landing frame is measured with Kage standing still, so no
    /// move timing is typed here; every dodge number is read from
    /// <see cref="DodgeTuning"/> (Resources/Tuning/dodge.json). Rolls and the one-per-dodge rule use the defence
    /// order's public step-2 seam (<see cref="Fighter.PreHit"/>) so they don't depend on the roll's reach.
    /// </summary>
    public class PerfectDodgeTests
    {
        readonly List<HeadlessFight> _fights = new();
        static DodgeTuning T => Tuning.Get<DodgeTuning>();

        /// <summary>The loop frame the Ronin presses the kick on.</summary>
        const int RoninPress = 14;

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        HeadlessFight Duel(float kageX = -0.37f, float roninX = 0.37f, CharacterDef kageDef = null)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, roninX, kageDef: kageDef);
            _fights.Add(f);
            return f;
        }

        /// <summary>The Ronin kicks on <see cref="RoninPress"/>; Kage presses <paramref name="kage"/> on loop frame <paramref name="at"/> (never, when negative).</summary>
        HeadlessFight Kicked(int at, System.Action<ScriptedInput> kage, CharacterDef kageDef = null, int frames = 60)
        {
            var f = Duel(kageDef: kageDef);
            f.RoninInput.Add(RoninPress).Add(1, buttons: "K").Add(600);
            if (at >= 0) { f.KageInput.Add(at); kage(f.KageInput); }
            f.KageInput.Add(600);
            f.Run(frames);
            return f;
        }

        static void Dash(ScriptedInput s) => s.Add(1, dash: true);

        /// <summary>The frame the kick lands on Kage standing still.</summary>
        int KickLands()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f);
            f.RoninInput.Add(RoninPress).Add(1, buttons: "K").Add(600);
            f.KageInput.Add(600);
            f.RunUntil(() => f.Log.Count(CombatEventType.Hit) > 0, 120);
            return f.Events(CombatEventType.Hit).Single().Frame;
        }

        static HitInfo Incoming(Fighter attacker) => new()
        {
            attacker = attacker,
            w = new HitWindow { dmg = 6, level = HitLevel.Mid, kb = new Vector2(2f, 0f), stun = 16 },
            dmg = 6, kb = new Vector2(-2f, 0f), zone = HitZone.Body, contact = new Vector2(-0.3f, 1f), moveId = "test.hit", window = 0,
        };

        // ---------------------------------------------------------------- the window

        [Test]
        public void A_dash_started_inside_the_window_perfect_dodges_the_hit()
        {
            int lands = KickLands();
            var f = Kicked(lands - 3, Dash);

            var e = f.Events(CombatEventType.PerfectDodged).Single();
            Assert.AreSame(f.Ronin, e.Source);
            Assert.AreSame(f.Kage, e.Target);
            Assert.AreEqual("kick1", e.MoveId);
            Assert.AreEqual(0f, e.Damage);
            Assert.That(e.Frame, Is.InRange(lands - 3, lands), "the kick met him during the dash");
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(), "the kick never hit");
            Assert.AreEqual(f.Kage.maxHealth, f.Kage.health);
        }

        [Test]
        public void A_dash_started_outside_the_window_is_a_normal_dash()
        {
            int lands = KickLands();
            var f = Kicked(lands - (T.windowFrames + 3), Dash);

            Assert.AreEqual(0, f.Events(CombatEventType.PerfectDodged).Count());
            var hit = f.Events(CombatEventType.Hit).Single();
            Assert.AreSame(f.Kage, hit.Target, "a forward dash is hittable once the window has passed");
            Assert.Less(f.Kage.health, f.Kage.maxHealth);
            Assert.AreEqual(1f, f.Ctx.Clock.WorldScale, "nothing slowed");
        }

        [Test]
        public void A_roll_or_backflip_inside_the_window_perfect_dodges_and_one_outside_is_its_normal_invulnerability()
        {
            foreach (var (name, tap) in new[] { ("roll", true), ("backflip", false) })
            {
                var f = Duel(-2f, 2f);
                f.RoninInput.Add(600);
                f.KageInput.Add(2).Add(1, tapFwd: tap, tapBack: !tap).Add(600);
                f.RunUntil(() => f.Kage.DodgeAge == T.windowFrames, 30);
                Assert.AreEqual(name, f.Kage.move.id);

                var c = new DefenceCheck { info = Incoming(f.Ronin) };
                Assert.IsTrue(f.Kage.PreHit(ref c), $"{name}: the last frame of the window");
                Assert.AreEqual(HitResult.Dodged, c.result);
                f.Dispose();

                var g = Duel(-2f, 2f);
                g.RoninInput.Add(600);
                g.KageInput.Add(2).Add(1, tapFwd: tap, tapBack: !tap).Add(600);
                g.RunUntil(() => g.Kage.DodgeAge == T.windowFrames + 1, 30);
                var d = new DefenceCheck { info = Incoming(g.Ronin) };
                Assert.IsFalse(g.Kage.PreHit(ref d), $"{name}: one frame past the window");
                Assert.IsFalse(g.Kage.HittableBy(d.info.w), $"{name}: still its own invulnerability: a normal dodge");
            }
        }

        [Test]
        public void Standing_walking_and_guarding_never_perfect_dodge()
        {
            var f = Duel();
            f.RoninInput.Add(600);
            f.KageInput.Add(10).Add(10, fwd: -1).Add(10, buttons: "G");
            for (int i = 0; i < 30; i++)
            {
                f.Run(1);
                var c = new DefenceCheck { info = Incoming(f.Ronin) };
                Assert.IsFalse(f.Kage.PreHit(ref c), $"frame {i}, {f.Kage.state}");
            }
        }

        [Test]
        public void The_dash_perk_widens_the_window()
        {
            // a dash perk of 0.1 adds round(0.1 x 30) = 3 frames: 8 -> 11 with the shipped numbers
            var def = CharacterCatalog.Get(KageCharacter.Id);
            def.perks = new GearPerks { dash = 0.1f };
            int lands = KickLands();

            var plain = Kicked(lands - (T.windowFrames + 2), Dash);
            Assert.AreEqual(0, plain.Events(CombatEventType.PerfectDodged).Count(), "outside the plain window");
            plain.Dispose();       // one live fight at a time: fights share the physics scene and the hurtbox layer

            var perked = Kicked(lands - (T.windowFrames + 2), Dash, def);
            Assert.AreEqual(T.windowFrames + Mathf.RoundToInt(0.1f * T.dashBonusWindowFrames), perked.Kage.PerfectDodgeWindow);
            Assert.AreEqual(1, perked.Events(CombatEventType.PerfectDodged).Count(), "inside the perked window");
        }

        // ---------------------------------------------------------------- meter, event, once

        [Test]
        public void A_perfect_dodge_gains_the_tunings_meter_once()
        {
            int lands = KickLands();
            var f = Kicked(lands - 3, Dash, frames: 120);
            Assert.AreEqual(1, f.Events(CombatEventType.PerfectDodged).Count());
            Assert.AreEqual(12f, T.meterGain, "the JS game's +12 Rage");
            Assert.AreEqual(T.meterGain, f.Kage.meter, 1e-4f);
        }

        [Test]
        public void One_dash_perfect_dodges_once_and_the_next_dash_can_again()
        {
            var f = Duel(-2f, 2f);
            f.RoninInput.Add(600);
            f.KageInput.Add(2).Add(1, dash: true).Add(60).Add(1, dash: true).Add(600);
            f.RunUntil(() => f.Kage.state == FState.Dash, 10);

            var a = new DefenceCheck { info = Incoming(f.Ronin) };
            Assert.IsTrue(f.Kage.PreHit(ref a));
            float meter = f.Kage.meter;
            var b = new DefenceCheck { info = Incoming(f.Ronin) };
            Assert.IsFalse(f.Kage.PreHit(ref b), "a second hit in the same dash goes through the normal order");
            Assert.AreEqual(meter, f.Kage.meter, "no second meter gain");

            f.RunUntil(() => f.Kage.state != FState.Dash, 30);
            f.RunUntil(() => f.Kage.state == FState.Dash, 90);
            var c = new DefenceCheck { info = Incoming(f.Ronin) };
            Assert.IsTrue(f.Kage.PreHit(ref c), "the next dash is a new dodge");
            Assert.AreEqual(meter + T.meterGain, f.Kage.meter, 1e-4f);
        }

        [Test]
        public void The_event_reports_no_damage_even_after_an_earlier_hit()
        {
            int lands = KickLands();
            var f = Duel();
            f.RoninInput.Add(RoninPress).Add(1, buttons: "K").Add(600);
            f.KageInput.Add(lands - 3).Add(1, dash: true).Add(600);
            f.Run(6);
            var hit = Incoming(f.Ronin);
            hit.w.stun = 1; hit.w.kb = Vector2.zero; hit.kb = Vector2.zero;
            f.Kage.ReceiveHit(hit);
            Assert.Greater(f.Kage.LastDamageTaken, 0f);
            float health = f.Kage.health;
            f.Run(60);

            Assert.AreEqual(0f, f.Events(CombatEventType.PerfectDodged).Single().Damage);
            Assert.AreEqual(health, f.Kage.health);
        }

        // ---------------------------------------------------------------- player only

        [Test]
        public void An_enemy_never_perfect_dodges()
        {
            // Kage jabs; the Ronin dashes in just before it lands
            int lands;
            using (var r = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f))
            {
                r.RoninInput.Add(600);
                r.KageInput.Add(RoninPress).Add(1, buttons: "P").Add(600);
                r.RunUntil(() => r.Log.Count(CombatEventType.Hit) > 0, 120);
                lands = r.Events(CombatEventType.Hit).Single().Frame;
            }
            var f = Duel();
            f.KageInput.Add(RoninPress).Add(1, buttons: "P").Add(600);
            f.RoninInput.Add(lands - 3).Add(1, dash: true).Add(600);
            f.RunUntil(() => f.Ronin.state == FState.Dash, 60);
            var c = new DefenceCheck { info = Incoming(f.Kage) };
            Assert.IsFalse(f.Ronin.PreHit(ref c), "a foe's dash is never a perfect dodge");
            f.Run(60);

            Assert.AreEqual(0, f.Events(CombatEventType.PerfectDodged).Count());
            Assert.AreSame(f.Ronin, f.Events(CombatEventType.Hit).Single().Target);
            Assert.AreEqual(1f, f.Ctx.Clock.WorldScale);
        }

        // ---------------------------------------------------------------- the world time scale

        [Test]
        public void The_perfect_dodge_slows_the_world_for_its_frames_but_not_Kage()
        {
            int lands = KickLands();
            var f = Kicked(lands - 3, Dash, frames: 0);
            f.RunUntil(() => f.Log.Count(CombatEventType.PerfectDodged) > 0, 60);
            var clock = f.Ctx.Clock;
            Assert.AreEqual(T.worldScale, clock.WorldScale);
            Assert.AreSame(f.Kage, clock.WorldExempt);
            Assert.AreEqual(T.slowFrames, clock.WorldFramesLeft);

            // 10 frames: Kage takes all 10, the Ronin's kick 10 x 0.4 = 4
            int kage0 = f.Kage.stateFrame, ronin0 = f.Ronin.stateFrame;
            Assert.AreEqual(FState.Dash, f.Kage.state);
            Assert.AreEqual(FState.Attack, f.Ronin.state);
            f.Run(5);
            Assert.AreEqual(kage0 + 5, f.Kage.stateFrame, "Kage runs every frame");
            f.Run(5);
            Assert.AreEqual(ronin0 + 4, f.Ronin.stateFrame, "the Ronin's kick plays at 0.4");

            f.Run(T.slowFrames - 10);
            Assert.AreEqual(0, clock.WorldFramesLeft, "the slow frames are spent");
            f.Run(1);
            Assert.AreEqual(1f, clock.WorldScale, "over after the slow frames");
            Assert.IsNull(clock.WorldExempt);
            f.RunUntil(() => f.Ronin.state == FState.Idle, 60);
            int r = f.Ronin.stateFrame;
            f.Run(3);
            Assert.AreEqual(r + 3, f.Ronin.stateFrame, "full speed again");
        }

        HeadlessFight Apart() { var f = Duel(-3f, 3f); return f; }

        [Test]
        public void A_slowed_fighter_walks_at_the_world_scale_while_Kage_walks_at_full_speed()
        {
            // both walk backward, away from each other (a back walk never breaks into a run)
            var f = Apart();
            f.KageInput.Add(600, fwd: -1);
            f.RoninInput.Add(600, fwd: -1);
            f.Run(30);      // both at walking speed
            f.Ctx.Clock.SlowWorld(0.4f, -1, f.Kage);
            float k0 = f.Kage.Pos.x, r0 = f.Ronin.Pos.x;
            f.Run(30);
            float kage = k0 - f.Kage.Pos.x, ronin = f.Ronin.Pos.x - r0;
            Assert.Greater(kage, 0.5f);
            Assert.AreEqual(0.4f, ronin / kage, 0.03f, "the Ronin covers 0.4 of Kage's distance");
            Assert.AreEqual(f.Kage.LogicVelocity.x, -f.Ronin.LogicVelocity.x, 0.05f, "both still walk at their own speed in their own time");
        }

        [Test]
        public void A_slowed_jump_reaches_the_same_height_and_lasts_longer()
        {
            (float peak, int air) Jump(float scale)
            {
                var f = Apart();
                f.KageInput.Add(600);
                f.RoninInput.Add(5).Add(1, dy: 1).Add(600);
                if (scale < 1f) f.Ctx.Clock.SlowWorld(scale, -1, f.Kage);
                float peak = 0f;
                int air = 0;
                for (int i = 0; i < 400; i++)
                {
                    f.Run(1);
                    peak = Mathf.Max(peak, f.Ronin.Pos.y);
                    if (f.Ronin.Pos.y > 0.1f) air++;
                    else if (air > 0) break;
                }
                return (peak, air);
            }
            var normal = Jump(1f);
            var slow = Jump(0.5f);
            Assert.Greater(normal.peak, 0.5f);
            Assert.AreEqual(normal.peak, slow.peak, 0.06f, "gravity scales with the square of the world scale");
            Assert.AreEqual(normal.air * 2f, slow.air, 6f, "twice as long in the air at 0.5");
        }

        [Test]
        public void A_frozen_world_holds_a_fighter_still_and_resume_gives_back_its_motion()
        {
            var f = Apart();
            f.KageInput.Add(600, fwd: -1);
            f.RoninInput.Add(600, fwd: -1);
            f.Run(30);
            float vx = f.Ronin.LogicVelocity.x;
            f.Ctx.Clock.SlowWorld(0f, -1, f.Kage);
            f.Run(1);
            Vector2 held = f.Ronin.Pos;
            int frame = f.Ronin.stateFrame;
            float k0 = f.Kage.Pos.x;
            f.Run(20);
            Assert.AreEqual(held.x, f.Ronin.Pos.x, 1e-4f);
            Assert.AreEqual(frame, f.Ronin.stateFrame, "no logic frames");
            Assert.Greater(k0 - f.Kage.Pos.x, 0.3f, "Kage walks on");

            f.Ctx.Clock.ResumeWorld();
            f.Run(2);
            Assert.AreEqual(vx, f.Ronin.LogicVelocity.x, 0.05f, "walking again at the same speed");
            Assert.Greater(f.Ronin.Pos.x, held.x);
        }

        static ProjectileDef Bolt() => new()
        {
            id = "testBolt",
            hit = new HitWindow { dmg = 5, level = HitLevel.Mid, kb = new Vector2(2f, 0.5f), stun = 18, hitstop = 4 },
            speed = 6f, radius = 0.1f, lifeFrames = 300,
        };

        [Test]
        public void Projectiles_fly_at_their_owners_time_scale()
        {
            var f = Apart();
            f.KageInput.Add(600);
            f.RoninInput.Add(600);
            f.Run(2);
            // high above both fighters, so nothing is hit
            var foe = Projectile.Spawn(f.Ctx, f.Ronin, Bolt(), new Vector2(0f, 4f), -1f);
            var own = Projectile.Spawn(f.Ctx, f.Kage, Bolt(), new Vector2(0f, 5f), 1f);
            f.Ctx.Clock.SlowWorld(0.5f, -1, f.Kage);
            f.Run(20);
            Assert.AreEqual(2f, own.Position.x, 0.01f, "Kage's own: 20 frames at 6 m/s");
            Assert.AreEqual(-1f, foe.Position.x, 0.01f, "the Ronin's: half as far");
            Assert.AreEqual(20, own.Age);
            Assert.AreEqual(10, foe.Age, "its life counts the slowed world's frames");

            f.Ctx.Clock.SlowWorld(0f, -1, f.Kage);
            f.Run(10);
            Assert.AreEqual(-1f, foe.Position.x, 0.01f, "held still in a frozen world");
        }

        static HazardDef Shock(int telegraph) => new()
        {
            id = "testShock", telegraphFrames = telegraph, strikeFrames = 2, size = new Vector2(0.5f, 1f),
            hit = new HitWindow { dmg = 5, level = HitLevel.Unblockable, kb = new Vector2(1f, 1f), stun = 10 },
        };

        [Test]
        public void Hazards_count_their_frames_at_their_owners_time_scale()
        {
            var f = Apart();
            f.KageInput.Add(600);
            f.RoninInput.Add(600);
            var foe = Hazard.Spawn(f.Ctx, f.Ronin, Shock(10), new Vector2(0f, 0f));
            var own = Hazard.Spawn(f.Ctx, f.Kage, Shock(10), new Vector2(1f, 0f));
            f.Ctx.Clock.SlowWorld(0.5f, -1, f.Kage);
            f.Run(11);
            Assert.AreEqual(HazardPhase.Strike, own.Phase, "Kage's own strikes after its 10 telegraph frames");
            Assert.AreEqual(HazardPhase.Telegraph, foe.Phase);
            Assert.AreEqual(5, foe.Age, "11 frames at 0.5");
            f.Run(11);
            Assert.AreEqual(HazardPhase.Strike, foe.Phase);
        }

        // ---------------------------------------------------------------- determinism

        static (List<string> events, List<string> frames) DodgeFight(int lands, int unityRandomSeed)
        {
            UnityEngine.Random.InitState(unityRandomSeed);
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f);
            f.RoninInput.Add(RoninPress).Add(1, buttons: "K");      // then the AI takes over
            f.KageInput.Add(lands - 3).Add(1, dash: true).Add(14).Add(1, buttons: "P").Add(8).Add(1, buttons: "P")
                .Add(30).Add(1, buttons: "K").Add(40).Add(1, tapFwd: true).Add(60).Add(1, buttons: "S");
            f.Run(300);
            return (f.Log.Lines().ToList(), f.Frames.ToList());
        }

        [Test]
        public void A_fight_with_a_perfect_dodge_is_identical_every_run()
        {
            int lands = KickLands();
            var a = DodgeFight(lands, 1);
            var b = DodgeFight(lands, 777);
            Assert.That(a.events, Has.Some.Contains(" PerfectDodged "), "the fight has a perfect dodge");
            CollectionAssert.AreEqual(a.events, b.events);
            CollectionAssert.AreEqual(a.frames, b.frames);
        }
    }
}
