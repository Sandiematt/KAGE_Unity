using System;
using System.Linq;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The Armory's state and actions, with no Unity types: which tab, row and item are selected, and Buy, Upgrade and Equip through
    /// the rules (<see cref="GameRules.BuyItem"/>, <see cref="GameRules.UpgradeItem"/>, <see cref="GameRules.EquipItem"/>). A
    /// successful action is saved at once and announced; a refused one changes nothing. <see cref="View"/> is what the screen draws.
    /// </summary>
    public sealed class ArmoryController
    {
        readonly SaveData save;
        readonly Action<SaveData> persist;
        readonly Action changed;
        string selected;
        ArmorLine? line;

        public ArmoryController(SaveData save, Action<SaveData> persist, Action changed)
        {
            this.save = save;
            this.persist = persist ?? (_ => { });
            this.changed = changed ?? (() => { });
        }

        public ArmoryTab Tab { get; private set; }

        public SaveData Save { get { return save; } }

        public ArmoryView View { get { return ArmoryView.Build(save, Tab, selected, line); } }

        /// <summary>The item the tab is on (never null: the worn item, or the row's first).</summary>
        public string SelectedId { get { return View.SelectedId; } }

        void Raise() { changed(); }

        /// <summary>Opens a tab. The selection goes back to the item worn in it.</summary>
        public void SelectTab(ArmoryTab tab)
        {
            Tab = tab;
            selected = null;
            line = null;
            Raise();
        }

        /// <summary>Q / E and the shoulder buttons: the next or previous tab, stopping at the ends.</summary>
        public void StepTab(int dir)
        {
            int t = Math.Max(0, Math.Min(2, (int)Tab + dir));
            if ((ArmoryTab)t != Tab) SelectTab((ArmoryTab)t);
        }

        /// <summary>Selects an item of the open tab (and row). Anything the tab doesn't list is ignored.</summary>
        public void Select(string id)
        {
            var v = View;
            if (id == null || v.Tiles.All(t => t.Id != id)) return;
            selected = id;
            Raise();
        }

        /// <summary>Switches the Armor tab's row.</summary>
        public void SetLine(ArmorLine l)
        {
            line = l;
            selected = null;
            Raise();
        }

        public bool Buy()
        {
            if (!GameRules.BuyItem(save, ArmoryView.KindOf(Tab), SelectedId)) return false;
            Persist();
            return true;
        }

        public bool Upgrade()
        {
            if (!GameRules.UpgradeItem(save, ArmoryView.KindOf(Tab), SelectedId)) return false;
            Persist();
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
