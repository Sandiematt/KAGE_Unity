using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// A fight starts with Kage built from the save's equipped weapon, helmet and armour and their levels (SAN-84): in the campaign
    /// flow (the stage world behind the loadout and the stage) and in the dev arena, drawn gear included.
    /// </summary>
    public class GearLoadoutFlowTests
    {
        const float Scale = 100f / 232f;
        string dir;
        GameFlow flow;
        GameObject game;
        System.Func<SaveData> savedSource;

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "kage_gear_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            savedSource = KageGame.SaveSource;
        }

        [TearDown]
        public void TearDown()
        {
            if (flow != null) Object.DestroyImmediate(flow.gameObject);
            if (game != null) Object.DestroyImmediate(game);
            foreach (var cam in Object.FindObjectsByType<Camera>()) Object.DestroyImmediate(cam.gameObject);
            Time.timeScale = 1f;
            GameFeel.paused = false;
            KageGame.SaveSource = savedSource;
            try { Directory.Delete(dir, true); } catch { }
        }

        /// <summary>A save wearing a drawn helmet and a drawn armour, upgraded, with a level-6 Shizuka, Kagetsu and Onikiri owned.</summary>
        static SaveData Loadout()
        {
            var s = GameRules.DefaultSave(false);
            s.level = 4;
            s.owned.weapons["shizuka"] = 6;
            s.owned.weapons["onikiri"] = 2;
            s.owned.armors["ronin_yoroi"] = 4;
            s.equipped.weapon = "shizuka";
            s.equipped.hairstyle = "messy_layers";
            s.equipped.armor = "ronin_yoroi";
            return s;
        }

        const float JsHealth = 256f, Defence = 0.096f;

        static Renderer[] Drawn(Fighter f, string prefix) =>
            f.rig.GetComponentsInChildren<Renderer>(true).Where(r => r.name.StartsWith(prefix) && !r.name.EndsWith("_Rim")).ToArray();

        static void AssertBuiltFrom(Fighter k, string weapon, int weaponLevel)
        {
            Assert.AreEqual(weapon, k.WeaponId);
            Assert.AreEqual(weaponLevel, k.WeaponLevel);
            Assert.AreEqual(JsHealth * Scale, k.maxHealth, 1e-3f);
            Assert.AreEqual(Defence, k.Defense, 1e-5f);
            Assert.AreEqual(0.1f, k.DashBonus, 1e-5f);
            Assert.IsNotEmpty(Drawn(k, "Hair_messy_layers"), "the selected hairstyle is drawn");
            Assert.IsNotEmpty(Drawn(k, "Armor_"), "the armour is drawn");
        }

        IEnumerator Until(System.Func<bool> cond, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!cond())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("timed out waiting for " + what);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheCampaignFightBuildsKageFromTheSavesLoadout()
        {
            new SaveStore(dir).Save(Loadout());
            flow = GameFlow.Create(dir);
            yield return null;
            flow.Controller.OpenCampaign();
            Assert.IsTrue(flow.Controller.BeginLoadout());
            AssertBuiltFrom(flow.World.Player, "shizuka", 6);        // the tableau behind the loadout

            Assert.IsTrue(flow.Controller.StartStage());
            yield return Until(() => flow.World.phase == StageWorld.Phase.Fight, 6f, "the fight to begin");
            AssertBuiltFrom(flow.World.Player, "shizuka", 6);
        }

        [UnityTest]
        public IEnumerator ChoosingAnotherWeaponAtTheLoadoutIsTheWeaponHeFightsWith()
        {
            new SaveStore(dir).Save(Loadout());
            flow = GameFlow.Create(dir);
            yield return null;
            flow.Controller.OpenCampaign();
            flow.Controller.BeginLoadout();
            Assert.AreEqual("shizuka", flow.World.Player.WeaponId);

            Assert.IsTrue(flow.Controller.EquipWeapon("onikiri"));
            Assert.AreEqual("onikiri", flow.World.Player.WeaponId, "the tableau follows the pick");
            Assert.AreEqual(2, flow.World.Player.WeaponLevel);

            flow.Controller.StartStage();
            yield return Until(() => flow.World.phase == StageWorld.Phase.Fight, 6f, "the fight to begin");
            Assert.AreEqual("onikiri", flow.World.Player.WeaponId);
            Assert.AreEqual(2, flow.World.Player.WeaponLevel);
        }

        [UnityTest]
        public IEnumerator TheDevArenaBuildsKageFromTheSavesLoadoutToo()
        {
            KageGame.SaveSource = Loadout;
            game = new GameObject("KAGE");
            var arena = game.AddComponent<KageGame>();
            yield return null;
            AssertBuiltFrom(arena.player, "shizuka", 6);
            Assert.AreEqual("kage", arena.player.FighterId);
        }

        [UnityTest]
        public IEnumerator TheDevArenaOnTheStarterSaveIsTheKageItAlwaysHas()
        {
            game = new GameObject("KAGE");
            var arena = game.AddComponent<KageGame>();
            yield return null;
            Assert.AreEqual(GameRules.StarterWeaponId, arena.player.WeaponId);
            Assert.AreEqual(1, arena.player.WeaponLevel);
            Assert.AreEqual(100f, arena.player.maxHealth, 1e-3f);
            Assert.IsNotEmpty(Drawn(arena.player, "Hair_spike"));
            Assert.IsEmpty(Drawn(arena.player, "Armor_"));
        }
    }
}
