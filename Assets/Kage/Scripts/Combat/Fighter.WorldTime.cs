using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The world time scale on a fighter's body (SAN-92, ADR-0002). A slowed fighter only takes a logic frame (input,
    /// tick) on the frames the clock's slowed world advances (FightLoop asks <see cref="FightClock.Advances"/>); its body
    /// keeps moving smoothly in between because the physics step sees a scaled copy of it:
    /// <list type="bullet">
    /// <item>Last thing in a frame (<see cref="ScaleWorldTime"/>): velocity × s and gravity × s². In world time
    /// <c>x(t) = X(s·t)</c>, so speed scales by s and acceleration by s²; over the 1/s frames one of its ticks lasts, the
    /// body covers exactly one normal frame's distance and gains one normal frame's fall speed.</item>
    /// <item>First thing in the next frame (<see cref="RestoreWorldTime"/>): velocity ÷ s and the gravity back, so all
    /// gameplay code (its own tick, a hit that knocks it back, the push) reads and writes normal, logic-unit velocities.</item>
    /// <item>At s = 0 (a freeze) the body is made kinematic and held still, and gets its velocity back on resume.</item>
    /// </list>
    /// The exempt fighter (Kage) is never scaled. Nothing changes at scale 1: both calls return at once.
    /// </summary>
    public partial class Fighter
    {
        float _worldApplied = 1f, _worldGravity;
        Vector2 _worldVelocity;
        bool _worldHeld;

        /// <summary>The time scale this fighter's body was given for the last physics step (1 when it wasn't slowed).</summary>
        public float WorldTimeScale => _worldApplied;

        /// <summary>
        /// The body's velocity in the fighter's own time: what its logic and its pose see. Between a scaled physics step
        /// and the next frame (LateUpdate in play, the harness's pose output) the rigidbody holds the scaled velocity.
        /// </summary>
        public Vector2 LogicVelocity
        {
            get
            {
                if (rb == null) return Vector2.zero;
                if (_worldHeld) return _worldVelocity;
                return _worldApplied < 1f ? rb.linearVelocity / _worldApplied : rb.linearVelocity;
            }
        }

        /// <summary>Fight loop, after the push sync and before physics: gives the body time scale <paramref name="s"/> for this physics step.</summary>
        public void ScaleWorldTime(float s)
        {
            if (s >= 1f || rb == null || !rb.simulated || rb.bodyType != RigidbodyType2D.Dynamic) return;
            _worldGravity = rb.gravityScale;
            if (s <= 0f)
            {
                _worldVelocity = rb.linearVelocity;
                _worldHeld = true;
                rb.linearVelocity = Vector2.zero;
                rb.bodyType = RigidbodyType2D.Kinematic;
                _worldApplied = 0f;
                return;
            }
            rb.linearVelocity *= s;
            rb.gravityScale = _worldGravity * s * s;
            _worldApplied = s;
        }

        /// <summary>Fight loop, first thing in a frame that runs: undoes <see cref="ScaleWorldTime"/>.</summary>
        public void RestoreWorldTime()
        {
            if (_worldApplied >= 1f) return;
            if (_worldHeld)
            {
                _worldHeld = false;
                if (rb.bodyType == RigidbodyType2D.Kinematic)
                {
                    rb.bodyType = RigidbodyType2D.Dynamic;
                    rb.linearVelocity = _worldVelocity;
                    rb.gravityScale = _worldGravity;
                }
            }
            else if (rb.simulated && rb.bodyType == RigidbodyType2D.Dynamic)
            {
                rb.linearVelocity /= _worldApplied;
                rb.gravityScale = _worldGravity;
            }
            _worldApplied = 1f;
        }
    }
}
