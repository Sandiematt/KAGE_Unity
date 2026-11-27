using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// Visual identity for each of the five helmet rarity tiers (Common, Rare, Epic, Trophy, Legendary):
    /// colours, border styling, glows, ambient particles, pulsing highlights, selection burst and subtle UI audio.
    /// </summary>
    public sealed class HelmetTierStyle
    {
        public readonly HelmetTier Tier;
        public readonly string Id;
        public readonly string Label;
        public readonly Color Primary;
        public readonly Color Secondary;
        public readonly Color Highlight;
        public readonly Color BgTop;
        public readonly Color BgBottom;
        public readonly Color InnerBorder;
        public readonly Color GlowColor;
        public readonly float GlowAlpha;
        public readonly bool HasParticles;
        public readonly bool HasRadialBackGlow;
        public readonly bool HasNotchedFrame;
        public readonly bool AllowTopBreakout;

        HelmetTierStyle(HelmetTier tier, string id, string label, string primary, string secondary, string highlight,
            string bgTop, string bgBottom, string innerBorder, string glowColor, float glowAlpha,
            bool particles, bool radialGlow, bool notched, bool breakout)
        {
            Tier = tier;
            Id = id;
            Label = label;
            Primary = Ui.Hex(primary);
            Secondary = Ui.Hex(secondary);
            Highlight = Ui.Hex(highlight);
            BgTop = Ui.Hex(bgTop);
            BgBottom = Ui.Hex(bgBottom);
            InnerBorder = Ui.Hex(innerBorder);
            GlowColor = Ui.Hex(glowColor);
            GlowAlpha = glowAlpha;
            HasParticles = particles;
            HasRadialBackGlow = radialGlow;
            HasNotchedFrame = notched;
            AllowTopBreakout = breakout;
        }

        public static readonly HelmetTierStyle Common = new HelmetTierStyle(
            HelmetTier.Common, "common", "COMMON",
            "#9CA3AF", "#6B7280", "#E5E7EB",
            "#1B1817", "#110F0E", "#4A4540", "#9CA3AF", 0f,
            particles: false, radialGlow: false, notched: false, breakout: false);

        public static readonly HelmetTierStyle Rare = new HelmetTierStyle(
            HelmetTier.Rare, "rare", "RARE",
            "#38BDF8", "#0284C7", "#BAE6FD",
            "#121C26", "#0B1016", "#1E4B6E", "#38BDF8", 0.28f,
            particles: false, radialGlow: false, notched: false, breakout: false);

        public static readonly HelmetTierStyle Epic = new HelmetTierStyle(
            HelmetTier.Epic, "epic", "EPIC",
            "#A855F7", "#7C3AED", "#E9D5FF",
            "#1C1326", "#0F0A15", "#5B218F", "#A855F7", 0.36f,
            particles: true, radialGlow: true, notched: false, breakout: false);

        public static readonly HelmetTierStyle Trophy = new HelmetTierStyle(
            HelmetTier.Trophy, "trophy", "TROPHY",
            "#EAB308", "#CA8A04", "#FEF08A",
            "#21190E", "#120D08", "#7D5618", "#EAB308", 0.40f,
            particles: true, radialGlow: true, notched: false, breakout: true);

        public static readonly HelmetTierStyle Legendary = new HelmetTierStyle(
            HelmetTier.Legendary, "legendary", "LEGENDARY",
            "#F97316", "#DC2626", "#FEF08A",
            "#26100A", "#130806", "#9A3412", "#F97316", 0.48f,
            particles: true, radialGlow: true, notched: true, breakout: true);

        public static readonly HelmetTierStyle[] All = { Common, Rare, Epic, Trophy, Legendary };

        public static HelmetTierStyle For(string tierId)
        {
            switch (tierId)
            {
                case "rare": return Rare;
                case "epic": return Epic;
                case "trophy": return Trophy;
                case "legendary": return Legendary;
                default: return Common;
            }
        }

        public static HelmetTierStyle For(HelmetTier tier)
        {
            switch (tier)
            {
                case HelmetTier.Rare: return Rare;
                case HelmetTier.Epic: return Epic;
                case HelmetTier.Trophy: return Trophy;
                case HelmetTier.Legendary: return Legendary;
                default: return Common;
            }
        }

        /// <summary>Clean chamfered ceremonial frame polygon for Legendary cards (8-point miter-ready bevel, zero jagged spikes or cracked seams).</summary>
        public static Vector2[] NotchedFramePoints(float x, float y, float w, float h, float notch = 8f, float bevel = 11f)
        {
            return Ui.Chamfer(x, y, w, h, bevel);
        }
    }

    /// <summary>
    /// Subtle rising ambient embers inside Epic, Trophy and Legendary cards — clean crisp diamond motes without jagged lightning crackle arcs.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class HelmetCardAmbient : MonoBehaviour
    {
        struct Spark
        {
            public RectTransform rt;
            public Graphic graphic;
            public float x0, phase, speed, amp, size, baseAlpha;
        }

        HelmetTier tier;
        float width, height;
        readonly List<Spark> sparks = new List<Spark>();

        public static HelmetCardAmbient Attach(Transform parent, HelmetTierStyle style, float w, float h, int seed)
        {
            if (!style.HasParticles) return null;
            var clip = Ui.Node(parent, "AmbientFx", 4f, 4f, w - 8f, h - 8f);
            clip.gameObject.AddComponent<RectMask2D>();
            var fx = clip.gameObject.AddComponent<HelmetCardAmbient>();
            fx.Init(clip, style, w - 8f, h - 8f, seed);
            return fx;
        }

        void Init(RectTransform root, HelmetTierStyle style, float w, float h, int seed)
        {
            tier = style.Tier;
            width = w;
            height = h;
            var rng = new System.Random(seed * 7919 + (int)tier * 131 + 17);
            int count = tier == HelmetTier.Legendary ? 10 : tier == HelmetTier.Trophy ? 8 : 7;

            for (int i = 0; i < count; i++)
            {
                float sz = tier == HelmetTier.Legendary
                    ? (float)(2.4 + rng.NextDouble() * 2.8)
                    : (float)(2.0 + rng.NextDouble() * 2.4);
                Color col;
                if (tier == HelmetTier.Legendary)
                    col = i % 3 == 0 ? style.Highlight : i % 3 == 1 ? style.Primary : style.Secondary;
                else if (tier == HelmetTier.Trophy)
                    col = i % 2 == 0 ? style.Highlight : style.Primary;
                else
                    col = i % 3 == 0 ? style.Highlight : style.Primary;

                var d = Ui.Diamond(root, 0, 0, sz * 1.25f, col, col, 0f, true);
                sparks.Add(new Spark
                {
                    rt = d.rectTransform,
                    graphic = d,
                    x0 = (float)(0.10 + rng.NextDouble() * 0.80) * w,
                    phase = (float)rng.NextDouble(),
                    speed = tier == HelmetTier.Legendary
                        ? (float)(0.22 + rng.NextDouble() * 0.22)
                        : (float)(0.11 + rng.NextDouble() * 0.14),
                    amp = (float)(3.0 + rng.NextDouble() * 6.0),
                    size = sz,
                    baseAlpha = tier == HelmetTier.Legendary ? 0.68f : 0.56f,
                });
            }

            Tick(UiClock.Animate ? UiClock.Now : 1.35f);
        }

        void Update()
        {
            if (!UiClock.Animate) return;
            Tick(UiClock.Now);
        }

        void Tick(float t)
        {
            for (int i = 0; i < sparks.Count; i++)
            {
                var s = sparks[i];
                if (s.rt == null) continue;
                float u = Mathf.Repeat(s.phase + t * s.speed, 1f);
                float y = Mathf.Lerp(height * 0.94f, height * 0.06f, u);
                float x = s.x0 + Mathf.Sin(t * 2.2f + s.phase * 14f) * s.amp;
                s.rt.anchoredPosition = new Vector2(x, -y);
                float fade = Mathf.Sin(u * Mathf.PI);
                float twinkle = 0.65f + 0.35f * Mathf.Sin(t * 4.8f + s.phase * 25f);
                var c = s.graphic.color;
                c.a = s.baseAlpha * fade * twinkle;
                s.graphic.color = c;
            }
        }
    }

    /// <summary>Pulsing fiery outer highlight for Legendary cards when hovered or selected.</summary>
    [AddComponentMenu("")]
    public sealed class HelmetCardPulse : MonoBehaviour
    {
        Graphic target;
        Color baseColor;
        float baseAlpha;
        bool active;

        public static HelmetCardPulse Attach(Graphic graphic, Color col, float alpha, bool startActive)
        {
            var p = graphic.gameObject.AddComponent<HelmetCardPulse>();
            p.target = graphic;
            p.baseColor = col;
            p.baseAlpha = alpha;
            p.active = startActive;
            p.Apply(startActive ? alpha : 0f);
            return p;
        }

        public void SetActive(bool on)
        {
            active = on;
            if (!on) Apply(0f);
            else if (!UiClock.Animate) Apply(baseAlpha);
        }

        void Update()
        {
            if (target == null || !active) return;
            float wave = UiClock.Animate ? 0.65f + 0.35f * Mathf.Sin(UiClock.Now * 5.2f) : 1f;
            Apply(baseAlpha * wave);
        }

        void Apply(float a)
        {
            if (target == null) return;
            var c = baseColor;
            c.a = a;
            target.color = c;
        }
    }

    /// <summary>One-shot radial particle burst spawned on a helmet card when selected.</summary>
    [AddComponentMenu("")]
    public sealed class HelmetSelectBurst : MonoBehaviour
    {
        struct Particle
        {
            public RectTransform rt;
            public Graphic g;
            public Vector2 dir;
            public float speed, rotSpeed;
        }

        const float Duration = 0.48f;
        float born;
        readonly List<Particle> parts = new List<Particle>();

        public static void Spawn(Transform parent, Vector2 centreStagePx, HelmetTierStyle style)
        {
            if (parent == null || !UiClock.Animate) return;
            var root = Ui.Node(parent, "SelectBurst", centreStagePx.x, centreStagePx.y, 4f, 4f);
            var burst = root.gameObject.AddComponent<HelmetSelectBurst>();
            burst.born = UiClock.Now;
            const int n = 14;
            for (int i = 0; i < n; i++)
            {
                float ang = (i / (float)n) * Mathf.PI * 2f + (i % 2) * 0.14f;
                Color col = i % 3 == 0 ? style.Highlight : i % 3 == 1 ? style.Primary : style.Secondary;
                float size = (i % 2 == 0 ? 7f : 4.5f) * (style.Tier == HelmetTier.Legendary ? 1.25f : 1f);
                var d = Ui.Diamond(root, 0, 0, size, col, col, 0f, true);
                burst.parts.Add(new Particle
                {
                    rt = d.rectTransform,
                    g = d,
                    dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)),
                    speed = (i % 2 == 0 ? 155f : 105f) * (style.Tier >= HelmetTier.Trophy ? 1.2f : 1f),
                    rotSpeed = (i % 2 == 0 ? 180f : -220f),
                });
            }
        }

        void Update()
        {
            float elapsed = UiClock.Now - born;
            if (elapsed >= Duration)
            {
                Destroy(gameObject);
                return;
            }
            float t = elapsed / Duration;
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            float alpha = 1f - t * t;
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                if (p.rt == null) continue;
                Vector2 pos = p.dir * (p.speed * ease * 0.55f);
                p.rt.anchoredPosition = new Vector2(pos.x, -pos.y);
                p.rt.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.2f, t);
                p.rt.localRotation = Quaternion.Euler(0f, 0f, p.rotSpeed * elapsed);
                var c = p.g.color;
                c.a = alpha;
                p.g.color = c;
            }
        }
    }

    /// <summary>Synthesised subtle tier-pitched UI selection chime (no audio files required).</summary>
    public static class HelmetSelectSfx
    {
        const int Rate = 44100;
        static AudioSource source;
        static readonly Dictionary<HelmetTier, AudioClip> clips = new Dictionary<HelmetTier, AudioClip>();

        public static void Play(HelmetTier tier)
        {
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            try
            {
                if (source == null)
                {
                    var go = new GameObject("HelmetUiSfx");
                    Object.DontDestroyOnLoad(go);
                    source = go.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 0f;
                }
                if (!clips.TryGetValue(tier, out var clip) || clip == null)
                {
                    clip = Synthesize(tier);
                    clips[tier] = clip;
                }
                source.PlayOneShot(clip, tier == HelmetTier.Legendary ? 0.24f : 0.17f);
            }
            catch
            {
                // Headless or audio-disabled environment: ignore safely
            }
        }

        static AudioClip Synthesize(HelmetTier tier)
        {
            float dur = tier == HelmetTier.Legendary ? 0.34f : tier == HelmetTier.Trophy ? 0.28f : 0.20f;
            int n = (int)(dur * Rate);
            var data = new float[n];
            float f0 = tier switch
            {
                HelmetTier.Common => 520f,
                HelmetTier.Rare => 740f,
                HelmetTier.Epic => 880f,
                HelmetTier.Trophy => 1046f,
                _ => 660f,
            };
            float f1 = tier == HelmetTier.Legendary ? f0 * 1.5f : f0 * 1.25f;
            float f2 = tier == HelmetTier.Legendary ? f0 * 2.0f : f0 * 2.5f;
            float decay = tier >= HelmetTier.Trophy ? 9f : 15f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t * decay);
                float s = Mathf.Sin(2f * Mathf.PI * f0 * t)
                        + 0.45f * Mathf.Sin(2f * Mathf.PI * f1 * t) * Mathf.Exp(-t * 4f)
                        + 0.22f * Mathf.Sin(2f * Mathf.PI * f2 * t) * Mathf.Exp(-t * 8f);
                data[i] = s * env * 0.35f;
            }
            var c = AudioClip.Create("helmet_select_" + tier, n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
