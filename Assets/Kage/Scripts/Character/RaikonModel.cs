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
        static readonly Vector2 Grip = new(540, 656), BladeTip = new(22, 1003);                 // tachi: grip in the hand, tip
        static readonly Vector2 HipN = new(505, 800), KneeN = new(518, 1000), AnkleN = new(520, 1118);

        // ---- palette of the trace ----
        static readonly Color Ink = new Color32(1, 0, 0, 255);

        static SvgArt _art;
        static Dictionary<string, List<SvgPiece>> _cuts;

        /// <summary>The owner's SVG, read once.</summary>
        public static SvgArt Art
        {
            get
            {
                if (_art != null) return _art;
                var ta = Resources.Load<TextAsset>(SvgResource);
                if (ta == null) throw new InvalidOperationException("Raikon's SVG is missing: Resources/" + SvgResource + ".bytes");
                _art = SvgArt.Parse(ta.text);
                _art.backgroundLayer = 0;        // #010000: the canvas and the figure's ink
                return _art;
            }
        }

        static List<SvgPiece> Cut(string region, float cell)
        {
            _cuts ??= new Dictionary<string, List<SvgPiece>>();
            string key = region + "@" + cell;
            if (_cuts.TryGetValue(key, out var pieces)) return pieces;
            float[][] polys = null;
            foreach (var r in RaikonRegions.Parts) if (r.name == region) { polys = r.polygons; break; }
            if (polys == null) throw new ArgumentException("no Raikon region " + region);
            var list = new List<Vector2[]>();
            foreach (var xy in polys) list.Add(Pts(xy));
            var art = Art;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var cut = _cuts[key] = art.Cut(list, cell);
            Debug.Log($"[SAN-71 timing] {key}: {sw.ElapsedMilliseconds} ms");
            return cut;
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
            var d = KageModel.Build(KageModel.Look.Kage);
            d.name = "RAIKON";
            d.parts.Clear();
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
            Cutout(d, "Torso", "Torso", B.Chest, 11, 0, trunk);

            // near leg (the painted one, with the buckle)
            Fill(d, "ThighN", "ThighN", B.LegNU, 13, 0, legNU);
            Cutout(d, "ShinN", "ShinN", B.LegNL, 14, 0, legNL);
            Cutout(d, "BootN", "BootN", B.FootN, 15, 0, footN);

            Cutout(d, "Skirt", "Skirt", B.Hips, 16, 0, trunk);
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
            Cutout(d, "Hat", "Hat", B.Head, 24, 0, trunk);
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
            // one indexed mesh in bind space, the colours painted in the trace's order
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            foreach (var piece in Cut(region, cell))
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
            d.parts.Add(new PartDef
            {
                name = name, bone = bone, order = order * 4 + sub, role = role, mesh = mesh, rim = false, glow = glow,
                swordLayer = swordLayer, soft = soft != null ? SoftKind.Chain : SoftKind.None, chains = soft, chainFeel = feel,
            });
        }

        /// <summary>A flat fill in the painting's colours (outlined in its ink): what the painting hides behind a moving part.</summary>
        static void Fill(RigDef d, string name, string region, B bone, int order, int sub, Func<Vector2, Vector2> map,
                         bool far = false, bool swordLayer = false)
        {
            foreach (var f in RaikonRegions.Fills)
            {
                if (f.name != region) continue;
                var pts = Map(Pts(f.polygon), map);
                Color col = Art.Layers[f.layer];
                if (far) col = Far(col);
                var polys = new List<(Vector2[] pts, Color col)> { (MeshKit.Grow(pts, 0.004f), Ink), (pts, col) };
                d.parts.Add(new PartDef { name = name, bone = bone, order = order * 4 + sub, role = PartRole.Body, polys = polys, rim = false, swordLayer = swordLayer });
                return;
            }
            throw new ArgumentException("no Raikon fill " + region);
        }

        static Vector2[] Map(Vector2[] pts, Func<Vector2, Vector2> map)
        {
            var r = new Vector2[pts.Length];
            for (int i = 0; i < r.Length; i++) r[i] = map(pts[i]);
            return r;
        }

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
