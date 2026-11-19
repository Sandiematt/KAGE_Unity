using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Fighting-game camera: frames both fighters (zooms out with distance), keeps the floor low
    /// in frame, clamps to the stage, and adds trauma shake, zoom punch and parallax layers.
    /// Runs on unscaled time so it stays smooth through hit-stop and slow motion.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class CameraRig : MonoBehaviour
    {
        public Camera cam;
        public readonly List<Transform> targets = new();
        public float minSize = 2.25f, maxSize = 3.6f, stageHalf = 9.4f;
        public Vector2 overrideCenter; public float overrideSize; public bool useOverride;
        float _size = 2.3f;
        Vector2 _pos = new(0f, 1.4f);

        struct Layer { public Transform t; public float factor; public float baseX; public float baseY; public float yFactor; }
        readonly List<Layer> _layers = new();

        public void AddParallax(Transform t, float factor, float yFactor = 0f)
        {
            _layers.Add(new Layer { t = t, factor = factor, baseX = t.position.x, baseY = t.position.y, yFactor = yFactor });
        }

        public void Snap()
        {
            Compute(out var c, out var s);
            _pos = c; _size = s;
            Apply();
        }

        void Compute(out Vector2 center, out float size)
        {
            if (useOverride) { center = overrideCenter; size = overrideSize; return; }
            float minX = float.MaxValue, maxX = float.MinValue, maxY = 0f;
            int n = 0;
            foreach (var t in targets)
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                Vector3 p = t.position;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y);
                n++;
            }
            if (n == 0) { center = new Vector2(0f, 1.4f); size = minSize; return; }
            float spread = maxX - minX;
            float aspect = Mathf.Max(0.5f, cam.aspect);
            size = Mathf.Clamp((spread + 2.8f) / (2f * aspect), minSize, maxSize);
            float cx = (minX + maxX) * 0.5f;
            float cy = size * 0.64f + Mathf.Max(0f, maxY - 1.6f) * 0.35f;
            float half = size * aspect;
            cx = Mathf.Clamp(cx, -stageHalf + half, stageHalf - half);
            center = new Vector2(cx, cy);
        }

        // ---- group fights (SAN-83) ----
        FightContext _fight;
        Fighter _focus;

        public void Frame(FightContext fight, Fighter focus) { _fight = fight; _focus = focus; }

        void LateUpdate()
        {
            if (cam == null) return;
            Follow(Time.unscaledDeltaTime);
        }

        public void Follow(float dt)
        {
            Compute(out var c, out var s);
            _pos = KMath.Damp(_pos, c, 6f, dt);
            _size = KMath.Damp(_size, s, 4f, dt);
            Apply();
        }

        void Apply()
        {
            float tr = GameFeel.trauma * GameFeel.trauma;
            float t = Time.unscaledTime * 22f;
            Vector2 shake = new Vector2(Mathf.PerlinNoise(t, 1.3f) - 0.5f, Mathf.PerlinNoise(4.1f, t) - 0.5f) * 2f * tr * 0.32f;
            float rot = (Mathf.PerlinNoise(t * 0.7f, 9.2f) - 0.5f) * 2f * tr * 2.2f;
            float size = _size * (1f - GameFeel.punch * 0.6f);
            cam.orthographicSize = size;
            transform.SetPositionAndRotation(new Vector3(_pos.x + shake.x, _pos.y + shake.y, -10f), Quaternion.Euler(0f, 0f, rot));
            foreach (var l in _layers)
            {
                if (l.t == null) continue;
                var p = l.t.position;
                p.x = l.baseX + _pos.x * l.factor;
                p.y = l.baseY + (_pos.y - 1.4f) * l.yFactor;
                l.t.position = p;
            }
        }
    }
}
