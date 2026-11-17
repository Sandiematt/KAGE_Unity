using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Being hit: hittability, overlap, damage, launch, knockdown, KO and stagger.</summary>
    public partial class Fighter
    {
        // ---- IHittable ----
        public bool HittableBy(HitWindow w)
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

        /// <summary>
        /// Defence step 2 (ADR-0004): runs <see cref="PreHitCheck"/> on a contact, before the hittable
        /// check. True when a check took the hit; <paramref name="c"/> then holds the result.
        /// </summary>
        public bool PreHit(ref DefenceCheck c)
        {
            PreHitCheck(ref c);
            return c.resolved;
        }

        /// <summary>Declared by SAN-65; implemented by SAN-92 (Fighter.PerfectDodge.cs).</summary>
        partial void PreHitCheck(ref DefenceCheck c);

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

        // ---- knockback (SAN-68) ----
        /// <summary>
        /// Authored knockback (<see cref="HitWindow"/>.kb, <see cref="HitInfo"/>.kb) is the velocity a hit
        /// gives a fighter of this mass (Kage's): the hit's impulse is kb x ReferenceMass.
        /// </summary>
        public const float ReferenceMass = 1f;

        /// <summary>The velocity a knockback gives this fighter: its impulse over this fighter's mass. The legacy switch ignores mass.</summary>
        Vector2 KnockVelocity(Vector2 kb) => LegacyPhysics ? kb : kb * (ReferenceMass / rb.mass);

        /// <summary>
        /// Gives the body velocity <paramref name="v"/> through a physics impulse. The hit replaces the
        /// current motion, as before: the body is stopped first. Under the legacy switch the velocity is set.
        /// </summary>
        void Knock(Vector2 v)
        {
            if (LegacyPhysics || rb.bodyType != RigidbodyType2D.Dynamic) { rb.linearVelocity = v; return; }
            rb.linearVelocity = Vector2.zero;
            rb.AddForce(v * rb.mass, ForceMode2D.Impulse);
        }

        public HitResult ReceiveHit(HitInfo info)
        {
            float before = health;
            var res = ReceiveHitCore(info);
            LastDamageTaken = before - health;
            return res;
        }

        HitResult ReceiveHitCore(HitInfo info)
        {
            var w = info.w;
            // Mirror Guard: parry and teleport-slash behind the attacker
            if (state == FState.Attack && move != null && move.counterFrom >= 0 && moveFrame >= move.counterFrom && moveFrame <= move.counterTo && w.level != HitLevel.Unblockable)
            {
                TriggerCounter(info.attacker);
                return HitResult.Countered;
            }

            if (TryGuard(info, out var guarded)) return guarded;

            if (state == FState.Down)
            {
                // ground hit: damage and a jolt, but no re-launch (prevents infinite down-loops)
                _otgHits++;
                health = Mathf.Max(0f, health - info.dmg);
                rig.Flash(Color.white, 0.8f, 14f);
                Knock(KnockVelocity(new Vector2(info.kb.x * 0.25f, 0f)));
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
                Knock(KnockVelocity(new Vector2(info.kb.x, Mathf.Max(0f, info.kb.y * 0.3f))));
            }
            return HitResult.Hit;
        }

        void Launch(HitInfo info)
        {
            _juggle++;
            float jm = Mathf.Pow(0.8f, _juggle - 1);
            // the launch impulse over this fighter's mass; the minimum pops below keep every launch off the ground
            Vector2 v = KnockVelocity(info.kb);
            if (v.y > 0f) v.y *= jm;
            if (Grounded && v.y < 2.5f) v.y = info.w.knockdown && info.kb.y < 0 ? 2.2f : 2.8f;
            if (!Grounded && v.y < 1.5f && info.kb.y >= 0f) v.y = Mathf.Max(v.y, 1.5f * jm);
            SetState(FState.Launched);
            _bounces = info.kb.y < 0f ? -1 : 0;     // spiked: guaranteed ground bounce
            Knock(v);
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
            SetHurtboxesActive(false);      // the ragdoll bodies take the bones; the hurt circles stay out of them
            _ragdoll = Ragdoll2D.Activate(rig, impulse, info.contact);
            Vfx.I?.KoBurst(info.contact, fxMain, info.attacker != null ? info.attacker.fxMain : fxMain);
            GameFeel.SlowMo(0.25f, 0.9f);
            GameFeel.Shake(0.5f);
            GameFeel.Punch(0.25f);
            KageGame.I?.Audio?.Play(SfxKind.KO, 1f);
            ctx?.Events.Publish(CombatEvent.KnockedOut(ctx.Clock.Frame, info.attacker, this, info.moveId, info.contact, impulse));
            onKO?.Invoke(this);
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
    }
}
