using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Crimson Crescent: Kage's shadow-art projectile. Layered crescent (dark alpha rim, saturated
    /// additive body, white-hot core) whose thickness breathes, shedding embers and fading
    /// echoes that stay where they were born. Paired impact = Vfx.HitBurst + InkBurst.
    /// </summary>
    public class Crescent : MonoBehaviour, IHasOwner
    {
        Fighter _owner;
        float _dir, _t, _echoT;
        const float Speed = 8.2f, Life = 1.15f;
        Mesh _rim, _body, _core;
        MeshRenderer _rRim, _rBody, _rCore;
        HitWindow _w;
        bool _done;
        public Fighter Owner => _owner;

        public static Crescent Spawn(Fighter owner, Vector2 pos, float dir)
        {
            var go = new GameObject("Crescent");
            go.transform.SetParent(KageGame.I != null ? KageGame.I.transform : null, false);
            go.transform.position = pos;
            var c = go.AddComponent<Crescent>();
            c.Init(owner, dir);
            return c;
        }

        void Init(Fighter owner, float dir)
        {
            _owner = owner;
            _dir = dir;
            transform.localScale = new Vector3(dir, 1f, 1f);
            _rim = new Mesh(); _body = new Mesh(); _core = new Mesh();
            int o = Vfx.Order + 30;
            _rRim = MeshKit.MakeRenderer("Rim", transform, _rim, MeshKit.Alpha, o);
            _rBody = MeshKit.MakeRenderer("Body", transform, _body, Vfx.I.AddHDR(2.4f), o + 1);
            _rCore = MeshKit.MakeRenderer("Core", transform, _core, Vfx.I.AddHDR(3.4f), o + 2);
            _w = new HitWindow { dmg = 10, level = HitLevel.Mid, kb = new Vector2(2.6f, 1.2f), stun = 22, hitstop = 7, shake = 0.22f, heavy = true, radius = 0.32f, sword = true };
            Build(0f);
            Vfx.I.Star(transform.position, 0.6f, owner.fxCore, owner.fxMain);
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
            if (_done) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            _t += dt;
            float u = _t / Life;
            transform.position += new Vector3(_dir * Speed * dt, 0f, 0f);
            Build(u);

            _echoT += dt;
            if (_echoT > 0.035f)
            {
                _echoT = 0f;
                Vfx.I.Echo(this, _rBody, 0.18f);
            }
            Vector2 p = transform.position;
            Vfx.I.Embers(p + new Vector2(-_dir * 0.2f, Random.Range(-0.45f, 0.45f)), new Vector2(-_dir * Random.Range(0.5f, 2f), Random.Range(-0.4f, 0.8f)), _owner.fxMain, Random.Range(0.03f, 0.06f), Random.Range(0.25f, 0.45f));

            // hit test: the crescent's leading arc
            Vector2 hp = p + new Vector2(_dir * 0.3f, 0f);
            if (TryHit(_owner.opponent, hp) || TryHit(_owner.extraTarget, hp)) return;

            if (u >= 1f || Mathf.Abs(p.x) > 9.5f) Fade();
        }

        bool TryHit(IHittable t, Vector2 hp)
        {
            if (t == null) return false;
            for (int i = -1; i <= 1; i++)
            {
                Vector2 q = hp + new Vector2(0f, i * 0.32f);
                if (!t.Overlap(q, _w.radius, _w, out var zone, out var contact)) continue;
                var info = new HitInfo { attacker = _owner, w = _w, dmg = _w.dmg, kb = new Vector2(_w.kb.x * _dir, _w.kb.y), zone = zone, contact = contact };
                var res = t.ReceiveHit(info);
                _owner.ResolveExternal(t, info, res);
                Vfx.I.InkBurst(contact, _owner.fxDark, 0.8f);
                Fade();
                return true;
            }
            return false;
        }

        void Fade()
        {
            _done = true;
            Vfx.I.Ring(transform.position, 0.2f, 0.9f, 0.08f, _owner.fxMain, 0.25f, false);
            Destroy(gameObject);
        }
    }

    public interface IHasOwner { Fighter Owner { get; } }
}
