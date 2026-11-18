using System.Collections.Generic;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// The spear and staff family (ADR-0006, SAN-78): Mizuchi, and the polearms of the Shieldbearer, Kuroe and Shiden.
    /// A two-handed polearm with the longest reach of the families: the lead (far) hand leads on the shaft, the rear
    /// (near) hand holds it a forearm behind (<see cref="Pose.PolearmSpacing"/>). The shaft and head are the
    /// <see cref="HitPoint.Blade"/> line, so every spear window can clash.
    /// <para>
    /// Weapon button: Long Thrust, Charging Lunge forward, Tide Sweep down, Raised Guard back. Punch: pokes (Quick Poke,
    /// Rising Poke, Step Poke, Low Poke, Skewer on a downed foe). Kick: kicks with the spear held upright in the lead
    /// hand, and Dragon Ascent back (a vaulting thrust that launches). Air: Diving Poke, Falling Dragon, Wave Crest.
    /// The body moves (roll, backflip, get-ups, throw) are the katana set's, by reference.
    /// </para>
    /// </summary>
    public sealed class SpearMoves : MoveSet
    {
        public MoveDef jab, poke2, stepPoke, lowPoke, skewer, kick, pushKick, lowKick;
        public MoveDef thrust, lunge, sweep, guardRaise, vault;
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

        /// <summary>Both hands on the shaft: the lead hand's wrist at (x, y), the shaft pointing <paramref name="blade"/> degrees.</summary>
        static Pose Hold(Pose p, float x, float y, float blade) => p.HandF(x, y).Blade(blade).Grip();

        /// <summary>A lunging body (hips forward, chest down) for the thrusts.</summary>
        static Pose Lunge(float hx = 0.1f, float hy = -0.1f, float feetN = 0.34f, float feetF = -0.24f) =>
            P().Hip(hx, hy, -10f).Chest(-20f).Head(10f).Feet(feetN, feetF);

        static Pose LowBody(float hx = 0f) => P().Hip(hx, -0.3f, 0f).Chest(-24f).Head(14f).FootN(0.3f, 0.13f).FootF(-0.26f, 0.15f, -25f);

        SpearMoves(KatanaMoves katana) { ShareBodyMoves(katana); }

        public static SpearMoves Build(KatanaMoves katana)
        {
            var m = new SpearMoves(katana);

            // ===================== PUNCH BUTTON: POKES =====================
            m.jab = new MoveDef("spear.jab", "Quick Poke", 20)
                .Key(4, Stance().HandF(0.14f, 0.96f).Blade(20f))
                .Key(7, Hold(Lunge(0.08f), 0.64f, 1.02f, 14f), Ease.OutExpo)
                .Key(11, Hold(Lunge(0.08f), 0.62f, 1.02f, 14f))
                .Key(20, Stance(), Ease.InOutQuad)
                .Hit(5, 8, Blade, 5, HitLevel.High, new Vector2(1.0f, 0f), stun: 13, hitstop: 4, shake: 0.06f, radius: 0.07f)
                .Fx(4, 9, FxKind.TrailBlade).Sword().Cancel(8);

            m.poke2 = new MoveDef("spear.poke2", "Rising Poke", 24)
                .Key(4, Hold(LowBody(0.02f).Hip(0.02f, -0.22f, -4f), 0.18f, 0.78f, 8f))
                .Key(8, Hold(P().Hip(0.08f, -0.12f, -6f).Chest(-14f).Head(8f).Feet(0.34f, -0.24f), 0.56f, 1.06f, 36f), Ease.OutExpo)
                .Key(12, Hold(P().Hip(0.08f, -0.12f, -6f).Chest(-14f).Head(8f).Feet(0.34f, -0.24f), 0.54f, 1.06f, 36f))
                .Key(24, Stance(), Ease.InOutQuad)
                .Hit(7, 11, Blade, 5, HitLevel.High, new Vector2(0.9f, 1.8f), stun: 16, hitstop: 4, shake: 0.08f, radius: 0.07f)
                .Fx(5, 12, FxKind.TrailBlade).Sword().Cancel(11);

            m.stepPoke = new MoveDef("spear.stepPoke", "Step Poke", 26)
                .Key(5, Hold(Stance().Chest(-6f), 0.1f, 0.98f, 18f))
                .Key(9, Hold(Lunge(0.12f, -0.12f, 0.4f, -0.2f), 0.66f, 1.0f, 12f), Ease.OutExpo)
                .Key(14, Hold(Lunge(0.12f, -0.12f, 0.4f, -0.2f), 0.64f, 1.0f, 12f))
                .Key(26, Stance(), Ease.InOutQuad)
                .Move(3, 10, 3.0f)
                .Hit(8, 12, Blade, 6, HitLevel.High, new Vector2(1.6f, 0.2f), stun: 16, hitstop: 5, shake: 0.1f, radius: 0.07f)
                .Fx(7, 12, FxKind.TrailBlade).Fx(3, 3, FxKind.Dust).Sword().Cancel(12);

            m.lowPoke = new MoveDef("spear.lowPoke", "Low Poke", 22)
                .Key(4, Hold(Crouch(), 0.18f, 0.74f, -6f))
                .Key(7, Hold(LowBody(0.04f), 0.62f, 0.66f, -10f), Ease.OutExpo)
                .Key(10, Hold(LowBody(0.04f), 0.6f, 0.66f, -10f))
                .Key(22, Crouch(), Ease.InOutQuad)
                .Hit(6, 9, Blade, 4, HitLevel.Low, new Vector2(0.9f, 0f), stun: 12, hitstop: 4, shake: 0.05f, radius: 0.07f)
                .Fx(5, 9, FxKind.TrailBlade).Sword().Cancel(9);

            m.skewer = new MoveDef("spear.skewer", "Skewer", 30)
                .Key(6, Hold(P().Hip(0f, -0.1f, 0f).Chest(8f).Head(-4f).Feet(0.28f, -0.26f), 0.24f, 1.5f, 100f))
                .Key(12, Hold(P().Hip(0.1f, -0.2f, -10f).Chest(-34f).Head(16f).Feet(0.36f, -0.28f), 0.62f, 0.92f, -44f), Ease.InCubic)
                .Key(18, Hold(P().Hip(0.1f, -0.2f, -10f).Chest(-34f).Head(16f).Feet(0.36f, -0.28f), 0.62f, 0.9f, -44f))
                .Key(30, Stance(), Ease.InOutQuad)
                .Hit(11, 17, Blade, 6, HitLevel.Mid, new Vector2(0.4f, 1.0f), stun: 18, hitstop: 8, shake: 0.2f, otg: true, radius: 0.08f)
                .Fx(9, 17, FxKind.TrailBlade).Fx(13, 13, FxKind.Dust).Sword();

            // ===================== KICK BUTTON: KICKS AND THE VAULT =====================
            // the spear stands upright in the lead hand while a leg does the work
            m.kick = new MoveDef("spear.kick", "Front Kick", 26)
                .Key(6, P().Hip(0.02f, -0.07f, 10f).Chest(8f).Head(-4f).LegN(98f, -118f, 10f).FootF(-0.16f, 0.13f).HandF(0.1f, 1.02f).Blade(78f).HandN(0.2f, 1.24f, 10f))
                .Key(10, P().Hip(0.12f, -0.04f, 18f).Chest(14f).Head(-8f).LegN(104f, -6f, 20f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.04f).Blade(80f).HandN(0.12f, 1.26f, 10f), Ease.OutExpo)
                .Key(14, P().Hip(0.12f, -0.04f, 18f).Chest(14f).Head(-8f).LegN(102f, -8f, 20f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.04f).Blade(80f).HandN(0.12f, 1.26f, 10f))
                .Key(26, Stance(), Ease.InOutQuad)
                .Hit(9, 13, FootN, 5, HitLevel.Mid, new Vector2(1.6f, 0.4f), stun: 16, hitstop: 5, shake: 0.1f, radius: 0.1f)
                .Fx(8, 14, FxKind.TrailFootN).Cancel(13);

            m.pushKick = new MoveDef("spear.pushKick", "Push Kick", 28)
                .Key(6, P().Hip(0f, -0.06f, 8f).Chest(8f).LegN(100f, -120f, 15f).FootF(-0.2f, 0.13f).HandF(0.12f, 1.0f).Blade(76f).HandN(0.2f, 1.22f, 10f))
                .Key(10, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(96f, -4f, 40f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.02f).Blade(78f).HandN(0.1f, 1.24f, 10f), Ease.OutExpo)
                .Key(14, P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(95f, -5f, 40f).FootF(-0.14f, 0.13f).HandF(0.08f, 1.02f).Blade(78f).HandN(0.1f, 1.24f, 10f))
                .Key(28, Stance(), Ease.InOutQuad)
                .Hit(9, 13, FootTip, 5, HitLevel.Mid, new Vector2(4.0f, 0.6f), stun: 20, hitstop: 6, shake: 0.15f, radius: 0.11f)
                .Fx(8, 13, FxKind.TrailFootN);

            m.lowKick = new MoveDef("spear.lowKick", "Low Kick", 24)
                .Key(5, P().Hip(0.0f, -0.32f, 4f).Chest(-14f).Head(10f).LegN(80f, -100f, 10f).FootF(-0.26f, 0.15f, -25f).HandF(0.14f, 0.86f).Blade(70f).HandN(0.22f, 1.0f, 10f))
                .Key(8, P().Hip(0.06f, -0.3f, 8f).Chest(-8f).Head(8f).LegN(92f, -4f, 20f).FootF(-0.26f, 0.15f, -25f).HandF(0.12f, 0.88f).Blade(72f).HandN(0.2f, 1.02f, 10f), Ease.OutExpo)
                .Key(12, P().Hip(0.06f, -0.3f, 8f).Chest(-8f).Head(8f).LegN(90f, -6f, 20f).FootF(-0.26f, 0.15f, -25f).HandF(0.12f, 0.88f).Blade(72f).HandN(0.2f, 1.02f, 10f))
                .Key(24, Crouch(), Ease.InOutQuad)
                .Hit(7, 11, FootN, 4, HitLevel.Low, new Vector2(0.9f, 0f), stun: 14, hitstop: 4, shake: 0.06f, radius: 0.1f)
                .Fx(6, 11, FxKind.TrailFootN).Cancel(11);

            // Dragon Ascent: a hop with the point driven up and forward: a launcher (the JS vaulting thrust)
            m.vault = new MoveDef("spear.vault", "Dragon Ascent", 40)
                .Key(5, Hold(LowBody(0.0f).Hip(0f, -0.28f, -4f), 0.3f, 0.7f, 66f))
                .Key(9, Hold(P().Hip(0.06f, 0f, 4f).Chest(6f).Head(-4f).LegN(70f, -100f).LegF(50f, -90f), 0.34f, 1.46f, 56f), Ease.OutQuad)
                .Key(16, Hold(P().Hip(0.06f, 0f, 4f).Chest(8f).Head(-6f).LegN(60f, -90f).LegF(40f, -80f), 0.38f, 1.56f, 54f))
                .Key(40, Stance(), Ease.InOutQuad)
                .Move(6, 6, 1.6f, 7.2f, true)
                .Hit(8, 16, Blade, 8, HitLevel.Mid, new Vector2(0.4f, 7.2f), stun: 30, hitstop: 7, shake: 0.2f, launch: true, heavy: true, radius: 0.09f)
                .JumpCancel(18)
                .Fx(5, 16, FxKind.TrailBlade).Fx(6, 6, FxKind.Dust).Sword();

            // ===================== WEAPON BUTTON =====================
            // Long Thrust: the lead hand draws back to the hip, then the whole spear drives out at full reach
            m.thrust = new MoveDef("spear.thrust", "Long Thrust", 34)
                .Key(7, Hold(P().Hip(-0.04f, -0.14f, -4f).Chest(0f).Head(4f).Feet(0.28f, -0.28f), -0.02f, 1.02f, 8f))
                .Key(10, Hold(Lunge(0.16f, -0.14f, 0.42f, -0.3f).Chest(-26f), 0.72f, 1.02f, 12f), Ease.OutExpo)
                .Key(17, Hold(Lunge(0.16f, -0.14f, 0.42f, -0.3f).Chest(-26f), 0.7f, 1.02f, 12f))
                .Key(34, Stance(), Ease.InOutQuad)
                .Move(8, 12, 2.2f)
                .Hit(9, 14, Blade, 9, HitLevel.Mid, new Vector2(2.4f, 0.2f), stun: 20, hitstop: 6, shake: 0.14f, radius: 0.07f)
                .Fx(8, 15, FxKind.TrailBlade).Sword().Cancel(14);

            // Charging Lunge: low and fast across the floor, the point at belly height
            m.lunge = new MoveDef("spear.lunge", "Charging Lunge", 40)
                .Key(6, Hold(P().Hip(-0.02f, -0.28f, -6f).Chest(-26f).Head(14f).FootN(0.3f, 0.13f).FootF(-0.28f, 0.15f, -25f), 0.04f, 0.9f, 4f))
                .Key(10, Hold(P().Hip(0.2f, -0.26f, -18f).Chest(-30f).Head(18f).Feet(0.5f, -0.3f), 0.74f, 0.98f, 8f), Ease.OutExpo)
                .Key(20, Hold(P().Hip(0.2f, -0.26f, -18f).Chest(-30f).Head(18f).Feet(0.5f, -0.3f), 0.72f, 0.98f, 8f))
                .Key(40, Stance(), Ease.InOutQuad)
                .Move(8, 18, 7.0f)
                .Hit(9, 18, Blade, 11, HitLevel.Mid, new Vector2(3.0f, 0.5f), stun: 26, hitstop: 8, shake: 0.25f, heavy: true, radius: 0.08f)
                .Fx(7, 19, FxKind.AfterImage).Fx(8, 19, FxKind.TrailBlade).Fx(8, 8, FxKind.Dust).Sword();

            // Tide Sweep: the shaft carried low round the legs
            m.sweep = new MoveDef("spear.sweep", "Tide Sweep", 38)
                .Key(7, Hold(P().Hip(-0.04f, -0.4f, 0f).Chest(-26f).Head(14f).FootN(0.22f, 0.13f).FootF(-0.3f, 0.15f, -30f), -0.08f, 0.62f, 178f))
                .Key(11, Hold(P().Hip(0.06f, -0.42f, -6f).Chest(-32f).Head(18f).FootN(0.32f, 0.13f).FootF(-0.3f, 0.15f, -30f), 0.52f, 0.56f, -8f), Ease.OutExpo)
                .Key(17, Hold(P().Hip(0.06f, -0.42f, -6f).Chest(-32f).Head(18f).FootN(0.32f, 0.13f).FootF(-0.3f, 0.15f, -30f), 0.52f, 0.54f, -4f))
                .Key(38, Stance(), Ease.InOutQuad)
                .Hit(10, 16, Blade, 8, HitLevel.Low, new Vector2(0.6f, 3.4f), stun: 22, hitstop: 6, shake: 0.15f, knockdown: true, otg: true, radius: 0.08f)
                .Fx(8, 17, FxKind.TrailBlade).Fx(11, 11, FxKind.Dust).Sword();

            // Raised Guard: the shaft held up across the line of attack. No hit window; armour frames for the guard
            // (the guard and counter mechanics themselves are SAN-86 and the boss abilities of ADR-0008).
            m.guardRaise = new MoveDef("spear.guardRaise", "Raised Guard", 34)
                .Key(5, Hold(P().Hip(-0.03f, -0.1f, -2f).Chest(-2f).Head(-4f).Feet(0.2f, -0.28f), 0.3f, 1.28f, 70f))
                .Key(26, Hold(P().Hip(-0.03f, -0.11f, -2f).Chest(-2f).Head(-4f).Feet(0.2f, -0.28f), 0.3f, 1.27f, 72f))
                .Key(34, Stance(), Ease.InOutQuad)
                .Armour(5, 24)
                .Fx(4, 4, FxKind.Glint).Sword();

            // chains: poke -> rising poke -> sweep, and the heavier thrust from either
            m.jab.chain[Btn.P] = m.poke2;
            m.jab.chain[Btn.S] = m.thrust;
            m.jab.chain[Btn.K] = m.kick;
            m.poke2.chain[Btn.S] = m.sweep;
            m.poke2.chain[Btn.P] = m.thrust;
            m.stepPoke.chain[Btn.S] = m.thrust;
            m.kick.chain[Btn.S] = m.thrust;
            m.thrust.chain[Btn.S] = m.sweep;
            m.lowPoke.chain[Btn.P] = m.poke2;

            // ===================== AIR =====================
            m.airPoke = new MoveDef("spear.airPoke", "Diving Poke", 30)
                .Key(4, Hold(P().Hip(0f, 0f, 0f).Chest(8f).LegN(60f, -90f).LegF(40f, -100f), 0.2f, 1.34f, 20f))
                .Key(8, Hold(P().Hip(0f, 0f, 0f).Chest(-14f).Head(8f).LegN(60f, -90f).LegF(40f, -100f), 0.56f, 1.14f, -34f), Ease.OutExpo)
                .Key(30, Hold(P().Hip(0f, 0f, 0f).Chest(-10f).Head(6f).LegN(50f, -70f).LegF(30f, -80f), 0.5f, 1.12f, -30f))
                .Hit(7, 14, Blade, 6, HitLevel.Overhead, new Vector2(1.4f, 0.5f), stun: 16, hitstop: 5, shake: 0.1f, radius: 0.07f)
                .Fx(6, 14, FxKind.TrailBlade).Sword().Air();

            m.airDive = new MoveDef("spear.airDive", "Falling Dragon", 40)
                .Key(5, Hold(P().Hip(0f, 0f, -8f).Chest(-8f).Head(4f).LegN(80f, -110f).LegF(60f, -100f), 0.34f, 1.3f, -62f), Ease.OutExpo)
                .Key(40, Hold(P().Hip(0f, 0f, -8f).Chest(-8f).Head(4f).LegN(78f, -108f).LegF(58f, -98f), 0.34f, 1.3f, -62f))
                .Hit(4, 30, Blade, 9, HitLevel.Overhead, new Vector2(2.0f, 1.6f), stun: 20, hitstop: 7, shake: 0.2f, knockdown: true, heavy: true, radius: 0.08f)
                .Fx(3, 30, FxKind.TrailBlade).Sword().Air();

            m.airSweep = new MoveDef("spear.airSweep", "Wave Crest", 30)
                .Key(3, Hold(P().Hip(0f, 0f, 0f).Chest(-10f).LegN(90f, -120f).LegF(80f, -110f), 0.4f, 1.15f, 6f))
                .Key(18, Hold(P().Hip(0f, 0f, 0f).Chest(-12f).LegN(92f, -122f).LegF(82f, -112f), 0.4f, 1.15f, 6f), Ease.Linear)
                .Key(30, Hold(P().Hip(0f, 0f, 0f).Chest(5f).LegN(40f, -60f).LegF(20f, -50f), 0.3f, 1.0f, -20f))
                .Spin(2, 19, -360f)
                .Hit(4, 9, Blade, 5, HitLevel.Overhead, new Vector2(1.2f, 1.0f), stun: 16, hitstop: 4, shake: 0.08f, radius: 0.08f)
                .Hit(11, 16, Blade, 5, HitLevel.Overhead, new Vector2(1.8f, 2.5f), stun: 20, hitstop: 6, shake: 0.15f, knockdown: true, radius: 0.08f)
                .Fx(3, 18, FxKind.TrailBlade).Sword().Air().Normalize();

            return m;
        }

        /// <summary>Picks a technique from button + direction (relative to facing) + context.</summary>
        public override MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self)
        {
            if (air) return b switch { Btn.P => airPoke, Btn.K => airDive, Btn.S => airSweep, _ => null };
            Fighter opp = self.opponent;
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
                    if (dy < 0) return sweep;
                    if (fwd > 0) return lunge;
                    if (fwd < 0) return guardRaise;
                    return thrust;
            }
            return null;
        }

        public override IEnumerable<MoveDef> All => new[]
        {
            jab, poke2, stepPoke, lowPoke, skewer, kick, pushKick, lowKick, vault, thrust, lunge, sweep, guardRaise,
            airPoke, airDive, airSweep,
            throwGrab, throwWhiff, roll, backflip, getUp, getUpFront,
        };
    }
}
