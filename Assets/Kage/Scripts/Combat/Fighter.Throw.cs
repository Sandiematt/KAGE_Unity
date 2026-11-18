using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Grab, carry, release and being thrown.</summary>
    public partial class Fighter
    {
        // =====================================================================================
        //  THROWS
        // =====================================================================================
        void ThrowUpdate()
        {
            var m = move;
            float f = _moveTime;
            if (Passed(m.grabFrame))
            {
                bool ok = opponent != null && Mathf.Abs(opponent.Pos.x - Pos.x) < m.grabRange && opponent.CanBeThrown;
                if (!ok)
                {
                    move = m.whiff;     // the whiff carries on from the same move time
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
                Vector2 p = Pos + new Vector2(facing * Mathf.Lerp(m.carryFrom, m.carryTo, e), Mathf.Sin(e * Mathf.PI) * m.carryHeight + 0.02f);
                _victim.rb.MovePosition(p);
                _victim._thrownSpin = e * 200f;
            }
            if (Passed(m.releaseFrame)) ReleaseVictim(true);
        }

        void ReleaseVictim(bool slam)
        {
            var v = _victim;
            _victim = null;
            if (v == null) return;
            v._thrower = null;
            v.rb.bodyType = RigidbodyType2D.Dynamic;
            if (!slam) { v.SetState(FState.Idle); return; }
            int throwDmg = move != null ? move.throwDmg : 12;
            var info = new HitInfo
            {
                attacker = this,
                w = new HitWindow { dmg = throwDmg, level = HitLevel.Unblockable, knockdown = true, heavy = true, hitstop = 8, shake = 0.35f },
                dmg = DamageCalc.For(this, v, throwDmg, false, false),     // a body move: the weapon doesn't count
                kb = move != null ? new Vector2(facing * move.throwKb.x, move.throwKb.y) : new Vector2(-facing * 2.4f, -3f),
                zone = HitZone.Body,
                contact = v.Pos + Vector2.up * 0.4f,
                moveId = move != null ? move.id : "throw", window = -1,
            };
            v.state = FState.Idle;
            float before = v.health;
            v.health = Mathf.Max(0f, v.health - info.dmg);
            v.LastDamageTaken = before - v.health;
            v.rig.Flash(Color.white, 0.9f);
            if (v.health <= 0f) v.KnockOut(info);
            else { v.Launch(info); v._tumbleVel = Mathf.Abs(v._tumbleVel) * 0.5f; }
            v._bounces = -1;
            comboCount++;
            GameFeel.Hitstop(8);
            GameFeel.Shake(0.35f);
            Vfx.I?.HitBurst(info.contact, Vector2.down, 1.3f, false, fxMain, fxCore, fxDark);
            KageGame.I?.Audio?.Play(SfxKind.HitHeavy, 1f);
            PublishHit(v, info, HitResult.Hit);
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
    }
}
