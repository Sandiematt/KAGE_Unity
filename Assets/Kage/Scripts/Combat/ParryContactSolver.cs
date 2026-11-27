using UnityEngine;

namespace Kage
{
    /// <summary>
    /// A fighter's pose-root space against the world: root space is the space every <see cref="Pose"/> is authored in (facing right,
    /// unscaled, the rig's own origin); the world is that times the rig's facing flip, body scale and squash, moved to where the rig
    /// stands. <c>world = origin + (x * sx, y * sy)</c>.
    /// </summary>
    public struct RootFrame
    {
        public Vector2 origin;
        public float sx, sy;

        /// <summary>A rig standing at <paramref name="origin"/>, facing <paramref name="facing"/> (±1), scaled by <paramref name="scale"/>.</summary>
        public static RootFrame Of(Vector2 origin, float facing, float scale) =>
            new RootFrame { origin = origin, sx = (facing < 0f ? -1f : 1f) * scale, sy = scale };

        /// <summary>The frame of a posed rig: its transform (the fighter's position, facing flip, body scale and squash).</summary>
        public static RootFrame Of(Transform rig)
        {
            Vector3 s = rig.lossyScale;
            return new RootFrame { origin = rig.position, sx = s.x, sy = s.y };
        }

        public bool Valid => Mathf.Abs(sx) > 1e-5f && Mathf.Abs(sy) > 1e-5f;
        public float Facing => sx < 0f ? -1f : 1f;
        public float Scale => Mathf.Abs(sy);

        public Vector2 ToRoot(Vector2 w) => new((w.x - origin.x) / sx, (w.y - origin.y) / sy);
        public Vector2 ToWorld(Vector2 r) => new(origin.x + r.x * sx, origin.y + r.y * sy);

        /// <summary>A world direction in root space (unit; zero stays zero).</summary>
        public Vector2 DirToRoot(Vector2 d)
        {
            var v = new Vector2(d.x / sx, d.y / sy);
            return v.sqrMagnitude > 1e-12f ? v.normalized : Vector2.zero;
        }

        /// <summary>A root-space direction in the world (unit; zero stays zero).</summary>
        public Vector2 DirToWorld(Vector2 d)
        {
            var v = new Vector2(d.x * sx, d.y * sy);
            return v.sqrMagnitude > 1e-12f ? v.normalized : Vector2.zero;
        }
    }

    /// <summary>
    /// The striking part of a parried blow, in the world: the weapon's (or limb's) segment from its hilt end to its tip at the frame of
    /// the hit, the way it was travelling, and where it touched the defender's hurt circle. Built by the attacker's hit detection
    /// (<see cref="Fighter"/>, Fighter.HitDetection.cs); a synthetic blow with no segment gets one made up from the contact and the level.
    /// </summary>
    public struct StrikeGeometry
    {
        public Vector2 baseWorld, tipWorld;
        /// <summary>Unit direction the striking part was travelling (zero when unknown).</summary>
        public Vector2 swingDirWorld;
        /// <summary>Where the window touched the hurt circle (the hit's contact).</summary>
        public Vector2 rawContactWorld;
        public HitLevel level;
        public HitPoint point;
        public bool isBlade, hasSegment;
        /// <summary>Authored mechanical strike trajectory (Auto infers from blade motion).</summary>
        public StrikeVector strikeVector;
    }

    /// <summary>What the solver needs to know about the defender: its rig, where it stands, its weapon and how it holds it.</summary>
    public struct DefenderBladeSpec
    {
        public RigDef rigDef;
        public RootFrame frame;
        /// <summary>Sword-bone local base (guard) and tip of the blade line.</summary>
        public Vector2 bladeBaseLocal, bladeTipLocal;
        public bool bareHanded, gripHeld, dual, polearm, keepPointOffFloor, crouchHeld;
        public float gripSpacing, gripPommel;
        /// <summary>The blade's reach from the guard, metres (the floor check of a long blade).</summary>
        public float reach;
        /// <summary><see cref="ParryTuning.guardPlaneX"/>.</summary>
        public float guardPlaneX;
    }

