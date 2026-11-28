using UnityEngine;

namespace Kage
{
    /// <summary>
    /// What the equipped armour does in a fight (SAN-84, ADR-0005): the perks that need the fighter. Health and defence
    /// are on the character (<see cref="CharacterDef.MaxHealth"/>, <see cref="Defense"/>), damage and speed perks are folded into
    /// <see cref="DamageMul"/> and <see cref="SpeedMul"/> by <see cref="KageCharacter.Def(Rules.SaveData)"/>. Every value is 0 or 1 for a
    /// fighter with no perks (every foe, and Kage in his starter gear), so none of it changes a fight that has no perk.
    /// </summary>
    public partial class Fighter
    {
        /// <summary>Hit-stun resist (JS <c>stunRes</c>, a fraction): <see cref="ScaleStun"/>.</summary>
        public float StunRes { get; private set; }
        /// <summary>Dash bonus (JS <c>dash</c>, a fraction): longer dash invulnerability, shorter dash cool-down.</summary>
        public float DashBonus { get; private set; }
        /// <summary>Multiplier on every Rage (<see cref="meter"/>) gain (JS <c>1 + rage</c>).</summary>
        public float RageMul { get; private set; } = 1f;
        /// <summary>Multiplier on kunai damage (JS <c>1 + kunai</c>).</summary>
        public float KunaiMul { get; private set; } = 1f;
        /// <summary>Extra multiplier on the Rage a block gains (JS <c>1 + blockRage</c>). Hook: see <see cref="BlockRageGain"/>.</summary>
        public float BlockRageMul { get; private set; } = 1f;

        /// <summary>Seconds of dash invulnerability a full dash bonus of 1 would add (JS <c>0.17 + dashBonus × 0.5</c>).</summary>
        const float DashInvulnSeconds = 0.5f;
        /// <summary>The factor a hit's hit-stun is scaled by per point of stun resist (JS <c>1 − stunRes × 0.4</c>).</summary>
        public const float HitStunResist = 0.4f;
        /// <summary>The factor for a guard-break, parry or deflect stun (JS <c>1 − stunRes × 0.5</c>). Used by the posture and parry tickets.</summary>
        public const float BreakStunResist = 0.5f;
        /// <summary>The factor for a hard stun move (JS <c>1 − stunRes</c>).</summary>
        public const float HardStunResist = 1f;

        void ApplyGear(in GearPerks p)
        {
            StunRes = p.stunRes;
            DashBonus = p.dash;
            RageMul = p.RageMul;
            KunaiMul = p.KunaiMul;
            BlockRageMul = p.BlockRageMul;
            kunaiStart = p.KunaiStart;
        }

        /// <summary>
        /// A stun length cut by the stun-resist perk: <c>frames × (1 − StunRes × k)</c>, never below one frame. <paramref name="k"/> is
        /// <see cref="HitStunResist"/> for hit-stun (applied now), <see cref="BreakStunResist"/> for a guard-break, parry or
        /// deflect stun and <see cref="HardStunResist"/> for a hard stun (hooks: those stuns belong to SAN-86 and SAN-91).
        /// </summary>
        public int ScaleStun(int frames, float k = HitStunResist)
        {
            if (StunRes == 0f || frames <= 0) return frames;
            return Mathf.Max(1, Mathf.RoundToInt(frames * (1f - Mathf.Clamp01(StunRes * k))));
        }

        /// <summary>Extra frames of invulnerability at the start of every dash from the dash perk: <c>dash × 0.5 s</c>.</summary>
        public int DashInvulnExtra => DashBonus == 0f ? 0 : Mathf.RoundToInt(DashBonus * DashInvulnSeconds * 60f);

        /// <summary>
        /// Whether a dash can be hit now. Without the dash perk: a back dash is untouchable for its first frames, any other dash
        /// never (Unity's own rule). The perk adds <see cref="DashInvulnExtra"/> untouchable frames at the start of every dash.
        /// </summary>
        bool DashHittable()
        {
            int extra = DashInvulnExtra;
            if (extra == 0) return _dashAir || _dashDir > 0 || _dashFrame > BackDashInvuln;
            return _dashFrame >= extra + (_dashAir || _dashDir > 0 ? 0 : BackDashInvuln + 1);
        }

        /// <summary>
        /// The current move's invulnerability, with the dash perk's extra frames after a roll's or a backflip's own (the dodge moves
        /// are Kage's other dashes). Every other move is exactly its own <see cref="MoveDef.Invulnerable"/>.
        /// </summary>
        bool MoveInvulnerable()
        {
            if (move.Invulnerable(moveFrame)) return true;
            int extra = DashInvulnExtra;
            if (extra == 0 || move.invulnFrom < 0 || moves == null || (move != moves.roll && move != moves.backflip)) return false;
            return moveFrame > move.invulnTo && moveFrame <= move.invulnTo + extra;
        }

        /// <summary>A dash cool-down of <paramref name="frames"/> cut by the dash perk: <c>× (1 − dash)</c>.</summary>
        public int DashCooldownFrames(int frames) => DashBonus == 0f ? frames : Mathf.Max(1, Mathf.RoundToInt(frames * (1f - DashBonus)));

        /// <summary>
        /// Hook for the block's Rage gain (JS <c>damage × 0.4 × rageMul × (1 + blockRage)</c>). Unity's block gives no Rage yet; the guard
        /// ticket adds <c>meter += BlockRageGain(damage)</c> in <see cref="TryGuard"/> and the Kagehira and Shikkō perks then work.
        /// </summary>
        public float BlockRageGain(float damage) => damage * 0.4f * RageMul * BlockRageMul;

        /// <summary>The damage multiplier of a projectile this fighter owns beyond its own damage multiplier: the kunai perk for the kunai.</summary>
        public float ProjectileDamageMul(ProjectileDef def) => ReferenceEquals(def, KunaiProjectile) ? KunaiMul : 1f;
    }
}
