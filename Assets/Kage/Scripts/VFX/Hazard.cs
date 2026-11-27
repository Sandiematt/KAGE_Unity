using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>What a hazard is (ADR-0007): data only. Skyfall, Storm Break's shock, Storm Fist's shock.</summary>
    public sealed class HazardDef
    {
        /// <summary>Event MoveId: "skyfall", "stormBreak.shock", "garan.stormFist.shock".</summary>
        public string id;
        /// <summary>Frames the ground mark shows with no hit.</summary>
        public int telegraphFrames;
        /// <summary>
        /// For this many of the first frames the hazard follows its owner's current target along the floor (SAN-107: Skyfall's mark follows Kage while the
        /// storm gathers, then locks). 0 = it stays where it was laid.
        /// </summary>
        public int followFrames;
        /// <summary>Active frames after the telegraph.</summary>
        public int strikeFrames = 1;
        /// <summary>The box on the floor, metres (width, height); it stands on the ground point.</summary>
        public Vector2 size;
        /// <summary>Unblockable unless set otherwise. Knockback x points away from the hazard's centre.</summary>
        public HitWindow hit = new() { level = HitLevel.Unblockable };
        public string telegraphCue = "VFX_Kage_HazardTelegraph";
        public string strikeCue = "VFX_Kage_HazardStrike";
    }

    public enum HazardPhase { Telegraph, Strike, Done }

    /// <summary>
    /// An area that warns, then strikes (ADR-0007): a trigger box on the Hazard layer that the fight loop
    /// ticks. For <see cref="HazardDef.telegraphFrames"/> it only shows its ground mark. Then, for
    /// <see cref="HazardDef.strikeFrames"/>, each frame it overlaps its box against the Hurtbox layer and
    /// delivers a hit to each target inside, once per target. A fighter who left the box isn't hit. It
    /// never hits its owner or the owner's team.
    /// </summary>
    public sealed class Hazard : MonoBehaviour, IHasOwner
    {
        public Fighter Owner { get; private set; }
        public HazardDef Def { get; private set; }
        public FightContext Ctx { get; private set; }
        public HazardPhase Phase { get; private set; }
        public bool Alive => Phase != HazardPhase.Done;
        public int Age { get; private set; }
        /// <summary>The floor box in world space.</summary>
        public Vector2 Centre { get; private set; }

        readonly List<IHittable> _log = new();
        static readonly List<Hurtbox> BoxTmp = new();
        static readonly List<IHittable> TargetTmp = new();

        /// <summary>Creates a hazard standing on <paramref name="groundPoint"/>; the telegraph starts at once.</summary>
        public static Hazard Spawn(FightContext ctx, Fighter owner, HazardDef def, Vector2 groundPoint)
        {
            var go = new GameObject("Hazard_" + def.id) { layer = PhysicsLayers.Hazard };
            go.transform.SetParent(owner.transform.parent, false);    // the fight root
            go.transform.position = groundPoint;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = def.size;
            col.offset = new Vector2(0f, def.size.y * 0.5f);
            var h = go.AddComponent<Hazard>();
            h.Init(ctx, owner, def, groundPoint);
            return h;
        }

        void Init(FightContext ctx, Fighter owner, HazardDef def, Vector2 ground)
        {
            Ctx = ctx; Owner = owner; Def = def;
            Centre = ground + new Vector2(0f, def.size.y * 0.5f);
            Phase = def.telegraphFrames > 0 ? HazardPhase.Telegraph : HazardPhase.Strike;
            ctx.Hazards.Add(this);
            EffectCues.Play(def.telegraphCue, ground, new CueArgs
            {
                owner = owner, scale = 1f, follow = transform, size = def.size, seconds = def.telegraphFrames / 60f,
            });
        }

        void OnDestroy() => Ctx?.Hazards.Remove(this);

        /// <summary>
        /// One fight-loop frame. Under the world time scale (SAN-92) a hazard its owner doesn't keep at full speed only
        /// counts (telegraph, strike) on the slowed world's frames.
        /// </summary>
        public void Tick()
        {
            if (Phase == HazardPhase.Done) return;
            if (!Ctx.Clock.Advances(Owner)) return;
            Age++;
            if (Age <= Def.followFrames && Owner.TryHazardFollowX(out float fx)) MoveTo(fx);
            int telegraph = Def.telegraphFrames;
            if (Age <= telegraph) return;
            if (Age == telegraph + 1)
            {
                Phase = HazardPhase.Strike;
                EffectCues.Play(Def.strikeCue, transform.position, new CueArgs { owner = Owner, scale = 1f, size = Def.size });
            }
            Strike();
            if (Age >= telegraph + Def.strikeFrames) Finish();
        }

        void MoveTo(float x)
        {
            transform.position = new Vector3(x, transform.position.y, transform.position.z);
            Centre = new Vector2(x, Centre.y);
        }

        void Strike()
        {
            Hurtbox.SyncAll();
            Hurtbox.OverlapBox(ExternalHit.PhysicsOf(Ctx), Centre, Def.size, BoxTmp);
            ExternalHit.OrderedTargets(Ctx, BoxTmp, TargetTmp);
            var targets = new List<IHittable>(TargetTmp);
            var inside = new List<Hurtbox>(BoxTmp);
            foreach (var t in targets)
            {
                if (!ExternalHit.CanTarget(Owner, t) || _log.Contains(t)) continue;
                Hurtbox best = null;
                foreach (var b in inside) if (ReferenceEquals(b.Owner, t) && (best == null || b.Index < best.Index)) best = b;
                if (best == null) continue;
                Vector2 pos = best.Centre;
                float away = pos.x >= Centre.x ? 1f : -1f;
                var w = Def.hit;
                var info = new HitInfo
                {
                    attacker = Owner, w = w, dmg = w.dmg <= 0 ? 0 : DamageCalc.For(Owner, t, w.dmg, false, false),     // 0: a knockback-only shockwave (Raikon's phase shifts)
                    kb = new Vector2(w.kb.x * away, w.kb.y), zone = best.Zone, contact = pos,
                    moveId = Def.id, window = -1, origin = HitOrigin.Hazard,
                };
                var outcome = ExternalHit.Deliver(Owner, t, info, out _);
                if (outcome != ExternalHit.Outcome.Missed) _log.Add(t);
            }
        }

        void Finish()
        {
            Phase = HazardPhase.Done;
            Ctx.Hazards.Remove(this);
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);      // an EditMode test ticking it outside play mode (as Projectile)
        }

        // ---- placeholder cues ----
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterCues()
        {
            EffectCues.Register("VFX_Kage_HazardTelegraph", PlayTelegraph);
            EffectCues.Register("VFX_Kage_HazardStrike", PlayStrike);
        }

        /// <summary>A pulsing red mark on the floor, as wide as the box, that lasts as long as the telegraph.</summary>
        static void PlayTelegraph(Vector2 pos, in CueArgs a)
        {
            if (a.follow == null) return;
            var mark = new GameObject("TelegraphMark");
            mark.transform.SetParent(a.follow, false);
            mark.transform.localPosition = Vector3.zero;
            var look = mark.AddComponent<HazardMark>();
            look.Init(a.size.x, Mathf.Max(0.05f, a.seconds), a.tint ?? new Color(0.9f, 0.12f, 0.1f));
        }

        static void PlayStrike(Vector2 pos, in CueArgs a)
        {
            Color c = a.tint ?? (a.owner != null ? a.owner.fxMain : Color.white);
            Vfx.I.Shockwave(pos + Vector2.up * 0.03f, Mathf.Max(0.6f, a.size.x * 0.5f), c);
            Vfx.I.Dust(pos, Mathf.Max(0.8f, a.size.x * 0.6f), 0f);
            GameFeel.Shake(0.2f);
        }
    }

    /// <summary>The placeholder ground mark of a hazard's telegraph: a flat strip that pulses faster as the strike nears.</summary>
    public sealed class HazardMark : MonoBehaviour
    {
        MeshRenderer _r;
        Mesh _mesh;
        Color _c;
        float _life, _t;
        MaterialPropertyBlock _mpb;      // made in Init: Unity forbids creating one in a MonoBehaviour's field initializer (SAN-104 found it)

        public void Init(float width, float seconds, Color c)
        {
            _life = seconds; _c = c;
            _mpb = new MaterialPropertyBlock();
            _mesh = new Mesh { name = "HazardMark" };
            float h = 0.07f, hw = width * 0.5f;
            _mesh.vertices = new[] { new Vector3(-hw, 0f), new Vector3(hw, 0f), new Vector3(hw, h), new Vector3(-hw, h) };
            _mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            var cols = new Color[4];
            for (int i = 0; i < 4; i++) cols[i] = MeshKit.L(Color.white);
            _mesh.colors = cols;
            _r = MeshKit.MakeRenderer("Mark", transform, _mesh, MeshKit.Alpha, Vfx.Order - 10);
            Apply(0f);
        }

        void Update()
        {
            _t += Time.deltaTime;
            Apply(Mathf.Clamp01(_t / _life));
        }

        void Apply(float u)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(_t * Mathf.Lerp(10f, 34f, u));
            _mpb.SetColor("_Color", new Color(_c.r, _c.g, _c.b, Mathf.Lerp(0.25f, 0.8f, pulse) * Mathf.Lerp(0.6f, 1f, u)));
            _r.SetPropertyBlock(_mpb);
        }

        void OnDestroy() { if (_mesh != null) Destroy(_mesh); }
    }
}
