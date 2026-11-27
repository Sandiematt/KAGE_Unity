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
                case FState.Attack: return move == null || !MoveInvulnerable();
                case FState.Dash: return DashHittable();
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

        /// <summary>Defence step 7 (ADR-0004): sets <paramref name="resistsFlinch"/> when armour takes this hit's flinch. Declared and implemented by SAN-80 (Fighter.Armour.cs).</summary>
        partial void ArmourCheck(in HitInfo info, ref bool resistsFlinch);

        public bool Overlap(Vector2 p, float r, HitWindow w, out HitZone zone, out Vector2 contact)
        {
            zone = HitZone.Body;
            contact = p;
            if (!HittableBy(w)) return false;
            foreach (var h in def.hurt)
            {
                Vector2 c = rig.World(h.bone, h.local);
                float rr = r + h.radius * _contactScale;
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

        /// <summary>
        /// The velocity a knockback gives this fighter: its impulse over this fighter's mass, times the character's
        /// knockback taken. The legacy switch ignores both.
        /// </summary>
        Vector2 KnockVelocity(Vector2 kb) => LegacyPhysics ? kb : kb * (ReferenceMass / rb.mass * KnockbackTaken);

        /// <summary>
        /// The soft parts' share of a blow (<see cref="FighterRig.Impulse"/>): the body snaps away with the knockback, so hair, cape,
        /// tassels and cloth are flung back towards the attacker and up, harder for a heavier blow. Held through hit-stop.
        /// </summary>
        void JoltSoftParts(HitInfo info, float scale)
        {
            if (rig == null) return;
            Vector2 kb = info.kb;
            float s = Mathf.Clamp(kb.magnitude, 1.5f, 9f) * scale;
            float away = Mathf.Abs(kb.x) > 0.01f ? Mathf.Sign(kb.x) : -facing;
            rig.Impulse(new Vector2(-away * s * 0.9f, s * 0.45f + Mathf.Max(0f, kb.y) * 0.2f));
        }

        /// <summary>A landing's jolt to the soft parts: they keep falling as the body stops, slap down and drape on the floor.</summary>
        void LandSoftParts(float impactSpeed, float scale = 1f)
        {
            if (rig == null || impactSpeed < 0.5f) return;
            rig.Impulse(new Vector2(0f, -Mathf.Min(impactSpeed * 0.5f * scale, 8f)), 1.4f);
        }

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

        /// <summary>
        /// Defence steps 4 to 7 for a hit that reached this fighter (ADR-0004): counter window, parry
        /// (<see cref="ParryCheck"/>), guard (<see cref="TryGuard"/>), then the hit itself: the guaranteed critical of a
        /// guard-break, damage, a little posture, armour, launch or hitstun, KO.
        /// </summary>
        public HitResult ReceiveHit(HitInfo info)
        {
            float before = health;
            _hitPosture = 0f;
            _hitCounter = false;
            _hitPunish = false;
            var res = ReceiveHitCore(ref info);
            LastDamageTaken = before - health;      // clears the posture and crit of the last hit, then:
            LastPostureTaken = _hitPosture;
            LastHitCrit = res == HitResult.Hit && info.crit;
            LastHitDamage = info.dmg;
            LastHitCounter = res == HitResult.Hit && _hitCounter;
            LastHitPunish = res == HitResult.Hit && _hitPunish;
            return res;
        }

        /// <summary>Set by step 7 when this hit is a whiff punish (Fighter.Flow.cs).</summary>
        bool _hitPunish;

        /// <summary>Set by step 7 when this hit is a counter hit (Fighter.Counter.cs).</summary>
        bool _hitCounter;

        HitResult ReceiveHitCore(ref HitInfo info)
        {
            var w = info.w;
            // step 4, the counter window. Mirror Guard: parry and teleport-slash behind the attacker; a counter stance (Censure): turn
            // the blow aside and answer it where it stands
            if (state == FState.Attack && move != null && move.counterFrom >= 0 && moveFrame >= move.counterFrom && moveFrame <= move.counterTo && w.level != HitLevel.Unblockable
                && (!move.counterFrontOnly || CounterCatches(info)) && (!move.counterLightOnly || !(w.heavy || w.breaksWeapon)))
            {
                if (move.counterAnswer != null) TriggerCounterAnswer(info, move.counterAnswer);
                else TriggerCounter(info.attacker);
                return HitResult.Countered;
            }

            if (TryParry(info, out var parried)) return parried;      // step 5 (SAN-91)
            if (TryGuard(info, out var guarded)) return guarded;      // step 6

            // step 7: the hit
            _hitOnBrokenGuard = GuardBroken;        // the armour check below reads it: the break is spent next (Fighter.Armour.cs)
            ApplyGuardBreakCrit(ref info);
            _hitCounter = ApplyCounterHit(ref info);     // the parrier's answer: more damage, stun and posture (Fighter.Counter.cs)
            _hitPunish = !_hitCounter && ApplyWhiffPunish(ref info);     // caught recovering from a move that touched nothing (Fighter.Flow.cs)
            if (state == FState.Down)
            {
                // ground hit: damage and a jolt, but no re-launch (prevents infinite down-loops)
                _otgHits++;
                health = Mathf.Max(0f, health - info.dmg);
                CleanHitPosture(info);
                rig.Flash(Color.white, 0.8f, 14f);
                JoltSoftParts(info, 0.5f);
                Knock(KnockVelocity(new Vector2(info.kb.x * 0.25f, 0f)));
                KickFall(info);
                if (health <= 0f) KnockOut(info);
                return HitResult.Hit;
            }

            health = Mathf.Max(0f, health - info.dmg);
            meter = Mathf.Min(100f, meter + (3f + info.dmg * 0.3f) * RageMul);
            CleanHitPosture(info, _hitCounter ? ParryT.counterPostureMul : 1f);
            rig.Flash(Color.white, 0.95f, 14f);
            JoltSoftParts(info, info.w.heavy || info.w.launch || info.w.knockdown || health <= 0f ? 1.3f : 1f);

            // armour (ADR-0004 step 7): the damage and posture land, the flinch doesn't, and a held victim stays held (Fighter.Armour.cs)
            bool resists = health > 0f && ResistsFlinch(info);
            if (_victim != null && !resists) ReleaseVictim(false);

            if (health <= 0f) { KnockOut(info); return HitResult.Hit; }
            NoteHitTaken();                         // the run of blows taken (Afterimage Feint, Fighter.Ascendant.cs)
            if (resists) { rig.Squash(0.03f); TryEscapeAbility(); return HitResult.Hit; }

            if (state == FState.Launched || !Grounded || w.launch || w.knockdown) Launch(info);
            else
            {
                TurnToBlow(info, true);               // a blow from behind: reel facing the blade, not into it (Fighter.Facing.cs)
                SetState(FState.Hitstun);
                rig.Squash(w.heavy ? 0.09f : 0.05f);
                _stun = ScaleStun(w.stun) + (_hitCounter ? ParryT.counterStunBonus : 0) + (_hitPunish ? FlowT.whiffPunishStunBonus : 0);
                _hurt = w.level == HitLevel.Low || info.zone == HitZone.Legs ? _hurtLow : info.zone == HitZone.Head ? _hurtHigh : _hurtBody;
                Knock(KnockVelocity(new Vector2(info.kb.x, Mathf.Max(0f, info.kb.y * 0.3f))));
                TryEscapeAbility();                   // Afterimage Feint: out of a long combo (Fighter.Ascendant.cs)
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
            bool falling = state == FState.Launched;     // a juggle: the body keeps the turn it has (the pose read below is stale mid-fall)
            SetState(FState.Launched);
            _bounces = info.kb.y < 0f ? -1 : 0;     // spiked: guaranteed ground bounce
            Knock(v);
            _lastVy = v.y;
            // tumble: knocked backwards -> turn onto the back, knocked forwards -> onto the front, and stop there (docs/combat-flow.md):
            // the body lies flat in the air rather than somersaulting
            bool backwards = v.x * facing <= 0f;
            if (!falling) _tumbleAng = KMath.Wrap(_display.a[(int)B.Hips]);
            var ft = FlowT;
            _tumbleTo = backwards ? ft.tumbleRestAngle : -ft.tumbleRestAngle;
            if (falling && Mathf.Abs(_tumbleAng) > 30f) _tumbleTo = Mathf.Sign(_tumbleAng) * ft.tumbleRestAngle;     // juggled: it stays on the side it already turned to
            float speed = Mathf.Clamp(170f + Mathf.Abs(v.x) * 55f + Mathf.Max(0f, v.y) * 22f, 170f, Mathf.Max(170f, ft.tumbleMaxSpeed));
            _tumbleVel = backwards ? speed : -speed;
            rig.Squash(-0.06f);
            StartPhysicalFall(v);
        }

        /// <summary>The angle the launched body turns to and holds (± the flow's rest angle: on its back or its front).</summary>
        float _tumbleTo;

        /// <summary>The launched body's turn about the hips now, degrees (0 upright, +90 on its back, -90 on its front).</summary>
        public float TumbleAngle => _tumbleAng;

        /// <summary>One frame of the launched turn: towards the rest angle at the tumble's speed, never past it.</summary>
        void TumbleStep() => _tumbleAng = Mathf.MoveTowards(_tumbleAng, _tumbleTo, Mathf.Abs(_tumbleVel) * Time.fixedDeltaTime);

        void LaunchedUpdate()
        {
            TumbleStep();
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
                    LandSoftParts(impact);
                    stateFrame = 0;
                }
                else
                {
                    // settle onto whichever side the tumble is closer to
                    float settle = KMath.Wrap(_tumbleAng);
                    _liePose = Mathf.Abs(Mathf.DeltaAngle(settle, 90f)) <= Mathf.Abs(Mathf.DeltaAngle(settle, -90f)) ? _lie : _lieFront;
                    rig.Squash(0.1f);
                    LandSoftParts(Mathf.Max(impact, 2.5f));
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
            if (FallingPhysically) _ragdoll.Release(impulse, info.contact);     // a fall becomes the free KO ragdoll
            else _ragdoll = Ragdoll2D.Activate(rig, impulse, info.contact);
            GameFeel.SlowMo(0.25f, 0.9f);
            if (PresentsCombat)
            {
                Vfx.I?.KoBurst(info.contact, fxMain, info.attacker != null ? info.attacker.fxMain : fxMain);
                GameFeel.Shake(0.5f);
                GameFeel.Punch(0.25f);
                GameFeel.Kick(impulse, 0.18f);
                GameFeel.ImpactFrame(new Color(0.07f, 0.015f, 0.035f, 0.92f), 3f / 60f);
                CombatAudio?.Play(SfxKind.KO, 1f);
            }
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
