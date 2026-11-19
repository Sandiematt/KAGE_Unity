using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Kage
{
    /// <summary>
    /// Which kind of device the player used last (SAN-96): the tutorial prompts show the binding of that one (<see cref="KeyBindings.Label"/>).
    /// The keyboard until a gamepad button or stick direction is pressed; a key press brings the keyboard back. <see cref="Observe"/> is the
    /// pure rule; <see cref="Poll"/> reads the devices once per rendered frame.
    /// </summary>
    public sealed class InputDeviceTracker
    {
        public InputDeviceKind Device { get; private set; } = InputDeviceKind.Keyboard;

        /// <summary>Records what was used this frame. If both were, the gamepad (the more deliberate choice) wins.</summary>
        public void Observe(bool keyboardUsed, bool gamepadUsed)
        {
            if (gamepadUsed) Device = InputDeviceKind.Gamepad;
            else if (keyboardUsed) Device = InputDeviceKind.Keyboard;
        }

        /// <summary>Reads the current keyboard and gamepad (a key down, a button down, a stick pushed to a direction this frame).</summary>
        public InputDeviceKind Poll()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            bool k = kb != null && kb.anyKey.wasPressedThisFrame;
            bool g = false;
            if (gp != null)
                foreach (var c in gp.allControls)
                    if (c is ButtonControl b && b.wasPressedThisFrame) { g = true; break; }
            Observe(k, g);
            return Device;
        }
    }
}
