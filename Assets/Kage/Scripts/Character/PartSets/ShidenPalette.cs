using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Shiden's colours (SAN-106), from the JS sub-boss tones (<c>21-characters/sub-bosses/_shared/tones.js</c>, <c>shiden/*.js</c>): black
    /// lacquer (<c>SUB_LACQ</c>: #060607, #111114, #2a2a30) for the kabuto, the dō, the sode and the kusazuri, dark iron (<c>SUB_IRON</c>) for
    /// the hōate, the kote and the suneate, the banner's violet-black cloth (#0b0812, #171124, #2b2240) for his gi, silver (<c>ANIME.silver</c>)
    /// for the star rivets, the lightning maedate and the lightning marks, and his violet (<c>#b07cff</c>, his accent, in the JS
    /// <c>subTone</c> steps; <c>#d6a8ff</c>, his eye) in the lacing, with a violet back-light. Kuroe is ink, gold and paper; Garan iron and
    /// storm red; Hisame rain-dark cloth and rain blue; the Order slate and silver: Shiden is black lacquer, silver and violet.
    /// </summary>
    public static class ShidenPalette
    {
        public static readonly Color Lacquer = KMath.Hex("#111114");         // SUB_LACQ[1]: kabuto, dō, sode, kusazuri
        public static readonly Color LacquerDeep = KMath.Hex("#060607");     // SUB_LACQ[0]
        public static readonly Color LacquerLit = KMath.Hex("#2A2A30");      // SUB_LACQ[2]: a lit edge
        public static readonly Color LacquerFar = KMath.Hex("#0B0B0D");      // SUB_LACQ_FAR[1]: the far side of the body
        public static readonly Color Iron = KMath.Hex("#16181B");            // SUB_IRON[1]: hōate, kote, suneate
        public static readonly Color IronLit = KMath.Hex("#33373E");         // SUB_IRON[2]
        public static readonly Color IronFar = KMath.Hex("#08090A");         // SUB_IRON[0]
        public static readonly Color Cloth = KMath.Hex("#171124");           // the banner's cloth: his gi
        public static readonly Color ClothFar = KMath.Hex("#0B0812");
        public static readonly Color ClothLit = KMath.Hex("#2B2240");
        public static readonly Color Silver = KMath.Hex("#AAB0B8");          // ANIME.silver: rivets, the maedate, the lightning marks
        public static readonly Color SilverHi = KMath.Hex("#E6E9EE");
        public static readonly Color SilverDark = KMath.Hex("#6C7076");
        public static readonly Color Accent = KMath.Hex("#B07CFF");          // his accent: the lacing, effect colour
        public static readonly Color AccentMid = KMath.Hex("#714FA3");       // subTone(#b07cff)[1]
        public static readonly Color AccentDeep = KMath.Hex("#3C2A57");      // subTone(#b07cff)[0]
        public static readonly Color Eye = KMath.Hex("#D6A8FF");             // his eye (ENEMIES.shiden.eye)
        public static readonly Color Pale = KMath.Hex("#E4D6FF");            // the glint on the point (the JS brace glint)
        public static readonly Color Face = KMath.Hex("#1A1820");            // the anime sub-bosses' black face, a violet cast
        public static readonly Color Rim = KMath.Hex("#A98ED8");             // a violet back-light
    }
}
