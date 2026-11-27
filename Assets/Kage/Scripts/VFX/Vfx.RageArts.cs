using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The weapon rage arts' and weapon styles' effects (docs/weapon-styles.md), on the effekseer-vfx craft rules as the rest of
    /// <see cref="Vfx"/>: a time structure (contact, a body that changes shape, secondary motion, dissipation), value layering (a
    /// white-hot core, a saturated body, a dark rim or residue in alpha blend), eased travel, and elements that differ in shape,
    /// motion and rhythm rather than colour:
    /// <list type="bullet">
    /// <item><b>Violet</b> (Shizuka): long thin lines and lens cuts that open and thin from the tips; stillness, petals that hang.</item>
    /// <item><b>Lightning</b> (Tsubame): the jagged <see cref="Bolt"/> that doesn't travel, re-struck; afterimages.</item>
    /// <item><b>Hell fire</b> (Onikiri): the floor splitting, cracks that cool from white to red to dark, columns that rise and collapse.</item>
    /// <item><b>Water</b> (Mizuchi): a ribbon body with a head (the dragon), droplets under gravity, a sheet on the floor, falling rain.</item>
    /// <item><b>Shadow</b> (Yamigama): alpha-black bodies with a violet edge, shadow clones, chains, eyes in the dark, ash.</item>
    /// </list>
    /// Not render-reviewed: this project has no Effekseer runtime and no Godot render (docs/weapon-styles.md, "Not verified").
    /// </summary>
    public partial class Vfx
    {
        static readonly Color InkViolet = new(0.05f, 0.02f, 0.09f, 1f);

        // =====================================================================================
        //  THE ART'S SKY (the JS drawSky): the arena takes the art's deep colour behind the fighters
        // =====================================================================================
        MeshRenderer _sky;
        Camera _skyCam;
        Color _skyCol;
        float _skyNow, _skyTarget;
        MaterialPropertyBlock _skyMpb;          // made on first use: a MonoBehaviour can't create one in a field initializer
        /// <summary>Sorted over the arena's backdrop and floor (-1100 and below) and under every fighter (1000 and up).</summary>
        public const int SkyOrder = -900;

        /// <summary>How strong the art's sky is now, 0..1.</summary>
        public float SkyGradeAmount => _skyNow;

        /// <summary>Eases the arena behind the fighters towards <paramref name="col"/> at <paramref name="amount"/> (0..1); 0 gives it back.</summary>
        public void SkyGrade(Color col, float amount, Camera cam = null)
        {
            if (cam != null) _skyCam = cam;                 // the fight's own camera (the review harness's isn't tagged MainCamera)
            if (amount > 0f) _skyCol = col;              // letting go fades out in the art's own colour
            _skyTarget = Mathf.Clamp01(amount);
            if (_skyTarget > 0f) EnsureSky();
        }

        void EnsureSky()
        {
            if (_sky == null)
            {
                var go = new GameObject("ArtSky");
                go.transform.SetParent(transform, false);
                _sky = MeshKit.MakeRenderer("ArtSkyMesh", go.transform, MeshKit.Quad(1f, 1f, Color.white), MeshKit.Alpha, SkyOrder);
                _sky.enabled = false;
            }
        }

        void TickSky(float dt)
        {
            bool impact = GameFeel.ImpactVisible;
            if (impact) EnsureSky();
            if (_sky == null) return;
            float rate = _skyTarget > _skyNow ? 6f : 3.5f;          // in fast, out slower (JS damp 6 / 3.5)
            _skyNow = Mathf.MoveTowards(_skyNow, _skyTarget, rate * dt);
            bool on = impact || _skyNow > 0.002f;
            _sky.enabled = on;
            if (!on) return;
            var cam = _skyCam != null ? _skyCam : Camera.main;
            if (cam != null)
            {
                float h = cam.orthographic ? cam.orthographicSize * 2f + 2f : 40f, w = h * Mathf.Max(1f, cam.aspect) + 2f;
                _sky.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);
                _sky.transform.localScale = new Vector3(w, h, 1f);
            }
            _skyMpb ??= new MaterialPropertyBlock();
            Color tint = impact ? GameFeel.impactTint : _skyCol.WithA(0.62f * _skyNow);
            _skyMpb.SetVector("_Color", tint);
            _sky.SetPropertyBlock(_skyMpb);
        }

        // =====================================================================================
        //  SHARED STROKES
        // =====================================================================================
        /// <summary>A centre-bright strip from a to b between path fractions [from, to], half-width w with a sine profile (both halves meet at the core).</summary>
        static void Strip(Ribbon rib, Vector2 a, Vector2 b, float from, float to, float w, Color edge, Color centre, int seg = 18, float bow = 0f)
        {
            if (to <= from) return;
            Vector2 d = b - a;
            Vector2 n = new Vector2(-d.y, d.x).normalized;
            for (int half = 0; half < 2; half++)
            {
                rib.Break();
                for (int i = 0; i <= seg; i++)
                {
                    float s = Mathf.Lerp(from, to, i / (float)seg);
                    float prof = Mathf.Sin(Mathf.Clamp01((s - from) / (to - from)) * Mathf.PI);
                    Vector2 c = a + d * s + n * (Mathf.Sin(s * Mathf.PI) * bow);
                    float ww = w * prof;
                    if (half == 0) rib.Add(c + n * ww, c, edge, centre);
                    else rib.Add(c, c - n * ww, centre, edge);
                }
            }
        }

        // =====================================================================================
        //  VIOLET (Shizuka)
        // =====================================================================================
        /// <summary>
        /// The one long thin line (craft §5.6): the path of a draw nobody saw, drawn across in a few frames, held, then thinning from
        /// where it started. A soft body, a white-hot core, a dark residue under it that outlives the light.
        /// </summary>
        public void ArtLine(Vector2 a, Vector2 b, Color body, Color hot, float life = 0.9f, float width = 0.03f)
        {
            if ((b - a).sqrMagnitude < 1e-4f) return;
            Dyn("ArtLine", AddHDR(2.6f), Order + 52, life, true, (rib, u) =>
            {
                float head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.07f));
                float tail = KMath.Apply(Ease.InCubic, Mathf.Clamp01((u - 0.3f) / 0.7f));
                float w = width * (1f - 0.75f * KMath.Apply(Ease.InQuad, u));
                Strip(rib, a, b, tail, head, w * 3.2f, body.WithA(0f), body.WithA(0.55f * (1f - u)), 24);
                Strip(rib, a, b, tail, head, w, hot.WithA(0f), hot.WithA(1f - u * 0.7f), 24);
            });
            Dyn("ArtLineResidue", MeshKit.Alpha, Order + 44, life * 1.5f, true, (rib, u) =>
            {
                float tail = KMath.Apply(Ease.InQuad, u), head = Mathf.Clamp01(u / 0.07f);
                Strip(rib, a, b, tail, head, width * 2.4f, InkViolet.WithA(0f), InkViolet.WithA(0.5f * (1f - u)), 16);
            });
        }

        /// <summary>
        /// A lens cut (the JS violet mark): a thin lens of light at <paramref name="angleDeg"/> that opens from its centre to its tips in the
        /// first fifth of its life, then thins away; a dark backing a little wider behind it, a white core inside.
        /// </summary>
        public void LensCut(Vector2 c, float angleDeg, float scale, Color body, Color hot, float life = 0.55f)
        {
            Vector2 d = KMath.Dir(angleDeg) * (0.72f * scale);
            Vector2 a = c - d, b = c + d;
            Dyn("LensBack", MeshKit.Alpha, Order + 46, life, true, (rib, u) =>
            {
                float open = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.2f));
                Strip(rib, a, b, 0.5f - 0.5f * open, 0.5f + 0.5f * open, 0.1f * scale * (1f - u), InkViolet.WithA(0f), InkViolet.WithA(0.6f * (1f - u)), 14);
            });
            Dyn("LensCut", AddHDR(2.8f), Order + 51, life, true, (rib, u) =>
            {
                float open = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.2f));
                float w = 0.07f * scale * (1f - KMath.Apply(Ease.InQuad, u));
                float lo = 0.5f - 0.5f * open, hi = 0.5f + 0.5f * open;
                Strip(rib, a, b, lo, hi, w, body.WithA(0f), body.WithA(0.9f * (1f - u)), 14);
                Strip(rib, a, b, lo + 0.08f, hi - 0.08f, w * 0.32f, hot.WithA(0f), hot.WithA(1f - u * 0.5f), 14);
            });
            Star(c, 0.18f * scale, hot, body, 0.08f, 4, true, Order + 60, 0.4f);
        }

        /// <summary>Shizuka's silent cut opening on a foe: a lens cut across the body, a contact flash, violet sparks that hang, petals of light.</summary>
        public void SilentCut(Vector2 p, float dir, float scale)
        {
            Color col = KMath.Hex("#a46bff"), hot = KMath.Hex("#f0e6ff");
            LensCut(p, dir > 0f ? -24f : 204f, scale, col, hot, 0.6f);
            Ring(p, 0.06f, 0.7f * scale, 0.05f * scale, col, 0.3f, true, 1f, 2.2f);
            for (int i = 0; i < Mathf.RoundToInt(10 * scale); i++)
                Emit(_sparks, p, KMath.Dir(Random.Range(0f, 360f)) * Random.Range(1.5f, 4.5f) * scale, Random.Range(0.025f, 0.045f), Random.Range(0.3f, 0.6f), i % 3 == 0 ? hot : col);
            for (int i = 0; i < 4; i++)
                Emit(_motes, p + Random.insideUnitCircle * 0.25f, Random.insideUnitCircle * 0.3f + Vector2.down * 0.2f, Random.Range(0.06f, 0.1f), Random.Range(0.6f, 1f), hot.WithA(0.7f));
        }

        /// <summary>Petals hanging in the still air round <paramref name="c"/> (Silent Moon): soft motes that barely drift.</summary>
        public void HangingPetals(Vector2 c, float halfW, float halfH, int n, Color col, Color hot, float life)
        {
            for (int i = 0; i < n; i++)
            {
                Vector2 p = c + new Vector2(Random.Range(-halfW, halfW), Random.Range(-halfH, halfH));
                Emit(_motes, p, new Vector2(Random.Range(-0.04f, 0.04f), Random.Range(-0.05f, 0.01f)), Random.Range(0.05f, 0.1f), life * Random.Range(0.85f, 1f),
                    Color.Lerp(col, hot, Random.Range(0.3f, 0.9f)).WithA(0.75f));
            }
        }

        /// <summary>Speed lines streaking back from <paramref name="at"/> against <paramref name="dir"/>: a dash nobody saw.</summary>
        public void SpeedLines(Vector2 at, float dir, Color col, int n)
        {
            var ys = new float[n]; var len = new float[n]; var x0 = new float[n];
            for (int i = 0; i < n; i++) { ys[i] = Random.Range(-0.85f, 0.75f); len[i] = Random.Range(0.6f, 1.6f); x0[i] = Random.Range(0f, 0.6f); }
            Dyn("SpeedLines", AddHDR(2f), Order + 30, 0.24f, true, (rib, u) =>
            {
                for (int i = 0; i < n; i++)
                {
                    float tip = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.4f)), tail = KMath.Apply(Ease.InQuad, u);
                    Vector2 a = at + new Vector2(-dir * (x0[i] + len[i] * tail), ys[i]), b = at + new Vector2(-dir * (x0[i] + len[i] * tip), ys[i]);
                    Strip(rib, a, b, 0f, 1f, 0.012f * (1f - u), col.WithA(0f), col.WithA(0.8f * (1f - u)), 4);
                }
            });
        }

        // =====================================================================================
        //  HELL FIRE (Onikiri)
        // =====================================================================================
        /// <summary>A tongue of the demon aura: fire licking up off the floor, a wisp of dark smoke now and then.</summary>
        public void FlameTongue(Vector2 p, Color col, Color hot)
        {
            Emit(_embers, p, new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(3f, 6.5f)), Random.Range(0.06f, 0.12f), Random.Range(0.25f, 0.5f), Random.value < 0.5f ? col : hot);
            if (Random.value < 0.35f) Emit(_dust, p + Vector2.up * 0.3f, new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.6f, 1.4f)), Random.Range(0.3f, 0.5f), Random.Range(0.5f, 0.8f), new Color(0.11f, 0.05f, 0.04f, 0.55f));
        }

        /// <summary>
        /// Cracks racing out along the floor from <paramref name="x"/>: jagged glowing lines that grow out fast, then cool from white-hot to
        /// the body colour to a dark scar (ground feedback, craft §5.8).
        /// </summary>
        public void GroundCracks(float x, int n, float minLen, float maxLen, Color col, Color hot, float life)
        {
            var dirs = new float[n]; var lens = new float[n]; var lift = new float[n];
            var jag = new float[n * 6];
            for (int i = 0; i < n; i++)
            {
                dirs[i] = i % 2 == 0 ? 1f : -1f;
                lens[i] = Random.Range(minLen, maxLen);
                lift[i] = Random.Range(0.005f, 0.06f);
                for (int k = 0; k < 6; k++) jag[i * 6 + k] = Random.Range(-0.025f, 0.025f);
            }
            System.Action<Ribbon, float, bool> build = (rib, u, core) =>
            {
                float grow = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.22f));
                for (int i = 0; i < n; i++)
                {
                    rib.Break();
                    float w = (core ? 0.012f : 0.035f) * (1f - u * 0.6f);
                    for (int k = 0; k <= 5; k++)
                    {
                        float s = k / 5f * grow;
                        Vector2 c = new(x + dirs[i] * lens[i] * s, 0.015f + lift[i] * s + jag[i * 6 + k]);
                        float a = (1f - s * 0.5f) * (1f - u);
                        Color cc = core ? Color.Lerp(hot, col, Mathf.Clamp01(u * 2.5f)).WithA(a) : col.WithA(a * 0.6f);
                        rib.Add(c + Vector2.up * w, c - Vector2.up * w, cc, cc);
                    }
                }
            };
            Dyn("Cracks", AddHDR(1.8f), Order - 5, life, false, (rib, u) => build(rib, u, false));
            Dyn("CracksCore", AddHDR(2.6f), Order - 4, life * 0.7f, false, (rib, u) => build(rib, u, true));
        }

        /// <summary>
        /// Hell split: the cleaver's landing. An impact flash, the floor's shock ring racing out, a column of fire that rises and
        /// collapses, rock and embers thrown up and out, dark smoke rolling over, dust.
        /// </summary>
        public void HellSplit(Vector2 p, float dir, Color col, Color hot, Color deep)
        {
            Star(p + Vector2.up * 0.35f, 1.4f, Color.white, col, 0.14f, 10, true, Order + 80);
            Ring(new Vector2(p.x, 0.03f), 0.2f, 3.6f, 0.18f, col, 0.55f, true, 0.14f, 2.4f);
            Ring(new Vector2(p.x, 0.03f), 0.1f, 2.2f, 0.09f, hot, 0.35f, true, 0.14f, 2f);
            Column(p, 2.0f, 0.55f, col, hot, deep, 0.7f);
            for (int i = 0; i < 26; i++)
            {
                Vector2 v = new(Random.Range(-4.5f, 4.5f), Random.Range(3f, 9f));
                Emit(_sparks, p + new Vector2(Random.Range(-0.5f, 0.5f), 0.05f), v, Random.Range(0.04f, 0.08f), Random.Range(0.5f, 1f), i % 3 == 0 ? new Color(0.11f, 0.1f, 0.09f, 1f) : Color.Lerp(col, hot, Random.value));
            }
            for (int i = 0; i < 14; i++)
                Emit(_dust, p + new Vector2(Random.Range(-1f, 1f), 0.1f), new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(0.4f, 1.6f)), Random.Range(0.4f, 0.8f), Random.Range(0.8f, 1.3f), new Color(0.12f, 0.06f, 0.05f, 0.6f));
            Dust(p, 2.2f, 0f);
        }

        /// <summary>A lava pillar erupting out of a fissure: it rises fast, holds, collapses; a white-hot core, embers off its top, a dark base.</summary>
        public void LavaPillar(Vector2 p, float height, Color col, Color hot, Color deep)
        {
            Column(p, height, 0.32f, col, hot, deep, 0.6f);
            Ring(new Vector2(p.x, 0.03f), 0.05f, 0.9f, 0.06f, col, 0.35f, false, 0.2f, 2f);
            for (int i = 0; i < 8; i++)
                Emit(_embers, p + new Vector2(Random.Range(-0.15f, 0.15f), height * 0.6f), new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(2f, 5f)), Random.Range(0.05f, 0.1f), Random.Range(0.4f, 0.8f), Random.value < 0.5f ? col : hot);
        }

        /// <summary>A column (fire or water) rising from the floor at <paramref name="p"/> and collapsing: alpha dark base, additive body and core.</summary>
        public void Column(Vector2 p, float height, float halfWidth, Color col, Color hot, Color deep, float life)
        {
            System.Func<float, float> h = u => height * Mathf.Sin(Mathf.Min(1f, u * 1.6f) * Mathf.PI * 0.5f) * (u < 0.65f ? 1f : 1f - (u - 0.65f) / 0.35f);
            Dyn("ColumnBase", MeshKit.Alpha, Order + 8, life, false, (rib, u) =>
            {
                float hh = h(u) * 0.5f;
                Strip(rib, p, p + Vector2.up * Mathf.Max(0.01f, hh), 0f, 1f, halfWidth * 1.5f, deep.WithA(0f), deep.WithA(0.55f * (1f - u)), 10);
            });
            Dyn("Column", AddHDR(1.45f), Order + 26, life, false, (rib, u) =>
            {
                float hh = Mathf.Max(0.01f, h(u));
                Vector2 top = p + Vector2.up * hh;
                // a tapered tongue: wide at the floor, a point at the top, the body cooling to its colour
                rib.Break();
                for (int i = 0; i <= 10; i++)
                {
                    float s = i / 10f, w = halfWidth * (1f - s) * (0.7f + 0.3f * Mathf.Sin(u * 30f + s * 9f));
                    Vector2 c = Vector2.Lerp(p, top, s) + Vector2.right * Mathf.Sin(s * 5f + u * 12f) * 0.04f;
                    Color cc = Color.Lerp(col, hot, (1f - s) * 0.35f).WithA(0.85f * (1f - u * 0.6f));
                    rib.Add(c + Vector2.left * w, c + Vector2.right * w, col.WithA(0f), cc);
                }
                Strip(rib, p, top, 0f, 0.7f, halfWidth * 0.2f, hot.WithA(0f), hot.WithA(0.75f * (1f - u)), 10);
            });
        }

        // =====================================================================================
        //  WATER (Mizuchi)
        // =====================================================================================
        /// <summary>The water dragon's body: its head's trail, newest first, pushed by the art every frame; <see cref="gone"/> fades it out.</summary>
        public sealed class DragonBody
        {
            public readonly List<Vector2> trail = new();
            public readonly List<bool> front = new();
            public float gone;
            public const int Length = 54;

            public void Push(Vector2 head, bool inFront)
            {
                if (gone >= 1f) { if (trail.Count > 0) { trail.RemoveAt(trail.Count - 1); front.RemoveAt(front.Count - 1); } return; }
                trail.Insert(0, head); front.Insert(0, inFront);
                if (trail.Count > Length) { trail.RemoveAt(trail.Count - 1); front.RemoveAt(front.Count - 1); }
            }
        }

        /// <summary>
        /// The water dragon: a tapering cel-shaded ribbon along <paramref name="body"/>'s trail (a dark rim, the water body, a light
        /// belly band, fins), its head pointing along its flight with horns and an open jaw. Spray sheds off it (the art emits it).
        /// </summary>
        public void WaterDragon(DragonBody body, Color col, Color hot, Color deep, float life)
        {
            Color water = Color.Lerp(col, deep, 0.25f);
            Dyn("DragonBody", MeshKit.Alpha, Order + 34, life, false, (rib, u) =>
            {
                var T = body.trail; int n = T.Count;
                if (n < 3) return;
                float fade = 1f - body.gone;
                for (int layer = 0; layer < 2; layer++)
                {
                    rib.Break();
                    for (int i = 0; i < n; i++)
                    {
                        Vector2 a = T[Mathf.Max(0, i - 1)], b = T[Mathf.Min(n - 1, i + 1)];
                        Vector2 t = (a - b); if (t.sqrMagnitude < 1e-8f) t = Vector2.right;
                        Vector2 nn = new Vector2(-t.y, t.x).normalized;
                        float w = (0.3f - 0.23f * Mathf.Pow(i / (float)(n - 1), 0.8f)) + (layer == 0 ? 0.045f : 0f);
                        Color c = (layer == 0 ? deep.WithA(0.9f) : water.WithA(0.92f)) * new Color(1, 1, 1, fade);
                        rib.Add(T[i] + nn * w, T[i] - nn * w, c, c);
                    }
                }
                // the head: an open jaw and two horns, along the flight
                Vector2 h = T[0], back = T[Mathf.Min(3, n - 1)];
                Vector2 fwd = (h - back).sqrMagnitude > 1e-6f ? (h - back).normalized : Vector2.right;
                Vector2 up = new(-fwd.y, fwd.x);
                if (up.y < 0f) up = -up;
                Vector2 P(float x, float y) => h + fwd * x * 0.011f + up * y * 0.011f;
                Color dk = deep.WithA(fade), bd = water.WithA(fade), lo = Color.Lerp(water, deep, 0.4f).WithA(fade);
                rib.AddTriangle(P(48, 2), P(-26, -14), P(-26, 16), dk, dk, dk);
                rib.AddTriangle(P(44, 0), P(-22, 11), P(18, 15), bd, bd, bd);          // upper jaw
                rib.AddTriangle(P(36, -8), P(-20, -6), P(0, -18), lo, lo, lo);        // lower jaw, open
            });
            Dyn("DragonLight", AddHDR(2.2f), Order + 36, life, false, (rib, u) =>
            {
                var T = body.trail; int n = T.Count;
                if (n < 3) return;
                float fade = 1f - body.gone;
                rib.Break();
                for (int i = 0; i < n; i++)
                {
                    Vector2 a = T[Mathf.Max(0, i - 1)], b = T[Mathf.Min(n - 1, i + 1)];
                    Vector2 t = (a - b); if (t.sqrMagnitude < 1e-8f) t = Vector2.right;
                    Vector2 nn = new Vector2(-t.y, t.x).normalized;
                    float w = 0.3f - 0.23f * Mathf.Pow(i / (float)(n - 1), 0.8f);
                    Vector2 c = T[i] + nn * w * 0.22f;
                    Color cc = hot.WithA(0.55f * fade * (1f - i / (float)n));
                    rib.Add(c + nn * w * 0.12f, c - nn * w * 0.12f, cc, cc);            // the belly band
                }
                for (int i = 2; i < n - 1; i += 4)
                {
                    Vector2 t = (T[i - 1] - T[i + 1]).normalized, nn = new(-t.y, t.x);
                    float w = 0.3f - 0.23f * Mathf.Pow(i / (float)(n - 1), 0.8f);
                    Color fc = hot.WithA(0.8f * fade);
                    rib.AddTriangle(T[i] - nn * w * 0.5f, T[i] - nn * w * 1.15f + t * w * 0.5f, T[i + 1] - nn * w * 0.5f, fc, fc.WithA(0f), fc);   // a fin
                }
                Vector2 h = T[0], back = T[Mathf.Min(3, n - 1)];
                Vector2 fwd = (h - back).sqrMagnitude > 1e-6f ? (h - back).normalized : Vector2.right;
                Vector2 up = new(-fwd.y, fwd.x);
                if (up.y < 0f) up = -up;
                Vector2 P(float x, float y) => h + fwd * x * 0.011f + up * y * 0.011f;
                Color hc = hot.WithA(fade);
                rib.AddTriangle(P(-8, 18), P(-46, 40), P(-18, 14), hc, hc.WithA(0.2f), hc);   // horns
                rib.AddTriangle(P(-14, 14), P(-56, 22), P(-22, 6), hc, hc.WithA(0.2f), hc);
                rib.AddTriangle(P(9, 11), P(3, 7), P(11, 7), Color.white.WithA(fade), Color.white.WithA(fade), hc);   // the eye
            });
        }

        /// <summary>A sheet of water over the floor from cx − halfW to cx + halfW: it spreads in, ripples, and drains away.</summary>
        public void Flood(Vector2 c, float halfW, Color col, Color hot, float life)
        {
            Dyn("Flood", MeshKit.Alpha, Order - 10, life, false, (rib, u) =>
            {
                float k = Mathf.Clamp01(u / 0.12f) * (u > 0.8f ? 1f - (u - 0.8f) / 0.2f : 1f);
                Color body = Color.Lerp(col, Color.black, 0.35f).WithA(0.34f * k);
                rib.Add(new Vector3(c.x - halfW, 0.1f), new Vector3(c.x - halfW, -0.04f), body, body);
                rib.Add(new Vector3(c.x + halfW, 0.1f), new Vector3(c.x + halfW, -0.04f), body, body);
            });
            Dyn("FloodLight", AddHDR(1.6f), Order - 9, life, false, (rib, u) =>
            {
                float k = Mathf.Clamp01(u / 0.12f) * (u > 0.8f ? 1f - (u - 0.8f) / 0.2f : 1f);
                rib.Break();
                for (int i = 0; i <= 30; i++)
                {
                    float x = c.x - halfW + 2f * halfW * i / 30f;
                    float y = 0.1f + Mathf.Sin(x * 3.1f + u * 24f) * 0.012f;
                    Color cc = hot.WithA(0.45f * k);
                    rib.Add(new Vector3(x, y + 0.012f), new Vector3(x, y - 0.012f), cc, cc);
                }
            });
        }

        /// <summary>A gust of rain over the frame (centre c, half-size): slanted streaks falling at a constant speed (no drag: rain doesn't hang).</summary>
        public void Rain(Vector2 c, float halfW, float halfH, Color col)
        {
            const int n = 7;
            var xs = new float[n]; var ys = new float[n];
            for (int i = 0; i < n; i++) { xs[i] = c.x + Random.Range(-halfW, halfW); ys[i] = c.y + halfH + Random.Range(0f, 0.6f); }
            Dyn("Rain", AddHDR(1.3f), Order + 4, 0.32f, false, (rib, u) =>
            {
                for (int i = 0; i < n; i++)
                {
                    float y = ys[i] - u * 16f * 0.32f;
                    Vector2 a = new(xs[i] + (ys[i] - y) * 0.2f, y), b = a + new Vector2(0.07f, 0.34f);
                    Strip(rib, a, b, 0f, 1f, 0.008f, col.WithA(0f), col.WithA(0.4f), 3);
                }
            });
        }

        /// <summary>A splash of water at p along dir: a flash, a ring, droplets thrown out and falling.</summary>
        public void Splash(Vector2 p, Vector2 dir, Color col, Color hot, float scale)
        {
            Star(p, 0.35f * scale, hot, col, 0.09f, 6, true, Order + 60);
            Ring(p, 0.06f, 0.65f * scale, 0.06f * scale, col, 0.3f, true, 1f, 2f);
            Vector2 d = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector2.up;
            for (int i = 0; i < Mathf.RoundToInt(14 * scale); i++)
            {
                Vector2 v = (d + Random.insideUnitCircle * 0.9f + Vector2.up * 0.6f).normalized * Random.Range(2.5f, 6f) * scale;
                Emit(_sparks, p, v, Random.Range(0.03f, 0.06f), Random.Range(0.35f, 0.7f), Random.value < 0.4f ? hot : col);
            }
        }

        /// <summary>The spear's sweet spot: a small crisp splash off the tip, along the thrust.</summary>
        public void TipSplash(Vector2 p, Vector2 dir) => Splash(p, dir, KMath.Hex("#2f9dff"), KMath.Hex("#d4f1ff"), 0.6f);

        /// <summary>The dragon's crash: a tidal column of water rising and collapsing, flattened rings racing out, droplets and foam.</summary>
        public void TidalCrash(Vector2 p, Color col, Color hot, Color deep, float scale)
        {
            Star(p + Vector2.up * 0.4f, 1.3f * scale, Color.white, col, 0.14f, 10, true, Order + 80);
            Column(p, 2.6f * scale, 0.7f * scale, col, hot, deep, 0.8f);
            Ring(new Vector2(p.x, 0.03f), 0.2f, 3.2f * scale, 0.16f, col, 0.6f, true, 0.14f, 2.2f);
            Ring(new Vector2(p.x, 0.03f), 0.1f, 1.9f * scale, 0.08f, hot, 0.4f, true, 0.14f, 2f);
            for (int i = 0; i < 40; i++)
            {
                float a = 90f + Random.Range(-75f, 75f);
                Emit(_sparks, p + new Vector2(Random.Range(-0.35f, 0.35f), 0.05f), KMath.Dir(a) * Random.Range(3f, 9f) * scale, Random.Range(0.03f, 0.07f), Random.Range(0.6f, 1.1f), Random.value < 0.4f ? hot : col);
            }
            for (int i = 0; i < 10; i++)
                Emit(_dust, p + new Vector2(Random.Range(-1.2f, 1.2f), 0.05f), new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(0.2f, 0.9f)), Random.Range(0.35f, 0.6f), Random.Range(0.6f, 1f), hot.WithA(0.45f));
        }

        // =====================================================================================
        //  SHADOW (Yamigama)
        // =====================================================================================
        /// <summary>A shadow clone (Reaper's Requiem): a held snapshot of a fighter as living shadow, its handle for <see cref="Ash"/>.</summary>
        public sealed class Clone
        {
            internal System.Action<float> ash;
            /// <summary>The clone falls to ash after <paramref name="delay"/> seconds.</summary>
            public void Ash(float delay = 0f) => ash?.Invoke(delay);
        }

        sealed class CloneFx : Fx
        {
            public MeshRenderer[] body, rim;
            public float rise, ashAt = -1f;
            public Vector2 at;
            public Color col, deep;
            public Transform pivot;
            public Vfx owner;
            readonly MaterialPropertyBlock _mpb = new();
            const float AshLen = 0.35f;

            public void AshAt(float delay)
            {
                if (ashAt >= 0f) return;
                ashAt = t + delay;
                life = ashAt + AshLen;
            }

            public override void Tick(float u)
            {
                float rk = rise > 0f ? KMath.Apply(Ease.OutCubic, Mathf.Clamp01(t / rise)) : 1f;
                float a = rk;
                if (ashAt >= 0f && t > ashAt)
                {
                    float k = Mathf.Clamp01((t - ashAt) / AshLen);
                    a *= 1f - k;
                    if (owner != null && Random.value < 0.6f)
                        Emit(owner._dust, at + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.2f, 1.6f)), new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.4f, 1.2f)), Random.Range(0.15f, 0.3f), 0.6f, new Color(0.25f, 0.18f, 0.32f, 0.6f));
                }
                pivot.position = new Vector3(at.x, at.y - 1.7f * (1f - rk), 0f);           // up out of the floor
                Paint(body, deep, 0.92f * a);
                Paint(rim, col, 0.65f * a);
            }

            void Paint(MeshRenderer[] rs, Color c, float alpha)
            {
                _mpb.SetVector("_Color", new Vector4(1f, 1f, 1f, alpha));
                _mpb.SetColor("_FlashColor", c);
                _mpb.SetFloat("_Flash", 1f);
                foreach (var r in rs) if (r != null) r.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>
        /// A shadow clone of <paramref name="rig"/>'s pose standing at <paramref name="at"/> (mirrored when <paramref name="mirror"/>): an
        /// alpha-black body with a violet edge, rising out of the floor over <paramref name="rise"/> seconds and held until it falls to ash.
        /// </summary>
        public Clone ShadowClone(FighterRig rig, Vector2 pivot, Vector2 at, bool mirror, Color col, Color deep, float rise)
        {
            if (rig == null) return null;
            var root = new GameObject("ShadowClone");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(pivot.x, pivot.y, 0f);
            var rimSnap = rig.Snapshot(col, rig.baseOrder - 46);
            var bodySnap = rig.Snapshot(deep, rig.baseOrder - 44);
            rimSnap.transform.SetParent(root.transform, true);
            bodySnap.transform.SetParent(root.transform, true);
            rimSnap.transform.localPosition += new Vector3(0.025f, 0.02f, 0f);
            root.transform.localScale = new Vector3(mirror ? -1f : 1f, 1f, 1f);
            var fx = new CloneFx
            {
                go = root, pivot = root.transform, at = at, rise = rise, col = col, deep = InkViolet, owner = this, life = 4f,
                body = bodySnap.GetComponentsInChildren<MeshRenderer>(), rim = rimSnap.GetComponentsInChildren<MeshRenderer>(),
            };
            _live.Add(fx);
            fx.Tick(0f);
            return new Clone { ash = fx.AshAt };
        }

        /// <summary>A chain thrown from a to b: it reaches out along a sagging curve, dark links over a faint violet glow, then hangs taut until it fades.</summary>
        public void ChainLine(Vector2 a, Vector2 b, Color col, Color hot, float life)
        {
            Vector2 ctrl = (a + b) * 0.5f + Vector2.down * 0.28f;
            Vector2 At(float s) => (1 - s) * (1 - s) * a + 2 * (1 - s) * s * ctrl + s * s * b;
            Dyn("ChainLinks", MeshKit.Alpha, Order + 22, life, false, (rib, u) =>
            {
                float reach = KMath.Apply(Ease.OutCubic, Mathf.Clamp01(u / 0.12f));
                float fade = u > 0.8f ? 1f - (u - 0.8f) / 0.2f : 1f;
                Color link = new Color(0.23f, 0.16f, 0.31f, fade);
                const int links = 22;
                for (int i = 0; i < links; i++)
                {
                    float s0 = i / (float)links * reach, s1 = (i + 0.6f) / links * reach;
                    Vector2 p0 = At(s0), p1 = At(s1), d = (p1 - p0), n = new Vector2(-d.y, d.x).normalized * (i % 2 == 0 ? 0.022f : 0.012f);
                    rib.Break();
                    rib.Add(p0 + n, p0 - n, link, link);
                    rib.Add(p1 + n, p1 - n, link, link);
                }
            });
            Dyn("ChainGlow", AddHDR(1.6f), Order + 23, life, false, (rib, u) =>
            {
                float reach = KMath.Apply(Ease.OutCubic, Mathf.Clamp01(u / 0.12f));
                float fade = u > 0.8f ? 1f - (u - 0.8f) / 0.2f : 1f;
                rib.Break();
                for (int i = 0; i <= 20; i++)
                {
                    Vector2 p = At(i / 20f * reach);
                    Color cc = col.WithA(0.4f * fade);
                    rib.Add(p + Vector2.up * 0.008f, p - Vector2.up * 0.008f, cc, cc);
                }
            });
            Glint(b, hot);
        }

        /// <summary>
        /// A scythe crescent through c (the dark element, craft §3: an alpha-black body with a lit edge): the arc sweeps open in the first
        /// quarter of its life, thick in the middle and thin at the tips, then erodes from the tips; a violet body and a hot outer edge.
        /// </summary>
        public void ScytheCrescent(Vector2 c, float rotDeg, float scale, float dir, Color col, Color hot, Color deep)
        {
            float R = 0.95f * scale;
            float rot = rotDeg * Mathf.Deg2Rad;
            Vector2 L(float x, float y)
            {
                x *= dir;
                return c + new Vector2(x * Mathf.Cos(rot) - y * Mathf.Sin(rot), x * Mathf.Sin(rot) + y * Mathf.Cos(rot));
            }
            System.Action<Ribbon, float, int> arc = (rib, u, layer) =>
            {
                float sweep = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.25f));
                float erode = KMath.Apply(Ease.InQuad, Mathf.Clamp01((u - 0.35f) / 0.65f));
                const float a0 = -1.35f, span = 2.7f;
                float lo = a0 + span * 0.5f * erode, hi = a0 + span * sweep - span * 0.5f * erode;
                if (hi <= lo) return;
                rib.Break();
                for (int i = 0; i <= 24; i++)
                {
                    float a = Mathf.Lerp(lo, hi, i / 24f);
                    float thick = Mathf.Sin(Mathf.Clamp01((a - a0) / span) * Mathf.PI);
                    float outer = R * (layer == 0 ? 1.06f : 1f), inner = R * (1f - (layer == 2 ? 0.05f : 0.2f) * thick);
                    Vector2 po = L(Mathf.Cos(a) * outer, Mathf.Sin(a) * outer), pi = L(Mathf.Cos(a) * inner - R * 0.1f * thick, Mathf.Sin(a) * inner);
                    Color co, ci;
                    if (layer == 0) { co = ci = deep.WithA(0.75f * (1f - u)); }
                    else if (layer == 1) { co = col.WithA(0.9f * (1f - u)); ci = col.WithA(0f); }
                    else { co = hot.WithA(1f - u); ci = hot.WithA(0f); }
                    rib.Add(po, pi, co, ci);
                }
            };
            Dyn("ScytheDark", MeshKit.Alpha, Order + 44, 0.6f, true, (rib, u) => arc(rib, u, 0));
            Dyn("Scythe", AddHDR(2.4f), Order + 50, 0.6f, true, (rib, u) => arc(rib, u, 1));
            Dyn("ScytheEdge", AddHDR(3f), Order + 52, 0.45f, true, (rib, u) => arc(rib, u, 2));
            for (int i = 0; i < Mathf.RoundToInt(10 * scale); i++)
                Emit(_sparks, c, KMath.Dir(Random.Range(0f, 360f)) * Random.Range(1.5f, 5f) * Mathf.Sqrt(scale), Random.Range(0.025f, 0.05f), Random.Range(0.3f, 0.6f), i % 3 == 0 ? hot : col);
        }

        /// <summary>A pair of eyes opening in the dark at p, then closing.</summary>
        public void Eyes(Vector2 p, Color col, Color hot)
        {
            float sz = Random.Range(0.05f, 0.09f), tilt = Random.Range(-0.2f, 0.2f);
            Dyn("Eyes", AddHDR(2f), Order - 30, 0.9f, false, (rib, u) =>
            {
                float open = Mathf.Sin(Mathf.Clamp01(u) * Mathf.PI);
                for (int e = -1; e <= 1; e += 2)
                {
                    Vector2 c = p + new Vector2(e * sz * 1.7f, e * tilt * sz);
                    rib.Break();
                    for (int i = 0; i <= 10; i++)
                    {
                        float a = i / 10f * Mathf.PI;
                        Vector2 top = c + new Vector2(-Mathf.Cos(a) * sz, Mathf.Sin(a) * sz * 0.38f * open);
                        Vector2 bot = c + new Vector2(-Mathf.Cos(a) * sz, -Mathf.Sin(a) * sz * 0.38f * open);
                        Color cc = Color.Lerp(col, hot, Mathf.Sin(a) * 0.5f).WithA(0.9f * open);
                        rib.Add(top, bot, cc, cc);
                    }
                }
            });
        }

        /// <summary>The chain sickle's reap: a dark burst that draws inward, then a violet ring and sparks.</summary>
        public void ReapBurst(Vector2 p, Vector2 dir)
        {
            Color col = KMath.Hex("#9a3cff"), hot = KMath.Hex("#f0c8ff");
            for (int i = 0; i < 8; i++)
            {
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.4f, 0.8f);
                Emit(_motes, p + off, -off / 0.22f, Random.Range(0.08f, 0.14f), 0.22f, InkViolet.WithA(0.85f));
            }
            Star(p, 0.4f, hot, col, 0.1f, 6, true, Order + 60);
            Ring(p, 0.05f, 0.7f, 0.07f, col, 0.3f, true, 1f, 2f);
            ScytheCrescent(p, Random.Range(-30f, 30f), 0.42f, dir.x >= 0f ? 1f : -1f, col, hot, InkViolet);
        }

        /// <summary>The hook mark taking hold: a small violet ring closing on the foe and a glint.</summary>
        public void HookMark(Vector2 p)
        {
            Color col = KMath.Hex("#9a3cff");
            Ring(p, 0.55f, 0.12f, 0.035f, col, 0.3f, true, 1f, 1.8f);
            Glint(p, col);
        }

        /// <summary>The cleaver crushing through a guard: hot orange sparks back off the guard and a broken ring.</summary>
        public void GuardCrush(Vector2 p, Vector2 dir)
        {
            Color col = KMath.Hex("#ff3a1c"), hot = KMath.Hex("#ffd2a0");
            Star(p, 0.42f, hot, col, 0.09f, 5, true, Order + 60, 0.7f);
            Ring(p, 0.08f, 0.55f, 0.06f, col, 0.24f, true, 1f, 2.2f);
            for (int i = 0; i < 12; i++)
                Emit(_sparks, p, KMath.Dir(KMath.Ang(dir) + Random.Range(-70f, 70f)) * Random.Range(3f, 7f), 0.04f, Random.Range(0.25f, 0.5f), i % 3 == 0 ? hot : col);
        }

        // =====================================================================================
        //  FOOTWORK (the JS WeaponFootwork.dash)
        // =====================================================================================
        /// <summary>One frame of a dash's trail in the weapon's element, behind a fighter at <paramref name="pos"/> dashing along <paramref name="dir"/>.</summary>
        public void FootworkTrail(Footwork kind, FighterRig rig, Vector2 pos, float dir, bool grounded)
        {
            Vector2 back = pos + new Vector2(-dir * 0.18f, Random.Range(0.25f, 1.45f));
            switch (kind)
            {
                case Kage.Footwork.Crimson:
                    Emit(_embers, back, new Vector2(-dir * Random.Range(0.5f, 1.5f), Random.Range(-0.2f, 0.4f)), 0.04f, 0.3f, KMath.Hex("#ff2238"));
                    break;
                case Kage.Footwork.Lightning:
                    if (Random.value < 0.5f) Bolt(back, back + new Vector2(-dir * Random.Range(0.35f, 0.8f), Random.Range(-0.25f, 0.25f)), 0.012f, 0.1f, KMath.Hex("#38c8ff"), 6, -1f, 1);
                    if (rig != null) Ghost(rig, KMath.Hex("#38c8ff").WithA(0.32f), 0.2f);
                    break;
                case Kage.Footwork.Violet:
                    if (rig != null) Ghost(rig, KMath.Hex("#a46bff").WithA(0.3f), 0.3f);
                    Emit(_motes, back, new Vector2(-dir * Random.Range(0.2f, 0.7f), Random.Range(-0.25f, 0.1f)), Random.Range(0.04f, 0.07f), Random.Range(0.4f, 0.7f), KMath.Hex("#c9a8ff").WithA(0.8f));
                    break;
                case Kage.Footwork.Flame:
                    Emit(_embers, pos + new Vector2(-dir * 0.15f, Random.Range(0.1f, 1.0f)), new Vector2(-dir * Random.Range(0.4f, 1.2f), Random.Range(1.4f, 2.8f)), Random.Range(0.04f, 0.08f), Random.Range(0.3f, 0.5f), Random.value < 0.5f ? KMath.Hex("#ffb35a") : KMath.Hex("#ff3a1c"));
                    break;
                case Kage.Footwork.Water:
                    if (grounded)
                        for (int i = 0; i < 2; i++)
                            Emit(_sparks, pos + new Vector2(-dir * Random.Range(0f, 0.25f), 0.03f), new Vector2(-dir * Random.Range(0.5f, 1.7f), Random.Range(1f, 2.8f)), 0.03f, Random.Range(0.3f, 0.55f), i == 0 ? KMath.Hex("#bfe6ff") : KMath.Hex("#2f9dff"));
                    if (rig != null && Random.value < 0.5f) Ghost(rig, KMath.Hex("#2f9dff").WithA(0.24f), 0.2f);
                    break;
                case Kage.Footwork.Earth:
                    if (grounded)
                    {
                        Dust(pos + new Vector2(-dir * 0.2f, 0f), 0.45f, -dir);
                        Emit(_sparks, pos + new Vector2(-dir * 0.15f, 0.04f), new Vector2(-dir * Random.Range(0.4f, 1.4f), Random.Range(1.5f, 3.5f)), Random.Range(0.04f, 0.07f), Random.Range(0.35f, 0.6f), Random.value < 0.3f ? new Color(1f, 0.71f, 0.12f, 1f) : new Color(0.29f, 0.23f, 0.16f, 1f));
                    }
                    if (rig != null && Random.value < 0.4f) Ghost(rig, new Color(1f, 0.71f, 0.12f, 0.18f), 0.16f);
                    break;
                case Kage.Footwork.Shadow:
                    Emit(_dust, back, new Vector2(-dir * Random.Range(0.3f, 0.8f), Random.Range(-0.3f, 0.05f)), Random.Range(0.15f, 0.26f), Random.Range(0.4f, 0.7f), new Color(0.09f, 0.04f, 0.13f, 0.55f));
                    if (rig != null && Random.value < 0.5f) Ghost(rig, new Color(0.24f, 0.08f, 0.35f, 0.45f), 0.3f);
                    break;
            }
        }
    }
}
