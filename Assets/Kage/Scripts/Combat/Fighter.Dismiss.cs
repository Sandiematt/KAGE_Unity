using UnityEngine;

namespace Kage
{
    /// <summary>Putting a fighter out of the fight for good (a stretch removes its fallen foes, SAN-89).</summary>
    public partial class Fighter
    {
        /// <summary>
        /// Takes the fighter out of play: a physical fall or knock-out ragdoll is put back on the rig (its container would otherwise
        /// be left behind) and the object is switched off, so it leaves every target list and hit test. The caller removes it from
        /// <c>ctx.Fighters</c> and destroys it.
        /// </summary>
        public void Dismiss()
        {
            if (_ragdoll != null) { _ragdoll.Restore(); _ragdoll = null; }
            AbortRageArt();
            gameObject.SetActive(false);
        }
    }
}
