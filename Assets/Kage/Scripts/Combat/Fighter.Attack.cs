using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Input buffer, move selection, starting a move and per-frame move playback.</summary>
    public partial class Fighter
    {
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
                    if (f >= w.from && f <= w.to) WindowActive(w);
            }

            if (f == m.projectileFrame) { if (IsKunaiThrow(m)) ThrowKunai(); else SpawnCrescent(); }
            if (m.isThrow) ThrowUpdate(f);

            if (m.airborne && m.endsOnLanding && f > 4 && Grounded) { Landed(); return; }
            if (f >= m.total)
            {
                if (!Grounded) { _airFrame = 0; _jumpDir = 0; _airAttackUsed = true; SetState(FState.Air); }
                else SetState(FState.Idle);
            }
        }
    }
}
