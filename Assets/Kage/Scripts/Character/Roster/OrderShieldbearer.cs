using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Order Shieldbearer (id "order_shieldbearer", JS ENEMIES entry, SAN-98): a spear-and-shield Order sentinel. JS stats come
    /// from the generated tables (220 health, defence 0.12, damage 1, speed 1, the defensive AI profile, build scale 1.06). He fights
    /// with the spear family's move set (<c>sentinel_spear</c>, JS base Mizuchi) with no move set of his own (ADR-0006). His shield is
    /// part of his look (<see cref="OrderShieldbearerPartSet"/>) and of his data: <see cref="CharacterFlags.FrontalArmour"/>, so light
    /// hits from the front land their damage without a flinch, while a hit from behind, a heavy hit or any hit after a guard-break
    /// flinches him (<c>Fighter.Armour.cs</c>, ADR-0004). The JS foe has no such flag; the shield is its tip ("the shield turns every
    /// cut") made a rule.
    /// </summary>
    public static class OrderShieldbearer
    {
        public const string Id = "order_shieldbearer";

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = Id;
            d.colors = new FighterColors(OrderPalette.Eye, Color.white, OrderPalette.SilverDark);
            d.family = WeaponFamilies.Spear;
            d.flags |= CharacterFlags.FrontalArmour;
            d.mass = 1.25f;             // armoured, with a plank shield on one arm: a little harder to shove than Kage
            return d;
        }
    }
}
