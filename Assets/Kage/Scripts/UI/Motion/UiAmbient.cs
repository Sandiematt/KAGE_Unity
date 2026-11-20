using System;

namespace Kage
{
    /// <summary>Where one drifting petal is and how it looks at a moment (JS wPetalGo + wPetalSway), in the field's own px, y down.</summary>
    public struct PetalState
    {
        public float X, Y, Alpha, Rot, TurnX, Size;
    }

    /// <summary>
    /// The JS drifting sakura petals (<c>wPetals</c>, wonder.css <c>.w-petals</c>) as a pure model: n petals travelling from one point to
    /// another in a w x h box, each with its own speed, size, sway and spin, deterministic per index. <see cref="Sample"/> gives petal i at a
    /// time: it travels linearly (wPetalGo, fading in over the first 8% and out over the last 12% of its flight) and the leaf itself sways and
    /// tumbles (wPetalSway, 14 px either way, -30 to 50 degrees, a half turn about its own vertical axis), back and forth.
    /// </summary>
    public sealed class PetalField
    {
        public enum Direction { Fall, Wind }

        public sealed class Petal
        {
            public float X0, Y0, X1, Y1, Duration, Delay, Size, SwayPeriod, Opacity;
        }

        public readonly Petal[] Petals;
        public readonly float Width, Height;

        static double Rnd(double k, int n)
        {
            double s = Math.Sin(k * 127.1 + n * 31.7) * 43758.5453;
            return s - Math.Floor(s);
        }

        public PetalField(int n, float w, float h, Direction dir)
        {
            Width = w; Height = h;
            Petals = new Petal[n];
            for (int i = 0; i < n; i++)
            {
                double a = Rnd(i + 1, n), b = Rnd(i + 7.3, n), c = Rnd(i + 13.9, n), d = Rnd(i + 21.1, n);
                bool wind = dir == Direction.Wind;
                double x0 = wind ? w + 30 : a * w * 1.1 - w * 0.1;
                double y0 = wind ? b * h * 0.9 - 10 : -30;
                double x1 = wind ? -40 : x0 + 120 + b * 220;
                double y1 = wind ? y0 + 30 + c * 70 : h + 40;
                double dur = (wind ? 6 : 11) + c * (wind ? 5 : 9);
                Petals[i] = new Petal
                {
                    X0 = (float)Math.Round(x0), Y0 = (float)Math.Round(y0), X1 = (float)Math.Round(x1), Y1 = (float)Math.Round(y1),
                    Duration = (float)Math.Round(dur, 1), Delay = (float)Math.Round(-d * dur, 1),
                    Size = (float)Math.Round(0.6 + a * 0.8, 2), SwayPeriod = (float)Math.Round(1.6 + b * 2.2, 1), Opacity = (float)Math.Round(0.55 + c * 0.45, 2),
                };
            }
        }

        public PetalState Sample(int i, float time)
        {
            var p = Petals[i];
            float local = time - p.Delay;
            float k = (local % p.Duration + p.Duration) % p.Duration / p.Duration;
            float alpha = k < 0.08f ? p.Opacity * (k / 0.08f) : k <= 0.88f ? p.Opacity : p.Opacity * (1f - (k - 0.88f) / 0.12f);
            // the leaf: sway runs back and forth over SwayPeriod seconds (ease-in-out alternate)
            float cyc = time / p.SwayPeriod;
            float frac = cyc - (float)Math.Floor(cyc);
            if (((int)Math.Floor(cyc) & 1) == 1) frac = 1f - frac;
            float e = UiEase.EaseInOut.Evaluate(frac);
            float sway = -14f + 28f * e, rot = -30f + 80f * e, turn = 160f * e;
            return new PetalState
            {
                X = p.X0 + (p.X1 - p.X0) * k + sway,
                Y = p.Y0 + (p.Y1 - p.Y0) * k,
                Alpha = alpha,
                Rot = rot,
                TurnX = (float)Math.Abs(Math.Cos(turn * Math.PI / 180.0)),
                Size = p.Size,
            };
        }
    }

    /// <summary>One drifting cloud band of the title (JS wClouds: [y, width, seconds, delay, opacity, tone]).</summary>
    public struct CloudBand
    {
        public float Y, Width, Seconds, Delay, Opacity;
        public bool Lit;

        /// <summary>The band's left edge at <paramref name="time"/>: wCloud runs it from -720 to 1700 linearly, forever.</summary>
        public float X(float time)
        {
            float local = time - Delay;
            float k = (local % Seconds + Seconds) % Seconds / Seconds;
            return -720f + (1700f + 720f) * k;
        }
    }
}
