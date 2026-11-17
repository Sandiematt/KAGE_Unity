using NUnit.Framework;

namespace Kage.Tests
{
    /// <summary>
    /// Tuning files (ADR-0001): Assets/Kage/Resources/Tuning/&lt;feature&gt;.json, one plain class each,
    /// read with Tuning.Get. The legacy switch is the first entry (combat.json).
    /// </summary>
    public class TuningTests
    {
        [TearDown]
        public void TearDown() => Tuning.ClearOverrides();

        [Test]
        public void TheShippedCombatTuningUsesTheNewHitResolution()
        {
            Assert.IsFalse(Tuning.Get<CombatTuning>().legacyHitResolution);
        }

        [Test]
        public void AnOverrideReplacesTheFileUntilCleared()
        {
            Tuning.Override(new CombatTuning { legacyHitResolution = true });
            Assert.IsTrue(Tuning.Get<CombatTuning>().legacyHitResolution);

            Tuning.ClearOverrides();
            Assert.IsFalse(Tuning.Get<CombatTuning>().legacyHitResolution);
        }

        [Test]
        public void AMissingFileMeansTheInCodeDefaults()
        {
            var t = Tuning.Get<NoFileTuning>();
            Assert.AreEqual(7, t.value);
        }

        [TuningFile("san65_no_such_file")]
        [System.Serializable]
        public class NoFileTuning { public int value = 7; }
    }
}
