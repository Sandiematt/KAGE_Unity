using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Hollow Husk (id "hollow_husk", JS ENEMIES entry, SAN-112): the heavy Hollow, a fists brute in the last wave of Stretch IV. JS
    /// stats come from the generated tables (270 health, defence 0.1, damage 1.15, speed 0.88, the heavy AI profile, build scale 1.16).
    /// It fights with the fists family's move set (<c>iron_fists</c>, the heavy fists, ADR-0006) with no move set of its own. The
    /// <see cref="CharacterFlags.Armour"/> flag and a mass of 2.5 are Unity values from the spec (the JS Husk is only "slow and hard to
    /// stagger"): light hits land without a flinch or a push, heavy hits and a guard-break stagger it, and the same blow moves it less than
    /// half as far as a Footman. Its look is <see cref="HollowHuskPartSet"/> in the shared <see cref="HollowPalette"/>.
    /// </summary>
    public static class HollowHusk
    {
        public const string Id = "hollow_husk";

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = Id;
            d.colors = new FighterColors(HollowPalette.Glow, HollowPalette.Eye, HollowPalette.VioletDeep);
            d.family = WeaponFamilies.Fists;
            d.flags |= CharacterFlags.Armour;
            d.mass = 2.5f;
            return d;
        }
    }
}
