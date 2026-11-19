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

        [Test]
        public void TheShippedDodgeTuningIsTheSpecsPerfectDodgeNumbers()
        {
            // dodge.json (SAN-92): the file and the in-code defaults agree, and they are the spec's numbers
            var file = Tuning.Get<DodgeTuning>();
            var code = new DodgeTuning();
            Assert.AreEqual(JsonUtility(code), JsonUtility(file), "dodge.json matches the DodgeTuning defaults");
            Assert.AreEqual(8, file.windowFrames, "about 8 frames");
            Assert.AreEqual(24, file.slowFrames, "about 0.4 s");
            Assert.AreEqual(0.4f, file.worldScale, "the glossary's 0.4 world speed");
            Assert.AreEqual(12f, file.meterGain, "the JS game's +12 Rage");
        }

        [Test]
        public void TheShippedEngagementTuningIsTheJsGroupFightNumbers()
        {
            // engagement.json (SAN-83): the file and the in-code defaults agree, and they are the JS stretch numbers
            var file = Tuning.Get<EngagementTuning>();
            var code = new EngagementTuning();
            Assert.AreEqual(JsonUtility(code), JsonUtility(file), "engagement.json matches the EngagementTuning defaults");
            Assert.AreEqual(2, file.maxAttackers, "JS STRETCH.maxEngaged");
            Assert.AreEqual(360f / 115f, file.waitDistance, 0.05f, "JS waitRing 360 px at 115 px a metre");
            Assert.Less(file.waitDistance - file.retreatInside, file.waitDistance + file.approachBeyond);
            Assert.Greater(file.behindPenalty, file.stickiness, "a foe behind needs more than the stickiness to turn Kage");
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
