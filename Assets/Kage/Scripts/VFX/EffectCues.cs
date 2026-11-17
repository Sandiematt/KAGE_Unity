using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>What a cue needs to play. Set only the fields the cue uses.</summary>
    public struct CueArgs
    {
        public Fighter owner;
        /// <summary>Facing or travel direction (x is the flip; a zero vector means right).</summary>
        public Vector2 dir;
        public float scale;
        public Color? tint;
        /// <summary>A persistent cue (a projectile's flight, a hazard's ground mark) rides this transform and goes with it.</summary>
        public Transform follow;
        /// <summary>How long a timed cue lasts, in seconds (a projectile's life, a hazard's telegraph).</summary>
        public float seconds;
        /// <summary>The floor box of a hazard cue, in metres.</summary>
        public Vector2 size;
    }

    public delegate void CuePlayer(Vector2 pos, in CueArgs args);
    public delegate void CueObserver(string cueId, Vector2 pos, in CueArgs args);

    /// <summary>
    /// Named effects (ADR-0007). Gameplay code asks for a cue by id; what plays is whatever is
    /// registered for that id: a placeholder now, the real effect later. Playing a cue is a no-op when
    /// there is no <see cref="Vfx"/> (headless fights). An unregistered id plays a generic star and
    /// warns once. Each feature registers its own cues from its own file, so no ticket edits a shared table.
    /// <para>Ids are the JS VFX manifest id where the JS game has the effect (VFX_Raikon_Sky_Strike),
    /// else <c>VFX_Kage_&lt;Name&gt;</c>.</para>
    /// </summary>
    public static class EffectCues
    {
        static readonly Dictionary<string, CuePlayer> Players = new();
        static readonly HashSet<string> Warned = new();

        /// <summary>Raised on every <see cref="Play"/>, with or without a Vfx: tests and tools watch cues here.</summary>
        public static event CueObserver Played;

        public static void Register(string cueId, CuePlayer player) => Players[cueId] = player;
        public static void Unregister(string cueId) { Players.Remove(cueId); Warned.Remove(cueId); }
        public static bool IsRegistered(string cueId) => cueId != null && Players.ContainsKey(cueId);

        public static void Play(string cueId, Vector2 pos, in CueArgs args = default)
        {
            if (string.IsNullOrEmpty(cueId)) return;
            Played?.Invoke(cueId, pos, args);
            if (Vfx.I == null) return;
            if (Players.TryGetValue(cueId, out var player)) { player(pos, args); return; }
            if (Warned.Add(cueId)) Debug.LogWarning($"EffectCues: no cue registered for '{cueId}'; playing the placeholder star.");
            Color main = args.tint ?? (args.owner != null ? args.owner.fxMain : Color.white);
            Vfx.I.Star(pos, 0.5f * (args.scale > 0f ? args.scale : 1f), Color.white, main);
        }
    }
}
