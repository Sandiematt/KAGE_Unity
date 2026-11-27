using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kage
{
    /// <summary>
    /// The boss bar (SAN-102, redrawn in the JS style by SAN-108): a read-only handle on what <see cref="Hud.BindBoss"/> put up. Everything it
    /// reports is the HUD model's boss view, which follows the boss's health from the fighter's public state and its phase from
    /// <see cref="CombatEventType.BossPhaseChanged"/> (ADR-0003).
    /// </summary>
    public sealed class BossBar
    {
        readonly HudBossView _v;
        readonly BossDef _def;

        internal BossBar(HudBossView view, BossDef def) { _v = view; _def = def; }

        public string Name => _v.Name;
        public string Title => _v.Title;
        /// <summary>The boss's health fraction the bar is heading for, 0..1 (the drawn fill eases there, <see cref="Shown"/>).</summary>
        public float Fill => _v.Health;
        /// <summary>The fill as drawn, easing down; and the trail behind it.</summary>
        public float Shown => _v.Hp;
        public float Trail => _v.Chip;
        /// <summary>The current phase (0 = the first) and how many there are.</summary>
        public int PhaseIndex => _v.PhaseIndex;
        public int PhaseCount => _def.Count;
        /// <summary>The current phase's name.</summary>
        public string PhaseName => _v.PhaseName;
        /// <summary>The health fractions the track marks: where each phase after the first opens.</summary>
        public IReadOnlyList<float> Marks => _v.Marks;
        public bool Visible => _v.Visible;
        /// <summary>The painted card's image id, or null when the boss has none (the JS bosses without art have no card).</summary>
        public string ArtId => _v.ArtId;
    }

    public partial class Hud
    {
        BossBar _boss;
        bool _hintWas;

        // ---- the bar, built once under the JS root and shown while a boss is bound ----
        RectTransform _bossArt;
        const float BossW = 704f, BossH = 76f, BossTrackY = 59.6f, BossTrackH = 16f;
        const float CardW = 118.4f, CardH = 153.6f;
        Text _bossName, _bossTitle, _bossPhase;
        HudBar _bossFill, _bossChip;
        readonly List<DiamondGraphic> _bossDots = new();
        readonly List<GameObject> _bossMarks = new();
        RawImage _cardImage;
        RadialGlow _cardAura;
        Image _cardHit, _cardBack;
        ShapeGraphic _cardCorner;
        RectTransform _cardRoot, _barRoot;
        string _bossShownKey;
        Vector2 _bossBasePos;
        Color _cardAccent;

        /// <summary>The boss bar, null until <see cref="BindBoss"/>.</summary>
        public BossBar Boss => _boss;

        void BuildBossBar()
        {
            _bottomBar = Container("BossBar", new Vector2(0.5f, 0f), new Vector2(0f, 25.6f), new Vector2(BossW, BossH));
            _barRoot = _bottomBar;
            _bossBasePos = _barRoot.anchoredPosition;
            var track = Ui.Node(_barRoot, "Track", 0, BossTrackY, BossW, BossTrackH);
            var hex = Hex(BossW, BossTrackH, 0f);
            Ui.Shape(track, 0, 0, BossW, BossTrackH, hex, Bone, null, null, false, "Frame");
            Ui.Shape(track, 0, 0, BossW, BossTrackH, Hex(BossW, BossTrackH, 2f), TrackInk, null, null, false, "Ink");
            _bossChip = MakeBar(track, BossW, BossTrackH, Hex(BossW, BossTrackH, 2f), false);
            _bossChip.SetColors(Bone, Bone);
            _bossFill = MakeBar(track, BossW, BossTrackH, Hex(BossW, BossTrackH, 2f), false);
            _bossFill.SetColors(HpRed, HpRed);
            _bossName = Lbl(_barRoot, "", UiFont.TitleHeavy, 34, Cream, 0f, 20f, BossW, 36f, TextAnchor.MiddleCenter, 4.7f);
            _bossTitle = Lbl(_barRoot, "", UiFont.Teko, 15, Gold, 0f, 0f, 300f, 18f, TextAnchor.MiddleLeft, 3.9f);
            _bossPhase = Lbl(_barRoot, "", UiFont.Teko, 15, Hex("#ff8a7a"), 0f, 0f, 120f, 18f, TextAnchor.MiddleLeft, 3f);

            // the art card, left of the bar (inner clip keeps portrait & aura strictly inside the frame with zero blur bleed)
            _cardRoot = Ui.Node(_barRoot, "Art", -(14.4f + CardW), BossH + 8f - CardH, CardW, CardH);
            var cardClip = Ui.Node(_cardRoot, "ArtClip", 0, 0, CardW, CardH);
            cardClip.gameObject.AddComponent<RectMask2D>();
            _cardBack = Ui.Box(cardClip, 0, 0, CardW, CardH, Hex("#0a0710"), "Back");
            _cardAura = Ui.Glow(cardClip, CardW / 2f, CardH * 0.38f, CardW * 0.56f, A(Crimson, 0.34f), 1.6f);
            _cardImage = Ui.Pic(cardClip, "boss_raikon", 0, 0, CardW, CardH);
            Ui.Grad(cardClip, 4, CardH - 40f, CardW - 8f, 36f, A(Hex("#050204"), 0f), A(Hex("#050204"), 0.85f), false);
            _cardHit = Ui.Box(cardClip, 0, 0, CardW, CardH, new Color(1, 1, 1, 0), "Hit");
            Ui.Frame(_cardRoot, 0, 0, CardW, CardH, 2f, Bone);
            Ui.Frame(_cardRoot, 2, 2, CardW - 4f, CardH - 4f, 1.5f, A(Color.black, 0.75f));
            _cardCorner = Ui.Shape(_cardRoot, CardW - 8f, CardH - 28f, 16f, 16f, new[] { new Vector2(8, 0), new Vector2(16, 8), new Vector2(8, 16), new Vector2(0, 8) }, Crimson, null, null, false, "Corner");
            _bossArt = _cardRoot;
            _barRoot.gameObject.SetActive(false);
        }

        /// <summary>A hexagonal track end (JS clip-path: .6rem 0, 100% - .6rem 0, 100% 50%, ...), inset round.</summary>
        static Vector2[] Hex(float w, float h, float i)
        {
            float s = 9.6f;
            return new[]
            {
                new Vector2(s + i, i), new Vector2(w - s - i, i), new Vector2(w - i, h / 2f), new Vector2(w - s - i, h - i), new Vector2(s + i, h - i), new Vector2(i, h / 2f),
            };
        }

        /// <summary>
        /// Shows the boss bar for <paramref name="boss"/> (phase data <paramref name="def"/>), updated by <paramref name="events"/>' phase changes
        /// and hits, and hides the right-hand foe panel it replaces (the JS HUD does the same for a boss). Binding again replaces the bar.
        /// </summary>
        public BossBar BindBoss(Fighter boss, BossDef def, CombatEvents events)
        {
            ClearBoss();
            var info = HudReader.Boss(boss, def);
            _model.BindBoss(info);
            _boss = new BossBar(_model.Boss, def);
            BindEvents(events);
            _bossShownKey = null;
            ApplyBossStatic(info);
            _hintWas = _hint.gameObject.activeSelf;
            _hint.gameObject.SetActive(false);       // the dev arena's key hint sits where the boss bar's card is
            return _boss;
        }

        /// <summary>Removes the boss bar and shows the right-hand foe panel again.</summary>
        public void ClearBoss()
        {
            if (_boss == null) return;
            _model.ClearBoss();
            _boss = null;
            if (_barRoot != null) _barRoot.gameObject.SetActive(false);
            _hint.gameObject.SetActive(_hintWas);
        }

        void ApplyBossStatic(HudBossInfo info)
        {
            // marks: dividers on the track
            foreach (var m in _bossMarks) if (m != null) Destroy(m);
            _bossMarks.Clear();
            var track = _bossFill.transform.parent;
            foreach (float at in info.Marks)
            {
                float x = at * BossW;
                var gold = Ui.Box(track, x - 2f, 0f, 4f, BossTrackH, A(Gold, 0.35f), "DivGold");
                var ink = Ui.Box(track, x - 1f, 0f, 2f, BossTrackH, Hex("#0a0505"), "Div");
                _bossMarks.Add(gold.gameObject); _bossMarks.Add(ink.gameObject);
            }
            _bossName.text = (info.Name ?? "").ToUpperInvariant();
            _bossTitle.text = (info.Title ?? "").ToUpperInvariant();
            _cardAccent = info.Accent;
            var tex = !string.IsNullOrEmpty(info.ArtId) ? Ui.Tex(info.ArtId) : null;
            _cardRoot.gameObject.SetActive(tex != null);
            if (tex != null)
            {
                _cardImage.texture = tex;
                _cardImage.color = Color.white;
                Ui.CoverUv(_cardImage, CardW, CardH, 0.5f, 1f);
                _cardBack.color = Color.Lerp(Hex("#120a12"), info.Accent, 0.34f) * 0.8f;
                _cardCorner.SetColors(info.Accent, info.Accent, info.Accent);
            }
        }

        void ApplyBoss(HudBossView v, float dt)
        {
            bool on = v.Visible && _boss != null;
            if (_barRoot.gameObject.activeSelf != on) _barRoot.gameObject.SetActive(on);
            if (!on) return;
            var off = HurtOffset(v.Hurt);
            _barRoot.anchoredPosition = _bossBasePos + new Vector2(off.x, -off.y);

            float low = v.Low ? 1f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * Mathf.PI / 0.7f)) : 1f;
            float flash = 1f + v.Hurt * 1.4f;
            _bossFill.SetFill(v.Hp);
            _bossChip.SetFill(v.Chip);
            _bossFill.SetColors(Brighten(HpRed, low * flash), Brighten(HpRed, low * flash));

            // the top row: phase diamonds, "PHASE II", then the title; centred as a unit
            string key = v.PhaseLabel + "|" + v.PhaseCount + "|" + v.Dots.Length + "|" + _bossTitle.text;
            if (key != _bossShownKey)
            {
                _bossShownKey = key;
                _bossPhase.text = v.PhaseLabel;
                int n = v.Dots.Length;
                while (_bossDots.Count < n) _bossDots.Add(Ui.Diamond(_barRoot, 0, 0, 9.6f, Color.clear, Crimson, 1f));
                float phaseW = _bossPhase.preferredWidth + 3f * _bossPhase.text.Length + 2f;
                float titleW = _bossTitle.preferredWidth + 3.9f * _bossTitle.text.Length + 2f;
                float groupW = n * 9.6f + n * 7.2f + 3.2f + phaseW + 6.4f;
                float total = groupW + (titleW > 4f ? 12.8f + titleW : 0f);
                float x = (BossW - total) / 2f, cy = 9f;
                for (int i = 0; i < _bossDots.Count; i++)
                {
                    bool use = i < n;
                    if (_bossDots[i].gameObject.activeSelf != use) _bossDots[i].gameObject.SetActive(use);
                    if (!use) continue;
                    float half = 9.6f * 0.7071f + 1f;
                    float cx = x + i * (9.6f + 7.2f) + 4.8f;
                    _bossDots[i].rectTransform.anchoredPosition = new Vector2(cx - half, -(cy - half));
                }
                float px = x + n * (9.6f + 7.2f) + 3.2f;
                _bossPhase.rectTransform.sizeDelta = new Vector2(phaseW, 18f);
                _bossPhase.rectTransform.anchoredPosition = new Vector2(px, 0f);
                _bossTitle.rectTransform.sizeDelta = new Vector2(titleW, 18f);
                _bossTitle.rectTransform.anchoredPosition = new Vector2(x + groupW + 12.8f, 0f);
            }
            for (int i = 0; i < v.Dots.Length && i < _bossDots.Count; i++)
            {
                var d = v.Dots[i];
                Color fill = d == HudPhaseDot.Done ? Hex("#3a2a22") : d == HudPhaseDot.On ? Crimson : Color.clear;
                Color edge = d == HudPhaseDot.Done ? Hex("#3a2a22") : Crimson;
                _bossDots[i].SetColors(fill, edge);
            }

            // the card: floats gently, aura breathes; white flash on a hit, a glitch on a new phase, red pulse under a quarter
            if (_cardRoot.gameObject.activeSelf)
            {
                float t = Time.unscaledTime;
                float sway = Mathf.Sin(t * Mathf.PI * 2f / 6.4f) * 2.4f;
                _cardRoot.anchoredPosition = new Vector2(-(14.4f + CardW), -(BossH + 8f - CardH) + sway);
                float breathe = 1f + 0.035f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / 3.6f));
                _cardImage.rectTransform.localScale = new Vector3(1.04f * breathe, 1.04f * breathe, 1f);
                Color aura = v.Low ? new Color(1f, 0.16f, 0.2f, 0.45f) : A(_cardAccent, 0.36f);
                float auraRate = v.Low ? 0.9f : 2.8f;
                float ab = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f / auraRate));
                aura.a *= ab;
                _cardAura.color = aura;
                _cardHit.color = new Color(1f, 1f, 1f, 0.85f * v.Hurt);
                if (v.PhaseGlitch > 0f)
                {
                    // steps(1): a few jumps in the 0.7 s
                    int step = Mathf.FloorToInt((1f - v.PhaseGlitch) * 5f);
                    float[] dx = { 4f, -5f, 2f, -3f, 0f, 0f };
                    _cardImage.rectTransform.anchoredPosition = new Vector2(dx[Mathf.Clamp(step, 0, 5)] * 0.01f * CardW * 0.5f, 0f);
                    _cardImage.color = step < 2 ? new Color(1.8f, 1.2f, 1.2f, 1f) : step < 3 ? new Color(0.9f, 0.9f, 1.5f, 1f) : Color.white;
                }
                else { _cardImage.rectTransform.anchoredPosition = Vector2.zero; _cardImage.color = Color.white; }
            }
        }
    }
}
