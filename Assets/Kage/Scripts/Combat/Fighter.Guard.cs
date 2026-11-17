using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Guard state and the guard branch of ReceiveHit.</summary>
    public partial class Fighter
    {
        /// <summary>The guard branch of <see cref="ReceiveHit"/>: true (and Blocked) when this fighter holds back and the hit's level allows blocking.</summary>
        bool TryGuard(HitInfo info, out HitResult result)
        {
            result = HitResult.Blocked;
            var w = info.w;
            bool canBlock = (state is FState.Idle or FState.Walk or FState.Crouch or FState.Blockstun) && Grounded && _guardHeld;
            if (canBlock && w.level != HitLevel.Unblockable)
            {
                bool ok = w.level switch { HitLevel.Low => _crouchHeld, HitLevel.Overhead => !_crouchHeld, _ => true };
                if (ok)
                {
                    SetState(FState.Blockstun);
                    _stun = Mathf.Max(8, Mathf.RoundToInt(w.stun * 0.6f));
                    rb.linearVelocity = new Vector2(info.kb.x * 0.55f + Mathf.Sign(info.kb.x) * 0.8f, rb.linearVelocity.y);
                    health = Mathf.Max(1f, health - (w.sword ? Mathf.Ceil(info.dmg * 0.15f) : 0f));
                    rig.Flash(new Color(0.85f, 0.9f, 1f), 0.35f);
                    return true;
                }
            }
            return false;
        }
    }
}
