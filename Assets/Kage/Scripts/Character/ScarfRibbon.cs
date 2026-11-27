using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Kage's two scarf tails as verlet ropes (position-based dynamics): inertia from the body,
    /// gravity, a back-wind with travelling flutter, length constraints and ground collision.
    /// Rendered as twisting two-tone ribbons (front red / back deep red) like the reference.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ScarfRibbon : MonoBehaviour
    {
        class Tail
        {
            public Vector2[] p, prev, drawn;
            public float seg, width, phase;
            public Vector3 anchorLocal;
            public bool hasColours;
            public Color front, back;
            public Ribbon ribbon;
        }

        FighterRig _rig;
        RigDef _def;
        Tail[] _tails;
        public float alpha = 1f;
        public float wind = 8f;
        public float groundY = 0f;
        float _time;
        float _wavePhase;
        Vector2 _pendingImpulse;
        float _pendingTipBias = 1f;
        const int N = 14;

        public void Init(FighterRig rig, RigDef d)
        {
            _rig = rig;
            _def = d;
            Transform chest = rig.Bone(B.Chest);
            Vector2 cp = d.pivot[(int)B.Chest];
            var tails = new List<Tail>
            {
                MakeTail(d.scarfAnchorA - cp, 0.78f, 0.105f, 0f),
                MakeTail(d.scarfAnchorB - cp, 0.64f, 0.085f, 1.7f),
            };
            foreach (var s in d.scarfStrands)
            {
                var t = MakeTail(s.anchor - cp, s.length, s.width, s.phase);
                t.hasColours = true; t.front = s.front; t.back = s.back;
                tails.Add(t);
            }
            _tails = tails.ToArray();
            SetOrder(rig.baseOrder + 3);
            ResetChain();
        }

        Tail MakeTail(Vector2 anchorLocal, float length, float width, float phase)
        {
            var t = new Tail
            {
                p = new Vector2[N], prev = new Vector2[N], drawn = new Vector2[N],
                seg = length / (N - 1), width = width, phase = phase,
                anchorLocal = anchorLocal,
                ribbon = new Ribbon("ScarfTail", transform, MeshKit.Alpha, 0),
            };
            return t;
        }

        /// <summary>Injects a world-space whip/jolt impulse into all scarf strands (held safely across hit-stop).</summary>
        public void AddImpulse(Vector2 worldImpulse, float tipBias = 1f)
        {
            _pendingImpulse += worldImpulse;
            _pendingTipBias = Mathf.Max(0f, tipBias);
        }

        public void ResetChain()
        {
            if (_tails == null) return;
            _pendingImpulse = Vector2.zero;
            _wavePhase = 0f;
            Transform chest = _rig.Bone(B.Chest);
            foreach (var t in _tails)
            {
                Vector2 a = chest.TransformPoint(t.anchorLocal);
                for (int i = 0; i < N; i++)
                {
                    t.p[i] = a + new Vector2(-_rig.facing * t.seg * i, -0.01f * i);
                    t.prev[i] = t.p[i];
                }
            }
        }

        public void SetOrder(int order)
        {
            if (_tails == null) return;
            foreach (var t in _tails) t.ribbon.renderer.sortingOrder = order;
        }

        public void SetVisible(bool v)
        {
            if (_tails == null) return;
            foreach (var t in _tails) t.ribbon.renderer.enabled = v;
        }

        /// <summary>Runs the simulation for a number of 60 Hz steps and draws (edit-mode baking).</summary>
        public void Prewarm(int steps)
        {
            if (_tails == null) return;
            for (int i = 0; i < steps; i++) Tick(1f / 60f);
        }

        /// <summary>Steps the scarf simulation by <paramref name="dt"/> seconds and redraws the ribbons.</summary>
        public void Tick(float dt)
        {
            if (_tails == null || _rig == null) return;
            if (dt > 0f)
            {
                _time += dt;
                float clampedDt = Mathf.Min(dt, 1f / 30f);
                float windMag = !_rig.wind.IsCalm ? _rig.wind.At(_time, _rig.Bone(B.Chest).position.x).magnitude : 0f;
                _wavePhase += clampedDt * (1f + Mathf.Clamp(windMag * 0.04f, 0f, 0.30f));
                const int sub = 3;
                float h = clampedDt / sub;
                for (int s = 0; s < sub; s++) Step(h, s == 0);
            }
            Draw();
        }

        void LateUpdate()
        {
            if (_tails == null || _rig == null) return;
            Tick(Time.deltaTime);
        }

        void Step(float h, bool applyImpulse)
        {
            Transform chest = _rig.Bone(B.Chest);
            Vector2 back = new Vector2(-_rig.facing, 0f);
            float damp = Mathf.Exp(-4.2f * h);
            bool windy = !_rig.wind.IsCalm;
            bool hasImpulse = applyImpulse && _pendingImpulse.sqrMagnitude > 1e-6f;
            Vector2 imp = hasImpulse ? Vector2.ClampMagnitude(_pendingImpulse, 24f) : Vector2.zero;
            if (hasImpulse) _pendingImpulse = Vector2.zero;

            foreach (var t in _tails)
            {
                Vector2 anchor = chest.TransformPoint(t.anchorLocal);
                if ((anchor - t.p[0]).sqrMagnitude > 1.8f)
                {
                    for (int i = 0; i < N; i++) t.p[i] = t.prev[i] = anchor + new Vector2(-_rig.facing * t.seg * i, -0.01f * i);
                    continue;
                }
                t.p[0] = anchor;
                t.prev[0] = anchor;
                Vector2 worldWind = windy ? _rig.wind.At(_time, anchor.x) : Vector2.zero;
                // Scale wave energy with actual airflow so calm/Armory cards drape gently instead of flapping vertically
                float airStrength = Mathf.Clamp((Mathf.Abs(wind) + worldWind.magnitude * 1.6f) / 8f, 0.25f, 1.35f);
                for (int i = 1; i < N; i++)
                {
                    float k = i / (float)(N - 1);
                    if (hasImpulse)
                    {
                        float iw = Mathf.Lerp(0.25f, 1f, Mathf.Pow(k, _pendingTipBias));
                        t.prev[i] -= imp * (iw * h);
                    }
                    Vector2 vel = Vector2.ClampMagnitude((t.p[i] - t.prev[i]) * damp, 20f * h);
                    t.prev[i] = t.p[i];
                    // Slow, gentle macro breeze drift on the Verlet spine (avoids pendulum up-and-down bobbing; silky S-wave flows along the normal in Draw)
                    float env = KMath.Smooth01(k / 0.45f) * (0.5f + 0.5f * k);
                    float flutter = (Mathf.Sin(_wavePhase * 1.25f - k * 5.2f + t.phase) * 1.15f
                                    + Mathf.Sin(_wavePhase * 2.05f - k * 8.4f + t.phase * 1.4f) * 0.22f) * airStrength;
                    float gustWave = wind + (Mathf.PerlinNoise(_time * 0.22f, t.phase) * 1.6f
                                    + Mathf.Cos(_wavePhase * 0.9f - k * 3.2f + t.phase) * 0.45f) * airStrength;
                    Vector2 acc = new Vector2(0f, -3.4f) + back * gustWave + new Vector2(0f, flutter * env);
                    if (windy)
                    {
                        Vector2 windAccel = new Vector2(worldWind.x, worldWind.y * 0.3f) * (1.2f + 1.8f * k);
                        acc += Vector2.ClampMagnitude(windAccel, 90f);
                    }
                    // air drag on the world velocity: the tails stream behind dashes and lift on a fall
                    acc += Vector2.ClampMagnitude(-vel / h * (1.4f + 2.6f * k), 45f);
                    t.p[i] += vel + acc * h * h;
                }
                float upright = Mathf.Clamp01(Vector2.Dot(((Vector2)chest.up).normalized, Vector2.up));
                float seamLimitX = anchor.x + _rig.facing * 0.03f;
                for (int it = 0; it < 6; it++)
                {
                    t.p[0] = anchor;
                    for (int i = 0; i < N - 1; i++)
                    {
                        Vector2 d = t.p[i + 1] - t.p[i];
                        float len = d.magnitude;
                        if (len < 1e-6f) continue;
                        Vector2 corr = d * ((len - t.seg) / len);
                        if (i == 0) t.p[i + 1] -= corr;
                        else { t.p[i] += corr * 0.5f; t.p[i + 1] -= corr * 0.5f; }
                    }
                    // Keep scarf ribbons trailing cleanly behind the collar/knot seam instead of clipping forward across chest armor or face
                    if (upright > 0.55f)
                    {
                        for (int i = 1; i < N; i++)
                        {
                            if ((t.p[i].x - anchor.x) * _rig.facing > 0.03f)
                            {
                                t.p[i].x = seamLimitX;
                                if ((t.p[i].x - t.prev[i].x) * _rig.facing > 0f) t.prev[i].x = t.p[i].x;
                            }
                        }
                    }
                    for (int i = 1; i < N; i++) if (t.p[i].y < groundY + 0.015f) t.p[i].y = groundY + 0.015f;
                }
            }
        }

        void Draw()
        {
            Color edge = _def.scarfEdge;
            Transform chest = _rig.Bone(B.Chest);
            float windMag = !_rig.wind.IsCalm ? _rig.wind.At(_time, chest.position.x).magnitude : 0f;
            float airStrength = Mathf.Clamp((Mathf.Abs(wind) + windMag * 1.6f) / 8f, 0.30f, 1.35f);
            float upright = Mathf.Clamp01(Vector2.Dot(((Vector2)chest.up).normalized, Vector2.up));
            float floor = groundY + 0.015f;

            foreach (var t in _tails)
            {
                // Layer a silky, slow unidirectional travelling S-wave along the ribbon normal so the scarf flows gracefully without pendulum bobbing
                float tailLen = t.seg * (N - 1);
                float waveScale = Mathf.Min(0.036f, tailLen * 0.048f) * airStrength;
                float seamLimitX = t.p[0].x + _rig.facing * 0.03f;
                t.drawn[0] = t.p[0];
                for (int i = 1; i < N; i++)
                {
                    Vector2 a = t.p[i - 1], b = t.p[Mathf.Min(N - 1, i + 1)];
                    Vector2 d = b - a;
                    Vector2 tan = d.sqrMagnitude > 1e-8f ? d.normalized : new Vector2(-_rig.facing, 0f);
                    Vector2 nrm = new Vector2(-tan.y, tan.x);
                    float k = i / (float)(N - 1);
                    float sEnv = KMath.Smooth01(k / 0.35f) * (0.75f + 0.25f * Mathf.Sin(k * Mathf.PI));
                    float sWave = (Mathf.Sin(_wavePhase * 1.45f - k * 7.2f + t.phase) * 0.82f
                                 + Mathf.Sin(_wavePhase * 2.35f - k * 11.6f + t.phase * 1.3f) * 0.18f) * waveScale * sEnv;
                    Vector2 q = t.p[i] + nrm * sWave;
                    if (upright > 0.55f && (q.x - t.p[0].x) * _rig.facing > 0.03f) q.x = seamLimitX;
                    if (q.y < floor) q.y = floor;
                    t.drawn[i] = q;
                }

                Color front = t.hasColours ? t.front : _def.scarfColor, backC = t.hasColours ? t.back : _def.scarfBack;
                var r = t.ribbon;
                r.Begin();
                for (int i = 0; i < N; i++)
                {
                    Vector2 a = t.drawn[Mathf.Max(0, i - 1)], b = t.drawn[Mathf.Min(N - 1, i + 1)];
                    Vector2 d = b - a;
                    Vector2 tan = d.sqrMagnitude > 1e-8f ? d.normalized : new Vector2(-_rig.facing, 0f);
                    Vector2 nrm = new Vector2(-tan.y, tan.x);
                    float k = i / (float)(N - 1);
                    float twist = Mathf.Cos(_wavePhase * 0.95f - k * 2.8f + t.phase);
                    float w = t.width * Mathf.Pow(1f - k, 0.55f) * (0.64f + 0.36f * Mathf.Abs(twist));
                    if (i == 0) w = t.width * 0.9f;
                    Color body = Color.Lerp(backC, front, 0.5f + 0.5f * twist);
                    Color e = Color.Lerp(body, edge, twist > 0 ? 0.55f : 0.1f);
                    body.a *= alpha; e.a *= alpha;
                    Vector3 l = transform.InverseTransformPoint(t.drawn[i] + _rig.PaintOffset + nrm * w * 0.5f);
                    Vector3 rr = transform.InverseTransformPoint(t.drawn[i] + _rig.PaintOffset - nrm * w * 0.5f);
                    r.Add(l, rr, e, body, k);
                }
                r.End();
            }
        }

        public void SnapshotInto(Transform parent, int order)
        {
            if (_tails == null) return;
            foreach (var t in _tails)
            {
                var m = Instantiate(t.ribbon.mesh);
                var owner = parent.GetComponent<MeshOwner>() ?? parent.gameObject.AddComponent<MeshOwner>();
                owner.meshes.Add(m);
                var mr = MeshKit.MakeRenderer("ScarfGhost", parent, m, MeshKit.Alpha, order);
                mr.transform.SetPositionAndRotation(transform.position, transform.rotation);
                mr.transform.localScale = transform.lossyScale;
            }
        }
    }
}
