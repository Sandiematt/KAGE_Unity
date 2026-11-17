using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    public enum FState { Intro, Idle, Walk, Crouch, Air, Land, Attack, Hitstun, Blockstun, Launched, Down, Thrown, KO, Victory, Ultimate, Run, Dash, Skid }
    public enum HitResult { Whiff, Hit, Blocked, Countered }

    public struct HitInfo
    {
        public Fighter attacker;
        public HitWindow w;
        public int dmg;
        public Vector2 kb;
        public HitZone zone;
        public Vector2 contact;
        public bool crit;
    }

    public interface IHittable
    {
        bool Overlap(Vector2 p, float r, HitWindow w, out HitZone zone, out Vector2 contact);
        HitResult ReceiveHit(HitInfo info);
    }

    /// <summary>
    /// A fighter: Rigidbody2D body (gravity, knockback, launches, bounces), a 60 Hz state machine,
    /// move playback with hit windows, blocking by holding back, throws, juggles, knockdowns,
    /// kip-ups, a physics ragdoll on K.O., and the Kage-only ultimate.
    /// </summary>
    public class Fighter : MonoBehaviour, IHittable
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
        public bool isPlayer, canUltimate;
        public Color fxMain = Palette.Trim, fxCore = Color.white, fxDark = Palette.TrimDeep;
        public bool controlEnabled;

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

        // ---- locomotion tuning (m, m/s, m/s²; gravity values are Rigidbody2D gravity scales) ----
        const float WalkFwd = 1.65f, WalkBack = 1.3f, RunSpeed = 4.6f;
        const float GroundAccel = 16f, GroundDecel = 22f, RunAccel = 11f, SkidDecel = 15f;
        const float RunAfter = 0.42f;                    // seconds of forward walking before breaking into a run
        const float JumpV = 8.4f, JumpX = 2.6f, AirJumpV = 7.2f, JumpCut = 0.55f;
        const float Gravity = 3.2f, RiseGravity = 2.9f, ApexGravity = 2.0f, FallGravity = 4.1f, MaxFall = 14f;
        const float AirAccel = 10f, AirMaxX = 2.8f, AirDrag = 1.2f;
        const float DashSpeed = 9.5f, BackDashSpeed = 7.5f, AirDashSpeed = 8.2f;
        const int DashFrames = 13, BackDashFrames = 15, AirDashFrames = 14, DashCooldown = 16, BackDashInvuln = 6;
        const float StageHalf = 7.6f;

        readonly Pose _start = new(), _display = new(), _target = new(), _walk = new(), _run = new();
        readonly Pose _airA = new(), _airB = new();
        static Pose _stance, _crouch, _block, _crouchBlock, _rise, _fall, _land, _hurtHigh, _hurtBody, _hurtLow, _tumble, _lie, _lieFront, _victory, _ref, _tuck;
        static Pose _apex, _airTuck, _fallLong, _preLand, _landDeep, _dashF, _dashB, _airDash, _skid;
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
        MotionTrail _trailBlade, _trailFist, _trailFootN, _trailFootF;
        Ragdoll2D _ragdoll;
        public int comboCount;
        public System.Action<Fighter, Fighter, HitInfo, HitResult> onHit;
        public System.Action<Fighter> onKO;

        static void InitPoses()
        {
            if (_stance != null) return;
            _stance = PoseLib.Stance(); _crouch = PoseLib.Crouch(); _block = PoseLib.Block(); _crouchBlock = PoseLib.CrouchBlock();
            _rise = PoseLib.JumpRise(); _fall = PoseLib.JumpFall(); _land = PoseLib.Land();
            _hurtHigh = PoseLib.HurtHigh(); _hurtBody = PoseLib.HurtBody(); _hurtLow = PoseLib.HurtLow();
            _tumble = PoseLib.Tumble(); _lie = PoseLib.Lie(); _lieFront = PoseLib.LieFront(); _tuck = PoseLib.JumpTuck(); _victory = PoseLib.Victory(); _ref = PoseLib.Ref();
            _apex = PoseLib.JumpApex(); _airTuck = PoseLib.AirTuck(); _fall = PoseLib.FallOpen(); _fallLong = PoseLib.FallLong();
            _preLand = PoseLib.PreLand(); _landDeep = PoseLib.LandDeep();
            _dashF = PoseLib.DashFwd(); _dashB = PoseLib.DashBack(); _airDash = PoseLib.AirDash(); _skid = PoseLib.Skid();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _stance = null; }

        // =====================================================================================
        public void Setup(string displayName, RigDef d, MoveSet set, int sortBase, Color main, Color core, Color dark)
        {
            InitPoses();
            fighterName = displayName;
            def = d;
            moves = set;
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
            body.sharedMaterial = KageGame.NoFriction;

            var vis = new GameObject("Visual");
            vis.transform.SetParent(transform, false);
            rig = vis.AddComponent<FighterRig>();
            rig.Build(d, sortBase);

            _trailBlade = new MotionTrail("TrailBlade", transform.parent, 0.12f, main, core, sortBase + 900, true);
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
            SetState(FState.Intro);
        }

        public void SetFacing(float f, bool instant = false)
        {
            facing = Mathf.Sign(f);
            rig.SetFacing(facing, instant);
        }

        public Vector2 Pos => rb != null ? rb.position : (Vector2)transform.position;
        public bool CanBeThrown => Grounded && (state == FState.Idle || state == FState.Walk || state == FState.Run || state == FState.Skid || state == FState.Crouch || state == FState.Blockstun || (state == FState.Attack && move != null && !move.isThrow && !move.Invulnerable(moveFrame)));
        public bool IsAttacking => state == FState.Attack && move != null;
        public bool Busy => state is FState.Attack or FState.Hitstun or FState.Blockstun or FState.Launched or FState.Down or FState.Thrown or FState.KO or FState.Ultimate;
        public bool IsDown => state is FState.Down or FState.KO;

        public void BeginFight() { SetState(FState.Idle); controlEnabled = true; }

        void SetState(FState s)
        {
            if (s != FState.Attack) { move = null; rig.SetSwordFront(false); }
            _start.CopyFrom(_display);
            _start.Normalize();
            state = s;
            stateFrame = 0;
        }

        // =====================================================================================
        void Update()
        {
            if (controlEnabled && input != null) input.Sample();
        }

        void FixedUpdate()
        {
            if (def == null || moves == null || rig == null || rig.def == null) return;   // mid hot-reload: KageGame rebuilds the scene
            if (state == FState.KO) return;
            var inp = (controlEnabled && input != null) ? input.Poll(this) : default;
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
            _guardHeld = fwd < 0 && controlEnabled;
            _crouchHeld = inp.dy < 0;

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
            if (!Grounded && rb.bodyType == RigidbodyType2D.Dynamic) _lastVy = rb.linearVelocity.y;
            ClampStage();
        }

        /// <summary>
        /// Jump arcs use asymmetric gravity: a lighter rise, a brief hang at the apex and a heavier fall,
        /// so jumps read as snappy instead of floaty. Hit reactions keep the base gravity so juggles
        /// stay as tuned. Fall speed is capped everywhere.
        /// </summary>
        void UpdateGravity()
        {
            if (rb.bodyType != RigidbodyType2D.Dynamic || !rb.simulated) return;
            Vector2 v = rb.linearVelocity;
            float g = Gravity;
            bool jumpArc = state == FState.Air || (state == FState.Attack && move != null && move.endsOnLanding && _fromJump);
            if (state == FState.Dash && _dashAir) g = _dashFrame < _dashTotal - 4 ? 0f : Gravity * 0.6f;
            else if (jumpArc)
            {
                if (Mathf.Abs(v.y) < 1.3f) g = ApexGravity;
                else g = v.y < 0f ? FallGravity : RiseGravity;
            }
            rb.gravityScale = g;
            if (v.y < -MaxFall) rb.linearVelocity = new Vector2(v.x, -MaxFall);
        }

        /// <summary>Advances the walk/run cycle by distance covered, so planted feet match the ground speed.</summary>
        void UpdateGait()
        {
            float dt = Time.fixedDeltaTime;
            float vl = rb.linearVelocity.x * facing;
            float runT = state == FState.Run ? KMath.Smooth01(Mathf.InverseLerp(WalkFwd, RunSpeed * 0.9f, vl)) : 0f;
            _runBlend = Mathf.MoveTowards(_runBlend, runT, 5f * dt);
            if (Mathf.Abs(vl) > 0.05f) _gaitDir = Mathf.Sign(vl);
            _gaitRate = PoseLib.GaitRate(Mathf.Abs(vl), _runBlend);
            _gaitPhase = Mathf.Repeat(_gaitPhase + _gaitRate * dt, 1f);
        }

        void Buffer(Btn b, FrameInput inp)
        {
            _buffer = b;
            _bufferAge = 0;
            _bufferDy = inp.dy;
            _bufferFwd = inp.dx == 0 ? 0 : (int)Mathf.Sign(inp.dx * facing);
        }

        void ConsumeBuffer() { _buffer = Btn.None; _bufferAge = 99; }

        void ClampStage()
        {
            var p = rb.position;
            if (p.x < -StageHalf || p.x > StageHalf)
            {
                p.x = Mathf.Clamp(p.x, -StageHalf, StageHalf);
                rb.position = p;
                var v = rb.linearVelocity;
                if ((p.x <= -StageHalf && v.x < 0) || (p.x >= StageHalf && v.x > 0)) rb.linearVelocity = new Vector2(0f, v.y);
            }
        }

        public bool AtWall => Mathf.Abs(rb.position.x) >= StageHalf - 0.05f;

        void FaceOpponent()
        {
            if (opponent == null || opponent.state == FState.Thrown) return;
            float dx = opponent.Pos.x - Pos.x;
            if (Mathf.Abs(dx) > 0.06f && Mathf.Sign(dx) != facing) SetFacing(Mathf.Sign(dx));
        }

        // =====================================================================================
        void Neutral(FrameInput inp, int fwd)
        {
            FaceOpponent();
            fwd = inp.dx == 0 ? 0 : (int)Mathf.Sign(inp.dx * facing);
            _guardHeld = fwd < 0 && controlEnabled;
            if (TryBuffered(inp, fwd)) return;

            bool rollF = facing > 0 ? inp.tapRight2 : inp.tapLeft2;
            bool rollB = facing > 0 ? inp.tapLeft2 : inp.tapRight2;
            if (TryDash(fwd, inp.dy)) return;
            if (rollF && inp.dy >= 0) { StartMove(moves.roll); return; }
            if (rollB && inp.dy >= 0) { StartMove(moves.backflip); return; }
            if ((inp.dy > 0 || _jumpBuf <= 5) && Grounded) { Jump(fwd); return; }

            float dt = Time.fixedDeltaTime;
            Vector2 v = rb.linearVelocity;
            float vl = v.x * facing;                     // ground speed along facing
            if (inp.dy < 0)
            {
                _walkHeld = 0f;
                if (state != FState.Crouch) SetState(FState.Crouch);
                vl = Mathf.MoveTowards(vl, 0f, GroundDecel * dt);
            }
            else if (fwd > 0)
            {
                _walkHeld += dt;
                float ahead = opponent != null ? (opponent.Pos.x - Pos.x) * facing : 99f;
                bool crowded = ahead > 0f && ahead < 0.95f && !opponent.IsDown;
                bool run = !crowded && (state == FState.Run || _walkHeld >= RunAfter || inp.dashHeld);
                if (run)
                {
                    if (state != FState.Run) SetState(FState.Run);
                    vl = Mathf.MoveTowards(vl, RunSpeed, (vl > RunSpeed ? GroundDecel : RunAccel) * dt);
                }
                else
                {
                    if (state != FState.Walk) SetState(FState.Walk);
                    vl = Mathf.MoveTowards(vl, WalkFwd, (vl > WalkFwd ? GroundDecel : GroundAccel) * dt);
                }
            }
            else
            {
                _walkHeld = 0f;
                // letting go of (or reversing out of) a full run plants the feet in a skid
                if (state == FState.Run && vl > RunSpeed * 0.55f) { StartSkid(); return; }
                if (fwd < 0)
                {
                    if (state != FState.Walk) SetState(FState.Walk);
                    vl = Mathf.MoveTowards(vl, -WalkBack, (vl > 0f ? GroundDecel * 1.3f : GroundAccel) * dt);
                }
                else
                {
                    vl = Mathf.MoveTowards(vl, 0f, GroundDecel * dt);
                    // keep stepping while still drifting so the feet don't skate to a stop
                    if (Mathf.Abs(vl) > 0.12f && state is FState.Walk or FState.Run) { if (state == FState.Run) SetState(FState.Walk); }
                    else if (state != FState.Idle) SetState(FState.Idle);
                }
            }
            v.x = vl * facing;
            rb.linearVelocity = v;
        }

        bool TryDash(int fwd, int dy)
        {
            if (_dashBuf > 4 || _dashCool > 0 || dy < 0) return false;
            _dashBuf = 99;
            StartDash(fwd < 0 ? -1 : 1, !Grounded);
            return true;
        }

        void StartDash(int dir, bool air)
        {
            _dashDir = dir;
            _dashAir = air;
            _dashFrame = 0;
            _dashTotal = air ? AirDashFrames : (dir > 0 ? DashFrames : BackDashFrames);
            _dashCool = DashCooldown + _dashTotal;
            if (air) _airDashUsed = true;
            _walkHeld = 0f;
            SetState(FState.Dash);
            rig.SnapFacing();
            rig.Squash(-0.08f);
            float sp = air ? AirDashSpeed : (dir > 0 ? DashSpeed : BackDashSpeed);
            rb.linearVelocity = new Vector2(dir * sp * facing, air ? 0f : Mathf.Min(0f, rb.linearVelocity.y));
            Vfx.I?.Dust(Pos + (air ? Vector2.up * 0.6f : Vector2.zero), air ? 0.5f : 0.9f, -dir * facing);
            Vfx.I?.Ghost(rig, fxMain, 0.3f);
            KageGame.I?.Audio?.Play(SfxKind.Whoosh, 0.4f);
        }

        void DashUpdate(FrameInput inp, int fwd)
        {
            _dashFrame++;
            int f = _dashFrame;
            float u = f / (float)_dashTotal;
            float sp0 = _dashAir ? AirDashSpeed : (_dashDir > 0 ? DashSpeed : BackDashSpeed);
            float spEnd = _dashAir ? 2.6f : (_dashDir > 0 ? (fwd > 0 ? RunSpeed : 1.2f) : 0.5f);
            // burst, hold, then bleed off (InQuad keeps the speed up early)
            float sp = Mathf.Lerp(sp0, spEnd, KMath.Apply(Ease.InQuad, u));
            Vector2 v = rb.linearVelocity;
            v.x = _dashDir * sp * facing;
            if (_dashAir && f < _dashTotal - 4) v.y = 0f;
            rb.linearVelocity = v;
            if (f % 3 == 0 && f < _dashTotal - 2) Vfx.I?.Ghost(rig, fxMain, 0.22f);

            // cancels: attacks out of the dash, dash-jump keeps the momentum
            if (f >= 4)
            {
                if (_dashAir)
                {
                    if (!_airAttackUsed && _bufferAge <= 6 && _buffer != Btn.None && _buffer != Btn.X)
                    {
                        var m = moves.Select(_buffer, 0, 0, true, this);
                        if (m != null) { ConsumeBuffer(); _airAttackUsed = true; StartMove(m); _fromJump = true; return; }
                    }
                }
                else if (TryBuffered(inp, fwd)) return;
            }
            if (!_dashAir && f >= 3 && _jumpBuf <= 3 && Grounded) { Jump(fwd); return; }
            if (_dashAir && f > 3 && Grounded) { Landed(); return; }

            if (f >= _dashTotal)
            {
                if (_dashAir || !Grounded) { _airFrame = 10; _jumpDir = 0; SetState(FState.Air); }
                else if (_dashDir > 0 && fwd > 0) { _walkHeld = RunAfter; SetState(FState.Run); }
                else SetState(FState.Idle);
            }
        }

        void StartSkid()
        {
            SetState(FState.Skid);
            rig.Squash(0.05f);
            Vfx.I?.Dust(Pos + Vector2.right * facing * 0.3f, 0.7f, facing);
        }

        void SkidUpdate(FrameInput inp, int fwd)
        {
            if (TryBuffered(inp, fwd)) return;
            if (stateFrame >= 3 && TryDash(fwd, inp.dy)) return;
            if (_jumpBuf <= 5 && Grounded) { Jump(fwd); return; }
            Vector2 v = rb.linearVelocity;
            v.x = Mathf.MoveTowards(v.x, 0f, SkidDecel * Time.fixedDeltaTime);
            rb.linearVelocity = v;
            if (stateFrame % 4 == 1) Vfx.I?.Dust(Pos + Vector2.right * facing * 0.35f, 0.4f, facing);
            if (Mathf.Abs(v.x) < 0.25f || stateFrame > 26) SetState(FState.Idle);
        }

        bool TryBuffered(FrameInput inp, int fwd)
        {
            if (_bufferAge > 8 || _buffer == Btn.None) return false;
            int dy = inp.dy != 0 ? inp.dy : _bufferDy;
            int f = fwd != 0 ? fwd : _bufferFwd;
            if (_buffer == Btn.X && dy < 0 && canUltimate && meter >= 100f && Grounded)
            {
                ConsumeBuffer();
                StartUltimate();
                return true;
            }
            var m = moves.Select(_buffer, f, dy, !Grounded, this);
            if (m == null) return false;
            if (m.meterCost > 0 && meter < m.meterCost) { ConsumeBuffer(); KageGame.I?.Hud?.Float(Pos + Vector2.up * 2.1f, "NO SHADOW", new Color(0.7f, 0.7f, 0.7f), 0.7f); return false; }
            ConsumeBuffer();
            StartMove(m);
            return true;
        }

        void Jump(int fwd)
        {
            _jumpDir = fwd;
            _jumpBuf = 99;
            // a running (or dashing) take-off keeps its momentum for a long jump
            float carry = rb.linearVelocity.x * facing;
            float vx = carry > WalkFwd + 0.3f && fwd >= 0 ? Mathf.Clamp(carry, JumpX, RunSpeed * 0.85f) : fwd * JumpX;
            rb.linearVelocity = new Vector2(vx * facing, JumpV);
            _airFrame = 0;
            _airJumpFrame = 99;
            _airAttackUsed = _airJumpUsed = _airDashUsed = _jumpCut = false;
            _walkHeld = 0f;
            SetState(FState.Air);
            rig.Squash(-0.1f);
            Vfx.I?.Dust(Pos, 0.6f, -Mathf.Sign(vx) * facing);
        }

        /// <summary>Second jump in the air: straight up-kick with a tuck, no somersault.</summary>
        void AirJump(int dirIn)
        {
            _airJumpUsed = true;
            _jumpCut = false;
            _jumpBuf = 99;
            _airJumpFrame = 0;
            if (dirIn != 0) _jumpDir = dirIn;
            float vxl = rb.linearVelocity.x * facing;
            if (dirIn != 0) vxl = dirIn * Mathf.Max(JumpX * 0.9f, dirIn * vxl);   // redirect, never slower than a jump
            else vxl *= 0.6f;
            rb.linearVelocity = new Vector2(vxl * facing, AirJumpV);
            rig.Squash(-0.09f);
            Vfx.I?.Dust(Pos + Vector2.up * 0.05f, 0.45f, 0f);
            KageGame.I?.Audio?.Play(SfxKind.Whoosh, 0.3f);
        }

        void AirUpdate(FrameInput inp)
        {
            _airFrame++;
            _airJumpFrame++;
            int dirIn = inp.dx == 0 ? 0 : (int)Mathf.Sign(inp.dx * facing);
            if (!_airAttackUsed && _bufferAge <= 6 && _buffer != Btn.None && _buffer != Btn.X)
            {
                var m = moves.Select(_buffer, 0, 0, true, this);
                if (m != null) { ConsumeBuffer(); _airAttackUsed = true; StartMove(m); _fromJump = true; return; }
            }
            if (!_airDashUsed && _airFrame > 3 && _dashBuf <= 4)
            {
                _dashBuf = 99;
                StartDash(dirIn < 0 ? -1 : 1, true);
                return;
            }
            if (!_airJumpUsed && _airFrame > 3 && _jumpBuf <= 3) { AirJump(dirIn); return; }

            float dt = Time.fixedDeltaTime;
            Vector2 v = rb.linearVelocity;
            // variable height: releasing jump while still rising cuts the arc (player only; the AI taps up)
            if (isPlayer && !_jumpCut && inp.dy <= 0 && v.y > 2f && _airFrame > 3)
            {
                _jumpCut = true;
                v.y *= JumpCut;
            }
            // air control: steer towards the held direction, never faster than the take-off momentum
            float vxl = v.x * facing;
            if (dirIn != 0)
            {
                float cap = Mathf.Max(AirMaxX, dirIn * vxl > 0f ? Mathf.Abs(vxl) : 0f);
                vxl = Mathf.MoveTowards(vxl, dirIn * cap, AirAccel * dt);
            }
            else vxl = Mathf.MoveTowards(vxl, 0f, AirDrag * dt);
            v.x = vxl * facing;
            rb.linearVelocity = v;

            if (stateFrame > 3 && Grounded) Landed();
        }

        void Landed()
        {
            float impact = Mathf.Abs(_lastVy);
            _landHard = Mathf.InverseLerp(5f, 13f, impact);
            _landFrames = Mathf.RoundToInt(Mathf.Lerp(4f, 9f, _landHard));
            float vl = rb.linearVelocity.x * facing;
            _landRun = vl > WalkFwd + 0.4f;
            SetState(FState.Land);
            // keep some momentum: a running landing rolls on, a standing one sticks
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * (_landRun ? 0.75f : 0.4f), 0f);
            _airJumpUsed = _airDashUsed = false;
            rig.Squash(Mathf.Lerp(0.08f, 0.17f, _landHard));
            Vfx.I?.Dust(Pos, Mathf.Lerp(0.6f, 1.1f, _landHard), 0f);
            if (_landHard > 0.5f) GameFeel.Shake(0.04f * _landHard);
            KageGame.I?.Audio?.Play(SfxKind.Land, Mathf.Lerp(0.35f, 0.65f, _landHard));
        }

        void LandUpdate(FrameInput inp, int fwd)
        {
            Vector2 v = rb.linearVelocity;
            float decel = (_landRun && fwd > 0 ? 4f : GroundDecel) * Time.fixedDeltaTime;
            rb.linearVelocity = new Vector2(Mathf.MoveTowards(v.x, 0f, decel), v.y);
            if (TryBuffered(inp, fwd)) return;
            if (stateFrame >= 2)
            {
                if (TryDash(fwd, inp.dy)) return;
                if (_jumpBuf <= 5 && Grounded) { Jump(fwd); return; }
            }
            if (stateFrame >= _landFrames)
            {
                if (_landRun && fwd > 0) { _walkHeld = RunAfter; SetState(FState.Run); }
                else SetState(FState.Idle);
            }
        }

        // =====================================================================================
        public void StartMove(MoveDef m)
        {
            if (m == null) return;
            meter = Mathf.Max(0f, meter - m.meterCost);
            _fromJump = false;
            _walkHeld = 0f;
            SetState(FState.Attack);
            move = m;
            moveFrame = 0;
            moveHit = false;
            _hitLog.Clear();
            rig.SetSwordFront(m.swordFront);
            rig.SnapFacing();
            if (Grounded && !m.airborne) rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.3f, rb.linearVelocity.y);
            KageGame.I?.Audio?.Play(m.swordFront ? SfxKind.SwordWhoosh : SfxKind.Whoosh, 0.35f);
        }

        void AttackUpdate(FrameInput inp, int fwd)
        {
            var m = move;
            if (m == null) { SetState(FState.Idle); return; }
            moveFrame++;
            int f = moveFrame;

            // chains / cancels
            if (_bufferAge <= 10 && _buffer != Btn.None && f >= m.cancelFrom && m.chain.TryGetValue(_buffer, out var next))
            {
                ConsumeBuffer();
                StartMove(next);
                return;
            }

            // launchers: after a hit, jump cancels the recovery so the launch can be chased
            if (moveHit && m.jumpCancelFrom >= 0 && f >= m.jumpCancelFrom && Grounded && (_jumpBuf <= 6 || inp.dy > 0))
            {
                Jump(fwd);
                return;
            }

            // root motion
            bool driven = false;
            foreach (var e in m.motions)
            {
                if (f < e.from || f > e.to) continue;
                var v = rb.linearVelocity;
                v.x = e.vel.x * facing;
                if (e.setY && f == e.from) v.y = e.vel.y;
                rb.linearVelocity = v;
                driven = true;
            }
            if (!driven && Grounded && !m.airborne)
                rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 16f * Time.fixedDeltaTime), rb.linearVelocity.y);

            // one-shot fx
            foreach (var e in m.fx)
            {
                switch (e.kind)
                {
                    case FxKind.AfterImage:
                        if (f >= e.from && f <= e.to && (f - e.from) % 2 == 0) Vfx.I?.Ghost(rig, fxMain, 0.28f);
                        break;
                    case FxKind.Dust: if (f == e.from) Vfx.I?.Dust(Pos, 0.9f, -facing); break;
                    case FxKind.Shockwave: if (f == e.from) { Vfx.I?.Shockwave(Pos + new Vector2(facing * 0.45f, 0.02f), 1.2f, fxMain); GameFeel.Shake(0.18f); } break;
                    case FxKind.Glint: if (f == e.from) Vfx.I?.Glint(rig.World(B.Sword, def.bladeTipLocal * 0.6f), fxCore); break;
                    case FxKind.SlashLine: if (f == e.from) Vfx.I?.SlashLine(Pos + Vector2.up * 1.05f - Vector2.right * facing * 1.2f, Pos + Vector2.up * 1.05f + Vector2.right * facing * 0.6f, fxMain, fxCore); break;
                    case FxKind.ChargeDark: if (f >= e.from && f <= e.to) Vfx.I?.ChargeDark(rig.World(B.HandF, Vector2.zero), fxMain, fxDark); break;
                }
            }

            // hit windows: pose the rig at this exact logic frame before testing
            bool active = false;
            foreach (var w in m.hits) if (f >= w.from && f <= w.to) active = true;
            if (active)
            {
                m.Evaluate(def, f, _start, _display);
                rig.Apply(_display);
                foreach (var w in m.hits)
                    if (f >= w.from && f <= w.to) CheckHits(w);
            }

            if (f == m.projectileFrame) SpawnCrescent();
            if (m.isThrow) ThrowUpdate(f);

            if (m.airborne && m.endsOnLanding && f > 4 && Grounded) { Landed(); return; }
            if (f >= m.total)
            {
                if (!Grounded) { _airFrame = 0; _jumpDir = 0; _airAttackUsed = true; SetState(FState.Air); }
                else SetState(FState.Idle);
            }
        }

        // =====================================================================================
        //  HIT DETECTION
        // =====================================================================================
        readonly List<Vector2> _pts = new();

        void HitPoints(HitPoint hp, List<Vector2> into)
        {
            switch (hp)
            {
                case HitPoint.Fist: into.Add(rig.World(B.HandN, def.fistLocal)); break;
                case HitPoint.Hilt: into.Add(rig.World(B.HandF, def.hiltLocal)); break;
                case HitPoint.FootN: into.Add(rig.World(B.FootN, def.footTipLocal)); into.Add(rig.World(B.FootN, def.heelLocal)); break;
                case HitPoint.FootF: into.Add(rig.World(B.FootF, def.footTipLocal)); into.Add(rig.World(B.FootF, def.heelLocal)); break;
                case HitPoint.ShinN: into.Add(rig.World(B.LegNL, new Vector2(0f, -0.1f))); break;
                case HitPoint.ShinF: into.Add(rig.World(B.LegFL, new Vector2(0f, -0.1f))); break;
                case HitPoint.KneeN: into.Add(rig.World(B.LegNL, Vector2.zero)); break;
                case HitPoint.Blade:
                    for (int i = 1; i <= 5; i++) into.Add(rig.World(B.Sword, Vector2.Lerp(def.bladeBaseLocal, def.bladeTipLocal, i / 5f)));
                    break;
            }
        }

        void CheckHits(HitWindow w)
        {
            _pts.Clear();
            foreach (var hp in w.points) HitPoints(hp, _pts);
            TryTarget(opponent, w);
            TryTarget(extraTarget, w);
        }

        void TryTarget(IHittable t, HitWindow w)
        {
            if (t == null || ReferenceEquals(t, this)) return;
            foreach (var (hw, ht) in _hitLog) if (hw == w && ReferenceEquals(ht, t)) return;
            foreach (var p in _pts)
            {
                if (!t.Overlap(p, w.radius, w, out var zone, out var contact)) continue;
                _hitLog.Add((w, t));
                int dmg = w.dmg;
                bool crit = Random.value < 0.1f;
                if (crit) dmg = Mathf.RoundToInt(dmg * 1.5f);
                var info = new HitInfo
                {
                    attacker = this, w = w, dmg = dmg, kb = new Vector2(w.kb.x * facing, w.kb.y), zone = zone, contact = contact, crit = crit
                };
                var res = t.ReceiveHit(info);
                OnHitResolved(t, info, res);
                return;
            }
        }

        public void ResolveExternal(IHittable t, HitInfo info, HitResult res) => OnHitResolved(t, info, res);

        void OnHitResolved(IHittable t, HitInfo info, HitResult res)
        {
            var w = info.w;
            Vector2 dir = new Vector2(facing, 0.15f).normalized;
            if (res == HitResult.Hit)
            {
                moveHit = true;
                meter = Mathf.Min(100f, meter + 5f + info.dmg * 0.6f);
                comboCount++;
                GameFeel.Hitstop(w.hitstop + (info.crit ? 3 : 0));
                GameFeel.Shake(w.shake * (info.crit ? 1.5f : 1f));
                if (w.heavy || info.crit) GameFeel.Punch(w.heavy ? 0.12f : 0.07f);
                Vfx.I?.HitBurst(info.contact, dir, w.heavy ? 1.35f : 0.9f + info.dmg * 0.03f, w.sword, fxMain, fxCore, fxDark);
                KageGame.I?.Audio?.Play(w.sword ? SfxKind.Slash : (w.heavy ? SfxKind.HitHeavy : SfxKind.Hit), 0.8f);
            }
            else if (res == HitResult.Blocked)
            {
                GameFeel.Hitstop(3);
                GameFeel.Shake(0.05f);
                Vfx.I?.BlockBurst(info.contact, -dir);
                KageGame.I?.Audio?.Play(SfxKind.Block, 0.7f);
                if (opponent != null && opponent.AtWall)
                    rb.linearVelocity = new Vector2(-facing * 1.6f, rb.linearVelocity.y);
            }
            if (t is Fighter f) onHit?.Invoke(this, f, info, res);
            else KageGame.I?.OnPropHit(this, info, res);
        }

        // ---- IHittable ----
        bool HittableBy(HitWindow w)
        {
            if (_hidden) return false;
            switch (state)
            {
                case FState.KO: case FState.Thrown: case FState.Intro: case FState.Victory: case FState.Ultimate: return false;
                case FState.Down: return w.otg && _otgHits < 2;
                case FState.Attack: return move == null || !move.Invulnerable(moveFrame);
                case FState.Dash: return _dashAir || _dashDir > 0 || _dashFrame > BackDashInvuln;
            }
            return true;
        }

        public bool Overlap(Vector2 p, float r, HitWindow w, out HitZone zone, out Vector2 contact)
        {
            zone = HitZone.Body;
            contact = p;
            if (!HittableBy(w)) return false;
            foreach (var h in def.hurt)
            {
                Vector2 c = rig.World(h.bone, h.local);
                float rr = r + h.radius;
                if ((c - p).sqrMagnitude <= rr * rr)
                {
                    zone = h.zone;
                    contact = Vector2.Lerp(p, c, r / rr);
                    return true;
                }
            }
            return false;
        }

        public HitResult ReceiveHit(HitInfo info)
        {
            var w = info.w;
            // Mirror Guard: parry and teleport-slash behind the attacker
            if (state == FState.Attack && move != null && move.counterFrom >= 0 && moveFrame >= move.counterFrom && moveFrame <= move.counterTo && w.level != HitLevel.Unblockable)
            {
                TriggerCounter(info.attacker);
                return HitResult.Countered;
            }

            bool canBlock = (state is FState.Idle or FState.Walk or FState.Crouch or FState.Blockstun) && Grounded && _guardHeld;
            if (canBlock && w.level != HitLevel.Unblockable)
            {
                bool ok = w.level switch { HitLevel.Low => _crouchHeld, HitLevel.Overhead => !_crouchHeld, _ => true };
                if (ok)
                {
                    SetState(FState.Blockstun);
                    _stun = Mathf.Max(8, Mathf.RoundToInt(w.stun * 0.6f));
                    rb.linearVelocity = new Vector2(info.kb.x * 0.55f + Mathf.Sign(info.kb.x) * 0.8f, rb.linearVelocity.y);
                    health = Mathf.Max(1f, health - (w.sword ? Mathf.Ceil(info.dmg * 0.15f) : 0f));
                    rig.Flash(new Color(0.85f, 0.9f, 1f), 0.35f);
                    return HitResult.Blocked;
                }
            }

            if (state == FState.Down)
            {
                // ground hit: damage and a jolt, but no re-launch (prevents infinite down-loops)
                _otgHits++;
                health = Mathf.Max(0f, health - info.dmg);
                rig.Flash(Color.white, 0.8f, 14f);
                rb.linearVelocity = new Vector2(info.kb.x * 0.25f, 0f);
                if (health <= 0f) KnockOut(info);
                return HitResult.Hit;
            }

            health = Mathf.Max(0f, health - info.dmg);
            meter = Mathf.Min(100f, meter + 3f + info.dmg * 0.3f);
            rig.Flash(Color.white, 0.95f, 14f);
            if (_victim != null) ReleaseVictim(false);

            if (health <= 0f) { KnockOut(info); return HitResult.Hit; }

            if (state == FState.Launched || !Grounded || w.launch || w.knockdown) Launch(info);
            else
            {
                SetState(FState.Hitstun);
                rig.Squash(w.heavy ? 0.09f : 0.05f);
                _stun = w.stun;
                _hurt = w.level == HitLevel.Low || info.zone == HitZone.Legs ? _hurtLow : info.zone == HitZone.Head ? _hurtHigh : _hurtBody;
                rb.linearVelocity = new Vector2(info.kb.x, Mathf.Max(0f, info.kb.y * 0.3f));
            }
            return HitResult.Hit;
        }

        void Launch(HitInfo info)
        {
            _juggle++;
            float jm = Mathf.Pow(0.8f, _juggle - 1);
            Vector2 v = info.kb;
            if (v.y > 0f) v.y *= jm;
            if (Grounded && v.y < 2.5f) v.y = info.w.knockdown && info.kb.y < 0 ? 2.2f : 2.8f;
            if (!Grounded && v.y < 1.5f && info.kb.y >= 0f) v.y = Mathf.Max(v.y, 1.5f * jm);
            SetState(FState.Launched);
            _bounces = info.kb.y < 0f ? -1 : 0;     // spiked: guaranteed ground bounce
            rb.linearVelocity = v;
            _lastVy = v.y;
            // tumble: knocked backwards -> rotate onto the back, knocked forwards -> onto the front
            bool backwards = v.x * facing <= 0f;
            _tumbleAng = KMath.Wrap(_display.a[(int)B.Hips]);
            float speed = Mathf.Clamp(170f + Mathf.Abs(v.x) * 55f + Mathf.Max(0f, v.y) * 22f, 170f, 460f);
            _tumbleVel = backwards ? speed : -speed;
            rig.Squash(-0.06f);
        }

        void LaunchedUpdate()
        {
            _tumbleAng += _tumbleVel * Time.fixedDeltaTime;
            if (stateFrame > 3 && Grounded)
            {
                float impact = Mathf.Abs(_lastVy);
                if (_bounces < 1 && (impact > 4.2f || _bounces < 0))
                {
                    _bounces = 1;
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.5f, Mathf.Clamp(impact * 0.38f, 2.2f, 4.2f));
                    _lastVy = rb.linearVelocity.y;
                    Vfx.I?.Dust(Pos, 1.2f, 0f);
                    Vfx.I?.Shockwave(Pos + Vector2.up * 0.03f, 0.8f, new Color(0.6f, 0.55f, 0.5f));
                    GameFeel.Shake(0.15f);
                    KageGame.I?.Audio?.Play(SfxKind.Thud, 0.8f);
                    _tumbleVel *= 0.55f;
                    rig.Squash(0.16f);
                    stateFrame = 0;
                }
                else
                {
                    // settle onto whichever side the tumble is closer to
                    float settle = KMath.Wrap(_tumbleAng);
                    _liePose = Mathf.Abs(Mathf.DeltaAngle(settle, 90f)) <= Mathf.Abs(Mathf.DeltaAngle(settle, -90f)) ? _lie : _lieFront;
                    rig.Squash(0.1f);
                    SetState(FState.Down);
                    _otgHits = 0;
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.3f, 0f);
                    Vfx.I?.Dust(Pos, 1.0f, 0f);
                    KageGame.I?.Audio?.Play(SfxKind.Thud, 0.5f);
                }
            }
        }

        void KnockOut(HitInfo info)
        {
            SetState(FState.KO);
            controlEnabled = false;
            Vector2 impulse = new Vector2(info.kb.x * 1.2f + Mathf.Sign(info.kb.x) * 1.8f, Mathf.Clamp(info.kb.y, 3.0f, 6f));
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
            _ragdoll = Ragdoll2D.Activate(rig, impulse, info.contact);
            Vfx.I?.KoBurst(info.contact, fxMain, info.attacker != null ? info.attacker.fxMain : fxMain);
            GameFeel.SlowMo(0.25f, 0.9f);
            GameFeel.Shake(0.5f);
            GameFeel.Punch(0.25f);
            KageGame.I?.Audio?.Play(SfxKind.KO, 1f);
            onKO?.Invoke(this);
        }

        // =====================================================================================
        //  THROWS
        // =====================================================================================
        void ThrowUpdate(int f)
        {
            var m = move;
            if (f == m.grabFrame)
            {
                bool ok = opponent != null && Mathf.Abs(opponent.Pos.x - Pos.x) < 0.95f && opponent.CanBeThrown;
                if (!ok)
                {
                    var keep = f;
                    var whiff = m.whiff;
                    move = whiff;
                    moveFrame = keep;
                    return;
                }
                _victim = opponent;
                opponent.BeginThrown(this);
                KageGame.I?.Audio?.Play(SfxKind.Grab, 0.8f);
            }
            if (_victim == null) return;
            if (f > m.grabFrame && f < m.releaseFrame)
            {
                float u = (f - m.grabFrame) / (float)(m.releaseFrame - m.grabFrame);
                float e = KMath.Apply(Ease.InOutQuad, u);
                Vector2 p = Pos + new Vector2(facing * Mathf.Lerp(0.62f, -0.78f, e), Mathf.Sin(e * Mathf.PI) * 1.3f + 0.02f);
                _victim.rb.MovePosition(p);
                _victim._thrownSpin = e * 200f;
            }
            if (f == m.releaseFrame) ReleaseVictim(true);
        }

        void ReleaseVictim(bool slam)
        {
            var v = _victim;
            _victim = null;
            if (v == null) return;
            v._thrower = null;
            v.rb.bodyType = RigidbodyType2D.Dynamic;
            if (!slam) { v.SetState(FState.Idle); return; }
            var info = new HitInfo
            {
                attacker = this,
                w = new HitWindow { dmg = move != null ? move.throwDmg : 12, level = HitLevel.Unblockable, knockdown = true, heavy = true, hitstop = 8, shake = 0.35f },
                dmg = move != null ? move.throwDmg : 12,
                kb = new Vector2(-facing * 2.4f, -3f),
                zone = HitZone.Body,
                contact = v.Pos + Vector2.up * 0.4f,
            };
            v.state = FState.Idle;
            v.health = Mathf.Max(0f, v.health - info.dmg);
            v.rig.Flash(Color.white, 0.9f);
            if (v.health <= 0f) v.KnockOut(info);
            else { v.Launch(info); v._tumbleVel = Mathf.Abs(v._tumbleVel) * 0.5f; }
            v._bounces = -1;
            comboCount++;
            GameFeel.Hitstop(8);
            GameFeel.Shake(0.35f);
            Vfx.I?.HitBurst(info.contact, Vector2.down, 1.3f, false, fxMain, fxCore, fxDark);
            KageGame.I?.Audio?.Play(SfxKind.HitHeavy, 1f);
            onHit?.Invoke(this, v, info, HitResult.Hit);
        }

        public void BeginThrown(Fighter by)
        {
            if (move != null && move.isThrow && _victim != null) ReleaseVictim(false);
            SetState(FState.Thrown);
            _thrower = by;
            _thrownSpin = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rig.Flash(Color.white, 0.5f);
        }

        // =====================================================================================
        //  COUNTER + PROJECTILE + ULTIMATE
        // =====================================================================================
        void TriggerCounter(Fighter attacker)
        {
            if (attacker == null) return;
            Vfx.I?.Glint(rig.World(B.Sword, def.bladeTipLocal * 0.5f), Color.white);
            Vfx.I?.Ghost(rig, fxMain, 0.35f);
            GameFeel.Hitstop(10);
            GameFeel.Flash(new Color(1f, 1f, 1f, 0.5f), 0.12f);
            float side = -attacker.facing;
            float x = Mathf.Clamp(attacker.Pos.x + side * 0.75f, -StageHalf, StageHalf);
            rb.position = new Vector2(x, 0.02f);
            transform.position = new Vector3(x, 0.02f, 0f);
            SetFacing(Mathf.Sign(attacker.Pos.x - x), true);
            attacker.Stagger(24);
            StartMove(moves.counterSlash);
            KageGame.I?.Hud?.Float(Pos + Vector2.up * 2.2f, "COUNTER!", fxMain, 1.1f);
            KageGame.I?.Audio?.Play(SfxKind.Counter, 1f);
        }

        /// <summary>Freezes this fighter in a stagger (used by counters and the ultimate).</summary>
        public void Stagger(int frames)
        {
            if (state == FState.KO) return;
            if (_victim != null) ReleaseVictim(false);
            SetState(FState.Hitstun);
            _hurt = _hurtBody;
            _stun = frames;
            rb.linearVelocity = Vector2.zero;
        }

        void SpawnCrescent()
        {
            Vector2 p = rig.World(B.Sword, def.bladeTipLocal * 0.55f);
            p.y = Mathf.Max(p.y, 1.0f);
            Crescent.Spawn(this, new Vector2(p.x, 1.05f), facing);
            GameFeel.Shake(0.1f);
            KageGame.I?.Audio?.Play(SfxKind.Crescent, 0.9f);
        }

        int _ultFrame;
        Vector2 _ultAnchor;
        static readonly float[] UltSides = { 1f, -1f, 1f, -1f, 1f };
        static readonly float[] UltHeights = { 0f, 0.55f, 0f, 0.8f, 0.2f };

        void StartUltimate()
        {
            meter = 0f;
            SetState(FState.Ultimate);
            _ultFrame = 0;
            rb.linearVelocity = Vector2.zero;
            GameFeel.SlowMo(0.5f, 0.35f);
            GameFeel.Punch(0.18f);
            KageGame.I?.Hud?.Banner("THOUSAND SHADOWS", fxMain, 1.4f);
            KageGame.I?.Audio?.Play(SfxKind.Ultimate, 1f);
        }

        void UltimateUpdate()
        {
            _ultFrame++;
            int f = _ultFrame;
            if (opponent == null) { SetState(FState.Idle); return; }
            if (f <= 12)
            {
                Vfx.I?.ChargeDark(rig.World(B.Chest, Vector2.zero), fxMain, fxDark);
                if (f == 12)
                {
                    Vfx.I?.InkBurst(rig.World(B.Chest, Vector2.zero), fxDark, 1.4f);
                    _hidden = true;
                    rig.SetVisible(false);
                    rb.simulated = false;
                    opponent.Stagger(90);
                    _ultAnchor = opponent.Pos;
                }
                return;
            }
            int slash = (f - 16) / 9;
            int sf = (f - 16) % 9;
            if (f >= 16 && slash < 5 && sf == 0)
            {
                float side = UltSides[slash] * (opponent.facing);
                Vector2 p = _ultAnchor + new Vector2(side * 0.95f, UltHeights[slash]);
                Vector2 q = _ultAnchor + new Vector2(-side * 0.95f, 1.6f - UltHeights[slash]);
                Vfx.I?.SlashLine(p + Vector2.up * 0.6f, q - Vector2.up * 0.3f, fxMain, fxCore);
                Vfx.I?.HitBurst(opponent.Pos + Vector2.up * 1.0f, (q - p).normalized, 1.0f, true, fxMain, fxCore, fxDark);
                opponent.health = Mathf.Max(1f, opponent.health - 4f);
                opponent.rig.Flash(Color.white, 0.9f);
                opponent.Stagger(60);
                GameFeel.Shake(0.18f);
                GameFeel.Hitstop(3);
                KageGame.I?.Audio?.Play(SfxKind.Slash, 0.8f);
                comboCount++;
            }
            if (f == 66)
            {
                float x = Mathf.Clamp(_ultAnchor.x - opponent.facing * 1.1f, -StageHalf, StageHalf);
                rb.simulated = true;
                rb.position = new Vector2(x, 0.02f);
                transform.position = new Vector3(x, 0.02f, 0f);
                SetFacing(-Mathf.Sign(_ultAnchor.x - x), true);          // back turned to the opponent: the reference pose
                _hidden = false;
                rig.SetVisible(true);
                _display.CopyFrom(_ref);
                _start.CopyFrom(_ref);
                Vfx.I?.InkBurst(rig.World(B.Chest, Vector2.zero), fxDark, 1.0f);
            }
            if (f == 84)
            {
                Vfx.I?.SlashLine(_ultAnchor + new Vector2(-1.3f, 0.3f), _ultAnchor + new Vector2(1.3f, 1.8f), fxMain, Color.white);
                Vfx.I?.SlashLine(_ultAnchor + new Vector2(-1.3f, 1.8f), _ultAnchor + new Vector2(1.3f, 0.3f), fxMain, Color.white);
                GameFeel.Flash(new Color(1f, 0.25f, 0.25f, 0.45f), 0.25f);
                var info = new HitInfo
                {
                    attacker = this,
                    w = new HitWindow { dmg = 14, level = HitLevel.Unblockable, knockdown = true, heavy = true, hitstop = 14, shake = 0.6f, sword = true, stun = 30 },
                    dmg = 14, kb = new Vector2(-facing * 2.0f, 6f), zone = HitZone.Body, contact = opponent.Pos + Vector2.up * 1.0f,
                };
                opponent.state = FState.Idle;
                var res = opponent.ReceiveHit(info);
                OnHitResolved(opponent, info, res);
                comboCount++;
            }
            if (f >= 110) SetState(FState.Idle);
        }

        // =====================================================================================
        //  POSE OUTPUT
        // =====================================================================================
        void LateUpdate()
        {
            if (rig == null || def == null || rig.def == null || _stance == null) return;
            if (state == FState.KO) { rig.Secondary(Time.deltaTime, Vector2.zero); return; }
            float alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);
            float sf = stateFrame + alpha;
            switch (state)
            {
                case FState.Attack:
                    move?.Evaluate(def, moveFrame + alpha, _start, _display);
                    break;
                case FState.Idle:
                    _target.CopyFrom(_stance);
                    float br = Mathf.Sin(Time.time * 2.4f + (isPlayer ? 0f : 1.3f));
                    _target.hip.y += br * 0.007f;
                    _target.a[(int)B.Chest] += br * 1.2f;
                    _target.a[(int)B.Head] -= br * 0.8f;
                    _target.MarkDirty();
                    BlendTo(_target, sf, 8f);
                    break;
                case FState.Walk:
                case FState.Run:
                    GaitPose(alpha);
                    BlendTo(_target, sf, state == FState.Run ? 6f : 4f);
                    break;
                case FState.Dash:
                    BlendTo(_dashAir ? _airDash : (_dashDir > 0 ? _dashF : _dashB), sf, 3f);
                    break;
                case FState.Skid: BlendTo(_skid, sf, 4f); break;
                case FState.Crouch: BlendTo(_guardHeld ? _crouchBlock : _crouch, sf, 5f); break;
                case FState.Land:
                    Pose.Blend(def, _land, _landDeep, _landHard, _target);
                    BlendTo(_target, sf, 2.5f);
                    break;
                case FState.Blockstun: BlendTo(_crouchHeld ? _crouchBlock : _block, sf, 2f); break;
                case FState.Hitstun:
                    if (_stun > 6) BlendTo(_hurt ?? _hurtBody, sf, 3f);
                    else { Pose.Blend(def, _hurt ?? _hurtBody, _stance, 1f - _stun / 6f, _display); }
                    break;
                case FState.Air: AirPose(sf); break;
                case FState.Launched:
                {
                    _target.CopyFrom(_tumble);
                    _target.a[(int)B.Hips] = _tumbleAng + _tumbleVel * alpha * Time.fixedDeltaTime;
                    _target.MarkDirty();
                    if (sf < 4f)
                    {
                        // blend limbs in, but never blend the spin itself (no unwinding)
                        _spinTmp.CopyFrom(_start);
                        _spinTmp.a[(int)B.Hips] = _target.a[(int)B.Hips];
                        _spinTmp.MarkDirty();
                        Pose.Blend(def, _spinTmp, _target, KMath.Apply(Ease.OutQuad, sf / 4f), _display);
                    }
                    else _display.CopyFrom(_target);
                    break;
                }
                case FState.Down: BlendTo(_liePose ?? _lie, sf, 7f); break;
                case FState.Thrown:
                    _target.CopyFrom(PoseLib.Thrown(_thrownSpin));
                    BlendTo(_target, sf, 3f);
                    break;
                case FState.Victory: BlendTo(_victory, sf, 24f); break;
                case FState.Intro: _display.CopyFrom(_ref); break;
                case FState.Ultimate:
                    if (_ultFrame < 66) BlendTo(_crouch, sf, 6f);
                    else _display.CopyFrom(_ref);
                    break;
            }
            rig.Apply(_display);
            rig.Secondary(Time.deltaTime, rb.linearVelocity);
            UpdateTrails();
        }

        void BlendTo(Pose target, float sf, float frames)
        {
            float t = KMath.Apply(Ease.OutQuad, sf / frames);
            Pose.Blend(def, _start, target, t, _display);
        }

        /// <summary>Walk and run cycles share one phase, blended by how far into a run the fighter is.</summary>
        void GaitPose(float alpha)
        {
            float ph = _gaitPhase + _gaitRate * alpha * Time.fixedDeltaTime;
            PoseLib.WalkCycle(ph, _gaitDir, _walk);
            if (_runBlend > 0.005f)
            {
                PoseLib.RunCycle(ph, _run);
                Pose.Blend(def, _walk, _run, _runBlend, _target);
            }
            else _target.CopyFrom(_walk);
        }

        /// <summary>
        /// Velocity-driven air pose (no somersault): rise → knees up at the apex → legs open and arms
        /// lift as the fall builds → a long-fall reach → feet reaching for the floor just before touchdown.
        /// The body leans into its horizontal travel and a second jump kicks in a quick tuck.
        /// </summary>
        void AirPose(float sf)
        {
            Vector2 vel = rb.linearVelocity;
            float vy = vel.y, vxl = vel.x * facing;
            float t = Time.time;

            float kApex = KMath.Smooth01(Mathf.InverseLerp(6f, 0.6f, vy));
            Pose.Blend(def, _rise, _apex, kApex, _airA);
            float kFall = KMath.Smooth01(Mathf.InverseLerp(0.4f, -5f, vy));
            Pose.Blend(def, _airA, _fall, kFall, _airB);
            float kLong = KMath.Smooth01(Mathf.InverseLerp(-7f, -12.5f, vy));
            Pose.Blend(def, _airB, _fallLong, kLong, _airA);

            // second jump: a quick tuck that opens back out
            if (_airJumpFrame < 18)
            {
                float k = Mathf.Sin(Mathf.PI * Mathf.Clamp01((_airJumpFrame + sf - stateFrame) / 18f));
                Pose.Blend(def, _airA, _airTuck, k * 0.9f, _airB);
                _airA.CopyFrom(_airB);
            }

            // anticipate the floor: time to impact from height and fall speed
            float tGround = vy < -0.5f ? Mathf.Max(0f, rb.position.y) / -vy : 9f;
            float kLand = KMath.Smooth01(Mathf.InverseLerp(0.16f, 0.04f, tGround));
            Pose.Blend(def, _airA, _preLand, kLand, _target);

            // balance flail while falling, fading as the feet reach for the floor
            float flail = kFall * (1f - kLand);
            if (flail > 0.01f)
            {
                _target.target[(int)Limb.ArmN] += new Vector2(Mathf.Sin(t * 8.3f) * 0.035f, Mathf.Cos(t * 6.1f) * 0.03f) * flail;
                _target.target[(int)Limb.ArmF] += new Vector2(Mathf.Sin(t * 7.1f + 1.3f) * 0.03f, Mathf.Cos(t * 9.2f) * 0.025f) * flail;
                _target.a[(int)B.Head] += Mathf.Sin(t * 5.3f) * 2.5f * flail;
            }
            // lean into the travel; rising pitches slightly back, falling slightly forward
            _target.MarkDirty();
            _target.SpinAbout(def, Mathf.Clamp(-vxl * 2.6f, -10f, 10f) + Mathf.Clamp(vy * 0.35f, -3f, 3f) * (1f - kLand));
            BlendTo(_target, sf, 4f);
        }

        void UpdateTrails()
        {
            bool blade = false, fist = false, footN = false, footF = false;
            if (state == FState.Attack && move != null)
            {
                foreach (var e in move.fx)
                {
                    if (moveFrame < e.from || moveFrame > e.to) continue;
                    switch (e.kind)
                    {
                        case FxKind.TrailBlade: blade = true; break;
                        case FxKind.TrailFist: fist = true; break;
                        case FxKind.TrailFootN: footN = true; break;
                        case FxKind.TrailFootF: footF = true; break;
                    }
                }
            }
            _trailBlade.Tick(blade, rig.World(B.Sword, Vector2.Lerp(def.bladeBaseLocal, def.bladeTipLocal, 0.38f)), rig.World(B.Sword, def.bladeTipLocal * 1.04f));
            Vector2 fistC = rig.World(B.HandN, def.fistLocal);
            _trailFist.Tick(fist, fistC + Vector2.up * 0.03f, fistC - Vector2.up * 0.03f);
            _trailFootN.Tick(footN, rig.World(B.FootN, def.footTipLocal), rig.World(B.LegNL, new Vector2(0f, -0.12f)));
            _trailFootF.Tick(footF, rig.World(B.FootF, def.footTipLocal), rig.World(B.LegFL, new Vector2(0f, -0.12f)));
        }

        public void Win()
        {
            controlEnabled = false;
            if (state != FState.KO) SetState(FState.Victory);
        }

        public void SetSortBase(int order) => rig.SetBaseOrder(order);

        void OnDestroy()
        {
            _trailBlade?.Dispose(); _trailFist?.Dispose(); _trailFootN?.Dispose(); _trailFootF?.Dispose();
        }
    }
}
