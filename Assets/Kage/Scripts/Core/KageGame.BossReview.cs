using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kage
{
    /// <summary>
    /// Dev arena keys for reviewing a boss's kit in a playtest (the Lightning Ascendant's expansion, 2026-11-27): without them a move only shows when
    /// the AI happens to pick it, and a phase only when the boss's health happens to cross it.
    /// <list type="bullet">
    /// <item><b>F11</b>: the boss drops to just under its next phase's threshold, so the phase opens the way it does in a fight (its entry move, its
    /// wind, its storm). On the last phase it says so.</item>
    /// <item><b>F12</b> / <b>Shift+F12</b>: the foe plays the next / previous of the abilities its current phase enables, newest first (the phase's own
    /// kit before the older one), in the phase's version of the move, facing Kage. A banner names it.</item>
    /// </list>
    /// </summary>
    public partial class KageGame
    {
        int _reviewIndex = -1;
        int _reviewPhase = -1;

        /// <summary>F11 and F12 (see the class notes). True when one of them was pressed.</summary>
        bool HandleBossReviewKeys(Keyboard kb)
        {
            if (enemy == null || player == null) return false;
            if (kb.f11Key.wasPressedThisFrame) { NextBossPhase(); return true; }
            if (kb.f12Key.wasPressedThisFrame) { ShowBossMove(kb.shiftKey.isPressed ? -1 : 1); return true; }
            return false;
        }

        /// <summary>Drops the boss just under its next phase's threshold; the phase controller opens it on its next tick, entry move and all.</summary>
        void NextBossPhase()
        {
            var phases = enemy.Phases;
            if (phases == null) { Hud?.Banner("NO PHASES (F11)", Color.white, 1.2f); return; }
            if (phases.Index + 1 >= phases.Count) { Hud?.Banner($"LAST PHASE: {phases.Current.name.ToUpperInvariant()} (F11)", Color.white, 1.4f); return; }
            var next = phases.Boss.phases[phases.Index + 1];
            enemy.health = Mathf.Max(1f, Mathf.Min(enemy.health, enemy.maxHealth * next.at - 1f));
            _reviewIndex = -1;
            Hud?.Banner($"PHASE {phases.Index + 2}: {next.name.ToUpperInvariant()} (F11)", Color.white, 1.4f);
        }

        /// <summary>The abilities the foe's current phase enables, newest first (reverse slot order: each phase's kit is appended after the last's).</summary>
        List<string> ReviewAbilities()
        {
            var ids = new List<string>();
            var all = enemy.AbilityIds;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var a = AbilityCatalog.Get(all[i]);
                if (a != null && a.move != null && enemy.AbilityEnabled(all[i])) ids.Add(all[i]);
            }
            return ids;
        }

        /// <summary>Queues the next (or previous) enabled ability as a forced move, the phase's version of it, facing Kage.</summary>
        void ShowBossMove(int step)
        {
            var ids = ReviewAbilities();
            if (ids.Count == 0) { Hud?.Banner("NO ABILITIES (F12)", Color.white, 1.2f); return; }
            int phase = enemy.Phases != null ? enemy.Phases.Index : 0;
            if (phase != _reviewPhase) { _reviewPhase = phase; _reviewIndex = -1; }
            _reviewIndex = ((_reviewIndex + step) % ids.Count + ids.Count) % ids.Count;
            var a = AbilityCatalog.Get(ids[_reviewIndex]);
            var m = a.move.variantFor?.Invoke(enemy) ?? a.move;
            float toKage = Mathf.Sign(player.Pos.x - enemy.Pos.x);
            if (toKage != 0f && !enemy.IsAttacking) enemy.SetFacing(toKage, true);
            enemy.QueueForcedMove(m);
            Hud?.Banner($"{_reviewIndex + 1}/{ids.Count}  {m.display.ToUpperInvariant()} (F12)", Color.white, 1.4f);
        }
    }
}
