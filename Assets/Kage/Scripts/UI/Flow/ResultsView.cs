using System.Collections.Generic;
using System.Text;
using Kage.Rules;

namespace Kage
{
    public sealed class ResultButton
    {
        public string Key, Label;
    }

    public sealed class ResultToast
    {
        public string Title, Text;
    }

    /// <summary>
    /// What the results screen shows (JS showResult), worked out from the save after the stage was applied, the rules' outcome and
    /// the fight result. Plain data; <see cref="ResultsScreen"/> only draws it.
    /// </summary>
    public sealed class ResultsView
    {
        public bool Win;
        public string StageId, Eyebrow, Title, Line, Tip;
        public string[] SealNames;
        public bool[] Seals;
        /// <summary>True for a win: the seals, rating and rewards. A loss shows the recap and the tip instead.</summary>
        public bool ShowRewards;
        public string Rating;
        public int Coins, Gems;
        public List<string> Items = new List<string>();
        /// <summary>The drawing of each weapon or armor in the rewards.</summary>
        public List<TileArt> ItemArt = new List<TileArt>();
        public List<ResultToast> Toasts = new List<ResultToast>();
        public int Level, Xp, XpNeed, XpGain;
        public bool LevelUp;
        public double XpBasePct, XpGainPct;
        /// <summary>Loss recap: whole percent of the foe's health taken.</summary>
        public int EnemyDamagePct;
        public ResultButton[] Buttons;
        public string PrimaryKey { get { return Buttons[0].Key; } }

        const string DefaultTip = "Block, then punish the recovery.";

        public static ResultsView Build(SaveData save, StageOutcome o, FightResult r)
        {
            var stage = GameRules.Stage(o.Stage);
            var foe = stage != null ? JsTables.Enemy(stage.Enemy) : null;
            bool win = o.Win;
            bool stretch = stage != null && stage.Kind == "stretch";
            string foeName = stage != null && !stretch && !string.IsNullOrEmpty(stage.Foe) ? stage.Foe : foe != null ? foe.Name : "";
            var v = new ResultsView { StageId = o.Stage, Win = win, ShowRewards = win };

            v.Eyebrow = (stage != null ? GameRules.ChapterOf(o.Stage).Name + " · " + stage.Name : "");
            v.Title = win ? "Victory" : "Defeated";
            v.SealNames = new[] { "Victory", "Under 60s", "Flawless" };
            v.Seals = new[] { win, win && r.time < 60, win && r.flawless };
            v.Rating = o.Rating.ToString();
            v.Coins = o.Coins;
            v.Gems = o.Gems;
            v.EnemyDamagePct = (int)System.Math.Round(System.Math.Max(0, System.Math.Min(1, r.enemyDamage)) * 100, System.MidpointRounding.AwayFromZero);
            v.Tip = foe != null && !string.IsNullOrEmpty(foe.Tip) ? foe.Tip : DefaultTip;

            if (stretch) v.Line = win ? "The road is walked." : "The road still bars the way. Rise again.";
            else if (win)
            {
                string tail = foe != null && foe.Defeat == "escape" ? "escaped. The way opens."
                    : foe != null && foe.Defeat == "reabsorb" ? "is part of you again."
                    : foe != null && foe.Type == "boss" ? "has fallen. The way opens." : "has fallen.";
                v.Line = foeName + " " + tail;
            }
            else v.Line = foeName + " still stands. Rise again.";

            // experience: the save already holds the gain
            v.Level = save.level;
            v.Xp = save.xp;
            v.XpNeed = GameRules.XpForLevel(save.level);
            v.XpGain = o.Xp;
            v.LevelUp = o.LevelUps > 0;
            v.XpGainPct = v.LevelUp ? (double)save.xp / v.XpNeed * 100 : System.Math.Min(100, (double)o.Xp / v.XpNeed * 100);
            v.XpBasePct = v.LevelUp ? 0 : System.Math.Max(0, (double)(save.xp - o.Xp) / v.XpNeed * 100);

            if (win)
            {
                foreach (var u in o.Unlocks)
                {
                    if (u.Kind == "chapter")
                    {
                        var c = GameRules.Chapter(u.Id);
                        v.Toasts.Add(new ResultToast { Title = "New chapter unlocked", Text = c != null ? c.Name + " · " + c.Title : u.Id });
                        continue;
                    }
                    var item = GameRules.Item(GameRules.KindFromName(u.Kind), u.Id);
                    string name = item != null ? Clean(item.Name) : u.Id;
                    v.Items.Add(name);
                    v.ItemArt.Add(GearArt.For(GameRules.KindFromName(u.Kind), u.Id));
                    v.Toasts.Add(new ResultToast { Title = u.Kind == "weapons" ? "Boss weapon" : "Trophy", Text = name });
                }
            }
            if (v.LevelUp) v.Toasts.Add(new ResultToast { Title = "Level up", Text = "Kage reaches level " + save.level });

            v.Buttons = win
                ? new[] { new ResultButton { Key = "continue", Label = "Continue" }, new ResultButton { Key = "retry", Label = "Replay" } }
                : new[] { new ResultButton { Key = "retry", Label = "Retry" }, new ResultButton { Key = "armory", Label = "Upgrade" }, new ResultButton { Key = "continue", Label = "Map" } };
            return v;
        }

        /// <summary>JS item names can carry stray control characters; they are dropped.</summary>
        static string Clean(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) if (c >= 32) sb.Append(c);
            return sb.ToString();
        }
    }
}
