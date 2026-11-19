using System.Collections.Generic;

namespace Kage.Rules
{
    /// <summary>
    /// What the Unity Prologue has that the JS stage <c>prologue.1</c> does not (SAN-96, ADR-0010). The JS road has one encounter (the
    /// tutorial Hollow) and five tutorial steps (move, jump, slash, block, dodge). The ticket and spec stories 74 and 75 add a
    /// <b>parry step</b>, a <b>Hollow Shade encounter</b> after the tutorial Hollow, and a slow practice partner for guard, parry and
    /// dodge. They are written here, in one place, on top of <see cref="StretchData.FromStage"/>; the generated tables are never edited
    /// (<c>StretchData.FromStage("prologue.1")</c> is still the pure JS road) and <see cref="StretchData.FromId"/> returns this.
    /// Positions are the JS pixels / 115 (ADR-0010); the rest of the stage (road length 4200 px, start 160 px, the tutorial Hollow's
    /// encounter at 2600 / gate 3200, the move, jump, attack, guard and dodge prompts at 0, 520, 1000, 1420, 1780 px, clue and story
    /// scene ids) is the JS data.
    /// </summary>
    public static class PrologueAdditions
    {
        public const string StageId = "prologue.1";

        /// <summary>The foe the guard, parry and dodge steps are practised against: the gentle tutorial Hollow, attacking slowly (<c>StretchTutorial</c>).</summary>
        public const string PracticePartner = "hollow_tutorial";

        /// <summary>The parry prompt: between the guard prompt (1420 px) and the dodge prompt (1780 px).</summary>
        public const double ParryAtPx = 1600;

        /// <summary>The Hollow Shade encounter: after the tutorial Hollow's gate (3200 px), a trigger at 3450 px and a gate at 4050 px on the 4200 px road.</summary>
        public const double ShadeX = 3450, ShadeGate = 4050;
        public const string ShadeId = "hollow_shade";

        /// <summary>Prompt hints the Unity Prologue words itself: guard, parry and dodge are now three steps (the JS block hint described the parry too).</summary>
        public static readonly IReadOnlyDictionary<string, string> Hints = new Dictionary<string, string>
        {
            [PromptStep.Guard] = "Hold Guard as the blow lands",
            [PromptStep.Parry] = "Press Guard just before the blow lands",
            [PromptStep.Dodge] = "Dash just before the blow lands: a perfect dodge",
        };

        /// <summary>Puts the additions on a road built from the JS <c>prologue.1</c>. Call once, on a fresh <see cref="StretchData.FromStage"/>.</summary>
        public static StretchData Apply(StretchData d)
        {
            var prompts = new List<StretchPrompt>(d.Prompts)
            {
                new StretchPrompt { Step = PromptStep.Parry, At = JsUnits.PxToMetres(ParryAtPx), Verb = PromptStep.Verb(PromptStep.Parry) },
            };
            prompts.Sort((a, b) => a.At.CompareTo(b.At));
            foreach (var p in prompts)
            {
                if (Hints.TryGetValue(p.Step, out var hint)) p.Hint = hint;
                if (p.Step == PromptStep.Guard || p.Step == PromptStep.Parry || p.Step == PromptStep.Dodge) p.Partner = PracticePartner;
            }
            d.Prompts = prompts.ToArray();

            var enc = new List<StretchEncounter>(d.Encounters)
            {
                new StretchEncounter { X = JsUnits.PxToMetres(ShadeX), Gate = JsUnits.PxToMetres(ShadeGate), Waves = new[] { new[] { ShadeId } } },
            };
            d.Encounters = enc.ToArray();
            d.Added = new[] { "prompt:parry", "prompt:practice-partner", "prompt:hints", "encounter:hollow_shade" };
            return d;
        }
    }
}
