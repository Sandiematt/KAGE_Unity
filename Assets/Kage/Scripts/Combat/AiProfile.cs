using System.Collections.Generic;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// What makes one kind of foe fight differently from another (SAN-95, ADR-0005): preferred range, aggression, guard and
    /// parry rate, the weights of its other choices, reaction time and combo length. There is one per JS archetype
    /// (regular, agile, defensive, ranged, heavy, boss), read from the generated JS table (<see cref="JsTables.AiProfiles"/>,
    /// ADR-0010), never typed by hand. A character picks one by id (<see cref="CharacterDef.aiProfile"/>); a boss phase can
    /// swap it (<see cref="AIInputSource.SetProfile"/>). Rates are chances per decision, 0..1, as in the JS game.
    /// <para>The AI reads a profile only through <see cref="AIInputSource"/>, so a profiled foe presses the same buttons a player does (ADR-0004).</para>
    /// </summary>
    public sealed class AiProfile
    {
        public string id;
        /// <summary>
        /// True for the profile that is no profile: the pre-SAN-95 utility AI, untouched (<see cref="AiProfiles.LegacyId"/>). The Ronin
        /// keeps it so the baseline replay and the dev arena's sparring partner behave as they always did.
        /// </summary>
        public bool legacy;

        /// <summary>The distance in metres it likes to fight at (JS <c>pref</c> px / 115). It walks in to this range and holds; a ranged foe backs off to it.</summary>
        public float preferredRange;
        /// <summary>How often it presses an attack when it is in range, per decision (JS <c>aggr</c>).</summary>
        public float aggression;
        /// <summary>How often it guards an attack that is about to reach it (JS <c>block</c>).</summary>
        public float guardRate;
        /// <summary>How often it tries to parry such an attack instead (JS <c>parry</c>). Rolled first, in the same decision as the guard.</summary>
        public float parryRate;
        /// <summary>How often it steps out of an attack instead (a backflip, JS <c>dodge</c>); also how readily a ranged foe breaks away.</summary>
        public float dodgeRate;
        /// <summary>The weight of a jump-in from range (JS <c>jump</c>).</summary>
        public float jumpRate;
        /// <summary>How often it counters an opening: the opponent's recovery, or after it guarded a blow (JS <c>punish</c>).</summary>
        public float punishRate;
        /// <summary>The weight of a throw when close (JS <c>grab</c>).</summary>
        public float grabRate;
        /// <summary>The weight of its ranged Special from a distance (JS <c>kunai</c>).</summary>
        public float kunaiRate;
        /// <summary>The weight of a dash-in with a strike from range (JS <c>dashIn</c>; only the agile profile has one).</summary>
        public float dashInRate;
        /// <summary>The delay before it answers an attack it has seen, in JS-timing frames (JS <c>react</c> seconds x 60). <see cref="AiTuning.reactionScale"/> scales it to Unity's quicker moves.</summary>
        public int reactMinFrames, reactMaxFrames;
        /// <summary>How many attacks a string runs, drawn once per string (JS <c>combo</c>): the first attack and up to (n - 1) chained on hit.</summary>
        public int comboMin = 1, comboMax = 1;
        /// <summary>Keeps its distance and shoots (JS <c>ranged</c>).</summary>
        public bool ranged;
        /// <summary>Slow to decide (JS <c>heavy</c>: decision gaps x <see cref="AiTuning.heavyThinkMul"/>).</summary>
        public bool heavy;
        /// <summary>Gets more careful, not bolder, when hurt (the defensive and ranged archetypes): below <see cref="AiTuning.lowHealthFraction"/> of health its aggression drops by <see cref="AiTuning.lowHealthBoldness"/>; every other profile gains <see cref="AiTuning.lowHealthRashness"/>.</summary>
        public bool cautious;

        /// <summary>The profile of a JS archetype row. Distances go from pixels to metres (ADR-0010), times from seconds to frames.</summary>
        public static AiProfile FromJs(JsAiProfile j)
        {
            return new AiProfile
            {
                id = j.Id,
                preferredRange = (float)JsUnits.PxToMetres(j.Pref),
                aggression = (float)j.Aggr,
                guardRate = (float)j.Block,
                parryRate = (float)j.Parry,
                dodgeRate = (float)j.Dodge,
                jumpRate = (float)j.Jump,
                punishRate = (float)j.Punish,
                grabRate = (float)j.Grab,
                kunaiRate = (float)j.Kunai,
                dashInRate = (float)j.DashIn,
                reactMinFrames = (int)System.Math.Round(j.React[0] * 60.0),
                reactMaxFrames = (int)System.Math.Round(j.React[1] * 60.0),
                comboMin = j.Combo[0],
                comboMax = j.Combo[1],
                ranged = j.Ranged,
                heavy = j.Heavy,
                cautious = j.Id == "defensive" || j.Id == "ranged",
            };
        }

        /// <summary>A copy to change freely (tests, a boss phase's variant).</summary>
        public AiProfile Clone() => (AiProfile)MemberwiseClone();
    }

    /// <summary>
    /// Every AI profile, by id (<see cref="CharacterDef.aiProfile"/>). The six JS archetypes are built from the generated table
    /// the first time one is asked for; <see cref="LegacyId"/> is the one profile that is not from it.
    /// </summary>
    public static class AiProfiles
    {
        /// <summary>The id of the pre-SAN-95 behaviour (the Ronin).</summary>
        public const string LegacyId = "legacy";

        static Dictionary<string, AiProfile> _byId;

        static void Ensure()
        {
            if (_byId != null) return;
            _byId = new Dictionary<string, AiProfile>();
            foreach (var j in JsTables.AiProfiles) _byId[j.Id] = AiProfile.FromJs(j);
            _byId[LegacyId] = new AiProfile { id = LegacyId, legacy = true };
        }

        /// <summary>A copy of the profile with this id (change it freely), or null for null or an unknown id.</summary>
        public static AiProfile Get(string id)
        {
            Ensure();
            return id != null && _byId.TryGetValue(id, out var p) ? p.Clone() : null;
        }

        /// <summary>The ids of the JS archetypes and the legacy profile.</summary>
        public static IEnumerable<string> Ids { get { Ensure(); return _byId.Keys; } }
    }
}
