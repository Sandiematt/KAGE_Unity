using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Prestige ambient VFX style carried directly on Legendary armors (active in both Armory previews and combat).</summary>
    public enum ArmorAuraKind
    {
        None,
        /// <summary>Yūrei Armor: drifting celestial spirit-azure &amp; gold embers and soft pulsing core rune shimmer.</summary>
        Spirit,
        /// <summary>Mugen Armor: subtle crackling violet-gold energy arcs across pauldrons &amp; halo, drifting void-gold embers and pulsing core rune.</summary>
        Void,
    }

    /// <summary>A detail painted over an armor piece (a lace, filigree scroll, rivet, crease): one polygon in one tone of the armor's palette.</summary>
    public sealed class ArmorDetail
    {
        public readonly string tone;
        /// <summary>0 shade, 1 mid, 2 lit.</summary>
        public readonly int which;
        public readonly float alpha;
        /// <summary>x, y pairs in reference px.</summary>
        public readonly float[] pts;

        public ArmorDetail(string tone, int which, float alpha, float[] pts)
        {
            this.tone = tone;
            this.which = which;
            this.alpha = alpha;
            this.pts = pts;
        }
    }

    /// <summary>One structured piece of an armor design: an outline in reference px on a bone at a draw order, cel-shaded or cloth-driven.</summary>
    public sealed class ArmorPiece
    {
        public string name;
        public PieceKind kind;
        public B bone = B.Chest;
        public int order;
        public string tone;
        /// <summary>Flat pieces: which tone of the triple (0 shade, 1 mid, 2 lit).</summary>
        public int which = 1;
        public float alpha = 1f;
        /// <summary>Cel shadow and lit-edge depths in reference px.</summary>
        public float sd, hd;
        public bool rim = true;
        public float seam = 1.6f;
        /// <summary>HDR multiplier: above 1 the piece glows/blooms in the game.</summary>
        public float glow = 1f;
        /// <summary>True for far-arm armor pieces so they move to the front with the sword during sword techniques.</summary>
        public bool swordLayer;
        /// <summary>Cloth pieces: the <see cref="SoftPresets"/> feel ("Cape", "Plates", "Tails", "Strip", "Tassel").</summary>
        public string feel;
        /// <summary>x, y pairs in reference px (KageModel's frame).</summary>
        public float[] outline;
        /// <summary>Cloth pieces: spring chains (x, y pairs in reference px, root first).</summary>
        public float[][] chains;
        public ArmorDetail[] detail;
    }

    /// <summary>An armor's cel-shading palette: back-light rim and named (shade, mid, lit) tone triples.</summary>
    public sealed class ArmorPalette
    {
        public readonly Color rim;
        readonly Dictionary<string, Color[]> _tones = new();

        public ArmorPalette(string rim, Dictionary<string, string[]> tones)
        {
            this.rim = KMath.Hex(rim);
            foreach (var kv in tones)
                _tones[kv.Key] = new[] { KMath.Hex(kv.Value[0]), KMath.Hex(kv.Value[1]), KMath.Hex(kv.Value[2]) };
        }

        public IEnumerable<string> ToneNames => _tones.Keys;

        public bool Has(string tone) => tone != null && _tones.ContainsKey(tone);

        /// <summary>One colour of a tone: 0 shade, 1 mid, 2 lit.</summary>
        public Color Colour(string tone, int which)
        {
            if (!_tones.TryGetValue(tone, out var t))
                throw new ArgumentException("no tone " + tone + " in armor palette");
            return t[Mathf.Clamp(which, 0, 2)];
        }

        public OpponentKit.Tone Tone(string tone) => new(Colour(tone, 0), Colour(tone, 1), Colour(tone, 2));
    }

    /// <summary>
    /// A structured 5-tier armor design authored in <c>Tools/armor_art/armors.py</c> and generated into <c>ArmorDesigns.g.cs</c>.
    /// </summary>
    public sealed class ArmorDesign
    {
        public readonly string id, look;
        public readonly ArmorTier tier;
        public readonly ArmorAuraKind aura;
        public readonly ArmorPalette palette;
        public readonly ArmorPiece[] pieces;

        public ArmorDesign(string id, string look, ArmorTier tier, ArmorAuraKind aura, ArmorPalette palette, ArmorPiece[] pieces)
        {
            this.id = id;
            this.look = look;
            this.tier = tier;
            this.aura = aura;
            this.palette = palette;
            this.pieces = pieces;
        }
    }

    /// <summary>Every tiered armor design by JS armor id (generated: <c>ArmorDesigns.g.cs</c>).</summary>
    public static partial class ArmorDesigns
    {
        static Dictionary<string, ArmorDesign> _byId;

        static Dictionary<string, ArmorDesign> ById
        {
            get
            {
                if (_byId != null) return _byId;
                var d = new Dictionary<string, ArmorDesign>();
                foreach (var a in Generated) d[a.id] = a;
                return _byId = d;
            }
        }

        public static bool Has(string armorId) => armorId != null && ById.ContainsKey(armorId);

        /// <summary>The structured design, or null for the starter, retired or unknown armour.</summary>
        public static ArmorDesign Get(string armorId) => Has(armorId) ? ById[armorId] : null;

        public static IEnumerable<string> Ids => ById.Keys;

        public static IEnumerable<ArmorDesign> All => Generated;
    }
}