    /// <summary>The solved parry: where the blades met, how the defender stands to meet it, and the attacker's recoil.</summary>
    public struct ParryContactSolution
    {
        public bool valid;
        /// <summary>The blade-to-blade crossing, world and defender root space.</summary>
        public Vector2 clashWorld, clashRoot;
        /// <summary>The way the parried weapon is thrown (unit, world): off the defender's blade, against the swing.</summary>
        public Vector2 clashNormalWorld;
        /// <summary>The defender's blade direction, root-space degrees (0 forward, 90 up).</summary>
        public float defenderBladeAngleRoot;
        /// <summary>The acute angle between the two blades' lines at the crossing, degrees.</summary>
        public float crossAngleDeg;
        /// <summary>Where on the defender's own blade it met the blow: 0 the guard, 1 the tip.</summary>
        public float bladeContactParam;
        /// <summary>Metres between the two blades' segments after the solve (0 when they cross).</summary>
        public float contactGap;
        /// <summary>The blow was out of the defender's reach: it met the nearest point it could.</summary>
        public bool clampedToReach;
        public bool edgeUp;
        public Vector2 wristTargetRoot, nearWristTargetRoot;
        public float nearBladeAngleRoot;
        public Vector2 hipOffsetDelta;
        public float hipRotDelta, chestRotDelta, headRotDelta;
        /// <summary>Degrees the parried blade turns back along the clash normal as it recoils.</summary>
        public float attackerRecoilDeg;
        /// <summary>The blow's segment in the defender's root space (hilt end, tip).</summary>
        public Vector2 strikeBaseRoot, strikeTipRoot;
        /// <summary>The height (root metres) the parry read the blow at.</summary>
        public float strikeY;
    }

    /// <summary>
    /// The contact-matched parry's geometry: where the defender's blade meets the incoming weapon, and the pose that puts it there.
    /// Pure and deterministic (no randomness, no allocation; <see cref="PoseFK"/> checks every result), and the same for Kage parrying a
    /// foe and a foe parrying Kage. All of it runs in the defender's root space (facing right, unscaled), so a fighter facing left or
    /// scaled up (Raikon's 1.24) solves the same mirrored problem.
    /// <list type="number">
    /// <item>The blow's segment and swing go into root space; a blow with no segment (a synthetic hit) gets one from its contact and level.</item>
    /// <item>The guard is adjusted for the height of the blow: a high blow braces upright, a mid one leans in, a low one sinks the stance
    /// (feet planted). Hips never turn.</item>
    /// <item>A search over the defender's blade angle, the point on the incoming segment and the point on its own blade: each candidate puts
    /// the far wrist where that blade point lands on that incoming point (the far arm's analytic IK closes the wrist-to-sword offset), and
    /// is kept only if the wrist is inside the arm's reach (never fully straight, never folded), forward of the shoulder, the blade
    /// above the floor and clear of the body, and the blades cross at 35° or more. The best scores a square cross, a meeting point on the
    /// guard plane at the blow's height, the incoming blade's middle on the defender's forte, a martial shape for the height (roof guard
    /// high, upright uke-nagashi mid, gedan-barai low) and the least change from the guard.</item>
    /// <item>The winner is posed and refined with <see cref="PoseFK"/> until its blade point is on the clash point (well under a millimetre).
    /// Twin blades cross the near blade under it; a bare-handed defender meets the blow on the forearm.</item>
    /// <item>The blow out of reach: the blade goes to the nearest point it can reach, square to the blow (<see cref="ParryContactSolution.clampedToReach"/>).</item>
    /// <item>The final crossing and the normal the parried weapon is thrown along come from the posed blades.</item>
    /// </list>
    /// </summary>
    public static class ParryContactSolver
    {
        /// <summary>Above this (root metres) a blow is high, below <see cref="LowY"/> low.</summary>
        public const float HighY = 1.38f, LowY = 0.78f;
        /// <summary>No parry crosses the blades at less than this.</summary>
        public const float MinCross = 35f;

        // the search grid (fixed, so every run is the same)
        static readonly float[] AttackerT = { 0.06f, 0.14f, 0.22f, 0.32f, 0.42f, 0.52f, 0.62f, 0.72f, 0.82f, 0.92f, 1f };
        static readonly float[] OwnU = { 0.1f, 0.22f, 0.34f, 0.46f, 0.58f };
        static readonly float[] PolearmOwnU = { 0.02f, 0.1f, 0.22f, 0.34f, 0.46f, 0.58f, 0.72f, 0.84f };
        static readonly float[] ForearmQ = { 0.55f, 0.7f, 0.85f };
        const int AngleSteps = 36;

        struct Arm
        {
            public Vector2 shoulder;
            public Vector2 l1v, l2v;
            public float l1, l2, min, max, handRel;
            /// <summary>Two hands on one handle: the near shoulder must reach the handle too (the near arm's reach, metres).</summary>
            public bool twoHand;
            public Vector2 nearShoulder;
            public float nearReach;
        }

