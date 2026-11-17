using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Push collider (SAN-68): a frictionless box on the fighter's own body, on the Push layer, which
    /// collides only with other push colliders. Physics keeps fighters apart and shares the push between
    /// them by mass. It follows the old push's rules:
    /// <list type="bullet">
    /// <item>Two fighters are kept <see cref="PushWidth"/> apart, sideways only. The box is taller than the
    /// push zone by its own width, so whenever two boxes touch they overlap more up and down than sideways,
    /// and physics always separates them sideways (nobody stands on a head or gets bumped up).</item>
    /// <item>A fighter <see cref="PushClearance"/> or more above another passes over it.</item>
    /// <item>The push moves fighters by position: a fighter's own horizontal speed survives the contact
    /// (<see cref="SettlePush"/>), so walking into an idle fighter pushes it at half the walking speed.</item>
    /// <item>During pass-through frames, and while down, thrown, KO or in the rage art, the box sits on
    /// the PushPassThrough layer, which touches nothing.</item>
    /// </list>
    /// Under the legacy switch the box is off and the fight loop's old manual push runs instead.
    /// </summary>
    public partial class Fighter
    {
        /// <summary>How far apart two fighters' centres are kept: the old manual push distance.</summary>
        public const float PushWidth = 0.72f;
        /// <summary>A fighter this far or more above another passes over it (the old push's height limit).</summary>
        public const float PushClearance = 1.1f;
        const float PushHeight = PushClearance + PushWidth, PushCentreY = 0.75f;

        BoxCollider2D _push;
        float _pushVx;

        void BuildPushCollider()
        {
            var go = new GameObject("Push") { layer = PhysicsLayers.Push };
            go.transform.SetParent(transform, false);
            _push = go.AddComponent<BoxCollider2D>();
            _push.sharedMaterial = PhysicsMaterials.NoFriction;
            _push.enabled = false;      // SyncPush turns it on once the fight knows its legacy switch
            SyncPushSize();
        }

        /// <summary>True when this fighter's push collider lets others through this frame (its own state).</summary>
        public bool PassesThrough =>
            state is FState.KO or FState.Thrown or FState.Ultimate || IsDown || (IsAttacking && move.PassThrough(moveFrame));

        /// <summary>True when this fighter is high enough above a fighter it could touch to pass over it.</summary>
        bool AboveAnother()
        {
            if (ctx == null) return false;
            Vector2 p = Pos;
            foreach (var o in ctx.Fighters)
            {
                if (o == this || o == null || o.rb == null || !o.isActiveAndEnabled) continue;
                Vector2 q = o.Pos;
                if (Mathf.Abs(q.x - p.x) < PushWidth * 1.5f && p.y - q.y >= PushClearance) return true;
            }
            return false;
        }

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
            int layer = PassesThrough || AboveAnother() ? PhysicsLayers.PushPassThrough : PhysicsLayers.Push;
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

        /// <summary>The box is sized in world metres whatever the fighter's scale (the Ronin is 1.04).</summary>
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
