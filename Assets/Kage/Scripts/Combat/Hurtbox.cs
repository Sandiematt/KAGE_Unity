using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>One result of a hurtbox sweep: the box, how far along the sweep it was touched (0..1) and where.</summary>
    public struct HurtboxHit
    {
        public Hurtbox box;
        public float fraction;
        public Vector2 point;
    }

    /// <summary>
    /// A hurt circle as a trigger <see cref="CircleCollider2D"/> on the Hurtbox layer (ADR-0007),
    /// parented to its bone (or prop body), carrying its owner and its hit zone. Fighters and the
    /// training bag use the same component, so every hit test (melee sweeps, later projectiles and
    /// hazards) finds them through the same query.
    /// <para>The circles hang under a kinematic carrier body (<see cref="AddCarrier"/>), so they
    /// never change the mass, centre of mass or inertia of the body they ride on. Their radius is
    /// world metres: <see cref="SyncAll"/> undoes the rig's scale (body scale, squash, the turn flip)
    /// and pushes the posed bones to the physics world before a query.</para>
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class Hurtbox : MonoBehaviour
    {
        public IHittable Owner { get; private set; }
        public HitZone Zone { get; private set; }
        /// <summary>Index in the owner's hurt list. When one point touches several circles, the lowest index gives the zone.</summary>
        public int Index { get; private set; }
        /// <summary>World radius in metres.</summary>
        public float Radius { get; private set; }
        public CircleCollider2D Collider { get; private set; }
        public Vector2 Centre => transform.position;

        static readonly List<Hurtbox> Live = new();
        static readonly List<Collider2D> ColTmp = new();
        static readonly List<RaycastHit2D> CastTmp = new();
        static readonly ContactFilter2D Filter = new()
        {
            useLayerMask = true, layerMask = PhysicsLayers.HurtboxMask, useTriggers = true,
        };

        /// <summary>Makes <paramref name="go"/> a kinematic body for the hurtboxes below it.</summary>
        public static Rigidbody2D AddCarrier(GameObject go)
        {
            if (!go.TryGetComponent<Rigidbody2D>(out var rb)) rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.None;
            rb.useFullKinematicContacts = false;
            return rb;
        }

        public static Hurtbox Add(Transform parent, Vector2 local, float radius, HitZone zone, int index, IHittable owner)
        {
            var go = new GameObject($"Hurt{index}_{zone}") { layer = PhysicsLayers.Hurtbox };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            var h = go.AddComponent<Hurtbox>();
            h.Collider = col;
            h.Owner = owner;
            h.Zone = zone;
            h.Index = index;
            h.Radius = radius;
            h.SyncRadius();
            return h;
        }

        void OnEnable() => Live.Add(this);
        void OnDisable() => Live.Remove(this);

        void SyncRadius()
        {
            if (Collider == null) return;
            Vector3 s = transform.lossyScale;
            float m = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
            float r = m > 1e-6f ? Radius / m : Radius;
            if (Collider.radius != r) Collider.radius = r;
        }

        /// <summary>Call before querying: fixes every radius for the current scale and syncs the posed transforms to physics.</summary>
        public static void SyncAll()
        {
            for (int i = 0; i < Live.Count; i++) Live[i].SyncRadius();
            Physics2D.SyncTransforms();
        }

        /// <summary>Every hurtbox a circle at <paramref name="p"/> touches.</summary>
        public static void Overlap(Vector2 p, float r, List<Hurtbox> into)
        {
            into.Clear();
            ColTmp.Clear();
            Physics2D.OverlapCircle(p, r, Filter, ColTmp);
            foreach (var c in ColTmp)
                if (c.TryGetComponent<Hurtbox>(out var h)) into.Add(h);
        }

        /// <summary>Every hurtbox an axis-aligned box centred at <paramref name="centre"/> touches (hazards, SAN-75).</summary>
        public static void OverlapBox(Vector2 centre, Vector2 size, List<Hurtbox> into)
        {
            into.Clear();
            ColTmp.Clear();
            Physics2D.OverlapBox(centre, size, 0f, Filter, ColTmp);
            foreach (var c in ColTmp)
                if (c.TryGetComponent<Hurtbox>(out var h)) into.Add(h);
        }

        /// <summary>
        /// Every hurtbox a circle moving from <paramref name="from"/> to <paramref name="to"/> touches on
        /// the way, with the fraction of the path where it first touches. A zero-length sweep finds nothing.
        /// </summary>
        public static void Sweep(Vector2 from, Vector2 to, float r, List<HurtboxHit> into)
        {
            into.Clear();
            Vector2 d = to - from;
            float len = d.magnitude;
            if (len < 1e-6f) return;
            CastTmp.Clear();
            Physics2D.CircleCast(from, r, d / len, Filter, CastTmp, len);
            foreach (var hit in CastTmp)
                if (hit.collider.TryGetComponent<Hurtbox>(out var h))
                    into.Add(new HurtboxHit { box = h, fraction = hit.fraction, point = hit.point });
        }
    }
}
