using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    public enum FState { Intro, Idle, Walk, Crouch, Air, Land, Attack, Hitstun, Blockstun, Launched, Down, Thrown, KO, Victory, Ultimate, Run, Dash, Skid }
    /// <summary>What one hit did. <c>Parried</c> (SAN-91) and <c>Dodged</c> (a perfect dodge, SAN-92) are declared by SAN-86 (ADR-0004).</summary>
    public enum HitResult { Whiff, Hit, Blocked, Countered, Parried, Dodged }

    public struct HitInfo
    {
        public Fighter attacker;
        public HitWindow w;
        public int dmg;
        public Vector2 kb;
        public HitZone zone;
        public Vector2 contact;
        public bool crit;
        /// <summary>Move, projectile or rage-art id for the event stream; window = index in the move's hits, -1 when not a window.</summary>
        public string moveId;
        public int window;
        /// <summary>What delivered the hit: a fighter's melee window (the default), a projectile or a hazard. The parry reads it (SAN-91).</summary>
        public HitOrigin origin;
    }

    /// <summary>Where a <see cref="HitInfo"/> came from (SAN-91): only a melee window staggers its owner when parried.</summary>
    public enum HitOrigin { Melee, Projectile, Hazard }

    /// <summary>Something hits can land on: a fighter or a prop. Its hurt circles are <see cref="Hurtbox"/>es.</summary>
    public interface IHittable
    {
        /// <summary>False when this window can't touch it now (invulnerable, thrown, KO, ...): the contact doesn't count.</summary>
        bool HittableBy(HitWindow w);
        /// <summary>The legacy point test (the legacy switch only).</summary>
        bool Overlap(Vector2 p, float r, HitWindow w, out HitZone zone, out Vector2 contact);
        HitResult ReceiveHit(HitInfo info);
    }

    /// <summary>
    /// A fighter: Rigidbody2D body (gravity, knockback, launches, bounces), a 60 Hz state machine,
    /// move playback with hit windows, the Guard button with posture and guard-break, throws, juggles, knockdowns,
    /// kip-ups, a physics ragdoll on K.O., and the Kage-only ultimate.
    /// </summary>
    public partial class Fighter : MonoBehaviour, IHittable
    {
        // ---- setup ----
        public string fighterName = "KAGE";
        [System.NonSerialized] public RigDef def;
        [System.NonSerialized] public MoveSet moves;
        public FighterRig rig;
        public Rigidbody2D rb;
        public BoxCollider2D body;
        public Fighter opponent;
        public IHittable extraTarget;
        public IInputSource input;
        public bool isPlayer;
        public Color fxMain = Palette.Trim, fxCore = Color.white, fxDark = Palette.TrimDeep;
        public bool controlEnabled;

        // ---- fight (ADR-0002) ----
        [System.NonSerialized] public FightContext ctx;
        /// <summary>0 = Kage's side, 1 = foes. Hits never land on a fighter of the same team.</summary>
        public int team;
        /// <summary>Character id, plus "#n" for the n-th copy ("kage", "ronin", "order_footman#2").</summary>
        [System.NonSerialized] public string FighterId, CharacterId;
        /// <summary>Health the last ReceiveHit removed (after crit and guard), for the event stream. Setting it clears the last hit's posture and crit (Fighter.Posture.cs).</summary>
        public float LastDamageTaken
        {
            get => _lastDamageTaken;
            private set { _lastDamageTaken = value; LastPostureTaken = 0f; LastHitCrit = false; LastHitDamage = 0; }
        }
        float _lastDamageTaken;

        // ---- character (ADR-0005) ----
        /// <summary>The character data this fighter was built from.</summary>
        [System.NonSerialized] public CharacterDef Character;
        /// <summary>The character's flags, changeable at run time (a boss phase adds or removes them). Read them with <see cref="Has"/>.</summary>
        public CharacterFlags Flags;
        /// <summary>Locomotion speed multiplier (walk, run, dash, jump distance). Not the move rate.</summary>
        public float SpeedMul = 1f;
        /// <summary>
        /// Damage dealt and damage reduction (<see cref="DamageCalc"/>), attack playback rate (× the weapon's speed,
        /// <see cref="PlaybackRate"/>), and the posture bar size (<see cref="Posture"/>, SAN-86).
        /// </summary>
        public float DamageMul = 1f, Defense, MoveRate = 1f, PostureMax = 100f;
        /// <summary>Extra multiplier on knockback received, on top of mass.</summary>
        public float KnockbackTaken = 1f;

        public bool Has(CharacterFlags f) => f != CharacterFlags.None && (Flags & f) == f;

        // ---- stats ----
        public float maxHealth = 100f, health = 100f, meter;
        public int roundsWon;

        // ---- state ----
        public FState state = FState.Idle;
        public int stateFrame;
        public float facing = 1f;
        [System.NonSerialized] public MoveDef move;
        public int moveFrame;
        public bool moveHit;
        public bool Grounded { get; private set; }


        readonly Pose _start = new(), _display = new(), _target = new(), _walk = new(), _run = new();
        readonly Pose _airA = new(), _airB = new();
        Pose _hurt;
        readonly List<(HitWindow, IHittable)> _hitLog = new();
        Btn _buffer = Btn.None;
        int _bufferAge = 99, _bufferDy, _bufferFwd;
        bool _guardHeld, _crouchHeld;
        float _gaitPhase, _gaitRate, _gaitDir = 1f, _runBlend, _walkHeld;
        int _airFrame, _jumpDir, _airJumpFrame = 99;
        bool _airAttackUsed, _airJumpUsed, _airDashUsed, _jumpCut, _fromJump;
        int _prevDy, _jumpBuf = 99, _dashBuf = 99, _dashCool;
        int _dashFrame, _dashTotal, _dashDir;
        bool _dashAir;
        float _landHard;
        int _landFrames = 5;
        bool _landRun;
        int _stun, _juggle, _bounces, _otgHits;
        float _lastVy, _tumbleAng, _tumbleVel;
        Pose _liePose;
        readonly Pose _spinTmp = new();
        Fighter _thrower, _victim;
        float _thrownSpin;
        bool _hidden;
        MotionTrail _trailBlade, _trailBladeN, _trailFist, _trailFootN, _trailFootF;
        Ragdoll2D _ragdoll;
        public int comboCount;
        public System.Action<Fighter, Fighter, HitInfo, HitResult> onHit;
        public System.Action<Fighter> onKO;


        // =====================================================================================
        /// <summary>
        /// Builds this fighter from character data (ADR-0005): <paramref name="d"/> is the rig its part set drew.
        /// Health is jsHealth × healthMul × the health scale. Body scale is the caller's (FightBuilder.Spawn), and so
        /// is the weapon (<see cref="EquipWeapon"/>), which sets the move set.
        /// </summary>
        public void Setup(CharacterDef c, RigDef d, int sortBase)
        {
            Character = c;
            Setup(c.displayName, d, sortBase, c.colors.main, c.colors.core, c.colors.dark);
            Mass = c.mass;
            isPlayer = c.IsPlayer;
            Flags = c.flags;
            SpeedMul = c.speedMul;
            DamageMul = c.damageMul;
            Defense = c.defense;
            MoveRate = c.moveRate;
            PostureMax = c.postureMax;
            KnockbackTaken = c.knockbackTaken;
            ApplyGear(c.perks);
            maxHealth = health = c.MaxHealth();
        }

        void Setup(string displayName, RigDef d, int sortBase, Color main, Color core, Color dark)
        {
            InitPoses();
            fighterName = displayName;
            def = d;
            fxMain = main; fxCore = core; fxDark = dark;
            gameObject.layer = KageGame.LayerFighter;

            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = Gravity;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.mass = 1f;
            body = gameObject.AddComponent<BoxCollider2D>();
            body.size = new Vector2(0.36f, 1.5f);
            body.offset = new Vector2(0f, 0.75f);
            body.sharedMaterial = PhysicsMaterials.NoFriction;
            BuildPushCollider();

            var vis = new GameObject("Visual");
            vis.transform.SetParent(transform, false);
            rig = vis.AddComponent<FighterRig>();
            rig.Build(d, sortBase);
            BuildHurtboxes();

            _trailBlade = new MotionTrail("TrailBlade", transform.parent, 0.12f, main, core, sortBase + 900, true);
            _trailBladeN = new MotionTrail("TrailBladeN", transform.parent, 0.12f, main, core, sortBase + 900, true);
            _trailFist = new MotionTrail("TrailFist", transform.parent, 0.09f, Color.Lerp(main, Color.white, 0.6f), core, sortBase + 900, false);
            _trailFootN = new MotionTrail("TrailFootN", transform.parent, 0.1f, Color.Lerp(main, Color.white, 0.6f), core, sortBase + 900, false);
            _trailFootF = new MotionTrail("TrailFootF", transform.parent, 0.1f, Color.Lerp(main, Color.white, 0.6f), core, sortBase + 900, false);

            _display.CopyFrom(_ref);
            _start.CopyFrom(_ref);
            rig.Apply(_display);
        }

        public void ResetForRound(float x, float face)
        {
            if (_ragdoll != null) { _ragdoll.Restore(); _ragdoll = null; }
            SetHurtboxesActive(true);
            if (_thrower != null || _victim != null) { _thrower = null; _victim = null; }
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.simulated = true;
            rb.position = new Vector2(x, 0.02f);
            transform.position = new Vector3(x, 0.02f, 0f);
            rb.linearVelocity = Vector2.zero;
            rb.gravityScale = Gravity;
            _runBlend = 0f; _walkHeld = 0f; _dashCool = 0; _jumpBuf = _dashBuf = 99; _prevDy = 0;
            _airJumpUsed = _airDashUsed = false;
            health = maxHealth;
            Phases?.Reset();                    // a boss starts again from its first phase (SAN-102)
            SetFacing(face, true);
            move = null;
            comboCount = 0;
            _juggle = 0;
            _hidden = false;
            rig.SetVisible(true);
            rig.SetAlpha(1f);
            rig.SetSwordFront(false);
            _display.CopyFrom(_ref);
            _start.CopyFrom(_ref);
            rig.Apply(_display);
            if (rig.scarf != null) rig.scarf.ResetChain();
            rig.ResetSoft();
            _liePose = _lie;
            ResetPosture();
            SetState(FState.Intro);
        }

        public void SetFacing(float f, bool instant = false)
        {
            facing = Mathf.Sign(f);
            rig.SetFacing(facing, instant);
        }

        public Vector2 Pos => rb != null ? rb.position : (Vector2)transform.position;
        /// <summary>The physics mass (Kage = 1). Knockback impulses are divided by it, so heavy fighters move less.</summary>
        public float Mass { get => rb.mass; set => rb.mass = value; }
        /// <summary>The legacy switch (combat.json): the old manual push and knockback that ignores mass.</summary>
        bool LegacyPhysics => ctx != null && ctx.LegacyHits;
        public bool CanBeThrown => Grounded && (state == FState.Idle || state == FState.Walk || state == FState.Run || state == FState.Skid || state == FState.Crouch || state == FState.Blockstun || (state == FState.Attack && move != null && !move.isThrow && !move.Invulnerable(moveFrame)));
        public bool IsAttacking => state == FState.Attack && move != null;
        public bool Busy => state is FState.Attack or FState.Hitstun or FState.Blockstun or FState.Launched or FState.Down or FState.Thrown or FState.KO or FState.Ultimate;
        public bool IsDown => state is FState.Down or FState.KO;

        public void BeginFight() { SetState(FState.Idle); controlEnabled = true; }

        void SetState(FState s)
        {
            if (_cmActive && s != FState.Ultimate) AbortRageArt();     // the art ended early: the world and the camera come back (SAN-82)
            if (s != FState.Launched && s != FState.Down && s != FState.KO) EndPhysicalFall();
            if (s != FState.Attack) { move = null; rig.SetSwordFront(false); }
            _start.CopyFrom(_display);
            _start.Normalize();
            state = s;
            stateFrame = 0;
            _stateEntries++;        // a new dash or dodge move can perfect-dodge again (Fighter.PerfectDodge.cs)
        }

        // =====================================================================================
        bool Headless => ctx != null && ctx.Clock.Manual;
        bool Ready => def != null && moves != null && rig != null && rig.def != null;   // false mid hot-reload: KageGame rebuilds the scene
        FrameInput _inp;

        void Update()
        {
            if (Headless) return;
            if (controlEnabled && input != null) input.Sample();
        }

        /// <summary>Fight loop phase 2: reads this frame's input. Every fighter polls before any fighter ticks.</summary>
        public void PollInput()
        {
            _inp = default;
            if (!Ready || state == FState.KO) return;
            if (controlEnabled && input != null) _inp = input.Poll(this);
            HoldBackIfWaiting(ref _inp);     // only engaged foes attack (SAN-83, Fighter.Targets.cs)
        }

        /// <summary>Fight loop phase 3: one frame of the state machine, root motion and move playback.</summary>
        public void Tick()
        {
            if (!Ready) return;
            if (state == FState.KO) return;
            var inp = _inp;
            _inp = default;
            stateFrame++;
            _bufferAge++;
            if (inp.p) Buffer(Btn.P, inp);
            if (inp.k) Buffer(Btn.K, inp);
            if (inp.s) Buffer(Btn.S, inp);
            if (inp.x) Buffer(Btn.X, inp);
            // jump: an explicit press, or up going from released to held (AI / stick)
            _jumpBuf++;
            _dashBuf++;
            if (inp.jump || (inp.dy > 0 && _prevDy <= 0)) _jumpBuf = 0;
            if (inp.dash) _dashBuf = 0;
            _prevDy = inp.dy;
            if (_dashCool > 0) _dashCool--;

            Vector2 v = rb.linearVelocity;
            Grounded = rb.position.y < 0.045f && v.y <= 0.1f && rb.bodyType == RigidbodyType2D.Dynamic;
            int fwd = Mathf.RoundToInt(Mathf.Sign(inp.dx) * (inp.dx != 0 ? 1 : 0) * facing);
            ReadGuard(inp);
            _crouchHeld = inp.dy < 0;
            PostureTick();

            switch (state)
            {
                case FState.Intro: rb.linearVelocity = new Vector2(0f, v.y); break;
                case FState.Idle:
                case FState.Walk:
                case FState.Run:
                case FState.Crouch: Neutral(inp, fwd); break;
                case FState.Skid: SkidUpdate(inp, fwd); break;
                case FState.Dash: DashUpdate(inp, fwd); break;
                case FState.Air: AirUpdate(inp); break;
                case FState.Land: LandUpdate(inp, fwd); break;
                case FState.Attack: AttackUpdate(inp, fwd); break;
                case FState.Hitstun:
                    rb.linearVelocity = new Vector2(Mathf.MoveTowards(v.x, 0f, 11f * Time.fixedDeltaTime), v.y);
                    if (--_stun <= 0) { _juggle = 0; SetState(FState.Idle); }
                    break;
                case FState.Blockstun:
                    rb.linearVelocity = new Vector2(Mathf.MoveTowards(v.x, 0f, 9f * Time.fixedDeltaTime), v.y);
                    if (--_stun <= 0) SetState(FState.Idle);
                    break;
                case FState.Launched: LaunchedUpdate(); break;
                case FState.Down:
                    rb.linearVelocity = new Vector2(Mathf.MoveTowards(v.x, 0f, 14f * Time.fixedDeltaTime), v.y);
                    if (health > 0 && (stateFrame > 52 || (stateFrame > 16 && inp.Any)))
                    {
                        _juggle = 0;
                        StartMove(_liePose == _lieFront ? moves.getUpFront : moves.getUp);
                    }
                    break;
                case FState.Thrown: break;
                case FState.Victory:
                case FState.Ultimate:
                    if (state == FState.Ultimate) UltimateUpdate();
                    else rb.linearVelocity = new Vector2(0f, v.y);
                    break;
            }

            if (state is FState.Idle or FState.Walk or FState.Run) UpdateGait();
            UpdateGravity();
            if (state is FState.Launched or FState.Down) DriveFall();
            if (!Grounded && rb.bodyType == RigidbodyType2D.Dynamic) _lastVy = rb.linearVelocity.y;
            ClampStage();
        }

        public void Win()
        {
            controlEnabled = false;
            if (state != FState.KO) SetState(FState.Victory);
        }

        public void SetSortBase(int order) => rig.SetBaseOrder(order);

        void OnDestroy()
        {
            AbortRageArt();
            _trailBlade?.Dispose(); _trailBladeN?.Dispose(); _trailFist?.Dispose(); _trailFootN?.Dispose(); _trailFootF?.Dispose();
        }
    }
}
