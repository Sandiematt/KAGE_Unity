using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kage.Rules
{
    // The save object (ADR-0009): the JS default save, field for field, with the JS JSON names, plus `devMode`.
    // It is a plain object: the rules (GameRules) read and change it, SaveSerializer (SAN-64) turns it into JSON.
    // Fields of out-of-scope features (clues, scenes, story flags, boss fates, New Game+, trials, boss rush) are kept
    // and round-tripped but unused here.

    public sealed class OwnedItems
    {
        /// <summary>Weapon id to weapon level.</summary>
        public Dictionary<string, int> weapons = new Dictionary<string, int>();
        public Dictionary<string, int> armors = new Dictionary<string, int>();
    }

    public sealed class Equipped
    {
        public string weapon = GameRules.StarterWeaponId;
        public string hairstyle = Hairstyles.Default;
        public string armor = "tachibana_yoroi";
    }

    public sealed class BossRushBest
    {
        public int bosses;
        public double time;
    }

    public sealed class SaveStats
    {
        public int wins, losses, perfects, bosses;
        public double playTime;
    }

    public sealed class SaveSettings
    {
        public string shake = "full";
        public bool reduceFlash;
        public bool sound = true, music = true;
        public string touch = "auto", stick = "float";
    }

    public sealed class SaveData
    {
        /// <summary>JS SAVE_VERSION.</summary>
        public int v = 2;
        public int coins = 250, gems = 5, xp, level = 1;
        public int ng;
        public bool ngUnlocked;
        public int ngCompletions;
        /// <summary>Stage id to true: cleared this cycle.</summary>
        public Dictionary<string, bool> cleared = new Dictionary<string, bool>();
        /// <summary>Stage id to wins.</summary>
        public Dictionary<string, int> defeated = new Dictionary<string, int>();
        public OwnedItems owned = new OwnedItems();
        public Equipped equipped = new Equipped();
        public Dictionary<string, bool> seenRewards = new Dictionary<string, bool>();
        public Dictionary<string, Dictionary<string, bool>> clues = new Dictionary<string, Dictionary<string, bool>>();
        public Dictionary<string, bool> seenScenes = new Dictionary<string, bool>();
        public Dictionary<string, bool> storyFlags = new Dictionary<string, bool>();
        public Dictionary<string, string> bossFates = new Dictionary<string, string>();
        public string difficulty = "normal";
        public bool difficultyPicked;
        public int trialsBest;
        public BossRushBest bossRushBest = new BossRushBest();
        public SaveStats stats = new SaveStats();
        public SaveSettings settings = new SaveSettings();

        // Unity additions

        /// <summary>Dev mode (ADR-0009): every stage unlocked, every item owned, plenty of coins.</summary>
        public bool devMode;

        /// <summary>Fields this version doesn't know are kept as they are.</summary>
        [JsonExtensionData]
        public IDictionary<string, JToken> extra;
    }
}