        struct Candidate
        {
            public bool ok;
            public float score, theta, t, u, q;
            public bool edgeUp;
            public Vector2 wrist, clash;
        }

        /// <summary>
        /// Solves the parry of <paramref name="strike"/> by the defender described by <paramref name="spec"/> from its guard
        /// <paramref name="guard"/>. <paramref name="output"/> gets the clash pose (root space, not yet through the fighter's grip finish);
        /// <paramref name="fk"/> is scratch forward kinematics on the defender's rig.
        /// </summary>
        public static ParryContactSolution Solve(in StrikeGeometry strike, in DefenderBladeSpec spec, Pose guard, Pose output, PoseFK fk)
        {
            var sol = new ParryContactSolution { clashWorld = strike.rawContactWorld };
            output.CopyFrom(guard);
            output.MarkDirty();
            var d = spec.rigDef;
            if (d == null || fk == null || fk.def != d || !spec.frame.Valid) return sol;
            var frame = spec.frame;

            // 1. the blow in root space
            StrikeInRoot(strike, frame, out Vector2 a, out Vector2 b, out Vector2 swing, out float strikeY);
            sol.strikeBaseRoot = a;
            sol.strikeTipRoot = b;
            sol.strikeY = strikeY;
            float phi = KMath.Ang(b - a);

            // 2. the guard braced for the height of the blow
            Brace(output, strikeY, spec.crouchHeld, ref sol);
            output.MarkDirty();
            fk.Solve(output);
            var arm = new Arm
            {
                shoulder = fk.pos[(int)B.ArmFU],
                l1v = d.BindLocal(B.ArmFL), l2v = d.BindLocal(B.HandF),
                handRel = output.a[(int)B.HandF],
            };
            arm.l1 = arm.l1v.magnitude;
            arm.l2 = arm.l2v.magnitude;
            arm.min = Mathf.Abs(arm.l1 - arm.l2) + 0.045f;
            arm.max = (arm.l1 + arm.l2) * 0.955f;
            arm.twoHand = spec.gripHeld && !spec.dual && !spec.bareHanded;
            arm.nearShoulder = fk.pos[(int)B.ArmNU];
            arm.nearReach = (d.BindLocal(B.ArmNL).magnitude + d.BindLocal(B.HandN).magnitude) * 0.97f;
            Vector2 hips = fk.pos[(int)B.Hips], head = fk.pos[(int)B.Head];
            Vector2 guardWrist = fk.pos[(int)B.HandF];
            float guardHand = fk.rot[(int)B.HandF];
            float guardBlade = guard.hasSwAbs ? guard.swAbs : fk.BladeAngle;
            var torso = new Rect(hips.x - 0.2f, hips.y - 0.35f, 0.33f, head.y + 0.25f - (hips.y - 0.35f));     // thighs to the crown
            float plane = spec.guardPlaneX + (strikeY > HighY ? -0.08f : 0f) + (spec.polearm ? 0.04f : 0f);
            float pref = PreferredAngle(strikeY, guardBlade);
            Vector2 aim = new(plane, Mathf.Clamp(strikeY, 0.3f, 1.85f));

            // 3-4. the search
            bool isThrust = strike.strikeVector == StrikeVector.Thrust || (swing.sqrMagnitude > 1e-6f && Mathf.Abs(Vector2.Dot(KMath.Dir(phi), swing)) > 0.82f);
            float idealCross = isThrust ? 75f : 90f;
            var best = new Candidate { score = float.MaxValue };
            if (spec.bareHanded)
            {
                foreach (float t in AttackerT)
                    foreach (float q in ForearmQ)
                        Consider(ref best, ForearmCandidate(d, arm, a, b, t, q, phi, aim, guardWrist, hips, strike.isBlade));
            }
            else
            {
                int extra = isThrust ? 4 : 2;
                for (int i = 0; i < AngleSteps + extra; i++)
                {
                    float theta = i < AngleSteps ? -180f + i * (360f / AngleSteps)
                        : i == AngleSteps ? KMath.Wrap(phi + 90f)
                        : i == AngleSteps + 1 ? KMath.Wrap(phi - 90f)
                        : i == AngleSteps + 2 ? KMath.Wrap(phi + idealCross)
                        : KMath.Wrap(phi - idealCross);
                    bool edge = EdgeFor(theta, swing, guard.edgeUp);
                    foreach (float t in AttackerT)
                        foreach (float u in (spec.polearm ? PolearmOwnU : OwnU))
                            Consider(ref best, BladeCandidate(d, spec, arm, a, b, t, u, theta, edge, phi, idealCross, aim, pref, guardWrist, guardHand, hips, torso, strike.isBlade));
                }
            }

            // 5. the blow out of reach: the nearest point the blade (or the forearm) can get to, square to the blow
            bool clamped = !best.ok;
            if (clamped) best = Clamped(d, spec, arm, a, b, phi, pref, swing, guard.edgeUp, guardHand);
            sol.clampedToReach = clamped;

            // 4b. pose it and refine it on the posed skeleton
            Materialize(output, spec, best, arm.handRel);
            Vector2 target = best.clash;
            Vector2 ownLocal = Vector2.Lerp(spec.bladeBaseLocal, spec.bladeTipLocal, best.u);
            for (int it = 0; it < 4; it++)
            {
                fk.Solve(output);
                Vector2 got = spec.bareHanded ? ForearmPoint(fk, d, best.q) : fk.Point(B.Sword, ownLocal);
                Vector2 err = target - got;
                if (err.sqrMagnitude < 1e-8f) break;
                best.wrist = ClampReach(best.wrist + err, arm);
                Materialize(output, spec, best, arm.handRel);
            }
            if (spec.dual && !spec.bareHanded) CrossNearBlade(output, fk, d, spec, best, ref sol);

            // 6. the posed blades: the crossing and the normal
            fk.Solve(output);
            Vector2 d0, d1;
            if (spec.bareHanded) { d0 = fk.pos[(int)B.ArmFL]; d1 = fk.Point(B.HandF, d.BindLocal(B.Sword)); }
            else { d0 = fk.Point(B.Sword, spec.bladeBaseLocal); d1 = fk.Point(B.Sword, spec.bladeTipLocal); }
            Vector2 clash;
            if (ClashGeometry.SegmentsCross(d0, d1, a, b, out var cross)) { clash = cross; sol.contactGap = 0f; }
            else
            {
                ClosestPoints(d0, d1, a, b, out var pd, out var pa);
                clash = (pd + pa) * 0.5f;
                sol.contactGap = (pd - pa).magnitude;
            }
            Vector2 own = d1 - d0;
            float ownLen = own.magnitude;
            Vector2 ownDir = ownLen > 1e-6f ? own / ownLen : KMath.Dir(best.theta);
            // the parried weapon is thrown off the defender's blade: against its swing, away from the defender's body, and (a low blow) off the floor
            Vector2 n = new(-ownDir.y, ownDir.x);
            if (NormalScore(-n, swing, clash) > NormalScore(n, swing, clash)) n = -n;

            sol.valid = true;
            sol.clashRoot = clash;
            sol.clashWorld = frame.ToWorld(clash);
            sol.clashNormalWorld = frame.DirToWorld(n);
            sol.defenderBladeAngleRoot = spec.bareHanded ? KMath.Ang(own) : best.theta;
            sol.crossAngleDeg = CrossAngle(KMath.Ang(own), phi);
            sol.bladeContactParam = ownLen > 1e-6f ? Mathf.Clamp01(Vector2.Dot(clash - d0, ownDir) / ownLen) : 0f;
            sol.edgeUp = output.edgeUp;
            sol.wristTargetRoot = best.wrist;
            sol.attackerRecoilDeg = Mathf.Lerp(35f, 48f, Mathf.Clamp01(sol.crossAngleDeg / 90f));
            return sol;
        }

