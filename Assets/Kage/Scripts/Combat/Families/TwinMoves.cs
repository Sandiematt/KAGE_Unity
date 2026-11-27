using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The twin blades family (ADR-0006): a short blade in each hand, built to be quick and light. A fast four-cut
    /// string on the Weapon button, a dash that passes clean through the foe, a cross-slash that launches, a diving
    /// air attack, pommel strikes and kicks that chain into the blades, and an air attack on every button. Compared
    /// with the katana set every move is shorter (a four-hit string takes about as long as two katana cuts) and
    /// hits for a third to a half of the damage, so the weapon wins by volume, not by single blows. Tsubame, the
    /// daggers of the Order's Runners and Flickers, Hisame's fans and Kagehira &amp; Yūrei all fight with it; the
    /// weapon's visual picks the drawing. JS move timings and damage were a design brief only.
    /// Frames are authored (rate 1); the weapon's speed and the character's move rate scale playback.
    /// </summary>
    public sealed class TwinMoves : MoveSet
    {
        public MoveDef jab, hook, crouchJab, lunge, groundStab;
        public MoveDef kick1, kick2, pushKick, backKick, sweep;
        public MoveDef fang1, fang2, fang3, fang4, crossSlash, dashThrough, lowSlice;
        public MoveDef airJab, airKick, airSpin, dive;

        /// <summary>Metres from guard to tip of each blade (the katana's is 0.72).</summary>
        public const float BladeLength = 0.42f;

        public static readonly WeaponFamilyDef Family = new()
        {
            id = WeaponFamilies.Twin,
            referenceWeapon = "tsubame",
            build = sets => Build((KatanaMoves)sets.Get(WeaponFamilies.Katana)),
            grip = GripPose.Dual,
            bladeLength = BladeLength,
            rageArt = Fighter.ThousandShadows,
            visual = new TwinVisual(),
        };

        // Both blades' lines are tested in every blade window (Blade is the clash-eligible point, BladeN the near blade's line).
        static readonly HitPoint[] Blades = { HitPoint.Blade, HitPoint.BladeN };
        static readonly HitPoint[] Fist = { HitPoint.Fist };
        static readonly HitPoint[] FistF = { HitPoint.FistF };
        static readonly HitPoint[] FootN = { HitPoint.FootN, HitPoint.ShinN };
        static readonly HitPoint[] FootF = { HitPoint.FootF, HitPoint.ShinF };
        static readonly HitPoint[] FootTip = { HitPoint.FootN };

        // ---- poses: a dual-wield pose carries a direction for each blade (Blade far, BladeN near) ----
        static Pose T() => new Pose().Dual();

        /// <summary>The ready stance the idle split-hands pose settles to: far blade up, near blade low and forward.</summary>
        static Pose Rest() => T().Hip(0f, -0.08f, -4f).Chest(-8f).Head(5f).Feet(0.24f, -0.26f)
            .HandN(0.44f, 1.01f).BladeN(10f).HandF(0.27f, 0.97f).Blade(34f);

        static Pose Lunge(float chest = -22f) => T().Hip(0.1f, -0.1f, -10f).Chest(chest).Head(10f).Feet(0.36f, -0.24f);

        public static TwinMoves Build(KatanaMoves katana)
        {
            var m = new TwinMoves();
            m.ShareBodyMoves(katana);

            // ===================== THE FOUR-CUT STRING (Weapon) =====================
            // 1: the near blade cuts level across the front
            m.fang1 = new MoveDef("twin.fang1", "Fang", 14)
                .Key(3, T().Hip(0f, -0.1f, -2f).Chest(6f).Head(2f).Feet(0.26f, -0.26f).HandN(0.16f, 1.3f).BladeN(115f).HandF(0.2f, 1.0f).Blade(40f))
                .Key(6, Lunge().HandN(0.62f, 1.16f).BladeN(-6f).HandF(0.24f, 1.08f).Blade(70f), Ease.OutExpo)
                .Key(9, Lunge().HandN(0.6f, 1.15f).BladeN(-12f).HandF(0.24f, 1.08f).Blade(70f))
                .Key(14, Rest(), Ease.InOutQuad)
                .Hit(4, 8, Blades, 3, HitLevel.Mid, new Vector2(0.7f, 0f), stun: 12, hitstop: 3, shake: 0.04f, radius: 0.07f)
                .Fx(3, 9, FxKind.TrailBladeN).Sword().Cancel(7);

            // 2: the far blade crosses back, high to low
            m.fang2 = new MoveDef("twin.fang2", "Counter Fang", 14)
                .Key(3, T().Hip(0.04f, -0.12f, -6f).Chest(-6f).Head(4f).Feet(0.3f, -0.26f).HandN(0.5f, 1.1f).BladeN(-8f).HandF(0.1f, 1.3f).Blade(125f))
                .Key(6, Lunge(-26f).Hip(0.1f, -0.12f, -12f).Feet(0.38f, -0.22f).HandN(0.3f, 1.0f).BladeN(20f).HandF(0.64f, 1.0f).Blade(-14f), Ease.OutExpo)
                .Key(9, Lunge(-26f).Hip(0.1f, -0.12f, -12f).Feet(0.38f, -0.22f).HandN(0.3f, 1.0f).BladeN(20f).HandF(0.6f, 0.98f).Blade(-20f))
                .Key(14, Rest(), Ease.InOutQuad)
                .Hit(4, 8, Blades, 3, HitLevel.Mid, new Vector2(0.7f, 0f), stun: 12, hitstop: 3, shake: 0.04f, radius: 0.07f)
                .Fx(3, 9, FxKind.TrailBlade).Sword().Cancel(7);

            // 3: both blades sweep up from low, a scissor rising
            m.fang3 = new MoveDef("twin.fang3", "Rising Fang", 16)
                .Key(3, T().Hip(0.04f, -0.26f, -8f).Chest(-24f).Head(14f).Feet(0.3f, -0.24f).HandN(0.4f, 0.6f).BladeN(-30f).HandF(0.34f, 0.62f).Blade(-40f))
                .Key(7, T().Hip(0.1f, -0.04f, 2f).Chest(8f).Head(-6f).FootN(0.34f, 0.13f).FootF(-0.16f, 0.15f, -20f).HandN(0.4f, 1.6f).BladeN(80f).HandF(0.36f, 1.5f).Blade(60f), Ease.OutExpo)
                .Key(10, T().Hip(0.1f, -0.04f, 2f).Chest(8f).Head(-6f).FootN(0.32f, 0.13f).FootF(-0.16f, 0.15f, -20f).HandN(0.34f, 1.62f).BladeN(100f).HandF(0.3f, 1.55f).Blade(80f))
                .Key(16, Rest(), Ease.InOutQuad)
                .Move(2, 6, 1.0f)
                .Hit(4, 9, Blades, 3, HitLevel.Mid, new Vector2(0.5f, 1.6f), stun: 14, hitstop: 3, shake: 0.05f, radius: 0.07f)
                .Fx(3, 10, FxKind.TrailBlade).Fx(3, 10, FxKind.TrailBladeN).Sword().Cancel(8);

            // 4: the finisher: both blades come down in a wide X with a step in
            m.fang4 = new MoveDef("twin.fang4", "Swallow Cross", 24)
                .Key(4, T().Hip(0f, -0.2f, 6f).Chest(10f).Head(-4f).Feet(0.26f, -0.28f).HandN(0f, 1.4f).BladeN(150f).HandF(0f, 1.4f).Blade(130f))
                .Key(8, T().Hip(0.14f, -0.2f, -14f).Chest(-34f).Head(18f).Feet(0.44f, -0.24f).HandN(0.62f, 0.7f).BladeN(-50f).HandF(0.6f, 0.78f).Blade(-30f), Ease.OutExpo)
                .Key(12, T().Hip(0.14f, -0.2f, -14f).Chest(-34f).Head(18f).Feet(0.44f, -0.24f).HandN(0.6f, 0.7f).BladeN(-56f).HandF(0.58f, 0.78f).Blade(-36f))
                .Key(24, Rest(), Ease.InOutQuad)
                .Move(5, 8, 2.2f)
                .Hit(6, 11, Blades, 5, HitLevel.Overhead, new Vector2(1.4f, -1.0f), stun: 22, hitstop: 6, shake: 0.14f, knockdown: true, heavy: true, radius: 0.08f)
                .Fx(5, 12, FxKind.TrailBlade).Fx(5, 12, FxKind.TrailBladeN).Fx(11, 11, FxKind.Dust).Sword();

            m.fang1.chain[Btn.S] = m.fang2;
            m.fang2.chain[Btn.S] = m.fang3;
            m.fang3.chain[Btn.S] = m.fang4;

            // ===================== SPECIAL CUTS (Weapon + direction) =====================
            // forward: the dash-through, a lunge that passes clean through the foe
            m.dashThrough = new MoveDef("twin.dashThrough", "Swallow Pass", 26)
                .Key(4, T().Hip(0f, -0.24f, -14f).Chest(-34f).Head(20f).Feet(0.3f, -0.3f).HandN(-0.1f, 1.0f).BladeN(-170f).HandF(-0.12f, 0.9f).Blade(-175f))
                .Key(7, T().Hip(0.16f, -0.26f, -20f).Chest(-40f).Head(24f).Feet(0.46f, -0.3f).HandN(0.6f, 1.0f).BladeN(-5f).HandF(0.56f, 1.04f).Blade(2f), Ease.OutExpo)
                .Key(13, T().Hip(0.14f, -0.24f, -18f).Chest(-38f).Head(22f).Feet(0.44f, -0.3f).HandN(0.54f, 1.0f).BladeN(-10f).HandF(0.5f, 1.04f).Blade(-4f))
                .Key(26, Rest(), Ease.InOutQuad)
                .Move(4, 11, 12.0f).Move(12, 15, 1.2f)
                .Hit(5, 11, Blades, 4, HitLevel.Mid, new Vector2(0.8f, 0.6f), stun: 18, hitstop: 4, shake: 0.08f, radius: 0.08f)
                .Invuln(4, 10).NoPush(3, 15)
                .Fx(4, 12, FxKind.AfterImage).Fx(5, 12, FxKind.TrailBlade).Fx(5, 12, FxKind.TrailBladeN).Fx(4, 4, FxKind.Dust).Sword();

            // back: the cross-slash, both blades cut an X in front and throw the foe up
            m.crossSlash = new MoveDef("twin.crossSlash", "Cross Slash", 22)
                .Key(5, T().Hip(-0.04f, -0.14f, 0f).Chest(6f).Head(0f).Feet(0.22f, -0.28f).HandN(0.1f, 1.5f).BladeN(120f).HandF(0.1f, 0.6f).Blade(-60f))
                .Key(8, Lunge(-26f).Hip(0.1f, -0.12f, -12f).HandN(0.6f, 0.8f).BladeN(-35f).HandF(0.6f, 1.4f).Blade(35f), Ease.OutExpo)
                .Key(11, Lunge(-26f).Hip(0.1f, -0.12f, -12f).HandN(0.58f, 0.82f).BladeN(-40f).HandF(0.58f, 1.38f).Blade(40f))
                .Key(22, Rest(), Ease.InOutQuad)
                .Move(4, 8, 1.6f).JumpCancel(13)
                .Hit(6, 10, Blades, 6, HitLevel.Mid, new Vector2(0.5f, 5.2f), stun: 24, hitstop: 5, shake: 0.14f, launch: true, heavy: true, radius: 0.08f)
                .Fx(5, 11, FxKind.TrailBlade).Fx(5, 11, FxKind.TrailBladeN).Fx(8, 8, FxKind.SlashLine).Sword();

            // down: a low double cut across the shins
            m.lowSlice = new MoveDef("twin.lowSlice", "Low Slice", 20)
                .Key(4, T().Hip(-0.02f, -0.4f, 0f).Chest(-24f).Head(14f).FootN(0.24f, 0.13f).FootF(-0.28f, 0.15f, -30f).HandN(-0.06f, 0.5f).BladeN(-170f).HandF(0.1f, 0.5f).Blade(-170f))
                .Key(7, T().Hip(0.06f, -0.42f, -6f).Chest(-32f).Head(16f).FootN(0.3f, 0.13f).FootF(-0.28f, 0.15f, -30f).HandN(0.6f, 0.36f).BladeN(-5f).HandF(0.5f, 0.4f).Blade(0f), Ease.OutExpo)
                .Key(10, T().Hip(0.06f, -0.42f, -6f).Chest(-32f).Head(16f).FootN(0.3f, 0.13f).FootF(-0.28f, 0.15f, -30f).HandN(0.6f, 0.36f).BladeN(2f).HandF(0.5f, 0.4f).Blade(6f))
                .Key(20, Rest(), Ease.InOutQuad)
                .Hit(5, 9, Blades, 4, HitLevel.Low, new Vector2(0.5f, 2.6f), stun: 18, hitstop: 4, shake: 0.08f, knockdown: true, otg: true, radius: 0.07f)
                .Fx(5, 10, FxKind.TrailBlade).Fx(5, 10, FxKind.TrailBladeN).Fx(7, 7, FxKind.Dust).Sword();

            // ===================== POMMEL STRIKES (Punch) =====================
            m.jab = new MoveDef("twin.jab", "Pommel Jab", 14)
                .Key(3, T().Hip(0.02f, -0.1f, -4f).Chest(-6f).Head(4f).Feet(0.26f, -0.26f).HandN(0.18f, 1.15f).BladeN(90f).HandF(0.24f, 0.98f).Blade(40f))
                .Key(5, T().Hip(0.08f, -0.09f, -6f).Chest(-20f).Head(10f).Feet(0.32f, -0.22f).HandN(0.56f, 1.2f).BladeN(80f).HandF(0.22f, 0.9f).Blade(50f), Ease.OutExpo)
                .Key(8, T().Hip(0.08f, -0.09f, -6f).Chest(-20f).Head(10f).Feet(0.32f, -0.22f).HandN(0.55f, 1.2f).BladeN(80f).HandF(0.22f, 0.9f).Blade(50f))
                .Key(14, Rest(), Ease.InOutQuad)
                .Hit(4, 7, Fist, 3, HitLevel.High, new Vector2(0.8f, 0f), stun: 12, hitstop: 3, shake: 0.04f)
                .Fx(3, 7, FxKind.TrailFist).Cancel(7);

            m.hook = new MoveDef("twin.hook", "Pommel Hook", 16)
                .Key(4, T().Hip(0.04f, -0.1f, -6f).Chest(-8f).Head(5f).Feet(0.28f, -0.24f).HandN(0.4f, 1.0f).BladeN(40f).HandF(0.1f, 1.22f).Blade(120f))
                .Key(6, T().Hip(0.09f, -0.09f, -8f).Chest(-26f).Head(14f).Feet(0.33f, -0.2f).HandN(0.22f, 0.92f).BladeN(40f).HandF(0.58f, 1.18f).Blade(80f), Ease.OutExpo)
                .Key(9, T().Hip(0.09f, -0.09f, -8f).Chest(-26f).Head(14f).Feet(0.33f, -0.2f).HandN(0.22f, 0.92f).BladeN(40f).HandF(0.57f, 1.18f).Blade(80f))
                .Key(16, Rest(), Ease.InOutQuad)
                .Hit(5, 8, FistF, 3, HitLevel.High, new Vector2(1.0f, 0f), stun: 12, hitstop: 3, shake: 0.05f)
                .Fx(4, 8, FxKind.TrailFist).Sword().Cancel(8);

            m.crouchJab = new MoveDef("twin.crouchJab", "Low Pommel", 14)
                .Key(3, T().Hip(-0.02f, -0.3f, 0f).Chest(-20f).Head(14f).FootN(0.24f, 0.13f).FootF(-0.26f, 0.16f, -25f).HandN(0.18f, 0.88f).BladeN(60f).HandF(0.2f, 0.74f).Blade(10f))
                .Key(5, T().Hip(0.04f, -0.3f, 0f).Chest(-24f).Head(16f).FootN(0.26f, 0.13f).FootF(-0.26f, 0.15f, -25f).HandN(0.52f, 0.82f).BladeN(60f).HandF(0.1f, 0.62f).Blade(0f), Ease.OutExpo)
                .Key(8, T().Hip(0.04f, -0.3f, 0f).Chest(-24f).Head(16f).FootN(0.26f, 0.13f).FootF(-0.26f, 0.15f, -25f).HandN(0.5f, 0.82f).BladeN(60f).HandF(0.1f, 0.62f).Blade(0f))
                .Key(14, Rest(), Ease.InOutQuad)
                .Hit(4, 7, Fist, 2, HitLevel.Mid, new Vector2(0.6f, 0f), stun: 10, hitstop: 3, shake: 0.03f)
                .Fx(3, 7, FxKind.TrailFist).Cancel(7);

            m.lunge = new MoveDef("twin.lunge", "Swallow Thrust", 18)
                .Key(3, T().Hip(0.02f, -0.14f, -8f).Chest(-14f).Head(8f).Feet(0.3f, -0.26f).HandN(0.2f, 1.1f).BladeN(10f).HandF(0.2f, 1.0f).Blade(20f))
                .Key(6, T().Hip(0.12f, -0.16f, -12f).Chest(-28f).Head(14f).Feet(0.42f, -0.24f).HandN(0.62f, 1.14f).BladeN(0f).HandF(0.3f, 1.0f).Blade(14f), Ease.OutExpo)
                .Key(10, T().Hip(0.12f, -0.16f, -12f).Chest(-28f).Head(14f).Feet(0.42f, -0.24f).HandN(0.6f, 1.14f).BladeN(0f).HandF(0.3f, 1.0f).Blade(14f))
                .Key(18, Rest(), Ease.InOutQuad)
                .Move(2, 8, 2.6f)
                .Hit(5, 9, Blades, 4, HitLevel.High, new Vector2(1.1f, 0.2f), stun: 14, hitstop: 4, shake: 0.06f, radius: 0.07f)
                .Fx(4, 9, FxKind.TrailBladeN).Fx(2, 2, FxKind.Dust).Sword().Cancel(9);

            m.groundStab = new MoveDef("twin.groundStab", "Twin Stab", 22)
                .Key(5, T().Hip(0f, -0.1f, 0f).Chest(8f).LegN(80f, -110f).LegF(60f, -100f).HandN(0.2f, 1.6f).BladeN(110f).HandF(0.14f, 1.55f).Blade(100f))
                .Key(9, T().Hip(0.05f, -0.34f, 0f).Chest(-34f).Head(18f).FootN(0.26f, 0.13f).FootF(-0.24f, 0.15f, -25f).HandN(0.5f, 0.3f).BladeN(-85f).HandF(0.44f, 0.34f).Blade(-80f), Ease.InCubic)
                .Key(14, T().Hip(0.05f, -0.36f, 0f).Chest(-34f).Head(18f).FootN(0.26f, 0.13f).FootF(-0.24f, 0.15f, -25f).HandN(0.5f, 0.26f).BladeN(-85f).HandF(0.44f, 0.3f).Blade(-80f))
                .Key(22, Rest(), Ease.InOutQuad)
                .Hit(8, 13, Blades, 4, HitLevel.Mid, new Vector2(0.3f, 0.6f), stun: 16, hitstop: 5, shake: 0.12f, otg: true, radius: 0.07f)
                .Fx(7, 13, FxKind.TrailBlade).Fx(7, 13, FxKind.TrailBladeN).Fx(10, 10, FxKind.Dust).Sword();

            m.jab.chain[Btn.P] = m.hook;
            m.hook.chain[Btn.S] = m.fang3;
            m.crouchJab.chain[Btn.P] = m.hook;
            m.jab.chain[Btn.S] = m.fang2;
            m.lunge.chain[Btn.S] = m.fang2;

            // ===================== KICKS =====================
            m.kick1 = new MoveDef("twin.kick1", "Quick Kick", 18)
                .Key(4, T().Hip(0.02f, -0.07f, 12f).Chest(10f).Head(-4f).LegN(95f, -115f, 10f).FootF(-0.16f, 0.13f).HandN(0.3f, 1.2f).BladeN(60f).HandF(0.1f, 0.95f).Blade(60f))
                .Key(7, T().Hip(0.14f, -0.03f, 28f).Chest(24f).Head(-16f).LegN(116f, -6f, 12f).FootF(-0.12f, 0.13f).HandN(0.2f, 1.25f).BladeN(70f).HandF(-0.1f, 0.95f).Blade(60f), Ease.OutExpo)
                .Key(10, T().Hip(0.14f, -0.03f, 28f).Chest(24f).Head(-16f).LegN(114f, -8f, 12f).FootF(-0.12f, 0.13f).HandN(0.2f, 1.25f).BladeN(70f).HandF(-0.1f, 0.95f).Blade(60f))
                .Key(14, T().Hip(0.06f, -0.07f, 10f).Chest(6f).Head(-2f).LegN(90f, -110f).FootF(-0.18f, 0.13f).HandN(0.3f, 1.1f).BladeN(40f).HandF(0.1f, 0.9f).Blade(50f))
                .Key(18, Rest(), Ease.InOutQuad)
                .Hit(6, 9, FootN, 3, HitLevel.High, new Vector2(1.0f, 0.2f), stun: 12, hitstop: 3, shake: 0.06f, radius: 0.1f)
                .Fx(5, 10, FxKind.TrailFootN).Cancel(9);

            m.kick2 = new MoveDef("twin.kick2", "Switch Kick", 18)
                .Key(4, T().Hip(0.05f, -0.08f, 6f).Chest(4f).FootN(0.22f, 0.13f).LegF(100f, -120f).HandN(0.3f, 1.18f).BladeN(60f).HandF(0.15f, 0.9f).Blade(60f))
                .Key(7, T().Hip(0.15f, -0.04f, 14f).Chest(16f).Head(-10f).FootN(0.16f, 0.13f).LegF(118f, -8f, 10f).HandN(0.2f, 1.22f).BladeN(70f).HandF(0f, 0.92f).Blade(60f), Ease.OutExpo)
                .Key(10, T().Hip(0.15f, -0.04f, 14f).Chest(16f).Head(-10f).FootN(0.16f, 0.13f).LegF(116f, -10f, 10f).HandN(0.2f, 1.22f).BladeN(70f).HandF(0f, 0.92f).Blade(60f))
                .Key(18, Rest(), Ease.InOutQuad)
                .Hit(6, 9, FootF, 3, HitLevel.Mid, new Vector2(1.4f, 0.3f), stun: 12, hitstop: 3, shake: 0.06f, radius: 0.1f)
                .Fx(5, 10, FxKind.TrailFootF).Cancel(9);

            m.kick1.chain[Btn.K] = m.kick2;
            m.kick1.chain[Btn.S] = m.fang2;
            m.kick2.chain[Btn.S] = m.fang3;
            m.jab.chain[Btn.K] = m.kick1;
            m.hook.chain[Btn.K] = m.kick2;

            m.pushKick = new MoveDef("twin.pushKick", "Shove Kick", 20)
                .Key(4, T().Hip(0f, -0.06f, 8f).Chest(8f).LegN(100f, -120f, 15f).FootF(-0.2f, 0.13f).HandN(0.3f, 1.2f).BladeN(60f).HandF(0.15f, 0.95f).Blade(60f))
                .Key(7, T().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(96f, -4f, 40f).FootF(-0.14f, 0.13f).HandN(0.2f, 1.22f).BladeN(70f).HandF(0.05f, 0.92f).Blade(60f), Ease.OutExpo)
                .Key(10, T().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(95f, -5f, 40f).FootF(-0.14f, 0.13f).HandN(0.2f, 1.22f).BladeN(70f).HandF(0.05f, 0.92f).Blade(60f))
                .Key(20, Rest(), Ease.InOutQuad)
                .Hit(6, 10, FootTip, 3, HitLevel.Mid, new Vector2(3.4f, 0.5f), stun: 14, hitstop: 4, shake: 0.08f, radius: 0.11f)
                .Fx(5, 10, FxKind.TrailFootN);

            m.backKick = new MoveDef("twin.backKick", "Heel Kick", 24)
                .Key(5, T().Hip(0.02f, -0.1f, -10f).Chest(-14f).Head(8f).FootN(0.2f, 0.13f).LegF(104f, -124f).HandN(0.4f, 1.1f).BladeN(50f).HandF(0.2f, 1.0f).Blade(70f))
                .Key(8, T().Hip(0.16f, -0.04f, 16f).Chest(20f).Head(-12f).FootN(0.18f, 0.13f).LegF(122f, -6f, 12f).HandN(0.22f, 1.3f).BladeN(70f).HandF(0.0f, 1.0f).Blade(60f), Ease.OutExpo)
                .Key(12, T().Hip(0.16f, -0.04f, 16f).Chest(20f).Head(-12f).FootN(0.18f, 0.13f).LegF(120f, -8f, 12f).HandN(0.22f, 1.3f).BladeN(70f).HandF(0.0f, 1.0f).Blade(60f))
                .Key(24, Rest(), Ease.InOutQuad)
                .Hit(7, 11, FootF, 4, HitLevel.Mid, new Vector2(2.4f, 0.4f), stun: 16, hitstop: 4, shake: 0.1f, knockdown: true, radius: 0.1f)
                .Fx(6, 12, FxKind.TrailFootF);

            m.sweep = new MoveDef("twin.sweep", "Quick Sweep", 22)
                .Key(5, T().Hip(-0.04f, -0.42f, 0f).Chest(-30f).Head(16f).FootN(0.2f, 0.13f).FootF(-0.28f, 0.15f, -30f).HandN(0.3f, 0.7f).BladeN(20f).HandF(-0.1f, 0.6f).Blade(-60f))
                .Key(8, T().Hip(-0.04f, -0.44f, 6f).Chest(-34f).Head(18f).LegN(92f, -2f, 25f).FootF(-0.28f, 0.15f, -30f).HandN(0.3f, 0.7f).BladeN(20f).HandF(-0.16f, 0.5f).Blade(-60f), Ease.OutExpo)
                .Key(12, T().Hip(-0.04f, -0.44f, 6f).Chest(-34f).Head(18f).LegN(90f, -4f, 25f).FootF(-0.28f, 0.15f, -30f).HandN(0.3f, 0.7f).BladeN(20f).HandF(-0.16f, 0.5f).Blade(-60f))
                .Key(22, Rest(), Ease.InOutQuad)
                .Hit(6, 10, FootN, 3, HitLevel.Low, new Vector2(0.6f, 3.0f), stun: 14, hitstop: 4, shake: 0.08f, knockdown: true, otg: true, radius: 0.11f)
                .Fx(5, 11, FxKind.TrailFootN).Fx(8, 8, FxKind.Dust);

            // ===================== AIR =====================
            m.airJab = new MoveDef("twin.airJab", "Air Pommel", 16)
                .Key(3, T().Hip(0f, 0f, 0f).Chest(8f).LegN(60f, -90f).LegF(40f, -100f).HandN(0.15f, 1.7f).BladeN(110f).HandF(0.1f, 1.2f).Blade(60f))
                .Key(6, T().Hip(0f, 0f, 0f).Chest(-20f).Head(10f).LegN(60f, -90f).LegF(40f, -100f).HandN(0.52f, 1.1f).BladeN(70f).HandF(0.1f, 1.1f).Blade(60f), Ease.OutExpo)
                .Key(16, T().Hip(0f, 0f, 0f).Chest(-10f).Head(6f).LegN(50f, -70f).LegF(30f, -80f).HandN(0.4f, 1.1f).BladeN(60f).HandF(0.1f, 1.1f).Blade(60f))
                .Hit(5, 9, Fist, 3, HitLevel.Overhead, new Vector2(1.0f, 0.4f), stun: 12, hitstop: 3, shake: 0.05f)
                .Fx(4, 9, FxKind.TrailFist).Air();

            m.airKick = new MoveDef("twin.airKick", "Swoop Kick", 28)
                .Key(4, T().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(70f, -4f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).BladeN(110f).HandF(-0.15f, 1.1f).Blade(100f), Ease.OutExpo)
                .Key(28, T().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(68f, -6f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).BladeN(110f).HandF(-0.15f, 1.1f).Blade(100f))
                .Hit(3, 18, FootN, 4, HitLevel.Overhead, new Vector2(2.0f, 1.2f), stun: 16, hitstop: 4, shake: 0.1f, radius: 0.11f)
                .Fx(3, 12, FxKind.TrailFootN).Air();

            // both blades crossed high, then scissored out through the foe in two flat cuts, the body upright (no whole-body spin):
            // the far blade sweeps forward, then the near blade answers on the way back
            m.airSpin = new MoveDef("twin.airSpin", "Whirl", 24)
                .Key(2, T().Hip(0f, 0f, 4f).Chest(6f).Head(-4f).LegN(90f, -120f).LegF(80f, -110f).HandN(0.1f, 1.3f).BladeN(150f).HandF(-0.1f, 1.3f).Blade(160f), Ease.OutQuad)
                .Key(6, T().Hip(0f, 0f, -10f).Chest(-16f).Head(8f).LegN(90f, -120f).LegF(80f, -110f).HandN(0.16f, 1.24f).BladeN(130f).HandF(0.56f, 1.12f).Blade(-6f), Ease.OutExpo)
                .Key(8, T().Hip(0f, 0f, -10f).Chest(-16f).Head(8f).LegN(90f, -120f).LegF(80f, -110f).HandN(0.18f, 1.22f).BladeN(120f).HandF(0.5f, 1.04f).Blade(-20f))
                .Key(12, T().Hip(0f, 0f, -12f).Chest(-20f).Head(10f).LegN(92f, -122f).LegF(82f, -112f).HandN(0.6f, 1.12f).BladeN(-4f).HandF(0.2f, 1.1f).Blade(60f), Ease.OutExpo)
                .Key(16, T().Hip(0f, 0f, -8f).Chest(-14f).Head(8f).LegN(92f, -122f).LegF(82f, -112f).HandN(0.52f, 1.04f).BladeN(-18f).HandF(0.16f, 1.1f).Blade(70f))
                .Key(24, T().Hip(0f, 0f, 0f).Chest(5f).LegN(40f, -60f).LegF(20f, -50f).HandN(0.4f, 1.1f).BladeN(10f).HandF(-0.2f, 1.1f).Blade(170f))
                .Hit(3, 8, Blades, 3, HitLevel.Overhead, new Vector2(1.0f, 0.8f), stun: 12, hitstop: 3, shake: 0.05f, radius: 0.07f)
                .Hit(9, 14, Blades, 3, HitLevel.Overhead, new Vector2(1.6f, 2.0f), stun: 16, hitstop: 4, shake: 0.08f, knockdown: true, radius: 0.07f)
                .Fx(3, 15, FxKind.TrailBlade).Fx(3, 15, FxKind.TrailBladeN).Sword().Air().Normalize();

            // the diving air attack: blades straight down, a steep drop through the foe
            m.dive = new MoveDef("twin.dive", "Swallow Dive", 30)
                .Key(3, T().Hip(0f, 0f, 0f).Chest(10f).LegN(60f, -90f).LegF(40f, -100f).HandN(0.2f, 0.7f).BladeN(-100f).HandF(0.1f, 0.65f).Blade(-95f), Ease.OutExpo)
                .Key(30, T().Hip(0f, 0f, 0f).Chest(8f).LegN(58f, -88f).LegF(38f, -98f).HandN(0.22f, 0.7f).BladeN(-100f).HandF(0.12f, 0.65f).Blade(-95f))
                .Move(2, 2, 1.0f, -13.0f, true)
                .Hit(3, 26, Blades, 5, HitLevel.Overhead, new Vector2(0.6f, -2.0f), stun: 18, hitstop: 5, shake: 0.14f, knockdown: true, heavy: true, radius: 0.08f)
                .Fx(2, 26, FxKind.TrailBlade).Fx(2, 26, FxKind.TrailBladeN).Sword().Air();

            return m;
        }

        /// <summary>Picks a technique from button + direction (relative to facing) + context.</summary>
        public override MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self)
        {
            Fighter opp = self.opponent;
            float dist = opp != null ? Mathf.Abs(opp.transform.position.x - self.transform.position.x) : 99f;
            bool oppDown = opp != null && opp.state == FState.Down;
            if (air)
            {
                switch (b)
                {
                    case Btn.P: return airJab;
                    case Btn.K: return airKick;
                    case Btn.S: return dy < 0 ? dive : airSpin;
                }
                return null;
            }
            switch (b)
            {
                case Btn.P:
                    if (dy < 0) return oppDown && dist < 1.1f ? groundStab : crouchJab;
                    if (fwd > 0) return dist < 0.85f && opp != null && opp.CanBeThrown ? throwGrab : lunge;
                    return jab;
                case Btn.K:
                    if (dy < 0) return sweep;
                    if (fwd > 0) return pushKick;
                    if (fwd < 0) return backKick;
                    return kick1;
                case Btn.S:
                    if (dy < 0) return lowSlice;
                    if (fwd > 0) return dashThrough;
                    if (fwd < 0) return crossSlash;
                    return fang1;
            }
            return null;
        }

        public override IEnumerable<MoveDef> All => new[]
        {
            jab, hook, crouchJab, lunge, groundStab, kick1, kick2, pushKick, backKick, sweep,
            fang1, fang2, fang3, fang4, crossSlash, dashThrough, lowSlice,
            airJab, airKick, airSpin, dive,
            throwGrab, throwWhiff, roll, backflip, getUp, getUpFront,
        };
    }
}
