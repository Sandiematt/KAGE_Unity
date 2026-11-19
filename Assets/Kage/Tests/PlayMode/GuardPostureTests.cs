using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kage.Tests
{
    /// <summary>
    /// The Guard button, posture and guard-break (SAN-86, ADR-0004 steps 5 to 7). Fight-harness runs drive both
    /// fighters with scripted input (Guard is "G"); a few checks use the <see cref="IHittable.ReceiveHit"/> seam with a
    /// plain hit, so the level rules and the posture numbers are shown without depending on a move's reach. Every
    /// number is read from <see cref="GuardTuning"/> (Resources/Tuning/guard.json), never typed here.
    /// </summary>
    public class GuardPostureTests
    {
        readonly List<HeadlessFight> _fights = new();
        static GuardTuning T => Tuning.Get<GuardTuning>();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        HeadlessFight Duel(float kageX = -0.37f, float roninX = 0.37f)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, roninX);
            _fights.Add(f);
            return f;
        }

        static HitInfo Incoming(Fighter attacker, HitLevel level, int dmg = 6, float postureMul = 1f, bool sword = false, bool crit = false) => new()
        {
            attacker = attacker,
            w = new HitWindow { dmg = dmg, level = level, kb = new Vector2(2f, 0f), stun = 16, postureMul = postureMul, sword = sword },
            dmg = dmg, kb = new Vector2(-2f, 0f), zone = HitZone.Body, contact = new Vector2(-0.3f, 1f), crit = crit, moveId = "test.hit", window = 0,
        };

        /// <summary>Kage holds Guard (with Down when <paramref name="low"/>) and settles into the stance; the Ronin stands still.</summary>
        /// <summary>Frames to hold Guard before a hit so it is blocked, not parried: past the parry window (SAN-91).</summary>
        static int PastParry => Tuning.Get<ParryTuning>().windowFrames + 2;

        HeadlessFight Guarding(bool low = false, int hold = 600)
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.KageInput.Add(hold, dy: low ? -1 : 0, buttons: "G");
            f.Run(PastParry);
            Assert.IsTrue(f.Kage.IsGuarding, "Kage is in his guard");
            return f;
        }

        // ---------------------------------------------------------------- the Guard input

        [Test]
        public void Scripted_G_holds_Guard_for_the_whole_step_and_presses_it_on_the_first_frame()
        {
            var f = Duel();
            var s = new ScriptedInput().Add(3, buttons: "G").Add(1);
            var polled = Enumerable.Range(0, 4).Select(_ => s.Poll(f.Kage)).ToArray();
            CollectionAssert.AreEqual(new[] { true, true, true, false }, polled.Select(i => i.guard).ToArray());
            CollectionAssert.AreEqual(new[] { true, false, false, false }, polled.Select(i => i.guardPressed).ToArray());
            Assert.IsFalse(polled[0].Any, "Guard is not 'any button' (kip-up mashing)");
        }

        [Test]
        public void The_fighter_counts_frames_since_the_last_Guard_press_from_held_input_alone()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.KageInput.Add(10).Add(5, buttons: "G").Add(5);
            f.Run(10);
            Assert.Greater(f.Kage.FramesSinceGuardPress, 100, "never pressed yet");
            f.Run(1);
            Assert.AreEqual(0, f.Kage.FramesSinceGuardPress, "the press frame");
            f.Run(4);
            Assert.AreEqual(4, f.Kage.FramesSinceGuardPress, "holding is not pressing again");
            Assert.IsTrue(f.Kage.GuardHeld);
            f.Run(1);
            Assert.IsFalse(f.Kage.GuardHeld);
        }

        // ---------------------------------------------------------------- the stance

        [Test]
        public void Holding_Guard_stands_in_guard_and_ignores_horizontal_input()
        {
            var f = Duel(-1.5f, 1.5f);
            f.RoninInput.Add(1200);
            f.KageInput.Add(60, fwd: -1, buttons: "G");
            f.Run(30);
            float x = f.Kage.Pos.x;
            f.Run(20);
            Assert.AreEqual(FState.Idle, f.Kage.state);
            Assert.IsTrue(f.Kage.IsGuarding);
            Assert.AreEqual(x, f.Kage.Pos.x, 0.001f, "holding back with Guard doesn't walk");
        }

        [Test]
        public void Guard_with_Down_is_a_low_guard()
        {
            var f = Guarding(low: true);
            Assert.AreEqual(FState.Crouch, f.Kage.state);
            Assert.IsTrue(f.Kage.LowGuard);
        }

        [Test]
        public void An_attack_comes_straight_out_of_the_guard_and_ends_it()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.KageInput.Add(20, buttons: "G").Add(1, buttons: "PG").Add(40, buttons: "G");
            f.Run(21);
            Assert.AreEqual(FState.Attack, f.Kage.state);
            Assert.AreEqual("jab", f.Kage.move.id);
            Assert.IsFalse(f.Kage.IsGuarding, "an attack ends the guard");
        }

        [Test]
        public void No_guarding_in_the_air()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.KageInput.Add(1, dy: 1).Add(30, buttons: "G");
            f.Run(10);
            Assert.AreEqual(FState.Air, f.Kage.state);
            Assert.IsFalse(f.Kage.IsGuarding);
            Assert.AreEqual(HitResult.Hit, f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.High)));
        }

        // ---------------------------------------------------------------- the level rules

        [TestCase(HitLevel.High, false, HitResult.Blocked)]
        [TestCase(HitLevel.Mid, false, HitResult.Blocked)]
        [TestCase(HitLevel.Overhead, false, HitResult.Blocked)]
        [TestCase(HitLevel.Low, false, HitResult.Hit)]
        [TestCase(HitLevel.High, true, HitResult.Blocked)]
        [TestCase(HitLevel.Mid, true, HitResult.Blocked)]
        [TestCase(HitLevel.Low, true, HitResult.Blocked)]
        [TestCase(HitLevel.Overhead, true, HitResult.Hit)]
        [TestCase(HitLevel.Unblockable, false, HitResult.Hit)]
        [TestCase(HitLevel.Unblockable, true, HitResult.Hit)]
        public void Standing_guard_blocks_highs_low_guard_blocks_lows_overheads_beat_low_guard(HitLevel level, bool low, HitResult expected)
        {
            var f = Guarding(low);
            Assert.AreEqual(expected, f.Kage.ReceiveHit(Incoming(f.Ronin, level)));
        }

        [Test]
        public void Walking_backward_no_longer_guards()
        {
            var f = Duel(-1.5f, 1.5f);
            f.RoninInput.Add(1200);
            f.KageInput.Add(60, fwd: -1);
            f.Run(10);
            Assert.AreEqual(FState.Walk, f.Kage.state);
            Assert.IsFalse(f.Kage.IsGuarding);
            Assert.AreEqual(HitResult.Hit, f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.High)));
        }

        [Test]
        public void In_a_fight_holding_Guard_blocks_the_Ronins_jab()
        {
            var f = Duel();
            f.KageInput.Add(120, buttons: "G");
            f.RoninInput.Add(4).Add(1, buttons: "P").Add(200);
            f.Run(40);
            var blocked = f.Events(CombatEventType.Blocked).Single();
            Assert.AreEqual("ronin", blocked.SourceId);
            Assert.AreEqual("kage", blocked.TargetId);
            Assert.AreEqual("jab", blocked.MoveId);
            Assert.AreEqual(HitLevel.High, blocked.Level);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.TargetId == "kage"));
        }

        [Test]
        public void In_a_fight_the_same_jab_lands_on_a_fighter_who_walks_backward()
        {
            var f = Duel();
            f.KageInput.Add(120, fwd: -1);
            f.RoninInput.Add(1).Add(1, buttons: "P").Add(200);
            f.Run(40);
            Assert.AreEqual(0, f.Events(CombatEventType.Blocked).Count());
            Assert.AreEqual(1, f.Events(CombatEventType.Hit).Count(e => e.TargetId == "kage" && e.MoveId == "jab"));
        }

        [Test]
        public void In_a_fight_an_overhead_beats_the_low_guard()
        {
            var f = Duel(-0.4f, 0.4f);
            f.RoninInput.Add(200, dy: -1, buttons: "G");     // low guard
            f.KageInput.Add(10).Add(1, fwd: -1, buttons: "K");   // axe kick (overhead)
            f.Run(60);
            Assert.AreEqual(0, f.Events(CombatEventType.Blocked).Count());
            var hit = f.Events(CombatEventType.Hit).First(e => e.TargetId == "ronin");
            Assert.AreEqual("axe", hit.MoveId);
            Assert.AreEqual(HitLevel.Overhead, hit.Level);
        }

        // ---------------------------------------------------------------- posture

        [Test]
        public void A_guarded_hit_fills_posture_by_damage_times_the_factor_times_the_windows_postureMul()
        {
            var f = Guarding();
            Assert.AreEqual(0f, f.Kage.Posture);
            f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 6));
            Assert.AreEqual(6f * T.blockPostureFactor, f.Kage.Posture, 1e-4f);
            Assert.AreEqual(6f * T.blockPostureFactor, f.Kage.LastPostureTaken, 1e-4f);
            f.Run(20);
            float before = f.Kage.Posture;
            f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 4, postureMul: 2f));
            Assert.AreEqual(before + 4f * T.blockPostureFactor * 2f, f.Kage.Posture, 1e-4f, "HitWindow.postureMul multiplies it");
        }

        [Test]
        public void The_Blocked_event_carries_the_posture_added()
        {
            var f = Duel();
            f.KageInput.Add(120, buttons: "G");
            f.RoninInput.Add(4).Add(1, buttons: "P").Add(200);
            f.Run(40);
            var blocked = f.Events(CombatEventType.Blocked).Single();
            Assert.Greater(blocked.Posture, 0f);
            Assert.AreEqual(f.Kage.Posture, blocked.Posture, 1e-4f, "nothing drained yet");
        }

        [Test]
        public void A_clean_hit_adds_a_little_posture_never_past_full_and_never_breaks_the_guard()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.KageInput.Add(600);
            f.Run(8);
            f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 6));
            Assert.AreEqual(6f * T.hitPostureFactor, f.Kage.Posture, 1e-4f);
            Assert.Less(f.Kage.Posture, 6f * T.blockPostureFactor, "a clean hit adds less than a guarded one");

            f.Kage.PostureMax = 2f;
            f.Run(30);
            f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 20));
            Assert.AreEqual(2f, f.Kage.Posture, 1e-4f, "capped at the bar");
            Assert.IsFalse(f.Kage.GuardBroken);
            Assert.AreEqual(0, f.Events(CombatEventType.GuardBroken).Count());
        }

        [Test]
        public void Posture_waits_the_delay_then_drains_once_Guard_is_let_go()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.KageInput.Add(PastParry + 2, buttons: "G").Add(600);
            f.Run(PastParry);
            f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 6));
            float p = f.Kage.Posture;
            f.Run(T.drainDelayFrames);
            Assert.AreEqual(p, f.Kage.Posture, 1e-4f, "nothing drains within the delay");
            f.Run(10);
            Assert.AreEqual(p - 10 * T.drainPerFrame, f.Kage.Posture, 1e-3f, "then it drains every frame");
            f.Run(600);
            Assert.AreEqual(0f, f.Kage.Posture, "down to empty");
        }

        [Test]
        public void Posture_does_not_drain_while_Guard_is_held()
        {
            Assume.That(T.drainWhileGuarding, Is.False);
            var f = Guarding();
            f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 6));
            float p = f.Kage.Posture;
            f.Run(T.drainDelayFrames + 60);
            Assert.AreEqual(p, f.Kage.Posture, 1e-4f);
        }

        [Test]
        public void Posture_maximum_comes_from_character_data_and_bosses_have_more()
        {
            var f = Duel();
            Assert.AreEqual(CharacterCatalog.Get(KageCharacter.Id).postureMax, f.Kage.PostureMax);
            Assert.AreEqual(CharacterCatalog.Get(Ronin.Id).postureMax, f.Ronin.PostureMax);
            Assert.Greater(CharacterCatalog.Get(Raikon.Id).postureMax, CharacterCatalog.Get(OrderFootman.Id).postureMax);
        }

        // ---------------------------------------------------------------- guard-break

        [Test]
        public void A_full_bar_breaks_the_guard_for_the_break_time()
        {
            var f = Guarding();
            f.Kage.PostureMax = 10f;
            Assert.AreEqual(HitResult.Blocked, f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 4)));     // 6 of 10
            Assert.IsFalse(f.Kage.GuardBroken);
            f.Run(20);
            Assert.AreEqual(HitResult.Blocked, f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 4)), "the hit that breaks it is still guarded");
            Assert.IsTrue(f.Kage.GuardBroken);
            Assert.AreEqual(FState.Hitstun, f.Kage.state, "helpless");
            Assert.AreEqual(10f * T.postureAfterBreak, f.Kage.Posture, 1e-4f, "the bar refills to its share");

            f.Run(T.breakFrames - 1);
            Assert.IsTrue(f.Kage.GuardBroken);
            Assert.AreEqual(FState.Hitstun, f.Kage.state, "still helpless though Guard is held");
            f.Run(1);
            Assert.IsFalse(f.Kage.GuardBroken);
            f.Run(2);
            Assert.IsTrue(f.Kage.IsGuarding, "and back in guard");
        }

        [Test]
        public void The_next_hit_on_a_broken_guard_is_a_guaranteed_critical_and_spends_the_break()
        {
            var f = Guarding();
            f.Kage.PostureMax = 5f;
            f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 4));
            Assert.IsTrue(f.Kage.GuardBroken);
            f.Run(5);
            float hp = f.Kage.health;
            Assert.AreEqual(HitResult.Hit, f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 5)));
            Assert.IsTrue(f.Kage.LastHitCrit);
            Assert.AreEqual(Mathf.RoundToInt(5 * DamageCalc.CritMul), f.Kage.LastHitDamage);
            Assert.AreEqual(hp - Mathf.RoundToInt(5 * DamageCalc.CritMul), f.Kage.health, 1e-4f);
            Assert.IsFalse(f.Kage.GuardBroken, "spent");

            f.Run(5);
            f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 5));
            Assert.IsFalse(f.Kage.LastHitCrit, "only the next hit");
        }

        [Test]
        public void An_already_critical_hit_on_a_broken_guard_is_not_multiplied_twice()
        {
            var f = Guarding();
            f.Kage.PostureMax = 5f;
            f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 4));
            f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 9, crit: true));
            Assert.IsTrue(f.Kage.LastHitCrit);
            Assert.AreEqual(9, f.Kage.LastHitDamage);
        }

        [Test]
        public void In_a_fight_a_guard_break_publishes_GuardBroken_after_Blocked_and_the_next_hit_is_a_critical()
        {
            var f = Duel();
            f.Kage.PostureMax = 1f;          // the first guarded jab breaks it
            f.KageInput.Add(200, buttons: "G");
            f.RoninInput.Add(4).Add(1, buttons: "P").Add(7).Add(1, buttons: "P").Add(200);     // jab, then the cross
            f.Run(60);

            var all = f.Log.All.ToList();
            int blocked = all.FindIndex(e => e.Type == CombatEventType.Blocked);
            int broken = all.FindIndex(e => e.Type == CombatEventType.GuardBroken);
            Assert.GreaterOrEqual(blocked, 0);
            Assert.AreEqual(blocked + 1, broken, "GuardBroken follows the Blocked event of the hit that filled the bar");
            var b = all[broken];
            Assert.AreEqual(all[blocked].Frame, b.Frame);
            Assert.AreEqual("ronin", b.SourceId);
            Assert.AreEqual("kage", b.TargetId);
            Assert.AreEqual("jab", b.MoveId);
            Assert.AreEqual(1f, b.Posture, "the bar that filled");

            var crit = f.Events(CombatEventType.Hit).FirstOrDefault(e => e.TargetId == "kage");
            Assert.AreEqual(CombatEventType.Hit, crit.Type, string.Join(" || ", f.Log.Lines()) + " ## " + string.Join(" || ", f.Frames.Take(40)));
            Assert.AreEqual("cross", crit.MoveId);
            Assert.IsTrue(crit.Crit, "a guaranteed critical");
            var cross = f.Ronin.moves.All.First(m => m.id == "cross").hits[crit.Window];
            int plain = DamageCalc.For(f.Ronin, f.Kage, cross.dmg, false, true);
            Assert.AreEqual(Mathf.RoundToInt(plain * DamageCalc.CritMul), crit.Damage);
        }

        // ---------------------------------------------------------------- gear perks (SAN-84 hooks)

        /// <summary>Kage holding Guard, with these perks on his character data.</summary>
        HeadlessFight GuardingWith(GearPerks perks)
        {
            var def = CharacterCatalog.Get(KageCharacter.Id);
            def.perks = perks;
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f, kageDef: def);
            _fights.Add(f);
            f.RoninInput.Add(1200);
            f.KageInput.Add(600, buttons: "G");
            f.Run(PastParry);
            Assert.IsTrue(f.Kage.IsGuarding);
            return f;
        }

        [Test]
        public void A_block_builds_Rage_and_the_blockRage_perk_multiplies_it()
        {
            var plain = GuardingWith(default);
            var perk = GuardingWith(new GearPerks { blockRage = 1f });
            plain.Kage.meter = perk.Kage.meter = 0f;
            plain.Kage.ReceiveHit(Incoming(plain.Ronin, HitLevel.Mid, dmg: 10));
            perk.Kage.ReceiveHit(Incoming(perk.Ronin, HitLevel.Mid, dmg: 10));
            Assert.AreEqual(plain.Kage.BlockRageGain(10f), plain.Kage.meter, 1e-4f, "JS: damage x 0.4");
            Assert.Greater(plain.Kage.meter, 0f);
            Assert.AreEqual(2f * plain.Kage.meter, perk.Kage.meter, 1e-4f, "blockRage 1 doubles it");
        }

        [Test]
        public void The_stunRes_perk_shortens_the_guard_break()
        {
            var f = GuardingWith(new GearPerks { stunRes = 1f });
            f.Kage.PostureMax = 1f;
            f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 4));
            Assert.IsTrue(f.Kage.GuardBroken);
            int helpless = f.Kage.ScaleStun(T.breakFrames, Fighter.BreakStunResist);
            Assert.Less(helpless, T.breakFrames, "stunRes 1 cuts it (JS: x (1 - 0.5 stunRes))");
            f.Run(helpless - 1);
            Assert.IsTrue(f.Kage.GuardBroken);
            f.Run(1);
            Assert.IsFalse(f.Kage.GuardBroken, "over after the shortened time");
        }

        // ---------------------------------------------------------------- the AI

        sealed class Recording : IInputSource
        {
            readonly IInputSource _inner;
            public readonly List<FrameInput> Polled = new();
            public Recording(IInputSource inner) { _inner = inner; }
            public void Sample() => _inner.Sample();
            public FrameInput Poll(Fighter self) { var i = _inner.Poll(self); Polled.Add(i); return i; }
        }

        [Test]
        public void The_AI_guards_with_the_Guard_input_not_by_walking_back()
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Ai, -0.37f, 0.37f);
            _fights.Add(f);
            f.Ai.level = 1f;
            var rec = new Recording(f.Ai);
            f.Ronin.input = rec;
            var s = f.KageInput;
            for (int i = 0; i < 12; i++) s.Add(1, buttons: "P").Add(7).Add(1, buttons: "P").Add(30);
            f.Run(480);

            var guards = rec.Polled.Where(i => i.guard).ToList();
            Assert.IsNotEmpty(guards, "the AI guarded at least once");
            Assert.IsTrue(guards.All(i => i.dx == 0), "with no horizontal input");
            // the recording wrapper hides the AI from the fighter, so its guard may go up inside the parry window: a block or a parry
            Assert.Greater(f.Events(CombatEventType.Blocked).Count(e => e.TargetId == "ronin") + f.Events(CombatEventType.Parried).Count(e => e.TargetId == "ronin"), 0, "and it guarded a hit");
        }

        // ---------------------------------------------------------------- the HUD

        [Test]
        public void The_HUD_shows_both_posture_bars()
        {
            var f = Guarding();
            f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Mid, dmg: 6));
            f.Ronin.ReceiveHit(Incoming(f.Kage, HitLevel.Mid, dmg: 10));
            var cam = new GameObject("HudCam").AddComponent<Camera>();
            var go = new GameObject("HUD", typeof(RectTransform));
            try
            {
                var hud = go.AddComponent<Hud>();
                hud.Build(cam);
                hud.Tick(f.Kage, f.Ronin, 60f, 1, true);
                Assert.Greater(f.Kage.PostureFill, 0f);
                Assert.AreEqual(f.Kage.PostureFill, hud.PostureBar(false), 1e-3f, "Kage's bar, on the left");
                Assert.AreEqual(f.Ronin.PostureFill, hud.PostureBar(true), 1e-3f, "the opponent's bar, on the right");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(cam.gameObject);
            }
        }
    }

    /// <summary>The real input source: Guard from the keyboard and the gamepad, through <see cref="KeyBindings"/>.</summary>
    public class GuardInputTests : InputTestFixture
    {
        [Test]
        public void Keyboard_O_and_B_are_Guard_pressed_then_held()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            foreach (var key in new[] { kb.oKey, kb.bKey })
            {
                var src = new PlayerInputSource();
                Press(key);
                src.Sample();
                var first = src.Poll(null);
                Assert.IsTrue(first.guard && first.guardPressed, key.name + " pressed");
                InputSystem.Update();       // the next rendered frame: the key is still down, no longer just pressed
                src.Sample();
                var held = src.Poll(null);
                Assert.IsTrue(held.guard, key.name + " held");
                Assert.IsFalse(held.guardPressed, "a held key is not pressed again");
                Release(key);
                src.Sample();
                Assert.IsFalse(src.Poll(null).guard, key.name + " released");
            }
        }

        [Test]
        public void Keyboard_Guard_with_S_is_a_low_guard_input()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            var src = new PlayerInputSource();
            Press(kb.oKey);
            Press(kb.sKey);
            src.Sample();
            var i = src.Poll(null);
            Assert.IsTrue(i.guard);
            Assert.AreEqual(-1, i.dy);
        }

        [Test]
        public void Gamepad_RB_and_RT_are_Guard()
        {
            var gp = InputSystem.AddDevice<Gamepad>();
            foreach (var b in new[] { gp.rightShoulder, gp.rightTrigger })
            {
                var src = new PlayerInputSource();
                Press(b);
                src.Sample();
                var i = src.Poll(null);
                Assert.IsTrue(i.guard && i.guardPressed, b.name);
                Assert.IsFalse(i.x || i.dash, b.name + " is no longer Special or Dash");
                Release(b);
                src.Sample();
                src.Poll(null);
            }
        }

        [Test]
        public void Gamepad_LB_and_LT_still_dash_and_B_is_still_Special()
        {
            var gp = InputSystem.AddDevice<Gamepad>();
            var src = new PlayerInputSource();
            Press(gp.leftShoulder); src.Sample(); Release(gp.leftShoulder);
            Assert.IsTrue(src.Poll(null).dash);
            Press(gp.leftTrigger); src.Sample(); Release(gp.leftTrigger);
            Assert.IsTrue(src.Poll(null).dash);
            Press(gp.buttonEast); src.Sample(); Release(gp.buttonEast);
            Assert.IsTrue(src.Poll(null).x);
        }

        [Test]
        public void Walking_backward_on_the_keyboard_is_not_Guard()
        {
            var kb = InputSystem.AddDevice<Keyboard>();
            var src = new PlayerInputSource();
            Press(kb.aKey);
            src.Sample();
            var i = src.Poll(null);
            Assert.AreEqual(-1, i.dx);
            Assert.IsFalse(i.guard);
        }

        [Test]
        public void The_bindings_table_labels_the_Guard_key_for_prompts()
        {
            Assert.AreEqual("O / B", KeyBindings.Label(GameAction.Guard, InputDeviceKind.Keyboard));
            Assert.AreEqual("RB / R1", KeyBindings.Label(GameAction.Guard, InputDeviceKind.Gamepad));
            Assert.AreEqual("J / Z", KeyBindings.Label(GameAction.Punch, InputDeviceKind.Keyboard));
        }
    }
}
