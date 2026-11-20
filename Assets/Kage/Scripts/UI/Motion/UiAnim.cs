using System;
using System.Collections.Generic;

namespace Kage
{
    /// <summary>
    /// A CSS cubic-bezier easing curve (the JS UI's <c>--ease</c>, <c>--pop</c> and the wipe's curve). <see cref="Evaluate"/> maps the
    /// time share x in 0..1 to the value share y; the curve is solved by bisection with a fixed iteration count, so it always ends.
    /// </summary>
    public readonly struct CubicBezier
    {
        public readonly float X1, Y1, X2, Y2;

        public CubicBezier(float x1, float y1, float x2, float y2) { X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; }

        static float Coord(float t, float a, float b)
        {
            float u = 1f - t;
            return 3f * u * u * t * a + 3f * u * t * t * b + t * t * t;
        }

        public float Evaluate(float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            if (Math.Abs(X1 - Y1) < 1e-6f && Math.Abs(X2 - Y2) < 1e-6f) return x;      // linear
            float lo = 0f, hi = 1f, t = x;
            for (int i = 0; i < 32; i++)                                                // fixed cap: 2^-32 of the interval
            {
                t = (lo + hi) * 0.5f;
                float cx = Coord(t, X1, X2);
                if (Math.Abs(cx - x) < 1e-5f) break;
                if (cx < x) lo = t; else hi = t;
            }
            return Coord(t, Y1, Y2);
        }
    }

    /// <summary>The easings of the JS stylesheet (styles.css, wonder.css) by their CSS names.</summary>
    public static class UiEase
    {
        /// <summary>--ease: cubic-bezier(.2, .7, .2, 1), the default of every rise and hover.</summary>
        public static readonly CubicBezier Ease = new CubicBezier(.2f, .7f, .2f, 1f);
        /// <summary>--pop: cubic-bezier(.3, 1.6, .5, 1), overshoots: cards, nodes, the VS mark.</summary>
        public static readonly CubicBezier Pop = new CubicBezier(.3f, 1.6f, .5f, 1f);
        /// <summary>The ink wipe's curve: cubic-bezier(.65, 0, .35, 1).</summary>
        public static readonly CubicBezier Wipe = new CubicBezier(.65f, 0f, .35f, 1f);
        public static readonly CubicBezier EaseOut = new CubicBezier(0f, 0f, .58f, 1f);
        public static readonly CubicBezier EaseIn = new CubicBezier(.42f, 0f, 1f, 1f);
        public static readonly CubicBezier EaseInOut = new CubicBezier(.42f, 0f, .58f, 1f);
        public static readonly CubicBezier Linear = new CubicBezier(0f, 0f, 1f, 1f);

        /// <summary>The JS Ease.outCubic (the counters and the splash).</summary>
        public static float OutCubic(float k) { k = Math.Max(0f, Math.Min(1f, k)); float u = 1f - k; return 1f - u * u * u; }
    }

    /// <summary>What an animation does to a node at one moment: opacity, a move, a scale and a turn about the node's centre.</summary>
    public struct UiPose
    {
        public float Alpha, X, Y, Scale, ScaleX, ScaleY, Rot;

        /// <summary>No change: opaque, in place, full size.</summary>
        public static UiPose Identity { get { return new UiPose { Alpha = 1f, Scale = 1f, ScaleX = 1f, ScaleY = 1f }; } }

        public float SX { get { return Scale * ScaleX; } }
        public float SY { get { return Scale * ScaleY; } }
    }

    public enum UiProp { Alpha, X, Y, Scale, ScaleX, ScaleY, Rot }

    /// <summary>
    /// One CSS keyframe animation as plain data: which properties move, between which keys (time share 0..1), with which easing per
    /// interval, for how long, after how long, and whether it repeats (back and forth for <c>alternate</c>). <see cref="Sample"/> answers
    /// "what is the node's pose at time t", so any animation can be checked without a scene and played from one clock.
    /// X and Y are CSS pixels (+Y is down). Rot is CSS degrees (positive is clockwise). Before the delay the first key holds and after the end
    /// the last key holds (CSS <c>both</c> fill), unless the animation loops.
    /// </summary>
    public sealed class UiAnim
    {
        struct Key { public float t, v; }