        // ------------------------------------------------------------------------------------------------ the blow

        static void StrikeInRoot(in StrikeGeometry s, RootFrame frame, out Vector2 a, out Vector2 b, out Vector2 swing, out float strikeY)
        {
            Vector2 c = frame.ToRoot(s.rawContactWorld);
            bool seg = s.hasSegment && (s.tipWorld - s.baseWorld).sqrMagnitude > 1e-4f;
            if (seg) { a = frame.ToRoot(s.baseWorld); b = frame.ToRoot(s.tipWorld); }
            else
            {
                Vector2 hilt = KMath.Dir(s.level switch { HitLevel.Overhead => 55f, HitLevel.High => 22f, HitLevel.Low => 18f, _ => 6f });
                a = c + hilt * 0.62f;
                b = c - hilt * 0.12f;
            }
            swing = s.swingDirWorld.sqrMagnitude > 1e-8f ? frame.DirToRoot(s.swingDirWorld) : Vector2.zero;
            if (swing.sqrMagnitude < 1e-6f)
                swing = s.strikeVector switch
                {
                    StrikeVector.HighOverhead => KMath.Dir(-115f),
                    StrikeVector.Upward => KMath.Dir(125f),
                    StrikeVector.Low => KMath.Dir(175f),
                    StrikeVector.Thrust => KMath.Dir(180f),
                    _ => KMath.Dir(s.level switch { HitLevel.Overhead => -110f, HitLevel.High => 190f, HitLevel.Low => 175f, _ => 180f }),
                };
            strikeY = c.y;
            if (s.strikeVector == StrikeVector.Low || s.level == HitLevel.Low) strikeY = Mathf.Min(strikeY, 0.7f);
            else if (s.strikeVector == StrikeVector.HighOverhead || s.level == HitLevel.Overhead) strikeY = Mathf.Max(strikeY, HighY + 0.06f);
        }

