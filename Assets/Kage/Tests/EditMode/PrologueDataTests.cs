using System.Linq;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The Prologue's road (SAN-96): the JS stage <c>prologue.1</c> read from the generated tables, and the Unity-authored additions
    /// (<see cref="PrologueAdditions"/>) on top of it: a parry step and a Hollow Shade encounter. Expected numbers are the JS stage data
    /// (10-data.js: the Empty Village, 4200 px, steps at 0, 520, 1000, 1420, 1780 px) divided by 115 px per metre (ADR-0010).
    /// </summary>
    public class PrologueDataTests
    {
        const double Px = 115.0;

        [Test]
        public void The_JS_road_is_untouched_by_the_additions()
        {
            var js = StretchData.FromStage("prologue.1");
            Assert.AreEqual(1, js.Encounters.Length, "FromStage is the pure JS road: one encounter");
            Assert.AreEqual(5, js.Prompts.Length, "and five tutorial steps");
            CollectionAssert.AreEqual(new[] { "move", "jump", "attack", "guard", "dodge" }, js.Prompts.Select(p => p.Step).ToArray(),
                "the JS slash is the attack and the JS block is the guard");
            CollectionAssert.IsEmpty(js.Added);
        }

        [Test]
        public void The_five_JS_prompts_sit_where_the_JS_data_puts_them()
        {
            var d = StretchData.FromId("prologue.1");
            double[] px = { 0, 520, 1000, 1420, 1780 };
            string[] steps = { "move", "jump", "attack", "guard", "dodge" };
            foreach (var (step, at) in steps.Zip(px, (s, p) => (s, p)))
                Assert.AreEqual(at / Px, d.Prompts.Single(p => p.Step == step).At, 1e-4, step);
        }

        [Test]
        public void The_Unity_additions_put_a_parry_step_between_guard_and_dodge()
        {
            var d = StretchData.FromId("prologue.1");
            CollectionAssert.AreEqual(new[] { "move", "jump", "attack", "guard", "parry", "dodge" }, d.Prompts.Select(p => p.Step).ToArray(),
                "move, jump, attack, guard, parry, dodge");
            var guard = d.Prompts.Single(p => p.Step == "guard");
            var parry = d.Prompts.Single(p => p.Step == "parry");
            var dodge = d.Prompts.Single(p => p.Step == "dodge");
            Assert.AreEqual(1600 / Px, parry.At, 1e-4);
            Assert.Greater(parry.At, guard.At);
            Assert.Less(parry.At, dodge.At);
            for (int i = 1; i < d.Prompts.Length; i++) Assert.Greater(d.Prompts[i].At, d.Prompts[i - 1].At, "in road order");
            CollectionAssert.Contains(d.Added, "prompt:parry");
        }

        [Test]
        public void Guard_parry_and_dodge_are_practised_against_the_tutorial_Hollow_and_the_rest_need_no_partner()
        {
            var d = StretchData.FromId("prologue.1");
            foreach (var p in d.Prompts)
            {
                bool practice = p.Step is "guard" or "parry" or "dodge";
                Assert.AreEqual(practice ? "hollow_tutorial" : null, p.Partner, p.Step);
            }
        }

        [Test]
        public void Every_prompt_has_a_verb_in_the_glossary_s_words_and_a_hint()
        {
            var d = StretchData.FromId("prologue.1");
            CollectionAssert.AreEqual(new[] { "Move", "Jump", "Attack", "Guard", "Parry", "Dodge" }, d.Prompts.Select(p => p.Verb).ToArray());
            foreach (var p in d.Prompts) Assert.IsNotEmpty(p.Hint, p.Step);
        }

        [Test]
        public void The_tutorial_Hollow_encounter_then_the_Hollow_Shade_encounter()
        {
            var d = StretchData.FromId("prologue.1");
            Assert.AreEqual(2, d.Encounters.Length);
            Assert.AreEqual(2600 / Px, d.Encounters[0].X, 1e-4, "JS encounter");
            Assert.AreEqual(3200 / Px, d.Encounters[0].Gate, 1e-4);
            CollectionAssert.AreEqual(new[] { "hollow_tutorial" }, d.Encounters[0].Waves.Single());
            CollectionAssert.AreEqual(new[] { "hollow_shade" }, d.Encounters[1].Waves.Single(), "the Unity addition");
            Assert.Greater(d.Encounters[1].X, d.Encounters[0].Gate, "the second encounter starts beyond the first gate");
            Assert.Greater(d.Encounters[1].Gate, d.Encounters[1].X);
            Assert.LessOrEqual(d.Encounters[1].Gate, d.Length - StretchData.EndReach, "the road still has its end beyond the last gate");
            CollectionAssert.Contains(d.Added, "encounter:hollow_shade");
        }

        [Test]
        public void Story_scene_and_clue_ids_stay_in_the_data()
        {
            var d = StretchData.FromId("prologue.1");
            Assert.AreEqual("prologue.open", d.SceneBefore);
            Assert.AreEqual("prologue.close", d.SceneAfter);
            CollectionAssert.AreEqual(new[] { "recording", "photograph", "message" }, d.Clues.Select(c => c.Id).ToArray());
            Assert.AreEqual("prologue.clue.recording", d.Clues[0].Scene);
            Assert.AreEqual(820 / Px, d.Clues[0].X, 1e-4);
        }

        [Test]
        public void The_road_is_the_JS_length_and_start_and_names_its_chapter()
        {
            var d = StretchData.FromId("prologue.1");
            Assert.AreEqual(4200 / Px, d.Length, 1e-4);
            Assert.AreEqual(160 / Px, d.Start, 1e-4);
            Assert.AreEqual("The Empty Village", d.Name);
            Assert.AreEqual("Prologue", d.ChapterName);
        }

        [Test]
        public void Other_roads_have_no_additions_and_the_Act_I_stretches_carry_no_prompts()
        {
            CollectionAssert.IsEmpty(StretchData.FromId("act1.stretch1").Added);
            CollectionAssert.IsEmpty(StretchData.FromId("act1.stretch1").Prompts);
            CollectionAssert.IsEmpty(StretchData.FromId(StretchData.DevFootmenId).Prompts);
        }

        [Test]
        public void Prompt_ids_map_from_the_JS_ones()
        {
            Assert.AreEqual("attack", PromptStep.FromJs("slash"));
            Assert.AreEqual("guard", PromptStep.FromJs("block"));
            Assert.AreEqual("move", PromptStep.FromJs("move"));
            Assert.AreEqual("Parry", PromptStep.Verb("parry"));
        }
    }
}
