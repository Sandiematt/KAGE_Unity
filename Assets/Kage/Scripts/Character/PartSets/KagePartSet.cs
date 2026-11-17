namespace Kage
{
    /// <summary>Kage's look (KageModel, Look.Kage). Colours other than his own crimson recolour the trim.</summary>
    public sealed class KagePartSet : IPartSet
    {
        public string Id => "kage";

        public RigDef Build(CharacterDef c)
        {
            var d = KageModel.Build(KageModel.Look.Kage);
            KageModel.Recolour(d, Palette.Trim, c.colors.main);
            KageModel.Recolour(d, Palette.TrimDeep, c.colors.dark);
            return d;
        }
    }
}
