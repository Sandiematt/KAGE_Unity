using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Runtime skeleton + meshes of a fighter. Builds bone transforms and polygon parts from a
    /// <see cref="RigDef"/>, applies poses, keeps the back-light rim offset in world space, and
    /// drives secondary motion: hair as a spring/wind rotation field (<see cref="HairField"/>),
    /// the coat as a small cloth simulation (<see cref="CoatCloth"/>), the scarf on a verlet
    /// ribbon (<see cref="ScarfRibbon"/>), plus a tweened turn-around and squash and stretch.
    /// </summary>
    public class FighterRig : MonoBehaviour
    {
        [System.NonSerialized] public RigDef def;
        public Transform[] bones;
        public float facing = 1f;
        public int baseOrder;
        public bool swordFront;

        class PartRT
        {
            public PartDef def;
            public MeshRenderer main, rim;
            public Transform jiggle;
            public float jAngle, jVel;
        }

        readonly List<PartRT> _parts = new();
        readonly List<MeshRenderer> _all = new();
        MaterialPropertyBlock _mpb;
        Vector3[] _bindLocal;
        public ScarfRibbon scarf;
        public readonly Pose current = new();

        // spring state
        float _headAngPrev, _headAngVel;
        Vector2 _prevHips;
        readonly List<HairField> _hair = new();
        readonly CoatCloth[] _cloths = new CoatCloth[2];
        public bool autoSecondary;          // gallery / preview rigs animate themselves
        public float groundY;
        float _vis = 1f;                    // visual facing, tweened through 0 for a paper-flip turn
        float _sq, _sqVel;                  // squash (+) / stretch (-) spring
        Vector3 _baseScale = Vector3.one;
        float _flash, _flashDecay = 18f;
        Color _flashColor = Color.white;
        float _alpha = 1f;
        public Vector2 lightDir = new(-0.0055f, 0.0055f);   // back-top rim light (root space)

        public Transform Bone(B b) => bones[(int)b];

        public void Build(RigDef d, int order)
        {
            def = d;
            baseOrder = order;
            _mpb = new MaterialPropertyBlock();
            bones = new Transform[(int)B.COUNT];
            _bindLocal = new Vector3[(int)B.COUNT];
            for (int i = 0; i < (int)B.COUNT; i++)
            {
                var go = new GameObject(((B)i).ToString());
                bones[i] = go.transform;
            }
            for (int i = 0; i < (int)B.COUNT; i++)
            {
                var b = (B)i;
                bones[i].SetParent(b == B.Root ? transform : bones[(int)d.parent[i]], false);
                _bindLocal[i] = d.BindLocal(b);
                bones[i].localPosition = _bindLocal[i];
            }

            _baseScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, 1f);
            foreach (var p in d.parts)
            {
                var rt = new PartRT { def = p };
                Transform parent = bones[(int)p.bone];
                Vector2 pivot = d.pivot[(int)p.bone];
                if (p.polys != null)
                {
                    var multi = MeshKit.PolyMulti(p.polys, pivot, p.name);
                    rt.main = MeshKit.MakeRenderer(p.name, parent, multi, MeshKit.Alpha, 0);
                    _all.Add(rt.main);
                    _parts.Add(rt);
                    if (p.soft == SoftKind.Hair) _hair.Add(new HairField(multi, d.hairCentre - pivot));
                    else if (p.soft == SoftKind.Coat) Cloth(d, p.clothLayer).Bind(multi);
                    continue;
                }
                if (p.jigglePivot.HasValue)
                {
                    var j = new GameObject(p.name + "_Pivot").transform;
                    j.SetParent(parent, false);
                    j.localPosition = p.jigglePivot.Value - pivot;
                    pivot = p.jigglePivot.Value;
                    parent = j;
                    rt.jiggle = j;
                }
                var local = new Vector2[p.pts.Length];
                for (int k = 0; k < local.Length; k++) local[k] = p.pts[k] - pivot;

                Color c = p.color;
                var mesh = MeshKit.Poly(local, c, p.name);
                rt.main = MeshKit.MakeRenderer(p.name, parent, mesh, MeshKit.Alpha, 0);
                if (p.rim)
                {
                    var rimMesh = MeshKit.Poly(MeshKit.Grow(local, p.rimGrow), p.rimColor ?? d.rimColor, p.name + "_Rim");
                    rt.rim = MeshKit.MakeRenderer(p.name + "_Rim", parent, rimMesh, MeshKit.Alpha, 0);
                    _all.Add(rt.rim);
                }
                _all.Add(rt.main);
                _parts.Add(rt);
                if (p.glow > 1.01f)
                {
                    _mpb.Clear();
                    _mpb.SetVector("_Color", new Vector4(p.glow, p.glow, p.glow, 1f));
                    rt.main.SetPropertyBlock(_mpb);
                }
            }
            ApplyOrders();

            if (d.scarf)
            {
                var sgo = new GameObject("Scarf");
                sgo.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
                scarf = sgo.AddComponent<ScarfRibbon>();
                scarf.Init(this, d);
            }
            _prevHips = Bone(B.Hips).position;
        }

        public void SetBaseOrder(int order)
        {
            if (order == baseOrder) return;
            baseOrder = order;
            ApplyOrders();
        }

        public void SetSwordFront(bool front)
        {
            if (front == swordFront) return;
            swordFront = front;
            ApplyOrders();
        }

        void ApplyOrders()
        {
            foreach (var p in _parts)
            {
                int o = p.def.order + (p.def.swordLayer && swordFront ? 30 * 4 : 0);
                p.main.sortingOrder = baseOrder + o * 3 + 2;
                if (p.rim != null) p.rim.sortingOrder = baseOrder + (o - o % 4) * 3;
            }
            if (scarf != null) scarf.SetOrder(baseOrder + 3);
        }

        /// <summary>Turns the fighter. The logical facing flips at once; the visual scale tweens
        /// through zero over ~7 frames (a paper-flip turn) unless instant is set.</summary>
        public void SetFacing(float f, bool instant = false)
        {
            facing = Mathf.Sign(f);
            if (instant) SnapFacing();
        }

        public void SnapFacing()
        {
            _vis = facing;
            ApplyScale();
        }

        /// <summary>Positive = squash (landing, impact), negative = stretch (take-off).</summary>
        public void Squash(float amount) { _sq = amount; _sqVel = 0f; }

        void ApplyScale()
        {
            float v = Mathf.Abs(_vis) < 0.08f ? 0.08f * (facing >= 0 ? 1f : -1f) : _vis;
            transform.localScale = new Vector3(_baseScale.x * v * (1f + _sq * 0.55f), _baseScale.y * (1f - _sq), 1f);
        }

        /// <summary>Coat cloth patch for a layer, created on first use with leg colliders.</summary>
        CoatCloth Cloth(RigDef d, int layer)
        {
            layer = d.coatTail ? Mathf.Clamp(layer, 0, 1) : 0;
            if (_cloths[layer] != null) return _cloths[layer];
            Vector2 hp = d.pivot[(int)B.Hips];
            var c = layer == 0
                ? new CoatCloth(Bone(B.Hips), d.coatF0 - hp, d.coatF1 - hp, d.coatB0 - hp, d.coatB1 - hp)
                : new CoatCloth(Bone(B.Hips), d.tailF0 - hp, d.tailF1 - hp, d.tailB0 - hp, d.tailB1 - hp, 1.45f);
            c.AddCollider(Bone(B.LegNU), d.BindLocal(B.LegNL) * 0.5f, 0.115f);
            c.AddCollider(Bone(B.LegNL), Vector2.zero, 0.105f);
            c.AddCollider(Bone(B.LegNL), d.BindLocal(B.FootN) * 0.6f, 0.09f);
            c.AddCollider(Bone(B.LegFU), d.BindLocal(B.LegFL) * 0.5f, 0.115f);
            c.AddCollider(Bone(B.LegFL), Vector2.zero, 0.105f);
            c.AddCollider(Bone(B.LegFL), d.BindLocal(B.FootF) * 0.6f, 0.09f);
            return _cloths[layer] = c;
        }

        public void ResetSoft()
        {
            foreach (var c in _cloths) c?.Reset();
            foreach (var h in _hair) h.Reset();
            _prevHips = Bone(B.Hips).position;
        }

        /// <summary>Applies a (resolved) pose to the bone transforms.</summary>
        public void Apply(Pose p)
        {
            p.Resolve(def);
            current.CopyFrom(p);
            for (int i = 1; i < (int)B.COUNT; i++)
            {
                var b = (B)i;
                if (b == B.CoatN || b == B.CoatF) continue;
                bones[i].localRotation = Quaternion.Euler(0f, 0f, p.a[i]);
            }
            bones[(int)B.Hips].localPosition = _bindLocal[(int)B.Hips] + (Vector3)p.hip;
        }

        void LateUpdate()
        {
            if (autoSecondary) Secondary(Time.deltaTime, Vector2.zero);
        }

        /// <summary>Called after Apply each frame: turn tween, squash, hair, coat, rim offsets, flash.</summary>
        public void Secondary(float dt, Vector2 bodyVelocity)
        {
            if (dt <= 0f) return;
            // --- turn-around tween + squash and stretch ---
            if (!Mathf.Approximately(_vis, facing) || Mathf.Abs(_sq) > 0.0005f || Mathf.Abs(_sqVel) > 0.005f)
            {
                _vis = Mathf.MoveTowards(_vis, facing, dt * 18f);
                Spring(ref _sq, ref _sqVel, 0f, 320f, 15f, dt);
                ApplyScale();
            }

            Vector2 hipsNow = Bone(B.Hips).position;
            Vector2 hv = Vector2.ClampMagnitude((hipsNow - _prevHips) / dt, 18f);
            _prevHips = hipsNow;
            Vector2 vLocal = new Vector2(hv.x * facing, hv.y);

            // --- hair: lag behind head motion, sway in the wind ---
            float headAng = current.a[(int)B.Hips] + current.a[(int)B.Chest] + current.a[(int)B.Neck] + current.a[(int)B.Head];
            float dA = Mathf.DeltaAngle(_headAngPrev, headAng);
            _headAngPrev = headAng;
            _headAngVel = Mathf.Lerp(_headAngVel, Mathf.Clamp(dA / dt, -900f, 900f), 0.5f);
            foreach (var h in _hair) h.Tick(dt, vLocal, _headAngVel);
            float drive = -_headAngVel * 0.04f - vLocal.x * 1.2f + hv.y * -1.4f;
            foreach (var p in _parts)
            {
                if (p.jiggle == null) continue;
                float tgt = Mathf.Clamp(drive * p.def.jiggle, -22f, 22f);
                Spring(ref p.jAngle, ref p.jVel, tgt, 260f, 9f, dt);
                p.jiggle.localRotation = Quaternion.Euler(0, 0, p.jAngle);
            }

            // --- coat cloth ---
            foreach (var c in _cloths)
            {
                if (c == null) continue;
                c.groundY = groundY;
                c.Tick(dt, facing, hv);
            }

            // --- rim light: keep the outline offset fixed towards the back-top in world space ---
            Vector3 lw = new Vector3(lightDir.x * facing, lightDir.y, 0f);
            foreach (var p in _parts)
            {
                if (p.rim == null) continue;
                var t = p.rim.transform;
                t.localPosition = t.parent.InverseTransformVector(lw);
            }

            // --- hit flash ---
            if (_flash > 0f || _alphaDirty)
            {
                _flash = Mathf.Max(0f, _flash - dt * _flashDecay);
                ApplyTint();
            }
        }

        static void Spring(ref float x, ref float v, float target, float k, float damp, float dt)
        {
            float steps = Mathf.Ceil(dt / 0.008f);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                v += ((target - x) * k - v * damp) * h;
                x += v * h;
            }
        }

        bool _alphaDirty;

        public void Flash(Color c, float amount, float decay = 18f)
        {
            _flashColor = c;
            _flash = Mathf.Max(_flash, amount);
            _flashDecay = decay;
            ApplyTint();
        }

        public void SetAlpha(float a)
        {
            _alpha = a;
            _alphaDirty = true;
            ApplyTint();
            if (scarf != null) scarf.alpha = a;
        }

        void ApplyTint()
        {
            _alphaDirty = false;
            foreach (var p in _parts)
            {
                float g = p.def.glow;
                _mpb.Clear();
                _mpb.SetVector("_Color", new Vector4(g, g, g, _alpha));
                _mpb.SetColor("_FlashColor", _flashColor);
                _mpb.SetFloat("_Flash", p.def.role == PartRole.Eye ? 0f : _flash);
                p.main.SetPropertyBlock(_mpb);
                if (p.rim != null)
                {
                    _mpb.SetVector("_Color", new Vector4(1, 1, 1, _alpha));
                    p.rim.SetPropertyBlock(_mpb);
                }
            }
        }

        public void SetVisible(bool v)
        {
            foreach (var r in _all) r.enabled = v;
            if (scarf != null) scarf.SetVisible(v);
            if (v && !SwordVisible) SetSwordVisible(false);
        }

        /// <summary>False while the katana is broken (SAN-113): every part on the sword bone is hidden.</summary>
        public bool SwordVisible { get; private set; } = true;

        public void SetSwordVisible(bool v)
        {
            SwordVisible = v;
            foreach (var p in _parts)
            {
                if (p.def.bone != B.Sword) continue;
                p.main.enabled = v;
                if (p.rim != null) p.rim.enabled = v;
            }
        }

        public Vector2 World(B b, Vector2 local) => bones[(int)b].TransformPoint(local);

        /// <summary>Copies every visible part into a frozen, tinted ghost (afterimage VFX).</summary>
        public GameObject Snapshot(Color tint, int order)
        {
            var root = new GameObject(name + "_Ghost");
            MeshOwner owner = null;
            foreach (var p in _parts)
            {
                if (!p.main.enabled) continue;
                var mf = p.main.GetComponent<MeshFilter>();
                Mesh gm = mf.sharedMesh;
                if (p.def.soft != SoftKind.None)
                {
                    gm = Instantiate(gm);        // soft meshes keep deforming: freeze a copy
                    if (owner == null) owner = root.AddComponent<MeshOwner>();
                    owner.meshes.Add(gm);
                }
                var mr = MeshKit.MakeRenderer(p.def.name, root.transform, gm, MeshKit.Alpha, order + (p.main.sortingOrder - baseOrder));
                var t = mr.transform;
                t.SetPositionAndRotation(p.main.transform.position, p.main.transform.rotation);
                t.localScale = p.main.transform.lossyScale;
            }
            if (scarf != null) scarf.SnapshotInto(root.transform, order);
            return root;
        }

        public IEnumerable<MeshRenderer> Renderers => _all;

        void OnDestroy()
        {
            if (scarf != null) Destroy(scarf.gameObject);
        }
    }
}
