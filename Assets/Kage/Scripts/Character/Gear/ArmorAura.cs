using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Subtle, prestige ambient VFX carried directly on Legendary armors (<see cref="ArmorAuraKind.Spirit"/> for Yūrei Armor
    /// and <see cref="ArmorAuraKind.Void"/> for Mugen Armor): drifting embers/spirit motes rising off the cape and pauldrons,
    /// soft pulsing rune shimmer at the chest core, and brief electric crackle accents across the pauldrons and halo for Mugen.
    /// <para>
    /// Completely self-contained per fighter (draws via local <see cref="Ribbon"/> meshes sorted with the fighter's rig), so it
    /// works cleanly in active combat, the Armory's multi-card live row (hiding automatically when an off-screen card hides its rig),
    /// Loadout, and ProfileKage without interfering with other fighters or mimicking unblockable combat tells.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(240)]
    public sealed class ArmorAura : MonoBehaviour
    {
        struct Mote
        {
            public bool live;
            public Vector2 pos, vel;
            public float age, life, size, phase;
            public Color col;
        }

        struct Arc
        {
            public bool live;
            public B boneA, boneB;
            public Vector2 offA, offB;
            public float age, life, seed;
        }

        Fighter _f;
        ArmorAuraKind _kind;
        GameObject _root;
        Ribbon _glowRibbon, _coreRibbon;
        int _builtOrder = int.MinValue;
        float _t, _alive = 1f, _moteAcc, _arcTimer;
        readonly Mote[] _motes = new Mote[18];
        readonly Arc[] _arcs = new Arc[3];
        readonly List<Vector2> _pts = new(8);

        static readonly B[] MoteBones = { B.Chest, B.ArmNU, B.Hips, B.ArmNL, B.Chest };
        static readonly Color SpiritPrimary = new(0.45f, 0.82f, 1.00f, 1f);
        static readonly Color SpiritGold = new(1.00f, 0.88f, 0.48f, 1f);
        static readonly Color SpiritCore = new(0.94f, 0.98f, 1.00f, 1f);
        static readonly Color VoidPrimary = new(0.84f, 0.36f, 1.00f, 1f);
        static readonly Color VoidGold = new(1.00f, 0.84f, 0.36f, 1f);
        static readonly Color VoidCore = new(0.98f, 0.92f, 1.00f, 1f);

        public ArmorAuraKind Kind => _kind;

        /// <summary>Attaches the prestige armor aura when <paramref name="armorId"/> is a Legendary armor with an aura.</summary>
        public static ArmorAura AttachIfLegendary(Fighter f, string armorId)
        {
            if (f == null || string.IsNullOrEmpty(armorId)) return null;
            var design = ArmorDesigns.Get(armorId);
            if (design == null || design.aura == ArmorAuraKind.None) return null;
            var a = f.GetComponent<ArmorAura>();
            if (a == null) a = f.gameObject.AddComponent<ArmorAura>();
            a._f = f;
            a._kind = design.aura;
            return a;
        }

        void EnsureBuilt()
        {
            if (_root != null) return;
            _root = new GameObject("ArmorAura_" + _kind);
            _root.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
            var addMat = MeshKit.MakeMat(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One, "ArmorAuraGlow_" + _kind, null);
            addMat.SetVector("_Color", new Vector4(1.65f, 1.65f, 1.65f, 1f));
            var coreMat = MeshKit.MakeMat(UnityEngine.Rendering.BlendMode.SrcAlpha, UnityEngine.Rendering.BlendMode.One, "ArmorAuraCore_" + _kind, null);
            coreMat.SetVector("_Color", new Vector4(2.2f, 2.2f, 2.2f, 1f));
            _glowRibbon = new Ribbon("AuraGlow", _root.transform, addMat, 0);
            _coreRibbon = new Ribbon("AuraCore", _root.transform, coreMat, 0);
            _builtOrder = int.MinValue;
            // Seed a few motes so still captures & immediate menu opens already show prestige accents
            for (int i = 0; i < 6; i++)
                SpawnMote(initialAgeFraction: (i + 0.5f) / 6.5f);
            if (_kind == ArmorAuraKind.Void) SpawnArc(initialAgeFraction: 0.25f);
        }

        bool RigVisible()
        {
            if (_f == null || _f.rig == null || !_f.rig.gameObject.activeInHierarchy) return false;
            foreach (var r in _f.rig.Renderers)
                if (r != null && r.enabled) return true;
            return false;
        }

        void SetVisible(bool on)
        {
            if (_root != null && _root.activeSelf != on) _root.SetActive(on);
        }

        void LateUpdate()
        {
            if (_f == null || _f.rig == null || _kind == ArmorAuraKind.None)
            {
                SetVisible(false);
                return;
            }
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!RigVisible())
            {
                SetVisible(false);
                return;
            }

            float dt = Time.deltaTime;
            _t += dt;
            bool ko = _f.state == FState.KO || _f.health <= 0f;
            _alive = ko ? Mathf.Max(0f, _alive - dt * 2f) : Mathf.Min(1f, _alive + dt * 3f);
            if (_alive <= 0.005f)
            {
                SetVisible(false);
                return;
            }

            EnsureBuilt();
            SetVisible(true);

            int targetOrder = _f.rig.baseOrder + 355;
            if (_builtOrder != targetOrder)
            {
                _builtOrder = targetOrder;
                _glowRibbon.renderer.sortingOrder = targetOrder;
                _coreRibbon.renderer.sortingOrder = targetOrder + 1;
            }

            if (!ko && dt > 0f)
            {
                float rate = _kind == ArmorAuraKind.Void ? 7.5f : 6.5f;
                _moteAcc += rate * dt;
                while (_moteAcc >= 1f)
                {
                    _moteAcc -= 1f;
                    SpawnMote(0f);
                }

                if (_kind == ArmorAuraKind.Void)
                {
                    _arcTimer -= dt;
                    if (_arcTimer <= 0f)
                    {
                        _arcTimer = Random.Range(0.28f, 0.58f);
                        SpawnArc(0f);
                    }
                }
            }

            _glowRibbon.Begin();
            _coreRibbon.Begin();

            // 1. Soft pulsing prestige rune shimmer at the chest emblem & shoulder crest
            float pulse = 0.62f + 0.38f * Mathf.Sin(_t * (_kind == ArmorAuraKind.Void ? 4.4f : 3.2f));
            Color cPrim = _kind == ArmorAuraKind.Void ? VoidPrimary : SpiritPrimary;
            Color cGold = _kind == ArmorAuraKind.Void ? VoidGold : SpiritGold;
            Color cCore = _kind == ArmorAuraKind.Void ? VoidCore : SpiritCore;

            Vector2 chestCore = _f.rig.World(B.Chest, new Vector2(0.08f, 0.22f));
            Vector2 sodeCore = _f.rig.World(B.ArmNU, new Vector2(-0.04f, 0.01f));
            Vfx.SoftBlob(_glowRibbon, chestCore, 0.075f, 0.095f, cPrim.WithA(0.24f * pulse * _alive), 0.12f, 12);
            Vfx.SoftBlob(_coreRibbon, chestCore, 0.032f, 0.044f, cCore.WithA(0.32f * pulse * _alive), 0.18f, 10);
            Vfx.SoftBlob(_glowRibbon, sodeCore, 0.055f, 0.068f, cGold.WithA(0.20f * pulse * _alive), 0.14f, 10);

            // 2. Drifting embers / spirit motes rising gently off the armor & cape
            for (int i = 0; i < _motes.Length; i++)
            {
                ref var m = ref _motes[i];
                if (!m.live) continue;
                m.age += dt;
                float u = m.age / m.life;
                if (u >= 1f) { m.live = false; continue; }
                m.pos += m.vel * dt;
                float sway = Mathf.Sin(_t * 4.5f + m.phase) * 0.014f;
                Vector2 p = m.pos + new Vector2(sway, 0f);
                float alpha = Mathf.Sin(u * Mathf.PI) * 0.72f * _alive;
                float sz = m.size * Mathf.Lerp(1.0f, 0.35f, u);
                Vfx.SoftBlob(_glowRibbon, p, sz * 1.65f, sz * 1.95f, m.col.WithA(alpha * 0.55f), 0.15f, 8);
                Vfx.SoftBlob(_coreRibbon, p, sz * 0.65f, sz * 0.78f, cCore.WithA(alpha * 0.85f), 0.25f, 6);
            }

            // 3. Brief crackling energy accents across pauldrons, chest & halo (Void Sovereign)
            if (_kind == ArmorAuraKind.Void)
            {
                for (int i = 0; i < _arcs.Length; i++)
                {
                    ref var a = ref _arcs[i];
                    if (!a.live) continue;
                    a.age += dt;
                    float u = a.age / a.life;
                    if (u >= 1f) { a.live = false; continue; }
                    Vector2 p0 = _f.rig.World(a.boneA, a.offA);
                    Vector2 p1 = _f.rig.World(a.boneB, a.offB);
                    BuildCrackle(p0, p1, a.seed + Mathf.Floor(_t * 22f) * 3.1f);
                    float env = Mathf.Sin(u * Mathf.PI) * _alive;
                    Vfx.SoftLine(_glowRibbon, _pts, 0.015f, 0.008f, VoidPrimary.WithA(0.68f * env));
                    Vfx.SoftLine(_coreRibbon, _pts, 0.006f, 0.003f, VoidCore.WithA(0.82f * env));
                }
            }

            _glowRibbon.End();
            _coreRibbon.End();
        }

        void SpawnMote(float initialAgeFraction)
        {
            for (int i = 0; i < _motes.Length; i++)
            {
                if (_motes[i].live) continue;
                B bone = MoteBones[Random.Range(0, MoteBones.Length)];
                Vector2 local = new(Random.Range(-0.14f, 0.12f), Random.Range(-0.10f, 0.20f));
                Vector2 p = _f.rig.World(bone, local);
                float life = Random.Range(0.75f, 1.35f);
                float driftX = -_f.facing * Random.Range(0.06f, 0.18f) + Random.Range(-0.04f, 0.04f);
                float driftY = Random.Range(0.22f, 0.44f);
                Color col = _kind == ArmorAuraKind.Void
                    ? (Random.value < 0.35f ? VoidGold : VoidPrimary)
                    : (Random.value < 0.35f ? SpiritGold : SpiritPrimary);
                float age = life * initialAgeFraction;
                _motes[i] = new Mote
                {
                    live = true,
                    pos = p + new Vector2(driftX, driftY) * age,
                    vel = new Vector2(driftX, driftY),
                    age = age,
                    life = life,
                    size = Random.Range(0.012f, 0.021f),
                    phase = Random.Range(0f, 6.28f),
                    col = col,
                };
                return;
            }
        }

        void SpawnArc(float initialAgeFraction)
        {
            for (int i = 0; i < _arcs.Length; i++)
            {
                if (_arcs[i].live) continue;
                int pattern = Random.Range(0, 3);
                B ba, bb;
                Vector2 oa, ob;
                if (pattern == 0)
                {
                    // Pauldron crest -> chest core
                    ba = B.ArmNU; oa = new Vector2(-0.08f, 0.08f);
                    bb = B.Chest; ob = new Vector2(0.08f, 0.22f);
                }
                else if (pattern == 1)
                {
                    // Halo crown -> pauldron crest
                    ba = B.Head; oa = new Vector2(-0.18f, 0.26f);
                    bb = B.ArmNU; ob = new Vector2(-0.12f, 0.10f);
                }
                else
                {
                    // Pauldron -> armored gauntlet
                    ba = B.ArmNU; oa = new Vector2(-0.06f, 0.02f);
                    bb = B.ArmNL; ob = new Vector2(-0.02f, -0.08f);
                }
                float life = Random.Range(0.14f, 0.24f);
                _arcs[i] = new Arc
                {
                    live = true,
                    boneA = ba, boneB = bb,
                    offA = oa, offB = ob,
                    age = life * initialAgeFraction,
                    life = life,
                    seed = Random.Range(0f, 100f),
                };
                return;
            }
        }

        void BuildCrackle(Vector2 a, Vector2 b, float seed)
        {
            _pts.Clear();
            const int segs = 5;
            Vector2 d = b - a;
            Vector2 perp = d.sqrMagnitude > 1e-6f ? new Vector2(-d.y, d.x).normalized : Vector2.up;
            float amp = Mathf.Min(0.055f, d.magnitude * 0.22f);
            for (int k = 0; k <= segs; k++)
            {
                float u = k / (float)segs;
                float j = (k == 0 || k == segs) ? 0f : Mathf.Sin(seed * 1.9f + k * 4.7f) * amp;
                _pts.Add(Vector2.Lerp(a, b, u) + perp * j);
            }
        }

        void OnDestroy()
        {
            if (_glowRibbon != null && _glowRibbon.mesh != null) Destroy(_glowRibbon.mesh);
            if (_coreRibbon != null && _coreRibbon.mesh != null) Destroy(_coreRibbon.mesh);
            if (_root != null) Destroy(_root);
        }
    }
}
