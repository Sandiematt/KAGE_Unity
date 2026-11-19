using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Kage.Rules;
using Object = UnityEngine.Object;

namespace Kage
{
    /// <summary>Where a stretch is (SAN-89): walking the road, in an encounter behind a closed gate, or over.</summary>
    public enum StretchState { Walking, Encounter, Cleared, Failed }

    /// <summary>
    /// The JS stretch format as a fight session (SAN-89, JS <c>StretchSession</c>): Kage walks a road; reaching an encounter's
    /// trigger closes its gate (the floor's bounds shrink to the arena that ends at the gate) and its waves arrive one after another,
    /// each when the previous is down; the last wave's fall opens the gate; with every encounter cleared and Kage at the end of
    /// the road the stage is cleared. Foes are ordinary fighters on team 1 spawned into the same context, so the two-attackers-at-most
    /// rule (<see cref="EngagementManager"/>) holds with no wiring.
    /// <para>It runs in the fight loop's late tick (<see cref="IFightSession"/>), so it is deterministic: every decision reads the
    /// frame that has just run. It publishes <c>WaveStarted</c>, <c>WaveCleared</c> (Index = the wave's number in the whole stretch,
    /// Name = the encounter id <c>{stage}.e{n}</c>), <c>StageCleared</c> and <c>StageFailed</c> (Kage knocked out; Name = stage id).</para>
    /// <para>The HUD reads <see cref="Progress"/>, <see cref="EncounterNumber"/>, <see cref="WaveNumber"/> and friends, or just
    /// <see cref="HudText"/>. The camera reads <see cref="CameraLeft"/> and <see cref="CameraRight"/>
    /// (<see cref="CameraRig.SetBounds"/>); the road draws its gates from <see cref="GateChanged"/>.</para>
    /// <para><b>Foe ids.</b> Each id is resolved through <see cref="CharacterCatalog"/>. An id with no character yet (Runner,
    /// Shieldbearer, Chainthrower, Ironclad, Flicker, Husk, until their tickets land) is replaced by the Order Footman
    /// (<see cref="FallbackFoeId"/>) and logged once as a warning; <see cref="Spawned"/> records what was asked for and what came.
    /// SAN-109 removes the fallback when every foe exists.</para>
    /// </summary>
    public sealed class StretchSession : IFightSession, ICameraBounds, IDisposable
    {
        /// <summary>The character that stands in for a foe id that has no character yet.</summary>
        public const string FallbackFoeId = OrderFootman.Id;

        /// <summary>JS: the first wave comes 0.35 s after the gate closes.</summary>
        public const int FirstWaveDelay = 21;
        /// <summary>A pause between a wave's fall and the next wave (JS 0.8 s).</summary>
        public const int BetweenWaves = 48;
        /// <summary>A fallen foe stays on the road this many frames, then is removed (JS: it fades after 2.2 s), so a long stretch never piles up bodies.</summary>
        public const int CorpseFrames = 132;

        /// <summary>A foe the session spawned: its fighter id, the id the road asked for and the character that came.</summary>
        public readonly struct SpawnRecord
        {
            public readonly int Frame, Encounter, Wave;
            public readonly string FighterId, RequestedId, CharacterId;
            public SpawnRecord(int frame, int encounter, int wave, string fighterId, string requestedId, string characterId)
            {
                Frame = frame; Encounter = encounter; Wave = wave; FighterId = fighterId; RequestedId = requestedId; CharacterId = characterId;
            }
            public bool FellBack => RequestedId != CharacterId;
        }

        readonly FightContext _ctx;
        readonly FightBuilder _builder;
        readonly Fighter _kage;
        readonly Func<CharacterDef, IInputSource> _foeInput;
        readonly List<Foe> _foes = new();
        readonly List<SpawnRecord> _spawned = new();
        readonly HashSet<string> _warned = new();

        sealed class Foe { public Fighter fighter; public int koFrame = -1; }

        int _cleared, _enc = -1, _wave = -1, _waveNumber, _next;
        bool _waveLive, _begun;

