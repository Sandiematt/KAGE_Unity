using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Shiden, the Last Lieutenant of Raikon — "purple-black lightning" (sub-boss IV; brief in docs/act1-subbosses-vfx.md). The darkest and most
    /// violent of the four: his lightning is violet inside a heavy purple-black rim; his spear throws blade energy waves — crescents with a black
    /// core, a violet glow outside it, a white-hot leading edge and lightning crawling along it; his impacts explode (a black spike burst, radial
    /// bolts, a violet ring, dark smoke) and the heavy ones get an impact frame; his finishing thrust drives a lance of black-violet energy wrapped
    /// in bolts; a dark electric aura smoulders round him. Shape: crescents, spikes and straight lines; motion: cuts forward and explodes outward;
    /// rhythm: a sharp contact, a violent body, a dark lingering exit. Value: white-hot core, violet body, purple-black backing, a few gold sparks
    /// only in his explosions. Phase 2 pulls Raikon's crimson into the violet.
    /// </summary>
    public partial class Vfx
    {
        public static readonly Color ShidenVolt = KMath.Hex("#B45CFF");      // body: violet
        public static readonly Color ShidenVoltHot = KMath.Hex("#F6EBFF");   // core
        public static readonly Color ShidenVoltDeep = KMath.Hex("#5A16B8");  // deep glow, a cooling scar
        public static readonly Color ShidenUmbra = KMath.Hex("#0B0414");     // purple-black: the rim of his lightning, the core of his waves
        public static readonly Color ShidenSmoke = KMath.Hex("#4A3A66");     // dark smoke matter (value high enough to read on a dark stage)
        public static readonly Color ShidenGold = KMath.Hex("#FFD98A");      // accent: a few sparks in his explosions only
        static Color ShidenViolet => ShidenVolt;
        static Color ShidenStorm => Color.Lerp(ShidenVolt, Crimson, 0.45f);

        // =========================================================================================== lightning pieces

        /// <summary>One of Shiden's bolts: white core, violet body (crimson-violet when <paramref name="crimson"/>), a heavy purple-black rim.</summary>
        public void ShidenBolt(Vector2 a, Vector2 b, float width, float life, bool crimson = false, int forks = 2, int segments = 14, float jitter = -1f, int order = Order + 55)
        {
            Bolt(a, b, width, life, crimson ? ShidenStorm : ShidenVolt, ShidenUmbra, 3.6f, 0.85f, segments, jitter, forks, false, order, 1f, 1f);
        }

        /// <summary>A short crawling arc of Shiden's static (no forks), purple-black behind it.</summary>
        public void ShidenArc(Vector2 a, Vector2 b, float life = 0.11f, float width = 0.015f, bool crimson = false)
        {
            float len = (b - a).magnitude;
            Bolt(a, b, width, life, crimson ? Crimson : ShidenVolt, ShidenUmbra, 3f, 0.7f, Mathf.Clamp(Mathf.RoundToInt(len / 0.07f), 4, 9), len * 0.18f, 0, false, Order + 48);
        }

        /// <summary><paramref name="n"/> of Shiden's arcs crawling on an ellipse round <paramref name="c"/>.</summary>
        public void ShidenCrackleRing(Vector2 c, float rx, float ry, int n, bool crimson = false)
        {
            for (int i = 0; i < n; i++)
            {
                float a0 = Random.Range(0f, 360f), a1 = a0 + Random.Range(35f, 90f) * (Random.value < 0.5f ? 1f : -1f);
                Vector2 p0 = c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad) * rx, Mathf.Sin(a0 * Mathf.Deg2Rad) * ry);
                Vector2 p1 = c + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad) * rx, Mathf.Sin(a1 * Mathf.Deg2Rad) * ry);
                ShidenArc(p0, p1, Random.Range(0.08f, 0.14f), Random.Range(0.013f, 0.02f), crimson && Random.value < 0.5f);
            }
        }

        /// <summary>A scorch where Shiden's lightning hit the floor: a purple-black ellipse, a violet glow ring that dies first, dark smoke.</summary>
        public void ShidenScorch(Vector2 p, float halfWidth, float life = 0.9f) =>
            TintedScorch(p, halfWidth, life, ShidenUmbra.WithA(0.7f), ShidenVolt, ShidenSmoke.WithA(0.5f));

        // =========================================================================================== the blade energy wave

        /// <summary>
        /// A blade energy wave swept along an ellipse round <paramref name="c"/> (radii <paramref name="rx"/>, <paramref name="ry"/>, turned by
        /// <paramref name="rot"/>) from <paramref name="a0"/> through <paramref name="sweep"/> degrees: a black core band, a violet glow outside it and a
        /// fainter one inside, a white-hot leading edge while fresh, and lightning crawling along the outer edge, re-struck every 3 frames; the head races
        /// along the arc, the tail tears away after it, sparks stream off both tips. It rides <paramref name="travel"/> and widens to <paramref name="grow"/>.
        /// </summary>
        public void ShidenEnergyCrescent(Vector2 c, float rot, float rx, float ry, float a0, float sweep, float thick, float life, Vector2 travel = default,
            float grow = 1f, bool crimson = false, bool unscaled = true, int order = Order + 44)
        {
            float seed = Random.Range(0f, 100f);
            Color volt = crimson ? ShidenStorm : ShidenVolt;
            float sparkAt = 0f;
            void Span(float u, out float tail, out float head)
            {
                head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.2f));
                tail = KMath.Apply(Ease.InCubic, Mathf.Clamp01((u - 0.15f) / 0.85f));
            }
            Vector2 Pt(float u, float s, out Vector2 n)
            {
                float e = KMath.Apply(Ease.OutExpo, u);
                float k = Mathf.Lerp(1f, grow, e);
                return Ell(c + travel * e, a0 + sweep * s, rx * k, ry * k, rot, out n);
            }
            float Prof(float x) => Mathf.Pow(Mathf.Clamp01(x), 0.5f) * Mathf.Sqrt(Mathf.Clamp01(1f - Mathf.Pow(Mathf.Clamp01(x), 10f)));
            const int seg = 26;
            // lay: 0 black core, 1 outer glow, 2 inner glow, 3 hot edge, 4 lightning edge
            void Build(Ribbon rib, float u, int lay)
            {
                Span(u, out float tail, out float head);
                if (head - tail <= 0.004f) return;
                int strike = Mathf.FloorToInt(u * life / RestrikeSeconds);
                float fade = 1f - u * 0.4f;
                rib.Break();
                for (int i = 0; i <= seg; i++)
                {
                    float s = Mathf.Lerp(tail, head, i / (float)seg);
                    float x = (s - tail) / Mathf.Max(0.001f, head - tail);
                    Vector2 p = Pt(u, s, out var n);
                    float t = thick * Prof(x) * (1f - u * 0.3f);
                    switch (lay)
                    {
                        case 0:
                        {
                            float tear = x < 0.35f ? Mathf.Clamp01((Noise(s * 9f, seed) + 0.2f - (0.35f - x) * 2.2f) / 0.2f) : 1f;    // the tail tears
                            if (tear < 0.05f) { rib.Break(); continue; }
                            Color cc = ShidenUmbra.WithA(0.92f * fade * tear);
                            rib.Add(p + n * (t * 0.32f), p - n * (t * 0.55f), cc, cc);
                            break;
                        }
                        case 1:
                        {
                            Color cc = volt.WithA(0.95f * fade);
                            rib.Add(p + n * (t * 1.25f), p + n * (t * 0.18f), cc.WithA(0f), cc);
                            break;
                        }
                        case 2:
                        {
                            Color cc = ShidenVoltDeep.WithA(0.7f * fade);
                            rib.Add(p - n * (t * 0.5f), p - n * (t * 0.85f), cc, cc.WithA(0f));
                            break;
                        }
                        case 3:
                        {
                            float fresh = 1f - Mathf.Clamp01(u / 0.6f);
                            float w = 0.01f + t * 0.07f;
                            Color cc = ShidenVoltHot.WithA(fresh * fresh * (0.25f + 0.75f * x));
                            Vector2 e = p + n * (t * 0.3f);
                            rib.Add(e + n * w, e - n * w, cc, cc);
                            break;
                        }
                        default:
                        {
                            float j = (Hash(i + strike * 37, seed) - 0.5f) * thick * 0.55f;
                            float w = 0.008f + 0.006f * x;
                            Color cc = Color.Lerp(volt, ShidenVoltHot, 0.5f).WithA(0.95f * fade * (0.3f + 0.7f * x));
                            Vector2 e = p + n * (t * 0.62f + j);
                            rib.Add(e + n * w, e - n * w, cc, cc);
                            break;
                        }
                    }
                }
                if (lay != 1 || u * life < sparkAt) return;
                sparkAt = u * life + 0.03f;
                Vector2 hp = Pt(u, head, out var hn), tp = Pt(u, tail, out var tn);
                Vector2 tg = KMath.Rot(hn, sweep >= 0f ? 90f : -90f);
                Emit(_sparks, hp + hn * (thick * 0.3f), tg * Random.Range(3f, 6f) + hn * Random.Range(0f, 1.5f), 0.022f, Random.Range(0.18f, 0.3f), Random.value < 0.3f ? Color.white : volt);
                Emit(_sparks, tp, -tg * Random.Range(1f, 2.5f) + tn * Random.Range(-1f, 1f), 0.02f, Random.Range(0.15f, 0.25f), volt);
            }
            Dyn("WaveInner", AddHDR(1.8f), order - 2, life, unscaled, (rib, u) => Build(rib, u, 2));
            Dyn("WaveCore", MeshKit.Alpha, order - 1, life, unscaled, (rib, u) => Build(rib, u, 0));
            Dyn("WaveGlow", AddHDR(2.3f), order, life, unscaled, (rib, u) => Build(rib, u, 1));
            Dyn("WaveEdge", AddHDR(3f), order + 1, life * 0.8f, unscaled, (rib, u) => Build(rib, u, 3));
            Dyn("WaveBolt", AddHDR(2.8f), order + 2, life, unscaled, (rib, u) => Build(rib, u, 4));
        }

        // =========================================================================================== the explosion

        /// <summary>
        /// An explosive lightning impact at <paramref name="p"/>: a white-cored star over a purple-black core, black shards blown outward (most along the
        /// blow, each with a violet rim light), a short violet glow, bolts thrown radially, a pressure ring along the blow, sparks (violet, white and a
        /// few gold), dark smoke that lingers; <paramref name="heavy"/> adds an impact frame (a near-black card on the same frame as the white star) and a shake.
        /// </summary>
        public void ShidenThunderBlast(Vector2 p, Vector2 dir, float s, bool crimson, bool heavy)
        {
            Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            float baseA = KMath.Ang(d);
            float R = 0.55f * s;
            // black shards blown outward (most along the blow), each a thin kite with a violet rim light on one edge
            int shards = Random.Range(9, 13);
            var sa = new float[shards]; var sl = new float[shards]; var sw = new float[shards]; var sr = new float[shards]; var spin = new float[shards];
            for (int i = 0; i < shards; i++)
            {
                sa[i] = (i < shards * 0.6f ? baseA + Random.Range(-70f, 70f) : Random.Range(0f, 360f));
                float along = Mathf.Max(0f, Mathf.Cos((sa[i] - baseA) * Mathf.Deg2Rad));
                sl[i] = R * Random.Range(0.35f, 0.7f) * (1f + 0.6f * along);
                sw[i] = R * Random.Range(0.06f, 0.12f);
                sr[i] = R * Random.Range(0.9f, 1.6f) * (1f + 0.4f * along);
                spin[i] = Random.Range(-25f, 25f);
            }
            Color volt = crimson ? ShidenStorm : ShidenVolt;
            if (heavy) GameFeel.Flash(new Color(0.04f, 0.01f, 0.08f, 0.32f), 0.07f);
            Dyn("BlastGlow", AddHDR(1.6f), Order + 40, 0.2f, true, (rib, u) =>
            {
                float f = 1f - KMath.Apply(Ease.OutQuad, u);
                Blob(rib, p, R * 0.95f * KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.2f)), volt.WithA(0.6f * f), volt.WithA(0f), 20);
            });
            void Shards(Ribbon rib, float u, bool edge)
            {
                float fly = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.5f));
                float fade = 1f - KMath.Apply(Ease.InQuad, Mathf.Clamp01((u - 0.35f) / 0.65f));
                if (fade <= 0.01f) return;
                for (int i = 0; i < shards; i++)
                {
                    float ang = sa[i] + spin[i] * u;
                    Vector2 dd = KMath.Dir(ang), nn = new(-dd.y, dd.x);
                    float r = Mathf.Lerp(R * 0.12f, sr[i], fly);
                    float len = sl[i] * (1f - u * 0.5f);
                    Vector2 mid = p + KMath.Dir(sa[i]) * r;
                    Vector2 tip = mid + dd * (len * 0.75f), tail = mid - dd * (len * 0.25f);
                    Vector2 l = mid + nn * sw[i], rr = mid - nn * sw[i];
                    if (!edge)
                    {
                        Color cc = ShidenUmbra.WithA(0.92f * fade);
                        rib.AddTriangle(tail, l, tip, cc, cc, cc);
                        rib.AddTriangle(tail, tip, rr, cc, cc, cc);
                    }
                    else
                    {
                        Color cc = Color.Lerp(volt, ShidenVoltHot, 0.3f).WithA(0.95f * fade * (1f - u * 0.5f));
                        Vector2 e0 = l, e1 = tip, en = (l - mid).normalized * 0.012f;
                        rib.Break();
                        rib.Add(e0 + en, e0 - en, cc, cc);
                        rib.Add(e1 + en * 0.3f, e1 - en * 0.3f, cc, cc);
                    }
                }
            }
            Dyn("BlastShards", MeshKit.Alpha, Order + 42, 0.36f, true, (rib, u) => Shards(rib, u, false));
            Dyn("BlastShardEdge", AddHDR(2.6f), Order + 43, 0.36f, true, (rib, u) => Shards(rib, u, true));
            Dyn("BlastCore", MeshKit.Alpha, Order + 41, 0.18f, true, (rib, u) =>
            {
                float rc = R * 0.42f * KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.15f)) * (1f - u);
                Blob(rib, p, rc, ShidenUmbra.WithA(0.85f), ShidenUmbra.WithA(0f), 16, 0.3f, R);
            });
            Star(p, 0.75f * s, Color.white, volt, 0.07f, 8, true, Order + 66, 0.5f);
            int bolts = heavy ? 7 : 5;
            for (int i = 0; i < bolts; i++)
            {
                float ang = baseA + (i < bolts - 2 ? Random.Range(-75f, 75f) : Random.Range(100f, 260f));
                float len = Random.Range(0.5f, 1.0f) * s;
                ShidenBolt(p, p + KMath.Dir(ang) * len, Random.Range(0.02f, 0.03f) * s, Random.Range(0.15f, 0.22f), crimson && i == 0, 1, 7, len * 0.2f);
            }
            OrientedRing(p, d, 0.05f, 0.6f * s, 0.05f, ShidenVoltHot, 0.18f, 0.3f, 0.3f, 2.4f);
            for (int i = 0; i < 14; i++)
            {
                Color sc = i < 3 ? ShidenGold : i % 3 == 0 ? Color.white : volt;
                Emit(_sparks, p, KMath.Dir(baseA + Random.Range(-80f, 80f) + (i % 5 == 0 ? 180f : 0f)) * Random.Range(3.5f, 9f) * s, Random.Range(0.022f, 0.04f), Random.Range(0.25f, 0.5f), sc);
            }
            for (int i = 0; i < 4; i++)
                Puff(p + Random.insideUnitCircle * 0.15f, d * Random.Range(0.3f, 0.9f) + new Vector2(0f, Random.Range(0.2f, 0.6f)), ShidenSmoke.WithA(0.55f), Random.Range(0.28f, 0.42f) * s, Random.Range(0.6f, 0.9f));
            if (crimson) Crackle(p, 0.4f * s, 0.35f * s, 2, Crimson);
            if (heavy) GameFeel.Shake(0.2f);
        }

        /// <summary>
        /// The lieutenant's lance: a beam of black-violet energy driven <paramref name="length"/> metres along <paramref name="d"/> from <paramref name="from"/>
        /// (a purple-black core inside a violet glow, a white-hot centre line), two bolts wrapped round it, cone rings riding down it; it flares, holds
        /// for a few frames and erodes from its root. The finishing thrust.
        /// </summary>
        public void ShidenLance(Vector2 from, Vector2 d, float length, bool crimson)
        {
            Vector2 dn = d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.right;
            Vector2 n = new(-dn.y, dn.x);
            Color volt = crimson ? ShidenStorm : ShidenVolt;
            void Beam(Ribbon rib, float u, float w0, Color c, bool soft)
            {
                float head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.15f));
                float tail = KMath.Apply(Ease.InCubic, Mathf.Clamp01((u - 0.35f) / 0.65f));
                if (head - tail <= 0.004f) return;
                const int seg = 16;
                for (int half = 0; half < (soft ? 2 : 1); half++)
                {
                    rib.Break();
                    for (int i = 0; i <= seg; i++)
                    {
                        float s = Mathf.Lerp(tail, head, i / (float)seg);
                        float x = (s - tail) / Mathf.Max(0.001f, head - tail);
                        float w = w0 * Mathf.Pow(Mathf.Sin(Mathf.Clamp01(x * 0.92f + 0.04f) * Mathf.PI), 0.35f) * (1f - u * 0.5f) * (1f + 0.25f * Mathf.Sin(s * 40f + u * 60f));
                        Vector2 p = from + dn * (length * s);
                        Color cc = c.WithA(c.a * (1f - u * 0.5f));
                        if (!soft) rib.Add(p + n * w, p - n * w, cc, cc);
                        else if (half == 0) rib.Add(p + n * w, p, cc.WithA(0f), cc);
                        else rib.Add(p, p - n * w, cc, cc.WithA(0f));
                    }
                }
            }
            Dyn("LanceGlow", AddHDR(2.2f), Order + 56, 0.34f, true, (rib, u) => Beam(rib, u, 0.26f, volt, true));
            Dyn("LanceCore", MeshKit.Alpha, Order + 57, 0.32f, true, (rib, u) => Beam(rib, u, 0.11f, ShidenUmbra.WithA(0.92f), false));
            Dyn("LanceHot", AddHDR(3.2f), Order + 58, 0.24f, true, (rib, u) => Beam(rib, u, 0.016f, ShidenVoltHot, false));
            // the bolts wrap the lance (drawn under it, wandering wide of the core) rather than covering its black core
            for (int k = 0; k < 2; k++)
                ShidenBolt(from + n * (k == 0 ? 0.16f : -0.16f), from + dn * length + n * (k == 0 ? -0.18f : 0.18f), 0.024f, 0.3f, crimson && k == 0, 2, 16, 0.2f, Order + 50);
            for (int i = 0; i < 3; i++)
                OrientedRing(from + dn * (0.3f + i * 0.45f), dn, 0.05f, 0.35f + i * 0.12f, 0.045f - i * 0.008f, i == 0 ? Color.white : volt, 0.16f + i * 0.05f, 0.38f, 0.5f + i * 0.3f, 2.3f);
            for (int i = 0; i < 10; i++)
                Emit(_sparks, from + dn * Random.Range(0.2f, length), dn * Random.Range(3f, 7f) + n * Random.Range(-2f, 2f), 0.024f, Random.Range(0.2f, 0.35f), i % 3 == 0 ? Color.white : volt);
        }

        // =========================================================================================== contact

        /// <summary>
        /// A blow of Shiden's lands: a needle glint (4 frames), a razor needle through the contact and out of the target's back along the blow, a Mach
        /// ring edge-on across it, an explosive lightning burst at the wound; a heavy blow explodes harder (impact frame) and a bolt carries on through;
        /// <paramref name="crimson"/>: the Phase 2 crimson-violet.
        /// </summary>
        public void ShidenPierce(Vector2 p, Vector2 dir, float s, bool heavy, bool crimson)
        {
            Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            Needle(p - d * 0.35f, p + d * (1.5f * s), 0.016f, ShidenViolet, ShidenVoltHot, 0.22f);
            OrientedRing(p + d * 0.12f, d, 0.05f, 0.45f * s, 0.05f, ShidenPalette.Pale, 0.2f, 0.22f, 0.3f, 2.4f);
            ShidenThunderBlast(p + d * 0.06f, d, heavy ? 0.95f * s : 0.7f * s, crimson, heavy);
            if (heavy) ShidenBolt(p, p + d * 1.3f + Vector2.up * Random.Range(-0.15f, 0.15f), 0.03f, 0.22f, crimson, 2, 10);
        }

        /// <summary>A blow of Shiden's is guarded: the point screeches along the guard, a fan of long thin sparks across it, static crawls the guard, a silver glint.</summary>
        public void ShidenScreech(Vector2 p, Vector2 dir)
        {
            Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            Vector2 along = new(-d.y, d.x);
            Star(p, 0.26f, Color.white, ShidenVolt, 0.05f, 4, true, Order + 62, 0.3f);
            LensStreak(p, 0.9f, ShidenPalette.Pale, 0.14f, KMath.Ang(along), 0.01f);
            ShidenCrackleRing(p, 0.2f, 0.34f, 2);
            for (int i = 0; i < 12; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Emit(_sparks, p, (along * side * Random.Range(3f, 7f)) - d * Random.Range(0.5f, 2f), 0.022f, Random.Range(0.16f, 0.3f), i % 3 == 0 ? Color.white : ShidenVolt);
            }
            Puff(p - d * 0.1f, -d * 0.4f + Vector2.up * 0.3f, ShidenSmoke.WithA(0.45f), 0.25f, 0.6f);
        }

        // =========================================================================================== set pieces

        /// <summary>The lieutenant's tell: a silver four-point glint on the point, a long horizontal lens streak, a small violet ring, a wisp of dark smoke.</summary>
        public void ShidenGlint(Vector2 tip)
        {
            Star(tip, 0.32f, Color.white, ShidenPalette.Pale, 0.14f, 4, true, Order + 66, 0.25f);
            LensStreak(tip, 1.3f, ShidenVolt, 0.18f, 0f, 0.012f);
            Ring(tip, 0.02f, 0.24f, 0.022f, ShidenVolt, 0.16f, true, 1f, 2f);
            Puff(tip, new Vector2(0f, 0.4f), ShidenSmoke.WithA(0.4f), 0.18f, 0.5f);
        }

        /// <summary>Static gathering on the spearhead: violet arcs (crimson in Phase 2), dark smoke drawn in, sparks lifting off the point.</summary>
        public void ShidenCrackle(Vector2 tip, bool crimson)
        {
            ShidenArc(tip + Random.insideUnitCircle * 0.14f, tip + Random.insideUnitCircle * 0.2f, 0.1f, 0.015f, crimson && Random.value < 0.5f);
            Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.35f, 0.55f);
            Puff(tip + off, -off / 0.3f, ShidenSmoke.WithA(0.4f), 0.14f, 0.3f);
            for (int i = 0; i < 3; i++)
                Emit(_sparks, tip + Random.insideUnitCircle * 0.12f, new Vector2(Random.Range(-1f, 1f), Random.Range(0.4f, 1.6f)), 0.03f, 0.22f, i == 0 ? Color.white : ShidenVolt);
        }

        /// <summary>
        /// A thrust released: three cone rings leave the point one after another down the line of the spear (a Mach cone), a 40:1 line races 2.2 m ahead
        /// of the point over a purple-black underline, a lens streak; <paramref name="storm"/> (the Ō-Tsuki finisher, Phase 2): the black-violet lance
        /// is driven down the line, an impact frame and a shake.
        /// </summary>
        public void ShidenMachRelease(Vector2 shaftBase, Vector2 tip, bool storm)
        {
            Vector2 d = (tip - shaftBase).sqrMagnitude > 1e-6f ? (tip - shaftBase).normalized : Vector2.right;
            Star(tip, 0.34f, Color.white, ShidenVolt, 0.05f, 4, true, Order + 64, 0.25f);
            for (int i = 0; i < 3; i++)
                OrientedRing(tip + d * (0.05f + i * 0.1f), d, 0.04f, 0.24f + i * 0.1f, 0.035f - i * 0.006f, i == 0 ? Color.white : ShidenPalette.Pale,
                    0.14f + i * 0.04f, 0.4f, 0.4f + i * 0.25f, 2.3f);
            Dyn("MachUnder", MeshKit.Alpha, Order + 50, 0.22f, true, (rib, u) => Strip(rib, u, tip, d * 2.2f, new Vector2(-d.y, d.x), 0.04f, ShidenUmbra.WithA(0.85f)));
            Needle(tip, tip + d * 2.2f, 0.013f, ShidenVolt, ShidenVoltHot, 0.2f);
            LensStreak((shaftBase + tip) * 0.5f, (tip - shaftBase).magnitude * 1.4f, ShidenVolt, 0.16f, KMath.Ang(d), 0.014f);
            if (!storm) return;
            ShidenLance(tip - d * 0.1f, d, 2.6f, true);
            GameFeel.Flash(new Color(0.04f, 0.01f, 0.08f, 0.3f), 0.07f);
            GameFeel.Shake(0.26f);
        }

        /// <summary>The rising fang: a blade energy wave swept from low in front up and over, a bolt laid along it, sparks thrown upward.</summary>
        public void ShidenRisingFang(Vector2 lo, Vector2 hi, bool crimson)
        {
            Vector2 d = hi - lo;
            float side = d.x >= 0f ? -1f : 1f;
            ArcThrough(lo, hi, side, out var c, out float r, out float a0, out float sweep);
            ShidenEnergyCrescent(c, 0f, r, r, a0, sweep, 0.34f, 0.36f, d.normalized * 0.25f, 1.12f, crimson);
            ShidenBolt(lo, hi, 0.024f, 0.22f, crimson, 1, 9);
            for (int i = 0; i < 8; i++)
                Emit(_sparks, Vector2.Lerp(lo, hi, Random.value), d.normalized * Random.Range(3f, 6f) + Vector2.up * Random.Range(0.5f, 2f), 0.025f, Random.Range(0.2f, 0.35f), i % 3 == 0 ? Color.white : ShidenVolt);
        }

        /// <summary>
        /// The Vanguard Break shock: a row of purple-black lightning strikes (2.8 m) marching away from him across the hazard, one every 3 frames, each
        /// exploding where it lands (a spike burst, a ring, a scorch); the last is the biggest; a crimson ground arc links them in Phase 2.
        /// </summary>
        public void ShidenStrikeRow(Vector2 from, Vector2 to, bool crimson)
        {
            const int n = 3;
            const float step = 0.05f;
            int fired = 0;
            Dyn("StrikeRow", MeshKit.Alpha, Order, step * n + 0.05f, false, (rib, u) =>
            {
                float t = u * (step * n + 0.05f);
                while (fired < n && t >= fired * step)
                {
                    Vector2 p = Vector2.Lerp(from, to, n == 1 ? 0.5f : fired / (float)(n - 1));
                    p.y = Mathf.Max(0.02f, p.y);
                    bool last = fired == n - 1;
                    ShidenBolt(p + new Vector2(Random.Range(-0.15f, 0.15f), 2.8f), p, last ? 0.065f : 0.05f, 0.32f, crimson, 2, 14, 0.13f);
                    ShidenThunderBlast(p + Vector2.up * 0.12f, Vector2.up, last ? 0.85f : 0.6f, crimson, false);
                    ShidenScorch(p, 0.45f, 0.9f);
                    fired++;
                }
            });
            if (crimson) Bolt(from + Vector2.up * 0.04f, to + Vector2.up * 0.04f, 0.026f, 0.28f, Crimson, 12, 0.06f, 1);
            GameFeel.Shake(0.26f);
        }

        /// <summary>The Maki-Otoshi bind: the cross-blade beats the guard down — crossing silver and violet glints over a purple-black X (a jūmonji), a tight ring, shards driven down.</summary>
        public void ShidenBindCross(Vector2 at, float facing)
        {
            Star(at, 0.3f, Color.white, ShidenVolt, 0.05f, 4, true, Order + 63, 0.35f);
            for (int k = 0; k < 2; k++)
            {
                Vector2 dd = KMath.Dir(k == 0 ? 45f : -45f);
                Dyn("BindUnder", MeshKit.Alpha, Order + 60, 0.18f, true, (rib, u) => Strip(rib, u, at - dd * 0.38f, dd * 0.76f, new Vector2(-dd.y, dd.x), 0.03f, ShidenUmbra.WithA(0.85f)));
            }
            LensStreak(at, 0.75f, ShidenPalette.SilverHi, 0.15f, 45f, 0.012f);
            LensStreak(at, 0.75f, ShidenVolt, 0.15f, -45f, 0.012f);
            Ring(at, 0.04f, 0.32f, 0.025f, ShidenVolt, 0.13f, true, 1f, 2f);
            Shards(at, new Vector2(facing * 0.35f, -1f), 5, 0.4f, 0.016f, Color.white, ShidenVolt, 0.15f, 26f);
        }

        /// <summary>The Suriage low sweep: a flat blade energy wave skimming the floor from behind to in front, dust thrown forward, sparks scraped up.</summary>
        public void ShidenLowSweep(Vector2 ground, float facing)
        {
            Vector2 g = new(ground.x, Mathf.Max(0f, ground.y));
            Vector2 c = g + new Vector2(facing * 0.55f, 0.62f);
            ShidenEnergyCrescent(c, 0f, 1.15f, 0.46f, facing >= 0f ? 205f : 335f, facing >= 0f ? 140f : -140f, 0.16f, 0.3f, new Vector2(facing * 0.2f, 0f), 1.05f, false);
            Dust(new Vector2(g.x + facing * 1.2f, g.y), 0.6f, facing);
            for (int i = 0; i < 7; i++)
                Emit(_sparks, new Vector2(g.x + facing * 1.5f, g.y + 0.03f), new Vector2(facing * Random.Range(1.5f, 4f), Random.Range(0.8f, 2.2f)), 0.022f, Random.Range(0.22f, 0.36f), i % 2 == 0 ? Color.white : ShidenVolt);
        }

        // =========================================================================================== presence

        /// <summary>
        /// Shiden between moves, his dark electric aura: purple-black smoke smoulders up off his shoulders, a violet glimmer on the point; in Phase 2 dark
        /// flames lick up from his feet, violet and crimson arcs crawl his outline and climb the shaft to the crest.
        /// </summary>
        public void ShidenAura(Fighter f, int level)
        {
            Vector2 tip = f.rig.World(B.Sword, f.BladeTipLocal * 0.93f);
            Emit(_sparks, tip, new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.3f, 1.2f)), 0.026f, 0.26f, ShidenPalette.Accent);
            Puff(f.Pos + new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(1.2f, 1.6f)), new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.4f, 0.8f)),
                ShidenSmoke.WithA(0.45f), Random.Range(0.16f, 0.26f), Random.Range(0.6f, 0.9f));
            if (Random.value < 0.3f) ShidenArc(f.rig.World(B.Sword, f.BladeTipLocal * 0.7f), tip, 0.1f, 0.012f);
            if (level < 1) return;
            for (int i = 0; i < 2; i++)
                Puff(f.Pos + new Vector2(Random.Range(-0.4f, 0.4f), 0.08f), new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.8f, 1.5f)), ShidenSmoke.WithA(0.55f), Random.Range(0.2f, 0.3f), Random.Range(0.5f, 0.8f));
            ShidenCrackleRing(f.Pos + Vector2.up * 1.05f, 0.4f, 0.85f, 1, true);
            ShidenArc(f.rig.World(B.Sword, f.BladeTipLocal * 0.6f), tip, 0.11f, 0.013f, Random.value < 0.5f);
            if (Random.value < 0.35f) ShidenArc(f.Pos + Vector2.up * 1.75f, f.Pos + new Vector2(Random.Range(-0.2f, 0.2f), 2.0f), 0.08f, 0.01f);
        }

        /// <summary>Shiden turns to "Hold the Line": a strike lands on his own position and explodes, a ring of violet runs out over the floor, a dark aura flares up round him.</summary>
        public void ShidenPhaseBurst(Fighter f)
        {
            ShidenStrikeRow(f.Pos, f.Pos, true);
            ShidenThunderBlast(f.Pos + Vector2.up * 1.0f, Vector2.up, 1.4f, true, true);
            Ring(f.Pos + Vector2.up * 0.04f, 0.1f, 1.8f, 0.07f, ShidenVolt, 0.4f, false, 0.18f, 2.2f);
            for (int i = 0; i < 8; i++)
                Puff(f.Pos + new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(0.1f, 1.8f)), new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.6f, 1.4f)), ShidenSmoke.WithA(0.6f),
                    Random.Range(0.3f, 0.45f), Random.Range(0.8f, 1.2f));
            ShidenCrackleRing(f.Pos + Vector2.up * 1.1f, 0.45f, 0.85f, 3, true);
        }
    }

    /// <summary>Shiden's combat look: the thunder trail of the spear's point, explosive piercing impacts.</summary>
    public sealed class ShidenLook : CombatLook
    {
        public override WeaponTrail Trail(Fighter f, TrailSlot slot, Transform parent, int order) =>
            slot == TrailSlot.Blade ? new ShidenSpearTrail(parent, order, f) : null;

        public override bool Hit(Fighter a, Vector2 p, Vector2 dir, HitWindow w, bool crit)
        {
            var v = Vfx.I;
            if (v == null) return false;
            bool heavy = w.heavy || crit;
            v.ShidenPierce(p, dir, heavy ? 1.25f : 1f, heavy, PhaseTwo(a));
            return true;
        }

        public override bool Block(Fighter a, Vector2 p, Vector2 dir, HitWindow w)
        {
            var v = Vfx.I;
            if (v == null) return false;
            v.ShidenScreech(p, dir);
            return true;
        }
    }

    /// <summary>
    /// The path of the spear's point, thunder-infused: a purple-black sheet trailing the last part of the blade, a violet sheet over it, a white needle
    /// along the point's own track, and a line of lightning beside the track re-struck every 3 frames. Gone in 0.18 s; sparks and arcs come off the
    /// point when it moves fast.
    /// </summary>
    public sealed class ShidenSpearTrail : SampledTrail
    {
        readonly float _seed = Random.Range(0f, 100f);

        public ShidenSpearTrail(Transform parent, int order, Fighter f)
            : base("ShidenSpearTrail", parent, 0.18f, order, f, MeshKit.Alpha,
                Vfx.I != null ? Vfx.I.AddHDR(2.0f) : MeshKit.Additive, Vfx.I != null ? Vfx.I.AddHDR(3.0f) : MeshKit.Additive) { }

        protected override void DrawStroke(List<Section> secs, bool live)
        {
            var back = ribs[0]; var sheet = ribs[1]; var needle = ribs[2];
            float now = TestClock != null ? TestClock() : Time.time;
            int strike = Mathf.FloorToInt(now / Vfx.RestrikeSeconds);
            back.Break(); sheet.Break(); needle.Break();
            for (int i = 0; i < secs.Count; i++)
            {
                var s = secs[i];
                float k = 1f - s.age;
                float ramp = Mathf.Clamp01(s.along * 2.5f);
                Color dark = Vfx.ShidenUmbra.WithA(0.75f * k * k * ramp);
                back.Add(Vector2.LerpUnclamped(s.a, s.b, 0.55f), Vector2.LerpUnclamped(s.a, s.b, 1.06f), dark.WithA(0f), dark);
                Color sv = Vfx.ShidenVolt.WithA(0.6f * Mathf.Pow(k, 1.2f) * ramp);
                sheet.Add(Vector2.LerpUnclamped(s.a, s.b, 0.68f), Vector2.LerpUnclamped(s.a, s.b, 1.04f), sv.WithA(0f), sv);
            }
            for (int i = 0; i < secs.Count; i++)
            {
                var s = secs[i];
                float k = 1f - s.age;
                float ramp = Mathf.Clamp01(s.along * 2.5f);
                Vector2 tip = s.b;
                Vector2 prev = i > 0 ? secs[i - 1].b : tip, next = i + 1 < secs.Count ? secs[i + 1].b : tip;
                Vector2 t = next - prev;
                Vector2 n = t.sqrMagnitude > 1e-8f ? new Vector2(-t.y, t.x).normalized : (s.b - s.a).normalized;
                float w = Mathf.Lerp(0.006f, 0.022f, s.along) * k;
                Color core = Color.Lerp(Vfx.ShidenVoltHot, Vfx.ShidenVolt, s.age).WithA(k * ramp);
                needle.Add(tip + n * w, tip - n * w, core, core);
            }
            // lightning running beside the point's track, re-struck every 3 frames
            needle.Break();
            for (int i = 0; i < secs.Count; i += 2)
            {
                var s = secs[i];
                float k = 1f - s.age;
                Vector2 across = (s.b - s.a).sqrMagnitude > 1e-8f ? (s.b - s.a).normalized : Vector2.up;
                float j = (Vfx.Hash(i + strike * 41, _seed) - 0.5f) * 0.16f * k;
                Vector2 q = s.b - across * (0.05f - j);
                float w = 0.007f * k;
                Color c = Color.Lerp(Vfx.ShidenVolt, Vfx.ShidenVoltHot, 0.4f).WithA(0.9f * k * Mathf.Clamp01(s.along * 2.5f));
                needle.Add(q + across * w, q - across * w, c, c);
            }
        }

        protected override void OnMove(Vector2 pa, Vector2 pb, Vector2 a, Vector2 b, float dt)
        {
            var v = Vfx.I;
            if (v == null || dt <= 0f) return;
            Vector2 vel = (b - pb) / dt;
            if (vel.sqrMagnitude < 49f) return;
            if (Random.value < 0.6f) v.Spark(b, vel * 0.3f + Random.insideUnitCircle * 0.6f, Random.value < 0.3f ? Color.white : Vfx.ShidenVolt, 0.02f, 0.2f);
            if (Random.value < 0.25f) v.ShidenArc(b, b - vel.normalized * Random.Range(0.25f, 0.45f) + Random.insideUnitCircle * 0.06f, 0.08f, 0.013f, PhaseTwo && Random.value < 0.4f);
        }
    }

    /// <summary>
    /// Shiden's Vanguard Raijin wave in flight: a standing blade wave skimming the floor — a black-cored crescent opening forward with a violet glow,
    /// a white edge and lightning crawling along it — short bolts struck down to the floor and forward every 3 frames, scorch left along the road, sparks.
    /// </summary>
    public sealed class VanguardWaveLook : MonoBehaviour
    {
        Transform _proj;
        float _dir, _strike, _scorch, _t, _seed;
        bool _crimson;
        Ribbon _core, _glow, _edge;

        public static void Play(Vector2 pos, in CueArgs a)
        {
            if (a.follow == null || Vfx.I == null) return;
            var go = new GameObject("VanguardWaveLook");
            go.transform.SetParent(a.follow, false);
            go.transform.localPosition = Vector3.zero;
            var l = go.AddComponent<VanguardWaveLook>();
            l._proj = a.follow;
            l._dir = a.dir.x >= 0f ? 1f : -1f;
            l._crimson = a.owner?.Phases != null && a.owner.Phases.Index >= 1;
            l._seed = Random.Range(0f, 100f);
            int o = Vfx.Order + 31;
            l._glow = new Ribbon("WaveGlow", go.transform, Vfx.I.AddHDR(2.2f), o);
            l._core = new Ribbon("WaveCore", go.transform, MeshKit.Alpha, o + 1);
            l._edge = new Ribbon("WaveEdge", go.transform, Vfx.I.AddHDR(3f), o + 2);
        }

        void Update()
        {
            var v = Vfx.I;
            if (_proj == null || v == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            _t += dt;
            Vector2 p = _proj.position;
            DrawWave(p);
            _strike -= dt;
            if (_strike <= 0f)
            {
                _strike = Vfx.RestrikeSeconds;
                bool cr = _crimson && Random.value < 0.5f;
                v.ShidenBolt(p, new Vector2(p.x + _dir * Random.Range(0.1f, 0.35f), Mathf.Max(0.01f, p.y - 0.45f)), 0.02f, 0.1f, cr, 0, 5, 0.06f);
                v.ShidenBolt(p, p + new Vector2(_dir * Random.Range(0.3f, 0.5f), Random.Range(-0.1f, 0.25f)), 0.016f, 0.08f, cr, 0, 5, 0.06f);
            }
            _scorch -= dt;
            if (_scorch <= 0f)
            {
                _scorch = 0.07f;
                v.ShidenScorch(new Vector2(p.x, 0.01f), 0.22f, 0.6f);
            }
            v.Spark(p + new Vector2(Random.Range(-0.08f, 0.08f), Random.Range(0f, 0.2f)), new Vector2(-_dir * Random.Range(0.5f, 1.5f), Random.Range(1f, 2.4f)),
                Random.value < 0.3f ? Color.white : Vfx.ShidenVolt, 0.026f, 0.24f);
        }

        /// <summary>The standing crescent at the wave's head: ±70° round its front, local to the projectile (the ribbons ride its transform).</summary>
        void DrawWave(Vector2 p)
        {
            _glow.Begin(); _core.Begin(); _edge.Begin();
            int strike = Mathf.FloorToInt(_t / Vfx.RestrikeSeconds);
            Vector2 o = new(-0.28f * _dir, 0.12f);
            const int seg = 18;
            for (int i = 0; i <= seg; i++)
            {
                float s = i / (float)seg;
                float th = Mathf.Lerp(-70f, 70f, s) * Mathf.Deg2Rad;
                Vector2 d = new(Mathf.Cos(th) * _dir, Mathf.Sin(th) * 1.25f);
                float prof = Mathf.Sin(s * Mathf.PI);
                float r = 0.36f, t = 0.16f * prof;
                Vector2 q = o + d * r;
                Vector2 n = d.normalized;
                Color g = (_crimson ? Color.Lerp(Vfx.ShidenVolt, Vfx.Crimson, 0.45f) : Vfx.ShidenVolt).WithA(0.95f * prof);
                _glow.Add(q + n * (t * 1.1f), q + n * (t * 0.2f), g.WithA(0f), g);
                Color k = Vfx.ShidenUmbra.WithA(0.9f * Mathf.Sqrt(prof));
                _core.Add(q + n * (t * 0.3f), q - n * (t * 0.6f), k, k);
                float j = (Vfx.Hash(i + strike * 29, _seed) - 0.5f) * 0.06f;
                Color e = Vfx.ShidenVoltHot.WithA(prof);
                Vector2 eq = q + n * (t * 0.35f + j);
                _edge.Add(eq + n * 0.008f, eq - n * 0.008f, e, e);
            }
            _glow.End(); _core.End(); _edge.End();
        }
    }
}
