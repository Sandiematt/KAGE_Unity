using System;
using System.Collections.Generic;

namespace Kage.Rules
{
    /// <summary>The kind of world a campaign stage is played in (SAN-109): a road with a session, or a boss fight in an arena.</summary>
    public enum StageWorldKind
    {
        /// <summary>A stretch stage (the Prologue and the four Act I stretches): the road, its gates and its waves.</summary>
        Road,
        /// <summary>A sub-boss stage (Kuroe, Garan, Hisame, Shiden): one boss fight in the boss format, in the sub-boss's arena.</summary>
        SubBoss,
        /// <summary>The Act I boss stage (Raikon): the same, in the boss's own arena.</summary>
        Boss,
    }

    public static partial class GameRules
    {
        /// <summary>
        /// What the world of a stage is built as, from the stage's kind in the generated JS campaign (stretch is a road, subboss and boss
        /// are fights). Throws for a stage the campaign doesn't have or a kind this scope doesn't play (scene, echo).
        /// </summary>
        public static StageWorldKind WorldKindOf(string stageId)
        {
            var st = Stage(stageId);
            if (st == null) throw new ArgumentException("unknown stage: " + stageId, "stageId");
            switch (st.Kind)
            {
                case "stretch": return StageWorldKind.Road;
                case "subboss": return StageWorldKind.SubBoss;
                case "boss": return StageWorldKind.Boss;
                default: throw new ArgumentException("stage " + stageId + " is a '" + st.Kind + "' stage, which this scope doesn't play", "stageId");
            }
        }

        /// <summary>
        /// The id of the JS environment a boss stage is fought in: the stage's own <c>env</c>, else its chapter's (JS <c>st.env || ch.env</c>).
        /// Null for a stretch (its road names its own).
        /// </summary>
        public static string BossEnvOf(string stageId)
        {
            var st = Stage(stageId);
            if (st == null || st.Kind == "stretch") return null;
            return !string.IsNullOrEmpty(st.Env) ? st.Env : ChapterOf(stageId)?.Env;
        }

        /// <summary>
        /// Every character id a stage plays, in the order they first appear: a road's waves (with the Prologue's additions and its practice
        /// partner, <see cref="StretchData.FromId"/>), or the boss. Each must be a character of the catalog (SAN-109 test).
        /// </summary>
        public static IReadOnlyList<string> StageFoeIds(string stageId)
        {
            var ids = new List<string>();
            void Add(string id) { if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id); }
            switch (WorldKindOf(stageId))
            {
                case StageWorldKind.Road:
                    var road = StretchData.FromId(stageId);
                    foreach (var e in road.Encounters) foreach (var w in e.Waves) foreach (var id in w) Add(id);
                    foreach (var p in road.Prompts) Add(p.Partner);
                    break;
                default:
                    Add(Stage(stageId).Enemy);
                    break;
            }
            return ids;
        }
    }
}
