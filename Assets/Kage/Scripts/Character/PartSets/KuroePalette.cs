using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Kuroe's colours (SAN-103), from the JS sub-boss tones (<c>21-characters/sub-bosses/_shared/tones.js</c>, <c>kuroe/*.js</c>): ink-black
    /// lacquer and robe, a kesa of dark brown patchwork edged in her accent gold (<c>#c9a45c</c>), pale censor's paper (<c>#ece5d3</c>, her eye
    /// colour) for the redaction slip, the ink stroke and her tabi, and a warm bone back-light. The Order is cool slate and silver, the Hollows
    /// violet, Kage warm black and crimson: Kuroe is the only one in ink, gold and paper.
    /// </summary>
    public static class KuroePalette
    {
        public static readonly Color Lacquer = KMath.Hex("#0B0B0D");        // the eboshi
        public static readonly Color LacquerLit = KMath.Hex("#34343C");
        public static readonly Color Ink = KMath.Hex("#17151B");            // the robe and its sleeves
        public static readonly Color InkFar = KMath.Hex("#0E0D11");         // the same on the far side of the body
        public static readonly Color InkLit = KMath.Hex("#2C2A33");         // a lit fold
        public static readonly Color InkDeep = KMath.Hex("#060607");
        public static readonly Color Kesa = KMath.Hex("#20180D");           // the patchwork stole
        public static readonly Color KesaLit = KMath.Hex("#3B2C17");
        public static readonly Color Gold = KMath.Hex("#C9A45C");           // her accent: kesa edges, cords, the ring, effect colour
        public static readonly Color GoldDark = KMath.Hex("#7E6438");
        public static readonly Color Paper = KMath.Hex("#ECE5D3");          // the redaction slip, the ink stroke, the tabi
        public static readonly Color PaperShade = KMath.Hex("#B4AB95");
        public static readonly Color Face = KMath.Hex("#1C1916");           // the anime sub-bosses' black face
        public static readonly Color Eye = KMath.Hex("#ECE5D3");
        public static readonly Color Rim = KMath.Hex("#C8B48C");            // warm bone back-light
    }
}