        /// <summary>The guard braced for the height of the blow (root space): the hips never turn and the feet stay where they were.</summary>
        static void Brace(Pose p, float y, bool crouch, ref ParryContactSolution sol)
        {
            Vector2 hip = Vector2.zero;
            float chest = 0f, head = 0f;
            if (y > HighY)
            {
                float k = Mathf.Clamp01((y - HighY) / 0.4f);
                chest = 8f * k; head = -6f * k; hip.y = 0.025f * k;
            }
            else if (y >= LowY) { hip.x = 0.02f; chest = -3f; }
            else
            {
                float k = Mathf.Clamp01((LowY - y) / 0.5f);
                if (crouch) { chest = -4f * k; head = 3f * k; }
                else { hip.y = -0.16f * k; chest = -14f * k; head = 10f * k; }
            }
            p.hip += hip;
            p.a[(int)B.Chest] += chest;
            p.a[(int)B.Head] += head;
            sol.hipOffsetDelta = hip;
            sol.chestRotDelta = chest;
            sol.headRotDelta = head;
            sol.hipRotDelta = 0f;
        }

        /// <summary>The blade shape a parry at this height reads as: a roof guard high, the guard's own upright mid, gedan-barai low.</summary>
        public static float PreferredAngle(float strikeY, float guardBlade)
        {
            if (strikeY > HighY) return 140f;
            if (strikeY < LowY) return guardBlade < -20f ? guardBlade : -60f;
            return guardBlade;
        }

        /// <summary>The edge meets the blow: turned over when the plain edge would face away from it.</summary>
        static bool EdgeFor(float theta, Vector2 swing, bool fallback)
        {
            if (swing.sqrMagnitude < 1e-6f) return fallback;
            return Vector2.Dot(KMath.Dir(theta - 90f), -swing) < 0f;
        }

        // ------------------------------------------------------------------------------------------------ candidates

        static void Consider(ref Candidate best, in Candidate c)
        {
            if (c.ok && c.score < best.score) best = c;
        }

        static Candidate BladeCandidate(RigDef d, in DefenderBladeSpec spec, in Arm arm, Vector2 a, Vector2 b, float t, float u, float theta, bool edge,
            float phi, float idealCross, Vector2 aim, float pref, Vector2 guardWrist, float guardHand, Vector2 hips, Rect torso, bool incomingBlade)
        {
            var c = new Candidate { theta = theta, t = t, u = u, edgeUp = edge };
            float cross = CrossAngle(theta, phi);
            if (cross < MinCross) return c;
            float bind = d.bladeBindAngle, swordRot = theta - bind;
            Vector2 C = Vector2.Lerp(a, b, t);
            Vector2 local = Vector2.Lerp(spec.bladeBaseLocal, spec.bladeTipLocal, u);
            if (edge) local = Pose.TurnOver(local, bind);
            Vector2 pivot = C - KMath.Rot(local, swordRot);
            Vector2 w = WristFor(pivot, d, arm, guardHand, out float rHand);
            float dist = (w - arm.shoulder).magnitude;
            if (dist < arm.min || dist > arm.max) return c;
            if (w.x < arm.shoulder.x - 0.08f) return c;
            if (arm.twoHand && !NearHandReaches(d, spec, arm, pivot, theta, rHand)) return c;
            Vector2 baseL = spec.bladeBaseLocal, tipL = spec.bladeTipLocal;
            if (edge) { baseL = Pose.TurnOver(baseL, bind); tipL = Pose.TurnOver(tipL, bind); }
            Vector2 p0 = pivot + KMath.Rot(baseL, swordRot), p1 = pivot + KMath.Rot(tipL, swordRot);
            if (Mathf.Min(p0.y, p1.y) < 0.06f) return c;
            if (spec.keepPointOffFloor && w.y + (spec.reach + 0.08f) * Mathf.Sin(theta * Mathf.Deg2Rad) < 0.07f) return c;
            for (int k = 0; k <= 6; k++) if (torso.Contains(Vector2.Lerp(p0, p1, k / 6f))) return c;

            float s = Sq((idealCross - cross) / 90f) * 2.5f;
            if (cross < 50f) s += Sq((50f - cross) / 50f) * 1.2f;
            s += ((C - aim) / 0.3f).sqrMagnitude * 0.6f;
            if (C.x < hips.x + 0.1f) s += 3f + (hips.x + 0.1f - C.x) * 10f;
            s += Outside(t, incomingBlade ? 0.35f : 0.5f, incomingBlade ? 0.85f : 1f) * 2f;
            s += Sq((u - 0.32f) / 0.3f) * 0.6f;
            s += Sq(Mathf.DeltaAngle(theta, pref) / 90f) * 0.9f;
            s += Sq((w - guardWrist).magnitude / 0.45f) * 0.5f;
            float slack = Mathf.Min(dist - arm.min, arm.max - dist);
            if (slack < 0.04f) s += (0.04f - slack) * 10f;
            c.ok = true;
            c.score = s;
            c.wrist = w;
            c.clash = C;
            return c;
        }

