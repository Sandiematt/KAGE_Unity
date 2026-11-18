using System.IO;
using UnityEditor;
using UnityEngine;

namespace KageEditor
{
    /// <summary>
    /// Bakes Raikon's cut (SAN-71): cuts his SVG along the generated regions and writes every piece to
    /// Resources/Art/Raikon/RaikonCut.bytes, so spawning him doesn't cut the SVG at run time. Re-bake after changing the
    /// SVG, <c>Tools/raikon_art/regions.py</c> (and its RaikonRegions.g.cs) or the cutting code; a stale bake is
    /// ignored (with a warning) and the EditMode tests fail until it is re-baked. Batch:
    /// <c>Unity.exe -batchmode -projectPath ... -executeMethod KageEditor.RaikonArtBake.Bake -quit</c>.
    /// </summary>
    public static class RaikonArtBake
    {
        public const string AssetPath = "Assets/Kage/Resources/Art/Raikon/RaikonCut.bytes";

        [MenuItem("Kage/Bake Raikon Art")]
        public static void Bake()
        {
            var bytes = Kage.RaikonModel.Bake();
            File.WriteAllBytes(AssetPath, bytes);
            AssetDatabase.ImportAsset(AssetPath);
            Debug.Log($"[SAN-71] Raikon's cut baked: {AssetPath} ({bytes.Length / 1024} KB)");
        }
    }
}
