namespace Kage
{
    public enum ArmorTier { Common, Rare, Epic, Trophy, Legendary }

    /// <summary>How a piece of armor is painted.</summary>
    public enum PieceKind
    {
        /// <summary>A rigid cel-shaded shape (<see cref="OpponentKit.Cel"/>) with the tier's back-light rim.</summary>
        Cel,
        /// <summary>One flat polygon in one tone of the palette (glow lines, halos, studs), optionally see-through.</summary>
        Flat,
        /// <summary>A cel-shaded soft piece bound to spring chains (<see cref="SoftChains"/>): tails, ribbons, drapes, tassels.</summary>
        Cloth,
    }

    public static class ArmorTiers
    {
        public static readonly ArmorTier[] All = { ArmorTier.Common, ArmorTier.Rare, ArmorTier.Epic, ArmorTier.Trophy, ArmorTier.Legendary };

        /// <summary>The tier's display label in uppercase.</summary>
        public static string Label(ArmorTier t) => t.ToString().ToUpperInvariant();

        /// <summary>Maps a rules tier id ("common", "rare", "epic", "trophy", "legendary") to a <see cref="ArmorTier"/>.</summary>
        public static ArmorTier FromId(string id)
        {
            switch (id)
            {
                case "rare": return ArmorTier.Rare;
                case "epic": return ArmorTier.Epic;
                case "trophy": return ArmorTier.Trophy;
                case "legendary": return ArmorTier.Legendary;
                default: return ArmorTier.Common;
            }
        }
    }

}
