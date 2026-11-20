using System.Collections.Generic;
using Kage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KageEditor
{
    /// <summary>
    /// Editor entry point for the campaign scene (SAN-67): KAGE/Build Campaign Scene creates Assets/Scenes/KageCampaign.unity,
    /// which holds a camera and the GameFlow object (everything else is built at run time), and adds it to the build next to the
    /// dev arena. Also callable from the CLI: -executeMethod KageEditor.FlowSceneBuilder.BuildScene.
    /// </summary>
    public static class FlowSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/KageCampaign.unity";

        [MenuItem("KAGE/Build Campaign Scene")]
        public static string BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2.3f;
            cam.transform.position = new Vector3(0f, 1.45f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.027f, 0.027f);
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;

            new GameObject("KAGE Campaign").AddComponent<GameFlow>().showSplash = true;

            EditorSceneManager.SaveScene(scene, ScenePath);

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            return ScenePath;
        }
    }

    /// <summary>
    /// The JS images in Resources/UI/Img are UI pictures: no mipmaps, no compression, bilinear, original size, so they stay as
    /// sharp as the JS art (a plain texture import would blur them when the screen scales).
    /// </summary>
    public sealed class UiTexturePostprocessor : AssetPostprocessor
    {
        public override uint GetVersion() { return 2; }

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/UI/Img/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Default;
            t.mipmapEnabled = false;
            t.npotScale = TextureImporterNPOTScale.None;
            t.alphaIsTransparency = true;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.filterMode = FilterMode.Bilinear;
            t.maxTextureSize = 2048;
            t.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
