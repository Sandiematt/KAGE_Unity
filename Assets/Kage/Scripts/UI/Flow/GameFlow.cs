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
    [DefaultExecutionOrder(-60)]
    public sealed class GameFlow : MonoBehaviour
    {
        /// <summary>Folder of the save file. Null means the persistent data path (tests set a temporary folder).</summary>
        public string saveDirectory;

        public FlowController Controller { get; private set; }
        public SaveStore Store { get; private set; }
        public StageWorld World { get; private set; }
        public RectTransform Stage { get; private set; }

        readonly Dictionary<FlowScreen, FlowPanel> panels = new Dictionary<FlowScreen, FlowPanel>();
        PauseOverlay pause;
        ConfirmOverlay confirm;
        ControlsOverlay controls;
        StageHudPanel stageHud;
        bool controlsOpen;
        int worldAttempt;
        FlowScreen? shown;
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
            pause = new PauseOverlay(this, Stage);
            confirm = new ConfirmOverlay(this, Stage);
            controls = new ControlsOverlay(this, Stage);
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
        }

        // ---------------- what is on screen ----------------

        public bool InFight { get { return Controller.Screen == FlowScreen.Stage && !Controller.Paused; } }

        /// <summary>The screen or overlay that has the focus.</summary>
        public FlowPanel Active
        {
            get
            {
                if (confirm.Visible) return confirm;
                if (controls.Visible) return controls;
                if (pause.Visible) return pause;
                return panels[Controller.Screen];
            }
        }

        public FlowPanel Panel(FlowScreen s) { return panels[s]; }

        public PauseOverlay PauseMenu { get { return pause; } }
        public ConfirmOverlay Confirm { get { return confirm; } }
        public ControlsOverlay ControlsGuide { get { return controls; } }

        public void OpenControls() { controlsOpen = true; Sync(); }

        public void CloseControls() { controlsOpen = false; Sync(); }

        void Sync()
        {
            var c = Controller;
            var scr = c.Screen;
            if (scr != FlowScreen.Settings) controlsOpen = false;

            switch (scr)
            {
                case FlowScreen.Title:
                case FlowScreen.Campaign:
                case FlowScreen.Settings:
                    DestroyWorld();
                    break;
                case FlowScreen.Loadout:
                    if (World == null || World.StageId != c.StageId || World.phase != StageWorld.Phase.Tableau)
                    {
                        DestroyWorld();
                        World = StageWorld.Create(transform, c.StageId);
                    }
                    break;
                case FlowScreen.Stage:
                    EnsureStageWorld();
                    break;
                case FlowScreen.Results:
                    if (World != null) World.FrameResults();
                    break;
            }

            foreach (var kv in panels) if (kv.Key != scr && kv.Value.Visible) kv.Value.Hide();
            var p = panels[scr];
            if (shown != scr || !p.Visible) p.Show(); else p.Rebuild();
            shown = scr;

            Overlay(pause, scr == FlowScreen.Stage && c.Paused);
            Overlay(confirm, c.DevOffPending);
            Overlay(controls, controlsOpen);
            if (World != null && scr == FlowScreen.Stage) World.Paused = c.Paused;
        }

        static void Overlay(FlowPanel o, bool want)
        {
            if (want) { if (o.Visible) o.Rebuild(); else o.Show(); }
            else if (o.Visible) o.Hide();
        }

        void EnsureStageWorld()
        {
            var c = Controller;
            if (World != null && World.StageId == c.StageId && World.phase == StageWorld.Phase.Tableau)
            {
                BeginWorld();
                return;
            }
            if (World != null && World.StageId == c.StageId && worldAttempt == c.Attempt) return;
            DestroyWorld();
            World = StageWorld.Create(transform, c.StageId);
            BeginWorld();
        }

        void BeginWorld()
        {
            worldAttempt = Controller.Attempt;
            World.Finished += OnWorldFinished;
            World.Begin();
        }

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

        void OnDestroy()
        {
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
                if (Controller.Screen == FlowScreen.Stage && !confirm.Visible && !controls.Visible)
                {
                    if (Controller.Paused) Controller.Resume(); else Controller.Pause();
                }
                return;
            }
            Active.Handle(cmd);
        }

        void Update()
        {
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
