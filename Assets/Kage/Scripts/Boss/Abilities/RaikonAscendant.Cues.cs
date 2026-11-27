using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The Lightning Ascendant's effect cues (ADR-0007): the three-colour telegraph, the lightning, the afterimages and the phase shifts. Each one is the
    /// brief in docs/raikon-ascendant-vfx.md drawn with <see cref="Vfx"/>'s lightning primitives (Vfx.Lightning.cs).
    /// </summary>
    public static partial class RaikonAscendant
    {
        // ---- the three-colour telegraph: steel = block / parry, red eye = dodge or parry tight (unguardable), crimson-white aura = unblockable ----
        public const string SteelCue = "VFX_Raikon_Tell_Steel", SteelHandCue = "VFX_Raikon_Tell_Steel_Hand", SteelFootCue = "VFX_Raikon_Tell_Steel_Foot";
        public const string EyeCue = "VFX_Raikon_Tell_Eye", AuraCue = "VFX_Raikon_Tell_Aura";
        // ---- afterimages (FighterRig.Snapshot ghosts; the frozen soft meshes are a MeshOwner's) ----
        public const string GhostCue = "VFX_Raikon_Blink_Ghost", DecoyCue = "VFX_Raikon_Decoy";
        // ---- on the body ----
        public const string BootStaticCue = "VFX_Raikon_Boot_Static", GroundSparkCue = "VFX_Raikon_Ground_Spark", PalmChargeCue = "VFX_Raikon_Palm_Charge";
        public const string PalmReleaseCue = "VFX_Raikon_Palm_Release", WheelCue = "VFX_Raikon_Wheel", FootArcCue = "VFX_Raikon_Foot_Arc";
        public const string BladeIgniteCue = "VFX_Raikon_Blade_Ignite", BladeArcCue = "VFX_Raikon_Blade_Arc", BladeStaticCue = "VFX_Raikon_Blade_Static";
        public const string IaiSliceCue = "VFX_Raikon_Iai_Slice";
        // ---- Final Overload ----
        public const string DimCue = "VFX_Raikon_Overload_Dim", InwardCue = "VFX_Raikon_Overload_Pull", ReleaseCue = "VFX_Raikon_Overload_Release";
        public const string FinisherCue = "VFX_Raikon_Overload_Finisher";
        // ---- hazards: telegraph and strike ----
        public const string SparkLineMarkCue = "VFX_Raikon_Floor_Spark_Line_Mark", SparkLineStrikeCue = "VFX_Raikon_Floor_Spark_Line";
        public const string TrailArcMarkCue = "VFX_Raikon_Trail_Arc_Mark", TrailArcStrikeCue = "VFX_Raikon_Trail_Arc";
        public const string VerticalBoltCue = "VFX_Raikon_Vertical_Bolt", BoltMarkCue = "VFX_Raikon_Sky_Bolt_Mark", SkyBoltCue = "VFX_Raikon_Sky_Bolt";
        public const string SlashMarkCue = "VFX_Raikon_Flurry_Slash_Mark", SlashDetonateCue = "VFX_Raikon_Flurry_Slash";
        public const string PlantShockCue = "VFX_Raikon_Plant_Shock", SpiralBurstCue = "VFX_Raikon_Spiral_Burst";
        // ---- the thunder pass: storm clouds gathering over him before the phase shifts' bolts, and a slam's thunder (scaled by phase) ----
        public const string GatherKindleCue = "VFX_Raikon_Cloud_Kindle", GatherAscendCue = "VFX_Raikon_Cloud_Ascend", GatherOverloadCue = "VFX_Raikon_Cloud_Overload";
        public const string SlamCue = "VFX_Raikon_Slam_Thunder";

        /// <summary>Seconds each phase shift's cloud takes to gather: to Storm Kindling's bolt (f6 → f30), Ascension's burst (f2 → f20), the Overload's release (f0 → f48).</summary>
        public const float KindleGather = 24 / 60f, AscendGather = 18 / 60f, OverloadGather = 48 / 60f;

        /// <summary>Every cue id this file and the expansion's (RaikonAscendant.ExpansionCues.cs) register.</summary>
        public static readonly string[] CueIds = Join(new[]
        {
            SteelCue, SteelHandCue, SteelFootCue, EyeCue, AuraCue, GhostCue, DecoyCue, BootStaticCue, GroundSparkCue, PalmChargeCue, PalmReleaseCue, WheelCue,
            FootArcCue, BladeIgniteCue, BladeArcCue, BladeStaticCue, IaiSliceCue, DimCue, InwardCue, ReleaseCue, FinisherCue, SparkLineMarkCue,
            SparkLineStrikeCue, TrailArcMarkCue, TrailArcStrikeCue, VerticalBoltCue, BoltMarkCue, SkyBoltCue, SlashMarkCue, SlashDetonateCue, PlantShockCue,
            SpiralBurstCue, GatherKindleCue, GatherAscendCue, GatherOverloadCue, SlamCue,
        }, ExpansionCueIds);

        static string[] Join(string[] a, string[] b)
        {
            var all = new string[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RegisterCues()
        {
            EffectCues.Register(SteelCue, (Vector2 p, in CueArgs a) => V?.SteelGlint(Tip(a, p, 0.9f)));
            EffectCues.Register(SteelHandCue, (Vector2 p, in CueArgs a) => V?.SteelGlint(Bone(a, B.HandF, p), 0.32f));
            EffectCues.Register(SteelFootCue, (Vector2 p, in CueArgs a) => V?.SteelGlint(Bone(a, B.FootN, p), 0.32f));
            EffectCues.Register(EyeCue, (Vector2 p, in CueArgs a) => V?.EyeFlash(EyePos(a.owner, p)));
            EffectCues.Register(AuraCue, (Vector2 p, in CueArgs a) => { V?.AuraPulse(Chest(a, p)); RaikonAura.Flare(a.owner); });
            EffectCues.Register(GhostCue, (Vector2 p, in CueArgs a) => V?.BlinkGhost(a.owner != null ? a.owner.rig : null));
            EffectCues.Register(DecoyCue, (Vector2 p, in CueArgs a) => V?.Decoy(a.owner != null ? a.owner.rig : null, DecoyPop.telegraphFrames / 60f, Chest(a, p)));
            EffectCues.Register(BootStaticCue, PlayBootStatic);
            EffectCues.Register(GroundSparkCue, PlayGroundSpark);
            EffectCues.Register(PalmChargeCue, PlayPalmCharge);
            EffectCues.Register(PalmReleaseCue, PlayPalmRelease);
            EffectCues.Register(WheelCue, (Vector2 p, in CueArgs a) => V?.WheelArcs(Chest(a, p), 1.0f));
            EffectCues.Register(FootArcCue, PlayFootArc);
            EffectCues.Register(BladeIgniteCue, PlayBladeIgnite);
            EffectCues.Register(BladeArcCue, (Vector2 p, in CueArgs a) => BladeArc(a.owner, 1));
            EffectCues.Register(BladeStaticCue, (Vector2 p, in CueArgs a) => BladeStatic(a.owner));
            EffectCues.Register(IaiSliceCue, PlayIaiSlice);
            EffectCues.Register(DimCue, (Vector2 p, in CueArgs a) => Vfx.Dim(0.35f, 1.1f));
            EffectCues.Register(InwardCue, (Vector2 p, in CueArgs a) => V?.InwardStreaks(Chest(a, p), 3.4f));
            EffectCues.Register(ReleaseCue, PlayRelease);
            EffectCues.Register(FinisherCue, PlayFinisher);
            EffectCues.Register(SparkLineMarkCue, (Vector2 p, in CueArgs a) => V?.SparkLineMark(p - Vector2.right * a.size.x * 0.5f, p + Vector2.right * a.size.x * 0.5f, a.seconds));
            EffectCues.Register(SparkLineStrikeCue, (Vector2 p, in CueArgs a) => V?.SparkLineStrike(p - Vector2.right * a.size.x * 0.5f, p + Vector2.right * a.size.x * 0.5f));
            EffectCues.Register(TrailArcMarkCue, (Vector2 p, in CueArgs a) => V?.SparkLineMark(p + new Vector2(-a.size.x * 0.5f, 0.9f), p + new Vector2(a.size.x * 0.5f, 0.9f), a.seconds));
            EffectCues.Register(TrailArcStrikeCue, PlayTrailArc);
            EffectCues.Register(VerticalBoltCue, PlayVerticalBolt);
            EffectCues.Register(BoltMarkCue, RaikonBoltMark.Play);
            EffectCues.Register(SkyBoltCue, PlaySkyBolt);
            EffectCues.Register(SlashMarkCue, (Vector2 p, in CueArgs a) => V?.SparkLineMark(p + new Vector2(-0.6f, 0.35f), p + new Vector2(0.6f, 1.5f), a.seconds));
            EffectCues.Register(SlashDetonateCue, PlaySlashDetonate);
            EffectCues.Register(PlantShockCue, PlayPlantShock);
            EffectCues.Register(SpiralBurstCue, PlaySpiralBurst);
            EffectCues.Register(GatherKindleCue, (Vector2 p, in CueArgs a) => GatherOver(a, 2.6f, KindleGather, 0.45f));
            EffectCues.Register(GatherAscendCue, (Vector2 p, in CueArgs a) => GatherOver(a, 3.6f, AscendGather, 0f));
            EffectCues.Register(GatherOverloadCue, (Vector2 p, in CueArgs a) => GatherOver(a, 5f, OverloadGather, 0f, 3));
            EffectCues.Register(SlamCue, PlaySlam);
            RegisterExpansionCues();
        }

        /// <summary>
        /// A storm cloud gathering over him for a phase shift's bolts, <paramref name="ahead"/> m in front of him (over Storm Kindling's planted blade),
        /// <paramref name="width"/> wide, at his phase's power unless <paramref name="power"/> says (an entry move plays after its phase has begun, so that
        /// is already the new phase's).
        /// </summary>
        static void GatherOver(in CueArgs a, float width, float seconds, float ahead, int power = -1)
        {
            if (V == null || a.owner == null) return;
            int pw = power >= 0 ? power : RaikonStorm.PowerOf(a.owner);
            ThunderCloud.Gather(a.owner.Pos.x + a.owner.facing * ahead, width, seconds, pw, null, a.owner, 1.2f);
        }

        /// <summary>Heavens' Verdict's and Final Overload's sky bolt: out of the cloud that gathered over its ring (a quick one if none), at his phase's power.</summary>
        static void PlaySkyBolt(Vector2 p, in CueArgs a)
        {
            var v = V;
            if (v == null) return;
            int power = RaikonStorm.PowerOf(a.owner);
            Vector2 top = ThunderCloud.StrikeOver(p.x, p.y, power, a.owner);
            v.ThunderStrike(p, top, 1f, power);
        }

        /// <summary>A slam's thunder where the blade meets the floor: cracks, and from phase 2 bolts erupting out of them (<see cref="Vfx.SlamThunder"/>).</summary>
        static void PlaySlam(Vector2 pos, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null) return;
            Vector2 tip = Tip(a, pos, 0.95f);
            v.SlamThunder(new Vector2(tip.x, a.owner.Pos.y), RaikonStorm.PowerOf(a.owner), a.owner.facing);
        }

        /// <summary>Ascension's burst: the crimson spiral, and the storm that gathered over him breaking: four bolts round him out of the cloud.</summary>
        static void PlaySpiralBurst(Vector2 p, in CueArgs a)
        {
            var v = V;
            if (v == null) return;
            v.SpiralBurst(p + Vector2.up * 1.1f, 2.6f);
            float x = a.owner != null ? a.owner.Pos.x : p.x, y = a.owner != null ? a.owner.Pos.y : p.y;
            var cloud = ThunderCloud.Near(x, 2.5f);
            float[] at = { -2.2f, -1.1f, 1.1f, 2.2f };
            for (int i = 0; i < at.Length; i++)
            {
                var g = new Vector2(x + at[i] + Random.Range(-0.2f, 0.2f), y);
                Vector2 top = cloud != null ? cloud.BoltTop(g.x) : g + Vector2.up * 7f;
                v.After(i * 0.04f, () => { if (Vfx.I != null) Vfx.I.ThunderStrike(g, top, 0.75f, 2, 0.3f); });
            }
            if (cloud != null) cloud.Strike(x);
        }

        /// <summary>Final Overload's release: five bolts out of the great cloud that gathered over the pull, round him and out to both walls' sides.</summary>
        static void OverloadBolts(Vector2 c, Fighter owner)
        {
            var v = V;
            if (v == null) return;
            float x = owner != null ? owner.Pos.x : c.x, y = owner != null ? owner.Pos.y : 0f;
            var cloud = ThunderCloud.Near(x, 3f);
            float[] at = { -3.2f, -1.6f, 0.9f, 2.0f, 3.4f };
            for (int i = 0; i < at.Length; i++)
            {
                var g = new Vector2(x + at[i] + Random.Range(-0.25f, 0.25f), y);
                Vector2 top = cloud != null ? cloud.BoltTop(g.x) : g + Vector2.up * 7f;
                v.After(0.03f + i * 0.05f, () => { if (Vfx.I != null) Vfx.I.ThunderStrike(g, top, 0.8f, 3, 0.25f); });
            }
            if (cloud != null) cloud.Strike(x);
        }

        static Vfx V => Vfx.I;

        // ---------------------------------------------------------------------------------------------- anchors
        /// <summary>A point on the painted blade at <paramref name="k"/> of its length (1 = the tip).</summary>
        static Vector2 Tip(in CueArgs a, Vector2 fallback, float k)
        {
            if (a.owner != null && a.owner.rig != null) return a.owner.rig.World(B.Sword, a.owner.BladeTipLocal * k);
            return fallback;
        }

        static Vector2 Bone(in CueArgs a, B bone, Vector2 fallback) => a.owner != null && a.owner.rig != null ? a.owner.rig.World(bone, Vector2.zero) : fallback;

        static Vector2 Chest(in CueArgs a, Vector2 fallback) => a.owner != null ? a.owner.Pos + Vector2.up * 1.05f : fallback;

        static readonly Dictionary<FighterRig, MeshRenderer> Eyes = new();

        /// <summary>Raikon's glowing eye (the rig part "Eye"), else a point on the head.</summary>
        public static Vector2 EyePos(Fighter f, Vector2 fallback)
        {
            if (f == null || f.rig == null) return fallback;
            if (!Eyes.TryGetValue(f.rig, out var eye) || eye == null)
            {
                eye = null;
                foreach (var r in f.rig.Renderers) if (r != null && r.name == "Eye") { eye = r; break; }
                if (Eyes.Count > 8) Eyes.Clear();
                Eyes[f.rig] = eye;
            }
            if (eye != null && eye.enabled) return eye.bounds.center;
            return f.rig.World(B.Head, new Vector2(0.06f, 0.06f));
        }

        // ---------------------------------------------------------------------------------------------- blade static
        /// <summary>One crawling arc between two points on the blade; <paramref name="n"/> of them.</summary>
        public static void BladeArc(Fighter f, int n)
        {
            var v = V;
            if (v == null || f == null || f.rig == null) return;
            Vector2 tip = f.BladeTipLocal;
            for (int i = 0; i < n; i++)
            {
                float t0 = Random.Range(0.2f, 0.8f), t1 = Mathf.Clamp01(t0 + Random.Range(0.12f, 0.3f));
                Vector2 a = f.rig.World(B.Sword, tip * t0), b = f.rig.World(B.Sword, tip * t1);
                Vector2 n2 = (b - a).sqrMagnitude > 1e-6f ? new Vector2(-(b - a).y, (b - a).x).normalized : Vector2.up;
                v.Arc(a + n2 * Random.Range(-0.03f, 0.03f), b + n2 * Random.Range(-0.04f, 0.04f), Random.Range(0.07f, 0.12f), 0.013f);
            }
            v.Spark(f.rig.World(B.Sword, tip * Random.Range(0.4f, 1f)), new Vector2(Random.Range(-0.8f, 0.8f), Random.Range(0.4f, 1.6f)), Vfx.HotWhite, 0.022f, 0.22f);
        }

        /// <summary>Phase 1's faint static: one tiny white spark off the blade, rarely a hair-thin arc. Never a tell shape.</summary>
        public static void BladeStatic(Fighter f)
        {
            var v = V;
            if (v == null || f == null || f.rig == null) return;
            Vector2 tip = f.BladeTipLocal;
            Vector2 at = f.rig.World(B.Sword, tip * Random.Range(0.3f, 1f));
            v.Spark(at, new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(0.3f, 1.1f)), Vfx.HotWhite, 0.018f, 0.18f);
            if (Random.value < 0.25f) v.Arc(at, at + Random.insideUnitCircle * 0.1f, 0.06f, 0.008f);
        }

        // ---------------------------------------------------------------------------------------------- body cues
        static void PlayBootStatic(Vector2 pos, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null || a.owner.rig == null) return;
            Vector2 n = a.owner.rig.World(B.FootN, Vector2.zero), f = a.owner.rig.World(B.FootF, Vector2.zero);
            v.Arc(n, f + Vector2.up * 0.05f, 0.09f, 0.014f);
            for (int i = 0; i < 2; i++)
                v.Spark(Vector2.Lerp(n, f, Random.value) + Vector2.up * 0.03f, new Vector2(Random.Range(-1.2f, 1.2f), Random.Range(0.5f, 1.6f)), i == 0 ? Color.white : Vfx.Crimson, 0.022f, 0.2f);
        }

        /// <summary>Low Sweep's sparks off the floor where the edge passes: warm metal sparks (phase 1 has no lightning).</summary>
        static void PlayGroundSpark(Vector2 pos, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null) return;
            float face = a.owner.facing;
            Vector2 at = new(Tip(a, pos, 0.9f).x, a.owner.Pos.y + 0.03f);
            var metal = new Color(1f, 0.86f, 0.7f, 1f);
            for (int i = 0; i < 9; i++)
                v.Spark(at + Vector2.right * Random.Range(-0.2f, 0.2f), new Vector2(face * Random.Range(2f, 5.5f), Random.Range(0.3f, 1.8f)), i % 3 == 0 ? Color.white : metal, 0.024f, Random.Range(0.18f, 0.35f));
            v.Dust(at, 0.6f, face);
        }

        static void PlayPalmCharge(Vector2 pos, in CueArgs a)
        {
            var v = V;
            if (v == null) return;
            Vector2 palm = Bone(a, B.HandN, pos);
            v.Crackle(palm, 0.16f, 0.16f, 1, Random.value < 0.5f ? Vfx.HotWhite : Vfx.Crimson);
            for (int i = 0; i < 2; i++)
            {
                Vector2 off = Random.insideUnitCircle.normalized * Random.Range(0.35f, 0.6f);
                v.Spark(palm + off, -off / 0.18f, i == 0 ? Color.white : Vfx.Crimson, 0.025f, 0.18f);
            }
        }

        static void PlayPalmRelease(Vector2 pos, in CueArgs a)
        {
            var v = V;
            if (v == null) return;
            Vector2 palm = Bone(a, B.HandN, pos);
            float face = a.owner != null ? a.owner.facing : 1f;
            v.Star(palm, 0.7f, Color.white, Vfx.Crimson);
            v.LensStreak(palm + Vector2.right * face * 0.5f, 1.8f, Vfx.Crimson, 0.2f, 0f, 0.03f);
            v.Ring(palm, 0.05f, 0.7f, 0.06f, Vfx.Crimson, 0.25f, true, 1f, 2.2f);
            v.Crackle(palm + Vector2.right * face * 0.4f, 0.5f, 0.35f, 3);
            for (int i = 0; i < 12; i++)
                v.Spark(palm, KMath.Dir(Random.Range(-35f, 35f) + (face > 0f ? 0f : 180f)) * Random.Range(3f, 8f), i % 3 == 0 ? Color.white : Vfx.Crimson, 0.035f, Random.Range(0.25f, 0.45f));
        }

        static void PlayFootArc(Vector2 pos, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null || a.owner.rig == null) return;
            Vector2 knee = a.owner.rig.World(B.LegNL, Vector2.zero), foot = a.owner.rig.World(B.FootN, Vector2.zero);
            v.Arc(knee, foot, 0.14f, 0.02f);
            v.Arc(foot, foot + Random.insideUnitCircle * 0.35f, 0.1f, 0.014f);
        }

        /// <summary>Storm Kindling's bolt: from the sky into the planted blade's tip, arcs running down the blade, a star on the point.</summary>
        static void PlayBladeIgnite(Vector2 pos, in CueArgs a)
        {
            var v = V;
            if (v == null) return;
            Vector2 tip = Tip(a, pos, 1f), hilt = Tip(a, pos, 0f);
            Vector2 top = ThunderCloud.StrikeOver(tip.x, tip.y, 1, a.owner, 2.6f);            // out of the cloud that gathered over the planted blade
            v.Bolt(top, tip, 0.065f, 0.45f, Vfx.Crimson, 18, 0.32f, 3);
            v.LightPillar(tip, top, 0.4f, 0.45f, new Color(1f, 0.35f, 0.4f, 0.9f));
            v.BackdropFlash(0.16f, 0.45f);
            v.Star(tip, 0.9f, Color.white, Vfx.Crimson);
            v.Arc(tip, hilt, 0.2f, 0.022f);
            v.Arc(Vector2.Lerp(tip, hilt, 0.3f), hilt, 0.16f, 0.016f);
            v.Scorch(new Vector2(tip.x, a.owner != null ? a.owner.Pos.y : tip.y), 0.6f);
            GameFeel.Shake(0.4f);
            GameFeel.Flash(new Color(1f, 0.55f, 0.55f, 0.18f), 0.15f);
        }

        /// <summary>The iaijutsu's long thin slice line across the path he just ran, its dark residue under it.</summary>
        static void PlayIaiSlice(Vector2 pos, in CueArgs a)
        {
            var v = V;
            if (v == null || a.owner == null) return;
            float face = a.owner.facing;
            Vector2 end = a.owner.Pos + new Vector2(face * 0.5f, 1.0f), start = end - new Vector2(face * (FlashDistance + 0.6f), 0.05f);
            v.SlashLine(start, end, Vfx.Crimson, Color.white, 0.32f, 0.035f);
        }

        static void PlayRelease(Vector2 pos, in CueArgs a)
        {
            var v = V;
            if (v == null) return;
            Vector2 c = Chest(a, pos);
            v.Star(c, 1.4f, Color.white, Vfx.Crimson);
            v.Ring(c + Vector2.down * 1.0f, 0.2f, 3.2f, 0.12f, Vfx.Crimson, 0.5f, false, 0.22f, 2.4f);
            v.Crackle(c, 1.0f, 1.2f, 5);
            OverloadBolts(c, a.owner);
            GameFeel.Flash(new Color(1f, 0.6f, 0.6f, 0.25f), 0.2f);
            GameFeel.Shake(0.55f);
        }

        /// <summary>The finisher's screen feedback: the only white flash frame of the fight, a punch and a shake.</summary>
        static void PlayFinisher(Vector2 pos, in CueArgs a)
        {
            GameFeel.Flash(new Color(1f, 0.95f, 0.95f, 0.45f), 0.12f);
            GameFeel.Punch(0.2f);
            GameFeel.Shake(0.65f);
        }

        // ---------------------------------------------------------------------------------------------- hazard cues
        static void PlayTrailArc(Vector2 pos, in CueArgs a)
        {
            var v = V;
            if (v == null) return;
            float half = a.size.x * 0.5f;
            Vector2 l = pos + new Vector2(-half, 0.9f), r = pos + new Vector2(half, 0.9f);
            v.Bolt(l, r, 0.05f, 0.32f, Vfx.Crimson, Mathf.Clamp(Mathf.RoundToInt(a.size.x / 0.3f), 8, 40), 0.22f, 4);
            for (int i = 0; i < 10; i++)
                v.Spark(Vector2.Lerp(l, r, Random.value), new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(-1f, 2.5f)), i % 2 == 0 ? Color.white : Vfx.Crimson, 0.03f, 0.35f);
            GameFeel.Shake(0.25f);
        }

        static void PlayVerticalBolt(Vector2 pos, in CueArgs a)
        {
            var v = V;
            if (v == null) return;
            float h = a.size.y > 0f ? a.size.y : 4f;
            Vector2 top = ThunderCloud.StrikeOver(pos.x, pos.y, RaikonStorm.PowerOf(a.owner), a.owner, 1.6f);     // it rises into a cloud that flashes
            v.Bolt(pos, top, 0.045f, 0.3f, Vfx.Crimson, 14, 0.16f, 2);
            v.Star(pos + Vector2.up * 1.2f, 0.7f, Color.white, Vfx.Crimson);
            for (int i = 0; i < 8; i++)
                v.Spark(pos + Vector2.up * Random.Range(0.2f, h * 0.6f), new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(0f, 2f)), i % 2 == 0 ? Color.white : Vfx.Crimson, 0.03f, 0.3f);
            v.Scorch(pos, 0.4f, 0.6f);
        }

        static void PlaySlashDetonate(Vector2 pos, in CueArgs a)
        {
            var v = V;
            if (v == null) return;
            Vector2 lo = pos + new Vector2(-0.6f, 0.35f), hi = pos + new Vector2(0.6f, 1.5f);
            v.SlashLine(lo, hi, Vfx.Crimson, Color.white, 0.24f, 0.06f);
            v.Bolt(lo, hi, 0.03f, 0.22f, Vfx.Crimson, 8, 0.1f, 1);
            v.Star((lo + hi) * 0.5f, 0.6f, Color.white, Vfx.Crimson);
            for (int i = 0; i < 8; i++)
                v.Spark((lo + hi) * 0.5f, Random.insideUnitCircle.normalized * Random.Range(2f, 5f), i % 2 == 0 ? Color.white : Vfx.Crimson, 0.03f, 0.3f);
            GameFeel.Shake(0.2f);
        }

        /// <summary>Storm Kindling's shockwave: no lightning yet (the bolt is the next beat): a flash at the blade's foot, a wide ground ellipse, a dust roll.</summary>
        static void PlayPlantShock(Vector2 pos, in CueArgs a)
        {
            var v = V;
            if (v == null) return;
            v.Star(pos + Vector2.up * 0.3f, 1.0f, Color.white, new Color(0.9f, 0.85f, 0.85f, 1f));
            v.Ring(pos + Vector2.up * 0.03f, 0.1f, 2.8f, 0.12f, new Color(1f, 0.92f, 0.9f, 0.9f), 0.5f, false, 0.2f, 2f);
            v.Ring(pos + Vector2.up * 0.03f, 0.05f, 1.6f, 0.06f, Color.white, 0.3f, false, 0.2f, 1.6f);
            v.Dust(pos, 1.8f, 1f);
            v.Dust(pos, 1.8f, -1f);
            GameFeel.Shake(0.5f);
        }
    }

    /// <summary>
    /// A sky bolt's ring on the floor (Heavens' Verdict, Final Overload): it rides the hazard (which follows Kage, then locks), pulses every 4 frames
    /// and tightens as the strike nears, with arcs crawling in it in the last 40 % and sparks in the last 20 %.
    /// </summary>
    public sealed class RaikonBoltMark : MonoBehaviour
    {
        float _t, _life, _pulse, _w;

        public static void Play(Vector2 pos, in CueArgs a)
        {
            if (Vfx.I == null) return;
            // the storm cloud the bolt will fall out of gathers over the ring for the whole telegraph, riding it while it tracks Kage
            ThunderCloud.Gather(pos.x, 2.4f, Mathf.Max(0.1f, a.seconds), RaikonStorm.PowerOf(a.owner), a.follow, a.owner);
            if (a.follow == null) { Vfx.I.Ring(pos + Vector2.up * 0.03f, 0.3f, 0.8f, 0.04f, Vfx.Crimson, Mathf.Max(0.2f, a.seconds), false, 0.22f, 1.6f); return; }
            var go = new GameObject("RaikonBoltMark");
            go.transform.SetParent(a.follow, false);
            var m = go.AddComponent<RaikonBoltMark>();
            m._life = Mathf.Max(0.1f, a.seconds);
            m._w = a.size.x > 0f ? a.size.x * 0.5f : 0.6f;
        }

        void Update()
        {
            var v = Vfx.I;
            _t += Time.deltaTime;
            if (v == null || _t >= _life) { Destroy(gameObject); return; }
            float u = _t / _life;
            _pulse -= Time.deltaTime;
            if (_pulse > 0f) return;
            _pulse = 0.067f;
            Vector2 p = transform.position;
            float r = Mathf.Lerp(_w * 1.6f, _w * 0.8f, u);
            v.Ring(p + Vector2.up * 0.03f, r * 0.85f, r, 0.025f + 0.035f * u, Vfx.Crimson.WithA(0.3f + 0.6f * u), 0.12f, false, 0.22f, 1.3f + u);
            if (u > 0.6f) v.Crackle(p + Vector2.up * 0.04f, r, r * 0.2f, 1);
            if (u > 0.8f) v.Spark(p + new Vector2(Random.Range(-r, r), 0.04f), new Vector2(0f, Random.Range(1.5f, 3f)), Color.white, 0.022f, 0.2f);
        }
    }
}
