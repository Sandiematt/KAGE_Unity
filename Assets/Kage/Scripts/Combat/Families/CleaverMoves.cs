using System.Collections.Generic;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// The cleaver family (ADR-0006, SAN-79): Onikiri, and the Order Ironclad's Raider Cleaver. A heavy two-handed
    /// grip (<see cref="GripPose.HeavyTwoHand"/>, hands a hand apart on a long handle) and a broad blade that is the
    /// <see cref="HitPoint.Blade"/> line, so every cleaver window can clash. The set is built to be clearly slower
    /// and harder-hitting than the katana's: a first hit about 1.7 times later, about 1.7 times the damage per window.
    /// <para>
    /// Weapon button: Overhead Chop (neutral), Shoulder Charge (forward), Ground Slam (down), Reaping Sweep (back).
    /// Punch: Hack and Return Cut, Advancing Hack, Low Hack, Crushing Hew on a downed foe. Kick: Boot, Shove Kick,
    /// Low Kick, Pommel Smash back (a launcher). Air: Diving Chop, Boot Drop, Plunging Cleave. The heavy swings carry
    /// armour frames and extra posture as data (SAN-80 and SAN-86 apply them). The body moves (roll, backflip,
    /// get-ups, throw) are the katana set's, by reference. No plain Special, as in the spear family.
    /// </para>
    /// </summary>
    public sealed class CleaverMoves : MoveSet
    {
        public MoveDef hack, hack2, stepHack, lowHack, hew, kick, pushKick, lowKick, pommel;
        public MoveDef overhead, sweep, charge, slam;
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
        static readonly HitPoint[] Shoulder = { HitPoint.Fist, HitPoint.KneeN };   // the leading forearm and knee: the shoulder barge

        static Pose P() => new Pose();

        /// <summary>Both hands on the handle: the lead hand's wrist at (x, y), the cleaver pointing <paramref name="blade"/> degrees.</summary>
        static Pose Hold(Pose p, float x, float y, float blade) => p.HandF(x, y).Blade(blade).Grip();

        /// <summary>A chopping body: hips forward, chest down.</summary>
        static Pose Chop(float hx = 0.1f, float hy = -0.12f, float feetN = 0.34f, float feetF = -0.24f) =>
            P().Hip(hx, hy, -8f).Chest(-24f).Head(12f).Feet(feetN, feetF);

        /// <summary>The cleaver raised high and back, the body arched under it.</summary>
        static Pose Raised(float hy = -0.04f) => Hold(P().Hip(-0.06f, hy, 8f).Chest(20f).Head(-12f).Feet(0.26f, -0.28f), -0.12f, 1.86f, 148f);

        /// <summary>Crouched low behind a heavy follow-through.</summary>
        static Pose LowBody(float hx = 0.1f, float hy = -0.3f) =>
            P().Hip(hx, hy, -8f).Chest(-38f).Head(18f).FootN(0.32f, 0.13f).FootF(-0.26f, 0.15f, -25f);

        CleaverMoves(KatanaMoves katana) { ShareBodyMoves(katana); }

        public static CleaverMoves Build(KatanaMoves katana)
        {
            var m = new CleaverMoves(katana);

            // ===================== PUNCH BUTTON: HACKS =====================
            m.hack = new MoveDef("cleaver.hack", "Hack", 28)
                .Key(9, Hold(P().Hip(0f, -0.1f, 2f).Chest(4f).Head(-4f).Feet(0.26f, -0.26f), 0.1f, 1.5f, 110f), Ease.OutQuad)
                .Key(13, Hold(Chop(), 0.55f, 1.0f, -14f), Ease.InCubic)
                .Key(19, Hold(Chop(), 0.56f, 0.98f, -14f))
                .Key(28, Stance(), Ease.InOutQuad)
                .Hit(11, 16, Blade, 8, HitLevel.High, new Vector2(1.5f, 0f), stun: 18, hitstop: 6, shake: 0.12f, radius: 0.09f)
                .Fx(10, 16, FxKind.TrailBlade).Sword().Cancel(16);

            // Return Cut: the cleaver swings back across the body on the same line
            m.hack2 = new MoveDef("cleaver.hack2", "Return Cut", 32)
                .Key(9, Hold(P().Hip(-0.02f, -0.12f, 6f).Chest(8f).Head(-4f).Feet(0.28f, -0.26f), 0.0f, 1.12f, 160f), Ease.OutQuad)
                .Key(14, Hold(Chop(0.1f, -0.14f), 0.56f, 1.1f, -6f), Ease.InCubic)
                .Key(20, Hold(Chop(0.1f, -0.14f), 0.57f, 1.08f, -6f))
                .Key(32, Stance(), Ease.InOutQuad)
                .Hit(12, 17, Blade, 9, HitLevel.Mid, new Vector2(1.7f, 0.2f), stun: 20, hitstop: 6, shake: 0.14f, radius: 0.09f)
                .Fx(11, 17, FxKind.TrailBlade).Sword().Cancel(18);

            m.stepHack = new MoveDef("cleaver.stepHack", "Advancing Hack", 34)
                .Key(8, Hold(Stance().Chest(-4f), 0.05f, 1.34f, 100f), Ease.OutQuad)
                .Key(12, Hold(Chop(0.12f, -0.14f, 0.4f, -0.22f), 0.6f, 0.98f, -16f), Ease.InCubic)
                .Key(18, Hold(Chop(0.12f, -0.14f, 0.4f, -0.22f), 0.6f, 0.96f, -16f))
                .Key(34, Stance(), Ease.InOutQuad)
                .Move(4, 13, 3.0f)
                .Hit(11, 16, Blade, 9, HitLevel.High, new Vector2(2.0f, 0.2f), stun: 20, hitstop: 7, shake: 0.14f, radius: 0.09f)
                .Fx(10, 16, FxKind.TrailBlade).Fx(4, 4, FxKind.Dust).Sword().Cancel(16);

            m.lowHack = new MoveDef("cleaver.lowHack", "Low Hack", 28)
                .Key(7, Hold(Crouch().Hip(-0.02f, -0.3f, 0f), 0.1f, 0.92f, 40f), Ease.OutQuad)
                .Key(11, Hold(P().Hip(0.04f, -0.32f, -4f).Chest(-26f).Head(16f).FootN(0.3f, 0.13f).FootF(-0.26f, 0.15f, -25f), 0.6f, 0.64f, -22f), Ease.InCubic)
                .Key(16, Hold(P().Hip(0.04f, -0.32f, -4f).Chest(-26f).Head(16f).FootN(0.3f, 0.13f).FootF(-0.26f, 0.15f, -25f), 0.6f, 0.64f, -22f))
                .Key(28, Crouch(), Ease.InOutQuad)
                .Hit(10, 15, Blade, 7, HitLevel.Low, new Vector2(1.0f, 0f), stun: 16, hitstop: 5, shake: 0.1f, radius: 0.09f)
                .Fx(9, 15, FxKind.TrailBlade).Sword().Cancel(15);

            // Crushing Hew: the cleaver brought straight down on a foe on the floor
            m.hew = new MoveDef("cleaver.hew", "Crushing Hew", 40)
                .Key(10, Hold(P().Hip(0f, -0.1f, 0f).Chest(10f).Head(-6f).Feet(0.28f, -0.26f), 0.1f, 1.7f, 110f), Ease.OutQuad)
                .Key(16, Hold(LowBody(0.1f, -0.28f), 0.6f, 0.74f, -44f), Ease.InCubic)
                .Key(26, Hold(LowBody(0.1f, -0.28f), 0.6f, 0.74f, -44f))
                .Key(40, Stance(), Ease.InOutQuad)
                .Hit(14, 22, Blade, 14, HitLevel.Mid, new Vector2(0.4f, 1.0f), stun: 20, hitstop: 9, shake: 0.3f, otg: true, heavy: true, radius: 0.09f)
                .Fx(12, 22, FxKind.TrailBlade).Fx(17, 17, FxKind.Dust).Sword();

            // ===================== KICK BUTTON: BOOTS AND THE POMMEL =====================
            // the cleaver rests upright in the lead hand while a leg does the work
            m.kick = new MoveDef("cleaver.kick", "Boot", 32)
                .Key(8, P().Hip(0.02f, -0.07f, 10f).Chest(8f).Head(-4f).LegN(98f, -118f, 10f).FootF(-0.16f, 0.13f).HandF(0.1f, 1.02f).Blade(78f).HandN(0.2f, 1.24f, 10f))
                .Key(12, P().Hip(0.12f, -0.04f, 18f).Chest(14f).Head(-8f).LegN(104f, -6f, 20f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.04f).Blade(80f).HandN(0.12f, 1.26f, 10f), Ease.OutExpo)
                .Key(17, P().Hip(0.12f, -0.04f, 18f).Chest(14f).Head(-8f).LegN(102f, -8f, 20f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.04f).Blade(80f).HandN(0.12f, 1.26f, 10f))
                .Key(32, Stance(), Ease.InOutQuad)
                .Hit(12, 17, FootN, 8, HitLevel.Mid, new Vector2(1.8f, 0.4f), stun: 18, hitstop: 6, shake: 0.12f, radius: 0.1f)
                .Fx(11, 17, FxKind.TrailFootN).Cancel(17);

            m.pushKick = new MoveDef("cleaver.pushKick", "Shove Kick", 34)
                .Key(8, P().Hip(0f, -0.06f, 8f).Chest(8f).LegN(100f, -120f, 15f).FootF(-0.2f, 0.13f).HandF(0.12f, 1.0f).Blade(76f).HandN(0.2f, 1.22f, 10f))
                .Key(12, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(96f, -4f, 40f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.02f).Blade(78f).HandN(0.1f, 1.24f, 10f), Ease.OutExpo)
                .Key(17, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(95f, -5f, 40f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.02f).Blade(78f).HandN(0.1f, 1.24f, 10f))
                .Key(34, Stance(), Ease.InOutQuad)
                .Hit(12, 17, FootTip, 7, HitLevel.Mid, new Vector2(4.2f, 0.6f), stun: 22, hitstop: 7, shake: 0.16f, radius: 0.11f)
                .Fx(11, 17, FxKind.TrailFootN);

            m.lowKick = new MoveDef("cleaver.lowKick", "Low Kick", 34)
                .Key(8, P().Hip(0.0f, -0.32f, 4f).Chest(-14f).Head(10f).LegN(80f, -100f, 10f).FootF(-0.26f, 0.15f, -25f).HandF(0.14f, 0.86f).Blade(70f).HandN(0.22f, 1.0f, 10f))
                .Key(11, P().Hip(0.06f, -0.3f, 8f).Chest(-8f).Head(8f).LegN(92f, -4f, 20f).FootF(-0.26f, 0.15f, -25f).HandF(0.12f, 0.88f).Blade(72f).HandN(0.2f, 1.02f, 10f), Ease.OutExpo)
                .Key(16, P().Hip(0.06f, -0.3f, 8f).Chest(-8f).Head(8f).LegN(90f, -6f, 20f).FootF(-0.26f, 0.15f, -25f).HandF(0.12f, 0.88f).Blade(72f).HandN(0.2f, 1.02f, 10f))
                .Key(34, Crouch(), Ease.InOutQuad)
                .Hit(10, 15, FootN, 7, HitLevel.Low, new Vector2(1.0f, 0f), stun: 16, hitstop: 5, shake: 0.08f, radius: 0.1f)
                .Fx(9, 15, FxKind.TrailFootN).Cancel(15);

            // Pommel Smash: the handle's butt driven up under the chin: a launcher
            m.pommel = new MoveDef("cleaver.pommel", "Pommel Smash", 40)
                .Key(10, Hold(P().Hip(0f, -0.2f, 0f).Chest(-14f).Head(10f).Feet(0.28f, -0.26f), 0.0f, 0.8f, -176f), Ease.OutQuad)
                .Key(16, Hold(P().Hip(0.1f, -0.04f, 2f).Chest(8f).Head(-6f).FootN(0.34f, 0.13f).FootF(-0.16f, 0.15f, -20f), 0.5f, 1.5f, 176f), Ease.OutExpo)
                .Key(24, Hold(P().Hip(0.1f, -0.04f, 2f).Chest(10f).Head(-6f).FootN(0.32f, 0.13f).FootF(-0.18f, 0.15f, -20f), 0.46f, 1.56f, 176f))
                .Key(40, Stance(), Ease.InOutQuad)
                .Move(8, 15, 1.8f)
                .Hit(14, 20, Hilt, 11, HitLevel.High, new Vector2(0.5f, 5.6f), stun: 30, hitstop: 8, shake: 0.22f, launch: true, heavy: true, radius: 0.13f)
                .JumpCancel(22)
                .Fx(13, 20, FxKind.Glint).Sword();

            // ===================== WEAPON BUTTON =====================
            // Overhead Chop: raised and held long, then the whole cleaver comes down through the foe
            m.overhead = new MoveDef("cleaver.overhead", "Overhead Chop", 58)
                .Key(11, Raised(-0.06f), Ease.OutQuad)
                .Key(24, Raised(-0.02f).Chest(24f), Ease.InOutQuad)
                .Key(31, Hold(LowBody(0.1f, -0.22f), 0.52f, 0.7f, -36f), Ease.InCubic)
                .Key(40, Hold(LowBody(0.1f, -0.22f), 0.52f, 0.68f, -36f))
                .Key(58, Stance(), Ease.InOutQuad)
                .Move(25, 30, 2.2f)
                .Armour(8, 31)
                .Hit(27, 34, Blade, 22, HitLevel.Overhead, new Vector2(1.2f, -3.2f), stun: 32, hitstop: 12, shake: 0.5f, knockdown: true, heavy: true, radius: 0.1f, postureMul: 1.6f)
                .Fx(24, 34, FxKind.TrailBlade).Fx(31, 31, FxKind.Dust).Sword();

            // Reaping Sweep: wound up behind the back hip, the cleaver unwinds in one wide arc to the front
            m.sweep = new MoveDef("cleaver.sweep", "Reaping Sweep", 50)
                .Key(9, Hold(P().Hip(-0.1f, -0.22f, 10f).Chest(10f).Head(-4f).Feet(0.3f, -0.3f), -0.28f, 0.98f, -172f), Ease.OutQuad)
                .Key(19, Hold(P().Hip(-0.12f, -0.22f, 12f).Chest(12f).Head(-4f).Feet(0.3f, -0.3f), -0.34f, 1.02f, -176f), Ease.InOutQuad)
                .Key(25, Hold(P().Hip(0.0f, -0.22f, -4f).Chest(-16f).Head(8f).Feet(0.34f, -0.3f), 0.2f, 1.14f, -92f), Ease.InCubic)
                .Key(29, Hold(P().Hip(0.12f, -0.2f, -14f).Chest(-30f).Head(16f).Feet(0.4f, -0.3f), 0.56f, 1.04f, -6f), Ease.OutQuad)
                .Key(36, Hold(P().Hip(0.12f, -0.2f, -14f).Chest(-30f).Head(16f).Feet(0.4f, -0.3f), 0.57f, 1.02f, -6f))
                .Key(50, Stance(), Ease.InOutQuad)
                .Move(20, 29, 3.0f)
                .Armour(9, 29)
                .Hit(23, 32, Blade, 16, HitLevel.Mid, new Vector2(3.2f, 1.0f), stun: 28, hitstop: 10, shake: 0.4f, knockdown: true, heavy: true, radius: 0.1f, postureMul: 1.4f)
                .Fx(21, 32, FxKind.TrailBlade).Fx(20, 20, FxKind.Dust).Sword().Cancel(36);

            // Shoulder Charge: low and driving, the cleaver trailed behind and the leading shoulder takes the foe
            m.charge = new MoveDef("cleaver.charge", "Shoulder Charge", 52)
                .Key(9, P().Hip(-0.06f, -0.28f, 6f).Chest(-14f).Head(10f).FootN(0.3f, 0.13f).FootF(-0.3f, 0.15f, -25f).HandN(0.3f, 1.0f, 10f).HandF(-0.3f, 0.9f).Blade(170f), Ease.OutQuad)
                .Key(14, P().Hip(0.22f, -0.26f, -22f).Chest(-44f).Head(22f).Feet(0.52f, -0.3f).HandN(0.5f, 1.16f, 20f).HandF(-0.3f, 0.95f).Blade(168f), Ease.OutExpo)
                .Key(32, P().Hip(0.24f, -0.26f, -22f).Chest(-44f).Head(22f).Feet(0.54f, -0.3f).HandN(0.52f, 1.16f, 20f).HandF(-0.3f, 0.95f).Blade(168f))
                .Key(52, Stance(), Ease.InOutQuad)
                .Move(12, 30, 6.0f)
                .Armour(6, 30)
                .Hit(16, 32, Shoulder, 14, HitLevel.Mid, new Vector2(4.5f, 1.0f), stun: 28, hitstop: 9, shake: 0.3f, knockdown: true, heavy: true, radius: 0.2f, postureMul: 1.3f)
                .Fx(12, 31, FxKind.AfterImage).Fx(13, 13, FxKind.Dust).Sword();

            // Ground Slam: a heavy hop, the cleaver brought down into the floor, the ground splitting under the foe
            m.slam = new MoveDef("cleaver.slam", "Ground Slam", 64)
                .Key(9, Hold(Crouch().Hip(0f, -0.32f, 0f), 0.1f, 1.0f, 60f), Ease.OutQuad)
                .Key(17, Hold(P().Hip(0.02f, 0f, 2f).Chest(14f).Head(-10f).LegN(70f, -100f).LegF(50f, -90f), -0.04f, 1.9f, 140f), Ease.OutQuad)
                .Key(26, Hold(P().Hip(0.02f, 0f, 2f).Chest(18f).Head(-10f).LegN(66f, -96f).LegF(46f, -86f), -0.1f, 1.94f, 150f))
                .Key(33, Hold(LowBody(0.08f, -0.3f), 0.62f, 0.72f, -44f), Ease.InCubic)
                .Key(44, Hold(LowBody(0.08f, -0.3f), 0.62f, 0.72f, -44f))
                .Key(64, Stance(), Ease.InOutQuad)
                .Move(14, 14, 2.2f, 5.4f, true)
                .Armour(8, 34)
                .Hit(31, 40, Blade, 26, HitLevel.Mid, new Vector2(1.4f, 3.0f), stun: 34, hitstop: 14, shake: 0.6f, knockdown: true, otg: true, heavy: true, radius: 0.11f, postureMul: 1.8f)
                .Fx(28, 40, FxKind.TrailBlade).Fx(34, 34, FxKind.Shockwave).Fx(34, 34, FxKind.Dust).Sword();

            // chains: the quick hacks run into the heavy swings
            m.hack.chain[Btn.P] = m.hack2;
            m.hack.chain[Btn.S] = m.sweep;
            m.hack.chain[Btn.K] = m.kick;
            m.hack2.chain[Btn.S] = m.overhead;
            m.hack2.chain[Btn.K] = m.pushKick;
            m.stepHack.chain[Btn.S] = m.overhead;
            m.kick.chain[Btn.S] = m.overhead;
            m.lowHack.chain[Btn.P] = m.hack2;
            m.sweep.chain[Btn.S] = m.overhead;

            // ===================== AIR =====================
            m.airChop = new MoveDef("cleaver.airChop", "Diving Chop", 36)
                .Key(5, Hold(P().Hip(0f, 0f, 0f).Chest(8f).LegN(60f, -90f).LegF(40f, -100f), 0.1f, 1.7f, 112f))
                .Key(11, Hold(P().Hip(0f, 0f, 0f).Chest(-20f).Head(10f).LegN(60f, -90f).LegF(40f, -100f), 0.55f, 1.12f, -30f), Ease.InCubic)
                .Key(36, Hold(P().Hip(0f, 0f, 0f).Chest(-14f).Head(8f).LegN(50f, -70f).LegF(30f, -80f), 0.5f, 1.1f, -30f))
                .Hit(9, 17, Blade, 10, HitLevel.Overhead, new Vector2(1.6f, 0.5f), stun: 20, hitstop: 6, shake: 0.14f, radius: 0.09f)
                .Fx(8, 17, FxKind.TrailBlade).Sword().Air();

            m.airKick = new MoveDef("cleaver.airKick", "Boot Drop", 44)
                .Key(6, P().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(70f, -4f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).HandF(-0.15f, 1.1f).Blade(-170f), Ease.OutExpo)
                .Key(44, P().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(68f, -6f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).HandF(-0.15f, 1.1f).Blade(-170f))
                .Hit(6, 34, FootN, 9, HitLevel.Overhead, new Vector2(2.6f, 1.8f), stun: 22, hitstop: 8, shake: 0.22f, knockdown: true, radius: 0.11f)
                .Fx(5, 14, FxKind.TrailFootN).Air();

            // Plunging Cleave: the cleaver raised, then the whole body dropped behind it
            m.airPlunge = new MoveDef("cleaver.airPlunge", "Plunging Cleave", 46)
                .Key(6, Hold(P().Hip(0f, 0f, -4f).Chest(12f).Head(-8f).LegN(70f, -100f).LegF(50f, -90f), 0.05f, 1.78f, 118f))
                .Key(13, Hold(P().Hip(0f, 0f, -8f).Chest(-24f).Head(12f).LegN(64f, -92f).LegF(44f, -84f), 0.5f, 0.98f, -52f), Ease.InCubic)
                .Key(46, Hold(P().Hip(0f, 0f, -8f).Chest(-22f).Head(12f).LegN(60f, -88f).LegF(40f, -80f), 0.5f, 0.96f, -52f))
                .Hit(11, 32, Blade, 18, HitLevel.Overhead, new Vector2(1.6f, -1.0f), stun: 28, hitstop: 12, shake: 0.4f, knockdown: true, heavy: true, radius: 0.1f, postureMul: 1.5f)
                .Fx(9, 32, FxKind.TrailBlade).Sword().Air();

            return m;
        }

        /// <summary>Picks a technique from button + direction (relative to facing) + context.</summary>
        public override MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self)
        {
            if (air) return b switch { Btn.P => airChop, Btn.K => airKick, Btn.S => airPlunge, _ => null };
            Fighter opp = self.opponent;
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
                    if (dy < 0) return slam;
                    if (fwd > 0) return charge;
                    if (fwd < 0) return sweep;
                    return overhead;
            }
            return null;
        }

        public override IEnumerable<MoveDef> All => new[]
        {
            hack, hack2, stepHack, lowHack, hew, kick, pushKick, lowKick, pommel, overhead, sweep, charge, slam,
            airChop, airKick, airPlunge,
            throwGrab, throwWhiff, roll, backflip, getUp, getUpFront,
        };
    }
}
