using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The one damage formula (ADR-0006):
    /// <c>raw = window dmg × attacker damageMul × weapon dmg × (crit ? 1.5 : 1) × (1 − defender defense)</c>,
    /// rounded once at the end, never below 1. With every multiplier at 1 and no defence this is the damage the moves
    /// always did (<c>RoundToInt(dmg × 1.5)</c> on a crit).
    /// </summary>
    public static class DamageCalc
    {
        public const float CritMul = 1.5f;

        public static int Damage(int windowDmg, float damageMul, float weaponDmg, bool crit, float defense)
        {
            float raw = windowDmg * damageMul * weaponDmg * (crit ? CritMul : 1f) * (1f - defense);
            return Mathf.Max(1, Mathf.RoundToInt(raw));
        }

        /// <summary>
        /// Damage of a hit from <paramref name="attacker"/> on <paramref name="target"/>. <paramref name="weaponHit"/>:
        /// the weapon's damage multiplier counts (every window of the family's move set, blade projectiles and the rage
        /// art; not throws or kunai). Props have no defence; a null attacker deals the window damage.
        /// </summary>
        public static int For(Fighter attacker, IHittable target, int windowDmg, bool crit, bool weaponHit)
        {
            float mul = attacker != null ? attacker.DamageMul : 1f;
            float weapon = weaponHit && attacker != null ? attacker.Weapon.dmg : 1f;
            float defense = target is Fighter f ? f.Defense : 0f;
            return Damage(windowDmg, mul, weapon, crit, defense);
        }
    }
}
