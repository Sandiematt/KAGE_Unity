using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KageEditor
{
    /// <summary>
    /// Checks every SVG rig cut the game reads (<c>Resources/Art/SvgRig/*.txt</c>: the Shade, the Husk, the Ironclad, Raikon) against
    /// its inputs (SAN-71 redesign; it replaces SAN-71's <c>Kage > Bake Raikon Art</c>). There is nothing to bake any more: a cut is
    /// written by <c>Tools/svg_art/svg_rig.py</c> outside Unity (Python 3, numpy, opencv) and read as text at spawn. The menu recomputes
    /// each cut's input hash (its SVG, config, painting and the tool, <see cref="Kage.SvgRigData.InputHash"/>) and logs every stale cut
    /// with the command that re-cuts it. The EditMode freshness test of each character fails on the same condition. Batch:
    /// <c>Unity.exe -batchmode -projectPath ... -executeMethod KageEditor.SvgRigCuts.Check -quit</c>.
    /// </summary>
    public static class SvgRigCuts
    {
        public const string Folder = "Assets/Kage/Resources/Art/SvgRig";

        [MenuItem("Kage/Check SVG Rig Cuts")]
        public static void Check()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            int stale = 0, n = 0;
            foreach (var file in Directory.GetFiles(Path.Combine(root, Folder), "*.txt").OrderBy(f => f))
            {
                n++;
                var data = Kage.SvgRigData.Parse(File.ReadAllText(file));
                string config = data.sources.Select(s => s.path).FirstOrDefault(p => p.EndsWith(".json")) ?? "<its rig.json>";
                string name = Path.GetFileNameWithoutExtension(file);
                var missing = data.sources.Where(s => !File.Exists(Path.Combine(root, s.path))).Select(s => s.path).ToList();
                if (missing.Count > 0) { stale++; Debug.LogError($"[SVG rig] {name}: missing input {string.Join(", ", missing)}"); continue; }
                if (data.InputHash(root) == data.hash) Debug.Log($"[SVG rig] {name}: fresh ({data.regions.Count} regions, {data.parts.Count} parts)");
                else { stale++; Debug.LogWarning($"[SVG rig] {name}: stale, re-cut with  python Tools/svg_art/svg_rig.py {config}"); }
            }
            Debug.Log($"[SVG rig] {n} cuts, {stale} stale");
        }
    }
}
