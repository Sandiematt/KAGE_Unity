using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The dev arena builds Kage from the save file on disk (SAN-84). The PlayMode tests must never read the machine's real save,
    /// so for the whole suite the arena's loadout comes from a fresh save (the starter loadout the baseline was recorded with).
    /// A test that wants another loadout sets <see cref="KageGame.SaveSource"/> itself and restores it.
    /// </summary>
    [SetUpFixture]
    public class PlayModeSaveIsolation
    {
        System.Func<SaveData> _before;

        [OneTimeSetUp]
        public void Isolate()
        {
            _before = KageGame.SaveSource;
            KageGame.SaveSource = () => GameRules.DefaultSave(false);
        }

        [OneTimeTearDown]
        public void Restore() => KageGame.SaveSource = _before;
    }
}
