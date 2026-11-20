using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Hair motion: every hair vertex (taken from the SVG shapes) rotates about the skull centre by
    /// an angle that grows towards the tips. The angle is a damped spring lagging behind head and
    /// body motion plus a travelling wind wave, so locks whip on flips and sway at rest while the
    /// face stays rigid (zero weight in the face/neck sector).
    /// </summary>
    public class HairField
    {
        readonly Mesh _mesh;
        readonly Vector3[] _base, _out;
        readonly float[] _w, _phi, _dist;
        readonly Vector2 _c;
        float _lag, _lagVel, _time;
        Vector2 _trail, _trailVel;
        public float wind = 5.5f;

        public HairField(Mesh mesh, Vector2 centre)
        {
            _mesh = mesh;
            _mesh.MarkDynamic();
            _c = centre;
            _base = mesh.vertices;
            _out = new Vector3[_base.Length];
            _w = new float[_base.Length]; _phi = new float[_base.Length]; _dist = new float[_base.Length];
            for (int i = 0; i < _base.Length; i++)
            {
                Vector2 d = (Vector2)_base[i] - _c;
                float dist = d.magnitude;
                float phi = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                // face + neck sector (down-forward to down-back) stays rigid
                float mask = 1f;
                if (phi > -150f && phi < 8f) mask = phi < -130f ? Mathf.InverseLerp(-130f, -150f, phi) : (phi > -12f ? Mathf.InverseLerp(-12f, 8f, phi) : 0f);
                float wd = KMath.Smooth01(Mathf.InverseLerp(0.1f, 0.31f, dist));
                _w[i] = wd * mask;
                _phi[i] = phi * Mathf.Deg2Rad;
                _dist[i] = dist;
            }
        }

        /// <param name="vLocal">body velocity in the fighter's facing space (x forward)</param>
        /// <param name="headAngVel">head angular velocity in root space, deg/s</param>
        public void Tick(float dt, Vector2 vLocal, float headAngVel)
        {
            _time += dt;
            // a long frame (hitch, alt-tab, unfocused editor) must not blow the springs up: cap and substep
            dt = Mathf.Min(dt, 1f / 20f);
            float target = Mathf.Clamp(vLocal.x * 3.2f + vLocal.y * 2.6f - headAngVel * 0.06f, -34f, 34f);
            Spring(ref _lag, ref _lagVel, target, 150f, 8.5f, dt);
            Vector2 tt = Vector2.ClampMagnitude(new Vector2(-vLocal.x, -vLocal.y) * 0.011f, 0.07f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.008f));
            float h = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                _trailVel += ((tt - _trail) * 160f - _trailVel * 14f) * h;
                _trail += _trailVel * h;
            }
            if (!IsFinite(_lag) || !IsFinite(_lagVel) || !IsFinite(_trail.x) || !IsFinite(_trail.y) || !IsFinite(_trailVel.x) || !IsFinite(_trailVel.y)) Reset();

            float gust = (Mathf.PerlinNoise(_time * 0.9f, 3.7f) - 0.5f) * 9f;
            for (int i = 0; i < _base.Length; i++)
            {
                float w = _w[i];
                if (w <= 0.0005f) { _out[i] = _base[i]; continue; }
                float wave = Mathf.Sin(_time * 5.2f + _phi[i] * 2.1f - _dist[i] * 7f) * wind + Mathf.Sin(_time * 8.7f + _phi[i] * 5f) * wind * 0.35f;
                float ang = (_lag + gust + wave) * w * Mathf.Deg2Rad;
                float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                float dx = _base[i].x - _c.x, dy = _base[i].y - _c.y;
                _out[i] = new Vector3(_c.x + dx * cs - dy * sn + _trail.x * w * w, _c.y + dx * sn + dy * cs + _trail.y * w * w, 0f);
            }
            _mesh.vertices = _out;
            _mesh.RecalculateBounds();
        }

        static bool IsFinite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);

        /// <summary>Back to rest (round reset, or recovery from a bad frame).</summary>
        public void Reset() { _lag = _lagVel = 0f; _trail = _trailVel = Vector2.zero; }

        static void Spring(ref float x, ref float v, float target, float k, float damp, float dt)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.008f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++) { v += ((target - x) * k - v * damp) * h; x += v * h; }
        }
    }

    /// <summary>
    /// Coat motion: the coat is a quad patch stretched between two verlet ropes (front edge and
    /// back edge), pinned at the belt. Particles have inertia, gravity, wind and hem flutter, are
    /// pushed out by the legs, and are pulled back to the drawn silhouette by shape matching that
    /// weakens towards the hem. Every coat vertex (panel, trim, cloud pattern from the SVG) is
    /// bound to the patch by (u, t) and follows it.
    /// </summary>
    public class CoatCloth
    {
        public const int N = 6;
        readonly Transform _hips;
        readonly Vector2[] _restF = new Vector2[N], _restB = new Vector2[N];     // hips-local rest
        readonly Vector2[] _f = new Vector2[N], _b = new Vector2[N], _pf = new Vector2[N], _pb = new Vector2[N];
        readonly Vector2[] _lf = new Vector2[N], _lb = new Vector2[N];           // hips-local current
        readonly float _segF, _segB;
        readonly float[] _width = new float[N];
        float _time;
        public float groundY;
        public Vector2 windDir = Vector2.left;
        Vector2 _bodyVel;

        class Bound { public Mesh mesh; public Vector3[] outV; public float[] u, t; }
        readonly List<Bound> _meshes = new();
        readonly List<(Transform t, Vector2 local, float r)> _colliders = new();

        readonly float _loose;

        /// <param name="loose">1 = default; higher = softer shape matching and more air drag (free coat tails)</param>
        public CoatCloth(Transform hips, Vector2 f0, Vector2 f1, Vector2 b0, Vector2 b1, float loose = 1f)
        {
            _hips = hips;
            _loose = loose;
            for (int i = 0; i < N; i++)
            {
                float k = i / (float)(N - 1);
                _restF[i] = Vector2.Lerp(f0, f1, k);
                _restB[i] = Vector2.Lerp(b0, b1, k);
                _width[i] = (_restB[i] - _restF[i]).magnitude;
            }
            _segF = (f1 - f0).magnitude / (N - 1);
            _segB = (b1 - b0).magnitude / (N - 1);
            Reset();
        }

        public void AddCollider(Transform t, Vector2 local, float radius) => _colliders.Add((t, local, radius));

        /// <summary>Binds a mesh (vertices in hips-local rest space) to the patch.</summary>
        public void Bind(Mesh mesh)
        {
            mesh.MarkDynamic();
            var v = mesh.vertices;
            var bd = new Bound { mesh = mesh, outV = new Vector3[v.Length], u = new float[v.Length], t = new float[v.Length] };
            Vector2 f0 = _restF[0], f1 = _restF[N - 1], b0 = _restB[0], b1 = _restB[N - 1];
            for (int i = 0; i < v.Length; i++)
            {
                Vector2 p = v[i];
                float t = Mathf.InverseLerp(f0.y, f1.y, p.y), u = 0.5f;
                for (int it = 0; it < 10; it++)
                {
                    Vector2 F = Vector2.LerpUnclamped(f0, f1, t), B = Vector2.LerpUnclamped(b0, b1, t);
                    Vector2 row = B - F;
                    u = Vector2.Dot(p - F, row) / row.sqrMagnitude;
                    Vector2 r = p - (F + row * u);
                    Vector2 D = Vector2.LerpUnclamped(f1 - f0, b1 - b0, u);
                    t += Vector2.Dot(r, D) / D.sqrMagnitude;
                }
                bd.u[i] = u; bd.t[i] = t;
            }
            _meshes.Add(bd);
        }

        public void Reset()
        {
            for (int i = 0; i < N; i++)
            {
                _f[i] = _pf[i] = _hips.TransformPoint(_restF[i]);
                _b[i] = _pb[i] = _hips.TransformPoint(_restB[i]);
            }
        }

        /// <param name="bodyVel">world velocity of the hips (m/s); fast motion loosens the cloth so it can fly</param>
        public void Tick(float dt, float facing, Vector2 bodyVel = default)
        {
            if (dt > 0f)
            {
                _time += dt;
                const int sub = 3;
                float h = Mathf.Min(dt, 1f / 30f) / sub;
                // a teleport (round reset, counter) should not whip the cloth across the stage
                if (((Vector2)_hips.TransformPoint(_restF[0]) - _f[0]).sqrMagnitude > 1.2f) Reset();
                // smoothed so a single-frame jolt (hit-stop, landing) doesn't snap the coat
                _bodyVel = Vector2.Lerp(_bodyVel, bodyVel, 1f - Mathf.Exp(-12f * dt));
                for (int s = 0; s < sub; s++) Step(h, facing);
            }
            for (int i = 0; i < N; i++)
            {
                _lf[i] = _hips.InverseTransformPoint(_f[i]);
                _lb[i] = _hips.InverseTransformPoint(_b[i]);
            }
            foreach (var m in _meshes)
            {
                for (int i = 0; i < m.outV.Length; i++)
                {
                    float t = m.t[i];
                    Vector2 F = Eval(_lf, t), B = Eval(_lb, t);
                    m.outV[i] = Vector2.LerpUnclamped(F, B, m.u[i]);
                }
                m.mesh.vertices = m.outV;
                m.mesh.RecalculateBounds();
            }
        }

        static Vector2 Eval(Vector2[] p, float t)
        {
            float s = t * (N - 1);
            if (s <= 0f) return p[0] + (p[1] - p[0]) * s;
            if (s >= N - 1) return p[N - 1] + (p[N - 1] - p[N - 2]) * (s - (N - 1));
            int i = Mathf.Min((int)s, N - 2);
            float k = s - i;
            Vector2 p0 = p[Mathf.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Mathf.Min(N - 1, i + 2)];
            // Catmull-Rom for a smooth drape between particles
            return 0.5f * (2f * p1 + (p2 - p0) * k + (2f * p0 - 5f * p1 + 4f * p2 - p3) * k * k + (3f * p1 - p0 - 3f * p2 + p3) * k * k * k);
        }

        void Step(float h, float facing)
        {
            Vector2 back = new Vector2(-facing, 0f);
            // ambient breeze trailing behind the fighter (the reference art sweeps the coat back)
            float breeze = 0.7f + Mathf.PerlinNoise(_time * 0.8f, 1.9f) * 1.1f;
            Vector2 wind = back * breeze;
            // the faster the body moves (run, dash, fall), the more the hem is left to the air
            float freedom = Mathf.Clamp01(_bodyVel.magnitude / 7f);
            for (int i = 0; i < N; i++)
            {
                Vector2 tf = _hips.TransformPoint(_restF[i]), tb = _hips.TransformPoint(_restB[i]);
                if (i == 0) { _f[0] = _pf[0] = tf; _b[0] = _pb[0] = tb; continue; }
                float k = i / (float)(N - 1);
                float flutter = Mathf.Sin(_time * 6.2f - i * 1.05f) * (5f + 9f * freedom) * k * k;
                // aerodynamic drag towards the air's velocity: cloth trails motion, lifts when falling
                float drag = Mathf.Lerp(0.8f, 4.2f, k) * Mathf.Lerp(1f, _loose, k);
                Vector2 accF = new Vector2(0f, -7f) + back * flutter + Drag(_f[i], _pf[i], wind, drag, h);
                Vector2 accB = new Vector2(0f, -7.6f) + new Vector2(0f, Mathf.Cos(_time * 5.1f - i) * (4f + 6f * freedom) * k * k)
                               + Drag(_b[i], _pb[i], wind * 1.15f, drag * 1.1f, h);
                Integrate(ref _f[i], ref _pf[i], accF, h, 0.985f);
                Integrate(ref _b[i], ref _pb[i], accB, h, 0.988f);
                // shape matching: firm at the belt, loose at the hem, looser still at speed
                float stiff = Mathf.Lerp(0.3f, 0.05f, Mathf.Pow(k, 0.7f)) * Mathf.Lerp(1f, 0.4f, freedom * k);
                stiff /= Mathf.Lerp(1f, _loose, k);
                _f[i] = Vector2.Lerp(_f[i], tf, stiff);
                _b[i] = Vector2.Lerp(_b[i], tb, stiff * 0.8f);
            }
            for (int it = 0; it < 3; it++)
            {
                for (int i = 0; i < N - 1; i++)
                {
                    Link(ref _f[i], ref _f[i + 1], _segF, i == 0);
                    Link(ref _b[i], ref _b[i + 1], _segB, i == 0);
                }
                for (int i = 1; i < N; i++) Width(ref _f[i], ref _b[i], _width[i]);
                foreach (var c in _colliders)
                {
                    Vector2 cp = c.t.TransformPoint(c.local);
                    for (int i = 1; i < N; i++) { Push(ref _f[i], cp, c.r); Push(ref _b[i], cp, c.r); }
                }
                for (int i = 1; i < N; i++)
                {
                    if (_f[i].y < groundY + 0.02f) _f[i].y = groundY + 0.02f;
                    if (_b[i].y < groundY + 0.02f) _b[i].y = groundY + 0.02f;
                }
            }
        }

        /// <summary>Linear drag pulling a particle's velocity towards the air velocity.</summary>
        static Vector2 Drag(Vector2 p, Vector2 prev, Vector2 air, float c, float h)
        {
            Vector2 v = (p - prev) / h;
            return Vector2.ClampMagnitude((air - v) * c, 60f);
        }

        static void Integrate(ref Vector2 p, ref Vector2 prev, Vector2 acc, float h, float damp)
        {
            Vector2 v = (p - prev) * damp;
            prev = p;
            p += v + acc * h * h;
        }

        static void Link(ref Vector2 a, ref Vector2 b, float rest, bool aPinned)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-6f) return;
            Vector2 corr = d * ((len - rest) / len);
            if (aPinned) b -= corr;
            else { a += corr * 0.5f; b -= corr * 0.5f; }
        }

        static void Width(ref Vector2 a, ref Vector2 b, float rest)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-6f) return;
            // let the panel narrow or flare by 25% before resisting
            float target = Mathf.Clamp(len, rest * 0.75f, rest * 1.25f);
            Vector2 corr = d * ((len - target) / len) * 0.5f;
            a += corr; b -= corr;
        }

        static void Push(ref Vector2 p, Vector2 c, float r)
        {
            Vector2 d = p - c;
            float len = d.magnitude;
            if (len >= r || len < 1e-5f) return;
            p = c + d / len * r;
        }
    }

    /// <summary>
    /// Spring chains (SAN-71: Raikon's cape, ponytail, hat tassels and fur mantle). Each chain is a verlet rope pinned at
    /// its root to the bone, with inertia, gravity and air drag (it trails the body's motion and a breeze behind the
    /// fighter), pulled back to its drawn shape by shape matching that weakens towards the tip. Every mesh vertex is bound
    /// to its two nearest chains at the nearest point along each, and follows that point's position and turn, blended by
    /// distance; so a part bends smoothly and stays joined where it is pinned.
    /// </summary>
    public class SoftChains
    {
        sealed class Chain
        {
            public Vector2[] rest;      // bone-local rest points, root first
            public float[] restAng;     // rest angle of each segment (deg)
            public Vector2[] p, prev;   // world particles
            public Vector2[] local;     // bone-local current particles
            public float[] jointAng;    // turn at each particle (deg), from its segments
        }

        readonly Mesh _mesh;
        readonly Transform _bone;
        readonly Chain[] _c;
        readonly Vector3[] _base, _out;
        readonly int[] _ca, _cb, _ia, _ib;
        readonly float[] _ta, _tb, _w;
        readonly Vector2[] _oa, _ob;
        readonly ChainFeel _feel;
        float _time;
        Vector2 _bodyVel;
        public float groundY = float.MinValue;
        /// <summary>
        /// World wind on top of the part's breeze (SAN-76): it pushes every particle along it, by the part's drag (light cloth more,
        /// heavy plates less) times <see cref="WindPush"/>, more towards the free end, gusts and all. Calm by default: the chain then
        /// runs exactly the code it ran before wind existed.
        /// </summary>
        public Wind wind;

        /// <summary>
        /// How hard a wind pushes, per m/s and unit of drag. Stronger than the breeze's trailing drag because a hanging part catches
        /// the wind like a sail, and the shape matching would otherwise hold its drawn shape against anything short of a gale.
        /// </summary>
        public const float WindPush = 6f;

        /// <param name="mesh">the part's mesh, in the bone's local space</param>
        /// <param name="bone">the bone the part (and every chain root) is pinned to</param>
        /// <param name="chains">chains in the same space as the mesh, root first</param>
        public SoftChains(Mesh mesh, Transform bone, IList<Vector2[]> chains, ChainFeel feel)
        {
            _mesh = mesh;
            _mesh.MarkDynamic();
            _bone = bone;
            _feel = feel;
            _c = new Chain[chains.Count];
            for (int k = 0; k < _c.Length; k++)
            {
                var r = chains[k];
                var c = new Chain { rest = r, restAng = new float[r.Length - 1], p = new Vector2[r.Length], prev = new Vector2[r.Length], local = new Vector2[r.Length], jointAng = new float[r.Length] };
                for (int i = 0; i < r.Length - 1; i++) c.restAng[i] = KMath.Ang(r[i + 1] - r[i]);
                _c[k] = c;
            }
            _base = mesh.vertices;
            _out = new Vector3[_base.Length];
            int n = _base.Length;
            _ca = new int[n]; _cb = new int[n]; _ia = new int[n]; _ib = new int[n];
            _ta = new float[n]; _tb = new float[n]; _w = new float[n];
            _oa = new Vector2[n]; _ob = new Vector2[n];
            for (int v = 0; v < n; v++)
            {
                Vector2 q = _base[v];
                int best = -1, second = -1; float d1 = float.MaxValue, d2 = float.MaxValue;
                int bi1 = 0, bi2 = 0; float bt1 = 0, bt2 = 0;
                for (int k = 0; k < _c.Length; k++)
                {
                    Closest(_c[k].rest, q, out int si, out float st, out float d);
                    if (d < d1) { second = best; d2 = d1; bi2 = bi1; bt2 = bt1; best = k; d1 = d; bi1 = si; bt1 = st; }
                    else if (d < d2) { second = k; d2 = d; bi2 = si; bt2 = st; }
                }
                if (second < 0) { second = best; d2 = d1; bi2 = bi1; bt2 = bt1; }
                _ca[v] = best; _ia[v] = bi1; _ta[v] = bt1; _oa[v] = q - At(_c[best].rest, bi1, bt1);
                _cb[v] = second; _ib[v] = bi2; _tb[v] = bt2; _ob[v] = q - At(_c[second].rest, bi2, bt2);
                float wa = 1f / (d1 * d1 + 1e-5f), wb = 1f / (d2 * d2 + 1e-5f);
                _w[v] = best == second ? 1f : wa / (wa + wb);
            }
            Reset();
        }

        static Vector2 At(Vector2[] pts, int i, float t) => Vector2.LerpUnclamped(pts[i], pts[i + 1], t);

        static void Closest(Vector2[] pts, Vector2 q, out int seg, out float t, out float dist)
        {
            seg = 0; t = 0f; dist = float.MaxValue;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i], ab = pts[i + 1] - a;
                float u = Mathf.Clamp01(Vector2.Dot(q - a, ab) / Mathf.Max(1e-9f, ab.sqrMagnitude));
                float d = (a + ab * u - q).magnitude;
                if (d < dist) { dist = d; seg = i; t = u; }
            }
        }

        /// <summary>Back to the drawn shape (round reset, or a teleport).</summary>
        public void Reset()
        {
            foreach (var c in _c)
                for (int i = 0; i < c.rest.Length; i++) c.p[i] = c.prev[i] = _bone.TransformPoint(c.rest[i]);
            _bodyVel = Vector2.zero;
            Deform();
        }

        /// <param name="bodyVel">world velocity of the body (m/s); fast motion loosens the chains so they fly</param>
        public void Tick(float dt, float facing, Vector2 bodyVel = default)
        {
            if (dt > 0f)
            {
                _time += dt;
                const int sub = 3;
                float h = Mathf.Min(dt, 1f / 30f) / sub;
                if (((Vector2)_bone.TransformPoint(_c[0].rest[0]) - _c[0].p[0]).sqrMagnitude > 1.2f) Reset();
                _bodyVel = Vector2.Lerp(_bodyVel, bodyVel, 1f - Mathf.Exp(-12f * dt));
                for (int s = 0; s < sub; s++) Step(h, facing);
            }
            Deform();
        }

        void Step(float h, float facing)
        {
            Vector2 back = new Vector2(-facing, 0f);
            float freedom = Mathf.Clamp01(_bodyVel.magnitude / 7f);
            for (int k = 0; k < _c.Length; k++)
            {
                var c = _c[k];
                int n = c.rest.Length;
                float gust = _feel.breeze * (0.6f + Mathf.PerlinNoise(_time * 0.8f + k * 3.1f, 1.9f) * 0.9f);
                Vector2 air = back * gust;
                c.p[0] = c.prev[0] = _bone.TransformPoint(c.rest[0]);
                bool windy = !wind.IsCalm;
                Vector2 blow = windy ? wind.At(_time, c.p[0].x) * (_feel.drag * WindPush) : Vector2.zero;
                for (int i = 1; i < n; i++)
                {
                    float u = i / (float)(n - 1);
                    Vector2 target = _bone.TransformPoint(c.rest[i]);
                    Vector2 v = (c.p[i] - c.prev[i]) / h;
                    float flutter = Mathf.Sin(_time * 5.7f - i * 1.1f + k * 2.3f) * (1.5f + 5f * freedom) * u * u;
                    Vector2 acc = new Vector2(0f, -_feel.gravity) + Vector2.ClampMagnitude((air - v) * _feel.drag * Mathf.Lerp(0.5f, 1.5f, u), 60f)
                                  + new Vector2(0f, flutter);
                    if (windy) acc += Vector2.ClampMagnitude(blow * Mathf.Lerp(0.5f, 1.5f, u), 160f);
                    Vector2 vel = (c.p[i] - c.prev[i]) * 0.985f;
                    c.prev[i] = c.p[i];
                    c.p[i] += vel + acc * h * h;
                    float stiff = Mathf.Lerp(_feel.stiffRoot, _feel.stiffTip, Mathf.Pow(u, 0.7f)) * Mathf.Lerp(1f, 0.5f, freedom * u);
                    c.p[i] = Vector2.Lerp(c.p[i], target, stiff);
                }
                for (int it = 0; it < 3; it++)
                {
                    for (int i = 0; i < n - 1; i++)
                    {
                        float rest = ((Vector2)_bone.TransformVector(c.rest[i + 1] - c.rest[i])).magnitude;
                        Vector2 d = c.p[i + 1] - c.p[i];
                        float len = d.magnitude;
                        if (len < 1e-6f) continue;
                        Vector2 corr = d * ((len - rest) / len);
                        if (i == 0) c.p[i + 1] -= corr;
                        else { c.p[i] += corr * 0.5f; c.p[i + 1] -= corr * 0.5f; }
                    }
                    for (int i = 1; i < n; i++)
                        if (c.p[i].y < groundY + 0.02f) c.p[i].y = groundY + 0.02f;
                }
            }
        }

        void Deform()
        {
            foreach (var c in _c)
            {
                int n = c.rest.Length;
                for (int i = 0; i < n; i++) c.local[i] = _bone.InverseTransformPoint(c.p[i]);
                // turn of each segment, then of each particle (average of its segments), so vertices turn smoothly
                for (int i = 0; i < n; i++)
                {
                    float a0 = i > 0 ? Mathf.DeltaAngle(c.restAng[i - 1], KMath.Ang(c.local[i] - c.local[i - 1])) : float.NaN;
                    float a1 = i < n - 1 ? Mathf.DeltaAngle(c.restAng[i], KMath.Ang(c.local[i + 1] - c.local[i])) : float.NaN;
                    c.jointAng[i] = float.IsNaN(a0) ? a1 : float.IsNaN(a1) ? a0 : (a0 + a1) * 0.5f;
                    if (i == 0) c.jointAng[i] *= 0.5f;   // the root is pinned: half the first segment's turn
                }
            }
            for (int v = 0; v < _base.Length; v++)
            {
                Vector2 qa = Place(_c[_ca[v]], _ia[v], _ta[v], _oa[v]);
                Vector2 q = _ca[v] == _cb[v] ? qa : Vector2.LerpUnclamped(Place(_c[_cb[v]], _ib[v], _tb[v], _ob[v]), qa, _w[v]);
                _out[v] = new Vector3(q.x, q.y, 0f);
            }
            _mesh.vertices = _out;
            _mesh.RecalculateBounds();
        }

        static Vector2 Place(Chain c, int i, float t, Vector2 offset)
        {
            float ang = Mathf.Lerp(c.jointAng[i], c.jointAng[i + 1], t);
            return Vector2.LerpUnclamped(c.local[i], c.local[i + 1], t) + KMath.Rot(offset, ang);
        }
    }

    /// <summary>Destroys meshes that were instantiated for a temporary object (ghost afterimages).</summary>
    public class MeshOwner : MonoBehaviour
    {
        public readonly List<Mesh> meshes = new();
        void OnDestroy() { foreach (var m in meshes) if (m != null) Destroy(m); }
    }
}
