using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The one clock every menu animation reads, and the switches that turn motion down. Animations are pure functions of this clock
    /// (<see cref="UiAnim.Sample"/>), so a test or a screenshot fixture can freeze it (<see cref="Freeze"/>), step it (<see cref="Advance"/>) or
    /// ask every animation to jump to its end (<see cref="Skip"/>).
    /// <para>Reduced motion: the JS stylesheet honours <c>prefers-reduced-motion</c> (ambient petals, clouds and moon stop, the boss card and the
    /// palette change cut short, the splash opens on its finished card). The Unity UI reads the same Windows setting ("Show animations in Windows",
    /// the one a browser reads) and treats it like <see cref="Skip"/> for entrances and holds the ambient loops still.</para>
    /// </summary>
    public static class UiClock
    {
        static bool frozen;
        static float frozenAt;
        static bool? osReduced;

        /// <summary>When true every animation is at its end state at once (screenshots of still screens, tests that read the screen).</summary>
        public static bool Skip;

        /// <summary>Overrides the OS setting (null: read the OS).</summary>
        public static bool? ForceReduced;

        public static float Now { get { return frozen ? frozenAt : Time.unscaledTime; } }

        public static bool IsFrozen { get { return frozen; } }

        public static void Freeze(float at) { frozen = true; frozenAt = at; }

        public static void Advance(float dt) { if (frozen) frozenAt += dt; }

        public static void Unfreeze() { frozen = false; }

        /// <summary>Back to the defaults (tests and fixtures call this when they are done).</summary>
        public static void Reset() { frozen = false; Skip = false; ForceReduced = null; }

        public static bool Reduced
        {
            get
            {
                if (ForceReduced.HasValue) return ForceReduced.Value;
                if (!osReduced.HasValue) osReduced = ReadOsReducedMotion();
                return osReduced.Value;
            }
        }

        /// <summary>True when motion plays: not skipped and not reduced.</summary>
        public static bool Animate { get { return !Skip && !Reduced; } }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [DllImport("user32.dll", SetLastError = false)]
        static extern bool SystemParametersInfo(uint action, uint param, ref int value, uint winIni);

        const uint SpiGetClientAreaAnimation = 0x1042;

        static bool ReadOsReducedMotion()
        {
            try
            {
                int on = 1;
                if (!SystemParametersInfo(SpiGetClientAreaAnimation, 0, ref on, 0)) return false;
                return on == 0;
            }
            catch (Exception) { return false; }
        }
#else
        static bool ReadOsReducedMotion() { return false; }
#endif
    }
}
