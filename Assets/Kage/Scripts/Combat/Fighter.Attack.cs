using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Input buffer, move selection, starting a move and per-frame move playback.</summary>
    public partial class Fighter
    {
        // ---- fractional move playback (ADR-0006) ----
        // The move time advances by the playback rate each frame; moveFrame is its whole part. The whole frames
        // "passed" this frame are (_movePrev, moveFrame]; with none passed (rate below 1) the current frame still counts
        // as touched, so windows and root motion stay on for as long as the move sits in them. At rate 1 exactly one
        // frame passes each frame and playback is the authored one.
        float _moveTime;
        int _movePrev, _moveTicks;

        /// <summary>The move's time in (fractional) frames.</summary>
        public float MoveTime => _moveTime;

        /// <summary>A one-frame trigger at <paramref name="frame"/> fires on the frame that passes it: once, never skipped.</summary>
        bool Passed(int frame) => frame > _movePrev && frame <= moveFrame;

        /// <summary>True when a whole frame touched this frame lies in <paramref name="from"/>..<paramref name="to"/>.</summary>
        bool Touches(int from, int to)
        {
            int lo = moveFrame > _movePrev ? _movePrev + 1 : moveFrame;
            return lo <= to && moveFrame >= from;
        }

        void Buffer(Btn b, FrameInput inp)
        {
            _buffer = b;
            _bufferAge = 0;
            _bufferDy = inp.dy;
            _bufferFwd = inp.dx == 0 ? 0 : (int)Mathf.Sign(inp.dx * facing);
        }

        void ConsumeBuffer() { _buffer = Btn.None; _bufferAge = 99; }

        bool TryBuffered(FrameInput inp, int fwd)
        {
            if (_bufferAge > 8 || _buffer == Btn.None) return false;
            int dy = inp.dy != 0 ? inp.dy : _bufferDy;
            int f = fwd != 0 ? fwd : _bufferFwd;
            if (_buffer == Btn.X && dy < 0 && TrySpecialDown()) return true;
            var m = moves.Select(_buffer, f, dy, !Grounded, this);
            if (m == null) return false;
            m = Armed(m);
            if (m == null) { ConsumeBuffer(); return false; }    // a sword move with the sword broken: the press does nothing
            if (m.meterCost > 0 && meter < m.meterCost) { ConsumeBuffer(); KageGame.I?.Hud?.Float(Pos + Vector2.up * 2.1f, "NO SHADOW", new Color(0.7f, 0.7f, 0.7f), 0.7f); return false; }
            ConsumeBuffer();
            StartMove(m);
            return true;
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
            _moveTime = 0f;
            _movePrev = 0;
            _moveTicks = 0;
            moveHit = false;
            _hitLog.Clear();
            _sweepPrev.Clear();
            ctx?.Events.Publish(CombatEvent.MoveStarted(ctx.Clock.Frame, this, m.id));
            rig.SetSwordFront(m.swordFront);
            rig.SnapFacing();
            if (Grounded && !m.airborne) rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.3f, rb.linearVelocity.y);
            KageGame.I?.Audio?.Play(m.swordFront ? SfxKind.SwordWhoosh : SfxKind.Whoosh, 0.35f);
        }

        void AttackUpdate(FrameInput inp, int fwd)
        {
            var m = move;
            if (m == null) { SetState(FState.Idle); return; }
            if (ContactFollowUp(m)) return;                       // a blow of a move with onContact landed: its follow-up plays (SAN-104)
            float rate = PlaybackRate;
            _movePrev = moveFrame;
            _moveTicks++;
            _moveTime += rate;
            moveFrame = Mathf.FloorToInt(_moveTime + 1e-4f);
            int f = moveFrame;

            // chains / cancels
            if (_bufferAge <= 10 && _buffer != Btn.None && f >= m.cancelFrom && m.chain.TryGetValue(_buffer, out var next) && (next = Armed(next)) != null)
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
                if (!Touches(e.from, e.to)) continue;
                var v = rb.linearVelocity;
                v.x = e.vel.x * facing * rate;                 // the same distance over the scaled frames
                if (e.setY && Passed(e.from)) v.y = e.vel.y;   // a jump impulse: unscaled, so the arc is the authored one
                rb.linearVelocity = v;
                driven = true;
            }
            if (RanIntoWall(m, driven)) return;                   // a charge into the wall it faces staggers (SAN-104)
            if (VaultStep(m)) driven = true;                      // an arc over the target (SAN-105)
            if (!driven && Grounded && !m.airborne)
                rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0f, 16f * Time.fixedDeltaTime), rb.linearVelocity.y);

            // one-shot fx
            foreach (var e in m.fx)
            {
                switch (e.kind)
                {
                    case FxKind.AfterImage:
                        for (int k = _movePrev + 1; k <= f; k++)
                            if (k >= e.from && k <= e.to && (k - e.from) % 2 == 0) Vfx.I?.Ghost(rig, fxMain, 0.28f);
                        break;
                    case FxKind.Dust: if (Passed(e.from)) Vfx.I?.Dust(Pos, 0.9f, -facing); break;
                    case FxKind.Shockwave: if (Passed(e.from)) { Vfx.I?.Shockwave(Pos + new Vector2(facing * 0.45f, 0.02f), 1.2f, fxMain); GameFeel.Shake(0.18f); } break;
                    case FxKind.Glint: if (Passed(e.from)) Vfx.I?.Glint(rig.World(B.Sword, def.bladeTipLocal * 0.6f), fxCore); break;
                    case FxKind.SlashLine: if (Passed(e.from)) Vfx.I?.SlashLine(Pos + Vector2.up * 1.05f - Vector2.right * facing * 1.2f, Pos + Vector2.up * 1.05f + Vector2.right * facing * 0.6f, fxMain, fxCore); break;
                    case FxKind.ChargeDark: if (Touches(e.from, e.to)) Vfx.I?.ChargeDark(rig.World(B.HandF, Vector2.zero), fxMain, fxDark); break;
                }
            }

            foreach (var c in m.cues) if (Passed(c.frame)) PlayMoveCue(c.cue);

            // hit windows: pose the rig at this exact logic frame before testing
            bool active = false;
            foreach (var w in m.hits) if (Touches(w.from, w.to)) active = true;
            if (active)
            {
                m.Evaluate(def, _moveTime, _start, _display);
                ApplyGrip(_display);
                rig.Apply(_display);
                foreach (var w in m.hits)
                    if (Touches(w.from, w.to)) WindowActive(w);
            }

            if (m.projectileFrame >= 0 && Passed(m.projectileFrame))
            {
                if (m.projectile != null) SpawnMoveProjectile(m);      // a thrown ability (Smoke Bomb, SAN-105)
                else if (IsKunaiThrow(m)) ThrowKunai();
                else SpawnCrescent();
            }
            if (m.hazard != null && Passed(m.hazardFrame)) SpawnMoveHazard(m);
            if (m.isThrow) ThrowUpdate();

            if (m.airborne && m.endsOnLanding && f > 4 && Grounded) { Landed(); return; }
            if (f >= m.total)
            {
                if (!Grounded) { _airFrame = 0; _jumpDir = 0; _airAttackUsed = true; SetState(FState.Air); }
                else SetState(FState.Idle);
            }
        }
    }
}
