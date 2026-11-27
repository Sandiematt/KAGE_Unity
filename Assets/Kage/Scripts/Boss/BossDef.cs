using System;
using System.Collections.Generic;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// One phase of a boss fight (ADR-0008): it opens when the boss's health fraction falls to <see cref="at"/> and then sets the
    /// boss's ability list, AI profile, multipliers and flags. A phase is <b>absolute</b>: its multipliers and flags apply to the
    /// boss's <see cref="CharacterDef"/> (not to the previous phase), so a phase can be read on its own. The AI profile is the one
    /// exception: <c>aiProfile</c> null keeps the current profile.
    /// </summary>
    public sealed class PhaseDef
    {
        /// <summary>The JS phase name ("The Storm Gathers"), published in <c>BossPhaseChanged</c> and shown by the boss bar.</summary>
        public string name;
        /// <summary>The health fraction (0..1) at or below which the phase opens (JS <c>at</c>). The first phase is 1.</summary>
        public float at = 1f;
        /// <summary>Ability ids usable in this phase (JS <c>abilities</c>); null allows every ability of the character.</summary>
        public string[] abilities;
        /// <summary>An <see cref="AiProfiles"/> id to switch to; null keeps the current profile.</summary>
        public string aiProfile;
        /// <summary>Multiplies <see cref="CharacterDef.speedMul"/> (walk, run, dash, jump distance).</summary>
        public float speedMul = 1f;
        /// <summary>Multiplies <see cref="CharacterDef.moveRate"/>: how fast the boss's attacks play (not how fast it walks).</summary>
        public float moveRateMul = 1f;
        /// <summary>Multiplies <see cref="CharacterDef.damageMul"/>.</summary>
        public float damageMul = 1f;
        /// <summary>Multiplies every ability cooldown while the phase lasts (with <see cref="berserk"/>'s on top): a sub-boss's rank (<see cref="SubBossTier"/>).</summary>
        public float cooldownMul = 1f;
        /// <summary>Flags added to and removed from <see cref="CharacterDef.flags"/> while the phase lasts (armour, no-grab).</summary>
        public CharacterFlags addFlags, removeFlags;
        /// <summary>JS flag: the AI is bolder (<see cref="AIInputSource.aggressive"/>, SAN-95).</summary>
        public bool aggressive;
        /// <summary>JS flag: abilities cool down faster (<see cref="PhaseController.BerserkCooldownMul"/>).</summary>
        public bool berserk;
        /// <summary>JS <c>secondWave</c>: the delay in seconds of Thunder Swing's second wave; 0 = none. Read from <see cref="Fighter.SecondWaveDelay"/>.</summary>
        public float secondWave;
        /// <summary>
        /// The move the boss plays the moment the phase opens, out of whatever it was doing (a forced move, <see cref="Fighter.QueueForcedMove"/>): Raikon's
        /// Storm Kindling, Ascension and Final Overload. Never played for the first phase or after a reset. Null: none.
        /// </summary>
        public MoveDef enterMove;
        /// <summary>
        /// The wind on both fighters' rigs while the phase lasts (<see cref="FighterRig.wind"/>), in the boss's own frame: +x is the way he faces when
        /// the phase opens. Ramped in from the last phase's wind over <see cref="windRampFrames"/>. Null keeps the wind as it is.
        /// </summary>
        public Wind? wind;
        /// <summary>Frames the wind takes to reach this phase's (0 = at once).</summary>
        public int windRampFrames;
        /// <summary>Called when the phase is applied (the first phase on spawn and on every reset too), with the boss and the phase index: the persistent look of a phase.</summary>
        public Action<Fighter, int> onEnter;
    }

    /// <summary>
    /// A boss's phase data (ADR-0008), the entry <see cref="CharacterDef.bossId"/> points at. Start from <see cref="FromJs"/> (names, thresholds,
    /// ability lists, aggressive, berserk and second wave come from the generated JS table, ADR-0010), set the Unity side (profile swaps,
    /// multipliers, flags) on the phases and <see cref="BossCatalog.Register"/> it.
    /// </summary>
    public sealed class BossDef
    {
        /// <summary>The same id as the boss's <see cref="CharacterDef.id"/> ("kuroe", "raikon").</summary>
        public string id;
        /// <summary>Boss bar text: the name ("Raikon") and title ("the Lightning Ascendant").</summary>
        public string name, title;
        /// <summary>UI image id of the boss bar's art card (SAN-67 imports the JS art cards); null = none.</summary>
        public string artCard;
        /// <summary><c>phases[0].at</c> is 1 and <c>at</c> strictly decreases.</summary>
        public List<PhaseDef> phases = new();

        public int Count => phases.Count;

        /// <summary>The boss of a JS ENEMIES entry that has phases; null when the id is unknown or has none. The phases are copies, change them freely.</summary>
        public static BossDef FromJs(string enemyId)
        {
            var e = JsTables.Enemy(enemyId);
            if (e == null || e.Phases == null || e.Phases.Length == 0) return null;
            var b = new BossDef { id = e.Id, name = e.Name, title = e.Title };
            foreach (var p in e.Phases)
                b.phases.Add(new PhaseDef
                {
                    name = p.Name, at = (float)p.At,
                    abilities = p.Abilities != null ? (string[])p.Abilities.Clone() : null,
                    aggressive = p.Aggressive, berserk = p.Berserk, secondWave = (float)p.SecondWave,
                });
            b.Validate();
            return b;
        }

        /// <summary>Throws when the phases are not a list of strictly falling thresholds starting at 1.</summary>
        public void Validate()
        {
            if (phases == null || phases.Count == 0) throw new InvalidOperationException($"boss {id}: no phases");
            if (phases[0].at != 1f) throw new InvalidOperationException($"boss {id}: the first phase must open at 1, not {phases[0].at}");
            for (int i = 1; i < phases.Count; i++)
                if (!(phases[i].at < phases[i - 1].at && phases[i].at > 0f))
                    throw new InvalidOperationException($"boss {id}: phase {i} opens at {phases[i].at}, which does not fall below phase {i - 1} ({phases[i - 1].at})");
        }
    }

    /// <summary>
    /// Every boss, by id (ADR-0008): one line per boss in <see cref="Register"/>'s callers. A boss with no entry of its own uses the plain
    /// JS data (<see cref="BossDef.FromJs"/>: abilities and flags per phase, no profile or multiplier changes), so Raikon and the sub-bosses
    /// work from the table until their ticket tunes them.
    /// </summary>
    public static class BossCatalog
    {
        static Dictionary<string, BossDef> _defs;

        static void Ensure()
        {
            if (_defs != null) return;
            _defs = new Dictionary<string, BossDef>();
            // ---- one line per tuned boss ----
            Register(FootmanBoss.Boss());          // the test boss (SAN-102)
            Register(Kuroe.Boss());                // Kuroe: Bōjutsu calligraphy ink kit + Raikon's Thunder Redaction
            Register(Garan.Boss());                // Garan: Karate · Sumō kit + broken vanguard lightning (SAN-104)
            Register(Hisame.Boss());               // Hisame: Tessenjutsu rain-mist kit + Raikon's Inazuma Step (SAN-105)
            Register(Shiden.Boss());               // Shiden: Hōzōin-ryū sōjutsu kit + Vanguard Raijin Arc (SAN-106)
            Register(Raikon.Boss());               // Raikon: the Lightning Ascendant's three phases and the Final Overload
        }

        /// <summary>Adds or replaces a boss (validated).</summary>
        public static void Register(BossDef d)
        {
            if (_defs == null) { Ensure(); }
            d.Validate();
            _defs[d.id] = d;
        }

        public static bool Unregister(string id) { Ensure(); return _defs.Remove(id); }

        /// <summary>The registered boss, else the plain JS one, else null. Shared: don't change it (a phase controller only reads it).</summary>
        public static BossDef Get(string id)
        {
            Ensure();
            if (id == null) return null;
            if (_defs.TryGetValue(id, out var d)) return d;
            d = BossDef.FromJs(id);
            if (d != null) _defs[id] = d;
            return d;
        }
    }
}
