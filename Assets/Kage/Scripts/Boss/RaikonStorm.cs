using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Lightning Ascendant's standing look between moves, one level per phase (set by each phase's <see cref="PhaseDef.onEnter"/>):
    /// <list type="bullet">
    /// <item>Level 0 (Pure Samurai): a faint static on the blade: a tiny white spark every half second or so, rarely a hair-thin arc.</item>
    /// <item>Level 1 (Storm Blade): crimson arcs crawling along the blade several times a second.</item>
    /// <item>Level 2 (Lightning Ascendant, Final Overload): denser blade arcs, an arc crawling over the body now and then, and the permanent red eye
    /// trail (a thin <see cref="MotionTrail"/> behind the eye).</item>
    /// </list>
    /// None of it uses a tell's shape (docs/raikon-ascendant-vfx.md: the three-colour rule). It draws nothing headless or once he is knocked out.
    /// </summary>
    public sealed class RaikonStorm : MonoBehaviour
    {
        Fighter _f;
        float _next, _bodyNext;
        MotionTrail _eye;

        /// <summary>The current level (0..2).</summary>
        public int Level { get; private set; }

        /// <summary>Whether the eye trail is lit.</summary>
        public bool EyeTrail => Level >= 2;

        /// <summary>A phase's <see cref="PhaseDef.onEnter"/>: the storm on <paramref name="f"/> at the level of phase <paramref name="phase"/>.</summary>
        public static void Enter(Fighter f, int phase)
        {
            if (f == null) return;
            var s = f.GetComponent<RaikonStorm>();
            if (s == null) s = f.gameObject.AddComponent<RaikonStorm>();
            s._f = f;
            s.SetLevel(Mathf.Clamp(phase, 0, 2));
        }

        void SetLevel(int level)
        {
            Level = level;
            if (!EyeTrail) DropEye();
        }

        void DropEye()
        {
            _eye?.Dispose();
            _eye = null;
        }

        void Update()
        {
            var v = Vfx.I;
            if (_f == null || v == null || _f.rig == null) return;
            if (_f.state == FState.KO || _f.health <= 0f) { DropEye(); return; }
            float dt = Time.deltaTime;
            _next -= dt;
            if (_next <= 0f)
            {
                switch (Level)
                {
                    case 0: _next = Random.Range(0.45f, 0.75f); RaikonAscendant.BladeStatic(_f); break;
                    case 1: _next = Random.Range(0.18f, 0.3f); RaikonAscendant.BladeArc(_f, 1); break;
                    default: _next = Random.Range(0.09f, 0.15f); RaikonAscendant.BladeArc(_f, Random.value < 0.35f ? 2 : 1); break;
                }
            }
            if (Level < 2) return;
            _bodyNext -= dt;
            if (_bodyNext <= 0f)
            {
                _bodyNext = Random.Range(0.5f, 0.85f);
                v.Crackle(_f.Pos + Vector2.up * 1.0f, 0.42f, 0.85f, 1);
            }
            _eye ??= new MotionTrail("RaikonEyeTrail", v.transform, 0.22f, Vfx.EyeRed, Color.white, Vfx.Order + 35, false);
            Vector2 eye = RaikonAscendant.EyePos(_f, _f.Pos + Vector2.up * 1.9f);
            _eye.Tick(true, eye, eye + new Vector2(0f, 0.035f));
        }

        void OnDestroy() => DropEye();
    }
}