        static Candidate ForearmCandidate(RigDef d, in Arm arm, Vector2 a, Vector2 b, float t, float q, float phi, Vector2 aim, Vector2 guardWrist,
            Vector2 hips, bool incomingBlade)
        {
            var c = new Candidate { t = t, q = q };
            Vector2 C = Vector2.Lerp(a, b, t);
            Vector2 w = ClampReach(C, arm);
            Vector2 fistOff = d.BindLocal(B.Sword);
            for (int it = 0; it < 5; it++)
            {
                Elbow(arm, w, out var elbow, out var wrist, out float rLower);
                Vector2 fist = wrist + KMath.Rot(fistOff, rLower + arm.handRel);
                Vector2 p = Vector2.Lerp(elbow, fist, q);
                w = ClampReach(w + (C - p), arm);
            }
            Elbow(arm, w, out var e2, out var w2, out float r2);
            Vector2 f2 = w2 + KMath.Rot(fistOff, r2 + arm.handRel);
            if (((Vector2.Lerp(e2, f2, q)) - C).sqrMagnitude > 0.03f * 0.03f) return c;
            float cross = CrossAngle(KMath.Ang(f2 - e2), phi);
            if (cross < 25f) return c;
            if (w.x < arm.shoulder.x - 0.05f || f2.y < 0.3f) return c;
            float s = Sq((90f - cross) / 90f) * 2.5f;
            s += ((C - aim) / 0.3f).sqrMagnitude * 0.6f;
            if (C.x < hips.x + 0.1f) s += 3f;
            s += Outside(t, incomingBlade ? 0.35f : 0.5f, incomingBlade ? 0.85f : 1f) * 2f;
            s += Sq((w - guardWrist).magnitude / 0.45f) * 0.5f;
            c.ok = true;
            c.score = s;
            c.wrist = w;
            c.clash = C;
            return c;
        }

        /// <summary>No candidate reached the blow: the blade (or forearm) square to it at the nearest point the arm can get to.</summary>
        static Candidate Clamped(RigDef d, in DefenderBladeSpec spec, in Arm arm, Vector2 a, Vector2 b, float phi, float pref, Vector2 swing, bool guardEdge, float guardHand)
        {
            Vector2 near = ClosestOnSegment(a, b, arm.shoulder);
            var c = new Candidate { ok = true, u = 0.5f, q = 0.8f, clash = near };
            if (spec.bareHanded)
            {
                c.wrist = ClampReach(near, arm);
                return c;
            }
            float t1 = KMath.Wrap(phi + 90f), t2 = KMath.Wrap(phi - 90f);
            c.theta = Mathf.Abs(Mathf.DeltaAngle(t1, pref)) <= Mathf.Abs(Mathf.DeltaAngle(t2, pref)) ? t1 : t2;
            if (Mathf.Sin(c.theta * Mathf.Deg2Rad) < -0.2f && spec.keepPointOffFloor) c.theta = KMath.Wrap(c.theta + 180f);
            c.edgeUp = EdgeFor(c.theta, swing, guardEdge);
            Vector2 local = Vector2.Lerp(spec.bladeBaseLocal, spec.bladeTipLocal, c.u);
            if (c.edgeUp) local = Pose.TurnOver(local, d.bladeBindAngle);
            Vector2 pivot = near - KMath.Rot(local, c.theta - d.bladeBindAngle);
            c.wrist = ClampReach(WristFor(pivot, d, arm, guardHand, out _), arm);
            return c;
        }

