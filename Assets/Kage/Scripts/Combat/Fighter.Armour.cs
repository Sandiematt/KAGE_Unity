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
    /// <see cref="HitWindow"/> without <c>heavy</c>), and the character-wide <see cref="CharacterFlags.Armour"/> flag (SAN-102: Ironclad,
    /// Raikon, a boss phase that adds it), which is the same rule for every frame, and
    /// the <see cref="CharacterFlags.FrontalArmour"/> flag (SAN-98: the Shieldbearer's shield), the same rule for light hits that
    /// come from the front only, and never after a guard-break.
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

        /// <summary>
        /// True when the guard was broken as this hit arrived (set by <see cref="ReceiveHitCore"/> before the break is spent by
        /// <see cref="ApplyGuardBreakCrit"/>): a guard-broken fighter has dropped its shield, so frontal armour doesn't hold.
        /// </summary>
        bool _hitOnBrokenGuard;

        /// <summary>
        /// True when this hit comes from the side the fighter faces. A hit pushes its target away from the attacker, so the push
        /// (<see cref="HitInfo.kb"/>) points the way the blow travelled; a hit with no sideways push is judged by where its attacker
        /// stands, and one with neither (a hazard that doesn't push) is never frontal.
        /// </summary>
        bool HitFromFront(in HitInfo info)
        {
            float away = info.kb.x;
            if (away == 0f && info.attacker != null) away = Pos.x - info.attacker.Pos.x;
            return away != 0f && away * facing < 0f;
        }

        partial void ArmourCheck(in HitInfo info, ref bool resistsFlinch)
        {
            if (resistsFlinch || info.w == null) return;
            bool light = !info.w.heavy;
            if (light && MoveArmoured) resistsFlinch = true;          // a move's armour frames (SAN-80)
            else if (light && Has(CharacterFlags.Armour)) resistsFlinch = true;    // the character-wide flag, from the data or a boss phase (SAN-102)
            else if (light && Has(CharacterFlags.FrontalArmour) && !_hitOnBrokenGuard && HitFromFront(info)) resistsFlinch = true;    // a shield: light hits from the front, never after a guard-break (SAN-98)
        }
    }
}
