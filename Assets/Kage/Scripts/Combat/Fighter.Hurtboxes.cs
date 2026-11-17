using System.Collections.Generic;

namespace Kage
{
    /// <summary>This fighter's hurt circles as <see cref="Hurtbox"/> triggers on its bones (ADR-0007, SAN-65).</summary>
    public partial class Fighter
    {
        readonly List<Hurtbox> _hurtboxes = new();

        /// <summary>One hurtbox per hurt circle of the rig, under a kinematic carrier on the rig root.</summary>
        void BuildHurtboxes()
        {
            Hurtbox.AddCarrier(rig.gameObject);
            for (int i = 0; i < def.hurt.Count; i++)
            {
                var h = def.hurt[i];
                _hurtboxes.Add(Hurtbox.Add(rig.Bone(h.bone), h.local, h.radius, h.zone, i, this));
            }
        }

        void SetHurtboxesActive(bool on)
        {
            foreach (var h in _hurtboxes) if (h != null) h.Collider.enabled = on;
        }
    }
}
