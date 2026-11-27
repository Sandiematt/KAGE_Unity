using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Kage.Tests
{
    /// <summary>
    /// The screen breaking like glass (Earthsplitter, docs/weapon-styles.md) draws on a real render: a HUD on a camera that renders into
    /// a texture (as the review harness does), the crack web lit over a plain background, then the shatter capturing the frame and its
    /// shards falling away. Needs a GPU: skips itself under -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class GlassScreenTests
    {
        const int W = 640, H = 360;
        static readonly Color Back = new(0.16f, 0.13f, 0.14f, 1f);

        GameObject _camGo, _hudGo;
        Camera _cam;
        RenderTexture _rt;
        Hud _hud;
        float _prevCapture;

        void Build()
        {
            // the glass runs on unscaled time: a fixed 1/60 s frame (batch-mode frames are a few milliseconds of real time)
            _prevCapture = Time.captureDeltaTime;
            Time.captureDeltaTime = 1f / 60f;
            _camGo = new GameObject("GlassCam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 2.3f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Back;
            _cam.transform.position = new Vector3(0f, 1f, -10f);
            _rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            _cam.targetTexture = _rt;
            _hudGo = new GameObject("HUD", typeof(RectTransform));
            _hud = _hudGo.AddComponent<Hud>();
            _hud.Build(_cam);
            var canvas = _hudGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _cam;
            canvas.planeDistance = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = _prevCapture;
            if (_hudGo != null) Object.DestroyImmediate(_hudGo);
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); }
        }

        Color32[] Render()
        {
            _cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            RenderTexture.active = prev;
            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return px;
        }

        /// <summary>Pixels far brighter than the background (the HUD's own panels are left out: only the middle band of the frame counts).</summary>
        static int Bright(Color32[] px)
        {
            int n = 0;
            for (int y = H / 3; y < H * 2 / 3; y++)
                for (int x = 0; x < W; x++)
                {
                    var c = px[y * W + x];
                    if (c.r + c.g + c.b > 3 * 150) n++;
                }
            return n;
        }

        string Describe()
        {
            var sb = new System.Text.StringBuilder();
            var glass = _hudGo.transform.Find("Glass");
            sb.Append($"glass {(glass == null ? "missing" : glass.gameObject.activeInHierarchy ? "active" : "inactive")}, showing {_hud.GlassShowing}, shattering {_hud.Shattering}");
            if (glass != null)
                foreach (var g in glass.GetComponentsInChildren<Graphic>(true))
                    sb.Append($"\n  {g.GetType().Name} '{g.name}' active {g.isActiveAndEnabled} rect {g.rectTransform.rect} colour {g.color} renderer {(g.GetComponent<CanvasRenderer>() != null ? "yes" : "MISSING")}");
            return sb.ToString();
        }

        [UnityTest]
        public IEnumerator The_crack_web_draws_over_the_screen()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("needs a GPU: run without -nographics"); yield break; }
            Build();
            yield return null;
            int before = Bright(Render());
            _hud.CrackScreen(new Vector2(0f, 1f), new Color(1f, 0.8f, 0.3f));
            for (int i = 0; i < 6; i++) yield return null;
            int after = Bright(Render());
            Debug.Log($"[glass] crack: bright pixels {before} -> {after}; {Describe()}");
            Assert.IsTrue(_hud.GlassShowing, "the crack is playing");
            Assert.Greater(after, before + 200, "the crack web lights the screen");
        }

        [UnityTest]
        public IEnumerator The_shatter_breaks_the_captured_frame_into_falling_shards()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Assert.Ignore("needs a GPU: run without -nographics"); yield break; }
            Build();
            // something bright in the middle of the frame for the shards to carry
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(_camGo.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, 5f);
            quad.transform.localScale = new Vector3(2f, 1f, 1f);
            var mat = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
            quad.GetComponent<Renderer>().sharedMaterial = mat;
            try
            {
                yield return null;
                Render();
                _hud.ShatterScreen(new Vector2(0f, 1f), new Color(1f, 0.9f, 0.5f));
                for (int i = 0; i < 4; i++) yield return null;
                Debug.Log($"[glass] shatter after 4 frames: {Describe()}");
                Assert.IsTrue(_hud.Shattering, "the frame was captured and the shatter is playing");
                // the quad goes; only the shards still carry it
                quad.SetActive(false);
                for (int i = 0; i < 10; i++) yield return null;
                int held = Bright(Render());
                Debug.Log($"[glass] shatter, quad hidden, 14 frames in: bright pixels {held}");
                Assert.Greater(held, 2000, "the shards carry the captured frame");
                for (int i = 0; i < 150; i++) yield return null;
                Assert.IsFalse(_hud.GlassShowing, "the shatter is over");
                Assert.Less(Bright(Render()), 200, "the shards are gone");
            }
            finally
            {
                Object.DestroyImmediate(quad);
                Object.DestroyImmediate(mat);
            }
        }
    }
}
