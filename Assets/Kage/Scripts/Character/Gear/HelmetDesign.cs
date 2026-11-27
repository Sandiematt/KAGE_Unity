using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The five rarity tiers a helmet belongs to: Common, Rare, Epic, Trophy, and Legendary.
    /// Each tier has its own palette and rim light (<see cref="HelmetTiers"/>); silhouette, detail and glow grow tier by tier.
    /// </summary>
    public enum HelmetTier
    {
        /// <summary>Steel gray (#9CA3AF): matte cloth and dark silhouette, close to the skull, no glow.</summary>
        Common,
        /// <summary>Ice blue (#38BDF8): detailed woven straw, steel brow plate and armored veil with crisp outlines.</summary>
        Rare,
        /// <summary>Arcane purple (#A855F7): layered iron kabuto plates, horned oni masks and sweeping violet plumes.</summary>
        Epic,
        /// <summary>Imperial gold (#EAB308): ceremonial white enamel, branching golden antlers, filigree and ribbons.</summary>
        Trophy,
        /// <summary>Blazing crimson-gold (#F97316 / #EF4444): demonic lacquered shells, fiery horns, molten flames and glowing eyes.</summary>
        Legendary,
    }

    /// <summary>How a helmet piece is painted.</summary>
    public enum PieceKind
    {
        /// <summary>A rigid cel-shaded shape (<see cref="OpponentKit.Cel"/>) with the tier's back-light rim.</summary>
        Cel,
        /// <summary>One flat polygon in one tone of the palette (glow lines, halos, studs), optionally see-through.</summary>
        Flat,
        /// <summary>A cel-shaded soft piece bound to spring chains (<see cref="SoftChains"/>): tails, ribbons, drapes, tassels.</summary>
        Cloth,
    }

    /// <summary>A detail painted over a piece (a fold, a rivet, a lace): one polygon in one tone of the palette.</summary>
    public sealed class HelmetDetail
    {
        public readonly string tone;
        /// <summary>0 shade, 1 mid, 2 lit.</summary>
        public readonly int which;
        public readonly float alpha;
        /// <summary>x, y pairs in reference px.</summary>
        public readonly float[] pts;

        public HelmetDetail(string tone, int which, float alpha, float[] pts)
        {
            this.tone = tone; this.which = which; this.alpha = alpha; this.pts = pts;
        }
    }

    /// <summary>One piece of a helmet design: an outline in reference px on a bone at a draw order, painted in a tone of its tier's palette.</summary>
    public sealed class HelmetPiece
    {
        public string name;
        public PieceKind kind;
        public B bone = B.Head;
        public int order;
        public string tone;
        /// <summary>Flat pieces: which tone of the triple (0 shade, 1 mid, 2 lit).</summary>
        public int which = 1;
        public float alpha = 1f;
        /// <summary>Cel shadow and lit-edge depths in reference px (the JS <c>sd</c> and <c>hd</c>).</summary>
        public float sd, hd;
        public bool rim = true;
        public float seam = 1.6f;
        /// <summary>HDR multiplier: above 1 the piece glows (it blooms in the game).</summary>
        public float glow = 1f;
        /// <summary>Cloth pieces: the <see cref="SoftPresets"/> feel.</summary>
        public string feel;
        /// <summary>x, y pairs in reference px (KageModel's frame).</summary>
        public float[] outline;
        /// <summary>Cloth pieces: the chains (x, y pairs in reference px, root first) the piece hangs on.</summary>
        public float[][] chains;
        public HelmetDetail[] detail;
    }

    /// <summary>
    /// A helmet's look: its tier, what happens to Kage's hair under it and its pieces. Authored in <c>Tools/helmet_art/helmets.py</c>
    /// and generated into <c>HelmetDesigns.g.cs</c>.
    /// </summary>
    public sealed class HelmetDesign
    {
        public readonly string id, look;
        public readonly HelmetTier tier;
        public readonly HairUnderHelmet hair;
        public readonly EyeUnderHelmet eye;
        public readonly HelmetPiece[] pieces;

        public HelmetDesign(string id, string look, HelmetTier tier, HairUnderHelmet hair, HelmetPiece[] pieces, EyeUnderHelmet eye = EyeUnderHelmet.Kept)
        {
            this.id = id; this.look = look; this.tier = tier; this.hair = hair; this.eye = eye; this.pieces = pieces;
        }
    }

    /// <summary>A tier's colours: the back-light rim and named cel tones (shade, mid, lit).</summary>
    public sealed class HelmetPalette
    {
        public readonly Color rim;
        readonly Dictionary<string, Color[]> _tones = new();

        public HelmetPalette(string rim, Dictionary<string, string[]> tones)
        {
            this.rim = KMath.Hex(rim);
            foreach (var kv in tones) _tones[kv.Key] = new[] { KMath.Hex(kv.Value[0]), KMath.Hex(kv.Value[1]), KMath.Hex(kv.Value[2]) };
        }

        public IEnumerable<string> ToneNames => _tones.Keys;

        public bool Has(string tone) => tone != null && _tones.ContainsKey(tone);

        /// <summary>One colour of a tone: 0 shade, 1 mid, 2 lit.</summary>
        public Color Colour(string tone, int which)
        {
            if (!_tones.TryGetValue(tone, out var t)) throw new ArgumentException("no tone " + tone + " in this tier's palette");
            return t[Mathf.Clamp(which, 0, 2)];
        }

        public OpponentKit.Tone Tone(string tone) => new(Colour(tone, 0), Colour(tone, 1), Colour(tone, 2));
    }

    /// <summary>The tier palettes (generated with the designs) and the tiers' names in rarity order.</summary>
    public static partial class HelmetTiers
    {
        /// <summary>The five rarity tiers in progression order: Common, Rare, Epic, Trophy, Legendary.</summary>
        public static readonly HelmetTier[] All = { HelmetTier.Common, HelmetTier.Rare, HelmetTier.Epic, HelmetTier.Trophy, HelmetTier.Legendary };

        public static HelmetPalette Palette(HelmetTier t) => Palettes[t];

        /// <summary>The tier's display label in uppercase.</summary>
        public static string Label(HelmetTier t) => t.ToString().ToUpperInvariant();

        /// <summary>Maps a rules tier id ("common", "rare", "epic", "trophy", "legendary") to a <see cref="HelmetTier"/>.</summary>
        public static HelmetTier FromId(string id)
        {
            switch (id)
            {
                case "rare": return HelmetTier.Rare;
                case "epic": return HelmetTier.Epic;
                case "trophy": return HelmetTier.Trophy;
                case "legendary": return HelmetTier.Legendary;
                default: return HelmetTier.Common;
            }
        }
    }

    /// <summary>Every helmet design by JS helmet id (generated: <c>HelmetDesigns.g.cs</c>).</summary>
    public static partial class HelmetDesigns
    {
        static Dictionary<string, HelmetDesign> _byId;

        static Dictionary<string, HelmetDesign> ById
        {
            get
            {
                if (_byId != null) return _byId;
                var d = new Dictionary<string, HelmetDesign>();
                foreach (var h in Generated) d[h.id] = h;
                return _byId = d;
            }
        }

        public static bool Has(string helmetId) => helmetId != null && ById.ContainsKey(helmetId);

        /// <summary>The design, or null when the helmet has none.</summary>
        public static HelmetDesign Get(string helmetId) => Has(helmetId) ? ById[helmetId] : null;

        public static IEnumerable<HelmetDesign> All => Generated;
    }
}
