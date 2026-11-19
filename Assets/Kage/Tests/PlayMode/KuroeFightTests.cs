using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Kuroe in a fight (SAN-103), through the fight harness: Censure, her counter stance (a blow into it is turned aside and punished;
    /// waiting it out is safe and leaves her open; a throw still takes her), and her two phases (the JS threshold, the JS ability list and
    /// the aggressive flag). Kuroe is scripted to press her ability slot; Kage is scripted. Only events and public state are read.
    /// </summary>
    public class KuroeFightTests
    {
        const int Seed = 103;
        const string CensureMove = "kuroe.censure", PunishMove = "kuroe.censurePunish";
        readonly List<HeadlessFight> _fights = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var f in _fights) f.Dispose();
            _fights.Clear();
            Tuning.ClearOverrides();
        }

        HeadlessFight Fight(Foe foe = Foe.Scripted, float kageX = -0.37f, float kuroeX = 0.37f)
        {
            var f = new HeadlessFight(Seed, foe, kageX, kuroeX, foeDef: CharacterCatalog.Get(Kuroe.Id));
            f.Kage.maxHealth = f.Kage.health = 1e6f;
            _fights.Add(f);
            return f;
        }

        static bool InCensure(Fighter k) => k.state == FState.Attack && k.move != null && k.move.id == CensureMove;
        static bool CounterUp(Fighter k) => InCensure(k) && k.moveFrame >= k.move.counterFrom && k.moveFrame <= k.move.counterTo;

        /// <summary>Kuroe raises Censure on frame 2 and then stands still.</summary>
        static void RaiseCensure(HeadlessFight f) => f.RoninInput.Add(2).Add(1, ability: 1).Add(5000);

        /// <summary>The loop frame on which <paramref name="until"/> first holds when Kuroe raises Censure and Kage stands still (a probe run).</summary>
        int FrameWhen(System.Func<Fighter, bool> until, float kageX = -0.37f, float kuroeX = 0.37f)
        {
            using var f = new HeadlessFight(Seed, Foe.Scripted, kageX, kuroeX, foeDef: CharacterCatalog.Get(Kuroe.Id));
            RaiseCensure(f);
            f.KageInput.Add(5000);
            f.RunUntil(() => until(f.Ronin), 300);
            Assert.IsTrue(until(f.Ronin), "the probe reached its frame");
            return f.Frames.Count;
        }

        /// <summary>Kuroe raises Censure; Kage presses <paramref name="buttons"/> (with <paramref name="fwd"/>) on loop frame <paramref name="at"/>.</summary>
        HeadlessFight PressAt(int at, string buttons, int fwd = 0, float kageX = -0.37f, float kuroeX = 0.37f)
        {
            var f = Fight(kageX: kageX, kuroeX: kuroeX);
            RaiseCensure(f);
            f.KageInput.Add(at).Add(1, fwd: fwd, buttons: buttons).Add(5000);
            f.Run(at);
            return f;
        }

        // ---------------------------------------------------------------- the counter

        [Test]
        public void Censure_starts_from_her_ability_slot_and_raises_a_counter_window()
        {
            var f = Fight();
            RaiseCensure(f);
            f.KageInput.Add(5000);
            f.RunUntil(() => CounterUp(f.Ronin), 30);
            Assert.IsTrue(CounterUp(f.Ronin), "Censure is up");
            Assert.Greater(f.Ronin.AbilityCooldownLeft("censure"), 0, "and cooling down");
        }

        [Test]
        public void A_blow_into_Censure_is_turned_aside_and_punished()
        {
            var f = PressAt(FrameWhen(CounterUp) + 2, "P");             // Kage jabs into the raised staff
            Assert.IsTrue(CounterUp(f.Ronin));
            float kuroeHealth = f.Ronin.health;
            f.Run(90);

            var countered = f.Events(CombatEventType.Countered).ToList();
            Assert.AreEqual(1, countered.Count, "the blow was caught");
            Assert.AreSame(f.Kage, countered[0].Source);
            Assert.AreSame(f.Ronin, countered[0].Target);
            Assert.AreEqual(kuroeHealth, f.Ronin.health, "the blow did nothing to her");

            var punish = f.Events(CombatEventType.Hit).Where(e => e.Source == f.Ronin && e.Target == f.Kage).ToList();
            Assert.AreEqual(1, punish.Count, "and she answered it");
            Assert.AreEqual(PunishMove, punish[0].MoveId);
            Assert.Greater(punish[0].Damage, 0f);
            Assert.Greater(punish[0].Frame, countered[0].Frame, "a beat after the catch");
            Assert.IsTrue(f.Frames.Any(l => l.Contains(" Launched ") || l.Contains(" Down ")), "the punish knocks Kage down");
        }

        [Test]
        public void Waiting_out_Censure_is_safe_and_she_drops_her_guard()
        {
            var f = Fight();
            RaiseCensure(f);
            f.KageInput.Add(5000);
            f.RunUntil(() => CounterUp(f.Ronin), 30);
            f.RunUntil(() => !InCensure(f.Ronin), 200);
            Assert.IsFalse(InCensure(f.Ronin), "the stance ends on its own");
            Assert.AreEqual(FState.Idle, f.Ronin.state, "back to neutral, not into an attack");
            Assert.AreEqual(0, f.Events(CombatEventType.Countered).Count());
            Assert.AreEqual(0, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Ronin), "nothing struck Kage");
            Assert.AreEqual(1e6f, f.Kage.health);
        }

        [Test]
        public void After_the_window_she_is_open_and_a_blow_lands()
        {
            var f = PressAt(FrameWhen(k => InCensure(k) && k.moveFrame > k.move.counterTo), "P");
            Assert.IsTrue(InCensure(f.Ronin) && !CounterUp(f.Ronin), "dropping the guard");
            float before = f.Ronin.health;
            f.Run(30);
            Assert.AreEqual(0, f.Events(CombatEventType.Countered).Count(), "the window is over");
            Assert.AreEqual(1, f.Events(CombatEventType.Hit).Count(e => e.Source == f.Kage && e.Target == f.Ronin), "the jab lands");
            Assert.Less(f.Ronin.health, before);
        }

        [Test]
        public void A_throw_takes_her_out_of_Censure()
        {
            var f = PressAt(FrameWhen(CounterUp, -0.3f, 0.3f) + 1, "P", fwd: 1, kageX: -0.3f, kuroeX: 0.3f);     // the shoulder throw (Punch + forward)
            Assert.IsTrue(CounterUp(f.Ronin));
            bool thrown = false;
            f.RunUntil(() => thrown |= f.Ronin.state == FState.Thrown, 60);
            Assert.IsTrue(thrown, "the JS tip: wait it out or throw him");
            Assert.AreEqual(0, f.Events(CombatEventType.Countered).Count());
        }

        // ---------------------------------------------------------------- phases

        [Test]
        public void Phase_two_Redacted_opens_at_half_health_with_Censure_and_the_aggressive_AI()
        {
            var f = Fight(Foe.Ai, -1.5f, 1.5f);
            f.KageInput.Add(5000);
            f.Run(3);
            Assert.AreEqual(0, f.Ronin.Phases.Index);
            Assert.IsTrue(f.Ronin.AbilityEnabled("censure"));
            Assert.IsFalse(f.Ai.aggressive, "phase one: not aggressive");

            f.Ronin.health = f.Ronin.maxHealth * 0.51f;
            f.Run(2);
            Assert.AreEqual(0, f.Ronin.Phases.Index, "51% is above the JS threshold");
            f.Ronin.health = f.Ronin.maxHealth * 0.5f;
            f.Run(2);
            Assert.AreEqual(1, f.Ronin.Phases.Index, "the JS threshold: half health");
            Assert.IsTrue(f.Ronin.AbilityEnabled("censure"), "Censure stays (the JS list)");
            Assert.IsTrue(f.Ai.aggressive, "the JS aggressive flag");
            Assert.AreEqual(AiProfiles.Get("defensive").id, f.Ai.profile.id, "still the defensive profile");

            var names = f.Events(CombatEventType.BossPhaseChanged).Select(e => e.Name).ToArray();
            CollectionAssert.AreEqual(new[] { "The Black Line", "Redacted" }, names);
        }

        [Test]
        public void Her_AI_raises_Censure_when_Kage_is_in_its_range()
        {
            var f = Fight(Foe.Ai, -0.9f, 0.9f);
            f.KageInput.Add(5000);
            bool raised = false;
            f.RunUntil(() => raised |= InCensure(f.Ronin), 1200);
            Assert.IsTrue(raised, "the AI pressed her ability slot");
        }
    }
}
