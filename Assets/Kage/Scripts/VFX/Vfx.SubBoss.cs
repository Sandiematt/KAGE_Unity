using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Sub-boss procedural VFX built on the <c>effekseer-vfx</c> skill's craft rules (craft.md, layering.md, timing.md, stylized.md):
    /// <list type="bullet">
    /// <item><b>Time structure</b>: anticipation → short contact flash (2–5 frames) → shape-changing body (sweeps open, erodes, or re-strikes) →
    /// eased secondary particles → slow floor/matter dissipation. Charges invert the structure (inward draw, one release).</item>
    /// <item><b>Value layering</b>: hot core (paper-white, ice-white, silver-white), saturated element body, alpha-blended dark backing/rim so
    /// effects read on bright rooftops and dark districts alike, and low-saturation matter (ink mist, dust, ash, cold fog).</item>
    /// <item><b>Element signatures</b> (each differing in shape, motion, rhythm, material and secondary motion):
    /// Kuroe's Bleach/Ichimonji sumi-e calligraphy ink, Garan's sumo-karate iron shock &amp; ember scars, Hisame's slanted rain &amp; sinking cold mist,
    /// and Shiden's Hōzōin-ryū 40:1 silver-violet spear streaks — each threaded with a sub-boss-scale echo of Raikon's crimson lightning training.</item>
    /// </list>
    /// </summary>
    public partial class Vfx
    {
        // ---- shared sub-boss ink & element tones ----
        public static readonly Color VoidInk = new(0.024f, 0.020f, 0.032f, 1f);     // #060508 deep sumi ink (alpha-blended)
        public static readonly Color WetInk = new(0.11f, 0.095f, 0.145f, 1f);       // #1C1825 lit wet ink sheen
        public static readonly Color PaperHot = new(0.96f, 0.93f, 0.85f, 1f);       // #F5EDE0 hot censor paper edge
        public static readonly Color CensorGold = new(0.788f, 0.643f, 0.361f, 1f);  // #C9A45C Kuroe accent gold

        // =====================================================================================
        //  KUROE — SUMI-E CALLIGRAPHY INK (Ichimonji brush slashes, ink blots, redaction seals)
        // =====================================================================================

        /// <summary>
        /// A sweeping calligraphy brush slash arc (Bleach / Ichimonji sumi-e): a wide alpha-blended void-black stroke with dry-brush bristle
        /// strands that sweeps from <paramref name="startDeg"/> across <paramref name="sweepDeg"/> and erodes from the tail, edged with a thin
        /// paper-gold hot nib line, flinging ink droplets tangentially and leaving an ink stain on the floor. With <paramref name="raikonSpark"/>,
        /// Raikon's crimson static crackles inside the wet ink.
        /// </summary>
        public void InkSlashArc(Vector2 c, float radius, float startDeg, float sweepDeg, float life = 0.34f, bool raikonSpark = false)
        {
            float tipDeg = startDeg + sweepDeg * 0.55f;
            Vector2 contact = c + KMath.Dir(tipDeg) * radius * 0.88f;
            // 2-4 frame contact glint at the brush's apex
            Star(contact, 0.42f, PaperHot, CensorGold, 0.08f, 4, true, Order + 62, 0.45f);

            int seed = Random.Range(1, 1 << 20);
            // Layer 1 (Alpha dark body): broad sumi-e brush sweep + two uneven dry-brush bristle sub-strokes (broken arcs, craft §5.1)
            Dyn("InkArcBody", MeshKit.Alpha, Order + 38, life * 1.25f, false, (rib, u) =>
            {
                float head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.28f));
                float tail = KMath.Apply(Ease.InCubic, Mathf.Clamp01((u - 0.14f) / 0.86f));
                if (head - tail <= 0.005f) return;
                for (int bristle = 0; bristle < 3; bristle++)
                {
                    rib.Break();
                    float rOff = bristle == 0 ? 0f : bristle == 1 ? 0.11f : -0.09f;
                    float wBase = bristle == 0 ? 0.16f : 0.045f;
                    float hLim = bristle == 0 ? head : Mathf.Clamp01(head * (bristle == 1 ? 1.06f : 0.92f));
                    const int seg = 22;
                    for (int i = 0; i <= seg; i++)
                    {
                        float s = Mathf.Lerp(tail, hLim, i / (float)seg);
                        float th = (startDeg + sweepDeg * s) * Mathf.Deg2Rad;
                        Vector2 d = new(Mathf.Cos(th), Mathf.Sin(th));
                        float prof = Mathf.Sin(Mathf.Clamp01((s - tail) / Mathf.Max(0.001f, hLim - tail)) * Mathf.PI);
                        float jag = 1f + 0.18f * Mathf.Sin((s * 19f + bristle * 2.3f + seed * 0.01f));
                        float w = wBase * prof * jag * (1f - u * 0.45f);
                        float rr = radius + rOff;
                        Color col = (bristle == 0 ? VoidInk : WetInk).WithA((bristle == 0 ? 0.94f : 0.78f) * (1f - u * 0.75f));
                        rib.Add(c + d * (rr + w), c + d * Mathf.Max(0.05f, rr - w), col, col);
                    }
                }
            });

            // Layer 2 (AddHDR core edge): razor-thin paper-white & censor-gold leading edge that dies before the ink body (decay rule, craft §5.5)
            Dyn("InkArcCore", AddHDR(2.2f), Order + 42, life * 0.68f, true, (rib, u) =>
            {
                float head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.32f));
                float tail = KMath.Apply(Ease.InQuad, Mathf.Clamp01((u - 0.06f) / 0.94f));
                if (head - tail <= 0.005f) return;
                const int seg = 18;
                for (int i = 0; i <= seg; i++)
                {
                    float s = Mathf.Lerp(tail, head, i / (float)seg);
                    float th = (startDeg + sweepDeg * s) * Mathf.Deg2Rad;
                    Vector2 d = new(Mathf.Cos(th), Mathf.Sin(th));
                    float prof = Mathf.Sin(Mathf.Clamp01((s - tail) / Mathf.Max(0.001f, head - tail)) * Mathf.PI);
                    float w = 0.028f * prof * (1f - u * 0.6f);
                    float rr = radius + 0.09f;
                    Color core = Color.Lerp(PaperHot, CensorGold, u).WithA(0.9f * (1f - u));
                    rib.Add(c + d * (rr + w), c + d * rr, core.WithA(0f), core);
                }
            });

            // Layer 3 (Secondary): flung sumi ink drops along the brush's tangent that hang and fall
            float sign = Mathf.Sign(sweepDeg);
            for (int i = 0; i < 12; i++)
            {
                float s = Random.Range(0.2f, 0.95f);
                float deg = startDeg + sweepDeg * s;
                Vector2 p = c + KMath.Dir(deg) * (radius + Random.Range(-0.08f, 0.12f));
                Vector2 tang = KMath.Dir(deg + sign * 82f) * Random.Range(2.2f, 5.8f);
                Emit(_dust, p, tang + Vector2.down * 0.6f, Random.Range(0.09f, 0.2f), Random.Range(0.32f, 0.62f), VoidInk.WithA(0.9f));
                if (i % 3 == 0)
                    Emit(_sparks, p, tang * 1.15f, 0.024f, Random.Range(0.18f, 0.32f), i == 0 ? PaperHot : CensorGold);
            }

            // Raikon's training: short crimson arcs snapping along the wet ink curve
            if (raikonSpark)
            {
                Vector2 a0 = c + KMath.Dir(startDeg + sweepDeg * 0.25f) * radius;
                Vector2 a1 = c + KMath.Dir(startDeg + sweepDeg * 0.8f) * radius;
                Bolt(a0, a1, 0.028f, 0.24f, Crimson, 8, 0.09f, 1);
            }
        }

        /// <summary>
        /// An Ichimonji ("Single Character") straight calligraphy stroke from <paramref name="a"/> to <paramref name="b"/>: heavy black ink
        /// laid down with brush pressure, dry-brush split tails, paper-gold hot spine, ink splatters and floor blot residue.
        /// </summary>
        public void InkBrushStroke(Vector2 a, Vector2 b, float width = 0.14f, float life = 0.38f, bool raikonSpark = false)
        {
            Vector2 d = b - a;
            Vector2 n = d.sqrMagnitude > 1e-6f ? new Vector2(-d.y, d.x).normalized : Vector2.up;
            Star(b, 0.45f, PaperHot, CensorGold, 0.08f, 4, true, Order + 62, 0.4f);

            Dyn("InkStroke", MeshKit.Alpha, Order + 39, life * 1.35f, false, (rib, u) =>
            {
                float head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.24f));
                float tail = KMath.Apply(Ease.InCubic, Mathf.Clamp01((u - 0.22f) / 0.78f));
                if (head - tail <= 0.005f) return;
                const int seg = 18;
                for (int i = 0; i <= seg; i++)
                {
                    float s = Mathf.Lerp(tail, head, i / (float)seg);
                    // Calligraphic pressure: heavy press at entry (ton), firm body, sharp flick at exit (harai)
                    float press = Mathf.Pow(1f - s * 0.75f, 0.6f) * Mathf.Sin(Mathf.Clamp01((s - tail) / Mathf.Max(0.001f, head - tail)) * Mathf.PI);
                    float w = width * press * (1f - u * 0.35f);
                    Vector2 p = a + d * s + n * (Mathf.Sin(s * 9f) * 0.018f);
                    Color col = VoidInk.WithA(0.95f * (1f - u * 0.7f));
                    rib.Add(p + n * w, p - n * w, col, col);
                }
            });

            // Hot gold-ivory spine line (40:1 slice feel, craft §5.6)
            Dyn("InkStrokeCore", AddHDR(2.3f), Order + 43, life * 0.6f, true, (rib, u) =>
            {
                float head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.22f));
                float tail = KMath.Apply(Ease.InQuad, u);
                if (head <= tail) return;
                Vector2 p0 = a + d * tail, p1 = a + d * head;
                float w = width * 0.18f * (1f - u);
                Color c0 = PaperHot.WithA(0.9f * (1f - u)), c1 = CensorGold.WithA(0f);
                rib.Add(p0 + n * w, p0 - n * w, c1, c0);
                rib.Add(p1 + n * (w * 0.3f), p1 - n * (w * 0.3f), c1, c0);
            });

            Vector2 dir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.right;
            for (int i = 0; i < 10; i++)
            {
                Vector2 at = Vector2.Lerp(a, b, Random.Range(0.15f, 1f));
                Vector2 vel = dir * Random.Range(2f, 6f) + n * Random.Range(-1.6f, 1.6f);
                Emit(_dust, at, vel, Random.Range(0.08f, 0.18f), Random.Range(0.3f, 0.6f), VoidInk.WithA(0.9f));
            }
            InkPool(new Vector2(b.x, 0f), 0.42f, 0.85f);
            if (raikonSpark) Bolt(a, b, 0.026f, 0.24f, Crimson, 9, 0.07f, 1);
        }

        /// <summary>A dark sumi ink pool on the floor that spreads fast, holds its glossy gold rim briefly, and dries away.</summary>
        public void InkPool(Vector2 ground, float halfWidth = 0.45f, float life = 0.95f)
        {
            Vector2 p = new(ground.x, Mathf.Max(0f, ground.y));
            Dyn("InkPool", MeshKit.Alpha, Order - 26, life, false, (rib, u) =>
            {
                float spread = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.18f));
                float hw = halfWidth * Mathf.Lerp(0.45f, 1f, spread);
                float a = 0.85f * Mathf.Pow(1f - u, 0.65f);
                const int seg = 18;
                for (int i = 0; i <= seg; i++)
                {
                    float t = i / (float)seg * Mathf.PI;
                    float wob = 1f + 0.14f * Mathf.Sin(t * 5f);
                    float x = Mathf.Cos(t) * hw * wob, y = Mathf.Sin(t) * hw * 0.13f * wob;
                    rib.Add(p + new Vector2(x, y + 0.012f), p + new Vector2(x, -y + 0.012f), VoidInk.WithA(a), VoidInk.WithA(a));
                }
            });
            Ring(p + Vector2.up * 0.02f, halfWidth * 0.15f, halfWidth * 0.85f, 0.035f, CensorGold, life * 0.35f, false, 0.16f, 1.5f, Order - 22);
        }

        /// <summary>Sumi ink splatter burst on brush impact or Censure block-turn: dark ink drops, paper flash, gold shock ring.</summary>
        public void InkSplatter(Vector2 p, Vector2 dir, float scale = 1f, bool raikonSpark = false)
        {
            Star(p, 0.44f * scale, PaperHot, CensorGold, 0.09f, 6, true, Order + 62);
            Ring(p, 0.06f, 0.65f * scale, 0.055f, CensorGold, 0.26f, true, 1f, 1.9f);
            float baseAng = dir.sqrMagnitude > 1e-6f ? KMath.Ang(dir) : 0f;
            for (int i = 0; i < 14; i++)
            {
                float ang = baseAng + Random.Range(-65f, 65f) + (i % 4 == 0 ? 180f : 0f);
                Vector2 v = KMath.Dir(ang) * Random.Range(2.2f, 6.2f) * scale;
                Emit(_dust, p + Random.insideUnitCircle * 0.08f, v, Random.Range(0.11f, 0.24f) * scale, Random.Range(0.35f, 0.68f), VoidInk.WithA(0.92f));
                if (i % 3 == 0) Emit(_sparks, p, v * 1.2f, 0.026f, Random.Range(0.2f, 0.38f), i == 0 ? PaperHot : CensorGold);
            }
            if (raikonSpark) Crackle(p, 0.32f * scale, 0.32f * scale, 2, Crimson);
        }

        /// <summary>Telegraph of Kuroe's Phase 2 ink-lightning line: a wet black brush line on the floor with dim crimson sparks crawling in it.</summary>
        public void InkSparkLineMark(Vector2 a, Vector2 b, float seconds)
        {
            if (seconds <= 0f) return;
            Dyn("InkLineMark", MeshKit.Alpha, Order - 14, seconds, false, (rib, u) =>
            {
                float grow = KMath.Apply(Ease.OutQuad, Mathf.Clamp01(u / 0.35f));
                Vector2 end = Vector2.Lerp(a, b, grow);
                float w = 0.055f * (0.7f + 0.3f * u);
                Color col = VoidInk.WithA(0.75f + 0.2f * u);
                rib.Add(a + new Vector2(0f, 0.02f + w * 0.3f), a + new Vector2(0f, 0.02f - w * 0.3f), col, col);
                rib.Add(end + new Vector2(0f, 0.02f + w * 0.3f), end + new Vector2(0f, 0.02f - w * 0.3f), col, col);
            });
            SparkLineMark(a, b, seconds);
        }

        /// <summary>Strike of Kuroe's ink-lightning floor line: upward black ink spikes laced with short Raikon crimson bolts.</summary>
        public void InkSparkLineStrike(Vector2 a, Vector2 b)
        {
            float len = (b - a).magnitude;
            int spikes = Mathf.Max(3, Mathf.CeilToInt(len / 0.38f));
            Dyn("InkSpikes", MeshKit.Alpha, Order + 36, 0.34f, false, (rib, u) =>
            {
                float rise = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.22f));
                float erode = KMath.Apply(Ease.InQuad, u);
                for (int i = 0; i <= spikes; i++)
                {
                    Vector2 baseP = Vector2.Lerp(a, b, i / (float)spikes);
                    float h = (0.65f + 0.35f * ((i * 37) % 3) / 2f) * rise * (1f - erode * 0.55f);
                    float hw = 0.075f * (1f - erode);
                    if (hw <= 0.002f) continue;
                    Color c = VoidInk.WithA(0.92f * (1f - erode));
                    rib.AddTriangle(baseP + Vector2.left * hw, baseP + Vector2.right * hw, baseP + new Vector2((i % 2 == 0 ? 0.04f : -0.04f), h), c, c, c);
                }
            });
            for (int i = 0; i <= 2; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / 2f);
                Bolt(p + new Vector2(Random.Range(-0.08f, 0.08f), Random.Range(0.55f, 0.95f)), p, 0.028f, 0.24f, Crimson, 6, 0.05f, 1);
            }
            InkPool((a + b) * 0.5f, len * 0.5f + 0.15f, 0.9f);
            GameFeel.Shake(0.18f);
        }

        /// <summary>Strike of Kuroe's Thunder Redaction seal: a sub-boss crimson-gold bolt (~3.4 m) into an erupting black ink crown.</summary>
        public void InkThunderSealStrike(Vector2 ground, float halfWidth = 0.55f)
        {
            Vector2 top = ground + new Vector2(Random.Range(-0.2f, 0.2f), 3.6f);
            Bolt(top, ground, 0.048f, 0.34f, Crimson, 12, 0.14f, 2);
            InkSparkLineStrike(ground - Vector2.right * halfWidth, ground + Vector2.right * halfWidth);
            Star(ground + Vector2.up * 0.22f, 0.68f, PaperHot, Crimson, 0.09f, 6, true, Order + 63);
            Ring(ground + Vector2.up * 0.03f, 0.08f, halfWidth * 1.5f, 0.065f, CensorGold, 0.3f, false, 0.2f, 2f);
        }

        // =====================================================================================
        //  GARAN — IRON SHOCK, SUMO STOMPS & BROKEN VANGUARD LIGHTNING
        // =====================================================================================

        /// <summary>
        /// Garan's sumo-karate ground slam / shockwave: ember-white contact star, flattened double shock ellipse, glowing storm-scar ground cracks,
        /// directional gravel/iron sparks, and (from Raikon's vanguard training) crawling crimson-ember floor arcs.
        /// </summary>
        public void IronStormSlam(Vector2 ground, float halfWidth, Color stormCol, bool raikonArcs = true)
        {
            Vector2 p = new(ground.x, Mathf.Max(0.02f, ground.y));
            Star(p + Vector2.up * 0.14f, 0.62f, HotWhite, stormCol, 0.09f, 6, true, Order + 60);
            Ring(p, 0.08f, Mathf.Max(0.7f, halfWidth * 1.15f), 0.085f, stormCol, 0.34f, false, 0.18f, 2.1f);
            Ring(p, 0.04f, Mathf.Max(0.42f, halfWidth * 0.65f), 0.045f, HotWhite, 0.2f, false, 0.18f, 1.7f);
            GroundCracks(p.x, 5, 0.32f, Mathf.Max(0.5f, halfWidth * 0.75f), stormCol, HotWhite, 0.75f);
            Dust(p, Mathf.Clamp(halfWidth * 0.85f, 0.8f, 1.35f), 0f);
            for (int i = 0; i < 12; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Vector2 v = new(side * Random.Range(1.2f, 4.5f), Random.Range(2.0f, 5.2f));
                Emit(_sparks, p + new Vector2(Random.Range(-halfWidth * 0.5f, halfWidth * 0.5f), 0.03f), v, Random.Range(0.03f, 0.055f), Random.Range(0.28f, 0.52f),
                    i % 3 == 0 ? Color.white : stormCol);
            }
            if (raikonArcs)
            {
                Bolt(p - Vector2.right * halfWidth * 0.7f + Vector2.up * 0.04f, p + Vector2.right * halfWidth * 0.7f + Vector2.up * 0.04f,
                    0.026f, 0.24f, Crimson, 8, 0.06f, 1);
            }
            Scorch(p, Mathf.Max(0.4f, halfWidth * 0.65f), 0.75f);
        }

        /// <summary>Vertical air-compression ring perpendicular to a Harite / Tsuppari palm strike, plus forward speed-shards.</summary>
        public void PalmShockRing(Vector2 palm, float facing, Color stormCol, bool heavy = false)
        {
            float scale = heavy ? 1.25f : 0.82f;
            Star(palm, 0.38f * scale, Color.white, stormCol, 0.08f, 6, true, Order + 61);
            // Vertical ellipse (squashed in x, tall in y) so the compression wave faces the thrust
            Dyn("PalmWave", AddHDR(2.1f), Order + 26, heavy ? 0.26f : 0.18f, true, (rib, u) =>
            {
                float r = Mathf.Lerp(0.08f, 0.48f * scale, KMath.Apply(Ease.OutExpo, u));
                float w = 0.045f * scale * (1f - KMath.Apply(Ease.InQuad, u));
                Color cc = stormCol.WithA(0.9f * (1f - u));
                const int seg = 28;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    Vector2 d = new(Mathf.Cos(a) * 0.34f, Mathf.Sin(a));
                    Vector2 c = palm + Vector2.right * (facing * u * 0.22f);
                    rib.Add(c + d * (r + w), c + d * Mathf.Max(0f, r - w), cc.WithA(0f), cc);
                }
            });
            Shards(palm, new Vector2(facing, 0.05f), heavy ? 8 : 5, 0.55f * scale, 0.022f, Color.white, stormCol, 0.2f, 32f);
            if (heavy)
            {
                Bolt(palm, palm + new Vector2(facing * 0.85f, Random.Range(-0.12f, 0.12f)), 0.032f, 0.22f, Crimson, 7, 0.08f, 1);
            }
        }

        /// <summary>
        /// Kyokushin <i>Seiken</i> kime (straight body punch): a 3-frame white-amber star at the knuckles, a horizontal compression streak
        /// gone before the shards land, a tight forward shard cone and one iron-ash puff pushed back off the elbow. Straight, short and
        /// horizontal, so it reads apart from the Harite's vertical ring and the Gedan kick's low arc (craft §3: shape, motion, rhythm).
        /// </summary>
        public void SeikenKime(Vector2 fist, float facing, Color stormCol)
        {
            float ang = facing >= 0f ? 0f : 180f;
            Star(fist, 0.34f, HotWhite, stormCol, 0.06f, 4, true, Order + 61, 0.45f);
            LensStreak(fist + Vector2.right * (facing * 0.22f), 0.8f, stormCol, 0.12f, ang, 0.02f);
            Shards(fist, new Vector2(facing, 0.04f), 4, 0.42f, 0.02f, Color.white, stormCol, 0.15f, 16f);
            Emit(_dust, fist - Vector2.right * (facing * 0.18f), new Vector2(-facing * 0.5f, 0.35f), 0.14f, 0.38f, GaranPalette.IronEdge.WithA(0.55f));
        }

        /// <summary>
        /// Kyokushin <i>Gedan Mawashi Geri</i> (low roundhouse to the shin): a flattened shin-height arc that whips open from behind the hips
        /// to low in front (dark iron rim, storm-ember body, hot core that dies first), a contact star where the shin lands, floor dust
        /// kicked forward and ember sparks that arc and fall.
        /// </summary>
        public void GedanShinArc(Vector2 ground, float facing, Color stormCol)
        {
            Vector2 g = new(ground.x, Mathf.Max(0f, ground.y));
            Vector2 c = g + Vector2.up * 0.42f;
            float start = facing >= 0f ? 180f : 0f;       // behind the hips
            float sweep = facing >= 0f ? 160f : -160f;    // down through the floor line to low in front
            const float rx = 0.82f, ry = 0.3f;

            void Pass(Ribbon rib, float u, float w0, Color col, float fade)
            {
                float head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.3f));
                float tail = KMath.Apply(Ease.InCubic, Mathf.Clamp01((u - 0.08f) / 0.92f));
                if (head - tail <= 0.005f) return;
                const int seg = 20;
                for (int i = 0; i <= seg; i++)
                {
                    float s = Mathf.Lerp(tail, head, i / (float)seg);
                    float th = (start + sweep * s) * Mathf.Deg2Rad;
                    float cs = Mathf.Cos(th), sn = Mathf.Sin(th);
                    Vector2 mid = c + new Vector2(cs * rx, sn * ry);
                    Vector2 nrm = new Vector2(cs * ry, sn * rx).normalized;    // the ellipse's outward normal
                    float prof = Mathf.Sin(Mathf.Clamp01((s - tail) / Mathf.Max(0.001f, head - tail)) * Mathf.PI);
                    float w = w0 * prof * (1f - u * 0.5f);
                    Color cc = col.WithA(col.a * (1f - u * fade));
                    rib.Add(mid + nrm * w, mid - nrm * (w * 0.5f), cc.WithA(0f), cc);
                }
            }
            Dyn("GedanRim", MeshKit.Alpha, Order + 35, 0.28f, false, (rib, u) => Pass(rib, u, 0.11f, GaranPalette.IronFar.WithA(0.8f), 0.7f));
            Dyn("GedanBody", AddHDR(2.0f), Order + 39, 0.22f, true, (rib, u) => Pass(rib, u, 0.065f, stormCol, 0.7f));
            Dyn("GedanCore", AddHDR(2.8f), Order + 40, 0.14f, true, (rib, u) => Pass(rib, u, 0.02f, HotWhite, 0.9f));

            float end = (start + sweep) * Mathf.Deg2Rad;
            Vector2 contact = c + new Vector2(Mathf.Cos(end) * rx, Mathf.Sin(end) * ry);
            Star(contact, 0.4f, HotWhite, stormCol, 0.07f, 6, true, Order + 61, 0.6f);
            Dust(new Vector2(contact.x, g.y), 0.7f, facing);
            for (int i = 0; i < 8; i++)
                Emit(_sparks, contact, new Vector2(facing * Random.Range(1.4f, 3.8f), Random.Range(0.6f, 2.4f)), Random.Range(0.025f, 0.04f),
                    Random.Range(0.3f, 0.5f), i % 3 == 0 ? Color.white : stormCol);
        }

        // =====================================================================================
        //  HISAME — SLANTED RAIN, COLD SINKING MIST & TESSENJUTSU FAN WINDS
        // =====================================================================================

        /// <summary>
        /// A Tessenjutsu war-fan wind slash: dark rain-slate alpha backing, crisp rain-blue crescent body, ice-white leading edge,
        /// slanted rain droplets flung forward and sinking cool mist puffs (craft §3: mist sinks, droplets streak). With <paramref name="raikonSpark"/>,
        /// a thin crimson Raikon arc snaps off the iron fan ribs.
        /// </summary>
        public void FanWindCrescent(Vector2 c, float facing, float radius = 0.95f, float tiltDeg = -18f, bool raikonSpark = false)
        {
            float baseDeg = facing >= 0f ? tiltDeg : 180f - tiltDeg;
            float sweep = (facing >= 0f ? 1f : -1f) * 128f;
            float start = baseDeg - sweep * 0.5f;
            Color rainBlue = KMath.Hex("#7FB8FF"), iceCore = KMath.Hex("#ECFAFF"), rainDark = KMath.Hex("#09131E");

            Star(c + KMath.Dir(baseDeg) * radius * 0.9f, 0.36f, iceCore, rainBlue, 0.08f, 4, true, Order + 62, 0.38f);

            void ArcPass(Ribbon rib, float u, float wMul, Color col, bool corePass)
            {
                float head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.26f));
                float tail = KMath.Apply(Ease.InCubic, Mathf.Clamp01((u - 0.1f) / 0.9f));
                if (head - tail <= 0.005f) return;
                const int seg = 20;
                for (int i = 0; i <= seg; i++)
                {
                    float s = Mathf.Lerp(tail, head, i / (float)seg);
                    float th = (start + sweep * s) * Mathf.Deg2Rad;
                    Vector2 d = new(Mathf.Cos(th), Mathf.Sin(th));
                    float prof = Mathf.Sin(Mathf.Clamp01((s - tail) / Mathf.Max(0.001f, head - tail)) * Mathf.PI);
                    float w = 0.085f * wMul * prof * (1f - u * 0.55f);
                    Color cc = col.WithA(col.a * (1f - u * (corePass ? 0.85f : 0.65f)));
                    rib.Add(c + d * (radius + w), c + d * (radius - w * 0.6f), cc.WithA(0f), cc);
                }
            }
            Dyn("FanRim", MeshKit.Alpha, Order + 36, 0.3f, false, (rib, u) => ArcPass(rib, u, 1.45f, rainDark.WithA(0.75f), false));
            Dyn("FanBody", AddHDR(2.1f), Order + 40, 0.25f, true, (rib, u) => ArcPass(rib, u, 0.9f, rainBlue, false));
            Dyn("FanCore", AddHDR(2.9f), Order + 41, 0.18f, true, (rib, u) => ArcPass(rib, u, 0.28f, iceCore, true));

            // Secondary: fast slanted rain needles + sinking cool mist
            for (int i = 0; i < 9; i++)
            {
                float s = Random.Range(0.15f, 0.9f);
                Vector2 p = c + KMath.Dir(start + sweep * s) * radius;
                Emit(_sparks, p, new Vector2(facing * Random.Range(3.5f, 7.5f), Random.Range(-2.2f, 1.4f)), 0.022f, Random.Range(0.16f, 0.3f), i % 3 == 0 ? iceCore : rainBlue);
            }
            for (int i = 0; i < 3; i++)
            {
                Vector2 p = c + KMath.Dir(start + sweep * Random.Range(0.2f, 0.8f)) * (radius * 0.85f);
                Emit(_dust, p, new Vector2(facing * Random.Range(0.4f, 1.2f), Random.Range(-0.45f, -0.1f)), Random.Range(0.18f, 0.3f), Random.Range(0.4f, 0.65f),
                    HisamePalette.Smoke.WithA(0.48f));
            }
            if (raikonSpark)
            {
                Vector2 a0 = c + KMath.Dir(start + sweep * 0.3f) * radius;
                Vector2 a1 = c + KMath.Dir(start + sweep * 0.75f) * radius;
                Arc(a0, a1, 0.14f, 0.016f, Crimson);
            }
        }

        /// <summary>Diagonal rain-needle curtain falling through <paramref name="centre"/> with tiny floor splash rings.</summary>
        public void RainBurst(Vector2 centre, float facing, int needles = 8)
        {
            Color rainBlue = KMath.Hex("#7FB8FF"), pale = KMath.Hex("#CFE9FF");
            for (int i = 0; i < needles; i++)
            {
                Vector2 p = centre + new Vector2(Random.Range(-0.55f, 0.55f), Random.Range(0.4f, 1.6f));
                Vector2 v = new(facing * Random.Range(1.8f, 3.4f), Random.Range(-7.5f, -4.5f));
                Emit(_sparks, p, v, 0.018f, Random.Range(0.16f, 0.26f), i % 2 == 0 ? pale : rainBlue);
            }
            Ring(new Vector2(centre.x + Random.Range(-0.25f, 0.25f), 0.02f), 0.02f, 0.24f, 0.018f, pale.WithA(0.65f), 0.22f, false, 0.2f, 1.5f);
        }

        /// <summary>
        /// Tessenjutsu <i>Atemi</i> (closed-fan point strike): a 3-frame ice star at the fan's rib tip, a needle-thin rain-blue jab line,
        /// five slanted rain needles thrown forward and one cold mist puff that sinks — a pinpoint, unlike the open-fan crescent.
        /// </summary>
        public void FanAtemiNeedle(Vector2 tip, float facing)
        {
            Color rainBlue = HisamePalette.Accent, iceCore = KMath.Hex("#ECFAFF");
            Star(tip, 0.3f, iceCore, rainBlue, 0.06f, 4, true, Order + 62, 0.35f);
            LensStreak(tip - Vector2.right * (facing * 0.15f), 0.6f, rainBlue, 0.11f, facing >= 0f ? -6f : 186f, 0.012f);
            for (int i = 0; i < 5; i++)
                Emit(_sparks, tip, new Vector2(facing * Random.Range(3.8f, 6.5f), Random.Range(-1.8f, 0.4f)), 0.016f, Random.Range(0.14f, 0.24f),
                    i == 0 ? iceCore : rainBlue);
            Emit(_dust, tip, new Vector2(facing * 0.3f, -0.4f), 0.16f, 0.5f, HisamePalette.Smoke.WithA(0.42f));
        }

        /// <summary>Craft-layered smoke &amp; rain-mist screen: dark slate inner volume, mid rain-smoke puffs, pale rim wisps, optional static.</summary>
        public void MistScreenBurst(Vector2 pos, float scale = 1f, bool electrified = false)
        {
            Vector2 ground = new(pos.x, Mathf.Max(0.05f, pos.y));
            Star(ground + Vector2.up * 0.18f, 0.48f * scale, Color.white, HisamePalette.Pale, 0.08f, 6, true, Order + 58);
            Ring(new Vector2(ground.x, 0.03f), 0.08f, 1.05f * scale, 0.06f, HisamePalette.Pale.WithA(0.75f), 0.32f, false, 0.2f, 1.8f);
            for (int i = 0; i < 16; i++)
            {
                float k = (i - 7.5f) / 7.5f, up = 1f - Mathf.Abs(k);
                Color col = i % 4 == 0 ? HisamePalette.RainDeep.WithA(0.82f) : i % 2 == 0 ? HisamePalette.SmokeDark.WithA(0.75f) : HisamePalette.Smoke.WithA(0.68f);
                Emit(_dust, ground + new Vector2(k * 0.48f * scale, 0.1f + up * 0.22f),
                    new Vector2(k * 1.35f * scale, (0.35f + up * 0.85f) * scale), (0.38f + up * 0.26f) * scale, Random.Range(0.7f, 1.05f), col);
            }
            RainBurst(ground + Vector2.up * 0.5f, 1f, 6);
            if (electrified) Crackle(ground + Vector2.up * 0.45f, 0.55f * scale, 0.4f * scale, 3, Crimson);
        }

        // =====================================================================================
        //  SHIDEN — HŌZŌIN-RYŪ 40:1 SPEAR STREAKS & LIEUTENANT LIGHTNING
        // =====================================================================================

        /// <summary>
        /// Razor-straight Hōzōin-ryū thrust streak from <paramref name="shaftBase"/> past <paramref name="tip"/> (aspect &gt; 40:1, craft §5.6):
        /// dark violet-black backing, saturated violet body, silver-white core, two Mach collar cones, and optional Raikon crimson-violet arc.
        /// </summary>
        public void SpearPierceStreak(Vector2 shaftBase, Vector2 tip, Color violet, bool raikonCrimson = false)
        {
            Vector2 d = tip - shaftBase;
            Vector2 dir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.right;
            Vector2 farTip = tip + dir * 0.45f;
            Star(tip, 0.46f, Color.white, violet, 0.08f, 4, true, Order + 64, 0.32f);
            LensStreak((shaftBase + farTip) * 0.5f, (farTip - shaftBase).magnitude * 1.15f, violet, 0.2f, KMath.Ang(dir), 0.018f);

            // Dark backing behind the thin pierce so it reads on bright skies too (stylized.md)
            Vector2 n = new(-dir.y, dir.x);
            Dyn("SpearPierceRim", MeshKit.Alpha, Order + 44, 0.24f, true, (rib, u) =>
            {
                float tail = KMath.Apply(Ease.InQuad, u);
                Vector2 p0 = Vector2.Lerp(shaftBase, farTip, tail);
                float w = 0.048f * (1f - u);
                Color dk = ShidenPalette.LacquerDeep.WithA(0.75f * (1f - u));
                rib.Add(p0 + n * w, p0 - n * w, dk, dk);
                rib.Add(farTip + n * 0.008f, farTip - n * 0.008f, dk.WithA(0f), dk.WithA(0f));
            });

            // Swept-back Mach ring at the spear's collar
            Ring(Vector2.Lerp(shaftBase, tip, 0.55f), 0.04f, 0.42f, 0.04f, violet, 0.2f, true, 0.38f, 2.1f);
            Shards(tip, dir, 6, 0.65f, 0.018f, Color.white, violet, 0.18f, 18f);
            if (raikonCrimson)
            {
                Bolt(shaftBase, farTip, 0.028f, 0.24f, Color.Lerp(violet, Crimson, 0.55f), 9, 0.075f, 1);
            }
        }

        /// <summary>Sub-boss-scale (~3.5 m) violet-crimson lightning strike for Shiden's Vanguard Break / Raijin Lance.</summary>
        public void LieutenantShockStrike(Vector2 pos, float halfWidth, Color violet)
        {
            Vector2 p = new(pos.x, Mathf.Max(0.02f, pos.y));
            Color blend = Color.Lerp(violet, Crimson, 0.42f);
            Bolt(p + new Vector2(Random.Range(-0.18f, 0.18f), 3.4f), p, 0.046f, 0.32f, blend, 12, 0.13f, 2);
            Star(p + Vector2.up * 0.18f, 0.65f, Color.white, violet, 0.09f, 6, true, Order + 62);
            Ring(p + Vector2.up * 0.03f, 0.08f, Mathf.Max(0.65f, halfWidth * 1.1f), 0.07f, violet, 0.3f, false, 0.2f, 2.1f);
            Bolt(p - Vector2.right * halfWidth * 0.85f + Vector2.up * 0.04f, p + Vector2.right * halfWidth * 0.85f + Vector2.up * 0.04f,
                0.03f, 0.26f, Crimson, 9, 0.06f, 1);
            for (int i = 0; i < 10; i++)
            {
                float ang = Random.Range(25f, 155f);
                Emit(_sparks, p + Vector2.up * 0.04f, KMath.Dir(ang) * Random.Range(2.2f, 5.8f), 0.028f, Random.Range(0.24f, 0.46f),
                    i % 3 == 0 ? Color.white : i % 2 == 0 ? violet : Crimson);
            }
            Scorch(p, Mathf.Max(0.45f, halfWidth * 0.75f), 0.8f);
            Dust(p, Mathf.Clamp(halfWidth * 0.7f, 0.7f, 1.2f), 0f);
        }

        /// <summary>
        /// Hōzōin-ryū <i>Maki-Otoshi</i> bind: the cross-blade winds over the foe's weapon and beats it down — two crossing silver and
        /// violet glint streaks (the jūmonji cross), a tight bind ring, and a shard spray driven down toward the floor.
        /// </summary>
        public void SpearBindCross(Vector2 at, float facing, Color violet)
        {
            Star(at, 0.38f, Color.white, violet, 0.07f, 4, true, Order + 63, 0.4f);
            LensStreak(at, 0.7f, ShidenPalette.SilverHi, 0.16f, 45f, 0.014f);
            LensStreak(at, 0.7f, violet, 0.16f, -45f, 0.014f);
            Ring(at, 0.04f, 0.32f, 0.03f, violet, 0.14f, true, 1f, 2f);
            Shards(at, new Vector2(facing * 0.35f, -1f), 5, 0.4f, 0.018f, Color.white, violet, 0.16f, 28f);
        }

        /// <summary>
        /// Sōjutsu <i>Suriage</i> low sweep: a thin silver-violet streak skimming the floor at shin height from behind to in front, floor dust
        /// thrown forward and sparks scraped off the ground at the far end.
        /// </summary>
        public void SpearLowSweep(Vector2 ground, float facing, Color violet)
        {
            Vector2 g = new(ground.x, Mathf.Max(0f, ground.y));
            Vector2 a = g + new Vector2(-facing * 0.35f, 0.24f), b = g + new Vector2(facing * 1.55f, 0.14f);
            SlashLine(a, b, violet, Color.white, 0.22f, 0.05f);
            Star(b, 0.32f, Color.white, violet, 0.06f, 4, true, Order + 62, 0.4f);
            Dust(new Vector2(b.x - facing * 0.4f, g.y), 0.6f, facing);
            for (int i = 0; i < 6; i++)
                Emit(_sparks, new Vector2(b.x, g.y + 0.03f), new Vector2(facing * Random.Range(1.5f, 4f), Random.Range(0.8f, 2.2f)), 0.024f,
                    Random.Range(0.22f, 0.38f), i % 2 == 0 ? Color.white : violet);
        }
    }
}
