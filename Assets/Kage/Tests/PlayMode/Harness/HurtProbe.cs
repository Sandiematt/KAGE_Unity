using System.Collections.Generic;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// A still target for hit tests: one hurt circle at a fixed point, hit through the same Hurtbox
    /// component as fighters and the bag. It also answers the legacy Overlap test, so a run with the
    /// legacy switch can aim at it (as the fighter's extra target).
    /// </summary>
    public sealed class HurtProbe : MonoBehaviour, IHittable
    {
        public float Radius { get; private set; }
        public Vector2 Centre => transform.position;
        public readonly List<HitInfo> Hits = new();

        public static HurtProbe Place(Transform parent, Vector2 centre, float radius)
        {
            var go = new GameObject("HurtProbe");
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            var p = go.AddComponent<HurtProbe>();
            p.Radius = radius;
            Hurtbox.AddCarrier(go);
            Hurtbox.Add(go.transform, Vector2.zero, radius, HitZone.Body, 0, p);
            return p;
        }

        public bool HittableBy(HitWindow w) => true;

        public bool Overlap(Vector2 p, float r, HitWindow w, out HitZone zone, out Vector2 contact)
        {
            zone = HitZone.Body;
            contact = p;
            float rr = r + Radius;
            return (Centre - p).sqrMagnitude <= rr * rr;
        }

        public HitResult ReceiveHit(HitInfo info)
        {
            Hits.Add(info);
            return HitResult.Hit;
        }
    }
}
