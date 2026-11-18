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
            public Vector2[] p, prev;
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
        public float wind = 9f;
        public float groundY = 0f;
        float _time;
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
                p = new Vector2[N], prev = new Vector2[N], seg = length / (N - 1), width = width, phase = phase,
                anchorLocal = anchorLocal,
                ribbon = new Ribbon("ScarfTail", transform, MeshKit.Alpha, 0),
            };
            return t;
        }

        public void ResetChain()
        {
            if (_tails == null) return;
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
            for (int i = 0; i < steps; i++) { _time += 1f / 60f; for (int s = 0; s < 3; s++) Step(1f / 180f); }
            Draw();
        }

        void LateUpdate()
        {
            if (_tails == null || _rig == null) return;
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                _time += dt;
                const int sub = 3;
                float h = Mathf.Min(dt, 1f / 30f) / sub;
                for (int s = 0; s < sub; s++) Step(h);
            }
            Draw();
        }

        void Step(float h)
        {
            Transform chest = _rig.Bone(B.Chest);
            Vector2 back = new Vector2(-_rig.facing, 0f);
            foreach (var t in _tails)
            {
                t.p[0] = chest.TransformPoint(t.anchorLocal);
                t.prev[0] = t.p[0];
                for (int i = 1; i < N; i++)
                {
                    float k = i / (float)(N - 1);
                    Vector2 vel = (t.p[i] - t.prev[i]) * 0.985f;
                    t.prev[i] = t.p[i];
                    float flutter = Mathf.Sin(_time * 7.5f - i * 0.75f + t.phase) * 7f + Mathf.Sin(_time * 3.1f + i * 0.4f) * 3f;
                    Vector2 acc = new Vector2(0f, -4.2f) + back * (wind + Mathf.PerlinNoise(_time * 0.7f, t.phase) * 4f) + new Vector2(0f, flutter * (0.35f + k));
                    // air drag on the world velocity: the tails stream behind dashes and lift on a fall
                    acc += Vector2.ClampMagnitude(-vel / h * (0.6f + 2.2f * k), 40f);
                    t.p[i] += vel + acc * h * h;
                }
                for (int it = 0; it < 6; it++)
                {
                    for (int i = 0; i < N - 1; i++)
                    {
                        Vector2 d = t.p[i + 1] - t.p[i];
                        float len = d.magnitude;
                        if (len < 1e-6f) continue;
                        Vector2 corr = d * ((len - t.seg) / len);
                        if (i == 0) t.p[i + 1] -= corr;
                        else { t.p[i] += corr * 0.5f; t.p[i + 1] -= corr * 0.5f; }
                    }
                    for (int i = 1; i < N; i++) if (t.p[i].y < groundY + 0.015f) t.p[i].y = groundY + 0.015f;
                }
            }
        }

        void Draw()
        {
            Color edge = _def.scarfEdge;
            foreach (var t in _tails)
            {
                Color front = t.hasColours ? t.front : _def.scarfColor, backC = t.hasColours ? t.back : _def.scarfBack;
                var r = t.ribbon;
                r.Begin();
                for (int i = 0; i < N; i++)
                {
                    Vector2 a = t.p[Mathf.Max(0, i - 1)], b = t.p[Mathf.Min(N - 1, i + 1)];
                    Vector2 tan = (b - a).normalized;
                    Vector2 nrm = new Vector2(-tan.y, tan.x);
                    float k = i / (float)(N - 1);
                    float twist = Mathf.Cos(k * 3.2f - _time * 4.2f + t.phase);
                    float w = t.width * Mathf.Pow(1f - k, 0.55f) * (0.45f + 0.55f * Mathf.Abs(twist));
                    if (i == 0) w = t.width * 0.9f;
                    Color body = Color.Lerp(backC, front, 0.5f + 0.5f * twist);
                    Color e = Color.Lerp(body, edge, twist > 0 ? 0.55f : 0.1f);
                    body.a *= alpha; e.a *= alpha;
                    Vector3 l = transform.InverseTransformPoint(t.p[i] + nrm * w * 0.5f);
                    Vector3 rr = transform.InverseTransformPoint(t.p[i] - nrm * w * 0.5f);
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
                var mr = MeshKit.MakeRenderer("ScarfGhost", parent, m, MeshKit.Alpha, order);
                mr.transform.SetPositionAndRotation(transform.position, transform.rotation);
                mr.transform.localScale = transform.lossyScale;
            }
        }
    }
}
