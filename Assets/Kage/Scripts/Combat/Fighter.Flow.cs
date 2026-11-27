using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Combat flow (docs/combat-flow.md): what a move earns by connecting, what a whiff costs, and the weight of a string on a guard.
    /// Numbers are in Resources/Tuning/flow.json (<see cref="CombatFlowTuning"/>).
    /// <list type="bullet">
    /// <item><b>Recovery cancels.</b> Once a ground move's last action is over (its blows, throws, hazards, its sheathing) and one of its
    /// blows landed or was guarded, a dash press cancels the rest of the recovery; if one landed, Guard does too. A whiffed move
    /// recovers in full. Hit-and-run: strike, dash out. Rushdown: keep the guard busy without eating every punish.</item>
    /// <item><b>The whiff punish.</b> A clean melee blow on a fighter recovering from a move that touched nothing deals
    /// <see cref="CombatFlowTuning.whiffPunishDamageMul"/>, reels it longer and calls "PUNISH". Only Kage scores it unless
    /// <see cref="CombatFlowTuning.foesPunishWhiffs"/>. Spacing and baiting pay.</item>
    /// <item><b>Guard pressure.</b> Guarded blows from one attacker less than <see cref="CombatFlowTuning.pressureWindowFrames"/> apart are a
    /// string: each after the first adds <see cref="CombatFlowTuning.pressureStep"/> more posture, up to <see cref="CombatFlowTuning.pressureMaxMul"/>.
    /// A turtled guard breaks sooner under a real string than under single pokes.</item>
    /// </list>
    /// </summary>
    public partial class Fighter
    {
        /// <summary>One of the current move's blows landed or was guarded (a parry, a clash or a dodge doesn't count).</summary>
        bool _moveContact;
        Fighter _pressureBy;
        int _pressureCount, _pressureIdle = 9999;

        /// <summary>The current move has connected: a blow landed or was guarded.</summary>
        public bool MoveConnected => _moveContact;

        /// <summary>The last hit this fighter took was a whiff punish.</summary>
        public bool LastHitPunish { get; private set; }

        /// <summary>The whiff punishes this fighter has landed since the round began.</summary>
        public int PunishesLanded { get; private set; }

        /// <summary>The blows in the current string of pressure on this fighter's guard (0 when none).</summary>
        public int PressureCount => _pressureIdle <= FlowT.pressureWindowFrames ? _pressureCount : 0;

        void ResetFlow()
        {
            _moveContact = false;
            _pressureBy = null;
            _pressureCount = 0;
            _pressureIdle = 9999;
            LastHitPunish = false;
            PunishesLanded = 0;
            ResetAnswerTell();
        }

        /// <summary>Once per tick: the pressure string's idle time and the answer tell count on.</summary>
        void FlowTick()
        {
            if (_pressureIdle < 9999) _pressureIdle++;
            AnswerTellTick();
        }

        /// <summary>The last frame on which the move still does something: a blow, a throw, a hazard, a pull, a brace, a vault, a counter window, its own posture cost, its sheathing.</summary>
        public static int LastActionFrame(MoveDef m)
        {
            int last = LastActiveFrame(m);
            if (m == null) return last;
            last = Mathf.Max(last, m.projectileFrame, m.hazard != null ? m.hazardFrame : -1, m.pullTo, m.counterTo, m.braceTo, m.vaultTo, m.grabTo, m.sheathFrom);
            if (m.isThrow) last = Mathf.Max(last, m.releaseFrame);
            foreach (var t in m.extraThrows) last = Mathf.Max(last, t.frame);
            foreach (var h in m.extraHazards) last = Mathf.Max(last, h.frame);
            foreach (var p in m.selfPosture) last = Mathf.Max(last, p.frame);
            return last;
        }

        /// <summary>A blow of the current move landed (Hit) or was guarded (Blocked): it connected (from OnHitResolved).</summary>
        void NoteMoveContact(HitResult res)
        {
            if (state == FState.Attack && move != null && res is HitResult.Hit or HitResult.Blocked) _moveContact = true;
        }

        /// <summary>
        /// The recovery of a ground move that connected: a dash press cancels it (the stick's way, forward unless held back), and if a blow landed,
        /// Guard held ends it into the guard. Not a throw, a move with a next link, or one still doing something. True when it cancelled.
        /// </summary>
        bool RecoveryCancel(MoveDef m, int fwd)
        {
            if (!_moveContact || !Grounded || m.airborne || m.isThrow || m.then != null || _victim != null) return false;
            if (m.hits.Count == 0 || moveFrame <= LastActionFrame(m)) return false;
            var t = FlowT;
            if (t.dashCancelOnContact && _dashBuf <= 4 && _dashCool <= 0)
            {
                _dashBuf = 99;
                PlayCancel(fwd < 0 ? -facing : facing);
                StartDash(fwd < 0 ? -1 : 1, false);
                return true;
            }
            if (t.guardCancelOnHit && moveHit && _guardHeld)
            {
                PlayCancel(facing);
                SetState(FState.Idle);           // guarding at once (IsGuarding); the guard stance takes over in neutral next frame
                return true;
            }
            return false;
        }

        void PlayCancel(float dir)
        {
            if (!PresentsCombat || Vfx.I == null) return;
            Vfx.I.Dust(Pos, 0.76f, -dir);
            Vfx.I.Ghost(rig, new Color(0.15f, 0.09f, 0.22f), 0.105f, new Vector2(-0.09f * dir, 0f));
        }

        /// <summary>
        /// Defence step 7: a clean melee blow on this fighter while it recovers from a move that touched nothing is a whiff punish (more damage;
        /// ReceiveHit adds the stun). Kage's only, unless foes punish too.
        /// </summary>
        bool ApplyWhiffPunish(ref HitInfo info)
        {
            var t = FlowT;
            var by = info.attacker;
            if (by == null || ReferenceEquals(by, this) || info.origin != HitOrigin.Melee || (!by.isPlayer && !t.foesPunishWhiffs)) return false;
            if (state != FState.Attack || move == null || move.isThrow || _moveContact || !InRecovery(move)) return false;
            info.dmg = Mathf.Max(1, Mathf.RoundToInt(info.dmg * t.whiffPunishDamageMul));
            return true;
        }

        /// <summary>The attacker's side of a whiff punish it landed on <paramref name="target"/>: the call-out and a little more hit-stop.</summary>
        void PunishLanded(Fighter target, in HitInfo info)
        {
            var t = FlowT;
            PunishesLanded++;
            GameFeel.Hitstop((info.w != null ? info.w.hitstop : 5) + t.whiffPunishHitstopFrames);
            if (PresentsCombat)
            {
                target.rig?.Flash(t.punishTint, 0.6f, 12f);
                Vfx.I?.CrossedSteel(info.contact, ImpactDirection(info, facing), new Color(0.32f, 0.92f, 1f), Color.white, 0.32f);
                GameFeel.Punch(0.065f);
                CombatAudio?.Play(SfxKind.PunishConfirm, 0.65f);
            }
            if (isPlayer) (ctx?.Hud ?? KageGame.I?.Hud)?.ShowCallout(CombatCalloutKind.Punish, info.contact);
        }

        /// <summary>
        /// The posture multiplier of a guarded blow from <paramref name="by"/>: 1 for the first of a string, then
        /// 1 + <see cref="CombatFlowTuning.pressureStep"/> per blow before it, at most <see cref="CombatFlowTuning.pressureMaxMul"/>. Counts the blow.
        /// </summary>
        float PressureMul(Fighter by)
        {
            if (by == null) return 1f;
            var t = FlowT;
            if (!ReferenceEquals(by, _pressureBy) || _pressureIdle > t.pressureWindowFrames) { _pressureBy = by; _pressureCount = 0; }
            float mul = Mathf.Min(Mathf.Max(1f, t.pressureMaxMul), 1f + t.pressureStep * _pressureCount);
            _pressureCount++;
            _pressureIdle = 0;
            return mul;
        }
    }
}
