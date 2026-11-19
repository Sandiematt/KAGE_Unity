using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Hollow Flicker (id "hollow_flicker", JS ENEMIES entry): a fast memory echo with a pair of daggers. JS stats come from the
    /// generated tables (140 health, defence 0.02, damage 0.8, speed 1.18, the agile AI profile, build scale 0.94). It fights with
    /// the twin family's move set (<c>shadow_daggers</c>) at full move rate, with no move set of its own (ADR-0006). Its look is
    /// <see cref="HollowFlickerPartSet"/> in the shared <see cref="HollowPalette"/>.
    /// </summary>
    public static class HollowFlicker
    {
        public const string Id = "hollow_flicker";

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = Id;
            d.colors = new FighterColors(HollowPalette.Glow, HollowPalette.Eye, HollowPalette.VioletDeep);
            d.family = "twin";
            return d;
        }
    }
}
