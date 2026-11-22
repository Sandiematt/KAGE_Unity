using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kage.Tests
{
    /// <summary>
    /// SAN-72 tier redesign: the review images in Screenshots/review/SAN-72/tiers/ and the pixel check of everything the helmets
    /// must not change. Needs a GPU: it skips itself under -nographics. Run <c>-testFilter HelmetTierScreenshots</c> without
    /// -nographics.
    /// </summary>
    [Category("Screenshots")]
    public class HelmetTierScreenshots
    {
        const int W = 800, H = 600;

        /// <summary>
        /// Kage with no helmet (and in the starter, which draws nothing) and every other character of the catalog, each tile alone,
        /// written to the folder named by the KAGE_PIXEL_DIR environment variable, so a run before the change and one after can be
        /// compared pixel for pixel. Skipped when the variable is not set (the review run does not need it).
        /// </summary>
        [Test]
        public void PixelCheckOfKageBareHeadedAndTheOtherCharacters()
        {
            Assume.That(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "needs a GPU: run without -nographics");
            string dir = Environment.GetEnvironmentVariable("KAGE_PIXEL_DIR");
            Assume.That(!string.IsNullOrEmpty(dir), "set KAGE_PIXEL_DIR to write the pixel check");
            Directory.CreateDirectory(dir);

            foreach (var helmet in new[] { null, "shiro_maki" })
            {
                string tag = helmet ?? "none";
                var def = CharacterCatalog.Get(KageCharacter.Id);
                def.helmet = helmet;
                using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, -0.2f, kageDef: def))
                {
                    f.Run(30);
                    Vector2 c = f.Kage.rig.Bone(B.Head).position;
                    HuskSvgScreenshots.Save(Path.Combine(dir, $"kage_{tag}_head.png"), HuskSvgScreenshots.Render(c + new Vector2(0f, 0.1f), 0.5f), W, H);
                    HuskSvgScreenshots.Save(Path.Combine(dir, $"kage_{tag}_body.png"), HuskSvgScreenshots.Render(f.Kage.Pos + new Vector2(0f, 1.1f), 1.25f), W, H);
                }
                using (var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, -0.2f, kageDef: def))
                {
                    f.KageInput.Add(24, fwd: 1).Add(300);
                    f.Run(34);
                    HuskSvgScreenshots.Save(Path.Combine(dir, $"kage_{tag}_motion.png"), HuskSvgScreenshots.Render(f.Kage.Pos + new Vector2(0f, 1.1f), 1.3f), W, H);
                }
            }

            foreach (var armor in ArmorVisuals.Ids)
            {
                var def = CharacterCatalog.Get(KageCharacter.Id);
                def.armor = armor;
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.None, -0.2f, kageDef: def);
                f.KageInput.Add(24, fwd: 1).Add(300);
                f.Run(34);
                HuskSvgScreenshots.Save(Path.Combine(dir, $"armor_{armor}.png"), HuskSvgScreenshots.Render(f.Kage.Pos + new Vector2(0f, 1.1f), 1.3f), W, H);
            }

            foreach (var id in CharacterCatalog.Ids)
            {
                if (id == KageCharacter.Id) continue;
                using var f = new HeadlessFight(KageMoveScenarios.Seed, Foe.Scripted, -3f, 0.3f, foeDef: CharacterCatalog.Get(id));
                f.KageInput.Add(300);
                f.RoninInput.Add(24, fwd: 1).Add(300);
                f.Run(34);
                HuskSvgScreenshots.Save(Path.Combine(dir, $"char_{id}.png"), HuskSvgScreenshots.Render(f.Ronin.Pos + new Vector2(0f, 1.1f), 1.3f), W, H);
            }
            Assert.IsTrue(File.Exists(Path.Combine(dir, "kage_none_head.png")));
        }
    }
}
