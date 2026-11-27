using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Key combat actions that earn a snappy Japanese brush-calligraphy callout right where the parry,
    /// clash, dodge, counter or guard break happens.
    /// </summary>
    public enum CombatCalloutKind
    {
        Parry,
        PerfectParry,
        Dodge,
        Counter,
        IaiCounter,
        Clash,
        Punish,
        GuardBreak,
        GuardBroken,
        Parried,
        Countered,
        /// <summary>A sub-boss or the boss turned Kage's rage art aside (Fighter.RageArtCounter.cs): PARRIED's red brush, held longer.</summary>
        RageCountered,
    }

    /// <summary>Minimal 3-dot timing state shown under Kage's health/rage panel (Ready / Window / Perfect).</summary>
    public enum HudTimingState
    {
        /// <summary>Neutral / ready: three subtle hollow rings (○ ○ ○).</summary>
        Ready,
        /// <summary>Active parry or dodge timing window: centre dot glows gold (○ ● ○).</summary>
        Window,
        /// <summary>Just landed a Perfect Parry or Perfect Dodge: all three dots glow gold (● ● ●).</summary>
        Perfect,
    }

    /// <summary>
    /// Visual and timing spec for one <see cref="CombatCalloutKind"/>: ALL-CAPS English word, hand-brushed
    /// sprite key under <c>Resources/UI/Img/</c>, compact display height (1280x720 stage px), timing-dot pattern
    /// shown beneath defensive/evasive callouts, and animation durations matching the Combat Callout UI design.
    /// </summary>
    public readonly struct CombatCalloutSpec
    {
        public readonly CombatCalloutKind Kind;
        public readonly string Label;
        public readonly string SpriteId;
        public readonly float DisplayHeight;
        public readonly Color GlowColor;
        public readonly Color DotColor;
        /// <summary>How many of the 3 minimal dots under the callout are filled (0 = hide dot row, 1 = ● ○ ○, 2 = ● ● ○, 3 = ● ● ●).</summary>
        public readonly int FilledDots;
        public readonly bool GoldDots;
        public readonly float PopInDuration;
        public readonly float HoldDuration;
        public readonly float FadeOutDuration;

        public float TotalDuration => PopInDuration + HoldDuration + FadeOutDuration;

        public CombatCalloutSpec(CombatCalloutKind kind, string label, string spriteId, float displayHeight,
            Color glowColor, Color dotColor, int filledDots, bool goldDots,
            float popIn = 0.10f, float hold = 0.25f, float fadeOut = 0.48f)
        {
            Kind = kind;
            Label = label;
            SpriteId = spriteId;
            DisplayHeight = displayHeight;
            GlowColor = glowColor;
            DotColor = dotColor;
            FilledDots = filledDots;
            GoldDots = goldDots;
            PopInDuration = popIn;
            HoldDuration = hold;
            FadeOutDuration = fadeOut;
        }
    }

    /// <summary>The sprite mapping, palette and timing for every core combat callout.</summary>
    public static class CombatCalloutCatalog
    {
        static Color H(string hex) => KMath.Hex(hex);

        static readonly CombatCalloutSpec[] Specs =
        {
            new(CombatCalloutKind.Parry,        "PARRY",         "callout_parry",         42f, H("#FFF7E4"), H("#F6F2E8"), 2, false, 0.10f, 0.25f, 0.48f),
            new(CombatCalloutKind.PerfectParry, "PERFECT PARRY", "callout_perfect_parry", 56f, H("#FFCB5C"), H("#FFE17A"), 3, true,  0.10f, 0.30f, 0.52f),
            new(CombatCalloutKind.Dodge,        "DODGE",         "callout_dodge",         40f, H("#E6F4FF"), H("#F6F2E8"), 1, false, 0.09f, 0.24f, 0.46f),
            new(CombatCalloutKind.Counter,      "COUNTER",       "callout_counter",       44f, H("#E83B3B"), H("#F25C54"), 0, false, 0.09f, 0.24f, 0.46f),
            new(CombatCalloutKind.IaiCounter,   "IAI COUNTER",   "callout_iai_counter",   52f, H("#F05844"), H("#FFE094"), 0, false, 0.10f, 0.26f, 0.48f),
            new(CombatCalloutKind.Clash,        "CLASH",         "callout_clash",         42f, H("#F4DFA4"), H("#F8F2E4"), 0, false, 0.09f, 0.24f, 0.46f),
            new(CombatCalloutKind.Punish,       "PUNISH",        "callout_punish",        40f, H("#7CE0F4"), H("#E8F8FF"), 0, false, 0.09f, 0.24f, 0.46f),
            new(CombatCalloutKind.GuardBreak,   "GUARD BREAK",   "callout_guard_break",   52f, H("#E83838"), H("#F05248"), 0, false, 0.10f, 0.28f, 0.52f),
            new(CombatCalloutKind.GuardBroken,  "GUARD BROKEN",  "callout_guard_broken",  52f, H("#D82830"), H("#F0444C"), 0, false, 0.10f, 0.28f, 0.52f),
            new(CombatCalloutKind.Parried,      "PARRIED",       "callout_parried",       42f, H("#E0343C"), H("#F04850"), 0, false, 0.09f, 0.25f, 0.48f),
            new(CombatCalloutKind.Countered,    "COUNTERED",     "callout_parried",       42f, H("#D83240"), H("#F04854"), 0, false, 0.09f, 0.25f, 0.48f),
            // two brushed lines like GUARD BROKEN, in PARRIED's palette; held long enough to read through the reel (Tools/callout_art/make_rage_countered.py)
            new(CombatCalloutKind.RageCountered, "RAGE COUNTERED", "callout_rage_countered", 60f, H("#E0343C"), H("#F04850"), 0, false, 0.10f, 0.70f, 0.60f),
        };

        public static CombatCalloutSpec Get(CombatCalloutKind kind)
        {
            int i = (int)kind;
            return i >= 0 && i < Specs.Length ? Specs[i] : Specs[0];
        }
    }

    /// <summary>
    /// Live state of one world-anchored combat callout following the 3-phase animation flow:
    /// Pop-in (0.08–0.12 s) → Hold (~0.20–0.30 s) → Fade-out (~0.40–0.60 s), stepped in unscaled time
    /// so hitstop and perfect-dodge slow motion never stall the feel.
    /// </summary>
    public struct HudCalloutEntry
    {
        public int Id;
        public CombatCalloutKind Kind;
        public string Label;
        /// <summary>World-space point where the parry, clash, dodge or break happened.</summary>
        public Vector2 WorldPos;
        public bool HasWorldPos;
        public float Age;
        public float PopIn;
        public float Hold;
        public float FadeOut;
        /// <summary>Vertical stack index for overlapping nearby callouts (0 = at contact, 1 = cleanly lifted above).</summary>
        public int StackIndex;
        public float VisualStack;

        public float Duration => PopIn + Hold + FadeOut;
        public float Progress => Duration > 0f ? Mathf.Clamp01(Age / Duration) : 1f;

        /// <summary>Pop-in (0 -&gt; 1), crisp Hold (1), smooth Fade-out (1 -&gt; 0).</summary>
        public float Alpha
        {
            get
            {
                float a;
                if (Age < PopIn)
                    a = KMath.Apply(Ease.OutCubic, Age / Mathf.Max(0.01f, PopIn));
                else if (Age < PopIn + Hold)
                    a = 1f;
                else
                    a = 1f - KMath.Smooth01((Age - PopIn - Hold) / Mathf.Max(0.01f, FadeOut));
                float stackDim = Mathf.Lerp(1f, 0.68f, Mathf.Clamp01(VisualStack));
                return a * stackDim;
            }
        }

        /// <summary>Snappy pop-in scale (0.76 -&gt; 1.00 with subtle overshoot), crisp hold, gentle settle on fade-out.</summary>
        public float Scale
        {
            get
            {
                float s;
                if (Age < PopIn)
                    s = Mathf.Lerp(0.76f, 1.0f, KMath.Apply(Ease.OutBack, Age / Mathf.Max(0.01f, PopIn)));
                else if (Age < PopIn + Hold)
                    s = 1f;
                else
                    s = Mathf.Lerp(1f, 0.94f, Mathf.Clamp01((Age - PopIn - Hold) / Mathf.Max(0.01f, FadeOut)));
                float stackScale = Mathf.Lerp(1f, 0.84f, Mathf.Clamp01(VisualStack));
                return s * stackScale;
            }
        }

        /// <summary>0..1 impact glow burst during pop-in and early hold (strongest on PERFECT PARRY).</summary>
        public float GlowPulse => 1f - KMath.Apply(Ease.OutCubic, Mathf.Clamp01(Age / Mathf.Max(0.01f, PopIn + Hold * 0.75f)));

        /// <summary>Subtle brush-stroke entrance sweep along X (in JS stage px).</summary>
        public float SweepX => Age < PopIn ? Mathf.Lerp(-12f, 0f, KMath.Apply(Ease.OutExpo, Age / Mathf.Max(0.01f, PopIn))) : 0f;

        /// <summary>World-space rise above the sword clash sparks (metres).</summary>
        public float WorldRiseY => 0.34f + KMath.Apply(Ease.OutCubic, Progress) * 0.22f;
    }

    /// <summary>
    /// Pure-data queue of active world-anchored combat HUD callouts (owned by <see cref="FightHudModel"/>, zero Unity
    /// object references). Capped at <see cref="MaxActive"/> (2) so combos stay crisp and clutter-free.
    /// </summary>
    public sealed class HudCalloutQueue
    {
        public const int MaxActive = 2;
        public const float DuplicateDebounceSeconds = 0.12f;
        public const float NearbyWorldDistance = 1.6f;

        readonly List<HudCalloutEntry> _entries = new(MaxActive);
        int _nextId = 1;

        public IReadOnlyList<HudCalloutEntry> Entries => _entries;
        public int Count => _entries.Count;
        public bool HasAny => _entries.Count > 0;
        public string LatestLabel => _entries.Count > 0 ? _entries[0].Label : null;
        public CombatCalloutKind? LatestKind => _entries.Count > 0 ? _entries[0].Kind : null;

        public void Push(CombatCalloutKind kind, Vector2 worldPos = default, bool hasWorldPos = false)
        {
            var spec = CombatCalloutCatalog.Get(kind);
            if (_entries.Count > 0 && _entries[0].Kind == kind && _entries[0].Age <= DuplicateDebounceSeconds)
            {
                var refreshed = _entries[0];
                refreshed.Id = _nextId++;
                refreshed.Age = 0f;
                refreshed.PopIn = spec.PopInDuration;
                refreshed.Hold = spec.HoldDuration;
                refreshed.FadeOut = spec.FadeOutDuration;
                if (hasWorldPos) { refreshed.WorldPos = worldPos; refreshed.HasWorldPos = true; }
                refreshed.StackIndex = 0;
                refreshed.VisualStack = 0f;
                _entries[0] = refreshed;
                return;
            }

            // lift nearby older callout upward and trim its hold so combos stay clutter-free
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                bool near = !hasWorldPos || !e.HasWorldPos || Vector2.Distance(e.WorldPos, worldPos) <= NearbyWorldDistance;
                if (near) e.StackIndex++;
                float minAge = e.PopIn + e.Hold * 0.5f;
                if (near && e.Age < minAge) e.Age = minAge;
                _entries[i] = e;
            }

            var entry = new HudCalloutEntry
            {
                Id = _nextId++,
                Kind = kind,
                Label = spec.Label,
                WorldPos = worldPos,
                HasWorldPos = hasWorldPos,
                Age = 0f,
                PopIn = spec.PopInDuration,
                Hold = spec.HoldDuration,
                FadeOut = spec.FadeOutDuration,
                StackIndex = 0,
                VisualStack = 0f,
            };
            _entries.Insert(0, entry);
            while (_entries.Count > MaxActive) _entries.RemoveAt(_entries.Count - 1);
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || _entries.Count == 0) return;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                float rate = e.StackIndex == 0 ? 1f : 1.35f;
                e.Age += dt * rate;
                e.VisualStack = KMath.Damp(e.VisualStack, e.StackIndex, 20f, dt);
                if (e.Age >= e.Duration) _entries.RemoveAt(i);
                else _entries[i] = e;
            }
        }

        public void Clear() => _entries.Clear();
    }
}
