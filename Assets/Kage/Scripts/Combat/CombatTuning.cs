namespace Kage
{
    /// <summary>Combat-wide switches (Resources/Tuning/combat.json).</summary>
    [System.Serializable, TuningFile("combat")]
    public class CombatTuning
    {
        /// <summary>
        /// The legacy switch: true restores the old hit resolution (distance checks of each hit point
        /// against the opponent and the extra target, inside the attacker's own tick) until the owner
        /// signs off the physics pass. Read when a fight is built.
        /// </summary>
        public bool legacyHitResolution;
    }
}