        sealed class Track
        {
            public UiProp prop;
            public readonly List<Key> keys = new List<Key>();
        }

        public readonly string Name;
        public float Duration;
        public float Delay;
        public CubicBezier Easing = UiEase.Ease;
        public bool Loop, Alternate;
        readonly List<Track> tracks = new List<Track>();

        public UiAnim(string name, float duration, float delay = 0f)
        {
            Name = name; Duration = Math.Max(0.0001f, duration); Delay = delay;
        }

        public UiAnim Eased(CubicBezier e) { Easing = e; return this; }

        public UiAnim Repeat(bool alternate = false) { Loop = true; Alternate = alternate; return this; }

        public UiAnim Delayed(float seconds) { Delay = seconds; return this; }

        /// <summary>Adds a key: property <paramref name="p"/> is <paramref name="v"/> at time share <paramref name="t"/> (0..1).</summary>
        public UiAnim At(UiProp p, float t, float v)
        {
            Track tr = tracks.Find(x => x.prop == p);
            if (tr == null) { tr = new Track { prop = p }; tracks.Add(tr); }
            tr.keys.Add(new Key { t = t, v = v });
            tr.keys.Sort((a, b) => a.t.CompareTo(b.t));
            return this;
        }

        /// <summary>CSS <c>from</c> and <c>to</c> in one go.</summary>
        public UiAnim FromTo(UiProp p, float from, float to) { return At(p, 0f, from).At(p, 1f, to); }

        /// <summary>The time at which a non-looping animation ends (delay plus duration).</summary>
        public float EndTime { get { return Delay + Duration; } }

        public bool IsFinished(float time) { return !Loop && time >= EndTime; }

        /// <summary>The share of the animation reached at <paramref name="time"/>, 0..1 (a loop wraps; <c>alternate</c> runs back and forth).</summary>
        public float Progress(float time)
        {
            float local = time - Delay;
            if (local <= 0f) return 0f;
            if (!Loop) return Math.Min(1f, local / Duration);
            float cycles = local / Duration;
            float whole = (float)Math.Floor(cycles);
            float frac = cycles - whole;
            if (Alternate && ((int)whole & 1) == 1) frac = 1f - frac;
            return frac;
        }

        public UiPose Sample(float time)
        {
            float p = Progress(time);
            var pose = UiPose.Identity;
            foreach (var tr in tracks)
            {
                float v = Value(tr, p);
                switch (tr.prop)
                {
                    case UiProp.Alpha: pose.Alpha = v; break;
                    case UiProp.X: pose.X = v; break;
                    case UiProp.Y: pose.Y = v; break;
                    case UiProp.Scale: pose.Scale = v; break;
                    case UiProp.ScaleX: pose.ScaleX = v; break;
                    case UiProp.ScaleY: pose.ScaleY = v; break;
                    case UiProp.Rot: pose.Rot = v; break;
                }
            }
            return pose;
        }

        float Value(Track tr, float p)
        {
            var k = tr.keys;
            if (k.Count == 0) return 0f;
            if (p <= k[0].t) return k[0].v;
            for (int i = 1; i < k.Count; i++)
            {
                if (p <= k[i].t)
                {
                    float span = k[i].t - k[i - 1].t;
                    float u = span <= 0f ? 1f : (p - k[i - 1].t) / span;
                    return k[i - 1].v + (k[i].v - k[i - 1].v) * Easing.Evaluate(u);
                }
            }
            return k[k.Count - 1].v;
        }
    }

