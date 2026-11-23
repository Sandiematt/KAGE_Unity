using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Hair motion: every hair vertex (taken from the SVG shapes) rotates about the skull centre by
    /// an angle that grows towards the tips. The angle is a damped spring lagging behind head and
    /// body motion, world wind, hit impulses, and head tilt (gravity droop), while the face stays rigid.
    /// </summary>
    public class HairField
    {
        readonly Mesh _mesh;
        readonly Vector3[] _base, _out;
        readonly float[] _w, _phi, _dist;
        readonly Vector2 _c;
        float _lag, _lagVel, _time;
        Vector2 _trail, _trailVel;
        Vector2 _pendingImpulse;
        float _pendingAngImpulse;

        public float wind = 5.5f;
        /// <summary>Optional world wind (same struct as SoftChains). Calm by default.</summary>
        public Wind worldWind;
        public float windScale = 1f;

        /// <param name="innerRadius">radius from skull centre where hair motion begins (rigid inside)</param>
        /// <param name="outerRadius">radius where hair reaches full weight</param>
        /// <param name="faceMinDeg">start of rigid face/neck sector in degrees</param>
        /// <param name="faceMaxDeg">end of rigid face/neck sector in degrees</param>
        public HairField(Mesh mesh, Vector2 centre, float innerRadius = 0.1f, float outerRadius = 0.31f, float faceMinDeg = -150f, float faceMaxDeg = 8f)
        {
            _mesh = mesh;
            _mesh.MarkDynamic();
            _c = centre;
            _base = mesh.vertices;
            _out = new Vector3[_base.Length];
            _w = new float[_base.Length];
            _phi = new float[_base.Length];
            _dist = new float[_base.Length];

            float feather = 20f;
            for (int i = 0; i < _base.Length; i++)
            {
                Vector2 d = (Vector2)_base[i] - _c;
                float dist = d.magnitude;
                float phi = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                // face + neck sector stays rigid
                float mask = 1f;
                if (phi > faceMinDeg && phi < faceMaxDeg)
                {
                    if (phi < faceMinDeg + feather) mask = Mathf.InverseLerp(faceMinDeg + feather, faceMinDeg, phi);
                    else if (phi > faceMaxDeg - feather) mask = Mathf.InverseLerp(faceMaxDeg - feather, faceMaxDeg, phi);
                    else mask = 0f;
                }
                float wd = KMath.Smooth01(Mathf.InverseLerp(innerRadius, outerRadius, dist));
                _w[i] = wd * mask;
                _phi[i] = phi * Mathf.Deg2Rad;
                _dist[i] = dist;
            }
        }

        /// <summary>
        /// Queues a hit/knockback/landing whip. Safe to call during hit-stop (dt == 0): fires on the next resumed tick.
        /// </summary>
        /// <param name="impulseLocal">impulse in fighter facing space (x forward, y up)</param>
        /// <param name="angImpulseDeg">optional direct angular kick in deg/s</param>
        public void AddImpulse(Vector2 impulseLocal, float angImpulseDeg = 0f)
        {
            _pendingImpulse += impulseLocal;
            _pendingAngImpulse += angImpulseDeg;
        }

        /// <param name="vLocal">body velocity in the fighter's facing space (x forward)</param>
        /// <param name="headAngVel">head angular velocity in root space, deg/s</param>
        /// <param name="facing">+1 facing right, -1 facing left (used to project worldWind into local space)</param>
        /// <param name="headTiltDeg">head tilt from upright in deg (causes hair to droop under gravity when knocked down)</param>
        /// <param name="worldPos">world position for sampling spatial wind gusts</param>
        public void Tick(float dt, Vector2 vLocal, float headAngVel, float facing = 1f, float headTiltDeg = 0f, Vector2 worldPos = default)
        {
            if (dt <= 0f) return;

            _time += dt;
            dt = Mathf.Min(dt, 1f / 20f);

            // Apply queued hit impulses as velocity kicks on the first active frame after hit-stop
            if (_pendingImpulse.sqrMagnitude > 1e-6f || Mathf.Abs(_pendingAngImpulse) > 1e-4f)
            {
                _lagVel += Mathf.Clamp(-_pendingImpulse.x * 38f - _pendingImpulse.y * 28f + _pendingAngImpulse, -650f, 650f);
                _trailVel += Vector2.ClampMagnitude(_pendingImpulse * 0.22f, 2.2f);
                _pendingImpulse = Vector2.zero;
                _pendingAngImpulse = 0f;
            }

            // World wind projection into fighter-local space
            Vector2 wLocal = Vector2.zero;
            float windBoost = 1f;
            if (!worldWind.IsCalm)
            {
                Vector2 wWorld = worldWind.At(_time, worldPos.x) * windScale;
                wLocal = new Vector2(wWorld.x * facing, wWorld.y);
                windBoost = 1f + Mathf.Clamp(wWorld.magnitude * 0.22f, 0f, 1.8f);
            }

            // Gravity droop when character tilts or lies down
            float droop = Mathf.Sin(headTiltDeg * Mathf.Deg2Rad) * 24f;

            float target = Mathf.Clamp(
                vLocal.x * 3.2f + vLocal.y * 2.6f - headAngVel * 0.06f - wLocal.x * 3.4f + droop,
                -48f, 48f
            );
            Spring(ref _lag, ref _lagVel, target, 145f, 8.2f, dt);

            Vector2 tt = Vector2.ClampMagnitude(new Vector2(-vLocal.x + wLocal.x * 0.85f, -vLocal.y + wLocal.y * 0.6f) * 0.011f, 0.09f);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.008f));
            float h = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                _trailVel += ((tt - _trail) * 155f - _trailVel * 13.5f) * h;
                _trail += _trailVel * h;
            }

            if (!IsFinite(_lag) || !IsFinite(_lagVel) || !IsFinite(_trail.x) || !IsFinite(_trail.y) || !IsFinite(_trailVel.x) || !IsFinite(_trailVel.y))
                Reset();

            float gust = (Mathf.PerlinNoise(_time * 0.9f, 3.7f) - 0.5f) * 9f * windBoost;
            float amp = wind * windBoost;
            for (int i = 0; i < _base.Length; i++)
            {
                float w = _w[i];
                if (w <= 0.0005f) { _out[i] = _base[i]; continue; }
                float wave = Mathf.Sin(_time * 5.2f + _phi[i] * 2.1f - _dist[i] * 7f) * amp
                           + Mathf.Sin(_time * 8.7f + _phi[i] * 5f) * amp * 0.35f;
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
        public void Reset()
        {
            _lag = _lagVel = 0f;
            _trail = _trailVel = Vector2.zero;
            _pendingImpulse = Vector2.zero;
            _pendingAngImpulse = 0f;
        }

        static void Spring(ref float x, ref float v, float target, float k, float damp, float dt)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.008f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                v += ((target - x) * k - v * damp) * h;
                x += v * h;
            }
        }
    }

    /// <summary>
    /// Coat / dress motion: a quad patch stretched between two Verlet ropes (front and back edge), pinned at the belt.
    /// Supports framerate-independent viscoelastic shape matching, world wind, hit impulses, falling lift,
    /// knockdown gravity drape, and floor friction.
    /// </summary>
    public class CoatCloth
    {
        public const int N = 8;
        readonly Transform _hips;
        readonly Vector2[] _restF = new Vector2[N], _restB = new Vector2[N];
        readonly Vector2[] _f = new Vector2[N], _b = new Vector2[N], _pf = new Vector2[N], _pb = new Vector2[N];
        readonly Vector2[] _lf = new Vector2[N], _lb = new Vector2[N];
        readonly float _segF, _segB;
        readonly float[] _width = new float[N];
        float _time;
        public float groundY = float.MinValue;
        Vector2 _bodyVel;

        // Impulse & knockdown relaxation state
        Vector2 _pendingImpulse;
        float _pendingTipBias = 1f;
        float _impulseRelax;

        class Bound { public Mesh mesh; public Vector3[] outV; public float[] u, t; }
        readonly List<Bound> _meshes = new();

        readonly float _loose;
        readonly Transform _leg;
        readonly float _follow;
        readonly float _phase;
        float _swing;

        /// <summary>Strength of the wind-field overlay at rest: sway (deg) and hem ripple (units).</summary>
        public float waveSway = 3.4f, waveRipple = 0.007f;
        /// <summary>World wind (same struct as SoftChains). Calm by default.</summary>
        public Wind wind;
        public float windPush = 5.5f;
        /// <summary>When true, falling sideways/upside-down relaxes shape matching so gravity drapes the dress onto the floor.</summary>
        public bool relaxOnTilt = true;

        public CoatCloth(Transform hips, Vector2 f0, Vector2 f1, Vector2 b0, Vector2 b1, float loose = 1f, Transform leg = null, float follow = 0f, float phase = 0f)
        {
            _hips = hips;
            _loose = loose;
            _leg = leg;
            _follow = follow;
            _phase = phase;
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
                    u = Vector2.Dot(p - F, row) / Mathf.Max(1e-9f, row.sqrMagnitude);
                    Vector2 r = p - (F + row * u);
                    Vector2 D = Vector2.LerpUnclamped(f1 - f0, b1 - b0, u);
                    t += Vector2.Dot(r, D) / Mathf.Max(1e-9f, D.sqrMagnitude);
                }
                bd.u[i] = u; bd.t[i] = t;
            }
            _meshes.Add(bd);
        }

        /// <summary>
        /// Injects a world-space hit/knockback/landing impulse into the coat or dress.
        /// Temporarily loosens shape matching so the hem whips naturally. Safe to call during hit-stop.
        /// </summary>
        public void AddImpulse(Vector2 worldImpulse, float tipBias = 1f)
        {
            _pendingImpulse += worldImpulse;
            _pendingTipBias = Mathf.Max(0f, tipBias);
            _impulseRelax = 1f;
        }

        public void Reset()
        {
            _swing = LegSwing();
            for (int i = 0; i < N; i++)
            {
                _f[i] = _pf[i] = _hips.TransformPoint(Target(_restF[i], i));
                _b[i] = _pb[i] = _hips.TransformPoint(Target(_restB[i], i));
            }
            _bodyVel = Vector2.zero;
            _pendingImpulse = Vector2.zero;
            _impulseRelax = 0f;
        }

        float LegSwing()
        {
            if (_leg == null || _follow == 0f) return 0f;
            return Mathf.Clamp(Mathf.DeltaAngle(0f, _leg.localEulerAngles.z) * _follow, -34f, 34f);
        }

        Vector2 Target(Vector2 rest, int i)
        {
            if (_leg == null || Mathf.Abs(_swing) < 0.01f) return rest;
            float k = i / (float)(N - 1);
            Vector2 pivot = _leg.localPosition;
            return pivot + KMath.Rot(rest - pivot, _swing * k * k * (3f - 2f * k));
        }

        public void Tick(float dt, float facing, Vector2 bodyVel = default)
        {
            float windFreedom = 0f;
            if (dt > 0f)
            {
                _time += dt;
                const int sub = 3;
                float clampedDt = Mathf.Min(dt, 1f / 30f);
                float h = clampedDt / sub;

                // Teleport guard (generous enough not to trip on high-speed knockback launches)
                if (((Vector2)_hips.TransformPoint(_restF[0]) - _f[0]).sqrMagnitude > 1.6f) Reset();

                _bodyVel = Vector2.Lerp(_bodyVel, bodyVel, 1f - Mathf.Exp(-12f * clampedDt));
                _swing = Mathf.Lerp(_swing, LegSwing(), 1f - Mathf.Exp(-16f * clampedDt));
                _impulseRelax = Mathf.MoveTowards(_impulseRelax, 0f, clampedDt * 2.4f);

                if (!wind.IsCalm)
                    windFreedom = Mathf.Clamp01(wind.At(_time, _f[0].x).magnitude / 8f);

                for (int s = 0; s < sub; s++) Step(h, facing, s == 0);

                if (!IsFinite(_f[N - 1]) || !IsFinite(_b[N - 1])) Reset();
            }

            for (int i = 0; i < N; i++)
            {
                _lf[i] = _hips.InverseTransformPoint(_f[i]);
                _lb[i] = _hips.InverseTransformPoint(_b[i]);
            }

            // Wind-field overlay: sway about belt + travelling hem ripple
            float freedom = Mathf.Clamp01(Mathf.Max(_bodyVel.magnitude / 7f, windFreedom));
            float sway = waveSway * (1f + 1.15f * freedom);
            float ripple = waveRipple * (1f + 1.3f * freedom);
            float gust = (Mathf.PerlinNoise(_time * 0.7f, 5.3f + _phase) - 0.5f) * 6f * (1f + windFreedom);
            Vector2 belt = Vector2.Lerp(_lf[0], _lb[0], 0.5f);
            float tm = _time + _phase;

            foreach (var m in _meshes)
            {
                for (int i = 0; i < m.outV.Length; i++)
                {
                    float t = m.t[i], u = m.u[i];
                    Vector2 q = Vector2.LerpUnclamped(Eval(_lf, t), Eval(_lb, t), u);
                    float w = Mathf.Min(1.45f, KMath.Smooth01(Mathf.InverseLerp(0.12f, 1f, t)) + Mathf.Max(0f, t - 1f) * 0.9f);
                    if (w > 0.0005f)
                    {
                        float wave = Mathf.Sin(tm * 4.4f - t * 5.2f + u * 2.4f) + Mathf.Sin(tm * 7.3f - t * 3.1f - u * 5.5f) * 0.4f;
                        float ang = (gust + wave * sway) * w * Mathf.Deg2Rad;
                        float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                        Vector2 d = q - belt;
                        q = belt + new Vector2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
                        float ph = tm * 6.1f - u * 7.5f + t * 1.5f;
                        q += new Vector2(Mathf.Cos(ph) * 0.5f, Mathf.Sin(ph)) * (ripple * w * w);
                    }
                    m.outV[i] = q;
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
            return 0.5f * (2f * p1 + (p2 - p0) * k + (2f * p0 - 5f * p1 + 4f * p2 - p3) * k * k + (3f * p1 - p0 - 3f * p2 + p3) * k * k * k);
        }

        void Step(float h, float facing, bool applyImpulse)
        {
            Vector2 back = new Vector2(-facing, 0f);
            float breeze = 0.7f + Mathf.PerlinNoise(_time * 0.8f, 1.9f + _phase) * 1.1f;
            Vector2 air = back * breeze;

            bool windy = !wind.IsCalm;
            Vector2 blow = windy ? wind.At(_time, _f[0].x) * windPush : Vector2.zero;
            float windFreedom = windy ? Mathf.Clamp01(blow.magnitude / 38f) : 0f;

            // Speed, falling, knockdown tilt, and hit impulses all relax shape matching
            float speedFreedom = Mathf.Clamp01(_bodyVel.magnitude / 7f);
            float fallFreedom = Mathf.Clamp01(-_bodyVel.y / 5.5f);
            float freedom = Mathf.Clamp01(Mathf.Max(speedFreedom, Mathf.Max(fallFreedom, windFreedom)));

            float upright = relaxOnTilt ? Mathf.Clamp01(Vector2.Dot(((Vector2)_hips.up).normalized, Vector2.up)) : 1f;
            float tiltScale = Mathf.Lerp(0.12f, 1f, upright);
            float impulseScale = Mathf.Lerp(1f, 0.18f, _impulseRelax);

            // Framerate-independent damping (equivalent to 0.985 / 0.988 at 180Hz)
            float dampF = Mathf.Exp(-2.72f * h);
            float dampB = Mathf.Exp(-2.17f * h);

            bool hasImpulse = applyImpulse && _pendingImpulse.sqrMagnitude > 1e-6f;
            Vector2 imp = hasImpulse ? Vector2.ClampMagnitude(_pendingImpulse, 24f) : Vector2.zero;
            if (hasImpulse) _pendingImpulse = Vector2.zero;

            for (int i = 0; i < N; i++)
            {
                Vector2 tf = _hips.TransformPoint(Target(_restF[i], i));
                Vector2 tb = _hips.TransformPoint(Target(_restB[i], i));
                if (i == 0) { _f[0] = _pf[0] = tf; _b[0] = _pb[0] = tb; continue; }

                float k = i / (float)(N - 1);
                if (hasImpulse)
                {
                    float iw = Mathf.Lerp(0.25f, 1f, Mathf.Pow(k, _pendingTipBias));
                    _pf[i] -= imp * (iw * h);
                    _pb[i] -= imp * (iw * h);
                }

                float flutterF = Mathf.Sin(_time * 6.2f - i * 0.8f + _phase) * (5f + 9f * freedom) * k * k;
                float flutterB = Mathf.Cos(_time * 5.1f - i * 0.75f + _phase) * (4f + 7f * freedom) * k * k;
                float drag = Mathf.Lerp(0.9f, 4.4f, k) * Mathf.Lerp(1f, _loose, k);
                // Falling upward billow: extra aerodynamic drag against vertical fall velocity
                Vector2 airF = air + new Vector2(0f, fallFreedom * 2.2f * k);
                Vector2 airB = air * 1.15f + new Vector2(0f, fallFreedom * 2.6f * k);

                Vector2 accF = new Vector2(0f, -8.5f) + back * flutterF + Drag(_f[i], _pf[i], airF, drag, h);
                Vector2 accB = new Vector2(0f, -9.0f) + back * (flutterB * 0.75f) + new Vector2(0f, flutterB * 0.6f)
                               + Drag(_b[i], _pb[i], airB, drag * 1.1f, h);

                if (windy)
                {
                    float wk = Mathf.Lerp(0.45f, 1.5f, k) * Mathf.Lerp(1f, _loose, k);
                    accF += Vector2.ClampMagnitude(blow * wk, 150f);
                    accB += Vector2.ClampMagnitude(blow * (wk * 1.1f), 150f);
                }

                Integrate(ref _f[i], ref _pf[i], accF, h, dampF);
                Integrate(ref _b[i], ref _pb[i], accB, h, dampB);

                // Framerate-independent viscoelastic shape matching (does not catapult Verlet velocity)
                float baseStiff = Mathf.Lerp(0.22f, 0.03f, Mathf.Pow(k, 0.75f));
                baseStiff *= Mathf.Lerp(1f, 0.35f, freedom * k) * tiltScale * impulseScale;
                baseStiff /= Mathf.Lerp(1f, _loose, k);

                float alphaF = 1f - Mathf.Pow(Mathf.Clamp01(1f - baseStiff), h * 180f);
                float alphaB = 1f - Mathf.Pow(Mathf.Clamp01(1f - baseStiff * 0.8f), h * 180f);
                ShapePull(ref _f[i], ref _pf[i], tf, alphaF);
                ShapePull(ref _b[i], ref _pb[i], tb, alphaB);
            }

            float floor = groundY + 0.02f;
            for (int it = 0; it < 3; it++)
            {
                for (int i = 0; i < N - 1; i++)
                {
                    Link(ref _f[i], ref _f[i + 1], _segF, i == 0);
                    Link(ref _b[i], ref _b[i + 1], _segB, i == 0);
                }
                for (int i = 1; i < N; i++) Width(ref _f[i], ref _b[i], _width[i]);
                for (int i = 1; i < N; i++)
                {
                    FloorCollide(ref _f[i], ref _pf[i], floor);
                    FloorCollide(ref _b[i], ref _pb[i], floor);
                }
            }
        }

        static void ShapePull(ref Vector2 p, ref Vector2 prev, Vector2 target, float alpha)
        {
            Vector2 delta = (target - p) * alpha;
            p += delta;
            // Bleed most of the correction into prev so shape matching restores silhouette without launching particles
            prev += delta * 0.72f;
        }

        static void FloorCollide(ref Vector2 p, ref Vector2 prev, float floorY)
        {
            if (p.y < floorY)
            {
                p.y = floorY;
                // Ground friction so fallen coats settle on the floor instead of skating/jittering
                prev.x = Mathf.Lerp(prev.x, p.x, 0.25f);
                if (prev.y < floorY) prev.y = floorY;
            }
        }

        static bool IsFinite(Vector2 v) => !float.IsNaN(v.x) && !float.IsInfinity(v.x) && !float.IsNaN(v.y) && !float.IsInfinity(v.y);

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
            float target = Mathf.Clamp(len, rest * 0.75f, rest * 1.25f);
            Vector2 corr = d * ((len - target) / len) * 0.5f;
            a += corr; b -= corr;
        }
    }

    /// <summary>
    /// Spring chains (capes, ponytails, scarves, tassels, sleeves). Each chain is a Verlet rope pinned at its root,
    /// bound smoothly to mesh vertices by its two nearest chains. Refined with hit impulses, knockdown gravity drape,
    /// framerate-independent viscoelastic shape matching, floor friction, and 180-deg-safe joint rotation.
    /// </summary>
    public class SoftChains
    {
        sealed class Chain
        {
            public Vector2[] rest;
            public float[] restAng;
            public float[] segLen;      // cached per-tick world segment lengths
            public Vector2[] p, prev;
            public Vector2[] local;
            public float[] jointAng;
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
        Vector2 _pendingImpulse;
        float _pendingTipBias = 1f;
        float _impulseRelax;

        public float groundY = float.MinValue;
        public Wind wind;
        public const float WindPush = 6f;
        /// <summary>When true, tilting the parent bone (knockdown/flip) softens shape matching so chains drape with gravity.</summary>
        public bool relaxOnTilt = true;

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
                var c = new Chain
                {
                    rest = r,
                    restAng = new float[r.Length - 1],
                    segLen = new float[r.Length - 1],
                    p = new Vector2[r.Length],
                    prev = new Vector2[r.Length],
                    local = new Vector2[r.Length],
                    jointAng = new float[r.Length]
                };
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

        /// <summary>
        /// Injects a world-space hit/knockback/landing impulse into all chains. Safe to call during hit-stop.
        /// </summary>
        public void AddImpulse(Vector2 worldImpulse, float tipBias = 1f)
        {
            _pendingImpulse += worldImpulse;
            _pendingTipBias = Mathf.Max(0f, tipBias);
            _impulseRelax = 1f;
        }

        public void Reset()
        {
            foreach (var c in _c)
                for (int i = 0; i < c.rest.Length; i++) c.p[i] = c.prev[i] = _bone.TransformPoint(c.rest[i]);
            _bodyVel = Vector2.zero;
            _pendingImpulse = Vector2.zero;
            _impulseRelax = 0f;
            Deform();
        }

        public void Tick(float dt, float facing, Vector2 bodyVel = default)
        {
            if (dt > 0f)
            {
                _time += dt;
                const int sub = 3;
                float clampedDt = Mathf.Min(dt, 1f / 30f);
                float h = clampedDt / sub;

                if (((Vector2)_bone.TransformPoint(_c[0].rest[0]) - _c[0].p[0]).sqrMagnitude > 1.6f) Reset();

                _bodyVel = Vector2.Lerp(_bodyVel, bodyVel, 1f - Mathf.Exp(-12f * clampedDt));
                _impulseRelax = Mathf.MoveTowards(_impulseRelax, 0f, clampedDt * 2.4f);

                // Cache world segment lengths once per tick (respects bone scale without calling TransformVector in inner loop)
                for (int k = 0; k < _c.Length; k++)
                {
                    var c = _c[k];
                    for (int i = 0; i < c.segLen.Length; i++)
                        c.segLen[i] = ((Vector2)_bone.TransformVector(c.rest[i + 1] - c.rest[i])).magnitude;
                }

                for (int s = 0; s < sub; s++) Step(h, facing, s == 0);

                // NaN safety check
                for (int k = 0; k < _c.Length; k++)
                {
                    Vector2 tip = _c[k].p[_c[k].p.Length - 1];
                    if (float.IsNaN(tip.x) || float.IsInfinity(tip.x) || float.IsNaN(tip.y) || float.IsInfinity(tip.y))
                    {
                        Reset();
                        break;
                    }
                }
            }
            Deform();
        }

        void Step(float h, float facing, bool applyImpulse)
        {
            Vector2 back = new Vector2(-facing, 0f);
            float speedFreedom = Mathf.Clamp01(_bodyVel.magnitude / 7f);
            float fallFreedom = Mathf.Clamp01(-_bodyVel.y / 5.5f);
            float upright = relaxOnTilt ? Mathf.Clamp01(Vector2.Dot(((Vector2)_bone.up).normalized, Vector2.up)) : 1f;
            float tiltScale = Mathf.Lerp(0.15f, 1f, upright);
            float impulseScale = Mathf.Lerp(1f, 0.2f, _impulseRelax);
            float damp = Mathf.Exp(-2.72f * h);

            bool hasImpulse = applyImpulse && _pendingImpulse.sqrMagnitude > 1e-6f;
            Vector2 imp = hasImpulse ? Vector2.ClampMagnitude(_pendingImpulse, 26f) : Vector2.zero;
            if (hasImpulse) _pendingImpulse = Vector2.zero;

            float floor = groundY + 0.02f;

            for (int k = 0; k < _c.Length; k++)
            {
                var c = _c[k];
                int n = c.rest.Length;
                float gust = _feel.breeze * (0.6f + Mathf.PerlinNoise(_time * 0.8f + k * 3.1f, 1.9f) * 0.9f);
                Vector2 air = back * gust;
                c.p[0] = c.prev[0] = _bone.TransformPoint(c.rest[0]);

                bool windy = !wind.IsCalm;
                Vector2 blow = windy ? wind.At(_time, c.p[0].x) * (_feel.drag * WindPush) : Vector2.zero;
                float windFreedom = windy ? Mathf.Clamp01(blow.magnitude / 45f) : 0f;
                float freedom = Mathf.Clamp01(Mathf.Max(speedFreedom, Mathf.Max(fallFreedom, windFreedom)));

                for (int i = 1; i < n; i++)
                {
                    float u = i / (float)(n - 1);
                    if (hasImpulse)
                    {
                        float iw = Mathf.Lerp(0.25f, 1f, Mathf.Pow(u, _pendingTipBias));
                        c.prev[i] -= imp * (iw * h);
                    }

                    Vector2 target = _bone.TransformPoint(c.rest[i]);
                    Vector2 v = (c.p[i] - c.prev[i]) / h;
                    float flutter = Mathf.Sin(_time * 5.7f - i * 1.1f + k * 2.3f) * (1.5f + 5.5f * freedom) * u * u;
                    Vector2 airLocal = air + new Vector2(0f, fallFreedom * 2f * u);

                    Vector2 acc = new Vector2(0f, -_feel.gravity)
                                  + Vector2.ClampMagnitude((airLocal - v) * _feel.drag * Mathf.Lerp(0.5f, 1.5f, u), 60f)
                                  + new Vector2(0f, flutter);
                    if (windy) acc += Vector2.ClampMagnitude(blow * Mathf.Lerp(0.5f, 1.5f, u), 160f);

                    Vector2 vel = (c.p[i] - c.prev[i]) * damp;
                    c.prev[i] = c.p[i];
                    c.p[i] += vel + acc * h * h;

                    // Framerate-independent viscoelastic shape matching
                    float stiff = Mathf.Lerp(_feel.stiffRoot, _feel.stiffTip, Mathf.Pow(u, 0.7f));
                    stiff *= Mathf.Lerp(1f, 0.4f, freedom * u) * tiltScale * impulseScale;
                    float alpha = 1f - Mathf.Pow(Mathf.Clamp01(1f - stiff), h * 180f);

                    Vector2 delta = (target - c.p[i]) * alpha;
                    c.p[i] += delta;
                    c.prev[i] += delta * 0.7f;
                }

                for (int it = 0; it < 3; it++)
                {
                    for (int i = 0; i < n - 1; i++)
                    {
                        float rest = c.segLen[i];
                        Vector2 d = c.p[i + 1] - c.p[i];
                        float len = d.magnitude;
                        if (len < 1e-6f) continue;
                        Vector2 corr = d * ((len - rest) / len);
                        if (i == 0) c.p[i + 1] -= corr;
                        else { c.p[i] += corr * 0.5f; c.p[i + 1] -= corr * 0.5f; }
                    }
                    for (int i = 1; i < n; i++)
                    {
                        if (c.p[i].y < floor)
                        {
                            c.p[i].y = floor;
                            c.prev[i].x = Mathf.Lerp(c.prev[i].x, c.p[i].x, 0.25f);
                            if (c.prev[i].y < floor) c.prev[i].y = floor;
                        }
                    }
                }
            }
        }

        void Deform()
        {
            foreach (var c in _c)
            {
                int n = c.rest.Length;
                for (int i = 0; i < n; i++) c.local[i] = _bone.InverseTransformPoint(c.p[i]);
                for (int i = 0; i < n; i++)
                {
                    float a0 = i > 0 ? Mathf.DeltaAngle(c.restAng[i - 1], KMath.Ang(c.local[i] - c.local[i - 1])) : float.NaN;
                    float a1 = i < n - 1 ? Mathf.DeltaAngle(c.restAng[i], KMath.Ang(c.local[i + 1] - c.local[i])) : float.NaN;
                    // Shortest-arc angle average prevents 180-deg flip glitches on tumbles/knockdowns
                    c.jointAng[i] = float.IsNaN(a0) ? a1 : float.IsNaN(a1) ? a0 : a0 + Mathf.DeltaAngle(a0, a1) * 0.5f;
                    if (i == 0) c.jointAng[i] *= 0.5f;
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
            float ang = Mathf.LerpAngle(c.jointAng[i], c.jointAng[i + 1], t);
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