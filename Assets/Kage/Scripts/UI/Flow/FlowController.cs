using System;
using Kage.Rules;

namespace Kage
{
    /// <summary>The screens of the campaign flow (ADR-0001, SAN-67). A pause is not a screen: it is <see cref="FlowController.Paused"/> on Stage.</summary>
    public enum FlowScreen
    {
        Title,
        Campaign,
        Loadout,
        Stage,
        Results,
        Settings,
        /// <summary>The Armory (SAN-90): reached from the title, the loadout and the results, and back to where it came from.</summary>
        Armory,
    }

    /// <summary>
    /// The rules of the campaign flow, with no Unity types: title, campaign map, pre-fight loadout, stage, results and
    /// back to the map, plus pause, settings and the dev-mode switch. <see cref="GameFlow"/> shows the screen this says
    /// and builds the fights; every decision about where the player goes next is made here, so it is tested on plain
    /// save objects. The save is changed through the rules (GameRules) and handed to <c>persist</c> after every change
    /// that matters.
    /// </summary>
    public sealed class FlowController
    {
        readonly Action<SaveData> persist;

        public FlowController(SaveData save, Action<SaveData> persist)
        {
            Save = save;
            this.persist = persist ?? (_ => { });
            SelectedStage = FrontierOrLast();
        }

        /// <summary>The save in play. Turning dev mode off replaces it, so don't keep the old object.</summary>
        public SaveData Save { get; private set; }

        public FlowScreen Screen { get; private set; } = FlowScreen.Title;

        /// <summary>The stage the map cursor is on (it may be locked: locked stages can be looked at).</summary>
        public string SelectedStage { get; private set; }

        /// <summary>The stage of the loadout, the fight and the results; null on the title and the map.</summary>
        public string StageId { get; private set; }

        /// <summary>Counts every start of a fight (a new stage, a retry). The host builds a fresh fight when it changes.</summary>
        public int Attempt { get; private set; }

        /// <summary>The fight is stopped and the pause menu is up (Stage only).</summary>
        public bool Paused { get; private set; }

        /// <summary>What the last finished stage earned; null before the first one.</summary>
        public StageOutcome LastOutcome { get; private set; }

        public FightResult LastResult { get; private set; }

        /// <summary>Turning dev mode off was asked for and waits for the player's answer.</summary>
        public bool DevOffPending { get; private set; }

        /// <summary>The Armory's state while it is open (SAN-90); null on every other screen.</summary>
        public ArmoryController Armory { get; private set; }

        /// <summary>The screen the Armory was opened from (Title, Loadout or Results): Back returns there.</summary>
        public FlowScreen ArmoryFrom { get; private set; }

        /// <summary>Raised after anything the screens show changed (screen, selection, pause, save).</summary>
        public event Action Changed;

        void Raise() { var c = Changed; if (c != null) c(); }

        void Persist() { persist(Save); }

        string FrontierOrLast()
        {
            return GameRules.NextStage(Save) ?? GameRules.Stages[GameRules.Stages.Length - 1].Id;
        }

        // ---------------- title, map, settings ----------------

        public void OpenCampaign()
        {
            if (Screen != FlowScreen.Title) return;
            SelectedStage = FrontierOrLast();
            Screen = FlowScreen.Campaign;
            Raise();
        }

        /// <summary>The title's Continue: straight to the loadout of the next stage.</summary>
        public bool Continue()
        {
            if (Screen != FlowScreen.Title) return false;
            SelectedStage = FrontierOrLast();
            return BeginLoadoutFor(SelectedStage);
        }

        public void OpenSettings()
        {
            if (Screen != FlowScreen.Title) return;
            Screen = FlowScreen.Settings;
            Raise();
        }

        /// <summary>Moves the map cursor to a stage. Locked stages can be selected (the card shows them locked).</summary>
        public bool SelectStage(string id)
        {
            if (Screen != FlowScreen.Campaign || GameRules.Stage(id) == null) return false;
            SelectedStage = id;
            Raise();
            return true;
        }

        // ---------------- loadout and stage ----------------

        /// <summary>The map's Fight button: opens the loadout of the selected stage when it is unlocked.</summary>
        public bool BeginLoadout()
        {
            return Screen == FlowScreen.Campaign && BeginLoadoutFor(SelectedStage);
        }

        bool BeginLoadoutFor(string id)
        {
            if (GameRules.Stage(id) == null || !GameRules.IsStageUnlocked(Save, id)) return false;
            StageId = id;
            Screen = FlowScreen.Loadout;
            Raise();
            return true;
        }

        /// <summary>Picks the weapon Kage takes into the fight. Only owned weapons can be picked. Saved at once.</summary>
        public bool EquipWeapon(string id)
        {
            if (!GameRules.EquipItem(Save, GearKind.Weapons, id)) return false;
            Persist();
            Raise();
            return true;
        }

        /// <summary>The loadout's Fight button: the stage begins.</summary>
        public bool StartStage()
        {
            if (Screen != FlowScreen.Loadout || StageId == null) return false;
            BeginAttempt();
            return true;
        }

