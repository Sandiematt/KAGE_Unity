using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Counts the fight's frames (60 Hz) and, on a manual clock, owns hitstop and whole-screen slow
    /// motion in frames (ADR-0002).
    /// In play (<see cref="Manual"/> false) GameFeel still freezes and slows the game through
    /// Time.timeScale on wall-clock time, exactly as before, and every loop step runs.
    /// In the headless harness (<see cref="Manual"/> true) GameFeel forwards here: hitstop of N frames
    /// skips N loop steps, and slow motion at scale s runs a step on s of the frames.
    /// The world time scale (SAN-92) is not built yet.
    /// </summary>
    public sealed class FightClock
    {
        public int Frame { get; private set; }
        public bool Manual { get; }

        int _hitstop, _slowFrames;
        float _slowScale = 1f, _slowAcc;

        public FightClock(bool manual) { Manual = manual; }

        /// <summary>Freezes the next <paramref name="frames"/> frames (the longest request wins).</summary>
        public void Hitstop(int frames) => _hitstop = Mathf.Max(_hitstop, frames);

        /// <summary>Whole-screen slow motion for <paramref name="frames"/> frames; replaces any running one.</summary>
        public void SlowMo(float scale, int frames)
        {
            _slowScale = Mathf.Clamp01(scale);
            _slowFrames = frames;
            _slowAcc = 0f;
        }

        public bool InHitstop => _hitstop > 0;

        /// <summary>
        /// Called by the fight loop at the start of a frame. Returns false when this frame is frozen
        /// (hitstop) or skipped (slow motion), in which case the loop only ends the frame.
        /// </summary>
        public bool BeginFrame()
        {
            if (!Manual) return true;
            if (_hitstop > 0)
            {
                _hitstop--;
                if (_slowFrames > 0) _slowFrames--;    // wall-clock slow motion keeps running under a freeze
                return false;
            }
            if (_slowFrames > 0)
            {
                _slowFrames--;
                _slowAcc += _slowScale;
                if (_slowAcc < 1f) return false;
                _slowAcc -= 1f;
            }
            return true;
        }

        /// <summary>Ends the frame: <see cref="Frame"/> counts every frame, frozen or not.</summary>
        public void EndFrame() => Frame++;
    }
}
