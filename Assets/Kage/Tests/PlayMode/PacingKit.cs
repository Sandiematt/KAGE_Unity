using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// What the pacing report (SAN-110) shares: the gear a player can realistically own at each stage of the Prologue and Act I, and the
    /// fights that measure time-to-kill.
    ///
    /// <para><b>Expected gear</b> (<see cref="Loadouts"/>). A new save plays the ten stages in order, every one a B-rated first clear (what the
    /// campaign rules test uses), through the real rules (<see cref="GameRules.CompleteStage"/>). Before each stage the player spends every
    /// coin on Kagetsu upgrades (the only weapon he can afford early, and the lever that raises damage), and wears the best helmet and armour
    /// he owns (most defence, then most health), which is how the sub-bosses' drops get on. Gems are not spent. Coins therefore run 250, 365,
    /// 480, 735, 850, 1105, 1220, 1475, 1590, 1845 at the ten stages and the player level follows the XP of the clears.</para>
    ///
    /// <para><b>Measuring convention</b>. Time-to-kill (TTK) is measured with <see cref="Helped"/> Kage: the same gear and damage the player has,
    /// with <see cref="SurvivalHelp"/> times the health, so the clock reads Kage's real damage against the foe's real health and a bot that
    /// would have died is not what ends the run. Alongside it the same fights are played with the real Kage (no help) and the report gives
    /// how often that bot wins. Kage is played by <see cref="AIInputSource"/> against regular foes and waves (the stretch bot) and by the
    /// sub-boss's own JS-tip bot against a sub-boss (the bots of the four bot fight tests).</para>
    /// </summary>
    internal static class PacingKit
    {
        public const float SurvivalHelp = 20f;
        public const float KageAiLevel = 0.9f;
        public const int FightCap = 60 * 240, RoadCap = 60 * 900, BossCap = 60 * 300;

        public static readonly string[] Order =
        {
            "prologue.1", "act1.stretch1", "act1.sub1", "act1.stretch2", "act1.sub2", "act1.stretch3", "act1.sub3", "act1.stretch4", "act1.sub4", "act1.boss",
        };

        // ---------------- expected gear ----------------

        public sealed class Loadout
        {
            public string Stage, Weapon, Helmet, Armor;
            public int WeaponLevel, HelmetLevel, ArmorLevel, Level, Coins, Gems;
            public CharacterDef Kage;
            public string Text =>
                $"Kage lv{Level}: {Weapon} L{WeaponLevel}, {Helmet} L{HelmetLevel}, {Armor} L{ArmorLevel} (JS hp {Kage.jsHealth:0}, def {Kage.defense:0.00}, dmg x{Kage.damageMul:0.000}), {Coins} coins held";
        }

        static string BestWorn(SaveData save, GearKind kind)
        {
            string best = null;
            double bestDef = -1, bestHp = -1;
            foreach (var kv in GameRules.OwnedOf(save, kind))
            {
                if (kv.Value <= 0) continue;
                double def = GameRules.GearDef(kind, kv.Key, kv.Value), hp = GameRules.GearHp(kind, kv.Key, kv.Value);
                if (def > bestDef || (def == bestDef && hp > bestHp)) { best = kv.Key; bestDef = def; bestHp = hp; }
            }
            return best;
        }

        /// <summary>The gear Kage walks into each stage with, in play order (<see cref="Order"/>).</summary>
        public static List<Loadout> Loadouts()
        {
            var save = GameRules.DefaultSave(false);
            var list = new List<Loadout>();
            foreach (var id in Order)
            {
                while (GameRules.UpgradeItem(save, GearKind.Weapons, "kagetsu")) { }
                GameRules.EquipItem(save, GearKind.Helmets, BestWorn(save, GearKind.Helmets));
                GameRules.EquipItem(save, GearKind.Armors, BestWorn(save, GearKind.Armors));
                var e = save.equipped;
                list.Add(new Loadout
                {
                    Stage = id, Level = save.level, Coins = save.coins, Gems = save.gems,
                    Weapon = e.weapon, WeaponLevel = save.owned.weapons[e.weapon],
                    Helmet = e.helmet, HelmetLevel = save.owned.helmets[e.helmet],
                    Armor = e.armor, ArmorLevel = save.owned.armors[e.armor],
                    Kage = KageCharacter.Def(save),
                });
                GameRules.CompleteStage(save, id, new FightResult { win = true, hpLeft = 0.8, time = 40, maxCombo = 4 });
            }
            return list;
        }

        public static Loadout LoadoutOf(string stage) => Loadouts().First(l => l.Stage == stage);

        /// <summary>The same Kage with <see cref="SurvivalHelp"/> times the health and his damage untouched.</summary>
        public static CharacterDef Helped(CharacterDef kage)
        {
            var d = kage.Clone();
            d.healthMul *= SurvivalHelp;
            return d;
        }

        // ---------------- one regular foe ----------------

        public sealed class Result
        {
            public bool Won;           // the foe (or the stretch, or the boss) was beaten
            public int Frames;
            public double HpLeft;      // Kage's health left, 0..1
            public double Seconds => Frames / 60.0;
            /// <summary>Seconds each stretch wave was alive, by the session's wave number (stretches only).</summary>
            public SortedDictionary<int, int> WaveFrames = new SortedDictionary<int, int>();
        }

        public static Result OneFoe(string foeId, int seed, CharacterDef kage, float level)
        {
            using var f = new HeadlessFight(seed, Foe.Ai, -1.2f, 1.2f, foeDef: CharacterCatalog.Get(foeId), kageDef: kage);
            f.Ai.level = level;
            f.KageInput.fallback = new AIInputSource { level = KageAiLevel };
            f.KageInput.Add(1);
            bool Over() => f.Kage.state == FState.KO || f.Ronin.state == FState.KO;
            f.RunUntil(Over, FightCap);
            return new Result { Won = f.Ronin.state == FState.KO, Frames = f.Frames.Count, HpLeft = f.Kage.health / f.Kage.maxHealth };
        }

        // ---------------- one stretch ----------------

        public static Result Stretch(string stage, int seed, CharacterDef kage)
        {
            using var f = new StretchFight(seed, StretchData.FromId(stage), kage, foeLevel: StageWorld.FoeLevel(StageWorldKind.Road));
            bool Over() => f.Session.Cleared || f.Session.Failed || f.Kage.state == FState.KO;
            int start = -1, wave = 0;
            var waves = new SortedDictionary<int, int>();
            f.RunUntil(Over, RoadCap, () =>
            {
                int now = f.Frames.Count, left = f.Session.FoesLeft;
                if (start < 0 && left > 0) { start = now; wave = f.Session.StretchWave; }
                else if (start >= 0 && left == 0) { waves[wave] = now - start; start = -1; }
            });
            return new Result { Won = f.Session.Cleared, Frames = f.Frames.Count, HpLeft = f.Kage.health / f.Kage.maxHealth, WaveFrames = waves };
        }

        // ---------------- one sub-boss ----------------

        public static Result Boss(string stage, int seed, CharacterDef kage)
        {
            var st = GameRules.Stage(stage);
            using var f = new HeadlessFight(seed, Foe.Ai, -1.2f, 1.2f, foeDef: CharacterCatalog.Get(st.Enemy), kageDef: kage);
            f.Ai.level = StageWorld.FoeLevel(GameRules.WorldKindOf(stage));
            f.KageInput.fallback = SagaBotCampaignTests.BossBot(st.Enemy);
            f.KageInput.Add(1);
            bool Over() => f.Kage.state == FState.KO || f.Ronin.state == FState.KO;
            f.RunUntil(Over, BossCap);
            return new Result { Won = f.Ronin.state == FState.KO, Frames = f.Frames.Count, HpLeft = f.Kage.health / f.Kage.maxHealth };
        }

        // ---------------- the table ----------------

        public sealed class Band
        {
            public readonly double Lo, Hi;
            public Band(double lo, double hi) { Lo = lo; Hi = hi; }
            public bool Holds(double seconds) => seconds >= Lo && seconds <= Hi;
            public override string ToString() => $"{Lo:0}-{Hi:0} s";
        }

        /// <summary>Regular foe 15 to 25 s, a stretch wave about 45 s (read as 35 to 55 s), a sub-boss 60 to 90 s, Raikon 2 to 3 min.</summary>
        public static readonly Band Regular = new Band(15, 25), Wave = new Band(35, 55), SubBoss = new Band(60, 90), Raikon = new Band(120, 180);

        public sealed class Row
        {
            public string Name, Gear;
            public Band Target;
            public List<double> Ttk = new List<double>();     // seconds, helped runs that won
            public int HelpedRuns, RealRuns, RealWins;
            public List<int> WonSeeds = new List<int>();
            public double Mean => Ttk.Count > 0 ? Ttk.Average() : double.NaN;
            public bool InBand => Ttk.Count > 0 && Target.Holds(Mean);
            public string Line() =>
                $"{Name,-26} target {Target.ToString(),-9} TTK {Mean,6:0.0} s (min {(Ttk.Count > 0 ? Ttk.Min() : double.NaN):0.0}, max {(Ttk.Count > 0 ? Ttk.Max() : double.NaN):0.0}, killed {Ttk.Count}/{HelpedRuns})  " +
                $"{(InBand ? "IN " : "OUT")}  bot wins with real health {RealWins}/{RealRuns}{(WonSeeds.Count > 0 ? " (seeds " + string.Join(",", WonSeeds) + ")" : "")}  | {Gear}";
        }

        public static string Table(string title, IEnumerable<Row> rows)
        {
            var sb = new StringBuilder(title).AppendLine();
            foreach (var r in rows) sb.Append("  ").AppendLine(r.Line());
            return sb.ToString();
        }
    }
}
