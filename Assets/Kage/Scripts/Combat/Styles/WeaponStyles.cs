using System.Collections.Generic;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// The shop weapons' attack patterns (docs/weapon-styles.md). Each weapon wins a different way:
    /// <list type="table">
    /// <item><b>Kagetsu</b>, Kenjutsu: the balanced reference. Guard or parry, then strike: the Iai Counter cancels the guard.</item>
    /// <item><b>Shizuka</b>, Iaijutsu: four draws from the scabbard on the Weapon button; the last sheathes the blade and the silent
    /// cut opens a beat later. Its own Iai Counter leaves a smaller silent cut.</item>
    /// <item><b>Tsubame</b>, Twin Storm: the fastest feet; every landed blow speeds up the next (momentum), and a landed blade
    /// blow can be dash-cancelled.</item>
    /// <item><b>Onikiri</b>, Oni Cleaver: the slowest feet and heavy steps; every Weapon swing is armoured against light blows, and
    /// a guarded blow chips through the guard and shoves it back.</item>
    /// <item><b>Mizuchi</b>, Tide Spear: spacing. The outer third of the reach is the sweet spot (more damage, more push);
    /// a foe inside the shaft takes less.</item>
    /// <item><b>Yamigama</b>, Reaper Chain (legendary): hook and reap. A blade blow marks the foe; the next one spends the mark for more
    /// damage and drags the foe in.</item>
    /// <item><b>Iwakudaki</b>, Earthbreaker (legendary): the war hammer's smash string splits the ground, and every smash shakes the floor,
    /// stunning the grounded foes near it.</item>
    /// <item><b>Mumei</b>, Plain Steel (the common starter): the katana set with no trait at all and the basic rage art.</item>
    /// </list>
    /// Foe weapons have no style: they fight exactly as their family does.
    /// </summary>
    public static class WeaponStyles
    {
        static Dictionary<string, WeaponStyle> _byWeapon;

        /// <summary>The style of a weapon, or null (a foe weapon, bare hands, an unknown id).</summary>
        public static WeaponStyle Of(string weaponId)
        {
            if (weaponId == null) return null;
            if (_byWeapon == null) Build();
            return _byWeapon.TryGetValue(weaponId, out var s) ? s : null;
        }

        /// <summary>Every registered style, in Armory order.</summary>
        public static IEnumerable<WeaponStyle> All
        {
            get { if (_byWeapon == null) Build(); return _order; }
        }

        static readonly List<WeaponStyle> _order = new();

        static void Add(WeaponStyle s) { _byWeapon[s.weaponId] = s; _order.Add(s); }

        static void Build()
        {
            _byWeapon = new Dictionary<string, WeaponStyle>();
            _order.Clear();

            Add(new WeaponStyle
            {
                // the plain starter (common): no trait, no trail, the basic rage art. Every other katana is measured against it in feel.
                weaponId = "mumei", name = "Plain Steel", summary = "The katana and nothing else: every cut is the family's own, the rage art is the basic Thousand Shadows.",
                rageArt = Fighter.ThousandShadows,
            });

            Add(new WeaponStyle
            {
                weaponId = "kagetsu", name = "Kenjutsu", summary = "Balanced cuts. Guard or parry, then strike: the Iai Counter cancels the guard.",
                footwork = Footwork.Crimson,
                riposteFrames = 45, riposte = KagetsuCounter,          // the JS 0.75 s iai counter window after a block or parry
                rageArt = Fighter.CrimsonMoon,
            });

            Add(new WeaponStyle
            {
                weaponId = "shizuka", name = "Iaijutsu", summary = "Four draws from the scabbard; the last sheathes the blade and the silent cut opens a beat later.",
                footwork = Footwork.Violet,
                riposteFrames = 45, riposte = ShizukaMoves.Counter,
                echoes = new[]
                {
                    new EchoMove { moveId = ShizukaMoves.NotoId, sheathFrame = ShizukaMoves.NotoSheathFrame, damage = 12 },
                    new EchoMove { moveId = ShizukaMoves.CounterId, sheathFrame = ShizukaMoves.CounterSheathFrame, damage = 6 },
                },
                echoDelay = 18,                                          // JS 0.3 s
                buildMoves = katana => new ShizukaMoves((KatanaMoves)katana),
                rageArt = Fighter.SilentMoon,
            });

            Add(new WeaponStyle
            {
                weaponId = "tsubame", name = "Twin Storm", summary = "The fastest feet; every landed blow speeds up the next, and a landed cut can dash-cancel.",
                moveMul = 1.12f, dashMul = 1.15f, footwork = Footwork.Lightning,
                tempoPerHit = 0.06f, tempoMax = 0.36f, tempoHoldFrames = 50,
                dashCancelOnHit = true,
                postureMul = 0.75f,                                      // many light blows, each a little posture
                rageArt = Fighter.SwallowStorm,
            });

            Add(new WeaponStyle
            {
                weaponId = "onikiri", name = "Oni Cleaver", summary = "Slow feet, heavy steps; every swing is armoured and a guarded blow still crushes through.",
                moveMul = 0.84f, dashMul = 0.85f, footwork = Footwork.Flame, heavyStep = true,
                armouredSwings = true,
                guardCrushChip = 0.25f, guardCrushPush = 1.7f,
                postureMul = 1.35f,
                rageArt = Fighter.HellSplitter,
            });

            Add(new WeaponStyle
            {
                weaponId = "mizuchi", name = "Tide Spear", summary = "Spacing: the outer third of the reach is the sweet spot; a foe inside the shaft takes less.",
                moveMul = 0.96f, footwork = Footwork.Water,
                tipZone = 0.34f, tipDamage = 1.35f, tipKnockback = 1.45f,
                innerZone = 0.3f, innerDamage = 0.8f,
                rageArt = Fighter.WaterDragon,
            });

            Add(new WeaponStyle
            {
                weaponId = "yamigama", name = "Reaper Chain", summary = "Hook and reap: a cut marks the foe, the next one spends the mark, cuts deeper and drags the foe in.",
                footwork = Footwork.Shadow,
                hookFrames = 90, hookDamage = 1.4f, hookPull = 5.5f,
                legendary = true,
                rageArt = Fighter.ReaperRequiem,
            });

            Add(new WeaponStyle
            {
                weaponId = "iwakudaki", name = "Earthbreaker", summary = "The slowest and the heaviest: a crushing three-blow hammer string and Meteor Sunder that split the ground and shake foes standing near.",
                moveMul = 0.78f, dashMul = 0.8f, footwork = Footwork.Earth, heavyStep = true,
                postureMul = 1.5f,
                quakes = new[]
                {
                    new QuakeMove { moveId = HammerMoves.SmashId, frame = HammerMoves.SmashImpact, ahead = 0.85f },
                    new QuakeMove { moveId = HammerMoves.MeteorSunderId, frame = HammerMoves.MeteorImpact, ahead = 0.95f },
                },
                quakeRadius = 2.4f, quakeStun = 22,
                buildMoves = cleaver => new HammerMoves((CleaverMoves)cleaver),
                legendary = true,
                rageArt = Fighter.Earthsplitter,
            });
        }

        // =====================================================================================
        //  KAGETSU'S IAI COUNTER
        // =====================================================================================
        public const string KagetsuCounterId = "kagetsu.iaiCounter";

        /// <summary>
        /// Kagetsu's Iai Counter (the JS <c>kc</c> counter draw: "block or parry, then strike to draw the iai counter"): out of a guard
        /// or a parry the blade comes out of waki in one lunging cut, fast (the blow lands on frame 3) and heavy, a guaranteed critical.
        /// </summary>
        public static readonly MoveDef KagetsuCounter = new MoveDef(KagetsuCounterId, "Iai Counter", 30)
            .Key(2, Waki(new Pose().Hip(0.02f, -0.18f, -8f).Chest(-14f).Head(10f).Feet(0.3f, -0.27f)).EdgeUp(), Ease.InQuad)
            .Key(5, new Pose().Hip(0.12f, -0.16f, -14f).Chest(-30f).Head(18f).Feet(0.42f, -0.28f).Grip().HandF(0.56f, 1.06f).Blade(6f).EdgeUp(), Ease.OutExpo)
            .Key(11, new Pose().Hip(0.12f, -0.16f, -14f).Chest(-30f).Head(18f).Feet(0.42f, -0.24f).Grip().HandF(0.5f, 1.16f).Blade(30f).EdgeUp())
            .Key(19, new Pose().Hip(0.06f, -0.14f, -8f).Chest(-16f).Head(9f).Feet(0.32f, -0.25f).Grip().HandF(0.4f, 0.94f).Blade(-28f), Ease.OutCubic)
            .Key(30, Stance(), Ease.InOutQuad)
            .Move(1, 5, 6.4f)
            .Hit(3, 7, new[] { HitPoint.Blade }, 13, HitLevel.Mid, new Vector2(2.4f, 1.4f), stun: 28, hitstop: 9, shake: 0.32f, heavy: true, radius: 0.1f)
            .Crit()
            .Invuln(0, 4).NoPush(1, 8)
            .Fx(1, 9, FxKind.TrailBlade).Fx(1, 6, FxKind.AfterImage).Fx(4, 4, FxKind.SlashLine).Sword();
    }
}
