using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Which of the HUD's parts a fight shows (SAN-108): the JS game's three arrangements.</summary>
    public enum HudLayout
    {
        /// <summary>One foe: Kage's panel, the foe's panel on the right, the round clock and pips in the middle.</summary>
        Duel,
        /// <summary>A story boss: Kage's panel and the boss bar at the bottom in place of the foe's panel; no clock, no rounds.</summary>
        Boss,
        /// <summary>A road: Kage's panel and the road's progress strip in place of the clock; no foe panel, no boss bar.</summary>
        Stretch,
    }

    /// <summary>One round pip: empty, won by Kage or won by the foe.</summary>
    public enum HudPip { Empty, Kage, Foe }

    /// <summary>
    /// A fighter as the HUD sees it (SAN-108): plain values read from the fighter's public state each tick (<see cref="HudReader"/>). The
    /// view model works on these and never touches a <see cref="Fighter"/>, so a test drives the HUD with made-up numbers.
    /// </summary>
    public struct HudFighterState
    {
        /// <summary>The fighter's <see cref="Fighter.FighterId"/>: the key combat events are matched by.</summary>
        public string Id;
        public string Name, Title;
        /// <summary>Kage's level (the save's); 0 = none shown.</summary>
        public int Level;
        public float Health, MaxHealth;
        /// <summary>Game health per JS hit point (ADR-0010): the number on the bar is <c>Health / HealthScale</c>, the JS hp. 1 shows raw health.</summary>
        public float HealthScale;
        /// <summary>Rage (the shadow meter), 0..100.</summary>
        public float Rage;
        /// <summary>The fighter has a rage art to ready (<see cref="CharacterFlags.CanRageArt"/>): only then can the bar say "Rage ready".</summary>
        public bool CanRageArt;
        /// <summary>The fighter throws kunai, and how many it has left.</summary>
        public bool HasKunai;
        public int Kunai;
        /// <summary>Posture 0..1 (<see cref="Fighter.PostureFill"/>) and whether the guard is broken.</summary>
        public float Posture;
        public bool GuardBroken;
        /// <summary>True while Kage's active Parry or Perfect Dodge timing window is open (drives the minimal HUD timing dots).</summary>
        public bool InTimingWindow;
        /// <summary>The combo this fighter is landing.</summary>
        public int Combo;
        public int RoundsWon;
    }

    /// <summary>The road a stretch is walking, as the HUD sees it (<see cref="StretchSession"/>'s public readout).</summary>
    public struct HudRoadState
    {
        public bool Active;
        /// <summary>Kage's place on the road, 0..1.</summary>
        public float Progress;
        /// <summary>Where each encounter sits on the road, 0..1 (the strip's marks), in road order.</summary>
        public float[] EncounterAt;
        public int EncountersCleared, EncounterCount;
        /// <summary>A gate is shut: the wave and the foes left read instead of the encounter count.</summary>
        public bool InEncounter;
        public int Wave, WaveCount, FoesLeft;
        /// <summary>The stretch is over (cleared or failed).</summary>
        public bool Finished;
    }

    /// <summary>A small health bar over a foe's head (JS <c>drawMiniBar</c>): where, and how full.</summary>
    public struct HudMiniBar
    {
        public Vector2 World;
        public float Ratio;
    }

    /// <summary>What the HUD reads each tick, built by <see cref="HudReader"/> or by hand in a test.</summary>
    public struct HudFrame
    {
        public HudLayout Layout;
        public HudFighterState Player;
        /// <summary>The foe's panel (or the boss's health); <see cref="HasFoe"/> false hides it (the training dojo).</summary>
        public HudFighterState Foe;
        public bool HasFoe;
        public float TimeLeft;
        /// <summary>The format has a clock; <see cref="ClockRuns"/> says whether it counts (false reads an infinity sign).</summary>
        public bool ClockShown, ClockRuns;
        public bool RoundsShown;
        public int RoundsToWin;
        public HudRoadState Road;
        /// <summary>The foes' small bars in a stretch; null or empty = none.</summary>
        public IReadOnlyList<HudMiniBar> MiniBars;
        /// <summary>The rage art's opening on Kage's target (<see cref="HudReader.RageCue"/>): the cue over a boss that would take the art now.</summary>
        public HudRageCue RageCue;
    }

    /// <summary>
    /// Kage's Rage is full and his target, a boss that reads rage arts, is open to one (<see cref="Fighter.OpenToRageArt"/>): now is the time.
    /// <see cref="World"/> is the point over its head the cue sits on.
    /// </summary>
    public struct HudRageCue
    {
        public bool On;
        public Vector2 World;
    }

    /// <summary>What a boss bar needs to know about its boss (from <see cref="BossDef"/> and the character data).</summary>
    public sealed class HudBossInfo
    {
        public string Id, Name, Title;
        /// <summary>The art card's UI image id (<c>Resources/UI/Img</c>), null = no card (the JS bosses without painted art have none).</summary>
        public string ArtId;
        /// <summary>The boss's accent colour (JS <c>accent</c>): the card's aura and corner diamond.</summary>
        public Color Accent = new Color(0.784f, 0.137f, 0.184f);
        /// <summary>Where each later phase opens, as a health fraction (the dividers on the track).</summary>
        public float[] Marks = new float[0];
        /// <summary>The phases' names, in order (the JS phase names).</summary>
        public string[] PhaseNames = new string[0];
        public int PhaseCount => PhaseNames != null && PhaseNames.Length > 0 ? PhaseNames.Length : Marks.Length + 1;
    }
}
