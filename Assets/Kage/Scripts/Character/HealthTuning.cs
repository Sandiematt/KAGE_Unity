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
    }
}
