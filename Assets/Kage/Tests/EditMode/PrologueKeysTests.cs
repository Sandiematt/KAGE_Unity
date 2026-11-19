using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// What a tutorial prompt shows (SAN-96): the bound key or button of the device used last, always read from
    /// <see cref="KeyBindings.Label"/> (ADR-0004), so a rebind changes the prompt. Expected strings are the labels the ADR documents.
    /// </summary>
    public class PrologueKeysTests
    {
        [Test]
        public void Prompts_show_the_keyboard_keys_from_the_bindings_table()
        {
            CollectionAssert.AreEqual(new[] { "A / ←", "D / →" }, StretchTutorial.Keys(PromptStep.Move, InputDeviceKind.Keyboard));
            CollectionAssert.AreEqual(new[] { "Space / W / ↑" }, StretchTutorial.Keys(PromptStep.Jump, InputDeviceKind.Keyboard));
            CollectionAssert.AreEqual(new[] { "L / C" }, StretchTutorial.Keys(PromptStep.Attack, InputDeviceKind.Keyboard));
            CollectionAssert.AreEqual(new[] { "O / B" }, StretchTutorial.Keys(PromptStep.Guard, InputDeviceKind.Keyboard));
            CollectionAssert.AreEqual(new[] { "O / B" }, StretchTutorial.Keys(PromptStep.Parry, InputDeviceKind.Keyboard), "parry is a Guard press");
            CollectionAssert.AreEqual(new[] { "Shift / Right Shift" }, StretchTutorial.Keys(PromptStep.Dodge, InputDeviceKind.Keyboard));
        }

        [Test]
        public void Prompts_show_the_gamepad_buttons_when_a_gamepad_was_used_last()
        {
            CollectionAssert.AreEqual(new[] { "←", "→" }, StretchTutorial.Keys(PromptStep.Move, InputDeviceKind.Gamepad));
            CollectionAssert.AreEqual(new[] { "↑" }, StretchTutorial.Keys(PromptStep.Jump, InputDeviceKind.Gamepad));
            CollectionAssert.AreEqual(new[] { "Y / △" }, StretchTutorial.Keys(PromptStep.Attack, InputDeviceKind.Gamepad));
            CollectionAssert.AreEqual(new[] { "RB / R1" }, StretchTutorial.Keys(PromptStep.Guard, InputDeviceKind.Gamepad));
            CollectionAssert.AreEqual(new[] { "RB / R1" }, StretchTutorial.Keys(PromptStep.Parry, InputDeviceKind.Gamepad));
            CollectionAssert.AreEqual(new[] { "LB / L1" }, StretchTutorial.Keys(PromptStep.Dodge, InputDeviceKind.Gamepad));
        }

        [Test]
        public void Every_prompt_key_is_the_bindings_table_label_for_the_action()
        {
            foreach (var d in new[] { InputDeviceKind.Keyboard, InputDeviceKind.Gamepad })
            {
                Assert.AreEqual(KeyBindings.Label(GameAction.Guard, d), StretchTutorial.Keys(PromptStep.Guard, d)[0]);
                Assert.AreEqual(KeyBindings.Label(GameAction.Dash, d), StretchTutorial.Keys(PromptStep.Dodge, d)[0]);
                Assert.AreEqual(KeyBindings.Label(GameAction.Weapon, d), StretchTutorial.Keys(PromptStep.Attack, d)[0]);
                Assert.AreEqual(KeyBindings.Label(GameAction.Jump, d), StretchTutorial.Keys(PromptStep.Jump, d)[0]);
                Assert.AreEqual(KeyBindings.Label(GameAction.Left, d), StretchTutorial.Keys(PromptStep.Move, d)[0]);
                Assert.AreEqual(KeyBindings.Label(GameAction.Right, d), StretchTutorial.Keys(PromptStep.Move, d)[1]);
            }
            Assert.IsEmpty(StretchTutorial.Keys("nonsense", InputDeviceKind.Keyboard));
        }

        [Test]
        public void The_device_is_the_one_used_last()
        {
            var t = new InputDeviceTracker();
            Assert.AreEqual(InputDeviceKind.Keyboard, t.Device, "the keyboard until told otherwise");
            t.Observe(false, false);
            Assert.AreEqual(InputDeviceKind.Keyboard, t.Device, "no input changes nothing");
            t.Observe(false, true);
            Assert.AreEqual(InputDeviceKind.Gamepad, t.Device);
            t.Observe(false, false);
            Assert.AreEqual(InputDeviceKind.Gamepad, t.Device, "it stays");
            t.Observe(true, false);
            Assert.AreEqual(InputDeviceKind.Keyboard, t.Device, "a key press brings the keyboard back");
            t.Observe(true, true);
            Assert.AreEqual(InputDeviceKind.Gamepad, t.Device, "both in one frame: the gamepad");
        }
    }
}
