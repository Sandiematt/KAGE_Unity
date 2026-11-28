using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>Rage art data (SAN-82, docs/weapon-styles.md): which art each family and each shop weapon has, their looks, and Crimson Moon's tuning file.</summary>
    public class RageArtTests
    {
        [Test]
        public void The_katana_family_has_Crimson_Moon_and_every_other_family_Thousand_Shadows()
        {
            foreach (var id in new[] { "katana", "fists", "twin", "spear", "chain", "cleaver", "tachi" })
            {
                var fam = WeaponFamilies.Get(id);
                Assert.IsNotNull(fam, id);
                Assert.AreEqual(id == "katana" ? Fighter.CrimsonMoon : Fighter.ThousandShadows, fam.rageArt, id);
            }
        }

        [Test]
        public void Each_shop_weapon_has_its_own_art_and_the_rest_keep_their_familys()
        {
            // docs/weapon-styles.md: the weapon's style names its art; a weapon without a style plays its family's
            var arts = new System.Collections.Generic.Dictionary<string, string>
            {
                { "kagetsu", Fighter.CrimsonMoon }, { "shizuka", Fighter.SilentMoon }, { "tsubame", Fighter.SwallowStorm },
                { "onikiri", Fighter.HellSplitter }, { "mizuchi", Fighter.WaterDragon }, { "yamigama", Fighter.ReaperRequiem },
                { "iwakudaki", Fighter.Earthsplitter },
            };
            foreach (var kv in arts)
            {
                var style = WeaponStyles.Of(kv.Key);
                Assert.IsNotNull(style, kv.Key);
                Assert.AreEqual(kv.Value, style.rageArt, kv.Key);
                Assert.IsTrue(Fighter.IsCinematicArt(kv.Value), kv.Value + " has a cut-in and a finisher");
            }
            Assert.AreEqual(arts.Count, arts.Values.Distinct().Count(), "no two shop weapons share an art");
            foreach (var weapon in new[] { "fists", "raiko_no_tachi", "cutter_blade", "shadow_daggers" })
                Assert.IsNull(WeaponStyles.Of(weapon), weapon + " has no style: it plays its family's art");
            Assert.AreEqual(Fighter.Mikazuki, WeaponStyles.Of("mumei").rageArt, "the plain starter plays the basic art: one giant crescent moon");
            Assert.AreEqual("katana", WeaponFamilies.FamilyIdOf("shizuka"), "Shizuka still fights with the katana family's moves underneath");
        }

        [Test]
        public void Every_art_has_a_look_with_its_name_kanji_and_finisher_word()
        {
            // the JS RAGE_ARTS table and the words RageArts.finish stamps on the screen
            var want = new System.Collections.Generic.Dictionary<string, (string name, string kanji, string stamp)>
            {
                { Fighter.CrimsonMoon, ("Crimson Moon", "紅月", "MOONFALL") },
                { Fighter.SilentMoon, ("Silent Moon", "静月", "SILENCE") },
                { Fighter.SwallowStorm, ("Thousand Swallows", "雷燕", "RAIJIN") },
                { Fighter.HellSplitter, ("Hell Splitter", "鬼斬", "HELL SPLIT") },
                { Fighter.WaterDragon, ("Rising Water Dragon", "水龍", "MIZUCHI") },
                { Fighter.ReaperRequiem, ("Reaper's Requiem", "闇鎌", "REQUIEM") },
                { Fighter.Earthsplitter, ("Earthsplitter", "地裂", "EARTHSPLITTER") },
            };
            foreach (var kv in want)
            {
                var look = RageArtLook.Of(kv.Key);
                Assert.IsNotNull(look, kv.Key);
                Assert.AreEqual(kv.Value.name, look.name, kv.Key);
                Assert.AreEqual(kv.Value.kanji, look.kanji, kv.Key);
                Assert.AreEqual(kv.Value.stamp, look.stamp, kv.Key);
                Assert.Greater(look.hot.grayscale, look.col.grayscale, kv.Key + ": the core is hotter than the body");
                Assert.Greater(look.col.grayscale, look.deep.grayscale, kv.Key + ": the body is brighter than the dark");
            }
            Assert.IsNull(RageArtLook.Of(Fighter.ThousandShadows), "Thousand Shadows has no cut-in");
            foreach (var kv in want)
                Assert.AreEqual(kv.Key == Fighter.ReaperRequiem || kv.Key == Fighter.Earthsplitter, RageArtLook.Of(kv.Key).legendary, kv.Key + ": only the legendary weapons' arts are legendary");
        }

        [Test]
        public void The_shipped_Crimson_Moon_tuning_is_the_JS_arts_numbers_at_60_frames_a_second()
        {
            var file = Tuning.Get<CrimsonMoonTuning>();
            var code = new CrimsonMoonTuning();
            Assert.AreEqual(JsonUtility.ToJson(code), JsonUtility.ToJson(file), "crimsonMoon.json matches the CrimsonMoonTuning defaults");
            // JS 67-rage-arts.js: freeze 0.55 s, cuts at 0.55, 0.74, 0.93 s for 7, step back 1.12 s, finisher 1.38 s for 15, 1.85 s in all
            Assert.AreEqual(33, file.anticipationFrames);
            CollectionAssert.AreEqual(new[] { 33, 44, 56 }, file.cutFrames);
            Assert.AreEqual(67, file.stepBackFrame);
            Assert.AreEqual(83, file.finisherFrame);
            Assert.AreEqual(111, file.totalFrames);
            Assert.AreEqual(7, file.cutDamage);
            Assert.AreEqual(15, file.finisherDamage);
            Assert.AreEqual(78, file.bindFrames, "1.3 s");
            Assert.AreEqual(7, file.finisherHitstop, "0.12 s");
            Assert.AreEqual(0.3f, file.finisherSlowScale);
            Assert.AreEqual(0.45f, file.finisherSlowSeconds);
            Assert.AreEqual(0.8f, file.standDistance, 0.01f, "92 px at 115 px a metre");
            Assert.AreEqual(3, file.cutAngles.Length);
            Assert.IsTrue(file.cutFrames.Zip(file.cutFrames.Skip(1), (a, b) => b > a).All(x => x), "cuts in order");
            Assert.Less(file.cutFrames.Last(), file.stepBackFrame);
            Assert.Less(file.stepBackFrame, file.finisherFrame);
            Assert.Less(file.finisherFrame, file.totalFrames);
        }

        [Test]
        public void Every_playable_weapon_has_clean_display_name_and_non_redundant_rage_art_captions()
        {
            var cases = new (string weaponId, string artId, string displayName, string cutInCaption, string stampWord, string stampCaption, bool legendary)[]
            {
                ("mumei", Fighter.Mikazuki, "Mumei", "RAGE ART  ·  MUMEI", "MIKAZUKI", "RAGE ART  ·  MUMEI", false),
                ("fists", Fighter.ThousandShadows, "Bare Hands", "SHADOW ART  ·  BARE HANDS", "SHADOWFALL", "THOUSAND SHADOWS  ·  BARE HANDS", false),
                ("raiko_no_tachi", Fighter.ThousandShadows, "Raikō no Tachi", "BOSS RAGE ART  ·  RAIKŌ NO TACHI", "SHADOWFALL", "THOUSAND SHADOWS  ·  RAIKŌ NO TACHI", false),
                ("kagehira_yurei", Fighter.ThousandShadows, "Kagehira & Yūrei", "BOSS RAGE ART  ·  KAGEHIRA & YŪREI", "SHADOWFALL", "THOUSAND SHADOWS  ·  KAGEHIRA & YŪREI", false),
                ("kagetsu", Fighter.CrimsonMoon, "Kagetsu", "RAGE ART  ·  KAGETSU", "MOONFALL", "CRIMSON MOON  ·  KAGETSU", false),
                ("shinsen", Fighter.CrimsonMoon, "Shinsen", "BOSS RAGE ART  ·  SHINSEN", "MOONFALL", "CRIMSON MOON  ·  SHINSEN", false),
                ("mugen_no_ha", Fighter.CrimsonMoon, "Mugen no Ha", "BOSS RAGE ART  ·  MUGEN NO HA", "MOONFALL", "CRIMSON MOON  ·  MUGEN NO HA", false),
                ("tsubame", Fighter.SwallowStorm, "Tsubame", "RAGE ART  ·  TSUBAME", "RAIJIN", "THOUSAND SWALLOWS  ·  TSUBAME", false),
                ("shizuka", Fighter.SilentMoon, "Shizuka", "RAGE ART  ·  SHIZUKA", "SILENCE", "SILENT MOON  ·  SHIZUKA", false),
                ("onikiri", Fighter.HellSplitter, "Onikiri", "RAGE ART  ·  ONIKIRI", "HELL SPLIT", "HELL SPLITTER  ·  ONIKIRI", false),
                ("mizuchi", Fighter.WaterDragon, "Mizuchi", "RAGE ART  ·  MIZUCHI", "MIZUCHI", "RISING WATER DRAGON", false),
                ("yamigama", Fighter.ReaperRequiem, "Yamigama", "LEGENDARY RAGE ART  ·  YAMIGAMA", "REQUIEM", "REAPER'S REQUIEM  ·  YAMIGAMA", true),
                ("iwakudaki", Fighter.Earthsplitter, "Iwakudaki", "LEGENDARY RAGE ART  ·  IWAKUDAKI", "EARTHSPLITTER", "LEGENDARY RAGE ART  ·  IWAKUDAKI", true),
            };

            CollectionAssert.AreEquivalent(WeaponFamilies.PlayableWeapons(), cases.Select(c => c.weaponId).ToArray(), "every playable weapon is covered");

            foreach (var c in cases)
            {
                string resolvedArt = WeaponStyles.Of(c.weaponId)?.rageArt ?? WeaponFamilies.Get(WeaponFamilies.FamilyIdOf(c.weaponId))?.rageArt ?? Fighter.ThousandShadows;
                Assert.AreEqual(c.artId, resolvedArt, c.weaponId + " resolves to expected art");

                string display = RageArtCues.WeaponDisplayName(c.weaponId);
                Assert.AreEqual(c.displayName, display, c.weaponId + " display name");
                Assert.IsFalse(display.Contains("_"), c.weaponId + " display name has no raw underscore");

                var look = RageArtLook.ForHud(c.artId);
                Assert.IsNotNull(look, c.weaponId + " has a HUD look");
                Assert.AreEqual(c.stampWord, look.stamp, c.weaponId + " stamp word");
                Assert.AreEqual(c.legendary, look.legendary, c.weaponId + " legendary flag");
                Assert.Greater(look.hot.grayscale, look.col.grayscale, c.weaponId + " hot > col");
                Assert.Greater(look.col.grayscale, look.deep.grayscale, c.weaponId + " col > deep");

                string cutInSub = RageArtCues.FormatCutInCaption(look, c.weaponId);
                string stampSub = RageArtCues.FormatStampCaption(look, c.weaponId);
                Assert.AreEqual(c.cutInCaption, cutInSub, c.weaponId + " cut-in caption");
                Assert.AreEqual(c.stampCaption, stampSub, c.weaponId + " stamp caption");
                Assert.IsFalse(cutInSub.Contains("_"), c.weaponId + " cut-in caption has no underscore");
                Assert.IsFalse(stampSub.Contains("_"), c.weaponId + " stamp caption has no underscore");
                Assert.AreNotEqual(look.stamp.ToUpperInvariant(), stampSub.ToUpperInvariant(), c.weaponId + " stamp caption does not repeat stamp word");
            }
        }

        [Test]
        public void Every_rage_art_kanji_pair_resolves_to_a_single_coherent_font_with_all_glyphs()
        {
            var looks = new[]
            {
                RageArtLook.Crimson, RageArtLook.Silent, RageArtLook.Swallows, RageArtLook.Hell,
                RageArtLook.Dragon, RageArtLook.Reaper, RageArtLook.Earth, RageArtLook.Shadows,
            };
            foreach (var look in looks)
            {
                Assert.IsFalse(string.IsNullOrEmpty(look.kanji), look.id + " has kanji");
                foreach (int size in new[] { 112, 240 })
                {
                    var font = Hud.RageKanjiFont(look.kanji, size);
                    Assert.IsNotNull(font, $"{look.id} ({look.kanji}) at {size}px resolved a font");
                    foreach (char ch in look.kanji)
                    {
                        if (char.IsWhiteSpace(ch)) continue;
                        Assert.IsTrue(font.HasCharacter(ch), $"{look.id}: font '{font.name}' at {size}px contains glyph '{ch}' (U+{(int)ch:X4})");
                    }
                }
            }
        }
    }
}
