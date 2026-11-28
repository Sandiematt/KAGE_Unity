using System.Collections.Generic;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// The chain sickle (kusarigama) family (ADR-0006, SAN-87): Yamigama, and the Order Chainthrower's chain hook.
    /// Asymmetric dual-range assassin weapon: lead hand (<see cref="B.HandF"/>) wields the curved sickle (<c>kama</c>)
    /// for razor-fast close hooks while off-hand (<see cref="B.HandN"/>) whips the dynamic multi-segment iron chain and
    /// weighted end (<c>fundo</c>) along curved-to-taut mid-range trajectories (<see cref="MoveDef.Chain"/>).
    /// <para>
    /// Core 7 attacks:
    /// 1. Light 1 — Reaping Hook (<c>chain.reap</c>, S): tight inward-to-outward close horizontal sickle slash at chest height.
    /// 2. Light 2 — Iron Whip Lash (<c>chain.lash</c>, S->S / Fwd+S): off-hand snaps the weighted chain taut at mid-range, then snaps back cleanly.
    /// 3. Light 3 — Reaper's Pull &amp; Gut-Rend (<c>chain.pull</c>, S->S->S / Fwd+P): overhand curving chain hook pulls inward into a close upward-diagonal reverse sickle rip.
    /// 4. Heavy / Charged — Cyclone Chain Arc (<c>chain.cyclone</c>, Back+S): readable overhead/side chain twirl loop (torso stays facing forward) erupting into a massive wide sweeping chain arc.
    /// 5. Low Attack — Ankle Snare Lash (<c>chain.graveHook</c>, Down+S): drops low and whips the weighted chain horizontally just above floor level.
    /// 6. Up / Anti-Air — Crescent Snatch (<c>chain.crescentLift</c>, Up+S): sweeps the sickle upward close while snapping the chain diagonally upward at 55 degrees.
    /// 7. Dash Attack — Shadow Hook Lunge (<c>chain.dashHook</c>, Dash+S / Dash+P): low forward rush ending in a vicious downward hooking drag with the sickle.
    /// </para>
    /// </summary>
    public sealed class ChainMoves : MoveSet
    {
        public MoveDef jab, hilt, crouchJab, pull, groundHook;
        public MoveDef kick1, kick2, pushKick, backKick, sweep;
        public MoveDef reap, reapUp, lash, cyclone, graveHook, crescentLift, dashHook;
        public MoveDef airJab, airKick, airReap, shadowFall;
        /// <summary>Back + S: the chain sickle's counter stance (Reaper's Snare) and the answer it reaps back with (Reaper's Reply).</summary>
        public MoveDef counterGuard, counterReap;

        /// <summary>Metres from the far hand's guard point to the sickle's tip.</summary>
        public const float BladeLength = 0.5f;

        /// <summary>Mid-range weighted-chain extension from off-hand on Iron Whip Lash (x weapon reach).</summary>
        public const float LashExtension = 1.48f;
        /// <summary>Overhand hook extension on Reaper's Pull &amp; Gut-Rend (reaches slightly beyond Lash, stays below Spear).</summary>
        public const float PullExtension = 1.56f;
        /// <summary>Inward knockback x on the hook window: negative pulls target cleanly through physics knockback.</summary>
        public const float PullKnockback = -5.4f;

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

        /// <summary>Yamigama's asymmetric assassin guard (matches Fighter.WeaponStance).</summary>
        static Pose Guard() =>
            P().Hip(0.03f, -0.11f, -5f).Chest(-14f).Head(10f).Feet(0.33f, -0.30f)
                .HandF(0.34f, 0.98f).Blade(32f).HandN(0.02f, 0.94f, -10f);

        static Pose Lunge(float hx = 0.12f, float hy = -0.13f, float hipRot = -12f, float chest = -24f) =>
            P().Hip(hx, hy, hipRot).Chest(chest).Head(12f).Feet(0.42f, -0.26f);

        static Pose Low(float hx = 0.08f, float hy = -0.40f) =>
            P().Hip(hx, hy, -8f).Chest(-32f).Head(18f).FootN(0.34f, 0.13f).FootF(-0.30f, 0.15f, -28f);

        ChainMoves(KatanaMoves katana) { ShareBodyMoves(katana); }

        public static ChainMoves Build(KatanaMoves katana)
        {
            var m = new ChainMoves(katana);

            // ===================== PUNCH BUTTON =====================
            m.jab = new MoveDef("chain.jab", "Chain Jab", 16)
                .Key(4, Guard().Chest(-10f).HandN(0.14f, 1.16f, 10f), Ease.OutQuad)
                .Key(6, P().Hip(0.08f, -0.09f, -6f).Chest(-22f).Head(12f).Feet(0.34f, -0.24f).HandN(0.52f, 1.20f).HandF(0.22f, 0.88f).Blade(-32f), Ease.OutExpo)
                .Key(9, P().Hip(0.08f, -0.09f, -6f).Chest(-22f).Head(12f).Feet(0.34f, -0.24f).HandN(0.50f, 1.20f).HandF(0.22f, 0.88f).Blade(-32f))
                .Key(16, Guard(), Ease.InOutQuad)
                .Hit(5, 8, Fist, 3, HitLevel.High, new Vector2(0.9f, 0f), stun: 13, hitstop: 4, shake: 0.05f)
                .Vector(StrikeVector.Thrust)
                .Fx(4, 8, FxKind.TrailFist).Cancel(8);

            m.hilt = new MoveDef("chain.hilt", "Hilt Strike", 20)
                .Key(5, Guard().Chest(-4f).HandF(0.06f, 1.02f).Blade(168f).HandN(0.18f, 0.92f, 10f), Ease.OutQuad)
                .Key(8, P().Hip(0.10f, -0.10f, -8f).Chest(-24f).Head(14f).Feet(0.36f, -0.22f).HandN(0.14f, 0.92f, 10f).HandF(0.54f, 1.16f).Blade(176f), Ease.OutExpo)
                .Key(12, P().Hip(0.10f, -0.10f, -8f).Chest(-24f).Head(14f).Feet(0.36f, -0.22f).HandN(0.14f, 0.92f, 10f).HandF(0.52f, 1.16f).Blade(176f))
                .Key(20, Guard(), Ease.InOutQuad)
                .Hit(7, 10, Hilt, 4, HitLevel.High, new Vector2(1.3f, 0f), stun: 15, hitstop: 4, shake: 0.07f)
                .Vector(StrikeVector.Thrust)
                .Sword().Cancel(10);

            m.crouchJab = new MoveDef("chain.crouchJab", "Low Jab", 16)
                .Key(4, Crouch().HandN(0.15f, 0.90f).HandF(0.14f, 0.66f).Blade(-16f), Ease.OutQuad)
                .Key(6, P().Hip(0.05f, -0.30f, -2f).Chest(-24f).Head(16f).FootN(0.28f, 0.13f).FootF(-0.26f, 0.15f, -25f).HandN(0.50f, 0.82f).HandF(0.12f, 0.64f).Blade(-20f), Ease.OutExpo)
                .Key(9, P().Hip(0.05f, -0.30f, -2f).Chest(-24f).Head(16f).FootN(0.28f, 0.13f).FootF(-0.26f, 0.15f, -25f).HandN(0.48f, 0.82f).HandF(0.12f, 0.64f).Blade(-20f))
                .Key(16, Crouch(), Ease.InOutQuad)
                .Hit(5, 8, Fist, 2, HitLevel.Mid, new Vector2(0.7f, 0f), stun: 11, hitstop: 3, shake: 0.04f)
                .Vector(StrikeVector.Thrust)
                .Fx(4, 8, FxKind.TrailFist).Cancel(8);

            // 3. Light 3 — Reaper's Pull & Gut-Rend (Mid-to-Close Finisher):
            //    Phase 1: Overhand curving chain arc hooks/pulls target sharply inward while stepping forward.
            //    Phase 2: Close-range upward-diagonal reverse sickle rip as chain snaps cleanly back.
            m.pull = new MoveDef("chain.pull", "Reaper's Pull & Gut-Rend", 42)
                .Key(5, P().Hip(-0.05f, -0.10f, 6f).Chest(12f).Head(-6f).Feet(0.24f, -0.30f).HandN(-0.08f, 1.54f, 18f).HandF(0.26f, 0.92f).Blade(-18f), Ease.OutQuad)
                .Key(9, Lunge(0.12f, -0.13f, -12f, -24f).HandN(0.50f, 1.14f, 4f).HandF(0.18f, 0.84f).Blade(-38f), Ease.OutExpo)
                .Key(13, Lunge(0.12f, -0.13f, -12f, -24f).HandN(0.48f, 1.12f, 0f).HandF(0.18f, 0.82f).Blade(-42f))
                .Key(18, P().Hip(0.06f, -0.22f, -6f).Chest(-22f).Head(12f).Feet(0.38f, -0.26f).HandN(-0.14f, 0.96f, -16f).HandF(0.18f, 0.66f).Blade(-112f).EdgeUp(), Ease.OutQuad)
                .Key(22, P().Hip(0.14f, -0.08f, 4f).Chest(6f).Head(-4f).Feet(0.42f, -0.24f).HandN(-0.18f, 0.98f, -18f).HandF(0.50f, 1.46f).Blade(58f).EdgeUp(), Ease.OutExpo)
                .Key(27, P().Hip(0.12f, -0.08f, 4f).Chest(8f).Head(-4f).Feet(0.40f, -0.24f).HandN(-0.14f, 0.98f, -14f).HandF(0.46f, 1.50f).Blade(68f).EdgeUp())
                .Key(42, Guard(), Ease.InOutQuad)
                // Curved overhand cast -> taut hook -> snap back to off-hand
                .Chain(4, 0.56f, 118f, bow: 0.30f, e: Ease.OutQuad)
                .Chain(9, PullExtension, 4f, bow: 0.02f, e: Ease.OutExpo)
                .Chain(13, PullExtension, -4f, bow: 0f)
                .Chain(18, 0.30f, -16f, bow: -0.18f, e: Ease.InQuad)
                .Chain(22, 0f, -10f, bow: 0f, e: Ease.OutQuad)
                .Move(14, 19, 1.8f)
                .Hit(8, 13, Blade, 4, HitLevel.Mid, new Vector2(PullKnockback, 0.2f), stun: 34, hitstop: 4, shake: 0.08f, radius: 0.09f)
                .ChainWhip(0.55f).Vector(StrikeVector.HighOverhead)
                .Hit(20, 24, Blade, 7, HitLevel.Mid, new Vector2(0.5f, 0.3f), stun: 22, hitstop: 6, shake: 0.15f, radius: 0.09f)
                .Vector(StrikeVector.Upward)
                .Gust(8, new Vector2(9.0f, -2.0f), 0.14f)
                .Gust(15, new Vector2(-8.0f, 2.2f), 0.12f)
                .Gust(21, new Vector2(8.0f, 6.0f), 0.15f)
                .Fx(7, 14, FxKind.TrailBlade).Fx(19, 25, FxKind.TrailBlade).Sword().Cancel(25);

            m.groundHook = new MoveDef("chain.groundHook", "Ground Hook", 24)
                .Key(5, P().Hip(-0.02f, -0.10f, 4f).Chest(8f).Feet(0.28f, -0.28f).HandF(0.14f, 1.50f).Blade(102f).HandN(0.10f, 1.24f, 10f), Ease.OutQuad)
                .Key(9, Low(0.06f, -0.34f).HandF(0.48f, 0.68f).Blade(-56f).HandN(-0.04f, 0.82f, -10f), Ease.InCubic)
                .Key(14, Low(0.06f, -0.34f).HandF(0.48f, 0.66f).Blade(-56f).HandN(-0.04f, 0.82f, -10f))
                .Key(24, Guard(), Ease.InOutQuad)
                .Hit(8, 13, Blade, 5, HitLevel.Mid, new Vector2(0.3f, 0.6f), stun: 16, hitstop: 5, shake: 0.12f, otg: true, radius: 0.08f)
                .Vector(StrikeVector.HighOverhead)
                .Gust(9, new Vector2(5.0f, -6.5f), 0.13f)
                .Fx(7, 13, FxKind.TrailBlade).Fx(10, 10, FxKind.Dust).Sword();

            // ===================== KICK BUTTON =====================
            m.kick1 = new MoveDef("chain.kick1", "Quick Kick", 18)
                .Key(4, P().Hip(0.02f, -0.07f, 12f).Chest(10f).Head(-4f).LegN(95f, -115f, 10f).FootF(-0.16f, 0.13f).HandN(0.24f, 1.18f).HandF(0.10f, 0.95f).Blade(56f))
                .Key(7, P().Hip(0.14f, -0.03f, 24f).Chest(20f).Head(-14f).LegN(116f, -6f, 12f).FootF(-0.12f, 0.13f).HandN(0.14f, 1.22f).HandF(-0.06f, 0.95f).Blade(56f), Ease.OutExpo)
                .Key(10, P().Hip(0.14f, -0.03f, 24f).Chest(20f).Head(-14f).LegN(114f, -8f, 12f).FootF(-0.12f, 0.13f).HandN(0.14f, 1.22f).HandF(-0.06f, 0.95f).Blade(56f))
                .Key(14, P().Hip(0.06f, -0.07f, 10f).Chest(6f).Head(-2f).LegN(90f, -110f).FootF(-0.18f, 0.13f).HandN(0.24f, 1.10f).HandF(0.10f, 0.90f).Blade(48f))
                .Key(18, Guard(), Ease.InOutQuad)
                .Hit(6, 9, FootN, 4, HitLevel.High, new Vector2(1.2f, 0.2f), stun: 12, hitstop: 3, shake: 0.06f, radius: 0.1f)
                .Vector(StrikeVector.Mid)
                .Fx(5, 10, FxKind.TrailFootN).Cancel(9);

            m.kick2 = new MoveDef("chain.kick2", "Switch Kick", 18)
                .Key(4, P().Hip(0.05f, -0.08f, 6f).Chest(4f).FootN(0.22f, 0.13f).LegF(100f, -120f).HandN(0.24f, 1.18f).HandF(0.15f, 0.90f).Blade(56f))
                .Key(7, P().Hip(0.15f, -0.04f, 14f).Chest(16f).Head(-10f).FootN(0.16f, 0.13f).LegF(118f, -8f, 10f).HandN(0.16f, 1.22f).HandF(0f, 0.92f).Blade(56f), Ease.OutExpo)
                .Key(10, P().Hip(0.15f, -0.04f, 14f).Chest(16f).Head(-10f).FootN(0.16f, 0.13f).LegF(116f, -10f, 10f).HandN(0.16f, 1.22f).HandF(0f, 0.92f).Blade(56f))
                .Key(18, Guard(), Ease.InOutQuad)
                .Hit(6, 9, FootF, 4, HitLevel.Mid, new Vector2(1.4f, 0.3f), stun: 12, hitstop: 3, shake: 0.06f, radius: 0.1f)
                .Vector(StrikeVector.Mid)
                .Fx(5, 10, FxKind.TrailFootF).Cancel(9);

            m.pushKick = new MoveDef("chain.pushKick", "Shove Kick", 20)
                .Key(4, P().Hip(0f, -0.06f, 8f).Chest(8f).LegN(100f, -120f, 15f).FootF(-0.2f, 0.13f).HandN(0.26f, 1.18f).HandF(0.15f, 0.95f).Blade(56f))
                .Key(7, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(96f, -4f, 40f).FootF(-0.14f, 0.13f).HandN(0.16f, 1.22f).HandF(0.05f, 0.92f).Blade(56f), Ease.OutExpo)
                .Key(10, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(95f, -5f, 40f).FootF(-0.14f, 0.13f).HandN(0.16f, 1.22f).HandF(0.05f, 0.92f).Blade(56f))
                .Key(20, Guard(), Ease.InOutQuad)
                .Hit(6, 10, FootTip, 4, HitLevel.Mid, new Vector2(3.4f, 0.5f), stun: 14, hitstop: 4, shake: 0.08f, radius: 0.11f)
                .Vector(StrikeVector.Thrust)
                .Fx(5, 10, FxKind.TrailFootN);

            m.backKick = new MoveDef("chain.backKick", "Heel Kick", 24)
                .Key(5, P().Hip(0.02f, -0.1f, -10f).Chest(-14f).Head(8f).FootN(0.2f, 0.13f).LegF(104f, -124f).HandN(0.32f, 1.1f).HandF(0.2f, 1.0f).Blade(64f))
                .Key(8, P().Hip(0.16f, -0.04f, 16f).Chest(20f).Head(-12f).FootN(0.18f, 0.13f).LegF(122f, -6f, 12f).HandN(0.18f, 1.28f).HandF(0.0f, 1.0f).Blade(56f), Ease.OutExpo)
                .Key(12, P().Hip(0.16f, -0.04f, 16f).Chest(20f).Head(-12f).FootN(0.18f, 0.13f).LegF(120f, -8f, 12f).HandN(0.18f, 1.28f).HandF(0.0f, 1.0f).Blade(56f))
                .Key(24, Guard(), Ease.InOutQuad)
                .Hit(7, 11, FootF, 5, HitLevel.Mid, new Vector2(2.4f, 0.4f), stun: 16, hitstop: 4, shake: 0.1f, knockdown: true, radius: 0.1f)
                .Vector(StrikeVector.Mid)
                .Fx(6, 12, FxKind.TrailFootF);

            m.sweep = new MoveDef("chain.sweep", "Quick Sweep", 22)
                .Key(5, P().Hip(-0.04f, -0.42f, 0f).Chest(-30f).Head(16f).FootN(0.2f, 0.13f).FootF(-0.28f, 0.15f, -30f).HandN(0.26f, 0.7f).HandF(-0.08f, 0.6f).Blade(-55f))
                .Key(8, P().Hip(-0.04f, -0.44f, 6f).Chest(-34f).Head(18f).LegN(92f, -2f, 25f).FootF(-0.28f, 0.15f, -30f).HandN(0.26f, 0.7f).HandF(-0.14f, 0.54f).Blade(-55f), Ease.OutExpo)
                .Key(12, P().Hip(-0.04f, -0.44f, 6f).Chest(-34f).Head(18f).LegN(90f, -4f, 25f).FootF(-0.28f, 0.15f, -30f).HandN(0.26f, 0.7f).HandF(-0.14f, 0.54f).Blade(-55f))
                .Key(22, Guard(), Ease.InOutQuad)
                .Hit(6, 10, FootN, 4, HitLevel.Low, new Vector2(0.6f, 3.0f), stun: 14, hitstop: 4, shake: 0.08f, knockdown: true, otg: true, radius: 0.11f)
                .Vector(StrikeVector.Low)
                .Fx(5, 11, FxKind.TrailFootN).Fx(8, 8, FxKind.Dust);

            // ===================== WEAPON BUTTON: YAMIGAMA'S 7 CORE TECHNIQUES =====================
            // 1. Light 1 — Reaping Hook (Close Horizontal Slash): razor-fast inward-to-outward sickle slash at chest height
            m.reap = new MoveDef("chain.reap", "Reaping Hook", 24)
                .Key(5, P().Hip(-0.04f, -0.13f, 6f).Chest(10f).Head(-4f).Feet(0.26f, -0.28f).HandF(0.08f, 1.18f).Blade(144f).HandN(0.22f, 0.98f, 10f), Ease.OutQuad)
                .Key(8, Lunge(0.12f, -0.14f, -12f, -24f).HandF(0.54f, 1.06f).Blade(-12f).HandN(-0.06f, 0.96f, -12f), Ease.OutExpo)
                .Key(12, Lunge(0.12f, -0.14f, -12f, -24f).HandF(0.52f, 1.04f).Blade(-22f).HandN(-0.08f, 0.96f, -12f))
                .Key(17, P().Hip(0.06f, -0.12f, -6f).Chest(-16f).Head(10f).Feet(0.36f, -0.28f).HandF(0.42f, 0.96f).Blade(8f).HandN(-0.02f, 0.94f, -10f), Ease.OutQuad)
                .Key(24, Guard(), Ease.InOutQuad)
                .Move(5, 8, 1.8f)
                .Hit(6, 10, Blade, 7, HitLevel.Mid, new Vector2(1.4f, 0.2f), stun: 17, hitstop: 5, shake: 0.10f, radius: 0.085f)
                .Vector(StrikeVector.Mid)
                .Gust(6, new Vector2(7.5f, 1.0f), 0.13f)
                .Fx(5, 11, FxKind.TrailBlade).Sword().Cancel(10);

            // Rising Reap: fast close-range reverse sickle launcher from Punch/Kick follow-ups
            m.reapUp = new MoveDef("chain.reapUp", "Rising Reap", 28)
                .Key(5, Low(0.0f, -0.28f).HandF(0.02f, 0.60f).Blade(-132f).EdgeUp().HandN(0.16f, 0.80f, 8f), Ease.OutQuad)
                .Key(9, P().Hip(0.10f, -0.06f, 4f).Chest(8f).Head(-6f).FootN(0.36f, 0.13f).FootF(-0.18f, 0.15f, -18f).HandF(0.48f, 1.48f).Blade(64f).EdgeUp().HandN(-0.06f, 1.06f, -12f), Ease.OutExpo)
                .Key(14, P().Hip(0.10f, -0.06f, 4f).Chest(10f).Head(-6f).FootN(0.34f, 0.13f).FootF(-0.20f, 0.15f, -18f).HandF(0.42f, 1.54f).Blade(80f).EdgeUp().HandN(-0.06f, 1.06f, -12f))
                .Key(28, Guard(), Ease.InOutQuad)
                .Move(4, 9, 1.4f)
                .Hit(7, 11, Blade, 7, HitLevel.Mid, new Vector2(0.5f, 5.4f), stun: 24, hitstop: 6, shake: 0.15f, launch: true, heavy: true, radius: 0.085f)
                .Vector(StrikeVector.Upward)
                .Gust(7, new Vector2(6.5f, 6.5f), 0.14f)
                .Fx(6, 13, FxKind.TrailBlade).Sword();

            // 2. Light 2 — Iron Whip Lash (Mid-Range Chain Snap): off-hand coils curved chain then snaps weighted end dead-taut at mid-range
            m.lash = new MoveDef("chain.lash", "Iron Whip Lash", 34)
                .Key(5, P().Hip(-0.05f, -0.12f, 6f).Chest(10f).Head(-4f).Feet(0.24f, -0.30f).HandN(-0.18f, 1.18f, 14f).HandF(0.28f, 0.90f).Blade(-14f), Ease.OutQuad)
                .Key(8, Lunge(0.14f, -0.13f, -12f, -24f).HandN(0.48f, 1.12f, 0f).HandF(0.16f, 0.88f).Blade(-36f), Ease.OutExpo)
                .Key(14, Lunge(0.14f, -0.13f, -12f, -24f).HandN(0.46f, 1.10f, 0f).HandF(0.16f, 0.88f).Blade(-36f))
                .Key(23, P().Hip(0.04f, -0.11f, -4f).Chest(-10f).Head(6f).Feet(0.32f, -0.28f).HandN(-0.10f, 0.98f, -12f).HandF(0.30f, 0.96f).Blade(20f), Ease.InOutQuad)
                .Key(34, Guard(), Ease.InOutQuad)
                // Dynamic chain: curved windup -> dead-taut whip snap -> ripple recovery
                .Chain(4, 0.44f, 148f, bow: 0.26f, e: Ease.OutQuad)
                .Chain(8, LashExtension, 4f, bow: 0f, e: Ease.OutExpo)
                .Chain(14, LashExtension, -2f, bow: 0f)
                .Chain(20, 0.34f, 16f, bow: -0.18f, e: Ease.InQuad)
                .Chain(26, 0f, 8f, bow: 0f, e: Ease.OutQuad)
                .Move(6, 10, 1.2f)
                .Hit(7, 13, Blade, 6, HitLevel.Mid, new Vector2(2.2f, 0.3f), stun: 20, hitstop: 5, shake: 0.11f, radius: 0.08f)
                .ChainWhip(0.55f).Vector(StrikeVector.Mid)
                .Gust(7, new Vector2(10.5f, 1.2f), 0.15f)
                .Fx(6, 14, FxKind.TrailBlade).Sword().Cancel(14);

            // 4. Heavy / Charged — Cyclone Chain Arc (Wide Mid-Range Sweep):
            //    Readable overhead/side twirl telegraph loop (torso stays facing forward - NO full-body spin!),
            //    then unleashes a massive high-velocity wide sweeping chain arc with long reach and heavy stagger.
            m.cyclone = new MoveDef("chain.cyclone", "Cyclone Chain Arc", 48)
                .Key(6, P().Hip(-0.04f, -0.12f, 4f).Chest(6f).Head(-2f).Feet(0.28f, -0.32f).HandN(0.06f, 1.46f, 20f).HandF(0.28f, 0.88f).Blade(-20f), Ease.OutQuad)
                .Key(12, P().Hip(-0.06f, -0.14f, 6f).Chest(10f).Head(-4f).Feet(0.28f, -0.32f).HandN(-0.02f, 1.50f, 24f).HandF(0.26f, 0.86f).Blade(-24f), Ease.InOutQuad)
                .Key(16, P().Hip(-0.08f, -0.16f, 8f).Chest(12f).Head(-6f).Feet(0.30f, -0.32f).HandN(-0.16f, 1.26f, 16f).HandF(0.24f, 0.86f).Blade(-28f), Ease.OutQuad)
                .Key(20, Lunge(0.16f, -0.16f, -14f, -26f).HandN(0.50f, 1.12f, 0f).HandF(0.12f, 0.86f).Blade(-44f), Ease.OutExpo)
                .Key(26, Lunge(0.16f, -0.16f, -14f, -26f).HandN(0.46f, 1.02f, -6f).HandF(0.12f, 0.86f).Blade(-46f), Ease.OutQuad)
                .Key(36, P().Hip(0.04f, -0.12f, -4f).Chest(-10f).Head(8f).Feet(0.34f, -0.30f).HandN(-0.10f, 0.98f, -12f).HandF(0.30f, 0.94f).Blade(18f), Ease.InOutQuad)
                .Key(48, Guard(), Ease.InOutQuad)
                // Overhead/side twirl telegraph (spinDeg orbits fundo around HandN while torso faces strictly forward), then huge taut arc
                .Chain(5, 0.48f, 90f, bow: 0.16f, spinDeg: 180f, e: Ease.Linear)
                .Chain(11, 0.54f, 90f, bow: 0.18f, spinDeg: 450f, e: Ease.Linear)
                .Chain(15, 0.62f, 146f, bow: 0.24f, spinDeg: 540f, e: Ease.OutQuad)
                .Chain(19, 1.60f, 20f, bow: 0.04f, spinDeg: 0f, e: Ease.OutExpo)
                .Chain(25, 1.60f, -14f, bow: 0f, spinDeg: 0f, e: Ease.OutQuad)
                .Chain(33, 0.34f, -24f, bow: -0.20f, spinDeg: 0f, e: Ease.InQuad)
                .Chain(38, 0f, 0f, bow: 0f, spinDeg: 0f, e: Ease.OutQuad)
                .Move(16, 22, 2.4f)
                .Hit(18, 25, Blade, 12, HitLevel.Mid, new Vector2(3.8f, 1.6f), stun: 30, hitstop: 9, shake: 0.28f, knockdown: true, heavy: true, radius: 0.10f, postureMul: 1.55f)
                .ChainWhip(0.48f).Vector(StrikeVector.Mid)
                .Gust(18, new Vector2(12.0f, 1.8f), 0.20f)
                .Fx(16, 26, FxKind.TrailBlade).Fx(17, 17, FxKind.Dust).Sword();

            // 5. Low Attack — Ankle Snare Lash: drops low and whips weighted chain horizontally just above floor level
            m.graveHook = new MoveDef("chain.graveHook", "Ankle Snare Lash", 34)
                .Key(5, P().Hip(-0.04f, -0.38f, 0f).Chest(-24f).Head(16f).FootN(0.24f, 0.13f).FootF(-0.30f, 0.15f, -30f).HandN(-0.14f, 0.60f, 12f).HandF(0.22f, 0.64f).Blade(-18f), Ease.OutQuad)
                .Key(9, Low(0.08f, -0.40f).HandN(0.44f, 0.44f, 0f).HandF(0.14f, 0.62f).Blade(-36f), Ease.OutExpo)
                .Key(15, Low(0.08f, -0.40f).HandN(0.42f, 0.44f, 0f).HandF(0.14f, 0.62f).Blade(-36f))
                .Key(24, P().Hip(0.02f, -0.24f, -4f).Chest(-18f).Head(12f).Feet(0.32f, -0.28f).HandN(-0.04f, 0.78f, -10f).HandF(0.28f, 0.84f).Blade(14f), Ease.InOutQuad)
                .Key(34, Guard(), Ease.InOutQuad)
                .Chain(5, 0.42f, -162f, bow: -0.18f, e: Ease.OutQuad)
                .Chain(9, 1.38f, -6f, bow: 0f, e: Ease.OutExpo)
                .Chain(15, 1.38f, -2f, bow: 0f)
                .Chain(22, 0.26f, -12f, bow: 0.14f, e: Ease.InQuad)
                .Chain(26, 0f, 0f, bow: 0f, e: Ease.OutQuad)
                .Hit(8, 14, Blade, 7, HitLevel.Low, new Vector2(0.8f, 3.0f), stun: 20, hitstop: 5, shake: 0.14f, knockdown: true, otg: true, radius: 0.085f)
                .ChainWhip(0.55f).Vector(StrikeVector.Low)
                .Gust(8, new Vector2(9.5f, -2.5f), 0.16f)
                .Fx(7, 15, FxKind.TrailBlade).Fx(10, 10, FxKind.Dust).Sword();

            // 6. Up / Anti-Air — Crescent Snatch: sweeps sickle upward close while snapping chain diagonally upward at 55 degrees
            m.crescentLift = new MoveDef("chain.crescentLift", "Crescent Snatch", 36)
                .Key(5, P().Hip(0.02f, -0.26f, -4f).Chest(-24f).Head(14f).Feet(0.30f, -0.26f).HandF(0.24f, 0.60f).Blade(-62f).EdgeUp().HandN(-0.08f, 0.66f, 14f), Ease.OutQuad)
                .Key(9, P().Hip(0.10f, -0.04f, 4f).Chest(10f).Head(-8f).FootN(0.38f, 0.13f).FootF(-0.18f, 0.15f, -18f).HandF(0.46f, 1.50f).Blade(76f).EdgeUp().HandN(0.32f, 1.32f, 18f), Ease.OutExpo)
                .Key(15, P().Hip(0.08f, -0.04f, 4f).Chest(12f).Head(-8f).FootN(0.34f, 0.13f).FootF(-0.20f, 0.15f, -18f).HandF(0.38f, 1.56f).Blade(92f).EdgeUp().HandN(0.30f, 1.34f, 18f))
                .Key(25, P().Hip(0.04f, -0.10f, -2f).Chest(-6f).Head(6f).Feet(0.32f, -0.28f).HandF(0.34f, 1.14f).Blade(42f).HandN(0.04f, 1.02f, -4f), Ease.InOutQuad)
                .Key(36, Guard(), Ease.InOutQuad)
                .Chain(4, 0.34f, -130f, bow: 0.16f, e: Ease.OutQuad)
                .Chain(9, 1.42f, 55f, bow: 0f, e: Ease.OutExpo)
                .Chain(15, 1.42f, 55f, bow: 0f)
                .Chain(23, 0.25f, 38f, bow: -0.14f, e: Ease.InQuad)
                .Chain(28, 0f, 20f, bow: 0f, e: Ease.OutQuad)
                .Move(2, 8, 1.8f).JumpCancel(15)
                .Hit(6, 10, Blade, 5, HitLevel.Mid, new Vector2(0.3f, 6.2f), stun: 26, hitstop: 5, shake: 0.14f, launch: true, radius: 0.09f)
                .Vector(StrikeVector.Upward)
                .Hit(8, 14, Blade, 7, HitLevel.High, new Vector2(0.5f, 7.2f), stun: 30, hitstop: 7, shake: 0.20f, launch: true, heavy: true, radius: 0.09f)
                .ChainWhip(0.50f).Vector(StrikeVector.Upward)
                .Gust(8, new Vector2(7.5f, 8.5f), 0.16f)
                .Fx(5, 15, FxKind.TrailBlade).Sword();

            // 7. Dash Attack — Shadow Hook Lunge: low forward rush ending in a vicious downward hooking drag with the sickle blade
            m.dashHook = new MoveDef("chain.dashHook", "Shadow Hook Lunge", 30)
                .Key(5, P().Hip(0.14f, -0.24f, -16f).Chest(-30f).Head(18f).Feet(0.44f, -0.28f).HandF(0.14f, 1.36f).Blade(108f).HandN(-0.14f, 0.96f, -14f), Ease.OutQuad)
                .Key(9, P().Hip(0.16f, -0.26f, -14f).Chest(-32f).Head(18f).Feet(0.46f, -0.28f).HandF(0.54f, 0.72f).Blade(-54f).HandN(-0.16f, 0.92f, -16f), Ease.OutExpo)
                .Key(15, P().Hip(0.14f, -0.24f, -12f).Chest(-28f).Head(16f).Feet(0.44f, -0.28f).HandF(0.46f, 0.68f).Blade(-68f).HandN(-0.12f, 0.92f, -14f))
                .Key(30, Guard(), Ease.InOutQuad)
                .Move(0, 8, 7.0f)
                .Hit(7, 13, Blade, 9, HitLevel.Mid, new Vector2(-1.4f, 0.6f), stun: 22, hitstop: 6, shake: 0.16f, heavy: true, radius: 0.09f)
                .Vector(StrikeVector.HighOverhead)
                .Gust(7, new Vector2(9.5f, -4.5f), 0.16f)
                .Fx(1, 13, FxKind.AfterImage).Fx(6, 14, FxKind.TrailBlade).Fx(6, 6, FxKind.Dust).Sword().Cancel(14);

            // chains: Light 1 (reap) -> Light 2 (lash) -> Light 3 (pull)
            m.jab.chain[Btn.P] = m.hilt;
            m.jab.chain[Btn.S] = m.reap;
            m.jab.chain[Btn.K] = m.kick1;
            m.hilt.chain[Btn.S] = m.reapUp;
            m.hilt.chain[Btn.K] = m.kick2;
            m.crouchJab.chain[Btn.P] = m.hilt;
            m.kick1.chain[Btn.K] = m.kick2;
            m.kick1.chain[Btn.S] = m.reap;
            m.kick2.chain[Btn.S] = m.reapUp;
            m.reap.chain[Btn.S] = m.lash;
            m.reap.chain[Btn.P] = m.pull;
            m.lash.chain[Btn.S] = m.pull;
            m.pull.chain[Btn.S] = m.reap;
            m.dashHook.chain[Btn.S] = m.lash;

            // ===================== AIR =====================
            m.airJab = new MoveDef("chain.airJab", "Air Pommel", 16)
                .Key(3, P().Hip(0f, 0f, 0f).Chest(8f).LegN(60f, -90f).LegF(40f, -100f).HandN(0.15f, 1.64f).HandF(0.10f, 1.20f).Blade(56f))
                .Key(6, P().Hip(0f, 0f, 0f).Chest(-20f).Head(10f).LegN(60f, -90f).LegF(40f, -100f).HandN(0.50f, 1.10f).HandF(0.10f, 1.10f).Blade(56f), Ease.OutExpo)
                .Key(16, P().Hip(0f, 0f, 0f).Chest(-10f).Head(6f).LegN(50f, -70f).LegF(30f, -80f).HandN(0.38f, 1.10f).HandF(0.10f, 1.10f).Blade(56f))
                .Hit(5, 9, Fist, 3, HitLevel.Overhead, new Vector2(1.0f, 0.4f), stun: 12, hitstop: 3, shake: 0.05f)
                .Vector(StrikeVector.HighOverhead)
                .Fx(4, 9, FxKind.TrailFist).Air();

            m.airKick = new MoveDef("chain.airKick", "Swoop Kick", 28)
                .Key(4, P().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(70f, -4f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).HandF(-0.15f, 1.1f).Blade(96f), Ease.OutExpo)
                .Key(28, P().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(68f, -6f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).HandF(-0.15f, 1.1f).Blade(96f))
                .Hit(3, 18, FootN, 5, HitLevel.Overhead, new Vector2(2.0f, 1.2f), stun: 16, hitstop: 4, shake: 0.1f, radius: 0.11f)
                .Vector(StrikeVector.HighOverhead)
                .Fx(3, 12, FxKind.TrailFootN).Air();

            m.airReap = new MoveDef("chain.airReap", "Moon Hook", 30)
                .Key(3, P().Hip(0f, 0f, 0f).Chest(8f).LegN(60f, -90f).LegF(40f, -100f).HandF(0.12f, 1.60f).Blade(112f).HandN(0.20f, 1.36f, 10f))
                .Key(7, P().Hip(0f, 0f, -4f).Chest(-16f).Head(8f).LegN(60f, -90f).LegF(40f, -100f).HandF(0.52f, 1.08f).Blade(-36f).HandN(0.42f, 1.18f, 4f), Ease.OutExpo)
                .Key(30, P().Hip(0f, 0f, -2f).Chest(-10f).Head(6f).LegN(50f, -70f).LegF(30f, -80f).HandF(0.46f, 1.08f).Blade(-30f).HandN(0.12f, 1.10f, 0f))
                .Chain(3, 0.30f, 118f, bow: 0.16f, e: Ease.OutQuad)
                .Chain(7, 1.10f, -34f, bow: 0f, e: Ease.OutExpo)
                .Chain(14, 0f, -20f, bow: 0f, e: Ease.InOutQuad)
                .Hit(6, 12, Blade, 5, HitLevel.Overhead, new Vector2(1.4f, 0.8f), stun: 16, hitstop: 4, shake: 0.08f, radius: 0.085f)
                .ChainWhip(0.48f).Vector(StrikeVector.HighOverhead)
                .Gust(6, new Vector2(7.5f, -4.5f), 0.14f)
                .Fx(5, 12, FxKind.TrailBlade).Sword().Air();

            m.shadowFall = new MoveDef("chain.shadowFall", "Shadow Fall", 32)
                .Key(3, P().Hip(0f, 0f, -4f).Chest(10f).LegN(60f, -90f).LegF(40f, -100f).HandF(0.28f, 0.88f).Blade(-84f).HandN(0.26f, 1.04f, -10f), Ease.OutExpo)
                .Key(32, P().Hip(0f, 0f, -4f).Chest(8f).LegN(58f, -88f).LegF(38f, -98f).HandF(0.28f, 0.88f).Blade(-84f).HandN(0.26f, 1.04f, -10f))
                .Chain(2, 0.20f, -80f, bow: 0.10f)
                .Chain(5, 0.95f, -86f, bow: 0f, e: Ease.OutExpo)
                .Chain(30, 0.95f, -86f, bow: 0f)
                .Move(2, 2, 1.0f, -12.0f, true)
                .Hit(3, 28, Blade, 6, HitLevel.Overhead, new Vector2(0.6f, -2.0f), stun: 18, hitstop: 5, shake: 0.14f, knockdown: true, heavy: true, radius: 0.085f)
                .ChainWhip(0.45f).Vector(StrikeVector.HighOverhead)
                .Gust(4, new Vector2(3.0f, -9.5f), 0.16f)
                .Fx(2, 28, FxKind.TrailBlade).Sword().Air();

            // Reaper's Snare (Back + S, the chain sickle's counter like the katana's Mirror Guard): the chain drawn taut between both hands
            // across the face, the sickle raised high. A blow caught in frames 4-24 is snared aside (the attacker reels as from a parry)
            // and Reaper's Reply answers it
            m.counterGuard = new MoveDef("chain.counterGuard", "Reaper's Snare", 34)
                .Key(5, P().Hip(-0.05f, -0.13f, 2f).Chest(2f).Head(-2f).Feet(0.28f, -0.32f).HandF(0.30f, 1.22f).Blade(74f).HandN(0.08f, 1.04f, 12f))
                .Key(26, P().Hip(-0.05f, -0.14f, 2f).Chest(2f).Head(-2f).Feet(0.28f, -0.32f).HandF(0.30f, 1.21f).Blade(76f).HandN(0.08f, 1.03f, 12f))
                .Key(34, Guard(), Ease.InOutQuad)
                .Fx(4, 4, FxKind.Glint).Sword();
            m.counterGuard.counterFrom = 4;
            m.counterGuard.counterTo = 24;

            // Reaper's Reply: the snare released, the sickle whips down and across in one short reaping cut through the reeling foe
            m.counterReap = new MoveDef("chain.counterReap", "Reaper's Reply", 28)
                .Key(2, P().Hip(-0.02f, -0.12f, 4f).Chest(6f).Head(-2f).Feet(0.28f, -0.28f).HandF(0.10f, 1.20f).Blade(140f).HandN(0.20f, 0.98f, 10f), Ease.OutQuad)
                .Key(5, Lunge(0.12f, -0.14f, -12f, -24f).HandF(0.54f, 1.06f).Blade(-12f).HandN(-0.06f, 0.96f, -12f), Ease.OutExpo)
                .Key(12, Lunge(0.12f, -0.14f, -12f, -24f).HandF(0.52f, 1.04f).Blade(-22f).HandN(-0.08f, 0.96f, -12f))
                .Key(18, P().Hip(0.06f, -0.12f, -6f).Chest(-16f).Head(10f).Feet(0.36f, -0.28f).HandF(0.42f, 0.96f).Blade(8f).HandN(-0.02f, 0.94f, -10f), Ease.OutQuad)
                .Key(28, Guard(), Ease.InOutQuad)
                .Move(0, 4, 2.0f)
                .Hit(2, 7, Blade, 11, HitLevel.Unblockable, new Vector2(1.8f, 1.4f), stun: 30, hitstop: 12, shake: 0.38f, knockdown: true, heavy: true, radius: 0.1f)
                .Vector(StrikeVector.Mid)
                .Gust(2, new Vector2(7.5f, 1.0f), 0.13f)
                .Invuln(0, 12).Fx(0, 8, FxKind.TrailBlade).Fx(3, 3, FxKind.SlashLine).Sword();
            m.counterGuard.counterAnswer = m.counterReap;

            return m;
        }

        /// <summary>Picks a technique from button + direction (relative to facing) + context.</summary>
        public override MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self)
        {
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
            if (self != null && self.IsForwardDash && b is Btn.S or Btn.P) return dashHook;
            Fighter opp = self != null ? self.opponent : null;
            float dist = opp != null ? Mathf.Abs(opp.transform.position.x - self.transform.position.x) : 99f;
            bool oppDown = opp != null && opp.state == FState.Down;
            switch (b)
            {
                case Btn.P:
                    if (dy < 0) return oppDown && dist < 1.4f ? groundHook : crouchJab;
                    if (fwd > 0) return dist < 0.85f && opp != null && opp.CanBeThrown ? throwGrab : pull;
                    return jab;
                case Btn.K:
                    if (dy < 0) return sweep;
                    if (fwd > 0) return pushKick;
                    if (fwd < 0) return backKick;
                    return kick1;
                case Btn.S:
                    if (dy > 0) return crescentLift;
                    if (dy < 0) return graveHook;
                    if (fwd > 0) return lash;
                    if (fwd < 0) return LegacyAi(self) ? cyclone : counterGuard;   // Back + S: Reaper's Snare (legacy AI keeps the Cyclone)
                    return reap;
            }
            return null;
        }

        public override IEnumerable<MoveDef> All => new[]
        {
            jab, hilt, crouchJab, pull, groundHook, kick1, kick2, pushKick, backKick, sweep,
            reap, reapUp, lash, cyclone, graveHook, crescentLift, dashHook,
            airJab, airKick, airReap, shadowFall, counterGuard, counterReap,
            throwGrab, throwWhiff, roll, backflip, getUp, getUpFront,
        };
    }
}
