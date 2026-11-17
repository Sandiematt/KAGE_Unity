namespace Kage
{
    /// <summary>The Ronin's look (KageModel, Look.Ronin: straw hat, mantle, no scarf). Colours other than amber recolour the trim.</summary>
    public sealed class RoninPartSet : IPartSet
    {
        public string Id => "ronin";

        public RigDef Build(CharacterDef c)
        {
            var d = KageModel.Build(KageModel.Look.Ronin);
            KageModel.Recolour(d, Palette.Amber, c.colors.main);
            KageModel.Recolour(d, Palette.AmberDeep, c.colors.dark);
            return d;
        }
    }
}
