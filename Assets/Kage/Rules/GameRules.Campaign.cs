using System;
using System.Collections.Generic;

namespace Kage.Rules
{
    /// <summary>Something a stage clear opened: the next chapter, or an item (Kind is "weapons", "helmets" or "armors").</summary>
    public sealed class StageUnlock
    {
        public string Kind, Id;
    }

    /// <summary>What marking a stage cleared did (JS completeStage).</summary>
    public sealed class StageClear
    {
        public string Stage;
        public bool FirstClear;
        /// <summary>The story frontier after the clear: the first uncleared stage, or null.</summary>
        public string Next;
        /// <summary>Items the stage's reward granted (each also appears in <see cref="Unlocks"/>).</summary>
        public List<StageUnlock> Gear = new List<StageUnlock>();
        public List<StageUnlock> Unlocks = new List<StageUnlock>();
    }

    public struct ChapterProgressInfo
    {
        public int Done, Total;
    }

    // The pure rules of the JS game's Logic module (src/js/15-logic.js) for the Prologue and Act I: stage unlocks, rewards,
    // rating, buy / upgrade / equip, gear stats. No Unity types: it works on plain SaveData and the generated JS tables.
    // Out of scope (ADR-0009, spec): New Game+, difficulties other than Normal, Echo Trials, Boss Rush, scenes, clues.
    public static partial class GameRules
    {
        // ---------------- save ----------------

        /// <summary>
        /// A fresh save (JS defaultSave): 250 coins, 5 gems, level 1, owning and wearing the starter gear at level 1.
        /// Dev mode only sets the flag; the dev grants are SAN-64's.
        /// </summary>
        public static SaveData DefaultSave(bool devMode)
        {
            var s = new SaveData { devMode = devMode };
            s.owned.weapons["kagetsu"] = 1;
            s.owned.helmets["shiro_maki"] = 1;
            s.owned.armors["tachibana_yoroi"] = 1;
            return s;
        }

        // ---------------- campaign map ----------------
        // The map is the generated JS campaign: chapters in story order, stages in play order, each with a stable id.
        // Progress is save.cleared[stageId]. Stages unlock strictly in story order (a stage opens once the one before it
        // is cleared) and cleared stages stay open for replay. Dev mode opens every stage, in any order.

        public static JsChapter[] Chapters { get { return JsTables.Chapters; } }

        /// <summary>Every stage of the Prologue and Act I in play order.</summary>
        public static JsStage[] Stages { get { return JsTables.Stages; } }

        public static JsChapter Chapter(string id) { return JsTables.Chapter(id); }

        public static JsStage Stage(string id) { return JsTables.Stage(id); }

        public static JsChapter ChapterOf(string stageId)
        {
            var st = Stage(stageId);
            return st == null ? null : Chapter(st.Chapter);
        }

        public static bool IsStageComplete(SaveData save, string id)
        {
            bool done;
            return save.cleared.TryGetValue(id, out done) && done;
        }

        public static bool IsStageUnlocked(SaveData save, string id)
        {
            var stages = Stages;
            int i = Array.FindIndex(stages, s => s.Id == id);
            if (i < 0) return false;
            return save.devMode || i == 0 || IsStageComplete(save, id) || IsStageComplete(save, stages[i - 1].Id);
        }

        public static bool IsChapterUnlocked(SaveData save, string id)
        {
            var c = Chapter(id);
            return c != null && IsStageUnlocked(save, c.Stages[0].Id);
        }

        public static bool IsChapterComplete(SaveData save, string id)
        {
            var c = Chapter(id);
            return c != null && Array.TrueForAll(c.Stages, s => IsStageComplete(save, s.Id));
        }

        public static ChapterProgressInfo ChapterProgress(SaveData save, string id)
        {
            var c = Chapter(id);
            return new ChapterProgressInfo
            {
                Done = Array.FindAll(c.Stages, s => IsStageComplete(save, s.Id)).Length,
                Total = c.Stages.Length,
            };
        }

        /// <summary>The story frontier: the id of the first uncleared stage, or null when the scope is finished.</summary>
        public static string NextStage(SaveData save)
        {
            foreach (var st in Stages) if (!IsStageComplete(save, st.Id)) return st.Id;
            return null;
        }

        /// <summary>Lifetime wins on a stage.</summary>
        public static int DefeatedCount(SaveData save, string id)
        {
            int n;
            return save.defeated.TryGetValue(id, out n) ? n : 0;
        }

        /// <summary>
        /// Marks a stage cleared (a won fight) and returns what that opened, or null when the stage is locked or unknown.
        /// JS completeStage.
        /// </summary>
        public static StageClear MarkStageCleared(SaveData save, string id)
        {
            var st = Stage(id);
            if (st == null || !IsStageUnlocked(save, id)) return null;
            bool firstClear = !IsStageComplete(save, id);
            var clear = new StageClear { Stage = id, FirstClear = firstClear };
            save.cleared[id] = true;
            save.defeated[id] = DefeatedCount(save, id) + 1;
            clear.Next = NextStage(save);
            if (firstClear)
            {
                var chapters = Chapters;
                int ci = Array.FindIndex(chapters, c => c.Id == st.Chapter);
                if (ci >= 0 && ci + 1 < chapters.Length && IsChapterComplete(save, st.Chapter))
                    clear.Unlocks.Add(new StageUnlock { Kind = "chapter", Id = chapters[ci + 1].Id });
            }
            clear.Gear.AddRange(GrantStageReward(save, st));
            clear.Unlocks.AddRange(clear.Gear);
            return clear;
        }

        /// <summary>
        /// Gear a stage gives when it is cleared. Each item is granted exactly once, at level 1, while the player
        /// does not own it; an item flagged NgOnly only drops in New Game+ (save.ng > 0). JS grantStageReward.
        /// </summary>
        public static List<StageUnlock> GrantStageReward(SaveData save, JsStage stage)
        {
            var got = new List<StageUnlock>();
            if (stage == null || stage.Reward == null) return got;
            Grant(save, GearKind.Weapons, stage.Reward.Weapons, got);
            Grant(save, GearKind.Helmets, stage.Reward.Helmets, got);
            Grant(save, GearKind.Armors, stage.Reward.Armors, got);
            return got;
        }

        /// <summary>
        /// Weapons kept out of the game for now (the owner: Shinsen comes only after Act 2 or 3, and the other boss blades wait too).
        /// The tables keep their rows; a stage reward never grants them and the Armory never lists them. Delete an id to turn it back on.
        /// </summary>
        static readonly HashSet<string> WithheldWeapons = new HashSet<string> { "shinsen", "raiko_no_tachi", "kagehira_yurei", "mugen_no_ha" };

        public static bool IsWithheld(GearKind kind, string id)
        {
            return kind == GearKind.Weapons && id != null && WithheldWeapons.Contains(id);
        }

        static void Grant(SaveData save, GearKind kind, string[] ids, List<StageUnlock> got)
        {
            foreach (var id in ids)
            {
                var it = Item(kind, id);
                if (it == null || IsWithheld(kind, id) || Owns(save, kind, id) || (it.NgOnly && save.ng <= 0)) continue;
                OwnedOf(save, kind)[id] = 1;
                got.Add(new StageUnlock { Kind = KindName(kind), Id = id });
            }
        }
    }
}
