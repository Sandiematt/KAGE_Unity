using System.Collections.Generic;
using Kage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KageEditor
{
    /// <summary>
    /// Editor entry points (also callable from the Unity CLI with `unity command script eval`):
    ///   KAGE/Build Arena Scene        – creates Assets/Scenes/KageArena.unity and adds it to the build
    ///   KAGE/Bake Kage Character Prefab – saves the code-built rig (bones + meshes) as a prefab to inspect
    /// </summary>
    public static class KageSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/KageArena.unity";
        public const string PrefabDir = "Assets/Kage/Prefabs";

        [MenuItem("KAGE/Build Arena Scene")]
        public static string BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2.3f;
            cam.transform.position = new Vector3(0f, 1.45f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.114f, 0.094f, 0.106f);
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;

            var game = new GameObject("KAGE");
            game.AddComponent<KageGame>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene> { new(ScenePath, true) };
            EditorBuildSettings.scenes = scenes.ToArray();
            return ScenePath;
        }

        [MenuItem("KAGE/Bake Kage Character Prefab")]
        public static string BakePrefab() => Bake(KageModel.Look.Kage, "Kage");

        [MenuItem("KAGE/Bake Ronin Character Prefab")]
        public static string BakeRonin() => Bake(KageModel.Look.Ronin, "Ronin");

        static string Bake(KageModel.Look look, string name)
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir)) AssetDatabase.CreateFolder("Assets/Kage", "Prefabs");
            string meshPath = $"{PrefabDir}/{name}_Meshes.asset";
            string prefabPath = $"{PrefabDir}/{name}.prefab";
            AssetDatabase.DeleteAsset(meshPath);

            var root = new GameObject(name);
            var vis = new GameObject("Visual");
            vis.transform.SetParent(root.transform, false);
            var rig = vis.AddComponent<FighterRig>();
            rig.Build(KageModel.Build(look), 0);
            var pose = PoseLib.Ref();
            rig.Apply(pose);
            if (rig.scarf != null) { rig.scarf.Prewarm(90); }

            // persist every generated mesh + the shared materials into one asset file
            var container = new Mesh { name = name + "_Container" };
            AssetDatabase.CreateAsset(container, meshPath);
            var seen = new HashSet<Object>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                if (m == null || seen.Contains(m)) continue;
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m))) { AssetDatabase.AddObjectToAsset(m, meshPath); }
                seen.Add(m);
            }
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mat = mr.sharedMaterial;
                if (mat == null || seen.Contains(mat)) continue;
                if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat))) AssetDatabase.AddObjectToAsset(mat, meshPath);
                seen.Add(mat);
            }
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.Refresh();
            return prefabPath;
        }
    }
}
