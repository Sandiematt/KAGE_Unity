using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Neutral, walk, run, dash, skid, jump, air, land, gravity, gait, stage clamp and facing.</summary>
    public partial class Fighter
    {
        // ---- locomotion tuning (m, m/s, m/s²; gravity values are Rigidbody2D gravity scales) ----
        // Kage's horizontal speeds. Every character moves at these × its speed multiplier (character data, SAN-66).
        // SpeedMul is applied last in each expression, so the constant part still folds at compile time and
        // a speed multiplier of 1 gives exactly Kage's old numbers.
        const float WalkFwd = 1.65f, WalkBack = 1.3f, RunSpeed = 4.6f;
        const float JumpX = 2.6f, AirMaxX = 2.8f;
        const float DashSpeed = 9.5f, BackDashSpeed = 7.5f, AirDashSpeed = 8.2f;
        const float GroundAccel = 16f, GroundDecel = 22f, RunAccel = 11f, SkidDecel = 15f;
        const float RunAfter = 0.42f;                    // seconds of forward walking before breaking into a run
        const float JumpV = 8.4f, AirJumpV = 7.2f, JumpCut = 0.55f;
        const float Gravity = 3.2f, RiseGravity = 2.9f, ApexGravity = 2.0f, FallGravity = 4.1f, MaxFall = 14f;
        const float AirAccel = 10f, AirDrag = 1.2f;
        const int DashFrames = 13, BackDashFrames = 15, AirDashFrames = 14, DashCooldown = 16, BackDashInvuln = 6;
        const float StageHalf = 7.6f;

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
            float runT = state == FState.Run ? KMath.Smooth01(Mathf.InverseLerp(WalkFwd * SpeedMul, RunSpeed * 0.9f * SpeedMul, vl)) : 0f;
            _runBlend = Mathf.MoveTowards(_runBlend, runT, 5f * dt);
            if (Mathf.Abs(vl) > 0.05f) _gaitDir = Mathf.Sign(vl);
            _gaitRate = PoseLib.GaitRate(Mathf.Abs(vl), _runBlend);
            _gaitPhase = Mathf.Repeat(_gaitPhase + _gaitRate * dt, 1f);
        }
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
            if (TryBuffered(inp, fwd)) return;

            bool rollF = facing > 0 ? inp.tapRight2 : inp.tapLeft2;
            bool rollB = facing > 0 ? inp.tapLeft2 : inp.tapRight2;
            if (TryDash(fwd, inp.dy)) return;
            if (rollF && inp.dy >= 0) { StartMove(moves.roll); return; }
            if (rollB && inp.dy >= 0) { StartMove(moves.backflip); return; }
            if ((inp.dy > 0 || _jumpBuf <= 5) && Grounded) { Jump(fwd); return; }
            if (GuardStance(inp)) return;         // Guard held: stand (or crouch) in guard; walking backward doesn't guard

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
                    float runV = RunSpeed * SpeedMul;
                    vl = Mathf.MoveTowards(vl, runV, (vl > runV ? GroundDecel : RunAccel) * dt);
                }
                else
                {
                    if (state != FState.Walk) SetState(FState.Walk);
                    float walkV = WalkFwd * SpeedMul;
                    vl = Mathf.MoveTowards(vl, walkV, (vl > walkV ? GroundDecel : GroundAccel) * dt);
                }
            }
            else
            {
                _walkHeld = 0f;
                // letting go of (or reversing out of) a full run plants the feet in a skid
                if (state == FState.Run && vl > RunSpeed * 0.55f * SpeedMul) { StartSkid(); return; }
                if (fwd < 0)
                {
                    if (state != FState.Walk) SetState(FState.Walk);
                    vl = Mathf.MoveTowards(vl, -WalkBack * SpeedMul, (vl > 0f ? GroundDecel * 1.3f : GroundAccel) * dt);
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
            float sp = (air ? AirDashSpeed : (dir > 0 ? DashSpeed : BackDashSpeed)) * SpeedMul;
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
            float sp0 = (_dashAir ? AirDashSpeed : (_dashDir > 0 ? DashSpeed : BackDashSpeed)) * SpeedMul;
            float spEnd = _dashAir ? 2.6f : (_dashDir > 0 ? (fwd > 0 ? RunSpeed * SpeedMul : 1.2f) : 0.5f);
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
                        var m = moves.Select(_buffer, 0, _bufferDy, true, this);     // Down + weapon in the air picks the dive (twin, chain)
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

        void Jump(int fwd)
        {
            _jumpDir = fwd;
            _jumpBuf = 99;
            // a running (or dashing) take-off keeps its momentum for a long jump
            float carry = rb.linearVelocity.x * facing;
            float vx = carry > (WalkFwd + 0.3f) * SpeedMul && fwd >= 0 ? Mathf.Clamp(carry, JumpX * SpeedMul, RunSpeed * 0.85f * SpeedMul) : fwd * JumpX * SpeedMul;
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
            if (dirIn != 0) vxl = dirIn * Mathf.Max(JumpX * 0.9f * SpeedMul, dirIn * vxl);   // redirect, never slower than a jump
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
                var m = moves.Select(_buffer, 0, _bufferDy, true, this);     // Down + weapon in the air picks the dive (twin, chain)
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
                float cap = Mathf.Max(AirMaxX * SpeedMul, dirIn * vxl > 0f ? Mathf.Abs(vxl) : 0f);
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
            _landRun = vl > (WalkFwd + 0.4f) * SpeedMul;
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
    }
}
