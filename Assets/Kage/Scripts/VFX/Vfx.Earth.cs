using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Earth and legendary effects (docs/weapon-styles.md): Iwakudaki's ground (stomps, quake waves racing along the floor, golden runes,
    /// rocks that float up and are flung away, light rising off the ground) and the legendary tier's prism (sparks and rings whose hue runs
    /// round the colour wheel). On the effekseer-vfx craft rules as the rest of <see cref="Vfx"/>: the earth element is heavy and ground-up
    /// (matter first: dust, rock, cracks; the gold is the light in the cracks and the runes), the prism is only ever an accent over an
    /// element's own body, never the body itself.
    /// </summary>
    public partial class Vfx
    {
        static readonly Color Gold = new(1f, 0.71f, 0.12f, 1f), GoldHot = new(1f, 0.945f, 0.75f, 1f), Rock = new(0.24f, 0.2f, 0.15f, 1f), RockLit = new(0.42f, 0.34f, 0.24f, 1f);

        /// <summary>A hammer meeting the floor: a gold contact star at the head, a flattened shock ring as wide as the tremor, cracks, dust and thrown rock.</summary>
        public void QuakeStomp(Vector2 at, float radius)
        {
            Star(at + Vector2.up * 0.15f, 0.7f, GoldHot, Gold, 0.1f, 6, true, Order + 60);
            Ring(new Vector2(at.x, 0.03f), 0.1f, radius, 0.09f, Gold, 0.4f, true, 0.16f, 2f);
            Ring(new Vector2(at.x, 0.03f), 0.05f, radius * 0.55f, 0.05f, GoldHot, 0.28f, true, 0.16f, 1.6f);
            GroundCracks(at.x, 6, 0.4f, radius * 0.6f, Gold, GoldHot, 0.9f);
            for (int i = 0; i < 10; i++)
                Emit(_sparks, at + new Vector2(Random.Range(-0.3f, 0.3f), 0.04f), new Vector2(Random.Range(-3f, 3f), Random.Range(2f, 6f)), Random.Range(0.04f, 0.07f), Random.Range(0.4f, 0.8f), i % 3 == 0 ? GoldHot : RockLit);
            Dust(at, 1.4f, 0f);
        }

        /// <summary>A golden quake wave racing out from x along the floor both ways: a flattened ring, a band of light, thrown rock and dust.</summary>
        public void QuakeWave(Vector2 at, float radius, float life = 0.6f)
        {
            Ring(new Vector2(at.x, 0.03f), 0.2f, radius, 0.16f, Gold, life, false, 0.13f, 2.4f);
            Ring(new Vector2(at.x, 0.03f), 0.1f, radius * 0.72f, 0.07f, GoldHot, life * 0.7f, false, 0.13f, 2f);
            Dyn("QuakeBand", AddHDR(1.8f), Order + 6, life, false, (rib, u) =>
            {
                float r = radius * KMath.Apply(Ease.OutCubic, u), a = 1f - u;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = at.x + side * r;
                    Strip(rib, new Vector2(x, 0f), new Vector2(x, 0.55f + 0.6f * a), 0f, 1f, 0.12f * a, Gold.WithA(0f), Gold.WithA(0.7f * a), 6);
                }
            });
            for (int i = 0; i < 16; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Emit(_sparks, new Vector2(at.x + side * Random.Range(0.3f, radius * 0.6f), 0.04f), new Vector2(side * Random.Range(1f, 3f), Random.Range(2.5f, 6.5f)), Random.Range(0.05f, 0.09f), Random.Range(0.5f, 0.9f), i % 4 == 0 ? Gold : Rock);
            }
            for (int i = 0; i < 8; i++)
                Emit(_dust, new Vector2(at.x + Random.Range(-radius * 0.7f, radius * 0.7f), 0.08f), new Vector2(Random.Range(-1f, 1f), Random.Range(0.3f, 1.1f)), Random.Range(0.35f, 0.6f), Random.Range(0.7f, 1.1f), new Color(0.36f, 0.3f, 0.24f, 0.55f));
        }

        /// <summary>
        /// A rune circle on the floor (flattened), two gold rings and a ring of glyph ticks that turn slowly; it lights over the first fifth of its
        /// life, pulses, and fades at the end. <paramref name="col"/> and <paramref name="hot"/> colour it (gold for Iwakudaki, red for Onikiri).
        /// </summary>
        public void RuneCircle(Vector2 at, float radius, Color col, Color hot, float life)
        {
            Dyn("RuneCircle", AddHDR(2f), Order - 6, life, false, (rib, u) =>
            {
                float k = Mathf.Clamp01(u / 0.2f) * (u > 0.85f ? 1f - (u - 0.85f) / 0.15f : 1f);
                float pulse = 0.75f + 0.25f * Mathf.Sin(u * 40f);
                const float sy = 0.24f;
                for (int ring = 0; ring < 2; ring++)
                {
                    float r = radius * (ring == 0 ? 1f : 0.78f), w = ring == 0 ? 0.035f : 0.02f;
                    Color c = (ring == 0 ? col : hot).WithA(0.85f * k * pulse);
                    rib.Break();
                    for (int i = 0; i <= 48; i++)
                    {
                        float a = i / 48f * Mathf.PI * 2f;
                        Vector2 d = new(Mathf.Cos(a), Mathf.Sin(a) * sy);
                        rib.Add(at + d * (r + w), at + d * (r - w), c.WithA(0f), c);
                    }
                }
                float spin = u * 1.6f;
                for (int i = 0; i < 16; i++)
                {
                    float a = i / 16f * Mathf.PI * 2f + spin;
                    Vector2 d = new(Mathf.Cos(a), Mathf.Sin(a) * sy);
                    Vector2 p0 = at + d * (radius * 0.82f), p1 = at + d * (radius * 0.96f);
                    Color c = (i % 2 == 0 ? hot : col).WithA(0.9f * k);
                    Strip(rib, p0, p1, 0f, 1f, 0.025f, c.WithA(0f), c, 3);
                }
            });
        }

        /// <summary>Long shafts of light rising off the ground round x (the JS goldenQuake sky), flickering, through <paramref name="life"/>.</summary>
        public void GroundRays(Vector2 at, float halfWidth, Color col, float life, int n = 7)
        {
            var xs = new float[n]; var ws = new float[n]; var ph = new float[n];
            for (int i = 0; i < n; i++) { xs[i] = at.x + Random.Range(-halfWidth, halfWidth); ws[i] = Random.Range(0.1f, 0.3f); ph[i] = Random.Range(0f, 6f); }
            Dyn("GroundRays", AddHDR(1.4f), Order - 30, life, false, (rib, u) =>
            {
                float k = Mathf.Clamp01(u / 0.15f) * (u > 0.8f ? 1f - (u - 0.8f) / 0.2f : 1f);
                for (int i = 0; i < n; i++)
                {
                    float fl = 0.6f + 0.4f * Mathf.Sin(u * 30f + ph[i]);
                    Vector2 b = new(xs[i], 0f), t = new(xs[i] + 0.4f, 4.5f);
                    Strip(rib, b, t, 0f, 1f, ws[i], col.WithA(0f), col.WithA(0.26f * k * fl), 6);
                }
            });
        }

        /// <summary>Rocks that float up off the floor and hang (Earthsplitter's anticipation); <see cref="Rocks.Fling"/> throws them away from a point.</summary>
        public sealed class Rocks
        {
            internal System.Action<Vector2> fling;
            /// <summary>Every rock is thrown away from <paramref name="from"/> and falls (the slam).</summary>
            public void Fling(Vector2 from) => fling?.Invoke(from);
        }

        sealed class RockFx : Fx
        {
            public Ribbon rib;
            public Vector2[] pos, vel; public float[] rot, spin, size, rise; public int seed;
            public bool flung; public float flungAt;
            public Color body, lit, glow;

            public override void Tick(float u)
            {
                float dt = unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                rib.Begin();
                for (int i = 0; i < pos.Length; i++)
                {
                    if (!flung)
                    {
                        float target = rise[i];
                        pos[i].y = Mathf.Lerp(pos[i].y, target + Mathf.Sin(t * 2f + i) * 0.04f, 1f - Mathf.Exp(-2.2f * dt));
                    }
                    else
                    {
                        vel[i].y -= 14f * dt;
                        pos[i] += vel[i] * dt;
                        if (pos[i].y < size[i] * 0.6f && vel[i].y < 0f) { pos[i].y = size[i] * 0.6f; vel[i] = new Vector2(vel[i].x * 0.5f, -vel[i].y * 0.25f); }
                    }
                    rot[i] += spin[i] * dt * (flung ? 3f : 1f);
                    float fade = flung ? Mathf.Clamp01(1f - (t - flungAt - 0.9f) / 0.5f) : Mathf.Clamp01(t / 0.3f);
                    Draw(i, fade);
                }
                rib.End();
            }

            void Draw(int i, float fade)
            {
                // a seven-sided rock: a dark body, a lit upper face and a gold crack across it
                const int n = 7;
                Vector2 c = pos[i];
                float r = size[i];
                var pts = new Vector2[n];
                for (int k = 0; k < n; k++)
                {
                    float a = k / (float)n * Mathf.PI * 2f + rot[i];
                    float rr = r * (0.72f + 0.42f * Mathf.Abs(Mathf.Sin((seed + i * 7 + k) * 12.9898f)));
                    pts[k] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
                }
                Color b = body.WithA(fade), l = lit.WithA(fade), g = glow.WithA(fade * 0.9f);
                for (int k = 0; k < n; k++) rib.AddTriangle(c, pts[k], pts[(k + 1) % n], b, b, b);
                rib.AddTriangle(c + new Vector2(0f, r * 0.15f), pts[1], pts[2], l, l, l);
                Vector2 d = new(Mathf.Cos(rot[i] + 0.6f), Mathf.Sin(rot[i] + 0.6f));
                Vector2 nrm = new(-d.y, d.x);
                rib.AddTriangle(c - d * r * 0.5f + nrm * 0.01f, c + d * r * 0.1f - nrm * 0.02f, c + d * r * 0.45f + nrm * 0.012f, g, g, g);
            }

            public override void Kill() { base.Kill(); }
        }

        /// <summary><paramref name="n"/> rocks rising off the floor across x ± halfWidth to hang 0.6 to 2.2 m up; returns the handle that flings them.</summary>
        public Rocks FloatingRocks(Vector2 at, float halfWidth, int n, float life)
        {
            var go = new GameObject("FloatingRocks");
            go.transform.SetParent(transform, false);
            var rib = new Ribbon("FloatingRocks", go.transform, MeshKit.Alpha, Order + 2);
            var fx = new RockFx
            {
                go = go, rib = rib, life = life, unscaled = false, seed = Random.Range(0, 1000),
                pos = new Vector2[n], vel = new Vector2[n], rot = new float[n], spin = new float[n], size = new float[n], rise = new float[n],
                body = Rock, lit = RockLit, glow = Gold,
            };
            for (int i = 0; i < n; i++)
            {
                fx.pos[i] = new Vector2(at.x + Mathf.Lerp(-halfWidth, halfWidth, (i + Random.Range(0.1f, 0.9f)) / n), 0.05f);
                fx.size[i] = Random.Range(0.09f, 0.22f);
                fx.rise[i] = Random.Range(0.6f, 2.2f);
                fx.rot[i] = Random.Range(0f, 6.28f);
                fx.spin[i] = Random.Range(-1.2f, 1.2f);
            }
            _live.Add(fx);
            fx.Tick(0f);
            return new Rocks
            {
                fling = from =>
                {
                    if (fx.flung) return;
                    fx.flung = true; fx.flungAt = fx.t;
                    fx.life = fx.t + 1.5f;
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 away = fx.pos[i] - from;
                        float side = away.x >= 0f ? 1f : -1f;
                        fx.vel[i] = new Vector2(side * Random.Range(2f, 6f), Random.Range(2f, 7f));
                    }
                },
            };
        }

        // =====================================================================================
        //  THE PRISM (the legendary tier)
        // =====================================================================================
        /// <summary>Sparks whose hues run round the wheel (the legendary accent), bursting from p and hanging.</summary>
        public void PrismSparks(Vector2 p, int n, float speed = 1f)
        {
            float h0 = Random.value;
            for (int i = 0; i < n; i++)
            {
                Color c = Color.HSVToRGB(Mathf.Repeat(h0 + i / (float)Mathf.Max(1, n), 1f), 0.55f, 1f);
                Emit(_sparks, p, KMath.Dir(Random.Range(0f, 360f)) * Random.Range(1.5f, 5f) * speed, Random.Range(0.025f, 0.045f), Random.Range(0.3f, 0.6f), c);
            }
        }

        /// <summary>A ring whose hue runs round the wheel and drifts as it opens (the legendary finisher's accent), over the art's own rings.</summary>
        public void PrismRing(Vector2 c, float r0, float r1, float width, float life, float sy = 1f)
        {
            float h0 = Random.value;
            Dyn("PrismRing", AddHDR(2f), Order + 22, life, true, (rib, u) =>
            {
                float r = Mathf.Lerp(r0, r1, KMath.Apply(Ease.OutExpo, u)), w = Mathf.Lerp(width, 0f, KMath.Apply(Ease.InQuad, u));
                const int seg = 48;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    Color cc = Color.HSVToRGB(Mathf.Repeat(h0 + i / (float)seg + u * 0.6f, 1f), 0.55f, 1f).WithA(0.85f * (1f - u));
                    Vector2 d = new(Mathf.Cos(a), Mathf.Sin(a) * sy);
                    rib.Add(c + d * (r + w * 0.5f), c + d * Mathf.Max(0f, r - w * 0.5f), cc.WithA(0f), cc);
                }
            });
        }
    }
}
