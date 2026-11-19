using Kage.Rules;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// Kuroe's abilities (SAN-103, ADR-0008): one, <b>Censure</b> (JS <c>censure</c>, "a brief, weaker Redline Counter whose punish can be
    /// blocked"). She raises the temple staff crosswise; a melee blow from the front that lands while it is up is turned aside (the attacker
    /// reels as if parried) and answered a beat later by a dashing diagonal strike that knocks down. Left alone, she drops the guard and is
    /// open for a moment: waiting it out is safe, and a throw still takes her (the JS tip: "wait it out or throw him").
    /// <para>
    /// Every JS number is the generated table's (<see cref="JsTables.BossAbility"/>): the cooldown, distance band and chance; the stance
    /// (how long the guard is up) and the punish delay become frames. The punish's damage is the JS <c>dmg</c> multiplier on the Unity
    /// counter answer the Redline Counter reduces, the Mirror Guard's Kage Counter (12). The rest (poses, knockback, the dash) is Unity's.
    /// </para>
    /// </summary>
    public static class KuroeAbilities
    {
        public const string CensureId = "censure";

        /// <summary>Damage of the Unity counter answer the JS Redline Counter is measured against (the Mirror Guard's Kage Counter).</summary>
        const int CounterAnswerDamage = 12;
        /// <summary>Frames from the start of the stance to the first frame that catches a blow (the staff is up).</summary>
        const int RaiseFrames = 4;
        /// <summary>Frames she takes to drop the guard after the stance: open, no window.</summary>
        const int DropFrames = 14;

        static int Frames(double seconds) => Mathf.RoundToInt((float)(seconds * 60.0));

        /// <summary>Registers Censure in the <see cref="AbilityCatalog"/>. Idempotent.</summary>
        public static void Register()
        {
            var js = JsTables.BossAbility(CensureId);
            AbilityCatalog.Register(new AbilityDef
            {
                id = CensureId,
                move = Censure(js),
                cooldownFrames = Frames(js.Cd),
                range = new Vector2((float)JsUnits.PxToMetres(js.Range[0]), (float)JsUnits.PxToMetres(js.Range[1])),
                chance = (float)js.Chance,
            });
        }

        /// <summary>Both hands on the staff: the lead hand's wrist at (x, y), the shaft pointing <paramref name="blade"/> degrees.</summary>
        static Pose Hold(Pose p, float x, float y, float blade) => p.HandF(x, y).Blade(blade).Grip();

        static Pose GuardBody() => new Pose().Hip(-0.05f, -0.12f, -2f).Chest(4f).Head(-6f).Feet(0.22f, -0.3f);

        /// <summary>The stance: the staff comes up crosswise, holds for the JS stance, then drops.</summary>
        static MoveDef Censure(JsBossAbility js)
        {
            int hold = Frames(js.Stance);
            int counterTo = RaiseFrames + hold - 1;
            int total = counterTo + 1 + DropFrames;
            var m = new MoveDef("kuroe.censure", "Censure", total)
                .Key(RaiseFrames, Hold(GuardBody(), 0.3f, 1.36f, 118f), Ease.OutExpo)
                .Key(counterTo, Hold(GuardBody().Hip(-0.05f, -0.13f, -2f), 0.3f, 1.35f, 120f))
                .Key(total, Stance(), Ease.InOutQuad)
                .Fx(RaiseFrames, RaiseFrames, FxKind.Glint).Sword();
            m.counterFrom = RaiseFrames;
            m.counterTo = counterTo;
            m.counterFrontOnly = true;
            m.counterAnswer = Punish(js);
            return m;
        }

        /// <summary>The punish: the JS delay, then a dashing diagonal strike (0.2 s live, as the JS area), a knockdown; blockable when the JS says so.</summary>
        static MoveDef Punish(JsBossAbility js)
        {
            int delay = Frames(js.Punish);
            int live = 12;                                      // JS: the area is live 0.2 s after the delay
            int total = delay + 33;                             // JS: the ability ends 0.55 s after the delay
            var level = js.Unb == true ? HitLevel.Unblockable : HitLevel.Mid;
            int dmg = Mathf.RoundToInt(CounterAnswerDamage * (float)js.Dmg);
            return new MoveDef("kuroe.censurePunish", "Censure", total)
                .Key(Mathf.Max(1, delay - 2), Hold(GuardBody().Chest(10f).Head(-8f), 0.14f, 1.56f, 128f), Ease.OutQuad)
                .Key(delay + 3, Hold(new Pose().Hip(0.2f, -0.18f, -16f).Chest(-28f).Head(16f).Feet(0.5f, -0.32f), 0.72f, 0.98f, -14f), Ease.OutExpo)
                .Key(delay + live, Hold(new Pose().Hip(0.2f, -0.18f, -16f).Chest(-28f).Head(16f).Feet(0.5f, -0.32f), 0.7f, 0.96f, -18f))
                .Key(total, Stance(), Ease.InOutQuad)
                .Move(delay, delay + 4, 7.0f)
                .Hit(delay, delay + live, new[] { HitPoint.Blade }, dmg, level, new Vector2(2.6f, 3.4f), stun: 26, hitstop: 9, shake: 0.3f,
                    knockdown: true, heavy: true, radius: 0.09f)
                .Fx(delay - 1, delay + live, FxKind.TrailBlade).Fx(delay, delay + 6, FxKind.AfterImage).Fx(delay, delay, FxKind.Dust).Sword();
        }
    }
}
