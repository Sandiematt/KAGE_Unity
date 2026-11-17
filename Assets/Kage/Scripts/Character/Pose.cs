using UnityEngine;

namespace Kage
{
    /// <summary>
    /// One full-body pose: local bone angles (degrees, relative to the reference bind pose),
    /// a hip offset, and optional IK goals for hands/feet plus absolute foot and blade angles.
    /// Angles are in root space with the fighter facing right (+x). Positive = counter-clockwise:
    /// a hanging arm or leg swings forward, a torso leans back.
    /// </summary>
    public class Pose
    {
        public readonly float[] a = new float[(int)B.COUNT];
        public Vector2 hip;
        public readonly bool[] ik = new bool[4];
        public readonly Vector2[] target = new Vector2[4];
        public readonly bool[] hasFootAbs = new bool[2];
        public readonly float[] footAbs = new float[2];
        public bool hasSwAbs;
        public float swAbs;
        bool _resolved;

        // ----- builder API used by the pose library -----
        public Pose Hip(float x, float y, float rot = 0f) { hip = new Vector2(x, y); a[(int)B.Hips] = rot; return this; }
        public Pose HipRot(float r) { a[(int)B.Hips] = r; return this; }
        public Pose Chest(float r) { a[(int)B.Chest] = r; return this; }
        public Pose Neck(float r) { a[(int)B.Neck] = r; return this; }
        public Pose Head(float r) { a[(int)B.Head] = r; return this; }
        public Pose Coat(float n, float f) { a[(int)B.CoatN] = n; a[(int)B.CoatF] = f; return this; }

        public Pose ArmN(float upper, float lower, float hand = 0f)
        { a[(int)B.ArmNU] = upper; a[(int)B.ArmNL] = lower; a[(int)B.HandN] = hand; ik[(int)Limb.ArmN] = false; return this; }
        public Pose ArmF(float upper, float lower, float hand = 0f)
        { a[(int)B.ArmFU] = upper; a[(int)B.ArmFL] = lower; a[(int)B.HandF] = hand; ik[(int)Limb.ArmF] = false; return this; }
        public Pose LegN(float upper, float lower, float foot = 0f)
        { a[(int)B.LegNU] = upper; a[(int)B.LegNL] = lower; a[(int)B.FootN] = foot; ik[(int)Limb.LegN] = false; hasFootAbs[0] = false; return this; }
        public Pose LegF(float upper, float lower, float foot = 0f)
        { a[(int)B.LegFU] = upper; a[(int)B.LegFL] = lower; a[(int)B.FootF] = foot; ik[(int)Limb.LegF] = false; hasFootAbs[1] = false; return this; }

        /// <summary>IK: near (fist) wrist goal in root space.</summary>
        public Pose HandN(float x, float y, float handAngle = 0f)
        { ik[(int)Limb.ArmN] = true; target[(int)Limb.ArmN] = new Vector2(x, y); a[(int)B.HandN] = handAngle; return this; }
        /// <summary>IK: far (katana) wrist goal in root space.</summary>
        public Pose HandF(float x, float y, float handAngle = 0f)
        { ik[(int)Limb.ArmF] = true; target[(int)Limb.ArmF] = new Vector2(x, y); a[(int)B.HandF] = handAngle; return this; }
        /// <summary>IK: near/lead ankle goal in root space; footAngle 0 = flat on the ground.</summary>
        public Pose FootN(float x, float y, float footAngle = 0f)
        { ik[(int)Limb.LegN] = true; target[(int)Limb.LegN] = new Vector2(x, y); hasFootAbs[0] = true; footAbs[0] = footAngle; return this; }
        public Pose FootF(float x, float y, float footAngle = 0f)
        { ik[(int)Limb.LegF] = true; target[(int)Limb.LegF] = new Vector2(x, y); hasFootAbs[1] = true; footAbs[1] = footAngle; return this; }
        public Pose Feet(float nx, float fx, float y = 0.13f) => FootN(nx, y).FootF(fx, y);

        public Pose Sword(float rel) { a[(int)B.Sword] = rel; hasSwAbs = false; return this; }
        /// <summary>Blade direction in root space, degrees (0 = pointing forward, 90 = up).</summary>
        public Pose Blade(float worldDeg) { hasSwAbs = true; swAbs = worldDeg; return this; }

        public Pose Clone()
        {
            var p = new Pose();
            p.CopyFrom(this);
            return p;
        }

        public void CopyFrom(Pose o)
        {
            System.Array.Copy(o.a, a, a.Length);
            hip = o.hip;
            System.Array.Copy(o.ik, ik, 4);
            System.Array.Copy(o.target, target, 4);
            System.Array.Copy(o.hasFootAbs, hasFootAbs, 2);
            System.Array.Copy(o.footAbs, footAbs, 2);
            hasSwAbs = o.hasSwAbs;
            swAbs = o.swAbs;
            _resolved = o._resolved;
        }

        public void MarkDirty() => _resolved = false;

        /// <summary>Rotates the whole pose rigidly about the hips: hip angle, IK goals, foot and blade
        /// angles all turn together, so flips and rolls spin as one body.</summary>
        public void SpinAbout(RigDef d, float deg)
        {
            if (deg == 0f) return;
            Vector2 c = d.pivot[(int)B.Hips] + hip;
            a[(int)B.Hips] += deg;
            for (int l = 0; l < 4; l++) if (ik[l]) target[l] = c + KMath.Rot(target[l] - c, deg);
            footAbs[0] += deg; footAbs[1] += deg;
            swAbs += deg;
            _resolved = false;
        }

