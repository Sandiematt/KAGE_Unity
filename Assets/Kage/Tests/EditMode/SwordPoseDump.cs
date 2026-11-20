using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// SAN-118 tool, not a check: writes every katana sword move's skeleton (and the stances and the run) at every quarter
    /// frame to Logs/san118_pose_&lt;tag&gt;.csv (tag from the KAGE_DUMP_TAG environment variable, default "now"), for stick
    /// figure plots and blade-sweep comparisons outside Unity. Explicit: runs only when named.
    /// </summary>
    [Explicit, Category("Diagnostics")]
    public class SwordPoseDump
    {
        static readonly B[] Bones =
        {
            B.Hips, B.Chest, B.Neck, B.Head, B.ArmNU, B.ArmNL, B.HandN, B.ArmFU, B.ArmFL, B.HandF, B.Sword,
            B.LegNU, B.LegNL, B.FootN, B.LegFU, B.LegFL, B.FootF,
        };

        [Test]
        public void DumpKatanaPoses()
        {
            var rig = KageModel.Skeleton("dump");
            var set = (KatanaMoves)new MoveSets().Get(WeaponFamilies.Katana);
            string tag = System.Environment.GetEnvironmentVariable("KAGE_DUMP_TAG");
            if (string.IsNullOrEmpty(tag)) tag = "now";
            var sb = new StringBuilder();
            sb.Append("move,frame,live,edgeUp,blade");
            foreach (var b in Bones) sb.Append($",{b}x,{b}y");
            sb.Append(",fistNx,fistNy,pommelx,pommely,guardx,guardy,tipx,tipy,toeNx,toeNy,toeFx,toeFy,headCx,headCy,gripErr\n");

            void Row(string name, float frame, bool live, Pose p)
            {
                var k = new PoseFK(rig).Solve(p);
                var head = rig.hurt.First(h => h.zone == HitZone.Head);
                string F(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
                sb.Append($"{name},{F(frame)},{(live ? 1 : 0)},{(p.edgeUp ? 1 : 0)},{F(KMath.Wrap(k.BladeAngle))}");
                foreach (var b in Bones) sb.Append($",{F(k.pos[(int)b].x)},{F(k.pos[(int)b].y)}");
                Vector2[] extra =
                {
                    k.NearFist, k.OnBlade(-0.17f), k.OnBlade(0.03f), k.OnBlade(0.80f),
                    k.Point(B.FootN, rig.footTipLocal), k.Point(B.FootF, rig.footTipLocal), k.Point(head.bone, head.local),
                };
                foreach (var v in extra) sb.Append($",{F(v.x)},{F(v.y)}");
                sb.Append($",{F(k.NearFistToHandle())}\n");
            }

            Row("stance", 0, false, PoseLib.Stance());
            Row("crouch", 0, false, PoseLib.Crouch());
            Row("block", 0, false, PoseLib.Block());
            Row("crouchBlock", 0, false, PoseLib.CrouchBlock());
            var run = new Pose();
            for (int i = 0; i < 16; i++) Row("run", i / 16f, false, PoseLib.RunCycle(i / 16f, run).Clone());
            var walk = new Pose();
            for (int i = 0; i < 16; i++) Row("walk", i / 16f, false, PoseLib.WalkCycle(i / 16f, 1f, walk).Clone());

            foreach (var m in set.All.Where(x => x != null && x.swordFront).Distinct())
            {
                Pose start = PoseLib.Stance();
                if (m == set.counterSlash) { var h = new Pose(); set.counterStance.Evaluate(rig, 15f, start, h); start = h.Clone(); }
                for (int i = 0; i <= Mathf.Min(m.total * 4, 800); i++)
                {
                    float f = i * 0.25f;
                    bool live = m.hits.Any(h => h.sword && f >= h.from && f <= h.to)
                                || (m.hits.Count == 0 && m.fx.Any(e => e.kind == FxKind.TrailBlade && f >= e.from && f <= e.to));
                    var p = new Pose();
                    m.Evaluate(rig, f, start, p);
                    Row(m.id, f, live, p);
                }
            }
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", $"san118_pose_{tag}.csv"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            Debug.Log($"[SAN-118] pose dump: {path}");
        }
    }
}