        public StretchData Data { get; }
        public StretchState State { get; private set; } = StretchState.Walking;

        /// <summary>Raised when a gate closes (true) or opens (false); the argument is the encounter's index. For the road's drawing only.</summary>
        public event Action<int, bool> GateChanged;

        /// <param name="foeInput">The input source of each foe (default: an AI at <paramref name="foeLevel"/>); tests pass their own.</param>
        public StretchSession(FightContext ctx, FightBuilder builder, StretchData data, Fighter kage, Func<CharacterDef, IInputSource> foeInput = null, float foeLevel = 0.5f)
        {
            _ctx = ctx;
            _builder = builder;
            Data = data;
            _kage = kage;
            _foeInput = foeInput ?? (_ => new AIInputSource { level = foeLevel });
            kage.turnToWalk = true;
            ctx.Sessions.Add(this);
            ApplyBounds();
        }

        /// <summary>Puts Kage at the road's start and lets him loose. Call once, after the fight context has begun.</summary>
        public void Begin()
        {
            if (_begun) return;
            _begun = true;
            _kage.ResetForRound((float)Data.Start, 1f);
            _kage.BeginFight();
            ApplyBounds();
        }

        public void Dispose()
        {
            _ctx.Sessions.Remove(this);
            if (_kage != null) _kage.turnToWalk = false;
            _ctx.Bounds.Reset();
        }

        // ---------------- what the HUD and the camera read ----------------

        public Fighter Kage => _kage;
        public bool Cleared => State == StretchState.Cleared;
        public bool Failed => State == StretchState.Failed;
        public bool GateClosed => State == StretchState.Encounter;

        /// <summary>Kage's place on the road, 0 at the left end and 1 at the end (the HUD's road strip).</summary>
        public float Progress => Mathf.Clamp01(_kage.Pos.x / Mathf.Max(0.01f, (float)Data.Length));

        /// <summary>Metres from the left end of the road, and the road's length.</summary>
        public float Distance => _kage.Pos.x;
        public float Length => (float)Data.Length;

        public int EncounterCount => Data.Encounters.Length;
        /// <summary>Encounters finished so far.</summary>
        public int EncountersCleared => _cleared;
        /// <summary>The encounter in play, 1-based; 0 while walking.</summary>
        public int EncounterNumber => _enc >= 0 ? _enc + 1 : 0;
        /// <summary>The current wave of the encounter, 1-based; 0 while walking (it stays at the last wave until the gate opens).</summary>
        public int WaveNumber => _enc >= 0 ? Mathf.Max(0, _wave + 1) : 0;
        /// <summary>Waves in the current encounter; 0 while walking.</summary>
        public int WaveCount => _enc >= 0 ? Data.Encounters[_enc].Waves.Length : 0;
        /// <summary>Waves started so far in the whole stretch (the number <c>WaveStarted</c> carries), and all the stretch has.</summary>
        public int StretchWave => _waveNumber;
        public int TotalWaves => Data.TotalWaves;
        /// <summary>True from the gate closing until the next wave of the encounter arrives (the pause between waves).</summary>
        public bool WaitingForWave => _enc >= 0 && !_waveLive && State == StretchState.Encounter;
        /// <summary>The foes on the road still standing.</summary>
        public int FoesLeft => LiveFoes();
        /// <summary>The foes on the road, standing or fallen, not yet removed.</summary>
        public IEnumerable<Fighter> Foes { get { foreach (var f in _foes) if (f.fighter != null) yield return f.fighter; } }
        /// <summary>Every foe spawned so far, in spawn order, with the id asked for.</summary>
        public IReadOnlyList<SpawnRecord> Spawned => _spawned;
        /// <summary>The foe ids that fell back to <see cref="FallbackFoeId"/>.</summary>
        public IEnumerable<string> FallbackIds => _warned;

        /// <summary>The walking limit now: the gate of the first uncleared encounter (the arena's right edge while one is on), else the end.</summary>
        public float WalkLimit => (float)Data.Limit(_cleared);

