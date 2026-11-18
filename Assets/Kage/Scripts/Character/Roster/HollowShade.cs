namespace Kage
{
    /// <summary>
    /// The Hollow Shade (id "hollow_shade", JS ENEMIES entry): a memory echo that fights like the man it remembers. JS stats from
    /// the generated tables (160 health, defence 0.03, damage 0.85, the cutter blade), the katana family at full move rate with no
    /// move set of its own (ADR-0006), the regular AI profile. Its look is <see cref="HollowShadePartSet"/> in the shared
    /// <see cref="HollowPalette"/>.
    /// </summary>
    public static class HollowShade
    {
        public const string Id = "hollow_shade";

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = Id;
            d.colors = new FighterColors(HollowPalette.Glow, HollowPalette.Eye, HollowPalette.VioletDeep);
            d.family = "katana";
            return d;
        }
    }
}
