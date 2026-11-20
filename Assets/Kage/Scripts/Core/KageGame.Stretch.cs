using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Kage.Rules;

namespace Kage
{
    /// <summary>
    /// The dev arena's stretch mode (SAN-89). <b>F10</b> reloads the arena as the test stretch of Footmen
    /// (<see cref="StretchData.DevFootmenId"/>) and F10 again goes back to the duel; <b>Shift+F10</b> reloads it on the next road
    /// (the test stretch, then the Prologue walk and the four Act I stretches, <see cref="DevStretchIds"/>). Kage walks right along the road; a gate closes at each encounter and its waves
    /// arrive one after another (two foes attack at a time, the rest wait). R starts the road again. The HUD's mode line shows the road
    /// progress, the gate and the wave (<see cref="StretchSession.HudText"/>).
    /// <para>Stretch mode builds the road instead of the arena and spawns no foe of its own: <see cref="StretchSession"/> brings them. The
    /// duel's round logic is not used, and the training dojo (F2) is not available here.</para>
    /// </summary>
    public partial class KageGame
    {
        [Tooltip("Stretch (F10, SAN-89): the road to play, 'dev.footmen' or a JS stretch stage id such as 'act1.stretch1'. Empty = the duel.")]
        public string stretchId = "";
        /// <summary>F10 sets this and reloads the arena; the new arena takes it once. "" = back to the duel.</summary>
        public static string NextStretchId;

        /// <summary>The stretch in play, null in the duel, the group fight and the training dojo.</summary>
        public StretchSession Stretch { get; private set; }
        /// <summary>The road of the stretch in play.</summary>
        public StretchRoad Road { get; private set; }

        /// <summary>The tutorial prompts of the road in play (SAN-96: the Prologue), null when the road has none.</summary>
        public StretchTutorial Tutorial { get; private set; }
        readonly InputDeviceTracker _device = new();
        bool _tutorialHold;
        /// <summary>The title card's length in seconds (SAN-96): a road with tutorial prompts keeps Kage still for it.</summary>
        public const float TitleCardSeconds = 2.4f;

        readonly HashSet<Fighter> _hooked = new();
        readonly Dictionary<Fighter, Transform> _stretchShadows = new();
        bool _stretchEnded;
        string _stretchTarget;

        /// <summary>The roads F10 offers: the test stretch of Footmen, then every JS stretch stage in campaign order.</summary>
        public static List<string> DevStretchIds()
        {
            var ids = new List<string> { StretchData.DevFootmenId };
            foreach (var st in JsTables.Stages) if (st.Kind == "stretch") ids.Add(st.Id);
            return ids;
        }

        /// <summary>The road after <paramref name="id"/> in <see cref="DevStretchIds"/>, wrapping round; the first for an unknown id.</summary>
        public static string NextStretch(string id)
        {
            var ids = DevStretchIds();
            int i = ids.IndexOf(id);
            return ids[(i + 1) % ids.Count];
        }

        /// <summary>Awake: takes the pending F10 request (or the inspector's <see cref="stretchId"/>) and returns the road to build, or null for the duel.</summary>
        StretchData TakeStretchRequest()
        {
            if (NextStretchId != null) { stretchId = NextStretchId; NextStretchId = null; }
            return string.IsNullOrEmpty(stretchId) ? null : StretchData.FromId(stretchId);
        }

        /// <summary>Awake, after Kage is spawned: the rest of the stretch arena. <paramref name="fight"/> still builds the foes the session brings.</summary>
        void BuildStretch(FightBuilder fight, StretchData data)
        {
            mode = Mode.Stretch;
            player.onHit += OnFighterHit;
            player.onKO += OnKO;
            Hud.Bind(player);
            Hud.SetNames("KAGE  影", "");
            _shadowP = MakeShadow("KageShadow", Palette.Trim);

            Fight = fight.Build();
            Fight.Camera = Cam;
            Fight.Hud = Hud;
            Hud.BindEvents(Fight.Events);
            Fight.Events.Published += OnCombatEvent;
            Fight.Begin();

            Stretch = new StretchSession(Fight, fight, data, player);
            Road.Bind(Stretch);
            Cam.SetBounds(Stretch);
            Cam.lookAhead = 2.0f;
            Cam.Frame(Fight, player);
            player.roundsWon = 0;
            player.meter = 0f;
            Stretch.Begin();
            Hud.BindStretch(Stretch);       // the road strip reads the session (SAN-108)
            Cam.Snap();
            Hud.SetTraining("");
            // the plain title card opens the stage (SAN-96); a road with prompts keeps Kage still until it has gone
            Hud.TitleCard((string.IsNullOrEmpty(data.ChapterName) ? "Dev" : data.ChapterName) + " · Stretch", data.Name ?? "Stretch", null, TitleCardSeconds);
            if (data.Prompts.Length > 0)
            {
                Tutorial = new StretchTutorial(Fight, Stretch, player);
                Tutorial.PromptCleared += _ => Audio.Play(SfxKind.Counter, 0.35f);
                player.controlEnabled = false;
                _tutorialHold = true;
            }
            Audio.Play(SfxKind.Gong, 0.6f);
        }

