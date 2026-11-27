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
        /// <summary>
        /// A situation the AI also waits for (self, current target), on top of range, cooldown and phase (SAN-104: Garan clinches only a
        /// guard, the JS style's <c>when</c>). Null: any. The fighter itself doesn't ask it, so a scripted press starts the ability anyway.
        /// </summary>
        public System.Func<Fighter, Fighter, bool> when;
        /// <summary>
        /// A reaction (Raikon's Iron Guard Counter vs light-attack spam, Sky-Splitter vs a jump): the AI checks it on every frame it is free to act,
        /// not only when it next decides, so <see cref="when"/> is answered the moment it holds. Range, cooldown, phase and chance still apply.
        /// </summary>
        public bool reactive;
        /// <summary>
        /// An escape (Raikon's Afterimage Feint): once the fighter has taken this many blows in a row (<see cref="Fighter.HitsTakenInARow"/>), it starts the
        /// ability itself on its next frame, out of the hit-stun or through its armour (Fighter.Ascendant.cs). The AI never picks it in neutral. 0: not an escape.
        /// </summary>
        public int escapeAfterHits;
        /// <summary>How often the AI picks it among the abilities it could use, against the others' weights (1 = as often as any). Raikon's later phases weight their own kit up.</summary>
        public float weight = 1f;
        /// <summary>
        /// A boss string: the abilities the AI may go straight into when this one's move (or its <see cref="MoveDef.then"/> links) ends, in order of preference;
        /// the first that is enabled, ready, in its distance band and whose <see cref="when"/> holds is taken, after <see cref="chainBeat"/> frames, on a
        /// <see cref="chainChance"/> roll. Not after a knockdown, a KO or a blow that stunned the fighter. Null: none.
        /// </summary>
        public string[] chain;
        /// <summary>The chance a <see cref="chain"/> is followed, asked of the fighter (by boss phase); null = 0.6.</summary>
        public System.Func<Fighter, float> chainChance;
        /// <summary>Frames the AI holds between the end of this ability and the chained one (the string's beat), asked of the fighter; null = 8.</summary>
        public System.Func<Fighter, int> chainBeat;
        /// <summary>
        /// The <see cref="chain"/> is followed only when the move that just ended passes this (Raikon's Tsuba Press: only a whiffed shove, which ends as the
        /// press itself; a connected one became the cleave). Null: whatever ended.
        /// </summary>
        public System.Func<MoveDef, bool> chainIf;
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
        /// <summary>Every registered ability (for the tests that sweep every boss and sub-boss move).</summary>
        public static IEnumerable<AbilityDef> All => Defs.Values;
    }
}