        /// <summary>Wrap every angle into [-180,180] so the next blend never spins the long way round.</summary>
        public void Normalize()
        {
            for (int i = 0; i < a.Length; i++) a[i] = KMath.Wrap(a[i]);
            swAbs = KMath.Wrap(swAbs);
            footAbs[0] = KMath.Wrap(footAbs[0]);
            footAbs[1] = KMath.Wrap(footAbs[1]);
        }

        // ------------------------------------------------------------------------------------
        static readonly float[] R = new float[(int)B.COUNT];

        /// <summary>Bakes IK goals, foot angles and the blade angle into local bone angles.</summary>
        public void Resolve(RigDef d)
        {
            if (_resolved) return;
            for (int i = 0; i < R.Length; i++) R[i] = 0f;
            R[(int)B.Hips] = a[(int)B.Hips];
            Vector2 hips = d.pivot[(int)B.Hips] + hip;
            R[(int)B.Chest] = R[(int)B.Hips] + a[(int)B.Chest];
            Vector2 chest = hips + KMath.Rot(d.BindLocal(B.Chest), R[(int)B.Hips]);

            Vector2 shN = chest + KMath.Rot(d.BindLocal(B.ArmNU), R[(int)B.Chest]);
            Vector2 shF = chest + KMath.Rot(d.BindLocal(B.ArmFU), R[(int)B.Chest]);
            Vector2 hipN = hips + KMath.Rot(d.BindLocal(B.LegNU), R[(int)B.Hips]);
            Vector2 hipF = hips + KMath.Rot(d.BindLocal(B.LegFU), R[(int)B.Hips]);

            Limb2(d, Limb.ArmN, B.ArmNU, B.ArmNL, B.HandN, R[(int)B.Chest], shN, -1f);
            Limb2(d, Limb.ArmF, B.ArmFU, B.ArmFL, B.HandF, R[(int)B.Chest], shF, -1f);
            Limb2(d, Limb.LegN, B.LegNU, B.LegNL, B.FootN, R[(int)B.Hips], hipN, 1f);
            Limb2(d, Limb.LegF, B.LegFU, B.LegFL, B.FootF, R[(int)B.Hips], hipF, 1f);

            float rLegNL = R[(int)B.Hips] + a[(int)B.LegNU] + a[(int)B.LegNL];
            float rLegFL = R[(int)B.Hips] + a[(int)B.LegFU] + a[(int)B.LegFL];
            if (hasFootAbs[0]) a[(int)B.FootN] = KMath.Wrap(footAbs[0] - rLegNL);
            if (hasFootAbs[1]) a[(int)B.FootF] = KMath.Wrap(footAbs[1] - rLegFL);

            if (hasSwAbs)
            {
                float rHandF = R[(int)B.Chest] + a[(int)B.ArmFU] + a[(int)B.ArmFL] + a[(int)B.HandF];
                a[(int)B.Sword] = KMath.Wrap(swAbs - d.bladeBindAngle - rHandF);
            }
            _resolved = true;
        }

        void Limb2(RigDef d, Limb limb, B upper, B lower, B end, float parentR, Vector2 root, float bend)
        {
            if (!ik[(int)limb]) return;
            Vector2 l1v = d.BindLocal(lower), l2v = d.BindLocal(end);
            float l1 = l1v.magnitude, l2 = l2v.magnitude;
            Vector2 t = target[(int)limb];
            Vector2 to = t - root;
            float dist = Mathf.Clamp(to.magnitude, Mathf.Abs(l1 - l2) + 1e-4f, (l1 + l2) * 0.9995f);
            float baseAng = KMath.Ang(to);
            float cosA = Mathf.Clamp((l1 * l1 + dist * dist - l2 * l2) / (2f * l1 * dist), -1f, 1f);
            float abs1 = baseAng + bend * Mathf.Acos(cosA) * Mathf.Rad2Deg;
            Vector2 knee = root + KMath.Dir(abs1) * l1;
            Vector2 endPos = root + to.normalized * dist;
            float abs2 = KMath.Ang(endPos - knee);
            float rUpper = abs1 - KMath.Ang(l1v);
            float rLower = abs2 - KMath.Ang(l2v);
            a[(int)upper] = KMath.Wrap(rUpper - parentR);
            a[(int)lower] = KMath.Wrap(rLower - rUpper);
        }

        /// <summary>output = lerp(p, q, t). Limbs that use IK in both poses blend their goals (feet stay planted).</summary>
        public static void Blend(RigDef d, Pose p, Pose q, float t, Pose output)
        {
            p.Resolve(d);
            q.Resolve(d);
            for (int i = 0; i < output.a.Length; i++) output.a[i] = Mathf.LerpUnclamped(p.a[i], q.a[i], t);
            output.hip = Vector2.LerpUnclamped(p.hip, q.hip, t);
            for (int l = 0; l < 4; l++)
            {
                output.ik[l] = p.ik[l] && q.ik[l];
                output.target[l] = Vector2.LerpUnclamped(p.target[l], q.target[l], t);
            }
            for (int f = 0; f < 2; f++)
            {
                output.hasFootAbs[f] = p.hasFootAbs[f] && q.hasFootAbs[f];
                output.footAbs[f] = Mathf.LerpUnclamped(p.footAbs[f], q.footAbs[f], t);
            }
            output.hasSwAbs = p.hasSwAbs && q.hasSwAbs;
            output.swAbs = Mathf.LerpUnclamped(p.swAbs, q.swAbs, t);
            output._resolved = false;
        }
    }
}
