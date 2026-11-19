using System;
using Kage.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage
{
    /// <summary>
    /// The 3D side of one stage in the campaign flow (SAN-67): the camera, post-processing, arena, Kage and the stage's foe, built
    /// through <see cref="FightBuilder"/> like the dev arena does (but without touching KageGame). It shows the pre-fight tableau
    /// behind the loadout, runs a plain placeholder fight (one foe, one round, no clock) and reports a <see cref="FightResult"/>
    /// when it ends. The real stage structure (stretches, waves, bosses) replaces the fight later (SAN-89, SAN-102).
    /// Pausing is <see cref="Paused"/>: it stops game time and mutes Kage's input.
    /// </summary>
    public sealed class StageWorld : MonoBehaviour
    {
        public enum Phase { Tableau, Intro, Fight, Over }

        public const float IntroSeconds = 1.6f, OverSeconds = 3.2f;

        public Phase phase { get; private set; } = Phase.Tableau;
        public Fighter Player { get; private set; }
        public Fighter Foe { get; private set; }
        public FightContext Fight { get; private set; }
        public Hud Hud { get; private set; }
        public CameraRig Cam { get; private set; }
        public string StageId { get; private set; }
        public string FoeName { get; private set; }
        /// <summary>What Kage was built from (<see cref="KageCharacter.LoadoutKey"/>); a screen builds a new world when the save's loadout no longer matches.</summary>
        public string LoadoutKey { get; private set; }

        /// <summary>Time since the current phase began, in unscaled seconds (the title card reads it).</summary>
        public float PhaseTime { get; private set; }

        /// <summary>Raised once when the fight is over (win or loss), after the K.O. has played out.</summary>
        public event Action<FightResult> Finished;

        Camera _camera;
        Volume _volume;
        ChromaticAberration _ca;
        Vignette _vig;
        Transform _shadowP, _shadowE;
        bool _paused, _koHandled, _controlWas;
        FightStatsRecorder _stats;
        Fighter _winner;

        /// <summary>Builds the world of a stage, in the tableau phase: both fighters stand and wait. Kage is built from <paramref name="save"/>'s loadout (null = a fresh save).</summary>
        public static StageWorld Create(Transform parent, string stageId, SaveData save = null)
        {
            var go = new GameObject("StageWorld");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<StageWorld>();
            w.Build(stageId, save);
            return w;
        }

        void Build(string stageId, SaveData save)
        {
            StageId = stageId;
            LoadoutKey = KageCharacter.LoadoutKey(save);
            var stage = GameRules.Stage(stageId);
            var foe = stage != null ? JsTables.Enemy(stage.Enemy) : null;
            FoeName = stage != null && stage.Kind != "stretch" && !string.IsNullOrEmpty(stage.Foe) ? stage.Foe : foe != null ? foe.Name : "RONIN";

            Time.timeScale = 1f;
            transform.position = Vector3.zero;
            var fight = new FightBuilder(transform, Environment.TickCount, manualClock: false);

            BuildCamera();
            BuildVolume();

            var vfxGo = new GameObject("VFX");
            vfxGo.transform.SetParent(transform, false);
            vfxGo.AddComponent<Vfx>();

            var arena = new GameObject("Stage");
            arena.transform.SetParent(transform, false);
            ArenaBuilder.Build(arena.transform, Cam);

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

            Player = fight.Spawn(KageCharacter.Def(save), 0, new PlayerInputSource());     // the save's equipped weapon, helmet and armour (SAN-84)
            Foe = fight.Spawn(CharacterCatalog.Get(Ronin.Id), 1, new AIInputSource { level = LevelFor(stage) });
            Player.onKO += OnKO;
            Foe.onKO += OnKO;
            Hud.Bind(Player);
            Hud.SetNames("KAGE  影", FoeName.ToUpperInvariant());

            _shadowP = MakeShadow(true);
            _shadowE = MakeShadow(false);

            Fight = fight.Build();
            Fight.Camera = Cam;
            Fight.Hud = Hud;        // what a rage art moves: the camera's override and the cut-in band (SAN-82)
            _stats = new FightStatsRecorder(Fight.Events, Player);
            Fight.Begin();
            Player.ResetForRound(-1.25f, 1f);
            Foe.ResetForRound(1.25f, -1f);
            Player.controlEnabled = Foe.controlEnabled = false;
            Cam.targets.Add(Player.rig.Bone(B.Hips));
            Cam.targets.Add(Foe.rig.Bone(B.Hips));
            SetTableauCamera();
            Cam.Snap();
        }

        static float LevelFor(JsStage stage)
        {
            if (stage == null) return 0.5f;
            return stage.Kind == "boss" ? 0.85f : stage.Kind == "subboss" ? 0.7f : 0.5f;
        }

        // ---------------- phases ----------------

        /// <summary>Starts the stage: the title card, then the fight.</summary>
        public void Begin()
        {
            if (phase != Phase.Tableau) return;
            Hud.gameObject.SetActive(true);
            Cam.useOverride = false;
            SetPhase(Phase.Intro);
        }

        void SetPhase(Phase p) { phase = p; PhaseTime = 0f; }

        void StartFight()
        {
            Player.BeginFight();
            Foe.BeginFight();
            _stats.MarkFightStart(Fight.Clock.Frame);
            Hud.Announce("FIGHT!", Palette.TrimBright, 0.9f);
            SetPhase(Phase.Fight);
        }

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

            switch (phase)
            {
                case Phase.Intro:
                    if (PhaseTime >= IntroSeconds) StartFight();
                    break;
                case Phase.Fight:
                    TrackCombo();
                    break;
                case Phase.Over:
                    if (PhaseTime > 1.3f && _winner != null && _winner.state != FState.Victory && _winner.Grounded && !_winner.Busy) _winner.Win();
                    if (PhaseTime >= OverSeconds) Complete();
                    break;
            }

            if (phase != Phase.Tableau)
            {
                Hud.Tick(Player, Foe, 0f, 1, false);
            }
            UpdatePost();
            UpdateShadows();
        }

        // the combo counter ends when the victim has recovered (as in the dev arena)
        void TrackCombo()
        {
            ResetComboIfRecovered(Player, Foe);
            ResetComboIfRecovered(Foe, Player);
        }

        static void ResetComboIfRecovered(Fighter attacker, Fighter victim)
        {
            if (!(victim.state is FState.Hitstun or FState.Launched or FState.Thrown or FState.KO or FState.Down)) attacker.comboCount = 0;
        }

        void OnKO(Fighter loser)
        {
            if (_koHandled) return;
            _koHandled = true;
            _winner = loser == Player ? Foe : Player;
            Player.controlEnabled = Foe.controlEnabled = false;
            Hud.Announce("K.O.", KMath.Hex("#FF4B4B"), 1.8f);
            SetPhase(Phase.Over);
        }

        void Complete()
        {
            var handler = Finished;
            Finished = null;
            if (handler == null) return;
            var r = _stats.Result();
            r.enemyDamage = Mathf.Clamp01(1f - Foe.health / Mathf.Max(1f, Foe.maxHealth));
            handler(r);
        }

        /// <summary>Test and tooling hook: knocks the foe (or Kage) out at once, as a finishing blow would.</summary>
        public void ForceKnockOut(bool foeLoses)
        {
            if (phase != Phase.Fight) return;
            var loser = foeLoses ? Foe : Player;
            loser.health = 0f;
            Fight.Events.Publish(CombatEvent.KnockedOut(Fight.Clock.Frame, foeLoses ? Player : Foe, loser, "force", Vector2.zero, Vector2.zero));
            OnKO(loser);
        }

        /// <summary>The results screen's framing: the fighters stand in the left half, the results column has the right (JS tableau).</summary>
        public void FrameResults()
        {
            if (Cam == null || Player == null || Foe == null) return;
            if (Hud != null) Hud.gameObject.SetActive(false);
            float mid = (Player.rig.Bone(B.Hips).position.x + Foe.rig.Bone(B.Hips).position.x) * 0.5f;
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
        }

        // The tableau frames both fighters in the left and right thirds with their feet low, above the loadout dock (JS prefight).
        void SetTableauCamera()
        {
            Cam.useOverride = true;
            Cam.overrideSize = 2.2f;
            Cam.overrideCenter = new Vector2(0f, 1.45f);
        }

        void BuildVolume()
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = p.Add<Bloom>(true);
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

        Transform MakeShadow(bool kage)
        {
            var t = new GameObject(kage ? "KageShadow" : "FoeShadow").transform;
            t.SetParent(transform, false);
            MeshKit.MakeRenderer("Contact", t, MeshKit.Disc(Vector2.zero, 0.55f, new Color(0f, 0f, 0f, 0.75f), new Color(0f, 0f, 0f, 0f), 32, 1f, 0.12f), MeshKit.Alpha, -900);
            var glow = MeshKit.MakeRenderer("Glow", t, MeshKit.Disc(Vector2.zero, 0.42f, (kage ? Palette.Trim : Palette.Amber).WithA(0.5f), new Color(0, 0, 0, 0), 32, 1f, 0.07f), Vfx.I.AddHDR(1.6f), -890);
            glow.transform.localPosition = new Vector3(0f, 0.005f, 0f);
            return t;
        }

        void UpdateShadows()
        {
            Place(_shadowP, Player);
            Place(_shadowE, Foe);
        }

        static void Place(Transform s, Fighter f)
        {
            if (f == null || !f.gameObject.activeInHierarchy) return;
            Vector2 hips = f.rig.Bone(B.Hips).position;
            float h = Mathf.Max(0f, hips.y - 0.6f);
            s.position = new Vector3(hips.x, 0.01f, 0f);
            float k = Mathf.Clamp(1f - h * 0.35f, 0.35f, 1f);
            s.localScale = new Vector3(k, k, 1f);
        }

        void UpdatePost()
        {
            if (_ca != null) _ca.intensity.Override(Mathf.Clamp01(GameFeel.aberration));
            if (_vig != null) _vig.intensity.Override(0.34f + (Player != null && Player.state == FState.Ultimate ? 0.22f : 0f));
            if (Player != null && Foe != null)
            {
                bool pFront = Player.IsAttacking || !Foe.IsAttacking;
                Player.SetSortBase(pFront ? 1500 : 1000);
                Foe.SetSortBase(pFront ? 1000 : 1500);
            }
        }

        void OnDestroy()
        {
            if (_stats != null) _stats.Dispose();
            GameFeel.paused = false;
            Time.timeScale = 1f;
            if (Cam != null) { Cam.targets.Clear(); Cam.useOverride = false; }
        }
    }
}
