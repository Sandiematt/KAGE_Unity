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
    /// Keyboard + gamepad. Keyboard: A/D or ←/→ move, W/↑/Space jump, S/↓ crouch, hold back to guard,
    /// J punch, K kick, L katana, I shadow art. Double-tap forward = roll, double-tap back = backflip.
    /// Shift dashes (forward, back, or in the air); keep holding forward after a dash to run.
    /// Jump again in the air for a second jump.
    /// Gamepad: stick/d-pad, X/□ punch, A/✕ kick, Y/△ katana, B/○ shadow art, LB / triggers dash.
    /// </summary>
    public class PlayerInputSource : IInputSource
    {
        bool _p, _k, _s, _x, _tapL, _tapR, _jump, _dash;
        float _lastLeft = -9f, _lastRight = -9f;
        int _prevDx;

        public void Sample()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            if (kb != null)
            {
                _p |= kb.jKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame;
                _k |= kb.kKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame;
                _s |= kb.lKey.wasPressedThisFrame || kb.cKey.wasPressedThisFrame;
                _x |= kb.iKey.wasPressedThisFrame || kb.vKey.wasPressedThisFrame;
                _jump |= kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame;
                _dash |= kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame;
            }
            if (gp != null)
            {
                _p |= gp.buttonWest.wasPressedThisFrame;
                _k |= gp.buttonSouth.wasPressedThisFrame;
                _s |= gp.buttonNorth.wasPressedThisFrame;
                _x |= gp.buttonEast.wasPressedThisFrame || gp.rightShoulder.wasPressedThisFrame;
                _dash |= gp.leftShoulder.wasPressedThisFrame || gp.leftTrigger.wasPressedThisFrame || gp.rightTrigger.wasPressedThisFrame;
            }
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
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) dx -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dx += 1;
            }
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
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed || kb.spaceKey.isPressed) dy += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) dy -= 1;
            }
            var gp = Gamepad.current;
            if (gp != null && dy == 0)
            {
                Vector2 v = gp.leftStick.ReadValue() + gp.dpad.ReadValue();
                if (v.y < -0.55f) dy = -1; else if (v.y > 0.6f) dy = 1;
            }
            return dy;
        }

        static bool DashHeld()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) return true;
            var gp = Gamepad.current;
            return gp != null && (gp.leftShoulder.isPressed || gp.leftTrigger.isPressed || gp.rightTrigger.isPressed);
        }

        public FrameInput Poll(Fighter self)
        {
            var f = new FrameInput
            {
                dx = ReadDx(), dy = ReadDy(), p = _p, k = _k, s = _s, x = _x, tapLeft2 = _tapL, tapRight2 = _tapR,
                jump = _jump, dash = _dash, dashHeld = DashHeld(),
            };
            _p = _k = _s = _x = _tapL = _tapR = _jump = _dash = false;
            return f;
        }
    }
}
