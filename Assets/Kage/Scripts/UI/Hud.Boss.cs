using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// The basic boss bar (SAN-102): bottom centre, as in the JS HUD (<c>.boss-bar</c>): the phase numerals and the title on a top row, the
    /// boss's name, then a wide health track with a mark where each later phase opens, and the current phase's name under it. It is built by
    /// <see cref="Hud.BindBoss"/> and reads the boss's health every tick; the phase comes from <see cref="CombatEventType.BossPhaseChanged"/>
    /// (ADR-0003), so it updates when the fight says so. The JS look (art card, portrait medal, hurt shake) is SAN-108.
    /// </summary>
    public sealed class BossBar
    {
        public const float Width = 880f;

        readonly Fighter _boss;
        readonly BossDef _def;
        readonly CombatEvents _events;
        readonly Hud _hud;
        readonly RectTransform _root;
        readonly Text _name, _title, _numerals, _phaseName;
        readonly InkBar _hp, _trail;
        readonly List<float> _marks = new();
        float _trailVal = 1f, _pulse;

        internal BossBar(Hud hud, RectTransform root, Fighter boss, BossDef def, CombatEvents events,
            Text name, Text title, Text numerals, Text phaseName, InkBar hp, InkBar trail, IEnumerable<float> marks)
        {
            _hud = hud; _root = root; _boss = boss; _def = def; _events = events;
            _name = name; _title = title; _numerals = numerals; _phaseName = phaseName; _hp = hp; _trail = trail;
            _marks.AddRange(marks);
            name.text = def.name.ToUpperInvariant();
            title.text = def.title ?? "";
            if (events != null) events.Published += OnEvent;
            PhaseIndex = boss.Phases != null ? boss.Phases.Index : 0;
            Refresh();
        }

        public string Name => _name.text;
        public string Title => _title.text;
        /// <summary>The boss's health as the bar shows it, 0..1.</summary>
        public float Fill => _hp.fill;
        /// <summary>The current phase (0 = the first) and how many there are.</summary>
        public int PhaseIndex { get; private set; }
        public int PhaseCount => _def.Count;
        /// <summary>The current phase's name, shown under the track.</summary>
        public string PhaseName => _phaseName.text;
        /// <summary>The health fractions the track marks: where each phase after the first opens.</summary>
        public IReadOnlyList<float> Marks => _marks;
        public bool Visible => _root != null && _root.gameObject.activeSelf;

        void OnEvent(CombatEvent e)
        {
            if (e.Type != CombatEventType.BossPhaseChanged || e.Target != _boss) return;
            bool changed = e.Index != PhaseIndex;
            PhaseIndex = e.Index;
            Refresh();
            if (changed && e.Index > 0) { _pulse = 1f; _hud.Banner("PHASE " + Roman(e.Index + 1) + "  ·  " + e.Name.ToUpperInvariant(), KMath.Hex("#FFD08A"), 1.6f); }
        }

        void Refresh()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _def.Count; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append(i == PhaseIndex ? "<color=#FFD08A>" : i < PhaseIndex ? "<color=#B38A60>" : "<color=#5C4A44>").Append(Roman(i + 1)).Append("</color>");
            }
            _numerals.text = sb.ToString();
            _phaseName.text = _def.phases[Mathf.Clamp(PhaseIndex, 0, _def.Count - 1)].name.ToUpperInvariant();
        }

        internal void Tick(float dt)
        {
            float hp = _boss != null && _boss.maxHealth > 0f ? Mathf.Clamp01(_boss.health / _boss.maxHealth) : 0f;
            _hp.Set(hp);
            _trailVal = _trailVal > hp ? Mathf.MoveTowards(_trailVal, hp, dt * (_boss.Busy ? 0.08f : 0.6f)) : hp;
            _trail.Set(_trailVal);
            _pulse = Mathf.Max(0f, _pulse - dt * 2f);
            var c = Color.Lerp(Color.white, KMath.Hex("#FFD08A"), _pulse);
            if (_name.color != c) _name.color = c;
        }

        internal void Dispose()
        {
            if (_events != null) _events.Published -= OnEvent;
            if (_root != null) Object.Destroy(_root.gameObject);
        }

        static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => n.ToString() };
    }

    public partial class Hud
    {
        BossBar _boss;
        readonly List<GameObject> _rightPanel = new();

        /// <summary>The boss bar, null until <see cref="BindBoss"/>.</summary>
        public BossBar Boss => _boss;

        /// <summary>
        /// Shows the boss bar for <paramref name="boss"/> (phase data <paramref name="def"/>), updated by <paramref name="events"/>' phase changes,
        /// and hides the right-hand foe panel it replaces (the JS HUD does the same for a boss). Binding again replaces the bar.
        /// </summary>
        public BossBar BindBoss(Fighter boss, BossDef def, CombatEvents events)
        {
            ClearBoss();
            var root = new GameObject("BossBar", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(_root, false);
            var anchor = new Vector2(0.5f, 0f);
            Place(root, anchor, Vector2.zero, Vector2.zero);

            var back = MakeBar("TrackBack", anchor, new Vector2(0, 54), new Vector2(BossBar.Width, 30), false, new Color(0.06f, 0.02f, 0.03f, 0.88f), new Color(0.06f, 0.02f, 0.03f, 0.88f));
            back.skew = 16f; back.transform.SetParent(root, false); back.Set(1f);
            var trail = MakeBar("Trail", anchor, new Vector2(0, 54), new Vector2(BossBar.Width - 8f, 22), false, new Color(1f, 0.92f, 0.86f, 0.9f), new Color(1f, 0.92f, 0.86f, 0.9f));
            trail.skew = 16f; trail.transform.SetParent(root, false);
            var hp = MakeBar("Fill", anchor, new Vector2(0, 54), new Vector2(BossBar.Width - 8f, 22), false, KMath.Hex("#8C1B26"), KMath.Hex("#F0605E"));
            hp.skew = 16f; hp.transform.SetParent(root, false);

            // a mark where each later phase opens: the track fills from the left, so a phase that opens at 0.5 is at half way
            var marks = new List<float>();
            for (int i = 1; i < def.Count; i++)
            {
                float at = def.phases[i].at;
                marks.Add(at);
                var m = MakeImage("PhaseMark" + i, root, new Color(1f, 0.93f, 0.84f, 0.95f));
                Place(m.rectTransform, anchor, new Vector2((at - 0.5f) * (BossBar.Width - 8f) + 8f, 54), new Vector2(4f, 38f));
            }

            var numerals = MakeText("Numerals", root, 22, TextAnchor.MiddleLeft, Color.white, anchor, new Vector2(-BossBar.Width / 2f + 120f, 118), new Vector2(240, 30), true);
            numerals.supportRichText = true;
            var title = MakeText("Title", root, 22, TextAnchor.MiddleRight, KMath.Hex("#E6B0A8"), anchor, new Vector2(BossBar.Width / 2f - 200f, 118), new Vector2(400, 30), false);
            var name = MakeText("Name", root, 40, TextAnchor.MiddleCenter, Color.white, anchor, new Vector2(0, 94), new Vector2(700, 50), true);
            var phaseName = MakeText("PhaseName", root, 17, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f), anchor, new Vector2(0, 22), new Vector2(600, 24), false);
            // the name may carry a kanji: use an OS font with Japanese glyphs when one exists (as SetNames)
            var jp = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Yu Gothic", "Meiryo", "MS Gothic", "Noto Sans CJK JP", "Arial Unicode MS", "Arial" }, 48);
            if (jp != null) { name.font = jp; title.font = jp; }

            _boss = new BossBar(this, root, boss, def, events, name, title, numerals, phaseName, hp, trail, marks);
            HideRightPanel();
            return _boss;
        }

        /// <summary>Removes the boss bar and shows the right-hand foe panel again.</summary>
        public void ClearBoss()
        {
            if (_boss == null) return;
            _boss.Dispose();
            _boss = null;
            foreach (var g in _rightPanel) if (g != null) g.SetActive(true);
            _rightPanel.Clear();
        }

        void HideRightPanel()
        {
            void Hide(Component c) { if (c != null) { c.gameObject.SetActive(false); _rightPanel.Add(c.gameObject); } }
            var s = _r;
            Hide(s.hpBack); Hide(s.trail); Hide(s.hp); Hide(s.meterBack); Hide(s.meter); Hide(s.meterLabel); Hide(s.postureBack); Hide(s.posture);
            Hide(s.postureLabel); Hide(s.name); Hide(s.notch);
            foreach (var p in s.pips) Hide(p);
        }

        void TickBoss(float dt) => _boss?.Tick(dt);
    }
}
