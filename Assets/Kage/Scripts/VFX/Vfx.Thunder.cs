using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Thunder for Raikon's storm (docs/raikon-ascendant-vfx.md, "Thunder pass"), on the effekseer-vfx skill's craft rules: lightning that has a
    /// source and a floor. A bolt from above falls out of a <see cref="ThunderCloud"/> that gathered first (the anticipation), strikes with the
    /// lightning signature of Vfx.Lightning.cs (main strike, return stroke, after-flicker), lights a column and the backdrop (the environment
    /// flash), and pins itself to the floor with running ground cracks, a scorch, dust and rising ash (ground feedback). <c>power</c> 0..3 is the
    /// boss phase: the same effect grows per phase (more forks, a branch, a re-strike, longer cracks, more sparks); it never changes shape or colour.
    /// </summary>
    public partial class Vfx
    {
        /// <summary>The storm cloud's palette: a lit grey-red body that reads on the blood-dark sky (the craft: a dark element still needs value), a darker backing, a highlight.</summary>
        public static readonly Color CloudBody = new(0.27f, 0.16f, 0.18f, 1f);
        public static readonly Color CloudDark = new(0.10f, 0.04f, 0.06f, 1f);
        public static readonly Color CloudLit = new(0.52f, 0.36f, 0.38f, 1f);
        /// <summary>The standing aura's red: deeper than the bolt crimson, so the bolts still name the element.</summary>
        public static readonly Color AuraRed = new(1f, 0.06f, 0.09f, 1f);

        /// <summary>The backdrop flash's order: over the backdrop's wash (EnvBackdrop, -2550), under its floor (-1200) and everything in the fight.</summary>
        public const int BackdropFlashOrder = -2540;

        const float Tau = Mathf.PI * 2f;

        // ------------------------------------------------------------------------------------------------ the view
        /// <summary>The camera the fight is seen through: the owner's fight camera, else the main camera, else any camera.</summary>
        public static Camera ViewCam(Fighter owner = null)
        {
            if (owner != null && owner.ctx != null && owner.ctx.Camera != null && owner.ctx.Camera.cam != null) return owner.ctx.Camera.cam;
            var m = Camera.main;
            if (m != null) return m;
            return Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null;
        }

        /// <summary>The world rectangle the camera shows (a 12 x 6 m box round the arena without one).</summary>
        public static Rect ViewRect(Camera cam)
        {
            if (cam == null || !cam.orthographic) return Rect.MinMaxRect(-6f, -1f, 6f, 5f);
            float h = cam.orthographicSize, w = h * cam.aspect;
            Vector3 p = cam.transform.position;
            return Rect.MinMaxRect(p.x - w, p.y - h, p.x + w, p.y + h);
        }

        /// <summary>The height of a storm cloud's base: just inside the top of the view, so a cloud is always seen gathering.</summary>
        public static float CloudBaseY(Camera cam) => Mathf.Clamp(ViewRect(cam).yMax - 0.95f, 2.4f, 7f);

        // ------------------------------------------------------------------------------------------------ shapes
        /// <summary>A soft ellipse: flat colour out to <paramref name="solid"/> of the radius, then a fade to alpha 0 at the rim (a cloud puff, a glow).</summary>
        public static void SoftBlob(Ribbon rib, Vector2 c, float rx, float ry, Color col, float solid = 0.5f, int seg = 16)
        {
            if (col.a <= 0.003f || rx <= 0f || ry <= 0f) return;
            Vector2 P(int i, float k)
            {
                float a = i * Tau / seg;
                return c + new Vector2(Mathf.Cos(a) * rx * k, Mathf.Sin(a) * ry * k);
            }
            if (solid > 0.01f)
                for (int i = 0; i < seg; i++) rib.AddTriangle(c, P(i, solid), P(i + 1, solid), col, col, col);
            rib.Break();
            Color e = col.WithA(0f);
            for (int i = 0; i <= seg; i++) rib.Add(P(i, solid), P(i, 1f), col, e);
            rib.Break();
        }

        /// <summary>A polyline as a soft ribbon (bright centre, clear edges), <paramref name="w0"/> wide at its first point and <paramref name="w1"/> at its last.</summary>
        public static void SoftLine(Ribbon rib, List<Vector2> p, float w0, float w1, Color c) => PolyStrip(rib, p, w0, w1, c, true);

        /// <summary>Calls <paramref name="act"/> after <paramref name="seconds"/> of fight time (a second branch, a re-strike); dropped if the Vfx goes first.</summary>
        public void After(float seconds, System.Action act)
        {
            if (act == null) return;
            _live.Add(new DelayFx { life = Mathf.Max(0.001f, seconds), act = act });
        }

        sealed class DelayFx : Fx
        {
            public System.Action act;
            public override void Tick(float u) { }
            public override void Kill()
            {
                if (t >= life) act?.Invoke();
                act = null;
            }
        }

        // ------------------------------------------------------------------------------------------------ light
        /// <summary>
        /// The column of light a strike leaves between the cloud and the floor: a soft additive band (bright along the stroke, clear at its edges, thinning
        /// towards the top) under the stroke envelope, so it flashes with the main strike and again with the return stroke. Gone with the bolt.
        /// </summary>
        public void LightPillar(Vector2 ground, Vector2 top, float halfWidth, float life, Color col, int order = Order + 38)
        {
            Dyn("LightPillar", AddHDR(1.8f), order, life, false, (rib, u) =>
            {
                float e = StrokeEnvelope(u);
                if (e <= 0.01f) return;
                float w = halfWidth * (0.7f + 0.3f * e);
                const int seg = 8;
                for (int half = 0; half < 2; half++)
                {
                    rib.Break();
                    for (int i = 0; i <= seg; i++)
                    {
                        float s = i / (float)seg;
                        Vector2 m = Vector2.Lerp(ground, top, s);
                        Color cc = col.WithA(col.a * e * 0.5f * (1f - s * s * 0.75f)), ce = col.WithA(0f);
                        if (half == 0) rib.Add(m + Vector2.left * w, m, ce, cc);
                        else rib.Add(m, m + Vector2.right * w, cc, ce);
                    }
                }
            });
        }

        /// <summary>A soft additive glow at <paramref name="c"/> under the stroke envelope: a flash inside a cloud, the sky round a distant bolt.</summary>
        public void SkyGlow(Vector2 c, float rx, float ry, Color col, float life, int order)
        {
            Dyn("SkyGlow", AddHDR(1.6f), order, life, false, (rib, u) => SoftBlob(rib, c, rx, ry, col.WithA(col.a * StrokeEnvelope(u)), 0.12f, 20));
        }

        /// <summary>
        /// The environment flash of a strike: the whole backdrop (sky, far city, roofs; not the floor, not the fighters) lit crimson-white, brightest at
        /// the top, under the stroke envelope. It follows the camera for its life. Never a full-screen white: the finisher keeps the only white frame.
        /// </summary>
        public void BackdropFlash(float peak, float life, Camera cam = null)
        {
            if (peak <= 0f) return;
            Dyn("BackdropFlash", AddHDR(1.3f), BackdropFlashOrder, life, false, (rib, u) =>
            {
                float e = StrokeEnvelope(u) * peak;
                if (e <= 0.003f) return;
                var r = ViewRect(cam != null ? cam : ViewCam());
                Color top = new(1f, 0.42f, 0.46f, e), low = new(1f, 0.3f, 0.32f, e * 0.2f);
                rib.Add(new Vector2(r.xMin - 1f, -0.3f), new Vector2(r.xMin - 1f, r.yMax + 1f), low, top);
                rib.Add(new Vector2(r.xMax + 1f, -0.3f), new Vector2(r.xMax + 1f, r.yMax + 1f), low, top);
            });
        }

        // ------------------------------------------------------------------------------------------------ the floor
        /// <summary>
        /// Cracks running out across the floor from <paramref name="p"/> (the floor's ellipse: wide in x, squashed in y) that cool: white-hot for the
        /// 3–4 frames they run, then the crimson glow, then a blood-black residue that outlives the glow. <paramref name="n"/> cracks, the longest
        /// <paramref name="reach"/> metres.
        /// </summary>
        public void GroundCracks(Vector2 p, float reach, int n, float life = 0.7f)
        {
            for (int k = 0; k < n; k++)
            {
                float ang = (k + Random.Range(0.15f, 0.85f)) * 360f / n;
                Vector2 d = KMath.Dir(ang);
                float len = reach * Random.Range(0.5f, 1f);
                Vector2 end = p + new Vector2(d.x * len, d.y * len * 0.16f);
                var jag = new Jag(p + Vector2.up * 0.01f, end + Vector2.up * 0.01f, Mathf.Clamp(Mathf.RoundToInt(len / 0.12f), 4, 12), 0.03f, 0, Random.Range(1, 1 << 20));
                jag.Strike(0);
                var run = new List<Vector2>(jag.main.Count);
                float w = Random.Range(0.012f, 0.02f);
                void Part(float head)
                {
                    run.Clear();
                    int last = Mathf.Clamp(Mathf.CeilToInt(head * (jag.main.Count - 1)), 1, jag.main.Count - 1);
                    for (int i = 0; i <= last; i++) run.Add(jag.main[i]);
                }
                Dyn("CrackGlow", AddHDR(2.2f), Order - 22, life, false, (rib, u) =>
                {
                    Part(KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.16f)));
                    Color c = u < 0.07f ? HotWhite : Crimson.WithA(Mathf.Pow(1f - u, 1.6f));
                    PolyStrip(rib, run, w * 1.5f, w * 0.5f, c, true);
                });
                Dyn("CrackResidue", MeshKit.Alpha, Order - 26, life * 1.7f, false, (rib, u) =>
                {
                    Part(KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u * 1.7f / 0.16f)));
                    PolyStrip(rib, run, w * 2.4f, w, BloodBlack.WithA(0.7f * Mathf.Pow(1f - u, 0.8f)), true);
                });
            }
        }

        // ------------------------------------------------------------------------------------------------ strikes
        /// <summary>
        /// A bolt from <paramref name="top"/> (a cloud's base) onto <paramref name="ground"/>: the stroke with forks, a light column, a contact star;
        /// from power 1 a thinner branch landing beside it 3 frames later, from power 2 a re-strike on a new path; running ground cracks, the ground
        /// ellipse and a white inner ring, sparks thrown up and out, dust both ways, a scorch, rising ash, the backdrop lit and the camera kicked.
        /// <paramref name="feedback"/> scales the screen feedback alone (shake, flashes), for several strikes landing together.
        /// </summary>
        public void ThunderStrike(Vector2 ground, Vector2 top, float scale = 1f, int power = 1, float feedback = 1f)
        {
            power = Mathf.Clamp(power, 0, 3);
            float h = Mathf.Max(1f, top.y - ground.y);
            float pw = 1f + 0.18f * power;
            Bolt(top, ground, 0.072f * scale * pw, 0.46f, Crimson, 22, h * 0.05f, 2 + power);
            LightPillar(ground, top, 0.42f * scale * pw, 0.46f, new Color(1f, 0.35f, 0.4f, 0.9f));
            Star(ground + Vector2.up * 0.25f, 0.95f * scale * pw, Color.white, Crimson, 0.1f, 8, true, Order + 62);
            if (power >= 1)
            {
                Vector2 bt = top + new Vector2(Random.Range(-0.7f, 0.7f), 0f);
                Vector2 bg = ground + new Vector2(Random.Range(0.35f, 0.75f) * (Random.value < 0.5f ? -1f : 1f), 0f);
                After(0.05f, () => { Bolt(bt, bg, 0.04f * scale, 0.32f, Crimson, 16, h * 0.05f, 1); Scorch(bg, 0.35f * scale, 0.6f); });
            }
            if (power >= 2)
            {
                Vector2 rt = top + new Vector2(Random.Range(-0.3f, 0.3f), 0f), rg = ground + Vector2.right * Random.Range(-0.15f, 0.15f);
                After(0.1f, () => Bolt(rt, rg, 0.05f * scale, 0.3f, Crimson, 18, h * 0.06f, 2));
            }
            GroundCracks(ground, (1.1f + 0.35f * power) * scale, 4 + power * 2, 0.6f + 0.1f * power);
            Ring(ground + Vector2.up * 0.03f, 0.1f, (1.3f + 0.3f * power) * scale, 0.1f, Crimson, 0.36f, false, 0.2f, 2.2f);
            Ring(ground + Vector2.up * 0.03f, 0.05f, (0.7f + 0.15f * power) * scale, 0.05f, Color.white, 0.2f, false, 0.2f, 1.6f);
            int sparks = 14 + 6 * power;
            for (int i = 0; i < sparks; i++)
            {
                float ang = Random.Range(20f, 160f);
                Emit(_sparks, ground + Vector2.up * 0.05f, KMath.Dir(ang) * Random.Range(2.5f, 8f) * scale, Random.Range(0.025f, 0.05f), Random.Range(0.3f, 0.65f),
                    i % 3 == 0 ? Color.white : Crimson);
            }
            Dust(ground, 0.9f + 0.2f * power, 1f);
            Dust(ground, 0.9f + 0.2f * power, -1f);
            Scorch(ground, 0.8f * scale * pw);
            for (int i = 0; i < 3 + power; i++)
                Emit(_embers, ground + new Vector2(Random.Range(-0.5f, 0.5f), 0.1f), new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.8f, 1.8f)), 0.04f,
                    Random.Range(0.5f, 0.9f), i % 2 == 0 ? HotWhite : Crimson);
            BackdropFlash((0.1f + 0.05f * power) * Mathf.Max(0.5f, feedback), 0.42f);
            GameFeel.Shake((0.3f + 0.08f * power) * scale * feedback);
            GameFeel.Flash(new Color(1f, 0.55f, 0.55f, (0.1f + 0.03f * power) * feedback), 0.12f);
        }

        /// <summary>
        /// Lightning on one of Raikon's landed blows, by phase: short arcs thrown out of the contact along the blow (a few against it), a crimson star;
        /// from power 1 a ring, from power 2 a crackle round the struck body. On unscaled time, so it blooms through the hit-stop. It sits on the
        /// ordinary hit burst and never replaces it.
        /// </summary>
        public void ThunderImpact(Vector2 p, Vector2 dir, int power, bool heavy)
        {
            power = Mathf.Clamp(power, 0, 3);
            float k = (1f + 0.25f * power) * (heavy ? 1.3f : 1f);
            float a0 = dir.sqrMagnitude > 1e-6f ? KMath.Ang(dir) : 0f;
            int n = 2 + power + (heavy ? 2 : 0);
            for (int i = 0; i < n; i++)
            {
                Vector2 d = KMath.Dir(a0 + Random.Range(-75f, 75f) + (i % 4 == 3 ? 180f : 0f));
                float len = Random.Range(0.3f, 0.6f) * k;
                Bolt(p, p + d * len, 0.02f * k, Random.Range(0.12f, 0.2f), Crimson, 6, len * 0.22f, 0, true, Order + 56);
            }
            Star(p, 0.45f * k, Color.white, Crimson, 0.09f, 6, true, Order + 62);
            if (power >= 1) Ring(p, 0.05f, 0.5f * k, 0.05f, Crimson, 0.2f, true, 1f, 2.2f);
            if (power >= 2) Crackle(p, 0.35f * k, 0.5f * k, 2 + power);
            for (int i = 0; i < 6 + 4 * power; i++)
                Emit(_sparks, p, KMath.Dir(a0 + Random.Range(-60f, 60f)) * Random.Range(3f, 8f) * k, Random.Range(0.025f, 0.045f), Random.Range(0.2f, 0.45f),
                    i % 3 == 0 ? Color.white : Crimson);
        }

        /// <summary>
        /// A slam's thunder (Kesa-Giri's cleave, Raijin Arc, the rush's slam, the flurry's dive, the Overload's release): the floor cracks open from the
        /// blade's foot; from power 1 short bolts erupt out of the cracks ahead of him (and fewer behind), from power 2 a ground ring and more of them.
        /// </summary>
        public void SlamThunder(Vector2 p, int power, float face)
        {
            power = Mathf.Clamp(power, 0, 3);
            GroundCracks(p, 0.7f + 0.4f * power, 3 + 2 * power, 0.55f + 0.1f * power);
            Star(p + Vector2.up * 0.1f, 0.55f + 0.15f * power, Color.white, Crimson, 0.09f, 6, true, Order + 62);
            for (int i = 0; i < power * 2; i++)
            {
                float side = i % 3 == 2 ? -0.6f : 1f;
                Vector2 at = p + new Vector2(face * side * Random.Range(0.25f, 0.7f + 0.35f * power), 0f);
                float hgt = Random.Range(0.45f, 0.95f) + 0.2f * power;
                Bolt(at + new Vector2(Random.Range(-0.15f, 0.15f), hgt), at, 0.035f, Random.Range(0.2f, 0.3f), Crimson, 8, 0.08f, 1);
            }
            if (power >= 2) Ring(p + Vector2.up * 0.03f, 0.1f, 1.5f + 0.3f * power, 0.08f, Crimson, 0.32f, false, 0.2f, 2.2f);
            for (int i = 0; i < 8 + 5 * power; i++)
                Emit(_sparks, p + Vector2.up * 0.04f, new Vector2(Random.Range(-2.5f, 2.5f) + face * 1.5f, Random.Range(1.5f, 5f)), 0.03f, Random.Range(0.25f, 0.5f),
                    i % 3 == 0 ? Color.white : Crimson);
            GameFeel.Shake(0.12f + 0.06f * power);
        }
    }
}
