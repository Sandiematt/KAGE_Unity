using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Draws the chain sickle family's weapon (ADR-0006, SAN-87) in place of the part set's katana: a bone-ribbed
    /// sickle (handle, flange, pommel ring, a crescent blade with a violet-blood inner edge and a back spur) built with
    /// the rig's family part API, and the chain, a <see cref="ChainLine"/>: a link mesh drawn every frame from the hand
    /// to the sickle. The sickle's parts hang under one anchor on the Sword bone that slides along the blade line by the
    /// move's extension (<see cref="Fighter.BladeExtension"/>), so the sickle is always drawn at the end of the hit line
    /// and the chain between it and the hand is as long as the line. Attaching is idempotent.
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

            // the crescent: a banana hooking up and back, its spine a curve widest at the base, a back spur under the base
            Vector2[] spine = { new(0.3f, 0f), new(0.42f, -0.006f), new(0.52f, 0.012f), new(0.6f, 0.065f), new(0.64f, 0.145f), new(0.63f, 0.225f), new(0.595f, 0.285f) };
            var outer = new List<Vector2>();
            var inner = new List<Vector2>();
            var edge = new List<Vector2>();
            for (int i = 0; i < spine.Length; i++)
            {
                Vector2 a = spine[Mathf.Max(0, i - 1)], b = spine[Mathf.Min(spine.Length - 1, i + 1)];
                Vector2 t = (b - a).normalized, n = new Vector2(-t.y, t.x);       // n points to the concave side (up and back)
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
            // the violet-blood stripe along the cutting edge
            var stripe = new List<Vector2>();
            for (int i = 1; i < inner.Count - 1; i++) stripe.Add(At(Vector2.Lerp(inner[i], edge[i], 0.15f).x, Vector2.Lerp(inner[i], edge[i], 0.15f).y));
            for (int i = inner.Count - 2; i >= 1; i--) stripe.Add(At(edge[i].x, edge[i].y));
            Add("Edge", 1, Violet, PartRole.Trim, stripe.ToArray(), false, 1.6f);
            Add("Spur", 0, Palette.SteelDark, PartRole.Metal, Poly(0.27f, -0.02f, 0.36f, -0.026f, 0.31f, -0.09f), true);

            // the sickle's parts hang under one anchor so the lash can slide them along the blade line
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
                // hidden at once: detached, it sits at the world origin until Destroy lands at the end of the frame, and a headless
                // session runs many fights in one frame (SAN-114: it was drawn behind Kage in the next test's screenshots)
                anchor.gameObject.SetActive(false);
                anchor.SetParent(null, false);
                Object.Destroy(anchor.gameObject);
            }
            var line = LineOf(rig);
            if (line != null)
            {
                line.gameObject.SetActive(false);          // gone at once (Destroy lands at the end of the frame)
                Object.Destroy(line.gameObject);
            }
        }
    }

    /// <summary>
    /// The chain of the chain sickle (SAN-87): a mesh of links drawn from the far hand to the sickle's pommel ring along
    /// the blade line, rebuilt every frame from the bones and the move's extension. Hanging in a loop to the near hand at
    /// rest, it pulls straight as the lash extends the sickle. It mirrors the sickle's visibility and tint, so it hides
    /// with a broken weapon and flashes with the fighter. Runs after the fighter's pose (LateUpdate); a headless fight
    /// calls <see cref="Refresh"/> itself where it needs the drawing.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class ChainLine : MonoBehaviour
    {
        const float LinkLength = 0.04f;
        const float PommelU = -0.19f;           // the sickle's ring, along the blade line behind the guard point
        const int MaxLinks = 80;

        FighterRig _rig;
        Fighter _fighter;
        Transform _anchor;
        MeshRenderer _probe, _renderer;
        Mesh _mesh;
        MaterialPropertyBlock _mpb;
        readonly List<Vector3> _v = new();
        readonly List<Color> _c = new();
        readonly List<int> _t = new();

        public MeshRenderer Renderer => _renderer;
        /// <summary>World metres from the hand to the sickle's anchor as last drawn: the extension the hit line has.</summary>
        public float HeadDistance { get; private set; }
        /// <summary>World metres of chain drawn (hand end to pommel ring) as last drawn.</summary>
        public float ChainLength { get; private set; }

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
            Refresh();
        }

        void LateUpdate()
        {
            // between two logic frames the pose interpolates, so the chain does too (a headless fight poses at 0)
            bool manual = _fighter != null && _fighter.ctx != null && _fighter.ctx.Clock.Manual;
            Refresh(manual ? 0f : Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime));
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        /// <summary>Slides the sickle out by the move's extension and rebuilds the chain mesh.</summary>
        public void Refresh(float alpha = 0f)
        {
            if (_rig == null || _anchor == null || _renderer == null) return;
            bool visible = _probe == null || _probe.enabled;
            _renderer.enabled = visible;
            if (!visible) return;
            if (_probe != null)
            {
                _probe.GetPropertyBlock(_mpb);
                _renderer.SetPropertyBlock(_mpb);       // the sickle's tint, alpha and hit flash
            }
            _renderer.sortingOrder = _rig.baseOrder + 16 * 3 + 1 + (_rig.swordFront ? 30 * 4 * 3 : 0);

            float ext = _fighter != null ? _fighter.BladeExtensionAt(alpha) : 0f;
            Vector2 dir = _fighter != null ? _fighter.BladeDirLocal : Vector2.right;
            _anchor.localPosition = dir * ext;

            Vector2 hand = _rig.World(B.Sword, Vector2.zero);
            Vector2 head = _rig.World(B.Sword, dir * ext);
            HeadDistance = (head - hand).magnitude;
            Vector2 ring = _rig.World(B.Sword, dir * (ext + PommelU));
            Vector2 nearHand = _rig.World(B.HandN, _rig.def.fistLocal);

            // the chain's hand end: the near hand's loop while the sickle is home, the far hand once it is out and taut
            float k = Mathf.SmoothStep(0f, 1f, ext / 0.3f);
            Vector2 start = Vector2.Lerp(nearHand, hand, k);
            float dist = (ring - start).magnitude;
            ChainLength = dist;
            _v.Clear(); _c.Clear(); _t.Clear();
            if (dist > 0.03f)
            {
                float slack = 1f - Mathf.Clamp01(ext / 0.5f);
                float sag = slack * Mathf.Min(0.3f, dist * 0.45f + 0.05f) + 0.012f;
                Vector2 ctl = (start + ring) * 0.5f + Vector2.down * (sag * 2f);
                float arc = dist + sag * 1.4f;
                int n = Mathf.Clamp(Mathf.CeilToInt(arc / LinkLength), 2, MaxLinks);
                // outlines first, then the links, so every link reads over its neighbours' outlines
                for (int pass = 0; pass < 2; pass++)
                    for (int i = 0; i < n; i++)
                    {
                        float t0 = i / (float)n, t1 = (i + 1) / (float)n, tm = (i + 0.5f) / (float)n;
                        Vector2 p0 = Bezier(start, ctl, ring, t0), p1 = Bezier(start, ctl, ring, t1), pm = Bezier(start, ctl, ring, tm);
                        Vector2 tan = (p1 - p0);
                        float len = tan.magnitude;
                        if (len < 1e-5f) continue;
                        tan /= len;
                        bool face = (i & 1) == 0;                   // links alternate face-on (an oval) and edge-on (a thin bar)
                        float a = len * 0.66f + (pass == 0 ? 0.005f : 0f), b = (face ? 0.017f : 0.008f) + (pass == 0 ? 0.005f : 0f);
                        Color col = pass == 0 ? KMath.Hex("#07050a") : (face ? KMath.Hex("#a3a8b5") : KMath.Hex("#5d616d"));
                        if (face) Link(pm, tan, col, (-a, 0f), (-a * 0.5f, b), (a * 0.5f, b), (a, 0f), (a * 0.5f, -b), (-a * 0.5f, -b));
                        else Link(pm, tan, col, (-a, -b), (a, -b), (a, b), (-a, b));
                    }
            }
            _mesh.Clear();
            _mesh.SetVertices(_v);
            _mesh.SetColors(_c);
            _mesh.SetTriangles(_t, 0);
            _mesh.RecalculateBounds();
        }

        static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
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
