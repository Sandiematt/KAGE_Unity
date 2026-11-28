using System.Collections.Generic;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// The cleaver family (ADR-0006, SAN-79): Onikiri, and the Order Ironclad's Raider Cleaver.
    /// Brutal, feral executioner blade — faster and sharper than the Hammer, heavier and more violent than a Katana.
    /// Uses one-handed wide savage sweeps and braced two-handed vertical execution chops, with full heel-to-tip
    /// cutting edge active segments (<c>[0.05..1.0]</c>).
    /// <para>
    /// Core 7 attacks:
    /// 1. Light 1 — Savage Diagonal Hack (<c>cleaver.hack</c>, P): aggressive step-in 45-degree downward shoulder-to-hip chop.
    /// 2. Light 2 — Butcher's Backhand Rip (<c>cleaver.hack2</c>, P->P): wide brutal horizontal backhand cleave ripping back across chest/waist.
    /// 3. Light 3 — Executioner's Guillotine (<c>cleaver.overhead</c>, P->P->P / P->P->S / S): braced two-hand overhead hang-time chop into ground.
    /// 4. Heavy / Charged — Spark-Drag Uppercut (<c>cleaver.slam</c>, Down+S): drags blade tip backward along ground spawning sparks/dust, then rips a massive 180-degree vertical uppercut cleave.
    /// 5. Low Attack — Ankle Hew (<c>cleaver.lowHack</c>, Down+P): aggressive low crouch horizontal shin-height hack.
    /// 6. Up / Anti-Air — Reaving Sky-Hack (<c>cleaver.sweep</c>, Up+S / Back+S): steep upward diagonal cleave trailing hip to high overhead.
    /// 7. Dash Attack — Lunging Meat-Hook Cleave (<c>cleaver.charge</c>, Dash+S / Dash+P / Fwd+S): explosive low sprint with trailing blade snapping into a vicious forward horizontal cleave.
    /// </para>
    /// </summary>
    public sealed class CleaverMoves : MoveSet
    {
        public MoveDef hack, hack2, stepHack, lowHack, hew, kick, pushKick, lowKick, pommel;
        public MoveDef overhead, sweep, charge, slam;
        /// <summary>Back + S: the cleaver's counter stance (Oni Wall) and the answer it hacks back with (Oni Rebuke).</summary>
        public MoveDef counterGuard, counterHack;
        public MoveDef airChop, airKick, airPlunge;

        /// <summary>Metres from the lead hand's guard point to the cleaver's tip (JS Onikiri length 104 against the katana's 82).</summary>
        public const float BladeLength = 0.9f;

        public static readonly WeaponFamilyDef Family = new()
        {
            id = WeaponFamilies.Cleaver,
            referenceWeapon = "onikiri",
            build = sets => Build((KatanaMoves)sets.Get(WeaponFamilies.Katana)),
            grip = GripPose.HeavyTwoHand,
            bladeLength = BladeLength,
            rageArt = Fighter.ThousandShadows,
            visual = new CleaverVisual(),
        };

        static readonly HitPoint[] Blade = { HitPoint.Blade };
        static readonly HitPoint[] FootN = { HitPoint.FootN, HitPoint.ShinN };
        static readonly HitPoint[] FootTip = { HitPoint.FootN };
        static readonly HitPoint[] Hilt = { HitPoint.Hilt };

        static Pose P() => new Pose();

        /// <summary>Onikiri's heavy forward-leaning executioner guard (matches Fighter.WeaponStance).</summary>
        static Pose Guard() =>
            P().Hip(0.03f, -0.14f, -5f).Chest(-14f).Head(10f).Feet(0.36f, -0.34f)
                .HandF(0.24f, 0.94f).Blade(52f).Grip();

        /// <summary>Braced two-handed grip: lead hand at (x, y), off-hand locked on the long handle behind it.</summary>
        static Pose Hold(Pose p, float x, float y, float blade) => p.HandF(x, y).Blade(blade).Grip();

        /// <summary>Aggressive lunging chopping body: hips driving forward, chest leaning over blade.</summary>
        static Pose Chop(float hx = 0.12f, float hy = -0.15f, float hipRot = -12f, float chest = -28f, float feetN = 0.42f, float feetF = -0.26f) =>
            P().Hip(hx, hy, hipRot).Chest(chest).Head(14f).Feet(feetN, feetF);

        /// <summary>Braced two-hand overhead hoist with lethal hang-time posture.</summary>
        static Pose Raised(float hy = -0.04f) =>
            Hold(P().Hip(-0.06f, hy, 8f).Chest(22f).Head(-12f).Feet(0.28f, -0.30f), -0.06f, 1.84f, 128f);

        /// <summary>Crouched low behind a heavy execution chop.</summary>
        static Pose LowBody(float hx = 0.12f, float hy = -0.28f) =>
            P().Hip(hx, hy, -10f).Chest(-38f).Head(18f).FootN(0.40f, 0.13f).FootF(-0.28f, 0.15f, -25f);

        CleaverMoves(KatanaMoves katana) { ShareBodyMoves(katana); }

        public static CleaverMoves Build(KatanaMoves katana)
        {
            var m = new CleaverMoves(katana);

            // ===================== LIGHT COMBO & LOW/OTG HACKS =====================
            // 1. Light 1 — Savage Diagonal Hack (High-to-Low 45°): aggressive forward step-in with ferocious 45° downward chop
            m.hack = new MoveDef("cleaver.hack", "Savage Diagonal Hack", 28)
                .Key(8, P().Hip(-0.04f, -0.10f, 6f).Chest(10f).Head(-6f).Feet(0.28f, -0.30f).HandF(0.08f, 1.56f).Blade(124f).HandN(0.30f, 1.06f, 12f), Ease.OutQuad)
                .Key(12, Chop(0.14f, -0.16f, -14f, -28f, 0.42f, -0.26f).HandF(0.54f, 0.90f).Blade(-42f).HandN(-0.10f, 1.00f, -14f), Ease.InCubic)
                .Key(18, Chop(0.14f, -0.16f, -14f, -28f, 0.42f, -0.26f).HandF(0.52f, 0.88f).Blade(-46f).HandN(-0.12f, 0.98f, -14f))
                .Key(28, Guard(), Ease.InOutQuad)
                .Move(5, 11, 2.2f)
                .Hit(10, 15, Blade, 9, HitLevel.High, new Vector2(1.8f, -0.2f), stun: 19, hitstop: 7, shake: 0.15f, radius: 0.10f)
                .Vector(StrikeVector.HighOverhead).Segment(0.05f, 1f)
                .Gust(10, new Vector2(9.5f, -5.0f), 0.16f)
                .Fx(9, 16, FxKind.TrailBlade).Fx(8, 8, FxKind.Dust).Sword().Cancel(15);

            // 2. Light 2 — Butcher's Backhand Rip (Low-to-Mid Horizontal): immediately wrenches blade back in wide brutal horizontal cleave
            m.hack2 = new MoveDef("cleaver.hack2", "Butcher's Backhand Rip", 32)
                .Key(8, P().Hip(-0.04f, -0.14f, 8f).Chest(10f).Head(-4f).Feet(0.30f, -0.28f).HandF(-0.14f, 1.04f).Blade(-154f).EdgeUp().HandN(0.28f, 1.08f, 10f), Ease.OutQuad)
                .Key(13, Chop(0.14f, -0.14f, -12f, -26f, 0.42f, -0.26f).HandF(0.58f, 1.10f).Blade(6f).EdgeUp().HandN(-0.16f, 1.02f, -14f), Ease.OutExpo)
                .Key(20, Chop(0.14f, -0.14f, -12f, -26f, 0.42f, -0.26f).HandF(0.56f, 1.08f).Blade(8f).EdgeUp().HandN(-0.16f, 1.00f, -14f))
                .Key(32, Guard(), Ease.InOutQuad)
                .Move(8, 13, 2.0f)
                .Hit(11, 17, Blade, 10, HitLevel.Mid, new Vector2(2.2f, 0.3f), stun: 22, hitstop: 7, shake: 0.18f, radius: 0.10f)
                .Vector(StrikeVector.Mid).Segment(0.05f, 1f)
                .Gust(11, new Vector2(11.0f, 1.2f), 0.16f)
                .Fx(10, 17, FxKind.TrailBlade).Sword().Cancel(18);

            m.stepHack = new MoveDef("cleaver.stepHack", "Advancing Hack", 34)
                .Key(8, Hold(P().Hip(-0.02f, -0.12f, 4f).Chest(4f).Head(-2f).Feet(0.28f, -0.28f), 0.08f, 1.38f, 104f), Ease.OutQuad)
                .Key(12, Hold(Chop(0.14f, -0.16f, -12f, -26f, 0.42f, -0.24f), 0.58f, 0.96f, -18f), Ease.InCubic)
                .Key(18, Hold(Chop(0.14f, -0.16f, -12f, -26f, 0.42f, -0.24f), 0.58f, 0.94f, -18f))
                .Key(34, Guard(), Ease.InOutQuad)
                .Move(4, 13, 3.0f)
                .Hit(11, 16, Blade, 9, HitLevel.High, new Vector2(2.0f, 0.2f), stun: 20, hitstop: 7, shake: 0.14f, radius: 0.10f)
                .Vector(StrikeVector.HighOverhead).Segment(0.05f, 1f)
                .Gust(11, new Vector2(9.0f, -3.5f), 0.15f)
                .Fx(10, 16, FxKind.TrailBlade).Fx(4, 4, FxKind.Dust).Sword().Cancel(16);

            // 5. Low Attack — Ankle Hew: aggressive crouch horizontal hack at shin height with wide forward reach
            m.lowHack = new MoveDef("cleaver.lowHack", "Ankle Hew", 28)
                .Key(7, Hold(P().Hip(-0.04f, -0.34f, 2f).Chest(-18f).Head(12f).FootN(0.26f, 0.13f).FootF(-0.30f, 0.15f, -26f), -0.04f, 0.72f, 164f), Ease.OutQuad)
                .Key(11, Hold(P().Hip(0.08f, -0.36f, -8f).Chest(-30f).Head(18f).FootN(0.38f, 0.13f).FootF(-0.30f, 0.15f, -26f), 0.58f, 0.58f, -14f), Ease.OutExpo)
                .Key(16, Hold(P().Hip(0.08f, -0.36f, -8f).Chest(-30f).Head(18f).FootN(0.38f, 0.13f).FootF(-0.30f, 0.15f, -26f), 0.56f, 0.58f, -14f))
                .Key(28, Crouch(), Ease.InOutQuad)
                .Move(8, 12, 1.4f)
                .Hit(10, 15, Blade, 8, HitLevel.Low, new Vector2(1.4f, 0.2f), stun: 17, hitstop: 6, shake: 0.12f, radius: 0.10f)
                .Vector(StrikeVector.Low).Segment(0.05f, 1f)
                .Gust(10, new Vector2(9.0f, -2.5f), 0.15f)
                .Fx(9, 15, FxKind.TrailBlade).Sword().Cancel(15);

            // Crushing Hew: braced two-hand downward execution on a downed foe
            m.hew = new MoveDef("cleaver.hew", "Crushing Hew", 40)
                .Key(10, Hold(P().Hip(-0.02f, -0.08f, 4f).Chest(12f).Head(-8f).Feet(0.28f, -0.28f), 0.08f, 1.72f, 114f), Ease.OutQuad)
                .Key(16, Hold(LowBody(0.12f, -0.28f), 0.56f, 0.74f, -42f), Ease.InCubic)
                .Key(26, Hold(LowBody(0.12f, -0.28f), 0.56f, 0.72f, -42f))
                .Key(40, Guard(), Ease.InOutQuad)
                .Hit(14, 22, Blade, 14, HitLevel.Mid, new Vector2(0.4f, 1.0f), stun: 20, hitstop: 9, shake: 0.30f, otg: true, heavy: true, radius: 0.10f)
                .Vector(StrikeVector.HighOverhead).Segment(0.05f, 1f)
                .Gust(15, new Vector2(4.5f, -9.5f), 0.18f)
                .Fx(12, 22, FxKind.TrailBlade).Fx(17, 17, FxKind.Dust).Sword();

            // ===================== KICK BUTTON: BOOTS AND POMMEL =====================
            m.kick = new MoveDef("cleaver.kick", "Boot", 32)
                .Key(8, P().Hip(0.02f, -0.07f, 10f).Chest(8f).Head(-4f).LegN(98f, -118f, 10f).FootF(-0.16f, 0.13f).HandF(0.10f, 1.02f).Blade(78f).HandN(0.20f, 1.24f, 10f))
                .Key(12, P().Hip(0.12f, -0.04f, 18f).Chest(14f).Head(-8f).LegN(104f, -6f, 20f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.04f).Blade(80f).HandN(0.12f, 1.26f, 10f), Ease.OutExpo)
                .Key(17, P().Hip(0.12f, -0.04f, 18f).Chest(14f).Head(-8f).LegN(102f, -8f, 20f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.04f).Blade(80f).HandN(0.12f, 1.26f, 10f))
                .Key(32, Guard(), Ease.InOutQuad)
                .Hit(12, 17, FootN, 8, HitLevel.Mid, new Vector2(1.8f, 0.4f), stun: 18, hitstop: 6, shake: 0.12f, radius: 0.1f)
                .Vector(StrikeVector.Mid)
                .Fx(11, 17, FxKind.TrailFootN).Cancel(17);

            m.pushKick = new MoveDef("cleaver.pushKick", "Shove Kick", 34)
                .Key(8, P().Hip(0f, -0.06f, 8f).Chest(8f).LegN(100f, -120f, 15f).FootF(-0.2f, 0.13f).HandF(0.12f, 1.0f).Blade(76f).HandN(0.2f, 1.22f, 10f))
                .Key(12, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(96f, -4f, 40f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.02f).Blade(78f).HandN(0.1f, 1.24f, 10f), Ease.OutExpo)
                .Key(17, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(95f, -5f, 40f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.02f).Blade(78f).HandN(0.1f, 1.24f, 10f))
                .Key(34, Guard(), Ease.InOutQuad)
                .Hit(12, 17, FootTip, 7, HitLevel.Mid, new Vector2(4.2f, 0.6f), stun: 22, hitstop: 7, shake: 0.16f, radius: 0.11f)
                .Vector(StrikeVector.Thrust)
                .Fx(11, 17, FxKind.TrailFootN);

            m.lowKick = new MoveDef("cleaver.lowKick", "Low Kick", 36)
                .Key(10, P().Hip(0.0f, -0.32f, 4f).Chest(-14f).Head(10f).LegN(80f, -100f, 10f).FootF(-0.26f, 0.15f, -25f).HandF(0.14f, 0.86f).Blade(70f).HandN(0.22f, 1.0f, 10f))
                .Key(13, P().Hip(0.06f, -0.3f, 8f).Chest(-8f).Head(8f).LegN(92f, -4f, 20f).FootF(-0.26f, 0.15f, -25f).HandF(0.12f, 0.88f).Blade(72f).HandN(0.2f, 1.02f, 10f), Ease.OutExpo)
                .Key(18, P().Hip(0.06f, -0.3f, 8f).Chest(-8f).Head(8f).LegN(90f, -6f, 20f).FootF(-0.26f, 0.15f, -25f).HandF(0.12f, 0.88f).Blade(72f).HandN(0.2f, 1.02f, 10f))
                .Key(36, Crouch(), Ease.InOutQuad)
                .Hit(12, 17, FootN, 7, HitLevel.Low, new Vector2(1.0f, 0f), stun: 16, hitstop: 5, shake: 0.08f, radius: 0.1f)
                .Vector(StrikeVector.Low)
                .Fx(11, 17, FxKind.TrailFootN).Cancel(17);

            m.pommel = new MoveDef("cleaver.pommel", "Pommel Smash", 40)
                .Key(10, Hold(P().Hip(0f, -0.2f, 0f).Chest(-14f).Head(10f).Feet(0.28f, -0.26f), 0.0f, 0.80f, 184f), Ease.OutQuad)
                .Key(16, Hold(P().Hip(0.10f, -0.04f, 2f).Chest(8f).Head(-6f).FootN(0.34f, 0.13f).FootF(-0.16f, 0.15f, -20f), 0.48f, 1.48f, 176f), Ease.OutExpo)
                .Key(24, Hold(P().Hip(0.10f, -0.04f, 2f).Chest(10f).Head(-6f).FootN(0.32f, 0.13f).FootF(-0.18f, 0.15f, -20f), 0.46f, 1.54f, 176f))
                .Key(40, Guard(), Ease.InOutQuad)
                .Move(8, 15, 1.8f)
                .Hit(14, 20, Hilt, 11, HitLevel.High, new Vector2(0.5f, 5.6f), stun: 30, hitstop: 8, shake: 0.22f, launch: true, heavy: true, radius: 0.13f)
                .Vector(StrikeVector.Upward)
                .JumpCancel(22)
                .Fx(13, 20, FxKind.Glint).Sword();

            // ===================== WEAPON BUTTON: EXECUTIONER CLEAVES =====================
            // 3. Light 3 — Executioner's Guillotine (Overhead Vertical Chop): braced two-hand hoist with hang-time -> straight vertical chop
            m.overhead = new MoveDef("cleaver.overhead", "Executioner's Guillotine", 58)
                .Key(12, Raised(-0.06f), Ease.OutQuad)
                .Key(24, Hold(P().Hip(-0.08f, -0.02f, 10f).Chest(24f).Head(-14f).Feet(0.28f, -0.30f), -0.08f, 1.88f, 134f), Ease.InOutQuad)
                .Key(31, Hold(LowBody(0.14f, -0.26f), 0.50f, 0.72f, -44f), Ease.InCubic)
                .Key(40, Hold(LowBody(0.14f, -0.26f), 0.50f, 0.70f, -44f))
                .Key(58, Guard(), Ease.InOutQuad)
                .Move(25, 30, 2.2f)
                .Armour(8, 31)
                .Hit(27, 34, Blade, 22, HitLevel.Overhead, new Vector2(1.2f, -3.4f), stun: 32, hitstop: 12, shake: 0.52f, knockdown: true, heavy: true, radius: 0.11f, postureMul: 1.65f)
                .Vector(StrikeVector.HighOverhead).Segment(0.05f, 1f)
                .Gust(28, new Vector2(5.5f, -11.5f), 0.20f)
                .Fx(24, 34, FxKind.TrailBlade).Fx(31, 31, FxKind.Dust).Sword();

            // 6. Up / Anti-Air — Reaving Sky-Hack: steep upward diagonal cleave snapping from trailing hip to high front overhead
            m.sweep = new MoveDef("cleaver.sweep", "Reaving Sky-Hack", 50)
                .Key(9, Hold(P().Hip(-0.08f, -0.18f, 6f).Chest(-6f).Head(4f).Feet(0.30f, -0.32f), -0.18f, 0.98f, -146f).EdgeUp(), Ease.OutQuad)
                .Key(19, Hold(P().Hip(-0.10f, -0.20f, 8f).Chest(-8f).Head(6f).Feet(0.30f, -0.32f), -0.20f, 1.00f, -152f).EdgeUp(), Ease.InOutQuad)
                .Key(22, Hold(P().Hip(0.02f, -0.14f, -2f).Chest(-8f).Head(6f).Feet(0.36f, -0.30f), 0.14f, 1.16f, -84f).EdgeUp(), Ease.OutQuad)
                .Key(25, Hold(P().Hip(0.08f, -0.12f, -4f).Chest(-4f).Head(2f).Feet(0.40f, -0.28f), 0.38f, 1.18f, -18f).EdgeUp(), Ease.OutQuad)
                .Key(29, Hold(P().Hip(0.14f, -0.06f, 6f).Chest(14f).Head(-10f).Feet(0.42f, -0.26f), 0.44f, 1.62f, 68f).EdgeUp(), Ease.OutExpo)
                .Key(36, Hold(P().Hip(0.14f, -0.06f, 6f).Chest(16f).Head(-10f).Feet(0.42f, -0.26f), 0.40f, 1.66f, 74f).EdgeUp())
                .Key(50, Guard(), Ease.InOutQuad)
                .Move(19, 27, 2.6f)
                .Armour(9, 28)
                .Hit(22, 30, Blade, 16, HitLevel.Mid, new Vector2(2.4f, 5.2f), stun: 28, hitstop: 10, shake: 0.38f, launch: true, knockdown: true, heavy: true, radius: 0.10f, postureMul: 1.45f)
                .Vector(StrikeVector.Upward).Segment(0.05f, 1f)
                .JumpCancel(31)
                .Gust(23, new Vector2(8.5f, 9.0f), 0.18f)
                .Fx(20, 31, FxKind.TrailBlade).Fx(20, 20, FxKind.Dust).Sword().Cancel(36);

            // 7. Dash Attack — Lunging Meat-Hook Cleave: explosive low forward sprint with cleaver trailing behind, snapping into a wide forward horizontal cleave
            m.charge = new MoveDef("cleaver.charge", "Lunging Meat-Hook Cleave", 50)
                .Key(8, P().Hip(0.08f, -0.22f, -14f).Chest(-24f).Head(14f).Feet(0.42f, -0.30f).HandF(-0.22f, 1.04f).Blade(-168f).HandN(0.38f, 1.06f, 14f), Ease.OutQuad)
                .Key(16, P().Hip(0.16f, -0.22f, -18f).Chest(-28f).Head(16f).Feet(0.48f, -0.30f).HandF(-0.24f, 1.08f).Blade(-172f).HandN(0.42f, 1.08f, 16f), Ease.InOutQuad)
                .Key(18, P().Hip(0.17f, -0.20f, -16f).Chest(-28f).Head(16f).Feet(0.48f, -0.28f).HandF(0.16f, 1.12f).Blade(-84f).HandN(0.10f, 1.04f, 0f), Ease.InCubic)
                .Key(21, Chop(0.18f, -0.18f, -14f, -28f, 0.48f, -0.28f).HandF(0.58f, 1.06f).Blade(-6f).HandN(-0.18f, 0.98f, -14f), Ease.OutExpo)
                .Key(30, Chop(0.18f, -0.18f, -14f, -28f, 0.48f, -0.28f).HandF(0.56f, 1.04f).Blade(-8f).HandN(-0.18f, 0.96f, -14f))
                .Key(50, Guard(), Ease.InOutQuad)
                .Move(6, 20, 6.4f)
                .Armour(6, 25)
                .Hit(17, 26, Blade, 15, HitLevel.Mid, new Vector2(4.2f, 1.2f), stun: 28, hitstop: 10, shake: 0.34f, knockdown: true, heavy: true, radius: 0.11f, postureMul: 1.4f)
                .Vector(StrikeVector.Mid).Segment(0.05f, 1f)
                .Gust(18, new Vector2(12.0f, 1.0f), 0.18f)
                .Fx(6, 24, FxKind.AfterImage).Fx(16, 27, FxKind.TrailBlade).Fx(12, 12, FxKind.Dust).Sword();

            // 4. Heavy / Charged — Spark-Drag Uppercut (Ground-Gouge Cleave):
            //    Drops heavy cleaver tip to drag backward along the ground during windup (spawning sparks/dust),
            //    then violently rips the blade upward in a massive 180-degree vertical uppercut cleave.
            m.slam = new MoveDef("cleaver.slam", "Spark-Drag Uppercut", 62)
                .Key(4, Hold(P().Hip(-0.02f, -0.06f, 2f).Chest(0f).Head(0f).Feet(0.32f, -0.32f), 0.10f, 1.34f, -84f), Ease.OutQuad)
                .Key(10, Hold(P().Hip(-0.08f, -0.24f, 4f).Chest(-10f).Head(8f).FootN(0.28f, 0.13f).FootF(-0.32f, 0.15f, -26f), -0.18f, 0.80f, -148f).EdgeUp(), Ease.OutQuad)
                .Key(23, Hold(P().Hip(-0.12f, -0.24f, 6f).Chest(-10f).Head(8f).FootN(0.32f, 0.13f).FootF(-0.34f, 0.15f, -26f), -0.22f, 0.82f, -152f).EdgeUp(), Ease.Linear)
                .Key(25, Hold(P().Hip(-0.02f, -0.10f, -2f).Chest(-4f).Head(4f).Feet(0.36f, -0.30f), 0.08f, 1.30f, -104f).EdgeUp(), Ease.OutQuad)
                .Key(27, Hold(P().Hip(0.04f, -0.08f, -4f).Chest(-4f).Head(4f).Feet(0.38f, -0.30f), 0.26f, 1.32f, -60f).EdgeUp(), Ease.Linear)
                .Key(29, Hold(P().Hip(0.08f, -0.12f, -6f).Chest(-6f).Head(4f).Feet(0.40f, -0.28f), 0.38f, 1.20f, -18f).EdgeUp(), Ease.OutQuad)
                .Key(33, Hold(P().Hip(0.14f, -0.04f, 8f).Chest(18f).Head(-12f).FootN(0.42f, 0.13f).FootF(-0.22f, 0.15f, -14f), 0.40f, 1.70f, 76f).EdgeUp(), Ease.OutExpo)
                .Key(44, Hold(P().Hip(0.14f, -0.04f, 8f).Chest(20f).Head(-12f).FootN(0.40f, 0.13f).FootF(-0.24f, 0.15f, -14f), 0.36f, 1.74f, 84f).EdgeUp())
                .Key(62, Guard(), Ease.InOutQuad)
                .Move(11, 22, 1.4f).Move(24, 32, 3.0f)
                .Armour(8, 34)
                .Cue(12, WeaponCues.OnikiriSparkDragCue)
                .Cue(16, WeaponCues.OnikiriSparkDragCue)
                .Cue(20, WeaponCues.OnikiriSparkDragCue)
                .Cue(24, WeaponCues.OnikiriSparkDragCue)
                .Hit(27, 36, Blade, 26, HitLevel.Mid, new Vector2(1.8f, 6.8f), stun: 34, hitstop: 13, shake: 0.56f, launch: true, knockdown: true, otg: true, heavy: true, radius: 0.11f, postureMul: 1.85f)
                .Vector(StrikeVector.Upward).Segment(0.05f, 1f)
                .Gust(28, new Vector2(8.5f, 11.0f), 0.22f)
                .Fx(25, 37, FxKind.TrailBlade).Fx(30, 30, FxKind.Shockwave).Fx(14, 14, FxKind.Dust).Sword();

            // chains: Light 1 (hack) -> Light 2 (hack2) -> Light 3 (overhead) on both P and S
            m.hack.chain[Btn.P] = m.hack2;
            m.hack.chain[Btn.S] = m.sweep;
            m.hack.chain[Btn.K] = m.kick;
            m.hack2.chain[Btn.P] = m.overhead;
            m.hack2.chain[Btn.S] = m.overhead;
            m.hack2.chain[Btn.K] = m.pushKick;
            m.stepHack.chain[Btn.S] = m.overhead;
            m.kick.chain[Btn.S] = m.overhead;
            m.lowHack.chain[Btn.P] = m.hack2;
            m.sweep.chain[Btn.S] = m.overhead;

            // ===================== AIR =====================
            m.airChop = new MoveDef("cleaver.airChop", "Diving Chop", 36)
                .Key(5, Hold(P().Hip(0f, 0f, 2f).Chest(8f).LegN(60f, -90f).LegF(40f, -100f), 0.10f, 1.68f, 112f))
                .Key(11, Hold(P().Hip(0f, 0f, -4f).Chest(-20f).Head(10f).LegN(60f, -90f).LegF(40f, -100f), 0.54f, 1.10f, -30f), Ease.InCubic)
                .Key(36, Hold(P().Hip(0f, 0f, -2f).Chest(-14f).Head(8f).LegN(50f, -70f).LegF(30f, -80f), 0.48f, 1.08f, -30f))
                .Hit(9, 17, Blade, 10, HitLevel.Overhead, new Vector2(1.6f, 0.5f), stun: 20, hitstop: 6, shake: 0.14f, radius: 0.10f)
                .Vector(StrikeVector.HighOverhead).Segment(0.05f, 1f)
                .Gust(10, new Vector2(7.5f, -5.0f), 0.14f)
                .Fx(8, 17, FxKind.TrailBlade).Sword().Air();

            m.airKick = new MoveDef("cleaver.airKick", "Boot Drop", 44)
                .Key(6, P().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(70f, -4f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).HandF(-0.15f, 1.1f).Blade(-170f), Ease.OutExpo)
                .Key(44, P().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(68f, -6f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).HandF(-0.15f, 1.1f).Blade(-170f))
                .Hit(6, 34, FootN, 9, HitLevel.Overhead, new Vector2(2.6f, 1.8f), stun: 22, hitstop: 8, shake: 0.22f, knockdown: true, radius: 0.11f)
                .Vector(StrikeVector.HighOverhead)
                .Fx(5, 14, FxKind.TrailFootN).Air();

            m.airPlunge = new MoveDef("cleaver.airPlunge", "Plunging Cleave", 46)
                .Key(6, Hold(P().Hip(0f, 0f, -4f).Chest(12f).Head(-8f).LegN(70f, -100f).LegF(50f, -90f), 0.05f, 1.76f, 118f))
                .Key(13, Hold(P().Hip(0f, 0f, -8f).Chest(-24f).Head(12f).LegN(64f, -92f).LegF(44f, -84f), 0.50f, 0.98f, -50f), Ease.InCubic)
                .Key(46, Hold(P().Hip(0f, 0f, -8f).Chest(-22f).Head(12f).LegN(60f, -88f).LegF(40f, -80f), 0.50f, 0.96f, -50f))
                .Hit(11, 32, Blade, 18, HitLevel.Overhead, new Vector2(1.6f, -1.0f), stun: 28, hitstop: 12, shake: 0.40f, knockdown: true, heavy: true, radius: 0.11f, postureMul: 1.5f)
                .Vector(StrikeVector.HighOverhead).Segment(0.05f, 1f)
                .Gust(12, new Vector2(6.5f, -9.5f), 0.18f)
                .Fx(9, 32, FxKind.TrailBlade).Sword().Air();

            // Oni Wall (Back + S, the cleaver's counter like the katana's Mirror Guard): the broad blade stood upright before the chest, the
            // flat a wall. A blow caught in frames 4-24 is turned aside (the attacker reels as from a parry) and Oni Rebuke answers it
            m.counterGuard = new MoveDef("cleaver.counterGuard", "Oni Wall", 34)
                .Key(5, Hold(P().Hip(-0.05f, -0.16f, 2f).Chest(-2f).Head(-2f).Feet(0.30f, -0.34f), 0.30f, 1.06f, 86f))
                .Key(26, Hold(P().Hip(-0.05f, -0.17f, 2f).Chest(-2f).Head(-2f).Feet(0.30f, -0.34f), 0.30f, 1.05f, 88f))
                .Key(34, Guard(), Ease.InOutQuad)
                .Fx(4, 4, FxKind.Glint).Sword();
            m.counterGuard.counterFrom = 4;
            m.counterGuard.counterTo = 24;

            // Oni Rebuke: straight off the wall, the blade drops in one short brutal diagonal hack through the reeling foe
            m.counterHack = new MoveDef("cleaver.counterHack", "Oni Rebuke", 32)
                .Key(2, Hold(P().Hip(-0.02f, -0.12f, 4f).Chest(8f).Head(-4f).Feet(0.30f, -0.30f), 0.12f, 1.50f, 118f), Ease.OutQuad)
                .Key(5, Hold(Chop(0.14f, -0.16f, -14f, -28f, 0.42f, -0.26f), 0.54f, 0.90f, -42f), Ease.InCubic)
                .Key(14, Hold(Chop(0.14f, -0.16f, -14f, -28f, 0.42f, -0.26f), 0.52f, 0.88f, -46f))
                .Key(32, Guard(), Ease.InOutQuad)
                .Move(0, 4, 1.8f)
                .Hit(3, 8, Blade, 14, HitLevel.Unblockable, new Vector2(2.2f, 1.0f), stun: 30, hitstop: 12, shake: 0.45f, knockdown: true, heavy: true, radius: 0.11f, postureMul: 1.4f)
                .Vector(StrikeVector.HighOverhead).Segment(0.05f, 1f)
                .Gust(3, new Vector2(9.5f, -5.0f), 0.16f)
                .Invuln(0, 12).Fx(1, 9, FxKind.TrailBlade).Fx(4, 4, FxKind.SlashLine).Fx(5, 5, FxKind.Dust).Sword();
            m.counterGuard.counterAnswer = m.counterHack;

            return m;
        }

        /// <summary>Picks a technique from button + direction (relative to facing) + context.</summary>
        public override MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self)
        {
            if (air) return b switch { Btn.P => airChop, Btn.K => airKick, Btn.S => airPlunge, _ => null };
            if (self != null && self.IsForwardDash && b is Btn.S or Btn.P) return charge;
            Fighter opp = self != null ? self.opponent : null;
            float dist = opp != null ? Mathf.Abs(opp.transform.position.x - self.transform.position.x) : 99f;
            bool oppDown = opp != null && opp.state == FState.Down;
            switch (b)
            {
                case Btn.P:
                    if (dy < 0) return oppDown && dist < 1.5f ? hew : lowHack;
                    if (fwd > 0) return dist < 0.85f && opp != null && opp.CanBeThrown ? throwGrab : stepHack;
                    return hack;
                case Btn.K:
                    if (dy < 0) return lowKick;
                    if (fwd > 0) return pushKick;
                    if (fwd < 0) return pommel;
                    return kick;
                case Btn.S:
                    if (dy > 0) return sweep;
                    if (dy < 0) return slam;
                    if (fwd > 0) return charge;
                    if (fwd < 0) return LegacyAi(self) ? sweep : counterGuard;   // Back + S: Oni Wall (legacy AI keeps the sweep)
                    return overhead;
            }
            return null;
        }

        public override IEnumerable<MoveDef> All => new[]
        {
            hack, hack2, stepHack, lowHack, hew, kick, pushKick, lowKick, pommel, overhead, sweep, charge, slam, counterGuard, counterHack,
            airChop, airKick, airPlunge,
            throwGrab, throwWhiff, roll, backflip, getUp, getUpFront,
        };
    }
}
