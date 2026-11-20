using UnityEngine;

namespace Kage
{
    /// <summary>
    /// A health bar's easing, the JS <c>hud.animBar</c> (SAN-108): the fill eases to the new health at 16 per second; when health drops the
    /// bone-white trail waits 0.45 s, then follows the fill at 5 per second; healing pushes the trail up with the bar. The trail is never
    /// below the fill.
    /// </summary>
    public sealed class HudTrail
    {
        public const float FillRate = 16f, ChipRate = 5f, ChipDelay = 0.45f;

        /// <summary>The fill as drawn, 0..1.</summary>
        public float Value = 1f;
        /// <summary>The trail as drawn, 0..1: at or above <see cref="Value"/>.</summary>
        public float Chip = 1f;
        /// <summary>Seconds the trail still waits before it follows.</summary>
        public float Delay;
        /// <summary>The health the bar is heading for.</summary>
        public float Target = 1f;

        /// <summary>Puts the bar straight at <paramref name="ratio"/> with no trail (a new fight, a new foe).</summary>
        public void Reset(float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            Value = Chip = Target = ratio;
            Delay = 0f;
        }

        /// <summary>One step towards <paramref name="ratio"/> (health / max health). Returns true when health dropped on this step.</summary>
        public bool Step(float ratio, float dt)
        {
            ratio = Mathf.Clamp01(ratio);
            bool dropped = ratio < Target - 0.001f;
            if (dropped) Delay = ChipDelay;
            if (ratio > Target) Chip = Mathf.Max(Chip, ratio);
            Target = ratio;
            Value += (ratio - Value) * (1f - Mathf.Exp(-FillRate * dt));
            if (Delay > 0f) Delay -= dt;
            else Chip += (Value - Chip) * (1f - Mathf.Exp(-ChipRate * dt));
            Chip = Mathf.Max(Chip, Value);
            return dropped;
        }
    }
}
