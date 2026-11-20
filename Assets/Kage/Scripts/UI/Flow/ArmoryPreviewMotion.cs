using System;
using UnityEngine;

namespace Kage
{
    /// <summary>Where the weapon illustration of the Armory's preview is at a moment, in the preview's own px (y down) and radians.</summary>
    public struct BladePose
    {
        public float X, Y, Angle, ScaleY;
    }

    /// <summary>
    /// The slow turn of the weapon illustration on the Armory's Weapons tab (JS startPreview): the blade sways by 0.08 rad about -0.48, bobs 6 px up
    /// and down and turns about its length (a vertical squash between 50% and 100%) in front of the sun. A pure function of time and the blade's drawn length.
    /// </summary>
    public static class ArmoryPreviewMotion
    {
        /// <summary>The preview canvas of the JS Armory (left 0, top 96, 1000 x 640 design px).</summary>
        public const float Width = 1000f, Height = 640f;

        /// <summary>The drawn length of a weapon: <c>min(w * 0.78, (len || 80) * 4.1)</c>.</summary>
        public static float DrawnLength(int len)
        {
            return Math.Min(Width * 0.78f, (len > 0 ? len : 80) * 4.1f);
        }

        public static BladePose At(float t, float length)
        {
            float ang = -0.48f + (float)Math.Sin(t * 0.9) * 0.08f;
            float sx = (float)Math.Cos(t * 0.7) * 0.25f + 0.75f;
            return new BladePose
            {
                Angle = ang,
                ScaleY = sx,
                X = Width * 0.5f - length * 0.42f * (float)Math.Cos(ang),
                Y = Height * 0.5f - length * 0.42f * (float)Math.Sin(ang) + (float)Math.Sin(t * 1.3) * 6f,
            };
        }
    }

    /// <summary>Moves the blade illustration of the Armory preview on <see cref="ArmoryPreviewMotion"/> and sweeps the faint light across the picture.</summary>
    [AddComponentMenu("")]
    public sealed class ArmoryBladeSway : MonoBehaviour
    {
        RectTransform blade, shimmer;
        float length, born;

        public static ArmoryBladeSway Build(RectTransform holder, Texture2D art, int weaponLen)
        {
            // the illustration is 1200 x 600 with the weapon's origin at 350 / 600 across: that point is the node's pivot
            var node = Ui.Node(holder, "Blade", 0, 0, 1200, 600);
            node.pivot = new Vector2(350f / 1200f, 0.5f);
            var ri = node.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            ri.texture = art;
            ri.raycastTarget = false;
            var sh = Ui.Node(holder, "Shimmer", 0, 0, 220, 1100);
            sh.pivot = new Vector2(0.5f, 0.5f);
            var g = sh.gameObject.AddComponent<GradRect>();
            g.raycastTarget = false;
            g.Set(true, new[] { new Color(0.93f, 0.9f, 0.83f, 0f), new Color(0.93f, 0.9f, 0.83f, 0.07f), new Color(0.93f, 0.9f, 0.83f, 0f) });
            sh.localRotation = Quaternion.Euler(0f, 0f, -18f);
            var s = holder.gameObject.AddComponent<ArmoryBladeSway>();
            s.blade = node; s.shimmer = sh;
            s.length = ArmoryPreviewMotion.DrawnLength(weaponLen);
            s.born = UiClock.Now;
            s.Tick();
            return s;
        }

        void Update() { Tick(); }

        void Tick()
        {
            if (blade == null) return;
            float t = UiClock.Animate ? UiClock.Now - born : 0f;
            var p = ArmoryPreviewMotion.At(t, length);
            blade.anchoredPosition = new Vector2(p.X, -p.Y);
            blade.localRotation = Quaternion.Euler(0f, 0f, -p.Angle * Mathf.Rad2Deg);
            blade.localScale = new Vector3(1f, p.ScaleY, 1f);
            float sp = UiClock.Animate ? (t * 0.35f) % 1f : 0.3f;
            shimmer.anchoredPosition = new Vector2(Mathf.Lerp(-150f, ArmoryPreviewMotion.Width + 150f, sp), -ArmoryPreviewMotion.Height * 0.5f);
        }
    }
}
