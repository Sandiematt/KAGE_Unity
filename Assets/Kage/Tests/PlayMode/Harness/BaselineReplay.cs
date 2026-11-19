using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Reads, writes and compares baseline replay files (Assets/Kage/Tests/PlayMode/Baselines).
    /// A file holds a scenario's canonical event lines (ADR-0003), which must match exactly, and its
    /// per-frame lines (positions, states, moves, health), whose numbers must match within 1e-4.
    /// Set the environment variable KAGE_RECORD_BASELINES=1 to (re)write the files instead of comparing.
    /// </summary>
    public static class BaselineReplay
    {
        public const float Tolerance = 1e-4f;
        const string EventsHeader = "[events]", FramesHeader = "[frames]";

        public static string Folder => Path.Combine(Application.dataPath, "Kage", "Tests", "PlayMode", "Baselines");
        public static string PathOf(string scenario) => Path.Combine(Folder, scenario + ".txt");
        /// <summary>
        /// The replay recorded before SAN-65 (hits resolved inside the attacker's tick by point tests).
        /// The legacy switch must still produce it exactly. It is re-recorded only when a deliberate mechanic change
        /// outside hit resolution moves it: SAN-86 (Guard button and posture; see ADR-0004's SAN-86 notes).
        /// </summary>
        public static string LegacyPathOf(string scenario) => Path.Combine(Folder, "Legacy", scenario + ".txt");
        public static bool Recording => Environment.GetEnvironmentVariable("KAGE_RECORD_BASELINES") == "1";
        /// <summary>KAGE_RECORD_LEGACY=1 also rewrites the legacy-switch baselines (Baselines/Legacy), when a deliberate mechanic change outside hit resolution moves them (SAN-82: the rage art).</summary>
        public static bool RecordingLegacy => Environment.GetEnvironmentVariable("KAGE_RECORD_LEGACY") == "1";

        public static string Format(string scenario, int seed, IEnumerable<string> events, IEnumerable<string> frames)
        {
            var sb = new StringBuilder();
            sb.Append("# KAGE baseline replay: ").Append(scenario).Append('\n');
            sb.Append("# seed ").Append(seed.ToString(CultureInfo.InvariantCulture))
              .Append(". Frame lines: frame, then per fighter: id x y state move:moveFrame hp; then the bag: x y rotation.\n");
            sb.Append(EventsHeader).Append('\n');
            foreach (var l in events) sb.Append(l).Append('\n');
            sb.Append(FramesHeader).Append('\n');
            foreach (var l in frames) sb.Append(l).Append('\n');
            return sb.ToString();
        }

        public static void WriteLegacy(string scenario, string text)
        {
            Directory.CreateDirectory(Path.Combine(Folder, "Legacy"));
            File.WriteAllText(LegacyPathOf(scenario), text, new UTF8Encoding(false));
        }

        public static void Write(string scenario, string text)
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(PathOf(scenario), text, new UTF8Encoding(false));
        }

        /// <summary>Null when equal; otherwise the first difference, described.</summary>
        public static string Compare(string expected, string actual)
        {
            Split(expected, out var ee, out var ef);
            Split(actual, out var ae, out var af);
            for (int i = 0; i < Math.Max(ee.Count, ae.Count); i++)
            {
                string x = i < ee.Count ? ee[i] : "<none>", y = i < ae.Count ? ae[i] : "<none>";
                if (x != y) return $"event {i}: expected\n  {x}\nbut was\n  {y}";
            }
            for (int i = 0; i < Math.Max(ef.Count, af.Count); i++)
            {
                string x = i < ef.Count ? ef[i] : "<none>", y = i < af.Count ? af[i] : "<none>";
                if (!SameFrame(x, y)) return $"frame line {i}: expected\n  {x}\nbut was\n  {y}";
            }
            return null;
        }

        static bool SameFrame(string x, string y)
        {
            var a = x.Split(' ');
            var b = y.Split(' ');
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] == b[i]) continue;
                if (!float.TryParse(a[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var fa) ||
                    !float.TryParse(b[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var fb) ||
                    Math.Abs(fa - fb) > Tolerance) return false;
            }
            return true;
        }

        static void Split(string text, out List<string> events, out List<string> frames)
        {
            events = new List<string>();
            frames = new List<string>();
            List<string> into = null;
            foreach (var raw in text.Split('\n'))
            {
                var l = raw.TrimEnd('\r');
                if (l.Length == 0 || l.StartsWith("#")) continue;
                if (l == EventsHeader) { into = events; continue; }
                if (l == FramesHeader) { into = frames; continue; }
                into?.Add(l);
            }
        }
    }
}
