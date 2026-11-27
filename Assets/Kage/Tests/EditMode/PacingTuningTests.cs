using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Kage.Rules;

namespace Kage.Tests
{
    /// <summary>
    /// The pacing numbers (SAN-110, ADR-0010): the one shared health scale, the per-character health multipliers that tune each foe's
    /// time-to-kill, and the guarantee that nothing else moved: every foe's damage, defence and speed multipliers are still the JS table's and
    /// every move's damage is the one recorded before the pacing pass.
    /// </summary>
    public class PacingTuningTests
    {
        [TearDown]
        public void TearDown() => Tuning.ClearOverrides();

        /// <summary>The per-foe health multipliers the pacing report (PacingReportTests) tuned. Every other character stays at 1.</summary>
        static readonly Dictionary<string, float> Multipliers = new Dictionary<string, float>
        {
            { "hollow_shade", 1.4f }, { "order_footman", 1.65f }, { "order_runner", 1.35f }, { "order_chainthrower", 1.7f },
            { "order_shieldbearer", 1.1f }, { "hollow_flicker", 2.5f }, { "order_ironclad", 1.35f }, { "hollow_husk", 1.4f },
            { "kuroe", 1.15f }, { "garan", 1.25f }, { "hisame", 1.35f }, { "shiden", 1.31f }, { "raikon", 1.4f },
        };

        static readonly string[] Foes =
        {
            "hollow_tutorial", "hollow_shade", "order_footman", "order_runner", "order_chainthrower", "order_shieldbearer", "hollow_flicker",
            "order_ironclad", "hollow_husk", "kuroe", "garan", "hisame", "shiden", "raikon",
        };

        [Test]
        public void The_shared_health_scale_keeps_Kages_fresh_save_health_at_100()
        {
            Assert.AreEqual(100.0 / 232.0, Tuning.Get<HealthTuning>().healthScale, 1e-9);
            Assert.AreEqual(100f, KageCharacter.Def(GameRules.DefaultSave(false)).MaxHealth(), 1e-3);
        }

        [Test]
        public void Every_foes_health_is_its_JS_health_times_its_multiplier_times_the_shared_scale()
        {
            double scale = Tuning.Get<HealthTuning>().healthScale;
            foreach (var id in Foes)
            {
                float mul = Multipliers.TryGetValue(id, out var m) ? m : 1f;
                var d = CharacterDef.FromJs(id);
                Assert.AreEqual(mul, d.healthMul, 1e-6, id + " multiplier");
                Assert.AreEqual(JsTables.Enemy(id).Hp * mul * scale, d.MaxHealth(), 1e-3, id + " health");
            }
        }

        [Test]
        public void Only_characters_with_a_tuned_multiplier_have_one()
        {
            foreach (var kv in Tuning.Get<HealthTuning>().multipliers)
                Assert.IsTrue(Multipliers.ContainsKey(kv.id), kv.id + " has a multiplier in health.json that this table does not know");
            Assert.AreEqual(1f, KageCharacter.Def(GameRules.DefaultSave(false)).healthMul, "Kage is never scaled by a multiplier");
        }

        [Test]
        public void No_damage_defence_or_speed_multiplier_of_a_foe_moved_off_the_JS_table()
        {
            foreach (var id in Foes)
            {
                var d = CharacterDef.FromJs(id);
                var js = JsTables.Enemy(id);
                Assert.AreEqual((float)js.Dmg, d.damageMul, 1e-6, id + " damage");
                Assert.AreEqual((float)js.Def, d.defense, 1e-6, id + " defence");
                Assert.AreEqual((float)js.Speed, d.speedMul, 1e-6, id + " speed");
            }
        }

        // ---------------- move damage did not change ----------------

        /// <summary>Every hit window's damage of every weapon family's moves and every registered character's abilities, as one text.</summary>
        static string DamageSignature()
        {
            var lines = new List<string>();
            foreach (var id in CharacterCatalog.Ids) CharacterCatalog.Get(id);
            var sets = new MoveSets();
            void Add(string owner, MoveDef m)
            {
                if (m == null) return;
                lines.Add($"{owner}|{m.id}|" + string.Join(",", m.hits.Select(h => $"{h.from}-{h.to}:{h.dmg}")));
            }
            foreach (var fam in new[] { "katana", "twin", "spear", "chain", "cleaver", "fists", "tachi" })
            {
                var set = sets.Get(fam);
                if (set == null) continue;
                foreach (var m in set.All) Add(fam, m);
            }
            foreach (var id in CharacterCatalog.Ids)
                foreach (var a in CharacterCatalog.Get(id).abilities ?? new List<string>())
                    Add("ability:" + a, AbilityCatalog.Get(a)?.move);
            lines.Sort(System.StringComparer.Ordinal);
            return string.Join("\n", lines.Distinct());
        }

        static string Fnv(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (var c in Encoding.UTF8.GetBytes(s)) { h ^= c; h *= 1099511628211UL; }
            return h.ToString("x16");
        }

        // SAN-118 cleave rework (+ katana|airCleave|10-17:12) + Raikon Ascendant & Act I sub-boss martial abilities; no weapon-family or prior ability damage changed.
        // The Lightning Ascendant expansion (2026-11-27) adds nine abilities' lines only; the signature without them still hashes to the old 4ee367fd82c0cc64.
        // The reference kata (2026-11-27, the contact-matched parry pass) add four moves' lines only (katana and tachi crownWheel and tameCut);
        // the signature without them still hashes to the old 426889ffedf59f2e.
        // The chain / cleaver / spear rework (2026-11-27, owner-approved re-bless): new chain.cyclone 18-25:12, chain.dashHook 7-13:9,
        // spear.crescentCleave 12-18:12, spear.dashSkewer 7-14:10, spear.twinFang 8-11:5,15-20:6; changed chain.crescentLift 5-10:8 -> 6-10:5,8-14:7,
        // chain.graveHook 8-13:7 -> 8-14:7, chain.groundHook 8-13:4 -> 8-13:5, chain.pull 8-14:4 -> 8-13:4,20-24:7, chain.reap 7-11:7 -> 6-10:7,
        // chain.reapUp 6-11:7 -> 7-11:7, cleaver.charge 16-32:14 -> 17-26:15, cleaver.hack 11-16:8 -> 10-15:9, cleaver.hack2 12-17:9 -> 11-17:10,
        // cleaver.lowHack 10-15:7 -> 10-15:8, cleaver.slam 31-40:26 -> 27-36:26, cleaver.sweep 23-32:16 -> 22-30:16, spear.lunge 9-18:11 -> 10-20:12,
        // spear.sweep 10-16:8 -> 10-17:8, spear.vault 8-16:8 -> 8-17:9, and Shiden's chargingThrust 35-106:11 -> 35-106:12. Nothing else moved.
        const string DamageHash = "2979bded56742b6a";

        [Test]
        public void No_move_damage_changed_in_the_pacing_pass()
        {
            var sig = DamageSignature();
            Assert.AreEqual(DamageHash, Fnv(sig), $"{sig.Split('\n').Length} moves; the signature:\n{sig}");
        }
    }
}
