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
    /// SAN-105 (Hisame) adds three more, each off unless a move sets it: a <b>vault</b> over the target (<see cref="MoveDef.vaultFrom"/>,
    /// Kocho), a <b>thrown projectile</b> (<see cref="MoveDef.projectile"/>, Smoke Bomb) and <b>move cues</b> (<see cref="MoveDef.cues"/>,
    /// her glint before the cut).
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

        // ---- SAN-105 (Hisame): the vault, a thrown projectile, move cues ----
        float _vaultX0, _vaultY0, _vaultDest;

        /// <summary>
        /// After root motion: a move with a vault (<see cref="MoveDef.vaultFrom"/>, Kocho) drives the body along its arc over the target.
        /// The destination is fixed on the vault's first frame from where the target stands then; each frame the body is given the
        /// velocity that reaches the arc's point at the move's time by the end of the physics step; on the last frame it lands, facing
        /// the target. True while it drives the body.
        /// </summary>
        bool VaultStep(MoveDef m)
        {
            if (m.vaultTo <= m.vaultFrom || moveFrame < m.vaultFrom || _movePrev >= m.vaultTo) return false;
            if (Passed(m.vaultFrom)) BeginVault(m);
            float u = Mathf.Clamp01((_moveTime - m.vaultFrom) / (m.vaultTo - m.vaultFrom));
            if (u >= 1f)
            {
                var at = new Vector2(_vaultDest, _vaultY0);
                rb.position = at;
                transform.position = new Vector3(at.x, at.y, transform.position.z);
                rb.linearVelocity = Vector2.zero;
                float to = opponent != null ? Mathf.Sign(opponent.Pos.x - at.x) : -facing;
                if (to != 0f) SetFacing(to, true);
                return true;
            }
            var want = new Vector2(Mathf.Lerp(_vaultX0, _vaultDest, KMath.Apply(Ease.InOutCubic, u)), _vaultY0 + Mathf.Sin(u * Mathf.PI) * m.vaultHeight);
            float dt = Time.fixedDeltaTime;
            var v = (want - rb.position) / dt;
            v.y -= Physics2D.gravity.y * rb.gravityScale * dt;      // the step adds gravity before it moves the body
            rb.linearVelocity = v;
            return true;
        }

        void BeginVault(MoveDef m)
        {
            _vaultX0 = Pos.x;
            _vaultY0 = Pos.y;
            _vaultDest = _vaultX0;
            var tgt = opponent;
            if (tgt == null) return;
            float toward = Mathf.Sign(tgt.Pos.x - Pos.x);
            if (toward == 0f) toward = facing;
            float lo = StageMin + m.vaultMargin, hi = StageMax - m.vaultMargin;
            float dest = Mathf.Clamp(tgt.Pos.x + toward * m.vaultPast, lo, hi);
            // no room at his back (a wall): come down beside him, on her own side
            if (Mathf.Abs(dest - tgt.Pos.x) < m.vaultRoom) dest = Mathf.Clamp(tgt.Pos.x - toward * m.vaultPast, lo, hi);
            _vaultDest = dest;
        }

        /// <summary>Throws the move's projectile (Smoke Bomb): a speed in the def's band from the fighter's own "throw/" stream.</summary>
        void SpawnMoveProjectile(MoveDef m)
        {
            if (ctx == null) return;
            var d = m.projectile;
            float speed = d.speedMax > d.speed ? ctx.Random.Stream("throw/" + FighterId).Range(d.speed, d.speedMax) : d.speed;
            var at = new Vector2(Pos.x + facing * m.projectileFrom.x, Pos.y + m.projectileFrom.y);
            Projectile.Spawn(ctx, this, d, at, facing, speed);
        }

        /// <summary>Plays a move cue at the far hand (the fan, the blade), the fighter as its owner.</summary>
        void PlayMoveCue(string id)
        {
            Vector2 at = rig != null ? rig.World(B.HandF, Vector2.zero) : Pos + Vector2.up;
            EffectCues.Play(id, at, new CueArgs { owner = this, dir = new Vector2(facing, 0f), scale = 1f });
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
