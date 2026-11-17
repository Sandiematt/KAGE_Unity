using System.Collections.Generic;
using Kage.Rules;
using UnityEngine;

namespace Kage
{
    /// <summary>What a character is allowed or immune to. Read only through <see cref="Fighter.Has"/>, so a boss phase can change them.</summary>
    [System.Flags]
    public enum CharacterFlags
    {
        None = 0,
        Armour = 1,          // light hits don't flinch (Ironclad, Garan, Raikon) — SAN-102
        NoGrab = 2,          // throws fail (Raikon, Shiden) — SAN-102
        FrontalArmour = 4,   // hits from the front don't flinch (Shieldbearer) — SAN-98
        CanRageArt = 8,      // Special + Down with full Rage (Kage)
        CanThrowKunai = 16,  // Special + Down kunai (Kage) — SAN-88
    }

    /// <summary>A character's effect colours: trails, hit bursts, dash ghosts, shadow glow. Its part set also uses them as its trim.</summary>
    [System.Serializable]
    public struct FighterColors
    {
        public Color main, core, dark;
        public FighterColors(Color main, Color core, Color dark) { this.main = main; this.core = core; this.dark = dark; }
    }

    /// <summary>
    /// Character data (ADR-0005): everything that makes one fighter different from another. A fighter is
    /// built from it by <see cref="FightBuilder.Spawn(CharacterDef,int,IInputSource)"/>. Entries live in
    /// Character/Roster (one file each) and are registered in <see cref="CharacterCatalog"/>.
    /// </summary>
    public sealed class CharacterDef
    {
        public string id;                 // "kage", "ronin", or the JS ENEMIES id: "order_footman", "kuroe", "raikon", ...
        public string displayName;        // HUD name ("Order Footman")
        public string title;              // HUD title ("Order of Silence", "the Lightning Ascendant")

        // ---- stats ----
        /// <summary>JS health before the health scale (ADR-0010). Kage: <see cref="GameRules.PlayerMaxHp"/> (232 on a fresh save).</summary>
        public float jsHealth;
        /// <summary>Per-character pacing knob on top of the shared health scale (SAN-110).</summary>
        public float healthMul = 1f;
        /// <summary>Damage reduction 0..0.5 (JS <c>def</c>). Applied by the damage formula (ADR-0006, SAN-70).</summary>
        public float defense;
        /// <summary>Multiplier on the damage this character deals (JS <c>dmg</c>). Applied by the damage formula (ADR-0006, SAN-70).</summary>
        public float damageMul = 1f;
        /// <summary>Locomotion speed: walk, run, dash, jump distance (JS <c>speed</c>). Not the move rate.</summary>
        public float speedMul = 1f;
        /// <summary>Attack playback rate (Footman 0.9). Applied by fractional move playback (ADR-0006, SAN-70).</summary>
        public float moveRate = 1f;
        /// <summary>Posture bar size; larger for bosses. Used by the posture bar (SAN-86).</summary>
        public float postureMax = 100f;
        /// <summary>Rigidbody2D mass; knockback impulses are divided by it (SAN-68).</summary>
        public float mass = 1f;
        /// <summary>Extra multiplier on knockback received, on top of mass.</summary>
        public float knockbackTaken = 1f;
        /// <summary>Uniform scale of the whole fighter (Ronin 1.04, Raikon about 1.24).</summary>
        public float bodyScale = 1f;

        // ---- look ----
        /// <summary>Id of the part set that draws this character (<see cref="CharacterCatalog.PartSet"/>).</summary>
        public string partSet;
        public FighterColors colors;

        // ---- fighting ----
        /// <summary>Weapon family id (ADR-0006): "katana", "twin", "spear", "chain", "cleaver", "fists", "tachi".</summary>
        public string family = "katana";
        /// <summary>JS weapon id for stats and drawing; null = the family's reference weapon.</summary>
        public string weaponId;
        /// <summary>"regular", "agile", "defensive", "ranged", "heavy", "boss"; null for the player.</summary>
        public string aiProfile;
        /// <summary>Ability ids this character may use, in slot order (ADR-0008).</summary>
        public List<string> abilities = new();
        /// <summary>BossDef id when the character has phases (ADR-0008); else null.</summary>
        public string bossId;
        public CharacterFlags flags;

        // ---- gameplay skeleton ----
        /// <summary>Hurt circles on the 20-bone skeleton; null = Kage's layout, scaled by <see cref="bodyScale"/>.</summary>
        public List<HurtDef> hurt;

        /// <summary>True for the player's character (no AI profile).</summary>
        public bool IsPlayer => aiProfile == null;

        /// <summary>Health at fight start: JS health × healthMul × the shared health scale (ADR-0010).</summary>
        public float MaxHealth() => (float)((double)jsHealth * healthMul * Tuning.Get<HealthTuning>().healthScale);

        /// <summary>A copy that can be changed without touching the original (lists are copied too).</summary>
        public CharacterDef Clone()
        {
            var c = (CharacterDef)MemberwiseClone();
            c.abilities = abilities != null ? new List<string>(abilities) : null;
            c.hurt = hurt != null ? new List<HurtDef>(hurt) : null;
            return c;
        }

        /// <summary>
        /// A character with the JS stats of an ENEMIES entry (raw <c>hp</c>, <c>def</c>, <c>dmg</c>, <c>speed</c>, name,
        /// title, AI archetype, weapon, build scale, <c>heavyArmor</c> and <c>noGrab</c>; ADR-0010). The character file adds
        /// the Unity values: part set, family, mass, posture, move rate, colours. Null when the id is unknown.
        /// </summary>
        public static CharacterDef FromJs(string enemyId)
        {
            var e = JsTables.Enemy(enemyId);
            if (e == null) return null;
            var c = new CharacterDef
            {
                id = e.Id,
                displayName = e.Name,
                title = e.Title,
                jsHealth = e.Hp,
                defense = (float)e.Def,
                damageMul = (float)e.Dmg,
                speedMul = (float)e.Speed,
                bodyScale = e.Build != null ? (float)e.Build.Scale : 1f,
                weaponId = e.Weapon,
                aiProfile = e.Ai,
                abilities = new List<string>(e.Abilities ?? new string[0]),
            };
            if (e.HeavyArmor) c.flags |= CharacterFlags.Armour;
            if (e.NoGrab) c.flags |= CharacterFlags.NoGrab;
            return c;
        }
    }
}
