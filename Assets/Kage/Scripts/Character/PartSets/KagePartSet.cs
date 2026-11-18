namespace Kage
{
    /// <summary>
    /// Kage's look (KageModel, Look.Kage). Colours other than his own crimson recolour the trim. His equipped armour (SAN-85) and
    /// helmet (SAN-72) are layered over him afterwards, so a recolour never changes a piece of gear's own colours.
    /// </summary>
    public sealed class KagePartSet : IPartSet
    {
        public string Id => "kage";

        public RigDef Build(CharacterDef c)
        {
            var d = KageModel.Build(KageModel.Look.Kage);
            KageModel.Recolour(d, Palette.Trim, c.colors.main);
            KageModel.Recolour(d, Palette.TrimDeep, c.colors.dark);
            ArmorVisuals.Apply(d, c.armor, c.colors.main, c.colors.dark);   // gear layer: his equipped armour (first, so the helmet takes its rim light); nothing when it has no drawing
            HelmetVisuals.Apply(d, c.helmet);     // gear layer: his equipped helmet over the head; nothing when it has no drawing
            return d;
        }
    }
}
