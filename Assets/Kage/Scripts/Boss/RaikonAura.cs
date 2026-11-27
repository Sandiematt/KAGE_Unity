using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Raikon's standing red aura: on all fight long, round the whole body (docs/raikon-ascendant-vfx.md, "Thunder pass"). Four layers, each with a job:
    /// <list type="number">
    /// <item><b>The silhouette glow</b>: his own live part meshes (the soft ones shared, so they keep deforming) drawn again behind him in flat additive
    /// red, scaled out from his centre: a tight bright rim, a wider glow that drifts up and sways like heat, and from phase 2 a faint halo. It hugs
    /// whatever pose he strikes.</item>
    /// <item><b>Flame tongues</b> rising off his outline, behind him so only the part outside the body shows: a blood-black rim, a red body, from
    /// phase 3 a white-hot root. Each grows in fast and erodes from its root up (the craft's shape change), leaning away from his motion.</item>
    /// <item><b>Embers</b> rising off him, and a red glow on the floor under his feet.</item>
    /// <item><b>Crawling arcs</b> between his limbs from phase 2: dark crimson with no white core, so they never look like the unblockable tell.</item>
    /// </list>
    /// Phase 1 smoulders, phase 2 burns, phase 3 blazes, the Final Overload blazes hotter; a phase shift surges it. <see cref="Flare"/> (the unblockable
    /// tell, <see cref="RaikonAscendant.AuraCue"/>) turns it white-hot for a moment, so the aura itself says "do not block". Nothing is built headless
    /// (no <see cref="Vfx"/>); it fades out on a KO.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public sealed class RaikonAura : MonoBehaviour
    {
        /// <summary>One level's numbers (0 = phase 1 .. 3 = Final Overload).</summary>
        struct Look
        {
            public float rimA, glowA, haloA, rimS, glowS, haloS, tongues, tongueH, embers, arcEvery, floorA;
        }

        static readonly Look[] Looks =
        {
            new() { rimA = 0.34f, glowA = 0.15f, haloA = 0f,    rimS = 1.035f, glowS = 1.085f, haloS = 1.16f, tongues = 9f,  tongueH = 0.3f,  embers = 3f,  arcEvery = 0f,    floorA = 0.16f },
            new() { rimA = 0.44f, glowA = 0.21f, haloA = 0.07f, rimS = 1.04f,  glowS = 1.1f,   haloS = 1.19f, tongues = 16f, tongueH = 0.42f, embers = 6f,  arcEvery = 0.5f,  floorA = 0.22f },
            new() { rimA = 0.54f, glowA = 0.27f, haloA = 0.11f, rimS = 1.045f, glowS = 1.115f, haloS = 1.22f, tongues = 26f, tongueH = 0.56f, embers = 10f, arcEvery = 0.22f, floorA = 0.3f },
            new() { rimA = 0.62f, glowA = 0.33f, haloA = 0.15f, rimS = 1.05f,  glowS = 1.13f,  haloS = 1.25f, tongues = 34f, tongueH = 0.66f, embers = 14f, arcEvery = 0.14f, floorA = 0.36f },
        };

        /// <summary>Sorting offsets from the rig's base order: all behind his parts (base + 2 and up).</summary>
        const int HaloOrder = -26, GlowOrder = -24, TongueRimOrder = -22, TongueOrder = -20, TongueCoreOrder = -19, RimOrder = -14;
        /// <summary>The floor glow's order: on the backdrop's floor (-1200), under the fighters and every ground mark.</summary>
        public const int FloorOrder = -1100;

        static readonly B[] Anchors =
        {
            B.Head, B.Head, B.Neck, B.Chest, B.Chest, B.Hips, B.ArmNU, B.ArmFU, B.ArmNL, B.ArmFL, B.HandN, B.HandF,
            B.LegNU, B.LegFU, B.LegNL, B.LegFL, B.FootN, B.FootF, B.CoatN, B.CoatF,
        };

        Fighter _f;
        int _level;
        float _flare, _surge, _t, _alive = 1f, _tongueAcc, _emberAcc, _arcT, _sync;
        Vector2 _lastPos;
        bool _hasLast;

        GameObject _root;
        readonly List<MeshRenderer> _src = new(), _seen = new();
        float _groundY;
        readonly List<MeshFilter> _srcMf = new();
        readonly List<MeshRenderer>[] _dst = { new(), new(), new() };
        readonly List<MeshFilter>[] _dstMf = { new(), new(), new() };
        readonly Material[] _mat = new Material[3];
        Ribbon _tRim, _tBody, _tCore, _floor;
        int _builtOrder = int.MinValue;
        readonly List<Vector2> _pts = new(8);
        readonly float[] _alpha = new float[3], _scale = new float[3];
        readonly Vector2[] _offset = new Vector2[3];
        /// <summary>The HDR multiplier of the rim, glow and halo (the rim blooms the most).</summary>
        static readonly float[] LayerHdr = { 1.8f, 1.4f, 1.2f };
        static readonly int[] LayerOrder = { RimOrder, GlowOrder, HaloOrder };

        struct Tongue { public B bone; public Vector2 off; public float age, life, h, w, ph; public bool live; }
        readonly Tongue[] _tg = new Tongue[72];

        /// <summary>The level (0..3) the aura burns at.</summary>
        public int Level => _level;

        /// <summary>The aura on <paramref name="f"/> at the level of boss phase <paramref name="phase"/>; a level up surges it.</summary>
        public static RaikonAura Attach(Fighter f, int phase)
        {
            if (f == null) return null;
            var a = f.GetComponent<RaikonAura>();
            if (a == null) a = f.gameObject.AddComponent<RaikonAura>();
            a._f = f;
            int lv = Mathf.Clamp(phase, 0, 3);
            if (lv > a._level) a._surge = 1f;
            a._level = lv;
            return a;
        }

        /// <summary>The unblockable tell's flare: the aura turns white-hot for a moment.</summary>
        public static void Flare(Fighter f, float amount = 1f)
        {
            if (f == null) return;
            var a = f.GetComponent<RaikonAura>();
            if (a != null) a._flare = Mathf.Max(a._flare, amount);
        }

        // ------------------------------------------------------------------------------------------------ the silhouette copies
        void Build()
        {
            var v = Vfx.I;
            if (_root == null)
            {
                for (int l = 0; l < 3; l++) if (_mat[l] != null) Destroy(_mat[l]);           // a new Vfx: the old root went with the old one
                foreach (var r in new[] { _tRim, _tBody, _tCore, _floor }) if (r != null && r.mesh != null) Destroy(r.mesh);
                _root = new GameObject("RaikonAura");
                _root.transform.SetParent(v.transform, false);
                _tRim = new Ribbon("AuraTongueRim", _root.transform, MeshKit.Alpha, 0);
                _tBody = new Ribbon("AuraTongues", _root.transform, v.AddHDR(1.7f), 0);
                _tCore = new Ribbon("AuraTongueCore", _root.transform, v.AddHDR(2.4f), 0);
                _floor = new Ribbon("AuraFloor", _root.transform, v.AddHDR(1.4f), FloorOrder);
                Color[] cols = { Vfx.AuraRed, new(0.85f, 0.04f, 0.08f, 1f), new(0.6f, 0.02f, 0.06f, 1f) };
                string[] names = { "AuraRim", "AuraGlow", "AuraHalo" };
                for (int l = 0; l < 3; l++)
                {
                    _mat[l] = MeshKit.MakeMat(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One, names[l], null);
                    _mat[l].SetFloat("_Flash", 1f);
                    _mat[l].SetColor("_FlashColor", cols[l]);
                }
            }
            for (int l = 0; l < 3; l++)
            {
                foreach (var r in _dst[l]) if (r != null) Destroy(r.gameObject);
                _dst[l].Clear(); _dstMf[l].Clear();
            }
            _src.Clear(); _srcMf.Clear(); _seen.Clear();
            foreach (var r in _f.rig.Renderers)
            {
                _seen.Add(r);
                if (r == null) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                _src.Add(r); _srcMf.Add(mf);
                for (int l = 0; l < 3; l++)
                {
                    var go = new GameObject(r.name);
                    go.transform.SetParent(_root.transform, false);
                    var dmf = go.AddComponent<MeshFilter>();
                    dmf.sharedMesh = mf.sharedMesh;
                    var dr = go.AddComponent<MeshRenderer>();
                    dr.sharedMaterial = _mat[l];
                    dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    dr.receiveShadows = false;
                    _dst[l].Add(dr); _dstMf[l].Add(dmf);
                }
            }
            _builtOrder = int.MinValue;
        }

        /// <summary>Whether the rig's parts changed since the copies were built (a weapon's parts swapped, a part dropped): compared with every renderer seen then.</summary>
        bool SourcesChanged()
        {
            int n = 0;
            foreach (var r in _f.rig.Renderers)
            {
                if (n >= _seen.Count || !ReferenceEquals(r, _seen[n])) return true;
                n++;
            }
            if (n != _seen.Count) return true;
            for (int j = 0; j < _src.Count; j++) if (_src[j] == null) return true;        // destroyed under us
            return false;
        }

        void SetOrders(int baseOrder)
        {
            if (baseOrder == _builtOrder) return;
            _builtOrder = baseOrder;
            for (int l = 0; l < 3; l++) foreach (var r in _dst[l]) if (r != null) r.sortingOrder = baseOrder + LayerOrder[l];
            _tRim.renderer.sortingOrder = baseOrder + TongueRimOrder;
            _tBody.renderer.sortingOrder = baseOrder + TongueOrder;
            _tCore.renderer.sortingOrder = baseOrder + TongueCoreOrder;
        }

        void Hide()
        {
            if (_root != null && _root.activeSelf) _root.SetActive(false);
        }

        // ------------------------------------------------------------------------------------------------ every frame
        void LateUpdate()
        {
            var v = Vfx.I;
            if (_f == null || _f.rig == null || v == null) { Hide(); return; }
            float dt = Time.deltaTime;
            _t += dt;
            bool ko = _f.state == FState.KO || _f.health <= 0f;
            _alive = ko ? Mathf.Max(0f, _alive - dt / 0.8f) : Mathf.Min(1f, _alive + dt / 0.4f);
            if (_alive <= 0f || !_f.rig.gameObject.activeInHierarchy || _f.Vanished) { Hide(); return; }      // gone from sight (Zenith Guillotine): the aura with him

            if (_root == null || (_sync -= dt) <= 0f)
            {
                _sync = 0.35f;
                if (_root == null || SourcesChanged()) Build();
            }
            if (!_root.activeSelf) _root.SetActive(true);
            SetOrders(_f.rig.baseOrder);

            _flare = Mathf.Max(0f, _flare - dt / 0.22f);
            _surge = Mathf.Max(0f, _surge - dt / 1.3f);
            var look = Looks[_level];
            float boost = (1f + 1.2f * _surge + 1.4f * _flare) * _alive;
            float flick = 1f + 0.12f * (Mathf.PerlinNoise(_t * 6f, 0.3f) - 0.5f) * 2f;

            Vector2 c = (_f.rig.World(B.Hips, Vector2.zero) + _f.rig.World(B.Chest, Vector2.zero)) * 0.5f;
            Vector2 pos = _f.Pos;
            if (_f.Grounded || !_hasLast) _groundY = pos.y;
            Vector2 vel = _hasLast && dt > 0f ? (pos - _lastPos) / dt : Vector2.zero;
            _lastPos = pos; _hasLast = true;
            float lean = Mathf.Clamp(-_f.facing * (0.06f + 0.05f * _level) - vel.x * 0.035f, -0.45f, 0.45f);

            // the silhouette: rim, glow, halo
            float breathe = 1f + 0.012f * Mathf.Sin(_t * 8.5f);
            float sway = (Mathf.PerlinNoise(_t * 1.7f, 4.2f) - 0.5f) * 2f;
            var alpha = _alpha; var scale = _scale; var offset = _offset;
            alpha[0] = look.rimA * boost * flick; alpha[1] = look.glowA * boost; alpha[2] = look.haloA * boost;
            scale[0] = look.rimS + 0.02f * _flare; scale[1] = (look.glowS + 0.04f * _surge) * breathe; scale[2] = (look.haloS + 0.06f * _surge) * breathe;
            offset[0] = Vector2.zero; offset[1] = new Vector2(sway * 0.02f + lean * 0.04f, 0.025f); offset[2] = new Vector2(sway * 0.035f + lean * 0.08f, 0.06f);
            Color hot = Color.Lerp(Vfx.AuraRed, Vfx.HotWhite, 0.55f * _flare);
            _mat[0].SetColor("_FlashColor", hot);
            for (int l = 0; l < 3; l++)
            {
                float a = Mathf.Clamp01(alpha[l]), k = LayerHdr[l];
                _mat[l].SetVector("_Color", new Vector4(k, k, k, a));
                bool on = a > 0.004f;
                var dst = _dst[l]; var dmf = _dstMf[l];
                for (int j = 0; j < _src.Count && j < dst.Count; j++)
                {
                    var s = _src[j]; var d = dst[j];
                    if (s == null || d == null) continue;
                    bool show = on && s.enabled && s.gameObject.activeInHierarchy;
                    if (d.enabled != show) d.enabled = show;
                    if (!show) continue;
                    var sm = _srcMf[j].sharedMesh;
                    if (dmf[j].sharedMesh != sm) dmf[j].sharedMesh = sm;
                    var st = s.transform;
                    Vector2 p = st.position;
                    d.transform.SetPositionAndRotation(new Vector3(c.x + (p.x - c.x) * scale[l] + offset[l].x, c.y + (p.y - c.y) * scale[l] + offset[l].y, st.position.z), st.rotation);
                    d.transform.localScale = st.lossyScale * scale[l];
                }
            }

            // flame tongues, embers, arcs, the floor glow
            if (!ko && dt > 0f)
            {
                _tongueAcc += look.tongues * (1f + 2f * _surge + 2.5f * _flare) * dt;
                while (_tongueAcc >= 1f) { _tongueAcc -= 1f; SpawnTongue(c, look); }
                _emberAcc += look.embers * (1f + 1.5f * _surge) * dt;
                while (_emberAcc >= 1f)
                {
                    _emberAcc -= 1f;
                    Vector2 at = Outline(c, out _) + Random.insideUnitCircle * 0.06f;
                    v.Embers(at, new Vector2(lean * 2.2f + Random.Range(-0.25f, 0.25f), Random.Range(0.6f, 1.4f)), Random.value < 0.2f ? Vfx.HotWhite : Vfx.AuraRed,
                        Random.Range(0.03f, 0.05f), Random.Range(0.5f, 0.9f));
                }
                if (look.arcEvery > 0f && (_arcT -= dt) <= 0f)
                {
                    _arcT = look.arcEvery * Random.Range(0.6f, 1.4f);
                    Vector2 a = _f.rig.World(Anchors[Random.Range(0, Anchors.Length)], Vector2.zero), b = _f.rig.World(Anchors[Random.Range(0, Anchors.Length)], Vector2.zero);
                    float dd = (b - a).magnitude;
                    if (dd > 0.2f && dd < 0.9f)
                        v.Bolt(a, b, 0.014f, Random.Range(0.08f, 0.13f), new Color(0.75f, 0.03f, 0.08f, 1f), 6, dd * 0.22f, 0, false, Vfx.Order + 46, 0.85f, 0f);
                }
            }
            DrawTongues(dt, lean);
            _floor.Begin();
            float lift = Mathf.Clamp01(1f - (pos.y - _groundY) / 2.5f);           // dimmer the higher he leaps
            Vfx.SoftBlob(_floor, new Vector2(pos.x, _groundY + 0.01f), 0.95f + 0.1f * _level, 0.13f,
                Vfx.AuraRed.WithA(look.floorA * (1f + 0.15f * Mathf.Sin(_t * 5f)) * boost * lift), 0.1f, 20);
            _floor.End();
        }

        /// <summary>A point on his outline: a random anchor bone pushed out from his centre.</summary>
        Vector2 Outline(Vector2 c, out B bone)
        {
            bone = Anchors[Random.Range(0, Anchors.Length)];
            Vector2 p = _f.rig.World(bone, Vector2.zero);
            Vector2 d = p - c;
            d = d.sqrMagnitude > 1e-4f ? d.normalized : Vector2.up;
            return p + d * Random.Range(0.06f, 0.16f);
        }

        void SpawnTongue(Vector2 c, Look look)
        {
            for (int i = 0; i < _tg.Length; i++)
            {
                if (_tg[i].live) continue;
                Vector2 at = Outline(c, out var bone);
                _tg[i] = new Tongue
                {
                    bone = bone, off = at - _f.rig.World(bone, Vector2.zero), age = 0f, life = Random.Range(0.32f, 0.55f),
                    h = look.tongueH * Random.Range(0.6f, 1.25f) * (1f + 0.5f * _surge), w = Random.Range(0.04f, 0.075f) * (1f + 0.15f * _level),
                    ph = Random.Range(0f, 6.28f), live = true,
                };
                return;
            }
        }

        void DrawTongues(float dt, float lean)
        {
            _tRim.Begin(); _tBody.Begin(); _tCore.Begin();
            float hotMul = 1f + 1.2f * _flare;
            for (int i = 0; i < _tg.Length; i++)
            {
                ref var tg = ref _tg[i];
                if (!tg.live) continue;
                tg.age += dt;
                float u = tg.age / tg.life;
                if (u >= 1f) { tg.live = false; continue; }
                float head = KMath.Apply(Ease.OutExpo, Mathf.Clamp01(u / 0.3f));
                float tail = KMath.Apply(Ease.InQuad, Mathf.Clamp01((u - 0.35f) / 0.65f));
                if (head - tail < 0.03f) continue;
                Vector2 root = _f.rig.World(tg.bone, Vector2.zero) + tg.off;
                _pts.Clear();
                const int seg = 6;
                for (int s = 0; s <= seg; s++)
                {
                    float q = Mathf.Lerp(tail, head, s / (float)seg);
                    float x = Mathf.Sin(tg.ph + q * 2.6f + _t * 7f) * 0.05f * q + lean * q * q * tg.h;
                    _pts.Add(root + new Vector2(x, q * tg.h));
                }
                float w0 = tg.w * (1f - tail) * Mathf.Min(1f, u / 0.12f), w1 = tg.w * 0.1f;
                float a = Mathf.Pow(1f - u, 0.6f) * _alive;
                Vfx.SoftLine(_tRim, _pts, w0 * 1.9f, w1 * 2f, Vfx.BloodBlack.WithA(0.4f * a));
                Vfx.SoftLine(_tBody, _pts, w0, w1, Color.Lerp(Vfx.AuraRed, Vfx.HotWhite, 0.5f * _flare).WithA(Mathf.Clamp01(0.75f * a * hotMul)));
                if (_level >= 2 || _flare > 0.1f) Vfx.SoftLine(_tCore, _pts, w0 * 0.35f, 0f, Vfx.HotWhite.WithA(Mathf.Clamp01(0.55f * a * (1f - u) * hotMul)));
            }
            _tRim.End(); _tBody.End(); _tCore.End();
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root);
            for (int l = 0; l < 3; l++) if (_mat[l] != null) Destroy(_mat[l]);
            foreach (var r in new[] { _tRim, _tBody, _tCore, _floor }) if (r != null && r.mesh != null) Destroy(r.mesh);
        }
    }
}
