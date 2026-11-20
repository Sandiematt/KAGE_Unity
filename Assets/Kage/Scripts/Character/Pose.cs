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
        /// <summary>Two-handed grip: the near hand closes on the katana's handle behind the far hand (SAN-113).</summary>
        public bool grip;
        /// <summary>
        /// Dual wield (twin blades): a second blade sits in the near hand. Resolving then turns that hand so the blade
        /// points along <see cref="BladeN"/>'s direction, or, with none authored, a fixed spread from the far blade.
        /// </summary>
        public bool dual;
        /// <summary>The near blade's direction in root space (set by <see cref="BladeN"/>; every resolved pose has one).</summary>
        public bool hasSwNAbs;
        public float swNAbs;
        /// <summary>Where the near hand sits on the handle (sword-axis metres behind the far hand) and how far back it can slide (SAN-78: a polearm holds its hands further apart).</summary>
        public float gripSpacing = GripSpacing, gripPommel = GripPommel;
        /// <summary>
        /// The katana turned over on its own axis (SAN-118, hasuji): its edge on the side its spine is drawn on. A rising cut
        /// (kiri-age, gyaku-kesa), a draw from the hip and Kasumi lead with the edge this way; the rig mirrors the sword bone
        /// across the blade's line, so the blade's line and its hit points stay where they were.
        /// </summary>
        public bool edgeUp;
        bool _resolved;

        // ----- builder API used by the pose library -----
        public Pose Hip(float x, float y, float rot = 0f) { hip = new Vector2(x, y); a[(int)B.Hips] = rot; return this; }
        public Pose HipRot(float r) { a[(int)B.Hips] = r; return this; }
        public Pose Chest(float r) { a[(int)B.Chest] = r; return this; }
        public Pose Neck(float r) { a[(int)B.Neck] = r; return this; }
        public Pose Head(float r) { a[(int)B.Head] = r; return this; }
        public Pose Coat(float n, float f) { a[(int)B.CoatN] = n; a[(int)B.CoatF] = f; return this; }

        public Pose ArmN(float upper, float lower, float hand = 0f)
        { a[(int)B.ArmNU] = upper; a[(int)B.ArmNL] = lower; a[(int)B.HandN] = hand; ik[(int)Limb.ArmN] = false; grip = false; return this; }
        public Pose ArmF(float upper, float lower, float hand = 0f)
        { a[(int)B.ArmFU] = upper; a[(int)B.ArmFL] = lower; a[(int)B.HandF] = hand; ik[(int)Limb.ArmF] = false; return this; }
        public Pose LegN(float upper, float lower, float foot = 0f)
        { a[(int)B.LegNU] = upper; a[(int)B.LegNL] = lower; a[(int)B.FootN] = foot; ik[(int)Limb.LegN] = false; hasFootAbs[0] = false; return this; }
        public Pose LegF(float upper, float lower, float foot = 0f)
        { a[(int)B.LegFU] = upper; a[(int)B.LegFL] = lower; a[(int)B.FootF] = foot; ik[(int)Limb.LegF] = false; hasFootAbs[1] = false; return this; }

        /// <summary>IK: near (fist) wrist goal in root space.</summary>
        public Pose HandN(float x, float y, float handAngle = 0f)
        { ik[(int)Limb.ArmN] = true; target[(int)Limb.ArmN] = new Vector2(x, y); a[(int)B.HandN] = handAngle; grip = false; return this; }
        /// <summary>
        /// Two-handed grip: the near wrist goal and hand angle come from the far hand and the blade, so the near
        /// fist closes on the handle behind the far fist wherever the swing takes the sword. A later
        /// <see cref="HandN"/> or <see cref="ArmN"/> lets go again.
        /// </summary>
        public Pose Grip() { ik[(int)Limb.ArmN] = true; grip = true; return this; }
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
        /// <summary>Turns the katana over so its edge faces the other way (<see cref="edgeUp"/>): edge up when the blade points forward.</summary>
        public Pose EdgeUp(bool up = true) { edgeUp = up; return this; }

        /// <summary>
        /// A sword-bone local point mirrored across the blade's line (<paramref name="bladeBindAngle"/>): where it lies on a
        /// katana turned over (<see cref="edgeUp"/>). Points on the line itself don't move.
        /// </summary>
        public static Vector2 TurnOver(Vector2 local, float bladeBindAngle)
        {
            Vector2 v = KMath.Rot(local, -bladeBindAngle);
            return KMath.Rot(new Vector2(v.x, -v.y), bladeBindAngle);
        }
        /// <summary>Marks the pose as dual wield: the near hand holds a second blade.</summary>
        public Pose Dual() { dual = true; return this; }
        /// <summary>Second (near hand) blade direction in root space, degrees (0 = forward, 90 = up). Dual wield only.</summary>
        public Pose BladeN(float worldDeg) { dual = true; hasSwNAbs = true; swNAbs = worldDeg; return this; }

        /// <summary>The near blade trails the far one by this much when a dual pose gives no direction of its own.</summary>
        public const float NearBladeSpread = -24f;

        /// <summary>
        /// A held-grip pose (both hands on one handle) for a dual wielder: the near hand lets go and takes its own
        /// blade at <paramref name="offset"/> from the far hand (root space).
        /// </summary>
        public void SplitHands(Vector2 offset)
        {
            if (!grip || !ik[(int)Limb.ArmN] || !ik[(int)Limb.ArmF]) return;
            target[(int)Limb.ArmN] = target[(int)Limb.ArmF] + offset;
            grip = false;
            hasSwNAbs = false;
            _resolved = false;
        }

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
            grip = o.grip;
            dual = o.dual;
            hasSwNAbs = o.hasSwNAbs;
            swNAbs = o.swNAbs;
            gripSpacing = o.gripSpacing;
            gripPommel = o.gripPommel;
            edgeUp = o.edgeUp;
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
            swNAbs += deg;
            _resolved = false;
        }

        /// <summary>Wrap every angle into [-180,180] so the next blend never spins the long way round.</summary>
        public void Normalize()
        {
            for (int i = 0; i < a.Length; i++) a[i] = KMath.Wrap(a[i]);
            swAbs = KMath.Wrap(swAbs);
            swNAbs = KMath.Wrap(swNAbs);
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

            Limb2(d, Limb.ArmF, B.ArmFU, B.ArmFL, B.HandF, R[(int)B.Chest], shF, -1f);
            Limb2(d, Limb.LegN, B.LegNU, B.LegNL, B.FootN, R[(int)B.Hips], hipN, 1f);
            Limb2(d, Limb.LegF, B.LegFU, B.LegFL, B.FootF, R[(int)B.Hips], hipF, 1f);

            float rLegNL = R[(int)B.Hips] + a[(int)B.LegNU] + a[(int)B.LegNL];
            float rLegFL = R[(int)B.Hips] + a[(int)B.LegFU] + a[(int)B.LegFL];
            if (hasFootAbs[0]) a[(int)B.FootN] = KMath.Wrap(footAbs[0] - rLegNL);
            if (hasFootAbs[1]) a[(int)B.FootF] = KMath.Wrap(footAbs[1] - rLegFL);

            float rArmFU = R[(int)B.Chest] + a[(int)B.ArmFU], rArmFL = rArmFU + a[(int)B.ArmFL];
            float rHandF = rArmFL + a[(int)B.HandF];
            if (hasSwAbs) a[(int)B.Sword] = KMath.Wrap(swAbs - d.bladeBindAngle - rHandF);

            float handN = rHandF;
            if (grip && ik[(int)Limb.ArmN]) target[(int)Limb.ArmN] = GripWrist(d, shF, rArmFU, rArmFL, rHandF, shN, out handN);
            Limb2(d, Limb.ArmN, B.ArmNU, B.ArmNL, B.HandN, R[(int)B.Chest], shN, -1f);
            if (grip && ik[(int)Limb.ArmN])
                a[(int)B.HandN] = KMath.Wrap(handN - (R[(int)B.Chest] + a[(int)B.ArmNU] + a[(int)B.ArmNL]));

            // the near blade points where it is told to (dual wield), else wherever the hand points
            float rArmN = R[(int)B.Chest] + a[(int)B.ArmNU] + a[(int)B.ArmNL];
            if (dual)
            {
                float far = hasSwAbs ? swAbs : rHandF + a[(int)B.Sword] + d.bladeBindAngle;
                a[(int)B.HandN] = KMath.Wrap((hasSwNAbs ? swNAbs : far + NearBladeSpread) - rArmN);
            }
            swNAbs = KMath.Wrap(rArmN + a[(int)B.HandN]);
            hasSwNAbs = true;
            _resolved = true;
        }

        /// <summary>Where the near hand rests on the handle behind the far fist (sword-axis offset, metres).</summary>
        public const float GripSpacing = -0.1f, GripPommel = -0.155f;
        /// <summary>The polearm grip (SAN-78): the near hand a forearm behind the far hand on the shaft, sliding back to the butt.</summary>
        public const float PolearmSpacing = -0.4f, PolearmPommel = -0.62f;
        /// <summary>The heavy two-handed grip (SAN-79): the near hand a hand's width behind the far hand on a long cleaver handle.</summary>
        public const float HeavySpacing = -0.24f, HeavyPommel = -0.42f;

        /// <summary>
        /// The near wrist goal for the two-handed grip: the near fist on the handle a fist behind the far hand,
        /// the hand turned like the far hand (<paramref name="handWorld"/>, root-space degrees). When the near
        /// shoulder can't reach that, the fist slides back along the handle towards the pommel, and if that is
        /// still too far the hand turns to point its fist at the handle, which adds the hand's length to the reach.
        /// </summary>
        Vector2 GripWrist(RigDef d, Vector2 shF, float rArmFU, float rArmFL, float rHandF, Vector2 shN, out float handWorld)
        {
            handWorld = rHandF;
            Vector2 elbow = shF + KMath.Rot(d.BindLocal(B.ArmFL), rArmFU);
            Vector2 wristF = elbow + KMath.Rot(d.BindLocal(B.HandF), rArmFL);
            Vector2 pivot = wristF + KMath.Rot(d.BindLocal(B.Sword), rHandF);
            Vector2 axis = KMath.Dir(rHandF + a[(int)B.Sword] + d.bladeBindAngle);
            Vector2 fistToWrist = -KMath.Rot(d.fistLocal, rHandF);
            float reach = (d.BindLocal(B.ArmNL).magnitude + d.BindLocal(B.HandN).magnitude) * 0.995f;
            Vector2 fist = pivot + axis * gripSpacing;
            float near = Mathf.Abs(d.BindLocal(B.ArmNL).magnitude - d.BindLocal(B.HandN).magnitude) * 1.1f;
            float dist = (fist + fistToWrist - shN).magnitude;
            if (dist < near)
            {
                // hands up by the near shoulder (Kasumi): the arm can't fold that far, so the wrist bends back and
                // the fist points down the forearm at the handle
                Vector2 away = (fist - shN).sqrMagnitude > 1e-6f ? (fist - shN).normalized : Vector2.down;
                handWorld = KMath.Ang(-away) - KMath.Ang(d.fistLocal);
                return fist + away * d.fistLocal.magnitude;
            }
            if (dist <= reach) return fist + fistToWrist;

            // slide towards the pommel: the point of the handle closest to the shoulder
            Vector2 back = pivot + axis * gripPommel;
            Vector2 seg = back - fist;
            float t = Mathf.Clamp01(Vector2.Dot(shN - fist, seg) / Mathf.Max(1e-6f, seg.sqrMagnitude));
            fist += seg * t;
            if ((fist + fistToWrist - shN).sqrMagnitude <= reach * reach) return fist + fistToWrist;

            // still too far: point the fist at the handle from the shoulder
            Vector2 dir = (fist - shN).normalized;
            handWorld = KMath.Ang(dir) - KMath.Ang(d.fistLocal);
            return fist - dir * d.fistLocal.magnitude;
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
                // a blend that has arrived (t = 1) takes on the target's IK, so the next blend from it keeps the goals
                output.ik[l] = (p.ik[l] && q.ik[l]) || (t >= 1f && q.ik[l]) || (t <= 0f && p.ik[l]);
                output.target[l] = Vector2.LerpUnclamped(p.target[l], q.target[l], t);
            }
            for (int f = 0; f < 2; f++)
            {
                output.hasFootAbs[f] = p.hasFootAbs[f] && q.hasFootAbs[f];
                output.footAbs[f] = Mathf.LerpUnclamped(p.footAbs[f], q.footAbs[f], t);
            }
            output.hasSwAbs = p.hasSwAbs && q.hasSwAbs;
            output.swAbs = Mathf.LerpUnclamped(p.swAbs, q.swAbs, t);
            output.dual = p.dual || q.dual;
            output.hasSwNAbs = p.hasSwNAbs && q.hasSwNAbs;
            output.swNAbs = Mathf.LerpUnclamped(p.swNAbs, q.swNAbs, t);
            // both gripping: the output re-grips on its own blended sword; else the near wrist blends between the
            // resolved goals (a gripping pose's goal is where its grip put the hand), so letting go and re-gripping is smooth
            output.grip = (p.grip && q.grip) || (t >= 1f && q.grip) || (t <= 0f && p.grip);
            output.gripSpacing = Mathf.LerpUnclamped(p.gripSpacing, q.gripSpacing, t);
            output.gripPommel = Mathf.LerpUnclamped(p.gripPommel, q.gripPommel, t);
            // the blade turns over half way through a blend (te-gaeshi): a quick roll of the wrists, drawn as one flip
            output.edgeUp = t >= 0.5f ? q.edgeUp : p.edgeUp;
            output._resolved = false;
        }
    }
}
