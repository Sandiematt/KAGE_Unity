using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Crimson Crescent's look, as effect cues (ADR-0007): the projectile itself is the generic
    /// <see cref="Projectile"/> (data in Fighter.CrescentProjectile); this file is only what it looks like.
    /// <c>VFX_Kage_Crescent</c> is the flight (a spawn star and the layered crescent that rides the projectile),
    /// <c>VFX_Kage_CrescentHit</c> the ink burst on contact, <c>VFX_Kage_CrescentFade</c> the ring where it ends.
    /// </summary>
    public static class CrescentCues
    {
        public const string Flight = "VFX_Kage_Crescent", Hit = "VFX_Kage_CrescentHit", Fade = "VFX_Kage_CrescentFade";
        /// <summary>Mikazuki's giant crescent moon (the plain starter's rage art): the same look at <see cref="MoonScale"/>, and a bigger burst where it strikes.</summary>
        public const string MoonFlight = "VFX_Kage_MoonCrescent", MoonHit = "VFX_Kage_MoonCrescentHit";
        public const float MoonScale = 2.2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            EffectCues.Register(Flight, CrescentLook.Play);
            EffectCues.Register(Hit, PlayHit);
            EffectCues.Register(Fade, PlayFade);
            EffectCues.Register(MoonFlight, PlayMoon);
            EffectCues.Register(MoonHit, PlayMoonHit);
        }

        static void PlayMoon(Vector2 pos, in CueArgs a)
        {
            var big = a;
            big.scale = MoonScale;
            CrescentLook.Play(pos, big);
            if (a.owner != null) Vfx.I.Ring(pos, 0.3f, 2.2f, 0.12f, a.owner.fxMain, 0.35f, false);
        }

        static void PlayMoonHit(Vector2 pos, in CueArgs a)
        {
            if (a.owner == null) return;
            Vfx.I.InkBurst(pos, a.owner.fxDark, 1.6f);
            Vfx.I.Ring(pos, 0.2f, 1.6f, 0.1f, a.owner.fxMain, 0.3f, false);
        }

        static void PlayHit(Vector2 pos, in CueArgs a)
        {
            if (a.owner != null) Vfx.I.InkBurst(pos, a.owner.fxDark, 0.8f);
        }

        static void PlayFade(Vector2 pos, in CueArgs a)
        {
            if (a.owner != null) Vfx.I.Ring(pos, 0.2f, 0.9f, 0.08f, a.owner.fxMain, 0.25f, false);
        }
    }

    /// <summary>
    /// Layered crescent (dark alpha rim, saturated additive body, white-hot core) whose thickness
    /// breathes, shedding embers and fading echoes that stay where they were born. A child of the
    /// projectile: it animates itself on frame time and goes when the projectile does.
    /// </summary>
    public sealed class CrescentLook : MonoBehaviour
    {
        Fighter _owner;
        float _t, _life, _echoT;
        Transform _proj;
        Mesh _rim, _body, _core;
        MeshRenderer _rBody;

        public static void Play(Vector2 pos, in CueArgs a)
        {
            if (a.follow == null || a.owner == null) return;
            var go = new GameObject("CrescentLook");
            go.transform.SetParent(a.follow, false);
            go.transform.localPosition = Vector3.zero;
            float k = a.scale > 0f ? a.scale : 1f;
            go.transform.localScale = new Vector3((a.dir.x < 0f ? -1f : 1f) * k, k, 1f);
            go.AddComponent<CrescentLook>().Init(a.owner, a.follow, Mathf.Max(0.1f, a.seconds));
            Vfx.I.Star(pos, 0.6f, a.owner.fxCore, a.owner.fxMain);
        }

        void Init(Fighter owner, Transform projectile, float life)
        {
            _owner = owner; _proj = projectile; _life = life;
            _rim = new Mesh(); _body = new Mesh(); _core = new Mesh();
            int o = Vfx.Order + 30;
            MeshKit.MakeRenderer("Rim", transform, _rim, MeshKit.Alpha, o);
            _rBody = MeshKit.MakeRenderer("Body", transform, _body, Vfx.I.AddHDR(2.4f), o + 1);
            MeshKit.MakeRenderer("Core", transform, _core, Vfx.I.AddHDR(3.4f), o + 2);
            Build(0f);
        }

        void OnDestroy()
        {
            if (_rim != null) Destroy(_rim);
            if (_body != null) Destroy(_body);
            if (_core != null) Destroy(_core);
        }

        void Build(float u)
        {
            float breathe = 1f + Mathf.Sin(_t * 40f) * 0.06f;
            BuildCrescent(_rim, 0.62f * breathe, 0.2f, new Color(0.06f, 0f, 0.02f, 0.75f));
            BuildCrescent(_body, 0.56f * breathe, 0.15f * (1f - u * 0.35f), _owner.fxMain);
            BuildCrescent(_core, 0.5f * breathe, 0.06f * (1f - u * 0.5f), Color.white);
        }

        static void BuildCrescent(Mesh m, float r, float thick, Color c)
        {
            c = MeshKit.L(c);
            const int seg = 22;
            var v = new Vector3[(seg + 1) * 2];
            var col = new Color[v.Length];
            var tri = new int[seg * 6];
            for (int i = 0; i <= seg; i++)
            {
                float t = i / (float)seg;
                float a = Mathf.Lerp(-78f, 78f, t) * Mathf.Deg2Rad;
                float th = thick * Mathf.Sin(t * Mathf.PI);
                Vector2 d = new(Mathf.Cos(a), Mathf.Sin(a));
                v[i * 2] = d * r;
                v[i * 2 + 1] = d * (r - th) - new Vector2(th * 0.6f, 0f);
                float edge = Mathf.Sin(t * Mathf.PI);
                col[i * 2] = c.WithA(c.a * edge);
                col[i * 2 + 1] = c.WithA(c.a * edge * 0.15f);
                if (i < seg)
                {
                    int k = i * 6, b = i * 2;
                    tri[k] = b; tri[k + 1] = b + 1; tri[k + 2] = b + 2;
                    tri[k + 3] = b + 1; tri[k + 4] = b + 3; tri[k + 5] = b + 2;
                }
            }
            m.Clear();
            m.vertices = v;
            m.colors = col;
            m.triangles = tri;
            m.RecalculateBounds();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Vfx.I == null) return;
            _t += dt;
            Build(_t / _life);

            float dir = transform.localScale.x < 0f ? -1f : 1f;
            _echoT += dt;
            if (_echoT > 0.035f)
            {
                _echoT = 0f;
                Vfx.I.Echo(this, _rBody, 0.18f);
            }
            Vector2 p = _proj.position;
            Vfx.I.Embers(p + new Vector2(-dir * 0.2f, Random.Range(-0.45f, 0.45f)), new Vector2(-dir * Random.Range(0.5f, 2f), Random.Range(-0.4f, 0.8f)), _owner.fxMain, Random.Range(0.03f, 0.06f), Random.Range(0.25f, 0.45f));
        }
    }
}
