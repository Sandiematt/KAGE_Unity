using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Boss-move hooks (SAN-104, Garan): three things a move can ask for that no move needed before, each off unless the move sets it
    /// (<see cref="MoveDef.hazard"/>, <see cref="MoveDef.onContact"/>, <see cref="MoveDef.wallStagger"/>), so every other move plays as it did.
    /// <list type="bullet">
    /// <item>A <b>hazard</b> laid down on a frame of the move, in front of the fighter (Storm Fist's shock area, ADR-0007).</item>
    /// <item>An <b>on-contact follow-up</b>: once a hit window lands or is blocked, the follow-up plays in place (the charge skids to a stop).</item>
    /// <item>A <b>wall stagger</b>: root motion that runs the fighter into the wall it faces leaves it staggered (the charge, dodged, meets the wall).</item>
    /// </list>
    /// The clinch's grab window (<see cref="MoveDef.grabTo"/>) is in Fighter.Throw.cs.
    /// </summary>
    public partial class Fighter
    {
        MoveDef _contactOf;     // the move whose blow made contact this frame; its onContact plays next frame

        void SpawnMoveHazard(MoveDef m)
        {
            if (ctx == null) return;
            Hazard.Spawn(ctx, this, m.hazard, new Vector2(Pos.x + facing * m.hazardAhead, Pos.y));
        }

        /// <summary>Called for every resolved blow of this fighter: a hit or a block by a move with an onContact follow-up marks it.</summary>
        void NoteContact(HitResult res)
        {
            if (move != null && move.onContact != null && state == FState.Attack && res is HitResult.Hit or HitResult.Blocked) _contactOf = move;
        }

        /// <summary>At the start of a move frame: the follow-up of a move whose blow made contact. True when it started.</summary>
        bool ContactFollowUp(MoveDef m)
        {
            if (_contactOf == null) return false;
            bool mine = ReferenceEquals(_contactOf, m);
            _contactOf = null;
            if (!mine) return false;
            StartMove(m.onContact);
            return true;
        }

        /// <summary>After root motion: a move with a wall stagger that drove the fighter into the wall it faces staggers it. True when it did.</summary>
        bool RanIntoWall(MoveDef m, bool driven)
        {
            if (m.wallStagger <= 0 || !driven || WallSide != facing) return false;
            Stagger(m.wallStagger);
            GameFeel.Shake(0.4f);
            Vfx.I?.Dust(Pos + Vector2.right * facing * 0.3f, 1.4f, -facing);
            KageGame.I?.Hud?.Float(Pos + Vector2.up * 2.2f, "STAGGERED", new Color(0.79f, 0.64f, 0.36f), 0.9f);
            return true;
        }
    }
}