        /// <summary>The floor's left and right limit: the arena of the encounter in play, else the road from its left end to the next gate.</summary>
        public Vector2 FloorLimits => _enc >= 0 ? new Vector2((float)Data.ArenaLeft(_enc), (float)Data.Encounters[_enc].Gate) : new Vector2(0f, WalkLimit);

        /// <summary>The camera may show this much past the floor's ends, so Kage at a wall is in view with room to spare.</summary>
        public const float CameraMargin = 0.6f;

        /// <summary>What the camera may show: the arena in an encounter (the gate span), else the whole road, plus <see cref="CameraMargin"/>.</summary>
        public float CameraLeft => (_enc >= 0 ? (float)Data.ArenaLeft(_enc) : 0f) - CameraMargin;
        public float CameraRight => (_enc >= 0 ? (float)Data.Encounters[_enc].Gate : Length) + CameraMargin;

        /// <summary>One line for the current HUD: <c>ROAD 43%  ·  ENCOUNTER 1/2  ·  WAVE 2/3  ·  3 FOES</c>.</summary>
        public string HudText
        {
            get
            {
                string road = "ROAD " + Mathf.RoundToInt(Progress * 100f).ToString(CultureInfo.InvariantCulture) + "%";
                switch (State)
                {
                    case StretchState.Cleared: return road + "  ·  STRETCH CLEARED";
                    case StretchState.Failed: return road + "  ·  FALLEN";
                }
                if (_enc < 0) return road + (_cleared >= EncounterCount ? "  ·  REACH THE END" : "  ·  NEXT GATE " + (_cleared + 1) + "/" + EncounterCount);
                return road + "  ·  GATE " + (_enc + 1) + "/" + EncounterCount + "  ·  WAVE " + Mathf.Max(1, _wave + 1) + "/" + WaveCount + "  ·  " + FoesLeft + " FOES";
            }
        }

        // ---------------- the late tick ----------------

        public void LateTick()
        {
            if (!_begun || State is StretchState.Cleared or StretchState.Failed) return;
            if (_kage.state == FState.KO)
            {
                State = StretchState.Failed;
                _ctx.Events.Publish(CombatEvent.StageFailed(_ctx.Clock.Frame, Data.Id));
                return;
            }
            RemoveCorpses();

            if (_enc < 0)
            {
                double x = _kage.Pos.x;
                int i = Data.EncounterAt(_cleared, x);
                if (i >= 0) StartEncounter(i);
                else if (Data.IsComplete(_cleared, x))
                {
                    State = StretchState.Cleared;
                    _ctx.Events.Publish(CombatEvent.StageCleared(_ctx.Clock.Frame, Data.Id));
                }
                return;
            }

            if (_waveLive)
            {
                if (LiveFoes() > 0) return;
                _waveLive = false;
                _ctx.Events.Publish(CombatEvent.WaveCleared(_ctx.Clock.Frame, _waveNumber, EncounterId(_enc)));
                if (_wave + 1 >= Data.Encounters[_enc].Waves.Length) { EndEncounter(); return; }
                _next = BetweenWaves;
                return;
            }
            if (_next > 0) { _next--; return; }
            SpawnWave(_wave + 1);
        }

        string EncounterId(int i) => Data.Id + ".e" + (i + 1).ToString(CultureInfo.InvariantCulture);

        void StartEncounter(int i)
        {
            _enc = i;
            _wave = -1;
            _waveLive = false;
            _next = FirstWaveDelay;
            State = StretchState.Encounter;
            ApplyBounds();
            GateChanged?.Invoke(i, true);
        }

        void EndEncounter()
        {
            int i = _enc;
            _cleared++;
            _enc = -1;
            _wave = -1;
            State = StretchState.Walking;
            ApplyBounds();
            GateChanged?.Invoke(i, false);
        }

        void ApplyBounds()
        {
            var l = FloorLimits;
            _ctx.Bounds.Set(l.x, l.y);
        }

        // ---------------- waves ----------------

