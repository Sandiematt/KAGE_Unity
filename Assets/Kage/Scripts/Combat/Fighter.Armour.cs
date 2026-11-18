namespace Kage
{
    /// <summary>
    /// The armour check (ADR-0004 step 7, ADR-0006): armour takes the hit's damage and posture but not its flinch
    /// (hitstun, launch, knockdown, knockback), and it doesn't cancel what the fighter is doing, so a throw's victim
    /// stays held. <see cref="ReceiveHit"/> asks <see cref="ResistsFlinch"/> once, after the damage, so a hit that
    /// kills is never resisted.
    /// <para>
    /// This file is the one place a source of armour is added. Today: a move's armour frames
    /// (<see cref="MoveDef.armorFrom"/>..<see cref="MoveDef.armorTo"/>, SAN-80), which ignore light hits (a
    /// <see cref="HitWindow"/> without <c>heavy</c>). The boss-wide Armour flag (SAN-102) and frontal armour (SAN-98)
    /// are two more lines in <see cref="ArmourCheck"/>.
    /// </para>
    /// </summary>
    public partial class Fighter
    {
        /// <summary>True when the fighter's current move has armour on this frame.</summary>
        public bool MoveArmoured => state == FState.Attack && move != null && move.Armoured(moveFrame);

        /// <summary>True when this hit would not flinch the fighter now (asked by <see cref="ReceiveHit"/> after the damage).</summary>
        bool ResistsFlinch(in HitInfo info)
        {
            bool resists = false;
            ArmourCheck(info, ref resists);
            return resists;
        }

        partial void ArmourCheck(in HitInfo info, ref bool resistsFlinch)
        {
            if (resistsFlinch || info.w == null) return;
            bool light = !info.w.heavy;
            if (light && MoveArmoured) resistsFlinch = true;          // a move's armour frames (SAN-80)
        }
    }
}
