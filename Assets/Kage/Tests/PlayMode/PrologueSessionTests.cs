using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The Prologue as a stretch (SAN-96) in the fight harness: a scripted Kage (<see cref="PrologueBot"/>) walks the whole road. The six
    /// tutorial prompts (move, jump, attack, guard, parry, dodge) show in order at their road positions and are done by what the fight
    /// reports: the walk, a jump, a move started, a <c>Blocked</c> hit, a <c>Parried</c> hit and a <c>PerfectDodged</c> hit, the last three
    /// practised against the tutorial Hollow acting as a slow practice partner. Then the tutorial Hollow's gate and the Hollow Shade's gate
    /// close and must be cleared, and the road's end clears the stage.
    /// </summary>
    public class PrologueSessionTests
    {
        const int Seed = 96;
        const int RunLimit = 12000;

        readonly List<StretchFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
        }

        static StretchData Road() => StretchData.FromId("prologue.1");

        /// <summary>A Prologue fight with the bot as Kage (override the bot's choices through <c>bot.Override</c>).</summary>
        StretchFight Fight(out PrologueBot bot, int seed = Seed)
        {
            var b = new PrologueBot(0.9f);
            var f = new StretchFight(seed, Road(), tutorial: true, kageInput: sf => { b.Fight = sf; return b; });
            _fights.Add(f);
            bot = b;
            return f;
        }

        /// <summary>Runs until the prompt for <paramref name="step"/> shows (Kage at its position), or fails.</summary>
        static void ToPrompt(StretchFight f, string step, int max = 4000)
        {
            Assert.IsTrue(f.RunUntil(() => f.Tutorial.Current != null && f.Tutorial.Current.Step == step, max), "the " + step + " prompt shows");
        }

        // ---------------- order and positions ----------------

        [Test]
        public void The_six_prompts_show_in_order_each_at_its_road_position()
        {
            var f = Fight(out _);
            var shown = new List<(string step, float x)>();
            string last = null;
            f.RunUntil(() => f.Tutorial.Finished, RunLimit, () =>
            {
                var c = f.Tutorial.Current;
                string s = c?.Step;
                if (s != null && s != last) shown.Add((s, f.Kage.Pos.x));
                if (s != null) last = s;
            });
            Assert.IsTrue(f.Tutorial.Finished, "the scripted Kage clears every prompt");
            CollectionAssert.AreEqual(new[] { "move", "jump", "attack", "guard", "parry", "dodge" }, shown.Select(p => p.step).ToArray());
            var data = f.Session.Data;
            foreach (var (step, x) in shown)
            {
                float at = (float)data.Prompts.Single(p => p.Step == step).At;
                Assert.GreaterOrEqual(x, at - 0.01f, step + ": never before its position");
                Assert.LessOrEqual(x, Mathf.Max(at, (float)data.Start) + 0.8f, step + ": shown as Kage reaches it (the first at the road's start), not far past");
            }
            CollectionAssert.AreEqual(new[] { "move", "jump", "attack", "guard", "parry", "dodge" }, f.Tutorial.Completed.Select(c => c.step).ToArray());
        }

        [Test]
        public void No_prompt_shows_before_Kage_reaches_it_and_none_while_the_gate_is_shut()
        {
            var f = Fight(out var bot);
            // the first prompt is at the start of the road and shows at once; the second waits for 520 px (4.5 m)
            f.Run(1);
            Assert.AreEqual("move", f.Tutorial.Current.Step);
            ToPrompt(f, "jump");
            Assert.GreaterOrEqual(f.Kage.Pos.x, 520 / 115f - 0.01f);
            Assert.AreEqual(1, f.Tutorial.Done);
            Assert.IsTrue(f.RunUntil(() => f.Tutorial.Finished, RunLimit));
            Assert.IsNull(f.Tutorial.Current, "nothing left to show");
            Assert.IsTrue(f.RunUntil(() => f.Session.State == StretchState.Encounter, 4000));
            Assert.IsNull(f.Tutorial.Current, "no prompt over an encounter");
        }

        // ---------------- each waits for its action ----------------

        [Test]
        public void A_prompt_waits_until_the_action_is_done_however_far_Kage_walks()
        {
            var f = Fight(out var bot);
            ToPrompt(f, "jump");
            bot.Override = (self, p) => new FrameInput { dx = 1 };      // walks on, never jumps
            float x0 = f.Kage.Pos.x;
            f.Run(100);
            Assert.Greater(f.Kage.Pos.x, x0 + 3f, "he walked well past the prompt's position");
            Assert.AreEqual("jump", f.Tutorial.Current.Step, "still waiting for the jump");
            Assert.AreEqual(1, f.Tutorial.Done);
        }

        [Test]
        public void Move_is_done_by_walking_140_px_not_by_standing()
        {
            var f = Fight(out var bot);
            bot.Override = (self, p) => new FrameInput();
            f.Run(120);
            Assert.AreEqual("move", f.Tutorial.Current.Step, "standing still does not move");
            bot.Override = null;
            f.Run(1);
            Assert.IsTrue(f.RunUntil(() => f.Tutorial.Done == 1, 300));
            Assert.AreEqual("move", f.Tutorial.Completed[0].step);
        }

        [Test]
        public void Jump_is_done_by_leaving_the_ground_and_attack_by_starting_an_attack_move()
        {
            var f = Fight(out var bot);
            ToPrompt(f, "jump");
            bot.Override = (self, p) => new FrameInput { s = true };       // a sword swing, not a jump
            f.Run(90);
            Assert.AreEqual("jump", f.Tutorial.Current.Step, "swinging the sword is not a jump");
            bot.Override = null;
            Assert.IsTrue(f.RunUntil(() => f.Tutorial.Done == 2, 300), "the jump is done");
            ToPrompt(f, "attack");
            bot.Override = (self, p) => new FrameInput { dash = true };   // a dash is not an attack
            f.Run(60);
            Assert.AreEqual("attack", f.Tutorial.Current.Step, "a dash is not an attack move");
            bot.Override = null;
            Assert.IsTrue(f.RunUntil(() => f.Tutorial.Done == 3, 300), "starting an attack move completes it");
        }

        [Test]
        public void Guard_is_done_by_a_blocked_hit_and_a_hit_that_lands_does_not_do_it()
        {
            var f = Fight(out var bot);
            ToPrompt(f, "guard");
            Assert.IsNotNull(f.Tutorial.Partner, "the practice partner stands ahead");
            Assert.AreEqual("hollow_tutorial", f.Tutorial.Partner.CharacterId);
            bot.Override = (self, p) => new FrameInput();      // stands there and is hit
            Assert.IsTrue(f.RunUntil(() => f.Events(CombatEventType.Hit).Any(e => e.Target == f.Kage), 600), "the partner's attack lands on a Kage who does not guard");
            Assert.AreEqual("guard", f.Tutorial.Current.Step, "a hit that lands is not a guard");
            Assert.AreEqual(0, f.Events(CombatEventType.Blocked).Count());
            bot.Override = (self, p) => new FrameInput { guard = true };      // guard held for good
            Assert.IsTrue(f.RunUntil(() => f.Tutorial.Done == 4, 900), "the next blocked hit does it");
            Assert.IsTrue(f.Events(CombatEventType.Blocked).Any(e => e.Target == f.Kage));
            Assert.IsNull(f.Tutorial.Partner, "the partner leaves when the prompt is done");
        }

        [Test]
        public void Parry_is_done_by_a_parried_hit_and_a_blocked_hit_does_not_do_it()
        {
            var f = Fight(out var bot);
            ToPrompt(f, "parry");
            Assert.IsNotNull(f.Tutorial.Partner);
            bot.Override = (self, p) => new FrameInput { guard = true };      // guard long before the blow: it is blocked, not parried
            Assert.IsTrue(f.RunUntil(() => f.Events(CombatEventType.Blocked).Any(e => e.Target == f.Kage), 600));
            f.Run(2);
            Assert.AreEqual("parry", f.Tutorial.Current.Step, "a block is not a parry");
            Assert.AreEqual(0, f.Events(CombatEventType.Parried).Count());
            bot.Override = null;
            Assert.IsTrue(f.RunUntil(() => f.Tutorial.Done == 5, 900), "a parry does it");
            var e = f.Events(CombatEventType.Parried).First();
            Assert.AreSame(f.Kage, e.Target);
            Assert.IsNull(f.Tutorial.Partner);
        }

        [Test]
        public void Dodge_is_done_by_a_perfect_dodge_and_a_plain_dash_does_not_do_it()
        {
            var f = Fight(out var bot);
            ToPrompt(f, "dodge");
            Assert.IsNotNull(f.Tutorial.Partner);
            int dashes = 0;
            bot.Override = (self, p) => self.state == FState.Idle && dashes < 1 && ++dashes > 0 ? new FrameInput { dash = true } : default;     // a dash with no blow to dodge
            f.Run(30);
            Assert.AreEqual("dodge", f.Tutorial.Current.Step, "a dash alone is not a dodge");
            Assert.AreEqual(0, f.Events(CombatEventType.PerfectDodged).Count());
            bot.Override = null;
            Assert.IsTrue(f.RunUntil(() => f.Tutorial.Done == 6, 900), "a perfect dodge does it");
            Assert.AreSame(f.Kage, f.Events(CombatEventType.PerfectDodged).First().Target);
            Assert.IsTrue(f.Tutorial.Finished);
        }

        // ---------------- the practice partner ----------------

        [Test]
        public void The_practice_partner_attacks_slowly_at_fixed_intervals_and_does_not_count_as_a_foe_of_the_road()
        {
            var f = Fight(out var bot);
            ToPrompt(f, "guard");
            var partner = f.Tutorial.Partner;
            Assert.AreEqual(0, f.Session.FoesLeft, "a practice partner is not a wave foe");
            Assert.AreEqual(0, f.Session.Spawned.Count, "and is not in the session's spawn record");
            Assert.Less(partner.MoveRate, 1f, "slow attacks: the tutorial Hollow's move rate");
            Assert.IsTrue(partner.maxHealth > 1000f, "too much health for a stray combo");
            var starts = new List<int>();
            bot.Override = (self, p) => new FrameInput();
            f.Run(520);
            foreach (var e in f.Events(CombatEventType.MoveStarted)) if (e.Source == partner) starts.Add(e.Frame);
            Assert.GreaterOrEqual(starts.Count, 2, "it keeps attacking while the prompt waits");
            for (int i = 1; i < starts.Count; i++)
                Assert.GreaterOrEqual(starts[i] - starts[i - 1], PracticeInput.Wait, "one attack at a time, a fixed pause apart");
        }

        [Test]
        public void The_partner_is_put_back_ahead_of_Kage_when_he_outruns_it()
        {
            var f = Fight(out var bot);
            ToPrompt(f, "guard");
            var partner = f.Tutorial.Partner;
            bot.Override = (self, p) => new FrameInput();
            f.Run(5);
            partner.ResetForRound(f.Kage.Pos.x + 12f, -1f);        // far ahead: out of reach
            partner.BeginFight();
            f.Run(3);
            Assert.LessOrEqual(Mathf.Abs(partner.Pos.x - f.Kage.Pos.x), StretchTutorial.PartnerLeash, "put back within the leash");
        }

        // ---------------- encounters and the end ----------------

        [Test]
        public void The_whole_Prologue_every_prompt_both_encounters_and_the_stage_clear()
        {
            var f = Fight(out _);
            Assert.IsTrue(f.RunUntil(() => f.Session.Cleared || f.Session.Failed, RunLimit), "the run ends");
            Assert.IsFalse(f.Session.Failed, "Kage did not fall");
            Assert.IsTrue(f.Session.Cleared);
            Assert.IsTrue(f.Tutorial.Finished, "every prompt was done");

            CollectionAssert.AreEqual(new[] { "move", "jump", "attack", "guard", "parry", "dodge" }, f.Tutorial.Completed.Select(c => c.step).ToArray());
            // the prompts come before the first encounter
            int firstWave = f.Events(CombatEventType.WaveStarted).First().Frame;
            Assert.Less(f.Tutorial.Completed.Last().frame, firstWave, "all six prompts are done before the first gate closes");

            // two encounters, one wave each, each cleared; the tutorial Hollow then the Hollow Shade
            var waves = f.Events(CombatEventType.WaveStarted).ToList();
            var clears = f.Events(CombatEventType.WaveCleared).ToList();
            CollectionAssert.AreEqual(new[] { "prologue.1.e1", "prologue.1.e2" }, waves.Select(w => w.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "prologue.1.e1", "prologue.1.e2" }, clears.Select(w => w.Name).ToArray());
            CollectionAssert.AreEqual(new[] { "hollow_tutorial", "hollow_shade" }, f.Session.Spawned.Select(s => s.RequestedId).ToArray());
            Assert.IsTrue(f.Session.Spawned.All(s => s.CharacterId == s.RequestedId), "both foes are the characters the road names");
            Assert.Less(waves[0].Frame, clears[0].Frame);
            Assert.Less(clears[0].Frame, waves[1].Frame, "the Hollow Shade comes after the tutorial Hollow is down");
            Assert.AreEqual(2, f.Session.EncountersCleared);

            // exactly one StageCleared, last
            var cleared = f.Events(CombatEventType.StageCleared).Single();
            Assert.AreEqual("prologue.1", cleared.Name);
            Assert.AreEqual(0, f.Events(CombatEventType.StageFailed).Count());
            Assert.Greater(cleared.Frame, clears[1].Frame);
            Assert.GreaterOrEqual(f.Kage.Pos.x, (float)(f.Session.Data.Length - StretchData.EndReach) - 0.05f, "Kage reached the end of the road");
        }

        [Test]
        public void Each_gate_closes_the_floor_until_its_encounter_is_cleared()
        {
            var f = Fight(out _);
            var d = f.Session.Data;
            Assert.IsTrue(f.RunUntil(() => f.Session.State == StretchState.Encounter, RunLimit), "the first encounter");
            Assert.AreEqual(1, f.Session.EncounterNumber);
            Assert.IsTrue(f.Session.GateClosed);
            Assert.AreEqual((float)d.Encounters[0].Gate, f.Ctx.Bounds.Right, 1e-3f, "Kage cannot pass the first gate");
            Assert.IsTrue(f.RunUntil(() => f.Session.EncountersCleared == 1, RunLimit));
            Assert.AreEqual(StretchState.Walking, f.Session.State);
            Assert.AreEqual((float)d.Encounters[1].Gate, f.Ctx.Bounds.Right, 1e-3f, "the walk now ends at the second gate");
            Assert.IsTrue(f.RunUntil(() => f.Session.State == StretchState.Encounter, RunLimit));
            Assert.AreEqual(2, f.Session.EncounterNumber);
            Assert.IsTrue(f.RunUntil(() => f.Session.StretchWave == 2, 200), "its wave comes");
            Assert.AreEqual("hollow_shade", f.Session.Spawned.Last().RequestedId);
            Assert.AreEqual(StretchState.Encounter, f.Session.State, "still shut until the Shade is down");
        }

        // ---------------- determinism ----------------

        [Test]
        public void The_prompts_part_of_the_Prologue_is_identical_every_run_under_the_same_seed()
        {
            (List<string> events, List<string> frames, List<string> done) Once()
            {
                var b = new PrologueBot(0.9f);
                using var f = new StretchFight(Seed, Road(), tutorial: true, kageInput: sf => { b.Fight = sf; return b; });
                Assert.IsTrue(f.RunUntil(() => f.Tutorial.Finished, RunLimit), "all six prompts are done");
                f.Run(60);
                return (f.Log.Lines().ToList(), f.Frames.ToList(), f.Tutorial.Completed.Select(c => c.step + "@" + c.frame).ToList());
            }
            var (e1, fr1, d1) = Once();
            var (e2, fr2, d2) = Once();
            CollectionAssert.AreEqual(d1, d2, "every prompt done on the same frame");
            CollectionAssert.AreEqual(e1, e2, "the same events");
            Assert.IsNull(BaselineReplay.Compare(BaselineReplay.Format("prologue", Seed, e1, fr1), BaselineReplay.Format("prologue", Seed, e2, fr2)), "and the same positions");
            Assert.Greater(e1.Count(l => l.Contains(" Parried ")), 0, "the run includes the parry");
            Assert.Greater(e1.Count(l => l.Contains(" PerfectDodged ")), 0, "and the perfect dodge");
        }
    }
}
