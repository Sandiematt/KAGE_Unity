using System;
using Kage.Rules;

namespace Kage
{
    public enum ChapterState { Locked, Open, Cleared }

    public enum StageState { Locked, Open, Cleared }

    public sealed class ChapterView
    {
        public string Id, Name, Title, Place;
        public ChapterState State;
        public int Done, Total;
        public bool Selected;
    }

    public sealed class StageView
    {
        public string Id, Name, Kind;
        /// <summary>1-based position among the stages of its kind in the chapter (the numeral on a stretch node).</summary>
        public int Number;
        public StageState State;
        /// <summary>The story frontier: the first stage not yet cleared.</summary>
        public bool IsNext;
        public bool Selected;
        /// <summary>The name under the node: the foe's name for the boss, the stage name otherwise.</summary>
        public string Label;
        /// <summary>The small line under the label ("Stretch", "Main boss", "Stretch · Next"), or empty.</summary>
        public string TypeLine;
    }

    /// <summary>The stage card on the right of the map: what the selected stage is, what it pays and whether it can be started.</summary>
    public sealed class StageCard
    {
        public string StageId, Title, KindLine, Description, Action;
        public bool CanStart, Cleared, Locked;
        public int Coins, Gems;
        /// <summary>1 to 3 lit threat pips, and the word beside them.</summary>
        public int Threat;
        public string ThreatLabel;
        /// <summary>The act boss whose art card shows under the stage card ("raikon"), or null.</summary>
        public string BossArt;
    }

    /// <summary>
    /// What the campaign map shows, worked out from the save (JS renderCampaign): the chapters with their state, the stages
    /// of the open chapter with locked / open / cleared, the story frontier and the card of the selected stage. Plain data;
    /// the map screen only draws it.
    /// </summary>
    public sealed class CampaignView
    {
        public ChapterView[] Chapters;
        public ChapterView Chapter;
        public StageView[] Stages;
        public StageCard Card;

        static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        static readonly string[] ThreatLabels = { "Calm", "Tense", "Fierce", "Brutal", "Mythic" };
        static readonly string[] ActBoss = { "act1", "raikon" };

        public static string RomanOf(int n) { return n >= 1 && n <= Roman.Length ? Roman[n - 1] : n.ToString(); }

        public static string KindName(string kind)
        {
            switch (kind)
            {
                case "stretch": return "Stretch";
                case "subboss": return "Sub-boss";
                case "boss": return "Main boss";
                default: return kind;
            }
        }

        /// <summary>
        /// Builds the view of <paramref name="chapterId"/> (falls back to the first chapter). With no stage selected the card
        /// shows the first open stage of the chapter, or its first stage when nothing is open.
        /// </summary>
        public static CampaignView Build(SaveData save, string chapterId, string selectedStageId)
        {
            var chapters = GameRules.Chapters;
            var open = GameRules.Chapter(chapterId) ?? chapters[0];
            string next = GameRules.NextStage(save);

            var v = new CampaignView { Chapters = new ChapterView[chapters.Length] };
            for (int i = 0; i < chapters.Length; i++)
            {
                var c = chapters[i];
                var p = GameRules.ChapterProgress(save, c.Id);
                v.Chapters[i] = new ChapterView
                {
                    Id = c.Id, Name = c.Name, Title = c.Title, Place = c.Place,
                    Done = p.Done, Total = p.Total,
                    Selected = c.Id == open.Id,
                    State = GameRules.IsChapterComplete(save, c.Id) ? ChapterState.Cleared
                        : GameRules.IsChapterUnlocked(save, c.Id) ? ChapterState.Open : ChapterState.Locked,
                };
                if (c.Id == open.Id) v.Chapter = v.Chapters[i];
            }

            JsStage selected = GameRules.Stage(selectedStageId);
            if (selected == null || selected.Chapter != open.Id)
            {
                selected = null;
                foreach (var st in open.Stages)
                    if (GameRules.IsStageUnlocked(save, st.Id) && !GameRules.IsStageComplete(save, st.Id)) { selected = st; break; }
                if (selected == null) selected = open.Stages[0];
            }

            v.Stages = new StageView[open.Stages.Length];
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            for (int i = 0; i < open.Stages.Length; i++)
            {
                var st = open.Stages[i];
                int n;
                counts.TryGetValue(st.Kind, out n);
                counts[st.Kind] = ++n;
                var state = GameRules.IsStageComplete(save, st.Id) ? StageState.Cleared
                    : GameRules.IsStageUnlocked(save, st.Id) ? StageState.Open : StageState.Locked;
                bool isNext = st.Id == next;
                string typeLine = isNext ? KindName(st.Kind) + " · Next" : st.Kind == "boss" ? "Main boss" : st.Kind == "stretch" ? "Stretch" : "";
                v.Stages[i] = new StageView
                {
                    Id = st.Id, Name = st.Name, Kind = st.Kind, Number = n, State = state, IsNext = isNext,
                    Selected = st.Id == selected.Id,
                    Label = st.Kind == "boss" ? (st.Foe ?? st.Name) : st.Name,
                    TypeLine = typeLine,
                };
            }

            v.Card = BuildCard(save, open, selected, v.Stages);
            return v;
        }

        static StageCard BuildCard(SaveData save, JsChapter ch, JsStage sel, StageView[] stages)
        {
            bool unlocked = GameRules.IsStageUnlocked(save, sel.Id), done = GameRules.IsStageComplete(save, sel.Id);
            var foe = JsTables.Enemy(sel.Enemy);
            int number = 0;
            foreach (var s in stages) if (s.Id == sel.Id) number = s.Number;
            bool numbered = (sel.Kind == "stretch" || sel.Kind == "subboss") && ch.Stages.Length > 1;

            string desc;
            if (sel.Kind == "stretch" && sel.Stretch != null)
            {
                int e = sel.Stretch.Encounters.Length;
                desc = e + (e == 1 ? " encounter holds the road." : " encounters hold the road.");
            }
            else
            {
                string name = sel.Foe ?? (foe != null ? foe.Name : sel.Name);
                string title = sel.FoeTitle ?? (foe != null ? foe.Title : null);
                desc = name + (string.IsNullOrEmpty(title) ? "" : ", " + title) + ".";
                if (foe != null && !string.IsNullOrEmpty(foe.Taunt)) desc += " “" + foe.Taunt + "”";
            }

            double lit = ch.Diff + (foe != null && foe.Type == "boss" ? 1 : foe != null && foe.Type == "sub" ? 0.5 : 0);
            int dl = (int)Math.Max(0, Math.Min(4, lit));
            var reward = GameRules.StageRewardPreview(save, sel.Id);
            return new StageCard
            {
                StageId = sel.Id,
                Title = sel.Kind == "boss" ? (sel.Foe ?? sel.Name) : sel.Name,
                KindLine = ch.Name + " · " + KindName(sel.Kind) + (numbered ? " " + RomanOf(number) : ""),
                Description = desc,
                Action = !unlocked ? "Locked" : done ? "Replay" : "Fight",
                CanStart = unlocked,
                Locked = !unlocked,
                Cleared = done,
                Coins = reward.Coins,
                Gems = reward.Gems,
                Threat = Math.Min(3, Math.Max(1, dl)),
                ThreatLabel = ThreatLabels[dl],
                BossArt = sel.Kind == "boss" && ch.Id == ActBoss[0] ? ActBoss[1] : null,
            };
        }
    }
}
