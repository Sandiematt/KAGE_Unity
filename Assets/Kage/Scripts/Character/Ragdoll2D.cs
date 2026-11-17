using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// K.O. ragdoll: the posed skeleton is flattened into Rigidbody2D bodies joined by limited
    /// HingeJoint2Ds and thrown with the finishing blow's impulse. Restore() puts the rig back.
    /// </summary>
    public class Ragdoll2D : MonoBehaviour
    {
        struct Saved
        {
            public Transform t, parent;
            public Vector3 lp, ls;
            public Quaternion lr;
        }

        static readonly B[] Bodies = { B.Hips, B.Chest, B.Head, B.ArmNU, B.ArmNL, B.ArmFU, B.ArmFL, B.LegNU, B.LegNL, B.LegFU, B.LegFL };

        static B JointParent(B b) => b switch
        {
            B.Chest => B.Hips, B.Head => B.Chest,
            B.ArmNU => B.Chest, B.ArmNL => B.ArmNU, B.ArmFU => B.Chest, B.ArmFL => B.ArmFU,
            B.LegNU => B.Hips, B.LegNL => B.LegNU, B.LegFU => B.Hips, B.LegFL => B.LegFU,
            _ => B.Hips
        };

        static float Limit(B b) => b switch
        {
            B.Chest => 35f, B.Head => 40f, B.ArmNU or B.ArmFU => 120f, B.ArmNL or B.ArmFL => 75f,
            B.LegNU or B.LegFU => 80f, B.LegNL or B.LegFL => 70f, _ => 45f
        };

        readonly List<Saved> _saved = new();
        readonly List<Component> _added = new();
        GameObject _container;
        FighterRig _rig;
        public readonly Dictionary<B, Rigidbody2D> bodies = new();

        public static Ragdoll2D Activate(FighterRig rig, Vector2 impulse, Vector2 contact)
        {
            var rd = rig.gameObject.AddComponent<Ragdoll2D>();
            rd.Go(rig, impulse, contact);
            return rd;
        }

        void Go(FighterRig rig, Vector2 impulse, Vector2 contact)
        {
            _rig = rig;
            var d = rig.def;
            _container = new GameObject(rig.def.name + "_Ragdoll");
            _container.transform.SetParent(KageGame.I != null ? KageGame.I.transform : null, false);

            // capture world poses first (mirrored rigs need an explicit 2D decomposition)
            var world = new Dictionary<B, (Vector3 pos, float ang, float sx)>();
            foreach (var b in Bodies)
            {
                var t = rig.Bone(b);
                Matrix4x4 m = t.localToWorldMatrix;
                Vector3 y = m.GetColumn(1);
                Vector3 x = m.GetColumn(0);
                float det = x.x * y.y - x.y * y.x;
                float ang = Mathf.Atan2(-y.x, y.y) * Mathf.Rad2Deg;
                world[b] = (m.GetColumn(3), ang, Mathf.Sign(det));
                _saved.Add(new Saved { t = t, parent = t.parent, lp = t.localPosition, lr = t.localRotation, ls = t.localScale });
            }
            foreach (var b in Bodies)
            {
                var t = rig.Bone(b);
                var w = world[b];
                t.SetParent(_container.transform, false);
                t.localPosition = w.pos;
                t.localRotation = Quaternion.Euler(0f, 0f, w.ang);
                t.localScale = new Vector3(w.sx, 1f, 1f);
                t.gameObject.layer = KageGame.LayerRagdoll;
            }

            var mat = new PhysicsMaterial2D("Ragdoll") { friction = 0.7f, bounciness = 0.12f };
            foreach (var b in Bodies)
            {
                var t = rig.Bone(b);
                var rb = t.gameObject.AddComponent<Rigidbody2D>();
                rb.mass = b switch { B.Hips => 3f, B.Chest => 3f, B.Head => 1f, _ => 0.7f };
                rb.linearDamping = 0.15f;
                rb.angularDamping = 0.6f;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _added.Add(rb);
                bodies[b] = rb;

                Collider2D col;
                if (b == B.Head)
                {
                    var c = t.gameObject.AddComponent<CircleCollider2D>();
                    c.radius = 0.12f;
                    c.offset = d.pivot[(int)B.Head] + new Vector2(0f, 0.09f) - d.pivot[(int)B.Head];
                    col = c;
                }
                else
                {
                    Vector2 toChild = b switch
                    {
                        B.Hips => new Vector2(0f, 0.25f), B.Chest => d.BindLocal(B.Neck),
                        B.ArmNU => d.BindLocal(B.ArmNL), B.ArmNL => d.BindLocal(B.HandN) + new Vector2(0, -0.05f),
                        B.ArmFU => d.BindLocal(B.ArmFL), B.ArmFL => d.BindLocal(B.HandF) + new Vector2(0, -0.05f),
                        B.LegNU => d.BindLocal(B.LegNL), B.LegNL => d.BindLocal(B.FootN) + new Vector2(0, -0.1f),
                        B.LegFU => d.BindLocal(B.LegFL), B.LegFL => d.BindLocal(B.FootF) + new Vector2(0, -0.1f),
                        _ => new Vector2(0f, -0.2f)
                    };
                    float width = b switch { B.Hips => 0.3f, B.Chest => 0.3f, B.LegNU or B.LegFU => 0.14f, _ => 0.09f };
                    var c = t.gameObject.AddComponent<CapsuleCollider2D>();
                    c.direction = CapsuleDirection2D.Vertical;
                    c.offset = toChild * 0.5f;
                    c.size = new Vector2(width, Mathf.Abs(toChild.y) + width);
                    col = c;
                }
                col.sharedMaterial = mat;
                _added.Add(col);
            }

            foreach (var b in Bodies)
            {
                if (b == B.Hips) continue;
                var t = rig.Bone(b);
                var j = t.gameObject.AddComponent<HingeJoint2D>();
                j.connectedBody = bodies[JointParent(b)];
                j.autoConfigureConnectedAnchor = true;
                j.anchor = Vector2.zero;
                j.enableCollision = false;
                j.useLimits = true;
                float lim = Limit(b);
                j.limits = new JointAngleLimits2D { min = -lim, max = lim };
                _added.Add(j);
            }

            // throw it: everything inherits the blow, the struck part gets the extra kick + spin
            Rigidbody2D nearest = null;
            float best = float.MaxValue;
            foreach (var kv in bodies)
            {
                kv.Value.linearVelocity = impulse * Random.Range(0.85f, 1.05f);
                float dd = ((Vector2)kv.Value.transform.position - contact).sqrMagnitude;
                if (dd < best) { best = dd; nearest = kv.Value; }
            }
            if (nearest != null) nearest.AddForceAtPosition(impulse * 0.7f, contact, ForceMode2D.Impulse);
            bodies[B.Chest].angularVelocity = -Mathf.Sign(impulse.x) * 240f;
        }

        public Vector2 Center => bodies.TryGetValue(B.Hips, out var h) && h != null ? h.position : (Vector2)transform.position;

        public void Restore()
        {
            for (int i = _added.Count - 1; i >= 0; i--)
                if (_added[i] is Joint2D) DestroyImmediate(_added[i]);
            for (int i = _added.Count - 1; i >= 0; i--)
                if (_added[i] != null && !(_added[i] is Rigidbody2D)) DestroyImmediate(_added[i]);
            for (int i = _added.Count - 1; i >= 0; i--)
                if (_added[i] != null) DestroyImmediate(_added[i]);
            _added.Clear();
            foreach (var s in _saved)
            {
                s.t.SetParent(s.parent, false);
                s.t.localPosition = s.lp;
                s.t.localRotation = s.lr;
                s.t.localScale = s.ls;
                s.t.gameObject.layer = 0;
            }
            _saved.Clear();
            if (_container != null) Destroy(_container);
            Destroy(this);
        }
    }
}
