using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Armour visuals, the second gear layer on Kage's part set (SAN-85, ADR-0005). An armour is drawn in code from its JS look
    /// (<c>21-characters/kage/armory</c> for the seven shop armours, the Act I sub-bosses' torsos for the two trophies): the shop
    /// armours are cuts of his own coat, scarf and belt (new tones, a cut hem with art along it, a chest piece, a shoulder guard,
    /// extra scarf strands); the trophies are plates over his chest, shoulder and hips. Everything is layered on the rig as parts of
    /// the bones it dresses (or appended to the coat's own cloth), so his skeleton, scarf simulation, coat cloth and hurt circles
    /// never change. An armour with no drawing (the starter Shinobi Garb, whose plain coat is his own design, and every later-act
    /// or retired one) leaves Kage exactly as he is. <see cref="KagePartSet"/> calls <see cref="Apply"/>.
    /// </summary>
    public static class ArmorVisuals
    {
        sealed class Look
        {
            public string name;                 // the JS look key
            public ArmorTheme theme;            // a recut of his coat, scarf and belt (shop armours)
            public Action<ArmorArt> draw;       // plates only (trophies)
        }

        // keyed by the JS armour id. The 9 Act I armours that have a drawing; the 10th, the starter, is Kage's plain design.
        static readonly Dictionary<string, Look> Looks = new()
        {
            ["ronin_yoroi"] = new Look { name = "ronin", theme = ArmorLooks.Ronin },
            ["yami_yoroi"] = new Look { name = "yami", theme = ArmorLooks.Yami },
            ["shinsen_yoroi"] = new Look { name = "shinsen", theme = ArmorLooks.Shinsen },
            ["raiko_yoroi"] = new Look { name = "raiko", theme = ArmorLooks.Raiko },
            ["kagehira_yoroi"] = new Look { name = "kagehira", theme = ArmorLooks.Kagehira },
            ["yurei_yoroi"] = new Look { name = "yurei", theme = ArmorLooks.Yurei },
            ["mugen_yoroi"] = new Look { name = "mugen", theme = ArmorLooks.Mugen },
            ["ikazuchi_do"] = new Look { name = "half_do", draw = ArmorLooks.HalfDo },
            ["shiden_yoroi"] = new Look { name = "vanguard_plate", draw = ArmorLooks.VanguardPlate },
        };

        /// <summary>True when this JS armour id has a drawing.</summary>
        public static bool Has(string armorId) => armorId != null && Looks.ContainsKey(armorId);

        /// <summary>The ids of every armour with a drawing.</summary>
        public static IEnumerable<string> Ids => Looks.Keys;

        /// <summary>
        /// Layers the armour on Kage's rig. Returns false and leaves the rig exactly as it was when the armour has no drawing
        /// (null, the starter, a later-act or retired armour, an unknown id).
        /// </summary>
        /// <param name="trim">Kage's trim colour in this rig (his character colours may have recoloured it).</param>
        /// <param name="trimDeep">Kage's deep trim colour in this rig.</param>
        public static bool Apply(RigDef rig, string armorId, Color trim, Color trimDeep)
        {
            if (!Has(armorId)) return false;
            var look = Looks[armorId];
            var art = new ArmorArt(rig, look.name);
            if (look.theme != null)
            {
                ArmorLooks.Retone(rig, look.theme, trim, trimDeep);
                ArmorLooks.Dress(art, look.theme);
            }
            else look.draw(art);
            art.Commit();
            return true;
        }
    }
}
