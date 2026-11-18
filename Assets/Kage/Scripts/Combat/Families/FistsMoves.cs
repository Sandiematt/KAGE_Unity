using System.Collections.Generic;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// The fists family (ADR-0006, SAN-70 and SAN-80): bare hands, extended for the heavy brawlers. Kage's own fist and
    /// kick techniques (jab, rear cross, rising fist, stepping punch, the kicks, the sweep, the ground strike, the
    /// shoulder throw, the flying kick and hammer fist) are the katana set's, shared by reference. The Weapon and
    /// Special buttons are the family's own, and the whole of what the iron fists of Garan fight with:
    /// <list type="bullet">
    /// <item><b>Weapon</b>: Iron Haymaker (an armoured, heavy overhand swing; jab and roundhouse chain into it),
    /// Iron Grip forward (the heavy grapple: grab, carry overhead, slam behind), Quake Fist down (a low smash),
    /// Iron Guard back (arms crossed, armour frames, no blow), Axe Handle in the air.</item>
    /// <item><b>Special</b>: Shadow Palm (25 Rage), a palm that hurls the foe away.</item>
    /// </list>
    /// Armour frames (<see cref="MoveDef.Armour"/>) are the armour check's (Fighter.Armour.cs): light hits land their damage
    /// and don't flinch the swing, the grip or the guard. No fists move has a <see cref="HitPoint.Blade"/> point, so none
    /// clashes. A foe with <c>iron_fists</c> fights with the same set; its gauntlets are part of its own part set.
    /// </summary>
    public sealed class FistsMoves : MoveSet
    {
        readonly KatanaMoves _k;

        public MoveDef haymaker, grapple, grappleWhiff, quake, ironGuard, shadowPalm, airSlam;

        public static readonly WeaponFamilyDef Family = new()
        {
            id = WeaponFamilies.Fists,
            referenceWeapon = "fists",
            build = sets => Build((KatanaMoves)sets.Get(WeaponFamilies.Katana)),
            grip = GripPose.Fists,
            bladeLength = 0f,
            rageArt = Fighter.ThousandShadows,
            visual = new FistsVisual(),
        };

        static readonly HitPoint[] Fist = { HitPoint.Fist };
        static readonly HitPoint[] BothFists = { HitPoint.Fist, HitPoint.FistF };

        static Pose P() => new Pose();

        /// <summary>The unarmed guard a move recovers into (both fists up).</summary>
        static Pose Guard() => FistGuard(Stance());

        FistsMoves(KatanaMoves katana)
        {
            _k = katana;
            ShareBodyMoves(katana);
        }

        public static FistsMoves Build(KatanaMoves katana)
        {
            var m = new FistsMoves(katana);

            // ===================== WEAPON BUTTON =====================
            // Iron Haymaker: the near fist drawn back behind the body, then swung over the top and down through the foe.
            // Armoured from the sink to the end of the swing: the blow goes through whatever light hits it takes.
            m.haymaker = new MoveDef("fists.haymaker", "Iron Haymaker", 46)
                .Key(4, P().Hip(-0.04f, -0.22f, 4f).Chest(10f).Head(-2f).Feet(0.3f, -0.3f).HandN(-0.1f, 0.95f, 30f).HandF(0.22f, 1.18f, 10f))
                .Key(11, P().Hip(-0.1f, -0.2f, 12f).Chest(24f).Head(-8f).Feet(0.3f, -0.32f).HandN(-0.52f, 1.12f, 50f).HandF(0.2f, 1.2f, 10f), Ease.OutQuad)
                .Key(15, P().Hip(0.06f, -0.14f, -6f).Chest(-14f).Head(6f).Feet(0.38f, -0.3f).HandN(0.42f, 1.5f, 10f).HandF(0.12f, 1.15f, 10f), Ease.InCubic)
                .Key(19, P().Hip(0.16f, -0.16f, -14f).Chest(-34f).Head(16f).Feet(0.46f, -0.3f).HandN(0.8f, 1.0f, -30f).HandF(0.0f, 1.1f, 10f), Ease.OutExpo)
                .Key(27, P().Hip(0.16f, -0.16f, -14f).Chest(-34f).Head(16f).Feet(0.46f, -0.3f).HandN(0.78f, 0.98f, -30f).HandF(0.0f, 1.1f, 10f))
                .Key(46, Guard(), Ease.InOutQuad)
                .Move(9, 19, 2.2f)
                .Hit(14, 20, Fist, 15, HitLevel.Mid, new Vector2(3.2f, 2.4f), stun: 28, hitstop: 10, shake: 0.35f, knockdown: true, heavy: true, radius: 0.14f, postureMul: 1.4f)
                .Armour(3, 22)
                .Fx(12, 21, FxKind.TrailFist).Fx(15, 15, FxKind.Dust);

            // Iron Grip: the heavy grapple. Both arms clamp the foe (the grab frame), heave it overhead, carry it over
            // the shoulders and slam it down behind, released into a physics launch (SAN-68). Armoured until the release.
            m.grappleWhiff = new MoveDef("fists.grappleWhiff", "Iron Grip", 46)
                .Key(5, P().Hip(0.1f, -0.14f, -8f).Chest(-24f).Head(12f).Feet(0.38f, -0.24f).HandN(0.55f, 1.18f).HandF(0.5f, 1.02f))
                .Key(9, P().Hip(0.14f, -0.18f, -10f).Chest(-28f).Head(14f).Feet(0.4f, -0.24f).HandN(0.6f, 1.14f).HandF(0.55f, 1.0f), Ease.OutExpo)
                .Key(18, P().Hip(0.12f, -0.16f, -10f).Chest(-26f).Head(14f).Feet(0.4f, -0.24f).HandN(0.58f, 1.12f).HandF(0.52f, 0.98f))
                .Key(46, Guard(), Ease.InOutQuad)
                .Armour(2, 9);
            m.grapple = new MoveDef("fists.grapple", "Iron Grip", 78)
                .Key(5, P().Hip(0.1f, -0.14f, -8f).Chest(-24f).Head(12f).Feet(0.38f, -0.24f).HandN(0.55f, 1.18f).HandF(0.5f, 1.02f))
                .Key(9, P().Hip(0.14f, -0.18f, -10f).Chest(-28f).Head(14f).Feet(0.4f, -0.24f).HandN(0.6f, 1.14f).HandF(0.55f, 1.0f), Ease.OutExpo)
                .Key(21, P().Hip(0f, -0.28f, -2f).Chest(-8f).Head(-4f).Feet(0.3f, -0.3f).HandN(0.3f, 1.75f).HandF(0.22f, 1.7f), Ease.InOutQuad)
                .Key(31, P().Hip(-0.04f, -0.16f, 8f).Chest(14f).Head(-12f).Feet(0.28f, -0.28f).HandN(-0.05f, 1.9f).HandF(-0.14f, 1.85f), Ease.InOutQuad)
                .Key(43, P().Hip(-0.06f, -0.34f, -12f).Chest(-44f).Head(20f).Feet(0.26f, -0.3f).HandN(-0.5f, 0.7f).HandF(-0.44f, 0.6f), Ease.InQuad)
                .Key(56, P().Hip(-0.04f, -0.3f, -8f).Chest(-36f).Head(16f).Feet(0.26f, -0.3f).HandN(-0.4f, 0.72f).HandF(-0.36f, 0.62f))
                .Key(78, Guard(), Ease.InOutQuad)
                .Armour(2, 43)
                .Fx(43, 43, FxKind.Dust);
            m.grapple.isThrow = true;
            m.grapple.grabFrame = 9;
            m.grapple.releaseFrame = 43;
            m.grapple.throwDmg = 18;
            m.grapple.whiff = m.grappleWhiff;
            m.grapple.grabRange = 1.25f;
            m.grapple.carryFrom = 0.62f;
            m.grapple.carryTo = -1.0f;
            m.grapple.carryHeight = 1.7f;
            m.grapple.throwKb = new Vector2(-5.0f, -5.0f);

            // Quake Fist: crouched, both fists raised and driven into the ground in front: a low smash that knocks down
            m.quake = new MoveDef("fists.quake", "Quake Fist", 48)
                .Key(5, P().Hip(0f, -0.32f, 0f).Chest(8f).Head(-6f).FootN(0.28f, 0.13f).FootF(-0.26f, 0.15f, -25f).HandN(0.12f, 1.7f, 60f).HandF(0.02f, 1.62f, 60f))
                .Key(10, P().Hip(0.08f, -0.46f, -8f).Chest(-40f).Head(20f).FootN(0.36f, 0.13f).FootF(-0.28f, 0.15f, -25f).HandN(0.7f, 0.22f, -40f).HandF(0.6f, 0.2f, -40f), Ease.InCubic)
                .Key(18, P().Hip(0.08f, -0.46f, -8f).Chest(-40f).Head(20f).FootN(0.36f, 0.13f).FootF(-0.28f, 0.15f, -25f).HandN(0.7f, 0.22f, -40f).HandF(0.6f, 0.2f, -40f))
                .Key(48, Guard(), Ease.InOutQuad)
                .Hit(9, 15, BothFists, 9, HitLevel.Low, new Vector2(1.4f, 3.6f), stun: 24, hitstop: 8, shake: 0.28f, knockdown: true, otg: true, heavy: true, radius: 0.14f)
                .Fx(10, 10, FxKind.Dust).Fx(10, 10, FxKind.Shockwave);

            // Iron Guard: arms crossed in front of the chest. No blow; armour frames (the guard mechanics are SAN-86's)
            m.ironGuard = new MoveDef("fists.ironGuard", "Iron Guard", 36)
                .Key(5, P().Hip(-0.04f, -0.12f, -2f).Chest(-2f).Head(-6f).Feet(0.2f, -0.28f).HandN(0.34f, 1.4f, 50f).HandF(0.3f, 1.26f, 50f))
                .Key(26, P().Hip(-0.04f, -0.13f, -2f).Chest(-2f).Head(-6f).Feet(0.2f, -0.28f).HandN(0.34f, 1.39f, 50f).HandF(0.3f, 1.25f, 50f))
                .Key(36, Guard(), Ease.InOutQuad)
                .Armour(5, 26);

            // ===================== SPECIAL =====================
            // Shadow Palm (25 Rage): the far hand gathers ink, then the palm drives through and hurls the foe away
            m.shadowPalm = new MoveDef("fists.shadowPalm", "Shadow Palm", 40)
                .Key(8, P().Hip(-0.06f, -0.14f, -2f).Chest(10f).Head(-6f).Feet(0.26f, -0.28f).HandN(0.16f, 1.2f, 10f).HandF(-0.16f, 1.28f, 20f))
                .Key(12, P().Hip(0.12f, -0.14f, -12f).Chest(-26f).Head(12f).Feet(0.36f, -0.26f).HandN(0.74f, 1.22f, -10f).HandF(0.1f, 0.98f, 10f), Ease.OutExpo)
                .Key(19, P().Hip(0.12f, -0.14f, -12f).Chest(-26f).Head(12f).Feet(0.36f, -0.26f).HandN(0.72f, 1.22f, -10f).HandF(0.1f, 0.98f, 10f))
                .Key(40, Guard(), Ease.InOutQuad)
                .Move(8, 14, 1.6f)
                .Hit(12, 16, Fist, 10, HitLevel.Mid, new Vector2(7.0f, 1.4f), stun: 26, hitstop: 9, shake: 0.3f, knockdown: true, heavy: true, radius: 0.18f, postureMul: 1.3f)
                .Fx(1, 11, FxKind.ChargeDark).Fx(11, 17, FxKind.TrailFist).Fx(12, 12, FxKind.Shockwave);
            m.shadowPalm.meterCost = 25;

            // ===================== AIR =====================
            // Axe Handle: the fists clasped overhead and driven down: a spike that bounces the foe off the floor
            m.airSlam = new MoveDef("fists.airSlam", "Axe Handle", 38)
                .Key(4, P().Hip(0f, 0f, 0f).Chest(8f).Head(-4f).LegN(60f, -90f).LegF(40f, -100f).HandN(0.16f, 1.85f, 60f).HandF(0.06f, 1.78f, 60f))
                .Key(9, P().Hip(0f, 0f, 0f).Chest(-30f).Head(16f).LegN(60f, -90f).LegF(40f, -100f).HandN(0.62f, 0.8f, -50f).HandF(0.55f, 0.74f, -50f), Ease.OutExpo)
                .Key(38, P().Hip(0f, 0f, 0f).Chest(-24f).Head(12f).LegN(50f, -80f).LegF(30f, -90f).HandN(0.6f, 0.82f, -50f).HandF(0.52f, 0.76f, -50f))
                .Hit(7, 30, BothFists, 10, HitLevel.Overhead, new Vector2(1.6f, -2.2f), stun: 24, hitstop: 8, shake: 0.22f, knockdown: true, heavy: true, radius: 0.13f)
                .Fx(6, 14, FxKind.TrailFist).Air();

            return m;
        }

        public override MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self)
        {
            if (air) return b switch { Btn.P => _k.airPunch, Btn.K => _k.airKick, Btn.S => airSlam, _ => null };
            switch (b)
            {
                case Btn.P:
                case Btn.K:
                    return _k.Select(b, fwd, dy, false, self);
                case Btn.S:
                    if (dy < 0) return quake;
                    if (fwd > 0) return grapple;
                    if (fwd < 0) return ironGuard;
                    return haymaker;
                case Btn.X:
                    return shadowPalm;
            }
            return null;
        }

        /// <summary>
        /// What plays in place of a katana move the shared fist techniques chain into: the Pommel Strike is the far-fist
        /// cross, and the Iai Draw (a jab or roundhouse's Weapon chain) is the Iron Haymaker.
        /// </summary>
        public override MoveDef BareHanded(MoveDef m) => m == _k.iai ? haymaker : _k.BareHanded(m);

        public override IEnumerable<MoveDef> All => new[]
        {
            _k.jab, _k.crossFist, _k.uppercut, _k.crouchPunch, _k.lunge, _k.kick1, _k.kick2, _k.axeKick, _k.sweep, _k.pushKick,
            haymaker, grapple, grappleWhiff, quake, ironGuard, shadowPalm,
            throwGrab, throwWhiff, _k.airKick, _k.airPunch, airSlam, roll, backflip, _k.groundPound, getUp, getUpFront,
        };
    }
}
