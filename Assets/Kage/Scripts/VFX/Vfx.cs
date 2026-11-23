using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Procedural VFX built on the effekseer-vfx craft rules: every effect has a time structure
    /// (anticipation → contact flash → body → secondary → dissipation), value layering (white-hot
    /// core, saturated body, dark rim/residue), a body that changes shape (sweeps open, erodes),
    /// eased particle travel (fast then hanging), and screen feedback (hit-stop, shake, flash).
    /// Contact flashes and sparks run on unscaled time so they bloom during hit-stop.
    /// </summary>
    public partial class Vfx : MonoBehaviour
    {
        public static Vfx I;
        ParticleSystem _sparks, _dust, _embers, _motes;
        readonly List<Fx> _live = new();
        readonly Dictionary<int, Material> _hdrAdd = new();
        public const int Order = 4000;

        abstract class Fx
        {
            public float t, life;
            public bool unscaled;
            public GameObject go;
            public abstract void Tick(float u);
            public virtual void Kill() { if (go != null) Destroy(go); }
        }

        public Material AddHDR(float k)
        {
            int key = Mathf.RoundToInt(k * 100f);
            if (_hdrAdd.TryGetValue(key, out var m) && m != null) return m;
            m = MeshKit.MakeMat(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One, "Kage AddHDR " + k, null);
            m.SetVector("_Color", new Vector4(k, k, k, 1f));
            _hdrAdd[key] = m;
            return m;
        }

        void Awake()
        {
            I = this;
            _sparks = MakePS("Sparks", AddHDRSoft(2.6f), Order + 40, true, ParticleSystemRenderMode.Stretch, drag: 5.5f, gravity: 0.55f, sizeEnd: 0.1f);
            _dust = MakePS("Dust", MeshKit.SoftAlpha, Order - 20, false, ParticleSystemRenderMode.Billboard, drag: 3.2f, gravity: -0.03f, sizeEnd: 1.7f, fadeIn: true);
            _embers = MakePS("Embers", AddHDRSoft(2.2f), Order + 30, false, ParticleSystemRenderMode.Billboard, drag: 1.2f, gravity: -0.05f, sizeEnd: 0.2f);
            _motes = MakePS("Motes", MeshKit.SoftAlpha, Order + 10, false, ParticleSystemRenderMode.Billboard, drag: 0f, gravity: 0f, sizeEnd: 0.3f);
        }

        Material AddHDRSoft(float k)
        {
            var m = MeshKit.MakeMat(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One, "Kage SoftHDR " + k, MeshKit.SoftDot);
            m.SetVector("_Color", new Vector4(k, k, k, 1f));
            return m;
        }

        ParticleSystem MakePS(string name, Material mat, int order, bool unscaled, ParticleSystemRenderMode mode, float drag, float gravity, float sizeEnd, bool fadeIn = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = unscaled;
            main.gravityModifier = gravity;
            main.startSpeed = 0f;
            main.startLifetime = 0.5f;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                fadeIn
                    ? new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) }
                    : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, sizeEnd));
            if (drag > 0f)
            {
                var lim = ps.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.limit = 100f;
                lim.drag = drag;
            }
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            r.sortingOrder = order;
            if (mode == ParticleSystemRenderMode.Stretch) { r.velocityScale = 0.035f; r.lengthScale = 1.2f; }
            ps.Play();
            return ps;
        }

        static void Emit(ParticleSystem ps, Vector2 p, Vector2 v, float size, float life, Color c, float rot = 0f)
        {
            var ep = new ParticleSystem.EmitParams
            {
                position = new Vector3(p.x, p.y, 0f),
                velocity = new Vector3(v.x, v.y, 0f),
                startSize = size,
                startLifetime = life,
                startColor = c,
                rotation = rot,
            };
            ps.Emit(ep, 1);
        }

        void Update()
        {
            float dt = Time.deltaTime, ud = Time.unscaledDeltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var f = _live[i];
                f.t += f.unscaled ? ud : dt;
                if (f.t >= f.life) { f.Kill(); _live.RemoveAt(i); continue; }
                f.Tick(f.t / f.life);
            }
        }

        // =========================================================================================
        //  MESH EFFECTS
        // =========================================================================================
        class StarFx : Fx
        {
            public MeshRenderer r; public float size, spin;
            public override void Tick(float u)
            {
                float pop = u < 0.25f ? KMath.Apply(Ease.OutExpo, u / 0.25f) : 1f;
                float s = size * Mathf.Lerp(0.5f, 1.25f, pop) * (u < 0.25f ? 1f : Mathf.Lerp(1f, 0.35f, (u - 0.25f) / 0.75f));
                go.transform.localScale = new Vector3(s, s * (u < 0.25f ? 1f : Mathf.Lerp(1f, 0.25f, (u - 0.25f) / 0.75f)), 1f);
                go.transform.Rotate(0, 0, spin * Time.unscaledDeltaTime);
                var mpb = new MaterialPropertyBlock();
                float a = u < 0.3f ? 1f : 1f - (u - 0.3f) / 0.7f;
                mpb.SetVector("_Color", new Vector4(3f, 3f, 3f, a));
                r.SetPropertyBlock(mpb);
            }
        }

        StarFx Star(Vector2 p, float size, Color core, Color tip, float life, int points, bool unscaled, int order, float aspect = 1f)
        {
            var go = new GameObject("Flash");
            go.transform.SetParent(transform, false);
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
            var mesh = MeshKit.Star(points, 1f, 0.22f * aspect, core, tip.WithA(0f));
            var r = MeshKit.MakeRenderer("FlashMesh", go.transform, mesh, MeshKit.Additive, order);
            var fx = new StarFx { go = go, r = r, size = size, life = life, unscaled = unscaled, spin = Random.Range(-90f, 90f) };
            _live.Add(fx);
            fx.Tick(0f);
            return fx;
        }

        /// <summary>Dynamic-mesh effect: a callback rebuilds a ribbon every frame.</summary>
        class DynFx : Fx
        {
            public Ribbon rib;
            public System.Action<Ribbon, float> build;
            public override void Tick(float u)
            {
                rib.Begin();
                build(rib, u);
                rib.End();
            }
        }

        DynFx Dyn(string name, Material mat, int order, float life, bool unscaled, System.Action<Ribbon, float> build)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var rib = new Ribbon(name, go.transform, mat, order);
            var fx = new DynFx { go = go, rib = rib, build = build, life = life, unscaled = unscaled };
            _live.Add(fx);
            fx.Tick(0f);
            return fx;
        }

        /// <summary>Expanding ring (shockwave). sy squashes it into a ground ellipse.</summary>
        public void Ring(Vector2 c, float r0, float r1, float w0, Color col, float life, bool unscaled, float sy = 1f, float k = 2f, int order = Order + 20)
        {
            Dyn("Ring", AddHDR(k), order, life, unscaled, (rib, u) =>
            {
                float e = KMath.Apply(Ease.OutExpo, u);
                float r = Mathf.Lerp(r0, r1, e);
                float w = Mathf.Lerp(w0, 0f, KMath.Apply(Ease.InQuad, u));
                Color cc = col.WithA(col.a * (1f - u));
                const int seg = 40;
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    Vector2 d = new(Mathf.Cos(a), Mathf.Sin(a) * sy);
                    rib.Add(c + d * (r + w * 0.5f), c + d * Mathf.Max(0f, r - w * 0.5f), cc.WithA(0f), cc);
                }
            });
        }

        /// <summary>Speed-line shards: grow out fast, then the tail catches the tip (erode), core→body colour.</summary>
        void Shards(Vector2 c, Vector2 dir, int n, float len, float width, Color core, Color body, float life, float spreadDeg)
        {
            var ang = new float[n]; var l = new float[n]; var w = new float[n]; var delay = new float[n];
            float baseA = KMath.Ang(dir);
            for (int i = 0; i < n; i++)
            {
                bool back = i % 4 == 3;
                ang[i] = baseA + (back ? 180f : 0f) + Random.Range(-spreadDeg, spreadDeg);
                l[i] = len * Random.Range(0.45f, 1.1f) * (back ? 0.5f : 1f);
                w[i] = width * Random.Range(0.6f, 1.2f);
                delay[i] = Random.Range(0f, 0.15f);
            }
            Dyn("Shards", AddHDR(2.4f), Order + 25, life, true, (rib, u) =>
            {
                for (int i = 0; i < n; i++)
                {
                    float uu = Mathf.Clamp01((u - delay[i]) / (1f - delay[i]));
                    if (uu <= 0f) continue;
                    Vector2 d = KMath.Dir(ang[i]);
                    Vector2 nrm = new(-d.y, d.x);
                    float tip = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(uu / 0.55f)) * l[i];
                    float tail = KMath.Apply(Ease.InQuad, uu) * l[i];
                    float ww = w[i] * (1f - uu * 0.7f);
                    Vector2 a = c + d * (0.05f + tail), b = c + d * (0.05f + tip);
                    Color ca = core.WithA(1f - uu), cb = body.WithA((1f - uu) * 0.9f);
                    rib.AddTriangle(a + nrm * ww, a - nrm * ww, b, ca, ca, cb.WithA(0f));
                }
            });
        }

        // =========================================================================================
        //  PUBLIC EFFECTS
        // =========================================================================================
        public void HitBurst(Vector2 p, Vector2 dir, float strength, bool sword, Color main, Color core, Color dark)
        {
            // contact flash (2–5 frames, 1.5–2x body size)
            Star(p, 0.42f * strength, core, main, 0.11f, 8, true, Order + 60);
            // body: shock ring + speed shards
            Ring(p, 0.05f, 0.55f * strength, 0.07f * strength, Color.Lerp(main, core, 0.3f), 0.24f, true, 1f, 2.2f);
            Shards(p, dir, sword ? 7 : 10, (sword ? 0.75f : 0.5f) * strength, (sword ? 0.018f : 0.03f) * strength, core, main, 0.22f, sword ? 25f : 65f);
            if (sword)
            {
                float a = KMath.Ang(dir) + (Random.value < 0.5f ? 55f : -55f);
                Vector2 d = KMath.Dir(a);
                SlashLine(p - d * 0.55f * strength, p + d * 0.55f * strength, main, core, 0.2f, 0.05f);
            }
            // secondary: sparks burst then hang and droop
            int n = Mathf.RoundToInt(16 * strength);
            for (int i = 0; i < n; i++)
            {
                float ang = KMath.Ang(dir) + Random.Range(-75f, 75f) + (i % 5 == 0 ? 180f : 0f);
                Vector2 v = KMath.Dir(ang) * Random.Range(3.5f, 9f) * strength;
                Emit(_sparks, p, v, Random.Range(0.025f, 0.05f), Random.Range(0.25f, 0.55f), Color.Lerp(core, main, Random.Range(0.2f, 1f)));
            }
            // dissipation: ink residue
            for (int i = 0; i < 3; i++)
                Emit(_dust, p + Random.insideUnitCircle * 0.1f, Random.insideUnitCircle * 0.6f + dir * 0.4f, Random.Range(0.3f, 0.5f) * strength, Random.Range(0.45f, 0.7f), dark.WithA(0.55f));
        }

        public void BlockBurst(Vector2 p, Vector2 towardAttacker)
        {
            Color steel = new(0.8f, 0.9f, 1f, 1f);
            Star(p, 0.3f, Color.white, steel, 0.09f, 4, true, Order + 60, 0.6f);
            float a0 = KMath.Ang(towardAttacker);
            Dyn("GuardArc", AddHDR(2f), Order + 20, 0.2f, true, (rib, u) =>
            {
                float r = Mathf.Lerp(0.18f, 0.42f, KMath.Apply(Ease.OutExpo, u));
                float w = 0.05f * (1f - u);
                for (int i = 0; i <= 16; i++)
                {
                    float a = (a0 - 70f + 140f * i / 16f) * Mathf.Deg2Rad;
                    Vector2 d = new(Mathf.Cos(a), Mathf.Sin(a));
                    float edge = Mathf.Sin(i / 16f * Mathf.PI);
                    rib.Add(p + d * (r + w), p + d * r, steel.WithA(0f), steel.WithA(edge * (1f - u)));
                }
            });
            for (int i = 0; i < 10; i++)
                Emit(_sparks, p, KMath.Dir(a0 + Random.Range(-60f, 60f)) * Random.Range(3f, 6.5f), 0.03f, Random.Range(0.18f, 0.35f), steel);
        }

        public void Dust(Vector2 p, float scale, float dirX)
        {
            for (int i = 0; i < 7; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Vector2 v = new Vector2(side * Random.Range(0.5f, 1.6f) + dirX * 0.8f, Random.Range(0.1f, 0.5f)) * scale;
                Emit(_dust, p + new Vector2(Random.Range(-0.15f, 0.15f), 0.05f), v, Random.Range(0.18f, 0.32f) * scale, Random.Range(0.45f, 0.8f), new Color(0.42f, 0.36f, 0.36f, 0.5f));
            }
        }

        public void Shockwave(Vector2 p, float radius, Color c)
        {
            Ring(p, 0.1f, radius, 0.08f, c, 0.35f, false, 0.22f, 2f);
            Ring(p, 0.05f, radius * 0.6f, 0.05f, Color.white, 0.22f, false, 0.22f, 1.5f);
            Dust(p, 1.1f, 0f);
        }

        public void Glint(Vector2 p, Color c)
        {
            Star(p, 0.35f, Color.white, c, 0.28f, 4, true, Order + 70, 0.35f);
        }

        /// <summary>A sword cut drawn through space: head sweeps a→b, then the tail erodes after it.</summary>
        public void SlashLine(Vector2 a, Vector2 b, Color main, Color core, float life = 0.28f, float width = 0.075f)
        {
            Vector2 d = (b - a);
            Vector2 n = new Vector2(-d.y, d.x).normalized;
            Dyn("SlashLine", AddHDR(2.8f), Order + 50, life, true, (rib, u) =>
            {
                float head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.3f));
                float tail = KMath.Apply(Ease.InCubic, Mathf.Clamp01((u - 0.15f) / 0.85f));
                const int seg = 18;
                for (int half = 0; half < 2; half++)
                {
                    rib.Break();
                    for (int i = 0; i <= seg; i++)
                    {
                        float s = Mathf.Lerp(tail, head, i / (float)seg);
                        float prof = Mathf.Sin(Mathf.Clamp01((s - tail) / Mathf.Max(0.001f, head - tail)) * Mathf.PI);
                        float w = width * prof * (1f - u * 0.5f);
                        Vector2 c = a + d * s + n * Mathf.Sin(s * Mathf.PI) * 0.04f;
                        Color cc = Color.Lerp(core, main, 0.15f).WithA(1f - u * 0.6f);
                        Color ce = main.WithA(0f);
                        if (half == 0) rib.Add(c + n * w, c, ce, cc);
                        else rib.Add(c, c - n * w, cc, ce);
                    }
                }
            });
            // dark residue line under the cut (value layering: dark rim)
            Dyn("SlashResidue", MeshKit.Alpha, Order + 45, life * 1.6f, true, (rib, u) =>
            {
                float tail = KMath.Apply(Ease.InQuad, u);
                for (int i = 0; i <= 12; i++)
                {
                    float s = Mathf.Lerp(tail, 1f, i / 12f);
                    float w = width * 1.8f * Mathf.Sin(s * Mathf.PI) * (1f - u);
                    Vector2 c = a + d * s;
                    Color k = new Color(0.08f, 0.01f, 0.02f, 0.55f * (1f - u));
                    rib.Add(c + n * w, c - n * w, k, k);
                }
            });
        }

        /// <summary>A fading flat snapshot of <paramref name="rig"/>, moved by <paramref name="offset"/> (metres; the perfect dodge's trail).</summary>
        public void Ghost(FighterRig rig, Color tint, float life, Vector2 offset = default)
        {
            var root = rig.Snapshot(tint, rig.baseOrder - 40);
            root.transform.SetParent(transform, true);
            if (offset != Vector2.zero) root.transform.position += (Vector3)offset;
            var rs = root.GetComponentsInChildren<MeshRenderer>();
            var fx = new GhostFx { go = root, rs = rs, tint = tint, life = life };
            _live.Add(fx);
            fx.Tick(0f);
        }

        class GhostFx : Fx
        {
            public MeshRenderer[] rs; public Color tint;
            readonly MaterialPropertyBlock _mpb = new();
            public override void Tick(float u)
            {
                float a = 0.55f * (1f - KMath.Apply(Ease.OutQuad, u));
                _mpb.SetVector("_Color", new Vector4(1f, 1f, 1f, a));
                _mpb.SetColor("_FlashColor", Color.Lerp(tint, Color.black, 0.35f + 0.4f * u));
                _mpb.SetFloat("_Flash", 1f);
                foreach (var r in rs) if (r != null) r.SetPropertyBlock(_mpb);
                go.transform.position += new Vector3(0f, Time.deltaTime * 0.15f, 0f);
            }
        }

        /// <summary>Dark element charge: motes drawn inward, alpha-black body, red sparks.</summary>
        public void ChargeDark(Vector2 c, Color main, Color dark)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.55f, 0.95f);
                float life = Random.Range(0.18f, 0.3f);
                Emit(_motes, c + off, -off / life, Random.Range(0.07f, 0.14f), life, new Color(0.05f, 0.0f, 0.02f, 0.85f));
            }
            Vector2 o2 = Random.insideUnitCircle.normalized * Random.Range(0.4f, 0.7f);
            Emit(_embers, c + o2, -o2 * 3f, 0.05f, 0.22f, main);
        }

        public void InkBurst(Vector2 c, Color dark, float scale)
        {
            for (int i = 0; i < 18; i++)
            {
                Vector2 v = Random.insideUnitCircle.normalized * Random.Range(1.5f, 4.5f) * scale;
                Emit(_dust, c + Random.insideUnitCircle * 0.2f, v, Random.Range(0.25f, 0.5f) * scale, Random.Range(0.4f, 0.75f), new Color(0.04f, 0.0f, 0.02f, 0.85f));
            }
            for (int i = 0; i < 10; i++)
                Emit(_embers, c, Random.insideUnitCircle.normalized * Random.Range(2f, 5f), 0.05f, Random.Range(0.3f, 0.6f), new Color(0.9f, 0.2f, 0.25f, 1f));
            Ring(c, 0.1f, 1.1f * scale, 0.12f, new Color(0.85f, 0.15f, 0.2f, 0.9f), 0.35f, true, 1f, 1.8f);
        }

        public void KoBurst(Vector2 p, Color a, Color b)
        {
            Star(p, 1.1f, Color.white, a, 0.25f, 12, true, Order + 80);
            Ring(p, 0.1f, 2.4f, 0.16f, a, 0.5f, true, 1f, 2.4f);
            Ring(p, 0.1f, 1.4f, 0.1f, Color.white, 0.35f, true, 1f, 2f);
            Shards(p, Vector2.right, 16, 1.4f, 0.05f, Color.white, b, 0.4f, 180f);
            for (int i = 0; i < 40; i++)
                Emit(_sparks, p, Random.insideUnitCircle.normalized * Random.Range(3f, 11f), Random.Range(0.03f, 0.06f), Random.Range(0.4f, 0.9f), Color.Lerp(Color.white, a, Random.value));
            InkBurst(p, Color.black, 1.3f);
            GameFeel.Flash(new Color(1f, 0.9f, 0.85f, 0.65f), 0.35f);
        }

        public void Star(Vector2 p, float size, Color core, Color main) => Star(p, size, core, main, 0.14f, 6, true, Order + 60);

        /// <summary>Freezes a copy of a renderer's current mesh where it is and fades it (projectile echoes).</summary>
        public void Echo(Component owner, MeshRenderer src, float life)
        {
            var mf = src.GetComponent<MeshFilter>();
            if (mf == null) return;
            var go = new GameObject("Echo");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
            go.transform.localScale = src.transform.lossyScale;
            var r = MeshKit.MakeRenderer("EchoMesh", go.transform, Instantiate(mf.sharedMesh), src.sharedMaterial, src.sortingOrder - 3);
            var fx = new EchoFx { go = go, r = r, life = life };
            _live.Add(fx);
            fx.Tick(0f);
        }

        class EchoFx : Fx
        {
            public MeshRenderer r;
            readonly MaterialPropertyBlock _mpb = new();
            public override void Tick(float u)
            {
                float k = r.sharedMaterial.HasProperty("_Color") ? r.sharedMaterial.GetVector("_Color").x : 1f;
                _mpb.SetVector("_Color", new Vector4(k, k * 0.6f, k * 0.6f, 0.5f * (1f - u)));
                r.SetPropertyBlock(_mpb);
                go.transform.localScale = new Vector3(go.transform.localScale.x, go.transform.localScale.y * (1f + Time.deltaTime * 0.8f), 1f);
            }
            public override void Kill()
            {
                var mf = r != null ? r.GetComponent<MeshFilter>() : null;
                if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
                base.Kill();
            }
        }

        public void Embers(Vector2 p, Vector2 v, Color c, float size = 0.05f, float life = 0.4f) => Emit(_embers, p, v, size, life, c);
        public void Puff(Vector2 p, Vector2 v, Color c, float size, float life) => Emit(_dust, p, v, size, life, c);
        public void Spark(Vector2 p, Vector2 v, Color c, float size, float life) => Emit(_sparks, p, v, size, life, c);

        void OnDestroy()
        {
            foreach (var f in _live) f.Kill();
            _live.Clear();
            if (I == this) I = null;
        }
    }

    /// <summary>
    /// Weapon/limb smear: a ribbon through the last ~0.1 s of an edge (blade base→tip, or a
    /// fist/foot strip). White-hot leading edge, coloured body, fading tail.
    /// </summary>
    public class MotionTrail
    {
        struct S { public Vector2 a, b; public float t; }
        readonly List<S> _s = new();
        readonly Ribbon _rib;
        readonly float _life;
        readonly Color _main, _core;
        readonly bool _blade;
        readonly GameObject _go;

        public MotionTrail(string name, Transform parent, float life, Color main, Color core, int order, bool blade)
        {
            _go = new GameObject(name);
            _go.transform.SetParent(parent, false);
            _rib = new Ribbon(name, _go.transform, Vfx.I != null ? Vfx.I.AddHDR(blade ? 1.5f : 1.3f) : MeshKit.Additive, order);
            _life = life; _main = main; _core = core; _blade = blade;
        }

        public void Tick(bool emit, Vector2 a, Vector2 b)
        {
            float now = Time.time;
            if (emit && Time.deltaTime > 0f)
            {
                if (_s.Count == 0 || (_s[^1].a - a).sqrMagnitude > 1e-6f || (_s[^1].b - b).sqrMagnitude > 1e-6f)
                    _s.Add(new S { a = a, b = b, t = now });
            }
            while (_s.Count > 0 && now - _s[0].t > _life) _s.RemoveAt(0);
            _rib.Begin();
            if (_s.Count >= 2)
            {
                for (int i = 0; i < _s.Count; i++)
                {
                    float age = Mathf.Clamp01((now - _s[i].t) / _life);
                    float k = 1f - age;
                    // subdivide between samples for a smooth arc
                    if (i > 0)
                    {
                        var p = _s[i - 1];
                        for (int j = 1; j < 3; j++)
                        {
                            float f = j / 3f;
                            float kk = Mathf.Lerp(1f - Mathf.Clamp01((now - p.t) / _life), k, f);
                            Vector2 aa = Vector2.Lerp(p.a, _s[i].a, f), bb = Vector2.Lerp(p.b, _s[i].b, f);
                            AddSection(aa, bb, kk);
                        }
                    }
                    AddSection(_s[i].a, _s[i].b, k);
                }
            }
            _rib.End();
        }

        void AddSection(Vector2 a, Vector2 b, float k)
        {
            // value layering: white-hot leading edge, saturated body, fading dark-red tail
            float hot = k * k * k * k;
            Color inner = _blade ? Color.Lerp(_main * 0.6f, _main, k).WithA(0.12f * k) : _main.WithA(0.2f * k);
            Color outer = Color.Lerp(_main, _core, hot).WithA((_blade ? 0.85f : 0.6f) * Mathf.Sqrt(k));
            _rib.Add(a, b, inner, outer);
        }

        public void Dispose() { if (_go != null) Object.Destroy(_go); }
    }
}
