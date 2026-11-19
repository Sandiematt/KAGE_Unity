using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// Stretch data (SAN-89): the road of a JS stretch stage read from the generated tables (ADR-0010), in metres, and the JS
    /// rules for where the road ends (Logic.encounterAt, stretchLimit, isStretchComplete). Expected numbers are the JS stage
    /// data (10-data.js ACT1) divided by the 115 px per metre of ADR-0010, not read back from the tables.
    /// </summary>
    public class StretchDataTests
    {
        const double Px = 115.0;

        [Test]
        public void Act_I_stretch_one_is_the_JS_road_in_metres()
        {
            var d = StretchData.FromStage("act1.stretch1");
            Assert.IsNotNull(d);
            Assert.AreEqual("act1.stretch1", d.Id);
            Assert.AreEqual(4400 / Px, d.Length, 1e-4);
            Assert.AreEqual(160 / Px, d.Start, 1e-4);
            Assert.AreEqual(2, d.Encounters.Length);
            Assert.AreEqual(1100 / Px, d.Encounters[0].X, 1e-4);
            Assert.AreEqual(1750 / Px, d.Encounters[0].Gate, 1e-4);
            Assert.AreEqual(2900 / Px, d.Encounters[1].X, 1e-4);
            Assert.AreEqual(3600 / Px, d.Encounters[1].Gate, 1e-4);
            CollectionAssert.AreEqual(new[] { "order_footman", "order_footman" }, d.Encounters[0].Waves[0]);
            CollectionAssert.AreEqual(new[] { "order_footman", "order_runner" }, d.Encounters[0].Waves[1]);
            CollectionAssert.AreEqual(new[] { "order_runner", "order_footman" }, d.Encounters[1].Waves[0]);
            CollectionAssert.AreEqual(new[] { "order_footman", "order_footman", "hollow_shade" }, d.Encounters[1].Waves[1]);
        }

        [Test]
        public void Act_I_stretch_four_has_three_encounters_and_ends_on_the_husk()
        {
            var d = StretchData.FromStage("act1.stretch4");
            Assert.AreEqual(5600 / Px, d.Length, 1e-4);
            Assert.AreEqual(3, d.Encounters.Length);
            CollectionAssert.AreEqual(new[] { "order_ironclad" }, d.Encounters[1].Waves[0]);
            CollectionAssert.AreEqual(new[] { "order_shieldbearer", "order_footman", "order_chainthrower", "hollow_husk" }, d.Encounters[2].Waves[1]);
        }

        [Test]
        public void The_Prologue_walk_is_a_stretch_too_and_boss_stages_are_not()
        {
            var p = StretchData.FromStage("prologue.1");
            Assert.AreEqual(4200 / Px, p.Length, 1e-4);
            Assert.AreEqual(1, p.Encounters.Length);
            CollectionAssert.AreEqual(new[] { "hollow_tutorial" }, p.Encounters[0].Waves[0]);
            Assert.IsNull(StretchData.FromStage("act1.sub1"), "a sub-boss stage has no road");
            Assert.IsNull(StretchData.FromStage("nowhere"));
        }

        [Test]
        public void Every_stretch_stage_of_the_campaign_converts_with_ordered_encounters_and_small_waves()
        {
            int n = 0;
            foreach (var st in JsTables.Stages.Where(s => s.Kind == "stretch"))
            {
                n++;
                var d = StretchData.FromStage(st.Id);
                Assert.IsNotNull(d, st.Id);
                Assert.Greater(d.Encounters.Length, 0, st.Id);
                double prevGate = 0;
                foreach (var e in d.Encounters)
                {
                    Assert.Greater(e.X, prevGate, $"{st.Id}: encounters in road order");
                    Assert.Greater(e.Gate, e.X, $"{st.Id}: the gate lies beyond the trigger");
                    Assert.LessOrEqual(e.Gate, d.Length, $"{st.Id}: the gate is on the road");
                    Assert.Greater(e.Waves.Length, 0, st.Id);
                    foreach (var w in e.Waves) Assert.That(w.Length, Is.InRange(1, 4), $"{st.Id}: a wave is one to four foes");
                    prevGate = e.Gate;
                }
            }
            Assert.AreEqual(5, n, "the Prologue walk and the four Act I stretches");
        }

        [Test]
        public void The_walking_limit_is_the_gate_of_the_first_uncleared_encounter_else_the_end()
        {
            var d = StretchData.FromStage("act1.stretch1");
            Assert.AreEqual(1750 / Px, d.Limit(0), 1e-4);
            Assert.AreEqual(3600 / Px, d.Limit(1), 1e-4);
            Assert.AreEqual(4400 / Px, d.Limit(2), 1e-4, "every encounter cleared: the end of the road");
        }

        [Test]
        public void An_encounter_starts_when_Kage_reaches_the_next_uncleared_trigger()
        {
            var d = StretchData.FromStage("act1.stretch1");
            Assert.AreEqual(-1, d.EncounterAt(0, 1099 / Px));
            Assert.AreEqual(0, d.EncounterAt(0, 1100 / Px));
            Assert.AreEqual(-1, d.EncounterAt(1, 2000 / Px), "the first is cleared; the second's trigger is further on");
            Assert.AreEqual(1, d.EncounterAt(1, 2900 / Px));
            Assert.AreEqual(-1, d.EncounterAt(2, 4400 / Px), "none left");
        }

        [Test]
        public void The_stretch_is_complete_when_every_encounter_is_cleared_and_Kage_is_within_60_px_of_the_end()
        {
            var d = StretchData.FromStage("act1.stretch1");
            Assert.IsFalse(d.IsComplete(1, 4400 / Px), "an encounter is still to clear");
            Assert.IsFalse(d.IsComplete(2, (4400 - 61) / Px));
            Assert.IsTrue(d.IsComplete(2, (4400 - 60) / Px));
            Assert.IsTrue(d.IsComplete(2, 4400 / Px));
        }

        [Test]
        public void The_arena_of_an_encounter_ends_at_its_gate_and_is_1500_px_wide_but_never_before_the_road_start()
        {
            var d = StretchData.FromStage("act1.stretch1");
            Assert.AreEqual((1750 - 1500) / Px, d.ArenaLeft(0), 1e-4);
            Assert.AreEqual((3600 - 1500) / Px, d.ArenaLeft(1), 1e-4);
            var near = new StretchData { Length = 1000 / Px, Encounters = new[] { new StretchEncounter { X = 300 / Px, Gate = 800 / Px, Waves = new[] { new[] { "order_footman" } } } } };
            Assert.AreEqual(0, near.ArenaLeft(0), 1e-9, "clamped to the left end of the road");
        }

        [Test]
        public void The_test_stretch_is_Footmen_only_with_waves_of_two_to_four()
        {
            var d = StretchData.FromId(StretchData.DevFootmenId);
            Assert.IsNotNull(d);
            Assert.AreEqual(StretchData.DevFootmenId, d.Id);
            Assert.GreaterOrEqual(d.Encounters.Length, 2);
            foreach (var e in d.Encounters)
                foreach (var w in e.Waves)
                {
                    Assert.That(w.Length, Is.InRange(2, 4));
                    foreach (var id in w) Assert.AreEqual("order_footman", id);
                }
            Assert.IsNotNull(StretchData.FromId("act1.stretch2"), "FromId also reads campaign stages");
            Assert.IsNull(StretchData.FromId("nowhere"));
        }
    }
}
