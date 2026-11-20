using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Raikon in a fight (SAN-107), through the fight harness: Thunder Swing (a readable wind-up, a cut and a ground wave; the wave is jumped or
    /// dashed through, it ignores a guard; in the last phase a second wave follows late), Storm Break (the shock is a hazard: a ground mark
    /// through the wind-up, a strike with the slam; stepping out or jumping it is safe; the slam crushes a guard), Skyfall (a mark that follows
    /// Kage, locks, and a bolt; step off it; only called from far), Redline Counter (a blow into the settled stance is turned aside and punished;
    /// waiting it out is safe and leaves him spent), his armour and no-grab, and his three phases. Raikon is scripted to press his ability slots;
    /// Kage is scripted. Only events and public state are read.
    /// </summary>
    public class RaikonFightTests
    {
        const int Seed = 107;
        const int SwingSlot = 1, BreakSlot = 2, SkySlot = 3, RedlineSlot = 4;
        const string Swing = RaikonAbilities.MoveSwing, Swing2 = RaikonAbilities.MoveSwing2, Break = RaikonAbilities.MoveBreak, Sky = RaikonAbilities.MoveSkyfall;
        const string Redline = RaikonAbilities.MoveRedline, RedlineShort = RaikonAbilities.MoveRedlineShort, Punish = RaikonAbilities.MovePunish;
        readonly List<HeadlessFight> _fights = new();
        readonly List<(int frame, string cue)> _cues = new();
        HeadlessFight _current;

        [SetUp]
        public void SetUp() => EffectCues.Played += OnCue;

        [TearDown]
        public void TearDown()
        {
            EffectCues.Played -= OnCue;
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            _cues.Clear();
        }

        void OnCue(string id, Vector2 pos, in CueArgs a) { if (_current != null && a.owner == _current.Ronin) _cues.Add((_current.Ctx.Clock.Frame, id)); }

        static HeadlessFight New(float kageX, float raikonX, Foe foe = Foe.Scripted)
        {
            var f = new HeadlessFight(Seed, foe, kageX, raikonX, foeDef: CharacterCatalog.Get(Raikon.Id));
            f.Kage.maxHealth = f.Kage.health = 1e6f;
            return f;
        }

        HeadlessFight Fight(float kageX, float raikonX, Foe foe = Foe.Scripted)
        {
            var f = New(kageX, raikonX, foe);
            _fights.Add(f);
            _current = f;
            return f;
        }

        static bool In(Fighter s, string moveId) => s.state == FState.Attack && s.move != null && s.move.id == moveId;

        /// <summary>Raikon presses <paramref name="slot"/> on frame 2, then stands.</summary>
        static void Press(HeadlessFight f, int slot) => f.RoninInput.Add(2).Add(1, ability: slot).Add(5000);

        static void Phase(HeadlessFight f, float fraction) => f.Ronin.health = f.Ronin.maxHealth * fraction;
        static void PhaseTwo(HeadlessFight f) => Phase(f, 0.65f);
        static void PhaseThree(HeadlessFight f) => Phase(f, 0.32f);

        static IEnumerable<CombatEvent> From(HeadlessFight f, CombatEventType t, string moveId) => f.Events(t).Where(e => e.Source == f.Ronin && e.MoveId == moveId);

        static bool Touched(HeadlessFight f, string moveId) =>
            From(f, CombatEventType.Hit, moveId).Any() || From(f, CombatEventType.Blocked, moveId).Any() || f.Events(CombatEventType.Parried).Any(e => e.Source == f.Ronin && e.MoveId == moveId);

        /// <summary>Runs <paramref name="frames"/> and returns the loop frames on which a new Thunder Swing wave appeared.</summary>
        static List<int> WaveSpawns(HeadlessFight f, int frames)
        {
            var seen = new HashSet<Projectile>();
            var at = new List<int>();
            for (int i = 0; i < frames; i++)
            {
                f.Run(1);
                foreach (var p in f.Ctx.Projectiles) if (p.Def.id == RaikonAbilities.WaveId && seen.Add(p)) at.Add(f.Ctx.Clock.Frame);
            }
            return at;
        }

        static int Waves(HeadlessFight f) => From(f, CombatEventType.Hit, RaikonAbilities.WaveId).Count();

        // ================================================================ Thunder Swing

        const float FarKage = -3.6f, FarRaikon = 1.0f;      // 4.6 m: past the cut, in the wave's path

        [Test]
        public void Thunder_Swing_winds_up_then_cuts_and_sends_a_wave_along_the_floor_that_hits_a_standing_Kage()
        {
            var f = Fight(FarKage, FarRaikon);
            Press(f, SwingSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => In(f.Ronin, Swing), 10);
            Assert.IsTrue(In(f.Ronin, Swing), "his first slot starts it");
            Assert.AreEqual(1f, f.Ronin.PlaybackRate, 1e-3f, "his moves play as authored: the tachi's JS speed does not shorten his tells");
            int start = f.Ctx.Clock.Frame;
            f.Run(30);
            Assert.AreEqual(0, f.Ctx.Projectiles.Count, "the wind-up: nothing thrown yet");
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Ronin), "and nothing lands");
            f.RunUntil(() => f.Ctx.Projectiles.Count > 0, 30);
            Assert.AreEqual(1, f.Ctx.Projectiles.Count, "the wave");
            var wave = f.Ctx.Projectiles[0];
            Assert.AreEqual(RaikonAbilities.WaveId, wave.Def.id);
            Assert.GreaterOrEqual(f.Ctx.Clock.Frame - start, f.Ronin.move.hits[0].from - 1, "released after the wind-up");
            Assert.Less(wave.Position.y, 0.6f, "along the floor");
            Assert.Less(wave.Position.x, f.Ronin.Pos.x, "toward Kage");
            f.Run(60);
            var hits = From(f, CombatEventType.Hit, RaikonAbilities.WaveId).ToList();
            Assert.AreEqual(1, hits.Count, "the wave found Kage once");
            Assert.AreSame(f.Kage, hits[0].Target);
            Assert.IsTrue(f.Frames.Any(l => l.Contains(" Down ") || l.Contains(" Launched ")), "and knocked him down");
            Assert.IsTrue(_cues.Any(c => c.cue == RaikonAbilities.BladeChargeCue), "red sparks on the blade through the wind-up");
            Assert.AreEqual(1, _cues.Count(c => c.cue == RaikonAbilities.GlintCue), "and a glint just before the release");
        }

        [Test]
        public void A_guard_does_not_stop_the_wave()
        {
            var f = Fight(FarKage, FarRaikon);
            Press(f, SwingSlot);
            f.KageInput.Add(5000, buttons: "G");
            f.Run(120);
            Assert.AreEqual(1, Waves(f), "unblockable");
            Assert.AreEqual(0, From(f, CombatEventType.Blocked, RaikonAbilities.WaveId).Count());
        }

        [Test]
        public void The_cut_reaches_about_as_far_as_the_JS_area_and_the_wave_covers_the_rest()
        {
            // the JS area: 210 px wide from 10 px behind him, x his scale 1.24 = 2.05 m in front of his feet; Kage's body is a little in front of his root
            float far = 0f;
            for (float d = 1.0f; d <= 3.2f; d += 0.1f)
            {
                var f = New(-d / 2f, d / 2f);
                Press(f, SwingSlot);
                f.KageInput.Add(5000);
                f.Run(60);
                if (From(f, CombatEventType.Hit, Swing).Any()) far = d;
                f.Dispose();
            }
            Debug.Log($"SAN-107 Thunder Swing cut reach: lands on a standing Kage up to {far:0.0} m away (JS area 2.05 m + his body)");
            Assert.That(far, Is.InRange(1.8f, 2.5f), "the painted blade is short (0.875 m); the arc is wide to make up the JS area");
        }

        [Test]
        public void The_cut_up_close_can_be_blocked()
        {
            var f = Fight(-0.9f, 0.9f);
            Press(f, SwingSlot);
            f.KageInput.Add(5000, buttons: "G");
            f.Run(120);
            Assert.AreEqual(1, From(f, CombatEventType.Blocked, Swing).Count(), "the cut is blockable (the JS area has no unb)");
            Assert.AreEqual(0, From(f, CombatEventType.Hit, Swing).Count());
        }

        /// <summary>Searches Kage's timing for <paramref name="answer"/> against the wave; returns the first start frame that avoided it, or -1.</summary>
        int FindTiming(System.Action<ScriptedInput, int> answer, System.Func<HeadlessFight, bool> success, out HeadlessFight result, int slot = SwingSlot,
            System.Action<HeadlessFight> setup = null, int run = 200)
        {
            result = null;
            for (int at = 20; at <= 110; at++)
            {
                var f = New(FarKage, FarRaikon);
                setup?.Invoke(f);
                Press(f, slot);
                answer(f.KageInput, at);
                f.Run(run);
                if (success(f)) { result = f; return at; }
                f.Dispose();
            }
            return -1;
        }

        [Test]
        public void A_jump_clears_the_wave()
        {
            int at = FindTiming((k, t) => k.Add(t).Add(40, dy: 1).Add(5000), f => Waves(f) == 0 && !f.Events(CombatEventType.Hit).Any(e => e.Source == f.Ronin), out var hit);
            Assert.GreaterOrEqual(at, 0, "the wave runs along the floor: a jump goes over it");
            _fights.Add(hit);
        }

        [Test]
        public void A_dash_through_the_wave_avoids_it()
        {
            int at = FindTiming((k, t) => k.Add(t).Add(1, dash: true, fwd: 1).Add(5000), f => Waves(f) == 0 && !f.Events(CombatEventType.Hit).Any(e => e.Source == f.Ronin), out var hit);
            Assert.GreaterOrEqual(at, 0, "a dash at the wave goes through it");
            _fights.Add(hit);
        }

        [Test]
        public void Standing_still_is_not_avoiding_it()
        {
            var f = Fight(FarKage, FarRaikon);
            Press(f, SwingSlot);
            f.KageInput.Add(5000);
            f.Run(160);
            Assert.AreEqual(1, Waves(f));
        }

        [Test]
        public void In_the_last_phase_a_second_wave_follows_late_and_a_dodge_made_too_early_is_hit()
        {
            // the early phases throw one wave
            var one = Fight(FarKage, FarRaikon);
            Press(one, SwingSlot);
            one.KageInput.Add(5000);
            var spawns = WaveSpawns(one, 200);
            Assert.AreEqual(1, spawns.Count, "one wave in phase one");

            var two = Fight(FarKage, FarRaikon);
            PhaseThree(two);
            Press(two, SwingSlot);
            two.KageInput.Add(5000);
            bool second = false;
            var spawns2 = new List<int>();
            var seen = new HashSet<Projectile>();
            for (int i = 0; i < 260; i++)
            {
                two.Run(1);
                second |= In(two.Ronin, Swing2);
                foreach (var p in two.Ctx.Projectiles) if (p.Def.id == RaikonAbilities.WaveId && seen.Add(p)) spawns2.Add(two.Ronin.moveFrame);
            }
            Assert.IsTrue(second, "the last phase starts the two-wave swing");
            Assert.AreEqual(2, spawns2.Count, "two waves");
            Assert.AreEqual(1f, two.Ronin.PlaybackRate, 1e-3f, "his moves play as authored in the last phase too");
            Assert.AreEqual(42, spawns2[1] - spawns2[0], "the second 0.7 s of the move after the first (the first wave landing on Kage freezes both for its hit-stop, so the move frames are counted, not the loop's)");
            Assert.IsTrue(_cues.Count(c => c.cue == RaikonAbilities.GlintCue) >= 2, "the blade glints again before the second release");
        }

        [Test]
        public void Jumping_the_first_wave_alone_is_hit_by_the_second()
        {
            int firstWave;
            using (var control = New(FarKage, FarRaikon))
            {
                PhaseThree(control);
                Press(control, SwingSlot);
                control.KageInput.Add(5000);
                control.Run(100);
                firstWave = From(control, CombatEventType.Hit, RaikonAbilities.WaveId).First().Frame;     // a Kage who stands is hit by the first wave here
            }
            int secondWave = firstWave + 42;      // the second arrives 0.7 s later (no hit-stop when the first is jumped)

            // a jump timed for the first wave, then standing: the second wave arrives after he has landed
            HeadlessFight hit = null;
            int early = -1;
            for (int j1 = firstWave - 24; j1 <= firstWave - 8 && early < 0; j1 += 4)
            {
                var f = New(FarKage, FarRaikon);
                PhaseThree(f);
                Press(f, SwingSlot);
                f.KageInput.Add(j1).Add(20, dy: 1).Add(5000);
                f.Run(260);
                var waveHits = From(f, CombatEventType.Hit, RaikonAbilities.WaveId).ToList();
                if (waveHits.Count == 1 && waveHits[0].Frame > firstWave + 20) { early = j1; hit = f; } else f.Dispose();
            }
            Assert.GreaterOrEqual(early, 0, "a dodge made too early meets the second wave");
            _fights.Add(hit);
            Assert.Greater(From(hit, CombatEventType.Hit, RaikonAbilities.WaveId).Single().Frame, firstWave + 20, "and it is the late one that lands");
        }

        // ================================================================ Storm Break

        [Test]
        public void Storm_Break_lays_a_ground_mark_through_its_wind_up_and_the_shock_strikes_with_the_slam()
        {
            var f = Fight(-1.5f, 2.2f);           // 3.7 m: outside the slam, inside the shock
            Press(f, BreakSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => f.Ctx.Hazards.Count > 0, 10);
            Assert.AreEqual(1, f.Ctx.Hazards.Count, "the shock is laid down at once");
            var h = f.Ctx.Hazards[0];
            Assert.AreEqual(RaikonAbilities.ShockId, h.Def.id);
            Assert.AreEqual(HazardPhase.Telegraph, h.Phase);
            Assert.Less(h.Centre.x, f.Ronin.Pos.x, "in front of him");

            f.RunUntil(() => !h.Alive || h.Phase == HazardPhase.Strike, 80);
            int strikeFrame = f.Ctx.Clock.Frame - 1;
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Frame < strikeFrame), "the mark never hits");
            Assert.AreEqual(f.Ronin.move.hits[0].from, f.Ronin.moveFrame, "it strikes on the slam's first frame");
            f.Run(40);
            var shock = f.Events(CombatEventType.Hit).Where(e => e.MoveId == RaikonAbilities.ShockId).ToList();
            Assert.AreEqual(1, shock.Count, "the shock struck Kage once");
            Assert.AreSame(f.Kage, shock[0].Target);
            Assert.IsTrue(_cues.Any(c => c.cue == RaikonAbilities.BladeChargeCue), "sparks on the raised blade");
        }

        [Test]
        public void Stepping_out_of_the_shock_is_safe()
        {
            var f = Fight(-1.5f, 2.2f);
            Press(f, BreakSlot);
            f.KageInput.Add(6).Add(1, dash: true, fwd: -1).Add(60, fwd: -1).Add(5000);
            f.Run(110);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Ronin), "nothing reached him");
        }

        [Test]
        public void A_jump_clears_the_shock()
        {
            bool cleared = false;
            for (int at = 30; at <= 50 && !cleared; at++)
            {
                var f = New(-1.5f, 2.2f);
                Press(f, BreakSlot);
                f.KageInput.Add(at).Add(40, dy: 1).Add(5000);
                f.Run(110);
                cleared = !f.Events(CombatEventType.Hit).Any(e => e.Source == f.Ronin);
                f.Dispose();
            }
            Assert.IsTrue(cleared, "the shock is a floor box: a jump goes over it");
        }

        [Test]
        public void The_Storm_Break_slam_crushes_a_guard()
        {
            var f = Fight(-0.8f, 0.8f);
            Press(f, BreakSlot);
            f.KageInput.Add(5000, buttons: "G");
            f.Run(90);
            Assert.AreEqual(1, From(f, CombatEventType.Blocked, Break).Count(), "guarded");
            Assert.IsTrue(f.Events(CombatEventType.GuardBroken).Any(e => e.Target == f.Kage), "and the guard broke (JS gb 110)");
        }

        // ================================================================ Skyfall

        const float SkyKage = -5f, SkyRaikon = 3.5f;

        [Test]
        public void Skyfall_opens_a_mark_under_Kage_that_follows_him_then_locks_and_a_bolt_strikes_it()
        {
            var f = Fight(SkyKage, SkyRaikon);
            Press(f, SkySlot);
            f.KageInput.Add(4).Add(18, fwd: 1).Add(5000);       // he walks while it charges
            f.RunUntil(() => f.Ctx.Hazards.Count > 0, 10);
            Assert.AreEqual(1, f.Ctx.Hazards.Count, "the mark opens at once");
            var h = f.Ctx.Hazards[0];
            Assert.AreEqual(RaikonAbilities.BoltId, h.Def.id);
            Assert.AreEqual(HazardPhase.Telegraph, h.Phase);
            Assert.AreEqual(f.Kage.Pos.x, h.Centre.x, 0.3f, "under Kage, not under Raikon");
            f.Run(14);
            Assert.AreEqual(f.Kage.Pos.x, h.Centre.x, 0.15f, "it follows him while the storm gathers");
            f.RunUntil(() => f.Ronin.moveFrame >= f.Ronin.move.hazard.followFrames + 2, 60);
            float locked = h.Centre.x;
            f.KageInput.Add(60, fwd: 1);
            f.Run(10);
            Assert.AreEqual(locked, h.Centre.x, 1e-4f, "locked: it stays where it was");
            f.RunUntil(() => h.Phase == HazardPhase.Strike || !h.Alive, 80);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Ronin && e.Frame < f.Ctx.Clock.Frame - 1), "the mark never hits before the bolt");
            Assert.IsTrue(_cues.Any(c => c.cue == RaikonAbilities.StormMantleCue), "the storm gathers round him");
        }

        [Test]
        public void Standing_on_the_locked_mark_is_struck_by_the_bolt()
        {
            var f = Fight(SkyKage, SkyRaikon);
            Press(f, SkySlot);
            f.KageInput.Add(5000);
            f.Run(100);
            var hits = From(f, CombatEventType.Hit, RaikonAbilities.BoltId).ToList();
            Assert.AreEqual(1, hits.Count, "the bolt strikes once");
            Assert.AreSame(f.Kage, hits[0].Target);
            Assert.IsTrue(_cues.Any(c => c.cue == RaikonAbilities.SkyStrikeCue), "the sky-strike cue");
            Assert.AreEqual(f.Ronin.move.hazard.telegraphFrames + 2 + 1, hits[0].Frame - 0, 3, "half a second after the lock (frame 58 + the press)");
        }

        [Test]
        public void Stepping_off_the_locked_mark_in_time_avoids_the_bolt()
        {
            var f = Fight(SkyKage, SkyRaikon);
            Press(f, SkySlot);
            f.KageInput.Add(32).Add(1, dash: true, fwd: 1).Add(40, fwd: 1).Add(5000);       // after the lock: off the mark
            f.Run(110);
            Assert.AreEqual(0, From(f, CombatEventType.Hit, RaikonAbilities.BoltId).Count(), "he was not under it");
        }

        [Test]
        public void Walking_with_the_mark_is_not_leaving_it_before_it_locks()
        {
            var f = Fight(SkyKage, SkyRaikon);
            Press(f, SkySlot);
            f.KageInput.Add(30, fwd: 1).Add(1, buttons: "G").Add(5000);       // walking while it follows, then stopping: the lock is under him
            f.Run(110);
            Assert.AreEqual(1, From(f, CombatEventType.Hit, RaikonAbilities.BoltId).Count() + From(f, CombatEventType.Blocked, RaikonAbilities.BoltId).Count(), "the mark followed him: he is on it when it locks");
        }

        // ================================================================ Redline Counter

        static bool StanceUp(Fighter s) => (In(s, Redline) || In(s, RedlineShort)) && s.moveFrame >= s.move.counterFrom && s.moveFrame <= s.move.counterTo;

        [Test]
        public void A_blow_into_the_settled_Redline_stance_is_turned_aside_and_punished_unblockably()
        {
            var f = Fight(-0.4f, 0.4f);
            PhaseTwo(f);
            Press(f, RedlineSlot);
            f.KageInput.Add(24).Add(1, buttons: "PG").Add(5000, buttons: "G");        // a jab once the stance has settled, then a guard up
            f.Run(140);
            var countered = f.Events(CombatEventType.Countered).ToList();
            Assert.AreEqual(1, countered.Count, "the blow was caught");
            Assert.AreSame(f.Ronin, countered[0].Target);
            var punish = From(f, CombatEventType.Hit, Punish).ToList();
            Assert.AreEqual(1, punish.Count, "and answered, through a raised guard (unblockable)");
            Assert.IsTrue(punish[0].Crit, "a critical");
            Assert.Greater(punish[0].Frame, countered[0].Frame);
        }

        [Test]
        public void A_blow_before_the_stance_settles_lands_as_a_plain_hit()
        {
            var f = Fight(-0.4f, 0.4f);
            PhaseTwo(f);
            Press(f, RedlineSlot);
            f.KageInput.Add(2).Add(1, buttons: "P").Add(5000);
            f.Run(40);
            Assert.AreEqual(0, f.Events(CombatEventType.Countered).Count(), "not yet settled");
            Assert.IsTrue(f.Events(CombatEventType.Hit).Any(e => e.Source == f.Kage && e.Target == f.Ronin), "the jab lands");
        }

        [Test]
        public void Waiting_out_Redline_is_safe_and_the_stance_is_spent()
        {
            var f = Fight(-0.4f, 0.4f);
            PhaseTwo(f);
            Press(f, RedlineSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => StanceUp(f.Ronin), 30);
            Assert.IsTrue(StanceUp(f.Ronin), "the stance is up (phase two)");
            Assert.AreEqual(90, f.Ronin.move.counterTo + 1, "held 1.5 s in phase two");
            float before = f.Ronin.Posture;
            f.RunUntil(() => !In(f.Ronin, Redline), 200);
            Assert.AreEqual(FState.Idle, f.Ronin.state);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Ronin), "nothing struck Kage");
            Assert.AreEqual(0, f.Events(CombatEventType.Countered).Count());
            Assert.Greater(f.Ronin.PostureFill, 0.2f, "spent: a large share of his posture is gone");
        }

        [Test]
        public void After_the_stance_is_spent_he_is_open_and_a_blow_lands()
        {
            var f = Fight(-0.4f, 0.4f);
            PhaseTwo(f);
            Press(f, RedlineSlot);
            f.KageInput.Add(100).Add(8, fwd: 1).Add(1, buttons: "P").Add(5000);
            f.Run(140);
            Assert.AreEqual(0, f.Events(CombatEventType.Countered).Count(), "the guard is down");
            Assert.IsTrue(f.Events(CombatEventType.Hit).Any(e => e.Source == f.Kage && e.Target == f.Ronin), "a jab in the spent stance lands");
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Ronin));
        }

        [Test]
        public void The_last_phase_holds_the_stance_shorter()
        {
            var f = Fight(-0.4f, 0.4f);
            PhaseThree(f);
            Press(f, RedlineSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => In(f.Ronin, RedlineShort), 30);
            Assert.IsTrue(In(f.Ronin, RedlineShort), "phase three: JS stance[1], 1.25 s");
            Assert.AreEqual(75, f.Ronin.move.counterTo + 1);
        }

        [Test]
        public void A_scripted_Redline_press_in_phase_one_does_nothing()
        {
            var f = Fight(-0.4f, 0.4f);
            Press(f, RedlineSlot);
            f.KageInput.Add(5000);
            f.Run(30);
            Assert.IsFalse(In(f.Ronin, Redline), "Redline Counter is phase two's");
        }

        // ================================================================ armour, no-grab, phases, the AI

        [Test]
        public void A_light_hit_does_not_flinch_him()
        {
            var f = Fight(-0.4f, 0.4f);
            f.RoninInput.Add(5000);
            f.KageInput.Add(4).Add(10, fwd: 1).Add(1, buttons: "P").Add(5000);
            float hp = f.Ronin.health;
            bool flinched = false;
            for (int i = 0; i < 40; i++) { f.Run(1); flinched |= f.Ronin.state == FState.Hitstun; }
            Assert.Less(f.Ronin.health, hp, "the jab landed");
            Assert.IsFalse(flinched, "Armour: a light hit does not flinch him");
        }

        [Test]
        public void A_throw_fails_on_him()
        {
            var f = Fight(-0.3f, 0.3f);
            f.RoninInput.Add(5000);
            f.KageInput.Add(4).Add(1, fwd: 1, buttons: "P");          // the shoulder throw
            bool thrown = false;
            for (int i = 0; i < 90; i++) { f.Run(1); thrown |= f.Ronin.state == FState.Thrown; }
            Assert.IsFalse(thrown, "never held (JS noGrab)");
            Assert.IsFalse(f.Kage.Busy, "the whiff played out");
        }

        [Test]
        public void The_phases_open_at_66_and_33_percent_with_the_JS_lists_the_second_wave_and_aggression()
        {
            var f = Fight(-1.5f, 1.5f, Foe.Ai);
            f.KageInput.Add(5000);
            f.Run(3);
            Assert.AreEqual(0, f.Ronin.Phases.Index);
            Assert.IsTrue(f.Ronin.AbilityEnabled("thunderSwing") && f.Ronin.AbilityEnabled("stormBreak") && f.Ronin.AbilityEnabled("skyfall"));
            Assert.IsFalse(f.Ronin.AbilityEnabled("redlineCounter"), "phase one has no Redline Counter");
            Assert.AreEqual(0f, f.Ronin.SecondWaveDelay);
            Assert.IsFalse(f.Ai.aggressive);

            Phase(f, 0.67f);
            f.Run(2);
            Assert.AreEqual(0, f.Ronin.Phases.Index, "67% is above the JS threshold");
            Phase(f, 0.66f);
            f.Run(2);
            Assert.AreEqual(1, f.Ronin.Phases.Index, "the JS threshold: 66%");
            Assert.IsTrue(f.Ronin.AbilityEnabled("redlineCounter"), "Redline Counter is added");
            Assert.AreEqual(0f, f.Ronin.SecondWaveDelay, "no second wave yet");
            Assert.IsFalse(f.Ai.aggressive);

            Phase(f, 0.34f);
            f.Run(2);
            Assert.AreEqual(1, f.Ronin.Phases.Index, "34% is above the last threshold");
            Phase(f, 0.33f);
            f.Run(2);
            Assert.AreEqual(2, f.Ronin.Phases.Index, "the JS threshold: 33%");
            Assert.AreEqual(0.7f, f.Ronin.SecondWaveDelay, 1e-6f, "the late second wave");
            Assert.IsTrue(f.Ai.aggressive, "the JS aggressive flag");
            Assert.AreEqual(AiProfiles.Get("boss").id, f.Ai.profile.id, "still the boss profile");
            CollectionAssert.AreEqual(new[] { "The Storm Gathers", "Redline", "Lightning Ascendant" }, f.Events(CombatEventType.BossPhaseChanged).Select(e => e.Name).ToArray());
            Assert.IsTrue(f.Ronin.Has(CharacterFlags.Armour) && f.Ronin.Has(CharacterFlags.NoGrab), "armour and no-grab in every phase");
        }

        [Test]
        public void A_blow_across_both_thresholds_opens_both_phases_in_order()
        {
            var f = Fight(-1.5f, 1.5f, Foe.Ai);
            f.KageInput.Add(5000);
            f.Run(3);
            Phase(f, 0.2f);
            f.Run(2);
            Assert.AreEqual(2, f.Ronin.Phases.Index);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, f.Events(CombatEventType.BossPhaseChanged).Select(e => e.Index).ToArray());
        }

        [Test]
        public void His_AI_calls_Skyfall_from_far_and_never_up_close_and_swings_at_mid_range()
        {
            var far = Fight(-6f, 3f, Foe.Ai);
            far.KageInput.Add(1500);
            bool sky = false;
            far.RunUntil(() => sky |= In(far.Ronin, Sky), 1500);
            Assert.IsTrue(sky, "Skyfall against a Kage who keeps his distance (9 m)");

            var near = Fight(-1.0f, 1.0f, Foe.Ai);
            near.KageInput.Add(1800);
            bool skyNear = false, swing = false, brk = false;
            near.RunUntil(() => { skyNear |= In(near.Ronin, Sky); brk |= In(near.Ronin, Break); return false; }, 1800);
            Assert.IsFalse(skyNear, "never from within 4 m");
            Assert.IsTrue(brk, "Storm Break up close");

            var mid = Fight(-2.0f, 2.0f, Foe.Ai);
            mid.KageInput.Add(2400);
            mid.RunUntil(() => swing |= In(mid.Ronin, Swing), 2400);
            Assert.IsTrue(swing, "Thunder Swing from mid range");
        }
    }
}
