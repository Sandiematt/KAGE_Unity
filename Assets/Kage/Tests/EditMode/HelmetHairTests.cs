using System.Linq;
using NUnit.Framework;

namespace Kage.Tests
{
    /// <summary>
    /// Kage's own hair under each helmet (SAN-72 rework: the owner saw his hair poking through some helmets). Seam: the equipped
    /// helmet and armour on his <see cref="CharacterDef"/> -> the rig his part set builds. A helmet "hides" the hair when no
    /// part of the built rig is hair: neither the soft hair part nor any part named for hair.
    /// The JS (<c>21-characters/kage/gear/head.js</c>, <c>sub-bosses/*/head.js</c>) draws a closed kabuto, a hood or a cap over the
    /// head; only a band (Hachigane), the open Oni mask and the straw hat's lower hair leave it showing.
    /// </summary>
    public class HelmetHairTests
    {
        static readonly string[] ArmourIds =
        {
            null, "tachibana_yoroi", "ronin_yoroi", "yami_yoroi", "shinsen_yoroi", "raiko_yoroi", "kagehira_yoroi", "yurei_yoroi", "mugen_yoroi",
            "ikazuchi_do", "shiden_yoroi", "shirabe_do", "kasane_haori", "tetsumon_do", "kudamaki_do", "mei_en_garb", "kysmei_robe", "kurokumo_yoroi", "shinsei_yoroi",
        };

        static RigDef Kage(string helmet, string armor = null)
        {
            var def = KageCharacter.Def();
            def.helmet = helmet;
            def.armor = armor;
            return new KagePartSet().Build(def);
        }

        static bool ShowsHair(RigDef d) => d.parts.Any(p => p.soft == SoftKind.Hair || p.name.Contains("Hair"));

        [TestCase("shiro_maki", true)]
        [TestCase("kuro_zukin", false)]
        [TestCase("sugegasa", true)]        // the hair that hangs below the brim
        [TestCase("hachigane", true)]       // a band round the brow
        [TestCase("kasumi_men", false)]
        [TestCase("tetsu_kabuto", false)]
        [TestCase("oni_menpo", true)]       // a brow plate and horns; the crown is open
        [TestCase("hoshi_kabuto", false)]
        [TestCase("shika_kabuto", false)]
        [TestCase("utsuro_kabuto", false)]
        [TestCase("shikko_zukin", false)]
        [TestCase("sumi_eboshi", false)]
        [TestCase("hisame_zukin", false)]
        [TestCase("raijin_kabuto", false)]
        public void EachHelmetHidesOrKeepsKagesHairAsTheJsDraws(string helmet, bool hairShows)
        {
            Assert.AreEqual(hairShows, ShowsHair(Kage(helmet)), helmet);
        }

        [Test]
        public void NoHelmetAtAllLeavesHisHairExactlyAsItWas()
        {
            var bare = KageModel.Build(KageModel.Look.Kage);
            var hair = bare.parts.First(p => p.soft == SoftKind.Hair);
            var h = Kage(null).parts.Single(p => p.soft == SoftKind.Hair);
            Assert.AreEqual(hair.polys.Count, h.polys.Count);
            Assert.AreEqual(hair.order, h.order);
        }

        [Test]
        public void AHairHidingHelmetHidesTheHairWhateverArmourIsWorn()
        {
            foreach (var armor in ArmourIds)
                foreach (var helmet in new[] { "kuro_zukin", "tetsu_kabuto", "hoshi_kabuto", "shika_kabuto", "utsuro_kabuto", "raijin_kabuto" })
                    Assert.IsFalse(ShowsHair(Kage(helmet, armor)), $"{helmet} over {armor}");
        }

        [Test]
        public void AHairHidingHelmetLeavesABlackHeadStandingWhereTheHairWas()
        {
            var hairOrder = KageModel.Build(KageModel.Look.Kage).parts.First(p => p.soft == SoftKind.Hair).order;
            foreach (var helmet in new[] { "tetsu_kabuto", "shika_kabuto", "utsuro_kabuto" })
            {
                var head = Kage(helmet).parts.Single(p => p.name == "Head" && p.bone == B.Head);
                Assert.AreEqual(hairOrder, head.order, helmet);
                Assert.AreEqual(SoftKind.None, head.soft, helmet);
            }
        }
    }
}