        /// <summary>Poses the candidate: the far wrist and blade (or forearm), keeping everything else of the braced guard.</summary>
        static void Materialize(Pose p, in DefenderBladeSpec spec, in Candidate c, float handRel)
        {
            p.HandF(c.wrist.x, c.wrist.y, handRel);
            if (!spec.bareHanded)
            {
                p.Blade(c.theta);
                p.edgeUp = c.edgeUp;
            }
            float spacing = spec.gripSpacing != 0f ? spec.gripSpacing : Pose.GripSpacing;
            float pommel = spec.gripPommel != 0f ? spec.gripPommel : Pose.GripPommel;
            if (spec.polearm && spec.crouchHeld) p.GripSpan(Mathf.Max(spacing, -0.16f), pommel);
            else { p.gripSpacing = spacing; p.gripPommel = pommel; }
            p.MarkDirty();
        }

        /// <summary>Twin blades: the near blade crosses under the far one in front (jūji-uke), its hand within the near arm's reach.</summary>
        static void CrossNearBlade(Pose p, PoseFK fk, RigDef d, in DefenderBladeSpec spec, in Candidate c, ref ParryContactSolution sol)
        {
            fk.Solve(p);
            Vector2 shN = fk.pos[(int)B.ArmNU];
            float l1 = d.BindLocal(B.ArmNL).magnitude, l2 = d.BindLocal(B.HandN).magnitude;
            var near = new Arm { shoulder = shN, l1 = l1, l2 = l2, min = Mathf.Abs(l1 - l2) + 0.045f, max = (l1 + l2) * 0.955f };
            Vector2 w = ClampReach(c.wrist + new Vector2(-0.05f, -0.12f), near);
            float ang = KMath.Wrap(c.theta + (c.theta > 0f ? -50f : 50f));
            p.HandN(w.x, w.y, 0f);
            p.BladeN(ang);
            p.dual = true;
            p.MarkDirty();
            sol.nearWristTargetRoot = w;
            sol.nearBladeAngleRoot = ang;
        }

        // ------------------------------------------------------------------------------------------------ the far arm

        /// <summary>
        /// The far wrist that puts the sword's pivot at <paramref name="pivot"/>: the pivot sits a hand's length off the wrist, turned with
        /// the hand, and the hand turns with the forearm the IK gives, so it is found by a short fixed-point loop on the arm's analytic IK
        /// (the same two-bone solve <see cref="Pose.Resolve"/> runs).
        /// </summary>
        static Vector2 WristFor(Vector2 pivot, RigDef d, in Arm arm, float guardHand, out float rHand)
        {
            Vector2 off = d.BindLocal(B.Sword);
            rHand = guardHand;
            Vector2 w = pivot - KMath.Rot(off, rHand);
            for (int k = 0; k < 3; k++)
            {
                Elbow(arm, w, out _, out _, out float rLower);
                rHand = rLower + arm.handRel;
                w = pivot - KMath.Rot(off, rHand);
            }
            return w;
        }

        /// <summary>
        /// The two-handed grip holds: the near wrist can reach some point of the handle behind the far fist (its grip spot back to the pommel,
        /// as <see cref="Pose"/>'s grip slides it), so both fists stay wrapped round the tsuka.
        /// </summary>
        static bool NearHandReaches(RigDef d, in DefenderBladeSpec spec, in Arm arm, Vector2 pivot, float theta, float rHand)
        {
            Vector2 axis = KMath.Dir(theta);
            float spacing = spec.gripSpacing != 0f ? spec.gripSpacing : Pose.GripSpacing;
            float pommel = spec.gripPommel != 0f ? spec.gripPommel : Pose.GripPommel;
            if (spec.polearm && spec.crouchHeld) spacing = Mathf.Max(spacing, -0.16f);
            Vector2 fistToWrist = -KMath.Rot(d.fistLocal, rHand);
            Vector2 h0 = pivot + axis * spacing + fistToWrist, h1 = pivot + axis * pommel + fistToWrist;
            Vector2 q = ClosestOnSegment(h0, h1, arm.nearShoulder);
            return (q - arm.nearShoulder).magnitude <= arm.nearReach;
        }

