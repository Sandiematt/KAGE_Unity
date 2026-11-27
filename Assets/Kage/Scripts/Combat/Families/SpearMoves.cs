using System.Collections.Generic;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// The spear and staff family (ADR-0006, SAN-78): Mizuchi, and the polearms of the Shieldbearer, Kuroe and Shiden.
    /// Long-range linear precision, sliding-grip two-handed thrusts where the rear hand drives the shaft cleanly through
    /// the guiding lead hand, and fluid serpent-like shaft control.
    /// <para>
    /// Core 7 attacks:
    /// 1. Light 1 — Needle Thrust (<c>spear.thrust</c>, S): coiled hip pull-back into explosive horizontal sliding-grip chest thrust.
    /// 2. Light 2 — Twin Fang Flurry (<c>spear.twinFang</c>, S->S): low shin thrust snapping into a rear-hand-angled high throat thrust.
    /// 3. Light 3 — Crescent Whip-Cleave (<c>spear.crescentCleave</c>, S->S->S): rear hand vaults shaft high into a wide downward diagonal crescent.
    /// 4. Heavy / Charged — Dragon Piercer (<c>spear.lunge</c>, Fwd+S): low crouched coil erupting into a committed max-velocity piercing lunge.
    /// 5. Low Attack — Riverbed Sweep (<c>spear.sweep</c>, Down+S): low split lunge sweeping the spearhead across ankle level.
    /// 6. Up / Anti-Air — Rising Serpent (<c>spear.vault</c>, Up+S / Back+K): steep 60-degree upward diagonal sliding thrust.
    /// 7. Dash Attack — Vaulting Skewer (<c>spear.dashSkewer</c>, Dash+S / Dash+P): fast forward glide snapping into a mid-height pierce.
    /// </para>
    /// </summary>
    public sealed class SpearMoves : MoveSet
    {
        public MoveDef jab, poke2, stepPoke, lowPoke, skewer, kick, pushKick, lowKick;
        public MoveDef thrust, twinFang, crescentCleave, lunge, sweep, guardRaise, vault, dashSkewer;
        public MoveDef airPoke, airDive, airSweep;

        /// <summary>Metres from the lead hand's guard point to the spear's tip (JS Mizuchi reach 96 against the katana's 58).</summary>
        public const float BladeLength = 1.45f;

        public static readonly WeaponFamilyDef Family = new()
        {
            id = WeaponFamilies.Spear,
            referenceWeapon = "mizuchi",
            build = sets => Build((KatanaMoves)sets.Get(WeaponFamilies.Katana)),
            grip = GripPose.Polearm,
            bladeLength = BladeLength,
            rageArt = Fighter.ThousandShadows,
            visual = new SpearVisual(),
        };

        static readonly HitPoint[] Blade = { HitPoint.Blade };
        static readonly HitPoint[] FootN = { HitPoint.FootN, HitPoint.ShinN };
        static readonly HitPoint[] FootTip = { HitPoint.FootN };

        static Pose P() => new Pose();

        /// <summary>Mizuchi's wide two-handed martial idle guard (matches Fighter.WeaponStance).</summary>
        static Pose Guard() =>
            P().Hip(0.02f, -0.12f, -4f).Chest(-10f).Head(8f).Feet(0.34f, -0.32f)
                .HandF(0.32f, 1.00f).Blade(18f).Grip();

        /// <summary>Both hands on the shaft: lead hand guides at (x, y), shaft at <paramref name="blade"/> deg, optional forward slide through lead hand.</summary>
        static Pose Hold(Pose p, float x, float y, float blade, float slide = 0f, float spacing = 0f, float pommel = 0f)
        {
            p.HandF(x, y).Blade(blade).Grip();
            if (slide != 0f) p.ShaftSlide(slide);
            if (spacing < 0f) p.GripSpan(spacing, pommel < 0f ? pommel : -0.66f);
            return p;
        }

        /// <summary>Grounded lunging body with hip drive and forward torso alignment.</summary>
        static Pose Lunge(float hx = 0.12f, float hy = -0.14f, float hipRot = -11f, float chest = -22f, float feetN = 0.40f, float feetF = -0.30f) =>
            P().Hip(hx, hy, hipRot).Chest(chest).Head(12f).Feet(feetN, feetF);

        static Pose LowBody(float hx = 0.04f, float hy = -0.32f, float hipRot = -6f, float chest = -26f) =>
            P().Hip(hx, hy, hipRot).Chest(chest).Head(15f).FootN(0.34f, 0.13f).FootF(-0.28f, 0.15f, -26f);

        SpearMoves(KatanaMoves katana) { ShareBodyMoves(katana); }

        public static SpearMoves Build(KatanaMoves katana)
        {
            var m = new SpearMoves(katana);

            // ===================== PUNCH BUTTON: SLIDING-GRIP POKES =====================
            m.jab = new MoveDef("spear.jab", "Quick Poke", 20)
                .Key(4, Hold(P().Hip(-0.04f, -0.12f, 4f).Chest(4f).Head(-2f).Feet(0.30f, -0.30f), 0.24f, 1.00f, 12f, -0.18f, -0.34f, -0.62f), Ease.OutQuad)
                .Key(7, Hold(Lunge(0.10f, -0.13f, -10f, -20f, 0.38f, -0.28f), 0.50f, 1.04f, 10f, 0.20f, -0.44f, -0.66f), Ease.OutExpo)
                .Key(11, Hold(Lunge(0.10f, -0.13f, -10f, -20f, 0.38f, -0.28f), 0.48f, 1.04f, 10f, 0.18f, -0.44f, -0.66f))
                .Key(15, Hold(Guard(), 0.34f, 1.02f, 14f, 0.04f, -0.40f, -0.62f), Ease.OutQuad)
                .Key(20, Guard(), Ease.InOutQuad)
                .Hit(5, 8, Blade, 5, HitLevel.High, new Vector2(1.0f, 0f), stun: 13, hitstop: 4, shake: 0.06f, radius: 0.075f)
                .Vector(StrikeVector.Thrust).Segment(0.28f, 1f)
                .Gust(5, new Vector2(6.5f, 0.4f), 0.12f)
                .Fx(4, 9, FxKind.TrailBlade).Sword().Cancel(8);

            m.poke2 = new MoveDef("spear.poke2", "Rising Poke", 24)
                .Key(4, Hold(LowBody(0.0f, -0.24f, -4f, -18f), 0.22f, 0.80f, 24f, -0.18f, -0.34f, -0.62f), Ease.OutQuad)
                .Key(8, Hold(P().Hip(0.10f, -0.10f, -6f).Chest(-10f).Head(4f).Feet(0.38f, -0.26f), 0.48f, 1.14f, 34f, 0.22f, -0.44f, -0.66f), Ease.OutExpo)
                .Key(13, Hold(P().Hip(0.10f, -0.10f, -6f).Chest(-10f).Head(4f).Feet(0.38f, -0.26f), 0.46f, 1.16f, 36f, 0.18f, -0.44f, -0.66f))
                .Key(24, Guard(), Ease.InOutQuad)
                .Hit(7, 11, Blade, 5, HitLevel.High, new Vector2(0.9f, 1.8f), stun: 16, hitstop: 4, shake: 0.08f, radius: 0.075f)
                .Vector(StrikeVector.Upward).Segment(0.26f, 1f)
                .Gust(7, new Vector2(6.0f, 4.0f), 0.13f)
                .Fx(5, 12, FxKind.TrailBlade).Sword().Cancel(11);

            m.stepPoke = new MoveDef("spear.stepPoke", "Step Poke", 26)
                .Key(5, Hold(P().Hip(-0.04f, -0.14f, 4f).Chest(4f).Head(-2f).Feet(0.28f, -0.30f), 0.22f, 1.00f, 10f, -0.20f, -0.34f, -0.62f), Ease.OutQuad)
                .Key(9, Hold(Lunge(0.14f, -0.14f, -12f, -24f, 0.44f, -0.26f), 0.54f, 1.02f, 8f, 0.24f, -0.46f, -0.66f), Ease.OutExpo)
                .Key(14, Hold(Lunge(0.14f, -0.14f, -12f, -24f, 0.44f, -0.26f), 0.52f, 1.02f, 8f, 0.22f, -0.46f, -0.66f))
                .Key(26, Guard(), Ease.InOutQuad)
                .Move(3, 10, 3.2f)
                .Hit(8, 12, Blade, 6, HitLevel.High, new Vector2(1.6f, 0.2f), stun: 16, hitstop: 5, shake: 0.1f, radius: 0.075f)
                .Vector(StrikeVector.Thrust).Segment(0.28f, 1f)
                .Gust(8, new Vector2(8.0f, 0.5f), 0.13f)
                .Fx(7, 12, FxKind.TrailBlade).Fx(3, 3, FxKind.Dust).Sword().Cancel(12);

            m.lowPoke = new MoveDef("spear.lowPoke", "Low Poke", 22)
                .Key(4, Hold(Crouch(), 0.20f, 0.72f, -6f, -0.16f, -0.34f, -0.62f), Ease.OutQuad)
                .Key(7, Hold(LowBody(0.06f, -0.32f, -6f, -24f), 0.50f, 0.64f, -8f, 0.20f, -0.44f, -0.66f), Ease.OutExpo)
                .Key(11, Hold(LowBody(0.06f, -0.32f, -6f, -24f), 0.48f, 0.64f, -8f, 0.18f, -0.44f, -0.66f))
                .Key(22, Crouch(), Ease.InOutQuad)
                .Hit(6, 9, Blade, 4, HitLevel.Low, new Vector2(0.9f, 0f), stun: 12, hitstop: 4, shake: 0.05f, radius: 0.075f)
                .Vector(StrikeVector.Thrust).Segment(0.28f, 1f)
                .Gust(6, new Vector2(6.5f, -0.8f), 0.12f)
                .Fx(5, 9, FxKind.TrailBlade).Sword().Cancel(9);

            m.skewer = new MoveDef("spear.skewer", "Skewer", 30)
                .Key(6, Hold(P().Hip(-0.02f, -0.10f, 4f).Chest(10f).Head(-6f).Feet(0.28f, -0.28f), 0.22f, 1.48f, 94f, -0.18f, -0.36f, -0.62f), Ease.OutQuad)
                .Key(12, Hold(P().Hip(0.12f, -0.22f, -12f).Chest(-32f).Head(16f).Feet(0.38f, -0.28f), 0.54f, 0.88f, -40f, 0.16f, -0.44f, -0.66f), Ease.InCubic)
                .Key(18, Hold(P().Hip(0.12f, -0.22f, -12f).Chest(-32f).Head(16f).Feet(0.38f, -0.28f), 0.54f, 0.86f, -40f, 0.14f, -0.44f, -0.66f))
                .Key(30, Guard(), Ease.InOutQuad)
                .Hit(11, 17, Blade, 6, HitLevel.Mid, new Vector2(0.4f, 1.0f), stun: 18, hitstop: 8, shake: 0.2f, otg: true, radius: 0.085f)
                .Vector(StrikeVector.Thrust).Segment(0.30f, 1f)
                .Gust(11, new Vector2(5.5f, -7.5f), 0.14f)
                .Fx(9, 17, FxKind.TrailBlade).Fx(13, 13, FxKind.Dust).Sword();

            // ===================== KICK BUTTON: MARTIAL KICKS & RISING SERPENT =====================
            m.kick = new MoveDef("spear.kick", "Front Kick", 26)
                .Key(6, P().Hip(0.02f, -0.07f, 10f).Chest(8f).Head(-4f).LegN(98f, -118f, 10f).FootF(-0.16f, 0.13f).HandF(0.10f, 1.02f).Blade(78f).HandN(0.20f, 1.24f, 10f))
                .Key(10, P().Hip(0.12f, -0.04f, 18f).Chest(14f).Head(-8f).LegN(104f, -6f, 20f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.04f).Blade(80f).HandN(0.12f, 1.26f, 10f), Ease.OutExpo)
                .Key(14, P().Hip(0.12f, -0.04f, 18f).Chest(14f).Head(-8f).LegN(102f, -8f, 20f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.04f).Blade(80f).HandN(0.12f, 1.26f, 10f))
                .Key(26, Guard(), Ease.InOutQuad)
                .Hit(9, 13, FootN, 5, HitLevel.Mid, new Vector2(1.6f, 0.4f), stun: 16, hitstop: 5, shake: 0.1f, radius: 0.1f)
                .Vector(StrikeVector.Mid)
                .Fx(8, 14, FxKind.TrailFootN).Cancel(13);

            m.pushKick = new MoveDef("spear.pushKick", "Push Kick", 28)
                .Key(6, P().Hip(0f, -0.06f, 8f).Chest(8f).LegN(100f, -120f, 15f).FootF(-0.2f, 0.13f).HandF(0.12f, 1.0f).Blade(76f).HandN(0.2f, 1.22f, 10f))
                .Key(10, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(96f, -4f, 40f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.02f).Blade(78f).HandN(0.1f, 1.24f, 10f), Ease.OutExpo)
                .Key(14, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(95f, -5f, 40f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.02f).Blade(78f).HandN(0.1f, 1.24f, 10f))
                .Key(28, Guard(), Ease.InOutQuad)
                .Hit(9, 13, FootTip, 5, HitLevel.Mid, new Vector2(4.0f, 0.6f), stun: 20, hitstop: 6, shake: 0.15f, radius: 0.11f)
                .Vector(StrikeVector.Thrust)
                .Fx(8, 13, FxKind.TrailFootN);

            m.lowKick = new MoveDef("spear.lowKick", "Low Kick", 24)
                .Key(5, P().Hip(0.0f, -0.32f, 4f).Chest(-14f).Head(10f).LegN(80f, -100f, 10f).FootF(-0.26f, 0.15f, -25f).HandF(0.14f, 0.86f).Blade(70f).HandN(0.22f, 1.0f, 10f))
                .Key(8, P().Hip(0.06f, -0.3f, 8f).Chest(-8f).Head(8f).LegN(92f, -4f, 20f).FootF(-0.26f, 0.15f, -25f).HandF(0.12f, 0.88f).Blade(72f).HandN(0.2f, 1.02f, 10f), Ease.OutExpo)
                .Key(12, P().Hip(0.06f, -0.3f, 8f).Chest(-8f).Head(8f).LegN(90f, -6f, 20f).FootF(-0.26f, 0.15f, -25f).HandF(0.12f, 0.88f).Blade(72f).HandN(0.2f, 1.02f, 10f))
                .Key(24, Crouch(), Ease.InOutQuad)
                .Hit(7, 11, FootN, 4, HitLevel.Low, new Vector2(0.9f, 0f), stun: 14, hitstop: 4, shake: 0.06f, radius: 0.1f)
                .Vector(StrikeVector.Low)
                .Fx(6, 11, FxKind.TrailFootN).Cancel(11);

            // 6. Up / Anti-Air — Rising Serpent: steep 60-degree upward diagonal sliding thrust from back hip to sky
            m.vault = new MoveDef("spear.vault", "Rising Serpent", 40)
                .Key(6, Hold(LowBody(-0.04f, -0.28f, -4f, -16f), 0.22f, 0.78f, 60f, -0.22f, -0.34f, -0.60f), Ease.OutQuad)
                .Key(10, Hold(P().Hip(0.08f, -0.02f, 4f).Chest(10f).Head(-10f).LegN(68f, -98f).LegF(48f, -88f), 0.36f, 1.38f, 60f, 0.28f, -0.44f, -0.66f), Ease.OutExpo)
                .Key(17, Hold(P().Hip(0.08f, -0.02f, 6f).Chest(12f).Head(-10f).LegN(60f, -90f).LegF(40f, -80f), 0.38f, 1.44f, 60f, 0.24f, -0.44f, -0.66f))
                .Key(26, Hold(P().Hip(0.04f, -0.10f, 0f).Chest(2f).Head(-2f).Feet(0.32f, -0.30f), 0.34f, 1.18f, 42f, 0.04f, -0.40f, -0.62f), Ease.OutQuad)
                .Key(40, Guard(), Ease.InOutQuad)
                .Move(6, 6, 1.6f, 6.2f, true)
                .Hit(8, 17, Blade, 9, HitLevel.Mid, new Vector2(0.5f, 7.2f), stun: 30, hitstop: 7, shake: 0.20f, launch: true, heavy: true, radius: 0.09f)
                .Vector(StrikeVector.Upward).Segment(0.24f, 1f)
                .Gust(8, new Vector2(6.0f, 9.0f), 0.16f)
                .JumpCancel(18)
                .Fx(6, 17, FxKind.TrailBlade).Fx(6, 6, FxKind.Dust).Sword();

            // ===================== WEAPON BUTTON: MIZUCHI'S 7 CORE TECHNIQUES =====================
            // 1. Light 1 — Needle Thrust (Mid Linear): coiled hip pull-back -> fast horizontal sliding-grip chest thrust
            m.thrust = new MoveDef("spear.thrust", "Needle Thrust", 34)
                .Key(7, Hold(P().Hip(-0.06f, -0.14f, 6f).Chest(6f).Head(-4f).Feet(0.28f, -0.32f), 0.24f, 1.04f, 6f, -0.24f, -0.34f, -0.60f), Ease.OutQuad)
                .Key(10, Hold(Lunge(0.16f, -0.15f, -12f, -24f, 0.44f, -0.30f), 0.60f, 1.08f, 6f, 0.34f, -0.46f, -0.66f), Ease.OutExpo)
                .Key(15, Hold(Lunge(0.16f, -0.15f, -12f, -24f, 0.44f, -0.30f), 0.58f, 1.08f, 6f, 0.32f, -0.46f, -0.66f))
                .Key(22, Hold(P().Hip(0.06f, -0.13f, -6f).Chest(-14f).Head(8f).Feet(0.36f, -0.30f), 0.40f, 1.02f, 12f, 0.08f, -0.42f, -0.64f), Ease.OutQuad)
                .Key(34, Guard(), Ease.InOutQuad)
                .Move(7, 11, 2.4f)
                .Hit(9, 14, Blade, 9, HitLevel.Mid, new Vector2(2.4f, 0.2f), stun: 20, hitstop: 6, shake: 0.14f, radius: 0.08f)
                .Vector(StrikeVector.Thrust).Segment(0.28f, 1f)
                .Gust(9, new Vector2(9.0f, 0.6f), 0.14f)
                .Fx(8, 15, FxKind.TrailBlade).Fx(8, 8, FxKind.Dust).Sword().Cancel(13);

            // 2. Light 2 — Twin Fang Flurry (Low-to-High Double Thrust): fast knee/shin thrust -> rear hand flicks tip up into throat thrust
            m.twinFang = new MoveDef("spear.twinFang", "Twin Fang Flurry", 38)
                .Key(5, Hold(P().Hip(-0.02f, -0.20f, 2f).Chest(-8f).Head(6f).Feet(0.32f, -0.30f), 0.24f, 0.88f, -12f, -0.20f, -0.34f, -0.62f), Ease.OutQuad)
                .Key(9, Hold(LowBody(0.12f, -0.26f, -10f, -24f), 0.50f, 0.72f, -14f, 0.22f, -0.44f, -0.66f), Ease.OutExpo)
                .Key(12, Hold(P().Hip(0.04f, -0.18f, -2f).Chest(-10f).Head(6f).Feet(0.36f, -0.30f), 0.28f, 0.96f, 20f, -0.14f, -0.36f, -0.62f), Ease.OutQuad)
                .Key(16, Hold(Lunge(0.14f, -0.10f, -8f, -14f, 0.42f, -0.28f), 0.52f, 1.18f, 22f, 0.26f, -0.46f, -0.66f), Ease.OutExpo)
                .Key(22, Hold(Lunge(0.14f, -0.10f, -8f, -14f, 0.42f, -0.28f), 0.50f, 1.18f, 22f, 0.22f, -0.46f, -0.66f))
                .Key(38, Guard(), Ease.InOutQuad)
                .Move(6, 9, 1.8f).Move(13, 17, 2.2f)
                .Hit(8, 11, Blade, 5, HitLevel.Low, new Vector2(1.0f, 0.1f), stun: 18, hitstop: 4, shake: 0.09f, radius: 0.08f)
                .Vector(StrikeVector.Thrust).Segment(0.28f, 1f)
                .Hit(15, 20, Blade, 6, HitLevel.High, new Vector2(1.8f, 0.6f), stun: 20, hitstop: 5, shake: 0.12f, radius: 0.08f)
                .Vector(StrikeVector.Thrust).Segment(0.28f, 1f)
                .Gust(8, new Vector2(7.5f, -1.2f), 0.12f)
                .Gust(15, new Vector2(8.5f, 3.2f), 0.14f)
                .Fx(7, 21, FxKind.TrailBlade).Sword().Cancel(20);

            // 3. Light 3 — Crescent Whip-Cleave (Wide Sweeping Finisher): rear hand vaults shaft high to whip a fast downward diagonal crescent
            m.crescentCleave = new MoveDef("spear.crescentCleave", "Crescent Whip-Cleave", 42)
                .Key(9, Hold(P().Hip(-0.04f, -0.06f, 8f).Chest(14f).Head(-8f).Feet(0.28f, -0.32f), 0.18f, 1.54f, 110f, -0.10f, -0.40f, -0.64f), Ease.OutQuad)
                .Key(13, Hold(P().Hip(0.08f, -0.14f, -6f).Chest(-14f).Head(8f).Feet(0.40f, -0.32f), 0.48f, 1.24f, 26f, 0.12f, -0.42f, -0.64f), Ease.InCubic)
                .Key(16, Hold(P().Hip(0.16f, -0.26f, -14f).Chest(-28f).Head(16f).Feet(0.46f, -0.32f), 0.48f, 0.76f, -16f, 0.16f, -0.42f, -0.64f), Ease.OutExpo)
                .Key(24, Hold(P().Hip(0.16f, -0.26f, -14f).Chest(-28f).Head(16f).Feet(0.46f, -0.32f), 0.46f, 0.74f, -18f, 0.12f, -0.42f, -0.64f))
                .Key(42, Guard(), Ease.InOutQuad)
                .Move(10, 16, 3.2f)
                .Hit(12, 18, Blade, 12, HitLevel.Overhead, new Vector2(3.4f, 1.4f), stun: 26, hitstop: 8, shake: 0.24f, knockdown: true, heavy: true, radius: 0.09f, postureMul: 1.5f)
                .Vector(StrikeVector.HighOverhead).Segment(0.15f, 1f)
                .Gust(13, new Vector2(9.5f, -5.5f), 0.18f)
                .Fx(10, 19, FxKind.TrailBlade).Fx(16, 16, FxKind.Dust).Sword();

            // 4. Heavy / Charged — Dragon Piercer (Committed Lunge Skewer): low crouched waist brace -> high-velocity max-reach skewer
            m.lunge = new MoveDef("spear.lunge", "Dragon Piercer", 42)
                .Key(8, Hold(P().Hip(-0.06f, -0.32f, -4f).Chest(-24f).Head(14f).FootN(0.30f, 0.13f).FootF(-0.30f, 0.15f, -26f), 0.18f, 0.86f, 4f, -0.26f, -0.34f, -0.60f), Ease.OutQuad)
                .Key(12, Hold(P().Hip(0.18f, -0.24f, -16f).Chest(-28f).Head(18f).Feet(0.48f, -0.32f), 0.58f, 0.96f, 6f, 0.34f, -0.46f, -0.66f), Ease.OutExpo)
                .Key(22, Hold(P().Hip(0.18f, -0.24f, -16f).Chest(-28f).Head(18f).Feet(0.48f, -0.32f), 0.56f, 0.96f, 6f, 0.32f, -0.46f, -0.66f))
                .Key(30, Hold(Lunge(0.10f, -0.18f, -8f, -16f, 0.40f, -0.30f), 0.42f, 0.98f, 12f, 0.08f, -0.42f, -0.64f), Ease.OutQuad)
                .Key(42, Guard(), Ease.InOutQuad)
                .Move(9, 20, 7.4f)
                .Hit(10, 20, Blade, 12, HitLevel.Mid, new Vector2(4.2f, 1.0f), stun: 28, hitstop: 9, shake: 0.26f, knockdown: true, heavy: true, radius: 0.085f, postureMul: 1.55f)
                .Vector(StrikeVector.Thrust).Segment(0.28f, 1f)
                .Gust(10, new Vector2(12.5f, 0.8f), 0.20f)
                .Fx(9, 21, FxKind.AfterImage).Fx(9, 21, FxKind.TrailBlade).Fx(9, 9, FxKind.Dust).Sword();

            // 5. Low Attack — Riverbed Sweep: low split lunge sweeping the spearhead horizontally across ankle level
            m.sweep = new MoveDef("spear.sweep", "Riverbed Sweep", 38)
                .Key(8, Hold(P().Hip(-0.04f, -0.34f, -2f).Chest(-22f).Head(14f).FootN(0.26f, 0.13f).FootF(-0.30f, 0.15f, -28f), 0.10f, 0.68f, 168f, -0.10f, -0.36f, -0.62f), Ease.OutQuad)
                .Key(12, Hold(P().Hip(0.08f, -0.38f, -8f).Chest(-28f).Head(16f).FootN(0.38f, 0.13f).FootF(-0.30f, 0.15f, -28f), 0.42f, 0.56f, -6f, 0.08f, -0.36f, -0.64f), Ease.OutExpo)
                .Key(18, Hold(P().Hip(0.08f, -0.38f, -8f).Chest(-28f).Head(16f).FootN(0.38f, 0.13f).FootF(-0.30f, 0.15f, -28f), 0.40f, 0.54f, -5f, 0.06f, -0.36f, -0.64f))
                .Key(26, Hold(LowBody(0.04f, -0.26f, -6f, -18f), 0.34f, 0.78f, 10f, 0.0f, -0.40f, -0.62f), Ease.OutQuad)
                .Key(38, Guard(), Ease.InOutQuad)
                .Move(9, 14, 1.8f)
                .Hit(10, 17, Blade, 8, HitLevel.Low, new Vector2(0.8f, 3.4f), stun: 22, hitstop: 6, shake: 0.16f, knockdown: true, otg: true, radius: 0.085f)
                .Vector(StrikeVector.Low).Segment(0.15f, 1f)
                .Gust(11, new Vector2(9.0f, -2.5f), 0.16f)
                .Fx(9, 18, FxKind.TrailBlade).Fx(11, 11, FxKind.Dust).Sword();

            // 7. Dash Attack — Vaulting Skewer: fast forward glide keeping spear point leading the body into a crisp mid-height pierce
            m.dashSkewer = new MoveDef("spear.dashSkewer", "Vaulting Skewer", 32)
                .Key(5, Hold(P().Hip(0.14f, -0.22f, -16f).Chest(-26f).Head(16f).Feet(0.44f, -0.30f), 0.30f, 0.98f, 4f, -0.18f, -0.36f, -0.62f), Ease.OutQuad)
                .Key(9, Hold(Lunge(0.18f, -0.18f, -14f, -26f, 0.48f, -0.30f), 0.58f, 1.04f, 4f, 0.30f, -0.46f, -0.66f), Ease.OutExpo)
                .Key(16, Hold(Lunge(0.18f, -0.18f, -14f, -26f, 0.48f, -0.30f), 0.56f, 1.04f, 4f, 0.26f, -0.46f, -0.66f))
                .Key(32, Guard(), Ease.InOutQuad)
                .Move(0, 9, 6.4f)
                .Hit(7, 14, Blade, 10, HitLevel.Mid, new Vector2(2.8f, 0.5f), stun: 22, hitstop: 7, shake: 0.18f, heavy: true, radius: 0.08f)
                .Vector(StrikeVector.Thrust).Segment(0.28f, 1f)
                .Gust(7, new Vector2(10.5f, 0.6f), 0.16f)
                .Fx(1, 14, FxKind.AfterImage).Fx(6, 15, FxKind.TrailBlade).Fx(6, 6, FxKind.Dust).Sword().Cancel(15);

            // Raised Guard: shaft braced up across the line of attack; armour frames for the guard
            m.guardRaise = new MoveDef("spear.guardRaise", "Raised Guard", 34)
                .Key(5, Hold(P().Hip(-0.03f, -0.1f, -2f).Chest(-2f).Head(-4f).Feet(0.24f, -0.30f), 0.30f, 1.28f, 70f))
                .Key(26, Hold(P().Hip(-0.03f, -0.11f, -2f).Chest(-2f).Head(-4f).Feet(0.24f, -0.30f), 0.30f, 1.27f, 72f))
                .Key(34, Guard(), Ease.InOutQuad)
                .Armour(5, 24)
                .Fx(4, 4, FxKind.Glint).Sword();

            // chains: Light 1 -> Light 2 -> Light 3 on S, plus P pokes into thrust/sweep
            m.jab.chain[Btn.P] = m.poke2;
            m.jab.chain[Btn.S] = m.thrust;
            m.jab.chain[Btn.K] = m.kick;
            m.poke2.chain[Btn.S] = m.sweep;
            m.poke2.chain[Btn.P] = m.thrust;
            m.stepPoke.chain[Btn.S] = m.thrust;
            m.kick.chain[Btn.S] = m.thrust;
            m.thrust.chain[Btn.S] = m.twinFang;
            m.twinFang.chain[Btn.S] = m.crescentCleave;
            m.dashSkewer.chain[Btn.S] = m.twinFang;
            m.lowPoke.chain[Btn.P] = m.poke2;

            // ===================== AIR =====================
            m.airPoke = new MoveDef("spear.airPoke", "Diving Poke", 30)
                .Key(4, Hold(P().Hip(0f, 0f, 0f).Chest(8f).LegN(60f, -90f).LegF(40f, -100f), 0.22f, 1.32f, 16f, -0.18f, -0.34f, -0.62f))
                .Key(8, Hold(P().Hip(0f, 0f, -4f).Chest(-14f).Head(8f).LegN(60f, -90f).LegF(40f, -100f), 0.50f, 1.12f, -32f, 0.24f, -0.44f, -0.66f), Ease.OutExpo)
                .Key(30, Hold(P().Hip(0f, 0f, -2f).Chest(-10f).Head(6f).LegN(50f, -70f).LegF(30f, -80f), 0.44f, 1.10f, -28f, 0.08f, -0.42f, -0.64f))
                .Hit(7, 14, Blade, 6, HitLevel.Overhead, new Vector2(1.4f, 0.5f), stun: 16, hitstop: 5, shake: 0.1f, radius: 0.08f)
                .Vector(StrikeVector.Thrust).Segment(0.28f, 1f)
                .Gust(7, new Vector2(7.0f, -4.5f), 0.14f)
                .Fx(6, 14, FxKind.TrailBlade).Sword().Air();

            m.airDive = new MoveDef("spear.airDive", "Falling Dragon", 40)
                .Key(5, Hold(P().Hip(0f, 0f, -8f).Chest(-8f).Head(4f).LegN(80f, -110f).LegF(60f, -100f), 0.34f, 1.28f, -60f, 0.18f, -0.42f, -0.64f), Ease.OutExpo)
                .Key(40, Hold(P().Hip(0f, 0f, -8f).Chest(-8f).Head(4f).LegN(78f, -108f).LegF(58f, -98f), 0.34f, 1.28f, -60f, 0.18f, -0.42f, -0.64f))
                .Hit(4, 30, Blade, 9, HitLevel.Overhead, new Vector2(2.0f, 1.6f), stun: 20, hitstop: 7, shake: 0.2f, knockdown: true, heavy: true, radius: 0.085f)
                .Vector(StrikeVector.Thrust).Segment(0.28f, 1f)
                .Gust(5, new Vector2(5.5f, -9.0f), 0.18f)
                .Fx(3, 30, FxKind.TrailBlade).Sword().Air();

            m.airSweep = new MoveDef("spear.airSweep", "Wave Crest", 30)
                .Key(3, Hold(P().Hip(0f, 0f, 4f).Chest(8f).Head(-4f).LegN(90f, -120f).LegF(80f, -110f), 0.06f, 1.32f, 142f), Ease.OutQuad)
                .Key(7, Hold(P().Hip(0f, 0f, -10f).Chest(-16f).Head(8f).LegN(90f, -120f).LegF(80f, -110f), 0.48f, 1.10f, -8f), Ease.OutExpo)
                .Key(10, Hold(P().Hip(0f, 0f, -10f).Chest(-16f).Head(8f).LegN(90f, -120f).LegF(80f, -110f), 0.42f, 1.00f, -24f))
                .Key(14, Hold(P().Hip(0f, 0f, -12f).Chest(-20f).Head(10f).LegN(92f, -122f).LegF(82f, -112f), 0.48f, 1.22f, 28f), Ease.OutExpo)
                .Key(19, Hold(P().Hip(0f, 0f, -8f).Chest(-12f).Head(6f).LegN(92f, -122f).LegF(82f, -112f), 0.42f, 1.24f, 38f))
                .Key(30, Hold(P().Hip(0f, 0f, 0f).Chest(5f).LegN(40f, -60f).LegF(20f, -50f), 0.30f, 1.00f, -20f))
                .Hit(4, 9, Blade, 5, HitLevel.Overhead, new Vector2(1.2f, 1.0f), stun: 16, hitstop: 4, shake: 0.08f, radius: 0.08f)
                .Vector(StrikeVector.HighOverhead).Segment(0.15f, 1f)
                .Hit(11, 16, Blade, 5, HitLevel.Overhead, new Vector2(1.8f, 2.5f), stun: 20, hitstop: 6, shake: 0.15f, knockdown: true, radius: 0.08f)
                .Vector(StrikeVector.Upward).Segment(0.15f, 1f)
                .Gust(5, new Vector2(8.0f, -2.5f), 0.14f)
                .Fx(3, 18, FxKind.TrailBlade).Sword().Air().Normalize();

            return m;
        }

        /// <summary>Picks a technique from button + direction (relative to facing) + context.</summary>
        public override MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self)
        {
            if (air) return b switch { Btn.P => airPoke, Btn.K => airDive, Btn.S => airSweep, _ => null };
            if (self != null && self.IsForwardDash && b is Btn.S or Btn.P) return dashSkewer;
            Fighter opp = self != null ? self.opponent : null;
            float dist = opp != null ? Mathf.Abs(opp.transform.position.x - self.transform.position.x) : 99f;
            bool oppDown = opp != null && opp.state == FState.Down;
            switch (b)
            {
                case Btn.P:
                    if (dy < 0) return oppDown && dist < 1.9f ? skewer : lowPoke;
                    if (fwd > 0) return dist < 0.85f && opp != null && opp.CanBeThrown ? throwGrab : stepPoke;
                    return jab;
                case Btn.K:
                    if (dy < 0) return lowKick;
                    if (fwd > 0) return pushKick;
                    if (fwd < 0) return vault;
                    return kick;
                case Btn.S:
                    if (dy > 0) return vault;
                    if (dy < 0) return sweep;
                    if (fwd > 0) return lunge;
                    if (fwd < 0) return guardRaise;
                    return thrust;
            }
            return null;
        }

        public override IEnumerable<MoveDef> All => new[]
        {
            jab, poke2, stepPoke, lowPoke, skewer, kick, pushKick, lowKick, vault,
            thrust, twinFang, crescentCleave, lunge, sweep, guardRaise, dashSkewer,
            airPoke, airDive, airSweep,
            throwGrab, throwWhiff, roll, backflip, getUp, getUpFront,
        };
    }
}
