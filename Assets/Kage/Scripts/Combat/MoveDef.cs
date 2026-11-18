using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    public enum Btn { None, P, K, S, X }
    public enum HitLevel { High, Mid, Low, Overhead, Unblockable }
    public enum HitPoint { Fist, Hilt, FootN, FootF, ShinN, ShinF, KneeN, Blade, FistF }
    public enum FxKind { TrailBlade, TrailFist, TrailFootN, TrailFootF, Dust, AfterImage, ChargeDark, Shockwave, Glint, SlashLine }

    public class PoseKey
    {
        public int frame; public Pose pose; public Ease ease;
    }

    public class HitWindow
    {
        public int from, to;
        public HitPoint[] points;
        public float radius = 0.09f;
        public int dmg;
        public HitLevel level;
        public Vector2 kb;                 // x is away from the attacker
        public int stun = 16;
        public bool knockdown, launch, otg, heavy, sword;
        /// <summary>A guarded hit from this window shatters the defender's sword (heavy enemy and boss attacks, SAN-113).</summary>
        public bool breaksWeapon;
        public int hitstop = 5;
        public float shake = 0.12f;
    }

    public class MotionEvt { public int from, to; public Vector2 vel; public bool setY; }
    public class FxEvt { public int from, to; public FxKind kind; }

    /// <summary>
    /// A data-driven technique: pose keyframes (60 fps frames), hit windows, root motion,
    /// VFX cues and cancel/chain rules. Built with a small fluent API in <see cref="KageMoves"/>.
    /// </summary>
    public class MoveDef
    {
        public string id, display;
        public int total;
        public readonly List<PoseKey> keys = new();
        public readonly List<HitWindow> hits = new();
        public readonly List<MotionEvt> motions = new();
        public readonly List<FxEvt> fx = new();
        public readonly Dictionary<Btn, MoveDef> chain = new();
        public int cancelFrom = 999;
        public int jumpCancelFrom = -1;     // on hit, jump may cancel recovery from this frame (launchers)
        public int invulnFrom = -1, invulnTo = -1;
        public int noPushFrom = -1, noPushTo = -1;
        public bool swordFront, airborne, endsOnLanding, normalizeAtEnd;
        public bool isThrow; public int grabFrame, releaseFrame; public MoveDef whiff; public int throwDmg;
        public int meterCost;
        public int projectileFrame = -1;
        public int counterFrom = -1, counterTo = -1;
        public int gravityOffFrom = -1, gravityOffTo = -1;
        public int spinFrom, spinTo; public float spinDeg;

        public MoveDef(string id, string display, int total) { this.id = id; this.display = display; this.total = total; }

        public MoveDef Key(int frame, Pose p, Ease e = Ease.OutQuad)
        {
            keys.Add(new PoseKey { frame = frame, pose = p, ease = e });
            return this;
        }

        public MoveDef Hit(int from, int to, HitPoint[] pts, int dmg, HitLevel lvl, Vector2 kb, int stun = 16, int hitstop = 5,
            float shake = 0.12f, bool knockdown = false, bool launch = false, bool otg = false, bool heavy = false, float radius = 0.09f,
            bool breaksWeapon = false)
        {
            hits.Add(new HitWindow
            {
                from = from, to = to, points = pts, dmg = dmg, level = lvl, kb = kb, stun = stun, hitstop = hitstop,
                shake = shake, knockdown = knockdown, launch = launch, otg = otg, heavy = heavy, radius = radius, breaksWeapon = breaksWeapon,
                sword = System.Array.IndexOf(pts, HitPoint.Blade) >= 0
            });
            return this;
        }

        public MoveDef Move(int from, int to, float vx, float vy = 0f, bool setY = false)
        {
            motions.Add(new MotionEvt { from = from, to = to, vel = new Vector2(vx, vy), setY = setY });
            return this;
        }

        public MoveDef Fx(int from, int to, FxKind k)
        {
            fx.Add(new FxEvt { from = from, to = to, kind = k });
            return this;
        }

        public MoveDef Invuln(int from, int to) { invulnFrom = from; invulnTo = to; return this; }
        public MoveDef NoPush(int from, int to) { noPushFrom = from; noPushTo = to; return this; }
        public MoveDef Cancel(int from) { cancelFrom = from; return this; }
        /// <summary>After a confirmed hit, jumping cancels the move's recovery from this frame (chase a launch).</summary>
        public MoveDef JumpCancel(int from) { jumpCancelFrom = from; return this; }
        public MoveDef Sword() { swordFront = true; return this; }
        public MoveDef Air(bool endsOnLanding = true) { airborne = true; this.endsOnLanding = endsOnLanding; return this; }
        public MoveDef Normalize() { normalizeAtEnd = true; return this; }
        /// <summary>Whole-body rotation about the hips between two frames, eased as one continuous turn.</summary>
        public MoveDef Spin(int from, int to, float degrees) { spinFrom = from; spinTo = to; spinDeg = degrees; return this; }

        /// <summary>Samples the move at a (fractional) frame, blending from the pose the fighter was in.</summary>
        public void Evaluate(RigDef d, float f, Pose start, Pose output)
        {
            int prevFrame = 0;
            Pose prev = start;
            bool done = false;
            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                if (f < k.frame)
                {
                    float t = (f - prevFrame) / Mathf.Max(1, k.frame - prevFrame);
                    Pose.Blend(d, prev, k.pose, KMath.Apply(k.ease, t), output);
                    done = true;
                    break;
                }
                prevFrame = k.frame;
                prev = k.pose;
            }
            if (!done) Pose.Blend(d, prev, prev, 0f, output);
            if (spinDeg != 0f && f > spinFrom)
                output.SpinAbout(d, spinDeg * KMath.SpinEase((f - spinFrom) / Mathf.Max(1f, spinTo - spinFrom)));
        }

        public bool Invulnerable(int f) => f >= invulnFrom && f <= invulnTo;
        public bool PassThrough(int f) => f >= noPushFrom && f <= noPushTo;
    }
}
