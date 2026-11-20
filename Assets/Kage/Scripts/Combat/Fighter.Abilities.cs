using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Ability slots (SAN-95, ADR-0004, ADR-0008). <see cref="FrameInput.ability"/> = n asks for the n-th entry of
    /// <see cref="CharacterDef.abilities"/> (1-based, 0 = none). The request goes through the same rules as a button: it is read in
    /// <c>Neutral</c>, so a fighter that is busy, in the air or in a hit reaction ignores it, and it is dropped while the foe waits its
    /// turn (<see cref="Fighter.HoldBackIfWaiting"/>). On top of that an ability is ignored while it is cooling down, not enabled in the
    /// current boss phase, unknown to <see cref="AbilityCatalog"/> or without a move. The player never sets it.
    /// </summary>
    public partial class Fighter
    {
        readonly Dictionary<string, int> _abilityReady = new();   // id -> the clock frame it can be used again
        HashSet<string> _abilitiesEnabled;                        // null = every ability of the character

        /// <summary>The boss phase controller (SAN-102, ADR-0008); null unless the character has a <see cref="CharacterDef.bossId"/>.</summary>
        public PhaseController Phases { get; internal set; }

        /// <summary>Scales every ability's cooldown: 1 normally, <see cref="PhaseController.BerserkCooldownMul"/> in a berserk phase (JS berserk).</summary>
        public float AbilityCooldownMul = 1f;

        /// <summary>The current phase's <see cref="PhaseDef.secondWave"/>: seconds between Thunder Swing's first and second wave; 0 = no second wave. The hook SAN-107 and SAN-89 read.</summary>
        public float SecondWaveDelay;

        /// <summary>The character's abilities in slot order (slot n is index n - 1); empty for a fighter with none.</summary>
        public IReadOnlyList<string> AbilityIds => (IReadOnlyList<string>)Character?.abilities ?? System.Array.Empty<string>();

        /// <summary>Which abilities a boss phase allows (ADR-0008): only these ids can start. Null allows every ability of the character.</summary>
        public void SetEnabledAbilities(IEnumerable<string> ids)
        {
            _abilitiesEnabled = ids == null ? null : new HashSet<string>(ids);
        }

        public bool AbilityEnabled(string id) => _abilitiesEnabled == null || _abilitiesEnabled.Contains(id);

        /// <summary>The move ability <paramref name="id"/> starts, or null when it has none (see <see cref="AbilityDef"/>).</summary>
        public MoveDef AbilityMove(string id)
        {
            if (id == null) return null;
            var a = AbilityCatalog.Get(id);
            if (a?.move != null) return a.move;
            var set = moves;
            if (set == null) return null;
            return set.Ability(a?.moveId ?? id) ?? set.Ability(CharacterId + "." + id);
        }

        /// <summary>Frames until the ability can be used again (0 = ready).</summary>
        public int AbilityCooldownLeft(string id)
        {
            if (ctx == null || !_abilityReady.TryGetValue(id, out var at)) return 0;
            return System.Math.Max(0, at - ctx.Clock.Frame);
        }

        /// <summary>
        /// Whether slot <paramref name="slot"/> (1-based) could start now as far as the ability itself goes: a known, enabled ability with a move,
        /// off cooldown, and enough Shadow for its move. The fighter's own state is not asked (the AI asks <see cref="Busy"/> itself).
        /// </summary>
        public bool AbilityReady(int slot)
        {
            var ids = AbilityIds;
            if (slot < 1 || slot > ids.Count) return false;
            var id = ids[slot - 1];
            if (!AbilityEnabled(id) || AbilityCooldownLeft(id) > 0) return false;
            var m = AbilityMove(id);
            return m != null && meter >= m.meterCost;
        }

        /// <summary>Called from Neutral with this frame's input: starts the requested ability when it may. True when a move started.</summary>
        bool TryAbility(in FrameInput inp)
        {
            int slot = inp.ability;
            if (slot < 1 || !AbilityReady(slot)) return false;
            var id = AbilityIds[slot - 1];
            var m = AbilityMove(id);
            if (m?.variantFor != null) m = m.variantFor(this) ?? m;       // the last phase's two-wave swing, the shorter stance (SAN-107)
            m = Armed(m);                                         // a blade ability with the sword broken does nothing
            if (m == null) return false;
            var a = AbilityCatalog.Get(id);
            if (a != null && a.cooldownFrames > 0 && ctx != null) _abilityReady[id] = ctx.Clock.Frame + Mathf.RoundToInt(a.cooldownFrames * AbilityCooldownMul);
            StartMove(m);
            return true;
        }
    }
}
