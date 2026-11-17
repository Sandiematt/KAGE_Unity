using System;
using System.IO;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    // SaveStore (ADR-0009) owns kage_save.json. Tests use a temporary folder, never the real persistent data path.
    public class SaveStoreTests
    {
        string dir;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "kage_save_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }

        string SavePath { get { return Path.Combine(dir, "kage_save.json"); } }

        [Test]
        public void A_saved_game_loads_back_in_a_new_session()
        {
            var first = new SaveStore(dir);
            var s = GameRules.DefaultSave(false);
            s.coins = 4321;
            s.owned.weapons["tsubame"] = 3;
            s.cleared[GameRules.Stages[0].Id] = true;
            first.Save(s);

            var loaded = new SaveStore(dir).Load();
            Assert.AreEqual(4321, loaded.coins);
            Assert.AreEqual(3, loaded.owned.weapons["tsubame"]);
            Assert.IsTrue(loaded.cleared[GameRules.Stages[0].Id]);
            Assert.IsFalse(loaded.devMode);
        }

        [Test]
        public void A_missing_file_gives_a_fresh_save_that_is_written_at_once()
        {
            var store = new SaveStore(dir);
            var s = store.Load();
            Assert.AreEqual(DevDefaults.DevModeOnFreshSave, s.devMode);
            Assert.IsTrue(File.Exists(SavePath));
            Assert.IsFalse(File.Exists(SavePath + ".corrupt"));
        }

        [Test]
        public void A_corrupt_file_gives_a_fresh_save_and_is_copied_aside()
        {
            File.WriteAllText(SavePath, "{ this is not json");
            var s = new SaveStore(dir).Load();

            Assert.AreEqual(1, s.owned.weapons["kagetsu"]);
            Assert.GreaterOrEqual(s.coins, 250);
            Assert.AreEqual("{ this is not json", File.ReadAllText(SavePath + ".corrupt"));
            // the file itself is a good save again
            SaveData again;
            string error;
            Assert.IsTrue(SaveSerializer.TryDeserialize(File.ReadAllText(SavePath), out again, out error), error);
        }

        [Test]
        public void An_empty_file_does_not_crash()
        {
            File.WriteAllText(SavePath, "");
            Assert.DoesNotThrow(() => new SaveStore(dir).Load());
        }

        [Test]
        public void A_missing_folder_is_created_when_saving()
        {
            var nested = Path.Combine(dir, "a", "b");
            var store = new SaveStore(nested);
            store.Save(GameRules.DefaultSave(false));
            Assert.IsTrue(File.Exists(Path.Combine(nested, "kage_save.json")));
        }

        [Test]
        public void Saving_leaves_no_temporary_file_behind()
        {
            var store = new SaveStore(dir);
            store.Save(GameRules.DefaultSave(false));
            store.Save(GameRules.DefaultSave(false));
            Assert.IsFalse(File.Exists(SavePath + ".tmp"));
        }

        [Test]
        public void A_leftover_temporary_file_from_a_crash_does_not_hide_the_real_save()
        {
            var store = new SaveStore(dir);
            var s = GameRules.DefaultSave(false);
            s.coins = 999;
            store.Save(s);
            File.WriteAllText(SavePath + ".tmp", "half a wri");
            Assert.AreEqual(999, new SaveStore(dir).Load().coins);
        }

        [Test]
        public void An_unwritable_location_does_not_throw()
        {
            // a file where the folder should be
            var blocker = Path.Combine(dir, "blocker");
            File.WriteAllText(blocker, "x");
            var store = new SaveStore(Path.Combine(blocker, "sub"));
            Assert.DoesNotThrow(() => store.Load());
            Assert.DoesNotThrow(() => store.Save(GameRules.DefaultSave(false)));
        }

        [Test]
        public void Dev_mode_save_is_topped_up_when_loaded()
        {
            File.WriteAllText(SavePath, "{\"devMode\":true,\"coins\":1}");
            var s = new SaveStore(dir).Load();
            Assert.GreaterOrEqual(s.coins, 99999);
            Assert.IsTrue(GameRules.Owns(s, GearKind.Armors, "mugen_yoroi"));
        }

        [Test]
        public void Turning_dev_mode_off_replaces_the_save_and_writes_it()
        {
            var store = new SaveStore(dir);
            var dev = store.SetDevMode(store.Load(), true);
            Assert.IsTrue(dev.devMode);

            var real = store.SetDevMode(dev, false);
            Assert.IsFalse(real.devMode);
            Assert.AreEqual(250, real.coins);

            var reloaded = new SaveStore(dir).Load();
            Assert.IsFalse(reloaded.devMode);
            Assert.AreEqual(250, reloaded.coins);
            Assert.AreEqual(1, reloaded.owned.weapons.Count);
        }
    }
}
