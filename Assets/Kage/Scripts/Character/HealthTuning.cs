namespace Kage
{
    /// <summary>
    /// The health scale (Resources/Tuning/health.json, ADR-0010): the one shared multiplier from JS health to
    /// Unity health. Every fighter starts with jsHealth × healthMul × healthScale. The default 100 / 232 keeps
    /// Kage's fresh-save health (JS 232) at 100. Unity's per-window damage values don't change with it.
    /// </summary>
    [System.Serializable, TuningFile("health")]
    public class HealthTuning
    {
        public const double Default = 100.0 / 232.0;
        public double healthScale = Default;

        /// <summary>Per-character pacing multipliers (SAN-110): a foe's <see cref="CharacterDef.healthMul"/>. Unlisted characters are 1.</summary>
        public System.Collections.Generic.List<CharacterHealthMul> multipliers = new System.Collections.Generic.List<CharacterHealthMul>();

        /// <summary>The multiplier of the character with this id in health.json (1 when it has none).</summary>
        public static float MulOf(string id)
        {
            foreach (var m in Tuning.Get<HealthTuning>().multipliers)
                if (m.id == id) return m.mul;
            return 1f;
        }
    }

    /// <summary>One row of <see cref="HealthTuning.multipliers"/>.</summary>
    [System.Serializable]
    public class CharacterHealthMul
    {
        public string id;
        public float mul = 1f;
    }
}
