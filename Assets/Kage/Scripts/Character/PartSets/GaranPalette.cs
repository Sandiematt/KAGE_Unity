using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Garan's colours (SAN-104), from the JS sub-boss tones (<c>21-characters/sub-bosses/_shared/tones.js</c>, <c>garan/*.js</c>): a near-black
    /// gi, dark storm iron for the brow band, the half dō, the sode and the tekko (<c>SUB_IRON</c>), a red lacquer oni menpō
    /// (<c>SUB_RED_LACQ</c>), bone horns, and his storm red (<c>#ff6a52</c>, his eye and accent) in the lightning scar, the lacing and a hot
    /// ember back-light. Kuroe is ink, gold and paper; the Order slate and silver; the Hollows violet: Garan is iron and storm red.
    /// </summary>
    public static class GaranPalette
    {
        public static readonly Color Gi = KMath.Hex("#141215");              // the gi and hakama
        public static readonly Color GiFar = KMath.Hex("#0C0B0D");
        public static readonly Color GiLit = KMath.Hex("#2A262B");           // a lit fold, the lapels
        public static readonly Color Iron = KMath.Hex("#16181B");            // SUB_IRON
        public static readonly Color IronFar = KMath.Hex("#0D0E10");
        public static readonly Color IronLit = KMath.Hex("#33373E");
        public static readonly Color IronEdge = KMath.Hex("#565C66");        // a worn edge, the studs
        public static readonly Color Lacquer = KMath.Hex("#35100D");         // SUB_RED_LACQ: the menpō
        public static readonly Color LacquerLit = KMath.Hex("#5E1D17");
        public static readonly Color LacquerDeep = KMath.Hex("#1B0706");
        public static readonly Color Mane = KMath.Hex("#0B0A0C");            // the wild mane
        public static readonly Color ManeLit = KMath.Hex("#24201F");
        public static readonly Color Horn = KMath.Hex("#B9AE98");            // bone horns on the brow band
        public static readonly Color HornShade = KMath.Hex("#6F6555");
        public static readonly Color Storm = KMath.Hex("#FF6A52");           // his accent: the scar, the lacing, the eye, effect colour
        public static readonly Color StormDark = KMath.Hex("#8E2A1E");
        public static readonly Color Rope = KMath.Hex("#5A2620");            // the thick belt rope
        public static readonly Color RopeLit = KMath.Hex("#7C3A2E");
        public static readonly Color Skin = KMath.Hex("#1C1916");            // the anime sub-bosses' black skin
        public static readonly Color Eye = KMath.Hex("#FF6A52");
        public static readonly Color Rim = KMath.Hex("#E2704E");             // a hot ember back-light
    }
}
