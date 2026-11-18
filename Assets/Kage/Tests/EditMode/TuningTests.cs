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
        public void TheShippedGuardTuningIsTheSpecsDefenceNumbers()
        {
            // guard.json (SAN-86): the file and the in-code defaults agree, and they are the spec's numbers
            var file = Tuning.Get<GuardTuning>();
            var code = new GuardTuning();
            Assert.AreEqual(JsonUtility(code), JsonUtility(file), "guard.json matches the GuardTuning defaults");
            Assert.AreEqual(1.5f, file.blockPostureFactor, "posture on block: damage x about 1.5");
            Assert.AreEqual(60, file.drainDelayFrames, "drains after about 1 s");
            Assert.AreEqual(45, file.breakFrames, "about 0.75 s helpless");
            Assert.Less(file.hitPostureFactor, file.blockPostureFactor, "clean hits add a little");
        }

        static string JsonUtility(object o) => UnityEngine.JsonUtility.ToJson(o);

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
