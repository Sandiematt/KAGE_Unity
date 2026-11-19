using System;

namespace Kage
{
    /// <summary>
    /// The left and right limit of the floor in a fight (SAN-89), in metres. The dev arena and the duel keep the default of
    /// ±<see cref="DefaultHalf"/>; a stretch session moves it as gates close and open (the road, an encounter's arena). Fighters
    /// clamp to it (<c>Fighter.ClampStage</c>), and so does what they do with it (teleports, rage arts).
    /// </summary>
    public sealed class StageBounds
    {
        public const float DefaultHalf = 7.6f;

        public float Left { get; private set; } = -DefaultHalf;
        public float Right { get; private set; } = DefaultHalf;

        /// <summary>Raised after the limits changed (the road moves its walls).</summary>
        public event Action Changed;

        public void Set(float left, float right)
        {
            if (right < left) right = left;
            if (left == Left && right == Right) return;
            Left = left;
            Right = right;
            Changed?.Invoke();
        }

        public void Reset() => Set(-DefaultHalf, DefaultHalf);
    }
}
