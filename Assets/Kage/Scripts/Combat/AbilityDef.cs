using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// A named special move a foe or boss can use (ADR-0008), chosen by its AI like a button press. A character lists the abilities it
    /// may use in <see cref="CharacterDef.abilities"/>; that order is the <b>ability slot</b>: <see cref="FrameInput.ability"/> = n starts
    /// the n-th entry (1-based, 0 = none). The ability is an ordinary <see cref="MoveDef"/>: hit windows, guard, defence order and events
    /// are the same as for any move. SAN-102 and SAN-103 to SAN-107 add the projectile and hazard fields (ADR-0007) and register the
    /// bosses' abilities.
    /// </summary>
    public sealed class AbilityDef
    {
        /// <summary>The ability's id, the entry in <see cref="CharacterDef.abilities"/>: the JS id ("thunderSwing") or a full one ("raikon.thunderSwing").</summary>
        public string id;
        /// <summary>The move it starts, written in a character file. Null: the family's <see cref="MoveSet.Ability"/> under <see cref="moveId"/>, <see cref="id"/> or "&lt;characterId&gt;.&lt;id&gt;".</summary>
        public MoveDef move;
        /// <summary>The id to ask <see cref="MoveSet.Ability"/> for when <see cref="move"/> is null; null = <see cref="id"/>.</summary>
        public string moveId;
        /// <summary>Frames before it can be used again, counted from the frame it starts (JS <c>cd</c> seconds x 60).</summary>
        public int cooldownFrames;
        /// <summary>The distance band in metres in which the AI uses it (JS <c>range</c> px / 115): x = nearest, y = furthest.</summary>
        public Vector2 range = new(0f, 99f);
        /// <summary>The chance per decision that the AI uses it when it is in range, ready and enabled (JS <c>chance</c>).</summary>
        public float chance = 1f;
    }

    /// <summary>
    /// Every registered <see cref="AbilityDef"/>, by id. Boss files register theirs (ADR-0008); a test registers a test ability and
    /// removes it again. A character's ability with no entry here is skipped by the AI and ignored by the fighter.
    /// </summary>
    public static class AbilityCatalog
    {
        static readonly Dictionary<string, AbilityDef> Defs = new();

        public static void Register(AbilityDef d) => Defs[d.id] = d;
        public static bool Unregister(string id) => Defs.Remove(id);
        public static AbilityDef Get(string id) => id != null && Defs.TryGetValue(id, out var d) ? d : null;
    }
}
