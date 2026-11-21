using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// A character cut from its painting's SVG trace (SAN-76 SVG redesign), as written by <c>Tools/svg_art/svg_rig.py</c> from the
    /// character's config (<c>Tools/hollow_art/&lt;name&gt;/rig.json</c>) into <c>Resources/Art/SvgRig/&lt;Name&gt;.txt</c>.
    /// <list type="bullet">
    /// <item><b>Regions</b>: the trace cut into pieces, each a list of simple polygons (SVG px, x right, y down) in paint order, each in a
    /// named tone (the style gives the colour), plus the piece's outline (for the back-light rim).</item>
    /// <item><b>Parts</b>: the config's rig parts, passed through: which region, which bone, the depth slot, how the region is mapped onto
    /// the skeleton (<see cref="SvgRig"/>), and the flags (rim, far side, sword layer, glow, role, swing pivot, soft preset and chains).</item>
    /// <item><b>Sources</b> and <b>hash</b>: every input of the cut (the SVG, the config, the tool, the painting) and their FNV-1a hash, so a
    /// test can tell when the cut is stale (<see cref="InputHash"/>).</item>
    /// </list>
    /// The text format is one record per line (<c>region</c>, <c>outline</c>, <c>poly</c>, <c>end</c>, <c>part</c>, ...); see the tool.
    /// </summary>
    public sealed class SvgRigData
    {
        public sealed class Region
        {
            public string name;
            public Vector2[] outline;
            public readonly List<(string tone, Vector2[] pts)> polys = new();
        }

        public sealed class PartSpec
        {
            public string name, region, soft, role;
            public B bone;
            public int slot, sub;
            public string[] map;
            public bool rim, far, sword;
            public float glow = 1f;
            public Vector2? swingPivot;
            public float swing;
            public readonly List<Vector2[]> chains = new();
            /// <summary>Underlays drawn before the region (capsules joint to joint): a bent joint shows the limb, not the background.</summary>
            public readonly List<(string tone, Vector2[] pts)> under = new();
        }

        public int version;
        public ulong hash;
        public readonly List<(string kind, string path)> sources = new();
        public float speck;
        public float fitK = 1f;
        public Vector2 fitSvg, fitRef;
        public string[] tones = Array.Empty<string>();
        public readonly Dictionary<string, Region> regions = new();
        public readonly List<PartSpec> parts = new();

        static readonly Dictionary<string, SvgRigData> _cache = new();

        /// <summary>A cut from Resources (cached for the run: the data never changes while the game runs).</summary>
        public static SvgRigData Load(string resource)
        {
            if (_cache.TryGetValue(resource, out var d)) return d;
            var ta = Resources.Load<TextAsset>(resource);
            if (ta == null) throw new InvalidOperationException("no SVG rig data at Resources/" + resource + ".txt (run Tools/svg_art/svg_rig.py)");
            return _cache[resource] = Parse(ta.text);
        }

        static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        static Vector2[] Pts(string[] t, int from)
        {
            var r = new Vector2[(t.Length - from) / 2];
            for (int i = 0; i < r.Length; i++) r[i] = new Vector2(F(t[from + i * 2]), F(t[from + i * 2 + 1]));
            return r;
        }

        public static SvgRigData Parse(string text)
        {
            var d = new SvgRigData();
            Region region = null;
            PartSpec part = null;
            int lineNo = 0;
            foreach (var raw in text.Split('\n'))
            {
                lineNo++;
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var t = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                try
                {
                    if (part != null)
                    {
                        switch (t[0])
                        {
                            case "region": part.region = t[1]; break;
                            case "bone": part.bone = (B)Enum.Parse(typeof(B), t[1]); break;
                            case "order": part.slot = int.Parse(t[1], CultureInfo.InvariantCulture); part.sub = int.Parse(t[2], CultureInfo.InvariantCulture); break;
                            case "map": part.map = t[1..]; break;
                            case "rim": part.rim = true; break;
                            case "far": part.far = true; break;
                            case "sword": part.sword = true; break;
                            case "glow": part.glow = F(t[1]); break;
                            case "role": part.role = t[1]; break;
                            case "swing": part.swingPivot = new Vector2(F(t[1]), F(t[2])); part.swing = F(t[3]); break;
                            case "soft": part.soft = t[1]; break;
                            case "chain": part.chains.Add(Pts(t, 1)); break;
                            case "under": part.under.Add((t[1], Pts(t, 2))); break;
                            case "end": d.parts.Add(part); part = null; break;
                            default: throw new FormatException("unknown part field " + t[0]);
                        }
                        continue;
                    }
                    if (region != null)
                    {
                        switch (t[0])
                        {
                            case "outline": region.outline = Pts(t, 1); break;
                            case "poly": region.polys.Add((t[1], Pts(t, 2))); break;
                            case "end": d.regions[region.name] = region; region = null; break;
                            default: throw new FormatException("unknown region field " + t[0]);
                        }
                        continue;
                    }
                    switch (t[0])
                    {
                        case "svgrig": d.version = int.Parse(t[1], CultureInfo.InvariantCulture); break;
                        case "hash": d.hash = ulong.Parse(t[1], CultureInfo.InvariantCulture); break;
                        case "source": d.sources.Add((t[1], t[2])); break;
                        case "speck": d.speck = F(t[1]); break;
                        case "fit": d.fitK = F(t[1]); d.fitSvg = new Vector2(F(t[2]), F(t[3])); d.fitRef = new Vector2(F(t[4]), F(t[5])); break;
                        case "tones": d.tones = t[1..]; break;
                        case "region": region = new Region { name = t[1] }; break;
                        case "part": part = new PartSpec { name = t[1], region = t[1] }; break;
                        default: throw new FormatException("unknown record " + t[0]);
                    }
                }
                catch (Exception e) when (e is not FormatException)
                {
                    throw new FormatException("SVG rig data line " + lineNo + ": " + e.Message, e);
                }
            }
            return d;
        }

        /// <summary>
        /// FNV-1a 64 over every source, in order, from the project root: text files by character with '\r' dropped (the same on a CRLF
        /// checkout), binary files by byte. Equal to <see cref="hash"/> when the cut is up to date with its inputs.
        /// </summary>
        public ulong InputHash(string projectRoot)
        {
            ulong h = 1469598103934665603UL;
            void Mix(ulong v) { h ^= v; h *= 1099511628211UL; }
            foreach (var (kind, path) in sources)
            {
                string full = Path.Combine(projectRoot, path);
                if (kind == "binary") foreach (byte b in File.ReadAllBytes(full)) Mix(b);
                else foreach (char c in File.ReadAllText(full)) if (c != '\r') Mix(c);
            }
            return h;
        }
    }
}
