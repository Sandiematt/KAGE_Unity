using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The thunderstorm in the backdrop of Raikon's fight (docs/raikon-ascendant-vfx.md, "Thunder pass"), one level per boss phase:
    /// <list type="bullet">
    /// <item><b>A storm bank</b> churning across the top of the sky, fixed to the view like the sky (in front of the moon and the painted clouds, behind
    /// the far city). Thicker and lower each phase; lit from below by every flash.</item>
    /// <item><b>Sheet lightning</b>: a patch of the bank flashing from inside.</item>
    /// <item><b>Far bolts</b> forking down out of the bank behind the far city (its blocks and pagodas stand black against them), with a glow at
    /// their foot.</item>
    /// <item>From phase 2, <b>crawlers</b>: a bolt running sideways along the bank's underside.</item>
    /// <item>From phase 3, <b>near strikes</b> on the roofs behind the arena (their ridges hide the foot) that light the whole backdrop.</item>
    /// </list>
    /// Phase 1 is a distant storm (a flash every 3.5–7 s), phase 2 rolls in (1.8–3.6 s), phase 3 breaks over the rooftops (0.8–1.9 s), the Final
    /// Overload is the storm at its height (0.45–1.1 s). A phase shift sets off a burst of three. It never touches the fight's own layers, never
    /// flashes the fighters, never shakes the camera more than a nudge, and draws nothing headless (no <see cref="Vfx"/>).
    /// </summary>
    [DefaultExecutionOrder(260)]
    public sealed class RaikonSkyStorm : MonoBehaviour
    {
        /// <summary>Sorting orders inside EnvBackdrop's layers: the bank over its moon (-2985) and clouds (-2980); far bolts over the bank, behind the far
        /// city (-2900); near strikes over its lanterns (-2650), behind its roof ridges (-2600).</summary>
        public const int OrderBankBack = -2978, OrderBank = -2977, OrderBankGlow = -2975, OrderFarGlow = -2958, OrderFar = -2950, OrderNear = -2606;

        const int Puffs = 28;

        static readonly float[] ThicknessOf = { 0.55f, 0.85f, 1.15f, 1.35f };
        static readonly Vector2[] IntervalOf = { new(3.5f, 7f), new(1.8f, 3.6f), new(0.8f, 1.9f), new(0.45f, 1.1f) };
        static readonly float[] SheetShareOf = { 0.55f, 0.35f, 0.2f, 0.15f };

        Fighter _f;
        int _level;
        int _burst;
        float _next = 1.2f, _t;
        GameObject _root;
        Ribbon _back, _body, _glow;
        readonly float[] _u = new float[Puffs], _v = new float[Puffs], _r = new float[Puffs], _ph = new float[Puffs];
        struct Flash { public float x, age, life, amp, rx; }
        readonly List<Flash> _flashes = new();

        /// <summary>The storm's level (0..3).</summary>
        public int Level => _level;

        /// <summary>The backdrop storm of <paramref name="f"/>'s fight at the level of boss phase <paramref name="phase"/>; a level up sets off a burst.</summary>
        public static RaikonSkyStorm Attach(Fighter f, int phase)
        {
            if (f == null) return null;
            var s = f.GetComponent<RaikonSkyStorm>();
            if (s == null) s = f.gameObject.AddComponent<RaikonSkyStorm>();
            s._f = f;
            int lv = Mathf.Clamp(phase, 0, 3);
            if (lv > s._level) { s._burst = 3; s._next = 0.15f; }
            s._level = lv;
            return s;
        }

        void Build(Vfx v)
        {
            foreach (var r in new[] { _back, _body, _glow }) if (r != null && r.mesh != null) Destroy(r.mesh);     // a new Vfx: the old root went with the old one
            _root = new GameObject("RaikonSkyStorm");
            _root.transform.SetParent(v.transform, false);
            _back = new Ribbon("StormBankBack", _root.transform, MeshKit.Alpha, OrderBankBack);
            _body = new Ribbon("StormBank", _root.transform, MeshKit.Alpha, OrderBank);
            _glow = new Ribbon("StormBankGlow", _root.transform, v.AddHDR(1.5f), OrderBankGlow);
            var rnd = new System.Random(4111);
            for (int i = 0; i < Puffs; i++)
            {
                _u[i] = -0.78f + 1.56f * (i + (float)rnd.NextDouble() * 0.8f) / Puffs;      // spread a little past both edges of the view
                _v[i] = (float)rnd.NextDouble();
                _r[i] = 0.45f + 0.35f * (float)rnd.NextDouble();
                _ph[i] = (float)rnd.NextDouble() * 6.28f;
            }
        }

        void LateUpdate()
        {
            var v = Vfx.I;
            if (_f == null || v == null) return;
            if (_root == null) Build(v);
            float dt = Time.deltaTime;
            _t += dt;
            var cam = Vfx.ViewCam(_f);
            var view = Vfx.ViewRect(cam);
            float thick = ThicknessOf[_level];
            float baseY = view.yMax - thick;

            // storm events
            if (dt > 0f && (_next -= dt) <= 0f)
            {
                if (_burst > 0) { _burst--; _next = Random.Range(0.12f, 0.25f); FarBolt(v, view, baseY, true); }
                else { _next = Random.Range(IntervalOf[_level].x, IntervalOf[_level].y); Event(v, view, baseY); }
            }

            // the bank: rolls with the wind, wraps outside the view
            float drift = (0.004f + 0.004f * _level) * dt;
            float w = view.width;
            _back.Begin(); _body.Begin(); _glow.Begin();
            Color body = Color.Lerp(new Color(0.16f, 0.08f, 0.1f, 1f), new Color(0.22f, 0.07f, 0.09f, 1f), _level / 3f);
            for (int i = 0; i < Puffs; i++)
            {
                _u[i] += drift;
                if (_u[i] > 0.8f) _u[i] -= 1.6f;
                float r = thick * _r[i] * (1f + 0.06f * Mathf.Sin(_ph[i] + _t * 0.9f));
                Vector2 p = new(view.center.x + _u[i] * w, view.yMax - thick * (0.15f + 0.6f * _v[i]) + 0.04f * Mathf.Sin(_ph[i] * 2f + _t * 0.6f));
                Vfx.SoftBlob(_back, p + Vector2.down * 0.05f, r * 1.35f, r * 0.75f, Vfx.CloudDark.WithA(0.9f), 0.45f, 18);
                Vfx.SoftBlob(_body, p, r * 1.1f, r * 0.6f, body.WithA(0.85f), 0.45f, 18);
                Vfx.SoftBlob(_body, p + new Vector2(0f, r * 0.22f), r * 0.65f, r * 0.3f, new Color(0.3f, 0.2f, 0.22f, 0.25f), 0.2f, 14);
            }
            // the under-glow: a dim steady red, and every flash
            float steady = 0.04f + 0.035f * _level;
            for (int i = 0; i < Puffs; i += 3)
            {
                Vector2 p = new(view.center.x + _u[i] * w, baseY + 0.05f);
                Vfx.SoftBlob(_glow, p, thick * 1.3f, thick * 0.35f, Vfx.Crimson.WithA(steady * (0.7f + 0.3f * Mathf.Sin(_ph[i] + _t * 1.3f))), 0.1f, 16);
            }
            for (int i = _flashes.Count - 1; i >= 0; i--)
            {
                var f = _flashes[i];
                f.age += dt;
                if (f.age >= f.life) { _flashes.RemoveAt(i); continue; }
                _flashes[i] = f;
                float e = Vfx.StrokeEnvelope(f.age / f.life) * f.amp;
                Vfx.SoftBlob(_glow, new Vector2(f.x, baseY + thick * 0.35f), f.rx, thick * 0.6f, Color.Lerp(Vfx.Crimson, Vfx.HotWhite, 0.4f).WithA(Mathf.Clamp01(e * 0.75f)), 0.15f, 20);
            }
            _back.End(); _body.End(); _glow.End();
        }

        void AddFlash(float x, float amp, float life, float rx = 2.2f) => _flashes.Add(new Flash { x = x, age = 0f, life = life, amp = amp, rx = rx });

        /// <summary>The kinds of storm event (<see cref="Play"/>).</summary>
        public enum StormEvent { Sheet, FarBolt, Crawler, NearStrike }

        /// <summary>
        /// Plays one storm event now at <paramref name="viewX"/> (-0.5..0.5 of the view's width from its centre), whatever the level allows: the review
        /// screenshots ask for each. Nothing without a <see cref="Vfx"/>.
        /// </summary>
        public void Play(StormEvent e, float viewX = 0.2f)
        {
            var v = Vfx.I;
            if (v == null) return;
            if (_root == null) Build(v);
            var view = Vfx.ViewRect(Vfx.ViewCam(_f));
            float baseY = view.yMax - ThicknessOf[_level];
            float x = view.center.x + viewX * view.width;
            switch (e)
            {
                case StormEvent.Sheet: AddFlash(x, 0.8f, 0.5f, 2.4f); break;
                case StormEvent.FarBolt: FarBolt(v, view, baseY, false, x); break;
                case StormEvent.Crawler: Crawler(v, x, baseY); break;
                default: NearStrike(v, view, x, baseY); break;
            }
        }

        void Event(Vfx v, Rect view, float baseY)
        {
            float roll = Random.value;
            float x = Random.Range(view.xMin + 0.6f, view.xMax - 0.6f);
            if (roll < SheetShareOf[_level]) { AddFlash(x, Random.Range(0.45f, 0.85f), 0.5f, Random.Range(1.6f, 2.8f)); return; }
            if (_level >= 2 && roll > 0.8f) { NearStrike(v, view, x, baseY); return; }
            if (_level >= 1 && roll > 0.68f) { Crawler(v, x, baseY); return; }
            FarBolt(v, view, baseY, false, x);
        }

        /// <summary>A bolt forking down out of the bank behind the far city, a glow at its foot, the bank lit over it.</summary>
        void FarBolt(Vfx v, Rect view, float baseY, bool big, float x = float.NaN)
        {
            if (float.IsNaN(x)) x = Random.Range(view.xMin + 0.6f, view.xMax - 0.6f);
            var top = new Vector2(x + Random.Range(-0.3f, 0.3f), baseY + 0.1f);
            var foot = new Vector2(x + Random.Range(-0.9f, 0.9f), Random.Range(0.35f, 1.3f));
            float k = big ? 1.4f : 1f;
            v.Bolt(top, foot, (0.022f + 0.005f * _level) * k, 0.42f, Vfx.Crimson.WithA(0.9f), 18, (top.y - foot.y) * 0.06f, 2 + _level, false, OrderFar, 0.75f, 0.6f);
            v.SkyGlow(foot + Vector2.up * 0.35f, 1.3f * k, 0.9f * k, new Color(1f, 0.3f, 0.35f, 0.35f), 0.42f, OrderFarGlow);
            AddFlash(top.x, k, 0.45f);
        }

        /// <summary>A bolt running sideways along the bank's underside.</summary>
        void Crawler(Vfx v, float x, float baseY)
        {
            float len = Random.Range(1.6f, 3f);
            var a = new Vector2(x - len, baseY + Random.Range(0.05f, 0.25f));
            var b = new Vector2(x + len, baseY + Random.Range(0.05f, 0.25f));
            if (Random.value < 0.5f) (a, b) = (b, a);
            v.Bolt(a, b, 0.016f, 0.38f, Vfx.Crimson.WithA(0.85f), 22, 0.12f, 3, false, OrderFar, 0.7f, 0.5f);
            AddFlash(x, 0.7f, 0.4f, len * 1.1f);
        }

        /// <summary>A strike on the roofs behind the arena: their ridges hide its foot; the whole backdrop lights up; a nudge of the camera.</summary>
        void NearStrike(Vfx v, Rect view, float x, float baseY)
        {
            var top = new Vector2(x + Random.Range(-0.2f, 0.2f), baseY + 0.1f);
            var foot = new Vector2(x + Random.Range(-0.5f, 0.5f), Random.Range(0.15f, 0.45f));
            v.Bolt(top, foot, 0.045f, 0.44f, Vfx.Crimson, 20, (top.y - foot.y) * 0.05f, 3, false, OrderNear, 0.9f, 0.8f);
            v.SkyGlow(foot + Vector2.up * 0.25f, 1.6f, 0.9f, new Color(1f, 0.35f, 0.4f, 0.45f), 0.44f, OrderNear - 8);
            v.BackdropFlash(0.12f + 0.03f * _level, 0.42f);
            AddFlash(x, 1.2f, 0.45f, 3f);
            GameFeel.Shake(0.04f);
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root);
            foreach (var r in new[] { _back, _body, _glow }) if (r != null && r.mesh != null) Destroy(r.mesh);
        }
    }
}
