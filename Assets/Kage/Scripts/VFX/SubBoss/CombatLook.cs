using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Which of a fighter's weapon trails a <see cref="WeaponTrail"/> draws (the five <see cref="FxKind"/> trail kinds).</summary>
    public enum TrailSlot { Blade, BladeN, Fist, FootN, FootF }

    /// <summary>
    /// A weapon trail: fed every frame with the two edges of the weapon (inner: toward the hand; outer: the tip) and whether the move's trail
    /// window is open. The generic one is <see cref="MotionTrail"/>; a sub-boss's <see cref="CombatLook"/> brings its own.
    /// </summary>
    public abstract class WeaponTrail
    {
        public abstract void Tick(bool emit, Vector2 inner, Vector2 outer);
        public virtual void Flare() { }
        public abstract void Dispose();
    }

    /// <summary>
    /// A character's signature combat look (docs/act1-subbosses-vfx.md): its own weapon trails, drawn along the real weapon path for every move it
    /// plays, and its own contact effects for a blow that lands or is guarded, in place of the generic <see cref="Vfx.HitBurst"/> and
    /// <see cref="Vfx.BlockBurst"/>. Looked up by character id when a fighter is built (<see cref="Fighter"/>); a character with none keeps the generic look.
    /// </summary>
    public abstract class CombatLook
    {
        /// <summary>The trail for <paramref name="slot"/>, or null for the generic <see cref="MotionTrail"/>.</summary>
        public virtual WeaponTrail Trail(Fighter f, TrailSlot slot, Transform parent, int order) => null;

        /// <summary>A blow of <paramref name="attacker"/>'s landed at <paramref name="p"/> travelling along <paramref name="dir"/>. False: play the generic burst.</summary>
        public virtual bool Hit(Fighter attacker, Vector2 p, Vector2 dir, HitWindow w, bool crit) => false;

        /// <summary>A blow of <paramref name="attacker"/>'s was guarded at <paramref name="p"/>. False: play the generic guard spark.</summary>
        public virtual bool Block(Fighter attacker, Vector2 p, Vector2 dir, HitWindow w) => false;

        static Dictionary<string, CombatLook> _looks;

        static void Ensure()
        {
            if (_looks != null) return;
            _looks = new Dictionary<string, CombatLook>
            {
                [Kuroe.Id] = new KuroeLook(),
                [Garan.Id] = new GaranLook(),
                [Hisame.Id] = new HisameLook(),
                [Shiden.Id] = new ShidenLook(),
            };
        }

        /// <summary>The look of character <paramref name="characterId"/>, or null (the generic look).</summary>
        public static CombatLook For(string characterId)
        {
            Ensure();
            return characterId != null && _looks.TryGetValue(characterId, out var l) ? l : null;
        }

        /// <summary>True once the fighter's boss fight is in its second phase (the Raikon-trained escalation of every look).</summary>
        public static bool PhaseTwo(Fighter f) => f != null && f.Phases != null && f.Phases.Index >= 1;
    }

    /// <summary>
    /// A weapon trail built from the weapon's sampled path: every frame the window is open, both edges are recorded; strokes (one open window
    /// each) are drawn by <see cref="DrawStroke"/> from Catmull-Rom subdivided sections, oldest first, each with its age (0 new .. 1 gone) and
    /// its place along the stroke. Runs on scaled time, so a stroke freezes with the fighters in hit-stop.
    /// </summary>
    public abstract class SampledTrail : WeaponTrail
    {
        protected struct Sample { public Vector2 a, b; public float t; public int stroke; }
        /// <summary>One drawn cross-section: inner edge, outer edge, age 0..1, place along the stroke 0 (oldest) .. 1 (newest), and its time (a stable noise key).</summary>
        protected struct Section { public Vector2 a, b; public float age, along, t; }

        protected readonly List<Sample> samples = new();
        readonly List<Section> _secs = new();
        protected readonly Ribbon[] ribs;
        protected readonly float life;
        protected readonly Fighter owner;
        readonly GameObject _go;
        int _stroke;
        bool _was;
        const int Subdiv = 4;

        protected SampledTrail(string name, Transform parent, float life, int order, Fighter owner, params Material[] mats)
        {
            _go = new GameObject(name);
            _go.transform.SetParent(parent, false);
            this.life = life;
            this.owner = owner;
            ribs = new Ribbon[mats.Length];
            var meshes = Application.isPlaying ? _go.AddComponent<MeshOwner>() : null;
            for (int i = 0; i < mats.Length; i++)
            {
                ribs[i] = new Ribbon(name + "_" + i, _go.transform, mats[i], order + i);
                if (meshes != null) meshes.meshes.Add(ribs[i].mesh);
            }
        }

        /// <summary>A clock for tests (seconds); null = scaled game time (<see cref="Time.time"/>), so a stroke freezes in hit-stop.</summary>
        public static System.Func<float> TestClock;
        float _last = -1f;
        float _flareUntil;
        MaterialPropertyBlock _flareBlock;

        public override void Flare()
        {
            _flareUntil = Time.unscaledTime + 0.065f;
        }

        void UpdateFlare()
        {
            if (_flareUntil <= 0f) return;
            float glow = Mathf.Clamp01((_flareUntil - Time.unscaledTime) / 0.065f);
            _flareBlock ??= new MaterialPropertyBlock();
            foreach (var ribbon in ribs)
            {
                var r = ribbon.renderer;
                var mat = r.sharedMaterial;
                if (mat.GetFloat("_DstBlend") != (float)UnityEngine.Rendering.BlendMode.One) continue;
                Vector4 color = mat.GetVector("_Color");
                float gain = 1f + glow * 1.8f;
                _flareBlock.SetVector("_Color", new Vector4(color.x * gain, color.y * gain, color.z * gain, color.w));
                r.SetPropertyBlock(glow > 0f ? _flareBlock : null);
            }
            if (glow == 0f) _flareUntil = 0f;
        }

        public override void Tick(bool emit, Vector2 a, Vector2 b)
        {
            float now = TestClock != null ? TestClock() : Time.time;
            float dt = TestClock != null ? (_last < 0f ? 1f / 60f : now - _last) : Time.deltaTime;
            _last = now;
            UpdateFlare();
            if (emit && !_was) { _stroke++; OnStrokeStart(a, b); }
            if (!emit && _was) OnStrokeEnd();
            _was = emit;
            if (emit && dt > 0f)
            {
                bool same = samples.Count > 0 && samples[^1].stroke == _stroke;
                if (!same || (samples[^1].b - b).sqrMagnitude > 1e-6f || (samples[^1].a - a).sqrMagnitude > 1e-6f)
                {
                    if (same) OnMove(samples[^1].a, samples[^1].b, a, b, dt);
                    samples.Add(new Sample { a = a, b = b, t = now, stroke = _stroke });
                }
            }
            while (samples.Count > 0 && now - samples[0].t > life) samples.RemoveAt(0);

            foreach (var r in ribs) r.Begin();
            int i = 0;
            while (i < samples.Count)
            {
                int j = i;
                while (j + 1 < samples.Count && samples[j + 1].stroke == samples[i].stroke) j++;
                if (j > i)
                {
                    Build(i, j, now);
                    DrawStroke(_secs, emit && samples[j].stroke == _stroke);
                }
                i = j + 1;
            }
            foreach (var r in ribs) r.End();
        }

        void Build(int i0, int i1, float now)
        {
            _secs.Clear();
            int n = i1 - i0;
            float total = n * Subdiv;
            for (int k = i0; k < i1; k++)
            {
                var p0 = samples[Mathf.Max(i0, k - 1)];
                var p1 = samples[k];
                var p2 = samples[k + 1];
                var p3 = samples[Mathf.Min(i1, k + 2)];
                for (int s = 0; s < Subdiv; s++)
                {
                    float f = s / (float)Subdiv;
                    float t = Mathf.Lerp(p1.t, p2.t, f);
                    _secs.Add(new Section
                    {
                        a = CatmullRom(p0.a, p1.a, p2.a, p3.a, f),
                        b = CatmullRom(p0.b, p1.b, p2.b, p3.b, f),
                        age = Mathf.Clamp01((now - t) / life),
                        along = ((k - i0) * Subdiv + s) / total,
                        t = t,
                    });
                }
            }
            var last = samples[i1];
            _secs.Add(new Section { a = last.a, b = last.b, age = Mathf.Clamp01((now - last.t) / life), along = 1f, t = last.t });
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        /// <summary>Draws one stroke into <see cref="ribs"/>; <paramref name="live"/> while its window is still open (the weapon is still cutting).</summary>
        protected abstract void DrawStroke(List<Section> secs, bool live);

        /// <summary>The weapon moved from (pa, pb) to (a, b) over <paramref name="dt"/> seconds within a stroke: shed secondary matter here.</summary>
        protected virtual void OnMove(Vector2 pa, Vector2 pb, Vector2 a, Vector2 b, float dt) { }

        protected virtual void OnStrokeStart(Vector2 a, Vector2 b) { }
        protected virtual void OnStrokeEnd() { }

        public override void Dispose()
        {
            if (_go == null) return;
            if (Application.isPlaying) Object.Destroy(_go); else Object.DestroyImmediate(_go);
        }

        /// <summary>The look's second-phase escalation is on (the owner's boss phase).</summary>
        protected bool PhaseTwo => CombatLook.PhaseTwo(owner);
    }
}
