using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>The horizontal limits of the view (SAN-89), set by a stretch session.</summary>
    public interface ICameraBounds
    {
        float CameraLeft { get; }
        float CameraRight { get; }
    }

    /// <summary>
    /// Fighting-game camera: frames both fighters (zooms out with distance), keeps the floor low
    /// in frame, clamps to the stage, and adds trauma shake, zoom punch and parallax layers.
    /// Runs on unscaled time so it stays smooth through hit-stop and slow motion.
    /// <para>It frames either <see cref="targets"/> (the duel) or, after <see cref="Frame"/>, a group fight (SAN-83): the
    /// focus fighter (Kage) and the foes engaged on him (<see cref="EngagementManager"/>), each frame, zooming out up to
    /// <see cref="groupMaxSize"/>. Waiting foes are not framed. The override fields win over both (rage arts, SAN-82).</para>
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class CameraRig : MonoBehaviour
    {
        public Camera cam;
        public readonly List<Transform> targets = new();
        public float minSize = 2.25f, maxSize = 3.6f, stageHalf = 9.4f;
        public Vector2 overrideCenter; public float overrideSize; public bool useOverride;
        /// <summary>The widest framing in a group fight (SAN-83): two engaged foes on either side of Kage need more room than a duel.</summary>
        public float groupMaxSize = 4.2f;
        /// <summary>How far ahead of Kage (metres, in his facing) the camera looks while he has nobody to fight: the road (SAN-89).</summary>
        public float lookAhead;
        float _size = 2.3f;
        float _look;
        Vector2 _pos = new(0f, 1.4f);

        struct Layer { public Transform t; public float factor; public float baseX; public float baseY; public float yFactor; }
        readonly List<Layer> _layers = new();

        public void AddParallax(Transform t, float factor, float yFactor = 0f)
        {
            _layers.Add(new Layer { t = t, factor = factor, baseX = t.position.x, baseY = t.position.y, yFactor = yFactor });
        }

        public void Snap()
        {
            if (_focus != null) _look = _focus.facing * lookAhead;
            Compute(out var c, out var s);
            _pos = c; _size = s;
            Apply();
        }

        void Compute(out Vector2 center, out float size)
        {
            if (useOverride) { center = overrideCenter; size = overrideSize; return; }
            float minX = float.MaxValue, maxX = float.MinValue, maxY = 0f;
            int n = 0;
            void Include(Transform t)
            {
                if (t == null || !t.gameObject.activeInHierarchy) return;
                Vector3 p = t.position;
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); maxY = Mathf.Max(maxY, p.y);
                n++;
            }
            bool group = _focus != null && _fight != null;
            if (group)
            {
                Include(Hips(_focus));
                foreach (var e in _fight.Engagement.Engaged) if (e.opponent == _focus) Include(Hips(e));
            }
            else foreach (var t in targets) Include(t);
            if (n == 0) { center = new Vector2(0f, 1.4f); size = minSize; return; }
            float spread = maxX - minX;
            float aspect = Mathf.Max(0.5f, cam.aspect);
            size = Mathf.Clamp((spread + 2.8f) / (2f * aspect), minSize, group ? Mathf.Max(maxSize, groupMaxSize) : maxSize);
            float cx = (minX + maxX) * 0.5f;
            if (group && n == 1) cx += _look;          // nobody to fight: look down the road
            float cy = size * 0.64f + Mathf.Max(0f, maxY - 1.6f) * 0.35f;
            float half = size * aspect;
            cx = ClampX(cx, half);
            center = new Vector2(cx, cy);
        }

        // ---- bounds (SAN-89) ----
        ICameraBounds _bounds;

        /// <summary>The camera's horizontal limits: a road's level, or the arena of a locked encounter (<see cref="StretchSession"/>). Null goes back to the stage's ±<see cref="stageHalf"/>.</summary>
        public void SetBounds(ICameraBounds bounds) => _bounds = bounds;

        /// <summary>Left and right limit of what the camera shows, in world x.</summary>
        public Vector2 Limits => _bounds != null ? new Vector2(_bounds.CameraLeft, _bounds.CameraRight) : new Vector2(-stageHalf, stageHalf);

        /// <summary>
        /// Keeps a view centred on <paramref name="cx"/>, <paramref name="half"/> wide each side, inside <see cref="Limits"/>; when the
        /// limits are narrower than the view it is centred on them.
        /// </summary>
        public float ClampX(float cx, float half)
        {
            var l = Limits;
            if (l.y - l.x <= 2f * half) return (l.x + l.y) * 0.5f;
            return Mathf.Clamp(cx, l.x + half, l.y - half);
        }

        // ---- group fights (SAN-83) ----
        FightContext _fight;
        Fighter _focus;

        /// <summary>Frames a group fight: <paramref name="focus"/> and the foes engaged on it. Null, null goes back to <see cref="targets"/>.</summary>
        public void Frame(FightContext fight, Fighter focus) { _fight = fight; _focus = focus; }

        static Transform Hips(Fighter f) => f != null && f.rig != null ? f.rig.Bone(B.Hips) : null;

        /// <summary>The focus fighter of a group fight (<see cref="Frame"/>), or null.</summary>
        public Fighter Focus => _focus;

        void LateUpdate()
        {
            if (cam == null) return;
            Follow(Time.unscaledDeltaTime);
        }

        /// <summary>One step of the follow: eases position and size towards the framing over <paramref name="dt"/> seconds.</summary>
        public void Follow(float dt)
        {
            if (_focus != null && lookAhead > 0f) _look = KMath.Damp(_look, _focus.facing * lookAhead, 1.6f, dt);
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
            shake += GameFeel.kick;
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
