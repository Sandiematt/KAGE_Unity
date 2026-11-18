using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Base stances and reactions. Root space, facing right, units (Kage ≈ 1.9 tall).
    /// Ankles rest at y = 0.13 (boot height). Blade angles: 0 forward, 90 up, ±180 back.
    /// </summary>
    public static class PoseLib
    {
        /// <summary>The reference pose itself: Kage standing, katana low, exactly as drawn.</summary>
        public static Pose Ref() => new Pose();

        // Two-handed stances (SAN-113, docs/reference/two_handed_sword_stances.png). Only the arms and the
        // blade are new: hips, chest, head and feet stay as they were, so the hurt circles don't move.

        /// <summary>Chūdan-no-kamae: both hands on the grip at the waist, the blade forward and up at the opponent.</summary>
        public static Pose Stance() => new Pose().Hip(0f, -0.08f, -4f).Chest(-8f).Head(5f)
            .Feet(0.24f, -0.26f).HandF(ChudanX, ChudanY, 0f).Blade(ChudanBlade).Grip();

        public const float ChudanX = 0.27f, ChudanY = 0.97f, ChudanBlade = 34f;

        /// <summary>Gedan-no-kamae: crouched, both hands low, the point lowered towards the opponent's knees.</summary>
        public static Pose Crouch() => new Pose().Hip(-0.02f, -0.3f, 0f).Chest(-20f).Head(14f)
            .FootN(0.24f, 0.13f).FootF(-0.26f, 0.16f, -25f).HandF(0.27f, 0.74f).Blade(-18f).Grip();

        /// <summary>High guard: both hands raised in front of the chest, the blade held straight up across the line of attack.</summary>
        public static Pose Block() => new Pose().Hip(-0.03f, -0.1f, -2f).Chest(-2f).Head(-6f)
            .Feet(0.2f, -0.28f).HandF(0.24f, 1.2f, 0f).Blade(84f).Grip();

        /// <summary>Low guard (Gedan): crouched, both hands low in front, the blade lowered straight across the legs.</summary>
        public static Pose CrouchBlock() => new Pose().Hip(-0.02f, -0.3f, 0f).Chest(-16f).Head(8f)
            .FootN(0.24f, 0.13f).FootF(-0.26f, 0.16f, -25f).HandF(0.26f, 0.84f).Blade(-62f).Grip();

        // ---------------------------------------------------------------- reference stances (start / finish of swings)
        /// <summary>Jōdan-no-kamae: hands above the forehead, the blade raised up and back. Overhead cuts start here.</summary>
        public static Pose Jodan(Pose p) => p.HandF(0.06f, 1.62f, 0f).Blade(118f).Grip();
        /// <summary>Waki-gamae: hands at the rear hip, the blade low and pointing back. Sweeps and draws start here.</summary>
        public static Pose Waki(Pose p) => p.HandF(0.02f, 0.86f, 0f).Blade(-160f).Grip();
        /// <summary>Kasumi-no-kamae: hands by the head, the blade level and pointing forward. Thrusts start here.</summary>
        public static Pose Kasumi(Pose p) => p.HandF(0.12f, 1.5f, 0f).Blade(4f).Grip();

        /// <summary>The unarmed guard (sword broken): both fists up.</summary>
        public static Pose FistGuard(Pose p) => p.HandN(0.26f, 1.3f, 10f).HandF(0.16f, 1.18f, 10f);

        public static Pose JumpRise() => new Pose().Hip(0f, 0f, 0f).Chest(-4f).Head(4f)
            .FootN(0.18f, 0.36f, -20f).FootF(-0.14f, 0.3f, -30f).HandN(0.18f, 1.18f).HandF(0.08f, 0.94f).Blade(-140f);

        public static Pose JumpTuck() => new Pose().Hip(0f, 0f, 0f).Chest(-38f).Neck(-10f).Head(-10f)
            .LegN(110f, -140f).LegF(105f, -135f).HandN(0.22f, 0.82f).HandF(0.18f, 0.72f).Blade(-170f);

        public static Pose JumpFall() => new Pose().Hip(0f, 0f, 0f).Chest(-6f).Head(6f)
            .FootN(0.15f, 0.08f, -10f).FootF(-0.15f, 0.1f, -20f).HandN(0.25f, 1.25f).HandF(0.3f, 1.0f).Blade(-60f);

        public static Pose Land() => new Pose().Hip(0f, -0.2f, 0f).Chest(-14f).Head(10f)
            .Feet(0.22f, -0.24f).HandN(0.22f, 1.0f).HandF(0.26f, 0.75f).Blade(-30f);

        /// <summary>Guard broken (SAN-86): knocked upright and open, the blade thrown up and back, the near arm flung wide.</summary>
        public static Pose GuardBroken() => new Pose().Hip(-0.02f, -0.08f, 4f).Chest(8f).Neck(6f).Head(12f)
            .Feet(0.26f, -0.3f).HandN(-0.16f, 1.22f, 70f).HandF(0.02f, 1.42f).Blade(140f);

        public static Pose HurtHigh() => new Pose().Hip(-0.05f, -0.08f, 6f).Chest(16f).Neck(8f).Head(22f)
            .Feet(0.22f, -0.28f).HandN(0.0f, 1.0f, 40f).HandF(-0.02f, 0.82f).Blade(-120f);

        public static Pose HurtBody() => new Pose().Hip(-0.03f, -0.14f, -6f).Chest(-34f).Neck(-6f).Head(-6f)
            .Feet(0.2f, -0.28f).HandN(0.14f, 0.88f, 30f).HandF(0.06f, 0.74f).Blade(-100f);

        public static Pose HurtLow() => new Pose().Hip(-0.02f, -0.22f, -2f).Chest(-14f).Head(12f)
            .FootN(0.28f, 0.16f, 30f).FootF(-0.24f, 0.13f).HandN(0.12f, 1.0f).HandF(0.1f, 0.8f).Blade(-60f);

        public static Pose Tumble() => new Pose().Hip(0f, 0f, 35f).Chest(20f).Head(18f)
            .LegN(60f, -40f).LegF(35f, -30f).ArmN(150f, 30f).ArmF(120f, 20f);

        public static Pose Lie() => new Pose().Hip(0.25f, -0.54f, 90f).Chest(0f).Head(-8f)
            .ArmN(30f, 10f).ArmF(-30f, 10f).LegN(0f, 0f).LegF(10f, 0f);

        public static Pose LieFront() => new Pose().Hip(-0.2f, -0.56f, -90f).Chest(0f).Head(14f)
            .ArmN(-40f, -20f).ArmF(40f, -30f).LegN(0f, 0f).LegF(-8f, 0f);

        public static Pose Thrown(float spin) => new Pose().Hip(0f, 0f, spin).Chest(18f).Head(16f)
            .LegN(50f, -60f).LegF(30f, -40f).ArmN(140f, 40f).ArmF(110f, 30f);

        public static Pose Victory() => new Pose().Head(-6f).Neck(-4f);

        // ---------------------------------------------------------------- air
        /// <summary>Top of the jump: knees drawn up, guard held.</summary>
        public static Pose JumpApex() => new Pose().Hip(0f, 0.02f, 0f).Chest(-10f).Head(8f)
            .FootN(0.22f, 0.45f, -25f).FootF(-0.06f, 0.38f, -40f).HandN(0.24f, 1.22f, 10f).HandF(0.12f, 0.98f).Blade(-120f);

        /// <summary>Second-jump kick-off: a tight tuck (no rotation).</summary>
        public static Pose AirTuck() => new Pose().Hip(0f, 0.04f, 0f).Chest(-24f).Neck(-6f).Head(6f)
            .FootN(0.18f, 0.56f, -35f).FootF(0.0f, 0.5f, -45f).HandN(0.26f, 1.0f, 10f).HandF(0.18f, 0.86f).Blade(-150f);

        /// <summary>Descending: legs open towards the ground, arms rise for balance.</summary>
        public static Pose FallOpen() => new Pose().Hip(0f, 0.02f, 3f).Chest(2f).Head(-4f)
            .FootN(0.18f, 0.12f, 10f).FootF(-0.16f, 0.2f, -30f).HandN(0.36f, 1.32f, 35f).HandF(-0.04f, 1.18f).Blade(-150f);

        /// <summary>Long, fast drop: arms high and wide, back arched, rear leg trailing.</summary>
        public static Pose FallLong() => new Pose().Hip(0f, 0.03f, 6f).Chest(8f).Head(-10f)
            .FootN(0.2f, 0.1f, 15f).FootF(-0.2f, 0.24f, -35f).HandN(0.4f, 1.48f, 60f).HandF(-0.28f, 1.4f).Blade(-195f);

        /// <summary>Last few frames before touchdown: knees soften, feet reach for the floor.</summary>
        public static Pose PreLand() => new Pose().Hip(0f, -0.04f, 0f).Chest(-10f).Head(8f)
            .FootN(0.24f, 0.2f, 12f).FootF(-0.22f, 0.22f, -8f).HandN(0.26f, 1.12f, 15f).HandF(0.2f, 0.86f).Blade(-45f);

        /// <summary>Hard landing: deep knee bend absorbing a long fall.</summary>
        public static Pose LandDeep() => new Pose().Hip(0.02f, -0.32f, 0f).Chest(-24f).Head(18f)
            .Feet(0.25f, -0.27f).HandN(0.3f, 0.86f).HandF(0.3f, 0.6f).Blade(-25f);

        // ---------------------------------------------------------------- dash / skid
        public static Pose DashFwd() => new Pose().Hip(0.06f, -0.2f, -14f).Chest(-24f).Head(26f)
            .FootN(0.4f, 0.15f, -8f).FootF(-0.42f, 0.24f, -40f).HandN(0.3f, 0.98f, 10f).HandF(-0.14f, 0.92f).Blade(-170f);

        public static Pose DashBack() => new Pose().Hip(-0.06f, -0.14f, 8f).Chest(4f).Head(-2f)
            .FootN(0.36f, 0.2f, 25f).FootF(-0.34f, 0.13f, -5f).HandN(0.22f, 1.22f, 30f).HandF(0.16f, 0.98f).Blade(-70f);

        public static Pose AirDash() => new Pose().Hip(0f, 0.05f, -18f).Chest(-20f).Head(28f)
            .FootN(0.12f, 0.38f, -30f).FootF(-0.32f, 0.3f, -50f).HandN(0.32f, 1.12f, 10f).HandF(-0.22f, 1.0f).Blade(-172f);

        /// <summary>Braking out of a run: lead foot planted ahead, body leaning back.</summary>
        public static Pose Skid() => new Pose().Hip(-0.04f, -0.2f, 8f).Chest(8f).Head(-2f)
            .FootN(0.4f, 0.13f, 22f).FootF(-0.18f, 0.13f, -6f).HandN(0.3f, 1.08f, 20f).HandF(0.0f, 0.86f).Blade(-60f);

        // ---------------------------------------------------------------- locomotion
        // Feet are planted: while a foot is on the ground it moves backwards in root space at exactly
        // the body's ground speed, so the cycle never slides. Phase is in cycles and advances by
        // distance travelled (GaitRate), never by time.
        public const float WalkDuty = 0.58f, WalkStride = 0.38f, RunDuty = 0.34f, RunStride = 0.6f;

        /// <summary>Gait cycles per second for a ground speed (m/s) and walk(0)..run(1) blend.</summary>
        public static float GaitRate(float speed, float run) =>
            speed * Mathf.Lerp(WalkDuty, RunDuty, run) / Mathf.Lerp(WalkStride, RunStride, run);

        /// <summary>One foot of a planted gait. p = foot phase (cycles), dir = +1 moving forward / -1 back.</summary>
        static void GaitFoot(float p, float dir, float centre, float stride, float duty, float lift, bool run,
            out float x, out float y, out float ang)
        {
            p -= Mathf.Floor(p);
            if (p < duty)
            {
                float s = p / duty;
                x = centre + dir * stride * (0.5f - s);
                y = 0.13f;
                ang = run ? Mathf.Lerp(6f, -16f, s) : 0f;      // heel-to-toe roll while running
            }
            else
            {
                float s = (p - duty) / (1f - duty);
                float e = KMath.Smooth01(s);
                x = centre + dir * stride * (-0.5f + e);
                // the run lifts the heel early (kick back), the walk shuffles low and even
                float arc = run ? Mathf.Sin(Mathf.PI * Mathf.Pow(s, 0.7f)) : Mathf.Sin(Mathf.PI * s);
                y = 0.13f + lift * arc;
                ang = run ? Mathf.Lerp(-50f, 8f, e) : -arc * lift * 120f;
            }
        }

        /// <summary>Fighting-stance shuffle walk (feet never cross). phase in cycles.</summary>
        public static Pose WalkCycle(float phase, float dir, Pose into)
        {
            GaitFoot(phase, dir, 0.24f, WalkStride, WalkDuty, 0.055f, false, out float nx, out float ny, out float na);
            GaitFoot(phase + 0.5f, dir, -0.26f, WalkStride, WalkDuty, 0.055f, false, out float fx, out float fy, out float fa);
            float w = phase * Mathf.PI * 2f;
            float s = Mathf.Sin(w), bob = Mathf.Cos(w * 2f);
            into.Hip(0.01f * dir, -0.09f + bob * 0.012f, -4f - 2f * dir).Chest(-9f - 2f * dir + s * 1.2f).Head(5f - s * 0.8f)
                .FootN(nx, ny, na).FootF(fx, fy, fa)
                .HandF(ChudanX - s * 0.015f, ChudanY - bob * 0.008f).Blade(ChudanBlade + s * 3f).Grip();
            into.MarkDirty();
            return into;
        }

        /// <summary>Forward sprint: torso low and forward, fist arm pumping, katana trailing. phase in cycles.</summary>
        public static Pose RunCycle(float phase, Pose into)
        {
            GaitFoot(phase, 1f, 0.04f, RunStride, RunDuty, 0.24f, true, out float nx, out float ny, out float na);
            GaitFoot(phase + 0.5f, 1f, 0.0f, RunStride, RunDuty, 0.24f, true, out float fx, out float fy, out float fa);
            // body drops on each mid-stance (twice per cycle) and rises through the flight phase
            float bob = Mathf.Cos((phase - RunDuty * 0.5f) * Mathf.PI * 4f);
            float arm = Mathf.Sin((phase - RunDuty) * Mathf.PI * 2f + Mathf.PI * 0.5f);   // fist swings against the near leg
            into.Hip(0.04f, -0.12f - bob * 0.045f, -9f).Chest(-17f + bob * 2f).Neck(4f).Head(16f - bob * 1.5f)
                .FootN(nx, ny, na).FootF(fx, fy, fa)
                .HandN(0.14f + arm * 0.2f, 1.02f + Mathf.Abs(arm) * 0.05f, 10f + arm * 25f)
                .HandF(-0.1f - arm * 0.07f, 1.0f).Blade(-168f + arm * 5f);
            into.MarkDirty();
            return into;
        }

        /// <summary>Procedural shuffle-walk built on the stance; phase in radians, dir = +1 forward / -1 back.</summary>
        public static Pose Walk(float phase, float dir, Pose into)
        {
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float liftN = Mathf.Max(0f, c * dir) * 0.06f;
            float liftF = Mathf.Max(0f, -c * dir) * 0.06f;
            into.Hip(0.01f * dir, -0.085f + Mathf.Abs(s) * 0.012f, -4f - 2f * dir).Chest(-9f - 2f * dir).Head(5f)
                .FootN(0.24f + s * 0.07f, 0.13f + liftN, liftN * -120f)
                .FootF(-0.26f - s * 0.07f, 0.13f + liftF, liftF * -150f)
                .HandN(0.2f + s * 0.015f, 1.12f + c * 0.01f, 10f).HandF(0.24f - s * 0.01f, 0.88f).Blade(-32f + s * 4f);
            into.MarkDirty();
            return into;
        }
    }
}
