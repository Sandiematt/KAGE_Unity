using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Guard button (ADR-0004, SAN-86): reading it, the guard stance, and the guard branch of ReceiveHit
    /// (defence step 6), with the parry hook (step 5) in front of it. Posture and guard-break are Fighter.Posture.cs.
    /// </summary>
    public partial class Fighter
    {
        bool _guardLast, _guardPose;
        int _sinceGuardPress = 9999;

        /// <summary>Guard is held this frame (the button, not the stance: see <see cref="IsGuarding"/>).</summary>
        public bool GuardHeld => _guardHeld;

        /// <summary>
        /// Frames since Guard was last pressed (0 on the frame of the press; large when never). A press is
        /// <c>guardPressed</c>, or <c>guard</c> going from released to held. The parry window reads this (SAN-91).
        /// </summary>
        public int FramesSinceGuardPress => _sinceGuardPress;

        /// <summary>
        /// The fighter is in its guard: on the ground, in a neutral state or in blockstun, with Guard held. A hit
        /// that reaches it is blocked by the level rules (<see cref="GuardBlocks"/>).
        /// </summary>
        public bool IsGuarding => _guardHeld && Grounded && state is FState.Idle or FState.Walk or FState.Crouch or FState.Blockstun;

        /// <summary>The guard is low (Guard with Down).</summary>
        public bool LowGuard => _crouchHeld;

        /// <summary>Reads Guard from this frame's input (once per tick, before the state machine).</summary>
        void ReadGuard(in FrameInput inp)
        {
            bool held = inp.guard && controlEnabled;
            bool press = held && (inp.guardPressed || !_guardLast);
            _guardLast = held;
            _guardHeld = held;
            if (press) _sinceGuardPress = 0;
            else if (_sinceGuardPress < 9999) _sinceGuardPress++;
            ParryTick(press);
        }

        /// <summary>
        /// The level rules (unchanged from the hold-back guard): a Low hit needs a low guard, an Overhead a standing
        /// guard, High and Mid are blocked by either, and Unblockable is never guarded.
        /// </summary>
        public static bool GuardBlocks(HitLevel level, bool lowGuard) => level switch
        {
            HitLevel.Unblockable => false,
            HitLevel.Low => lowGuard,
            HitLevel.Overhead => !lowGuard,
            _ => true,
        };

        /// <summary>
        /// The guard stance, from Neutral: Guard held on the ground. Horizontal input is ignored (the fighter slows to a
        /// stop); Down makes it a low guard. Attacks, dash, roll and jump were already tried by Neutral, so they come
        /// straight out of the guard. True when the stance took this frame.
        /// </summary>
        bool GuardStance(FrameInput inp)
        {
            bool guarding = _guardHeld && Grounded;
            if (guarding != _guardPose)
            {
                _guardPose = guarding;
                if (state is FState.Idle or FState.Crouch) SetState(state);     // blend into (or out of) the guard pose
            }
            if (!guarding) return false;
            TryGuardTurn(inp);                    // Guard + a fresh Back press: turn to the foe behind (Fighter.Counter.cs)
            _walkHeld = 0f;
            var want = inp.dy < 0 ? FState.Crouch : FState.Idle;
            if (state != want) SetState(want);
            Vector2 v = rb.linearVelocity;
            v.x = Mathf.MoveTowards(v.x, 0f, GroundDecel * Time.fixedDeltaTime);
            rb.linearVelocity = v;
            return true;
        }

        /// <summary>Declared by SAN-86; implemented by SAN-91 in Fighter.Parry.cs. Defence step 5 (ADR-0004), before the guard.</summary>
        partial void ParryCheck(ref DefenceCheck c);

        /// <summary>Defence step 5: the parry check. True when it took the hit; <paramref name="result"/> is then its result.</summary>
        bool TryParry(HitInfo info, out HitResult result)
        {
            var c = new DefenceCheck { info = info };
            ParryCheck(ref c);
            result = c.result;
            return c.resolved;
        }

        /// <summary>
        /// Defence step 6, the guard branch of <see cref="ReceiveHit"/>: true (and Blocked) when this fighter is guarding
        /// and the hit's level allows it. A guarded hit puts the fighter in blockstun, takes chip damage off a blade
        /// hit, and adds posture (damage × <see cref="GuardTuning.blockPostureFactor"/> × the window's postureMul); a
        /// full bar breaks the guard (<see cref="BreakGuard"/>). A block builds Rage (<see cref="BlockRageGain"/>: damage × 0.4, × the
        /// Rage and blockRage perks), as in the JS game.
        /// </summary>
        bool TryGuard(HitInfo info, out HitResult result)
        {
            result = HitResult.Blocked;
            var w = info.w;
            if (!IsGuarding || w.unguardable || !GuardBlocks(w.level, _crouchHeld)) return false;     // unguardable: only a parry stops it (Raikon's iaijutsu)
            var t = Tuning.Get<GuardTuning>();
            SetState(FState.Blockstun);
            _stun = Mathf.Max(t.minBlockstun, Mathf.RoundToInt(w.stun * t.blockstunFactor));
            Knock(new Vector2(KnockVelocity(new Vector2(info.kb.x * 0.55f + Mathf.Sign(info.kb.x) * 0.8f, 0f)).x, rb.linearVelocity.y));
            health = Mathf.Max(1f, health - (w.sword ? Mathf.Ceil(info.dmg * t.chipFactor) : 0f));
            rig.Flash(new Color(0.85f, 0.9f, 1f), 0.35f);
            rig.Impulse(new Vector2(-facing * (w.heavy ? 4.8f : 2.6f), 1.0f), 1.15f);
            if (w.breaksWeapon) BreakSword();
            meter = Mathf.Min(100f, meter + BlockRageGain(info.dmg));      // blocking builds Rage, more with the blockRage perk (SAN-84)
            AddPosture(info.dmg * t.blockPostureFactor * w.postureMul * info.PostureScale * PressureMul(info.attacker), true, info);   // a string of guarded blows weighs more (Fighter.Flow.cs)
            NoteDefended();                                                // a guard opens the weapon's riposte (Fighter.Style.cs)
            TurnAfterBlock(info);                                          // a blow from behind turns the guard round (Fighter.Counter.cs)
            return true;
        }
    }
}
