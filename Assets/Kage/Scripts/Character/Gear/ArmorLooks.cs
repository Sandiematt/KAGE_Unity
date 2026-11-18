using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    enum HemKind { Rags, Shards, Flames, Tatters }
    enum MotifKind { Rope, Tomoe, Wards, Lightning, Flames, VioletFire, Spirit }
    enum ChestKind { Sash, Strap, Cords, Plate, Lamellar, Emblem, Mantle }
    enum SodeKind { Rags, Plate, Silver, Guard, Mantle }

    /// <summary>
    /// One shop armour's cut of Kage's coat, scarf and belt (JS <c>KAGE_ARMOR</c>, <c>armory/_themes.js</c>): its tones, how the coat's
    /// hem is cut and the art along it, the chest piece and shoulder guard, extra scarf strands, a halo. Colours are the JS tones.
    /// </summary>
    sealed class ArmorTheme
    {
        public Color rim, red, redLo, scarf, scarfLo, scarfHi, belt, coat, coatLo, acc, tooth;
        public bool fringe = true;                  // the cut hem gets a line in the accent
        public HemKind hem;
        public int teeth;
        public MotifKind motif;
        public int strands;
        public float strandW;                       // JS px; one JS px of strand width is 0.0145 units of ribbon
        public Color? streamerFront, streamerBack;  // the strands' own colours (Shinsen's white streamers)
        public ChestKind chest;
        public SodeKind sode;
        public bool halo;
    }

    /// <summary>
    /// The nine armour drawings (SAN-85), authored in reference-image pixels against Kage's body (see <see cref="ArmorArt"/>). The
    /// JS cel shading is flattened to one tone per shape with a thin rim light; glow, the baked Godot flame and spirit sheets and
    /// the JS hairstyles per armour are not drawn (see the SAN-85 notes in ADR-0005).
    /// </summary>
    static class ArmorLooks
    {
        static Color C(string hex) => KMath.Hex(hex);

        // ---------------------------------------------------------------- the seven shop armours (JS tones)

        public static readonly ArmorTheme Ronin = new()
        {
            rim = C("#d2683f"), red = C("#8c3b22"), redLo = C("#4c2116"), scarf = C("#7e3624"), scarfLo = C("#4e2117"), scarfHi = C("#d0683f"),
            belt = C("#9a7452"), coat = C("#1c1311"), coatLo = C("#110b0a"), acc = C("#d2683f"), tooth = C("#4c2116"),
            hem = HemKind.Rags, teeth = 7, motif = MotifKind.Rope, strands = 4, strandW = 3.6f, chest = ChestKind.Sash, sode = SodeKind.Rags,
        };

        public static readonly ArmorTheme Yami = new()
        {
            rim = C("#b65cff"), red = C("#a447ff"), redLo = C("#3a1660"), scarf = C("#5e24a0"), scarfLo = C("#371563"), scarfHi = C("#c58cff"),
            belt = C("#7a3cc0"), coat = C("#120c17"), coatLo = C("#09070d"), acc = C("#b65cff"), tooth = C("#3a1660"),
            hem = HemKind.Shards, teeth = 5, motif = MotifKind.Tomoe, strands = 3, strandW = 3.2f, chest = ChestKind.Strap, sode = SodeKind.Plate,
        };

        public static readonly ArmorTheme Shinsen = new()
        {
            rim = C("#ff5a5a"), red = C("#c8202c"), redLo = C("#7a1218"), scarf = C("#c8202c"), scarfLo = C("#7a1218"), scarfHi = C("#ff6464"),
            belt = C("#d0303a"), coat = C("#ebe6ee"), coatLo = C("#b8b0c2"), acc = C("#ece8f0"), tooth = C("#ebe6ee"),
            hem = HemKind.Shards, teeth = 4, motif = MotifKind.Wards, strands = 2, strandW = 3.4f,
            streamerFront = C("#ece8f0"), streamerBack = C("#aea6b8"), chest = ChestKind.Cords, sode = SodeKind.Silver,
        };

        public static readonly ArmorTheme Raiko = new()
        {
            rim = C("#ffc23a"), red = C("#ffcf4a"), redLo = C("#5e3e0a"), scarf = C("#b07a14"), scarfLo = C("#5e3e0a"), scarfHi = C("#ffe08a"),
            belt = C("#c8961e"), coat = C("#140f09"), coatLo = C("#0b0805"), acc = C("#ffc23a"), tooth = C("#5e3e0a"),
            hem = HemKind.Shards, teeth = 5, motif = MotifKind.Lightning, strands = 3, strandW = 3.2f, chest = ChestKind.Plate, sode = SodeKind.Guard,
        };

        public static readonly ArmorTheme Kagehira = new()
        {
            rim = C("#ff3c3c"), red = C("#e8222c"), redLo = C("#6a0c12"), scarf = C("#d01c26"), scarfLo = C("#7e101a"), scarfHi = C("#ff6a5a"),
            belt = C("#c0202c"), coat = C("#160808"), coatLo = C("#0c0505"), acc = C("#ff3c3c"), tooth = C("#e8222c"), fringe = false,
            hem = HemKind.Flames, teeth = 5, motif = MotifKind.Flames, strands = 2, strandW = 3.6f, chest = ChestKind.Lamellar, sode = SodeKind.Guard,
        };

        public static readonly ArmorTheme Yurei = new()
        {
            rim = C("#e4dcff"), red = C("#4f86d8"), redLo = C("#2c4a86"), scarf = C("#d4daf0"), scarfLo = C("#8e98bc"), scarfHi = C("#ffffff"),
            belt = C("#5a6284"), coat = C("#d6daee"), coatLo = C("#9aa2c4"), acc = C("#8fc2ff"), tooth = C("#d6daee"),
            hem = HemKind.Tatters, teeth = 6, motif = MotifKind.Spirit, strands = 4, strandW = 3.4f, chest = ChestKind.Mantle, sode = SodeKind.Mantle,
        };

        public static readonly ArmorTheme Mugen = new()
        {
            rim = C("#d474ff"), red = C("#c04bff"), redLo = C("#4a1470"), scarf = C("#7428b4"), scarfLo = C("#43166c"), scarfHi = C("#e2a4ff"),
            belt = C("#f060ff"), coat = C("#100a16"), coatLo = C("#08060c"), acc = C("#d474ff"), tooth = C("#c04bff"), fringe = false,
            hem = HemKind.Flames, teeth = 6, motif = MotifKind.VioletFire, strands = 4, strandW = 3.6f, chest = ChestKind.Emblem, sode = SodeKind.Plate, halo = true,
        };

        // ---------------------------------------------------------------- recolouring

        /// <summary>Retones everything of Kage that an armour recuts: trim, belt, coat cloth, rim light, scarf. Nothing moves.</summary>
        public static void Retone(RigDef d, ArmorTheme t, Color trim, Color trimDeep)
        {
            Color rimFrom = d.rimColor;
            // the coat cloth, as KageModel.BuildCoat paints it
            bool pale = t.coat.grayscale > 0.5f;
            KageModel.Recolour(d, Color.Lerp(Palette.TrimDeep, Palette.Trim, 0.55f), Color.Lerp(t.redLo, t.red, 0.55f));     // the cloud motif on the coat
            KageModel.Recolour(d, rimFrom.Mul(0.75f), t.rim.Mul(0.75f));
            KageModel.Recolour(d, rimFrom, t.rim);
            KageModel.Recolour(d, Palette.TrimBright, t.scarfHi);
            KageModel.Recolour(d, trim, t.red);
            KageModel.Recolour(d, trimDeep, t.redLo);
            KageModel.Recolour(d, C("#B8323A"), t.belt);
            KageModel.Recolour(d, C("#1A161B"), t.coat);
            KageModel.Recolour(d, C("#151116"), Color.Lerp(t.coat, t.coatLo, 0.4f));
            KageModel.Recolour(d, C("#120A0C"), t.coatLo);
            KageModel.Recolour(d, C("#2B242A"), pale ? Color.Lerp(t.coat, t.coatLo, 0.45f) : Color.Lerp(t.coat, Color.white, 0.12f));
            KageModel.Recolour(d, C("#0C090B"), t.coatLo.Mul(0.7f));

            // the scarf: its wrap, fold and knot and the two tails
            foreach (var p in d.parts)
            {
                if (p.name == "Scarf_Wrap" || p.name == "Scarf_Knot") p.color = t.scarf;
                else if (p.name == "Scarf_Fold" || p.name == "Scarf_KnotShade") p.color = t.scarfLo;
            }
            d.scarfColor = t.scarf;
            d.scarfBack = t.scarfLo;
            d.scarfEdge = t.scarfHi;
        }

        // ---------------------------------------------------------------- dressing a shop armour

        const int ChestOrder = 52;      // over Kage's torso and lapel (44, 48), under his belt (72) and scarf (100)
        const int SodeOrder = 107;      // over his near upper arm and cuff (104..106), under the forearm (108)
        const int HaloOrder = 82;       // behind his head (hair 84), over his neck (80)
        const float Q = 1.9f;           // reference pixels per JS hem pixel

        public static void Dress(ArmorArt a, ArmorTheme t)
        {
            var side = new HemLine(928, 740, 964, 744, 1010, 738, 1056, 726);
            var back = new HemLine(906, 744, 950, 750, 1000, 746, 1046, 734, 1082, 726);
            Teeth(a, "Coat_Back", back, t, 11f, t.coatLo.Mul(0.8f), false, false);
            Teeth(a, "Coat_Side", side, t, 0f, t.tooth, t.fringe, true);
            Motif(a, t, side);
            Chest(a, t);
            Sode(a, t);
            Strands(a.Rig, t);
            if (t.halo) Halo(a, t);
        }

        // ---------------------------------------------------------------- the hem

        sealed class HemLine
        {
            readonly Vector2[] _p;
            static readonly Vector2 Out = new Vector2(0.28f, 1f).normalized;
            public HemLine(params float[] xy)
            {
                _p = new Vector2[xy.Length / 2];
                for (int i = 0; i < _p.Length; i++) _p[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            }
            public Vector2 H(float h)
            {
                float s = Mathf.Clamp01(h) * (_p.Length - 1);
                int i = Mathf.Min((int)s, _p.Length - 2);
                return Vector2.Lerp(_p[i], _p[i + 1], s - i);
            }
            /// <summary>The point d pixels out past the hem at h (negative: up and in from it), never below the ground.</summary>
            public Vector2 At(float h, float d)
            {
                var v = H(h) + Out * d;
                v.y = Mathf.Min(v.y, 808f);
                return v;
            }
        }

        // JS _kaHash: a steady pseudo-random number in [0, 1)
        static float Hash(float x)
        {
            double v = System.Math.Sin(x * 127.1 + 311.7) * 43758.5453;
            return (float)(v - System.Math.Floor(v));
        }

        /// <summary>
        /// The cut hem (JS kageArmorHemPts): teeth of uneven width, growing toward the far tip, in the theme's kind. Rags: torn strips,
        /// some split. Shards: long blades, long and short in turn. Flames: tongues that hook toward the tip. Tatters: long ribbons.
        /// </summary>
        static void Teeth(ArmorArt a, string part, HemLine hem, ArmorTheme t, float seed, Color fill, bool fringe, bool rim)
        {
            int n = t.teeth;
            var cuts = new float[n + 1];
            float sum = 0f;
            for (int i = 0; i < n; i++) { sum += 0.7f + 0.6f * Hash(i * 5.7f + n * 1.3f + seed); cuts[i + 1] = sum; }
            for (int i = 0; i <= n; i++) cuts[i] /= sum;
            for (int i = 0; i < n; i++)
            {
                float h0 = cuts[i], w = cuts[i + 1] - h0, r = Hash(i * 7.31f + n + seed), r2 = Hash(i * 3.17f + 9.4f + seed);
                float grow = 0.4f + 1.2f * Mathf.Pow((i + 0.5f) / n, 1.3f);
                var o = new List<Vector2>();
                Vector2 At(float h, float d) => hem.At(h, d);
                switch (t.hem)
                {
                    case HemKind.Rags:
                    {
                        float L = (12f + 18f * r) * grow * Q, sw = Mathf.Sin(i * 1.9f) * 0.12f * w;
                        if (r2 > 0.5f) { o.Add(At(h0 + w * 0.2f, L * 0.3f)); o.Add(At(h0 + w * 0.62f + sw, L)); o.Add(At(h0 + w * 0.7f + sw, L * 0.6f)); o.Add(At(h0 + w * 0.92f + sw, L * 0.88f)); o.Add(At(h0 + w * 0.9f, L * 0.2f)); }
                        else { o.Add(At(h0 + w * 0.24f, L * 0.26f)); o.Add(At(h0 + w * (0.8f + 0.2f * r2) + sw, L)); o.Add(At(h0 + w * (0.92f + 0.1f * r) + sw, L * 0.74f)); o.Add(At(h0 + w * 0.9f, L * 0.2f)); }
                        break;
                    }
                    case HemKind.Shards:
                    {
                        float L = (i % 2 == 1 ? 14f : 26f) * (0.8f + 0.4f * r) * grow * Q;
                        o.Add(At(h0 + w * 0.1f, L * 0.14f)); o.Add(At(h0 + w * (1.05f + 0.3f * r2), L)); o.Add(At(h0 + w * 0.92f, L * 0.22f));
                        break;
                    }
                    case HemKind.Flames:
                    {
                        float L = (16f + 12f * r) * grow * Q * (1f + 0.14f * Mathf.Sin(i * 2.3f)), cu = 0.12f * Mathf.Sin(i * 1.3f);
                        o.Add(At(h0 + w * 0.14f, L * 0.32f)); o.Add(At(h0 + w * 0.46f, L * 0.7f)); o.Add(At(h0 + w * (1f + cu), L)); o.Add(At(h0 + w * 0.72f, L * 0.56f)); o.Add(At(h0 + w * 0.94f, L * 0.26f));
                        break;
                    }
                    default:
                    {
                        float L = (18f + 20f * r) * grow * Q, ph = i * 1.6f;
                        float Wv(float k) => Mathf.Sin(ph - k * 3.2f) * 0.22f * w * k;
                        foreach (float k in new[] { 0.3f, 0.55f, 0.8f }) o.Add(At(h0 + w * (0.22f + 0.36f * k) + Wv(k), L * k));
                        o.Add(At(h0 + w * 0.62f + Wv(1f), L)); o.Add(At(h0 + w * 0.76f + Wv(0.95f), L * 0.94f));
                        foreach (float k in new[] { 0.7f, 0.42f }) o.Add(At(h0 + w * (0.62f + 0.3f * k) + Wv(k), L * k * 0.9f));
                        o.Add(At(h0 + w * 0.92f, L * 0.18f));
                        break;
                    }
                }
                var poly = new List<Vector2> { At(h0, -7f) };
                poly.AddRange(o);
                poly.Add(At(h0 + w, -7f));
                // the tooth goes under the coat (its rim and fill overlap the root), so it reads as cut cloth
                a.CoatFill(part, fill, rim, poly, atFront: true);
                if (fringe)
                {
                    var line = new List<Vector2> { At(h0, 0f) };
                    line.AddRange(o);
                    line.Add(At(h0 + w, 0f));
                    a.CoatLine(part, t.red, 1.8f, line);
                }
            }
        }

        // ---------------------------------------------------------------- the hem art

        static List<Vector2> Ellipse(Vector2 c, float rx, float ry, int n = 10)
        {
            var o = new List<Vector2>(n);
            for (int i = 0; i < n; i++) { float k = i / (float)n * Mathf.PI * 2f; o.Add(new Vector2(c.x + Mathf.Cos(k) * rx, c.y + Mathf.Sin(k) * ry)); }
            return o;
        }

        static List<Vector2> Spiral(Vector2 c, float r, float turns, int n = 16)
        {
            var o = new List<Vector2>(n + 1);
            for (int i = 0; i <= n; i++)
            {
                float k = i / (float)n, ang = k * turns * Mathf.PI * 2f + 0.6f, rr = r * (1f - 0.78f * k);
                o.Add(new Vector2(c.x + Mathf.Cos(ang) * rr, c.y + Mathf.Sin(ang) * rr));
            }
            return o;
        }

        static void Tomoe(ArmorArt a, string part, Vector2 c, float R, Color col)
        {
            a.CoatFill(part, col, false, ArmorArt.Circle(c.x, c.y, R * 0.5f, 10));
            a.CoatFill(part, col, false, ArmorArt.Strip(ArmorArt.Quad(c.x - R * 0.42f, c.y - R * 0.3f, c.x - R * 1.1f, c.y + R * 0.9f, c.x + R * 0.5f, c.y + R * 1.0f, 8), R * 0.5f, R * 0.04f));
        }

        static void Motif(ArmorArt a, ArmorTheme t, HemLine H)
        {
            const string S = "Coat_Side";
            float q = Q;
            switch (t.motif)
            {
                case MotifKind.Rope:
                {
                    // a twisted rope stitched across the tail above the hem band, scorched patches, tears running up from the notches
                    var ra = new List<Vector2>();
                    var rb = new List<Vector2>();
                    for (int i = 0; i <= 18; i++) { float h = 0.04f + i / 18f * 0.86f; ra.Add(H.At(h, -11f * q)); rb.Add(H.At(h, -15.5f * q)); }
                    a.CoatLine(S, t.belt, 1.6f, ra);
                    a.CoatLine(S, t.belt, 1.6f, rb);
                    for (int i = 0; i < 18; i++) a.CoatLine(S, t.belt, 1.3f, new List<Vector2> { ra[i], rb[i + 1] });
                    foreach (var (h, d, r) in new[] { (0.22f, 6f, 3.2f), (0.47f, 5f, 2.6f), (0.66f, 7f, 3.6f) })
                        a.CoatFill(S, t.redLo, false, Ellipse(H.At(h, -d * q - 20f), r * 1.6f * q, r * q));
                    foreach (float h in new[] { 0.33f, 0.55f, 0.78f })
                        a.CoatLine(S, t.coatLo, 1.5f, new List<Vector2> { H.At(h, -26f), H.At(h + 0.02f, -9f * q - 26f), H.At(h - 0.01f, -17f * q - 26f) });
                    break;
                }
                case MotifKind.Tomoe:
                {
                    // a tomoe and a smaller one near the tip, with a sweeping line under them
                    Tomoe(a, S, H.At(0.44f, -14f * q - 20f), 9.5f * q, t.red);
                    Tomoe(a, S, H.At(0.74f, -10f * q - 18f), 5.6f * q, t.red);
                    a.CoatLine(S, t.red, 2.2f, new List<Vector2> { H.At(0.3f, -26f * q), H.At(0.45f, -33f * q), H.At(0.62f, -30f * q), H.At(0.72f, -22f * q) });
                    break;
                }
                case MotifKind.Wards:
                {
                    // red ink wards on the white silk: thorned flames rising from the hem, splashes above them, a red tip
                    foreach (var (h, R) in new[] { (0.18f, 1f), (0.42f, 1.25f), (0.66f, 1.05f) })
                    {
                        var sp = new[] { (-0.07f, 0f), (-0.05f, 14f), (-0.03f, 6f), (0f, 24f), (0.02f, 8f), (0.05f, 17f), (0.06f, 4f), (0.075f, 0f) };
                        var poly = new List<Vector2>();
                        foreach (var (dh, d) in sp) poly.Add(H.At(h + dh * R, -(4f + d * R) * q - 14f));
                        a.CoatFill(S, t.red, false, poly);
                    }
                    foreach (var (h, d, r) in new[] { (0.3f, 30f, 1.6f), (0.55f, 36f, 1.2f), (0.36f, 40f, 0.9f), (0.8f, 26f, 1.4f) })
                    {
                        var c = H.At(h, -d * q);
                        a.CoatFill(S, t.redLo, false, ArmorArt.Circle(c.x, c.y, r * q, 8));
                    }
                    foreach (var (h, d) in new[] { (0.27f, 28f), (0.52f, 33f) })
                        a.CoatLine(S, t.red, 2.6f, new List<Vector2> { H.At(h, -d * q), H.At(h + 0.03f, -(d + 7f) * q), H.At(h + 0.07f, -(d + 6f) * q) });
                    break;
                }
                case MotifKind.Lightning:
                {
                    // bolts branching up from the hem
                    for (int b = 0; b < 4; b++)
                    {
                        float h0 = 0.12f + b * 0.24f, len = 22f + 18f * Hash(b * 5.1f), h = h0;
                        var pts = new List<Vector2> { H.At(h0, -12f) };
                        for (int k = 1; k <= 6; k++) { h += (Hash(b * 13f + k) - 0.5f) * 0.08f; pts.Add(H.At(h, -len * k / 6f * q - 12f)); }
                        a.CoatLine(S, t.red, 2.6f, pts);
                        a.CoatLine(S, t.red, 1.7f, new List<Vector2> { pts[3], H.At(h + 0.06f, -len * 0.62f * q - 12f), H.At(h + 0.1f, -len * 0.82f * q - 12f) });
                        a.CoatLine(S, C("#fff4c8"), 0.9f, pts);
                    }
                    break;
                }
                case MotifKind.Flames:
                case MotifKind.VioletFire:
                {
                    // the lower tail burning: tongues of colour rising from the hem, cut through with black flames; the Mugen's is violet and sends thin energy veins up the coat
                    bool vi = t.motif == MotifKind.VioletFire;
                    float Tip(float h) { float k = Mathf.Clamp01((h - 0.7f) / 0.3f); return 16f * k * k * (3f - 2f * k); }
                    float Fl(float h)
                    {
                        float d = 6f + Tip(h) * 0.6f;
                        for (int j = 0; j < 5; j++)
                        {
                            float c = 0.1f + j * 0.2f + 0.03f * Mathf.Sin(j), k = 1f - Mathf.Abs(h - c) / 0.1f;
                            if (k > 0f) d = Mathf.Max(d, 6f + (13f + 7f * Hash(j + 2f)) * Mathf.Pow(k, 1.6f));
                        }
                        return d;
                    }
                    var top = new List<Vector2>();
                    var bottom = new List<Vector2>();
                    for (int i = 0; i <= 28; i++) { float h = i / 28f; top.Add(H.At(h, -Fl(h) * q)); bottom.Add(H.At(h, 2f)); }
                    bottom.Reverse();
                    top.AddRange(bottom);
                    a.CoatFill(S, t.red, false, top);
                    Color cut = Color.Lerp(t.coat, t.coatLo, 0.4f);
                    foreach (var (h, d, L) in new[] { (0.26f, 3f, 8f), (0.5f, 4f, 10f), (0.73f, 3f, 8f) })
                        a.CoatFill(S, cut, false, new List<Vector2> { H.At(h - 0.035f, -d * q), H.At(h - 0.02f, -(d + L * 0.7f) * q), H.At(h + 0.05f, -(d + L) * q), H.At(h + 0.03f, -d * q) });
                    var core = new List<Vector2>();
                    for (int i = 0; i <= 28; i++) { float h = i / 28f; core.Add(H.At(h, -(Fl(h) - 1.2f) * q)); }
                    a.CoatLine(S, vi ? t.scarfHi : C("#ff8a6a"), 1.4f, core);
                    if (vi)
                    {
                        foreach (var (h, L) in new[] { (0.2f, 34f), (0.46f, 42f), (0.7f, 30f) })
                        {
                            var pts = new List<Vector2>();
                            for (int k = 0; k <= 8; k++) pts.Add(H.At(h + 0.04f * Mathf.Sin(k * 1.3f + h * 9f), -(Fl(h) + L * k / 8f) * q));
                            a.CoatLine(S, t.red, 1.4f, pts);
                        }
                    }
                    else
                    {
                        foreach (var (h, d, r) in new[] { (0.32f, 26f, 1.4f), (0.58f, 31f, 1.1f), (0.62f, 24f, 0.8f), (0.85f, 22f, 1.2f) })
                        {
                            var c = H.At(h, -d * q);
                            a.CoatFill(S, t.red, false, ArmorArt.Circle(c.x, c.y, r * q, 8));
                        }
                    }
                    break;
                }
                case MotifKind.Spirit:
                {
                    // blue spirit clouds on the pale silk: scrolls along the hem and smaller ones drifting higher, and two lines of spirit script up the tail
                    foreach (var (h, R, d) in new[] { (0.14f, 5.6f, 4f), (0.34f, 7f, 4f), (0.56f, 6.4f, 4f), (0.76f, 5f, 4f), (0.28f, 3.6f, 22f), (0.62f, 4f, 26f) })
                    {
                        var c = H.At(h, -(d + R * 1.3f) * q);
                        a.CoatLine(S, t.red, 2.6f, Spiral(c, R * q * 1.1f, 1.6f));
                    }
                    foreach (float h in new[] { 0.45f, 0.85f })
                    {
                        var pts = new List<Vector2>();
                        for (int k = 0; k <= 10; k++) pts.Add(H.At(h + 0.025f * Mathf.Sin(k * 1.4f), -(14f + k * 3.2f) * q));
                        a.CoatLine(S, t.redLo, 1.5f, pts);
                    }
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- the scarf's extra strands

        static void Strands(RigDef d, ArmorTheme t)
        {
            for (int k = 0; k < t.strands; k++)
            {
                bool second = k % 2 == 1;
                Vector2 a = second ? d.scarfAnchorB : d.scarfAnchorA;
                d.scarfStrands.Add(new ScarfStrand
                {
                    anchor = a + new Vector2(0.004f * k, -0.006f * k),
                    length = Mathf.Max(0.3f, 0.5f - 0.045f * (k >> 1) - 0.02f * k),
                    width = t.strandW * 0.0145f,
                    phase = 0.9f + k * 1.3f,
                    front = t.streamerFront ?? t.scarf,
                    back = t.streamerBack ?? t.scarfLo,
                });
            }
        }

        // ---------------------------------------------------------------- the halo

        /// <summary>The Mugen Armor's crescent behind his head (the JS code-drawn stand-in for the card art), thick behind him and thin in front.</summary>
        static void Halo(ArmorArt a, ArmorTheme t)
        {
            Vector2 c = new(922f, 138f);
            var o = new List<Vector2>();
            for (int i = 0; i <= 20; i++) { float ang = Mathf.Lerp(135f, 415f, i / 20f) * Mathf.Deg2Rad; o.Add(c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 82f); }
            Vector2 ci = c + new Vector2(-16f, 8f);
            for (int i = 20; i >= 0; i--) { float ang = Mathf.Lerp(135f, 415f, i / 20f) * Mathf.Deg2Rad; o.Add(ci + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 66f); }
            a.Fill(B.Head, HaloOrder, t.red, false, o, 1.5f);
        }

        // ---------------------------------------------------------------- the chest piece

        static void Chest(ArmorArt a, ArmorTheme t)
        {
            const B bone = B.Chest;
            const int O = ChestOrder;
            // the plate on the upper chest the gold, crimson and violet armours share
            float[] plate = { 818, 262, 811, 300, 815, 338, 842, 350, 878, 346, 892, 316, 888, 276, 868, 256 };
            switch (t.chest)
            {
                case ChestKind.Sash:
                    // Ronin: a rust rag slung from the back of the shoulder to the front of the hip, torn along its edges
                    a.Fill(bone, O, t.scarfLo, true, 880, 256, 894, 262, 872, 306, 846, 346, 826, 372, 812, 364, 820, 346, 812, 340, 832, 316, 842, 296, 860, 280);
                    a.Line(bone, O, t.scarf, 3.2f, 886, 262, 858, 298, 826, 350);
                    a.Line(bone, O, t.coatLo, 1.6f, 856, 296, 862, 306);
                    a.Line(bone, O, t.coatLo, 1.6f, 836, 326, 842, 336);
                    break;
                case ChestKind.Strap:
                    // Yami: a dark guard over the upper chest, its lower edge glowing violet, and a strap across to the far shoulder
                    a.Fill(bone, O, C("#16101e"), true, 824, 262, 818, 296, 828, 316, 854, 322, 868, 298, 862, 262);
                    a.Line(bone, O, t.acc, 2.4f, 830, 314, 852, 318, 864, 300);
                    a.Fill(bone, O, C("#0b080e"), true, 878, 256, 890, 262, 846, 332, 834, 324);
                    a.Line(bone, O, t.red, 1.8f, 880, 262, 842, 326);
                    break;
                case ChestKind.Cords:
                    // Shinsen: the black jacket's front panel, a white collar edge and red cords laced across it
                    a.Fill(bone, O, C("#0b090c"), true, 822, 262, 814, 304, 818, 340, 846, 350, 868, 330, 874, 290, 864, 262);
                    a.Fill(bone, O, t.coat, false, 828, 262, 836, 262, 842, 340, 834, 340);
                    for (int i = 0; i < 4; i++)
                    {
                        float y = 272f + i * 18f;
                        a.Line(bone, O, t.red, 2.2f, 838, y, 868, y + 14);
                        a.Line(bone, O, t.red, 2.2f, 868, y, 838, y + 14);
                    }
                    break;
                case ChestKind.Plate:
                case ChestKind.Lamellar:
                case ChestKind.Emblem:
                {
                    Color baseCol = t.chest == ChestKind.Plate ? C("#2a2214") : t.chest == ChestKind.Lamellar ? C("#2e1214") : C("#241830");
                    a.Fill(bone, O, baseCol, true, plate);
                    if (t.chest == ChestKind.Lamellar)
                    {
                        // Kagehira: rows of small plates laced crimson
                        foreach (float x in new[] { 826f, 842f, 858f, 874f }) a.Line(bone, O, t.red, 1.8f, x, 262, x + 2, 346);
                        foreach (float y in new[] { 290f, 316f, 338f }) a.Line(bone, O, C("#0a0304"), 1.5f, 814, y, 890, y - 4);
                    }
                    else
                    {
                        // Raiko / Mugen: trim inset along the plate's edges, two lames
                        a.Line(bone, O, t.acc, 2f, 888, 318, 878, 340, 844, 344, 820, 330);
                        a.Line(bone, O, C("#000000"), 1.8f, 856, 264, 858, 340);
                        a.Line(bone, O, C("#000000"), 1.8f, 872, 262, 874, 334);
                        float R = t.chest == ChestKind.Emblem ? 11f : 7.5f;
                        var ring = ArmorArt.Circle(834, 304, R, 14);
                        ring.Add(ring[0]);
                        a.Line(bone, O, t.acc, t.chest == ChestKind.Emblem ? 2.6f : 2f, ring);
                        a.Disc(bone, O, t.acc, 834, 304, R * 0.32f);
                    }
                    break;
                }
                case ChestKind.Mantle:
                    // Yurei: the back of a pale burial mantle over the shoulders, its lower edge in tatters, a blue cord along it
                    a.Fill(bone, O, t.coat, true, 900, 236, 958, 244, 964, 292, 954, 326, 946, 304, 938, 344, 924, 312, 912, 334, 902, 296, 892, 260);
                    a.Fill(bone, O, t.coatLo, false, 944, 250, 960, 260, 956, 304, 940, 296);
                    a.Line(bone, O, t.red, 2f, 950, 262, 920, 270, 906, 302);
                    break;
            }
        }

        // ---------------------------------------------------------------- the shoulder guard (near upper arm)

        static void Sode(ArmorArt a, ArmorTheme t)
        {
            const B bone = B.ArmNU;
            const int O = SodeOrder;
            switch (t.sode)
            {
                case SodeKind.Rags:
                    a.Fill(bone, O, t.scarfLo, true, 878, 254, 930, 240, 958, 262, 956, 300, 946, 318, 952, 334, 936, 346, 932, 328, 918, 352, 910, 326, 896, 340, 890, 300);
                    a.Line(bone, O, t.scarf, 3.4f, 886, 296, 914, 314, 946, 300);
                    a.Line(bone, O, t.coatLo, 1.6f, 912, 270, 930, 288);
                    break;
                case SodeKind.Plate:
                    a.Fill(bone, O, C("#1a1424"), true, 876, 254, 928, 240, 956, 262, 954, 312, 926, 328, 892, 314, 882, 284);
                    a.Line(bone, O, C("#000000"), 2f, 892, 322, 928, 320);
                    a.Line(bone, O, t.acc, 2.2f, 888, 318, 930, 324, 952, 304, 950, 268);
                    a.Fill(bone, O, C("#241a30"), true, 898, 298, 946, 298, 948, 342, 904, 346);
                    a.Line(bone, O, t.acc, 2f, 902, 342, 946, 338);
                    break;
                case SodeKind.Silver:
                    // a domed silver shoulder plate over two lames, each overlapping the one below
                    a.Fill(bone, O, C("#a8a3b3"), true, 902, 304, 960, 304, 962, 336, 910, 342);
                    a.Fill(bone, O, C("#cdc9d4"), true, 890, 280, 958, 278, 960, 312, 896, 316);
                    a.Fill(bone, O, C("#cdc9d4"), true, 876, 256, 926, 238, 958, 258, 958, 290, 884, 292);
                    a.Fill(bone, O, C("#8d8797"), false, 884, 276, 958, 274, 958, 290, 884, 292);
                    a.Line(bone, O, C("#fbfaff"), 2.2f, 884, 256, 926, 242, 954, 258);
                    a.Line(bone, O, C("#4c4654"), 1.6f, 886, 292, 958, 290);
                    a.Line(bone, O, C("#4c4654"), 1.6f, 892, 316, 960, 312);
                    break;
                case SodeKind.Guard:
                    a.Fill(bone, O, t.chest == ChestKind.Lamellar ? C("#2e1214") : C("#2a2214"), true, 880, 254, 928, 242, 954, 262, 952, 308, 924, 322, 892, 308);
                    a.Line(bone, O, t.acc, 2.2f, 886, 304, 924, 318, 948, 304);
                    a.Line(bone, O, C("#000000"), 1.6f, 914, 262, 920, 308);
                    break;
                case SodeKind.Mantle:
                    // Yurei: the mantle's front falling over the shoulder and the upper arm, cut into tatters
                    a.Fill(bone, O, t.coat, true, 876, 252, 930, 238, 958, 258, 964, 300, 952, 322, 958, 340, 940, 336, 932, 358, 920, 332, 906, 350, 898, 320, 884, 334, 880, 290);
                    a.Line(bone, O, t.red, 2.2f, 884, 300, 916, 322, 950, 308);
                    a.Fill(bone, O, t.coatLo, false, 930, 280, 960, 286, 956, 316, 934, 310);
                    break;
            }
        }

        // ---------------------------------------------------------------- the trophies (plates over his own coat)

        static readonly Color IronBase = new Color32(0x26, 0x2a, 0x31, 255), IronHi = new Color32(0x40, 0x45, 0x4e, 255);
        static readonly Color LacqBase = new Color32(0x1e, 0x1e, 0x26, 255), LacqHi = new Color32(0x34, 0x34, 0x3e, 255);

        /// <summary>Kusazuri: hanging skirt plates laced in the accent, painted on the coat's front flap so they swing with the cloth.</summary>
        static void Kusazuri(ArmorArt a, Color tone, Color hi, Color lace, int panels, float depth)
        {
            float[] xs = panels == 1 ? new[] { 806f, 892f } : new[] { 804f, 834f, 864f, 894f };
            for (int i = 0; i < xs.Length - 1; i++)
            {
                float x0 = xs[i], x1 = xs[i + 1], y1 = 398f + depth;
                a.CoatFill("Coat", tone, true, new List<Vector2> { new(x0, 398f), new(x1, 398f), new(x1 - 2f, y1), new(x0 + 2f, y1 + 4f) });
                for (int r = 1; r <= 3; r++)
                {
                    float y = 398f + depth * r / 4f;
                    a.CoatLine("Coat", lace, 1.6f, new List<Vector2> { new(x0 + 3f, y), new(x1 - 3f, y) });
                }
                a.CoatLine("Coat", hi, 1.6f, new List<Vector2> { new(x0 + 2f, 402f), new(x1 - 2f, 402f) });
            }
        }

        /// <summary>Ikazuchi-do (the Garan half-do): iron over the chest, its back edge burnt ragged, a lightning scar, one huge iron sode.</summary>
        public static void HalfDo(ArmorArt a)
        {
            Color ember = C("#ff6a52"), lace = C("#a34434");
            // half a cuirass over the front of his chest, ragged down the middle
            a.Fill(B.Chest, ChestOrder, IronBase, true, 818, 260, 810, 300, 814, 344, 846, 352, 880, 348, 868, 336, 882, 318, 866, 304, 880, 288, 864, 274, 878, 262, 850, 254);
            for (int i = 1; i <= 3; i++) a.Line(B.Chest, ChestOrder, lace, 1.8f, 816, 262 + i * 22, 872, 262 + i * 22 - 4);
            a.Line(B.Chest, ChestOrder, C("#000000"), 2f, 850, 258, 852, 346);
            a.Line(B.Chest, ChestOrder, IronHi, 2f, 822, 270, 818, 326);
            foreach (var (x, y) in new[] { (868f, 336f), (866f, 304f), (864f, 274f) }) a.Disc(B.Chest, ChestOrder, ember, x, y, 2.4f);
            a.Line(B.Chest, ChestOrder, ember, 2.4f, 852, 272, 838, 300, 846, 292, 830, 330);
            // the near sode, one huge lacquered iron plate
            a.Fill(B.ArmNU, SodeOrder, IronBase, true, 868, 252, 930, 234, 966, 258, 968, 322, 932, 344, 890, 330, 876, 292);
            for (int i = 1; i <= 3; i++) a.Line(B.ArmNU, SodeOrder, lace, 1.8f, 884, 250 + i * 22, 960, 244 + i * 22 + 4);
            a.Line(B.ArmNU, SodeOrder, IronHi, 2.2f, 874, 256, 930, 238, 962, 258);
            a.Line(B.ArmNU, SodeOrder, ember, 2.4f, 904, 270, 924, 292, 916, 296, 940, 322);
            Kusazuri(a, IronBase, IronHi, lace, 1, 80f);
        }

        /// <summary>Shiden Yoroi (the vanguard plate): a full do laced violet with a silver lightning mark, stacked sode, kusazuri.</summary>
        public static void VanguardPlate(ArmorArt a)
        {
            Color violet = C("#b07cff"), lace = C("#714fa3"), silver = Palette.Silver;
            a.Fill(B.Chest, ChestOrder, LacqBase, true, 822, 258, 811, 300, 808, 352, 846, 358, 900, 358, 944, 352, 944, 296, 934, 262, 890, 248);
            for (int i = 0; i < 4; i++) a.Line(B.Chest, ChestOrder, lace, 1.8f, 814, 276 + i * 22, 940, 272 + i * 22);
            a.Line(B.Chest, ChestOrder, C("#000000"), 2f, 850, 254, 852, 354);
            a.Line(B.Chest, ChestOrder, LacqHi, 2.2f, 818, 268, 812, 340);
            // the silver lightning mark on the chest plate
            a.Fill(B.Chest, ChestOrder, silver, false, 828, 290, 838, 290, 832, 306, 842, 304, 826, 330, 830, 312, 822, 314);
            a.Line(B.Chest, ChestOrder, violet, 1.8f, 822, 348, 904, 348);
            // two stacked sode over the near shoulder
            a.Fill(B.ArmNU, SodeOrder, LacqBase, true, 878, 252, 930, 236, 960, 256, 958, 296, 886, 292);
            a.Fill(B.ArmNU, SodeOrder, LacqBase, true, 886, 288, 960, 288, 964, 338, 896, 344);
            for (int i = 0; i < 2; i++) a.Line(B.ArmNU, SodeOrder, lace, 1.8f, 886, 266 + i * 20, 956, 262 + i * 20);
            for (int i = 0; i < 2; i++) a.Line(B.ArmNU, SodeOrder, lace, 1.8f, 894, 308 + i * 18, 962, 306 + i * 18);
            a.Line(B.ArmNU, SodeOrder, LacqHi, 2.2f, 882, 256, 930, 240, 956, 258);
            a.Line(B.ArmNU, SodeOrder, violet, 2f, 892, 340, 960, 334);
            Kusazuri(a, LacqBase, LacqHi, lace, 3, 72f);
        }
    }
}
