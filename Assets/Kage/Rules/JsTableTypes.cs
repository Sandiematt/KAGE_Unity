using System.Collections.Generic;

namespace Kage.Rules
{
    // Row types of the generated JS content tables (JsTables.g.cs, ADR-0010). Plain data: the export script fills them with
    // the values the JS game has once every module has loaded. A field the JS entry doesn't have keeps its default here.
    // Defaults mirror the JS reads: `dmg || 1`, `speed || 1`, a build's `legs`, `head` and `arms` are 1.

    /// <summary>One rarity step (JS GEAR_TIERS).</summary>
    public sealed class JsTier
    {
        public string Id;
        public int Rank;
        public string Label;
    }

    /// <summary>A weapon (JS WEAPONS), shop, trophy or enemy-only.</summary>
    public sealed class JsWeapon
    {
        public string Id, Name, Cls, Tab, Tier, Base, Stance, Hint, Desc, Lore;
        /// <summary>"0", "1", "2" or "dual".</summary>
        public string Hands;
        public int Len;
        /// <summary>[damage, speed, reach] at weapon level 1.</summary>
        public int[] Stats;
        public int Price, Gems;
        public bool Exclusive, Enemy;
    }

    /// <summary>A helmet or an armour (JS HELMETS, ARMORS).</summary>
    public sealed class JsGear
    {
        public string Id, Name, Tier, Look, Hue, Hint, GivenBy, Lore;
        public double Def;
        public int Hp, Price, Gems;
        public Dictionary<string, double> Perks;
        public bool Exclusive, Enemy, Retired, NgOnly;
    }

    public sealed class JsBuild
    {
        public double Scale = 1, W = 10, Legs = 1, Head = 1, Arms = 1;
    }

    /// <summary>A boss phase: starts when health falls to <see cref="At"/> (a fraction).</summary>
    public sealed class JsPhase
    {
        public string Name;
        public double At;
        public string[] Abilities = new string[0];
        public bool Aggressive, Berserk;
        public double SecondWave;
    }

    /// <summary>A foe (JS ENEMIES): the Prologue and Act I regular foes, the four sub-bosses, Raikon and the Hollow Husk.</summary>
    public sealed class JsEnemy
    {
        public string Id, Name, Title, Type, Family, Unit, Outfit, Ai, Weapon, Helmet, Armor, Eye, Accent, LongScarf;
        public string Defeat, Taunt, Tip, Lore, DefeatLine, Win;
        public int Rank, Hp;
        public double Def, Dmg = 1, Speed = 1, FlashEyes;
        public JsBuild Build = new JsBuild();
        public bool HeavyArmor, NoGrab, Tutorial;
        public JsPhase[] Phases = new JsPhase[0];
        public string[] Abilities = new string[0];
    }

    /// <summary>
    /// A boss ability the exported foes use (JS BOSS_ABILITIES: cooldown in seconds, distance band in pixels, pick chance) and, for an
    /// Act I sub-boss's reduced Raikon move (JS RAIKON_VARIANTS), the variant's numbers: the base move, its call-out, how long a counter
    /// stance is held, the punish delay, the damage multiplier, whether the punish breaks guards (null = the base move's), the wind-up
    /// fraction, the shockwave, the reach and dash, and whether a Thunder Swing sends its wave. Seconds and pixels as in the JS (ADR-0010).
    /// </summary>
    public sealed class JsBossAbility
    {
        public string Id, Base, Label;
        public double Cd, Chance;
        public double[] Range;
        public double Stance, Punish, Dmg, Windup, Shock, ShockLife, Reach, Dash;
        public bool? Unb, Wave;
    }

    /// <summary>One AI archetype (JS AI_PROFILES): a design brief for the Unity AI profiles.</summary>
    public sealed class JsAiProfile
    {
        public string Id;
        public double Pref, Aggr, Block, Parry, Dodge, Jump, Punish, Grab, Kunai, DashIn;
        public double[] React;
        public int[] Combo;
        public bool Ranged, Heavy;
    }

    public sealed class JsTutorialStep
    {
        public string Id, Verb, Hint;
    }

    /// <summary>Gear a stage grants the first time it is cleared (JS stage `reward`).</summary>
    public sealed class JsReward
    {
        public string[] Weapons = new string[0], Helmets = new string[0], Armors = new string[0];
    }

    public sealed class JsEncounter
    {
        /// <summary>Road position (JS pixels) where the encounter starts.</summary>
        public double X;
        /// <summary>Road position (JS pixels) of the gate that holds the player until the encounter is cleared.</summary>
        public double Gate;
        public double MimicIntro;
        /// <summary>Foe ids, one list per wave.</summary>
        public string[][] Waves = new string[0][];
    }

    public sealed class JsTutorialPrompt
    {
        public string Step;
        public double At;
    }

    public sealed class JsClue
    {
        public string Id, Label, Scene;
        public double X;
    }

    /// <summary>The road of a stretch stage. Distances are JS pixels; convert with <see cref="JsUnits"/>.</summary>
    public sealed class JsStretch
    {
        public double Length, Start;
        public string Env;
        public JsEncounter[] Encounters = new JsEncounter[0];
        public JsTutorialPrompt[] Tutorial = new JsTutorialPrompt[0];
        public JsClue[] Clues = new JsClue[0];
    }

    /// <summary>A campaign stage. Kind is "stretch", "subboss" or "boss" in this scope.</summary>
    public sealed class JsStage
    {
        public string Id, Kind, Chapter, Name, Enemy, Foe, FoeTitle, Format, Env;
        /// <summary>Position inside its chapter.</summary>
        public int Pos;
        public JsReward Reward;
        public string SceneBefore, SceneAfter, SceneDefeat;
        public JsStretch Stretch;
    }

    public sealed class JsChapter
    {
        public string Id, Name, Title, Place, Env;
        /// <summary>Chapter difficulty tier: scales coins and XP.</summary>
        public int Diff;
        public JsStage[] Stages = new JsStage[0];
    }

    /// <summary>Unit conversion from the JS game (ADR-0010).</summary>
    public static class JsUnits
    {
        /// <summary>JS pixels per metre (the JS body is about 200 px tall, Kage about 1.75 m).</summary>
        public const double PxPerMetre = 115;

        public static double PxToMetres(double px) { return px / PxPerMetre; }
    }
}