    /// <summary>
    /// The JS stylesheet's keyframe animations, named as in the CSS. Durations and offsets are the JS ones; a screen picks the one its
    /// element uses and its own delay.
    /// </summary>
    public static class UiAnims
    {
        /// <summary>wRise (wonder.css): up 18 px and in, <c>--ease</c>.</summary>
        public static UiAnim WRise(float duration = .6f, float delay = 0f)
        {
            return new UiAnim("wRise", duration, delay).FromTo(UiProp.Alpha, 0, 1).FromTo(UiProp.Y, 18, 0);
        }

        /// <summary>wFade: opacity only.</summary>
        public static UiAnim WFade(float duration = .35f, float delay = 0f)
        {
            return new UiAnim("wFade", duration, delay).FromTo(UiProp.Alpha, 0, 1);
        }

        /// <summary>rise (styles.css): up 1.5 rem (24 px) and in.</summary>
        public static UiAnim Rise(float duration = .7f, float delay = 0f)
        {
            return new UiAnim("rise", duration, delay).FromTo(UiProp.Alpha, 0, 1).FromTo(UiProp.Y, 24, 0);
        }

        /// <summary>cardIn2: up 24 px, 95% size, <c>--pop</c> (stage cards, panels, the overlay cards).</summary>
        public static UiAnim CardIn(float duration = .5f, float delay = 0f)
        {
            return new UiAnim("cardIn2", duration, delay).Eased(UiEase.Pop).FromTo(UiProp.Alpha, 0, 1).FromTo(UiProp.Y, 24, 0).FromTo(UiProp.Scale, .95f, 1);
        }

        /// <summary>cpPop: a campaign node pops in from 60%.</summary>
        public static UiAnim NodePop(float delay = 0f)
        {
            return new UiAnim("cpPop", .45f, delay).Eased(UiEase.Pop).FromTo(UiProp.Alpha, 0, 1).FromTo(UiProp.Scale, .6f, 1);
        }

        /// <summary>vsPop / pfVs: the VS mark and the rating drop in large and turned and settle.</summary>
        public static UiAnim VsPop(float duration, float delay, float scaleFrom, float rotFrom)
        {
            return new UiAnim("vsPop", duration, delay).Eased(UiEase.Pop).FromTo(UiProp.Alpha, 0, 1).FromTo(UiProp.Scale, scaleFrom, 1).FromTo(UiProp.Rot, rotFrom, 0);
        }

        /// <summary>toastIn: down from above, 85% size, a little turned, <c>--pop</c>.</summary>
        public static UiAnim ToastIn()
        {
            return new UiAnim("toastIn", .5f).Eased(UiEase.Pop).FromTo(UiProp.Alpha, 0, 1).FromTo(UiProp.Y, -16, 0).FromTo(UiProp.Scale, .85f, 1).FromTo(UiProp.Rot, -2, 0);
        }

        /// <summary>toastOut: away upward and fading, ease-in, .3 s.</summary>
        public static UiAnim ToastOut()
        {
            return new UiAnim("toastOut", .3f).Eased(UiEase.EaseIn).FromTo(UiProp.Alpha, 1, 0).FromTo(UiProp.Y, 0, -12.8f).FromTo(UiProp.Scale, 1, .95f);
        }

        /// <summary>stIn: the story panel rises 19 px, <c>--pop</c>.</summary>
        public static UiAnim StIn()
        {
            return new UiAnim("stIn", .35f).Eased(UiEase.Pop).FromTo(UiProp.Alpha, 0, 1).FromTo(UiProp.Y, 19, 0);
        }

        /// <summary>titleRise (results): the heading rises 24 px, 1 s.</summary>
        public static UiAnim TitleRise(float delay = 0f)
        {
            return new UiAnim("titleRise", 1f, delay).FromTo(UiProp.Alpha, 0, 1).FromTo(UiProp.Y, 24, 0);
        }

        /// <summary>wGlow: opacity .8 to 1 and back, forever (the sun, the halo ring, the play button).</summary>
        public static UiAnim WGlow(float period)
        {
            return new UiAnim("wGlow", period / 2f).Eased(UiEase.EaseInOut).FromTo(UiProp.Alpha, .8f, 1f).Repeat(true);
        }

