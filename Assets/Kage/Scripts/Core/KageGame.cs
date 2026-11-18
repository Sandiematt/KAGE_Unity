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
    /// Hotkeys: Tab move list · F2 duel/training · F3 AI level · F4 AI passive · F5 next opponent · F7 break/restore sword · F8 next weapon (Shift: level) · R restart · P pause.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class KageGame : MonoBehaviour
    {
        public static KageGame I;
        public const int LayerFighter = PhysicsLayers.Fighter, LayerRagdoll = PhysicsLayers.Ragdoll, LayerGround = PhysicsLayers.Ground, LayerProp = PhysicsLayers.Prop;

        public enum Mode { Duel, Training }
        public Mode startMode = Mode.Duel;
        [Range(0, 2)] public int aiLevel = 1;
        [Tooltip("The opponent's character id (ronin, order_footman, ...). F5 cycles it.")]
        public string foeId = Ronin.Id;
        /// <summary>F5 sets this and reloads the arena; the new arena takes it once.</summary>
        public static string NextFoeId;
        /// <summary>
        /// Where the arena reads Kage's loadout from: the equipped weapon, helmet and armour and their levels (SAN-84). Null = the
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

        enum Phase { Intro, Fight, RoundOver, MatchOver }
        Phase _phase;
        float _phaseT, _timer;
        int _round;
        bool _firstStrike, _koHandled;
        Fighter _roundWinner;
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

            BuildCamera();
            BuildVolume();

            var vfxGo = new GameObject("VFX");
            vfxGo.transform.SetParent(transform, false);
            vfxGo.AddComponent<Vfx>();

            Audio = new GameObject("Audio").AddComponent<KageAudio>();
            Audio.transform.SetParent(transform, false);

            var stage = new GameObject("Stage");
            stage.transform.SetParent(transform, false);
            ArenaBuilder.Build(stage.transform, Cam);

            var hudGo = new GameObject("HUD", typeof(RectTransform));
            hudGo.transform.SetParent(transform, false);
            Hud = hudGo.AddComponent<Hud>();
            Hud.Build(_camera);

            player = fight.Spawn(KageCharacter.Def(SaveSource != null ? SaveSource() : SaveStore.AtPersistentDataPath().Load()), 0, new PlayerInputSource());
            _ai = new AIInputSource();
            if (NextFoeId != null) { foeId = NextFoeId; NextFoeId = null; }
            var foeDef = CharacterCatalog.Get(foeId) ?? CharacterCatalog.Get(Ronin.Id);
            foeId = foeDef.id;
            enemy = fight.Spawn(foeDef, 1, _ai);
            player.onHit += OnFighterHit;
            enemy.onHit += OnFighterHit;
            player.onKO += OnKO;
            enemy.onKO += OnKO;
            Hud.Bind(player);
            Hud.SetNames("KAGE  影", foeDef.displayName.ToUpperInvariant());

            _shadowP = MakeShadow("KageShadow", Palette.Trim);
            _shadowE = MakeShadow("FoeShadow", foeDef.colors.main);

            bag = fight.AddBag(stage.transform, new Vector2(1.6f, 2.65f));
            Fight = fight.Build();
            Fight.Events.Published += OnCombatEvent;
            Fight.Begin();
            Cam.targets.Add(player.rig.Bone(B.Hips));
            Cam.targets.Add(enemy.rig.Bone(B.Hips));

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
            enemy.gameObject.SetActive(!training);
            _shadowE.gameObject.SetActive(!training);
            bag.gameObject.SetActive(training);
            player.opponent = training ? null : enemy;
            player.extraTarget = training ? bag : null;
            player.roundsWon = enemy.roundsWon = 0; player.ResetForFight(); enemy.ResetForFight();
            _round = 1;
            Hud.SetMode(training ? "TRAINING DOJO  ·  F2 to duel" : "DUEL  ·  AI " + AiNames[aiLevel] + "  ·  F2 to train  ·  F5 foe");
            _ai.level = aiLevel switch { 0 => 0.3f, 1 => 0.6f, _ => 0.9f };
            if (training)
            {
                bag.ResetBag();
                player.ResetForRound(-0.4f, 1f);
                player.BeginFight();
                player.meter = 100f;
                _phase = Phase.Fight;
                Hud.Announce("TRAINING", Palette.TrimBright, 1.4f);
                Cam.targets.Remove(enemy.rig.Bone(B.Hips));
            }
            else
            {
                if (!Cam.targets.Contains(enemy.rig.Bone(B.Hips))) Cam.targets.Add(enemy.rig.Bone(B.Hips));
                StartRound();
            }
            Cam.Snap();
        }

        void StartRound()
        {
            _koHandled = false;
            _firstStrike = false;
            _roundWinner = null;
            player.ResetForRound(-1.25f, 1f);
            enemy.ResetForRound(1.25f, -1f);
            player.meter = Mathf.Min(player.meter, 100f);
            _phase = Phase.Intro;
            _phaseT = 0f;
            _timer = 60f;
            Audio.Play(SfxKind.Gong, 0.6f);
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
            float dt = Time.unscaledDeltaTime;
            _phaseT += dt;

            if (mode == Mode.Duel)
            {
                switch (_phase)
                {
                    case Phase.Intro:
                        if (_phaseT > 0.35f && _phaseT - dt <= 0.35f) Hud.Announce("ROUND " + _round, Color.white, 1.0f);
                        if (_phaseT > 1.45f)
                        {
                            Hud.Announce("DUEL!", Palette.TrimBright, 0.9f);
                            player.BeginFight();
                            enemy.BeginFight();
                            _phase = Phase.Fight;
                            _phaseT = 0f;
                        }
                        break;
                    case Phase.Fight:
                        _timer -= Time.deltaTime;
                        if (_timer <= 0f) TimeUp();
                        break;
                    case Phase.RoundOver:
                        if (_phaseT > 1.3f && _roundWinner != null && _roundWinner.state != FState.Victory && _roundWinner.Grounded && !_roundWinner.Busy) _roundWinner.Win();
                        if (_phaseT > 3.6f)
                        {
                            if (player.roundsWon >= 2 || enemy.roundsWon >= 2)
                            {
                                _phase = Phase.MatchOver;
                                _phaseT = 0f;
                                bool kageWon = player.roundsWon >= 2;
                                Hud.Announce(kageWon ? "KAGE WINS" : enemy.Character.displayName.ToUpperInvariant() + " WINS", kageWon ? Palette.TrimBright : enemy.fxMain, 3.5f);
                                Hud.Banner("PRESS  R  TO DUEL AGAIN", Color.white, 30f);
                            }
                            else { _round++; StartRound(); }
                        }
                        break;
                }
            }
            else
            {
                player.health = player.maxHealth;
                player.meter = Mathf.Min(100f, player.meter + Time.deltaTime * 12f);
                Hud.SetTraining($"BAG DAMAGE  {bag.totalDamage:0}    ·    R resets the bag");
            }
            if (mode == Mode.Duel) Hud.SetTraining("");

            // combo counters end when the victim recovers
            ResetComboIfRecovered(player, enemy);
            ResetComboIfRecovered(enemy, player);

            Hud.Tick(player, mode == Mode.Duel ? enemy : null, _timer, _round, mode == Mode.Duel);
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
            Place(_shadowE, enemy);
        }

        void UpdatePost()
        {
            if (_ca != null) _ca.intensity.Override(Mathf.Clamp01(GameFeel.aberration));
            if (_vig != null) _vig.intensity.Override(0.34f + (player != null && player.state == FState.Ultimate ? 0.22f : 0f));
            if (player != null && enemy != null && mode == Mode.Duel)
            {
                bool pFront = player.IsAttacking || !enemy.IsAttacking;
                player.SetSortBase(pFront ? 1500 : 1000);
                enemy.SetSortBase(pFront ? 1000 : 1500);
            }
        }

        void HandleHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.tabKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame) Hud.ToggleMoveList();
            if (kb.f2Key.wasPressedThisFrame) SetMode(mode == Mode.Duel ? Mode.Training : Mode.Duel);
            if (kb.f3Key.wasPressedThisFrame)
            {
                aiLevel = (aiLevel + 1) % 3;
                _ai.level = aiLevel switch { 0 => 0.3f, 1 => 0.6f, _ => 0.9f };
                Hud.SetMode("DUEL  ·  AI " + AiNames[aiLevel] + "  ·  F2 to train  ·  F5 foe");
                Hud.Banner("AI  " + AiNames[aiLevel], Color.white, 1.2f);
            }
            if (kb.f4Key.wasPressedThisFrame)
            {
                _ai.passive = !_ai.passive;
                Hud.Banner(_ai.passive ? "AI PASSIVE (practice)" : "AI ACTIVE", Color.white, 1.2f);
            }
            if (kb.f5Key.wasPressedThisFrame) ReloadWithFoe(NextFoe(foeId));      // dev: next opponent (SAN-69)
            if (kb.pKey.wasPressedThisFrame) GameFeel.paused = !GameFeel.paused;
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
            if (Gallery != null && Gallery.page >= 0)
            {
                if (kb.pageDownKey.wasPressedThisFrame) Gallery.Show(Gallery.page + 1);
                if (kb.pageUpKey.wasPressedThisFrame) Gallery.Show(Gallery.page - 1);
            }
            if (kb.rKey.wasPressedThisFrame)
            {
                if (mode == Mode.Training) { bag.ResetBag(); player.ResetForRound(-0.4f, 1f); player.BeginFight(); }
                else { player.roundsWon = enemy.roundsWon = 0; player.meter = 0f; enemy.meter = 0f; player.ResetForFight(); enemy.ResetForFight(); _round = 1; StartRound(); }
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
            if (mode == Mode.Duel) enemy.gameObject.SetActive(!show);
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
            if (e.Type == CombatEventType.GuardBroken && e.Target != null)
                Hud.Float(e.Target.Pos + Vector2.up * 2.0f, "GUARD BREAK!", KMath.Hex("#FF7A4B"), 1.1f);
        }

        void OnPropHit(CombatEvent e)
        {
            _lastBagHit = Time.time;
            int dmg = Mathf.RoundToInt(e.Damage);
            Hud.Float(e.Contact + Vector2.up * 0.4f, e.Crit ? "CRITICAL! " + dmg : dmg.ToString(), e.Crit ? KMath.Hex("#FF4B4B") : Color.white, e.Crit ? 1f : 0.7f);
        }

        void OnKO(Fighter loser)
        {
            if (_koHandled || mode != Mode.Duel) return;
            _koHandled = true;
            var winner = loser == player ? enemy : player;
            winner.roundsWon++;
            winner.controlEnabled = false;
            loser.controlEnabled = false;
            _roundWinner = winner;
            _phase = Phase.RoundOver;
            _phaseT = 0f;
            Hud.Announce("K.O.", KMath.Hex("#FF4B4B"), 1.8f);
        }

        void TimeUp()
        {
            _phase = Phase.RoundOver;
            _phaseT = 0f;
            player.controlEnabled = enemy.controlEnabled = false;
            if (Mathf.Abs(player.health - enemy.health) < 0.5f) { Hud.Announce("DRAW", Color.white, 1.8f); _roundWinner = null; return; }
            _roundWinner = player.health > enemy.health ? player : enemy;
            _roundWinner.roundsWon++;
            Hud.Announce("TIME", Color.white, 1.8f);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            Time.timeScale = 1f;
        }
    }
}
