using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Push collider (SAN-68): a frictionless vertical capsule on the fighter's own body, on the Push
    /// layer, which collides only with other push colliders. Physics keeps fighters apart and shares the
    /// push between them by mass. The push moves fighters by position only, as the old push did: a
    /// fighter's own horizontal speed survives the contact (<see cref="SettlePush"/>), so walking into
    /// someone pushes them at half the walking speed instead of stalling. During pass-through frames (and while down, thrown, KO or in the rage
    /// art) it sits on the PushPassThrough layer, which touches nothing. Under the legacy switch it is off
    /// and the fight loop's old manual push runs instead.
    /// </summary>
    public partial class Fighter
    {
        /// <summary>How far apart two fighters' centres are kept: the old manual push distance.</summary>
        public const float PushWidth = 0.72f;
        /// <summary>
        /// Capsule height. Fighters at the same height push like the old 0.72 x 1.1 m box; the rounded ends
        /// let a fighter who comes down on another's head slide off instead of standing on it.
        /// </summary>
        const float PushHeight = 1.1f, PushCentreY = 0.75f;

        CapsuleCollider2D _push;
        float _pushVx;

        void BuildPushCollider()
        {
            var go = new GameObject("Push") { layer = PhysicsLayers.Push };
            go.transform.SetParent(transform, false);
            _push = go.AddComponent<CapsuleCollider2D>();
            _push.direction = CapsuleDirection2D.Vertical;
            _push.sharedMaterial = PhysicsMaterials.NoFriction;
            _push.enabled = false;      // SyncPush turns it on once the fight knows its legacy switch
            SyncPushSize();
        }

        /// <summary>True when this fighter's push collider lets others through this frame.</summary>
        public bool PassesThrough =>
            state is FState.KO or FState.Thrown or FState.Ultimate || IsDown || (IsAttacking && move.PassThrough(moveFrame));

        /// <summary>
        /// Fight loop, after the hit phase and before physics runs: switches the push collider on or off
        /// (legacy switch) and between the Push and PushPassThrough layers for this frame.
        /// </summary>
        public void SyncPush()
        {
            if (_push == null) return;
            bool on = !LegacyPhysics;
            if (_push.enabled != on) _push.enabled = on;
            if (!on) return;
            int layer = PassesThrough ? PhysicsLayers.PushPassThrough : PhysicsLayers.Push;
            if (_push.gameObject.layer != layer) _push.gameObject.layer = layer;
            SyncPushSize();
            _pushVx = rb.linearVelocity.x;
        }

        /// <summary>
        /// Fight loop, first thing in a frame: if the last physics step pushed this fighter against
        /// another, gives back the horizontal speed it had before that step. The contact still moved both
        /// bodies apart (sharing the push by mass); only the speed change is undone, so the state machine
        /// keeps driving its own speed as it did under the old positional push.
        /// </summary>
        public void SettlePush()
        {
            if (_push == null || !_push.enabled || !rb.simulated || rb.bodyType != RigidbodyType2D.Dynamic) return;
            if (_push.gameObject.layer != PhysicsLayers.Push || !_push.IsTouchingLayers(1 << PhysicsLayers.Push)) return;
            rb.linearVelocity = new Vector2(_pushVx, rb.linearVelocity.y);
        }

        /// <summary>The capsule is sized in world metres whatever the fighter's scale (the Ronin is 1.04).</summary>
        void SyncPushSize()
        {
            Vector3 s = transform.lossyScale;
            float sx = Mathf.Abs(s.x) > 1e-6f ? Mathf.Abs(s.x) : 1f, sy = Mathf.Abs(s.y) > 1e-6f ? Mathf.Abs(s.y) : 1f;
            var size = new Vector2(PushWidth / sx, PushHeight / sy);
            var offset = new Vector2(0f, PushCentreY / sy);
            if (_push.size != size) _push.size = size;
            if (_push.offset != offset) _push.offset = offset;
        }
    }
}
