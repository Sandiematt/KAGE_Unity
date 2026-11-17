using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// Kage's moveset. The unarmed techniques follow the reference recording frame by frame
    /// (jab/cross chain, roundhouse, switch kick, sweep, flying kick, forward roll, backflip,
    /// over-the-shoulder throw, ground strike, knockdown and kip-up). The katana and shadow
    /// techniques extend that kit to fit Kage's design.
    /// </summary>
    public class MoveSet
    {
        public MoveDef jab, cross, uppercut, kick1, kick2, axeKick, sweep, crouchPunch, pushKick, lunge;
        public MoveDef throwGrab, throwWhiff, airKick, airPunch, airSlash, roll, backflip, groundPound, getUp, getUpFront;
        public MoveDef iai, risingMoon, cleave, shadowStep, lowReap, counterStance, counterSlash, crescent;

        static readonly HitPoint[] Fist = { HitPoint.Fist };
        static readonly HitPoint[] Hilt = { HitPoint.Hilt };
        static readonly HitPoint[] FootN = { HitPoint.FootN, HitPoint.ShinN };
        static readonly HitPoint[] FootF = { HitPoint.FootF, HitPoint.ShinF };
        static readonly HitPoint[] FootTip = { HitPoint.FootN };
        static readonly HitPoint[] Blade = { HitPoint.Blade };

        static Pose P() => new Pose();

        public static MoveSet Build()
        {
            var m = new MoveSet();

            // ===================== FISTS =====================
            m.jab = new MoveDef("jab", "Jab", 20)
                .Key(4, Stance().Chest(-11f).HandN(0.13f, 1.15f, 10f))
                .Key(7, P().Hip(0.07f, -0.08f, -6f).Chest(-22f).Head(12f).Feet(0.31f, -0.22f).HandN(0.56f, 1.2f).HandF(0.22f, 0.86f).Blade(-40f), Ease.OutExpo)
                .Key(11, P().Hip(0.07f, -0.08f, -6f).Chest(-22f).Head(12f).Feet(0.31f, -0.22f).HandN(0.55f, 1.2f).HandF(0.22f, 0.86f).Blade(-40f))
                .Key(20, Stance(), Ease.InOutQuad)
                .Hit(6, 9, Fist, 4, HitLevel.High, new Vector2(1.0f, 0f), stun: 14, hitstop: 4, shake: 0.06f)
                .Fx(5, 10, FxKind.TrailFist).Cancel(9);

            m.cross = new MoveDef("cross", "Pommel Strike", 24)
                .Key(5, Stance().Chest(-4f).HandF(0.02f, 1.0f).Blade(170f))
                .Key(9, P().Hip(0.09f, -0.09f, -8f).Chest(-26f).Head(14f).Feet(0.33f, -0.2f).HandN(0.12f, 1.08f, 10f).HandF(0.56f, 1.16f).Blade(178f), Ease.OutExpo)
                .Key(13, P().Hip(0.09f, -0.09f, -8f).Chest(-26f).Head(14f).Feet(0.33f, -0.2f).HandN(0.12f, 1.08f, 10f).HandF(0.55f, 1.16f).Blade(178f))
                .Key(24, Stance(), Ease.InOutQuad)
                .Hit(8, 11, Hilt, 5, HitLevel.High, new Vector2(1.4f, 0f), stun: 16, hitstop: 5, shake: 0.08f)
                .Sword().Cancel(11);

            m.uppercut = new MoveDef("uppercut", "Rising Fist", 30)
                .Key(6, P().Hip(0.04f, -0.2f, -6f).Chest(-26f).Head(16f).Feet(0.3f, -0.22f).HandN(0.18f, 0.86f, 30f).HandF(0.12f, 0.78f).Blade(-150f))
                .Key(10, P().Hip(0.12f, -0.02f, 2f).Chest(6f).Head(-4f).FootN(0.36f, 0.13f).FootF(-0.13f, 0.16f, -25f).HandN(0.48f, 1.5f, 70f).HandF(0.02f, 0.98f).Blade(-140f), Ease.OutExpo)
                .Key(15, P().Hip(0.1f, 0f, 2f).Chest(9f).Head(-8f).FootN(0.34f, 0.13f).FootF(-0.15f, 0.17f, -28f).HandN(0.36f, 1.66f, 85f).HandF(0f, 1.0f).Blade(-140f))
                .Key(30, Stance(), Ease.InOutQuad)
                .Hit(8, 13, Fist, 7, HitLevel.Mid, new Vector2(0.5f, 6.4f), stun: 30, hitstop: 7, shake: 0.22f, launch: true, heavy: true, radius: 0.12f)
                .Move(2, 9, 1.8f).JumpCancel(15)
                .Fx(8, 14, FxKind.TrailFist);

            m.jab.chain[Btn.P] = m.cross;
            m.cross.chain[Btn.P] = m.uppercut;

            m.crouchPunch = new MoveDef("crouchPunch", "Low Jab", 18)
                .Key(4, Crouch().HandN(0.15f, 0.9f))
                .Key(7, P().Hip(0.04f, -0.3f, 0f).Chest(-24f).Head(16f).FootN(0.26f, 0.13f).FootF(-0.26f, 0.15f, -25f).HandN(0.52f, 0.82f).HandF(0.1f, 0.62f).Blade(-20f), Ease.OutExpo)
                .Key(10, P().Hip(0.04f, -0.3f, 0f).Chest(-24f).Head(16f).FootN(0.26f, 0.13f).FootF(-0.26f, 0.15f, -25f).HandN(0.5f, 0.82f).HandF(0.1f, 0.62f).Blade(-20f))
                .Key(18, Crouch())
                .Hit(6, 9, Fist, 3, HitLevel.Mid, new Vector2(0.8f, 0f), stun: 12, hitstop: 4, shake: 0.05f)
                .Fx(5, 9, FxKind.TrailFist).Cancel(9);
            m.crouchPunch.chain[Btn.P] = m.uppercut;

            m.lunge = new MoveDef("lunge", "Stepping Punch", 26)
                .Key(5, Stance().Chest(-6f).HandN(0.08f, 1.12f))
                .Key(9, P().Hip(0.1f, -0.1f, -8f).Chest(-26f).Head(14f).Feet(0.38f, -0.18f).HandN(0.58f, 1.18f).HandF(0.2f, 0.82f).Blade(-60f), Ease.OutExpo)
                .Key(14, P().Hip(0.1f, -0.1f, -8f).Chest(-26f).Head(14f).Feet(0.38f, -0.18f).HandN(0.57f, 1.18f).HandF(0.2f, 0.82f).Blade(-60f))
                .Key(26, Stance(), Ease.InOutQuad)
                .Move(3, 10, 3.2f)
                .Hit(8, 12, Fist, 6, HitLevel.High, new Vector2(1.8f, 0.2f), stun: 16, hitstop: 5, shake: 0.1f)
                .Fx(7, 12, FxKind.TrailFist).Fx(3, 3, FxKind.Dust).Cancel(12);
            m.lunge.chain[Btn.P] = m.cross;

            // ===================== KICKS =====================
            m.kick1 = new MoveDef("kick1", "Roundhouse", 26)
                .Key(6, P().Hip(0.02f, -0.07f, 12f).Chest(10f).Head(-4f).LegN(95f, -115f, 10f).FootF(-0.16f, 0.13f).HandN(0.12f, 1.2f, 10f).HandF(0.02f, 0.95f).Blade(-150f))
                .Key(10, P().Hip(0.14f, -0.03f, 28f).Chest(24f).Head(-16f).LegN(116f, -6f, 12f).FootF(-0.12f, 0.13f).HandN(0.05f, 1.25f, 10f).HandF(-0.1f, 0.95f).Blade(-160f), Ease.OutExpo)
                .Key(14, P().Hip(0.14f, -0.03f, 28f).Chest(24f).Head(-16f).LegN(114f, -8f, 12f).FootF(-0.12f, 0.13f).HandN(0.05f, 1.25f, 10f).HandF(-0.1f, 0.95f).Blade(-160f))
                .Key(19, P().Hip(0.06f, -0.07f, 10f).Chest(6f).Head(-2f).LegN(90f, -110f).FootF(-0.18f, 0.13f).HandN(0.15f, 1.18f).HandF(0.1f, 0.9f).Blade(-90f))
                .Key(26, Stance(), Ease.InOutQuad)
                .Hit(9, 13, FootN, 6, HitLevel.High, new Vector2(1.6f, 0.4f), stun: 18, hitstop: 6, shake: 0.12f, radius: 0.1f)
                .Fx(8, 14, FxKind.TrailFootN).Cancel(13);

            m.kick2 = new MoveDef("kick2", "Switch Kick", 26)
                .Key(6, P().Hip(0.05f, -0.08f, 6f).Chest(4f).FootN(0.22f, 0.13f).LegF(100f, -120f).HandN(0.15f, 1.18f).HandF(0.15f, 0.9f).Blade(-140f))
                .Key(10, P().Hip(0.15f, -0.04f, 14f).Chest(16f).Head(-10f).FootN(0.16f, 0.13f).LegF(118f, -8f, 10f).HandN(0.08f, 1.22f).HandF(0f, 0.92f).Blade(-155f), Ease.OutExpo)
                .Key(14, P().Hip(0.15f, -0.04f, 14f).Chest(16f).Head(-10f).FootN(0.16f, 0.13f).LegF(116f, -10f, 10f).HandN(0.08f, 1.22f).HandF(0f, 0.92f).Blade(-155f))
                .Key(26, Stance(), Ease.InOutQuad)
                .Hit(9, 13, FootF, 6, HitLevel.Mid, new Vector2(2.2f, 0.3f), stun: 18, hitstop: 6, shake: 0.12f, radius: 0.1f)
                .Fx(8, 14, FxKind.TrailFootF).Cancel(13);

            m.axeKick = new MoveDef("axe", "Axe Kick", 36)
                .Key(8, P().Hip(0.02f, -0.04f, 22f).Chest(20f).Head(-12f).LegN(160f, -10f).FootF(-0.14f, 0.15f, -15f).HandN(0f, 1.3f).HandF(-0.12f, 1.0f).Blade(-160f), Ease.OutCubic)
                .Key(11, P().Hip(0.08f, -0.02f, 24f).Chest(22f).Head(-14f).LegN(172f, -8f).FootF(-0.12f, 0.16f, -20f).HandN(0f, 1.32f).HandF(-0.12f, 1.0f).Blade(-160f))
                .Key(15, P().Hip(0.16f, -0.12f, 0f).Chest(-18f).Head(10f).LegN(70f, -5f).FootF(-0.14f, 0.13f).HandN(0.25f, 1.1f).HandF(0.15f, 0.85f).Blade(-60f), Ease.InCubic)
                .Key(22, P().Hip(0.16f, -0.14f, -2f).Chest(-15f).Head(8f).FootN(0.48f, 0.13f).FootF(-0.14f, 0.13f).HandN(0.25f, 1.1f).HandF(0.2f, 0.85f).Blade(-40f))
                .Key(36, Stance(), Ease.InOutQuad)
                .Hit(13, 16, FootN, 10, HitLevel.Overhead, new Vector2(1.0f, -1.5f), stun: 26, hitstop: 9, shake: 0.32f, knockdown: true, heavy: true, radius: 0.11f)
                .Fx(9, 16, FxKind.TrailFootN).Fx(16, 16, FxKind.Dust);

            m.kick1.chain[Btn.K] = m.kick2;
            m.kick2.chain[Btn.K] = m.axeKick;
            m.jab.chain[Btn.K] = m.kick1;
            m.cross.chain[Btn.K] = m.kick2;

            m.sweep = new MoveDef("sweep", "Leg Sweep", 32)
                .Key(7, P().Hip(-0.04f, -0.42f, 0f).Chest(-30f).Head(16f).FootN(0.2f, 0.13f).FootF(-0.28f, 0.15f, -30f).HandN(0.12f, 0.5f, 40f).HandF(-0.12f, 0.52f).Blade(-175f))
                .Key(11, P().Hip(-0.04f, -0.44f, 6f).Chest(-34f).Head(18f).LegN(92f, -2f, 25f).FootF(-0.28f, 0.15f, -30f).HandN(0.08f, 0.48f, 40f).HandF(-0.16f, 0.5f).Blade(-178f), Ease.OutExpo)
                .Key(16, P().Hip(-0.04f, -0.44f, 6f).Chest(-34f).Head(18f).LegN(90f, -4f, 25f).FootF(-0.28f, 0.15f, -30f).HandN(0.08f, 0.48f, 40f).HandF(-0.16f, 0.5f).Blade(-178f))
                .Key(24, P().Hip(-0.02f, -0.36f, 0f).Chest(-26f).Head(14f).FootN(0.22f, 0.13f).FootF(-0.27f, 0.14f, -20f).HandN(0.15f, 0.7f).HandF(0.1f, 0.6f).Blade(-60f))
                .Key(32, Stance(), Ease.InOutQuad)
                .Hit(10, 15, FootN, 6, HitLevel.Low, new Vector2(0.6f, 3.4f), stun: 20, hitstop: 6, shake: 0.14f, knockdown: true, otg: true, radius: 0.11f)
                .Fx(9, 16, FxKind.TrailFootN).Fx(11, 11, FxKind.Dust);

            m.pushKick = new MoveDef("push", "Push Kick", 28)
                .Key(6, P().Hip(0f, -0.06f, 8f).Chest(8f).LegN(100f, -120f, 15f).FootF(-0.2f, 0.13f).HandN(0.15f, 1.2f).HandF(0.15f, 0.95f).Blade(-120f))
                .Key(10, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(96f, -4f, 40f).FootF(-0.14f, 0.13f).HandN(0.08f, 1.22f).HandF(0.05f, 0.92f).Blade(-140f), Ease.OutExpo)
                .Key(14, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(95f, -5f, 40f).FootF(-0.14f, 0.13f).HandN(0.08f, 1.22f).HandF(0.05f, 0.92f).Blade(-140f))
                .Key(28, Stance(), Ease.InOutQuad)
                .Hit(9, 13, FootTip, 6, HitLevel.Mid, new Vector2(4.0f, 0.6f), stun: 20, hitstop: 6, shake: 0.15f, radius: 0.11f)
                .Fx(8, 13, FxKind.TrailFootN);

            // ===================== THROW =====================
            m.throwWhiff = new MoveDef("throwWhiff", "Grab", 30)
                .Key(5, P().Hip(0.12f, -0.12f, -10f).Chest(-26f).Head(14f).Feet(0.36f, -0.2f).HandN(0.5f, 1.16f).HandF(0.46f, 1.02f).Blade(-175f))
                .Key(14, P().Hip(0.12f, -0.12f, -10f).Chest(-26f).Head(14f).Feet(0.36f, -0.2f).HandN(0.48f, 1.12f).HandF(0.44f, 1.0f).Blade(-175f))
                .Key(30, Stance(), Ease.InOutQuad);
            m.throwGrab = new MoveDef("throw", "Shoulder Throw", 46)
                .Key(5, P().Hip(0.12f, -0.12f, -10f).Chest(-26f).Head(14f).Feet(0.36f, -0.2f).HandN(0.5f, 1.16f).HandF(0.46f, 1.02f).Blade(-175f))
                .Key(14, P().Hip(0.06f, -0.22f, -6f).Chest(-12f).Head(6f).Feet(0.32f, -0.24f).HandN(0.3f, 1.5f).HandF(0.24f, 1.44f).Blade(-170f), Ease.InOutQuad)
                .Key(22, P().Hip(-0.04f, -0.16f, 6f).Chest(14f).Head(-10f).Feet(0.28f, -0.26f).HandN(-0.06f, 1.68f).HandF(-0.12f, 1.6f).Blade(170f))
                .Key(30, P().Hip(-0.06f, -0.32f, -12f).Chest(-42f).Head(20f).Feet(0.26f, -0.3f).HandN(-0.42f, 0.62f).HandF(-0.38f, 0.52f).Blade(-120f), Ease.InQuad)
                .Key(46, Stance(), Ease.InOutQuad);
            m.throwGrab.isThrow = true;
            m.throwGrab.grabFrame = 6;
            m.throwGrab.releaseFrame = 30;
            m.throwGrab.throwDmg = 12;
            m.throwGrab.whiff = m.throwWhiff;

            // ===================== AIR =====================
            m.airKick = new MoveDef("airKick", "Flying Kick", 40)
                .Key(5, P().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(70f, -4f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).HandF(-0.15f, 1.1f).Blade(-170f), Ease.OutExpo)
                .Key(40, P().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(68f, -6f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).HandF(-0.15f, 1.1f).Blade(-170f))
                .Hit(4, 30, FootN, 8, HitLevel.Overhead, new Vector2(2.4f, 1.8f), stun: 20, hitstop: 7, shake: 0.2f, knockdown: true, radius: 0.11f)
                .Fx(3, 12, FxKind.TrailFootN).Air();

            m.airPunch = new MoveDef("airPunch", "Hammer Fist", 30)
                .Key(4, P().Hip(0f, 0f, 0f).Chest(8f).LegN(60f, -90f).LegF(40f, -100f).HandN(0.15f, 1.7f, 60f).HandF(-0.05f, 1.0f).Blade(-150f))
                .Key(8, P().Hip(0f, 0f, 0f).Chest(-20f).Head(10f).LegN(60f, -90f).LegF(40f, -100f).HandN(0.5f, 1.05f, -20f).HandF(-0.05f, 1.0f).Blade(-150f), Ease.OutExpo)
                .Key(30, P().Hip(0f, 0f, 0f).Chest(-14f).Head(8f).LegN(50f, -70f).LegF(30f, -80f).HandN(0.45f, 1.08f, -20f).HandF(0f, 1.0f).Blade(-140f))
                .Hit(7, 12, Fist, 6, HitLevel.Overhead, new Vector2(1.4f, 0.5f), stun: 16, hitstop: 5, shake: 0.1f)
                .Fx(6, 12, FxKind.TrailFist).Air();

            m.airSlash = new MoveDef("airSlash", "Crescent Spin", 30)
                .Key(3, P().Hip(0f, 0f, 0f).Chest(-10f).LegN(90f, -120f).LegF(80f, -110f).HandN(0.1f, 1.1f).HandF(0.45f, 1.15f).Blade(10f))
                .Key(18, P().Hip(0f, 0f, 0f).Chest(-12f).LegN(92f, -122f).LegF(82f, -112f).HandN(0.1f, 1.1f).HandF(0.46f, 1.15f).Blade(10f), Ease.Linear)
                .Key(30, P().Hip(0f, 0f, 0f).Chest(5f).LegN(40f, -60f).LegF(20f, -50f).HandN(0.2f, 1.15f).HandF(0.35f, 1.0f).Blade(-30f))
                .Spin(2, 19, -360f)
                .Hit(4, 9, Blade, 5, HitLevel.Overhead, new Vector2(1.2f, 1.0f), stun: 16, hitstop: 4, shake: 0.08f, radius: 0.07f)
                .Hit(11, 16, Blade, 5, HitLevel.Overhead, new Vector2(1.8f, 2.5f), stun: 20, hitstop: 6, shake: 0.15f, knockdown: true, radius: 0.07f)
                .Fx(3, 18, FxKind.TrailBlade).Sword().Air().Normalize();

            // ===================== MOBILITY =====================
            var tuck = P().Chest(-55f).Neck(-15f).Head(-25f).LegN(115f, -145f).LegF(108f, -140f).ArmN(60f, 70f).ArmF(50f, 60f).Sword(-40f);
            m.roll = new MoveDef("roll", "Shadow Roll", 30)
                .Key(4, tuck.Clone().Hip(0.08f, -0.24f, -8f), Ease.OutQuad)
                .Key(12, tuck.Clone().Hip(0.12f, -0.13f, 0f), Ease.InOutQuad)
                .Key(18, tuck.Clone().Hip(0.1f, -0.15f, 0f), Ease.Linear)
                .Key(24, P().Hip(0.04f, -0.3f, 0f).Chest(-20f).Head(14f).FootN(0.26f, 0.13f).FootF(-0.24f, 0.15f, -25f).HandN(0.2f, 0.9f).HandF(0.22f, 0.7f).Blade(-30f), Ease.InOutQuad)
                .Key(30, Stance(), Ease.InOutQuad)
                .Spin(1, 23, -360f)
                .Move(2, 20, 4.6f).Invuln(2, 20).NoPush(2, 22)
                .Fx(3, 18, FxKind.AfterImage).Fx(3, 3, FxKind.Dust).Fx(22, 22, FxKind.Dust).Normalize();

            var open = P().Chest(22f).Head(-14f).LegN(30f, -40f).LegF(20f, -30f).ArmN(150f, 20f).ArmF(140f, 20f).Sword(20f);
            m.backflip = new MoveDef("backflip", "Backflip", 34)
                .Key(3, P().Hip(0f, -0.2f, 0f).Chest(-10f).Feet(0.22f, -0.24f).HandN(0f, 0.9f).HandF(-0.05f, 0.8f).Blade(-160f))
                .Key(9, open.Clone().Hip(0f, 0f, 10f), Ease.OutQuad)
                .Key(17, tuck.Clone().Hip(0f, 0f, 0f).Chest(-40f), Ease.InOutQuad)
                .Key(25, P().Hip(0f, 0f, 0f).Chest(0f).LegN(55f, -75f).LegF(35f, -55f).ArmN(40f, 40f).ArmF(30f, 40f).Sword(-20f), Ease.InOutQuad)
                .Key(30, P().Hip(0f, -0.14f, 0f).Chest(-8f).Feet(0.22f, -0.24f).HandN(0.2f, 1.1f).HandF(0.22f, 0.86f).Blade(-32f), Ease.OutQuad)
                .Key(34, Stance(), Ease.InOutQuad)
                .Spin(4, 28, 360f)
                .Move(3, 3, -3.0f, 7.2f, true).Invuln(2, 16)
                .Fx(3, 3, FxKind.Dust).Fx(4, 14, FxKind.AfterImage).Air(false).Normalize();

            m.groundPound = new MoveDef("groundPound", "Ground Strike", 32)
                .Key(4, Crouch())
                .Key(10, P().Hip(0f, 0f, 0f).Chest(10f).LegN(80f, -110f).LegF(60f, -100f).HandN(0.2f, 1.75f, 80f).HandF(-0.1f, 1.0f).Blade(-150f))
                .Key(18, P().Hip(0f, -0.1f, 0f).Chest(-40f).Head(20f).LegN(80f, -110f).LegF(60f, -100f).HandN(0.45f, 0.55f, -40f).HandF(-0.1f, 0.9f).Blade(-150f), Ease.InCubic)
                .Key(24, P().Hip(0.05f, -0.38f, 0f).Chest(-35f).Head(18f).FootN(0.26f, 0.13f).FootF(-0.24f, 0.15f, -25f).HandN(0.45f, 0.25f, -40f).HandF(0f, 0.6f).Blade(-120f))
                .Key(32, Stance(), Ease.InOutQuad)
                .Move(4, 4, 1.5f, 4.0f, true)
                .Hit(16, 24, Fist, 6, HitLevel.Mid, new Vector2(0.5f, 1.0f), stun: 18, hitstop: 8, shake: 0.25f, otg: true, radius: 0.12f)
                .Fx(24, 24, FxKind.Dust).Fx(24, 24, FxKind.Shockwave);

            m.getUpFront = new MoveDef("getUpFront", "Push-up", 26)
                .Key(8, P().Hip(-0.12f, -0.42f, -55f).Chest(-10f).Head(25f).LegN(60f, -70f).LegF(40f, -60f).HandN(0.25f, 0.28f).HandF(0.3f, 0.22f).Blade(-10f))
                .Key(16, P().Hip(-0.02f, -0.3f, -15f).Chest(-20f).Head(14f).FootN(0.24f, 0.13f).FootF(-0.26f, 0.15f, -20f).HandN(0.2f, 0.8f).HandF(0.24f, 0.7f).Blade(-32f))
                .Key(26, Stance(), Ease.InOutQuad)
                .Invuln(0, 26).Fx(14, 14, FxKind.Dust);

            m.getUp = new MoveDef("getUp", "Kip-up", 30)
                .Key(8, P().Hip(0.2f, -0.5f, 128f).Chest(-10f).Head(-10f).LegN(60f, -20f).LegF(40f, -10f).ArmN(140f, 30f).ArmF(130f, 30f))
                .Key(15, P().Hip(0.08f, -0.2f, 20f).Chest(-25f).Head(15f).LegN(80f, -120f).LegF(60f, -110f).HandN(0.2f, 1.0f).HandF(0.2f, 0.8f).Blade(-60f))
                .Key(22, P().Hip(0f, -0.24f, 0f).Chest(-20f).Head(12f).FootN(0.24f, 0.13f).FootF(-0.26f, 0.15f, -20f).HandN(0.2f, 1.0f).HandF(0.24f, 0.8f).Blade(-32f))
                .Key(30, Stance(), Ease.InOutQuad)
                .Invuln(0, 30).Fx(15, 15, FxKind.Dust);

            // ===================== KATANA =====================
            m.iai = new MoveDef("iai", "Iai Draw", 30)
                .Key(6, P().Hip(-0.03f, -0.11f, -4f).Chest(4f).Head(4f).Feet(0.22f, -0.26f).HandN(0.16f, 1.16f).HandF(-0.04f, 0.84f).Blade(-158f))
                .Key(9, P().Hip(0.08f, -0.11f, -10f).Chest(-16f).Head(10f).Feet(0.32f, -0.22f).HandN(0.08f, 1.12f).HandF(0.42f, 1.08f).Blade(8f), Ease.OutExpo)
                .Key(12, P().Hip(0.1f, -0.1f, -12f).Chest(-20f).Head(12f).Feet(0.32f, -0.22f).HandN(0.06f, 1.1f).HandF(0.36f, 1.26f).Blade(62f))
                .Key(18, P().Hip(0.1f, -0.1f, -12f).Chest(-20f).Head(12f).Feet(0.32f, -0.22f).HandN(0.06f, 1.1f).HandF(0.36f, 1.26f).Blade(70f))
                .Key(30, Stance(), Ease.InOutQuad)
                .Hit(8, 12, Blade, 9, HitLevel.Mid, new Vector2(1.8f, 0.3f), stun: 18, hitstop: 6, shake: 0.12f, radius: 0.07f)
                .Fx(6, 14, FxKind.TrailBlade).Sword().Cancel(12);

            m.risingMoon = new MoveDef("risingMoon", "Rising Moon", 32)
                .Key(5, P().Hip(0.04f, -0.26f, -6f).Chest(-26f).Head(14f).Feet(0.3f, -0.22f).HandN(0.12f, 1.0f).HandF(0.3f, 0.58f).Blade(-55f))
                .Key(9, P().Hip(0.12f, -0.04f, 2f).Chest(8f).Head(-6f).FootN(0.36f, 0.13f).FootF(-0.15f, 0.15f, -20f).HandN(0f, 1.2f).HandF(0.36f, 1.42f).Blade(96f), Ease.OutQuad)
                .Key(13, P().Hip(0.1f, -0.04f, 2f).Chest(10f).Head(-6f).FootN(0.32f, 0.13f).FootF(-0.18f, 0.15f, -20f).HandN(0f, 1.2f).HandF(0.26f, 1.52f).Blade(110f))
                .Key(32, Stance(), Ease.InOutQuad)
                .Hit(5, 10, Blade, 8, HitLevel.Mid, new Vector2(0.4f, 7.2f), stun: 30, hitstop: 7, shake: 0.2f, launch: true, heavy: true, radius: 0.09f)
                .Move(1, 7, 2.4f).JumpCancel(14)
                .Fx(4, 13, FxKind.TrailBlade).Sword().Cancel(12);

            m.cleave = new MoveDef("cleave", "Kage Cleave", 46)
                .Key(6, P().Hip(0f, -0.22f, 0f).Chest(8f).Head(-6f).Feet(0.24f, -0.26f).HandN(0.1f, 1.1f).HandF(-0.08f, 1.45f).Blade(128f))
                .Key(14, P().Hip(0f, 0f, 0f).Chest(18f).Head(-10f).LegN(70f, -100f).LegF(40f, -90f).HandN(0f, 1.4f).HandF(0.02f, 1.78f).Blade(98f))
                .Key(20, P().Hip(0f, 0f, -10f).Chest(-28f).Head(14f).LegN(60f, -70f).LegF(30f, -60f).HandN(0.2f, 1.1f).HandF(0.52f, 1.02f).Blade(-48f), Ease.InCubic)
                .Key(26, P().Hip(0.04f, -0.32f, -6f).Chest(-34f).Head(16f).FootN(0.3f, 0.13f).FootF(-0.24f, 0.15f, -25f).HandN(0.25f, 0.8f).HandF(0.52f, 0.5f).Blade(-28f))
                .Key(46, Stance(), Ease.InOutQuad)
                .Move(7, 7, 2.6f, 5.0f, true)
                .Hit(18, 25, Blade, 12, HitLevel.Overhead, new Vector2(1.4f, -3.0f), stun: 30, hitstop: 10, shake: 0.42f, knockdown: true, heavy: true, radius: 0.08f)
                .Fx(16, 26, FxKind.TrailBlade).Fx(26, 26, FxKind.Shockwave).Fx(26, 26, FxKind.Dust).Sword();

            m.iai.chain[Btn.S] = m.risingMoon;
            m.risingMoon.chain[Btn.S] = m.cleave;
            m.jab.chain[Btn.S] = m.iai;
            m.kick1.chain[Btn.S] = m.iai;

            m.shadowStep = new MoveDef("shadowStep", "Shadow Step", 34)
                .Key(4, P().Hip(0f, -0.2f, -10f).Chest(-30f).Head(18f).Feet(0.26f, -0.26f).HandN(0.15f, 0.95f).HandF(-0.02f, 0.78f).Blade(-170f))
                .Key(8, P().Hip(0.12f, -0.2f, -18f).Chest(-38f).Head(22f).Feet(0.4f, -0.3f).HandN(-0.1f, 0.95f).HandF(0.55f, 1.0f).Blade(0f), Ease.OutExpo)
                .Key(14, P().Hip(0.1f, -0.22f, -16f).Chest(-34f).Head(20f).Feet(0.38f, -0.3f).HandN(-0.12f, 0.95f).HandF(0.5f, 1.04f).Blade(8f))
                .Key(22, P().Hip(0.06f, -0.18f, -12f).Chest(-26f).Head(14f).Feet(0.34f, -0.28f).HandN(0f, 1.0f).HandF(0.42f, 0.98f).Blade(-150f))
                .Key(34, Stance(), Ease.InOutQuad)
                .Move(5, 13, 10.5f).Move(14, 16, 1.0f)
                .Hit(6, 13, Blade, 10, HitLevel.Mid, new Vector2(0.6f, 0.6f), stun: 26, hitstop: 8, shake: 0.25f, heavy: true, radius: 0.09f)
                .Invuln(5, 13).NoPush(4, 18)
                .Fx(5, 14, FxKind.AfterImage).Fx(6, 14, FxKind.TrailBlade).Fx(9, 9, FxKind.SlashLine).Fx(5, 5, FxKind.Dust).Sword();

            m.lowReap = new MoveDef("lowReap", "Low Reap", 32)
                .Key(6, P().Hip(-0.03f, -0.4f, 0f).Chest(-28f).Head(16f).FootN(0.24f, 0.13f).FootF(-0.28f, 0.15f, -30f).HandN(0.08f, 0.75f).HandF(-0.12f, 0.45f).Blade(-178f))
                .Key(10, P().Hip(0.06f, -0.42f, -6f).Chest(-34f).Head(18f).FootN(0.3f, 0.13f).FootF(-0.28f, 0.15f, -30f).HandN(0f, 0.75f).HandF(0.42f, 0.36f).Blade(-6f), Ease.OutExpo)
                .Key(14, P().Hip(0.06f, -0.42f, -6f).Chest(-34f).Head(18f).FootN(0.3f, 0.13f).FootF(-0.28f, 0.15f, -30f).HandN(0f, 0.75f).HandF(0.42f, 0.36f).Blade(4f))
                .Key(32, Stance(), Ease.InOutQuad)
                .Hit(9, 13, Blade, 9, HitLevel.Low, new Vector2(0.5f, 3.0f), stun: 20, hitstop: 6, shake: 0.15f, knockdown: true, otg: true, radius: 0.08f)
                .Fx(7, 14, FxKind.TrailBlade).Fx(10, 10, FxKind.Dust).Sword();

            m.counterStance = new MoveDef("counterStance", "Mirror Guard", 34)
                .Key(5, P().Hip(-0.04f, -0.14f, -2f).Chest(4f).Head(-2f).Feet(0.2f, -0.3f).HandN(0.32f, 1.22f).HandF(0.08f, 1.18f).Blade(-8f))
                .Key(26, P().Hip(-0.04f, -0.15f, -2f).Chest(4f).Head(-2f).Feet(0.2f, -0.3f).HandN(0.32f, 1.21f).HandF(0.08f, 1.17f).Blade(-8f))
                .Key(34, Stance(), Ease.InOutQuad)
                .Fx(4, 4, FxKind.Glint).Sword();
            m.counterStance.counterFrom = 4;
            m.counterStance.counterTo = 24;

            m.counterSlash = new MoveDef("counterSlash", "Kage Counter", 30)
                .Key(3, P().Hip(0.08f, -0.2f, -14f).Chest(-34f).Head(20f).Feet(0.38f, -0.28f).HandN(-0.08f, 0.96f).HandF(0.55f, 1.0f).Blade(2f), Ease.OutExpo)
                .Key(10, P().Hip(0.08f, -0.2f, -14f).Chest(-34f).Head(20f).Feet(0.38f, -0.28f).HandN(-0.08f, 0.96f).HandF(0.55f, 1.0f).Blade(6f))
                .Key(30, Stance(), Ease.InOutQuad)
                .Hit(1, 6, Blade, 12, HitLevel.Unblockable, new Vector2(1.6f, 4.5f), stun: 30, hitstop: 12, shake: 0.4f, knockdown: true, heavy: true, radius: 0.12f)
                .Invuln(0, 12).Fx(0, 8, FxKind.TrailBlade).Fx(2, 2, FxKind.SlashLine).Sword();

            // ===================== SHADOW ART =====================
            m.crescent = new MoveDef("crescent", "Crimson Crescent", 34)
                .Key(9, P().Hip(-0.06f, -0.14f, -2f).Chest(10f).Head(-6f).Feet(0.22f, -0.28f).HandN(0.25f, 1.2f).HandF(-0.18f, 1.32f).Blade(150f))
                .Key(13, P().Hip(0.1f, -0.13f, -12f).Chest(-24f).Head(12f).Feet(0.33f, -0.24f).HandN(0f, 1.05f).HandF(0.56f, 1.02f).Blade(-12f), Ease.OutExpo)
                .Key(18, P().Hip(0.1f, -0.13f, -12f).Chest(-24f).Head(12f).Feet(0.33f, -0.24f).HandN(0f, 1.05f).HandF(0.55f, 1.0f).Blade(-14f))
                .Key(34, Stance(), Ease.InOutQuad)
                .Fx(1, 12, FxKind.ChargeDark).Fx(10, 15, FxKind.TrailBlade).Sword();
            m.crescent.projectileFrame = 12;
            m.crescent.meterCost = 35;

            return m;
        }

        /// <summary>Picks a technique from button + direction (relative to facing) + context.</summary>
        public MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self)
        {
            Fighter opp = self.opponent;
            float dist = opp != null ? Mathf.Abs(opp.transform.position.x - self.transform.position.x) : 99f;
            bool oppDown = opp != null && opp.state == FState.Down;
            if (air)
            {
                return b switch { Btn.P => airPunch, Btn.K => airKick, Btn.S => airSlash, _ => null };
            }
            switch (b)
            {
                case Btn.P:
                    if (dy < 0) return oppDown && dist < 1.1f ? groundPound : crouchPunch;
                    if (fwd > 0) return dist < 0.85f && opp != null && opp.CanBeThrown ? throwGrab : lunge;
                    return jab;
                case Btn.K:
                    if (dy < 0) return sweep;
                    if (fwd > 0) return pushKick;
                    if (fwd < 0) return axeKick;
                    return kick1;
                case Btn.S:
                    if (dy < 0) return lowReap;
                    if (fwd > 0) return shadowStep;
                    if (fwd < 0) return counterStance;
                    return iai;
                case Btn.X:
                    return crescent;
            }
            return null;
        }
    }
}
