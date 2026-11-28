namespace Kage
{
    /// <summary>
    /// Builds Kage's head and clothing from the loadout.
    /// </summary>
    public sealed class KagePartSet : IPartSet
    {
        public string Id => "kage";

        public RigDef Build(CharacterDef c)
        {
            var d = KageModel.Build(KageModel.Look.Kage, c.hairstyle);
            KageModel.Recolour(d, Palette.Trim, c.colors.main);
            KageModel.Recolour(d, Palette.TrimDeep, c.colors.dark);
            ArmorVisuals.Apply(d, c.armor, c.colors.main, c.colors.dark);
            return d;
        }
    }
}
