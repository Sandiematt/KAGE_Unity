namespace Kage
{
    /// <summary>
    /// The physics layers (ADR-0007). Their names are in ProjectSettings/TagManager.asset and the
    /// collision matrix is in ProjectSettings/Physics2DSettings.asset (SAN-68); docs/physics.md lists both.
    /// No code sets layer collisions.
    /// </summary>
    public static class PhysicsLayers
    {
        public const int Fighter = 8, Ragdoll = 9, Ground = 10, Prop = 11;
        /// <summary>Hurt circles (fighters and props). Collides with nothing; only hit casts query it.</summary>
        public const int Hurtbox = 12;
        /// <summary>Fighter push colliders: they collide only with each other, so fighters can't overlap.</summary>
        public const int Push = 13;
        /// <summary>A push collider during pass-through frames (and while down, thrown, KO or in the rage art): collides with nothing.</summary>
        public const int PushPassThrough = 14;
        /// <summary>Projectile bodies (SAN-75): collide with the ground only.</summary>
        public const int Projectile = 15;
        /// <summary>Hazard areas (SAN-75): collide with nothing; queried by overlap only.</summary>
        public const int Hazard = 16;

        public const int HurtboxMask = 1 << Hurtbox;
    }
}
