using UnityEngine;

namespace Kage
{
    /// <summary>
    /// A flowing piece cut from an SVG (hair, ribbons, tassels, fur, a cape, apron or skirt panels), moved the way Kage's hair is
    /// (<see cref="HairField"/>): every vertex turns about the point the piece is tied on at (<see cref="PartDef.flowRoot"/>, as
    /// Kage's hair turns about its skull centre) by an angle that grows from the root to the free end. The angle is a damped spring
    /// lagging behind the body's motion and the bone's turning, plus a travelling wind wave that never stops, so the piece sways and
    /// ripples even while the fighter stands still, and whips on a hit or a landing.
    /// <para>
    /// Unlike the hair (always behind the head) a piece can hang any way, so the drives are worked out in the world from the piece's
    /// own axis (root to its middle): the air the body moves through drags it back, the world wind (<see cref="wind"/>) blows it
    /// along, and gravity keeps it hanging the way it was drawn when the body tilts or lies down. It never sinks under the floor
    /// (<see cref="groundY"/>). How much of each comes from the part's <see cref="SoftPresets"/> feel: a light cape swings far and
    /// waves freely, heavy plates barely stir, tassels hang straight. No chains are drawn by hand: the root, the axis, the reach and
    /// each vertex's weight are measured from the mesh.
    /// </para>
    /// </summary>
    public sealed class FlowField
    {
        readonly Mesh _mesh;
        readonly Transform _bone, _rig;
        readonly Vector3[] _base, _out;
        readonly float[] _w, _u, _phi;
        readonly Vector2 _root, _axis;
        readonly float _reach, _seed;
        readonly float _k, _damp, _swing, _maxAng, _wave, _hang, _windGain, _angLag;

        float _time, _wavePhase, _lag, _lagVel, _prevAng, _angVel, _mirror;
        bool _hasPrev;
        Vector2 _trail, _trailVel, _bodyVel;
        Vector2 _pendingImpulse;
        float _pendingTipBias = 1f;

        /// <summary>The world wind (<see cref="Kage.Wind"/>). Calm by default: only the piece's own breeze wave.</summary>
        public Wind wind;
        /// <summary>The floor (world y) the piece drapes on.</summary>
        public float groundY = float.MinValue;

        /// <param name="mesh">the part's mesh, in <paramref name="bone"/>'s space</param>
        /// <param name="bone">the transform the mesh rides</param>
        /// <param name="rig">the fighter rig's root (bind orientation: where "hanging as drawn" points in the world)</param>
        /// <param name="root">the tie point, in <paramref name="bone"/>'s space</param>
        /// <param name="feel">the part's preset (<see cref="SoftPresets"/>)</param>
        /// <param name="seed">a phase offset, so neighbouring pieces do not wave in step</param>
        public FlowField(Mesh mesh, Transform bone, Transform rig, Vector2 root, ChainFeel feel, float seed = 0f)
        {
            _mesh = mesh;
            _mesh.MarkDynamic();
            _bone = bone;
            _rig = rig;
            _root = root;
            _seed = seed;
            _base = mesh.vertices;
            _out = new Vector3[_base.Length];
            int n = _base.Length;
            _w = new float[n]; _u = new float[n]; _phi = new float[n];

            // the axis: root to the piece's middle; the reach: the furthest vertex from the root
            Vector2 mean = Vector2.zero;
            float reach = 1e-4f;
            for (int i = 0; i < n; i++)
            {
                Vector2 d = (Vector2)_base[i] - root;
                mean += d;
                reach = Mathf.Max(reach, d.magnitude);
            }
            _axis = n > 0 && mean.sqrMagnitude > 1e-10f ? (mean / n).normalized : Vector2.down;
            _reach = reach;
            float along = 1e-4f;
            for (int i = 0; i < n; i++) along = Mathf.Max(along, Vector2.Dot((Vector2)_base[i] - root, _axis));
            for (int i = 0; i < n; i++)
            {
                Vector2 d = (Vector2)_base[i] - root;
                // how far down the piece a vertex is: along the axis (a panel's whole top edge stays on the belt), and a little by plain
                // distance (a tuft that sticks out sideways from the root still stirs)
                float t = Mathf.Max(Vector2.Dot(d, _axis) / along, d.magnitude / reach * 0.5f);
                _u[i] = Mathf.Clamp01(t);
                _w[i] = KMath.Smooth01(Mathf.InverseLerp(0.03f, 1f, t));
                _phi[i] = Mathf.Atan2(Cross(_axis, d),Vector2.Dot(_axis, d));
            }

            // the preset as spring, swing, wave and hang (smooth flowing wave and graceful damping)
            float loose = 1f - Mathf.Clamp01(feel.stiffRoot);
            _k = 40f + 300f * feel.stiffRoot;
            _damp = 2f * Mathf.Sqrt(_k) * 0.62f;
            _swing = 5f * feel.drag * loose;
            _maxAng = 12f + 60f * loose * loose;
            _wave = 2.8f * feel.breeze;
            _hang = Mathf.Clamp01(feel.gravity / 9f) * loose;
            _windGain = 1.6f * feel.drag;
            _angLag = 0.06f * loose;
            Reset();
        }