        /// <summary>hmMoon: the key art's red moon breathes (6 s): glow opacity .55 to 1, scale .96 to 1.06.</summary>
        public static UiAnim HmMoon()
        {
            return new UiAnim("hmMoon", 3f).Eased(UiEase.EaseInOut).FromTo(UiProp.Alpha, .55f, 1f).FromTo(UiProp.Scale, .96f, 1.06f).Repeat(true);
        }

        /// <summary>hmCMoon: the Continue card's moon (5 s): opacity .75 to 1.</summary>
        public static UiAnim HmCMoon()
        {
            return new UiAnim("hmCMoon", 2.5f).Eased(UiEase.EaseInOut).FromTo(UiProp.Alpha, .75f, 1f).Repeat(true);
        }

        /// <summary>hmHill: a hill of the Continue card slides 10 px either way (14 s and 10 s, alternating).</summary>
        public static UiAnim HmHill(float seconds, float delay)
        {
            return new UiAnim("hmHill", seconds, delay).Eased(UiEase.EaseInOut).FromTo(UiProp.X, -10, 10).Repeat(true);
        }

        /// <summary>cpHalo: the current node's ring breathes (2.4 s): scale 1.3 to 1.45, opacity 1 to .45.</summary>
        public static UiAnim CpHalo()
        {
            return new UiAnim("cpHalo", 1.2f).Eased(UiEase.EaseInOut).FromTo(UiProp.Scale, 1.3f, 1.45f).FromTo(UiProp.Alpha, 1f, .45f).Repeat(true);
        }

        /// <summary>pfRipple: the water ring under a fighter (3.2 s): scaleX 1 to 1.06, opacity .9 to .5.</summary>
        public static UiAnim PfRipple(float delay)
        {
            return new UiAnim("pfRipple", 1.6f, delay).Eased(UiEase.EaseInOut).FromTo(UiProp.ScaleX, 1f, 1.06f).FromTo(UiProp.Alpha, .9f, .5f).Repeat(true);
        }

        /// <summary>blink: opacity 1 to .35 and back (period seconds): the boot prompt, Rage ready, the story "more" mark.</summary>
        public static UiAnim Blink(float period)
        {
            return new UiAnim("blink", period / 2f).Eased(UiEase.EaseInOut).FromTo(UiProp.Alpha, 1f, .35f).Repeat(true);
        }

        /// <summary>The press feedback of every JS button (:active scale .96 and back), a quick punch.</summary>
        public static UiAnim Press(float low = .96f)
        {
            return new UiAnim("press", .14f).Eased(UiEase.EaseOut).At(UiProp.Scale, 0f, low).At(UiProp.Scale, 1f, 1f);
        }
    }

    /// <summary>
    /// The focus tween of a menu item: a share 0..1 that moves toward the focused (1) or the resting (0) state in the item's own duration
    /// (the JS hover transitions are .16 to .3 s), so a hover eases in and out instead of snapping. Pure and testable.
    /// </summary>
    public static class UiFocusCurve
    {
        public static float Step(float value, bool focused, float dt, float duration)
        {
            float target = focused ? 1f : 0f;
            if (duration <= 0f) return target;
            float step = dt / duration;
            if (value < target) return Math.Min(target, value + step);
            return Math.Max(target, value - step);
        }
    }

    /// <summary>A number that counts up like the JS results counters: <c>target * outCubic((t - delay) / seconds)</c>.</summary>
    public static class UiCount
    {
        public static int Value(int target, float time, float delay, float seconds)
        {
            float k = Math.Max(0f, Math.Min(1f, seconds <= 0f ? 1f : (time - delay) / seconds));
            return (int)Math.Floor(target * UiEase.OutCubic(k) + 0.5);
        }

        public static float Share(float time, float delay, float seconds)
        {
            return UiEase.OutCubic(seconds <= 0f ? 1f : (time - delay) / seconds);
        }
    }
}
