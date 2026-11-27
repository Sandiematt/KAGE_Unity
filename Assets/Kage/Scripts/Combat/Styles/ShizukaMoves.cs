using System.Collections.Generic;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// Shizuka's iaijutsu (the JS <c>sz1</c> to <c>sz4</c>, <c>szc</c>; docs/weapon-styles.md): the katana set with its Weapon button
    /// played as four draws from the scabbard. Every other button is the katana set's own move, by reference.
    /// <list type="number">
    /// <item><b>Nukitsuke</b>: drawn one-handed from the hip straight into a level cut, the free hand holding the scabbard back.
    /// The fastest first blow of any weapon (it lands on frame 4).</item>
    /// <item><b>Kiriage</b>: both hands, the blade rising from the knee past the head; it lifts the foe.</item>
    /// <item><b>Tsuki</b>: the needle point driven straight through, twice.</item>
    /// <item><b>Noto</b>: one wide draw, then the blade slides home in the follow-through. Sheathing ends the string: if the draw
    /// landed, the silent cut opens on the foe a beat after the blade clicks shut (<see cref="WeaponStyle.echoes"/>).</item>
    /// </list>
    /// The Iai Counter (<see cref="Counter"/>) is the style's riposte: the fastest draw, straight out of a guard or a parry; it sheathes
    /// at once and leaves a small silent cut. Frames are authored (rate 1), as in every family.
    /// </summary>
    public sealed class ShizukaMoves : MoveSet
    {
        public const string NotoId = "shizuka.noto", CounterId = "shizuka.iaiCounter";
        /// <summary>The frame the Noto's blade clicks home (the silent cut counts from it).</summary>
        public const int NotoSheathFrame = 30;
        public const int CounterSheathFrame = 14;

        readonly KatanaMoves _katana;
        public readonly MoveDef draw1, draw2, draw3, noto;

        static readonly HitPoint[] Blade = { HitPoint.Blade };
        static Pose P() => new Pose();

        /// <summary>
        /// The blade in its scabbard and the right hand on the hilt at the left hip, the left hand on the saya's mouth: the iai
        /// ready. Root-space metres, facing right; the hands sit on <see cref="KatanaVisual"/>'s saya hilt.
        /// </summary>
        public static Pose Sheathed(Pose p) => p.HandF(0.25f, 0.9f).Blade(-158f).HandN(0.13f, 0.84f);

        static Pose ReadyBody() => P().Hip(-0.02f, -0.17f, -6f).Chest(-18f).Head(12f).Feet(0.28f, -0.28f);

        public ShizukaMoves(KatanaMoves katana)
        {
            _katana = katana;
            ShareBodyMoves(katana);
            counterSlash = katana.counterSlash;

            // 1. Nukitsuke: one-handed from the hip into a level cut, the near hand holding the scabbard back
            draw1 = new MoveDef("shizuka.nukitsuke", "Nukitsuke", 22)
                .Key(3, Sheathed(P().Hip(0f, -0.16f, -8f).Chest(-20f).Head(12f).Feet(0.3f, -0.27f)).HandF(0.2f, 0.9f).Blade(-148f), Ease.InOutQuad)
                .Key(5, P().Hip(0.1f, -0.12f, -12f).Chest(-22f).Head(12f).Feet(0.36f, -0.24f).HandF(0.62f, 1.12f).Blade(4f).HandN(-0.06f, 0.86f).EdgeUp(), Ease.OutExpo)
                .Key(9, P().Hip(0.1f, -0.12f, -12f).Chest(-22f).Head(12f).Feet(0.36f, -0.22f).HandF(0.6f, 1.16f).Blade(14f).HandN(-0.06f, 0.86f).EdgeUp())
                .Key(14, P().Hip(0.06f, -0.12f, -8f).Chest(-14f).Head(8f).Feet(0.32f, -0.24f).Grip().HandF(0.42f, 1.0f).Blade(-16f), Ease.InOutQuad)
                .Key(22, Stance(), Ease.InOutQuad)
                .Move(2, 6, 3.0f)
                .Hit(4, 8, Blade, 7, HitLevel.Mid, new Vector2(1.4f, 0.2f), stun: 16, hitstop: 4, shake: 0.1f, radius: 0.07f)
                .Fx(3, 10, FxKind.TrailBlade).Sword().Cancel(9);

            // 2. Kiriage: both hands, rising from the knee past the head
            draw2 = new MoveDef("shizuka.kiriage", "Kiriage", 24)
                .Key(2, P().Hip(0.02f, -0.28f, -4f).Chest(-26f).Head(14f).Feet(0.3f, -0.24f).Grip().HandF(0.24f, 0.6f).Blade(-48f).EdgeUp(), Ease.InOutQuad)
                .Key(6, P().Hip(0.1f, -0.06f, 2f).Chest(6f).Head(-4f).FootN(0.34f, 0.13f).FootF(-0.16f, 0.15f, -16f).Grip().HandF(0.34f, 1.46f).Blade(100f).EdgeUp(), Ease.OutExpo)
                .Key(10, P().Hip(0.1f, -0.06f, 2f).Chest(8f).Head(-6f).FootN(0.32f, 0.13f).FootF(-0.18f, 0.15f, -16f).Grip().HandF(0.26f, 1.54f).Blade(112f).EdgeUp())
                .Key(16, Jodan(P().Hip(0.06f, -0.06f, 0f).Chest(6f).Head(-4f).Feet(0.3f, -0.22f)), Ease.InOutQuad)
                .Key(24, Stance(), Ease.InOutQuad)
                .Move(1, 5, 2.0f)
                .Hit(3, 7, Blade, 7, HitLevel.Mid, new Vector2(0.5f, 3.4f), stun: 22, hitstop: 5, shake: 0.14f, radius: 0.08f)
                .Fx(2, 10, FxKind.TrailBlade).Sword().Cancel(10);

            // 3. Tsuki: the needle point driven straight through, twice
            draw3 = new MoveDef("shizuka.tsuki", "Tsuki", 28)
                .Key(3, P().Hip(-0.02f, -0.14f, -2f).Chest(-6f).Head(4f).Feet(0.26f, -0.28f).Grip().HandF(0.12f, 1.1f).Blade(2f), Ease.InOutQuad)
                .Key(6, P().Hip(0.14f, -0.14f, -12f).Chest(-26f).Head(14f).Feet(0.44f, -0.26f).Grip().HandF(0.66f, 1.12f).Blade(0f), Ease.OutExpo)
                .Key(9, P().Hip(0.08f, -0.14f, -8f).Chest(-14f).Head(8f).Feet(0.4f, -0.26f).Grip().HandF(0.28f, 1.1f).Blade(2f), Ease.InOutQuad)
                .Key(12, P().Hip(0.16f, -0.14f, -12f).Chest(-26f).Head(14f).Feet(0.46f, -0.26f).Grip().HandF(0.68f, 1.1f).Blade(0f), Ease.OutExpo)
                .Key(18, P().Hip(0.08f, -0.13f, -8f).Chest(-14f).Head(8f).Feet(0.38f, -0.25f).Grip().HandF(0.4f, 1.04f).Blade(-10f), Ease.InOutQuad)
                .Key(28, Stance(), Ease.InOutQuad)
                .Move(4, 7, 3.4f).Move(10, 13, 2.4f)
                .Hit(5, 7, Blade, 4, HitLevel.Mid, new Vector2(0.6f, 0.1f), stun: 18, hitstop: 3, shake: 0.08f, radius: 0.06f)
                .Hit(11, 13, Blade, 5, HitLevel.Mid, new Vector2(2.2f, 0.2f), stun: 20, hitstop: 6, shake: 0.16f, radius: 0.07f)
                .Fx(4, 14, FxKind.TrailBlade).Sword().Cancel(15);

            // 4. Noto: one wide draw, the chiburi flick, the blade sliding home (frame 30), stillness, and back to guard
            noto = new MoveDef(NotoId, "Noto", 56)
                .Key(4, Sheathed(P().Hip(-0.02f, -0.18f, -6f).Chest(-18f).Head(12f).Feet(0.28f, -0.28f)).HandF(0.2f, 0.9f).Blade(-152f), Ease.InOutQuad)
                .Key(8, P().Hip(0.12f, -0.14f, -14f).Chest(-28f).Head(16f).Feet(0.42f, -0.26f).HandF(0.62f, 1.04f).Blade(-4f).HandN(-0.04f, 0.86f).EdgeUp(), Ease.OutExpo)
                .Key(13, P().Hip(0.12f, -0.14f, -14f).Chest(-28f).Head(16f).Feet(0.42f, -0.22f).HandF(0.56f, 1.24f).Blade(36f).HandN(-0.04f, 0.86f).EdgeUp())
                .Key(18, P().Hip(0.08f, -0.16f, -10f).Chest(-20f).Head(12f).Feet(0.38f, -0.24f).HandF(0.5f, 0.9f).Blade(-40f).HandN(0f, 0.86f), Ease.OutCubic)
                .Key(28, P().Hip(0.02f, -0.16f, -6f).Chest(-16f).Head(10f).Feet(0.34f, -0.25f).HandF(0.3f, 0.92f).Blade(-162f).HandN(0.12f, 0.85f), Ease.InOutQuad)
                .Key(31, Sheathed(ReadyBody()))
                .Key(46, Sheathed(P().Hip(-0.02f, -0.15f, -5f).Chest(-16f).Head(10f).Feet(0.28f, -0.28f)))
                .Key(56, Stance(), Ease.InOutQuad)
                .Move(4, 8, 3.4f)
                .Hit(6, 10, Blade, 8, HitLevel.Mid, new Vector2(1.2f, 0.3f), stun: 24, hitstop: 6, shake: 0.16f, radius: 0.08f)
                .Sheath(NotoSheathFrame, 50)
                .Fx(5, 14, FxKind.TrailBlade).Fx(NotoSheathFrame, NotoSheathFrame, FxKind.Glint).Sword();

            draw1.chain[Btn.S] = draw2;
            draw2.chain[Btn.S] = draw3;
            draw3.chain[Btn.S] = noto;
        }

        /// <summary>
        /// The Iai Counter (JS <c>szc</c>): the fastest draw in the Armory, straight out of a guard or a parry (the blow lands on frame
        /// 2), sheathed again at once; it leaves a small silent cut. Shared by every Shizuka (a static move, like the rage arts' tracks).
        /// </summary>
        public static readonly MoveDef Counter = new MoveDef(CounterId, "Iai Counter", 32)
            .Key(2, Sheathed(P().Hip(0.02f, -0.18f, -8f).Chest(-16f).Head(10f).Feet(0.3f, -0.27f)).HandF(0.22f, 0.9f).Blade(-150f), Ease.InQuad)
            .Key(4, P().Hip(0.14f, -0.16f, -14f).Chest(-30f).Head(18f).Feet(0.44f, -0.28f).HandF(0.64f, 1.08f).Blade(2f).HandN(-0.06f, 0.86f).EdgeUp(), Ease.OutExpo)
            .Key(9, P().Hip(0.14f, -0.16f, -14f).Chest(-30f).Head(18f).Feet(0.44f, -0.26f).HandF(0.58f, 1.2f).Blade(30f).HandN(-0.06f, 0.86f).EdgeUp())
            .Key(14, P().Hip(0.06f, -0.16f, -8f).Chest(-16f).Head(10f).Feet(0.36f, -0.25f).HandF(0.3f, 0.92f).Blade(-162f).HandN(0.12f, 0.85f), Ease.InOutQuad)
            .Key(24, Sheathed(ReadyBody()))
            .Key(32, Stance(), Ease.InOutQuad)
            .Move(1, 4, 7.0f)
            .Hit(2, 6, Blade, 14, HitLevel.Mid, new Vector2(2.8f, 1.6f), stun: 30, hitstop: 10, shake: 0.34f, heavy: true, radius: 0.1f)
            .Crit()
            .Invuln(0, 4).NoPush(1, 8)
            .Sheath(CounterSheathFrame, 27)
            .Fx(1, 9, FxKind.TrailBlade).Fx(1, 6, FxKind.AfterImage).Fx(CounterSheathFrame, CounterSheathFrame, FxKind.Glint).Sword();

        /// <summary>The Weapon button on the ground with no direction starts the iai string; everything else is the katana set's.</summary>
        public override MoveDef Select(Btn b, int fwd, int dy, bool air, Fighter self)
        {
            if (!air && b == Btn.S && fwd == 0 && dy >= 0) return draw1;
            return _katana.Select(b, fwd, dy, air, self);
        }

        public override MoveDef Ability(string abilityId) => _katana.Ability(abilityId);
        public override MoveDef BareHanded(MoveDef m) => _katana.BareHanded(m);

        public override IEnumerable<MoveDef> All
        {
            get
            {
                foreach (var m in _katana.All) yield return m;
                yield return draw1; yield return draw2; yield return draw3; yield return noto; yield return Counter;
            }
        }
    }
}