        /// <summary>
        /// JS spawnWave: the foes of a wave alternate ahead (near the gate) and behind (the arena's left edge), 90 px apart in each row,
        /// so they close in from both sides; one that would stand within 260 px of Kage stands 320 px from him instead.
        /// </summary>
        void SpawnWave(int k)
        {
            var ids = Data.Encounters[_enc].Waves[k];
            _wave = k;
            _waveNumber++;
            float arenaL = (float)Data.ArenaLeft(_enc), gate = (float)Data.Encounters[_enc].Gate;
            float kx = _kage.Pos.x;
            _placed.Clear();
            for (int j = 0; j < ids.Length; j++)
            {
                bool ahead = j % 2 == 0;
                int row = j / 2;
                float x = ahead ? gate - Px(50) - row * Px(90) : arenaL + Px(50) + row * Px(90);
                if (Mathf.Abs(x - kx) < Px(260)) x = Mathf.Clamp(kx + (ahead ? 1f : -1f) * Px(320), arenaL + Px(30), gate - Px(30));
                // The JS game lets foes that land on the same spot overlap and pushes them apart afterwards. Two bodies starting
                // inside each other are separated in an order that depends on the physics engine's internals, which would make a
                // wave differ from run to run, so each is placed a row (90 px) further into the arena instead.
                for (int tries = 0; tries < 4 && Crowded(x); tries++)
                    x = Mathf.Clamp(x + (ahead ? -1f : 1f) * Px(90), arenaL + Px(30), gate - Px(30));
                _placed.Add(x);
                SpawnFoe(ids[j], x, kx > x ? 1f : -1f);
            }
            _waveLive = true;
            _ctx.Events.Publish(CombatEvent.WaveStarted(_ctx.Clock.Frame, _waveNumber, EncounterId(_enc)));
        }

        readonly List<float> _placed = new();

        bool Crowded(float x)
        {
            foreach (var p in _placed) if (Mathf.Abs(p - x) < Px(60)) return true;
            return false;
        }

        static float Px(float px) => (float)JsUnits.PxToMetres(px);

        void SpawnFoe(string requestedId, float x, float facing)
        {
            var def = ResolveFoe(requestedId, out bool fellBack);
            if (fellBack && _warned.Add(requestedId))
                Debug.LogWarning($"[KAGE] stretch {Data.Id}: foe '{requestedId}' is not built yet; the Order Footman stands in for it (StretchSession.FallbackFoeId).");
            var f = _builder.Spawn(def, 1, _foeInput(def));
            f.ResetForRound(x, facing);
            f.BeginFight();
            _foes.Add(new Foe { fighter = f });
            _spawned.Add(new SpawnRecord(_ctx.Clock.Frame, _enc, _wave, f.FighterId, requestedId, def.id));
        }

        /// <summary>
        /// The character for a foe id on a road: the catalog's entry, or, when the id has no character yet, the Order Footman
        /// (<see cref="FallbackFoeId"/>) with <paramref name="fellBack"/> set. The one place the fallback lives (SAN-109 removes it).
        /// </summary>
        public static CharacterDef ResolveFoe(string requestedId, out bool fellBack)
        {
            var def = CharacterCatalog.Get(requestedId);
            fellBack = def == null;
            return def ?? CharacterCatalog.Get(FallbackFoeId);
        }

        int LiveFoes()
        {
            int n = 0;
            foreach (var f in _foes) if (f.fighter != null && f.fighter.state != FState.KO) n++;
            return n;
        }

        void RemoveCorpses()
        {
            for (int i = _foes.Count - 1; i >= 0; i--)
            {
                var foe = _foes[i];
                var f = foe.fighter;
                if (f == null) { _foes.RemoveAt(i); continue; }
                if (f.state != FState.KO) continue;
                if (foe.koFrame < 0) foe.koFrame = _ctx.Clock.Frame;
                if (_ctx.Clock.Frame - foe.koFrame < CorpseFrames) continue;
                _foes.RemoveAt(i);
                f.Dismiss();
                _ctx.Fighters.Remove(f);
                Object.Destroy(f.gameObject);
            }
        }
    }
}
