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
        /// <summary>Every hit of this window is a critical, no roll (SAN-106: Shiden's braced thrust, the JS counter-hit critical).</summary>
        public bool critical;
        /// <summary>
        /// The weapon's damage multiplier does not count for this window (SAN-107): a boss ability whose damage is the JS number, not the number times the weapon
        /// (Raikon's tachi is ×0.78 on his ordinary cuts; his Thunder Swing, Storm Break and Redline hit for the JS damage). Everything else leaves it off.
        /// </summary>
        public bool fixedDamage;
        /// <summary>
        /// A guard does not stop this window, but a parry still does (Raikon's iaijutsu and blinks, the red-eye tell): "dodge it or parry it tight".
        /// Unlike <see cref="HitLevel.Unblockable"/>, which neither guards nor parries. Everything else leaves it off.
        /// </summary>
        public bool unguardable;
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

        // ---- boss-move hooks (SAN-104, Garan; Fighter.MoveHooks.cs). Each is off by default, so no other move changes. ----
        /// <summary>
        /// A throw's grab window (the clinch): the grab catches the foe on any frame from <see cref="grabFrame"/> to this one that it is in
        /// reach, and the carry then runs from this frame. -1: the grab is tried on <see cref="grabFrame"/> only (every other throw).
        /// </summary>
        public int grabTo = -1;
        /// <summary>A hazard (ADR-0007) the move lays down on <see cref="hazardFrame"/>, standing <see cref="hazardAhead"/> metres in front of the fighter's feet (Storm Fist's shock area).</summary>
        public HazardDef hazard;
        public int hazardFrame = -1;
        public float hazardAhead;
        /// <summary>What plays in place of this move once one of its hit windows lands or is blocked (the charge skids to a stop).</summary>
        public MoveDef onContact;
        /// <summary>Frames of stagger when the move's root motion runs the fighter into the wall it faces (the charge); 0 = none.</summary>
        public int wallStagger;

        // ---- more boss-move hooks (SAN-105, Hisame; Fighter.MoveHooks.cs). Off by default too. ----
        /// <summary>
        /// A vault over the current target (Hisame's Kocho): from <see cref="vaultFrom"/> to <see cref="vaultTo"/> the fighter arcs
        /// <see cref="vaultHeight"/> metres high to land <see cref="vaultPast"/> metres past the target, facing it. With less than
        /// <see cref="vaultRoom"/> metres between the target and the wall at its back, it comes down that far on its own side instead.
        /// It stays <see cref="vaultMargin"/> metres inside the stage. -1: no vault.
        /// </summary>
        public int vaultFrom = -1, vaultTo = -1;
        public float vaultPast, vaultHeight, vaultRoom, vaultMargin;
        /// <summary>What the move throws on <see cref="projectileFrame"/> (Smoke Bomb), from <see cref="projectileFrom"/> (metres ahead, up). Null: the kunai or the Crescent.</summary>
        public ProjectileDef projectile;
        public Vector2 projectileFrom;
        /// <summary>Effect cues (ADR-0007) the move plays on a frame where the fighter stands (Kocho's glint).</summary>
        public readonly List<(int frame, string cue)> cues = new();

        // ---- the brace (SAN-106, Shiden; Fighter.MoveHooks.cs). Off by default. ----
        /// <summary>
        /// A brace (Shiden's Braced Spear): on any frame from <see cref="braceFrom"/> to <see cref="braceTo"/>, a current target in front
        /// within <see cref="braceTrigger"/> metres for which <see cref="braceWhen"/> holds (rushing in) sets off <see cref="braceAnswer"/>,
        /// played in place at once. Unlike a counter window it reads the target's approach, not a blow. -1: no brace.
        /// </summary>
        public int braceFrom = -1, braceTo = -1;
        public float braceTrigger;
        public MoveDef braceAnswer;
        public System.Func<Fighter, Fighter, bool> braceWhen;

        // ---- more boss-move hooks (SAN-107, Raikon; Fighter.MoveHooks.cs). Off by default too. ----
        /// <summary>The hazard stands on the current target's feet instead of ahead of the fighter (Skyfall's mark); with <see cref="HazardDef.followFrames"/> it follows the target.</summary>
        public bool hazardOnTarget;
        /// <summary>More projectiles the move throws, each on its own frame (Thunder Swing's second wave); <see cref="projectile"/> is the first.</summary>
        public readonly List<(int frame, ProjectileDef def, Vector2 from, bool back)> extraThrows = new();
        /// <summary>
        /// Another version of this ability to start in place of it, asked of the fighter when the ability is pressed (Thunder Swing's two-wave swing in
        /// the last phase, the shorter Redline stance); null or a null answer starts this move.
        /// </summary>
        public System.Func<Fighter, MoveDef> variantFor;
        /// <summary>Posture the fighter's own bar takes on a frame (<c>fraction</c> of its maximum): Redline's spent stance costs Raikon a share of his posture.</summary>
        public readonly List<(int frame, float fraction)> selfPosture = new();

        // ---- the Lightning Ascendant's hooks (Raikon's three-phase fight; Fighter.MoveHooks.cs). Off by default too. ----
        /// <summary>
        /// More hazards the move lays, each on its own frame: <c>ahead</c> metres in front of the fighter's feet, or on the current target's feet when
        /// <c>onTarget</c> (Heavens' Verdict's three bolts, Blink Flurry's delayed slashes). <see cref="hazard"/> is still the first.
        /// </summary>
        public readonly List<(int frame, HazardDef def, float ahead, bool onTarget)> extraHazards = new();
        /// <summary>Kicks to the fighter's soft parts (<see cref="FighterRig.Impulse"/>) on a frame, world m/s with x forward: dashes, blinks, slams, bursts.</summary>
        public readonly List<(int frame, Vector2 impulse)> impulses = new();
        /// <summary>
        /// From <see cref="pullFrom"/> to <see cref="pullTo"/> the current target is drawn towards the fighter at <see cref="pullSpeed"/> m/s along the floor
        /// (Final Overload's inward pull). A target that is down, thrown, knocked out or invulnerable is not pulled. -1: no pull.
        /// </summary>
        public int pullFrom = -1, pullTo = -1;
        public float pullSpeed;
        /// <summary>The move that starts when this one runs out, whatever happened (Blink Flurry's three dashes); null: back to neutral.</summary>
        public MoveDef then;
        /// <summary>The counter window catches only light blows: a heavy window or a guard-break lands as an ordinary hit (Iron Guard Counter).</summary>
        public bool counterLightOnly;

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
        /// <summary>Lays <paramref name="def"/> down on <paramref name="frame"/>, <paramref name="ahead"/> metres in front of the fighter (SAN-104).</summary>
        public MoveDef Hazard(int frame, HazardDef def, float ahead) { hazardFrame = frame; hazard = def; hazardAhead = ahead; return this; }
        /// <summary>Lays <paramref name="def"/> down on <paramref name="frame"/> on the current target's feet (SAN-107: Skyfall's mark).</summary>
        public MoveDef HazardOnTarget(int frame, HazardDef def) { hazardFrame = frame; hazard = def; hazardOnTarget = true; return this; }
        /// <summary>Throws another <paramref name="def"/> on <paramref name="frame"/> from <paramref name="from"/>, after the first (SAN-107: the second wave).</summary>
        public MoveDef ThrowAlso(int frame, ProjectileDef def, Vector2 from) { extraThrows.Add((frame, def, from, false)); return this; }
        /// <summary>Throws <paramref name="def"/> on <paramref name="frame"/> <b>behind</b> the fighter: <paramref name="from"/> is metres behind, up, and it flies backwards (Final Overload's twin arcs).</summary>
        public MoveDef ThrowBack(int frame, ProjectileDef def, Vector2 from) { extraThrows.Add((frame, def, from, true)); return this; }
        /// <summary>Lays another <paramref name="def"/> on <paramref name="frame"/>, <paramref name="ahead"/> metres in front (see <see cref="extraHazards"/>).</summary>
        public MoveDef HazardAlso(int frame, HazardDef def, float ahead) { extraHazards.Add((frame, def, ahead, false)); return this; }
        /// <summary>Lays another <paramref name="def"/> on <paramref name="frame"/> on the current target's feet (see <see cref="extraHazards"/>).</summary>
        public MoveDef HazardOnTargetAlso(int frame, HazardDef def) { extraHazards.Add((frame, def, 0f, true)); return this; }
        /// <summary>Kicks the soft parts on <paramref name="frame"/> (see <see cref="impulses"/>).</summary>
        public MoveDef Impulse(int frame, Vector2 impulse) { impulses.Add((frame, impulse)); return this; }
        /// <summary>Draws the target in from <paramref name="from"/> to <paramref name="to"/> at <paramref name="speed"/> m/s (see <see cref="pullFrom"/>).</summary>
        public MoveDef Pull(int from, int to, float speed) { pullFrom = from; pullTo = to; pullSpeed = speed; return this; }
        /// <summary>Starts <paramref name="next"/> when this move runs out (see <see cref="then"/>).</summary>
        public MoveDef Then(MoveDef next) { then = next; return this; }
        /// <summary>The last hit window added cannot be guarded but can be parried (<see cref="HitWindow.unguardable"/>).</summary>
        public MoveDef Unguardable() { hits[hits.Count - 1].unguardable = true; return this; }
        /// <summary>The fighter's own posture takes <paramref name="fraction"/> of its maximum on <paramref name="frame"/> (SAN-107).</summary>
        public MoveDef SelfPosture(int frame, float fraction) { selfPosture.Add((frame, fraction)); return this; }
        /// <summary>Vaults over the current target from <paramref name="from"/> to <paramref name="to"/> (SAN-105; see <see cref="vaultFrom"/>).</summary>
        public MoveDef Vault(int from, int to, float past, float height, float room, float margin)
        {
            vaultFrom = from; vaultTo = to; vaultPast = past; vaultHeight = height; vaultRoom = room; vaultMargin = margin;
            return this;
        }
        /// <summary>Throws <paramref name="def"/> on <paramref name="frame"/> from <paramref name="from"/> (metres ahead, up) (SAN-105).</summary>
        public MoveDef Throw(int frame, ProjectileDef def, Vector2 from) { projectileFrame = frame; projectile = def; projectileFrom = from; return this; }
        /// <summary>Plays effect cue <paramref name="id"/> on <paramref name="frame"/> (SAN-105).</summary>
        public MoveDef Cue(int frame, string id) { cues.Add((frame, id)); return this; }
        /// <summary>A brace from <paramref name="from"/> to <paramref name="to"/> (SAN-106; see <see cref="braceFrom"/>).</summary>
        public MoveDef Brace(int from, int to, float trigger, MoveDef answer, System.Func<Fighter, Fighter, bool> when)
        {
            braceFrom = from; braceTo = to; braceTrigger = trigger; braceAnswer = answer; braceWhen = when;
            return this;
        }
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
