using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Kage
{
    /// <summary>
    /// The bust in a HUD medal (JS <c>drawPortrait</c>, SAN-108): the live fighter framed on its head. A small hidden camera looks at the
    /// head bone and draws only that fighter's renderers (they are put on a spare layer for the moment of the render and put back), into
    /// a transparent texture the medal shows over its radial glow. Called on the fight's thread (Update), never while a camera renders.
    /// </summary>
    public sealed class HudPortrait
    {
        /// <summary>A layer nothing else uses.</summary>
        public const int Layer = 31;
        public const int Size = 192;
        /// <summary>Half the height of the framed area, metres: the JS frames the head at about 30% of the medal.</summary>
        public const float HalfHeight = 0.42f;

        public RenderTexture Texture { get; private set; }
        Camera _cam;
        GameObject _go;
        readonly List<(GameObject go, int layer)> _saved = new();

        public HudPortrait()
        {
            Texture = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { name = "HudPortrait", antiAliasing = 1 };
            Texture.Create();
        }

        /// <summary>Renders <paramref name="f"/>'s head into <see cref="Texture"/>. Returns false when the fighter has nothing to draw yet.</summary>
        public bool Capture(Fighter f)
        {
            if (f == null || f.rig == null || Texture == null) return false;
            var head = f.rig.Bone(B.Head);
            if (head == null) return false;
            var renderers = f.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return false;
            EnsureCamera();

            _saved.Clear();
            foreach (var r in renderers)
            {
                var g = r.gameObject;
                _saved.Add((g, g.layer));
                g.layer = Layer;
            }
            float face = f.facing >= 0f ? 1f : -1f;
            // JS: the head sits a little below the middle of the medal
            _go.transform.position = new Vector3(head.position.x + face * 0.03f, head.position.y - 0.02f, -10f);
            _cam.orthographicSize = HalfHeight * Mathf.Max(0.8f, f.Character != null ? f.Character.bodyScale : 1f);
            try { _cam.Render(); }
            finally
            {
                foreach (var (go, layer) in _saved) if (go != null) go.layer = layer;
                _saved.Clear();
            }
            return true;
        }

        void EnsureCamera()
        {
            if (_cam != null) return;
            _go = new GameObject("HudPortraitCamera") { hideFlags = HideFlags.HideAndDontSave };
            _cam = _go.AddComponent<Camera>();
            _cam.enabled = false;
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _cam.cullingMask = 1 << Layer;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 50f;
            _cam.targetTexture = Texture;
            var data = _cam.GetUniversalAdditionalCameraData();
            if (data != null) { data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None; data.renderShadows = false; }
        }

        public void Dispose()
        {
            if (_go != null) Object.Destroy(_go);
            _go = null; _cam = null;
            if (Texture != null) { Texture.Release(); Object.Destroy(Texture); Texture = null; }
        }
    }
}
