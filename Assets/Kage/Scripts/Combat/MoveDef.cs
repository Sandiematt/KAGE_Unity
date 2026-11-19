using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    public enum Btn { None, P, K, S, X }
    public enum HitLevel { High, Mid, Low, Overhead, Unblockable }
    /// <summary><c>BladeN</c> is the second blade of the twin family, held in the near hand (clash-eligible like <c>Blade</c>).</summary>
    public enum HitPoint { Fist, Hilt, FootN, FootF, ShinN, ShinF, KneeN, Blade, FistF, BladeN }
    public enum FxKind { TrailBlade, TrailFist, TrailFootN, TrailFootF, Dust, AfterImage, ChargeDark, Shockwave, Glint, SlashLine, TrailBladeN }

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
        /// <summary>Multiplies the posture this window adds (ADR-0006; posture itself is SAN-86). Raikō no Tachi is the highest.</summary>
        public float postureMul = 1f;
    }

    /// <summary>A key of a move's blade extension curve (<see cref="MoveDef.Extend"/>).</summary>
    public struct ExtKey { public int frame; public float metres; public Ease ease; }

    public class MotionEvt { public int from, to; public Vector2 vel; public bool setY; }
    public class FxEvt { public int from, to; public FxKind kind; }

    /// <summary>
    /// A data-driven technique: pose keyframes (60 fps frames), hit windows, root motion,
    /// VFX cues and cancel/chain rules. Built with a small fluent API in a family's move set
    /// (<see cref="KatanaMoves"/>). Played at the fighter's playback rate (ADR-0006): frames here are authored frames.
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
        /// <summary>
        /// Throw tuning (SAN-80); the defaults are the shoulder throw's. The grab needs the foe within <c>grabRange</c> metres at
        /// <c>grabFrame</c>; the victim is carried from <c>carryFrom</c> to <c>carryTo</c> metres in front of the thrower (negative =
        /// behind) on an arc <c>carryHeight</c> high; the release launches it with <c>throwKb</c> (x forward of the thrower,
        /// the impulse of a mass-1 victim, SAN-68).
        /// </summary>
        public float grabRange = 0.95f, carryFrom = 0.62f, carryTo = -0.78f, carryHeight = 1.3f;
        public Vector2 throwKb = new Vector2(-2.4f, -3f);
        public int meterCost;
        /// <summary>
        /// How far the weapon's <see cref="HitPoint.Blade"/> line runs past its blade, in metres at reach x1 (SAN-87: the
        /// chain sickle's lash). Keys are eased like pose keys from 0 at frame 0; empty = never extended.
        /// </summary>
        public readonly List<ExtKey> extension = new();
        public int projectileFrame = -1;
        public int counterFrom = -1, counterTo = -1;
        /// <summary>
        /// What a hit caught in the counter window starts (ADR-0008). Null: the Mirror Guard's answer, a teleport behind the attacker and the
        /// family's <see cref="MoveSet.counterSlash"/>. Set (a boss's counter stance: Kuroe's Censure): the blow is turned aside like a parry
        /// (the attacker reels and loses posture by its role, <see cref="ParryTuning"/>) and this move plays where the fighter stands.
        /// </summary>
        public MoveDef counterAnswer;
        /// <summary>The counter window catches only a melee blow from the front (the JS counter stance); a projectile, a hazard or a blow from behind lands as usual.</summary>
        public bool counterFrontOnly;
        public int gravityOffFrom = -1, gravityOffTo = -1;
        public int spinFrom, spinTo; public float spinDeg;
        /// <summary>Armour frames (ADR-0006): light hits (windows without <c>heavy</c>) don't flinch the fighter during them. Applied by the armour check (SAN-80).</summary>
        public int armorFrom = -1, armorTo = -1;

        public MoveDef(string id, string display, int total) { this.id = id; this.display = display; this.total = total; }

        public MoveDef Key(int frame, Pose p, Ease e = Ease.OutQuad)
        {
            keys.Add(new PoseKey { frame = frame, pose = p, ease = e });
            return this;
        }

        public MoveDef Hit(int from, int to, HitPoint[] pts, int dmg, HitLevel lvl, Vector2 kb, int stun = 16, int hitstop = 5,
            float shake = 0.12f, bool knockdown = false, bool launch = false, bool otg = false, bool heavy = false, float radius = 0.09f,
            bool breaksWeapon = false, float postureMul = 1f)
        {
            hits.Add(new HitWindow
            {
                from = from, to = to, points = pts, dmg = dmg, level = lvl, kb = kb, stun = stun, hitstop = hitstop,
                shake = shake, knockdown = knockdown, launch = launch, otg = otg, heavy = heavy, radius = radius, breaksWeapon = breaksWeapon,
                postureMul = postureMul,
                sword = System.Array.IndexOf(pts, HitPoint.Blade) >= 0 || System.Array.IndexOf(pts, HitPoint.BladeN) >= 0
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

        /// <summary>The blade line is <paramref name="metres"/> longer at <paramref name="frame"/> (eased from the previous key; 0 before the first).</summary>
        public MoveDef Extend(int frame, float metres, Ease e = Ease.Linear)
        {
            extension.Add(new ExtKey { frame = frame, metres = metres, ease = e });
            return this;
        }

        /// <summary>The blade extension at a (fractional) move frame, in metres at reach x1.</summary>
        public float ExtensionAt(float f)
        {
            if (extension.Count == 0) return 0f;
            int prevFrame = 0;
            float prev = 0f;
            foreach (var k in extension)
            {
                if (f < k.frame) return Mathf.Lerp(prev, k.metres, KMath.Apply(k.ease, (f - prevFrame) / Mathf.Max(1, k.frame - prevFrame)));
                prevFrame = k.frame;
                prev = k.metres;
            }
            return prev;
        }

        public MoveDef Invuln(int from, int to) { invulnFrom = from; invulnTo = to; return this; }
        public MoveDef NoPush(int from, int to) { noPushFrom = from; noPushTo = to; return this; }
        /// <summary>Armour frames: light hits don't flinch the fighter from <paramref name="from"/> to <paramref name="to"/> (SAN-80 applies it).</summary>
        public MoveDef Armour(int from, int to) { armorFrom = from; armorTo = to; return this; }
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
        public bool Armoured(int f) => f >= armorFrom && f <= armorTo;
    }
}