        void BeginAttempt()
        {
            Screen = FlowScreen.Stage;
            Paused = false;
            Attempt++;
            Raise();
        }

        /// <summary>
        /// The fight ended (win or loss): the rules apply it to the save, the save is written and the results show.
        /// Ignored when no stage is in play.
        /// </summary>
        public void FinishStage(FightResult result)
        {
            if (Screen != FlowScreen.Stage || StageId == null || result == null) return;
            LastResult = result;
            LastOutcome = GameRules.CompleteStage(Save, StageId, result);
            Paused = false;
            Persist();
            Screen = FlowScreen.Results;
            Raise();
        }

        // ---------------- results ----------------

        /// <summary>Results to the map. After a first clear the map lands on the stage that just opened.</summary>
        public void ContinueFromResults()
        {
            if (Screen != FlowScreen.Results) return;
            bool opened = LastOutcome != null && LastOutcome.Win && LastOutcome.FirstClear && LastOutcome.Next != null;
            SelectedStage = opened ? LastOutcome.Next : StageId;
            StageId = null;
            Screen = FlowScreen.Campaign;
            Raise();
        }

        /// <summary>Results to a fresh attempt of the same stage.</summary>
        public void RetryFromResults()
        {
            if (Screen != FlowScreen.Results || StageId == null) return;
            BeginAttempt();
        }

        // ---------------- pause ----------------

        public void Pause()
        {
            if (Screen != FlowScreen.Stage || Paused) return;
            Paused = true;
            Raise();
        }

        public void Resume()
        {
            if (Screen != FlowScreen.Stage || !Paused) return;
            Paused = false;
            Raise();
        }

        /// <summary>From the pause menu: the stage starts again from the top.</summary>
        public void Retry()
        {
            if (Screen != FlowScreen.Stage || !Paused) return;
            BeginAttempt();
        }

        /// <summary>From the pause menu: leave the fight and go back to the map. Nothing is cleared.</summary>
        public void QuitToMap()
        {
            if (Screen != FlowScreen.Stage || !Paused) return;
            Paused = false;
            SelectedStage = StageId;
            StageId = null;
            Screen = FlowScreen.Campaign;
            Raise();
        }

        // ---------------- armory ----------------

        /// <summary>
        /// Opens the Armory from the title, the pre-fight loadout or the results (the JS game's Armory tile, Change gear and Upgrade).
        /// It buys, upgrades and equips through the rules and saves each change; Back returns to where it came from.
        /// </summary>
        public bool OpenArmory()
        {
            if (Screen != FlowScreen.Title && Screen != FlowScreen.Loadout && Screen != FlowScreen.Results) return false;
            ArmoryFrom = Screen;
            Armory = new ArmoryController(Save, s => persist(s), Raise);
            Screen = FlowScreen.Armory;
            Raise();
            return true;
        }

        void CloseArmory()
        {
            Armory = null;
            Screen = ArmoryFrom;
            Raise();
        }

        // ---------------- back ----------------

        /// <summary>The Back / Escape key: one step back, whatever screen this is.</summary>
        public void Back()
        {
            if (DevOffPending) { CancelDevOff(); return; }
            switch (Screen)
            {
                case FlowScreen.Campaign:
                case FlowScreen.Settings:
                    Screen = FlowScreen.Title;
                    Raise();
                    break;
                case FlowScreen.Loadout:
                    SelectedStage = StageId;
                    StageId = null;
                    Screen = FlowScreen.Campaign;
                    Raise();
                    break;
                case FlowScreen.Stage:
                    if (Paused) Resume(); else Pause();
                    break;
                case FlowScreen.Results:
                    ContinueFromResults();
                    break;
                case FlowScreen.Armory:
                    CloseArmory();
                    break;
            }
        }

        // ---------------- settings and dev mode ----------------

        /// <summary>Changes the player's settings (shake, flashing, sound, music) and saves.</summary>
        public void UpdateSettings(Action<SaveSettings> change)
        {
            if (change == null) return;
            change(Save.settings);
            Persist();
            Raise();
        }

        /// <summary>
        /// Switches dev mode. On is immediate (everything unlocks and is granted). Off asks first, because it replaces the
        /// save with a fresh one (ADR-0009): nothing changes until <see cref="ConfirmDevOff"/>.
        /// </summary>
        public void SetDevMode(bool on)
        {
            if (on)
            {
                if (Save.devMode && !DevOffPending) return;
                DevOffPending = false;
                Save = GameRules.SetDevMode(Save, true);
                Persist();
                Raise();
                return;
            }
            if (!Save.devMode) return;
            DevOffPending = true;
            Raise();
        }

        /// <summary>The player said yes: the dev save is replaced by a fresh JS save, which is written.</summary>
        public void ConfirmDevOff()
        {
            if (!DevOffPending) return;
            DevOffPending = false;
            Save = GameRules.SetDevMode(Save, false);
            SelectedStage = FrontierOrLast();
            StageId = null;
            Persist();
            Raise();
        }

        /// <summary>The player said no: the dev save stays as it was.</summary>
        public void CancelDevOff()
        {
            if (!DevOffPending) return;
            DevOffPending = false;
            Raise();
        }
    }
}
