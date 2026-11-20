using System;
using UnityEngine;

namespace Kage
{
    /// <summary>One letter of the splash title: the key of its image, its bounds in the letter frame, the window it is written in and the angle of its stroke.</summary>
    public sealed class SplashLetter
    {
        public string Key;
        public float X0, Y0, X1, Y1, T0, T1, Angle;
    }

    /// <summary>One slash that carves a letter: a straight line (x0 y0 x1 y1) or an arc (cx cy rx ry rot a0 a1), when it runs and how it fades.</summary>
    public sealed class SplashSlash
    {
        public float T0, T1, Fade, Width;
        public float[] Line, Arc;
    }

    /// <summary>Where a slash is at a moment: how far its head has run along the path, how far its tail has caught up, and its opacity.</summary>
    public struct SlashState
    {
        public float Head, Tail, Alpha;
        public bool Visible;
    }

    /// <summary>
    /// The timeline of the JS opening splash (65-splash.js) as plain data and pure functions of time: the painted scene fading up with a push-in,
    /// K-A-G-E written stroke by stroke (each letter revealed behind a ragged dry-brush front, each cut in by a red blade of light), the final cut
    /// with its bump and shake, the subtitle between two rules and the pulsing prompt. The numbers are the JS ones; the art space is the key art's
    /// own pixels (1774 x 887).
    /// </summary>
    public static class SplashTimeline
    {
        public const float ArtW = 1774f, ArtH = 887f;
        /// <summary>The frame shared by the four letter images, and the point the letters scale about.</summary>
        public static readonly Rect LetterFrame = new Rect(321f, 303f, 1153f, 449f);
        public const float LetterScale = 0.9f;
        public static Vector2 LetterCentre { get { return new Vector2(LetterFrame.x + LetterFrame.width / 2f, LetterFrame.y + LetterFrame.height / 2f); } }

        public static readonly SplashLetter[] Letters =
        {
            new SplashLetter { Key = "k", X0 = 0, Y0 = 0, X1 = 502, Y1 = 448, T0 = 0.5f, T1 = 1.0f, Angle = -1.05f },
            new SplashLetter { Key = "a", X0 = 344, Y0 = 24, X1 = 629, Y1 = 358, T0 = 1.0f, T1 = 1.4f, Angle = -0.55f },
            new SplashLetter { Key = "g", X0 = 590, Y0 = 59, X1 = 830, Y1 = 393, T0 = 1.4f, T1 = 1.8f, Angle = -0.1f },
            new SplashLetter { Key = "e", X0 = 829, Y0 = 40, X1 = 1152, Y1 = 314, T0 = 1.8f, T1 = 2.2f, Angle = -0.18f },
        };

        public static readonly SplashSlash[] Slashes =
        {
            new SplashSlash { T0 = 0.5f, T1 = 0.7f, Fade = 0.32f, Width = 13, Line = new float[] { 300, 830, 700, 290 } },
            new SplashSlash { T0 = 1.0f, T1 = 1.24f, Fade = 0.3f, Width = 11, Arc = new float[] { 760, 600, 330, 70, -0.1f, 2.75f, 6.55f } },
            new SplashSlash { T0 = 1.4f, T1 = 1.58f, Fade = 0.3f, Width = 14, Line = new float[] { 1950, 400, 1000, 650 } },
            new SplashSlash { T0 = 1.8f, T1 = 1.96f, Fade = 0.3f, Width = 14, Line = new float[] { 1120, 690, 1960, 420 } },
        };

        /// <summary>The final cut through K and A, which stays.</summary>
        public const float CutStart = 2.2f, CutEnd = 2.3f;
        public static readonly float[] CutLine = { 462, 670, 1048, 450 };
        public const float MarkStart = 2.6f, TagStart = 2.85f, PromptStart = 3.2f;
        /// <summary>The time the splash shows when it opens finished (reduced motion).</summary>
        public const float FinishedAt = 4f;

        static float Clamp01(float v) { return Math.Max(0f, Math.Min(1f, v)); }

        /// <summary>The share of the letter written at <paramref name="t"/> (the stroke takes 72% of the letter's window).</summary>
        public static float LetterReveal(int i, float t)
        {
            var L = Letters[i];
            return Clamp01((t - L.T0) / ((L.T1 - L.T0) * 0.72f));
        }

