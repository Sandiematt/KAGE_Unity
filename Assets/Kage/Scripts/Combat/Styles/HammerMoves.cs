using System.Collections.Generic;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// Iwakudaki's heavy war hammer / maul (<c>Earthbreaker</c>, docs/weapon-styles.md): crushing mass, deliberate
    /// counterbalanced windups, violent hip/shoulder torque, and ground-shaking impact across all seven signature attacks.
    /// <para>
    /// Core 7 attacks:
    /// 1. Light 1 — Crushing Overhead Smash (<c>iwakudaki.smash</c>, S): arched-back haul with lead foot lift -> brutal vertical slam + ground fissure &amp; shock VFX.
    /// 2. Light 2 — Earth-Scoop Uppercut (<c>iwakudaki.break</c>, S->S): rebound low-to-high diagonal scoop lifting the torso to launch/break guard.
    /// 3. Light 3 — Battering Haymaker (<c>iwakudaki.haymaker</c>, S->S->S): deep trailing-hip pivot unleashing a full-body horizontal battering arc.
    /// 4. Heavy / Charged — Meteor Sunder (<c>iwakudaki.meteorSunder</c>, Fwd+S): low back-foot charge surging into a hopping lunge overhead slam for maximum posture damage.
    /// 5. Low Attack — Shin-Breaker Drag (<c>iwakudaki.shinBreaker</c>, Down+S): deep crouched horizontal sweep inches above ground.
    /// 6. Up / Anti-Air — Rising Anvil (<c>iwakudaki.risingAnvil</c>, Up+S / Back+S): explosive vertical upward hammer thrust waist-to-sky.
    /// 7. Dash Attack — Iron Ram (<c>iwakudaki.ironRam</c>, Dash+S / Dash+P): braced diagonal haft driving the hammer head straight forward like a battering ram.
    /// </para>
    /// </summary>
    public sealed class HammerMoves : MoveSet
    {
        public const string SmashId = "iwakudaki.smash";
        public const string BreakId = "iwakudaki.break";
        public const string HaymakerId = "iwakudaki.haymaker";
        public const string MeteorSunderId = "iwakudaki.meteorSunder";
        public const string ShinBreakerId = "iwakudaki.shinBreaker";
        public const string RisingAnvilId = "iwakudaki.risingAnvil";
        public const string IronRamId = "iwakudaki.ironRam";

        /// <summary>The frame Crushing Overhead Smash meets the floor: fissure spawns, ground cracks, and tremor shakes.</summary>
        public const int SmashImpact = 29;
        /// <summary>The frame Meteor Sunder crashes into the floor.</summary>
        public const int MeteorImpact = 33;
        /// <summary>Legacy alias kept for Cleaver Ground Slam reference.</summary>
        public const int SlamImpact = 30;

        public const string FissureCue = "VFX_Iwakudaki_Fissure", FissureHitCue = "VFX_Iwakudaki_FissureHit";

        readonly CleaverMoves _cleaver;
        public readonly MoveDef smash, rise, haymaker, meteorSunder, shinBreaker, risingAnvil, ironRam;

        static readonly HitPoint[] Blade = { HitPoint.Blade };
        static Pose P() => new Pose();
        static Pose Hold(Pose p, float x, float y, float blade) => p.HandF(x, y).Blade(blade).Grip();

        /// <summary>Iwakudaki's counterbalanced heavy maul guard (matches Fighter.WeaponStance).</summary>
        static Pose Guard() =>
            P().Hip(-0.04f, -0.11f, 6f).Chest(10f).Head(-6f).Feet(0.34f, -0.34f)
                .HandF(0.14f, 1.18f).Blade(124f).Grip();

        /// <summary>
        /// Crushing Overhead Smash's ground split: a low wave along the floor (4 m ahead) catching standing legs.
        /// </summary>
        public static readonly ProjectileDef Fissure = new()
        {
            id = "iwakudaki.fissure",
            hit = new HitWindow { dmg = 8, level = HitLevel.Low, kb = new Vector2(0.8f, 3.2f), stun = 26, hitstop = 6, shake = 0.2f, heavy = true, radius = 0.3f },
            speed = 7.5f,
            groundWave = true,
            radius = 0.3f,
            lifeFrames = 34,
            maxHits = 1,
            samples = new[] { new Vector2(0.15f, 0f), new Vector2(0.15f, 0.35f) },
            flightCue = FissureCue, hitCue = FissureHitCue,
        };

        public HammerMoves(CleaverMoves cleaver)
        {
            _cleaver = cleaver;
            ShareBodyMoves(cleaver);

            // 1. Light 1 — Crushing Overhead Smash (High-to-Ground): arched back + lifted lead foot -> brutal vertical slam
            smash = new MoveDef(SmashId, "Crushing Overhead Smash", 62)
                .Key(11, Hold(P().Hip(-0.08f, -0.05f, 8f).Chest(22f).Head(-12f).FootN(0.28f, 0.20f, -14f).FootF(-0.30f, 0.13f), -0.12f, 1.78f, 146f), Ease.OutQuad)
                .Key(23, Hold(P().Hip(-0.10f, -0.02f, 11f).Chest(26f).Head(-14f).FootN(0.26f, 0.24f, -18f).FootF(-0.31f, 0.13f), -0.16f, 1.84f, 152f), Ease.InOutQuad)
                .Key(SmashImpact, Hold(P().Hip(0.14f, -0.26f, -10f).Chest(-36f).Head(18f).FootN(0.40f, 0.13f).FootF(-0.28f, 0.15f, -24f), 0.48f, 0.78f, -36f), Ease.InCubic)
                .Key(42, Hold(P().Hip(0.14f, -0.26f, -10f).Chest(-36f).Head(18f).FootN(0.40f, 0.13f).FootF(-0.28f, 0.15f, -24f), 0.48f, 0.76f, -36f))
                .Key(62, Guard(), Ease.InOutQuad)
                .Move(24, 29, 2.0f)
                .Armour(6, 31)
                .Hit(25, 31, Blade, 30, HitLevel.Overhead, new Vector2(1.0f, -3.6f), stun: 34, hitstop: 14, shake: 0.58f, knockdown: true, heavy: true, radius: 0.13f, postureMul: 1.9f)
                .Vector(StrikeVector.HighOverhead).Segment(0.54f, 1f)
                .Throw(SmashImpact, Fissure, new Vector2(1.05f, 0.22f))
                .Cue(SmashImpact, WeaponCues.HammerGroundImpactCue)
                .Gust(SmashImpact, new Vector2(4.0f, -12.0f), 0.22f)
                .Fx(22, 31, FxKind.TrailBlade).Fx(SmashImpact, SmashImpact, FxKind.Shockwave).Fx(SmashImpact, SmashImpact, FxKind.Dust).Sword().Cancel(38);

            // 2. Light 2 — Earth-Scoop Uppercut (Low-to-High): uses low rebound momentum to scoop diagonally upward shin-to-overhead
            rise = new MoveDef(BreakId, "Earth-Scoop Uppercut", 56)
                .Key(7, Hold(P().Hip(-0.04f, -0.22f, 2f).Chest(-16f).Head(10f).Feet(0.32f, -0.30f), -0.14f, 0.96f, -148f), Ease.OutQuad)
                .Key(18, Hold(P().Hip(-0.06f, -0.22f, 4f).Chest(-16f).Head(12f).Feet(0.32f, -0.32f), -0.18f, 0.98f, -152f), Ease.InOutQuad)
                .Key(22, Hold(P().Hip(0.04f, -0.14f, -4f).Chest(-10f).Head(6f).Feet(0.36f, -0.30f), 0.14f, 1.16f, -84f), Ease.OutQuad)
                .Key(25, Hold(P().Hip(0.10f, -0.12f, -6f).Chest(-6f).Head(4f).Feet(0.40f, -0.28f), 0.36f, 1.16f, -20f), Ease.OutQuad)
                .Key(29, Hold(P().Hip(0.12f, -0.04f, 8f).Chest(16f).Head(-10f).FootN(0.38f, 0.13f).FootF(-0.22f, 0.15f, -14f), 0.34f, 1.64f, 76f), Ease.OutExpo)
                .Key(38, Hold(P().Hip(0.12f, -0.04f, 8f).Chest(18f).Head(-10f).FootN(0.38f, 0.13f).FootF(-0.22f, 0.15f, -14f), 0.32f, 1.66f, 80f))
                .Key(56, Guard(), Ease.InOutQuad)
                .Move(19, 27, 2.4f)
                .Armour(4, 29)
                .Hit(22, 29, Blade, 24, HitLevel.Mid, new Vector2(2.2f, 5.8f), stun: 32, hitstop: 12, shake: 0.46f, launch: true, heavy: true, radius: 0.13f, postureMul: 1.75f)
                .Vector(StrikeVector.Upward).Segment(0.54f, 1f)
                .Gust(23, new Vector2(8.5f, 8.5f), 0.18f)
                .Fx(20, 30, FxKind.TrailBlade).Fx(20, 20, FxKind.Dust).Sword().Cancel(34);

            // 3. Light 3 — Battering Haymaker (Full-Body Horizontal Finisher): deep hip pivot behind trailing hip -> baseball battering arc
            haymaker = new MoveDef(HaymakerId, "Battering Haymaker", 60)
                .Key(10, Hold(P().Hip(-0.10f, -0.18f, 10f).Chest(12f).Head(-6f).Feet(0.30f, -0.34f), -0.22f, 1.04f, -168f), Ease.OutQuad)
                .Key(21, Hold(P().Hip(-0.14f, -0.20f, 12f).Chest(16f).Head(-8f).Feet(0.30f, -0.36f), -0.26f, 1.04f, -174f), Ease.InOutQuad)
                .Key(26, Hold(P().Hip(0.02f, -0.18f, -4f).Chest(-14f).Head(8f).Feet(0.38f, -0.32f), 0.18f, 1.08f, -84f), Ease.InCubic)
                .Key(30, Hold(P().Hip(0.18f, -0.18f, -16f).Chest(-32f).Head(16f).Feet(0.48f, -0.30f), 0.56f, 1.06f, -4f), Ease.OutExpo)
                .Key(42, Hold(P().Hip(0.18f, -0.18f, -16f).Chest(-32f).Head(16f).Feet(0.48f, -0.30f), 0.56f, 1.04f, -6f))
                .Key(60, Guard(), Ease.InOutQuad)
                .Move(21, 30, 3.8f)
                .Armour(6, 31)
                .Hit(25, 32, Blade, 28, HitLevel.Mid, new Vector2(5.4f, 2.2f), stun: 34, hitstop: 14, shake: 0.56f, knockdown: true, heavy: true, radius: 0.13f, postureMul: 1.95f)
                .Vector(StrikeVector.Mid).Segment(0.54f, 1f)
                .Gust(26, new Vector2(13.0f, 1.5f), 0.22f)
                .Fx(23, 33, FxKind.TrailBlade).Fx(23, 23, FxKind.Dust).Sword();

            // 4. Heavy / Charged — Meteor Sunder (Stepping Overhead Slam): low back-foot charge -> committed lunge-hop -> vertical crash
            meteorSunder = new MoveDef(MeteorSunderId, "Meteor Sunder", 66)
                .Key(12, Hold(P().Hip(-0.10f, -0.26f, 4f).Chest(-8f).Head(8f).FootN(0.28f, 0.13f).FootF(-0.32f, 0.15f, -24f), -0.20f, 0.88f, -148f), Ease.OutQuad)
                .Key(21, Hold(P().Hip(0.04f, 0.0f, 6f).Chest(20f).Head(-12f).LegN(70f, -100f).LegF(48f, -88f), -0.08f, 1.86f, 144f), Ease.OutQuad)
                .Key(27, Hold(P().Hip(0.06f, 0.0f, 8f).Chest(22f).Head(-12f).LegN(64f, -94f).LegF(44f, -84f), -0.10f, 1.88f, 150f))
                .Key(MeteorImpact, Hold(P().Hip(0.16f, -0.28f, -10f).Chest(-38f).Head(18f).FootN(0.42f, 0.13f).FootF(-0.28f, 0.15f, -26f), 0.50f, 0.78f, -36f), Ease.InCubic)
                .Key(46, Hold(P().Hip(0.16f, -0.28f, -10f).Chest(-38f).Head(18f).FootN(0.42f, 0.13f).FootF(-0.28f, 0.15f, -26f), 0.50f, 0.76f, -36f))
                .Key(66, Guard(), Ease.InOutQuad)
                .Move(15, 15, 3.8f, 4.4f, true)
                .Armour(8, 34)
                .Hit(29, 36, Blade, 32, HitLevel.Overhead, new Vector2(1.8f, -3.8f), stun: 36, hitstop: 14, shake: 0.62f, knockdown: true, otg: true, heavy: true, radius: 0.14f, postureMul: 2.0f)
                .Vector(StrikeVector.HighOverhead).Segment(0.52f, 1f)
                .Cue(MeteorImpact, WeaponCues.HammerGroundImpactCue)
                .Gust(MeteorImpact, new Vector2(4.5f, -13.0f), 0.24f)
                .Fx(26, 36, FxKind.TrailBlade).Fx(MeteorImpact, MeteorImpact, FxKind.Shockwave).Fx(MeteorImpact, MeteorImpact, FxKind.Dust).Sword();

            // 5. Low Attack — Shin-Breaker Drag: deep crouched horizontal sweep keeping heavy hammer head inches above ground
            shinBreaker = new MoveDef(ShinBreakerId, "Shin-Breaker Drag", 50)
                .Key(10, Hold(P().Hip(-0.08f, -0.34f, 4f).Chest(-20f).Head(14f).FootN(0.26f, 0.13f).FootF(-0.32f, 0.15f, -28f), -0.18f, 0.64f, -20f), Ease.OutQuad)
                .Key(18, Hold(P().Hip(-0.10f, -0.36f, 6f).Chest(-22f).Head(14f).FootN(0.26f, 0.13f).FootF(-0.32f, 0.15f, -28f), -0.22f, 0.60f, -18f), Ease.InOutQuad)
                .Key(23, Hold(P().Hip(0.10f, -0.38f, -8f).Chest(-32f).Head(18f).FootN(0.40f, 0.13f).FootF(-0.32f, 0.15f, -28f), 0.50f, 0.48f, -10f), Ease.OutExpo)
                .Key(32, Hold(P().Hip(0.10f, -0.38f, -8f).Chest(-32f).Head(18f).FootN(0.40f, 0.13f).FootF(-0.32f, 0.15f, -28f), 0.48f, 0.48f, -10f))
                .Key(50, Guard(), Ease.InOutQuad)
                .Move(18, 24, 2.2f)
                .Armour(8, 26)
                .Hit(20, 27, Blade, 18, HitLevel.Low, new Vector2(2.6f, 2.8f), stun: 28, hitstop: 11, shake: 0.38f, knockdown: true, otg: true, heavy: true, radius: 0.12f, postureMul: 1.6f)
                .Vector(StrikeVector.Low).Segment(0.52f, 1f)
                .Gust(21, new Vector2(10.0f, -3.5f), 0.18f)
                .Fx(18, 27, FxKind.TrailBlade).Fx(22, 22, FxKind.Dust).Sword();

            // 6. Up / Anti-Air — Rising Anvil: explosive vertical upward thrust/uppercut from waist to sky
            risingAnvil = new MoveDef(RisingAnvilId, "Rising Anvil", 48)
                .Key(10, Hold(P().Hip(0.02f, -0.32f, -6f).Chest(-24f).Head(14f).FootN(0.32f, 0.13f).FootF(-0.28f, 0.15f, -24f), 0.18f, 0.68f, 44f), Ease.OutQuad)
                .Key(14, Hold(P().Hip(0.06f, -0.14f, 0f).Chest(-2f).Head(0f).FootN(0.36f, 0.13f).FootF(-0.24f, 0.15f, -18f), 0.32f, 1.06f, 52f), Ease.OutQuad)
                .Key(18, Hold(P().Hip(0.08f, -0.02f, 6f).Chest(14f).Head(-10f).FootN(0.36f, 0.13f).FootF(-0.22f, 0.15f, -14f), 0.34f, 1.56f, 78f), Ease.OutExpo)
                .Key(26, Hold(P().Hip(0.08f, -0.02f, 6f).Chest(16f).Head(-10f).FootN(0.34f, 0.13f).FootF(-0.24f, 0.15f, -14f), 0.30f, 1.62f, 84f))
                .Key(48, Guard(), Ease.InOutQuad)
                .Move(10, 17, 1.6f)
                .Armour(6, 20)
                .Hit(13, 21, Blade, 20, HitLevel.Mid, new Vector2(0.6f, 7.6f), stun: 32, hitstop: 11, shake: 0.36f, launch: true, heavy: true, radius: 0.12f, postureMul: 1.65f)
                .Vector(StrikeVector.Upward).Segment(0.45f, 1f)
                .JumpCancel(22)
                .Gust(14, new Vector2(3.5f, 11.5f), 0.18f)
                .Fx(11, 22, FxKind.TrailBlade).Fx(13, 13, FxKind.Dust).Sword();

            // 7. Dash Attack — Iron Ram: forward charge bracing haft diagonally across chest, violently thrusting head forward like a battering ram
            ironRam = new MoveDef(IronRamId, "Iron Ram", 42)
                .Key(7, Hold(P().Hip(0.08f, -0.22f, -10f).Chest(-20f).Head(14f).Feet(0.40f, -0.32f), 0.14f, 1.04f, 10f), Ease.OutQuad)
                .Key(13, Hold(P().Hip(0.20f, -0.20f, -16f).Chest(-30f).Head(18f).Feet(0.48f, -0.30f), 0.56f, 1.06f, 6f), Ease.OutExpo)
                .Key(22, Hold(P().Hip(0.20f, -0.20f, -16f).Chest(-30f).Head(18f).Feet(0.48f, -0.30f), 0.54f, 1.06f, 6f))
                .Key(42, Guard(), Ease.InOutQuad)
                .Move(0, 14, 6.2f)
                .Armour(4, 19)
                .Hit(11, 19, Blade, 19, HitLevel.Mid, new Vector2(5.2f, 1.4f), stun: 30, hitstop: 12, shake: 0.42f, knockdown: true, heavy: true, radius: 0.13f, postureMul: 1.85f)
                .Vector(StrikeVector.Thrust).Segment(0.45f, 1f)
                .Gust(11, new Vector2(12.0f, 0.8f), 0.18f)
                .Fx(1, 19, FxKind.AfterImage).Fx(9, 19, FxKind.TrailBlade).Fx(10, 10, FxKind.Dust).Sword();

            // 3-hit Light combo chain on S: Crushing Overhead Smash -> Earth-Scoop Uppercut -> Battering Haymaker
            smash.chain[Btn.S] = rise;
            rise.chain[Btn.S] = haymaker;
        }

        /// <summary>Routes all 7 signature Hammer techniques on the ground; air and auxiliary moves fall back to Cleaver.</summary>
        public override MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self)
        {
            if (air) return _cleaver.Select(b, fwd, dy, air, self);
            if (self != null && self.IsForwardDash && b is Btn.S or Btn.P) return ironRam;
            if (b == Btn.S)
            {
                if (dy > 0) return risingAnvil;
                if (dy < 0) return shinBreaker;
                if (fwd > 0) return meteorSunder;
                if (fwd < 0) return risingAnvil;
                return smash;
            }
            return _cleaver.Select(b, fwd, dy, air, self);
        }

        public override MoveDef Ability(string abilityId) => _cleaver.Ability(abilityId);
        public override MoveDef BareHanded(MoveDef m) => _cleaver.BareHanded(m);

        public override IEnumerable<MoveDef> All
        {
            get
            {
                foreach (var m in _cleaver.All) yield return m;
                yield return smash;
                yield return rise;
                yield return haymaker;
                yield return meteorSunder;
                yield return shinBreaker;
                yield return risingAnvil;
                yield return ironRam;
            }
        }
    }
}
