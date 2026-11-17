using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Kage.Tests
{
    /// <summary>Effect cues (SAN-75, ADR-0007): named effects with placeholder visuals, a no-op without a Vfx.</summary>
    public class EffectCueTests
    {
        readonly List<GameObject> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _objects) if (o != null) Object.DestroyImmediate(o);
            _objects.Clear();
            EffectCues.Unregister("VFX_Test_Cue");
        }

        Vfx NewVfx()
        {
            var go = new GameObject("TestVfx");
            _objects.Add(go);
            return go.AddComponent<Vfx>();
        }

        [Test]
        public void ARegisteredCueRunsItsPlayerWithPositionAndArgs()
        {
            NewVfx();
            Vector2 gotPos = default;
            float gotScale = 0f;
            EffectCues.Register("VFX_Test_Cue", (Vector2 pos, in CueArgs a) => { gotPos = pos; gotScale = a.scale; });

            EffectCues.Play("VFX_Test_Cue", new Vector2(1f, 2f), new CueArgs { scale = 1.5f });

            Assert.AreEqual(new Vector2(1f, 2f), gotPos);
            Assert.AreEqual(1.5f, gotScale);
        }

        [Test]
        public void PlayingACueWithoutAVfxDoesNothingButStillTellsObservers()
        {
            Assert.IsNull(Vfx.I, "no Vfx in the headless fight");
            bool ran = false;
            EffectCues.Register("VFX_Test_Cue", (Vector2 pos, in CueArgs a) => ran = true);
            var seen = new List<string>();
            void Rec(string id, Vector2 pos, in CueArgs a) => seen.Add(id);
            EffectCues.Played += Rec;
            try { EffectCues.Play("VFX_Test_Cue", Vector2.zero); }
            finally { EffectCues.Played -= Rec; }

            Assert.IsFalse(ran);
            Assert.AreEqual(new[] { "VFX_Test_Cue" }, seen.ToArray());
        }

        [Test]
        public void AnUnregisteredCuePlaysThePlaceholderStarAndWarnsOnce()
        {
            NewVfx();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("VFX_Test_Missing"));
            EffectCues.Play("VFX_Test_Missing", Vector2.zero);
            EffectCues.Play("VFX_Test_Missing", Vector2.one);    // no second warning (an unexpected one would fail the test)
            Assert.IsFalse(EffectCues.IsRegistered("VFX_Test_Missing"));
        }

        [Test]
        public void TheCrescentAndTheHazardCuesAreRegistered()
        {
            foreach (var id in new[] { "VFX_Kage_Crescent", "VFX_Kage_CrescentHit", "VFX_Kage_CrescentFade", "VFX_Kage_HazardTelegraph", "VFX_Kage_HazardStrike" })
                Assert.IsTrue(EffectCues.IsRegistered(id), id);
        }

        [Test]
        public void AFlightCueRidesTheProjectileAndGoesWithIt()
        {
            NewVfx();
            using var fight = new HeadlessFight(KageMoveScenarios.Seed, Foe.None);
            var def = new ProjectileDef { id = "t", hit = new HitWindow(), speed = 1f, radius = 0.2f, lifeFrames = 10, flightCue = "VFX_Kage_Crescent" };
            var p = Projectile.Spawn(fight.Ctx, fight.Kage, def, new Vector2(0f, 1f), 1f);
            Assert.IsNotNull(p.GetComponentInChildren<CrescentLook>(), "the crescent look is a child of the projectile");
            _objects.Add(p.gameObject);
        }
    }
}
