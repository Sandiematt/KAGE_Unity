using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Order Ironclad (id "order_ironclad", JS ENEMIES entry, SAN-100): a heavily armoured Order raider with a cleaver. JS stats come
    /// from the generated tables (280 health, defence 0.14, damage 1.15, speed 0.9, the heavy AI profile, build scale 1.14). It fights
    /// with the cleaver family's move set (<c>raider_cleaver</c>) with no move set of its own (ADR-0006); the weight is in the set's
    /// frames, not a move rate. <see cref="CharacterFlags.Armour"/> and a mass of 2 are Unity additions (the JS foe has no
    /// <c>heavyArmor</c>; the spec asks for both): light hits land without a flinch or a push, heavy hits and a guard-break stagger it,
    /// and the same blow moves it half as far as Kage. Its look is <see cref="OrderIroncladPartSet"/>.
    /// </summary>
    public static class OrderIronclad
    {
        public const string Id = "order_ironclad";

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = Id;
            d.colors = new FighterColors(OrderPalette.Eye, Color.white, OrderPalette.SilverDark);
            d.family = WeaponFamilies.Cleaver;
            d.flags |= CharacterFlags.Armour;
            d.mass = 2f;
            return d;
        }
    }
}
