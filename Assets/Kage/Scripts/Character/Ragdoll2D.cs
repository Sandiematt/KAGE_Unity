using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Ragdoll: the posed skeleton is flattened into Rigidbody2D bodies joined by limited HingeJoint2Ds.
    /// K.O. (<see cref="Activate"/>) throws it free with the finishing blow's impulse. A physical fall
    /// (<see cref="ActivateFall"/>, SAN-113) is the same bodies <see cref="Tethered"/> to the fighter's
    /// gameplay body: each frame <see cref="Drive"/> pulls the whole ragdoll towards where the hips should be
    /// and turns the torso towards the gameplay tumble, while the limbs, the impact and the settling are
    /// physics. <see cref="Capture"/> reads the fallen body back as a pose; Restore() puts the rig back.
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

        /// <summary>True during a physical fall: the ragdoll follows the gameplay body (no randomness, no interpolation).</summary>
        public bool Tethered { get; private set; }

        /// <summary>K.O.: the free ragdoll, thrown by the finishing blow.</summary>
        public static Ragdoll2D Activate(FighterRig rig, Vector2 impulse, Vector2 contact)
        {
            var rd = rig.gameObject.AddComponent<Ragdoll2D>();
            rd.Build(rig, false);
            rd.Throw(impulse, contact);
            return rd;
        }

        /// <summary>
        /// A physical fall: every body starts with the hit's velocity <paramref name="velocity"/> plus a spin of
        /// <paramref name="spinDeg"/> degrees a second about the hips (world, counter-clockwise), under <paramref name="gravityScale"/>.
        /// </summary>
        public static Ragdoll2D ActivateFall(FighterRig rig, Vector2 velocity, float spinDeg, float gravityScale)
        {
            var rd = rig.gameObject.AddComponent<Ragdoll2D>();
            rd.Build(rig, true);
            foreach (var kv in rd.bodies) kv.Value.gravityScale = gravityScale;
            rd.AddMotion(velocity, spinDeg);
            return rd;
        }

        /// <summary>Adds a velocity and a spin about the hips to every body (a fresh hit on a falling body).</summary>
        public void AddMotion(Vector2 velocity, float spinDeg)
        {
            Vector2 c = bodies[B.Hips].position;
            float w = spinDeg * Mathf.Deg2Rad;
            foreach (var b in Bodies)
            {
                var rb = bodies[b];
                Vector2 r = rb.position - c;
                rb.linearVelocity += velocity + new Vector2(-r.y, r.x) * w;
                rb.angularVelocity += spinDeg;
            }
        }

        /// <summary>A push at a point (a ground hit on the lying body).</summary>
        public void Kick(Vector2 impulse, Vector2 at)
        {
            Rigidbody2D nearest = null;
            float best = float.MaxValue;
            foreach (var b in Bodies)
            {
                float dd = (bodies[b].position - at).sqrMagnitude;
                if (dd < best) { best = dd; nearest = bodies[b]; }
            }
            if (nearest != null) nearest.AddForceAtPosition(impulse, at, ForceMode2D.Impulse);
        }

        /// <summary>Tether gains: position (1/s², 1/s) and torso turn (1/s², 1/s), and the tether's acceleration cap (m/s²).</summary>
        public const float PosK = 160f, PosD = 18f, TurnK = 90f, TurnD = 11f, MaxAccel = 120f;

        /// <summary>
        /// One fall frame, before the physics step: one acceleration on every body takes the hips towards
        /// <paramref name="hipsTarget"/> (moving at <paramref name="targetVel"/>), so the tether never bends the body,
        /// and a torque turns the hips and chest towards <paramref name="angleTarget"/> (world degrees), scaled by
        /// <paramref name="turnWeight"/>. Ground contacts, joint limits and the limbs' own momentum do the rest.
        /// </summary>
        public void Drive(Vector2 hipsTarget, Vector2 targetVel, float angleTarget, float turnWeight)
        {
            var hips = bodies[B.Hips];
            Vector2 acc = PosK * (hipsTarget - hips.position) + PosD * (targetVel - hips.linearVelocity);
            acc = Vector2.ClampMagnitude(acc, MaxAccel);
            foreach (var b in Bodies) bodies[b].AddForce(acc * bodies[b].mass);
            if (turnWeight <= 0f) return;
            foreach (var b in TurnBodies)
            {
                var rb = bodies[b];
                float err = Mathf.DeltaAngle(rb.rotation, angleTarget) * Mathf.Deg2Rad;
                float alpha = (TurnK * err - TurnD * rb.angularVelocity * Mathf.Deg2Rad) * turnWeight;
                rb.AddTorque(alpha * rb.inertia);
            }
        }

        static readonly B[] TurnBodies = { B.Hips, B.Chest };

        /// <summary>Ends a fall as a K.O.: the tether lets go and the finishing blow throws the body.</summary>
        public void Release(Vector2 impulse, Vector2 contact)
        {
            Tethered = false;
            foreach (var b in Bodies)
            {
                bodies[b].interpolation = RigidbodyInterpolation2D.Interpolate;
                bodies[b].gravityScale = 1f;
            }
            Throw(impulse, contact);
        }

        /// <summary>
        /// The fallen body as a pose of the rig: each body's angle relative to its rig parent and the hips' offset,
        /// in the rig's root space (facing right). Bones that are not bodies (neck, hands, sword, feet) keep the angle
        /// the last pose gave them. The result is plain bone angles: no IK goals, no grip.
        /// </summary>
        public void Capture(Pose into)
        {
            var rig = _rig;
            into.CopyFrom(rig.current);
            var root = rig.transform;
            float RootAngle(Transform t)
            {
                Vector3 y = root.InverseTransformVector(t.TransformVector(Vector3.up));
                return Mathf.Atan2(-y.x, y.y) * Mathf.Rad2Deg;
            }
            var abs = new float[(int)B.COUNT];
            for (int i = 1; i < (int)B.COUNT; i++)      // the enum lists every parent before its children
            {
                var b = (B)i;
                float parentAbs = abs[(int)rig.def.parent[i]];
                if (bodies.ContainsKey(b))
                {
                    abs[i] = RootAngle(rig.Bone(b));
                    into.a[i] = KMath.Wrap(abs[i] - parentAbs);
                }
                else abs[i] = parentAbs + into.a[i];
            }
            Vector2 hips = root.InverseTransformPoint(rig.Bone(B.Hips).position);
            into.hip = hips - rig.def.pivot[(int)B.Hips];
            for (int l = 0; l < 4; l++) into.ik[l] = false;
            into.hasFootAbs[0] = into.hasFootAbs[1] = false;
            into.hasSwAbs = false;
            into.grip = false;
            into.MarkDirty();
        }

        void Build(FighterRig rig, bool tethered)
        {
            Tethered = tethered;
            _rig = rig;
            var d = rig.def;
            _container = new GameObject(rig.def.name + (tethered ? "_Fall" : "_Ragdoll"));
            // under the fight root (KageGame in the dev arena), so a headless fight cleans it up with the rest
            var fighter = rig.transform.parent;
            _container.transform.SetParent(fighter != null ? fighter.parent : null, false);

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
                world[b] = (m.GetColumn(3), ang, Mathf.Sign(det) * y.magnitude);
                _saved.Add(new Saved { t = t, parent = t.parent, lp = t.localPosition, lr = t.localRotation, ls = t.localScale });
            }
            foreach (var b in Bodies)
            {
                var t = rig.Bone(b);
                var w = world[b];
                t.SetParent(_container.transform, false);
                t.localPosition = w.pos;
                t.localRotation = Quaternion.Euler(0f, 0f, w.ang);
                t.localScale = new Vector3(w.sx, Mathf.Abs(w.sx), 1f);      // keeps the body scale (squash dropped)
                t.gameObject.layer = KageGame.LayerRagdoll;
            }

            var mat = PhysicsMaterials.Ragdoll;
            foreach (var b in Bodies)
            {
                var t = rig.Bone(b);
                var rb = t.gameObject.AddComponent<Rigidbody2D>();
                rb.mass = b switch { B.Hips => 3f, B.Chest => 3f, B.Head => 1f, _ => 0.7f };
                rb.linearDamping = 0.15f;
                rb.angularDamping = 0.6f;
                // a tethered fall carries the hurtboxes: no interpolation, so the bones are exactly the bodies at every hit query
                rb.interpolation = tethered ? RigidbodyInterpolation2D.None : RigidbodyInterpolation2D.Interpolate;
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
        }

        void Throw(Vector2 impulse, Vector2 contact)
        {
            // throw it: everything inherits the blow, the struck part gets the extra kick + spin
            Rigidbody2D nearest = null;
            float best = float.MaxValue;
            foreach (var b in Bodies)
            {
                var rb = bodies[b];
                rb.linearVelocity = impulse * Random.Range(0.85f, 1.05f);
                float dd = ((Vector2)rb.transform.position - contact).sqrMagnitude;
                if (dd < best) { best = dd; nearest = rb; }
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
            bodies.Clear();
            foreach (var s in _saved)
            {
                s.t.SetParent(s.parent, false);
                s.t.localPosition = s.lp;
                s.t.localRotation = s.lr;
                s.t.localScale = s.ls;
                s.t.gameObject.layer = 0;
            }
            _saved.Clear();
            if (_container != null) DestroyImmediate(_container);
            DestroyImmediate(this);
        }
    }
}
