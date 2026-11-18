namespace Kage
{
    /// <summary>
    /// The tutorial Hollow (id "hollow_tutorial", JS ENEMIES entry): the Prologue's one fight, a Hollow Shade in look and family
    /// but a gentle foe. JS stats from the generated tables (95 health, no defence, damage 0.3). It plays the shared katana move set
    /// at a move rate of 0.75 (slow attacks) and the AI throws only three in ten of the attacks it picks
    /// (<see cref="CharacterDef.attackRate"/> 0.3; rare attacks). Its look is <see cref="HollowTutorialPartSet"/>.
    /// </summary>
    public static class HollowTutorial
    {
        public const string Id = "hollow_tutorial";

        public static CharacterDef Def()
        {
            var d = CharacterDef.FromJs(Id);
            d.partSet = Id;
            d.colors = new FighterColors(HollowPalette.Glow, HollowPalette.Eye, HollowPalette.VioletDeep);
            d.family = "katana";
            d.moveRate = 0.75f;
            d.attackRate = 0.3f;
            return d;
        }
    }
}
