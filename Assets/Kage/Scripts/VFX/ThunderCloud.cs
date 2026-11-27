using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// A storm cloud that gathers over a point before lightning falls out of it: every bolt from the sky in Raikon's fight comes from one
    /// (docs/raikon-ascendant-vfx.md, "Thunder pass"). It is the strike's anticipation (craft §2):
    /// <list type="bullet">
    /// <item><b>Gather</b>: puffs drawn in from wide and swelling into a lumpy band just inside the top of the view; it churns, and a crimson glow under
    /// it rises towards the strike, with flickers inside it that come faster as the strike nears and, from power 1, arcs crawling between its puffs.</item>
    /// <item><b>Strike</b> (<see cref="Strike"/>): the cloud flashes white-crimson on the stroke envelope (main strike, return stroke, after-flicker).</item>
    /// <item><b>Dissipation</b>: the puffs swell, drift apart and up, and thin out.</item>
    /// </list>
    /// Three value layers: a dark backing, the lit grey-red body with a highlight on its top, the additive under-glow. It follows a transform's x while
    /// it gathers (a sky bolt's ring that tracks Kage) and stops when it strikes; one that is never struck melts away on its own.
    /// </summary>
    [DefaultExecutionOrder(240)]
    public sealed class ThunderCloud : MonoBehaviour
    {
        static readonly List<ThunderCloud> Live = new();

        /// <summary>Sorting orders: behind the fighters (1000..1700) and their trails, in front of the backdrop and its floor.</summary>
        public const int OrderBack = 600, OrderBody = 601, OrderGlow = 602;
        const int MaxPuffs = 18;

        Ribbon _back, _body, _glow;
        Transform _follow;
        Fighter _owner;
        float _x, _w, _t, _form, _linger = 0.9f, _struckAt = -1f, _flickT, _flickLeft, _arcT;
        int _power, _n, _flickPuff = -1;
        bool _melt;
        readonly Vector2[] _home = new Vector2[MaxPuffs], _from = new Vector2[MaxPuffs], _at = new Vector2[MaxPuffs];
        readonly float[] _r = new float[MaxPuffs], _rNow = new float[MaxPuffs], _ph = new float[MaxPuffs], _spin = new float[MaxPuffs];

        /// <summary>True once lightning has left it.</summary>
        public bool Struck => _struckAt >= 0f;
        /// <summary>World x of its centre.</summary>
        public float X => _x;
        /// <summary>World y of its base, where its bolts start.</summary>
        public float BaseY { get; private set; }
        /// <summary>Its power (the boss phase it was gathered in, 0..3).</summary>
        public int Power => _power;

        /// <summary>
        /// Gathers a cloud over <paramref name="x"/>, <paramref name="width"/> metres wide, ready to strike after <paramref name="form"/> seconds.
        /// Null with no <see cref="Vfx"/> (headless).
        /// </summary>
        public static ThunderCloud Gather(float x, float width, float form, int power, Transform follow = null, Fighter owner = null, float linger = 0.9f)
        {
            var v = Vfx.I;
            if (v == null) return null;
            var go = new GameObject("ThunderCloud");
            go.transform.SetParent(v.transform, false);
            var c = go.AddComponent<ThunderCloud>();
            c.Init(v, x, width, form, power, follow, owner, linger);
            return c;
        }

        /// <summary>The nearest cloud within <paramref name="within"/> metres of <paramref name="x"/> that has not struck yet, or null.</summary>
        public static ThunderCloud Near(float x, float within = 1.8f)
        {
            ThunderCloud best = null;
            float bd = within;
            foreach (var c in Live)
            {
                if (c == null || c.Struck) continue;
                float d = Mathf.Abs(c._x - x);
                if (d <= bd) { bd = d; best = c; }
            }
            return best;
        }

        /// <summary>
        /// The cloud over <paramref name="x"/> that is waiting to strike, or a new one that forms in <paramref name="form"/> seconds; it strikes and returns
        /// the point under it a bolt towards <paramref name="x"/> starts from. Without a <see cref="Vfx"/>: a point 7.5 m up.
        /// </summary>
        public static Vector2 StrikeOver(float x, float groundY, int power, Fighter owner = null, float width = 2.2f, float form = 0.06f)
        {
            var c = Near(x);
            if (c == null) c = Gather(x, width, form, power, null, owner, 0.7f);
            return c != null ? c.Strike(x) : new Vector2(x, groundY + 7.5f);
        }

        void Init(Vfx v, float x, float width, float form, int power, Transform follow, Fighter owner, float linger)
        {
            _x = x; _w = Mathf.Max(1f, width); _form = Mathf.Max(0.05f, form); _power = Mathf.Clamp(power, 0, 3);
            _follow = follow; _owner = owner; _linger = Mathf.Max(0.2f, linger);
            _n = Mathf.Min(MaxPuffs, 8 + 3 * _power);
            for (int i = 0; i < _n; i++)
            {
                float s = i / (float)(_n - 1) * 2f - 1f;
                float dome = 1f - s * s;
                _home[i] = new Vector2(s * _w * 0.5f + Random.Range(-0.12f, 0.12f), 0.18f + 0.32f * dome + Random.Range(-0.06f, 0.12f));
                _r[i] = (0.3f + 0.26f * dome) * Random.Range(0.85f, 1.15f) * (0.85f + 0.08f * _power);
                _from[i] = new Vector2(_home[i].x * 2.4f + Random.Range(-0.6f, 0.6f), _home[i].y + Random.Range(0.3f, 0.9f));
                _ph[i] = Random.Range(0f, 6.28f);
                _spin[i] = Random.Range(1.2f, 2.4f) * (Random.value < 0.5f ? -1f : 1f);
            }
            _back = new Ribbon("CloudBack", transform, MeshKit.Alpha, OrderBack);
            _body = new Ribbon("CloudBody", transform, MeshKit.Alpha, OrderBody);
            _glow = new Ribbon("CloudGlow", transform, v.AddHDR(1.8f), OrderGlow);
            BaseY = Vfx.CloudBaseY(Vfx.ViewCam(_owner));
            Live.Add(this);
            Draw(0f);
        }

        /// <summary>Lightning leaves the cloud: its flash, and the start of its dissipation. Returns the point under it a bolt towards <paramref name="towardX"/> starts from.</summary>
        public Vector2 Strike(float towardX)
        {
            if (!Struck)
            {
                if (_t < _form) _t = _form;            // struck early (a cloud made on the strike's own frame): it is whole when it flashes
                _struckAt = _t;
                Draw(0f);
            }
            return BoltTop(towardX);
        }

        /// <summary>A point on the cloud's base towards <paramref name="towardX"/>.</summary>
        public Vector2 BoltTop(float towardX) => new(Mathf.Clamp(towardX, _x - _w * 0.35f, _x + _w * 0.35f) + Random.Range(-0.12f, 0.12f), BaseY + 0.05f);

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (!Struck && _t > _form + 0.6f) { _struckAt = _t; _melt = true; }      // never struck: it melts away, with no flash
            if (Struck && _t - _struckAt >= _linger) { Destroy(gameObject); return; }
            Draw(dt);
        }

        void Draw(float dt)
        {
            if (!Struck && _follow != null) _x = _follow.position.x;
            BaseY = Vfx.CloudBaseY(Vfx.ViewCam(_owner));
            float u = Mathf.Clamp01(_t / _form);                                  // gathering 0..1
            float g = KMath.Apply(Ease.OutCubic, u);
            float v = Struck ? Mathf.Clamp01((_t - _struckAt) / _linger) : 0f;    // dissipating 0..1
            float fade = Struck ? Mathf.Pow(1f - v, 1.4f) : Mathf.Clamp01(u * 2.2f);
            var c0 = new Vector2(_x, BaseY);

            _back.Begin(); _body.Begin(); _glow.Begin();
            for (int i = 0; i < _n; i++)
            {
                Vector2 home = _home[i] * (1f + 0.45f * v) + new Vector2(0f, 0.35f * v);
                Vector2 p = Vector2.Lerp(_from[i], home, g);
                float a = _ph[i] + _t * _spin[i];
                p += new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * 0.05f;                                   // churn
                float r = _r[i] * Mathf.Lerp(0.35f, 1f, g) * (1f + 0.07f * Mathf.Sin(_ph[i] * 3f + _t * 4.5f)) * (1f + 0.4f * v);
                _at[i] = c0 + p;
                _rNow[i] = r;
                Vfx.SoftBlob(_back, _at[i] + new Vector2(0f, -0.04f), r * 1.18f, r * 0.82f, Vfx.CloudDark.WithA(0.85f * fade), 0.45f);
            }
            for (int i = 0; i < _n; i++)
            {
                float r = _rNow[i];
                Vfx.SoftBlob(_body, _at[i], r, r * 0.7f, Vfx.CloudBody.WithA(0.92f * fade), 0.5f);
                Vfx.SoftBlob(_body, _at[i] + new Vector2(-0.05f, r * 0.28f), r * 0.6f, r * 0.34f, Vfx.CloudLit.WithA(0.32f * fade), 0.2f);    // the light on its top
            }

            // the under-glow: it rises towards the strike and flickers; the strike flashes the whole cloud on the stroke envelope
            float charge = Struck ? 0f : (0.12f + 0.55f * u * u) * (1f + Random.Range(-0.25f, 0.25f));
            float strike = Struck && !_melt ? Vfx.StrokeEnvelope(Mathf.Clamp01((_t - _struckAt) / 0.45f)) : 0f;
            float glowA = Mathf.Clamp01(charge * fade + strike * 1.1f);
            Color gc = Color.Lerp(Vfx.Crimson, Vfx.HotWhite, strike * 0.7f);
            for (int i = 0; i < _n; i += 2)
                Vfx.SoftBlob(_glow, _at[i] + Vector2.down * _rNow[i] * 0.3f, _rNow[i] * 1.15f, _rNow[i] * 0.55f, gc.WithA(glowA * 0.5f), 0.1f);

            if (!Struck && dt > 0f)
            {
                // a flicker inside the cloud, faster as the strike nears
                _flickT -= dt;
                if (_flickT <= 0f)
                {
                    _flickT = Mathf.Lerp(0.3f, 0.06f, u) * Random.Range(0.7f, 1.3f);
                    _flickPuff = Random.Range(0, _n);
                    _flickLeft = 0.05f;
                }
                if (_flickLeft > 0f && _flickPuff >= 0)
                {
                    _flickLeft -= dt;
                    float r = _rNow[_flickPuff];
                    Vfx.SoftBlob(_glow, _at[_flickPuff], r * 0.9f, r * 0.6f, Vfx.HotWhite.WithA((0.45f + 0.4f * u) * fade), 0.15f);
                }
                // from power 1: arcs crawl between the puffs' undersides
                var vfx = Vfx.I;
                if (_power >= 1 && vfx != null && u > 0.3f)
                {
                    _arcT -= dt;
                    if (_arcT <= 0f)
                    {
                        _arcT = Mathf.Lerp(0.3f, 0.08f, u) / (0.7f + 0.3f * _power);
                        int i0 = Random.Range(0, _n), i1 = Mathf.Clamp(i0 + (Random.value < 0.5f ? -2 : 2), 0, _n - 1);
                        vfx.Arc(_at[i0] + Vector2.down * _rNow[i0] * 0.3f, _at[i1] + Vector2.down * _rNow[i1] * 0.3f, Random.Range(0.08f, 0.14f), 0.016f);
                    }
                }
            }
            _back.End(); _body.End(); _glow.End();
        }

        void OnDestroy()
        {
            Live.Remove(this);
            if (_back != null) Destroy(_back.mesh);
            if (_body != null) Destroy(_body.mesh);
            if (_glow != null) Destroy(_glow.mesh);
        }
    }
}