        /// <summary>How far the piece reaches from its root (bone units).</summary>
        public float Reach => _reach;

        /// <summary>The piece's axis from its root (bone space, unit).</summary>
        public Vector2 Axis => _axis;

        /// <summary>
        /// A world-space jolt (m/s) from a hit, a knockdown or a landing: the free end is flung with it, the root stays tied. Held
        /// through hit-stop and spent on the next tick.
        /// </summary>
        public void AddImpulse(Vector2 worldImpulse, float tipBias = 1f)
        {
            _pendingImpulse += worldImpulse;
            _pendingTipBias = Mathf.Max(0f, tipBias);
        }

        /// <summary>Back to the drawn shape (a round reset, or recovery from a bad frame).</summary>
        public void Reset()
        {
            _lag = _lagVel = 0f;
            _wavePhase = 0f;
            _trail = _trailVel = Vector2.zero;
            _bodyVel = Vector2.zero;
            _pendingImpulse = Vector2.zero;
            _hasPrev = false;
            _angVel = 0f;
            for (int i = 0; i < _base.Length; i++) _out[i] = _base[i];
            _mesh.vertices = _out;
            _mesh.RecalculateBounds();
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        /// <param name="bodyVel">the world velocity of the body (m/s), as the cloth and chains take it</param>
        public void Tick(float dt, float facing, Vector2 bodyVel = default)
        {
            if (dt <= 0f) return;      // hit-stop: hold still, keep the jolt for later
            _time += dt;
            dt = Mathf.Min(dt, 1f / 20f);

            Matrix4x4 m = _bone.localToWorldMatrix;
            float det = m.m00 * m.m11 - m.m01 * m.m10;
            float mirror = det < 0f ? -1f : 1f;
            Vector2 axisW = ((Vector2)m.MultiplyVector(_axis)).normalized;
            Vector2 rootW = m.MultiplyPoint3x4(_root);

            // the bone's turning (world deg/s); a paper-flip turn mirrors the frame: start the count again
            float ang = Mathf.Atan2(m.m10, m.m00) * Mathf.Rad2Deg;
            if (!_hasPrev || mirror != _mirror) { _prevAng = ang; _angVel = 0f; _hasPrev = true; _mirror = mirror; }
            _angVel = Mathf.Lerp(_angVel, Mathf.Clamp(Mathf.DeltaAngle(_prevAng, ang) / dt, -900f, 900f), 0.5f);
            _prevAng = ang;
            _bodyVel = Vector2.Lerp(_bodyVel, bodyVel, 1f - Mathf.Exp(-12f * dt));

            // the drives, in world degrees (anticlockwise on screen): the air drags the free end back from the motion, the wind blows
            // it along, gravity keeps it hanging as drawn while the body tilts, and it lags behind the bone's turning
            Vector2 windV = wind.IsCalm ? Vector2.zero : wind.At(_time, rootW.x);
            float windBoost = 1f + Mathf.Clamp(windV.magnitude * 0.10f, 0f, 0.75f);
            _wavePhase += dt * (1f + Mathf.Clamp((windBoost - 1f) * 0.28f, 0f, 0.28f));
            Vector2 restW = _rig != null ? ((Vector2)_rig.localToWorldMatrix.MultiplyVector(_axis)).normalized : axisW;
            float hang = Vector2.SignedAngle(axisW, restW) * _hang;
            float drive = Cross(axisW, -_bodyVel) * _swing + Cross(axisW, windV) * _windGain + hang - _angVel * _angLag;
            float target = Mathf.Clamp(drive, -_maxAng, _maxAng) * mirror;        // world turn to the bone's own turn

            // a jolt: a kick to the swing and to the trail, harder on a long loose piece
            if (_pendingImpulse.sqrMagnitude > 1e-6f)
            {
                Vector2 imp = Vector2.ClampMagnitude(_pendingImpulse, 26f);
                float tip = Mathf.Lerp(0.6f, 1.2f, Mathf.Clamp01(_pendingTipBias - 0.5f));
                _lagVel += Mathf.Clamp(Cross(axisW, imp) * 42f * tip * (_swing / 9f + 0.35f), -700f, 700f) * mirror;
                Vector2 impLocal = m.inverse.MultiplyVector(imp);
                _trailVel += Vector2.ClampMagnitude(impLocal * 0.05f, 0.6f);
                _pendingImpulse = Vector2.zero;
            }

            Vector2 vLocal = m.inverse.MultiplyVector(_bodyVel);
            Vector2 tt = Vector2.ClampMagnitude(-vLocal * 0.006f * (_swing / 9f), 0.05f * _reach);
            if (!wind.IsCalm) tt += Vector2.ClampMagnitude((Vector2)m.inverse.MultiplyVector(windV) * 0.003f * _windGain, 0.04f * _reach);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.008f));
            float h = dt / steps;
            for (int s = 0; s < steps; s++)
            {
                _lagVel += ((target - _lag) * _k - _lagVel * _damp) * h;
                _lag += _lagVel * h;
                _trailVel += ((tt - _trail) * 150f - _trailVel * 13f) * h;
                _trail += _trailVel * h;
            }
            if (!Finite(_lag) || !Finite(_lagVel) || !Finite(_trail.x) || !Finite(_trail.y) || !Finite(_trailVel.x) || !Finite(_trailVel.y))
            {
                Reset();
                return;
            }

