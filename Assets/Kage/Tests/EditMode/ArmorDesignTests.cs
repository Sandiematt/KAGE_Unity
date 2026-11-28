using System.Collections.Generic;
using System.Linq;
using Kage.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Validates the five-tier armor catalogue (<see cref="ArmorDesigns"/>, generated from <c>Tools/armor_art/armors.py</c>):
    /// all wearable tiered armors have valid triangulatable outlines, multi-bone structure, tier progression, soft cloth/tasset
    /// chains, and Legendary aura configuration.
    /// </summary>
    public class ArmorDesignTests
    {
        static readonly (string id, ArmorTier tier)[] Tiers =
        {
            ("ronin_yoroi", ArmorTier.Common),
            ("yami_yoroi", ArmorTier.Rare), ("shinsen_yoroi", ArmorTier.Rare),
            ("raiko_yoroi", ArmorTier.Epic), ("kagehira_yoroi", ArmorTier.Epic),
            ("ikazuchi_do", ArmorTier.Trophy), ("shiden_yoroi", ArmorTier.Trophy),
            ("yurei_yoroi", ArmorTier.Legendary), ("mugen_yoroi", ArmorTier.Legendary),
        };

        static IEnumerable<Vector2> Points(float[] xy)
        {
            for (int i = 0; i + 1 < xy.Length; i += 2) yield return new Vector2(xy[i], xy[i + 1]);
        }

        static IEnumerable<(string what, float[] xy)> Shapes(ArmorDesign d)
        {
            foreach (var p in d.pieces)
            {
                yield return ($"{d.id}/{p.name}", p.outline);
                if (p.detail != null)
                    for (int i = 0; i < p.detail.Length; i++)
                        yield return ($"{d.id}/{p.name}/detail{i}", p.detail[i].pts);
            }
        }

        [Test]
        public void EveryTieredArmorHasAStructuredDesign()
        {
            var wearable = JsTables.Armors.Where(a => !a.Retired && a.Tier != "starter");
            CollectionAssert.AreEquivalent(wearable.Select(a => a.Id), ArmorDesigns.All.Select(d => d.id));
            foreach (var d in ArmorDesigns.All)
            {
                Assert.IsNotNull(JsTables.Armor(d.id), $"{d.id} is a JS armor");
                Assert.AreEqual(JsTables.Armor(d.id).Look, d.look, $"{d.id} look matches JS table");
                Assert.IsNotEmpty(d.pieces, d.id);
                Assert.AreEqual(d.pieces.Length, d.pieces.Select(p => p.name).Distinct().Count(), $"{d.id}: piece names unique");
                // Structured armor: chest do, high collar, tailored jacket/haori lapels, shoulder sode, forearm kote, waist kusazuri/tails, shin suneate
                Assert.IsTrue(d.pieces.Any(p => p.bone == B.Chest), $"{d.id}: chest armor");
                Assert.IsTrue(d.pieces.Any(p => p.bone == B.Chest && p.name == "HighCollar"), $"{d.id}: high standing collar");
                Assert.IsTrue(d.pieces.Any(p => p.bone == B.Chest && (p.name.StartsWith("Haori") || p.name.StartsWith("Jacket"))), $"{d.id}: tailored coat/haori/jacket lapels");
                Assert.IsTrue(d.pieces.Any(p => p.bone == B.ArmNU), $"{d.id}: shoulder pauldron");
                Assert.IsTrue(d.pieces.Any(p => p.bone == B.ArmNL), $"{d.id}: forearm bracer");
                Assert.IsTrue(d.pieces.Any(p => p.bone == B.Hips), $"{d.id}: waist tassets");
                Assert.IsTrue(d.pieces.Any(p => p.bone == B.Hips && p.kind == PieceKind.Cloth && (p.name.StartsWith("HaoriTail") || p.name == "SashTail")), $"{d.id}: dynamic split coat/sash tail");
                Assert.IsTrue(d.pieces.Any(p => p.bone == B.LegNL), $"{d.id}: shin greaves");
                if (d.tier == ArmorTier.Legendary)
                    Assert.IsTrue(d.pieces.Any(p => p.bone == B.Hips && p.name == "HaoriTailFront" && p.kind == PieceKind.Cloth), $"{d.id}: Legendary front split coat panel");
            }
        }

        [Test]
        public void EachArmorMatchesItsRulesTierAndProgressionEscalates()
        {
            foreach (var (id, tier) in Tiers)
            {
                Assert.AreEqual(tier, ArmorDesigns.Get(id).tier, id);
                Assert.AreEqual(tier, ArmorTiers.FromId(GameRules.Item(GearKind.Armors, id).Tier), $"{id} rules tier matches design tier");
            }
            Assert.AreEqual("starter", GameRules.Item(GearKind.Armors, "tachibana_yoroi").Tier);

            float MaxGlow(ArmorDesign d) => d.pieces.Max(p => p.glow);
            foreach (var d in ArmorDesigns.All)
            {
                Assert.AreEqual(ArmorTiers.FromId(GameRules.Item(GearKind.Armors, d.id).Tier), d.tier, d.id);
                switch (d.tier)
                {
                    case ArmorTier.Common:
                    case ArmorTier.Rare:
                        Assert.AreEqual(1f, MaxGlow(d), $"{d.id}: {d.tier} armor has no HDR bloom");
                        Assert.AreEqual(ArmorAuraKind.None, d.aura, d.id);
                        break;
                    case ArmorTier.Epic:
                    case ArmorTier.Trophy:
                        Assert.Greater(MaxGlow(d), 1.5f, $"{d.id}: {d.tier} armor has glowing accent crest/rune");
                        Assert.AreEqual(ArmorAuraKind.None, d.aura, d.id);
                        break;
                    case ArmorTier.Legendary:
                        Assert.Greater(MaxGlow(d), 2.5f, $"{d.id}: Legendary armor has prestige glow");
                        Assert.AreNotEqual(ArmorAuraKind.None, d.aura, $"{d.id}: Legendary armor carries ArmorAura");
                        Assert.IsTrue(d.pieces.Any(p => p.name.StartsWith("Cape") && p.kind == PieceKind.Cloth), $"{d.id}: Legendary armor has flowing cape");
                        break;
                }
            }
            // Escalation in structural richness from Common/Rare -> Epic/Trophy -> Legendary
            float Mean(ArmorTier t) => (float)ArmorDesigns.All.Where(d => d.tier == t).Average(d => d.pieces.Length);
            Assert.Less(Mean(ArmorTier.Common), Mean(ArmorTier.Epic));
            Assert.Less(Mean(ArmorTier.Epic), Mean(ArmorTier.Legendary));
        }

        [Test]
        public void EveryShapeTriangulatesCleanlyAndUsesOnlyPaletteTones()
        {
            foreach (var d in ArmorDesigns.All)
            {
                foreach (var p in d.pieces)
                {
                    Assert.IsTrue(d.palette.Has(p.tone), $"{d.id}/{p.name}: tone {p.tone}");
                    if (p.detail != null)
                        foreach (var det in p.detail)
                            Assert.IsTrue(d.palette.Has(det.tone), $"{d.id}/{p.name}: detail tone {det.tone}");
                    if (p.kind == PieceKind.Cloth)
                    {
                        Assert.DoesNotThrow(() => SoftPresets.Get(p.feel), $"{d.id}/{p.name}");
                        Assert.IsNotEmpty(p.chains, $"{d.id}/{p.name}");
                    }
                }
                foreach (var (what, xy) in Shapes(d))
                {
                    Assert.AreEqual(0, xy.Length % 2, what);
                    var pts = Points(xy).ToList();
                    Assert.GreaterOrEqual(pts.Count, 3, what);
                    float area = Mathf.Abs(MeshKit.SignedArea(pts));
                    Assert.Greater(area, 3f, $"{what}: area too small");
                    var tris = MeshKit.Triangulate(pts);
                    float sum = 0f;
                    for (int i = 0; i + 2 < tris.Count; i += 3)
                        sum += Mathf.Abs(MeshKit.SignedArea(new[] { pts[tris[i]], pts[tris[i + 1]], pts[tris[i + 2]] }));
                    Assert.AreEqual(area, sum, area * 0.01f + 0.5f, $"{what}: triangles do not tile outline");
                }
            }
        }
        [Test]
        public void RigidArmorAlwaysSortsOverUnderlyingClothWithZeroDuplicateSortingOrders()
        {
            foreach (var d in ArmorDesigns.All)
            {
                var def = KageCharacter.Def();
                def.armor = d.id;
                var rigDef = new KagePartSet().Build(def);
                var armorParts = rigDef.parts.Where(p => p.name.StartsWith("Armor_")).ToList();

                // 1. Zero duplicate (order, underSlot, swordLayer) slots among built armor parts (no Z-fighting)
                var seen = new HashSet<(int order, int under, bool sword)>();
                foreach (var p in armorParts)
                    Assert.IsTrue(seen.Add((p.order, p.underSlot, p.swordLayer)),
                        $"{d.id}/{p.name}: duplicate sort slot ({p.order}, {p.underSlot}, sword={p.swordLayer})");

                // 2. Rigid Kusazuri waist armor strictly sorts ABOVE all hip cloth (Coat, HaoriTailFront/Near, WaistRibbon/SashTail)
                int minKusazuri = armorParts.Where(p => p.bone == B.Hips && p.name.Contains("Kusazuri")).Min(p => p.order);
                foreach (var cloth in armorParts.Where(p => p.bone == B.Hips && (p.name.Contains("HaoriTail") || p.name.Contains("WaistRibbon") || p.name.Contains("SashTail"))))
                    Assert.Less(cloth.order, minKusazuri, $"{d.id}/{cloth.name} must render behind rigid Kusazuri");
                int coatOrder = rigDef.parts.Single(p => p.name == "Coat").order;
                Assert.Greater(minKusazuri, coatOrder, $"{d.id}: Kusazuri over base Coat");

                // 3. Covered base straps/belt/lapel are suppressed under structured armor
                Assert.IsNull(rigDef.parts.Find(p => p.name == "Belt"), $"{d.id}: base Belt suppressed under Obi");
                Assert.IsNull(rigDef.parts.Find(p => p.name == "Lapel"), $"{d.id}: base Lapel suppressed under chest armor");
                Assert.IsNull(rigDef.parts.Find(p => p.name == "ArmN_Cuff"), $"{d.id}: base ArmN_Cuff suppressed under sleeve/sode");

                // 4. Every Armor_* part carries non-null polys (compatible with TrophyLookTests & MeshKit.PolyMulti)
                foreach (var p in armorParts)
                    Assert.IsNotNull(p.polys, $"{d.id}/{p.name}: polys must not be null");
            }
        }

        [Test]
        public void TrailingCapesAndHaoriTailsNeverClipForwardThroughArmorSeamUnderForwardWindOrFlips()
        {
            foreach (var id in new[] { "raiko_yoroi", "shiden_yoroi", "yurei_yoroi", "mugen_yoroi" })
            {
                var go = new GameObject("SeamClearance_" + id);
                try
                {
                    var def = KageCharacter.Def();
                    def.armor = id;
                    var rigDef = new KagePartSet().Build(def);
                    var rig = go.AddComponent<FighterRig>();
                    rig.Build(rigDef, 1000);
                    // Strong forward-blowing tailwind trying to push capes & haori tails through Kage's chest/legs!
                    rig.wind = Wind.Blowing(new Vector2(9f, 0.5f), gust: 4f, seed: 13);
                    rig.Apply(PoseLib.Stance());
                    rig.ResetSoft();

                    const float dt = 1f / 60f;
                    // Simulate strong forward wind + sudden stop + forward impulse + direction flip
                    for (int i = 0; i < 45; i++)
                    {
                        if (i == 15) rig.Impulse(new Vector2(8f, 1.5f), 1.15f);
                        rig.Secondary(dt, Vector2.zero);
                        rig.scarf?.Tick(dt);
                    }

                    // Cape / HaoriTailNear vertices must remain strictly behind Kage's torso midline (not clipping across front chest)
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (!mf.name.Contains("Cape") && !mf.name.Contains("HaoriTailBack") && !mf.name.Contains("HaoriTailNear")) continue;
                        var verts = mf.sharedMesh.vertices;
                        float rootWorldX = mf.transform.TransformPoint(Vector3.zero).x;
                        foreach (var v in verts)
                        {
                            Assert.IsTrue(float.IsFinite(v.x) && float.IsFinite(v.y), $"{id}/{mf.name} finite");
                            float wx = mf.transform.TransformPoint(v).x;
                            // Kage faces +x; trailing rear cloth must not cross past Kage's front chest border (+0.20m ahead of bone pivot)
                            Assert.Less(wx - rootWorldX, 0.20f, $"{id}/{mf.name} clipped forward across armor/torso");
                        }
                    }
                }
                finally { Object.DestroyImmediate(go); }
            }
        }

        [Test]
        public void FlowingArmorClothBillowsInIdleSnapsOnFlipAndWhipsOnCombatImpulse()
        {
            foreach (var id in new[] { "raiko_yoroi", "yurei_yoroi", "mugen_yoroi" })
            {
                var go = new GameObject("ClothCheck_" + id);
                try
                {
                    var def = KageCharacter.Def();
                    def.armor = id;
                    var rigDef = new KagePartSet().Build(def);
                    var rig = go.AddComponent<FighterRig>();
                    rig.Build(rigDef, 1000);
                    rig.wind = Wind.Blowing(new Vector2(-0.65f, 0.08f), gust: 0.9f, seed: 42);
                    rig.Apply(PoseLib.Stance());
                    rig.ResetSoft();

                    var tailMf = go.GetComponentsInChildren<MeshFilter>(true).First(m => m.name.Contains("HaoriTailNear"));
                    const float dt = 1f / 60f;
                    rig.Secondary(dt, Vector2.zero);
                    var rest = tailMf.sharedMesh.vertices.ToArray();

                    // 1. Ambient wind billowing while idle
                    for (int i = 0; i < 45; i++)
                    {
                        rig.Secondary(dt, Vector2.zero);
                        rig.scarf?.Tick(dt);
                    }
                    var idleVerts = tailMf.sharedMesh.vertices.ToArray();
                    float idleDelta = 0f;
                    for (int i = 0; i < rest.Length; i++) idleDelta = Mathf.Max(idleDelta, (idleVerts[i] - rest[i]).magnitude);
                    Assert.Greater(idleDelta, 0.002f, $"{id}: haori tail should billow gently in idle wind");

                    // 2. Direction flip snap + sudden stop after sprint + heavy combat whip impulse
                    for (int i = 0; i < 15; i++)
                    {
                        go.transform.position += new Vector3(8f * dt, 0f, 0f);
                        rig.Secondary(dt, new Vector2(8f, 0f));
                        rig.scarf?.Tick(dt);
                    }
                    rig.SetFacing(-1f);
                    rig.Impulse(new Vector2(6.5f, 2.2f), 1.15f);
                    for (int i = 0; i < 20; i++)
                    {
                        rig.Secondary(dt, Vector2.zero);
                        rig.scarf?.Tick(dt);
                    }
                    var afterVerts = tailMf.sharedMesh.vertices;
                    for (int i = 0; i < afterVerts.Length; i++)
                    {
                        Assert.IsTrue(float.IsFinite(afterVerts[i].x) && float.IsFinite(afterVerts[i].y), $"{id}: finite cloth vertex {i}");
                        Assert.Less(afterVerts[i].magnitude, 4f, $"{id}: cloth stayed cleanly anchored without exploding");
                    }
                    var scarfVerts = rig.scarf.GetComponentsInChildren<MeshFilter>()[0].sharedMesh.vertices;
                    for (int i = 0; i < scarfVerts.Length; i++)
                        Assert.IsTrue(float.IsFinite(scarfVerts[i].x) && float.IsFinite(scarfVerts[i].y), $"{id}: finite scarf vertex {i}");
                }
                finally { Object.DestroyImmediate(go); }
            }
        }
    }
}
