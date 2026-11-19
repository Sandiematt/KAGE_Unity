using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>
    /// Crimson Moon (SAN-82, ADR-0004, ADR-0006): the katana family's rage art on Special + Down with full Rage, in the fight
    /// harness. Every number it checks is the JS art's, in <see cref="CrimsonMoonTuning"/>: the cut-in and the freeze, three
    /// crescent cuts on frames 33, 44 and 56 for 7, the step back, the giant crescent on frame 83 for 15 with a knockdown, hit-stop
    /// and slow motion, the camera's override, and the guarantees: unblockable and unparryable, a world that runs again, no
    /// perfect dodge, the same fight every run. (Frames below are the art's own: Kage's ticks, which hit-stop doesn't advance.)
    /// </summary>
    public class RageArtCrimsonMoonTests
    {
        readonly List<IDisposable> _fights = new();
        readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            foreach (var o in _objects) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _objects.Clear();
            Tuning.ClearOverrides();
            GameFeel.trauma = GameFeel.punch = 0f;
        }

        // ---- scaffolding ----

        /// <summary>Kage and an idle Ronin 1.2 m apart (the art walks Kage to 0.8 m), Rage full.</summary>
        HeadlessFight Duel(float meter = 100f, CharacterDef kage = null, CharacterDef foe = null, bool foeIdle = true)
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.6f, 0.6f, meter, foeDef: foe, kageDef: kage);
            _fights.Add(f);
            if (foeIdle) f.RoninInput.Add(2400);
            return f;
        }

        static void Cast(HeadlessFight f) => f.KageInput.Add(1, dy: -1, buttons: "X").Add(2400);

        /// <summary>Runs frame by frame until the art is over, calling <paramref name="each"/> (the art's frame) after every frame.</summary>
        static void RunArt(HeadlessFight f, Action<int> each = null, int max = 700)
        {
            bool started = false;
            for (int i = 0; i < max; i++)
            {
                f.Run(1);
                bool active = f.Kage.state == FState.Ultimate;
                started |= active;
                if (active) each?.Invoke(f.Kage.RageArtFrame);
                if (started && !active) return;
            }
            Assert.Fail("the art never ended");
        }

        static List<CombatEvent> ArtHits(HeadlessFight f) =>
            f.Events(CombatEventType.Hit).Where(e => e.MoveId == Fighter.CrimsonMoon).ToList();

        static CrimsonMoonTuning T => Tuning.Get<CrimsonMoonTuning>();

        // ---- who triggers it ----

        [Test]
        public void A_katana_with_full_Rage_starts_Crimson_Moon_and_spends_the_Rage()
        {
            var f = Duel();
            Cast(f);
            f.Run(3);

            var rage = f.Events(CombatEventType.RageArt).Single();
            Assert.AreEqual("kage", rage.SourceId);
            Assert.AreEqual(Fighter.CrimsonMoon, rage.MoveId);
            Assert.AreEqual(FState.Ultimate, f.Kage.state);
            Assert.AreEqual(Fighter.CrimsonMoon, f.Kage.ActiveRageArt);
            Assert.AreEqual(0f, f.Kage.meter, "the art spends the whole meter");
            Assert.AreEqual(0, f.Ctx.Projectiles.Count, "and no kunai goes");
            Assert.AreEqual(5, f.Kage.KunaiCount);
            f.Run(300);
            Assert.AreEqual(0f, f.Kage.meter, "the art's own hits give Kage no Rage back");
            Assert.AreEqual(1, f.Events(CombatEventType.RageArt).Count());
        }

        [Test]
        public void Without_full_Rage_the_same_input_is_a_kunai()
        {
            var f = Duel(meter: 99f);
            Cast(f);
            f.Run(30);
            Assert.AreEqual(0, f.Events(CombatEventType.RageArt).Count());
            Assert.AreEqual(4, f.Kage.KunaiCount);
        }

        [Test]
        public void The_family_decides_not_the_weapon_id([Values("kagetsu", "shizuka")] string weapon)
        {
            var f = Duel(kage: SpearFamilyFightTests.KageWith(weapon));
            Assert.AreEqual("katana", f.Kage.Family.id);
            Assert.AreEqual(Fighter.CrimsonMoon, f.Kage.Family.rageArt);
            Cast(f);
            f.Run(3);
            Assert.AreEqual(f.Kage.Family.rageArt, f.Events(CombatEventType.RageArt).Single().MoveId);
        }

        [Test]
        public void A_rage_art_stays_the_katana_families_even_if_the_family_data_names_the_other()
        {
            // the trigger reads the family's rageArt, so a family that names Thousand Shadows gets it whatever the weapon
            var f = Duel();
            var katana = f.Kage.Family;
            var saved = katana.rageArt;
            try
            {
                katana.rageArt = Fighter.ThousandShadows;
                Cast(f);
                f.Run(3);
                Assert.AreEqual(Fighter.ThousandShadows, f.Events(CombatEventType.RageArt).Single().MoveId);
            }
            finally { katana.rageArt = saved; }
        }

        [Test]
        public void Without_a_target_the_press_is_spent_and_nothing_stays_frozen()
        {
            var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, 0f, kageMeter: 100f);
            _fights.Add(f);
            Cast(f);
            f.Run(40);
            Assert.AreEqual(Fighter.CrimsonMoon, f.Events(CombatEventType.RageArt).Single().MoveId);
            Assert.AreEqual(0f, f.Kage.meter);
            Assert.AreEqual(FState.Idle, f.Kage.state);
            Assert.AreEqual(1f, f.Ctx.Clock.WorldScale, "no freeze with nobody to cut");
        }

        // ---- the Crescent is untouched ----

        [Test]
        public void The_Crescent_is_still_a_normal_Rage_move_even_with_a_full_meter()
        {
            var f = Duel(meter: 100f);
            f.KageInput.Add(1, buttons: "X").Add(2400);
            f.Run(3);
            Assert.AreEqual(65f, f.Kage.meter, 0.01f, "35 Rage spent, the rest kept");
            f.Run(37);
            Assert.AreEqual(0, f.Events(CombatEventType.RageArt).Count(), "no Down, no rage art");
            Assert.AreEqual("crescent", f.Events(CombatEventType.MoveStarted).Single(e => e.SourceId == "kage").MoveId);
            Assert.AreEqual(1, f.Ctx.Projectiles.Count);
        }

        // ---- the timeline and the damage ----

        [Test]
        public void Three_cuts_then_the_giant_crescent_land_on_the_JS_frames_for_the_JS_damage()
        {
            var f = Duel();
            var ticks = new List<int>();
            f.Kage.onHit += (a, d, info, res) => { if (info.moveId == Fighter.CrimsonMoon) ticks.Add(a.RageArtFrame); };
            Cast(f);
            int last = 0;
            RunArt(f, t => last = Mathf.Max(last, t));

            var hits = ArtHits(f);
            Assert.AreEqual(4, hits.Count, "three cuts and the giant crescent");
            CollectionAssert.AreEqual(new[] { 7f, 7f, 7f, 15f }, hits.Select(h => h.Damage).ToArray());
            CollectionAssert.AreEqual(new[] { 33, 44, 56, 83 }, ticks, "JS 0.55, 0.74, 0.93 and 1.38 s at 60 a second");
            Assert.AreEqual(110, last, "the whole art is the JS art's 1.85 s: on its frame 111 Kage is back in neutral");
            Assert.IsTrue(hits.All(h => h.Level == HitLevel.Unblockable));
            Assert.IsTrue(hits.All(h => h.TargetId == "ronin" && h.SourceId == "kage" && h.Window == -1 && !h.Crit));
            Assert.AreEqual(100f - 36f, f.Ronin.health, 0.01f, "7 + 7 + 7 + 15 of the Ronin's 100");
            Assert.AreEqual(FState.Idle, f.Kage.state, "Kage is back in neutral");
            Assert.AreEqual(1f, f.Ctx.Clock.WorldScale);
        }

        [Test]
        public void The_timeline_is_the_tuning_files()
        {
            var t = new CrimsonMoonTuning { cutFrames = new[] { 20, 26, 32 }, finisherFrame = 50, totalFrames = 70, anticipationFrames = 18, stepBackFrame = 40, cutDamage = 3, finisherDamage = 9 };
            Tuning.Override(t);
            var f = Duel();
            var ticks = new List<int>();
            f.Kage.onHit += (a, d, info, res) => { if (info.moveId == Fighter.CrimsonMoon) ticks.Add(a.RageArtFrame); };
            Cast(f);
            int last = 0;
            RunArt(f, x => last = Mathf.Max(last, x));
            CollectionAssert.AreEqual(new[] { 20, 26, 32, 50 }, ticks);
            CollectionAssert.AreEqual(new[] { 3f, 3f, 3f, 9f }, ArtHits(f).Select(h => h.Damage).ToArray());
            Assert.AreEqual(69, last, "back in neutral on frame 70");
        }

        [Test]
        public void The_weapons_level_scales_the_art_like_every_blade_hit()
        {
            var kage = SpearFamilyFightTests.KageWith("kagetsu", 5);
            var f = Duel(kage: kage);
            Cast(f);
            RunArt(f);
            var w = f.Kage.Weapon;
            Assert.Greater(w.dmg, 1f, "a level 5 Kagetsu hits harder");
            float cut = DamageCalc.Damage(7, f.Kage.DamageMul, w.dmg, false, f.Ronin.Defense);
            float fin = DamageCalc.Damage(15, f.Kage.DamageMul, w.dmg, false, f.Ronin.Defense);
            CollectionAssert.AreEqual(new[] { cut, cut, cut, fin }, ArtHits(f).Select(h => h.Damage).ToArray());
        }

        [Test]
        public void The_finisher_has_hit_stop_slow_motion_and_throws_the_foe()
        {
            var f = Duel();
            Cast(f);
            int skipped = 0, afterFinisher = 0;
            bool finisher = false;
            for (int i = 0; i < 600 && !(finisher && afterFinisher > 60); i++)
            {
                bool ran = f.Loop.StepHeadless();
                if (finisher) { afterFinisher++; if (!ran) skipped++; }
                if (!finisher && f.Kage.RageArtFrame == 83) { finisher = true; Assert.IsTrue(f.Ctx.Clock.InHitstop, "hit-stop on the blow"); }
            }
            Assert.IsTrue(finisher);
            // 7 frames of hit-stop, then slow motion at 0.3 for 0.45 s: a third of the next 60 frames are skipped
            Assert.Greater(skipped, 15, "hit-stop and slow motion on the finisher");
            Assert.IsTrue(f.Ronin.state is FState.Launched or FState.Down or FState.Idle or FState.Hitstun, "thrown, down or getting up");
            Assert.IsTrue(f.Events(CombatEventType.Hit).Any(e => e.MoveId == Fighter.CrimsonMoon && e.Damage == 15f));
        }

        [Test]
        public void The_finisher_knocks_the_foe_down_and_away()
        {
            var f = Duel();
            Cast(f);
            bool launched = false;
            float x83 = 0f;
            RunArt(f, t =>
            {
                if (t == 83) { x83 = f.Ronin.Pos.x; launched = f.Ronin.state == FState.Launched; }
            });
            Assert.IsTrue(launched, "the blow knocks the foe down");
            f.Run(20);
            Assert.Greater(Mathf.Abs(f.Ronin.Pos.x - x83), 0.4f, "and sends it away");
        }

        // ---- the freeze ----

        [Test]
        public void The_world_holds_still_while_Kage_cuts_and_runs_again_afterwards()
        {
            var f = Duel(foeIdle: false);
            f.RoninInput.Add(2400, fwd: 1);                 // the Ronin walks at Kage
            Cast(f);
            var scale = new Dictionary<int, float>();
            var ronin = new Dictionary<int, float>();
            var kage = new Dictionary<int, float>();
            RunArt(f, t => { scale[t] = f.Ctx.Clock.WorldScale; ronin[t] = f.Ronin.Pos.x; kage[t] = f.Kage.Pos.x; });

            for (int t = 0; t < 83; t++) Assert.AreEqual(0f, scale[t], $"frozen at art frame {t}");
            for (int t = 1; t < 83; t++) Assert.AreEqual(ronin[0], ronin[t], 1e-4f, $"the Ronin holds still at art frame {t}");
            Assert.Greater(Mathf.Abs(kage[40] - kage[0]), 0.2f, "Kage moves: he is beside the foe");
            Assert.AreEqual(0.8f, Mathf.Abs(kage[40] - ronin[40]), 0.02f, "0.8 m from it, as the JS 92 px");
            Assert.AreEqual(1f, scale[90], "the world runs again from the finisher on");
            Assert.AreEqual(1f, f.Ctx.Clock.WorldScale);
            Assert.AreEqual(0, f.Ctx.Clock.WorldFramesLeft);
        }

        [Test]
        public void Kage_alternates_sides_for_the_three_cuts()
        {
            var f = Duel();
            Cast(f);
            var side = new Dictionary<int, float>();
            RunArt(f, t => { if (t is 34 or 45 or 57) side[t] = Mathf.Sign(f.Kage.Pos.x - f.Ronin.Pos.x); });
            Assert.AreEqual(-1f, side[34], "he starts on his own side");
            Assert.AreEqual(1f, side[45], "crosses over");
            Assert.AreEqual(-1f, side[57], "and back");
        }

        [Test]
        public void A_foes_projectile_hangs_in_the_air_and_Kages_own_flies_on()
        {
            var f = Duel();
            Cast(f);
            Projectile foe = null, own = null;
            var foeX = new List<float>();
            var ownX = new List<float>();
            RunArt(f, t =>
            {
                if (t == 2 && foe == null)
                {
                    foe = Projectile.Spawn(f.Ctx, f.Ronin, Fighter.CrescentProjectile, new Vector2(1f, 3f), -1f);      // above everyone
                    own = Projectile.Spawn(f.Ctx, f.Kage, Fighter.CrescentProjectile, new Vector2(-1f, 3.5f), 1f);
                }
                else if (foe != null && t is > 2 and < 83) { foeX.Add(foe.Position.x); ownX.Add(own.Position.x); }
            });
            Assert.IsTrue(foeX.All(x => Mathf.Abs(x - foeX[0]) < 1e-4f), "the foe's crescent holds still");
            Assert.Greater(ownX[ownX.Count - 1] - ownX[0], 3f, "Kage's runs at full speed");
        }

        [Test]
        public void In_a_group_fight_every_other_foe_freezes_and_only_the_target_is_cut()
        {
            var d = CharacterCatalog.Get(KageCharacter.Id);
            using var g = new GroupFight(82, 0f, new[] { 1.3f, -2.4f, 2.8f, -3.8f }, kageAi: false, kageDef: d, aiLevel: 0.9f);
            g.Kage.meter = 100f;
            g.KageInput.Add(1, dy: -1, buttons: "X").Add(2400);
            g.Run(2);
            var target = g.Kage.opponent;
            Assert.IsNotNull(target);
            var held = g.Foes.ToDictionary(x => x, x => x.Pos.x);
            var hp = g.Foes.ToDictionary(x => x, x => x.health);
            for (int i = 0; i < 400 && g.Kage.state == FState.Ultimate && g.Kage.RageArtFrame < 83; i++)
            {
                g.Run(1);
                if (g.Kage.RageArtFrame >= 83) break;
                foreach (var foe in g.Foes)
                    Assert.AreEqual(held[foe], foe.Pos.x, 1e-4f, $"{foe.FighterId} holds still at art frame {g.Kage.RageArtFrame}");
                Assert.AreSame(target, g.Kage.opponent, "the art holds its target");
            }
            g.Run(200);
            var hits = g.Events(CombatEventType.Hit).Where(e => e.MoveId == Fighter.CrimsonMoon).ToList();
            Assert.AreEqual(4, hits.Count);
            Assert.IsTrue(hits.All(h => h.Target == target), "only the foe it started on is bound and cut");
            foreach (var foe in g.Foes.Where(x => x != target))
                Assert.AreEqual(hp[foe], foe.health, 1e-4f, $"{foe.FighterId} is not touched by the art");
            Assert.AreEqual(1f, g.Ctx.Clock.WorldScale, "and the arena returns to normal");
        }

        // ---- the hits can't be turned aside ----

        [Test]
        public void A_guarding_foe_cannot_block_it_and_a_fresh_guard_cannot_parry_it()
        {
            var f = Duel(foeIdle: false);
            f.RoninInput.Add(2400, buttons: "G");       // Guard pressed on the first frame and held: the parry window is open as the art starts
            Cast(f);
            bool guardedAtFirstCut = false;
            int sincePress = 999;
            RunArt(f, t =>
            {
                if (t == 32) { guardedAtFirstCut = f.Ronin.IsGuarding; sincePress = f.Ronin.FramesSinceGuardPress; }
            });
            Assert.IsTrue(guardedAtFirstCut, "the Ronin really is guarding (the world is frozen with its guard up)");
            Assert.LessOrEqual(sincePress, Tuning.Get<ParryTuning>().windowFrames, "inside the parry window, held there by the freeze");
            Assert.AreEqual(4, ArtHits(f).Count);
            Assert.AreEqual(0, f.Events(CombatEventType.Blocked).Count(), "never blocked");
            Assert.AreEqual(0, f.Events(CombatEventType.Parried).Count(), "never parried");
            Assert.AreEqual(0, f.Events(CombatEventType.Countered).Count(), "never countered");
            Assert.AreEqual(0f, f.Kage.health - f.Kage.maxHealth, "Kage is unhurt");
            Assert.AreEqual(64f, f.Ronin.health, 0.01f);
        }

        [Test]
        public void A_foe_in_a_Mirror_Guard_counter_window_cannot_counter_it()
        {
            var f = Duel(foeIdle: false);
            f.RoninInput.Add(1, fwd: -1, buttons: "S").Add(2400);    // Mirror Guard (back + Katana): its counter window is frames 4 to 24
            f.KageInput.Add(8).Add(1, dy: -1, buttons: "X").Add(2400);       // the art starts with the Ronin well inside the window
            bool inWindow = false;
            RunArt(f, t => { if (t == 32) inWindow = f.Ronin.state == FState.Attack && f.Ronin.move.counterFrom >= 0 && f.Ronin.moveFrame >= f.Ronin.move.counterFrom && f.Ronin.moveFrame <= f.Ronin.move.counterTo; });
            Assert.IsTrue(inWindow, "the Ronin's counter window is open (held open by the freeze) at the first cut");
            Assert.AreEqual(0, f.Events(CombatEventType.Countered).Count());
            Assert.AreEqual(4, ArtHits(f).Count);
        }

        // ---- the foe is bound, KO is handled ----

        [Test]
        public void The_first_cut_binds_the_foe_where_it_stands()
        {
            var f = Duel();
            Cast(f);
            FState atFirstCut = default;
            RunArt(f, t => { if (t == 34) atFirstCut = f.Ronin.state; });
            Assert.AreEqual(FState.Hitstun, atFirstCut, "bound: held in a stagger, frozen with the world");
        }

        [Test]
        public void A_cut_that_knocks_the_foe_out_ends_the_cutting_but_not_the_art()
        {
            var foe = CharacterCatalog.Get(Ronin.Id);
            foe.jsHealth = 20f;                          // 8.6 of the scale: the second cut kills
            var f = Duel(foe: foe);
            Cast(f);
            RunArt(f);
            Assert.AreEqual(1, f.Events(CombatEventType.KnockedOut).Count());
            Assert.AreEqual(2, ArtHits(f).Count, "the cuts after the knock-out cut nothing");
            Assert.AreEqual(FState.KO, f.Ronin.state);
            Assert.AreEqual(FState.Idle, f.Kage.state);
            Assert.AreEqual(1f, f.Ctx.Clock.WorldScale);
        }

        // ---- the world and the camera come back however the art ends ----

        [Test]
        public void A_round_reset_during_the_art_gives_the_world_back()
        {
            var f = Duel();
            Cast(f);
            f.Run(20);
            Assert.AreEqual(0f, f.Ctx.Clock.WorldScale);
            f.Kage.ResetForRound(-0.6f, 1f);
            Assert.AreEqual(1f, f.Ctx.Clock.WorldScale);
            f.Run(130);
            Assert.IsNull(f.Kage.ActiveRageArt, "no stale art tick runs on");
            Assert.AreEqual(1f, f.Ctx.Clock.WorldScale);
        }

        [Test]
        public void Winning_the_fight_mid_art_gives_the_world_back()
        {
            var f = Duel();
            Cast(f);
            f.Run(40);
            f.Kage.Win();
            Assert.AreEqual(1f, f.Ctx.Clock.WorldScale);
            Assert.AreEqual(FState.Victory, f.Kage.state);
            f.Run(130);
            Assert.AreEqual(FState.Victory, f.Kage.state, "no stale art tick takes him out of it");
        }

        // ---- no perfect dodge during the art ----

        [Test]
        public void A_perfect_dodge_cannot_start_during_the_art()
        {
            var f = Duel();
            Cast(f);
            bool checkedOnce = false;
            RunArt(f, t =>
            {
                if (t < 1 || t >= 111) return;
                Assert.IsFalse(f.Kage.InPerfectDodgeWindow, $"art frame {t}");
                var c = new DefenceCheck
                {
                    info = new HitInfo { attacker = f.Ronin, w = new HitWindow { dmg = 5, level = HitLevel.Mid, kb = new Vector2(1f, 0f) }, dmg = 5, contact = f.Kage.Pos + Vector2.up, moveId = "test.hit", window = -1 },
                };
                Assert.IsFalse(f.Kage.PreHit(ref c), "no pre-hit check takes a hit during the art");
                Assert.IsFalse(c.resolved);
                checkedOnce = true;
            });
            Assert.IsTrue(checkedOnce);
            Assert.AreEqual(0, f.Events(CombatEventType.PerfectDodged).Count());
        }

        [Test]
        public void The_art_replaces_a_perfect_dodges_slow_world_and_clears_it_at_the_end()
        {
            var f = Duel();
            f.Ctx.Clock.SlowWorld(0.4f, 24, f.Kage);
            Cast(f);
            f.Run(5);
            Assert.AreEqual(0f, f.Ctx.Clock.WorldScale, "the art's freeze replaced the 0.4 slow");
            RunArt(f);
            Assert.AreEqual(1f, f.Ctx.Clock.WorldScale);
            Assert.AreEqual(0, f.Ctx.Clock.WorldFramesLeft);
        }

        // ---- the cues ----

        [Test]
        public void The_art_plays_its_named_cues_and_the_Rage_art_event_comes_first()
        {
            var played = new List<(string id, int tick, float scale)>();
            var f = Duel();
            CueObserver watch = (string id, Vector2 p, in CueArgs a) => played.Add((id, f.Kage.RageArtFrame, a.scale));
            EffectCues.Played += watch;
            try
            {
                Cast(f);
                RunArt(f);
            }
            finally { EffectCues.Played -= watch; }

            Assert.AreEqual(1, played.Count(p => p.id == "VFX_Kage_RageCutIn"), "one cut-in band");
            var crescents = played.Where(p => p.id == "VFX_Rage_Crimson_Crescent").ToList();
            CollectionAssert.AreEqual(new[] { 33, 44, 56, 83 }, crescents.Select(c => c.tick).ToArray(), "a crescent on each cut and the giant one");
            Assert.AreEqual(T.finisherScale, crescents[3].scale, 1e-4f);
            Assert.Greater(crescents[3].scale, crescents[0].scale * 2f, "the last is the giant one");
            Assert.AreEqual(1, played.Count(p => p.id == "VFX_Kage_CrimsonMoonFinish"));
            Assert.IsTrue(EffectCues.IsRegistered("VFX_Rage_Crimson_Crescent"));
            Assert.IsTrue(EffectCues.IsRegistered("VFX_Kage_RageCutIn"));
        }

        // ---- the camera ----

        CameraRig Rig(FightContext ctx, Fighter focus, bool group)
        {
            var go = new GameObject("TestCamera");
            _objects.Add(go);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.aspect = 16f / 9f;
            cam.enabled = false;
            var rig = go.AddComponent<CameraRig>();
            rig.cam = cam;
            if (group) rig.Frame(ctx, focus);
            else rig.targets.Add(focus.rig.Bone(B.Hips));
            rig.Snap();
            ctx.Camera = rig;
            return rig;
        }

        [Test]
        public void The_camera_override_closes_in_wins_over_the_group_framing_and_is_released()
        {
            var d = CharacterCatalog.Get(KageCharacter.Id);
            using var g = new GroupFight(82, 0f, new[] { 1.3f, -2.4f, 2.8f, -3.8f }, kageAi: false, kageDef: d, aiLevel: 0.9f);
            var rig = Rig(g.Ctx, g.Kage, group: true);
            Assert.IsFalse(rig.useOverride);
            g.Kage.meter = 100f;
            g.KageInput.Add(1, dy: -1, buttons: "X").Add(2400);

            var sizeAt = new Dictionary<int, float>();
            bool started = false;
            for (int i = 0; i < 400; i++)
            {
                g.Run(1);
                GameFeel.trauma = GameFeel.punch = 0f;
                rig.Follow(1f / 60f);
                if (g.Kage.state == FState.Ultimate) started = true;
                else if (started) break;
                int t = g.Kage.RageArtFrame;
                if (t > 0) sizeAt[t] = rig.cam.orthographicSize;
                if (t == 10) Assert.IsTrue(rig.useOverride, "the art takes the override");
                if (t == 40)
                {
                    Assert.IsTrue(rig.useOverride);
                    Assert.AreEqual(T.exchangeSize, rig.overrideSize, 1e-4f);
                    rig.Follow(10f);                 // let it settle: the override, not the framing of Kage and the engaged foes
                    float cx = (g.Kage.Pos.x + g.Kage.opponent.Pos.x) * 0.5f;
                    Assert.AreEqual(cx, rig.transform.position.x, 0.01f, "centred on Kage and his foe");
                    Assert.AreEqual(T.exchangeSize, rig.cam.orthographicSize, 1e-3f);
                    Assert.AreEqual(T.exchangeSize * 0.64f, rig.transform.position.y, 0.01f);
                }
            }
            Assert.IsTrue(started && g.Kage.state != FState.Ultimate, "the art ran and ended");
            Assert.Less(sizeAt[33], 2.0f, "closed in on Kage through the anticipation (framing alone is never below 2.25)");
            Assert.IsFalse(rig.useOverride, "released afterwards");
        }

        [Test]
        public void The_camera_gives_back_the_override_it_found()
        {
            var f = Duel();
            var rig = Rig(f.Ctx, f.Kage, group: false);
            rig.useOverride = true;
            rig.overrideCenter = new Vector2(1.5f, 2.5f);
            rig.overrideSize = 3.1f;
            Cast(f);
            RunArt(f);
            Assert.IsTrue(rig.useOverride, "the tableau's own override is back");
            Assert.AreEqual(new Vector2(1.5f, 2.5f), rig.overrideCenter);
            Assert.AreEqual(3.1f, rig.overrideSize);
        }

        // ---- the same fight every run ----

        static (List<string> events, List<string> frames) Fight()
        {
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Ai, -0.5f, 0.5f, 100f);
            f.KageInput.Add(1, dy: -1, buttons: "X");
            f.Run(260);
            return (f.Log.Lines().ToList(), f.Frames.ToList());
        }

        [Test]
        public void A_fight_with_Crimson_Moon_is_identical_every_run()
        {
            var a = Fight();
            var b = Fight();
            Assert.IsTrue(a.events.Any(l => l.Contains("RageArt")));
            CollectionAssert.AreEqual(a.events, b.events);
            CollectionAssert.AreEqual(a.frames, b.frames);
        }
    }

    /// <summary>The cut-in band on the HUD (SAN-82) and the art starting it through the fight context.</summary>
    public class RageArtCutInTests
    {
        GameObject _root, _vfx;
        Hud _hud;
        Camera _cam;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("CutInTest");
            _cam = new GameObject("Cam", typeof(Camera)).GetComponent<Camera>();
            _cam.transform.SetParent(_root.transform);
            _cam.orthographic = true;
            var hudGo = new GameObject("HUD", typeof(RectTransform));
            hudGo.transform.SetParent(_root.transform, false);
            _hud = hudGo.AddComponent<Hud>();
            _hud.Build(_cam);
        }

        [TearDown]
        public void TearDown()
        {
            if (_vfx != null) UnityEngine.Object.DestroyImmediate(_vfx);
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        }

        [UnityTest]
        public IEnumerator The_band_slides_in_with_the_name_and_kanji_and_goes_after_its_time()
        {
            Assert.IsFalse(_hud.CutInShowing);
            _hud.CutIn("CRIMSON MOON", "紅月", "RAGE ART  ·  KAGETSU", Color.red, Color.white, 0.5f);
            Assert.IsTrue(_hud.CutInShowing);
            Assert.AreEqual("CRIMSON MOON", _hud.CutInName);
            Assert.AreEqual("紅月", _hud.CutInKanji);
            float until = Time.realtimeSinceStartup + 0.9f;
            while (Time.realtimeSinceStartup < until) { _hud.Tick(null, null, 0f, 1, false); yield return null; }
            Assert.IsFalse(_hud.CutInShowing, "gone after its seconds");
        }

        [Test]
        public void The_art_asks_the_hud_for_the_band_through_the_fight_context()
        {
            _vfx = new GameObject("Vfx");
            _vfx.AddComponent<Vfx>();
            using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -0.6f, 0.6f, 100f);
            f.RoninInput.Add(600);
            f.Ctx.Hud = _hud;
            f.KageInput.Add(1, dy: -1, buttons: "X").Add(600);
            f.Run(4);
            Assert.IsTrue(_hud.CutInShowing);
            Assert.AreEqual("CRIMSON MOON", _hud.CutInName);
            Assert.AreEqual("紅月", _hud.CutInKanji);
        }
    }
}
