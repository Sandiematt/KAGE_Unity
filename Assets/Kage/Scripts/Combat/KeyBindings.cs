using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Kage
{
    /// <summary>A fight action the player can bind (ADR-0004). Directions on a gamepad are the left stick and the d-pad.</summary>
    public enum GameAction { Left, Right, Up, Down, Jump, Punch, Kick, Weapon, Special, Guard, Dash }

    /// <summary>Which kind of device a prompt shows the binding for.</summary>
    public enum InputDeviceKind { Keyboard, Gamepad }

    /// <summary>
    /// The one table of fight bindings (ADR-0004, SAN-86): each action's keyboard keys and gamepad buttons.
    /// <see cref="PlayerInputSource"/> reads it; tutorial prompts show <see cref="Label"/> (SAN-96). Directions on a
    /// gamepad also come from the left stick and the d-pad, which the input source reads as axes.
    /// <para>
    /// Keyboard: A/D or ←/→ move, W/↑/Space jump, S/↓ down, J (or Z) punch, K (or X) kick, L (or C) weapon,
    /// I (or V) special, O (or B) guard, Shift dash.
    /// Gamepad: stick or d-pad, X/□ punch, A/✕ kick, Y/△ weapon, B/○ special, RB/R1 or RT/R2 guard, LB/L1 or LT/L2 dash.
    /// </para>
    /// </summary>
    public static class KeyBindings
    {
        static readonly Dictionary<GameAction, Key[]> Keys = new()
        {
            [GameAction.Left] = new[] { Key.A, Key.LeftArrow },
            [GameAction.Right] = new[] { Key.D, Key.RightArrow },
            [GameAction.Up] = new[] { Key.W, Key.UpArrow, Key.Space },
            [GameAction.Down] = new[] { Key.S, Key.DownArrow },
            [GameAction.Jump] = new[] { Key.Space, Key.W, Key.UpArrow },
            [GameAction.Punch] = new[] { Key.J, Key.Z },
            [GameAction.Kick] = new[] { Key.K, Key.X },
            [GameAction.Weapon] = new[] { Key.L, Key.C },
            [GameAction.Special] = new[] { Key.I, Key.V },
            [GameAction.Guard] = new[] { Key.O, Key.B },
            [GameAction.Dash] = new[] { Key.LeftShift, Key.RightShift },
        };

        static readonly Dictionary<GameAction, GamepadButton[]> Buttons = new()
        {
            [GameAction.Left] = new[] { GamepadButton.DpadLeft },
            [GameAction.Right] = new[] { GamepadButton.DpadRight },
            [GameAction.Up] = new[] { GamepadButton.DpadUp },
            [GameAction.Down] = new[] { GamepadButton.DpadDown },
            [GameAction.Jump] = new GamepadButton[0],          // up on the stick or d-pad
            [GameAction.Punch] = new[] { GamepadButton.West },
            [GameAction.Kick] = new[] { GamepadButton.South },
            [GameAction.Weapon] = new[] { GamepadButton.North },
            [GameAction.Special] = new[] { GamepadButton.East },
            [GameAction.Guard] = new[] { GamepadButton.RightShoulder, GamepadButton.RightTrigger },
            [GameAction.Dash] = new[] { GamepadButton.LeftShoulder, GamepadButton.LeftTrigger },
        };

        /// <summary>The keyboard keys bound to <paramref name="action"/>, first one first.</summary>
        public static IReadOnlyList<Key> KeysOf(GameAction action) => Keys[action];

        /// <summary>The gamepad buttons bound to <paramref name="action"/>, first one first.</summary>
        public static IReadOnlyList<GamepadButton> ButtonsOf(GameAction action) => Buttons[action];

        /// <summary>True when a key or button bound to <paramref name="action"/> went down this rendered frame.</summary>
        public static bool Pressed(GameAction action, Keyboard kb, Gamepad gp)
        {
            if (kb != null) foreach (var k in Keys[action]) if (kb[k].wasPressedThisFrame) return true;
            if (gp != null) foreach (var b in Buttons[action]) if (gp[b].wasPressedThisFrame) return true;
            return false;
        }

        /// <summary>True while a key or button bound to <paramref name="action"/> is held.</summary>
        public static bool Held(GameAction action, Keyboard kb, Gamepad gp)
        {
            if (kb != null) foreach (var k in Keys[action]) if (kb[k].isPressed) return true;
            if (gp != null) foreach (var b in Buttons[action]) if (gp[b].isPressed) return true;
            return false;
        }

        /// <summary>
        /// The bound key or button as a prompt shows it: "O" for Guard on the keyboard, "RB" on a gamepad. Several
        /// keyboard keys are joined with " / " ("J / Z"); a gamepad shows its first button.
        /// </summary>
        public static string Label(GameAction action, InputDeviceKind device)
        {
            if (device == InputDeviceKind.Gamepad)
            {
                var bs = Buttons[action];
                if (bs.Length == 0) return action == GameAction.Jump ? "↑" : "";
                return ButtonName(bs[0]);
            }
            var sb = new StringBuilder();
            foreach (var k in Keys[action])
            {
                if (sb.Length > 0) sb.Append(" / ");
                sb.Append(KeyName(k));
            }
            return sb.ToString();
        }

        static string KeyName(Key k) => k switch
        {
            Key.LeftArrow => "←", Key.RightArrow => "→", Key.UpArrow => "↑", Key.DownArrow => "↓",
            Key.LeftShift => "Shift", Key.RightShift => "Right Shift", Key.Space => "Space",
            _ => k.ToString(),
        };

        static string ButtonName(GamepadButton b) => b switch
        {
            GamepadButton.West => "X / □", GamepadButton.South => "A / ✕", GamepadButton.North => "Y / △", GamepadButton.East => "B / ○",
            GamepadButton.RightShoulder => "RB / R1", GamepadButton.RightTrigger => "RT / R2",
            GamepadButton.LeftShoulder => "LB / L1", GamepadButton.LeftTrigger => "LT / L2",
            GamepadButton.DpadLeft => "←", GamepadButton.DpadRight => "→", GamepadButton.DpadUp => "↑", GamepadButton.DpadDown => "↓",
            _ => b.ToString(),
        };
    }
}
