namespace Kage
{
    /// <summary>
    /// The physics layers (ADR-0007). Their names are in ProjectSettings/TagManager.asset; the
    /// collision rules are still set in code (FightBuilder.ConfigurePhysics) until SAN-68 moves the
    /// whole matrix into Physics2DSettings.
    /// </summary>
    public static class PhysicsLayers
    {
        public const int Fighter = 8, Ragdoll = 9, Ground = 10, Prop = 11;
        /// <summary>Hurt circles (fighters and props). Collides with nothing; only hit casts query it.</summary>
        public const int Hurtbox = 12;

        public const int HurtboxMask = 1 << Hurtbox;
    }
}
