namespace Kage
{
    /// <summary>Combat-wide switches (Resources/Tuning/combat.json).</summary>
    [System.Serializable, TuningFile("combat")]
    public class CombatTuning
    {
        /// <summary>
        /// The legacy switch: true restores the old hit and push resolution until the owner signs off the
        /// physics pass: distance checks of each hit point against the opponent and the extra target,
        /// inside the attacker's own tick (SAN-65); the fight loop's manual push instead of push colliders,
        /// and knockback that ignores mass (SAN-68). Read when a fight is built.
        /// </summary>
        public bool legacyHitResolution;
    }
}
