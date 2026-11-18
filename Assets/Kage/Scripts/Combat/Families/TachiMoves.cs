using System.Collections.Generic;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// The heavy tachi family (ADR-0006, SAN-81): Raikō no Tachi, and the same blade in Raikon's hands (<c>raikon_tachi</c>).
    /// A two-handed odachi with the longest blade of the families and the highest posture damage: slow to start, every
    /// cut heavy. The hands sit a hand-and-a-half apart on the long hilt (<see cref="Pose.HeavySpacing"/>).
    /// <para>
    /// Weapon button: Broad Cut (a wide horizontal sweep), chained into the Kesa Cut (a diagonal cut) and Thunder Fall (the
    /// overhead cleave, armoured, with a shockwave); Lightning Thrust forward (the one lunging thrust, armoured), Low Sweep
    /// down, Redline Guard back (no blow, armour frames). Punch: hilt strikes (Pommel Strike, Rising Hilt, Step Pommel,
    /// Low Pommel, and Earthsplit, a downward stab into a downed foe). Kick: kicks with the long blade carried behind
    /// (Front Kick, Push Kick, Low Kick, Heel Drop). Air: Falling Hilt, Flying Kick, Falling Thunder (a diving overhead
    /// cut). Plain Special does nothing yet. The body moves (roll, backflip, get-ups, throw) are the katana set's, by reference.
    /// </para>
    /// <para>
    /// Every blade window lists <see cref="HitPoint.Blade"/> (clash eligible) and carries a posture multiplier
    /// (<see cref="HitWindow.postureMul"/>) of 2.2 to 3.0, against 1.4 at most elsewhere. Raikon's named abilities
    /// (ADR-0008, SAN-107) are added with <see cref="AddAbility"/> and found through <see cref="Ability"/>.
    /// </para>
    /// </summary>
    public sealed class TachiMoves : MoveSet
    {
        public MoveDef pommel, hiltRise, stepPommel, lowPommel, groundStab, kick, pushKick, lowKick, heelDrop;
        public MoveDef cut1, cut2, overhead, thrust, sweep, guard;
        public MoveDef airHilt, airKick, airCleave;

        /// <summary>Metres from the guard to the tip: the longest blade of the families (the katana's is 0.72, the spear's 1.45).</summary>
        public const float BladeLength = 1.5f;

        public static readonly WeaponFamilyDef Family = new()
        {
            id = WeaponFamilies.Tachi,
            referenceWeapon = "raiko_no_tachi",
            build = sets => Build((KatanaMoves)sets.Get(WeaponFamilies.Katana)),
            grip = GripPose.HeavyTwoHand,
            bladeLength = BladeLength,
            rageArt = Fighter.ThousandShadows,
            visual = new TachiVisual(),
            keepPointOffFloor = true,
        };

        static readonly HitPoint[] Blade = { HitPoint.Blade };
        static readonly HitPoint[] Hilt = { HitPoint.Hilt };
        static readonly HitPoint[] FootN = { HitPoint.FootN, HitPoint.ShinN };
        static readonly HitPoint[] FootTip = { HitPoint.FootN };

        readonly Dictionary<string, MoveDef> _abilities = new();

        static Pose P() => new Pose();

        /// <summary>Both hands on the hilt: the lead hand's wrist at (x, y), the blade pointing <paramref name="blade"/> degrees.</summary>
        static Pose Hold(Pose p, float x, float y, float blade) => p.HandF(x, y).Blade(blade).Grip();

        /// <summary>The long blade carried back over the shoulder in the lead hand while a leg (or the other hand) works.</summary>
        static Pose Carry(Pose p, float x = 0.1f, float y = 1.0f) => p.HandF(x, y).Blade(158f).HandN(0.2f, 1.24f, 10f);

        /// <summary>A lunging body for the thrusts and big cuts.</summary>
        static Pose Lunge(float hx = 0.1f, float hy = -0.12f, float feetN = 0.4f, float feetF = -0.26f) =>
            P().Hip(hx, hy, -12f).Chest(-24f).Head(12f).Feet(feetN, feetF);

        static Pose LowBody(float hx = 0f) => P().Hip(hx, -0.3f, 0f).Chest(-24f).Head(14f).FootN(0.3f, 0.13f).FootF(-0.26f, 0.15f, -25f);

        TachiMoves(KatanaMoves katana) { ShareBodyMoves(katana); }

        public static TachiMoves Build(KatanaMoves katana)
        {
            var m = new TachiMoves(katana);

            // ===================== WEAPON BUTTON: THE CUTS =====================
            // Broad Cut: the blade drawn back low behind the hip, then swept round in one wide arc to level in front
            m.cut1 = new MoveDef("tachi.cut1", "Broad Cut", 48)
                .Key(11, Hold(P().Hip(-0.08f, -0.16f, 6f).Chest(8f).Head(-2f).Feet(0.22f, -0.32f), -0.16f, 1.02f, -162f), Ease.InOutQuad)
                .Key(15, Hold(Lunge(0.1f, -0.12f, 0.4f, -0.26f), 0.5f, 1.12f, 4f), Ease.OutExpo)
                .Key(22, Hold(Lunge(0.1f, -0.12f, 0.4f, -0.26f), 0.46f, 1.24f, 22f))
                .Key(48, Stance(), Ease.InOutQuad)
                .Hit(14, 20, Blade, 16, HitLevel.Mid, new Vector2(3.4f, 0.5f), stun: 26, hitstop: 10, shake: 0.3f, heavy: true, radius: 0.09f, postureMul: 2.2f)
                .Move(12, 18, 1.6f)
                .Fx(12, 22, FxKind.TrailBlade).Fx(12, 12, FxKind.Dust).Sword().Cancel(23);

            // Kesa Cut: raised behind the shoulder, then drawn down across the foe on the diagonal
            m.cut2 = new MoveDef("tachi.cut2", "Kesa Cut", 50)
                .Key(11, Hold(P().Hip(-0.04f, -0.12f, 6f).Chest(6f).Head(-4f).Feet(0.26f, -0.28f), -0.02f, 1.44f, 146f), Ease.InOutQuad)
                .Key(15, Hold(Lunge(0.1f, -0.14f, 0.4f, -0.28f), 0.5f, 1.1f, -8f), Ease.OutExpo)
                .Key(23, Hold(Lunge(0.1f, -0.14f, 0.4f, -0.28f), 0.46f, 1.0f, -22f))
                .Key(50, Stance(), Ease.InOutQuad)
                .Hit(14, 21, Blade, 18, HitLevel.Mid, new Vector2(2.6f, 0.2f), stun: 28, hitstop: 11, shake: 0.34f, heavy: true, radius: 0.09f, postureMul: 2.4f)
                .Move(12, 18, 1.8f)
                .Fx(12, 22, FxKind.TrailBlade).Sword().Cancel(25);

            // Thunder Fall: the blade lifted high over the head, then brought down through the foe with the whole body.
            // Armoured from the lift to the end of the cut; a shockwave where it lands.
            m.overhead = new MoveDef("tachi.overhead", "Thunder Fall", 66)
                .Key(14, Jodan(P().Hip(0f, -0.06f, 4f).Chest(10f).Head(-8f).Feet(0.28f, -0.3f)).HandF(0.04f, 1.68f).Blade(112f), Ease.InOutQuad)
                .Key(22, Hold(P().Hip(0.1f, -0.16f, -12f).Chest(-30f).Head(16f).Feet(0.42f, -0.28f), 0.5f, 1.0f, -50f), Ease.InCubic)
                .Key(32, Hold(P().Hip(0.1f, -0.2f, -14f).Chest(-34f).Head(18f).Feet(0.44f, -0.28f), 0.5f, 0.82f, -46f))
                .Key(66, Stance(), Ease.InOutQuad)
                .Move(10, 20, 1.5f)
                .Hit(20, 28, Blade, 28, HitLevel.Overhead, new Vector2(1.8f, -3.2f), stun: 34, hitstop: 14, shake: 0.55f, knockdown: true, heavy: true, radius: 0.1f, postureMul: 3.0f)
                .Armour(8, 30)
                .Fx(14, 29, FxKind.TrailBlade).Fx(24, 24, FxKind.Shockwave).Fx(24, 24, FxKind.Dust).Sword();

            // Lightning Thrust: the blade drawn back to the hip, then driven out at the end of a long lunging step
            m.thrust = new MoveDef("tachi.thrust", "Lightning Thrust", 58)
                .Key(11, Hold(P().Hip(-0.06f, -0.2f, -4f).Chest(0f).Head(4f).Feet(0.28f, -0.3f), -0.04f, 1.0f, 8f), Ease.InOutQuad)
                .Key(16, Hold(Lunge(0.18f, -0.16f, 0.46f, -0.32f).Chest(-28f), 0.54f, 1.04f, 10f), Ease.OutExpo)
                .Key(26, Hold(Lunge(0.18f, -0.16f, 0.46f, -0.32f).Chest(-28f), 0.52f, 1.04f, 10f))
                .Key(58, Stance(), Ease.InOutQuad)
                .Move(11, 21, 5.0f)
                .Hit(15, 23, Blade, 22, HitLevel.Mid, new Vector2(3.8f, 0.4f), stun: 30, hitstop: 12, shake: 0.4f, heavy: true, radius: 0.08f, postureMul: 2.6f)
                .Armour(7, 24)
                .Fx(12, 23, FxKind.AfterImage).Fx(13, 24, FxKind.TrailBlade).Fx(12, 12, FxKind.Dust).Sword();

            // Low Sweep: the blade carried low round the shins
            m.sweep = new MoveDef("tachi.sweep", "Low Sweep", 54)
                .Key(12, Hold(P().Hip(-0.04f, -0.4f, 0f).Chest(-26f).Head(14f).FootN(0.22f, 0.13f).FootF(-0.3f, 0.15f, -30f), -0.1f, 0.66f, 176f), Ease.InOutQuad)
                .Key(17, Hold(P().Hip(0.06f, -0.42f, -6f).Chest(-32f).Head(18f).FootN(0.32f, 0.13f).FootF(-0.3f, 0.15f, -30f), 0.5f, 0.54f, -6f), Ease.OutExpo)
                .Key(24, Hold(P().Hip(0.06f, -0.42f, -6f).Chest(-32f).Head(18f).FootN(0.32f, 0.13f).FootF(-0.3f, 0.15f, -30f), 0.5f, 0.52f, -2f))
                .Key(54, Stance(), Ease.InOutQuad)
                .Hit(16, 23, Blade, 16, HitLevel.Low, new Vector2(0.6f, 3.4f), stun: 24, hitstop: 8, shake: 0.2f, knockdown: true, otg: true, radius: 0.08f, postureMul: 2.2f)
                .Fx(13, 23, FxKind.TrailBlade).Fx(17, 17, FxKind.Dust).Sword();

            // Redline Guard: the long blade held up across the line of attack. No hit window; armour frames
            // (the guard and counter mechanics are SAN-86, and Raikon's Redline Counter is SAN-107's)
            m.guard = new MoveDef("tachi.guard", "Redline Guard", 38)
                .Key(6, Hold(P().Hip(-0.03f, -0.1f, -2f).Chest(-2f).Head(-4f).Feet(0.2f, -0.28f), 0.26f, 1.24f, 78f))
                .Key(28, Hold(P().Hip(-0.03f, -0.11f, -2f).Chest(-2f).Head(-4f).Feet(0.2f, -0.28f), 0.26f, 1.23f, 80f))
                .Key(38, Stance(), Ease.InOutQuad)
                .Armour(6, 26)
                .Fx(5, 5, FxKind.Glint).Sword();

            // chains: the three cuts in order, each heavier than the last
            m.cut1.chain[Btn.S] = m.cut2;
            m.cut2.chain[Btn.S] = m.overhead;

            // ===================== PUNCH BUTTON: HILT STRIKES =====================
            // Pommel Strike: the blade swung back, the far hand driving the hilt into the foe
            m.pommel = new MoveDef("tachi.pommel", "Pommel Strike", 26)
                .Key(5, Stance().Chest(-4f).HandF(0.02f, 1.0f).Blade(170f))
                .Key(9, Hold(P().Hip(0.09f, -0.09f, -8f).Chest(-26f).Head(14f).Feet(0.33f, -0.2f), 0.56f, 1.16f, 178f), Ease.OutExpo)
                .Key(13, Hold(P().Hip(0.09f, -0.09f, -8f).Chest(-26f).Head(14f).Feet(0.33f, -0.2f), 0.55f, 1.16f, 178f))
                .Key(26, Stance(), Ease.InOutQuad)
                .Hit(8, 11, Hilt, 6, HitLevel.High, new Vector2(1.4f, 0f), stun: 16, hitstop: 5, shake: 0.08f)
                .Sword().Cancel(11);

            // Rising Hilt: the hilt driven up under the chin
            m.hiltRise = new MoveDef("tachi.hiltRise", "Rising Hilt", 32)
                .Key(6, Hold(P().Hip(0.04f, -0.2f, -4f).Chest(-20f).Head(12f).Feet(0.3f, -0.24f), 0.28f, 0.72f, 168f))
                .Key(10, Hold(P().Hip(0.12f, -0.04f, 2f).Chest(6f).Head(-4f).FootN(0.36f, 0.13f).FootF(-0.15f, 0.15f, -20f), 0.42f, 1.5f, 166f), Ease.OutExpo)
                .Key(14, Hold(P().Hip(0.1f, -0.04f, 2f).Chest(8f).Head(-6f).FootN(0.34f, 0.13f).FootF(-0.17f, 0.15f, -20f), 0.4f, 1.58f, 164f))
                .Key(32, Stance(), Ease.InOutQuad)
                .Hit(8, 13, Hilt, 7, HitLevel.Mid, new Vector2(0.7f, 2.6f), stun: 24, hitstop: 7, shake: 0.18f, radius: 0.11f)
                .Move(2, 9, 1.6f)
                .Sword().Cancel(14);

            m.pommel.chain[Btn.P] = m.hiltRise;
            m.pommel.chain[Btn.S] = m.cut1;
            m.hiltRise.chain[Btn.S] = m.overhead;

            // Step Pommel: a stepping hilt strike (the shared throw when the foe is close enough to be thrown)
            m.stepPommel = new MoveDef("tachi.stepPommel", "Step Pommel", 28)
                .Key(5, Hold(Stance().Chest(-6f), 0.08f, 1.0f, 172f))
                .Key(9, Hold(Lunge(0.1f, -0.1f, 0.38f, -0.18f), 0.58f, 1.18f, 176f), Ease.OutExpo)
                .Key(14, Hold(Lunge(0.1f, -0.1f, 0.38f, -0.18f), 0.57f, 1.18f, 176f))
                .Key(28, Stance(), Ease.InOutQuad)
                .Move(3, 10, 3.2f)
                .Hit(8, 12, Hilt, 7, HitLevel.High, new Vector2(1.8f, 0.2f), stun: 16, hitstop: 5, shake: 0.1f)
                .Fx(3, 3, FxKind.Dust).Sword().Cancel(12);
            m.stepPommel.chain[Btn.S] = m.cut1;

            // Low Pommel: crouched, the hilt driven out low
            m.lowPommel = new MoveDef("tachi.lowPommel", "Low Pommel", 24)
                .Key(5, Hold(Crouch(), 0.2f, 0.74f, 172f))
                .Key(8, Hold(LowBody(0.04f), 0.56f, 0.8f, 176f), Ease.OutExpo)
                .Key(11, Hold(LowBody(0.04f), 0.55f, 0.8f, 176f))
                .Key(24, Crouch(), Ease.InOutQuad)
                .Hit(7, 10, Hilt, 5, HitLevel.Mid, new Vector2(0.8f, 0f), stun: 12, hitstop: 4, shake: 0.05f)
                .Sword().Cancel(10);
            m.lowPommel.chain[Btn.P] = m.hiltRise;

            // Earthsplit: against a downed foe, the point driven straight down into it
            m.groundStab = new MoveDef("tachi.groundStab", "Earthsplit", 36)
                .Key(8, Hold(P().Hip(0f, -0.1f, 0f).Chest(8f).Head(-4f).Feet(0.28f, -0.26f), 0.26f, 1.5f, 76f))
                .Key(15, Hold(P().Hip(0.1f, -0.2f, -10f).Chest(-34f).Head(16f).Feet(0.36f, -0.28f), 0.62f, 1.12f, -54f), Ease.InCubic)
                .Key(22, Hold(P().Hip(0.1f, -0.2f, -10f).Chest(-34f).Head(16f).Feet(0.36f, -0.28f), 0.62f, 1.1f, -54f))
                .Key(36, Stance(), Ease.InOutQuad)
                .Hit(14, 21, Blade, 10, HitLevel.Mid, new Vector2(0.4f, 1.0f), stun: 20, hitstop: 8, shake: 0.22f, otg: true, radius: 0.09f, postureMul: 1.6f)
                .Fx(12, 21, FxKind.TrailBlade).Fx(16, 16, FxKind.Dust).Sword();

            // ===================== KICK BUTTON =====================
            // the long blade rides behind in the lead hand while a leg does the work
            m.kick = new MoveDef("tachi.kick", "Front Kick", 28)
                .Key(7, Carry(P().Hip(0.02f, -0.07f, 10f).Chest(8f).Head(-4f).LegN(98f, -118f, 10f).FootF(-0.16f, 0.13f)))
                .Key(11, Carry(P().Hip(0.12f, -0.04f, 18f).Chest(14f).Head(-8f).LegN(104f, -6f, 20f).FootF(-0.14f, 0.13f), 0.08f, 1.02f), Ease.OutExpo)
                .Key(15, Carry(P().Hip(0.12f, -0.04f, 18f).Chest(14f).Head(-8f).LegN(102f, -8f, 20f).FootF(-0.14f, 0.13f), 0.08f, 1.02f))
                .Key(28, Stance(), Ease.InOutQuad)
                .Hit(10, 14, FootN, 5, HitLevel.Mid, new Vector2(1.6f, 0.4f), stun: 16, hitstop: 5, shake: 0.1f, radius: 0.1f)
                .Fx(9, 15, FxKind.TrailFootN).Cancel(14);
            m.kick.chain[Btn.S] = m.cut1;
            m.pommel.chain[Btn.K] = m.kick;

            m.pushKick = new MoveDef("tachi.pushKick", "Push Kick", 30)
                .Key(7, Carry(P().Hip(0f, -0.06f, 8f).Chest(8f).LegN(100f, -120f, 15f).FootF(-0.2f, 0.13f)))
                .Key(11, Carry(P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(96f, -4f, 40f).FootF(-0.14f, 0.13f), 0.08f, 1.02f), Ease.OutExpo)
                .Key(15, Carry(P().Hip(0.12f, -0.04f, 16f).Chest(16f).Head(-8f).LegN(95f, -5f, 40f).FootF(-0.14f, 0.13f), 0.08f, 1.02f))
                .Key(30, Stance(), Ease.InOutQuad)
                .Hit(10, 14, FootTip, 5, HitLevel.Mid, new Vector2(4.2f, 0.6f), stun: 20, hitstop: 6, shake: 0.15f, radius: 0.11f)
                .Fx(9, 14, FxKind.TrailFootN);

            m.lowKick = new MoveDef("tachi.lowKick", "Low Kick", 26)
                .Key(6, Carry(P().Hip(0.0f, -0.32f, 4f).Chest(-14f).Head(10f).LegN(80f, -100f, 10f).FootF(-0.26f, 0.15f, -25f), 0.14f, 0.88f))
                .Key(9, Carry(P().Hip(0.06f, -0.3f, 8f).Chest(-8f).Head(8f).LegN(92f, -4f, 20f).FootF(-0.26f, 0.15f, -25f), 0.12f, 0.9f), Ease.OutExpo)
                .Key(13, Carry(P().Hip(0.06f, -0.3f, 8f).Chest(-8f).Head(8f).LegN(90f, -6f, 20f).FootF(-0.26f, 0.15f, -25f), 0.12f, 0.9f))
                .Key(26, Crouch(), Ease.InOutQuad)
                .Hit(8, 12, FootN, 4, HitLevel.Low, new Vector2(0.9f, 0f), stun: 14, hitstop: 4, shake: 0.06f, radius: 0.1f)
                .Fx(7, 12, FxKind.TrailFootN).Cancel(12);

            // Heel Drop (back + kick): the leg raised and brought down on the foe
            m.heelDrop = new MoveDef("tachi.heelDrop", "Heel Drop", 38)
                .Key(9, Carry(P().Hip(0.02f, -0.04f, 22f).Chest(20f).Head(-12f).LegN(160f, -10f).FootF(-0.14f, 0.15f, -15f), 0.0f, 1.0f), Ease.OutCubic)
                .Key(12, Carry(P().Hip(0.08f, -0.02f, 24f).Chest(22f).Head(-14f).LegN(172f, -8f).FootF(-0.12f, 0.16f, -20f), 0.0f, 1.0f))
                .Key(16, Carry(P().Hip(0.16f, -0.12f, 0f).Chest(-18f).Head(10f).LegN(70f, -5f).FootF(-0.14f, 0.13f), 0.1f, 0.95f), Ease.InCubic)
                .Key(24, Carry(P().Hip(0.16f, -0.14f, -2f).Chest(-15f).Head(8f).FootN(0.48f, 0.13f).FootF(-0.14f, 0.13f), 0.1f, 0.95f))
                .Key(38, Stance(), Ease.InOutQuad)
                .Hit(14, 17, FootN, 9, HitLevel.Overhead, new Vector2(1.0f, -1.5f), stun: 26, hitstop: 9, shake: 0.3f, knockdown: true, heavy: true, radius: 0.11f)
                .Fx(10, 17, FxKind.TrailFootN).Fx(17, 17, FxKind.Dust);

            // ===================== AIR =====================
            m.airHilt = new MoveDef("tachi.airHilt", "Falling Hilt", 32)
                .Key(4, Hold(P().Hip(0f, 0f, 0f).Chest(8f).LegN(60f, -90f).LegF(40f, -100f), 0.16f, 1.46f, 172f))
                .Key(8, Hold(P().Hip(0f, 0f, 0f).Chest(-20f).Head(10f).LegN(60f, -90f).LegF(40f, -100f), 0.52f, 1.06f, 178f), Ease.OutExpo)
                .Key(32, Hold(P().Hip(0f, 0f, 0f).Chest(-14f).Head(8f).LegN(50f, -70f).LegF(30f, -80f), 0.46f, 1.08f, 176f))
                .Hit(7, 13, Hilt, 6, HitLevel.Overhead, new Vector2(1.4f, 0.5f), stun: 16, hitstop: 5, shake: 0.1f)
                .Sword().Air();

            m.airKick = new MoveDef("tachi.airKick", "Flying Kick", 40)
                .Key(5, P().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(70f, -4f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).HandF(-0.15f, 1.1f).Blade(-170f), Ease.OutExpo)
                .Key(40, P().Hip(0f, 0f, -12f).Chest(18f).Head(-8f).LegN(68f, -6f, 10f).LegF(95f, -135f).HandN(-0.05f, 1.3f).HandF(-0.15f, 1.1f).Blade(-170f))
                .Hit(4, 30, FootN, 8, HitLevel.Overhead, new Vector2(2.4f, 1.8f), stun: 20, hitstop: 7, shake: 0.2f, knockdown: true, radius: 0.11f)
                .Fx(3, 12, FxKind.TrailFootN).Air();

            // Falling Thunder: the blade raised, then brought down in a long diving cut
            m.airCleave = new MoveDef("tachi.airCleave", "Falling Thunder", 44)
                .Key(7, Hold(P().Hip(0f, 0f, -4f).Chest(6f).Head(-6f).LegN(80f, -110f).LegF(60f, -100f), 0.1f, 1.6f, 112f), Ease.InOutQuad)
                .Key(11, Hold(P().Hip(0f, 0f, -8f).Chest(-12f).Head(6f).LegN(80f, -110f).LegF(60f, -100f), 0.44f, 1.2f, -56f), Ease.OutExpo)
                .Key(44, Hold(P().Hip(0f, 0f, -8f).Chest(-12f).Head(6f).LegN(78f, -108f).LegF(58f, -98f), 0.44f, 1.2f, -56f))
                .Hit(10, 34, Blade, 18, HitLevel.Overhead, new Vector2(2.0f, 1.6f), stun: 22, hitstop: 9, shake: 0.28f, knockdown: true, heavy: true, radius: 0.09f, postureMul: 2.6f)
                .Fx(8, 34, FxKind.TrailBlade).Sword().Air();

            return m;
        }

        /// <summary>Picks a technique from button + direction (relative to facing) + context.</summary>
        public override MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self)
        {
            if (air) return b switch { Btn.P => airHilt, Btn.K => airKick, Btn.S => airCleave, _ => null };
            Fighter opp = self.opponent;
            float dist = opp != null ? Mathf.Abs(opp.transform.position.x - self.transform.position.x) : 99f;
            bool oppDown = opp != null && opp.state == FState.Down;
            switch (b)
            {
                case Btn.P:
                    if (dy < 0) return oppDown && dist < 1.7f ? groundStab : lowPommel;
                    if (fwd > 0) return dist < 0.85f && opp != null && opp.CanBeThrown ? throwGrab : stepPommel;
                    return pommel;
                case Btn.K:
                    if (dy < 0) return lowKick;
                    if (fwd > 0) return pushKick;
                    if (fwd < 0) return heelDrop;
                    return kick;
                case Btn.S:
                    if (dy < 0) return sweep;
                    if (fwd > 0) return thrust;
                    if (fwd < 0) return guard;
                    return cut1;
            }
            return null;
        }

        /// <summary>A boss ability move of this set (ADR-0008), added with <see cref="AddAbility"/>; null when the set has none by that id.</summary>
        public override MoveDef Ability(string abilityId) => abilityId != null && _abilities.TryGetValue(abilityId, out var m) ? m : null;

        /// <summary>Registers an ability move (<c>raikon.thunderSwing</c>, ...). SAN-107 builds its moves from this set's poses and adds them here.</summary>
        public void AddAbility(string abilityId, MoveDef move) => _abilities[abilityId] = move;

        public override IEnumerable<MoveDef> All
        {
            get
            {
                var list = new List<MoveDef>
                {
                    pommel, hiltRise, stepPommel, lowPommel, groundStab, kick, pushKick, lowKick, heelDrop,
                    cut1, cut2, overhead, thrust, sweep, guard,
                    airHilt, airKick, airCleave,
                    throwGrab, throwWhiff, roll, backflip, getUp, getUpFront,
                };
                list.AddRange(_abilities.Values);
                return list;
            }
        }
    }
}
