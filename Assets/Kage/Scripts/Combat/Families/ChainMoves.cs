using System.Collections.Generic;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// The chain sickle family (ADR-0006, SAN-87): Yamigama, and the Order Chainthrower's chain hook. A one-handed
    /// sickle on a chain: the far hand holds the sickle, the near hand the chain's loose end. Close in it fights with
    /// short reaping cuts; at mid range the Weapon button lashes the sickle out on its chain, and a pull hooks the foe
    /// and drags him in.
    /// <para>
    /// The lash is a <see cref="HitPoint.Blade"/> line, not a projectile: the move extends the blade line along its
    /// own axis (<see cref="MoveDef.Extend"/>), so the hand-to-sickle line is the hit line, it follows the move's
    /// frames and playback rate, and it can clash like any blade. The drawn chain is that same line
    /// (<see cref="ChainVisual"/>). The pull is a hit whose knockback points at the attacker, so the foe is dragged
    /// by the ordinary knockback and push, not moved by hand.
    /// </para>
    /// <para>
    /// Weapon button: Reaping Cut (a diagonal cut from above), Rising Reap, Chain Lash forward, Grave Hook down (a low
    /// hook), Crescent Lift back (a launcher). Punch: near-fist jab, Hilt Strike, Reaper's Pull forward (the shared
    /// throw when the foe is close), Low Jab, Ground Hook on a downed foe. Kick: kicks, a sweep. Air: Air Pommel,
    /// Swoop Kick, Moon Hook, Shadow Fall. The body moves (roll, backflip, get-ups, throw) are the katana set's, by
    /// reference.
    /// </para>
    /// </summary>
    public sealed class ChainMoves : MoveSet
    {
        public MoveDef jab, hilt, crouchJab, pull, groundHook;
        public MoveDef kick1, kick2, pushKick, backKick, sweep;
        public MoveDef reap, reapUp, lash, graveHook, crescentLift;
        public MoveDef airJab, airKick, airReap, shadowFall;

        /// <summary>Metres from the far hand's guard point to the sickle's tip when the chain is not out (the katana's is 0.72).</summary>
        public const float BladeLength = 0.5f;

        public static readonly WeaponFamilyDef Family = new()
        {
            id = WeaponFamilies.Chain,
            referenceWeapon = "yamigama",
            build = sets => Build((KatanaMoves)sets.Get(WeaponFamilies.Katana)),
            grip = GripPose.OneHand,
            bladeLength = BladeLength,
            rageArt = Fighter.ThousandShadows,
            visual = new ChainVisual(),
        };

        static readonly HitPoint[] Fist = { HitPoint.Fist };
        static readonly HitPoint[] Hilt = { HitPoint.Hilt };
        static readonly HitPoint[] FootN = { HitPoint.FootN, HitPoint.ShinN };
        static readonly HitPoint[] FootF = { HitPoint.FootF, HitPoint.ShinF };
        static readonly HitPoint[] FootTip = { HitPoint.FootN };
        static readonly HitPoint[] Blade = { HitPoint.Blade };

        static Pose P() => new Pose();

        /// <summary>A lunging body (hips forward, chest down) for the throws of the chain.</summary>
        static Pose Lunge(float hx = 0.12f) => P().Hip(hx, -0.1f, -12f).Chest(-22f).Head(10f).Feet(0.38f, -0.24f);

        /// <summary>The body low, for hooks along the floor.</summary>
        static Pose Low(float hx = 0.06f) => P().Hip(hx, -0.42f, -6f).Chest(-34f).Head(18f).FootN(0.3f, 0.13f).FootF(-0.28f, 0.15f, -30f);

        ChainMoves(KatanaMoves katana) { ShareBodyMoves(katana); }

        public static ChainMoves Build(KatanaMoves katana)
        {
            var m = new ChainMoves(katana);

            // ===================== PUNCH BUTTON =====================
            m.jab = new MoveDef("chain.jab", "Chain Jab", 16)
                .Key(4, Stance().Chest(-11f).HandN(0.13f, 1.15f, 10f))
                .Key(6, P().Hip(0.07f, -0.08f, -6f).Chest(-22f).Head(12f).Feet(0.31f, -0.22f).HandN(0.56f, 1.2f).HandF(0.22f, 0.86f).Blade(-40f), Ease.OutExpo)
                .Key(9, P().Hip(0.07f, -0.08f, -6f).Chest(-22f).Head(12f).Feet(0.31f, -0.22f).HandN(0.55f, 1.2f).HandF(0.22f, 0.86f).Blade(-40f))
                .Key(16, Stance(), Ease.InOutQuad)
                .Hit(5, 8, Fist, 3, HitLevel.High, new Vector2(0.9f, 0f), stun: 13, hitstop: 4, shake: 0.05f)
                .Fx(4, 8, FxKind.TrailFist).Cancel(8);

            // the sickle turned round: the handle's ring and pommel strike
            m.hilt = new MoveDef("chain.hilt", "Hilt Strike", 20)
                .Key(5, Stance().Chest(-4f).HandF(0.02f, 1.0f).Blade(170f).HandN(0.2f, 0.9f, 10f))
                .Key(8, P().Hip(0.09f, -0.09f, -8f).Chest(-26f).Head(14f).Feet(0.33f, -0.2f).HandN(0.2f, 0.9f, 10f).HandF(0.56f, 1.16f).Blade(178f), Ease.OutExpo)
                .Key(12, P().Hip(0.09f, -0.09f, -8f).Chest(-26f).Head(14f).Feet(0.33f, -0.2f).HandN(0.2f, 0.9f, 10f).HandF(0.55f, 1.16f).Blade(178f))
                .Key(20, Stance(), Ease.InOutQuad)
                .Hit(7, 10, Hilt, 4, HitLevel.High, new Vector2(1.3f, 0f), stun: 15, hitstop: 4, shake: 0.07f)
                .Sword().Cancel(10);

            m.crouchJab = new MoveDef("chain.crouchJab", "Low Jab", 16)
                .Key(4, Crouch().HandN(0.15f, 0.9f))
                .Key(6, P().Hip(0.04f, -0.3f, 0f).Chest(-24f).Head(16f).FootN(0.26f, 0.13f).FootF(-0.26f, 0.15f, -25f).HandN(0.52f, 0.82f).HandF(0.1f, 0.62f).Blade(-20f), Ease.OutExpo)
                .Key(9, P().Hip(0.04f, -0.3f, 0f).Chest(-24f).Head(16f).FootN(0.26f, 0.13f).FootF(-0.26f, 0.15f, -25f).HandN(0.5f, 0.82f).HandF(0.1f, 0.62f).Blade(-20f))
                .Key(16, Crouch(), Ease.InOutQuad)
                .Hit(5, 8, Fist, 2, HitLevel.Mid, new Vector2(0.7f, 0f), stun: 11, hitstop: 3, shake: 0.04f)
                .Fx(4, 8, FxKind.TrailFist).Cancel(8);

            // Reaper's Pull (the JS dash strike): the sickle hooks the foe and the chain drags him in. A hit whose
            // knockback points at the attacker: the foe is moved by the ordinary knockback and push boxes, and the move
            // ends with him close enough for the reap that chains out of it.
            m.pull = new MoveDef("chain.pull", "Reaper's Pull", 40)
                .Key(5, P().Hip(-0.04f, -0.12f, 6f).Chest(10f).Head(-4f).Feet(0.2f, -0.28f).HandF(-0.05f, 1.45f).Blade(120f).HandN(0.3f, 1.1f, 10f))
                .Key(9, Lunge(0.1f).HandF(0.55f, 1.12f).Blade(4f).HandN(0.28f, 1.05f, 10f), Ease.OutExpo)
                .Key(15, Lunge(0.1f).HandF(0.54f, 1.12f).Blade(4f).HandN(0.26f, 1.05f, 10f))
                .Key(24, P().Hip(0.02f, -0.1f, -4f).Chest(-8f).Head(6f).Feet(0.26f, -0.26f).HandF(0.22f, 1.0f).Blade(30f).HandN(-0.1f, 0.9f, 10f), Ease.InOutQuad)
                .Key(40, Stance(), Ease.InOutQuad)
                .Extend(5, 0f).Extend(9, PullExtension, Ease.OutExpo).Extend(15, PullExtension).Extend(26, 0f, Ease.InOutQuad)
                .Hit(8, 14, Blade, 4, HitLevel.Mid, new Vector2(PullKnockback, 0.2f), stun: 36, hitstop: 4, shake: 0.06f, radius: 0.08f)
                .Fx(7, 15, FxKind.TrailBlade).Sword().Cancel(16);

            // on a downed foe: the sickle driven down
            m.groundHook = new MoveDef("chain.groundHook", "Ground Hook", 22)
                .Key(5, P().Hip(0f, -0.1f, 0f).Chest(8f).LegN(80f, -110f).LegF(60f, -100f).HandF(0.14f, 1.55f).Blade(100f).HandN(0.2f, 1.5f, 10f))
                .Key(9, P().Hip(0.05f, -0.34f, 0f).Chest(-34f).Head(18f).FootN(0.26f, 0.13f).FootF(-0.24f, 0.15f, -25f).HandF(0.46f, 0.55f).Blade(-68f).HandN(0.2f, 0.8f, 10f), Ease.InCubic)
                .Key(14, P().Hip(0.05f, -0.36f, 0f).Chest(-34f).Head(18f).FootN(0.26f, 0.13f).FootF(-0.24f, 0.15f, -25f).HandF(0.46f, 0.52f).Blade(-68f).HandN(0.2f, 0.8f, 10f))
                .Key(22, Stance(), Ease.InOutQuad)
                .Extend(5, 0f).Extend(9, 0.2f, Ease.OutExpo).Extend(16, 0f)
                .Hit(8, 13, Blade, 4, HitLevel.Mid, new Vector2(0.3f, 0.6f), stun: 16, hitstop: 5, shake: 0.12f, otg: true, radius: 0.07f)
                .Fx(7, 13, FxKind.TrailBlade).Fx(10, 10, FxKind.Dust).Sword();

            // ===================== KICK BUTTON =====================
            m.kick1 = new MoveDef("chain.kick1", "Quick Kick", 18)
                .Key(4, P().Hip(0.02f, -0.07f, 12f).Chest(10f).Head(-4f).LegN(95f, -115f, 10f).FootF(-0.16f, 0.13f).HandN(0.3f, 1.2f).HandF(0.1f, 0.95f).Blade(60f))
                .Key(7, P().Hip(0.14f, -0.03f, 28f).Chest(24f).Head(-16f).LegN(116f, -6f, 12f).FootF(-0.12f, 0.13f).HandN(0.2f, 1.25f).HandF(-0.1f, 0.95f).Blade(60f), Ease.OutExpo)
                .Key(10, P().Hip(0.14f, -0.03f, 28f).Chest(24f).Head(-16f).LegN(114f, -8f, 12f).FootF(-0.12f, 0.13f).HandN(0.2f, 1.25f).HandF(-0.1f, 0.95f).Blade(60f))
                .Key(14, P().Hip(0.06f, -0.07f, 10f).Chest(6f).Head(-2f).LegN(90f, -110f).FootF(-0.18f, 0.13f).HandN(0.3f, 1.1f).HandF(0.1f, 0.9f).Blade(50f))
                .Key(18, Stance(), Ease.InOutQuad)
                .Hit(6, 9, FootN, 4, HitLevel.High, new Vector2(1.2f, 0.2f), stun: 12, hitstop: 3, shake: 0.06f, radius: 0.1f)
                .Fx(5, 10, FxKind.TrailFootN).Cancel(9);

            m.kick2 = new MoveDef("chain.kick2", "Switch Kick", 18)
                .Key(4, P().Hip(0.05f, -0.08f, 6f).Chest(4f).FootN(0.22f, 0.13f).LegF(100f, -120f).HandN(0.3f, 1.18f).HandF(0.15f, 0.9f).Blade(60f))
                .Key(7, P().Hip(0.15f, -0.04f, 14f).Chest(16f).Head(-10f).FootN(0.16f, 0.13f).LegF(118f, -8f, 10f).HandN(0.2f, 1.22f).HandF(0f, 0.92f).Blade(60f), Ease.OutExpo)
                .Key(10, P().Hip(0.15f, -0.04f, 14f).Chest(16f).Head(-10f).FootN(0.16f, 0.13f).LegF(116f, -10f, 10f).HandN(0.2f, 1.22f).HandF(0f, 0.92f).Blade(60f))
                .Key(18, Stance(), Ease.InOutQuad)
                .Hit(6, 9, FootF, 4, HitLevel.Mid, new Vector2(1.4f, 0.3f), stun: 12, hitstop: 3, shake: 0.06f, radius: 0.1f)
                .Fx(5, 10, FxKind.TrailFootF).Cancel(9);

            m.pushKick = new MoveDef("chain.pushKick", "Shove Kick", 20)
                .Key(4, P().Hip(0f, -0.06f, 8f).Chest(8f).LegN(100f, -120f, 15f).FootF(-0.2f, 0.13f).HandN(0.3f, 1.2f).HandF(0.15f, 0.95f).Blade(60f))
                .Key(7, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(96f, -4f, 40f).FootF(-0.14f, 0.13f).HandN(0.2f, 1.22f).HandF(0.05f, 0.92f).Blade(60f), Ease.OutExpo)
                .Key(10, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(95f, -5f, 40f).FootF(-0.14f, 0.13f).HandN(0.2f, 1.22f).HandF(0.05f, 0.92f).Blade(60f))
                .Key(20, Stance(), Ease.InOutQuad)
                .Hit(6, 10, FootTip, 4, HitLevel.Mid, new Vector2(3.4f, 0.5f), stun: 14, hitstop: 4, shake: 0.08f, radius: 0.11f)
                .Fx(5, 10, FxKind.TrailFootN);

            m.backKick = new MoveDef("chain.backKick", "Heel Kick", 24)
                .Key(5, P().Hip(0.02f, -0.1f, -10f).Chest(-14f).Head(8f).FootN(0.2f, 0.13f).LegF(104f, -124f).HandN(0.4f, 1.1f).HandF(0.2f, 1.0f).Blade(70f))
                .Key(8, P().Hip(0.16f, -0.04f, 16f).Chest(20f).Head(-12f).FootN(0.18f, 0.13f).LegF(122f, -6f, 12f).HandN(0.22f, 1.3f).HandF(0.0f, 1.0f).Blade(60f), Ease.OutExpo)
                .Key(12, P().Hip(0.16f, -0.04f, 16f).Chest(20f).Head(-12f).FootN(0.18f, 0.13f).LegF(120f, -8f, 12f).HandN(0.22f, 1.3f).HandF(0.0f, 1.0f).Blade(60f))
                .Key(24, Stance(), Ease.InOutQuad)
                .Hit(7, 11, FootF, 5, HitLevel.Mid, new Vector2(2.4f, 0.4f), stun: 16, hitstop: 4, shake: 0.1f, knockdown: true, radius: 0.1f)
                .Fx(6, 12, FxKind.TrailFootF);

            m.sweep = new MoveDef("chain.sweep", "Quick Sweep", 22)
                .Key(5, P().Hip(-0.04f, -0.42f, 0f).Chest(-30f).Head(16f).FootN(0.2f, 0.13f).FootF(-0.28f, 0.15f, -30f).HandN(0.3f, 0.7f).HandF(-0.1f, 0.6f).Blade(-60f))
                .Key(8, P().Hip(-0.04f, -0.44f, 6f).Chest(-34f).Head(18f).LegN(92f, -2f, 25f).FootF(-0.28f, 0.15f, -30f).HandN(0.3f, 0.7f).HandF(-0.16f, 0.5f).Blade(-60f), Ease.OutExpo)
                .Key(12, P().Hip(-0.04f, -0.44f, 6f).Chest(-34f).Head(18f).LegN(90f, -4f, 25f).FootF(-0.28f, 0.15f, -30f).HandN(0.3f, 0.7f).HandF(-0.16f, 0.5f).Blade(-60f))
                .Key(22, Stance(), Ease.InOutQuad)
                .Hit(6, 10, FootN, 4, HitLevel.Low, new Vector2(0.6f, 3.0f), stun: 14, hitstop: 4, shake: 0.08f, knockdown: true, otg: true, radius: 0.11f)
                .Fx(5, 11, FxKind.TrailFootN).Fx(8, 8, FxKind.Dust);

            // ===================== WEAPON BUTTON =====================
            // Reaping Cut (JS Blood Reap): a diagonal cut from high behind down across the front, the chain whipping a hand's length further
            m.reap = new MoveDef("chain.reap", "Reaping Cut", 26)
                .Key(5, P().Hip(-0.02f, -0.1f, 4f).Chest(10f).Head(-4f).Feet(0.22f, -0.26f).HandF(0f, 1.65f).Blade(120f).HandN(0.25f, 1.2f, 10f))
                .Key(8, P().Hip(0.1f, -0.12f, -12f).Chest(-24f).Head(12f).Feet(0.34f, -0.24f).HandF(0.56f, 0.92f).Blade(-42f).HandN(0.1f, 1.0f, 10f), Ease.OutExpo)
                .Key(12, P().Hip(0.1f, -0.12f, -12f).Chest(-24f).Head(12f).Feet(0.34f, -0.24f).HandF(0.55f, 0.9f).Blade(-46f).HandN(0.1f, 1.0f, 10f))
                .Key(26, Stance(), Ease.InOutQuad)
                .Extend(4, 0f).Extend(8, 0.35f, Ease.OutExpo).Extend(14, 0f, Ease.InOutQuad)
                .Hit(7, 11, Blade, 7, HitLevel.Mid, new Vector2(1.5f, 0.3f), stun: 17, hitstop: 5, shake: 0.1f, radius: 0.08f)
                .Fx(5, 12, FxKind.TrailBlade).Sword().Cancel(10);

            // Rising Reap (JS Blood Rise): the diagonal back up from low to high: a short launch
            m.reapUp = new MoveDef("chain.reapUp", "Rising Reap", 28)
                .Key(5, P().Hip(0f, -0.28f, -4f).Chest(-24f).Head(14f).FootN(0.28f, 0.13f).FootF(-0.26f, 0.15f, -25f).HandF(-0.05f, 0.55f).Blade(-145f).HandN(0.2f, 0.75f, 10f))
                .Key(9, P().Hip(0.1f, -0.06f, 2f).Chest(6f).Head(-6f).FootN(0.36f, 0.13f).FootF(-0.15f, 0.15f, -20f).HandF(0.5f, 1.5f).Blade(64f).HandN(0.05f, 1.1f, 10f), Ease.OutQuad)
                .Key(13, P().Hip(0.1f, -0.06f, 2f).Chest(8f).Head(-6f).FootN(0.34f, 0.13f).FootF(-0.17f, 0.15f, -20f).HandF(0.42f, 1.58f).Blade(84f).HandN(0.05f, 1.1f, 10f))
                .Key(28, Stance(), Ease.InOutQuad)
                .Extend(4, 0f).Extend(9, 0.4f, Ease.OutExpo).Extend(15, 0f, Ease.InOutQuad)
                .Move(2, 8, 1.4f)
                .Hit(6, 11, Blade, 7, HitLevel.Mid, new Vector2(0.5f, 5.2f), stun: 24, hitstop: 6, shake: 0.15f, launch: true, heavy: true, radius: 0.08f)
                .Fx(5, 13, FxKind.TrailBlade).Sword();

            // Chain Lash (JS Chain Lash): the sickle goes out on its chain, level across the front: the long reach of the set
            m.lash = new MoveDef("chain.lash", "Chain Lash", 34)
                .Key(4, P().Hip(-0.03f, -0.1f, 4f).Chest(8f).Head(-2f).Feet(0.22f, -0.26f).HandF(0f, 1.3f).Blade(110f).HandN(0.3f, 1.0f, 10f))
                .Key(8, Lunge().HandF(0.56f, 1.1f).Blade(2f).HandN(-0.05f, 0.95f, 10f), Ease.OutExpo)
                .Key(14, Lunge().HandF(0.54f, 1.1f).Blade(2f).HandN(-0.05f, 0.95f, 10f))
                .Key(24, P().Hip(0.04f, -0.1f, -6f).Chest(-10f).Head(6f).Feet(0.28f, -0.26f).HandF(0.3f, 1.0f).Blade(30f).HandN(0.0f, 0.9f, 10f), Ease.InOutQuad)
                .Key(34, Stance(), Ease.InOutQuad)
                .Extend(4, 0f).Extend(8, LashExtension, Ease.OutExpo).Extend(14, LashExtension).Extend(24, 0f, Ease.InOutQuad)
                .Move(6, 10, 1.2f)
                .Hit(7, 13, Blade, 6, HitLevel.Mid, new Vector2(2.2f, 0.3f), stun: 20, hitstop: 5, shake: 0.1f, radius: 0.07f)
                .Fx(6, 14, FxKind.TrailBlade).Sword().Cancel(14);

            // Grave Hook (JS Grave Hook): the sickle swept low along the floor and hooked back: a knockdown
            m.graveHook = new MoveDef("chain.graveHook", "Grave Hook", 34)
                .Key(5, P().Hip(-0.03f, -0.4f, 0f).Chest(-26f).Head(16f).FootN(0.24f, 0.13f).FootF(-0.28f, 0.15f, -30f).HandF(-0.05f, 0.5f).Blade(-170f).HandN(0.2f, 0.7f, 10f))
                .Key(9, Low().HandF(0.5f, 0.45f).Blade(-4f).HandN(0f, 0.7f, 10f), Ease.OutExpo)
                .Key(14, Low().HandF(0.5f, 0.45f).Blade(6f).HandN(0f, 0.7f, 10f))
                .Key(34, Stance(), Ease.InOutQuad)
                .Extend(5, 0f).Extend(9, 0.7f, Ease.OutExpo).Extend(15, 0.7f).Extend(22, 0f, Ease.InOutQuad)
                .Hit(8, 13, Blade, 7, HitLevel.Low, new Vector2(0.5f, 2.8f), stun: 20, hitstop: 5, shake: 0.12f, knockdown: true, otg: true, radius: 0.08f)
                .Fx(7, 14, FxKind.TrailBlade).Fx(10, 10, FxKind.Dust).Sword();

            // Crescent Lift (JS Crescent Lift): a rising cut from a step back: a launcher
            m.crescentLift = new MoveDef("chain.crescentLift", "Crescent Lift", 36)
                .Key(5, P().Hip(0.04f, -0.26f, -6f).Chest(-26f).Head(14f).Feet(0.3f, -0.22f).HandF(0.3f, 0.58f).Blade(-55f).HandN(0.1f, 0.75f, 10f))
                .Key(9, P().Hip(0.12f, -0.04f, 2f).Chest(8f).Head(-6f).FootN(0.36f, 0.13f).FootF(-0.15f, 0.15f, -20f).HandF(0.4f, 1.55f).Blade(96f).HandN(0f, 1.2f, 10f), Ease.OutQuad)
                .Key(13, P().Hip(0.1f, -0.04f, 2f).Chest(10f).Head(-6f).FootN(0.32f, 0.13f).FootF(-0.18f, 0.15f, -20f).HandF(0.28f, 1.62f).Blade(112f).HandN(0f, 1.2f, 10f))
                .Key(36, Stance(), Ease.InOutQuad)
                .Extend(5, 0f).Extend(9, 0.3f, Ease.OutExpo).Extend(14, 0f, Ease.InOutQuad)
                .Move(1, 7, 2.2f).JumpCancel(14)
                .Hit(5, 10, Blade, 8, HitLevel.Mid, new Vector2(0.4f, 7.0f), stun: 30, hitstop: 7, shake: 0.2f, launch: true, heavy: true, radius: 0.09f)
                .Fx(4, 13, FxKind.TrailBlade).Sword();

            // chains: jab -> hilt, reap; reap -> rising reap; the lash and the pull recover into a reap
            m.jab.chain[Btn.P] = m.hilt;
            m.jab.chain[Btn.S] = m.reap;
            m.jab.chain[Btn.K] = m.kick1;
            m.hilt.chain[Btn.S] = m.reapUp;
            m.hilt.chain[Btn.K] = m.kick2;
            m.crouchJab.chain[Btn.P] = m.hilt;
            m.kick1.chain[Btn.K] = m.kick2;
            m.kick1.chain[Btn.S] = m.reap;
            m.kick2.chain[Btn.S] = m.reapUp;
            m.reap.chain[Btn.S] = m.reapUp;
            m.reap.chain[Btn.P] = m.pull;
            m.pull.chain[Btn.S] = m.reap;
            m.lash.chain[Btn.S] = m.reap;

            // ===================== AIR =====================
            m.airJab = new MoveDef("chain.airJab", "Air Pommel", 16)
                .Key(3, P().Hip(0f, 0f, 0f).Chest(8f).LegN(60f, -90f).LegF(40f, -100f).HandN(0.15f, 1.7f).HandF(0.1f, 1.2f).Blade(60f))
                .Key(6, P().Hip(0f, 0f, 0f).Chest(-20f).Head(10f).LegN(60f, -90f).LegF(40f, -100f).HandN(0.52f, 1.1f).HandF(0.1f, 1.1f).Blade(60f), Ease.OutExpo)
                .Key(16, P().Hip(0f, 0f, 0f).Chest(-10f).Head(6f).LegN(50f, -70f).LegF(30f, -80f).HandN(0.4f, 1.1f).HandF(0.1f, 1.1f).Blade(60f))
                .Hit(5, 9, Fist, 3, HitLevel.Overhead, new Vector2(1.0f, 0.4f), stun: 12, hitstop: 3, shake: 0.05f)
                .Fx(4, 9, FxKind.TrailFist).Air();

            m.airKick = new MoveDef("chain.airKick", "Swoop Kick", 28)
                .Key(4, P().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(70f, -4f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).HandF(-0.15f, 1.1f).Blade(100f), Ease.OutExpo)
                .Key(28, P().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(68f, -6f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).HandF(-0.15f, 1.1f).Blade(100f))
                .Hit(3, 18, FootN, 5, HitLevel.Overhead, new Vector2(2.0f, 1.2f), stun: 16, hitstop: 4, shake: 0.1f, radius: 0.11f)
                .Fx(3, 12, FxKind.TrailFootN).Air();

            // Moon Hook (JS Moon Hook): a diagonal cut down from above, the chain whipping out
            m.airReap = new MoveDef("chain.airReap", "Moon Hook", 30)
                .Key(3, P().Hip(0f, 0f, 0f).Chest(8f).LegN(60f, -90f).LegF(40f, -100f).HandF(0.1f, 1.65f).Blade(115f).HandN(0.3f, 1.3f, 10f))
                .Key(7, P().Hip(0f, 0f, 0f).Chest(-16f).Head(8f).LegN(60f, -90f).LegF(40f, -100f).HandF(0.55f, 1.1f).Blade(-35f).HandN(0.1f, 1.2f, 10f), Ease.OutExpo)
                .Key(30, P().Hip(0f, 0f, 0f).Chest(-10f).Head(6f).LegN(50f, -70f).LegF(30f, -80f).HandF(0.5f, 1.1f).Blade(-30f).HandN(0.1f, 1.2f, 10f))
                .Extend(3, 0f).Extend(7, 0.5f, Ease.OutExpo).Extend(14, 0f, Ease.InOutQuad)
                .Hit(6, 12, Blade, 5, HitLevel.Overhead, new Vector2(1.4f, 0.8f), stun: 16, hitstop: 4, shake: 0.08f, radius: 0.08f)
                .Fx(5, 12, FxKind.TrailBlade).Sword().Air();

            // Shadow Fall (JS Shadow Fall): a straight dive with the sickle driven down on its chain
            m.shadowFall = new MoveDef("chain.shadowFall", "Shadow Fall", 32)
                .Key(3, P().Hip(0f, 0f, 0f).Chest(10f).LegN(60f, -90f).LegF(40f, -100f).HandF(0.25f, 0.85f).Blade(-92f).HandN(0.1f, 1.0f, 10f), Ease.OutExpo)
                .Key(32, P().Hip(0f, 0f, 0f).Chest(8f).LegN(58f, -88f).LegF(38f, -98f).HandF(0.27f, 0.85f).Blade(-92f).HandN(0.1f, 1.0f, 10f))
                .Extend(2, 0f).Extend(5, 0.5f, Ease.OutExpo).Extend(30, 0.5f)
                .Move(2, 2, 1.0f, -12.0f, true)
                .Hit(3, 28, Blade, 6, HitLevel.Overhead, new Vector2(0.6f, -2.0f), stun: 18, hitstop: 5, shake: 0.14f, knockdown: true, heavy: true, radius: 0.08f)
                .Fx(2, 28, FxKind.TrailBlade).Sword().Air();

            return m;
        }

        /// <summary>How far the lash runs the sickle out on its chain, metres past its resting blade (x the weapon's reach).</summary>
        public const float LashExtension = 0.85f;
        /// <summary>Reaper's Pull throws the chain a little further than the lash.</summary>
        public const float PullExtension = 0.95f;
        /// <summary>The pull's knockback x: negative, towards the attacker. A foe of mass 1 gets this as his velocity, so he slides about 1.3 m before the hitstun friction stops him.</summary>
        public const float PullKnockback = -5.4f;

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
                    case Btn.S: return dy < 0 ? shadowFall : airReap;
                }
                return null;
            }
            switch (b)
            {
                case Btn.P:
                    if (dy < 0) return oppDown && dist < 1.1f ? groundHook : crouchJab;
                    if (fwd > 0) return dist < 0.85f && opp != null && opp.CanBeThrown ? throwGrab : pull;
                    return jab;
                case Btn.K:
                    if (dy < 0) return sweep;
                    if (fwd > 0) return pushKick;
                    if (fwd < 0) return backKick;
                    return kick1;
                case Btn.S:
                    if (dy < 0) return graveHook;
                    if (fwd > 0) return lash;
                    if (fwd < 0) return crescentLift;
                    return reap;
            }
            return null;
        }

        public override IEnumerable<MoveDef> All => new[]
        {
            jab, hilt, crouchJab, pull, groundHook, kick1, kick2, pushKick, backKick, sweep,
            reap, reapUp, lash, graveHook, crescentLift,
            airJab, airKick, airReap, shadowFall,
            throwGrab, throwWhiff, roll, backflip, getUp, getUpFront,
        };
    }
}