        /// <summary>The far arm's two-bone IK for wrist goal <paramref name="w"/> (bend -1, as <see cref="Pose.Resolve"/>): elbow, reached wrist, forearm world angle.</summary>
        static void Elbow(in Arm arm, Vector2 w, out Vector2 elbow, out Vector2 wrist, out float rLower)
        {
            Vector2 to = w - arm.shoulder;
            float mag = to.magnitude;
            float dist = Mathf.Clamp(mag, Mathf.Abs(arm.l1 - arm.l2) + 1e-4f, (arm.l1 + arm.l2) * 0.9995f);
            Vector2 dir = mag > 1e-6f ? to / mag : Vector2.right;
            float cosA = Mathf.Clamp((arm.l1 * arm.l1 + dist * dist - arm.l2 * arm.l2) / (2f * arm.l1 * dist), -1f, 1f);
            float abs1 = KMath.Ang(dir) - Mathf.Acos(cosA) * Mathf.Rad2Deg;
            elbow = arm.shoulder + KMath.Dir(abs1) * arm.l1;
            wrist = arm.shoulder + dir * dist;
            rLower = KMath.Ang(wrist - elbow) - KMath.Ang(arm.l2v);
        }

        static Vector2 ClampReach(Vector2 w, in Arm arm)
        {
            Vector2 to = w - arm.shoulder;
            float m = to.magnitude;
            if (m < 1e-6f) return arm.shoulder + Vector2.right * arm.min;
            float k = Mathf.Clamp(m, arm.min, arm.max);
            return arm.shoulder + to * (k / m);
        }

        /// <summary>Clamps a far-arm wrist target (root space) strictly inside the anatomical reach annulus [armMin, armMax].</summary>
        public static Vector2 ClampFarArmReach(RigDef d, Vector2 shoulder, Vector2 wrist)
        {
            float l1 = d.BindLocal(B.ArmFL).magnitude, l2 = d.BindLocal(B.HandF).magnitude;
            float min = Mathf.Abs(l1 - l2) + 0.045f, max = (l1 + l2) * 0.955f;
            Vector2 to = wrist - shoulder;
            float m = to.magnitude;
            if (m < 1e-6f) return shoulder + Vector2.right * min;
            return shoulder + to * (Mathf.Clamp(m, min, max) / m);
        }

        /// <summary>A point <paramref name="q"/> of the way from the far elbow to the far fist.</summary>
        static Vector2 ForearmPoint(PoseFK fk, RigDef d, float q) =>
            Vector2.Lerp(fk.pos[(int)B.ArmFL], fk.Point(B.HandF, d.BindLocal(B.Sword)), q);

        // ------------------------------------------------------------------------------------------------ geometry

        /// <summary>How well a normal reads as the way a parried weapon is thrown (root space): against the swing, away from the defender, off the floor.</summary>
        static float NormalScore(Vector2 n, Vector2 swing, Vector2 clash) =>
            (swing.sqrMagnitude > 1e-6f ? Vector2.Dot(n, -swing) : 0f) + 0.6f * n.x + (clash.y < 0.6f ? 0.5f * n.y : 0f);

        /// <summary>The acute angle between two lines at angles <paramref name="a"/> and <paramref name="b"/> (degrees), 0 to 90.</summary>
        public static float CrossAngle(float a, float b)
        {
            float d = Mathf.Abs(Mathf.DeltaAngle(a, b)) % 180f;
            return d > 90f ? 180f - d : d;
        }

        static float Sq(float x) => x * x;
        static float Outside(float x, float lo, float hi) => x < lo ? lo - x : x > hi ? x - hi : 0f;

        static Vector2 ClosestOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float l = ab.sqrMagnitude;
            if (l < 1e-10f) return a;
            return a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / l);
        }

        /// <summary>The closest points between segments p0-p1 and q0-q1.</summary>
        public static void ClosestPoints(Vector2 p0, Vector2 p1, Vector2 q0, Vector2 q1, out Vector2 onP, out Vector2 onQ)
        {
            Vector2 bestP = p0, bestQ = ClosestOnSegment(q0, q1, p0);
            float best = (bestP - bestQ).sqrMagnitude;
            void Try(Vector2 x, Vector2 y, ref Vector2 bp, ref Vector2 bq, ref float bd)
            {
                float dd = (x - y).sqrMagnitude;
                if (dd < bd) { bd = dd; bp = x; bq = y; }
            }
            Try(p1, ClosestOnSegment(q0, q1, p1), ref bestP, ref bestQ, ref best);
            Try(ClosestOnSegment(p0, p1, q0), q0, ref bestP, ref bestQ, ref best);
            Try(ClosestOnSegment(p0, p1, q1), q1, ref bestP, ref bestQ, ref best);
            onP = bestP;
            onQ = bestQ;
        }
    }
}
