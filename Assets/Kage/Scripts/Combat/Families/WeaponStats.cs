using Kage.Rules;

namespace Kage
{
    /// <summary>A weapon's combat multipliers at a level: damage, move-rate speed and blade reach.</summary>
    public struct WeaponCombat
    {
        public float dmg, speed, reach;
        public static readonly WeaponCombat One = new() { dmg = 1f, speed = 1f, reach = 1f };
    }

    /// <summary>
    /// Combat multipliers of a weapon (ADR-0006): the JS rule (+8 % damage, +2.5 % speed, +1.5 % reach per level, speed
    /// capped at ×1.25 and reach at ×1.2) measured against the weapon's family reference at level 1. For every weapon
    /// with a JS <c>base</c> that is the JS base, so this equals <see cref="GameRules.WeaponMul(string,int)"/>; a family's
    /// reference weapon is measured against itself, which only changes Raikō no Tachi (JS base Onikiri, ×1 here).
    /// <see cref="GameRules.WeaponMul(string,int)"/> stays the exact JS port for the parity tests.
    /// </summary>
    public static class WeaponStats
    {
        public static WeaponCombat Combat(string weaponId, int level)
        {
            var m = WeaponFamilies.IsReference(weaponId) ? GameRules.WeaponMul(weaponId, level, weaponId) : GameRules.WeaponMul(weaponId, level);
            return new WeaponCombat { dmg = (float)m.Dmg, speed = (float)m.Spd, reach = (float)m.Reach };
        }
    }
}
