namespace Kage
{
    /// <summary>
    /// The Ronin (id "ronin", dev only): the dev arena's sparring partner, a recolour of Kage's build with a
    /// straw hat and mantle. Same health as Kage (JS 232), 4 % larger, Kage's moves, no rage art.
    /// </summary>
    public static class Ronin
    {
        public const string Id = "ronin";

        public static CharacterDef Def() => new()
        {
            id = Id,
            displayName = "RONIN",
            title = "",
            jsHealth = 232f,
            bodyScale = 1.04f,
            partSet = "ronin",
            colors = new FighterColors(Palette.Amber, KMath.Hex("#FFF2D6"), Palette.AmberDeep),
            family = "katana",
            aiProfile = "regular",
        };
    }
}
