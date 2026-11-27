using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// Layered Title-screen hero presenter: renders the feathered static scene (<c>ui_hero_bg</c>) completely still
    /// behind Kage, and animates ONLY the cut-out Kage character (<c>ui_hero_char</c>) with a subdivided Live2D mesh
    /// (heroic breathing, billowing crimson scarf, wind-swept hair tips, and flowing flared coat hem) plus character-only
    /// details (eye gleam &amp; natural blink, subtle katana pommel glint). Respects reduced motion (<see cref="UiClock.Animate"/>).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class TitleHeroLive : MonoBehaviour
    {
        // Exact landmark coordinates measured on ui_hero_char.png (866 x 1100), normalized (u: 0..1 left-right, v: 0..1 top-bottom)
        const float EyeU = 0.5902f, EyeV = 0.3723f;
        const float PommelU = 0.735f, PommelV = 0.548f;

        TitleHeroMesh charMesh;
        RectTransform eyeRoot, eyeLid, glintRt;
        RadialGlow eyeGlow;
        Graphic glintGfx;
        float width, height;

        public static RectTransform Build(Transform parent, string bgTexName, string charTexName, float x, float y, float w, float h)
        {
            var rt = Ui.Node(parent, "HeroLive", x, y, w, h);
            var live = rt.gameObject.AddComponent<TitleHeroLive>();
            var bgTex = Ui.Tex(bgTexName);
            var charTex = Ui.Tex(charTexName);
            live.Init(rt, bgTex, charTex != null ? charTex : Ui.Tex("ui_hero"), w, h);
            return rt;
        }

        void Init(RectTransform root, Texture2D bgTex, Texture2D charTex, float w, float h)
        {
            width = w;
            height = h;

            // 1. Static feathered background layer (moon, torii, hills, maple branch, lake — stays 100% still)
            if (bgTex != null)
            {
                bgTex.wrapMode = TextureWrapMode.Clamp;
                var bgRt = Ui.Node(root, "HeroBg", 0, 0, w, h);
                var bgImg = bgRt.gameObject.AddComponent<RawImage>();
                bgImg.texture = bgTex;
                bgImg.raycastTarget = false;
            }

            // 2. Animated character-only subdivided Live2D mesh (Kage + scarf only)
            if (charTex != null) charTex.wrapMode = TextureWrapMode.Clamp;
            var meshRt = Ui.Node(root, "HeroChar", 0, 0, w, h);
            charMesh = meshRt.gameObject.AddComponent<TitleHeroMesh>();
            charMesh.raycastTarget = false;
            charMesh.SetTexture(charTex);

            // 3. Live eye gleam + natural blink shutter synced with Kage's breathing
            eyeRoot = Ui.Node(root, "EyeFx", EyeU * w, EyeV * h, 24f, 24f);
            eyeRoot.pivot = new Vector2(0.5f, 0.5f);
            eyeRoot.anchoredPosition = new Vector2(EyeU * w, -EyeV * h);
            eyeGlow = Ui.Glow(eyeRoot, 12f, 12f, 14f, Ui.A(Color.white, 0.24f), 2.0f);
            eyeLid = Ui.Box(eyeRoot, 2f, 4f, 18f, 14f, Ui.Hex("#070606"), "BlinkLid").rectTransform;
            eyeLid.pivot = new Vector2(0.5f, 1f);
            eyeLid.anchoredPosition = new Vector2(12f, -5f);
            eyeLid.localRotation = Quaternion.Euler(0f, 0f, 14f);
            eyeLid.localScale = new Vector3(1f, 0f, 1f);

            // 4. Subtle metallic 4-point star glint on Kage's katana pommel
            var glintNode = Ui.Node(root, "PommelGlint", PommelU * w, PommelV * h, 20f, 20f);
            glintNode.pivot = new Vector2(0.5f, 0.5f);
            glintNode.anchoredPosition = new Vector2(PommelU * w, -PommelV * h);
            glintRt = glintNode;
            glintGfx = Ui.Shape(glintNode, 0, 0, 20, 20, Star4(10f, 10f, 9f, 2f), Ui.Hex("#fff3d1"), null, null, false, "Star");
            glintGfx.color = Color.clear;

            Tick(UiClock.Animate ? UiClock.Now : 0f, UiClock.Animate);
        }

        void Update()
        {
            if (charMesh == null) return;
            bool anim = UiClock.Animate;
            Tick(anim ? UiClock.Now : 0f, anim);
        }

        void Tick(float t, bool animate)
        {
            charMesh.SetTime(t, animate);

            // Keep eye & katana pommel synced to Kage's breathing offset
            float breath = animate ? 0.5f + 0.5f * Mathf.Sin(t * (Mathf.PI * 2f / 3.6f) - Mathf.PI * 0.5f) : 0f;
            float headDy = animate ? -4.2f * breath : 0f;
            float hipDy = animate ? -1.8f * breath : 0f;

            if (eyeRoot != null)
            {
                eyeRoot.anchoredPosition = new Vector2(EyeU * width, -(EyeV * height + headDy));
                float pulse = animate ? 0.18f + 0.16f * (0.5f + 0.5f * Mathf.Sin(t * 2.1f)) : 0.14f;
                float blink = 0f;
                if (animate)
                {
                    float cyc = Mathf.Repeat(t + 1.8f, 5.4f);
                    if (cyc < 0.15f) blink = Mathf.Sin(cyc / 0.15f * Mathf.PI);
                }
                eyeLid.localScale = new Vector3(1f, blink, 1f);
                eyeGlow.color = Ui.A(Color.white, pulse * (1f - 0.85f * blink));
            }

            if (glintRt != null)
            {
                glintRt.anchoredPosition = new Vector2(PommelU * width, -(PommelV * height + hipDy));
                float glint = 0f;
                if (animate)
                {
                    float cyc = Mathf.Repeat(t + 0.9f, 4.6f);
                    if (cyc < 0.55f) glint = Mathf.Sin(cyc / 0.55f * Mathf.PI);
                }
                glintRt.localRotation = Quaternion.Euler(0f, 0f, t * 28f);
                glintRt.localScale = Vector3.one * (0.45f + 0.65f * glint);
                glintGfx.color = Ui.A(Ui.Hex("#fff3d1"), 0.85f * glint);
            }
        }

        static Vector2[] Star4(float cx, float cy, float outer, float inner)
        {
            var pts = new Vector2[8];
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                float r = (i % 2 == 0) ? outer : inner;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }
    }

    /// <summary>
    /// Subdivided 56 x 72 Live2D mesh graphic for the cut-out Kage character (<c>ui_hero_char.png</c>):
    /// animates ONLY Kage (breathing, billowing crimson scarf, wind-swept hair spikes, and flowing flared coat hem)
    /// while his boots stay planted and the background layer behind him remains completely motionless.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class TitleHeroMesh : MaskableGraphic
    {
        const int Cols = 56, Rows = 72;
        const int VertCount = (Cols + 1) * (Rows + 1);

        [SerializeField] Texture2D tex;
        [SerializeField] float time;
        [SerializeField] bool animate;
        bool weightsReady;

        // Precomputed per-vertex normalized coords and smooth character-only weights
        readonly float[] vu = new float[VertCount];
        readonly float[] vv = new float[VertCount]; // y-down 0..1
        readonly float[] wBreath = new float[VertCount];
        readonly float[] wScarf = new float[VertCount];
        readonly float[] scarfDist = new float[VertCount];
        readonly float[] wHair = new float[VertCount];
        readonly float[] wCoat = new float[VertCount];

        public override Texture mainTexture { get { return tex != null ? tex : s_WhiteTexture; } }

        public void SetTexture(Texture2D t)
        {
            tex = t;
            if (!weightsReady) BuildWeights();
            SetAllDirty();
        }

        public void SetTime(float t, bool isAnimated)
        {
            if (!isAnimated && !animate) return;
            time = t;
            animate = isAnimated;
            SetVerticesDirty();
        }

        void BuildWeights()
        {
            weightsReady = true;
            for (int r = 0; r <= Rows; r++)
            {
                float y = r / (float)Rows; // 0 = top, 1 = bottom
                for (int c = 0; c <= Cols; c++)
                {
                    float u = c / (float)Cols; // 0 = left, 1 = right
                    int idx = r * (Cols + 1) + c;
                    vu[idx] = u;
                    vv[idx] = y;

                    // 1. Heroic upper-body breathing (head, collar, chest, shoulders, arms; pinned strictly to 0 at boots/lower legs)
                    // Ramps smoothly upward from waist (y=0.60) so the whole upper body breathes naturally as one
                    float upperBody = Smooth(0.66f, 0.44f, y);
                    float onTorso = Smooth(0.24f, 0.10f, Mathf.Abs(u - 0.56f));
                    wBreath[idx] = upperBody * onTorso;

                    // 2. Flowing crimson scarf tails (from collar knot (0.62, 0.435) out to trailing tips (0.955, 0.63))
                    float dA = DistSeg(u, y, 0.625f, 0.435f, 0.775f, 0.455f, out float tA);
                    float dB = DistSeg(u, y, 0.775f, 0.455f, 0.950f, 0.585f, out float tB);
                    float dC = DistSeg(u, y, 0.640f, 0.435f, 0.740f, 0.395f, out float tC);
                    float dScarf = Mathf.Min(dA, Mathf.Min(dB, dC));
                    float along = dA <= dB && dA <= dC ? tA * 0.48f
                                : dB <= dC ? 0.48f + tB * 0.52f
                                : tC * 0.38f;
                    float rightOfCollar = Smooth(0.610f, 0.675f, u);
                    float scarfBand = Smooth(0.115f, 0.030f, dScarf);
                    scarfDist[idx] = along;
                    wScarf[idx] = scarfBand * rightOfCollar * Mathf.Pow(Mathf.Clamp01(along * 1.15f), 0.85f);

                    // 3. Spiky hair wind rustle (u: 0.46..0.75, y: 0.23..0.37; pinned near face/eye)
                    float hx = (u - 0.595f) / 0.165f;
                    float hy = (y - 0.300f) / 0.085f;
                    float hField = Smooth(1.0f, 0.25f, Mathf.Sqrt(hx * hx + hy * hy));
                    float awayFromEye = Smooth(0.045f, 0.125f, Vector2.Distance(new Vector2(u, y), new Vector2(0.565f, 0.372f)));
                    wHair[idx] = hField * awayFromEye;

                    // 4. Flared kimono coat skirt & hanging crimson tassels (y: 0.56..0.89; pinned at waist & zero at boots)
                    float coatY = Smooth(0.555f, 0.69f, y) * Smooth(0.905f, 0.840f, y);
                    float halfW = Mathf.Lerp(0.12f, 0.30f, Mathf.Clamp01((y - 0.54f) / 0.30f));
                    float coatX = Smooth(halfW + 0.06f, halfW * 0.50f, Mathf.Abs(u - 0.535f));
                    float hemBias = 0.45f + 0.55f * Mathf.Clamp01((y - 0.58f) / 0.26f);
                    wCoat[idx] = coatY * coatX * hemBias;
                }
            }
        }

        static float Smooth(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        static float DistSeg(float px, float py, float ax, float ay, float bx, float by, out float proj)
        {
            float vx = bx - ax, vy = by - ay;
            float lenSq = vx * vx + vy * vy;
            proj = lenSq > 1e-6f ? Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / lenSq) : 0f;
            float cx = ax + vx * proj, cy = ay + vy * proj;
            float dx = px - cx, dy = py - cy;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!weightsReady) BuildWeights();

            Rect r = rectTransform.rect;
            float w = r.width, h = r.height;
            if (w <= 0.1f || h <= 0.1f) return;

            float t = animate ? time : 0f;
            float breath = animate ? 0.5f + 0.5f * Mathf.Sin(t * (Mathf.PI * 2f / 3.6f) - Mathf.PI * 0.5f) : 0f;
            float gust = animate ? 0.5f * Mathf.Sin(t * 0.85f) + 0.5f * Mathf.Sin(t * 1.45f + 1.1f) : 0f;
            Color32 col32 = color;

            var vert = UIVertex.simpleVert;
            vert.color = col32;

            for (int row = 0; row <= Rows; row++)
            {
                for (int col = 0; col <= Cols; col++)
                {
                    int idx = row * (Cols + 1) + col;
                    float u = vu[idx], y = vv[idx];
                    float dx = 0f, dy = 0f;

                    if (animate)
                    {
                        // Breathing: heroic chest/shoulder lift & subtle breadth expansion
                        float wb = wBreath[idx];
                        if (wb > 0.001f)
                        {
                            dy -= 4.2f * breath * wb;
                            dx += (u - 0.56f) * 12.0f * breath * wb;
                        }

                        // Scarf: silky travelling S-wave blowing freely across the static moon
                        float ws = wScarf[idx];
                        if (ws > 0.001f)
                        {
                            float ph = scarfDist[idx] * 9.0f - t * 3.2f;
                            dy += (Mathf.Sin(ph) * 12.5f + Mathf.Sin(ph * 1.85f + 0.8f) * 4.0f) * ws;
                            dx += (Mathf.Cos(ph * 0.85f) * 5.8f + gust * 6.5f) * ws;
                        }

                        // Hair spikes: crisp wind sway
                        float wh = wHair[idx];
                        if (wh > 0.001f)
                        {
                            dx += (Mathf.Sin(t * 2.4f - y * 15f) * 4.6f + gust * 2.8f) * wh;
                            dy += Mathf.Cos(t * 2.0f - u * 11f) * 2.6f * wh;
                        }

                        // Flared kimono coat hem & tassels: fluid cloth billowing
                        float wc = wCoat[idx];
                        if (wc > 0.001f)
                        {
                            float ph = t * 2.0f - y * 11.0f - u * 4.0f;
                            dx += (Mathf.Sin(ph) * 8.5f + gust * 4.5f) * wc;
                            dy += Mathf.Cos(ph * 0.9f + 0.5f) * 2.8f * wc;
                        }
                    }

                    float px = r.xMin + u * w + dx;
                    float py = r.yMax - (y * h + dy);
                    vert.position = new Vector3(px, py, 0f);
                    vert.uv0 = new Vector2(u, 1f - y);
                    vh.AddVert(vert);
                }
            }

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    int a = row * (Cols + 1) + col;
                    int b = a + 1;
                    int c = a + (Cols + 1);
                    int d = c + 1;
                    vh.AddTriangle(a, b, d);
                    vh.AddTriangle(a, d, c);
                }
            }
        }
    }
}
