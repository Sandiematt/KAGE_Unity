using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Fighting a group: turning to a blow from behind, and the counter hit (docs/combat-turn-and-counter.md). Numbers are in
    /// Resources/Tuning/parry.json (<see cref="ParryTuning"/>) and engagement.json (<see cref="EngagementTuning.warnBackAttacks"/>).
    /// <list type="bullet">
    /// <item><b>Turn to the blow.</b> A parry of a blow from behind turns the parrier round at once to face its attacker. A blocked blow
    /// from behind does too (since docs/combat-flow.md also in a 1v1: a cross-up), and so does a reel from one (Fighter.Facing.cs).</item>
    /// <item><b>Turn by hand.</b> Guard held and a fresh Back press turns the fighter round to the nearest foe behind it.</item>
    /// <item><b>Focus.</b> The foe a fighter parried (or turned to) stays its target for a while (<see cref="Focus"/>), over the
    /// nearest-in-front rule, so the answer goes to the foe it turned for.</item>
    /// <item><b>Counter hit.</b> A parried fighter is open to a counter hit from its parrier for its stagger plus
    /// <see cref="ParryTuning.counterGraceFrames"/>. The first clean blow the parrier lands in it is a counter hit: more damage,
    /// more hit-stun (so the next blow links), more posture and hit-stop, a "COUNTER" call-out and a
    /// <see cref="CombatEventType.CounterHit"/> event after the Hit. Only Kage scores them unless <see cref="ParryTuning.foesCounterHit"/>.</item>
    /// <item><b>The warning.</b> A foe behind Kage that starts an attack flashes a "!" so he knows to turn.</item>
    /// </list>
    /// </summary>
    public partial class Fighter
    {
        Fighter _focus;
        int _focusLeft;
        Fighter _counterBy;
        int _counterLeft;
        int _prevDx;
        bool _dxFresh;

        /// <summary>The foe this fighter keeps as its target over the nearest-in-front rule now; null when none.</summary>
        public Fighter Focus => _focusLeft > 0 ? _focus : null;

        /// <summary>True when <paramref name="by"/>'s next clean blow on this fighter is a counter hit (it parried this fighter).</summary>
        public bool CounterOpenFor(Fighter by) => _counterLeft > 0 && by != null && ReferenceEquals(_counterBy, by);

        /// <summary>Frames left of this fighter's opening to a counter hit (0 when none).</summary>
        public int CounterOpenLeft => _counterLeft;

        /// <summary>The last hit this fighter took was a counter hit.</summary>
        public bool LastHitCounter { get; private set; }

        /// <summary>The counter hits this fighter has landed since the round began.</summary>
        public int CounterHitsLanded { get; private set; }

        void ResetCounter()
        {
            _focus = null; _focusLeft = 0;
            _counterBy = null; _counterLeft = 0;
            _prevDx = 0;
            _dxFresh = false;
            LastHitCounter = false;
            CounterHitsLanded = 0;
        }

        /// <summary>Once per tick: the focus and the counter opening count down, and a fresh horizontal press is noted.</summary>
        void CounterTick(in FrameInput inp)
        {
            int dx = inp.dx == 0 ? 0 : (int)Mathf.Sign(inp.dx);
            _dxFresh = dx != 0 && dx != _prevDx;
            _prevDx = dx;
            if (_focusLeft > 0 && --_focusLeft == 0) _focus = null;
            if (_counterLeft > 0 && --_counterLeft == 0) _counterBy = null;
        }

        /// <summary>True when <paramref name="f"/> stands behind this fighter (against <see cref="facing"/>).</summary>
        public bool IsBehind(Fighter f) => f != null && (f.Pos.x - Pos.x) * facing < 0f;

        /// <summary>Keeps <paramref name="f"/> as the target for <paramref name="frames"/> (a foe only).</summary>
        void FocusOn(Fighter f, int frames)
        {
            if (f == null || f == this || f.team == team || frames <= 0) return;
            _focus = f;
            _focusLeft = frames;
            opponent = f;
        }

        /// <summary>Turns round at once to face <paramref name="f"/> when it stands behind. True when it turned.</summary>
        bool TurnToward(Fighter f)
        {
            if (!IsBehind(f)) return false;
            SetFacing(f.Pos.x - Pos.x, true);
            return true;
        }

        /// <summary>The focus, for <see cref="PickTarget"/>: true (and <see cref="opponent"/> set) while a focused foe can still be fought.</summary>
        bool HoldFocus()
        {
            if (_focusLeft <= 0 || _focus == null) return false;
            if (_focus.state is FState.KO or FState.Down || !Targets.Contains(_focus)) { _focus = null; _focusLeft = 0; return false; }
            opponent = _focus;
            return true;
        }

        /// <summary>
        /// From the guard stance: a fresh Back press (Back now, not last frame) with Guard held turns the fighter to the nearest
        /// standing foe behind it, which becomes its focus. No foe behind: nothing (Back in the guard stays ignored).
        /// </summary>
        void TryGuardTurn(in FrameInput inp)
        {
            if (!_dxFresh || Mathf.Sign(inp.dx) == facing || Targets.Count == 0) return;
            Fighter best = null;
            float bestD = float.MaxValue;
            foreach (var o in Targets)
            {
                if (o == null || o.state == FState.KO || !IsBehind(o)) continue;
                float d = Mathf.Abs(o.Pos.x - Pos.x);
                if (d < bestD) { bestD = d; best = o; }
            }
            if (best == null) return;
            TurnToward(best);
            FocusOn(best, ParryT.turnFocusFrames);
            if (state is FState.Idle or FState.Crouch) SetState(state);         // blend the guard pose to the new side
            Vfx.I?.Dust(Pos, 0.5f, facing);
        }

        /// <summary>A parry: turn to the attacker when it was behind, and keep it as the target (called before the cue plays).</summary>
        void TurnAfterParry(Fighter attacker)
        {
            if (attacker == null || ReferenceEquals(attacker, this)) return;
            var t = ParryT;
            if (t.turnToBlow) TurnToward(attacker);
            FocusOn(attacker, t.parryFocusFrames);
        }

        /// <summary>
        /// A block: a blow from behind turns the guard round to face it (in a group fight it stays the target a while). In a 1v1 the flow rule
        /// <see cref="CombatFlowTuning.turnToBlowFromBehind"/> turns it too (a cross-up), so the blockstun never faces away from the blade.
        /// </summary>
        void TurnAfterBlock(in HitInfo info)
        {
            var attacker = info.attacker;
            if (attacker == null || ReferenceEquals(attacker, this) || !ParryT.turnToBlow) return;
            if (Targets.Count <= 1) { TurnToBlow(info, false); return; }
            if (TurnToward(attacker)) FocusOn(attacker, ParryT.turnFocusFrames);
        }

        /// <summary>This fighter was parried by <paramref name="by"/> and reels for <paramref name="stun"/> frames: it is open to a counter hit.</summary>
        void OpenToCounter(Fighter by, int stun)
        {
            if (by == null || (!by.isPlayer && !ParryT.foesCounterHit)) return;
            _counterBy = by;
            _counterLeft = Mathf.Max(1, stun) + ParryT.counterGraceFrames;
        }

        /// <summary>Defence step 7: a clean melee blow from the fighter that parried this one, inside the opening, is a counter hit (spends it).</summary>
        bool ApplyCounterHit(ref HitInfo info)
        {
            if (!CounterOpenFor(info.attacker) || info.origin != HitOrigin.Melee || state == FState.Down) return false;
            _counterLeft = 0;
            _counterBy = null;
            info.dmg = Mathf.Max(1, Mathf.RoundToInt(info.dmg * ParryT.counterDamageMul));
            return true;
        }

        /// <summary>The attacker's side of a counter hit it landed on <paramref name="target"/>: feedback and the event.</summary>
        void CounterHitLanded(Fighter target, in HitInfo info)
        {
            var t = ParryT;
            CounterHitsLanded++;
            GameFeel.Hitstop(t.counterHitstopFrames);
            if (PresentsCombat)
            {
                Vector2 dir = ImpactDirection(info, facing);
                Color crimson = new(0.93f, 0.075f, 0.16f);
                Vfx.I?.SlashLine(info.contact - dir * 0.48f, info.contact + dir * 0.98f, crimson, t.counterTint, 0.22f, 0.092f);
                GameFeel.ImpactFrame(new Color(0.16f, 0.018f, 0.045f, 0.82f), 2f / 60f);
                target.rig?.Flash(t.counterTint, 0.7f, 12f);
                CombatAudio?.Play(SfxKind.Counter, 0.7f);
                CombatAudio?.Play(SfxKind.Thud, 0.54f);
            }
            ctx?.Events.Publish(CombatEvent.CounterHit(ctx.Clock.Frame, this, target, info.moveId, info.window, target.LastDamageTaken, info.contact));
        }

        /// <summary>A foe behind Kage starts an attack: flash a "!" over it so he knows to turn.</summary>
        void WarnBackAttack(MoveDef m)
        {
            if (isPlayer || m == null || m.hits.Count == 0 || opponent == null || !opponent.isPlayer) return;
            if (opponent.Targets.Count <= 1 || !opponent.IsBehind(this) || !Tuning.Get<EngagementTuning>().warnBackAttacks) return;
            KageGame.I?.Hud?.Float(Pos + Vector2.up * 2.3f, "!", ParryT.counterTint, 1.2f);
            Vfx.I?.Glint(Pos + Vector2.up * 1.9f, ParryT.counterTint);
        }
    }
}