            // Smooth outward-travelling wind wave: slow swell and phase-locked ripple flowing from root to free end
            float gust = (Mathf.PerlinNoise(_time * 0.22f, 3.7f + _seed) - 0.5f) * 0.9f * _wave * windBoost;
            float amp = _wave * windBoost;
            float tm = _wavePhase + _seed;
            for (int i = 0; i < _base.Length; i++)
            {
                float w = _w[i];
                if (w <= 0.0005f) { _out[i] = _base[i]; continue; }
                float u = _u[i], phi = _phi[i];
                float wave = Mathf.Sin(tm * 1.20f + phi * 1.5f - u * 4.4f) * amp
                           + Mathf.Sin(tm * 1.95f + phi * 2.6f - u * 7.2f) * amp * 0.18f;
                float a = (_lag + gust + wave) * w * Mathf.Deg2Rad;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                float dx = _base[i].x - _root.x, dy = _base[i].y - _root.y;
                _out[i] = new Vector3(_root.x + dx * cs - dy * sn + _trail.x * w * w, _root.y + dx * sn + dy * cs + _trail.y * w * w, 0f);
            }

            // drape on the floor: nothing that moves goes under it (only checked when the piece can reach it)
            if (groundY > float.MinValue * 0.5f)
            {
                float floor = groundY + 0.02f;
                float reachW = ((Vector2)m.MultiplyVector(new Vector2(_reach, 0f))).magnitude + 0.1f;
                if (rootW.y - reachW < floor)
                {
                    Matrix4x4 inv = m.inverse;
                    for (int i = 0; i < _out.Length; i++)
                    {
                        // every vertex, the tied-on ones too: lying on his back, a cape's top edge is pressed flat under the body
                        Vector3 p = m.MultiplyPoint3x4(_out[i]);
                        if (p.y >= floor) continue;
                        p.y = floor;
                        _out[i] = inv.MultiplyPoint3x4(p);
                        _out[i].z = 0f;
                    }
                }
            }
            _mesh.vertices = _out;
            _mesh.RecalculateBounds();
        }

        static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
