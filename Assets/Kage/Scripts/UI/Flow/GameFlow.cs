using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The campaign entry (ADR-0001, SAN-67): title, campaign map, pre-fight loadout, stage, results and back to the map, with pause
    /// and settings. It owns the save, the <see cref="FlowController"/> (which decides where the player goes), the JS-style screens
    /// and the stage world of the fight in play, and routes keyboard, gamepad and mouse to whichever screen or overlay is on top.
    /// It lives in its own scene and never touches KageGame, which stays the dev arena.
    /// </summary>
    public enum InfoView
    {
        None,
        Controls,
        Records,
        Trophies,
        Profile,
    }

    [DefaultExecutionOrder(-60)]
    public sealed class GameFlow : MonoBehaviour
    {
        /// <summary>Folder of the save file. Null means the persistent data path (tests set a temporary folder).</summary>
        public string saveDirectory;

        /// <summary>Opens on the animated splash (the JS boot screen) before the title. The campaign scene turns it on; tests and fixtures leave it off.</summary>
        public bool showSplash;

        public FlowController Controller { get; private set; }
        public SaveStore Store { get; private set; }
        public StageWorld World { get; private set; }
        /// <summary>The Armory's camera and preview Kages (SAN-90); null on every other screen.</summary>
        public ArmoryWorld ArmoryWorld { get; private set; }
        public RectTransform Stage { get; private set; }

        readonly Dictionary<FlowScreen, FlowPanel> panels = new Dictionary<FlowScreen, FlowPanel>();
        PauseOverlay pause;
        ConfirmOverlay confirm;
        ControlsOverlay controls;
        RecordsOverlay records;
        TrophiesOverlay trophies;
        ProfileOverlay profile;
        ToastLayer toasts;
        StageHudPanel stageHud;
        InfoView info = InfoView.None;
        int worldAttempt;
        FlowScreen? shown;
        RectTransform canvasRoot;
        ScreenWipe wipe;
        SplashPanel splash;
        readonly MenuInput input = new MenuInput();
        readonly List<MenuCommand> commands = new List<MenuCommand>();
        Vector2 lastMouse;

        /// <summary>Creates a flow on its own object. <paramref name="saveDirectory"/> null uses the persistent data path.</summary>
        public static GameFlow Create(string saveDirectory = null)
        {
            var go = new GameObject("GameFlow");
            go.SetActive(false);
            var f = go.AddComponent<GameFlow>();
            f.saveDirectory = saveDirectory;
            go.SetActive(true);
            return f;
        }

        void Awake()
        {
            Time.timeScale = 1f;
            Store = string.IsNullOrEmpty(saveDirectory) ? SaveStore.AtPersistentDataPath() : new SaveStore(saveDirectory);
            Controller = new FlowController(Store.Load(), s => Store.Save(s));
            Controller.Changed += Sync;
            BuildCanvas();
            panels[FlowScreen.Title] = new TitleScreen(this, Stage);
            panels[FlowScreen.Campaign] = new CampaignScreen(this, Stage);
            panels[FlowScreen.Loadout] = new LoadoutScreen(this, Stage);
            stageHud = new StageHudPanel(this, Stage);
            panels[FlowScreen.Stage] = stageHud;
            panels[FlowScreen.Results] = new ResultsScreen(this, Stage);
            panels[FlowScreen.Settings] = new SettingsScreen(this, Stage);
            panels[FlowScreen.Armory] = new ArmoryScreen(this, Stage);
            pause = new PauseOverlay(this, Stage);
            confirm = new ConfirmOverlay(this, Stage);
            controls = new ControlsOverlay(this, Stage);
            records = new RecordsOverlay(this, Stage);
            trophies = new TrophiesOverlay(this, Stage);
            profile = new ProfileOverlay(this, Stage);
            toasts = ToastLayer.Create(Stage);
            Controller.Toasted += (e, t, red) => toasts.Show(e, t, red);
            Sync();
        }

        void BuildCanvas()
        {
            var go = new GameObject("FlowCanvas", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var stageGo = new GameObject("Stage", typeof(RectTransform));
            Stage = (RectTransform)stageGo.transform;
            Stage.SetParent(go.transform, false);
            Stage.anchorMin = Stage.anchorMax = new Vector2(0.5f, 0.5f);
            Stage.pivot = new Vector2(0.5f, 0.5f);
            Stage.sizeDelta = new Vector2(1600, 900);
            Stage.anchoredPosition = Vector2.zero;
            canvasRoot = (RectTransform)go.transform;
            // over the stage: the ink wipe between screens and, on a real start, the opening splash
            wipe = ScreenWipe.Create(canvasRoot);
            if (showSplash) splash = SplashPanel.Create(canvasRoot);
        }

        /// <summary>The opening splash while it is up (null when the flow opened on the title).</summary>
        public SplashPanel Splash { get { return splash; } }

        /// <summary>The ink wipe between screens.</summary>
        public ScreenWipe Wipe { get { return wipe; } }

        // ---------------- what is on screen ----------------

        public bool InFight { get { return Controller.Screen == FlowScreen.Stage && !Controller.Paused; } }

        /// <summary>The screen or overlay that has the focus.</summary>
        public FlowPanel Active
        {
            get
            {
                if (confirm.Visible) return confirm;
                if (controls.Visible) return controls;
                if (records.Visible) return records;
                if (trophies.Visible) return trophies;
                if (profile.Visible) return profile;
                if (pause.Visible) return pause;
                return panels[Controller.Screen];
            }
        }

        public FlowPanel Panel(FlowScreen s) { return panels[s]; }

        public PauseOverlay PauseMenu { get { return pause; } }
        public ConfirmOverlay Confirm { get { return confirm; } }
        public ControlsOverlay ControlsGuide { get { return controls; } }
        public RecordsOverlay Records { get { return records; } }
        public TrophiesOverlay Trophies { get { return trophies; } }
        public ProfileOverlay Profile { get { return profile; } }
        public ToastLayer Toasts { get { return toasts; } }

        /// <summary>The info overlay that is up (the controls guide, the records, the trophies, the profile).</summary>
        public InfoView Info { get { return info; } }

        public void OpenControls() { info = InfoView.Controls; Sync(); }

        /// <summary>The title's Records, Trophies and Profile diamonds, and its Codex button (the controls guide).</summary>
        public void OpenInfo(InfoView view) { info = view; Sync(); }

        public void CloseControls() { info = InfoView.None; Sync(); }

        public void CloseInfo() { info = InfoView.None; Sync(); }

        void Sync()
        {
            var c = Controller;
            var scr = c.Screen;
            // a change of screen plays the JS ink wipe: the old screen is copied first and stays under the stroke until it covers the
            // screen; the new one is built at once (input and the controller never wait for the animation)
            GameObject ghost = null;
            bool changing = shown.HasValue && shown.Value != scr && wipe != null && UiClock.Animate && (splash == null || !splash.Active);
            if (wipe != null && wipe.Running) wipe.Finish(true);
            if (changing) ghost = MakeGhost(panels[shown.Value]);
            if (scr != FlowScreen.Settings && scr != FlowScreen.Title) info = InfoView.None;
            if (scr != FlowScreen.Armory) DestroyArmoryWorld();

            switch (scr)
            {
                case FlowScreen.Title:
                case FlowScreen.Campaign:
                case FlowScreen.Settings:
                    DestroyWorld();
                    break;
                case FlowScreen.Loadout:
                    if (World == null || World.StageId != c.StageId || World.phase != StageWorld.Phase.Tableau || World.LoadoutKey != KageCharacter.LoadoutKey(c.Save))
                    {
                        DestroyWorld();
                        World = StageWorld.Create(transform, c.StageId, c.Save);
                    }
                    break;
                case FlowScreen.Stage:
                    EnsureStageWorld();
                    break;
                case FlowScreen.Results:
                    // back from the Armory the stage world was put away: stand the fighters up again for the results
                    if (World == null && c.StageId != null) World = StageWorld.Create(transform, c.StageId, c.Save);
                    if (World != null) World.FrameResults();
                    break;
                case FlowScreen.Armory:
                    // the Armory has its own camera and fighters
                    DestroyWorld();
                    if (ArmoryWorld == null) ArmoryWorld = ArmoryWorld.Create(transform);
                    break;
            }

            foreach (var kv in panels) if (kv.Key != scr && kv.Value.Visible) kv.Value.Hide();
            var p = panels[scr];
            if (shown != scr || !p.Visible) p.Show(); else p.Rebuild();
            shown = scr;
            if (changing) BeginWipe(ghost, p);

            Overlay(pause, scr == FlowScreen.Stage && c.Paused);
            Overlay(confirm, c.DevOffPending);
            Overlay(controls, info == InfoView.Controls);
            Overlay(records, info == InfoView.Records);
            Overlay(trophies, info == InfoView.Trophies);
            Overlay(profile, info == InfoView.Profile);
            if (World != null && scr == FlowScreen.Stage) World.Paused = c.Paused;
        }

        /// <summary>A still copy of a screen: what stays on show under the wipe's stroke until it covers the screen.</summary>
        GameObject MakeGhost(FlowPanel old)
        {
            if (old == null || !old.Visible) return null;
            var g = Instantiate(old.Root.gameObject, Stage);
            g.name = "Ghost";
            g.SetActive(true);
            g.transform.SetAsLastSibling();
            var group = g.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
            // a copy does not move, listen or choose
            foreach (var t in g.GetComponentsInChildren<UiFocusTween>(true)) Destroy(t);
            foreach (var n in g.GetComponentsInChildren<NavItem>(true)) Destroy(n);
            return g;
        }

        void BeginWipe(GameObject ghost, FlowPanel entering)
        {
            var group = entering.Root.GetComponent<CanvasGroup>();
            if (group == null) group = entering.Root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            wipe.Begin(() =>
            {
                if (ghost != null) Destroy(ghost);
                if (group != null) group.alpha = 1f;
            });
        }

        static void Overlay(FlowPanel o, bool want)
        {
            if (want) { if (o.Visible) o.Rebuild(); else o.Show(); }
            else if (o.Visible) o.Hide();
        }

        void EnsureStageWorld()
        {
            var c = Controller;
            if (World != null && World.StageId == c.StageId && World.phase == StageWorld.Phase.Tableau && World.LoadoutKey == KageCharacter.LoadoutKey(c.Save))
            {
                BeginWorld();
                return;
            }
            if (World != null && World.StageId == c.StageId && worldAttempt == c.Attempt) return;
            DestroyWorld();
            World = StageWorld.Create(transform, c.StageId, c.Save);
            BeginWorld();
        }

        void BeginWorld()
        {
            worldAttempt = Controller.Attempt;
            World.Finished += OnWorldFinished;
            World.Hud.PauseRequested -= OnHudPause;
            World.Hud.PauseRequested += OnHudPause;       // the HUD's pause button (SAN-108)
            World.Begin();
        }

        void OnHudPause() { Send(MenuCommand.Pause); }

        void OnWorldFinished(FightResult r)
        {
            Controller.FinishStage(r);
        }

        void DestroyWorld()
        {
            if (World == null) return;
            World.Finished -= OnWorldFinished;
            // immediately: a world built right after this one must not be touched by this one's teardown
            DestroyImmediate(World.gameObject);
            World = null;
            worldAttempt = 0;
        }

        void DestroyArmoryWorld()
        {
            if (ArmoryWorld == null) return;
            DestroyImmediate(ArmoryWorld.gameObject);
            ArmoryWorld = null;
        }

        void OnDestroy()
        {
            if (wipe != null) wipe.Finish(false);
            DestroyArmoryWorld();
            DestroyWorld();
            GameFeel.paused = false;
            Time.timeScale = 1f;
        }

        // ---------------- input ----------------

        /// <summary>One player command: the same entry keyboard, gamepad and tests use.</summary>
        public void Send(MenuCommand cmd)
        {
            if (cmd == MenuCommand.Pause)
            {
                if (Controller.Screen == FlowScreen.Stage && !confirm.Visible && info == InfoView.None)
                {
                    if (Controller.Paused) Controller.Resume(); else Controller.Pause();
                }
                return;
            }
            Active.Handle(cmd);
        }

        void Update()
        {
            // the splash takes the first press (any key, click or button); the menu hears nothing until it is open
            if (splash != null && splash.Active) return;
            commands.Clear();
            input.Poll(commands, InFight);
            foreach (var cmd in commands) Send(cmd);
            Pointer();
            stageHud.Tick();
            if (Controller.Screen == FlowScreen.Results) panels[FlowScreen.Results].Tick();
        }

        void Pointer()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            var nav = Active.Nav;
            Vector2 pos = mouse.position.ReadValue();
            if ((pos - lastMouse).sqrMagnitude > 0.5f)
            {
                var hover = nav.ItemAt(pos, null);
                if (hover != null && hover.interactable) nav.FocusOn(hover, true);
            }
            lastMouse = pos;
            if (mouse.leftButton.wasPressedThisFrame)
            {
                var hit = nav.ItemAt(pos, null);
                if (hit != null && hit.interactable)
                {
                    nav.FocusOn(hit, true);
                    Send(MenuCommand.Submit);
                }
            }
        }
    }
}
