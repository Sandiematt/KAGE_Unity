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
            float spin = facing * _tumbleVel;        // root-space degrees a second, mirrored into the world
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
            float angle;
            if (state == FState.Down)
            {
                var lie = _liePose ?? _lie;
                local = new Vector2(pivot.x + lie.hip.x, pivot.y + lie.hip.y);
                angle = lie.a[(int)B.Hips];
            }
            else
            {
                // the hips come down to lying height as the body nears the floor, so it lands on its back or front
                float h = Mathf.Lerp(LieHipsHeight, pivot.y, Mathf.Clamp01(rb.position.y / 0.7f));
                local = new Vector2(pivot.x, h);
                angle = _tumbleAng;
            }
            Vector2 target = rb.position + new Vector2(local.x * facing, local.y) * s;
            _ragdoll.Drive(target, rb.linearVelocity, facing * angle, 1f);
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
