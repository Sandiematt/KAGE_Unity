using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    public interface IHasOwner { Fighter Owner { get; } }

    /// <summary>What a projectile is (ADR-0007): data only. Crescent, kunai, the Chainthrower's lash, Thunder Swing's wave.</summary>
    public sealed class ProjectileDef
    {
        /// <summary>Event MoveId: "crescent", "kunai", "chainthrower.lash", "thunderSwing.wave".</summary>
        public string id;
        /// <summary>Damage, level, knockback, stun, hitstop and shake of its hit. Its own radius field is unused: see <see cref="radius"/>.</summary>
        public HitWindow hit;
        /// <summary>Metres per second along the facing.</summary>
        public float speed;
        /// <summary>0 = a straight line. Ignored by a ground wave.</summary>
        public float gravityScale;
        /// <summary>Metres per second upward at the throw (a lob: Hisame's Smoke Bomb, SAN-105); 0 = level.</summary>
        public float launchVy;
        /// <summary>
        /// Above <see cref="speed"/>: the thrower rolls each throw's speed in [speed, speedMax] on its own random stream (the JS smoke
        /// bomb's <c>rand(380, 560)</c>, SAN-105). 0 = always <see cref="speed"/>.
        /// </summary>
        public float speedMax;
        /// <summary>Slides along the floor at its spawn height (Thunder Swing).</summary>
        public bool groundWave;
        /// <summary>Hit radius, metres.</summary>
        public float radius;
        public int lifeFrames;
        /// <summary>Targets it can hit before it is spent.</summary>
        public int maxHits = 1;
        /// <summary>
        /// Where the hit circle sits relative to the projectile, x ahead (flipped with the facing), y up.
        /// Each point is cast; null or empty is the projectile's centre.
        /// </summary>
        public Vector2[] samples;
        /// <summary>Cue ids (<see cref="EffectCues"/>). Flight plays at spawn and rides the projectile; hit at the contact; end where it ends.</summary>
        public string flightCue, hitCue, endCue;
    }

    /// <summary>
    /// A thrown or launched object (ADR-0007): a kinematic body with a trigger on the Projectile layer
    /// that the fight loop moves each frame (never in Update), owned by a fighter. Each frame it casts its
    /// circle from where it was to where it is against the Hurtbox layer, skips its owner and the owner's
    /// team, and delivers a hit through the defence order like a melee window. It keeps its own hit log,
    /// so it hits a target at most once. Projectiles never clash with each other.
    /// </summary>
    public sealed class Projectile : MonoBehaviour, IHasOwner
    {
        /// <summary>A projectile that flies past this |x| is spent (the arena edge).</summary>
        public const float ArenaLimit = 9.5f;

        public Fighter Owner { get; private set; }
        public ProjectileDef Def { get; private set; }
        public FightContext Ctx { get; private set; }
        public float Dir { get; private set; }
        /// <summary>Fight-loop frames it has lived.</summary>
        public int Age { get; private set; }
        public int HitsLanded { get; private set; }
        public Vector2 Position => _pos;
        public bool Alive => !_done;

        Vector2 _pos, _vel;
        bool _done;
        readonly List<IHittable> _log = new();
        readonly List<(Vector2 from, Vector2 to)> _samples = new();
        static readonly List<Hurtbox> BoxTmp = new();
        static readonly List<HurtboxHit> SweepTmp = new();
        static readonly List<IHittable> TargetTmp = new();

        /// <summary>
        /// Creates a projectile at <paramref name="pos"/> flying along <paramref name="dir"/> (+1 right, -1 left); the fight loop moves it.
        /// <paramref name="speed"/> (metres per second) replaces the def's speed for this one when it is not negative.
        /// </summary>
        public static Projectile Spawn(FightContext ctx, Fighter owner, ProjectileDef def, Vector2 pos, float dir, float speed = -1f)
        {
            var go = new GameObject("Projectile_" + def.id) { layer = PhysicsLayers.Projectile };
            go.transform.SetParent(owner.transform.parent, false);    // the fight root
            go.transform.position = pos;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = def.radius;
            var p = go.AddComponent<Projectile>();
            p.Init(ctx, owner, def, pos, dir >= 0f ? 1f : -1f, speed >= 0f ? speed : def.speed);
            return p;
        }

        void Init(FightContext ctx, Fighter owner, ProjectileDef def, Vector2 pos, float dir, float speed)
        {
            Ctx = ctx; Owner = owner; Def = def; Dir = dir;
            _pos = pos;
            _vel = new Vector2(dir * speed, def.launchVy);
            ctx.Projectiles.Add(this);
            EffectCues.Play(def.flightCue, pos, new CueArgs
            {
                owner = owner, dir = new Vector2(dir, 0f), scale = 1f, follow = transform, seconds = def.lifeFrames / 60f,
            });
        }

        void OnDestroy() => Ctx?.Projectiles.Remove(this);

        /// <summary>
        /// One fight-loop frame: move, cast, deliver hits, expire. Under the world time scale (SAN-92) a projectile its
        /// owner doesn't keep at full speed moves by s of a frame every frame (smoothly; gravity's pull scales by s² in
        /// world time), and its <see cref="Age"/> counts only the slowed world's frames, so it flies just as far.
        /// </summary>
        public void Tick()
        {
            if (_done) return;
            var clock = Ctx.Clock;
            float s = clock.ScaleFor(Owner);
            if (s <= 0f) return;                       // a frozen world holds it in the air
            if (clock.Advances(Owner)) Age++;
            float dt = Time.fixedDeltaTime * s;
            Vector2 from = _pos;
            bool falls = Def.gravityScale != 0f && !Def.groundWave;
            if (falls) _vel.y += Physics2D.gravity.y * Def.gravityScale * dt;
            Vector2 to = from + _vel * dt;

            if (falls && HitsGround(from, to, out var landed)) { End(landed); return; }

            _pos = to;
            transform.position = to;

            Hurtbox.SyncAll();
            CastSamples(from, to);
            if (_done) return;

            if (Age >= Def.lifeFrames || Mathf.Abs(_pos.x) > ArenaLimit) End(_pos);
        }

        bool HitsGround(Vector2 from, Vector2 to, out Vector2 at)
        {
            at = to;
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 1e-6f) return false;
            var hit = ExternalHit.PhysicsOf(Ctx).CircleCast(from, Def.radius, d / len, len, 1 << PhysicsLayers.Ground);
            if (hit.collider == null) return false;
            at = hit.centroid;
            return true;
        }

        void CastSamples(Vector2 from, Vector2 to)
        {
            _samples.Clear();
            var offsets = Def.samples;
            if (offsets == null || offsets.Length == 0) _samples.Add((from, to));
            else foreach (var o in offsets)
            {
                var off = new Vector2(o.x * Dir, o.y);
                _samples.Add((from + off, to + off));
            }

            BoxTmp.Clear();
            var all = new List<Hurtbox>();
            var physics = ExternalHit.PhysicsOf(Ctx);
            foreach (var (f, t) in _samples)
            {
                Hurtbox.Overlap(physics, t, Def.radius, BoxTmp);
                all.AddRange(BoxTmp);
                Hurtbox.Sweep(physics, f, t, Def.radius, SweepTmp);
                foreach (var h in SweepTmp) all.Add(h.box);
            }
            ExternalHit.OrderedTargets(Ctx, all, TargetTmp);
            var targets = new List<IHittable>(TargetTmp);
            foreach (var t in targets)
            {
                if (!ExternalHit.CanTarget(Owner, t) || _log.Contains(t)) continue;
                if (!ExternalHit.FindContact(physics, t, _samples, Def.radius, out var zone, out var contact)) continue;
                if (Deliver(t, zone, contact)) return;
            }
        }

        /// <summary>Delivers a hit; true when the projectile is spent.</summary>
        bool Deliver(IHittable t, HitZone zone, Vector2 contact)
        {
            var w = Def.hit;
            var info = new HitInfo
            {
                // a blade projectile (the Crescent) carries the weapon's damage; a kunai doesn't
                attacker = Owner, w = w, dmg = DamageCalc.For(Owner, t, w.dmg, false, w.sword, Owner != null ? Owner.ProjectileDamageMul(Def) : 1f), kb = new Vector2(w.kb.x * Dir, w.kb.y), zone = zone, contact = contact,
                moveId = Def.id, window = -1, origin = HitOrigin.Projectile,
            };
            var outcome = ExternalHit.Deliver(Owner, t, info, out var result);
            if (outcome == ExternalHit.Outcome.Missed) return false;
            _log.Add(t);
            if (outcome == ExternalHit.Outcome.Dodged) return false;      // it flew through a perfect dodge
            HitsLanded++;
            EffectCues.Play(Def.hitCue, contact, new CueArgs { owner = Owner, dir = new Vector2(Dir, 0f), scale = 1f });
            // a countered or parried projectile is destroyed; otherwise it is spent after its last target
            if (result == HitResult.Countered || result == HitResult.Parried || HitsLanded >= Def.maxHits) { End(_pos); return true; }
            return false;
        }

        void End(Vector2 at)
        {
            _done = true;
            Ctx.Projectiles.Remove(this);
            EffectCues.Play(Def.endCue, at, new CueArgs { owner = Owner, dir = new Vector2(Dir, 0f), scale = 1f });
            Destroy(gameObject);
        }
    }
}
