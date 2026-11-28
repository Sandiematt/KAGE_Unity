using System.Collections.Generic;
using System.Linq;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// Whether an item is available in the current campaign.
    /// </summary>
    public enum ArmoryClass
    {
        /// <summary>(a) Drawn on Kage in fights, and the player can get it in the Prologue or Act I (starter, shop or an Act I reward).</summary>
        Implemented,
        /// <summary>(b) Planned for a later act: no drawing yet, or no way to get it before then.</summary>
        LaterAct,
        /// <summary>(c) A pure placeholder: enemy-only or retired JS gear that was never meant for the player.</summary>
        Placeholder,
    }

    /// <summary>Which weapons, hairstyles and armour the Armory lists, and why (<c>docs/armory-items.md</c> is this table in prose).</summary>
    public static class ArmoryListing
    {
        // A weapon whose family has no Unity implementation: it fights with another family's moves and drawing as a stand-in.
        // Iwakudaki has no hammer family yet (it fights with the cleaver's moves) but is drawn as its own hammer (CleaverVisual.Hammer),
        // so it is listed. None are stand-ins today.
        static readonly HashSet<string> StandIns = new HashSet<string>();

        /// <summary>Every table row of a kind that the player could ever hold (not enemy-only weapons, not bare hands), in table order.</summary>
        public static IEnumerable<GearItem> Candidates(GearKind kind)
        {
            IEnumerable<string> ids = kind == GearKind.Weapons
                ? JsTables.AllWeapons.Select(w => w.Id)
                : kind == GearKind.Hairstyles ? Hairstyles.Ids : JsTables.Armors.Select(g => g.Id);
            return ids.Select(id => GameRules.Item(kind, id));
        }

        public static ArmoryClass Classify(GearKind kind, string id)
        {
            var it = GameRules.Item(kind, id);
            if (it == null || it.Enemy || it.Retired) return ArmoryClass.Placeholder;
            if (GameRules.IsWithheld(kind, id) || !Drawn(it) || !Obtainable(it)) return ArmoryClass.LaterAct;
            return ArmoryClass.Implemented;
        }

        /// <summary>The Armory lists this item (bare hands are the fallback when no weapon is held, never an item).</summary>
        public static bool Listed(GearKind kind, string id)
        {
            return id != "fists" && Classify(kind, id) == ArmoryClass.Implemented;
        }

        /// <summary>True when the item has a drawing on Kage in a fight. The starter pieces are Kage's own design.</summary>
        public static bool Drawn(GearItem it)
        {
            switch (it.Kind)
            {
                case GearKind.Weapons:
                    return !StandIns.Contains(it.Id) && WeaponFamilies.Get(WeaponFamilies.FamilyIdOf(it.Id)) != null;
                case GearKind.Hairstyles:
                    return Hairstyles.Name(it.Id) != null;
                default:
                    return it.Tier == "starter" || ArmorVisuals.Has(it.Id);
            }
        }

        /// <summary>True when the player can get the item in the Prologue or Act I: the starter, the shop, or a stage reward of the tables (which hold only those two chapters).</summary>
        public static bool Obtainable(GearItem it)
        {
            if (!it.Exclusive && !it.NgOnly) return true;
            return GameRules.Stages.Any(s => s.Reward != null && Rewards(s.Reward, it.Kind).Contains(it.Id));
        }

        static string[] Rewards(JsReward r, GearKind kind)
        {
            var list = kind == GearKind.Weapons ? r.Weapons : r.Armors;
            return list ?? new string[0];
        }
    }
}
