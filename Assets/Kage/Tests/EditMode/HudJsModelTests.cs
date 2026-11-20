using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The JS-style HUD's view model (SAN-108): which panels each fight type shows, the health bars' eased damage trail, Rage ready, kunai,
    /// posture, combo, road progress and wave. The model works on plain <see cref="HudFrame"/> values and <see cref="CombatEvent"/>s,
    /// so these tests drive it with made-up numbers and a recorded stream: no fighter, no scene, no uGUI.
    /// </summary>
    public class HudJsModelTests
    {
        const float Dt = 1f / 60f;

        static HudFighterState Kage(float health = 100f, float rage = 0f) => new HudFighterState
        {
            Id = "kage", Name = "Kage", Level = 3, Health = health, MaxHealth = 100f, HealthScale = 1f, Rage = rage,
            CanRageArt = true, HasKunai = true, Kunai = 5,
        };

        static HudFighterState Foe(string id = "footman", float health = 80f) => new HudFighterState
        {
            Id = id, Name = "Footman", Title = "Order of the Mask", Health = health, MaxHealth = 80f, HealthScale = 1f,
        };

        static HudFrame Duel() => new HudFrame
        {
            Layout = HudLayout.Duel, Player = Kage(), Foe = Foe(), HasFoe = true, TimeLeft = 47.2f,
            ClockShown = true, ClockRuns = true, RoundsShown = true, RoundsToWin = 2,
        };

        static HudFrame Boss()
        {
            var raikon = Foe("raikon", 640f);
            raikon.MaxHealth = 640f;
            return new HudFrame { Layout = HudLayout.Boss, Player = Kage(), Foe = raikon, HasFoe = true, RoundsToWin = 1 };
        }

        static HudFrame Road(float progress = 0f) => new HudFrame
        {
            Layout = HudLayout.Stretch, Player = Kage(), RoundsToWin = 1,
            Road = new HudRoadState { Active = true, Progress = progress, EncounterAt = new[] { 0.25f, 0.6f }, EncounterCount = 2 },
        };

        static FightHudModel Run(HudFrame f, int ticks = 1)
        {
            var m = new FightHudModel();
            for (int i = 0; i < ticks; i++) m.Tick(Dt, f);
            return m;
        }

        static void Step(FightHudModel m, HudFrame f, float seconds)
        {
            int n = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < n; i++) m.Tick(Dt, f);
        }

        static CombatEvent HitOn(string targetId, float damage) =>
            CombatEvent.HitResolved(1, CombatEventType.Hit, null, null, targetId, "jab", 0, damage, false, HitLevel.Mid, HitZone.Body, Vector2.zero, Vector2.zero);

        // ---------------- panels by fight type ----------------

        [Test]
        public void A_duel_shows_both_panels_and_the_clock_and_no_boss_bar_or_road()
        {
            var m = Run(Duel());
            Assert.IsTrue(m.Player.Visible);
            Assert.IsTrue(m.Foe.Visible, "the foe's panel");
            Assert.IsTrue(m.Clock.Visible, "the clock");
            Assert.IsFalse(m.Boss.Visible);
            Assert.IsFalse(m.Road.Visible);
        }

        [Test]
        public void A_boss_fight_puts_the_boss_bar_where_the_foe_panel_was_and_has_no_clock()
        {
            var f = Boss();
            f.ClockShown = false;
            var m = new FightHudModel();
            m.BindBoss(new HudBossInfo { Id = "raikon", Name = "Raikon", Title = "the Lightning Ascendant", PhaseNames = new[] { "A", "B", "C" }, Marks = new[] { 0.66f, 0.33f } });
            m.Tick(Dt, f);
            Assert.IsTrue(m.Player.Visible);
            Assert.IsTrue(m.Boss.Visible, "the boss bar");
            Assert.IsFalse(m.Foe.Visible, "replaces the foe panel");
            Assert.IsFalse(m.Clock.Visible, "no clock in a boss fight");
            Assert.IsFalse(m.Road.Visible);
        }

        [Test]
        public void A_stretch_shows_the_road_and_no_foe_panel_clock_or_boss_bar()
        {
            var m = Run(Road(0.4f));
            Assert.IsTrue(m.Player.Visible);
            Assert.IsTrue(m.Road.Visible, "the road strip");
            Assert.IsFalse(m.Foe.Visible, "no foe panel in a stretch");
            Assert.IsFalse(m.Clock.Visible, "no clock in a stretch");
            Assert.IsFalse(m.Boss.Visible);
        }

        [Test]
        public void A_boss_layout_without_a_bound_boss_shows_no_boss_bar()
        {
            var m = Run(Boss());
            Assert.IsFalse(m.Boss.Visible, "nothing to draw until the boss is bound");
        }

        [Test]
        public void A_duel_with_no_clock_hides_the_clock_and_one_round_hides_the_pips()
        {
            var f = Duel();
            f.ClockShown = false;
            Assert.IsFalse(Run(f).Clock.Visible);
            f = Duel();
            f.RoundsShown = false;
            var m = Run(f);
            Assert.IsTrue(m.Clock.Visible);
            Assert.IsFalse(m.Clock.PipsVisible);
        }

        [Test]
        public void With_no_foe_the_foe_panel_is_hidden()
        {
            var f = Duel();
            f.HasFoe = false;
            Assert.IsFalse(Run(f).Foe.Visible, "the training dojo has nobody to show");
        }

        // ---------------- health bars: ease down, then a delayed trail ----------------

        [Test]
        public void A_health_bar_eases_down_to_the_new_value_and_does_not_jump()
        {
            var f = Duel();
            var m = Run(f, 3);
            Assert.AreEqual(1f, m.Player.Hp, 1e-4f);
            f.Player.Health = 50f;
            m.Tick(Dt, f);
            Assert.Less(m.Player.Hp, 1f, "it moves");
            Assert.Greater(m.Player.Hp, 0.5f, "but not all at once");
            Step(m, f, 1.5f);
            Assert.AreEqual(0.5f, m.Player.Hp, 0.01f, "and arrives");
        }

        [Test]
        public void The_damage_trail_waits_then_follows()
        {
            var f = Duel();
            var m = Run(f, 3);
            f.Player.Health = 40f;
            Step(m, f, 0.3f);
            Assert.AreEqual(1f, m.Player.Chip, 1e-4f, "the trail waits a beat after a hit");
            Assert.Less(m.Player.Hp, 0.7f, "while the bar has gone ahead");
            Step(m, f, 0.4f);
            Assert.Less(m.Player.Chip, 1f, "now it follows");
            Assert.Greater(m.Player.Chip, m.Player.Hp, "and trails behind the bar");
            Step(m, f, 3f);
            Assert.AreEqual(0.4f, m.Player.Chip, 0.01f, "until it catches up");
        }

        [Test]
        public void Healing_pushes_the_trail_up_with_the_bar()
        {
            var f = Duel();
            f.Player.Health = 30f;
            var m = Run(f, 2);
            Step(m, f, 3f);
            f.Player.Health = 80f;
            Step(m, f, 0.5f);
            Assert.GreaterOrEqual(m.Player.Chip, m.Player.Hp, "the trail is never behind the bar");
            Assert.Greater(m.Player.Hp, 0.6f);
        }

        [Test]
        public void The_trail_easing_follows_the_JS_rates_exactly()
        {
            var t = new HudTrail();
            t.Step(1f, Dt);
            t.Step(0.5f, Dt);
            float k = 1f - Mathf.Exp(-16f * Dt);
            Assert.AreEqual(1f + (0.5f - 1f) * k, t.Value, 1e-5f, "the bar damps at 16 per second");
            Assert.AreEqual(1f, t.Chip, 1e-6f, "the trail holds");
            Assert.AreEqual(0.45f - Dt, t.Delay, 1e-4f, "for 0.45 s (less this step)");
        }

        [Test]
        public void The_player_bar_shows_the_JS_hit_points_and_goes_low_under_a_quarter()
        {
            var f = Duel();
            f.Player.Health = 50.2f; f.Player.MaxHealth = 100f; f.Player.HealthScale = 0.5f;
            var m = Run(f);
            Assert.AreEqual(101, m.Player.HpValue, "ceil(50.2 / 0.5)");
            Assert.IsFalse(m.Player.Low);
            f.Player.Health = 20f;
            m.Tick(Dt, f);
            Assert.IsTrue(m.Player.Low);
        }

        [Test]
        public void Switching_to_another_foe_starts_its_bar_full_and_does_not_slide_from_the_last_one()
        {
            var f = Duel();
            f.Foe.Health = 10f;
            var m = Run(f, 90);
            Assert.Less(m.Foe.Hp, 0.2f);
            f.Foe = Foe("footman2", 80f);
            m.Tick(Dt, f);
            Assert.AreEqual(1f, m.Foe.Hp, 1e-4f);
            Assert.AreEqual(1f, m.Foe.Chip, 1e-4f);
        }

        // ---------------- hurt flash comes from the event stream ----------------

        [Test]
        public void A_hit_on_Kage_shakes_his_panel_and_a_hit_on_the_foe_shakes_the_foe_panel()
        {
            var m = Run(Duel(), 2);
            Assert.AreEqual(0f, m.Player.Hurt);
            m.Handle(HitOn("kage", 6f));
            Assert.AreEqual(1f, m.Player.Hurt, 1e-4f);
            Assert.AreEqual(0f, m.Foe.Hurt, "only the one hit");
            m.Handle(HitOn("footman", 6f));
            Assert.AreEqual(1f, m.Foe.Hurt, 1e-4f);
            Step(m, Duel(), 0.4f);
            Assert.AreEqual(0f, m.Player.Hurt, 1e-4f, "it settles in about a third of a second");
        }

        [Test]
        public void A_blocked_or_zero_damage_hit_does_not_shake_a_panel_and_a_stranger_is_ignored()
        {
            var m = Run(Duel(), 2);
            m.Handle(HitOn("kage", 0f));
            m.Handle(HitOn("someone_else", 9f));
            m.Handle(CombatEvent.HitResolved(2, CombatEventType.Blocked, null, null, "kage", "jab", 0, 0f, false, HitLevel.Mid, HitZone.Body, Vector2.zero, Vector2.zero));
            Assert.AreEqual(0f, m.Player.Hurt);
            Assert.AreEqual(0f, m.Foe.Hurt);
        }

        [Test]
        public void A_hit_on_the_boss_shakes_the_boss_bar()
        {
            var m = new FightHudModel();
            m.BindBoss(new HudBossInfo { Id = "raikon", Name = "Raikon", PhaseNames = new[] { "A", "B" }, Marks = new[] { 0.5f } });
            m.Tick(Dt, Boss());
            m.Handle(HitOn("raikon", 12f));
            Assert.AreEqual(1f, m.Boss.Hurt, 1e-4f);
        }

        // ---------------- rage, kunai, posture, combo ----------------

        [Test]
        public void The_rage_bar_fills_with_rage_and_says_ready_only_when_full()
        {
            var f = Duel();
            f.Player.Rage = 50f;
            var m = Run(f);
            Assert.AreEqual(0.5f, m.Player.RageFill, 1e-4f);
            Assert.IsFalse(m.Player.RageReady);
            f.Player.Rage = 100f;
            m.Tick(Dt, f);
            Assert.AreEqual(1f, m.Player.RageFill, 1e-4f);
            Assert.IsTrue(m.Player.RageReady, "Rage ready");
        }

        [Test]
        public void Rage_ready_pops_when_it_becomes_ready_and_not_again_while_it_stays_ready()
        {
            var f = Duel();
            f.Player.Rage = 99f;
            var m = Run(f);
            Assert.AreEqual(0, m.Player.RageReadyCount);
            f.Player.Rage = 100f;
            m.Tick(Dt, f);
            Assert.AreEqual(1, m.Player.RageReadyCount, "one pop");
            Assert.Greater(m.Player.RageReadyPop, 0f);
            Step(m, f, 1f);
            Assert.AreEqual(1, m.Player.RageReadyCount);
            Assert.AreEqual(0f, m.Player.RageReadyPop, 1e-4f, "the pop is over");
            f.Player.Rage = 0f;
            Step(m, f, 0.2f);
            Assert.IsFalse(m.Player.RageReady, "spent");
            f.Player.Rage = 100f;
            m.Tick(Dt, f);
            Assert.AreEqual(2, m.Player.RageReadyCount, "ready again, pops again");
        }

        [Test]
        public void A_fighter_with_no_rage_art_is_never_ready()
        {
            var f = Duel();
            f.Player.Rage = 100f;
            f.Player.CanRageArt = false;
            Assert.IsFalse(Run(f).Player.RageReady);
        }

        [Test]
        public void The_kunai_count_shows_for_a_thrower_and_follows_the_count()
        {
            var f = Duel();
            var m = Run(f);
            Assert.IsTrue(m.Player.KunaiShown);
            Assert.AreEqual(5, m.Player.Kunai);
            f.Player.Kunai = 2;
            m.Tick(Dt, f);
            Assert.AreEqual(2, m.Player.Kunai);
            f.Player.HasKunai = false;
            m.Tick(Dt, f);
            Assert.IsFalse(m.Player.KunaiShown);
        }

        [Test]
        public void Posture_follows_the_fighter_and_flags_a_broken_guard_on_either_panel()
        {
            var f = Duel();
            f.Player.Posture = 0.4f;
            f.Foe.Posture = 0.9f;
            var m = Run(f);
            Assert.AreEqual(0.4f, m.Player.Posture, 1e-4f);
            Assert.AreEqual(0.9f, m.Foe.Posture, 1e-4f);
            Assert.IsFalse(m.Player.PostureBroken);
            f.Player.Posture = 1f; f.Player.GuardBroken = true;
            m.Tick(Dt, f);
            Assert.IsTrue(m.Player.PostureBroken);
            Assert.IsFalse(m.Foe.PostureBroken);
        }

        [Test]
        public void A_combo_shows_from_two_hits_and_bumps_on_each_new_one()
        {
            var f = Duel();
            var m = Run(f, 2);
            Assert.IsFalse(m.Combo.Visible);
            f.Player.Combo = 1;
            m.Tick(Dt, f);
            Assert.IsFalse(m.Combo.Visible, "one hit is not a combo");
            f.Player.Combo = 2;
            m.Tick(Dt, f);
            Assert.IsTrue(m.Combo.Visible);
            Assert.AreEqual(2, m.Combo.Count);
            Assert.AreEqual(1f, m.Combo.Bump, 1e-4f, "bumps in big");
            Step(m, f, 0.3f);
            Assert.AreEqual(0f, m.Combo.Bump, 1e-4f);
            f.Player.Combo = 3;
            m.Tick(Dt, f);
            Assert.AreEqual(3, m.Combo.Count);
            Assert.AreEqual(1f, m.Combo.Bump, 1e-4f);
            f.Player.Combo = 0;
            Step(m, f, 0.5f);
            Assert.IsFalse(m.Combo.Visible);
            Assert.AreEqual(0f, m.Combo.Opacity, 1e-3f, "and fades out");
        }

        // ---------------- the clock and the round pips ----------------

        [Test]
        public void The_clock_counts_whole_seconds_down_goes_urgent_at_ten_and_reads_infinity_when_it_does_not_run()
        {
            var f = Duel();
            var m = Run(f);
            Assert.AreEqual("48", m.Clock.Text, "47.2 s rounds up");
            Assert.IsFalse(m.Clock.Urgent);
            f.TimeLeft = 9.4f;
            m.Tick(Dt, f);
            Assert.AreEqual("10", m.Clock.Text);
            Assert.IsTrue(m.Clock.Urgent, "10 s or less");
            f.TimeLeft = -3f;
            m.Tick(Dt, f);
            Assert.AreEqual("0", m.Clock.Text);
            f.ClockRuns = false;
            m.Tick(Dt, f);
            Assert.AreEqual("∞", m.Clock.Text);
            Assert.IsFalse(m.Clock.Urgent);
        }

        [Test]
        public void The_round_pips_are_best_of_N_with_Kages_wins_first_then_the_foes()
        {
            var f = Duel();
            f.RoundsToWin = 3;
            var m = Run(f);
            CollectionAssert.AreEqual(new[] { HudPip.Empty, HudPip.Empty, HudPip.Empty, HudPip.Empty, HudPip.Empty }, m.Clock.Pips);
            f.Player.RoundsWon = 1; f.Foe.RoundsWon = 2;
            m.Tick(Dt, f);
            CollectionAssert.AreEqual(new[] { HudPip.Kage, HudPip.Foe, HudPip.Foe, HudPip.Empty, HudPip.Empty }, m.Clock.Pips);
        }

        // ---------------- the road ----------------

        [Test]
        public void The_road_strip_fills_with_progress_and_marks_the_encounters_done_or_not()
        {
            var f = Road(0.4f);
            f.Road.EncountersCleared = 1;
            var m = Run(f);
            Assert.AreEqual(0.4f, m.Road.Fill, 1e-4f);
            Assert.AreEqual(2, m.Road.Marks.Count);
            Assert.AreEqual(0.25f, m.Road.Marks[0].At, 1e-4f);
            Assert.IsTrue(m.Road.Marks[0].Done, "the first gate is behind Kage");
            Assert.IsFalse(m.Road.Marks[1].Done);
        }

        [Test]
        public void The_road_label_reads_the_encounter_count_then_the_foes_and_wave_then_the_road_is_open()
        {
            var f = Road(0.1f);
            var m = Run(f);
            Assert.AreEqual("ENCOUNTERS 0/2", m.Road.Label);
            f.Road.InEncounter = true; f.Road.Wave = 1; f.Road.WaveCount = 3; f.Road.FoesLeft = 4;
            m.Tick(Dt, f);
            Assert.AreEqual("FOES 4 · WAVE 1/3", m.Road.Label);
            f.Road.InEncounter = true; f.Road.Wave = 0; f.Road.WaveCount = 3; f.Road.FoesLeft = 0;
            m.Tick(Dt, f);
            Assert.AreEqual("FOES 0 · WAVE 1/3", m.Road.Label, "before the first wave lands the wave reads 1");
            f.Road.InEncounter = false; f.Road.EncountersCleared = 2;
            m.Tick(Dt, f);
            Assert.AreEqual("THE ROAD IS OPEN", m.Road.Label);
        }

        [Test]
        public void A_wave_starting_flashes_the_wave_label_and_the_stage_ending_marks_it_finished()
        {
            var m = Run(Road(0.3f), 2);
            Assert.AreEqual(0f, m.Road.WavePulse);
            m.Handle(CombatEvent.WaveStarted(10, 1, "dev.footmen.e1"));
            Assert.AreEqual(1f, m.Road.WavePulse, 1e-4f);
            Assert.AreEqual(1, m.Road.WaveNumber, "the stretch's wave count, from the event");
            Step(m, Road(0.3f), 1.5f);
            Assert.AreEqual(0f, m.Road.WavePulse, 1e-4f);
            Assert.IsFalse(m.Finished);
            m.Handle(CombatEvent.StageCleared(900, "dev.footmen"));
            Assert.IsTrue(m.Finished);
            Assert.IsTrue(m.Cleared);
        }

        [Test]
        public void The_road_updates_as_Kage_walks()
        {
            var f = Road(0f);
            var m = Run(f);
            f.Road.Progress = 0.75f;
            m.Tick(Dt, f);
            Assert.AreEqual(0.75f, m.Road.Fill, 1e-4f);
        }

        [Test]
        public void The_foes_small_bars_pass_through_in_a_stretch()
        {
            var f = Road(0.2f);
            f.MiniBars = new List<HudMiniBar> { new HudMiniBar { World = new Vector2(3f, 1f), Ratio = 0.5f } };
            var m = Run(f);
            Assert.AreEqual(1, m.MiniBars.Count);
            Assert.AreEqual(0.5f, m.MiniBars[0].Ratio, 1e-4f);
            Assert.AreEqual(0, Run(Duel()).MiniBars.Count, "a duel has none");
        }

        // ---------------- the boss bar ----------------

        static FightHudModel BossModel()
        {
            var m = new FightHudModel();
            m.BindBoss(new HudBossInfo
            {
                Id = "raikon", Name = "Raikon", Title = "the Lightning Ascendant", ArtId = "boss_raikon",
                PhaseNames = new[] { "The Storm Gathers", "Redline", "Thunder Swing" }, Marks = new[] { 0.66f, 0.33f },
            });
            return m;
        }

        [Test]
        public void The_boss_bar_carries_the_boss_name_title_art_and_phase_marks()
        {
            var m = BossModel();
            m.Tick(Dt, Boss());
            Assert.AreEqual("RAIKON", m.Boss.Name);
            Assert.AreEqual("the Lightning Ascendant", m.Boss.Title);
            Assert.AreEqual("boss_raikon", m.Boss.ArtId);
            CollectionAssert.AreEqual(new[] { 0.66f, 0.33f }, m.Boss.Marks);
            Assert.AreEqual(3, m.Boss.PhaseCount);
            Assert.AreEqual(0, m.Boss.PhaseIndex);
            Assert.AreEqual("PHASE I", m.Boss.PhaseLabel);
            CollectionAssert.AreEqual(new[] { HudPhaseDot.On, HudPhaseDot.Upcoming, HudPhaseDot.Upcoming }, m.Boss.Dots);
        }

        [Test]
        public void A_phase_event_moves_the_boss_bar_to_that_phase_and_glitches_the_card()
        {
            var m = BossModel();
            m.Tick(Dt, Boss());
            m.Handle(CombatEvent.BossPhaseChanged(30, "raikon", 1, "Redline"));
            Assert.AreEqual(1, m.Boss.PhaseIndex);
            Assert.AreEqual("PHASE II", m.Boss.PhaseLabel);
            Assert.AreEqual("REDLINE", m.Boss.PhaseName);
            CollectionAssert.AreEqual(new[] { HudPhaseDot.Done, HudPhaseDot.On, HudPhaseDot.Upcoming }, m.Boss.Dots);
            Assert.AreEqual(1f, m.Boss.PhaseGlitch, 1e-4f);
            Step(m, Boss(), 1f);
            Assert.AreEqual(0f, m.Boss.PhaseGlitch, 1e-4f);
        }

        [Test]
        public void The_opening_phase_event_does_not_glitch_and_another_bosses_events_are_ignored()
        {
            var m = BossModel();
            m.Tick(Dt, Boss());
            m.Handle(CombatEvent.BossPhaseChanged(1, "raikon", 0, "The Storm Gathers"));
            Assert.AreEqual(0f, m.Boss.PhaseGlitch);
            m.Handle(CombatEvent.BossPhaseChanged(2, "someone_else", 2, "x"));
            Assert.AreEqual(0, m.Boss.PhaseIndex);
        }

        [Test]
        public void The_boss_bar_drains_with_a_trail_and_pulses_red_under_a_quarter()
        {
            var f = Boss();
            var m = BossModel();
            m.Tick(Dt, f);
            f.Foe.Health = 100f;     // of 640
            m.Tick(Dt, f);
            Step(m, f, 0.3f);
            Assert.AreEqual(1f, m.Boss.Chip, 1e-4f, "the trail waits");
            Step(m, f, 4f);
            Assert.AreEqual(100f / 640f, m.Boss.Hp, 0.01f);
            Assert.IsTrue(m.Boss.Low, "under a quarter");
        }

        // ---------------- the model reads only events and state ----------------

        [Test]
        public void The_model_takes_only_plain_values_and_events_never_a_fighter()
        {
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var type = typeof(FightHudModel);
            foreach (var m in type.GetMethods(flags))
                foreach (var p in m.GetParameters())
                    Assert.IsFalse(typeof(Component).IsAssignableFrom(p.ParameterType) || typeof(Object).IsAssignableFrom(p.ParameterType),
                        $"{m.Name}({p.Name}): a Unity object would let the HUD reach into the fight");
            foreach (var f in type.GetFields(flags | BindingFlags.NonPublic))
                Assert.IsFalse(typeof(Object).IsAssignableFrom(f.FieldType), $"{f.Name} holds a Unity object");
            foreach (var t in new[] { typeof(HudFrame), typeof(HudFighterState), typeof(HudRoadState), typeof(HudBossInfo) })
                foreach (var f in t.GetFields())
                    Assert.IsFalse(typeof(Component).IsAssignableFrom(f.FieldType), $"{t.Name}.{f.Name} holds a component");
        }

        [Test]
        public void A_recorded_event_stream_replays_to_the_same_view_values()
        {
            var events = new List<CombatEvent>
            {
                HitOn("kage", 5f), HitOn("footman", 7f), CombatEvent.WaveStarted(40, 2, "dev.footmen.e1"),
                CombatEvent.BossPhaseChanged(50, "raikon", 1, "Redline"),
            };
            string View(FightHudModel m) => $"{m.Player.Hurt:0.###} {m.Foe.Hurt:0.###} {m.Road.WaveNumber} {m.Boss.PhaseIndex} {m.Road.WavePulse:0.###} {m.Boss.PhaseGlitch:0.###}";
            string Play()
            {
                var m = BossModel();
                m.Tick(Dt, Duel());
                foreach (var e in events) { m.Handle(e); m.Tick(Dt, Duel()); }
                return View(m);
            }
            Assert.AreEqual(Play(), Play());
        }
    }
}
