using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Physical falls (SAN-113): a launch, knockdown, trip or sweep turns the rig into a tethered
    /// <see cref="Ragdoll2D"/> for <see cref="FState.Launched"/> and <see cref="FState.Down"/>. The gameplay body
    /// (its arc, bounce, landing and the down timer) is unchanged; the ragdoll follows it: the hit's momentum and
    /// the tumble's spin start it, the hips are pulled to where the canned pose had them and the torso is turned
    /// towards the gameplay tumble angle, while the limbs flail, hit the ground and settle as physics. The
    /// get-up starts from the captured fallen pose. KO lets the tether go (the full ragdoll). The legacy switch
    /// keeps the canned fall.
    /// </summary>
    public partial class Fighter
    {
        /// <summary>True while the rig is a tethered physics ragdoll (launched or down).</summary>
        public bool FallingPhysically => _ragdoll != null && _ragdoll.Tethered;

        /// <summary>The ragdoll body of bone <paramref name="b"/> while the rig is physics (fall or KO), else null.</summary>
        public Rigidbody2D FallBody(B b) => _ragdoll != null && _ragdoll.bodies.TryGetValue(b, out var r) ? r : null;

        /// <summary>Height of the hips above the feet when lying (the lie pose's hips, m at body scale 1).</summary>
        const float LieHipsHeight = 0.2f;

        void StartPhysicalFall(Vector2 v)
        {
            if (LegacyPhysics || rig == null) return;
            // root-space degrees a second, mirrored into the world; no more than the turn still left to the rest angle over a quarter second,
            // so the bodies don't overshoot the lying angle the tether holds (docs/combat-flow.md)
            float left = _tumbleTo - _tumbleAng;
            float spin = facing * Mathf.Sign(left) * Mathf.Min(Mathf.Abs(_tumbleVel), Mathf.Abs(left) * 4f);
            if (_ragdoll == null) _ragdoll = Ragdoll2D.ActivateFall(rig, v, spin, Gravity);
            else if (_ragdoll.Tethered) _ragdoll.AddMotion(v - _ragdoll.bodies[B.Hips].linearVelocity, spin * 0.5f);
        }

        /// <summary>Each fall frame, before the physics step: pulls the ragdoll after the gameplay body.</summary>
        void DriveFall()
        {
            if (!FallingPhysically) return;
            float s = Mathf.Abs(transform.lossyScale.y);
            Vector2 pivot = def.pivot[(int)B.Hips];
            Vector2 local;
            float angle, settle = 0f, turn;
            if (state == FState.Down)
            {
                var lie = _liePose ?? _lie;
                local = new Vector2(pivot.x + lie.hip.x, pivot.y + lie.hip.y);
                angle = lie.a[(int)B.Hips];
                // the impact: the trunk is pulled flat first (the back or the chest takes the floor, never a sit-up on straight legs),
                // the loose limbs fold and bounce, then the muscles ease the body into the authored lie
                settle = Mathf.Clamp01((stateFrame - 4) / 16f);
                turn = Mathf.Lerp(2.2f, 1f, settle);
                _ragdoll.DrivePose(lie, facing, Mathf.Lerp(10f, 130f, settle * settle), Mathf.Lerp(2.5f, 16f, settle));
            }
            else
            {
                // the hips come down to lying height as the body nears the floor, so it lands on its back or front
                float h = Mathf.Lerp(LieHipsHeight, pivot.y, Mathf.Clamp01(rb.position.y / 0.7f));
                local = new Vector2(pivot.x, h);
                // coming down near the floor, the trunk turns ahead of the tumble to the side it will lie on, so a low fall (a sweep)
                // lands on its back or front instead of sitting up with its legs out
                float near = rb.linearVelocity.y < 0f ? 1f - Mathf.Clamp01(rb.position.y / 0.8f) : 0f;
                angle = Mathf.Lerp(_tumbleAng, _tumbleTo, near);
                turn = 1f + near;
                // loose: the limbs trail and flail round a soft, curved tumble shape (onto the back, or the front)
                _ragdoll.DrivePose(_tumbleTo < 0f ? _tumbleFront : _tumble, facing, 45f, 6f);
            }
            Vector2 target = rb.position + new Vector2(local.x * facing, local.y) * s;
            // once settled, a firmer pull slides the body (against the ground's friction) to where it lies in gameplay
            _ragdoll.Drive(target, rb.linearVelocity, facing * angle, turn, 1f + 2f * settle);
        }

        /// <summary>The fall is over (get-up, stagger, round reset): the captured fallen pose becomes the rig's pose again.</summary>
        void EndPhysicalFall()
        {
            if (!FallingPhysically) return;
            _ragdoll.Capture(_display);
            _ragdoll.Restore();
            _ragdoll = null;
            rig.Apply(_display);
        }

        /// <summary>A ground hit on the lying body jolts it.</summary>
        void KickFall(HitInfo info)
        {
            if (FallingPhysically) _ragdoll.Kick(new Vector2(info.kb.x * 0.6f, 0.8f), info.contact);
        }
    }
}
