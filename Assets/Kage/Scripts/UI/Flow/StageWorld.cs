using System;
using System.Collections.Generic;
using Kage.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage
{
    /// <summary>
    /// The 3D side of one stage in the campaign flow (SAN-67, SAN-109): the camera, post-processing, arena, Kage and the stage's foes, built
    /// through <see cref="FightBuilder"/> like the dev arena does (but without touching KageGame). What it builds is the stage's
    /// <see cref="Kind"/> (<see cref="GameRules.WorldKindOf"/>, from the JS stage data):
    /// <list type="bullet">
    /// <item><b>Road</b> (the Prologue and the four Act I stretches): the road with its gates (<see cref="StretchRoad"/>), the
    /// <see cref="StretchSession"/> that brings the JS squads wave by wave, the HUD's road layout and, on a road with prompts, the tutorial.</item>
    /// <item><b>SubBoss</b> and <b>Boss</b>: one fight in the boss format (<see cref="FightSession"/> with the stage id, so it publishes
    /// <c>StageCleared</c> or <c>StageFailed</c>), in the sub-boss's JS environment (<see cref="BossArena"/>) or the shrine, with the boss bar.</item>
    /// </list>
    /// It shows the pre-fight tableau behind the loadout, then the title card (the flow's <c>StageHudPanel</c> draws it over the
    /// intro), the fight, and reports a <see cref="FightResult"/> (from <see cref="FightStatsRecorder"/>) when the stage is over.
    /// Pausing is <see cref="Paused"/>: it stops game time and mutes Kage's input.
    /// </summary>
    public sealed class StageWorld : MonoBehaviour
    {
        public enum Phase { Tableau, Intro, Fight, Over }

        /// <summary>The title card's and the intro's length: a fight's own intro (<see cref="FightSession.IntroTime"/>).</summary>
        public const float IntroSeconds = FightSession.IntroTime;
        /// <summary>From the knock-out or the stage's end to the results (<see cref="FightSession.RoundOverTime"/>).</summary>
        public const float OverSeconds = FightSession.RoundOverTime;

        public Phase phase { get; private set; } = Phase.Tableau;
        /// <summary>What this world is: a road, a sub-boss fight or the boss fight.</summary>
        public StageWorldKind Kind { get; private set; }
        public Fighter Player { get; private set; }
        /// <summary>The boss of a boss stage; null on a road (its foes are <see cref="Stretch"/>'s).</summary>
        public Fighter Foe { get; private set; }
        public FightContext Fight { get; private set; }
        public Hud Hud { get; private set; }
        public CameraRig Cam { get; private set; }
        public string StageId { get; private set; }
        public string FoeName { get; private set; }
        /// <summary>The road's session; null on a boss stage.</summary>
        public StretchSession Stretch { get; private set; }
        /// <summary>The road's floor and gates; null on a boss stage.</summary>
        public StretchRoad Road { get; private set; }
        /// <summary>The road's tutorial prompts (the Prologue); null when the road has none.</summary>
        public StretchTutorial Tutorial { get; private set; }
        /// <summary>The boss fight's rounds and events (one round, no clock); null on a road.</summary>
        public FightSession Match { get; private set; }
        /// <summary>What Kage was built from (<see cref="KageCharacter.LoadoutKey"/>); a screen builds a new world when the save's loadout no longer matches.</summary>
        public string LoadoutKey { get; private set; }

        /// <summary>Time since the current phase began, in unscaled seconds (the title card reads it).</summary>
        public float PhaseTime { get; private set; }

        /// <summary>Raised once when the stage is over (win or loss), after the K.O. or the end of the road has played out.</summary>
        public event Action<FightResult> Finished;

        Camera _camera;
        Volume _volume;
        ChromaticAberration _ca;
        Vignette _vig;
        Bloom _bloom;
        Transform _shadowP, _shadowE;
        bool _paused, _koHandled, _controlWas, _tutorialHold;
        FightStatsRecorder _stats;
        Fighter _winner;
        string _target;
        readonly HashSet<Fighter> _hooked = new();
        readonly Dictionary<Fighter, Transform> _foeShadows = new();
        readonly InputDeviceTracker _device = new();

        /// <summary>How hard a stage's foes fight (the AI level): a placeholder ladder the tuning ticket (SAN-110) replaces.</summary>
        public static float FoeLevel(StageWorldKind kind) => kind == StageWorldKind.Boss ? 0.85f : kind == StageWorldKind.SubBoss ? 0.7f : 0.5f;

        /// <summary>Builds the world of a stage, in the tableau phase: Kage (and the boss) stand and wait. Kage is built from <paramref name="save"/>'s loadout (null = a fresh save).</summary>
        public static StageWorld Create(Transform parent, string stageId, SaveData save = null)
        {
            var go = new GameObject("StageWorld");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<StageWorld>();
            w.Build(stageId, save ?? GameRules.DefaultSave(false));
            return w;
        }

        void Build(string stageId, SaveData save)
        {
            StageId = stageId;
            LoadoutKey = KageCharacter.LoadoutKey(save);
            var stage = GameRules.Stage(stageId);
            Kind = GameRules.WorldKindOf(stageId);        // a stage the campaign doesn't have is an error here, not a placeholder foe
            bool road = Kind == StageWorldKind.Road;
            var roadData = road ? StretchData.FromId(stageId) : null;
            CharacterDef foeDef = null;
            if (!road)
            {
                foeDef = CharacterCatalog.Get(stage.Enemy);
                if (foeDef == null) throw new InvalidOperationException($"stage {stageId}: its foe '{stage.Enemy}' is not a character");
            }
            FoeName = road ? stage.Name : (!string.IsNullOrEmpty(stage.Foe) ? stage.Foe : foeDef.displayName);

            Time.timeScale = 1f;
            transform.position = Vector3.zero;
            var fight = new FightBuilder(transform, Environment.TickCount, manualClock: false);

            BuildCamera();
            BuildVolume();

            var vfxGo = new GameObject("VFX");
            vfxGo.transform.SetParent(transform, false);
            vfxGo.AddComponent<Vfx>();
            fight.Context.Audio = vfxGo.AddComponent<KageAudio>();

            var arena = new GameObject("Stage");
            arena.transform.SetParent(transform, false);
            if (road) Road = StretchRoad.Build(arena.transform, roadData, fight.Context.Bounds, Cam);
            else if (!BossArena.TryBuildForStage(arena.transform, Cam, stageId)) ArenaBuilder.Build(arena.transform, Cam);

            var hudGo = new GameObject("HUD", typeof(RectTransform));
            hudGo.transform.SetParent(transform, false);
            Hud = hudGo.AddComponent<Hud>();
            Hud.Build(_camera);
            // the dev arena's key hints don't belong in the campaign
            foreach (var name in new[] { "Hint", "Round", "Mode", "Training" })
            {
                var t = hudGo.transform.Find(name);
                if (t != null) t.gameObject.SetActive(false);
            }
            Hud.gameObject.SetActive(false);

            Player = fight.Spawn(KageCharacter.Def(save), 0, new PlayerInputSource());     // the save's equipped weapon, hairstyle and armour (SAN-84)
            if (!road)
            {
                Foe = fight.Spawn(foeDef, 1, new AIInputSource { level = FoeLevel(Kind) });
                Player.onKO += OnKO;
                Foe.onKO += OnKO;
            }
            Hud.Bind(Player);
            Hud.SetNames("KAGE  影", road ? "" : FoeName.ToUpperInvariant());

            _shadowP = MakeShadow("KageShadow", Palette.Trim);
            if (!road) _shadowE = MakeShadow("FoeShadow", Foe.fxMain);

            Fight = fight.Build();
            Fight.Camera = Cam;
            Fight.Hud = Hud;        // what a rage art moves: the camera's override and the cut-in band (SAN-82)
            Hud.BindEvents(Fight.Events);
            Hud.SetPlayerLevel(save.level);
            Hud.DevText = false;
            Fight.Begin();
            Player.controlEnabled = false;

            if (road)
            {
                _stats = new FightStatsRecorder(Fight.Events, Player, stretch: true);
                Stretch = new StretchSession(Fight, fight, roadData, Player, foeLevel: FoeLevel(Kind));
                Road.Bind(Stretch);
                Cam.SetBounds(Stretch);
                Cam.lookAhead = 2.0f;
                Cam.Frame(Fight, Player);
                Hud.BindStretch(Stretch);       // the road strip reads the session (SAN-108)
                Player.ResetForRound((float)roadData.Start, 1f);
                if (roadData.Prompts.Length > 0) { Tutorial = new StretchTutorial(Fight, Stretch, Player); _tutorialHold = true; }
                Cam.targets.Clear();
            }
            else
            {
                _stats = new FightStatsRecorder(Fight.Events, Player);
                Match = new FightSession(Fight, FightFormat.Boss, Player, new[] { Foe }, stageId);
                Match.RoundStarting += PlaceFighters;
                Match.FightBegan += OnMatchFightBegan;
                Match.MatchEnded += _ => OnMatchEnded();
                Hud.SetFormat(FightFormat.Boss);        // one round, no clock: the campaign's single fight
                if (foeDef.bossId != null) Hud.BindBoss(Foe, BossCatalog.Get(foeDef.bossId), Fight.Events);
                PlaceFighters();
                Cam.targets.Add(Player.rig.Bone(B.Hips));
                Cam.targets.Add(Foe.rig.Bone(B.Hips));
            }
            SetTableauCamera();
            Cam.Snap();
        }

        /// <summary>The boss fight's marks: Kage on the left, the boss on the right, both held until the intro is over.</summary>
        void PlaceFighters()
        {
            Player.ResetForRound(-1.25f, 1f);
            Foe.ResetForRound(1.25f, -1f);
            Player.controlEnabled = Foe.controlEnabled = false;
        }

        // ---------------- phases ----------------

        /// <summary>Starts the stage: the title card, then the fight.</summary>
        public void Begin()
        {
            if (phase != Phase.Tableau) return;
            Hud.gameObject.SetActive(true);
            Cam.useOverride = false;
            SetPhase(Phase.Intro);
            if (Match != null) Match.StartRound();
        }

        void SetPhase(Phase p) { phase = p; PhaseTime = 0f; }

        /// <summary>A road starts when the title card has gone: Kage walks. (A boss fight is started by its <see cref="FightSession"/>.)</summary>
        void StartRoad()
        {
            _tutorialHold = false;
            Stretch.Begin();
            _stats.MarkFightStart(Fight.Clock.Frame);
            SetPhase(Phase.Fight);
        }

        void OnMatchFightBegan()
        {
            _stats.MarkFightStart(Fight.Clock.Frame);
            Hud.Announce("FIGHT!", Palette.TrimBright, 0.9f);
            SetPhase(Phase.Fight);
        }

        /// <summary>The fight's match is decided (the K.O. has played out): the stage is over.</summary>
        void OnMatchEnded() { _matchDone = true; }
        bool _matchDone;

        /// <summary>The fight is stopped and Kage's keys are ignored (the pause menu is up).</summary>
        public bool Paused
        {
            get { return _paused; }
            set
            {
                if (_paused == value) return;
                _paused = value;
                GameFeel.paused = value;
                if (value) { _controlWas = Player.controlEnabled; Player.controlEnabled = false; }
                else Player.controlEnabled = _controlWas;
            }
        }

        void Update()
        {
            GameFeel.Tick();
            float dt = Time.unscaledDeltaTime;
            if (!_paused) PhaseTime += dt;

            if (Kind == StageWorldKind.Road) { if (UpdateRoad()) return; }
            else if (UpdateBoss(dt)) return;

            UpdatePost();
            UpdateShadows();
        }

        // ---------------- a boss fight ----------------

        /// <summary>Returns true when the stage was completed (the world may be gone).</summary>
        bool UpdateBoss(float dt)
        {
            if (phase != Phase.Tableau && !_paused) Match.Update(dt, Time.deltaTime);
            if (phase == Phase.Fight) TrackCombo();
            if (phase != Phase.Tableau) Hud.Tick(Player, Foe, 0f, 1, false);
            if (_matchDone) { Complete(); return true; }
            return false;
        }

        void OnKO(Fighter loser)
        {
            if (_koHandled) return;
            _koHandled = true;
            _winner = loser == Player ? Foe : Player;
            Hud.Announce("K.O.", KMath.Hex("#FF4B4B"), 1.8f);
            SetPhase(Phase.Over);
            Match.OnKnockOut(loser);        // the round ends, the winner takes a bow, then the match (and with it the stage) is over
        }

        // the combo counter ends when the victim has recovered (as in the dev arena)
        void TrackCombo()
        {
            ResetComboIfRecovered(Player, Foe);
            ResetComboIfRecovered(Foe, Player);
        }

        static void ResetComboIfRecovered(Fighter attacker, Fighter victim)
        {
            if (attacker == null || victim == null) return;
            if (!(victim.state is FState.Hitstun or FState.Launched or FState.Thrown or FState.KO or FState.Down)) attacker.comboCount = 0;
        }

        // ---------------- a road ----------------

        /// <summary>Returns true when the stage was completed (the world may be gone).</summary>
        bool UpdateRoad()
        {
            switch (phase)
            {
                case Phase.Intro:
                    if (PhaseTime >= IntroSeconds) StartRoad();
                    break;
                case Phase.Fight:
                    if (!_paused && (Stretch.Cleared || Stretch.Failed)) EndRoad(Stretch.Cleared);
                    break;
                case Phase.Over:
                    if (PhaseTime > 1.3f && _winner != null && _winner.state != FState.Victory && _winner.Grounded && !_winner.Busy) _winner.Win();
                    if (PhaseTime >= OverSeconds) { Complete(); return true; }
                    break;
            }
            if (phase == Phase.Tableau) return false;

            var foes = new List<Fighter>(Stretch.Foes);
            int n = 0;
            foreach (var f in foes)
            {
                if (_hooked.Add(f)) f.SetSortBase(FightBuilder.FoeSortOrder - 400 * (n % 4));
                n++;
            }
            _hooked.RemoveWhere(f => f == null);
            var target = Player.opponent != null && Player.opponent.state != FState.KO ? Player.opponent : null;
            string name = target != null ? target.Character.displayName.ToUpperInvariant() : "";
            if (name != _target) { _target = name; Hud.SetNames("KAGE  影", name); }
            UpdateTutorialHud();
            Hud.Tick(Player, target, 0f, 1, false);
            ResetComboIfRecovered(Player, Player.opponent);
            foreach (var f in foes) ResetComboIfRecovered(f, Player);
            return false;
        }

        void EndRoad(bool cleared)
        {
            if (phase == Phase.Over) return;
            _winner = cleared ? Player : null;
            Player.controlEnabled = false;
            if (cleared) Hud.ShowInkBanner("Stretch cleared", FoeName, Ui.Gold);
            else Hud.Banner("DEFEATED", KMath.Hex("#FF4B4B"), 2.5f);   // a plain line, not the big brush banner
            SetPhase(Phase.Over);
        }

        /// <summary>The prompt slip follows the tutorial and the last device used; Kage is held still while the title card shows.</summary>
        void UpdateTutorialHud()
        {
            var t = Tutorial;
            if (t == null) return;
            t.Device = _device.Poll();
            Vector2 head = Player.Pos + Vector2.up * 2.3f;
            Hud.SetPrompt(_tutorialHold ? null : t.NowView(), t.DoneView(), head);
        }

        // ---------------- the end ----------------

        void Complete()
        {
            var handler = Finished;
            Finished = null;
            if (handler == null) return;
            var r = _stats.Result();
            if (Kind == StageWorldKind.Road)
            {
                // the loss recap's "health taken": the share of the road's waves beaten
                int beaten = Stretch.StretchWave - (Stretch.FoesLeft > 0 ? 1 : 0);
                r.enemyDamage = r.win ? 1.0 : Mathf.Clamp01(beaten / (float)Mathf.Max(1, Stretch.TotalWaves));
            }
            else r.enemyDamage = Mathf.Clamp01(1f - Foe.health / Mathf.Max(1f, Foe.maxHealth));
            handler(r);
        }

        /// <summary>Test and tooling hook: ends the stage at once, as a finishing blow (a boss's knock-out, or Kage's) or the road's end would.</summary>
        public void ForceKnockOut(bool foeLoses)
        {
            if (phase != Phase.Fight) return;
            if (Kind == StageWorldKind.Road)
            {
                int frame = Fight.Clock.Frame;
                Fight.Events.Publish(foeLoses ? CombatEvent.StageCleared(frame, StageId) : CombatEvent.StageFailed(frame, StageId));
                EndRoad(foeLoses);
                return;
            }
            var loser = foeLoses ? Foe : Player;
            loser.health = 0f;
            Fight.Events.Publish(CombatEvent.KnockedOut(Fight.Clock.Frame, foeLoses ? Player : Foe, loser, "force", Vector2.zero, Vector2.zero));
            OnKO(loser);
        }

        /// <summary>The results screen's framing: the fighters stand in the left half, the results column has the right (JS tableau).</summary>
        public void FrameResults()
        {
            if (Cam == null || Player == null) return;
            if (Hud != null) Hud.gameObject.SetActive(false);
            float mid = Foe != null ? (Player.rig.Bone(B.Hips).position.x + Foe.rig.Bone(B.Hips).position.x) * 0.5f : Player.rig.Bone(B.Hips).position.x + 1.25f;
            Cam.useOverride = true;
            Cam.overrideSize = 2.2f;
            Cam.overrideCenter = new Vector2(mid + 2.1f, 1.45f);
        }

        // ---------------- camera, post, shadows (the dev arena's, without KageGame) ----------------

        void BuildCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = cgo.AddComponent<Camera>();
                cgo.AddComponent<AudioListener>();
            }
            _camera.orthographic = true;
            _camera.orthographicSize = 2.3f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Palette.Background;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 100f;
            _camera.cullingMask = ~(1 << 5);
            var data = _camera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            Cam = _camera.GetComponent<CameraRig>();
            if (Cam == null) Cam = _camera.gameObject.AddComponent<CameraRig>();
            Cam.cam = _camera;
            Cam.targets.Clear();
            Cam.useOverride = false;
            Cam.lookAhead = 0f;
            Cam.SetBounds(null);
        }

        // The tableau frames both fighters in the left and right thirds with their feet low, above the loadout dock (JS prefight);
        // on a road, Kage stands at the start with the road ahead of him.
        void SetTableauCamera()
        {
            Cam.useOverride = true;
            Cam.overrideSize = 2.2f;
            Cam.overrideCenter = new Vector2(Kind == StageWorldKind.Road ? Player.Pos.x + 1.25f : 0f, 1.45f);
        }

        void BuildVolume()
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = _bloom = p.Add<Bloom>(true);
            bloom.threshold.Override(0.92f);
            bloom.intensity.Override(1.15f);
            bloom.scatter.Override(0.68f);
            bloom.tint.Override(new Color(1f, 0.86f, 0.82f));
            _vig = p.Add<Vignette>(true);
            _vig.intensity.Override(0.34f);
            _vig.smoothness.Override(0.45f);
            _vig.color.Override(Color.black);
            var ca = p.Add<ColorAdjustments>(true);
            ca.contrast.Override(14f);
            ca.saturation.Override(6f);
            _ca = p.Add<ChromaticAberration>(true);
            _ca.intensity.Override(0f);
            var grain = p.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.22f);
            _volume.sharedProfile = p;
        }

        Transform MakeShadow(string name, Color glowColour)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            MeshKit.MakeRenderer("Contact", t, MeshKit.Disc(Vector2.zero, 0.55f, new Color(0f, 0f, 0f, 0.75f), new Color(0f, 0f, 0f, 0f), 32, 1f, 0.12f), MeshKit.Alpha, -900);
            var glow = MeshKit.MakeRenderer("Glow", t, MeshKit.Disc(Vector2.zero, 0.42f, glowColour.WithA(0.5f), new Color(0, 0, 0, 0), 32, 1f, 0.07f), Vfx.I.AddHDR(1.6f), -890);
            glow.transform.localPosition = new Vector3(0f, 0.005f, 0f);
            return t;
        }

        void UpdateShadows()
        {
            Place(_shadowP, Player);
            if (Foe != null) Place(_shadowE, Foe);
            if (Stretch == null) return;
            foreach (var f in Stretch.Foes)
            {
                if (!_foeShadows.TryGetValue(f, out var t)) _foeShadows[f] = t = MakeShadow("FoeShadow", f.fxMain);
                Place(t, f);
            }
            List<Fighter> gone = null;
            foreach (var kv in _foeShadows) if (kv.Key == null) (gone ??= new List<Fighter>()).Add(kv.Key);
            if (gone != null) foreach (var f in gone) { Destroy(_foeShadows[f].gameObject); _foeShadows.Remove(f); }
        }

        static void Place(Transform s, Fighter f)
        {
            if (f == null || !f.gameObject.activeInHierarchy) { if (s != null) s.gameObject.SetActive(false); return; }
            s.gameObject.SetActive(true);
            Vector2 hips = f.rig.Bone(B.Hips).position;
            float h = Mathf.Max(0f, hips.y - 0.6f);
            s.position = new Vector3(hips.x, 0.01f, 0f);
            float k = Mathf.Clamp(1f - h * 0.35f, 0.35f, 1f);
            s.localScale = new Vector3(k, k, 1f);
        }

        void UpdatePost()
        {
            GameFeel.ApplyPost(_bloom, _vig, _ca, Player);
            if (Player != null && Foe != null)
            {
                bool pFront = Player.IsAttacking || !Foe.IsAttacking;
                Player.SetSortBase(pFront ? 1500 : 1000);
                Foe.SetSortBase(pFront ? 1000 : 1500);
            }
        }

        void OnDestroy()
        {
            Tutorial?.Dispose();
            Stretch?.Dispose();
            Road?.Dispose();
            if (_stats != null) _stats.Dispose();
            GameFeel.paused = false;
            Time.timeScale = 1f;
            if (Cam != null) { Cam.targets.Clear(); Cam.useOverride = false; Cam.SetBounds(null); Cam.lookAhead = 0f; }
        }
    }
}
