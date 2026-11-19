using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The parry (SAN-91, ADR-0004 step 5). Fight-harness runs: the Ronin kicks (scripted), Kage presses Guard a known number
    /// of frames before the kick lands. The landing frame is measured with Kage standing still, so no move timing is typed here;
    /// every parry number is read from <see cref="ParryTuning"/> (Resources/Tuning/parry.json). The lockout, the unblockable
    /// rule, the projectile rule and the stagger roles use the <see cref="IHittable.ReceiveHit"/> seam with a plain hit, so they don't
    /// depend on a move's reach.
    /// </summary>
    public class ParryTests
    {
        readonly List<HeadlessFight> _fights = new();
        static ParryTuning T => Tuning.Get<ParryTuning>();
        static GuardTuning G => Tuning.Get<GuardTuning>();

        /// <summary>The loop frame the Ronin presses the kick on.</summary>
        const int RoninPress = 14;

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Tuning.ClearOverrides();
        }

        HeadlessFight Duel(float kageX = -0.37f, float roninX = 0.37f, CharacterDef foeDef = null, CharacterDef kageDef = null)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, kageX, roninX, foeDef: foeDef, kageDef: kageDef);
            _fights.Add(f);
            return f;
        }

        /// <summary>The Ronin kicks on <see cref="RoninPress"/>; Kage presses Guard on loop frame <paramref name="at"/> and holds it.</summary>
        HeadlessFight Kicked(int at, int frames = 70, CharacterDef kageDef = null)
        {
            var f = Duel(kageDef: kageDef);
            f.RoninInput.Add(RoninPress).Add(1, buttons: "K").Add(600);
            f.KageInput.Add(at).Add(600, buttons: "G");
            f.Run(frames);
            return f;
        }

        /// <summary>The fighter's own frames in the hit-stun it is in now (hit-stop doesn't count: it doesn't tick).</summary>
        static int ReelFrames(HeadlessFight f, Fighter who)
        {
            int most = 0;
            f.RunUntil(() => { if (who.state == FState.Hitstun) most = Mathf.Max(most, who.stateFrame); return who.state != FState.Hitstun; }, 300);
            return most + 1;
        }

        /// <summary>The frame the kick lands on Kage standing still.</summary>
        int KickLands()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f);
            f.RoninInput.Add(RoninPress).Add(1, buttons: "K").Add(600);
            f.KageInput.Add(600);
            f.RunUntil(() => f.Log.Count(CombatEventType.Hit) > 0, 120);
            return f.Events(CombatEventType.Hit).Single().Frame;
        }

        static HitInfo Incoming(Fighter attacker, HitLevel level = HitLevel.Mid, HitOrigin origin = HitOrigin.Melee) => new()
        {
            attacker = attacker,
            w = new HitWindow { dmg = 6, level = level, kb = new Vector2(2f, 0f), stun = 16 },
            dmg = 6, kb = new Vector2(-2f, 0f), zone = HitZone.Body, contact = new Vector2(-0.3f, 1f), moveId = "test.hit", window = 0, origin = origin,
        };

        /// <summary>Kage holds Guard from loop frame 0 (pressed then); the Ronin stands still.</summary>
        HeadlessFight Guarding(CharacterDef foeDef = null, CharacterDef kageDef = null)
        {
            var f = Duel(foeDef: foeDef, kageDef: kageDef);
            f.RoninInput.Add(1200);
            f.KageInput.Add(1200, buttons: "G");
            return f;
        }

        // ---------------------------------------------------------------- the window

        [Test]
        public void A_Guard_press_inside_the_window_parries_the_hit()
        {
            int lands = KickLands();
            var f = Kicked(lands - 3);

            var e = f.Events(CombatEventType.Parried).Single();
            Assert.AreSame(f.Ronin, e.Source, "Source is the attacker");
            Assert.AreSame(f.Kage, e.Target, "Target is the parrier");
            Assert.AreEqual("kick1", e.MoveId);
            Assert.AreEqual(lands, e.Frame);
            Assert.AreEqual(0f, e.Damage);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count() + f.Events(CombatEventType.Blocked).Count(), "a parry is neither");
        }

        [Test]
        public void The_window_edge_is_inclusive_a_press_windowFrames_before_parries_and_one_more_only_blocks()
        {
            int lands = KickLands();
            for (int n = 0; n <= T.windowFrames + 1; n++)
            {
                var f = Kicked(lands - n);
                bool inside = n <= T.windowFrames;
                Assert.AreEqual(inside ? 1 : 0, f.Events(CombatEventType.Parried).Count(), $"press {n} frames before the hit: parried");
                Assert.AreEqual(inside ? 0 : 1, f.Events(CombatEventType.Blocked).Count(), $"press {n} frames before the hit: blocked");
                Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(), $"press {n} frames before the hit: never a clean hit");
                f.Dispose();
                _fights.Remove(f);
            }
        }

        [Test]
        public void Guard_held_long_before_the_hit_blocks_it()
        {
            int lands = KickLands();
            var f = Kicked(2);
            Assert.AreEqual(0, f.Events(CombatEventType.Parried).Count());
            Assert.AreEqual(lands, f.Events(CombatEventType.Blocked).Single().Frame);
        }

        [Test]
        public void A_hit_with_no_Guard_held_is_not_parried()
        {
            var f = Duel();
            f.RoninInput.Add(RoninPress).Add(1, buttons: "K").Add(600);
            f.KageInput.Add(600);
            f.Run(70);
            Assert.AreEqual(0, f.Events(CombatEventType.Parried).Count());
            Assert.AreEqual(1, f.Events(CombatEventType.Hit).Count());
        }

        [Test]
        public void The_window_is_measured_from_the_press_as_FramesSinceGuardPress()
        {
            var f = Guarding();
            f.Run(1);
            Assert.AreEqual(0, f.Kage.FramesSinceGuardPress);
            Assert.AreEqual(0, f.Kage.ParryWindowAge, "the press that opened the window");
            f.Run(T.windowFrames);
            Assert.IsTrue(f.Kage.InParryWindow, "the last frame of the window");
            f.Run(1);
            Assert.IsFalse(f.Kage.InParryWindow, "one frame later");
        }

        [Test]
        public void A_parry_does_not_depend_on_the_level_of_the_hit()
        {
            // the JS game has no levels: a standing guard parries a low sweep, a low guard an overhead
            var f = Guarding();
            f.Run(2);
            Assert.AreEqual(HitResult.Parried, f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Low)));
        }

        // ---------------------------------------------------------------- the defender

        [Test]
        public void The_defender_takes_no_damage_no_posture_and_no_blockstun_and_gains_rage()
        {
            int lands = KickLands();
            var f = Kicked(lands - 2, frames: lands + 1);
            float meterBefore = 0f;
            Assert.AreEqual(f.Kage.maxHealth, f.Kage.health);
            Assert.AreEqual(0f, f.Kage.Posture);
            Assert.AreNotEqual(FState.Blockstun, f.Kage.state);
            Assert.IsTrue(f.Kage.IsGuarding, "he keeps his guard and can answer at once");
            Assert.AreEqual(T.meterGain, f.Kage.meter - meterBefore, 1e-3f);
            Assert.AreEqual(1, f.Kage.ParriesMade);
        }

        [Test]
        public void The_Rage_perk_scales_the_Rage_a_parry_gives()
        {
            var d = CharacterCatalog.Get(KageCharacter.Id).Clone();
            d.perks = new GearPerks { rage = 0.2f };
            var f = Guarding(kageDef: d);
            f.Run(2);
            f.Kage.ReceiveHit(Incoming(f.Ronin));
            Assert.AreEqual(T.meterGain * 1.2f, f.Kage.meter, 1e-3f);
        }

        // ---------------------------------------------------------------- the attacker

        [Test]
        public void The_parried_attacker_staggers_for_its_roles_frames_and_loses_posture()
        {
            int lands = KickLands();
            var f = Kicked(lands - 2, frames: lands + 1);
            var ronin = f.Ronin;
            Assert.AreEqual(FState.Hitstun, ronin.state, "reeling");
            Assert.AreEqual(ronin.PostureMax * T.regularPostureShare, ronin.Posture, 1e-3f);
            var e = f.Events(CombatEventType.Parried).Single();
            Assert.AreEqual(ronin.Posture, e.Posture, 1e-3f, "the event's Posture is what the attacker lost");
            Assert.AreEqual(ronin.PostureMax * T.regularPostureShare, f.Kage.LastParryPosture, 1e-3f);

            Assert.That(ReelFrames(f, ronin), Is.InRange(T.regularStaggerFrames - 1, T.regularStaggerFrames + 1), "it reels for the role's frames");
        }

        [Test]
        public void The_parried_attacker_is_pushed_away_from_the_parrier()
        {
            int lands = KickLands();
            var f = Kicked(lands - 2, frames: lands);
            float x = f.Ronin.Pos.x;
            f.Run(40);
            Assert.Greater(f.Ronin.Pos.x, x + 0.1f, "the Ronin is on the right of Kage and goes further right");
        }

        [Test]
        public void The_stagger_and_posture_follow_the_attackers_role()
        {
            foreach (var (id, frames, share) in new[]
                     { ("order_footman", T.regularStaggerFrames, T.regularPostureShare), ("kuroe", T.subStaggerFrames, T.subPostureShare), ("raikon", T.bossStaggerFrames, T.bossPostureShare) })
            {
                // the Ronin's build under another JS id: the role (regular, sub, boss) comes from the id's JS type
                var def = CharacterCatalog.Get(Ronin.Id).Clone();
                def.id = id;
                def.postureMax = id == "raikon" ? 100f : 60f;
                var f = Guarding(foeDef: def);
                f.Run(2);
                Assert.AreEqual(HitResult.Parried, f.Kage.ReceiveHit(Incoming(f.Ronin)), id);
                Assert.AreEqual(f.Ronin.PostureMax * share, f.Ronin.Posture, 1e-3f, id + " posture");
                Assert.That(ReelFrames(f, f.Ronin), Is.InRange(frames - 1, frames + 1), id + " stagger");
                f.Dispose();
                _fights.Remove(f);
            }
        }

        [Test]
        public void A_parry_that_fills_the_attackers_bar_breaks_its_guard_and_GuardBroken_follows_the_Parried_event()
        {
            int lands = KickLands();
            var f = Duel();
            f.RoninInput.Add(RoninPress).Add(1, buttons: "K").Add(600);
            f.KageInput.Add(lands - 3).Add(600, buttons: "G");
            f.Ronin.AddPosture(f.Ronin.PostureMax * (1f - T.regularPostureShare) + 0.5f, f.Kage, "setup");
            f.Run(lands + 3);
            Assert.IsTrue(f.Ronin.GuardBroken, "the parry's posture filled the bar");
            var order = f.Log.All.Where(e => e.Type is CombatEventType.GuardBroken or CombatEventType.Parried).Select(e => e.Type).ToList();
            CollectionAssert.AreEqual(new[] { CombatEventType.Parried, CombatEventType.GuardBroken }, order);
            Assert.AreEqual(f.Ronin.PostureMax, f.Events(CombatEventType.GuardBroken).Single().Posture, "the bar that filled");
        }

        [Test]
        public void The_stunRes_perk_shortens_the_stagger_of_a_parried_Kage_through_ScaleStun()
        {
            int Reel(float stunRes)
            {
                var d = CharacterCatalog.Get(KageCharacter.Id).Clone();
                d.perks = new GearPerks { stunRes = stunRes };
                var f = Duel(kageDef: d);
                f.RoninInput.Add(600, buttons: "G");        // a scripted foe may parry
                f.KageInput.Add(600);
                f.Run(3);
                Assert.AreEqual(HitResult.Parried, f.Ronin.ReceiveHit(Incoming(f.Kage)));
                Assert.AreEqual(f.Kage.PostureMax * T.playerPostureShare, f.Kage.Posture, 1e-3f, "Kage loses his share of posture");
                int n = ReelFrames(f, f.Kage);
                f.Dispose();
                _fights.Remove(f);
                return n;
            }
            Assert.That(Reel(0f), Is.InRange(T.playerStaggerFrames - 1, T.playerStaggerFrames + 1));
            int expected = Mathf.RoundToInt(T.playerStaggerFrames * (1f - 0.4f * T.stunResistFactor));
            Assert.That(Reel(0.4f), Is.InRange(expected - 1, expected + 1), "x (1 - stunRes x factor)");
        }

        // ---------------------------------------------------------------- the lockout

        [Test]
        public void A_mistimed_press_locks_out_parrying_for_the_lockout_but_still_guards()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            // press at 0 (nothing arrives), release, press again at 12 (inside the lockout), hit on 14
            f.KageInput.Add(2, buttons: "G").Add(10).Add(30, buttons: "G");
            f.Run(15);
            Assert.AreEqual(2, f.Kage.FramesSinceGuardPress, "the second press is 2 frames old");
            Assert.IsTrue(f.Kage.ParryLockoutLeft > 0);
            Assert.AreEqual(HitResult.Blocked, f.Kage.ReceiveHit(Incoming(f.Ronin)), "it still guards, it doesn't parry");
            Assert.Greater(f.Kage.Posture, 0f);
        }

        [Test]
        public void The_lockout_ends_lockoutFrames_after_the_press_and_the_next_press_parries()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            int second = T.lockoutFrames;       // the first press on frame 0 locks frames 0 to lockoutFrames - 1
            f.KageInput.Add(2, buttons: "G").Add(second - 2).Add(30, buttons: "G");
            f.Run(second);                      // loop frames 0 to second - 1
            Assert.IsFalse(f.Kage.GuardHeld);
            f.Run(1);                           // loop frame second: the press
            Assert.AreEqual(0, f.Kage.FramesSinceGuardPress, "pressed on frame " + second);
            Assert.AreEqual(HitResult.Parried, f.Kage.ReceiveHit(Incoming(f.Ronin)), "the lockout is over");
        }

        [Test]
        public void A_press_one_frame_inside_the_lockout_does_not_parry()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            int second = T.lockoutFrames - 1;
            f.KageInput.Add(2, buttons: "G").Add(second - 2).Add(30, buttons: "G");
            f.Run(second + 1);
            Assert.AreEqual(0, f.Kage.FramesSinceGuardPress, "pressed on frame " + second);
            Assert.AreEqual(HitResult.Blocked, f.Kage.ReceiveHit(Incoming(f.Ronin)));
        }

        [Test]
        public void Mashing_Guard_cannot_chain_parries()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            for (int i = 0; i < 30; i++) f.KageInput.Add(1, buttons: "G").Add(2);       // a press every 3 frames
            f.Run(4);                                                                      // loop frames 0 to 3: presses on 0 and 3
            Assert.AreEqual(HitResult.Parried, f.Kage.ReceiveHit(Incoming(f.Ronin)), "the first press parries a hit that lands inside its window");
            f.Run(6);                                                                      // loop frame 9, the fourth press; the first is 9 frames old
            Assert.AreEqual(HitResult.Blocked, f.Kage.ReceiveHit(Incoming(f.Ronin)), "the mashed presses were locked out");
        }

        [Test]
        public void One_press_parries_one_hit()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.KageInput.Add(600, buttons: "G");
            f.Run(3);
            Assert.AreEqual(HitResult.Parried, f.Kage.ReceiveHit(Incoming(f.Ronin)));
            f.Run(1);
            Assert.AreEqual(HitResult.Blocked, f.Kage.ReceiveHit(Incoming(f.Ronin)), "the same press is spent");
        }

        // ---------------------------------------------------------------- what can't be parried

        [Test]
        public void An_unblockable_hit_is_never_parried_even_with_perfect_timing()
        {
            var f = Guarding();
            f.Run(3);
            Assert.IsTrue(f.Kage.InParryWindow);
            var res = f.Kage.ReceiveHit(Incoming(f.Ronin, HitLevel.Unblockable));
            Assert.AreEqual(HitResult.Hit, res);
            Assert.Less(f.Kage.health, f.Kage.maxHealth);
            Assert.AreEqual(0, f.Events(CombatEventType.Parried).Count());
        }

        [Test]
        public void A_projectile_is_blocked_not_parried_as_in_the_JS_game_and_a_hazard_never_is()
        {
            Assert.IsFalse(T.parryProjectiles, "the shipped data follows the JS game");
            var f = Guarding();
            f.Run(3);
            Assert.AreEqual(HitResult.Blocked, f.Kage.ReceiveHit(Incoming(f.Ronin, origin: HitOrigin.Projectile)));
            var g = Guarding();
            g.Run(3);
            Assert.AreEqual(HitResult.Blocked, g.Kage.ReceiveHit(Incoming(g.Ronin, origin: HitOrigin.Hazard)), "a hazard guarded, never parried");
        }

        [Test]
        public void With_parryProjectiles_on_a_parried_projectile_leaves_its_owner_alone()
        {
            Tuning.Override(new ParryTuning { parryProjectiles = true });
            var f = Guarding();
            f.Run(3);
            Assert.AreEqual(HitResult.Parried, f.Kage.ReceiveHit(Incoming(f.Ronin, origin: HitOrigin.Projectile)));
            Assert.AreEqual(FState.Idle, f.Ronin.state, "the thrower isn't staggered");
            Assert.AreEqual(0f, f.Ronin.Posture, "and loses no posture");
            Assert.AreEqual(0f, f.Kage.LastParryPosture);
        }

        [Test]
        public void A_hit_on_a_fighter_who_cannot_guard_is_not_parried()
        {
            var f = Duel();
            f.RoninInput.Add(1200);
            f.KageInput.Add(2).Add(30, buttons: "G");
            f.Run(2);
            Assert.AreEqual(HitResult.Hit, f.Kage.ReceiveHit(Incoming(f.Ronin)), "no Guard yet: a clean hit puts him in hitstun");
            f.Run(3);                                   // the Guard is pressed and held, and does nothing in hitstun
            Assert.IsFalse(f.Kage.IsGuarding);
            Assert.IsFalse(f.Kage.InParryWindow);
        }

        // ---------------------------------------------------------------- the event, the cue, the count

        [Test]
        public void A_parry_is_published_once_and_counted_for_the_fight_rating()
        {
            int lands = KickLands();
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.37f, 0.37f);
            _fights.Add(f);
            using var rec = new FightStatsRecorder(f.Ctx.Events, f.Kage);
            rec.MarkFightStart(0);
            f.RoninInput.Add(RoninPress).Add(1, buttons: "K").Add(600);
            f.KageInput.Add(lands - 4).Add(600, buttons: "G");
            f.Run(lands + 5);

            Assert.AreEqual(1, f.Events(CombatEventType.Parried).Count(), "one event");
            Assert.AreEqual(1, rec.Parries, "counted");
            Assert.AreEqual(1, rec.Result().parries, "and in the rating's input");
            StringAssert.Contains("Parried ronin>kage kick1#", f.Log.Lines().Single(l => l.Contains(" Parried ")) + "");
        }

        [Test]
        public void A_foe_parrying_Kage_is_not_counted_for_the_rating()
        {
            var f = Duel();
            using var rec = new FightStatsRecorder(f.Ctx.Events, f.Kage);
            f.RoninInput.Add(600, buttons: "G");
            f.KageInput.Add(600);
            f.Run(3);
            Assert.AreEqual(HitResult.Parried, f.Ronin.ReceiveHit(Incoming(f.Kage)));
            Assert.AreEqual(0, rec.Parries);
        }

        [Test]
        public void A_parry_plays_the_parry_cue_and_freezes_the_fight_briefly()
        {
            var cues = new List<string>();
            void Rec(string id, Vector2 pos, in CueArgs a) => cues.Add(id);
            EffectCues.Played += Rec;
            try
            {
                int lands = KickLands();
                var f = Kicked(lands - 2, frames: lands + 1);
                CollectionAssert.Contains(cues, Fighter.ParryCue);
                Assert.AreEqual(1, cues.Count(c => c == Fighter.ParryCue));
                Assert.IsTrue(f.Ctx.Clock.InHitstop, "a short hit-stop");
            }
            finally { EffectCues.Played -= Rec; }
        }

        [Test]
        public void The_parry_cue_is_registered_with_a_placeholder_player()
        {
            Assert.IsTrue(EffectCues.IsRegistered(Fighter.ParryCue));
        }

        // ---------------------------------------------------------------- the AI

        /// <summary>Kage jabs and kicks at the Ronin AI for a while; counts how its guard went.</summary>
        (int parried, int blocked) AiGuards(float parryRate, int seed)
        {
            var foe = CharacterCatalog.Get(Ronin.Id).Clone();
            foe.parryRate = parryRate;
            using var f = new HeadlessFight(seed, Foe.Ai, -0.37f, 0.55f, foeDef: foe);
            for (int i = 0; i < 60; i++) f.KageInput.Add(1, buttons: i % 2 == 0 ? "P" : "K").Add(14);
            f.Run(900);
            int parried = f.Events(CombatEventType.Parried).Count(e => e.Target == f.Ronin);
            int blocked = f.Events(CombatEventType.Blocked).Count(e => e.Target == f.Ronin);
            return (parried, blocked);
        }

        [Test]
        public void The_AI_never_parries_with_a_parry_rate_of_zero_even_when_its_guard_goes_up_inside_the_window()
        {
            int parried = 0, blocked = 0;
            for (int seed = 1; seed <= 6; seed++) { var r = AiGuards(0f, seed); parried += r.parried; blocked += r.blocked; }
            Assert.AreEqual(0, parried);
            Assert.Greater(blocked, 0, "it did guard");
        }

        [Test]
        public void An_AI_foe_with_a_parry_rate_of_one_parries_the_attacks_it_chooses_to_guard()
        {
            int parried = 0, blocked = 0;
            for (int seed = 1; seed <= 6; seed++) { var r = AiGuards(1f, seed); parried += r.parried; blocked += r.blocked; }
            Assert.Greater(parried, 0, "it parries");
            Assert.Greater(parried, blocked, "and, trying every time, parries more than it blocks");
        }
    }
}
