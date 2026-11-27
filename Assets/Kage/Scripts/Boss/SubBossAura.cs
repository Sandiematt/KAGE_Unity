using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Standing between-move motif and Phase 2 escalation for each of the four Act I sub-bosses (wired to <see cref="PhaseDef.onEnter"/>,
    /// parallel to <see cref="RaikonStorm"/>):
    /// <list type="bullet">
    /// <item><b>Kuroe</b>: Phase 1 drips wet sumi ink from the calligraphy brush tip; Phase 2 ("Redacted") bursts with ink &amp; Raikon's crimson
    /// static on entry and crawls red-gold lightning arcs along the inked brush nib.</item>
    /// <item><b>Garan</b>: Phase 1 breathes hot embers from his burnt chest cuirass; Phase 2 ("Stormbroken") erupts with a storm-scar shockwave on
    /// entry and arcs crimson-orange Raikon lightning from his chest scar to both iron tekko gauntlets.</item>
    /// <item><b>Hisame</b>: Phase 1 sheds slanted rain droplets off her mino cape; Phase 2 ("Downpour") bursts in a rain-mist squall on entry and
    /// crackles crimson-blue Raikon static across both iron-ribbed Mist Fans.</item>
    /// <item><b>Shiden</b>: Phase 1 glimmers with disciplined violet static at the Vanguard Yari's point; Phase 2 ("Hold the Line") surges with
    /// lieutenant lightning on entry and crawls violet-crimson arcs up the spear shaft and maedate crest.</item>
    /// </list>
    /// The drawing is each boss's own (Vfx.Kuroe.cs, Vfx.Garan.cs, Vfx.Hisame.cs, Vfx.Shiden.cs). None of these ambient cues uses a tell shape
    /// (docs/raikon-ascendant-vfx.md). Draws nothing headless or after knock-out.
    /// </summary>
    public sealed class SubBossAura : MonoBehaviour
    {
        public enum Kind { Kuroe, Garan, Hisame, Shiden }

        Fighter _f;
        Kind _kind;
        float _next;

        public Kind BossKind => _kind;
        public int Level { get; private set; }

        public static void EnterKuroe(Fighter f, int phase) => Enter(f, Kind.Kuroe, phase);
        public static void EnterGaran(Fighter f, int phase) => Enter(f, Kind.Garan, phase);
        public static void EnterHisame(Fighter f, int phase) => Enter(f, Kind.Hisame, phase);
        public static void EnterShiden(Fighter f, int phase) => Enter(f, Kind.Shiden, phase);

        public static void Enter(Fighter f, Kind kind, int phase)
        {
            if (f == null) return;
            var a = f.GetComponent<SubBossAura>();
            if (a == null) a = f.gameObject.AddComponent<SubBossAura>();
            int prev = a.Level;
            a._f = f;
            a._kind = kind;
            a.Level = Mathf.Clamp(phase, 0, 1);
            if (phase >= 1 && prev < 1) a.PlayPhaseTwoBurst();
        }

        void PlayPhaseTwoBurst()
        {
            if (_f == null) return;
            _f.rig?.Impulse(new Vector2(-3.2f * _f.facing, 3.8f));
            var v = Vfx.I;
            if (v == null || _f.rig == null) return;
            switch (_kind)
            {
                case Kind.Kuroe: v.KuroePhaseBurst(_f); break;
                case Kind.Garan: v.GaranPhaseBurst(_f); break;
                case Kind.Hisame: v.HisamePhaseBurst(_f); break;
                case Kind.Shiden: v.ShidenPhaseBurst(_f); break;
            }
            GameFeel.Shake(0.28f);
        }

        void Update()
        {
            var v = Vfx.I;
            if (_f == null || v == null || _f.rig == null) return;
            if (_f.state == FState.KO || _f.health <= 0f) return;
            _next -= Time.deltaTime;
            if (_next > 0f) return;
            _next = Level == 0 ? Random.Range(0.55f, 0.85f) : Random.Range(0.22f, 0.36f);
            switch (_kind)
            {
                case Kind.Kuroe: v.KuroeAura(_f, Level); break;
                case Kind.Garan: v.GaranAura(_f, Level); break;
                case Kind.Hisame: v.HisameAura(_f, Level); break;
                case Kind.Shiden: v.ShidenAura(_f, Level); break;
            }
        }
    }
}
