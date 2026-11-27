using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Lightning Ascendant's standing look and the thunder on everything he does, one level per phase (set by each phase's <see cref="PhaseDef.onEnter"/>).
    /// The phase index is <see cref="Phase"/> (0 Pure Samurai, 1 Storm Blade, 2 Lightning Ascendant, 3 Final Overload); <see cref="Level"/> is the blade static's
    /// level (0..2, as before):
    /// <list type="bullet">
    /// <item>Level 0 (Pure Samurai): a faint static on the blade: a tiny white spark every half second or so, rarely a hair-thin arc.</item>
    /// <item>Level 1 (Storm Blade): crimson arcs crawling along the blade several times a second.</item>
    /// <item>Level 2 (Lightning Ascendant, Final Overload): denser blade arcs, an arc crawling over the body now and then, and the permanent red eye
    /// trail (a thin <see cref="MotionTrail"/> behind the eye).</item>
    /// </list>
    /// On top of that, the thunder pass (docs/raikon-ascendant-vfx.md), all scaled by <see cref="Phase"/>: the standing red aura (<see cref="RaikonAura"/>),
    /// the storm in the backdrop (<see cref="RaikonSkyStorm"/>), lightning along the blade (and down the striking limb) through every active window and a
    /// lightning streak behind every dash, and thunder on his landed and guarded blows (<see cref="Vfx.ThunderImpact"/>); from phase 3 a heavy blow also
    /// calls a bolt down out of a cloud onto the one he hit. None of it uses a tell's shape (the three-colour rule). It draws nothing headless or once he
    /// is knocked out.
    /// </summary>
    public sealed class RaikonStorm : MonoBehaviour
    {
        Fighter _f;
        float _next, _bodyNext, _surgeT, _skyHitCd;
        MotionTrail _eye;
        Vector2 _lastTip, _lastChest;
        bool _hasTip, _hasChest;
        FightContext _subscribed;

        /// <summary>The blade static's level (0..2).</summary>
        public int Level { get; private set; }

        /// <summary>The boss phase it was last set to (0..3): how much thunder everything carries.</summary>
        public int Phase { get; private set; }

        /// <summary>Whether the eye trail is lit.</summary>
        public bool EyeTrail => Level >= 2;

        /// <summary>A phase's <see cref="PhaseDef.onEnter"/>: the storm on <paramref name="f"/> at the level of phase <paramref name="phase"/>.</summary>
        public static void Enter(Fighter f, int phase)
        {
            if (f == null) return;
            var s = f.GetComponent<RaikonStorm>();
            if (s == null) s = f.gameObject.AddComponent<RaikonStorm>();
            s._f = f;
            s.Phase = Mathf.Clamp(phase, 0, 3);
            s.SetLevel(Mathf.Clamp(phase, 0, 2));
            RaikonAura.Attach(f, s.Phase);
            RaikonSkyStorm.Attach(f, s.Phase);
        }

        /// <summary>The thunder power of <paramref name="f"/>'s storm: its phase, 0 without one.</summary>
        public static int PowerOf(Fighter f)
        {
            if (f == null) return 0;
            var s = f.GetComponent<RaikonStorm>();
            return s != null ? s.Phase : RaikonAscendant.PhaseOf(f);
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
            Subscribe();
            if (_f.Vanished) { _eye?.Tick(false, Vector2.zero, Vector2.zero); return; }     // Zenith Guillotine: gone from sight, the blade static and the eye trail with him
            float dt = Time.deltaTime;
            _skyHitCd -= dt;
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
            _surgeT -= dt;
            if (_surgeT <= 0f)
            {
                _surgeT += 1f / 60f;
                if (_surgeT < 0f) _surgeT = 0f;
                Surge(v);
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

        // ------------------------------------------------------------------------------------------------ the thunder on his moves
        /// <summary>
        /// One 60 Hz step of the thunder on his moves. Through an active blade window: arcs along the blade and the swept path behind its tip drawn as
        /// lightning (phase 1: a thin arc every other step; phase 2: an arc a step and sparks; phase 3: two, and the swept bolt). Through a limb's window,
        /// arcs down the striking limb from phase 2. Moving faster than a run in a move (the iaijutsu, a blink, a dash), a lightning streak behind him.
        /// </summary>
        void Surge(Vfx v)
        {
            int p = Phase;
            bool attacking = _f.state == FState.Attack && _f.move != null;
            bool blade = false, limb = false;
            B limbEnd = B.FootN, limbMid = B.LegNL;
            if (attacking)
                foreach (var w in _f.move.hits)
                {
                    if (_f.moveFrame < w.from - 1 || _f.moveFrame > w.to + 1 || w.points == null) continue;
                    foreach (var hp in w.points)
                    {
                        if (hp == HitPoint.Blade) blade = true;
                        else if (hp == HitPoint.Fist || hp == HitPoint.Hilt) { limb = true; limbEnd = hp == HitPoint.Fist ? B.HandN : B.HandF; limbMid = hp == HitPoint.Fist ? B.ArmNL : B.ArmFL; }
                        else if (hp is HitPoint.FootN or HitPoint.ShinN or HitPoint.KneeN) { limb = true; limbEnd = B.FootN; limbMid = B.LegNL; }
                        else if (hp is HitPoint.FootF or HitPoint.ShinF) { limb = true; limbEnd = B.FootF; limbMid = B.LegFL; }
                    }
                }

            if (blade)
            {
                Vector2 tip = _f.rig.World(B.Sword, _f.BladeTipLocal), hilt = _f.rig.World(B.Sword, _f.BladeBaseLocal);
                bool every = p >= 1 || Time.frameCount % 2 == 0;
                if (every)
                    for (int i = 0; i < 1 + (p >= 2 ? 1 : 0); i++)
                    {
                        float t0 = Random.Range(0.15f, 0.75f), t1 = Mathf.Clamp01(t0 + Random.Range(0.15f, 0.3f));
                        v.Arc(Vector2.Lerp(hilt, tip, t0), Vector2.Lerp(hilt, tip, t1), Random.Range(0.06f, 0.1f), p == 0 ? 0.009f : 0.013f + 0.002f * p);
                    }
                if (_hasTip)
                {
                    float d = (tip - _lastTip).magnitude;
                    if (d > 0.12f && (p >= 1 || Time.frameCount % 2 == 0))
                        v.Bolt(_lastTip, tip, p == 0 ? 0.012f : 0.018f + 0.006f * p, 0.12f + 0.03f * p, Vfx.Crimson, Mathf.Clamp(Mathf.RoundToInt(d / 0.1f), 3, 8), d * 0.18f, 0,
                            false, Vfx.Order + 50, p == 0 ? 0.6f : 1f, p == 0 ? 0.3f : 1f);
                }
                if (p >= 1)
                    v.Spark(Vector2.Lerp(hilt, tip, Random.Range(0.5f, 1f)), new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(0.2f, 2f)), Random.value < 0.4f ? Color.white : Vfx.Crimson,
                        0.025f, Random.Range(0.15f, 0.3f));
                _lastTip = tip; _hasTip = true;
            }
            else _hasTip = false;

            if (limb && (p >= 1 || Time.frameCount % 3 == 0))
            {
                Vector2 a = _f.rig.World(limbMid, Vector2.zero), b = _f.rig.World(limbEnd, Vector2.zero);
                v.Arc(a, b, Random.Range(0.07f, 0.11f), p == 0 ? 0.01f : 0.016f);
                if (p >= 2) v.Arc(b, b + Random.insideUnitCircle * 0.3f, 0.08f, 0.012f);
            }

            Vector2 chest = _f.Pos + Vector2.up * 1.0f;
            if ((attacking || _f.state == FState.Dash) && _hasChest)
            {
                float d = (chest - _lastChest).magnitude;
                if (d > 0.16f && d < 3f)       // faster than about 10 m/s in a move: an iaijutsu, a blink, a dash
                {
                    v.Bolt(_lastChest, chest, p == 0 ? 0.014f : 0.022f + 0.006f * p, 0.16f + 0.03f * p, Vfx.Crimson, Mathf.Clamp(Mathf.RoundToInt(d / 0.12f), 3, 10), 0.08f + 0.02f * p,
                        p >= 2 ? 1 : 0, false, Vfx.Order + 49, p == 0 ? 0.7f : 1f, p == 0 ? 0.4f : 1f);
                    if (p >= 1 && _f.Grounded)
                        v.Spark(new Vector2(_f.Pos.x, 0.04f), new Vector2(-_f.facing * Random.Range(1f, 3f), Random.Range(0.5f, 2f)), Vfx.Crimson, 0.028f, 0.3f);
                }
            }
            _lastChest = chest; _hasChest = true;
        }

        // ------------------------------------------------------------------------------------------------ thunder on his blows
        void Subscribe()
        {
            var ctx = _f.ctx;
            if (ctx == _subscribed) return;
            if (_subscribed != null) _subscribed.Events.Published -= OnEvent;
            _subscribed = ctx;
            if (ctx != null) ctx.Events.Published += OnEvent;
        }

        void OnEvent(CombatEvent e)
        {
            var v = Vfx.I;
            if (v == null || e.Source != _f || _f == null) return;
            if (e.Type != CombatEventType.Hit && e.Type != CombatEventType.Blocked) return;
            Vector2 at = e.Contact != Vector2.zero ? e.Contact : (e.Target != null ? e.Target.Pos + Vector2.up * 1.0f : _f.Pos + new Vector2(_f.facing * 0.8f, 1f));
            Vector2 dir = e.Knockback.sqrMagnitude > 1e-4f ? e.Knockback.normalized : new Vector2(_f.facing, 0.2f);
            if (e.Type == CombatEventType.Blocked)
            {
                if (Phase >= 1) v.ThunderImpact(at, -dir, Phase - 1, false);
                return;
            }
            bool heavy = e.Knockback.magnitude > 4.5f;
            v.ThunderImpact(at, dir, Phase, heavy);
            // from phase 3 a heavy melee blow calls a bolt down onto the one it hit (art only: no hit window); not on the sky bolts themselves
            if (Phase >= 2 && heavy && _skyHitCd <= 0f && e.Target != null && _f.move != null && e.MoveId == _f.move.id)
            {
                _skyHitCd = 1.4f;
                float x = e.Target.Pos.x;
                var ground = new Vector2(x, 0f);
                v.After(0.06f, () =>
                {
                    if (Vfx.I == null) return;
                    Vector2 top = ThunderCloud.StrikeOver(x, 0f, Phase, _f, 1.8f);
                    Vfx.I.ThunderStrike(ground, top, 0.8f, Phase - 1);
                });
            }
        }

        void OnDestroy()
        {
            DropEye();
            if (_subscribed != null) _subscribed.Events.Published -= OnEvent;
            _subscribed = null;
        }
    }
}
