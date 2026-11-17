using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The shared physics materials, as assets under Assets/Kage/Resources/Physics (SAN-68; docs/physics.md).
    /// </summary>
    public static class PhysicsMaterials
    {
        static PhysicsMaterial2D _noFriction, _ragdoll;

        /// <summary>Fighter body boxes and push colliders: friction 0, bounciness 0.</summary>
        public static PhysicsMaterial2D NoFriction => _noFriction != null ? _noFriction : _noFriction = Load("NoFriction");

        /// <summary>KO ragdoll limbs: friction 0.7, bounciness 0.12.</summary>
        public static PhysicsMaterial2D Ragdoll => _ragdoll != null ? _ragdoll : _ragdoll = Load("Ragdoll");

        static PhysicsMaterial2D Load(string name)
        {
            var m = Resources.Load<PhysicsMaterial2D>("Physics/" + name);
            if (m == null) Debug.LogError($"Missing physics material Resources/Physics/{name}.physicsMaterial2D");
            return m;
        }
    }
}