        /// <summary>Update in stretch mode: the prompt slip follows the tutorial and the last device used, and the title card releases Kage.</summary>
        void UpdateTutorialHud()
        {
            if (_tutorialHold && !Hud.TitleCardShowing)
            {
                _tutorialHold = false;
                if (!_stretchEnded) player.controlEnabled = true;
            }
            var t = Tutorial;
            if (t == null) return;
            t.Device = _device.Poll();
            Vector2 head = player.Pos + Vector2.up * 2.3f;
            Hud.SetPrompt(_tutorialHold ? null : t.NowView(), t.DoneView(), head);
        }

        /// <summary>Update in stretch mode (the duel's round logic doesn't run).</summary>
        void UpdateStretch()
        {
            var s = Stretch;
            int n = 0;
            foreach (var f in s.Foes)
            {
                if (_hooked.Add(f)) { f.onHit += OnFighterHit; f.SetSortBase(FightBuilder.FoeSortOrder - 400 * (n % 4)); }
                n++;
            }
            _hooked.RemoveWhere(f => f == null);

            if (!_stretchEnded && (s.Cleared || s.Failed))
            {
                _stretchEnded = true;
                player.controlEnabled = false;
                if (s.Cleared) Hud.Announce("STRETCH CLEARED", Palette.TrimBright, 3.2f);
                else Hud.Announce("FALLEN", KMath.Hex("#FF4B4B"), 3.2f);
                Hud.Banner("PRESS  R  TO PLAY THE ROAD AGAIN", Color.white, 30f);
            }
            if (_stretchEnded && s.Cleared && player.state != FState.Victory && player.Grounded && !player.Busy) player.Win();

            Hud.SetMode(s.HudText + "  ·  R restart  ·  Shift+F10 next road  ·  F10 duel");
            var target = player.opponent != null && player.opponent.state != FState.KO ? player.opponent : null;
            string name = target != null ? target.Character.displayName.ToUpperInvariant() : "";
            if (name != _stretchTarget) { _stretchTarget = name; Hud.SetNames("KAGE  影", name); }
            UpdateTutorialHud();
            Hud.Tick(player, target, 0f, 1, false);

            ResetComboIfRecovered(player, player.opponent);
            foreach (var f in s.Foes) ResetComboIfRecovered(f, player);
            UpdatePost();
            UpdateStretchShadows();
        }

        void UpdateStretchShadows()
        {
            void Place(Transform t, Fighter f)
            {
                if (f == null || !f.gameObject.activeInHierarchy) { t.gameObject.SetActive(false); return; }
                t.gameObject.SetActive(true);
                Vector2 hips = f.rig.Bone(B.Hips).position;
                float h = Mathf.Max(0f, hips.y - 0.6f);
                t.position = new Vector3(hips.x, 0.01f, 0f);
                float k = Mathf.Clamp(1f - h * 0.35f, 0.35f, 1f);
                t.localScale = new Vector3(k, k, 1f);
            }
            Place(_shadowP, player);
            foreach (var f in Stretch.Foes)
            {
                if (!_stretchShadows.TryGetValue(f, out var t)) _stretchShadows[f] = t = MakeShadow("FoeShadow", f.fxMain);
                Place(t, f);
            }
            List<Fighter> gone = null;
            foreach (var kv in _stretchShadows) if (kv.Key == null) (gone ??= new List<Fighter>()).Add(kv.Key);
            if (gone != null) foreach (var f in gone) { Destroy(_stretchShadows[f].gameObject); _stretchShadows.Remove(f); }
        }

        /// <summary>
        /// F10 (and, in stretch mode, every dev key that works there). Returns true when the frame's keys were handled here, so the
        /// duel's hotkeys (which assume a foe) don't run.
        /// </summary>
        bool HandleStretchHotkeys(Keyboard kb)
        {
            if (kb.f10Key.wasPressedThisFrame)
            {
                string next = kb.shiftKey.isPressed ? NextStretch(string.IsNullOrEmpty(stretchId) ? StretchData.DevFootmenId : stretchId)
                            : mode == Mode.Stretch ? "" : StretchData.DevFootmenId;
                ReloadWithStretch(next);
                return true;
            }
            if (mode != Mode.Stretch) return false;
            if (kb.tabKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame) Hud.ToggleMoveList();
            if (kb.f5Key.wasPressedThisFrame) ReloadWithStretch("");
            if (kb.f6Key.wasPressedThisFrame) { NextGroupSize = GroupFightSize; ReloadWithStretch(""); }
            if (PausePressed(kb)) TogglePause();
            if (kb.f7Key.wasPressedThisFrame)
            {
                if (player.SwordBroken) player.RestoreSword(); else player.BreakSword();
                Hud?.Banner(player.SwordBroken ? "SWORD BROKEN (F7)" : "SWORD RESTORED (F7)", Color.white, 1.2f);
            }
            if (kb.f8Key.wasPressedThisFrame)
            {
                if (kb.shiftKey.isPressed) CycleWeaponLevel(); else CycleWeapon();
                var w = JsTables.Weapon(player.WeaponId);
                Hud?.Banner($"{(w != null ? w.Name : player.WeaponId)}  LV {player.WeaponLevel}  ·  {player.Family.id} (F8)", Color.white, 1.2f);
            }
            if (kb.rKey.wasPressedThisFrame) ReloadWithStretch(stretchId);
            return true;
        }

        /// <summary>Dev (F10): restarts the arena on a road (<paramref name="id"/>), or back to the duel (""). A rebuild, so nothing of the old fight survives.</summary>
        void ReloadWithStretch(string id)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path)) { Hud?.Banner("no scene to reload", Color.white, 1.2f); return; }
            NextStretchId = id;
            Reload(scene);
        }
    }
}
