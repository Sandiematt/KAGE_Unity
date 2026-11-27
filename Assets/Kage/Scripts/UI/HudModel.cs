using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>A phase mark on the boss bar's row of diamonds: behind the boss, now, or still to come.</summary>
    public enum HudPhaseDot { Done, On, Upcoming }

    /// <summary>One fighter's panel (Kage's, or the foe's): portrait medal, name, health with its trail, rage, kunai, posture. Values only.</summary>
    public sealed class HudPanelView
    {
        public bool Visible;
        public string Id, Name, Title, LevelText;
        /// <summary>Health as drawn (eased) and the trail behind it, 0..1.</summary>
        public float Hp = 1f, Chip = 1f;
        /// <summary>The number beside the bar: the JS hit points left.</summary>
        public int HpValue;
        /// <summary>Under a quarter health: the fill pulses.</summary>
        public bool Low;
        /// <summary>1 when the panel was just hit, falling to 0 over <see cref="FightHudModel.HurtSeconds"/> (the JS hurt shake).</summary>
        public float Hurt;
        public float RageFill;
        public bool RageReady;
        /// <summary>How many times Rage has become ready, and the pop of the last one (1 falling to 0).</summary>
        public int RageReadyCount;
        public float RageReadyPop;
        public bool KunaiShown;
        public int Kunai;
        public float Posture;
        public bool PostureBroken;
        /// <summary>Minimal 3-dot timing indicator state under Kage's bar (Ready / Window / Perfect).</summary>
        public HudTimingState TimingState;
        /// <summary>0..1 glow pulse when Perfect Timing lands (falls cleanly back to 0).</summary>
        public float TimingPulse;
    }

    /// <summary>The boss bar: art card, phase marks, title, name, health track.</summary>
    public sealed class HudBossView
    {
        public bool Visible;
        public string Id, Name, Title, ArtId, PhaseName, PhaseLabel;
        public Color Accent;
        /// <summary>Health as drawn (eased), its trail, and the health the bar is heading for.</summary>
        public float Hp = 1f, Chip = 1f, Health = 1f, Hurt;
        public bool Low;
        public int PhaseIndex, PhaseCount;
        public float[] Marks = new float[0];
        public HudPhaseDot[] Dots = new HudPhaseDot[0];
        /// <summary>1 when a new phase opened, falling to 0 (the card glitches).</summary>
        public float PhaseGlitch;
    }

    /// <summary>The round clock and pips.</summary>
    public sealed class HudClockView
    {
        public bool Visible, PipsVisible, Urgent;
        public string Text = "";
        public HudPip[] Pips = new HudPip[0];
    }

    /// <summary>The road progress strip: Kage's place, the encounter marks, the label under it.</summary>
    public sealed class HudRoadView
    {
        public struct Mark { public float At; public bool Done; }

        public bool Visible;
        public float Fill;
        public readonly List<Mark> Marks = new();
        public string Label = "";
        /// <summary>The stretch's wave number from the last wave event, and a pulse (1 falling to 0) when one starts.</summary>
        public int WaveNumber;
        public float WavePulse;
    }

    /// <summary>The combo counter.</summary>
    public sealed class HudComboView
    {
        public bool Visible;
        public int Count;
        /// <summary>1 when the count just went up, falling to 0 (the JS bump); and the opacity easing in and out.</summary>
        public float Bump, Opacity;
    }

    /// <summary>
    /// The in-fight HUD's logic (SAN-108), apart from any drawing. It reads two things and nothing else: a <see cref="HudFrame"/> of plain
    /// values each tick (the fighters' public state, <see cref="HudReader"/>) and the fight's <see cref="CombatEvent"/>s (ADR-0003: a hit
    /// shakes a panel, a phase event moves the boss bar, a wave event flashes the wave label). It decides which panels a fight type
    /// shows, eases the health bars with the delayed damage trail, and keeps the values the uGUI draws. It holds no fighter and no Unity
    /// object, so a test drives it with made-up numbers and a recorded stream.
    /// </summary>
    public sealed class FightHudModel
    {
        public const float HurtSeconds = 0.32f, BumpSeconds = 0.22f, ReadyPopSeconds = 0.4f, GlitchSeconds = 0.7f, WavePulseSeconds = 1f, ComboFadePerSecond = 4f, TimingPulseSeconds = 0.55f;
        const float Low = 0.25f;

        public HudLayout Layout { get; private set; }
        public readonly HudPanelView Player = new(), Foe = new();
        public readonly HudBossView Boss = new();
        public readonly HudClockView Clock = new();
        public readonly HudRoadView Road = new();
        public readonly HudComboView Combo = new();
        public readonly HudCalloutQueue Callouts = new();
        /// <summary>The cue over a boss that would take the rage art now (HudRageCue.cs).</summary>
        public readonly HudRageCueView RageCue = new();
        readonly List<HudMiniBar> _mini = new();
        /// <summary>The foes' small bars in a stretch.</summary>
        public IReadOnlyList<HudMiniBar> MiniBars => _mini;

        /// <summary>The stretch ended: cleared or failed (<see cref="CombatEventType.StageCleared"/> / <see cref="CombatEventType.StageFailed"/>).</summary>
        public bool Finished { get; private set; }
        public bool Cleared { get; private set; }

        readonly HudTrail _pTrail = new(), _fTrail = new(), _bTrail = new();
        HudBossInfo _boss;
        string _foeId;
        bool _started, _wasReady;

        /// <summary>Shows the boss bar for this boss in a <see cref="HudLayout.Boss"/> fight. Its <see cref="HudBossInfo.Id"/> is the boss fighter's id (what its events carry).</summary>
        public void BindBoss(HudBossInfo info)
        {
            _boss = info;
            _bTrail.Reset(1f);
            Boss.Id = info.Id;
            Boss.Name = (info.Name ?? "").ToUpperInvariant();
            Boss.Title = info.Title ?? "";
            Boss.ArtId = info.ArtId;
            Boss.Accent = info.Accent;
            Boss.Marks = info.Marks != null ? (float[])info.Marks.Clone() : new float[0];
            Boss.PhaseCount = info.PhaseCount;
            Boss.Hurt = Boss.PhaseGlitch = 0f;
            SetPhase(0);
        }

        public void ClearBoss()
        {
            _boss = null;
            Boss.Visible = false;
        }

        /// <summary>Back to a fresh fight (a round reset keeps the model; a new fight starts it again).</summary>
        public void Reset()
        {
            _started = false;
            Finished = Cleared = false;
            Road.WaveNumber = 0;
            Road.WavePulse = 0f;
            Player.Hurt = Foe.Hurt = Boss.Hurt = Boss.PhaseGlitch = Player.TimingPulse = 0f;
            Player.TimingState = HudTimingState.Ready;
            Callouts.Clear();
            RageCue.Reset();
            if (_boss != null) SetPhase(0);
        }

        /// <summary>One combat event from the fight's stream. Only reads: it moves view values, never the fight.</summary>
        public void Handle(in CombatEvent e, bool perfectParry = false)
        {
            switch (e.Type)
            {
                case CombatEventType.Hit:
                    if (e.Damage <= 0f || e.TargetId == null) break;
                    if (e.TargetId == Player.Id) Player.Hurt = 1f;
                    else if (_boss != null && e.TargetId == _boss.Id) Boss.Hurt = 1f;
                    else if (e.TargetId == Foe.Id) Foe.Hurt = 1f;
                    break;
                case CombatEventType.Parried:
                    if (!string.IsNullOrEmpty(Player.Id))
                    {
                        bool hasPos = e.Contact != Vector2.zero;
                        if (e.TargetId == Player.Id)
                        {
                            Callouts.Push(perfectParry ? CombatCalloutKind.PerfectParry : CombatCalloutKind.Parry, e.Contact, hasPos);
                            Player.TimingPulse = 1f;
                            Player.TimingState = HudTimingState.Perfect;
                        }
                        else if (e.SourceId == Player.Id) Callouts.Push(CombatCalloutKind.Parried, e.Contact, hasPos);
                    }
                    break;
                case CombatEventType.PerfectDodged:
                    if (!string.IsNullOrEmpty(Player.Id) && e.TargetId == Player.Id)
                    {
                        Callouts.Push(CombatCalloutKind.Dodge, e.Contact, e.Contact != Vector2.zero);
                        Player.TimingPulse = 1f;
                        Player.TimingState = HudTimingState.Perfect;
                    }
                    break;
                case CombatEventType.CounterHit:
                    if (!string.IsNullOrEmpty(Player.Id) && e.SourceId == Player.Id)
                        Callouts.Push(CombatCalloutKind.Counter, e.Contact, e.Contact != Vector2.zero);
                    break;
                case CombatEventType.Countered:
                    if (!string.IsNullOrEmpty(Player.Id))
                    {
                        bool hasPos = e.Contact != Vector2.zero;
                        if (e.TargetId == Player.Id) Callouts.Push(CombatCalloutKind.Counter, e.Contact, hasPos);
                        else if (e.SourceId == Player.Id)       // a boss turning his rage art aside says so (Fighter.RageArtCounter.cs)
                            Callouts.Push(Fighter.IsRageArtId(e.MoveId) ? CombatCalloutKind.RageCountered : CombatCalloutKind.Countered, e.Contact, hasPos);
                    }
                    break;
                case CombatEventType.Clashed:
                    if (!string.IsNullOrEmpty(Player.Id) && (e.SourceId == Player.Id || e.TargetId == Player.Id))
                        Callouts.Push(CombatCalloutKind.Clash, e.Contact, e.Contact != Vector2.zero);
                    break;
                case CombatEventType.GuardBroken:
                    if (!string.IsNullOrEmpty(Player.Id))
                    {
                        bool hasPos = e.Contact != Vector2.zero;
                        if (e.TargetId == Player.Id) Callouts.Push(CombatCalloutKind.GuardBroken, e.Contact, hasPos);
                        else if (e.SourceId == Player.Id || !string.IsNullOrEmpty(e.TargetId)) Callouts.Push(CombatCalloutKind.GuardBreak, e.Contact, hasPos);
                    }
                    break;
                case CombatEventType.BossPhaseChanged:
                    if (_boss == null || e.TargetId != _boss.Id) break;
                    bool changed = e.Index != Boss.PhaseIndex;
                    SetPhase(e.Index);
                    if (changed && e.Index > 0) Boss.PhaseGlitch = 1f;
                    break;
                case CombatEventType.WaveStarted:
                    Road.WaveNumber = e.Index;
                    Road.WavePulse = 1f;
                    break;
                case CombatEventType.WaveCleared:
                    Road.WavePulse = 0.5f;
                    break;
                case CombatEventType.StageCleared:
                    Finished = Cleared = true;
                    break;
                case CombatEventType.StageFailed:
                    Finished = true;
                    Cleared = false;
                    break;
            }
        }

        void SetPhase(int index)
        {
            int n = Mathf.Max(1, Boss.PhaseCount);
            index = Mathf.Clamp(index, 0, n - 1);
            Boss.PhaseIndex = index;
            Boss.PhaseLabel = "PHASE " + Roman(index + 1);
            Boss.PhaseName = _boss != null && _boss.PhaseNames != null && index < _boss.PhaseNames.Length ? _boss.PhaseNames[index].ToUpperInvariant() : "";
            var dots = new HudPhaseDot[n];
            for (int i = 0; i < n; i++) dots[i] = i < index ? HudPhaseDot.Done : i == index ? HudPhaseDot.On : HudPhaseDot.Upcoming;
            Boss.Dots = dots;
        }

        /// <summary>One step of <paramref name="dt"/> seconds (unscaled: the HUD runs through hit-stop) over the state of the fight.</summary>
        public void Tick(float dt, in HudFrame f)
        {
            Layout = f.Layout;
            DecayTimers(dt);

            bool first = !_started;
            _started = true;
            if (first) _pTrail.Reset(Ratio(f.Player));
            UpdatePanel(Player, _pTrail, f.Player, dt, true);

            bool foeNew = f.HasFoe && f.Foe.Id != _foeId;
            if (foeNew || first) { _foeId = f.Foe.Id; _fTrail.Reset(Ratio(f.Foe)); if (!first) { Foe.Hurt = 0f; } if (_boss != null) _bTrail.Reset(Ratio(f.Foe)); }

            // the foe's panel, or the boss bar in its place
            bool bossBar = f.Layout == HudLayout.Boss && _boss != null && f.HasFoe;
            Foe.Visible = f.Layout == HudLayout.Duel && f.HasFoe;
            Boss.Visible = bossBar;
            if (f.HasFoe)
            {
                if (Foe.Visible) UpdatePanel(Foe, _fTrail, f.Foe, dt, false);
                if (_boss != null)
                {
                    _bTrail.Step(Ratio(f.Foe), dt);
                    Boss.Hp = _bTrail.Value;
                    Boss.Chip = _bTrail.Chip;
                    Boss.Health = Ratio(f.Foe);
                    Boss.Low = Ratio(f.Foe) < Low;
                }
            }

            UpdateClock(f);
            UpdateRoad(f);
            UpdateCombo(f.Player.Combo, dt);
            Callouts.Tick(dt);
            RageCue.Step(dt, f.RageCue);

            _mini.Clear();
            if (f.Layout == HudLayout.Stretch && f.MiniBars != null) for (int i = 0; i < f.MiniBars.Count; i++) _mini.Add(f.MiniBars[i]);
        }

        void DecayTimers(float dt)
        {
            Player.Hurt = Mathf.Max(0f, Player.Hurt - dt / HurtSeconds);
            Foe.Hurt = Mathf.Max(0f, Foe.Hurt - dt / HurtSeconds);
            Boss.Hurt = Mathf.Max(0f, Boss.Hurt - dt / HurtSeconds);
            Boss.PhaseGlitch = Mathf.Max(0f, Boss.PhaseGlitch - dt / GlitchSeconds);
            Player.RageReadyPop = Mathf.Max(0f, Player.RageReadyPop - dt / ReadyPopSeconds);
            Player.TimingPulse = Mathf.Max(0f, Player.TimingPulse - dt / TimingPulseSeconds);
            Road.WavePulse = Mathf.Max(0f, Road.WavePulse - dt / WavePulseSeconds);
            Combo.Bump = Mathf.Max(0f, Combo.Bump - dt / BumpSeconds);
        }

        static float Ratio(in HudFighterState s) => s.MaxHealth > 0f ? Mathf.Clamp01(s.Health / s.MaxHealth) : 0f;

        void UpdatePanel(HudPanelView v, HudTrail trail, in HudFighterState s, float dt, bool isPlayer)
        {
            if (isPlayer) v.Visible = true;
            v.Id = s.Id;
            v.Name = s.Name ?? "";
            v.Title = s.Title ?? "";
            v.LevelText = s.Level > 0 ? "LV " + s.Level : "";
            float ratio = Ratio(s);
            trail.Step(ratio, dt);
            v.Hp = trail.Value;
            v.Chip = trail.Chip;
            v.Low = ratio < Low;
            float scale = s.HealthScale > 0f ? s.HealthScale : 1f;
            v.HpValue = Mathf.CeilToInt(Mathf.Max(0f, s.Health) / scale);
            v.Posture = Mathf.Clamp01(s.Posture);
            v.PostureBroken = s.GuardBroken;
            if (!isPlayer) return;
            v.RageFill = Mathf.Clamp01(s.Rage / 100f);
            bool ready = s.CanRageArt && s.Rage >= 100f;
            if (ready && !_wasReady) { v.RageReadyCount++; v.RageReadyPop = 1f; }
            _wasReady = ready;
            v.RageReady = ready;
            v.KunaiShown = s.HasKunai;
            v.Kunai = s.Kunai;
            v.TimingState = v.TimingPulse > 0f ? HudTimingState.Perfect : s.InTimingWindow ? HudTimingState.Window : HudTimingState.Ready;
        }

        void UpdateClock(in HudFrame f)
        {
            Clock.Visible = f.Layout == HudLayout.Duel && f.ClockShown;
            Clock.PipsVisible = Clock.Visible && f.RoundsShown;
            int sec = Mathf.CeilToInt(Mathf.Max(0f, f.TimeLeft));
            Clock.Text = f.ClockRuns ? sec.ToString() : "∞";
            Clock.Urgent = f.ClockRuns && sec <= 10;
            int n = Mathf.Max(1, f.RoundsToWin * 2 - 1);
            if (Clock.Pips.Length != n) Clock.Pips = new HudPip[n];
            int k = 0;
            for (int i = 0; i < f.Player.RoundsWon && k < n; i++) Clock.Pips[k++] = HudPip.Kage;
            for (int i = 0; i < f.Foe.RoundsWon && k < n; i++) Clock.Pips[k++] = HudPip.Foe;
            while (k < n) Clock.Pips[k++] = HudPip.Empty;
        }

        void UpdateRoad(in HudFrame f)
        {
            var r = f.Road;
            Road.Visible = f.Layout == HudLayout.Stretch && r.Active;
            Road.Fill = Mathf.Clamp01(r.Progress);
            Road.Marks.Clear();
            if (r.EncounterAt != null)
                for (int i = 0; i < r.EncounterAt.Length; i++) Road.Marks.Add(new HudRoadView.Mark { At = r.EncounterAt[i], Done = i < r.EncountersCleared });
            if (r.InEncounter) Road.Label = "FOES " + r.FoesLeft + " · WAVE " + Mathf.Max(1, r.Wave) + "/" + Mathf.Max(1, r.WaveCount);
            else if (r.EncounterCount > 0 && r.EncountersCleared >= r.EncounterCount) Road.Label = "THE ROAD IS OPEN";
            else Road.Label = "ENCOUNTERS " + r.EncountersCleared + "/" + r.EncounterCount;
        }

        int _lastCombo;

        void UpdateCombo(int count, float dt)
        {
            bool on = count >= 2;
            if (on && count != _lastCombo) Combo.Bump = 1f;
            _lastCombo = count;
            Combo.Visible = on;
            if (on) Combo.Count = count;
            Combo.Opacity = on ? Mathf.Min(1f, Combo.Opacity + dt * ComboFadePerSecond) : Mathf.Max(0f, Combo.Opacity - dt * ComboFadePerSecond);
        }

        public static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString() };
    }
}
