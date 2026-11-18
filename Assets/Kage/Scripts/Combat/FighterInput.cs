using UnityEngine;
using UnityEngine.InputSystem;

namespace Kage
{
    /// <summary>One logic frame (60 Hz) of fighter input. dx/dy are world directions.</summary>
    public struct FrameInput
    {
        public int dx, dy;
        public bool p, k, s, x;
        public bool tapLeft2, tapRight2;
        /// <summary>Jump pressed this step (edge). Fighters also derive an edge from dy, so AI needs no change.</summary>
        public bool jump;
        /// <summary>Dash pressed this step / dash button held (held = keep sprinting after the dash).</summary>
        public bool dash, dashHeld;
        /// <summary>
        /// Guard held at poll time, or pressed since the last poll (ADR-0004, SAN-86). With <c>dy &lt; 0</c> it is a low guard.
        /// The fighter derives a press from <c>guard</c> going from released to held, so AI and scripted input only set this.
        /// </summary>
        public bool guard;
        /// <summary>Guard went down since the last poll (latched between physics steps by <see cref="PlayerInputSource"/>).</summary>
        public bool guardPressed;
        /// <summary>Kip-up mashing and the like: Guard doesn't count as "any button".</summary>
        public bool Any => p || k || s || x;
    }

    public interface IInputSource
    {
        /// <summary>Called every rendered frame to catch presses between physics steps.</summary>
        void Sample();
        /// <summary>Called once per 60 Hz logic step; returns and clears the pending presses.</summary>
        FrameInput Poll(Fighter self);
    }

    /// <summary>
    /// Keyboard + gamepad, through the <see cref="KeyBindings"/> table. Keyboard: A/D or ←/→ move, W/↑/Space jump,
    /// S/↓ crouch, J punch, K kick, L weapon, I special, O guard (with S: low guard), Shift dash.
    /// Double-tap forward = roll, double-tap back = backflip. Keep holding forward after a dash to run.
    /// Jump again in the air for a second jump. Walking backward doesn't guard.
    /// Gamepad: stick/d-pad, X/□ punch, A/✕ kick, Y/△ weapon, B/○ special, RB / RT guard, LB / LT dash.
    /// </summary>
    public class PlayerInputSource : IInputSource
    {
        bool _p, _k, _s, _x, _tapL, _tapR, _jump, _dash, _guard;
        float _lastLeft = -9f, _lastRight = -9f;
        int _prevDx;

        public void Sample()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            _p |= KeyBindings.Pressed(GameAction.Punch, kb, gp);
            _k |= KeyBindings.Pressed(GameAction.Kick, kb, gp);
            _s |= KeyBindings.Pressed(GameAction.Weapon, kb, gp);
            _x |= KeyBindings.Pressed(GameAction.Special, kb, gp);
            _guard |= KeyBindings.Pressed(GameAction.Guard, kb, gp);
            _jump |= KeyBindings.Pressed(GameAction.Jump, kb, null);
            _dash |= KeyBindings.Pressed(GameAction.Dash, kb, gp);
            int dx = ReadDx();
            if (dx != _prevDx && dx != 0)
            {
                float now = Time.unscaledTime;
                if (dx < 0) { if (now - _lastLeft < 0.26f) _tapL = true; _lastLeft = now; }
                else { if (now - _lastRight < 0.26f) _tapR = true; _lastRight = now; }
            }
            _prevDx = dx;
        }

        static int ReadDx()
        {
            int dx = 0;
            var kb = Keyboard.current;
            if (KeyBindings.Held(GameAction.Left, kb, null)) dx -= 1;
            if (KeyBindings.Held(GameAction.Right, kb, null)) dx += 1;
            var gp = Gamepad.current;
            if (gp != null && dx == 0)
            {
                Vector2 v = gp.leftStick.ReadValue() + gp.dpad.ReadValue();
                if (v.x < -0.5f) dx = -1; else if (v.x > 0.5f) dx = 1;
            }
            return dx;
        }

        static int ReadDy()
        {
            int dy = 0;
            var kb = Keyboard.current;
            if (KeyBindings.Held(GameAction.Up, kb, null)) dy += 1;
            if (KeyBindings.Held(GameAction.Down, kb, null)) dy -= 1;
            var gp = Gamepad.current;
            if (gp != null && dy == 0)
            {
                Vector2 v = gp.leftStick.ReadValue() + gp.dpad.ReadValue();
                if (v.y < -0.55f) dy = -1; else if (v.y > 0.6f) dy = 1;
            }
            return dy;
        }

        public FrameInput Poll(Fighter self)
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            var f = new FrameInput
            {
                dx = ReadDx(), dy = ReadDy(), p = _p, k = _k, s = _s, x = _x, tapLeft2 = _tapL, tapRight2 = _tapR,
                jump = _jump, dash = _dash, dashHeld = KeyBindings.Held(GameAction.Dash, kb, gp),
                guard = _guard || KeyBindings.Held(GameAction.Guard, kb, gp), guardPressed = _guard,
            };
            _p = _k = _s = _x = _tapL = _tapR = _jump = _dash = _guard = false;
            return f;
        }
    }
}
