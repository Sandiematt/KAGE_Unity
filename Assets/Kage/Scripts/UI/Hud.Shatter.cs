using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// The screen breaking like glass (Earthsplitter, docs/weapon-styles.md; the owner: "the entire screen should break like glass after the
    /// opponent has been killed"):
    /// <list type="bullet">
    /// <item><see cref="CrackScreen"/>: a star of cracks spreads round the impact in a few frames, glints, and fades. Nothing is captured:
    /// the fight goes on under it. (The smash's warning of what is coming.)</item>
    /// <item><see cref="ShatterScreen"/>: the frame is captured (the whole screen, the HUD with it), the web races over all of it and the
    /// frame shivers with the cracks lit for <see cref="GlassBreakAt"/>, then it breaks: the shards fall away under gravity, turning and
    /// tumbling, their edges catching the light, and the fight shows through behind them.</item>
    /// </list>
    /// The web is radial: kinked spokes from the impact and bowed, broken rings round it, so the shards near the impact are small and the
    /// outer ones large, as real glass breaks. Both run on unscaled time on their own overlay canvas above the HUD.
    /// </summary>
    public partial class Hud
    {
        RectTransform _glass;
        GlassFx _glassFx;

        /// <summary>The shatter (or the crack web) is on screen.</summary>
        public bool GlassShowing => _glassFx != null && _glassFx.isActiveAndEnabled && _glassFx.Playing;
        /// <summary>True while the screen is broken into falling shards (not just cracked).</summary>
        public bool Shattering => GlassShowing && _glassFx.Shatter;

        /// <summary>Seconds from the start of a shatter to the break (the cracks lit and the frame held until then).</summary>
        public const float GlassBreakAt = 0.42f;

        /// <summary>Cracks the screen round <paramref name="world"/> (a point in the fight), the cracks lit in <paramref name="tint"/>.</summary>
        public void CrackScreen(Vector2 world, Color tint)
        {
            EnsureGlass();
            if (_glassFx.Playing && _glassFx.Shatter) return;           // a shatter is stronger than a crack
            _glassFx.Begin(GlassPoint(world), tint, null, false, false);
        }

        /// <summary>Captures the screen and shatters it from <paramref name="world"/>.</summary>
        public void ShatterScreen(Vector2 world, Color tint)
        {
            EnsureGlass();
            Vector2 at = GlassPoint(world);
            // a camera drawing into a texture (the review harness: the HUD is a camera canvas, drawn into it too) holds the last frame
            // already: take it now. The end-of-frame wait never comes back in a batch-mode Editor.
            if (_cam != null && _cam.targetTexture != null) { _glassFx.Begin(at, tint, Capture(), false, true); return; }
            StartCoroutine(ShatterAtEndOfFrame(at, tint));
        }

        RenderTexture Capture()
        {
            var src = _cam.targetTexture;
            var rt = new RenderTexture(src.width, src.height, 0, RenderTextureFormat.ARGB32) { wrapMode = TextureWrapMode.Clamp };
            Graphics.Blit(src, rt);
            return rt;
        }

        Vector2 GlassPoint(Vector2 world)
        {
            if (_cam == null) return Vector2.zero;
            var canvas = GetComponent<Canvas>();
            Camera uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector3 sp = _cam.WorldToScreenPoint(world);
            if (_cam.targetTexture != null && uiCam == null)
                sp = new Vector3(sp.x / _cam.targetTexture.width * Screen.width, sp.y / _cam.targetTexture.height * Screen.height, sp.z);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, sp, uiCam, out var lp);
            return lp;
        }

        void EnsureGlass()
        {
            if (_glass != null) return;
            var go = new GameObject("Glass", typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(_root, false);
            _glass = (RectTransform)go.transform;
            Stretch(_glass);
            var c = go.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = 30000;                   // above every HUD panel, the stamp and the flash (and, on a camera canvas, the fighters)
            var back = MakeImage("Back", _glass, new Color(0f, 0f, 0f, 0f));
            Stretch(back.rectTransform);
            _glassFx = go.AddComponent<GlassFx>();
            _glassFx.Init(_glass, back);
        }

        IEnumerator ShatterAtEndOfFrame(Vector2 at, Color tint)
        {
            yield return new WaitForEndOfFrame();
            if (_cam != null && _cam.targetTexture != null) { _glassFx.Begin(at, tint, Capture(), false, true); yield break; }
            var rt = new RenderTexture(Mathf.Max(64, Screen.width), Mathf.Max(64, Screen.height), 0, RenderTextureFormat.ARGB32) { wrapMode = TextureWrapMode.Clamp };
            ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
            bool flip = SystemInfo.graphicsUVStartsAtTop;          // the captured back buffer comes in upside down there
            _glassFx.Begin(at, tint, rt, flip, true);
        }

        /// <summary>The overlay: the black behind the shards, the shards (textured with the captured frame) and the crack web over them.</summary>
        sealed class GlassFx : MonoBehaviour
        {
            // a crack: a local star of cracks round the impact that spreads, glints and fades; a shatter: the web over the whole screen
            const float CrackReach = 640f, CrackDur = 0.8f, ShatterDur = 2.05f;

            RectTransform _root;
            Image _back;
            CrackGraphic _cracks;
            readonly List<ShardGraphic> _shards = new();
            RenderTexture _rt;
            float _t, _dur, _maxReach;
            public bool Playing => _t < _dur;
            public bool Shatter { get; private set; }

            public void Init(RectTransform root, Image back)
            {
                _root = root;
                _back = back;
                // the graphics are nested classes: Unity has no script asset to read Graphic's [RequireComponent] from, so the
                // CanvasRenderer is added here (without it they draw nothing, silently)
                var crackGo = new GameObject("Cracks", typeof(RectTransform), typeof(CanvasRenderer));
                crackGo.transform.SetParent(root, false);
                Stretch((RectTransform)crackGo.transform);
                _cracks = crackGo.AddComponent<CrackGraphic>();
                _cracks.raycastTarget = false;
                _t = _dur = 0f;
                gameObject.SetActive(false);
            }

            public void Begin(Vector2 at, Color tint, RenderTexture captured, bool flip, bool shatter)
            {
                Clear();
                gameObject.SetActive(true);
                transform.SetAsLastSibling();
                _t = 0f;
                Shatter = shatter && captured != null;
                _dur = Shatter ? ShatterDur : CrackDur;
                _maxReach = Shatter ? 99999f : CrackReach;
                _rt = captured;
                var rect = _root.rect;
                var web = Web.Make(at, rect, Random.Range(0, 100000), _maxReach);
                _cracks.Set(web.segments, tint, _shards);
                _back.transform.SetAsFirstSibling();
                if (Shatter)
                {
                    foreach (var cell in web.cells)
                    {
                        var go = new GameObject("Shard", typeof(RectTransform), typeof(CanvasRenderer));
                        go.transform.SetParent(_root, false);
                        var r = (RectTransform)go.transform;
                        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                        r.sizeDelta = Vector2.zero;
                        var g = go.AddComponent<ShardGraphic>();
                        g.raycastTarget = false;
                        g.Set(cell, rect, captured, flip, at);
                        _shards.Add(g);
                    }
                    _cracks.transform.SetAsLastSibling();
                }
                Tick(0f);
            }

            void Clear()
            {
                foreach (var s in _shards) if (s != null) Destroy(s.gameObject);
                _shards.Clear();
                if (_rt != null) { _rt.Release(); Destroy(_rt); _rt = null; }
            }

            void Update() => Tick(UiDeltaTime);

            void Tick(float dt)
            {
                if (!Playing) { if (gameObject.activeSelf) { Clear(); gameObject.SetActive(false); } return; }
                _t += dt;
                float t = _t;
                if (Shatter)
                {
                    // the web races over the screen in 0.14 s, the frame shivers with the cracks lit, then it breaks: the shards fall
                    // away turning, their edges catching the light, and the black behind them gives way to the fight
                    float reach = KMath.Apply(Ease.OutCubic, Mathf.Clamp01(t / 0.14f)) * 2800f;
                    float since = t - GlassBreakAt;
                    float webA = since < 0f ? 1f : Mathf.Clamp01(1f - since / 0.12f);
                    float shiver = t > 0.14f && since < 0f ? Mathf.Sin(t * 150f) * 2.2f * Mathf.Clamp01(t / 0.3f) : 0f;
                    foreach (var s in _shards) s.Step(since, dt, shiver);
                    _cracks.Show(reach, webA, t, since >= 0f);
                    float backA = since < 0f ? 0f : Mathf.Clamp01(1f - (since - 0.1f) / 0.7f) * 0.92f;
                    _back.color = new Color(0f, 0f, 0f, backA);
                }
                else
                {
                    // a crack: out from the impact in 0.09 s, a glint along it, held a moment, then gone
                    float reach = KMath.Apply(Ease.OutCubic, Mathf.Clamp01(t / 0.09f)) * _maxReach;
                    float a = t < 0.22f ? 1f : Mathf.Clamp01(1f - (t - 0.22f) / (CrackDur - 0.22f));
                    _cracks.Show(reach, a * a, t, false);
                    _back.color = new Color(0f, 0f, 0f, 0f);
                }
                if (!Playing) { Clear(); gameObject.SetActive(false); }
            }

            void OnDestroy() => Clear();
        }

        /// <summary>
        /// The radial web, as glass breaks: spokes out of the impact that kink a little at every ring, and rings between them that bow and
        /// are broken off here and there further out, close together at the impact (small shards) and far apart at the edges (large ones).
        /// Each cell's outline runs through the same kinks, so the shards break along the cracks.
        /// </summary>
        sealed class Web
        {
            public readonly List<(Vector2 a, Vector2 b, float d)> segments = new();
            public readonly List<Vector2[]> cells = new();

            static readonly float[] Rings = { 40f, 95f, 170f, 270f, 400f, 580f, 820f, 1150f, 1650f, 2400f };

            public static Web Make(Vector2 at, Rect rect, int seed, float maxReach)
            {
                var w = new Web();
                var rnd = new System.Random(seed);
                float R() => (float)rnd.NextDouble();
                float S() => R() * 2f - 1f;
                const int spokes = 14;
                int rings = Rings.Length;
                var ang = new float[spokes];
                for (int i = 0; i < spokes; i++) ang[i] = (i + S() * 0.32f) / spokes * Mathf.PI * 2f;
                var p = new Vector2[spokes, rings];
                for (int i = 0; i < spokes; i++)
                    for (int j = 0; j < rings; j++)
                    {
                        float a = ang[i] + S() * 0.07f;                       // the spoke kinks at every ring
                        p[i, j] = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Rings[j] * (0.84f + 0.32f * R());
                    }
                Vector2 Kink(Vector2 a, Vector2 b, float amount)
                {
                    Vector2 d = b - a;
                    return (a + b) * 0.5f + new Vector2(-d.y, d.x) * (S() * amount);
                }
                // a kink in each spoke piece and each ring piece
                var s0 = new Vector2[spokes];
                var sm = new Vector2[spokes, rings];
                var rm = new Vector2[spokes, rings];
                for (int i = 0; i < spokes; i++)
                {
                    int n = (i + 1) % spokes;
                    s0[i] = Kink(at, p[i, 0], 0.07f);
                    for (int j = 0; j < rings; j++)
                    {
                        rm[i, j] = Kink(p[i, j], p[n, j], 0.1f);
                        if (j + 1 < rings) sm[i, j] = Kink(p[i, j], p[i, j + 1], 0.09f);
                    }
                }
                Rect keep = new(rect.xMin - 160f, rect.yMin - 160f, rect.width + 320f, rect.height + 320f);
                bool On(params Vector2[] q) { foreach (var v in q) if (keep.Contains(v)) return true; return false; }
                void Crack(Vector2 a, Vector2 b, Vector2 c)
                {
                    float d = Mathf.Min((a - at).magnitude, (c - at).magnitude);
                    if (d > maxReach || !On(a, b, c)) return;
                    w.segments.Add((a, b, d));
                    w.segments.Add((b, c, d));
                }
                for (int i = 0; i < spokes; i++)
                {
                    int n = (i + 1) % spokes;
                    Crack(at, s0[i], p[i, 0]);
                    if (On(at, p[i, 0], p[n, 0])) w.cells.Add(new[] { at, s0[i], p[i, 0], rm[i, 0], p[n, 0], s0[n] });
                    for (int j = 0; j < rings; j++)
                    {
                        // the rings are whole round the impact and broken off here and there further out
                        if (j < 2 || R() < 0.7f) Crack(p[i, j], rm[i, j], p[n, j]);
                        if (j + 1 >= rings) continue;
                        Crack(p[i, j], sm[i, j], p[i, j + 1]);
                        if (On(p[i, j], p[n, j], p[n, j + 1], p[i, j + 1]))
                            w.cells.Add(new[] { p[i, j], rm[i, j], p[n, j], sm[n, j], p[n, j + 1], rm[i, j + 1], p[i, j + 1], sm[i, j] });
                    }
                }
                return w;
            }
        }

        /// <summary>
        /// The crack web (thin white-hot lines with a softer tinted glow, drawn out to <c>reach</c> from the impact, a glint running out
        /// along it) and, after the break, the bright rims of the falling shards (glass catching the light as each one turns edge-on).
        /// </summary>
        [AddComponentMenu("")]
        sealed class CrackGraphic : MaskableGraphic
        {
            List<(Vector2 a, Vector2 b, float d)> _segs = new();
            List<ShardGraphic> _shards;
            Color _tint;
            float _reach, _alpha, _t;
            bool _rims;

            public void Set(List<(Vector2 a, Vector2 b, float d)> segs, Color tint, List<ShardGraphic> shards) { _segs = segs; _tint = tint; _shards = shards; SetVerticesDirty(); }
            public void Show(float reach, float alpha, float t, bool rims) { _reach = reach; _alpha = alpha; _t = t; _rims = rims; SetVerticesDirty(); }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                Vector2 off = rectTransform.rect.center;
                if (_alpha > 0.001f)
                {
                    var core = Color.Lerp(Color.white, _tint, 0.12f);
                    foreach (var (a, b, d) in _segs)
                    {
                        if (d > _reach) continue;
                        float near = Mathf.Clamp01(1f - d / 1300f);
                        float glint = Mathf.Clamp01(1f - Mathf.Abs(_t * 2600f - d - 150f) / 240f);      // a glint running out along the web
                        Line(vh, a - off, b - off, 8f + 8f * near, _tint.WithA(0.3f * _alpha * (0.45f + 0.55f * near)));
                        Line(vh, a - off, b - off, 2f + 2.2f * near + 1.5f * glint, core.WithA(_alpha * (0.7f + 0.3f * glint)));
                    }
                }
                if (_rims && _shards != null)
                    foreach (var s in _shards)
                    {
                        if (s == null || s.Alpha <= 0.01f) continue;
                        var pts = s.Outline();
                        float lit = s.Glint;
                        var rim = Color.Lerp(_tint, Color.white, 0.55f + 0.45f * lit).WithA(s.Alpha * (0.4f + 0.55f * lit));
                        for (int i = 0; i < pts.Length; i++) Line(vh, pts[i] - off, pts[(i + 1) % pts.Length] - off, 1.8f + 2.2f * lit, rim);
                    }
            }

            static void Line(VertexHelper vh, Vector2 a, Vector2 b, float w, Color c)
            {
                Vector2 d = b - a;
                if (d.sqrMagnitude < 1e-4f) return;
                Vector2 n = new Vector2(-d.y, d.x).normalized * (w * 0.5f);
                int k = vh.currentVertCount;
                var v = UIVertex.simpleVert;
                v.color = c;
                v.position = a + n; vh.AddVert(v);
                v.position = a - n; vh.AddVert(v);
                v.position = b - n; vh.AddVert(v);
                v.position = b + n; vh.AddVert(v);
                vh.AddTriangle(k, k + 1, k + 2);
                vh.AddTriangle(k, k + 2, k + 3);
            }
        }

        /// <summary>
        /// One shard: a polygon of the captured frame (each vertex's UV is where it sat on the screen) that falls, turns and tumbles (its
        /// width swinging with the turn about its own axis, as a flat piece of glass does) and fades.
        /// </summary>
        [AddComponentMenu("")]
        sealed class ShardGraphic : MaskableGraphic
        {
            Vector2[] _local, _uv, _outline;
            Texture _tex;
            Vector2 _home, _pos, _vel;
            float _rot, _spin, _tumble, _tumbleSpeed, _scale = 1f;

            public float Alpha { get; private set; } = 1f;
            /// <summary>0..1: how much the shard is catching the light (most when it turns edge-on).</summary>
            public float Glint => Mathf.Pow(1f - Mathf.Abs(Mathf.Cos(_tumble)), 2f);

            public override Texture mainTexture => _tex != null ? _tex : s_WhiteTexture;

            public void Set(Vector2[] cell, Rect rect, Texture tex, bool flip, Vector2 impact)
            {
                _tex = tex;
                Vector2 c = Vector2.zero;
                foreach (var v in cell) c += v;
                c /= cell.Length;
                _home = _pos = c;
                _local = new Vector2[cell.Length];
                _uv = new Vector2[cell.Length];
                _outline = new Vector2[cell.Length];
                for (int i = 0; i < cell.Length; i++)
                {
                    _local[i] = cell[i] - c;
                    float u = (cell[i].x - rect.xMin) / rect.width, v = (cell[i].y - rect.yMin) / rect.height;
                    _uv[i] = new Vector2(u, flip ? 1f - v : v);
                }
                Vector2 away = c - impact;
                float dist = away.magnitude;
                Vector2 dir = dist > 1f ? away / dist : Random.insideUnitCircle.normalized;
                // the pieces at the impact burst out hardest; the far ones mostly drop
                _vel = dir * Random.Range(220f, 640f) * Mathf.Lerp(1.7f, 0.5f, Mathf.Clamp01(dist / 1400f)) + Vector2.up * Random.Range(60f, 340f);
                _spin = Random.Range(50f, 280f) * (Random.value < 0.5f ? -1f : 1f);
                _tumbleSpeed = Random.Range(3f, 9f) * (Random.value < 0.5f ? -1f : 1f);
                _tumble = 0f;
                rectTransform.anchoredPosition = _pos;
                SetVerticesDirty();
            }

            /// <summary><paramref name="since"/>: seconds since the break (negative before it).</summary>
            public void Step(float since, float dt, float shiver)
            {
                if (since < 0f)
                {
                    rectTransform.anchoredPosition = _home + new Vector2(shiver, -shiver * 0.6f);
                    return;
                }
                _vel.y -= 2400f * dt;
                _pos += _vel * dt;
                _rot += _spin * dt;
                _tumble += _tumbleSpeed * dt;
                _scale = Mathf.Lerp(1f, 0.84f, Mathf.Clamp01(since / 1.2f));
                Alpha = Mathf.Clamp01(1f - (since - 0.85f) / 0.6f);
                rectTransform.anchoredPosition = _pos;
                rectTransform.localRotation = Quaternion.Euler(0f, 0f, _rot);
                rectTransform.localScale = new Vector3(_scale * Mathf.Cos(_tumble), _scale, 1f);
                color = new Color(1f, 1f, 1f, Alpha);
            }

            /// <summary>The shard's outline where it is now, in the glass overlay's space.</summary>
            public Vector2[] Outline()
            {
                var q = Quaternion.Euler(0f, 0f, _rot);
                var sc = rectTransform.localScale;
                for (int i = 0; i < _local.Length; i++)
                    _outline[i] = _pos + (Vector2)(q * new Vector3(_local[i].x * sc.x, _local[i].y * sc.y, 0f));
                return _outline;
            }

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                if (_local == null || _local.Length < 3) return;
                var v = UIVertex.simpleVert;
                v.color = Color.white;
                Vector2 uvc = Vector2.zero;
                foreach (var u in _uv) uvc += u;
                uvc /= _uv.Length;
                v.position = Vector3.zero; v.uv0 = uvc; vh.AddVert(v);
                for (int i = 0; i < _local.Length; i++) { v.position = _local[i]; v.uv0 = _uv[i]; vh.AddVert(v); }
                for (int i = 0; i < _local.Length; i++) vh.AddTriangle(0, 1 + i, 1 + (i + 1) % _local.Length);
            }
        }
    }
}
