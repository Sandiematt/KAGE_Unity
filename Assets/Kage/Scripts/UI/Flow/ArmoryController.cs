using System;
using System.Linq;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The Armory's state and actions, with no Unity types: which tab and item are selected, and Buy, Upgrade and Equip through
    /// the rules (<see cref="GameRules.BuyItem"/>, <see cref="GameRules.UpgradeItem"/>, <see cref="GameRules.EquipItem"/>). A
    /// successful action is saved at once and announced; a refused one changes nothing. <see cref="View"/> is what the screen draws.
    /// </summary>
    public sealed class ArmoryController
    {
        readonly SaveData save;
        readonly Action<SaveData> persist;
        readonly Action changed;
        readonly Action<string, string> toast;
        string selected;

        public ArmoryController(SaveData save, Action<SaveData> persist, Action changed, Action<string, string> toast = null)
        {
            this.toast = toast ?? ((a, b) => { });
            this.save = save;
            this.persist = persist ?? (_ => { });
            this.changed = changed ?? (() => { });
        }

        public ArmoryTab Tab { get; private set; }

        public SaveData Save { get { return save; } }

        public ArmoryView View { get { return ArmoryView.Build(save, Tab, selected); } }

        /// <summary>The item the tab is on (never null: the worn item, or the list's first).</summary>
        public string SelectedId { get { return View.SelectedId; } }

        void Raise() { changed(); }

        /// <summary>Opens a tab. The selection goes back to the item worn in it.</summary>
        public void SelectTab(ArmoryTab tab)
        {
            Tab = tab;
            selected = null;
            Raise();
        }

        /// <summary>Q / E and the shoulder buttons: the next or previous tab, stopping at the ends.</summary>
        public void StepTab(int dir)
        {
            int t = Math.Max(0, Math.Min(2, (int)Tab + dir));
            if ((ArmoryTab)t != Tab) SelectTab((ArmoryTab)t);
        }

        /// <summary>
        /// Selects an item of the open tab: what the player does by clicking its tile or pressing confirm on it. Focus and hover never
        /// call this, so pointing at a tile changes nothing. Anything the tab doesn't list is ignored.
        /// </summary>
        public void Select(string id)
        {
            var v = View;
            if (id == null || v.Tiles.All(t => t.Id != id)) return;
            selected = id;
            if (Tab == ArmoryTab.Hairstyles && save.equipped.hairstyle != id)
            {
                GameRules.EquipItem(save, GearKind.Hairstyles, id);
                persist(save);
            }
            Raise();
        }

        public bool Buy()
        {
            if (!GameRules.BuyItem(save, ArmoryView.KindOf(Tab), SelectedId)) return false;
            Persist();
            toast("Acquired", GameRules.Item(ArmoryView.KindOf(Tab), SelectedId).Name);
            return true;
        }

        public bool Upgrade()
        {
            if (!GameRules.UpgradeItem(save, ArmoryView.KindOf(Tab), SelectedId)) return false;
            Persist();
            var kind = ArmoryView.KindOf(Tab);
            int lvl;
            GameRules.OwnedOf(save, kind).TryGetValue(SelectedId, out lvl);
            toast("Level " + lvl, GameRules.Item(kind, SelectedId).Name + " tempered");
            return true;
        }

        public bool Equip()
        {
            if (!GameRules.EquipItem(save, ArmoryView.KindOf(Tab), SelectedId)) return false;
            Persist();
            return true;
        }

        void Persist()
        {
            // keep the selection on the item just acted on, whatever the row's order does
            selected = SelectedId;
            persist(save);
            Raise();
        }
    }
}