        /// <summary>Where the ragged front of the letter's reveal is along its stroke direction, in px from the letter's centre.</summary>
        public static float Front(int i, float k)
        {
            var L = Letters[i];
            float cx = LetterFrame.x + (L.X0 + L.X1) / 2f, cy = LetterFrame.y + (L.Y0 + L.Y1) / 2f;
            float c = (float)Math.Cos(L.Angle), s = (float)Math.Sin(L.Angle);
            float lo = float.MaxValue, hi = float.MinValue;
            float[] xs = { L.X0, L.X1, L.X0, L.X1 }, ys = { L.Y0, L.Y0, L.Y1, L.Y1 };
            for (int n = 0; n < 4; n++)
            {
                float p = (LetterFrame.x + xs[n] - cx) * c + (LetterFrame.y + ys[n] - cy) * s;
                lo = Math.Min(lo, p); hi = Math.Max(hi, p);
            }
            return lo - 40f + (hi - lo + 80f) * UiEase.OutCubic(k);
        }

        /// <summary>The letter's centre in art space (the point its stroke angle turns about).</summary>
        public static Vector2 LetterCentreOf(int i)
        {
            var L = Letters[i];
            return new Vector2(LetterFrame.x + (L.X0 + L.X1) / 2f, LetterFrame.y + (L.Y0 + L.Y1) / 2f);
        }

        public static SlashState Slash(int i, float t)
        {
            var s = Slashes[i];
            if (t < s.T0 || t > s.T1 + s.Fade) return default;
            float u = Clamp01((t - s.T0) / (s.T1 - s.T0));
            float head = UiEase.OutCubic(u);
            float tail = Math.Max(0f, head - 0.55f) + (t > s.T1 ? (t - s.T1) / s.Fade : 0f) * head;
            float a = t > s.T1 ? 1f - (t - s.T1) / s.Fade : 1f;
            return new SlashState { Head = head, Tail = Math.Min(head, tail), Alpha = a, Visible = true };
        }

        /// <summary>The 23 points of a slash's path between <paramref name="tail"/> and <paramref name="head"/> (shares of its length).</summary>
        public static Vector2[] SlashPoints(int i, float head, float tail)
        {
            var s = Slashes[i];
            const int N = 22;
            var pts = new Vector2[N + 1];
            for (int n = 0; n <= N; n++)
            {
                float u = tail + (head - tail) * n / N;
                if (s.Line != null)
                    pts[n] = new Vector2(s.Line[0] + (s.Line[2] - s.Line[0]) * u, s.Line[1] + (s.Line[3] - s.Line[1]) * u);
                else
                {
                    var a = s.Arc;
                    float ang = a[5] + (a[6] - a[5]) * u;
                    float x = (float)Math.Cos(ang) * a[2], y = (float)Math.Sin(ang) * a[3];
                    float cr = (float)Math.Cos(a[4]), sr = (float)Math.Sin(a[4]);
                    pts[n] = new Vector2(a[0] + x * cr - y * sr, a[1] + x * sr + y * cr);
                }
            }
            return pts;
        }

        /// <summary>The letters' little bump when the final cut lands.</summary>
        public static float Bump(float t)
        {
            if (t <= CutStart) return 1f;
            return 1f + 0.045f * Math.Max(0f, 1f - (t - CutStart) / 0.18f) * (float)Math.Sin(Clamp01((t - CutStart) / 0.18f) * Math.PI);
        }

        /// <summary>The shake of the whole picture right after the cut, in art px.</summary>
        public static float Shake(float t)
        {
            if (t > CutStart && t < CutStart + 0.32f) return (1f - (t - CutStart) / 0.32f) * 7f;
            return t == CutStart ? 7f : 0f;
        }

        public static float SceneAlpha(float t) { return UiEase.OutCubic(Clamp01(t / 0.6f)); }

        public static float SceneZoom(float t) { return 1.05f - 0.05f * SceneAlpha(t) + 0.006f * (float)Math.Sin(t * 0.6f); }

        public static float MarkAlpha(float t) { return UiEase.OutCubic(Clamp01((t - MarkStart) / 0.4f)); }

        public static float PromptAlpha(float t)
        {
            float pk = Clamp01((t - PromptStart) / 0.45f);
            if (pk <= 0f) return 0f;
            return pk * (0.62f + 0.38f * (float)Math.Sin((t - PromptStart) * 3.2f + 1.6f));
        }
    }
}
