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
    /// <para>
    /// The <b>world time scale</b> (SAN-92; Crimson Moon's freeze, SAN-82, reuses it) is counted here too, in play and on a
    /// manual clock alike: while it is below 1, every fighter, projectile and hazard except the exempt fighter's own runs
    /// slow. One shared accumulator gains <see cref="WorldScale"/> on each frame the loop runs; on the frames it passes 1
    /// (<see cref="WorldRuns"/>) the slowed fighters poll and tick, and hazards count a frame. Physics in between is
    /// scaled by the loop (FightLoop.ScaleWorldBodies), projectiles move by <see cref="ScaleFor"/> of a frame each frame.
    /// The exempt fighter and what it owns run every frame at full speed. Time.timeScale stays hitstop's and whole-screen
    /// slow motion's.
    /// </para>
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

        // ---- the world time scale (SAN-92, ADR-0002) ----
        int _worldLeft;
        float _worldAcc;

        /// <summary>1 normal; below 1 the world (everything but <see cref="WorldExempt"/>) runs at this speed; 0 holds it still.</summary>
        public float WorldScale { get; private set; } = 1f;
        /// <summary>The fighter that keeps full speed while the world is slow (Kage), and with it its projectiles and hazards.</summary>
        public Fighter WorldExempt { get; private set; }
        /// <summary>True on the frames the slowed world advances (always, at scale 1). Set by <see cref="BeginFrame"/>.</summary>
        public bool WorldRuns { get; private set; } = true;
        /// <summary>Loop frames the slow world has left; -1 until <see cref="ResumeWorld"/>, 0 when the world runs normally.</summary>
        public int WorldFramesLeft => WorldScale < 1f ? _worldLeft : 0;
        /// <summary>How far the slowed world is into its next frame (0..1): what a slowed fighter's pose interpolates by.</summary>
        public float WorldPhase => WorldScale < 1f ? _worldAcc : 0f;

        /// <summary>
        /// Slows the world to <paramref name="scale"/> (clamped to 0..1) for the next <paramref name="frames"/> loop frames, or
        /// until <see cref="ResumeWorld"/> when negative; <paramref name="exempt"/> keeps full speed. Replaces a running one.
        /// It starts with the physics step of the frame it is called in, and counts only frames the loop runs (not hitstop).
        /// </summary>
        public void SlowWorld(float scale, int frames, Fighter exempt)
        {
            scale = Mathf.Clamp01(scale);
            if (frames == 0 || scale >= 1f) { ResumeWorld(); return; }
            if (WorldScale >= 1f) _worldAcc = 0f;
            WorldScale = scale;
            WorldExempt = exempt;
            _worldLeft = frames;
        }

        /// <summary>The world runs at full speed again (from the next frame's physics on).</summary>
        public void ResumeWorld()
        {
            WorldScale = 1f;
            WorldExempt = null;
            _worldLeft = 0;
            _worldAcc = 0f;
            WorldRuns = true;
        }

        /// <summary>True when <paramref name="f"/> runs at full speed now: the world isn't slow, or it is the exempt fighter.</summary>
        public bool IsExempt(Fighter f) => WorldScale >= 1f || (f != null && ReferenceEquals(f, WorldExempt));

        /// <summary>The time scale of <paramref name="owner"/> and of what it owns: 1 when exempt, else <see cref="WorldScale"/>.</summary>
        public float ScaleFor(Fighter owner) => IsExempt(owner) ? 1f : WorldScale;

        /// <summary>True when <paramref name="owner"/> (or what it owns) takes a logic frame on this frame.</summary>
        public bool Advances(Fighter owner) => WorldRuns || IsExempt(owner);

        void AdvanceWorld()
        {
            if (WorldScale < 1f && _worldLeft == 0) ResumeWorld();
            if (WorldScale >= 1f) { WorldRuns = true; return; }
            if (_worldLeft > 0) _worldLeft--;
            _worldAcc += WorldScale;
            WorldRuns = _worldAcc >= 1f;
            if (WorldRuns) _worldAcc -= 1f;
        }

        /// <summary>
        /// Called by the fight loop at the start of a frame. Returns false when this frame is frozen
        /// (hitstop) or skipped (slow motion), in which case the loop only ends the frame.
        /// </summary>
        public bool BeginFrame()
        {
            if (!Manual) { AdvanceWorld(); return true; }
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
            AdvanceWorld();
            return true;
        }

        /// <summary>Ends the frame: <see cref="Frame"/> counts every frame, frozen or not.</summary>
        public void EndFrame() => Frame++;
    }
}
