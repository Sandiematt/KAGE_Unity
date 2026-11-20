using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Shiden in a fight (SAN-106), through the fight harness: the charging thrust (a readable wind-up, then a run with the point out across
    /// most of the arena; it lands, a guard blocks it, a parry turns it, a jump clears it, a roll gets through), Vanguard Break (the shockwave
    /// is a hazard: a ground mark through the wind-up, a strike with the slam; stepping back out of it is safe; the slam crushes a guard),
    /// the Braced Spear (a rush onto the point is met by a critical thrust; waiting it out is safe and leaves him open), Hold the Line (a
    /// counter stance in phase two), no-grab (throws fail) and his two phases. Shiden is scripted to press his ability slots; Kage is
    /// scripted. Only events and public state are read.
    /// </summary>
    public class ShidenFightTests
    {
        const int Seed = 106;
        const int BreakSlot = 1, BraceSlot = 2, GuardSlot = 3, ChargeSlot = 4;
        const string Charge = "shiden.chargingThrust", ChargeStop = "shiden.chargeStop", Break = "shiden.vanguardBreak";
        const string Brace = "shiden.brace", BraceThrust = "shiden.braceThrust", HoldTheLine = "shiden.holdTheLine", Punish = "shiden.holdTheLinePunish";
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

        static HeadlessFight New(float kageX, float shidenX, Foe foe = Foe.Scripted)
        {
            var f = new HeadlessFight(Seed, foe, kageX, shidenX, foeDef: CharacterCatalog.Get(Shiden.Id));
            f.Kage.maxHealth = f.Kage.health = 1e6f;
            return f;
        }

        HeadlessFight Fight(float kageX, float shidenX, Foe foe = Foe.Scripted)
        {
            var f = New(kageX, shidenX, foe);
            _fights.Add(f);
            _current = f;
            return f;
        }

        static bool In(Fighter s, string moveId) => s.state == FState.Attack && s.move != null && s.move.id == moveId;

        /// <summary>Shiden presses <paramref name="slot"/> on frame 2, then stands.</summary>
        static void Press(HeadlessFight f, int slot) => f.RoninInput.Add(2).Add(1, ability: slot).Add(5000);

        static void PhaseTwo(HeadlessFight f) => f.Ronin.health = f.Ronin.maxHealth * 0.5f;

        static IEnumerable<CombatEvent> From(HeadlessFight f, CombatEventType t, string moveId) => f.Events(t).Where(e => e.Source == f.Ronin && e.MoveId == moveId);

        static bool Touched(HeadlessFight f, string moveId) =>
            From(f, CombatEventType.Hit, moveId).Any() || From(f, CombatEventType.Blocked, moveId).Any() || f.Events(CombatEventType.Parried).Any(e => e.Source == f.Ronin && e.MoveId == moveId);

        // ================================================================ the charging thrust

        const float ChargeKage = -4.5f, ChargeShiden = 3.5f;      // 8 m apart: across most of the arena

        [Test]
        public void The_charging_thrust_winds_up_then_runs_across_the_arena_and_lands()
        {
            var f = Fight(ChargeKage, ChargeShiden);
            Press(f, ChargeSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => In(f.Ronin, Charge), 10);
            Assert.IsTrue(In(f.Ronin, Charge), "his fourth slot starts it");
            int start = f.Ctx.Clock.Frame;
            float x0 = f.Ronin.Pos.x;
            f.Run(20);
            Assert.AreEqual(x0, f.Ronin.Pos.x, 0.05f, "the wind-up: drawn back, standing");
            bool stopped = false;
            f.RunUntil(() => stopped |= In(f.Ronin, ChargeStop), 200);
            var hits = From(f, CombatEventType.Hit, Charge).ToList();
            Assert.AreEqual(1, hits.Count, "the point lands once");
            Assert.AreSame(f.Kage, hits[0].Target);
            Assert.GreaterOrEqual(hits[0].Frame - start, 35, "nothing during the wind-up");
            Assert.Greater(x0 - f.Ronin.Pos.x, 5f, "he ran most of the way across the arena (the point carries the rest of the 8 m)");
            Assert.IsTrue(stopped, "and stopped on contact");
            var glint = _cues.Where(c => c.cue == ShidenAbilities.ChargeGlintCue).ToList();
            Assert.AreEqual(1, glint.Count, "a glint on the point");
            Assert.Less(glint[0].frame, start + 35, "before the run: the wind-up reads");
            Assert.IsTrue(f.Frames.Any(l => l.Contains(" Launched ") || l.Contains(" Down ")), "Kage is knocked down");
        }

        [Test]
        public void A_guard_blocks_the_charging_thrust_and_he_stops()
        {
            var f = Fight(ChargeKage, ChargeShiden);
            Press(f, ChargeSlot);
            f.KageInput.Add(5000, buttons: "G");
            bool stopped = false;
            f.RunUntil(() => stopped |= In(f.Ronin, ChargeStop), 200);
            Assert.AreEqual(1, From(f, CombatEventType.Blocked, Charge).Count());
            Assert.AreEqual(0, From(f, CombatEventType.Hit, Charge).Count());
            Assert.IsTrue(stopped);
        }

        /// <summary>Searches Kage's timing for <paramref name="answer"/> against the charge; returns the first loop frame that avoided it, or -1.</summary>
        static int FindTiming(System.Action<ScriptedInput, int> answer, System.Func<HeadlessFight, bool> success, out HeadlessFight result)
        {
            result = null;
            for (int at = 20; at <= 130; at++)
            {
                var f = New(ChargeKage, ChargeShiden);
                Press(f, ChargeSlot);
                answer(f.KageInput, at);
                f.Run(180);
                if (success(f)) { result = f; return at; }
                f.Dispose();
            }
            return -1;
        }

        [Test]
        public void A_parry_turns_the_charging_thrust()
        {
            int at = FindTiming((k, t) => k.Add(t).Add(30, buttons: "G").Add(5000),
                f => f.Events(CombatEventType.Parried).Any(e => e.Source == f.Ronin && e.MoveId == Charge), out var hit);
            Assert.GreaterOrEqual(at, 0, "a Guard press just before the point arrives parries it");
            _fights.Add(hit);
            Assert.AreEqual(0, From(hit, CombatEventType.Hit, Charge).Count(), "nothing lands");
            Assert.IsFalse(In(hit.Ronin, Charge), "and the run is over");
        }

        [Test]
        public void A_jump_clears_the_charging_thrust()
        {
            int at = FindTiming((k, t) => k.Add(t).Add(40, dy: 1).Add(5000), f => !Touched(f, Charge) && !f.Events(CombatEventType.PerfectDodged).Any(), out var hit);
            Assert.GreaterOrEqual(at, 0, "the point is low enough to jump");
            _fights.Add(hit);
            Assert.Less(hit.Ronin.Pos.x, hit.Kage.Pos.x, "he ran on underneath and past");
        }

        [Test]
        public void A_roll_gets_through_the_charging_thrust()
        {
            int at = FindTiming((k, t) => k.Add(t).Add(1, tapFwd: true).Add(5000), f => !Touched(f, Charge), out var hit);
            Assert.GreaterOrEqual(at, 0, "rolling through him as he comes");
            _fights.Add(hit);
            Assert.Less(hit.Ronin.Pos.x, hit.Kage.Pos.x, "they passed each other");
        }

        [Test]
        public void A_dash_into_the_point_at_the_last_moment_is_a_perfect_dodge()
        {
            int at = FindTiming((k, t) => k.Add(t).Add(1, dash: true, fwd: 1).Add(5000), f => f.Events(CombatEventType.PerfectDodged).Any(), out var hit);
            Assert.GreaterOrEqual(at, 0, "a dash just before the point lands is a perfect dodge");
            _fights.Add(hit);
            Assert.AreEqual(0, From(hit, CombatEventType.Hit, Charge).Count());
        }

        // ================================================================ Vanguard Break

        [Test]
        public void Vanguard_Break_lays_a_ground_mark_through_its_wind_up_and_the_shockwave_strikes_with_the_slam()
        {
            var f = Fight(-1.5f, 1.5f);           // 3 m: past the spear's slam, inside the shockwave
            Press(f, BreakSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => f.Ctx.Hazards.Count > 0, 10);
            Assert.AreEqual(1, f.Ctx.Hazards.Count, "the shockwave is laid down at once");
            var h = f.Ctx.Hazards[0];
            Assert.AreEqual(ShidenAbilities.ShockId, h.Def.id);
            Assert.AreEqual(HazardPhase.Telegraph, h.Phase);
            Assert.Less(h.Centre.x, f.Ronin.Pos.x, "in front of him");

            f.RunUntil(() => h == null || !h.Alive || h.Phase == HazardPhase.Strike, 80);
            int strikeFrame = f.Ctx.Clock.Frame - 1;
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Frame < strikeFrame), "the mark never hits");
            Assert.AreEqual(f.Ronin.move.hits[0].from, f.Ronin.moveFrame, "it strikes on the slam's first frame");
            f.Run(30);
            var shock = f.Events(CombatEventType.Hit).Where(e => e.MoveId == ShidenAbilities.ShockId).ToList();
            Assert.AreEqual(1, shock.Count, "the shockwave struck Kage once");
            Assert.AreSame(f.Kage, shock[0].Target);
            Assert.AreSame(f.Ronin, shock[0].Source);
        }

        [Test]
        public void Stepping_back_from_the_shockwave_is_safe()
        {
            var f = Fight(-1.5f, 1.5f);
            Press(f, BreakSlot);
            f.KageInput.Add(6).Add(1, dash: true, fwd: -1).Add(60, fwd: -1).Add(5000);      // the JS tip: step back from the shockwave
            f.Run(100);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Ronin), "nothing reached him");
        }

        [Test]
        public void The_Vanguard_Break_slam_crushes_a_guard()
        {
            var f = Fight(-0.8f, 0.8f);
            Press(f, BreakSlot);
            f.KageInput.Add(5000, buttons: "G");
            f.Run(70);
            Assert.AreEqual(1, From(f, CombatEventType.Blocked, Break).Count(), "guarded");
            Assert.IsTrue(f.Events(CombatEventType.GuardBroken).Any(e => e.Target == f.Kage), "and the guard broke (JS gb 110)");
        }

        // ================================================================ the Braced Spear

        [Test]
        public void Brace_meets_a_Kage_walking_onto_the_point_with_a_critical_thrust()
        {
            var f = Fight(-1.6f, 1.6f);              // 3.2 m: outside the trigger
            Press(f, BraceSlot);
            f.KageInput.Add(20).Add(80, fwd: 1).Add(5000);   // he walks in once the spear is set
            bool thrust = false;
            f.RunUntil(() => thrust |= In(f.Ronin, BraceThrust), 120);
            Assert.IsTrue(thrust, "the rush set it off");
            f.Run(20);
            var hit = From(f, CombatEventType.Hit, BraceThrust).ToList();
            Assert.AreEqual(1, hit.Count, "the thrust met him");
            Assert.AreSame(f.Kage, hit[0].Target);
            Assert.IsTrue(hit[0].Crit, "a counter-hit critical");
        }

        [Test]
        public void Brace_meets_a_dash_onto_the_point()
        {
            var f = Fight(-1.6f, 1.6f);
            Press(f, BraceSlot);
            f.KageInput.Add(30).Add(1, dash: true, fwd: 1).Add(5000);
            bool thrust = false;
            f.RunUntil(() => thrust |= In(f.Ronin, BraceThrust), 80);
            Assert.IsTrue(thrust, "a dash in is a rush");
        }

        [Test]
        public void Waiting_out_the_brace_is_safe_and_he_lifts_the_point()
        {
            var f = Fight(-0.6f, 0.6f);              // well inside the trigger, standing
            Press(f, BraceSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => In(f.Ronin, Brace), 10);
            Assert.IsTrue(In(f.Ronin, Brace));
            f.RunUntil(() => !In(f.Ronin, Brace), 200);
            Assert.AreEqual(FState.Idle, f.Ronin.state, "back to neutral, not into the thrust");
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Ronin), "nothing struck Kage");
        }

        [Test]
        public void After_the_brace_he_is_open_and_a_blow_lands()
        {
            int at;
            using (var probe = New(-0.4f, 0.4f))
            {
                Press(probe, BraceSlot);
                probe.KageInput.Add(5000);
                probe.RunUntil(() => In(probe.Ronin, Brace) && probe.Ronin.moveFrame > probe.Ronin.move.braceTo, 200);
                at = probe.Frames.Count;
            }
            var f = Fight(-0.4f, 0.4f);
            Press(f, BraceSlot);
            f.KageInput.Add(at).Add(6, fwd: 1).Add(1, buttons: "P").Add(5000);      // step in and strike
            f.Run(at + 1);
            Assert.IsTrue(In(f.Ronin, Brace), "the point lifted, still in the move");
            f.Run(25);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Ronin), "no thrust once the hold is over");
            Assert.AreEqual(1, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Kage && e.Target == f.Ronin),
                "the jab lands: the opening. Events: " + string.Join("; ", f.Log.All.Where(e => e.Frame >= at - 2).Select(e => $"{e.Frame} {e.Type} {e.MoveId}")));
        }

        // ================================================================ Hold the Line

        static bool GuardUp(Fighter s) => In(s, HoldTheLine) && s.moveFrame >= s.move.counterFrom && s.moveFrame <= s.move.counterTo;

        [Test]
        public void A_blow_into_Hold_the_Line_is_turned_aside_and_punished()
        {
            var f = Fight(-0.37f, 0.37f);
            PhaseTwo(f);
            Press(f, GuardSlot);
            f.KageInput.Add(12).Add(1, buttons: "P").Add(5000);
            f.Run(100);
            var countered = f.Events(CombatEventType.Countered).ToList();
            Assert.AreEqual(1, countered.Count, "the blow was caught");
            Assert.AreSame(f.Ronin, countered[0].Target);
            var punish = From(f, CombatEventType.Hit, Punish).ToList();
            Assert.AreEqual(1, punish.Count, "and answered with the thrust");
            Assert.Greater(punish[0].Frame, countered[0].Frame);
        }

        [Test]
        public void Waiting_out_Hold_the_Line_is_safe_and_he_drops_it_open()
        {
            var f = Fight(-0.37f, 0.37f);
            PhaseTwo(f);
            Press(f, GuardSlot);
            f.KageInput.Add(5000);
            f.RunUntil(() => GuardUp(f.Ronin), 20);
            Assert.IsTrue(GuardUp(f.Ronin), "the guard is up (phase two)");
            Assert.LessOrEqual(f.Ronin.move.counterTo - f.Ronin.move.counterFrom + 1, 48, "a short guard: the JS 0.8 s");
            f.RunUntil(() => !In(f.Ronin, HoldTheLine), 120);
            Assert.AreEqual(FState.Idle, f.Ronin.state);
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Ronin), "nothing struck Kage");
        }

        [Test]
        public void A_scripted_Hold_the_Line_press_in_phase_one_does_nothing()
        {
            var f = Fight(-0.37f, 0.37f);
            Press(f, GuardSlot);
            f.KageInput.Add(5000);
            f.Run(20);
            Assert.IsFalse(In(f.Ronin, HoldTheLine), "Vanguard Guard is phase two's");
        }

        // ================================================================ no-grab, phases, the AI

        [Test]
        public void A_throw_fails_on_him()
        {
            var f = Fight(-0.3f, 0.3f);
            f.RoninInput.Add(5000);
            f.KageInput.Add(4).Add(1, fwd: 1, buttons: "P");          // the shoulder throw
            bool thrown = false;
            for (int i = 0; i < 90; i++) { f.Run(1); thrown |= f.Ronin.state == FState.Thrown; }
            Assert.IsFalse(thrown, "never held (JS noGrab)");
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Target == f.Ronin && e.MoveId != null && e.MoveId.Contains("hrow")), "no throw damage");
            Assert.IsFalse(f.Kage.Busy, "the whiff played out");
        }

        [Test]
        public void Phase_two_Hold_the_Line_opens_at_half_health_with_Vanguard_Guard_and_the_aggressive_AI()
        {
            var f = Fight(-1.5f, 1.5f, Foe.Ai);
            f.KageInput.Add(5000);
            f.Run(3);
            Assert.AreEqual(0, f.Ronin.Phases.Index);
            Assert.IsTrue(f.Ronin.AbilityEnabled("vanguardBreak") && f.Ronin.AbilityEnabled("shidenBrace") && f.Ronin.AbilityEnabled(ShidenAbilities.ChargeId));
            Assert.IsFalse(f.Ronin.AbilityEnabled("vanguardGuard"), "phase one has no Vanguard Guard");
            Assert.IsFalse(f.Ai.aggressive);

            f.Ronin.health = f.Ronin.maxHealth * 0.51f;
            f.Run(2);
            Assert.AreEqual(0, f.Ronin.Phases.Index, "51% is above the JS threshold");
            f.Ronin.health = f.Ronin.maxHealth * 0.5f;
            f.Run(2);
            Assert.AreEqual(1, f.Ronin.Phases.Index, "the JS threshold: half health");
            Assert.IsTrue(f.Ronin.AbilityEnabled("vanguardGuard"), "Vanguard Guard is added");
            Assert.IsTrue(f.Ronin.AbilityEnabled("vanguardBreak") && f.Ronin.AbilityEnabled("shidenBrace") && f.Ronin.AbilityEnabled(ShidenAbilities.ChargeId));
            Assert.IsTrue(f.Ai.aggressive, "the JS aggressive flag");
            Assert.AreEqual(AiProfiles.Get("boss").id, f.Ai.profile.id, "still the boss profile");
            CollectionAssert.AreEqual(new[] { "Vanguard", "Hold the Line" }, f.Events(CombatEventType.BossPhaseChanged).Select(e => e.Name).ToArray());
        }

        [Test]
        public void His_AI_charges_from_across_the_arena_and_braces_up_close()
        {
            var far = Fight(ChargeKage, ChargeShiden, Foe.Ai);
            far.KageInput.Add(1500);
            bool charged = false;
            far.RunUntil(() => charged |= In(far.Ronin, Charge), 1500);
            Assert.IsTrue(charged, "the charging thrust from range");

            var near = Fight(-1.2f, 1.2f, Foe.Ai);
            near.KageInput.Add(2400);
            bool braced = false;
            near.RunUntil(() => braced |= In(near.Ronin, Brace), 2400);
            Assert.IsTrue(braced, "the brace in its range");
        }
    }
}
