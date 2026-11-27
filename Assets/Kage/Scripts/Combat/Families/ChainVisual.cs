using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Draws the chain sickle family's weapon (ADR-0006, SAN-87) in place of the part set's katana: a bone-ribbed
    /// sickle (<c>kama</c>) gripped firmly in the lead hand (<see cref="B.Sword"/>), a dynamic multi-segment iron chain
    /// (<see cref="ChainLine"/>) connecting the sickle's pommel ring to the off-hand (<see cref="B.HandN"/>), and the
    /// weighted iron <c>fundo</c> controlled by the off-hand along curved windup/twirl splines, taut whipping arcs,
    /// and a live Verlet hanging pendulum at rest. Attaching is idempotent.
    /// </summary>
    public sealed class ChainVisual : IWeaponVisual
    {
        const string AnchorName = "Chain_Sickle";

        /// <summary>The chain line drawn for this rig, or null when it isn't carrying a chain sickle.</summary>
        public static ChainLine LineOf(FighterRig rig) =>
            rig != null && rig.transform.parent != null ? rig.transform.parent.GetComponentInChildren<ChainLine>() : null;

        static readonly Color Bone = KMath.Hex("#cdbf9f"), Rib = KMath.Hex("#3b2f45"), Flange = KMath.Hex("#6b5a80"), Ring = KMath.Hex("#8a8f9a"),
            Violet = KMath.Hex("#b36bff");

        public void Attach(FighterRig rig, string weaponId)
        {
            Clean(rig);
            rig.ClearFamilyParts();                  // idempotent: never add the parts twice
            rig.SetBaseSwordHidden(true);
            var d = rig.def;
            Transform bone = rig.Bone(B.Sword);

            // the drawing's axis is the hit line: from the guard point towards the tip, in the weapon frame AddFamilyPart expects
            Vector2 dir = KMath.Rot((d.bladeTipLocal - d.bladeBaseLocal).normalized, -d.bladeBindAngle);
            Vector2 perp = new(-dir.y, dir.x);
            Vector2 At(float u, float v) => dir * u + perp * v;
            Vector2[] Poly(params float[] uv)
            {
                var r = new Vector2[uv.Length / 2];
                for (int i = 0; i < r.Length; i++) r[i] = At(uv[i * 2], uv[i * 2 + 1]);
                return r;
            }
            var names = new List<string>();
            void Add(string name, int sub, Color c, PartRole role, Vector2[] pts, bool rim = false, float glow = 1f)
            {
                names.Add("Chain_" + name);
                rig.AddFamilyPart("Chain_" + name, pts, c, role, sub, rim, glow);
            }

            // the handle: a bone grip with ribs, a flange where the blade starts, a ring at the pommel
            Add("Handle", 0, Bone, PartRole.Body, Poly(-0.12f, -0.018f, 0.34f, -0.016f, 0.34f, 0.016f, -0.12f, 0.018f), true);
            for (int i = 0; i < 4; i++)
            {
                float u = -0.08f + i * 0.07f;
                Add("Rib", 1, Rib, PartRole.TrimDeep, Poly(u - 0.009f, -0.019f, u + 0.009f, -0.019f, u + 0.009f, 0.019f, u - 0.009f, 0.019f));
            }
            Add("Flange", 2, Flange, PartRole.Metal, Poly(0.3f, -0.04f, 0.36f, -0.03f, 0.36f, 0.03f, 0.3f, 0.04f), true);
            Add("Ring", 1, Ring, PartRole.Metal, Poly(-0.19f, 0f, -0.165f, -0.032f, -0.12f, -0.032f, -0.1f, 0f, -0.12f, 0.032f, -0.165f, 0.032f), true);
            Add("RingHole", 2, KMath.Hex("#0d0a10"), PartRole.Body, Poly(-0.165f, 0f, -0.152f, -0.014f, -0.13f, -0.014f, -0.12f, 0f, -0.13f, 0.014f, -0.152f, 0.014f));

            // the crescent: a hooked sickle blade with violet-blood inner cutting edge and back spur
            Vector2[] spine = { new(0.3f, 0f), new(0.42f, -0.006f), new(0.52f, 0.012f), new(0.6f, 0.065f), new(0.64f, 0.145f), new(0.63f, 0.225f), new(0.595f, 0.285f) };
            var outer = new List<Vector2>();
            var inner = new List<Vector2>();
            var edge = new List<Vector2>();
            for (int i = 0; i < spine.Length; i++)
            {
                Vector2 a = spine[Mathf.Max(0, i - 1)], b = spine[Mathf.Min(spine.Length - 1, i + 1)];
                Vector2 t = (b - a).normalized, n = new Vector2(-t.y, t.x);
                float k = i / (float)(spine.Length - 1);
                float w = Mathf.Lerp(0.04f, 0.004f, Mathf.Pow(k, 0.8f));
                outer.Add(spine[i] - n * w);
                inner.Add(spine[i] + n * w);
                edge.Add(spine[i] + n * (w * 0.55f));
            }
            var poly = new List<Vector2>();
            foreach (var p in outer) poly.Add(At(p.x, p.y));
            for (int i = inner.Count - 2; i >= 0; i--) poly.Add(At(inner[i].x, inner[i].y));
            Add("Blade", 0, Palette.Steel, PartRole.Metal, poly.ToArray(), true);

            var stripe = new List<Vector2>();
            for (int i = 1; i < inner.Count - 1; i++) stripe.Add(At(Vector2.Lerp(inner[i], edge[i], 0.15f).x, Vector2.Lerp(inner[i], edge[i], 0.15f).y));
            for (int i = inner.Count - 2; i >= 1; i--) stripe.Add(At(edge[i].x, edge[i].y));
            Add("Edge", 1, Violet, PartRole.Trim, stripe.ToArray(), false, 1.6f);
            Add("Spur", 0, Palette.SteelDark, PartRole.Metal, Poly(0.27f, -0.02f, 0.36f, -0.026f, 0.31f, -0.09f), true);

            // sickle stays anchored cleanly in HandF (B.Sword); never detaches like a stick
            var anchor = new GameObject(AnchorName).transform;
            anchor.SetParent(bone, false);
            MeshRenderer probe = null;
            foreach (var n in names)
            {
                foreach (var nm in new[] { n, n + "_Rim" })
                {
                    var t = bone.Find(nm);
                    if (t == null) continue;
                    t.SetParent(anchor, false);
                    if (probe == null && nm == "Chain_Handle") probe = t.GetComponent<MeshRenderer>();
                }
            }

            var go = new GameObject("Chain_Line");
            go.transform.SetParent(rig.transform.parent != null ? rig.transform.parent : rig.transform, false);
            go.AddComponent<ChainLine>().Init(rig, rig.GetComponentInParent<Fighter>(), anchor, probe);
        }

        public void Detach(FighterRig rig)
        {
            Clean(rig);
            rig.ClearFamilyParts();
        }

        /// <summary>Removes the anchor and the chain line a previous Attach made.</summary>
        static void Clean(FighterRig rig)
        {
            var anchor = rig.Bone(B.Sword).Find(AnchorName);
            if (anchor != null)
            {
                anchor.gameObject.SetActive(false);
                anchor.SetParent(null, false);
                Object.Destroy(anchor.gameObject);
            }
            var line = LineOf(rig);
            if (line != null)
            {
                line.gameObject.SetActive(false);
                Object.Destroy(line.gameObject);
            }
        }
    }

    /// <summary>
    /// Dynamic multi-segment chain and weighted iron <c>fundo</c> for Yamigama (kusarigama):
    /// <list type="bullet">
    /// <item>Draws a draping tether chain between the sickle's pommel ring in <see cref="B.Sword"/> and the off-hand (<see cref="B.HandN"/>).</item>
    /// <item>During chain whip moves (<see cref="MoveDef.Chain"/>), renders the whip chain along the dynamic curved-to-taut
    /// spline from <see cref="B.HandN"/> to the iron weight (<c>fundo</c>), curving cleanly on windup/twirl, snapping dead-taut
    /// along whipping arcs, and rippling cleanly back on recovery.</item>
    /// <item>At rest and during close sickle cuts, simulates a 6-node Verlet pendulum hanging from <see cref="B.HandN"/> with
    /// gravity, wind/gust reaction, and floor clearance so the chain never behaves like a rigid stick.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class ChainLine : MonoBehaviour
    {
        const float LinkLength = 0.042f;
        const float PommelU = -0.19f;
        const int MaxLinks = 72;
        const int PendulumNodes = 6;
        const float PendulumSegLen = 0.072f;

        FighterRig _rig;
        Fighter _fighter;
        Transform _anchor;
        MeshRenderer _probe, _renderer;
        Mesh _mesh;
        MaterialPropertyBlock _mpb;
        readonly List<Vector3> _v = new();
        readonly List<Color> _c = new();
        readonly List<int> _t = new();

        readonly Vector2[] _nodePos = new Vector2[PendulumNodes];
        readonly Vector2[] _nodePrev = new Vector2[PendulumNodes];
        bool _pendulumInit;
        Vector2 _prevNearHand;

        public MeshRenderer Renderer => _renderer;
        /// <summary>World metres from off-hand to the active chain whip weight (<c>fundo</c>); 0 when retracted/hanging at rest.</summary>
        public float HeadDistance { get; private set; }
        /// <summary>World metres of active whip chain (or hanging pendulum chain) as last drawn.</summary>
        public float ChainLength { get; private set; }
        /// <summary>World position of the weighted iron end (<c>fundo</c>) as last drawn.</summary>
        public Vector2 WeightWorld { get; private set; }
        /// <summary>Perpendicular spline bow deviation (metres) of the whip chain midpoint from its chord; >0 during curved windup/twirl, ~0 when taut.</summary>
        public float CurveDeviation { get; private set; }

        public void Init(FighterRig rig, Fighter fighter, Transform anchor, MeshRenderer probe)
        {
            _rig = rig;
            _fighter = fighter;
            _anchor = anchor;
            _probe = probe;
            _mesh = new Mesh { name = "ChainLine" };
            _mesh.MarkDynamic();
            _renderer = MeshKit.MakeRenderer("Chain_Line", transform, _mesh, MeshKit.Alpha, 0);
            _mpb = new MaterialPropertyBlock();
            _pendulumInit = false;
            Refresh();
        }

        void LateUpdate()
        {
            bool manual = _fighter != null && _fighter.ctx != null && _fighter.ctx.Clock.Manual;
            Refresh(manual ? 0f : Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime));
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        /// <summary>Rebuilds the multi-segment tether chain, dynamic whip/pendulum chain, and faceted iron <c>fundo</c>.</summary>
        public void Refresh(float alpha = 0f)
        {
            if (_rig == null || _anchor == null || _renderer == null) return;
            bool visible = _probe == null || _probe.enabled;
            _renderer.enabled = visible;
            if (!visible) return;
            if (_probe != null)
            {
                _probe.GetPropertyBlock(_mpb);
                _renderer.SetPropertyBlock(_mpb);
            }
            _renderer.sortingOrder = _rig.baseOrder + 16 * 3 + 1 + (_rig.swordFront ? 30 * 4 * 3 : 0);

            // Keep the sickle firmly gripped in the lead hand at all times
            _anchor.localPosition = Vector3.zero;

            Vector2 dir = _fighter != null ? _fighter.BladeDirLocal : Vector2.right;
            Vector2 ring = _rig.World(B.Sword, dir * PommelU);
            Vector2 nearHand = _rig.World(B.HandN, _rig.def.fistLocal);

            _v.Clear(); _c.Clear(); _t.Clear();

            // 1. Short draping tether chain between sickle pommel ring (HandF) and off-hand (HandN)
            float tetherDist = (ring - nearHand).magnitude;
            if (tetherDist > 0.04f)
            {
                float tetherSag = Mathf.Min(0.24f, tetherDist * 0.42f + 0.045f);
                Vector2 tetherCtrl = (nearHand + ring) * 0.5f + Vector2.down * (tetherSag * 1.6f);
                float floor = _rig.groundY + 0.04f;
                if (tetherCtrl.y < floor) tetherCtrl.y = floor;
                DrawBezierChain(nearHand, tetherCtrl, ring, tetherDist + tetherSag * 1.1f);
            }

            // 2. Dynamic weighted chain (off-hand HandN -> iron fundo)
            if (_fighter != null && _fighter.TryChainWhipWorld(alpha, out var whipHand, out var whipWeight, out var whipCtrl, out float whipExt) && whipExt > 0.02f)
            {
                HeadDistance = (whipWeight - whipHand).magnitude;
                Vector2 chord = whipWeight - whipHand;
                float chordLen = chord.magnitude;
                Vector2 mid = Fighter.ChainBezier(whipHand, whipCtrl, whipWeight, 0.5f);
                Vector2 chordMid = (whipHand + whipWeight) * 0.5f;
                CurveDeviation = (mid - chordMid).magnitude;
                float approxArc = chordLen + CurveDeviation * 1.6f;
                ChainLength = approxArc;
                WeightWorld = whipWeight;

                DrawBezierChain(whipHand, whipCtrl, whipWeight, approxArc);

                Vector2 tipTan = whipWeight - Fighter.ChainBezier(whipHand, whipCtrl, whipWeight, 0.88f);
                if (tipTan.sqrMagnitude < 1e-6f) tipTan = chordLen > 1e-5f ? chord / chordLen : Vector2.right * _rig.facing;
                else tipTan.Normalize();
                DrawFundo(whipWeight, tipTan);

                // Sync Verlet pendulum nodes along the whip spline for a seamless physical drop on recovery
                for (int i = 0; i < PendulumNodes; i++)
                {
                    float u = i / (float)(PendulumNodes - 1);
                    Vector2 p = Fighter.ChainBezier(whipHand, whipCtrl, whipWeight, u);
                    _nodePos[i] = p;
                    _nodePrev[i] = p;
                }
                _pendulumInit = true;
                _prevNearHand = nearHand;
            }
            else
            {
                // Fallback for any move using legacy MoveDef.Extend without Chain(...)
                float legacyExt = _fighter != null ? _fighter.BladeExtensionAt(alpha) : 0f;
                if (legacyExt > 0.02f)
                {
                    Vector2 tipDir = (_rig.World(B.Sword, dir) - _rig.World(B.Sword, Vector2.zero)).normalized;
                    Vector2 wPos = nearHand + tipDir * legacyExt;
                    float floor = _rig.groundY + 0.05f;
                    if (wPos.y < floor) wPos.y = floor;
                    Vector2 ctrl = (nearHand + wPos) * 0.5f + Vector2.down * 0.03f;
                    HeadDistance = (wPos - nearHand).magnitude;
                    ChainLength = HeadDistance;
                    CurveDeviation = 0.015f;
                    WeightWorld = wPos;
                    DrawBezierChain(nearHand, ctrl, wPos, HeadDistance);
                    DrawFundo(wPos, (wPos - nearHand).normalized);
                    _pendulumInit = false;
                }
                else
                {
                    // Dynamic Verlet pendulum hanging from off-hand with wind, inertia and floor collision
                    StepPendulum(nearHand);
                    HeadDistance = 0f;
                    CurveDeviation = PendulumCurvature();
                    float len = 0f;
                    for (int i = 1; i < PendulumNodes; i++) len += (_nodePos[i] - _nodePos[i - 1]).magnitude;
                    ChainLength = len;
                    WeightWorld = _nodePos[PendulumNodes - 1];

                    DrawPolylineChain(_nodePos);
                    Vector2 tan = _nodePos[PendulumNodes - 1] - _nodePos[PendulumNodes - 2];
                    tan = tan.sqrMagnitude > 1e-6f ? tan.normalized : Vector2.down;
                    DrawFundo(WeightWorld, tan);
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(_v);
            _mesh.SetColors(_c);
            _mesh.SetTriangles(_t, 0);
            _mesh.RecalculateBounds();
        }

        void StepPendulum(Vector2 anchor)
        {
            if (!_pendulumInit || (anchor - _prevNearHand).sqrMagnitude > 1.2f * 1.2f)
            {
                for (int i = 0; i < PendulumNodes; i++)
                {
                    Vector2 p = anchor + new Vector2(-_rig.facing * i * 0.015f, -i * PendulumSegLen);
                    p.y = Mathf.Max(_rig.groundY + 0.05f, p.y);
                    _nodePos[i] = p;
                    _nodePrev[i] = p;
                }
                _pendulumInit = true;
                _prevNearHand = anchor;
                return;
            }

            float dt = Time.fixedDeltaTime;
            Vector2 windAccel = _rig.wind.At(Time.time, anchor.x) * 1.6f + _rig.ActiveGust * 2.2f;
            Vector2 grav = new Vector2(windAccel.x, -15.5f + windAccel.y);
            float floor = _rig.groundY + 0.05f;

            _nodePos[0] = anchor;
            _nodePrev[0] = anchor;
            for (int i = 1; i < PendulumNodes; i++)
            {
                Vector2 cur = _nodePos[i];
                Vector2 vel = (cur - _nodePrev[i]) * 0.90f;
                _nodePrev[i] = cur;
                Vector2 next = cur + vel + grav * (dt * dt);
                if (next.y < floor) next.y = floor;
                _nodePos[i] = next;
            }
            for (int it = 0; it < 4; it++)
            {
                _nodePos[0] = anchor;
                for (int i = 1; i < PendulumNodes; i++)
                {
                    Vector2 d = _nodePos[i] - _nodePos[i - 1];
                    float m = d.magnitude;
                    if (m < 1e-5f) { _nodePos[i] = _nodePos[i - 1] + Vector2.down * PendulumSegLen; continue; }
                    Vector2 corr = d * ((m - PendulumSegLen) / m);
                    if (i == 1) _nodePos[i] -= corr;
                    else { _nodePos[i - 1] += corr * 0.35f; _nodePos[i] -= corr * 0.65f; }
                    if (_nodePos[i].y < floor) _nodePos[i].y = floor;
                }
            }
            _prevNearHand = anchor;
        }

        float PendulumCurvature()
        {
            Vector2 a = _nodePos[0], b = _nodePos[PendulumNodes - 1];
            Vector2 mid = _nodePos[PendulumNodes / 2];
            return (mid - (a + b) * 0.5f).magnitude;
        }

        void DrawBezierChain(Vector2 a, Vector2 ctrl, Vector2 b, float arcLen)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(arcLen / LinkLength), 2, MaxLinks);
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    float t0 = i / (float)n, t1 = (i + 1) / (float)n, tm = (i + 0.5f) / (float)n;
                    Vector2 p0 = Fighter.ChainBezier(a, ctrl, b, t0);
                    Vector2 p1 = Fighter.ChainBezier(a, ctrl, b, t1);
                    Vector2 pm = Fighter.ChainBezier(a, ctrl, b, tm);
                    EmitLink(pm, p1 - p0, i, pass);
                }
        }

        void DrawPolylineChain(Vector2[] pts)
        {
            int segs = pts.Length - 1;
            for (int pass = 0; pass < 2; pass++)
                for (int s = 0; s < segs; s++)
                {
                    Vector2 p0 = pts[s], p1 = pts[s + 1];
                    Vector2 pm = (p0 + p1) * 0.5f;
                    EmitLink(pm, p1 - p0, s, pass);
                }
        }

        void EmitLink(Vector2 pm, Vector2 delta, int index, int pass)
        {
            float len = delta.magnitude;
            if (len < 1e-5f) return;
            Vector2 tan = delta / len;
            bool face = (index & 1) == 0;
            float a = Mathf.Min(len * 0.68f, LinkLength * 0.85f) + (pass == 0 ? 0.005f : 0f);
            float b = (face ? 0.017f : 0.008f) + (pass == 0 ? 0.005f : 0f);
            Color col = pass == 0 ? KMath.Hex("#07050a") : (face ? KMath.Hex("#a3a8b5") : KMath.Hex("#5d616d"));
            if (face) Link(pm, tan, col, (-a, 0f), (-a * 0.5f, b), (a * 0.5f, b), (a, 0f), (a * 0.5f, -b), (-a * 0.5f, -b));
            else Link(pm, tan, col, (-a, -b), (a, -b), (a, b), (-a, b));
        }

        /// <summary>Draws the faceted heavy iron <c>fundo</c> weight at the end of the chain with outline, steel bevel, and violet rune core.</summary>
        void DrawFundo(Vector2 centre, Vector2 tan)
        {
            // Dark outline octagon
            Link(centre, tan, KMath.Hex("#07050a"),
                (-0.044f, -0.022f), (-0.018f, -0.044f), (0.028f, -0.040f), (0.062f, 0f),
                (0.028f, 0.040f), (-0.018f, 0.044f), (-0.044f, 0.022f));
            // Dark iron body
            Link(centre, tan, KMath.Hex("#474956"),
                (-0.038f, -0.018f), (-0.015f, -0.038f), (0.024f, -0.034f), (0.054f, 0f),
                (0.024f, 0.034f), (-0.015f, 0.038f), (-0.038f, 0.018f));
            // Lit steel upper bevel
            Link(centre, tan, KMath.Hex("#a8aebd"),
                (-0.030f, 0.004f), (-0.010f, 0.031f), (0.022f, 0.027f), (0.046f, 0.002f));
            // Violet glowing rune core
            Link(centre, tan, KMath.Hex("#b36bff"),
                (-0.014f, 0f), (0.004f, -0.014f), (0.022f, 0f), (0.004f, 0.014f));
        }

        /// <summary>Adds a convex polygon (points in link space: x along the chain) as a triangle fan.</summary>
        void Link(Vector2 centre, Vector2 tan, Color col, params (float x, float y)[] pts)
        {
            Vector2 nrm = new(-tan.y, tan.x);
            Color lc = MeshKit.L(col);
            int b0 = _v.Count;
            foreach (var (x, y) in pts)
            {
                _v.Add(transform.InverseTransformPoint(centre + tan * x + nrm * y));
                _c.Add(lc);
            }
            for (int i = 1; i < pts.Length - 1; i++) { _t.Add(b0); _t.Add(b0 + i); _t.Add(b0 + i + 1); }
        }
    }
}
