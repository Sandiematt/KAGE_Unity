using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Reads the fight's public state into the plain values the HUD's view model works on (SAN-108): the one place the HUD touches a
    /// <see cref="Fighter"/> or a <see cref="StretchSession"/>, and only through their public fields and properties. Nothing here writes.
    /// </summary>
    public static class HudReader
    {
        /// <summary>Boss ids with a painted card in the JS game (<c>src/img/boss_*.webp</c>, converted to <c>Resources/UI/Img</c>).</summary>
        static readonly HashSet<string> PaintedCards = new() { "raikon", "akari", "takashi", "witness" };

        /// <summary>A fighter's panel values. <paramref name="name"/> replaces the character's display name when not empty (the group fight's "× 4").</summary>
        public static HudFighterState Fighter(Fighter f, string name = null, int level = 0)
        {
            if (f == null) return default;
            var def = f.Character;
            return new HudFighterState
            {
                Id = f.FighterId,
                Name = !string.IsNullOrEmpty(name) ? name : def != null ? def.displayName : f.fighterName,
                Title = def != null ? def.title : null,
                Level = level,
                Health = f.health,
                MaxHealth = f.maxHealth,
                HealthScale = HealthScale,
                Rage = f.meter,
                CanRageArt = f.Has(CharacterFlags.CanRageArt),
                HasKunai = f.Has(CharacterFlags.CanThrowKunai),
                Kunai = f.KunaiCount,
                Posture = f.PostureFill,
                GuardBroken = f.GuardBroken,
                InTimingWindow = f.isPlayer && (f.InParryWindow || f.InPerfectDodgeWindow),
                Combo = f.comboCount,
                RoundsWon = f.roundsWon,
            };
        }

        /// <summary>Game health per JS hit point (<c>health.json</c>, ADR-0010): the bars show JS hit points.</summary>
        public static float HealthScale
        {
            get
            {
                float s = (float)Tuning.Get<HealthTuning>().healthScale;
                return s > 0f ? s : 1f;
            }
        }

        /// <summary>The road a stretch is on.</summary>
        public static HudRoadState Road(StretchSession s)
        {
            if (s == null) return default;
            var at = new float[s.Data.Encounters.Length];
            float len = Mathf.Max(0.01f, s.Length);
            for (int i = 0; i < at.Length; i++) at[i] = Mathf.Clamp01((float)s.Data.Encounters[i].X / len);
            return new HudRoadState
            {
                Active = true,
                Progress = s.Progress,
                EncounterAt = at,
                EncountersCleared = s.EncountersCleared,
                EncounterCount = s.EncounterCount,
                InEncounter = s.GateClosed,
                Wave = s.WaveNumber,
                WaveCount = s.WaveCount,
                FoesLeft = s.FoesLeft,
                Finished = s.Cleared || s.Failed,
            };
        }

        /// <summary>The foes' small bars over their heads (JS <c>drawMiniBar</c>): every live foe of a stretch.</summary>
        public static void MiniBars(StretchSession s, List<HudMiniBar> into)
        {
            into.Clear();
            if (s == null) return;
            foreach (var f in s.Foes)
            {
                if (f == null || !f.gameObject.activeInHierarchy || f.state == FState.KO || f.maxHealth <= 0f) continue;
                float scale = f.Character != null ? f.Character.bodyScale : 1f;
                into.Add(new HudMiniBar { World = f.Pos + Vector2.up * (2.15f * scale), Ratio = Mathf.Clamp01(f.health / f.maxHealth) });
            }
        }

        /// <summary>
        /// The rage art's opening (Fighter.RageArtCounter.cs): on while <paramref name="kage"/> has a full Rage bar and can act or is finishing a
        /// move, and his target is a sub-boss or the boss that would take the art now; at the point over its head. Off for a foe that takes the art
        /// anywhere (it needs no cue) and during the art itself.
        /// </summary>
        public static HudRageCue RageCue(Fighter kage)
        {
            if (kage == null || !kage.Has(CharacterFlags.CanRageArt) || kage.meter < 100f || kage.health <= 0f || !kage.Grounded) return default;
            if (kage.state is FState.Hitstun or FState.Launched or FState.Down or FState.Thrown or FState.KO or FState.Ultimate or FState.Intro or FState.Victory) return default;
            var t = kage.opponent;
            if (t == null || !t.isActiveAndEnabled || t.health <= 0f || !t.CountersRageArts || !t.OpenToRageArt(kage)) return default;
            float scale = t.Character != null ? t.Character.bodyScale : 1f;
            return new HudRageCue { On = true, World = t.Pos + Vector2.up * (2.2f * scale) };
        }

        /// <summary>The boss bar's boss.</summary>
        public static HudBossInfo Boss(Fighter boss, BossDef def)
        {
            var info = new HudBossInfo { Id = boss != null ? boss.FighterId : def.id, Name = def.name, Title = def.title };
            info.PhaseNames = new string[def.Count];
            var marks = new List<float>();
            for (int i = 0; i < def.Count; i++)
            {
                info.PhaseNames[i] = def.phases[i].name ?? "";
                if (i > 0) marks.Add(def.phases[i].at);
            }
            info.Marks = marks.ToArray();
            string art = !string.IsNullOrEmpty(def.artCard) ? def.artCard : PaintedCards.Contains(def.id) ? "boss_" + def.id : null;
            info.ArtId = art;
            var js = Rules.JsTables.Enemy(def.id);
            if (js != null && !string.IsNullOrEmpty(js.Accent) && ColorUtility.TryParseHtmlString(js.Accent, out var c)) info.Accent = c;
            return info;
        }
    }
}
