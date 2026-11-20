using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// Gameplay timing is frozen while the poses are reworked (SAN-118): every move of every registered weapon family keeps
    /// its frame data (length, hit windows and their numbers and flags, root motion, cancels, invulnerability, armour,
    /// counter, spin, throw and effect frames). The expected text is a snapshot of main before SAN-118
    /// (<c>Data/MoveFrameData.txt</c>, generated from 0cf7534); a pose-only change leaves it equal.
    /// </summary>
    public class MoveFrameDataTests
    {
        static string Expected => Path.Combine(Application.dataPath, "Kage", "Tests", "EditMode", "Data", "MoveFrameData.txt");
        static string Actual => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MoveFrameData.actual.txt"));

        static readonly string[] Families =
        {
            WeaponFamilies.Katana, WeaponFamilies.Fists, WeaponFamilies.Twin, WeaponFamilies.Spear,
            WeaponFamilies.Chain, WeaponFamilies.Cleaver, WeaponFamilies.Tachi,
        };

        [Test]
        public void EveryMovesFrameDataMatchesTheSnapshotTakenBeforeThePoseRework()
        {
            string actual = Snapshot();
            Directory.CreateDirectory(Path.GetDirectoryName(Actual));
            File.WriteAllText(Actual, actual);
            Assert.IsTrue(File.Exists(Expected), $"no snapshot at {Expected}; this run's is at {Actual}");
            string[] want = File.ReadAllText(Expected).Replace("\r\n", "\n").Split('\n');
            string[] got = actual.Split('\n');
            for (int i = 0; i < Mathf.Min(want.Length, got.Length); i++)
                Assert.AreEqual(want[i], got[i], $"line {i + 1} (this run's snapshot: {Actual})");
            Assert.AreEqual(want.Length, got.Length, "line count");
        }

        static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        static string V(Vector2 v) => F(v.x) + "," + F(v.y);

        static string Snapshot()
        {
            var sb = new StringBuilder();
            foreach (var fam in Families)
            {
                var set = new MoveSets().Get(fam);
                foreach (var m in set.All.Where(x => x != null).Distinct().OrderBy(x => x.id, System.StringComparer.Ordinal))
                    Write(sb, fam, m);
            }
            return sb.ToString().Replace("\r\n", "\n");
        }

        static void Write(StringBuilder sb, string fam, MoveDef m)
        {
            sb.Append($"{fam}.{m.id} total={m.total} cancel={m.cancelFrom} jumpCancel={m.jumpCancelFrom} invuln={m.invulnFrom}..{m.invulnTo} " +
                      $"noPush={m.noPushFrom}..{m.noPushTo} armour={m.armorFrom}..{m.armorTo} counter={m.counterFrom}..{m.counterTo} " +
                      $"gravOff={m.gravityOffFrom}..{m.gravityOffTo} spin={m.spinFrom}..{m.spinTo}:{F(m.spinDeg)} air={m.airborne}/{m.endsOnLanding} " +
                      $"sword={m.swordFront} norm={m.normalizeAtEnd} throw={m.isThrow}:{m.grabFrame}..{m.grabTo}/{m.releaseFrame}/{m.throwDmg} " +
                      $"meter={m.meterCost} proj={m.projectileFrame} hazard={m.hazardFrame} vault={m.vaultFrom}..{m.vaultTo} brace={m.braceFrom}..{m.braceTo} " +
                      $"wallStagger={m.wallStagger}\n");
            foreach (var h in m.hits)
                sb.Append($"  hit {h.from}..{h.to} pts={string.Join("+", h.points)} r={F(h.radius)} dmg={h.dmg} lvl={h.level} kb={V(h.kb)} stun={h.stun} " +
                          $"kd={h.knockdown} launch={h.launch} otg={h.otg} heavy={h.heavy} sword={h.sword} breaks={h.breaksWeapon} stop={h.hitstop} " +
                          $"shake={F(h.shake)} posture={F(h.postureMul)} crit={h.critical} fixed={h.fixedDamage}\n");
            foreach (var mv in m.motions) sb.Append($"  move {mv.from}..{mv.to} v={V(mv.vel)} setY={mv.setY}\n");
            foreach (var fx in m.fx) sb.Append($"  fx {fx.from}..{fx.to} {fx.kind}\n");
            foreach (var e in m.extension) sb.Append($"  ext {e.frame} {F(e.metres)} {e.ease}\n");
            foreach (var c in m.cues) sb.Append($"  cue {c.frame} {c.cue}\n");
            foreach (var kv in m.chain.OrderBy(k => k.Key)) sb.Append($"  chain {kv.Key}->{kv.Value?.id}\n");
        }
    }
}
