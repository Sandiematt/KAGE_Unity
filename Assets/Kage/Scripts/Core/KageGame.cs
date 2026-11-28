using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Kage
{
    /// <summary>
    /// Dev arena entry point (ADR-0001). Builds everything at runtime from code — camera, post-processing, stage,
    /// VFX, audio, HUD, Kage and the Ronin — then runs rounds (best of 3, 60 s) or the
    /// training dojo with the physics bag.
    /// Hotkeys: Tab move list · F2 duel/training · F3 AI level · F4 AI passive · F5 next opponent · F6 group fight (Kage vs 4 Footmen) and back
    /// · F7 break/restore sword · F8 next weapon (Shift: level) · F10 stretch: Kage on a road with gates and waves, and back (Shift: next road; KageGame.Stretch.cs) · R restart · P pause.
    /// The group fight (SAN-83) plays like the duel's rounds: Kage wins a round when every foe is down. Two foes at most are
    /// engaged (<see cref="EngagementManager"/>); the camera frames Kage and the engaged foes (<see cref="CameraRig.Frame"/>).
    /// The rounds and the clock are <see cref="FightSession"/>'s (SAN-102): a duel is best of three on a 60 s clock; a foe with phases
    /// (<see cref="CharacterDef.bossId"/>: the test Footman Captain, Raikon) is fought in the boss format, one round with no clock, and shows the
    /// boss bar (<see cref="Hud.BindBoss"/>).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public partial class KageGame : MonoBehaviour
    {
        public static KageGame I;
        public const int LayerFighter = PhysicsLayers.Fighter, LayerRagdoll = PhysicsLayers.Ragdoll, LayerGround = PhysicsLayers.Ground, LayerProp = PhysicsLayers.Prop;

        public enum Mode { Duel, Training, Stretch }
        public Mode startMode = Mode.Duel;
        [Range(0, 2)] public int aiLevel = 1;
        [Tooltip("The opponent's character id (ronin, order_footman, ...). F5 cycles it.")]
        public string foeId = Ronin.Id;
        /// <summary>F5 sets this and reloads the arena; the new arena takes it once.</summary>
        public static string NextFoeId;
        [Tooltip("Group fight (F6, SAN-83): Kage against this many copies of the group foe. 0 or 1 = the duel.")]
        public int groupSize;
        [Tooltip("The group fight's foe (character id).")]
        public string groupFoeId = OrderFootman.Id;
        /// <summary>The group fight's size (F6): Kage against four Footmen.</summary>
        public const int GroupFightSize = 4;
        /// <summary>F6 sets this and reloads the arena; the new arena takes it once (0 = back to the duel).</summary>
        public static int? NextGroupSize;
        /// <summary>Where the group starts, Kage at 0: the nearest two (one each side) are engaged, the others wait.</summary>
        static readonly float[] GroupStart = { 1.3f, -2.4f, 2.8f, -3.8f };
        /// <summary>Every foe: the duel's one, or the group (<see cref="enemy"/> is the first).</summary>
        public readonly System.Collections.Generic.List<Fighter> foes = new();
        readonly System.Collections.Generic.List<AIInputSource> _ais = new();
        readonly System.Collections.Generic.List<Transform> _foeShadows = new();
        /// <summary>True in the group fight.</summary>
        public bool Group => foes.Count > 1;
        /// <summary>
        /// Where the arena reads Kage's loadout from: the equipped weapon, hairstyle and armour and their levels (SAN-84). Null = the
        /// save file on disk (<see cref="SaveStore"/>), so the arena shows what the campaign would fight with. Tests set it.
        /// </summary>
        public static System.Func<Rules.SaveData> SaveSource;

        public Hud Hud { get; private set; }
        public KageAudio Audio { get; private set; }
        public CameraRig Cam { get; private set; }
        public Fighter player, enemy;
        public PunchingBag bag;
        public Mode mode;
        Camera _camera;
        Volume _volume;
        Bloom _bloom;
        ChromaticAberration _ca;
        Vignette _vig;
        AIInputSource _ai;
        [Tooltip("Fight seed. 0 = a new seed every run (the seed is published in FightStarted).")]
        public int fixedSeed;
        public FightContext Fight { get; private set; }

        /// <summary>The rounds and the clock of this arena fight (SAN-102).</summary>
        public FightSession Session { get; private set; }
        bool _firstStrike;
        Transform _shadowP, _shadowE;
        static readonly string[] AiNames = { "EASY", "NORMAL", "HARD" };

        void Awake()
        {
            I = this;
            Time.timeScale = 1f;
            Application.targetFrameRate = 120;
            transform.position = Vector3.zero;
            // the fight: context, loop (60 Hz step), physics rules; fighters are added below
            var fight = new FightBuilder(transform, fixedSeed != 0 ? fixedSeed : System.Environment.TickCount, manualClock: false);

            var stretchData = TakeStretchRequest();     // F10 (SAN-89, KageGame.Stretch.cs): a road instead of the arena

            BuildCamera();
            BuildVolume();

            var vfxGo = new GameObject("VFX");
            vfxGo.transform.SetParent(transform, false);
            vfxGo.AddComponent<Vfx>();

            Audio = new GameObject("Audio").AddComponent<KageAudio>();
            Audio.transform.SetParent(transform, false);
            fight.Context.Audio = Audio;

            var stage = new GameObject("Stage");
            stage.transform.SetParent(transform, false);
            if (stretchData != null) Road = StretchRoad.Build(stage.transform, stretchData, fight.Context.Bounds, Cam);
            else if (!BossArena.TryBuild(stage.transform, Cam, (NextGroupSize ?? groupSize) > 1 ? null : NextFoeId ?? foeId)) ArenaBuilder.Build(stage.transform, Cam);   // a sub-boss fights in its arena (SAN-103)

            var hudGo = new GameObject("HUD", typeof(RectTransform));
            hudGo.transform.SetParent(transform, false);
            Hud = hudGo.AddComponent<Hud>();
            Hud.Build(_camera);

            var save = SaveSource != null ? SaveSource() : SaveStore.AtPersistentDataPath().Load();
            Hud.SetPlayerLevel(save.level);
            Hud.PauseRequested += TogglePause;
            player = fight.Spawn(KageCharacter.Def(save), 0, new PlayerInputSource());
            if (stretchData != null) { BuildStretch(fight, stretchData); return; }      // the session brings the foes
            _ai = new AIInputSource();
            if (NextFoeId != null) { foeId = NextFoeId; NextFoeId = null; }
            if (NextGroupSize.HasValue) { groupSize = NextGroupSize.Value; NextGroupSize = null; }
            var foeDef = (groupSize > 1 ? CharacterCatalog.Get(groupFoeId) : CharacterCatalog.Get(foeId)) ?? CharacterCatalog.Get(Ronin.Id);
            if (groupSize > 1) groupFoeId = foeDef.id; else foeId = foeDef.id;
            enemy = fight.Spawn(foeDef, 1, _ai);
            foes.Add(enemy);
            _ais.Add(_ai);
            for (int i = 1; i < groupSize; i++)
            {
                var ai = new AIInputSource();
                _ais.Add(ai);
                foes.Add(fight.Spawn(foeDef, 1, ai));
            }
            player.onHit += OnFighterHit;
            player.onKO += OnKO;
            foreach (var f in foes) { f.onHit += OnFighterHit; f.onKO += OnKO; }
            for (int i = 1; i < foes.Count; i++) foes[i].SetSortBase(FightBuilder.FoeSortOrder - 400 * i);     // each foe its own depth
            Hud.Bind(player);
            Hud.SetNames("KAGE  影", foeDef.displayName.ToUpperInvariant() + (Group ? "  ×" + foes.Count : ""));

            _shadowP = MakeShadow("KageShadow", Palette.Trim);
            foreach (var _ in foes) _foeShadows.Add(MakeShadow("FoeShadow", foeDef.colors.main));
            _shadowE = _foeShadows[0];

            bag = fight.AddBag(stage.transform, new Vector2(1.6f, 2.65f));
            Fight = fight.Build();
            Fight.Camera = Cam;
            Fight.Hud = Hud;        // what a rage art moves: the camera's override and the cut-in band (SAN-82)
            Hud.BindEvents(Fight.Events);       // the HUD hears hits, phases and waves from the stream (SAN-108)
            Fight.Events.Published += OnCombatEvent;
            Fight.Begin();
            Cam.targets.Add(player.rig.Bone(B.Hips));
            Cam.targets.Add(enemy.rig.Bone(B.Hips));

            var format = groupSize > 1 ? FightFormat.Duel : FightFormat.For(foeDef);
            Session = new FightSession(Fight, format, player, foes);
            Session.RoundStarting += PlaceFighters;
            Session.IntroBeat += () => Hud.Announce(Session.Format == FightFormat.Boss ? foeDef.displayName.ToUpperInvariant() : "ROUND " + Session.Round, Color.white, 1.0f);
            Session.FightBegan += () => Hud.Announce(Session.Format == FightFormat.Boss ? "FIGHT!" : "DUEL!", Palette.TrimBright, 0.9f);
            Session.RoundEnded += OnRoundEnded;
            Session.MatchEnded += OnMatchEnded;
            Hud.SetFormat(format);
            if (foeDef.bossId != null && groupSize <= 1) Hud.BindBoss(enemy, BossCatalog.Get(foeDef.bossId), Fight.Events);

            SetMode(startMode);
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
            var data = _camera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            }
            Cam = _camera.GetComponent<CameraRig>();
            if (Cam == null) Cam = _camera.gameObject.AddComponent<CameraRig>();
            Cam.cam = _camera;
        }

        void BuildVolume()
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            _bloom = p.Add<Bloom>(true);
            _bloom.threshold.Override(0.92f);
            _bloom.intensity.Override(1.15f);
            _bloom.scatter.Override(0.68f);
            _bloom.tint.Override(new Color(1f, 0.86f, 0.82f));
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

        // =========================================================================================
        void SetMode(Mode m)
        {
            mode = m;
            bool training = m == Mode.Training;
            foreach (var f in foes) f.gameObject.SetActive(!training);
            foreach (var s in _foeShadows) s.gameObject.SetActive(!training);
            bag.gameObject.SetActive(training);
            player.opponent = training ? null : enemy;
            player.extraTarget = training ? bag : null;
            player.roundsWon = enemy.roundsWon = 0; player.ResetForFight();
            foreach (var f in foes) f.ResetForFight();
            Hud.SetMode(training ? "TRAINING DOJO  ·  F2 to duel" : ModeText());
            foreach (var ai in _ais) ai.level = aiLevel switch { 0 => 0.3f, 1 => 0.6f, _ => 0.9f };
            if (training)
            {
                bag.ResetBag();
                player.ResetForRound(-0.4f, 1f);
                player.BeginFight();
                player.meter = 100f;
                Hud.Announce("TRAINING", Palette.TrimBright, 1.4f);
                Cam.targets.Remove(enemy.rig.Bone(B.Hips));
                Cam.Frame(null, null);
            }
            else
            {
                if (!Cam.targets.Contains(enemy.rig.Bone(B.Hips))) Cam.targets.Add(enemy.rig.Bone(B.Hips));
                if (Group) Cam.Frame(Fight, player);     // Kage and the engaged foes (SAN-83)
                Session.Restart();
            }
            Cam.Snap();
        }

        /// <summary>A round is about to begin (<see cref="FightSession.RoundStarting"/>): puts the fighters at their marks and rings the gong.</summary>
        void PlaceFighters()
        {
            _firstStrike = false;
            if (Group)
            {
                player.ResetForRound(0f, 1f);
                for (int i = 0; i < foes.Count; i++)
                {
                    float x = i < GroupStart.Length ? GroupStart[i] : (i % 2 == 0 ? 1f : -1f) * (2.6f + 1.2f * (i / 2));
                    foes[i].ResetForRound(x, x > 0f ? -1f : 1f);
                }
            }
            else
            {
                player.ResetForRound(-1.25f, 1f);
                enemy.ResetForRound(1.25f, -1f);
            }
            player.meter = Mathf.Min(player.meter, 100f);
            Audio.Play(SfxKind.Gong, 0.6f);
        }

        void OnRoundEnded(RoundOutcome o)
        {
            switch (o.Reason)
            {
                case RoundEnd.KnockOut: Hud.Announce("K.O.", KMath.Hex("#FF4B4B"), 1.8f); break;
                case RoundEnd.TimeUp: Hud.Announce("TIME", Color.white, 1.8f); break;
                default: Hud.Announce("DRAW", Color.white, 1.8f); break;
            }
        }

        void OnMatchEnded(Fighter winner)
        {
            bool kageWon = winner == player;
            Hud.Announce(kageWon ? "KAGE WINS" : enemy.Character.displayName.ToUpperInvariant() + " WINS", kageWon ? Palette.TrimBright : enemy.fxMain, 3.5f);
            Hud.Banner(Session.Format == FightFormat.Boss ? "PRESS  R  TO FIGHT AGAIN" : "PRESS  R  TO DUEL AGAIN", Color.white, 30f);
        }

        /// <summary>
        /// A script recompile during Play (Editor hot reload) wipes the runtime-built state: statics,
        /// rig definitions, movesets, cloth. Nothing here is designed to survive that, so detect it
        /// (the static instance comes back null) and rebuild the scene from scratch instead of
        /// throwing every frame.
        /// </summary>
        bool ReloadedUnderUs()
        {
            if (I == this) return false;
            if (I == null && player != null && (player.def == null || player.moves == null))
            {
                I = this;   // only once
                Debug.Log("[KAGE] Scripts reloaded during Play: restarting the scene.");
#if UNITY_EDITOR
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                    new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#endif
                return true;
            }
            if (I == null) I = this;
            return false;
        }

        void Update()
        {
            if (ReloadedUnderUs()) return;
            GameFeel.Tick();
            HandleHotkeys();
            if (mode == Mode.Stretch) { UpdateStretch(); return; }
            float dt = Time.unscaledDeltaTime;

            if (mode == Mode.Duel) Session.Update(dt, Time.deltaTime);
            else
            {
                player.health = player.maxHealth;
                player.meter = Mathf.Min(100f, player.meter + Time.deltaTime * 12f);
                Hud.SetTraining($"BAG DAMAGE  {bag.totalDamage:0}    ·    R resets the bag");
            }
            if (mode == Mode.Duel) Hud.SetTraining("");

            // combo counters end when the victim recovers
            ResetComboIfRecovered(player, Group ? player.opponent : enemy);
            foreach (var f in foes) ResetComboIfRecovered(f, player);

            // the right-hand bar: the foe, or in a group fight Kage's current target
            Hud.Tick(player, mode == Mode.Duel ? (Group && player.opponent != null ? player.opponent : enemy) : null, Session.TimeLeft, Session.Round, mode == Mode.Duel && Session.Format.HasClock);
            UpdatePost();
            UpdateShadows();
        }

        void ResetComboIfRecovered(Fighter attacker, Fighter victim)
        {
            if (attacker == null) return;
            if (mode == Mode.Training)
            {
                if (attacker.state != FState.Attack && attacker.comboCount > 0 && Time.time - _lastBagHit > 0.9f) attacker.comboCount = 0;
                return;
            }
            if (victim == null) return;
            if (!(victim.state is FState.Hitstun or FState.Launched or FState.Thrown or FState.KO or FState.Down)) attacker.comboCount = 0;
        }

        void UpdateShadows()
        {
            void Place(Transform s, Fighter f)
            {
                if (f == null || !f.gameObject.activeInHierarchy) return;
                Vector2 hips = f.rig.Bone(B.Hips).position;
                float h = Mathf.Max(0f, hips.y - 0.6f);
                s.position = new Vector3(hips.x, 0.01f, 0f);
                float k = Mathf.Clamp(1f - h * 0.35f, 0.35f, 1f);
                s.localScale = new Vector3(k, k, 1f);
            }
            Place(_shadowP, player);
            for (int i = 0; i < foes.Count; i++) Place(_foeShadows[i], foes[i]);
        }

        void UpdatePost()
        {
            GameFeel.ApplyPost(_bloom, _vig, _ca, player);
            if (player != null && enemy != null && mode == Mode.Duel && !Group)
            {
                bool pFront = player.IsAttacking || !enemy.IsAttacking;
                player.SetSortBase(pFront ? 1500 : 1000);
                enemy.SetSortBase(pFront ? 1000 : 1500);
            }
        }

        /// <summary>P, Esc and the gamepad's Start pause the arena, like the HUD's pause button (SAN-108).</summary>
        static bool PausePressed(Keyboard kb)
        {
            var pad = Gamepad.current;
            return kb.pKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame || (pad != null && pad.startButton.wasPressedThisFrame);
        }

        /// <summary>Stops and restarts the arena's game time (the HUD's pause button, P, Esc, Start).</summary>
        public void TogglePause() => GameFeel.paused = !GameFeel.paused;

        void HandleHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (HandleStretchHotkeys(kb)) return;       // F10, and every key in stretch mode (SAN-89)
            if (kb.tabKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame) Hud.ToggleMoveList();
            if (kb.f2Key.wasPressedThisFrame) SetMode(mode == Mode.Duel ? Mode.Training : Mode.Duel);
            if (kb.f3Key.wasPressedThisFrame)
            {
                aiLevel = (aiLevel + 1) % 3;
                foreach (var ai in _ais) ai.level = aiLevel switch { 0 => 0.3f, 1 => 0.6f, _ => 0.9f };
                Hud.SetMode(ModeText());
                Hud.Banner("AI  " + AiNames[aiLevel], Color.white, 1.2f);
            }
            if (kb.f4Key.wasPressedThisFrame)
            {
                _ai.passive = !_ai.passive;
                foreach (var ai in _ais) ai.passive = _ai.passive;
                Hud.Banner(_ai.passive ? "AI PASSIVE (practice)" : "AI ACTIVE", Color.white, 1.2f);
            }
            if (kb.f5Key.wasPressedThisFrame) ReloadWithFoe(NextFoe(foeId));      // dev: next opponent (SAN-69)
            if (kb.f6Key.wasPressedThisFrame) ReloadWithGroup(Group ? 0 : GroupFightSize);      // dev: Kage vs 4 Footmen and back (SAN-83)
            if (PausePressed(kb)) TogglePause();
            if (kb.f7Key.wasPressedThisFrame)       // dev: break (or restore) the player's sword (SAN-113)
            {
                if (player.SwordBroken) player.RestoreSword(); else player.BreakSword();
                Hud?.Banner(player.SwordBroken ? "SWORD BROKEN (F7)" : "SWORD RESTORED (F7)", Color.white, 1.2f);
            }
            if (kb.f8Key.wasPressedThisFrame)       // dev: next weapon (Shift: next weapon level) for the player (SAN-70)
            {
                if (kb.shiftKey.isPressed) CycleWeaponLevel(); else CycleWeapon();
                var w = Rules.JsTables.Weapon(player.WeaponId);
                Hud?.Banner($"{(w != null ? w.Name : player.WeaponId)}  LV {player.WeaponLevel}  ·  {player.Family.id} (F8)", Color.white, 1.2f);
            }
            if (kb.f9Key.wasPressedThisFrame) ToggleGallery();
            HandleBossReviewKeys(kb);               // dev: F11 the boss's next phase, F12 / Shift+F12 its moves one by one (KageGame.BossReview.cs)
            if (Gallery != null && Gallery.page >= 0)
            {
                if (kb.pageDownKey.wasPressedThisFrame) Gallery.Show(Gallery.page + 1);
                if (kb.pageUpKey.wasPressedThisFrame) Gallery.Show(Gallery.page - 1);
            }
            if (kb.rKey.wasPressedThisFrame)
            {
                if (mode == Mode.Training) { bag.ResetBag(); player.ResetForRound(-0.4f, 1f); player.BeginFight(); }
                else
                {
                    player.meter = 0f; player.ResetForFight();
                    foreach (var f in foes) { f.meter = 0f; f.ResetForFight(); }
                    Session.Restart();
                }
            }
        }

        /// <summary>The opponents the dev arena offers: every foe character in the catalog (not Kage, not the test dummies), the Ronin first.</summary>
        public static System.Collections.Generic.List<string> DevFoeIds()
        {
            var ids = new System.Collections.Generic.List<string>();
            foreach (var id in CharacterCatalog.Ids)
                if (id != Ronin.Id && !id.StartsWith("test_") && !CharacterCatalog.Get(id).IsPlayer) ids.Add(id);
            ids.Sort(System.StringComparer.Ordinal);
            ids.Insert(0, Ronin.Id);
            return ids;
        }

        /// <summary>The opponent after <paramref name="id"/> in <see cref="DevFoeIds"/>, wrapping round; the first for an unknown id.</summary>
        public static string NextFoe(string id)
        {
            var ids = DevFoeIds();
            int i = ids.IndexOf(id);
            return ids[(i + 1) % ids.Count];
        }

        /// <summary>Dev (F5): restarts the arena against another opponent. A rebuild, so nothing of the old fight survives.</summary>
        void ReloadWithFoe(string id)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path)) { Hud?.Banner("no scene to reload", Color.white, 1.2f); return; }
            NextFoeId = id;
            Reload(scene);
        }

        /// <summary>Dev (F6): restarts the arena as the group fight (<paramref name="size"/> foes), or back to the duel (0).</summary>
        void ReloadWithGroup(int size)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path)) { Hud?.Banner("no scene to reload", Color.white, 1.2f); return; }
            NextGroupSize = size;
            Reload(scene);
        }

        static void Reload(UnityEngine.SceneManagement.Scene scene)
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#else
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.path);
#endif
        }

        /// <summary>Dev (F8): equips the player's next weapon of a registered family, at level 1.</summary>
        public void CycleWeapon()
        {
            var list = WeaponFamilies.PlayableWeapons();
            int i = System.Array.IndexOf(list, player.WeaponId);
            player.EquipWeapon(list[(i + 1) % list.Length], 1);
        }

        /// <summary>Dev (Shift+F8): raises the player's weapon level, back to 1 after its maximum.</summary>
        public void CycleWeaponLevel()
        {
            int max = Rules.GameRules.MaxLevel(Rules.GearKind.Weapons, player.WeaponId);
            player.EquipWeapon(player.WeaponId, player.WeaponLevel >= max ? 1 : player.WeaponLevel + 1);
        }

        public PoseGallery Gallery { get; private set; }

        /// <summary>Shows/hides the pose contact sheet (F9). Fighters are hidden while it is open.</summary>
        public void ToggleGallery(int page = 0)
        {
            if (Gallery == null)
            {
                var g = new GameObject("PoseGallery");
                g.transform.SetParent(transform, false);
                Gallery = g.AddComponent<PoseGallery>();
            }
            bool show = Gallery.page < 0;
            if (show) Gallery.Show(page); else Gallery.Hide();
            player.gameObject.SetActive(!show);
            if (mode == Mode.Duel) foreach (var f in foes) f.gameObject.SetActive(!show);
            if (player.rig.scarf != null) player.rig.scarf.gameObject.SetActive(!show);
            GameFeel.paused = false;
        }

        // =========================================================================================
        float _lastBagHit;

        void OnFighterHit(Fighter att, Fighter def, HitInfo info, HitResult res)
        {
            if (res == HitResult.Hit)
            {
                Vector2 p = info.contact + Vector2.up * 0.25f;
                if (!_firstStrike) { _firstStrike = true; Hud.Float(p + Vector2.up * 0.35f, "FIRST STRIKE!", Color.white, 0.85f); }
                if (info.crit) Hud.Float(p, "CRITICAL!", KMath.Hex("#FF4B4B"), 1.1f);
                else if (info.zone == HitZone.Head && info.dmg >= 5) Hud.Float(p, "HEAD HIT!", KMath.Hex("#FFC35A"), 0.95f);
                else if (info.w.heavy) Hud.Float(p, "HARD!", KMath.Hex("#FFC35A"), 0.9f);
                Hud.Float(info.contact + new Vector2(0f, 0.6f), info.dmg.ToString(), new Color(1f, 1f, 1f, 0.85f), 0.6f);
            }
            else if (res == HitResult.Blocked) Hud.Float(info.contact + Vector2.up * 0.3f, "GUARD", new Color(0.75f, 0.85f, 1f), 0.7f);
        }

        void OnCombatEvent(CombatEvent e)
        {
            // prop hits: the event names the prop, and there is no target fighter
            if (e.Target == null && e.TargetId != null && e.Type is CombatEventType.Hit or CombatEventType.Blocked or CombatEventType.Countered)
                OnPropHit(e);
        }

        void OnPropHit(CombatEvent e)
        {
            _lastBagHit = Time.time;
            int dmg = Mathf.RoundToInt(e.Damage);
            Hud.Float(e.Contact + Vector2.up * 0.4f, e.Crit ? "CRITICAL! " + dmg : dmg.ToString(), e.Crit ? KMath.Hex("#FF4B4B") : Color.white, e.Crit ? 1f : 0.7f);
        }

        void OnKO(Fighter loser)
        {
            if (mode != Mode.Duel) return;
            Session.OnKnockOut(loser);
        }

        string ModeText() => Group
            ? $"GROUP FIGHT  ·  {foes.Count} × {enemy.Character.displayName.ToUpperInvariant()}  ·  AI {AiNames[aiLevel]}  ·  F2 to train  ·  F6 duel"
            : "DUEL  ·  AI " + AiNames[aiLevel] + "  ·  F2 to train  ·  F5 foe  ·  F6 group";

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
        }
    }
}
