using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Facing (docs/combat-flow.md). A fighter faces left or right, nothing in between: <see cref="SetFacing"/> flips the rig's scale x
    /// at once (<see cref="FighterRig.SetFacing"/>), with no tween through zero.
    /// <list type="bullet">
    /// <item><b>When it turns on its own.</b> In neutral, towards its target, past a dead zone and not straight back after a turn
    /// (<see cref="CombatFlowTuning.turnDeadZone"/>, <see cref="CombatFlowTuning.turnHoldFrames"/>); and as a new ground attack starts
    /// (<see cref="FaceTargetForAttack"/>), so a chain or a button pressed with the target behind turns between two animations.</item>
    /// <item><b>The lock.</b> <see cref="FacingLocked"/>: a move's wind-up and blows, a dash, the air, a reel, a block, a fall, a throw and
    /// the rage art never turn on their own. Only a move's own turn-around frames (a vault's landing, a counter's teleport, an art's beats)
    /// and the defence's turn to a blow set facing then.</item>
    /// <item><b>Turning to a blow.</b> A reel or a block from a blow from behind turns the fighter to its attacker as the reel or block
    /// begins (<see cref="TurnToBlow"/>), so it recoils away from the blade instead of into it.</item>
    /// <item><b>No spins.</b> Nothing turns the whole body round: no move rotates the rig about its hips (the old <c>MoveDef.Spin</c> is
    /// gone), so a turn is only ever this flip of scale x.</item>
    /// </list>
    /// </summary>
    public partial class Fighter
    {
        static CombatFlowTuning FlowT => Tuning.Get<CombatFlowTuning>();

        int _sinceTurn = 9999;

        /// <summary>Frames since this fighter last turned round (large when it hasn't this round).</summary>
        public int FramesSinceTurn => _sinceTurn;

        /// <summary>
        /// True while nothing turns this fighter on its own: a move's wind-up and blows (a move with no blows, such as a roll, throughout),
        /// a dash, the air, a reel, a block, a fall, lying, being thrown, the rage art, the intro and the victory.
        /// </summary>
        public bool FacingLocked => state switch
        {
            FState.Attack => move == null || !InRecovery(move),
            FState.Dash or FState.Air or FState.Hitstun or FState.Blockstun or FState.Launched or FState.Down or FState.Thrown
                or FState.KO or FState.Ultimate or FState.Intro or FState.Victory => true,
            _ => false,
        };

        /// <summary>The last frame of the move's last blow; -1 for a move with none.</summary>
        public static int LastActiveFrame(MoveDef m)
        {
            int last = -1;
            if (m == null) return last;
            foreach (var w in m.hits) if (w.to > last) last = w.to;
            return last;
        }

        /// <summary>The move has blows and they are all over: what is left of it is recovery.</summary>
        bool InRecovery(MoveDef m) => m != null && m.hits.Count > 0 && moveFrame > LastActiveFrame(m);

        void TurnTick() { if (_sinceTurn < 9999) _sinceTurn++; }

        /// <summary>Neutral: turn to the target when it stands past the dead zone on the other side, unless this fighter only just turned.</summary>
        void FaceOpponent()
        {
            if (opponent == null || opponent.state == FState.Thrown) return;
            var t = FlowT;
            float dx = opponent.Pos.x - Pos.x;
            if (Mathf.Abs(dx) <= t.turnDeadZone || Mathf.Sign(dx) == facing) return;
            if (_sinceTurn < t.turnHoldFrames && Mathf.Abs(dx) < t.turnForceDistance) return;     // no flip-flop over a foe crossing close
            SetFacing(Mathf.Sign(dx));
        }

        /// <summary>
        /// A new ground attack (a button from neutral, a landing, a dash or a skid, a chain, an ability) faces its target first when the
        /// target stands behind: the flip falls between two animations, never inside one. True when it turned.
        /// </summary>
        bool FaceTargetForAttack()
        {
            if (!FlowT.turnOnMoveStart || opponent == null || !Grounded) return false;
            if (opponent.state is FState.Thrown or FState.KO) return false;
            float dx = opponent.Pos.x - Pos.x;
            if (Mathf.Abs(dx) <= FlowT.turnDeadZone || Mathf.Sign(dx) == facing) return false;
            SetFacing(dx, true);
            return true;
        }

        /// <summary>
        /// A reel or a block from a melee blow from behind: turn to the attacker (in a group fight it stays the target a while). A shield
        /// bearer doesn't turn on a reel: its back is its weak side, and turning would cover it.
        /// </summary>
        void TurnToBlow(in HitInfo info, bool reel)
        {
            var attacker = info.attacker;
            if (attacker == null || ReferenceEquals(attacker, this) || info.origin != HitOrigin.Melee || !FlowT.turnToBlowFromBehind) return;
            if (reel && Has(CharacterFlags.FrontalArmour)) return;
            if (TurnToward(attacker) && Targets.Count > 1) FocusOn(attacker, ParryT.turnFocusFrames);
        }
    }
}
