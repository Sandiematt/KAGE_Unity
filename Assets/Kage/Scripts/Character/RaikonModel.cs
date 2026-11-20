using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Builds Raikon's rig from his SVG (see <see cref="RaikonPartSet"/>). The cut is made once per run and cached.</summary>
    public static class RaikonModel
    {
        public const string SvgResource = "Art/Raikon/Raikon.svg";

        // ---- the painting on the skeleton: SVG px -> Kage reference px (0.688 per SVG px), his soles on Kage's ground ----
        const float K = 0.688f, SvgGround = 1195f, SvgHipsX = 501f;

        static Vector2 G(Vector2 s) => KageModel.P(878f + (s.x - SvgHipsX) * K, 815f - (SvgGround - s.y) * K);

        // ---- drawn joints (SVG px) ----
        static readonly Vector2 ShoulderN = new(545, 410), ElbowN = new(548, 528), WristN = new(542, 628);
        static readonly Vector2 ElbowFist = new(392, 578), WristFist = new(358, 628);          // the painted far (fist) arm
        static readonly Vector2 Grip = new(545, 665), BladeTip = new(105, 1005);                // tachi: grip in the hand, tip
        static readonly Vector2 HipN = new(505, 800), KneeN = new(518, 1000), AnkleN = new(520, 1118);

        /// <summary>The blade's guard on the skeleton's sword bone (<c>KageModel.Skeleton</c>'s blade base): the length is measured from here, as the hit line is.</summary>
        const float BladeGuard = 0.08f;

        static float? _paintedBlade;

        /// <summary>
        /// The painted tachi's blade, guard to tip, in Sword-bone units (SAN-107): how far the painting's <c>Tachi_Blade</c> runs along the drawn blade's axis
        /// from the sword bone's pivot (the grip), less the guard. This is Raikon's <see cref="CharacterDef.bladeLength"/>, so his hit line ends where his
        /// painted blade does; the tachi family's 1.5 m would put it more than a metre beyond. Computed from the traced regions, so it follows the art.
        /// </summary>
        public static float PaintedBladeLength => _paintedBlade ??= MeasurePaintedBlade();

        static float MeasurePaintedBlade()
        {
            Vector2 axis = (G(BladeTip) - G(Grip)).normalized;
            float tip = 0f;
            foreach (var r in RaikonRegions.Parts)
            {
                if (r.name != "Tachi_Blade") continue;
                foreach (var xy in r.polygons)
                    for (int i = 0; i + 1 < xy.Length; i += 2)
                        tip = Mathf.Max(tip, Vector2.Dot(G(new Vector2(xy[i], xy[i + 1])) - G(Grip), axis));
            }
            return tip - BladeGuard;
        }

        // ---- palette of the trace ----
        static readonly Color Ink = new Color32(1, 0, 0, 255);

        /// <summary>The baked cut (<see cref="Bake"/>): every piece the part set needs, so a spawn doesn't cut the SVG.</summary>
        public const string BakeResource = "Art/Raikon/RaikonCut";

        /// <summary>Bump when the cutting code changes what a cut produces: the bake then counts as stale.</summary>
        const int CutVersion = 1;

        static SvgArt _art;
        static Dictionary<string, List<SvgPiece>> _cuts;

        /// <summary>The owner's SVG, read once.</summary>
        public static SvgArt Art
        {
            get
            {
                if (_art != null) return _art;
                _art = SvgArt.Parse(SvgText());
                _art.backgroundLayer = 0;        // #010000: the canvas and the figure's ink
                return _art;
            }
        }

        static string SvgText()
        {
            var ta = Resources.Load<TextAsset>(SvgResource);
            if (ta == null) throw new InvalidOperationException("Raikon's SVG is missing: Resources/" + SvgResource + ".bytes");
            return ta.text;
        }

        static List<SvgPiece> Cut(string region, float cell) => Cached("cut:" + region + "@" + cell, () =>
        {
            float[][] polys = null;
            foreach (var r in RaikonRegions.Parts) if (r.name == region) { polys = r.polygons; break; }
            if (polys == null) throw new ArgumentException("no Raikon region " + region);
            var list = new List<Vector2[]>();
            foreach (var xy in polys) list.Add(Pts(xy));
            return Art.Cut(list, cell);
        });

        static List<SvgPiece> Cached(string key, Func<List<SvgPiece>> make)
        {
            if (_cuts == null)
            {
                _cuts = new Dictionary<string, List<SvgPiece>>();
                LoadBake();
            }
            if (_cuts.TryGetValue(key, out var pieces)) return pieces;
            if (_baked) { Debug.LogWarning($"Raikon's baked cut has no '{key}': cutting the SVG now (re-bake: Kage > Bake Raikon Art)"); _baked = false; }
            return _cuts[key] = make();
        }

        static bool _baked;

        /// <summary>True when the last build came entirely from the bake.</summary>
        public static bool BuiltFromBake => _baked;

        /// <summary>A hash of everything a cut depends on: the SVG, the generated regions and the cutting code's version.</summary>
        public static ulong InputHash()
        {
            ulong h = 1469598103934665603UL;
            void Mix(ulong v) { h ^= v; h *= 1099511628211UL; }
            foreach (char c in SvgText()) if (c != '\r') Mix(c);      // the same on a CRLF checkout
            foreach (var (name, polygons) in RaikonRegions.Parts)
            {
                foreach (char c in name) Mix(c);
                foreach (var p in polygons) foreach (var f in p) Mix((ulong)BitConverter.SingleToInt32Bits(f));
            }
            foreach (var (name, layer, polygon) in RaikonRegions.Fills)
            {
                foreach (char c in name) Mix(c);
                Mix((ulong)layer);
                foreach (var f in polygon) Mix((ulong)BitConverter.SingleToInt32Bits(f));
            }
            Mix(CutVersion);
            return h;
        }

        static void LoadBake()
        {
            _baked = false;
            var ta = Resources.Load<TextAsset>(BakeResource);
            if (ta == null) return;
            using var r = new System.IO.BinaryReader(new System.IO.MemoryStream(ta.bytes));
            if (r.ReadUInt64() != InputHash())
            {
                Debug.LogWarning("Raikon's baked cut is stale (the SVG or the regions changed): cutting the SVG instead. Re-bake: Kage > Bake Raikon Art");
                return;
            }
            int keys = r.ReadInt32();
            for (int k = 0; k < keys; k++)
            {
                string key = r.ReadString();
                int n = r.ReadInt32();
                var list = new List<SvgPiece>(n);
                for (int i = 0; i < n; i++)
                {
                    var p = new SvgPiece { layer = r.ReadInt32() };
                    p.color = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                    p.vertices = new Vector2[r.ReadInt32()];
                    for (int v = 0; v < p.vertices.Length; v++) p.vertices[v] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    p.triangles = new int[r.ReadInt32()];
                    for (int t = 0; t < p.triangles.Length; t++) p.triangles[t] = r.ReadUInt16();
                    list.Add(p);
                }
                _cuts[key] = list;
            }
            _baked = true;
        }

        /// <summary>
        /// Cuts everything from the SVG (ignoring any bake) and returns the bake's bytes: written by the Editor to
        /// Resources/<see cref="BakeResource"/>.bytes (Kage > Bake Raikon Art).
        /// </summary>
        public static byte[] Bake()
        {
            _cuts = new Dictionary<string, List<SvgPiece>>();
            _baked = false;
            Build();
            using var ms = new System.IO.MemoryStream();
            using (var w = new System.IO.BinaryWriter(ms))
            {
                w.Write(InputHash());
                w.Write(_cuts.Count);
                foreach (var kv in _cuts)
                {
                    w.Write(kv.Key);
                    w.Write(kv.Value.Count);
                    foreach (var p in kv.Value)
                    {
                        if (p.vertices.Length > ushort.MaxValue) throw new InvalidOperationException($"{kv.Key}: a piece has too many vertices to bake");
                        w.Write(p.layer);
                        Color32 c = p.color;
                        w.Write(c.r); w.Write(c.g); w.Write(c.b); w.Write(c.a);
                        w.Write(p.vertices.Length);
                        foreach (var v in p.vertices) { w.Write(v.x); w.Write(v.y); }
                        w.Write(p.triangles.Length);
                        foreach (var t in p.triangles) w.Write((ushort)t);
                    }
                }
            }
            return ms.ToArray();
        }

        static Vector2[] Pts(float[] xy)
        {
            var r = new Vector2[xy.Length / 2];
            for (int i = 0; i < r.Length; i++) r[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return r;
        }

        public static RigDef Build()
        {
            // the shared skeleton, hurt circles and hit points: Kage's (art never moves a gameplay joint)
            var d = KageModel.Skeleton("RAIKON");
            d.scarf = false;
            d.softCoat = false;
            d.coatTail = false;
            d.rimColor = new Color32(0xc7, 0x18, 0x1f, 255);
            d.bodyColor = new Color32(0x1e, 0x12, 0x18, 255);

            // ---- where each piece goes ----
            Func<Vector2, Vector2> trunk = G;
            Func<Vector2, Vector2> Seg(Vector2 a, Vector2 b, B from, B to)
            {
                Vector2 ga = G(a), gb = G(b), pa = d.pivot[(int)from], pb = d.pivot[(int)to];
                Vector2 u = (gb - ga).normalized, w = (pb - pa).normalized;
                float s = (pb - pa).magnitude / (gb - ga).magnitude;
                Vector2 un = new(-u.y, u.x), wn = new(-w.y, w.x);
                return x =>
                {
                    Vector2 r = G(x) - ga;
                    return pa + w * (Vector2.Dot(r, u) * s) + wn * Vector2.Dot(r, un);
                };
            }
            Func<Vector2, Vector2> Attach(Vector2 at, Vector2 dir0, Vector2 dir1, B bone, B to0, B to1)
            {
                float turn = KMath.Ang(d.pivot[(int)to1] - d.pivot[(int)to0]) - KMath.Ang(G(dir1) - G(dir0));
                Vector2 ga = G(at), pa = d.pivot[(int)bone];
                return x => pa + KMath.Rot(G(x) - ga, turn);
            }
            // the tachi and the hand that grips it: the grip on the sword bone's pivot, the blade along Kage's blade
            float bladeTurn = d.bladeBindAngle - KMath.Ang(G(BladeTip) - G(Grip));
            Func<Vector2, Vector2> tachi = x => d.pivot[(int)B.Sword] + KMath.Rot(G(x) - G(Grip), bladeTurn);

            var armNU = Seg(ShoulderN, ElbowN, B.ArmNU, B.ArmNL);
            var armNL = Seg(ElbowN, WristN, B.ArmNL, B.HandN);
            var armFU = Seg(ShoulderN, ElbowN, B.ArmFU, B.ArmFL);
            var armFL = Seg(ElbowN, WristN, B.ArmFL, B.HandF);
            var fist = Attach(WristFist, ElbowFist, WristFist, B.HandN, B.ArmNL, B.HandN);
            var legNU = Seg(HipN, KneeN, B.LegNU, B.LegNL);
            var legNL = Seg(KneeN, AnkleN, B.LegNL, B.FootN);
            var footN = Attach(AnkleN, KneeN, AnkleN, B.FootN, B.LegNL, B.FootN);
            var legFU = Seg(HipN, KneeN, B.LegFU, B.LegFL);
            var legFL = Seg(KneeN, AnkleN, B.LegFL, B.FootF);
            var footF = Attach(AnkleN, KneeN, AnkleN, B.FootF, B.LegFL, B.FootF);

            // ---- parts, back to front (order x4 + sub, Kage's depth slots) ----
            Cutout(d, "Cape", "Cape", B.Chest, 0, 0, trunk, cell: 40f, soft: Chains(trunk,
                new[] { 575, 450, 595, 600, 625, 750, 660, 900, 700, 1060f },
                new[] { 605, 440, 670, 560, 730, 700, 780, 850, 815, 1000f },
                new[] { 635, 430, 730, 530, 810, 650, 850, 790, 850, 940f }),
                feel: new ChainFeel { stiffRoot = 0.22f, stiffTip = 0.035f, drag = 2.4f, gravity = 6f, breeze = 1.1f });
            Cutout(d, "Ponytail", "Ponytail", B.Head, 0, 2, trunk, cell: 36f, soft: Chains(trunk,
                new[] { 525, 112, 560, 60, 620, 25, 700, 40, 780, 80f },
                new[] { 530, 118, 610, 125, 680, 175, 730, 260, 765, 360, 775, 455f }),
                feel: new ChainFeel { stiffRoot = 0.3f, stiffTip = 0.05f, drag = 2f, gravity = 4f, breeze = 0.8f });

            // far (sword) arm, drawn from the painted near arm a shade darker, and the tachi
            Cutout(d, "ArmF_Upper", "ArmN_Upper", B.ArmFU, 1, 0, armFU, far: true, swordLayer: true);
            Cutout(d, "ArmF_Fore", "ArmN_Fore", B.ArmFL, 2, 0, armFL, far: true, swordLayer: true);
            Fill(d, "Tachi_Grip", "GripFill", B.Sword, 4, 0, tachi, swordLayer: true);
            Cutout(d, "Tachi_Hilt", "Tachi_Hilt", B.Sword, 4, 1, tachi, swordLayer: true);
            Cutout(d, "Tachi_Blade", "Tachi_Blade", B.Sword, 4, 1, tachi, swordLayer: true, glow: 1.7f);
            Cutout(d, "HandF", "GripHand", B.HandF, 4, 3, tachi, far: true, swordLayer: true);

            // far leg: the near leg a shade darker
            Fill(d, "ThighF", "ThighN", B.LegFU, 5, 0, legFU, far: true);
            Cutout(d, "ShinF", "ShinN", B.LegFL, 6, 0, legFL, far: true);
            Cutout(d, "BootF", "BootN", B.FootF, 7, 0, footF, far: true);

            // trunk
            Fill(d, "HipsFill", "HipsFill", B.Hips, 8, 0, trunk);
            Fill(d, "TorsoFill", "TorsoFill", B.Chest, 10, 0, trunk);
            Cutout(d, "Torso", "Torso", B.Chest, 11, 0, trunk, cell: 64f);

            // near leg (the painted one, with the buckle)
            Fill(d, "ThighN", "ThighN", B.LegNU, 13, 0, legNU);
            Cutout(d, "ShinN", "ShinN", B.LegNL, 14, 0, legNL);
            Cutout(d, "BootN", "BootN", B.FootN, 15, 0, footN);

            Fill(d, "Skirt_UnderTachi", "SkirtUnderBlade", B.Hips, 16, 0, trunk, outline: false);
            Cutout(d, "Skirt", "Skirt", B.Hips, 16, 1, trunk, cell: 64f);
            Cutout(d, "Rope", "Rope", B.Chest, 18, 0, trunk);

            // head
            Cutout(d, "Head", "Head", B.Head, 21, 0, trunk);
            Cutout(d, "Eye", "Eye", B.Head, 21, 1, trunk, glow: 2.6f, role: PartRole.Eye);
            Cutout(d, "Fur", "Fur", B.Chest, 22, 0, trunk, cell: 32f, soft: Chains(trunk,
                new[] { 490, 320, 460, 280, 445, 240f },
                new[] { 520, 320, 545, 275, 565, 238f },
                new[] { 545, 330, 610, 300, 660, 295f },
                new[] { 560, 360, 640, 365, 700, 380f },
                new[] { 570, 400, 640, 430, 690, 460f },
                new[] { 480, 330, 430, 315, 395, 300f }),
                feel: new ChainFeel { stiffRoot = 0.5f, stiffTip = 0.16f, drag = 2.5f, gravity = 1.5f, breeze = 0.5f });
            Cutout(d, "Hat", "Hat", B.Head, 24, 0, trunk, cell: 64f);
            var tassel = new ChainFeel { stiffRoot = 0.18f, stiffTip = 0.03f, drag = 1.6f, gravity = 9f, breeze = 0.5f };
            Cutout(d, "Tassel1", "Tassel1", B.Head, 24, 1, trunk, cell: 16f, soft: Chains(trunk, new[] { 293, 246, 294, 290, 296, 345f }), feel: tassel);
            Cutout(d, "Tassel2", "Tassel2", B.Head, 24, 1, trunk, cell: 16f, soft: Chains(trunk, new[] { 365, 240, 367, 290, 369, 345f }), feel: tassel);
            Cutout(d, "Tassel3", "Tassel3", B.Head, 24, 1, trunk, cell: 16f, soft: Chains(trunk, new[] { 627, 190, 629, 228, 631, 266f }), feel: tassel);

            // near arm: the shoulder plate, sleeve and gauntlet as painted, and the painted fist
            Cutout(d, "ArmN_Upper", "ArmN_Upper", B.ArmNU, 26, 0, armNU);
            Cutout(d, "SodeN", "SodeN", B.ArmNU, 26, 1, armNU);
            Cutout(d, "ArmN_Fore", "ArmN_Fore", B.ArmNL, 27, 0, armNL);
            Cutout(d, "HandN", "Fist", B.HandN, 28, 0, fist);
            return d;
        }

        static List<Vector2[]> Chains(Func<Vector2, Vector2> map, params float[][] xy)
        {
            var r = new List<Vector2[]>();
            foreach (var c in xy)
            {
                var pts = Pts(c);
                for (int i = 0; i < pts.Length; i++) pts[i] = map(pts[i]);
                r.Add(pts);
            }
            return r;
        }

        /// <summary>A part cut from the painting: every colour inside the region, mapped onto the skeleton.</summary>
        static void Cutout(RigDef d, string name, string region, B bone, int order, int sub, Func<Vector2, Vector2> map,
                        float cell = 0f, List<Vector2[]> soft = null, ChainFeel feel = default, bool far = false,
                        bool swordLayer = false, float glow = 1f, PartRole role = PartRole.Body)
        {
            d.parts.Add(new PartDef
            {
                name = name, bone = bone, order = order * 4 + sub, role = role, mesh = Mesh(name, Cut(region, cell), map, far), rim = false, glow = glow,
                swordLayer = swordLayer, soft = soft != null ? SoftKind.Chain : SoftKind.None, chains = soft, chainFeel = feel,
            });
        }

        /// <summary>One indexed mesh in bind space, the pieces painted in order.</summary>
        static Mesh Mesh(string name, List<SvgPiece> pieces, Func<Vector2, Vector2> map, bool far)
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            foreach (var piece in pieces)
            {
                Color col = MeshKit.L(far ? Far(piece.color) : piece.color);
                int b = verts.Count;
                foreach (var v in piece.vertices) { verts.Add(map(v)); cols.Add(col); }
                foreach (var t in piece.triangles) tris.Add(b + t);
            }
            var mesh = new Mesh { name = name, indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// A fill in the painting's colours: a flat underlay (outlined in its ink) for what the painting hides behind a
        /// moving part, or a patch painted back where the painting's tachi covers the skirt.
        /// </summary>
        static void Fill(RigDef d, string name, string region, B bone, int order, int sub, Func<Vector2, Vector2> map,
                         bool far = false, bool swordLayer = false, bool outline = true)
        {
            var pieces = Cached("fill:" + region + (outline ? ":outlined" : ""), () =>
            {
                var list = new List<SvgPiece>();
                foreach (var f in RaikonRegions.Fills)
                {
                    if (f.name != region) continue;
                    var pts = Pts(f.polygon);
                    if (outline) list.Add(SvgArt.Tessellate(new[] { Grow(pts, 2f) }, Ink));
                    list.Add(SvgArt.Tessellate(new[] { pts }, Art.Layers[f.layer]));
                }
                if (list.Count == 0) throw new ArgumentException("no Raikon fill " + region);
                return list;
            });
            d.parts.Add(new PartDef { name = name, bone = bone, order = order * 4 + sub, role = PartRole.Body, mesh = Mesh(name, pieces, map, far), rim = false, swordLayer = swordLayer });
        }

        /// <summary>A polygon grown outward by <paramref name="px"/> (SVG px, which has y down).</summary>
        static Vector2[] Grow(Vector2[] pts, float px) => MeshKit.Grow(pts, px);

        /// <summary>The far limbs: one step darker along the trace's palette (the reds stay red, a little deeper).</summary>
        static Color Far(Color c)
        {
            // palette steps from light to dark, by the trace's own colours
            Color32 k = c;
            string hex = $"{k.r:x2}{k.g:x2}{k.b:x2}";
            switch (hex)
            {
                case "f9eeef": return new Color32(0xd2, 0xb5, 0xb7, 255);
                case "d2b5b7": return new Color32(0x8a, 0x6c, 0x73, 255);
                case "8a6c73": return new Color32(0x39, 0x22, 0x2a, 255);
                case "39222a": return new Color32(0x1e, 0x12, 0x18, 255);
                case "1e1218": return new Color32(0x14, 0x0b, 0x10, 255);
                case "c7181f": return new Color32(0x9a, 0x12, 0x18, 255);
                default: return c;
            }
        }
    }
}
