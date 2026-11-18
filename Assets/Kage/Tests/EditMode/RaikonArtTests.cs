using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Raikon's part set (SAN-71): the owner's SVG cut into rig parts on the shared skeleton. Seams: the catalog's
    /// <c>raikon</c> entry → <see cref="IPartSet.Build"/> → the <see cref="RigDef"/> it returns. The independent sources of
    /// truth are the ticket's part list, Kage's own rig (<c>KageModel.Build(Look.Kage)</c>, the shared skeleton) and the
    /// JS build scale (1.24).
    /// </summary>
    public class RaikonArtTests
    {
        static RigDef _rig;

        static RigDef Rig()
        {
            if (_rig != null) return _rig;
            var def = CharacterCatalog.Get("raikon");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _rig = CharacterCatalog.PartSet(def.partSet).Build(def);
            Debug.Log($"[SAN-71] Raikon's first build (from the bake):{sw.ElapsedMilliseconds} ms, " +
                      $"{_rig.parts.Sum(p => (p.mesh != null ? p.mesh.triangles.Length / 3 : 0) + (p.polys?.Count ?? 0))} triangles, " +
                      $"{_rig.parts.Sum(p => p.mesh != null ? p.mesh.vertexCount : 0)} vertices; " +
                      string.Join(", ", _rig.parts.Where(p => p.mesh != null).Select(p => p.name + "=" + p.mesh.vertexCount)));
            sw.Restart();
            CharacterCatalog.PartSet(def.partSet).Build(def);
            Debug.Log($"[SAN-71] Raikon's second build (cached cut): {sw.ElapsedMilliseconds} ms");
            return _rig;
        }

        static IEnumerable<PartDef> Named(string prefix) => Rig().parts.Where(p => p.name.StartsWith(prefix));

        /// <summary>Every drawn point of a part, in bind space.</summary>
        static IEnumerable<Vector2> Drawn(PartDef p)
        {
            if (p.mesh != null) foreach (var v in p.mesh.vertices) yield return v;
            if (p.polys != null) foreach (var (pts, _) in p.polys) foreach (var v in pts) yield return v;
            if (p.pts != null) foreach (var v in p.pts) yield return v;
        }

        [Test]
        public void TheBakedCutIsUpToDateWithTheSvgAndTheRegions()
        {
            var bake = Resources.Load<TextAsset>(RaikonModel.BakeResource);
            Assert.IsNotNull(bake, "no bake: Kage > Bake Raikon Art");
            Assert.AreEqual(RaikonModel.InputHash(), System.BitConverter.ToUInt64(bake.bytes, 0),
                "the SVG, the regions or the cutting code changed since the bake: Kage > Bake Raikon Art");
            Rig();
            Assert.IsTrue(RaikonModel.BuiltFromBake, "every piece came from the bake (no cutting at spawn)");
        }

        [Test]
        public void TheSvgInResourcesIsTheOwnersFileByteForByte()
        {
            var original = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "..", "Tools", "raikon_art", "original", "Raikon.svg"));
            var copy = Resources.Load<TextAsset>(RaikonModel.SvgResource).text;
            Assert.AreEqual(original.Replace("\r\n", "\n"), copy.Replace("\r\n", "\n"));
        }

        [Test]
        public void RaikonIsInTheCatalogWithHisJsBuildScale()
        {
            var def = CharacterCatalog.Get("raikon");
            Assert.IsNotNull(def);
            Assert.AreEqual("raikon", def.partSet);
            Assert.AreEqual(1.24f, def.bodyScale, 1e-4f);
            Assert.AreEqual(WeaponFamilies.Tachi, def.family);
        }

        // the ticket's regions → the part names that draw them
        static readonly (string region, string[] parts)[] TicketParts =
        {
            ("hat", new[] { "Hat" }),
            ("hat tassels", new[] { "Tassel1", "Tassel2", "Tassel3" }),
            ("head", new[] { "Head" }),
            ("ponytail", new[] { "Ponytail" }),
            ("fur mantle", new[] { "Fur" }),
            ("shoulder plates", new[] { "SodeN" }),
            ("torso", new[] { "Torso" }),
            ("upper arms", new[] { "ArmN_Upper", "ArmF_Upper" }),
            ("lower arms", new[] { "ArmN_Fore", "ArmF_Fore" }),
            ("hands", new[] { "HandN", "HandF" }),
            ("waist rope", new[] { "Rope" }),
            ("skirt panels", new[] { "Skirt" }),
            ("thighs", new[] { "ThighN", "ThighF" }),
            ("shins", new[] { "ShinN", "ShinF" }),
            ("boots", new[] { "BootN", "BootF" }),
            ("torn cape", new[] { "Cape" }),
            ("tachi", new[] { "Tachi_Blade", "Tachi_Hilt" }),
        };

        [Test]
        public void EveryRegionTheTicketNamesIsAPartWithArt()
        {
            var missing = new List<string>();
            foreach (var (region, parts) in TicketParts)
            foreach (var name in parts)
            {
                var p = Rig().parts.FirstOrDefault(q => q.name == name);
                if (p == null) missing.Add($"{region}: no part {name}");
                else if (Drawn(p).Count() < 3) missing.Add($"{region}: part {name} has nothing drawn");
            }
            CollectionAssert.IsEmpty(missing);
        }

        [Test]
        public void LimbsSitOnTheirBones()
        {
            var expect = new Dictionary<string, B>
            {
                { "Hat", B.Head }, { "Head", B.Head }, { "Ponytail", B.Head }, { "Tassel1", B.Head },
                { "Torso", B.Chest }, { "Fur", B.Chest }, { "Cape", B.Chest }, { "Rope", B.Chest }, { "Skirt", B.Hips },
                { "SodeN", B.ArmNU }, { "ArmN_Upper", B.ArmNU }, { "ArmN_Fore", B.ArmNL }, { "HandN", B.HandN },
                { "ArmF_Upper", B.ArmFU }, { "ArmF_Fore", B.ArmFL }, { "HandF", B.HandF },
                { "ThighN", B.LegNU }, { "ShinN", B.LegNL }, { "BootN", B.FootN },
                { "ThighF", B.LegFU }, { "ShinF", B.LegFL }, { "BootF", B.FootF },
            };
            foreach (var kv in expect)
                Assert.AreEqual(kv.Value, Rig().parts.First(p => p.name == kv.Key).bone, kv.Key);
        }

        [Test]
        public void TheTachiIsItsOwnPartOnTheSwordHand()
        {
            var tachi = Named("Tachi_").ToList();
            Assert.IsNotEmpty(tachi);
            Assert.IsTrue(tachi.All(p => p.bone == B.Sword), "every tachi part is on the sword bone");
            Assert.AreEqual(B.HandF, Rig().parent[(int)B.Sword], "the sword bone hangs from the hand");
            Assert.IsTrue(tachi.All(p => p.swordLayer), "the tachi comes to the front with the sword arm");
            Assert.Greater(tachi.First(p => p.name == "Tachi_Blade").glow, 1.2f, "the blade glows");
        }

        [Test]
        public void CapePonytailTasselsAndFurAreTheOnlySoftParts()
        {
            var soft = Rig().parts.Where(p => p.soft != SoftKind.None).Select(p => p.name).OrderBy(n => n).ToList();
            CollectionAssert.AreEqual(new[] { "Cape", "Fur", "Ponytail", "Tassel1", "Tassel2", "Tassel3" }, soft);
            foreach (var p in Rig().parts.Where(p => p.soft != SoftKind.None))
            {
                Assert.AreEqual(SoftKind.Chain, p.soft, p.name);
                Assert.IsNotNull(p.chains, p.name);
                Assert.IsNotEmpty(p.chains, p.name);
                Assert.IsTrue(p.chains.All(c => c.Length >= 2), p.name + ": every chain has a root and a tip");
            }
        }

        [Test]
        public void ArtNeverMovesAGameplayJoint()
        {
            var kage = KageModel.Build(KageModel.Look.Kage);
            var r = Rig();
            for (int i = 0; i < (int)B.COUNT; i++)
            {
                Assert.AreEqual(kage.pivot[i], r.pivot[i], ((B)i) + " pivot");
                Assert.AreEqual(kage.parent[i], r.parent[i], ((B)i) + " parent");
            }
            Assert.AreEqual(kage.hurt.Count, r.hurt.Count);
            for (int i = 0; i < kage.hurt.Count; i++)
            {
                Assert.AreEqual(kage.hurt[i].bone, r.hurt[i].bone);
                Assert.AreEqual(kage.hurt[i].local, r.hurt[i].local);
                Assert.AreEqual(kage.hurt[i].radius, r.hurt[i].radius);
            }
            Assert.AreEqual(kage.fistLocal, r.fistLocal);
            Assert.AreEqual(kage.footTipLocal, r.footTipLocal);
            Assert.AreEqual(kage.heelLocal, r.heelLocal);
            Assert.AreEqual(kage.hiltLocal, r.hiltLocal);
            Assert.AreEqual(kage.bladeBindAngle, r.bladeBindAngle);
            Assert.AreEqual(kage.bladeBaseLocal, r.bladeBaseLocal);
            Assert.AreEqual(kage.bladeTipLocal, r.bladeTipLocal);
        }

        [Test]
        public void RaikonStandsOnTheGroundAndIsAsTallAsTheSkeletonAllows()
        {
            // bind space: the ground is y = 0; Kage's head top is about 1.9 units up, Raikon's hat reaches higher
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var p in Rig().parts)
            foreach (var v in Drawn(p)) { lo = Mathf.Min(lo, v.y); hi = Mathf.Max(hi, v.y); }
            Assert.AreEqual(0f, lo, 0.03f, "boot soles on the ground");
            Assert.That(hi, Is.InRange(1.95f, 2.6f), "top of the hat and ponytail");
        }
    }
}
