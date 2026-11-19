using Kage.Rules;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// Garan's abilities (SAN-104, ADR-0008): the three JS ones and the clinch the ticket's comment adds, each an ordinary <see cref="MoveDef"/>
    /// on the heavy fists set.
    /// <list type="bullet">
    /// <item><b>Storm Fist</b> (<c>stormFist</c>, JS "a short Storm Break with the gauntlets"): both fists crackle overhead, armoured, while a
    /// ground mark shows where the shock will run; then the slam (a crushing, knock-down blow that breaks a guard) and the shock area, a
    /// <see cref="Hazard"/> (ADR-0007) that runs along the floor in front of him. Then a long open recovery: the JS tip is "dash away or
    /// behind him, then hit the long recovery".</item>
    /// <item><b>Tsuppari</b> (<c>garanTsuppari</c>, 41-styles/garan.js): the shikiri crouch (the tell), then four open-palm thrusts walking
    /// forward, each leaning hard on a guard (a guard through all of them breaks); a parried palm ends it; then an opening.</item>
    /// <item><b>Charge</b> (<c>charge</c>, phase two): a 0.6 s tell, then an armoured run across the arena; on contact (hit or guard) a crushing
    /// blow and a skid to a stop; dodged, he runs into the wall and stands staggered for 1.7 s.</item>
    /// <item><b>Clinch</b> (<c>garan.clinch</c>, the ticket's "armoured grapple", the JS style's grab on a guarding Kage): arms wide, a short
    /// step in, and whoever is in reach during the JS grab's 0.08 s is taken (a grab window, <see cref="MoveDef.grabTo"/>), carried up and
    /// over and thrown down behind him. Armoured until the release, so a light hit neither stops it nor frees the victim. A dash, a roll or
    /// a backflip out of it, a jump, or simply not standing in a guard in front of him, avoids it.</item>
    /// </list>
    /// <para>
    /// <b>Numbers.</b> The JS data comes from the generated tables: cooldown, range and chance (<see cref="JsTables.BossAbility"/>), Storm
    /// Fist's variant (wind-up, damage, shock and its life), the shock projectile (<see cref="JsTables.Projectile"/>), the grab's timing
    /// (<see cref="JsTables.Move"/>), the clinch's range (<see cref="JsTables.FightStyle"/>), and the tells' scale on Normal for a sub-boss
    /// (<see cref="JsTables.Difficulty"/> × <see cref="JsTables.AiRole"/> wind-up, the JS <c>ai.p.windup</c>). The numbers the JS writes in
    /// its ability code (the constants below) are named and sourced here. Damage: a JS damage times <see cref="UnityPerJsDamage"/>, the
    /// ratio of Unity's jab to the JS jab (4 / 5), so his blows sit on Unity's scale as the JS ones sit on the JS scale. Posture: a JS
    /// <c>gb</c> becomes <see cref="HitWindow.postureMul"/> as gb over the JS default (damage × 1.3): how much heavier on a guard than a plain
    /// blow of that damage, which is what postureMul means.
    /// </para>
    /// </summary>
    public static class GaranAbilities
    {
        public const string StormFistId = "stormFist", TsuppariId = "garanTsuppari", ChargeId = "charge", ClinchId = "garan.clinch";
        public const string ShockId = "garan.stormFist.shock";
        /// <summary>The JS manifest's ground crack, played where the shock strikes.</summary>
        public const string ShockCue = "VFX_Anime_Ground_Crack";

        // ---- constants of the JS ability code (not data, so not in the generated tables) ----
        const double StormBreakDmg = 19;            // 40-ai.js stormBreak: the strike is 19 x the variant's dmg
        const double StormBreakGb = 110;            //   ...and leans gb 110 on a guard
        const double StormBreakRecovery = 0.75;     //   ...the ability lasts the wind-up + 0.75 s
        const double ShockStartPx = 120;            //   ...the shockwave starts 120 px x the build scale in front
        const double TsuppariTell = 0.5, PalmGap = 0.17, PalmWalkPx = 170, TsuppariRecovery = 0.5;   // 41-styles/garan.js garanTsuppari
        const int Palms = 4;
        const double PalmDmg = 6, LastPalmDmg = 10, PalmGb = 24;
        const double ChargeTell = 0.6, ChargeDur = 3.2, ChargeRunPx = 1050, ChargeBackPx = 40;      // 40-ai.js charge
        const double ChargeDmg = 22, ChargeGb = 120, ChargeStop = 0.6, ChargeWallStun = 1.7;
        const double ThrowDmg = 16, ThrowCarry = 0.5, ThrowReachPx = 70, ThrowLiftPx = 120;         // 30-combat.js applyThrow, 20-fighter.js updateThrow
        const double ThrowVxPx = 360, ThrowVyPx = 380;
        const float ClinchChance = 0.55f;           // 41-styles/garan.js decide(): a guard within 115 px, chance(0.55)
        const double GuardLeanPerDmg = 1.3;         // 30-combat.js: a blow without gb leans dmg x 1.3 on a guard

        static readonly HitPoint[] Fist = { HitPoint.Fist };
        static readonly HitPoint[] FarFist = { HitPoint.FistF };
        static readonly HitPoint[] BothFists = { HitPoint.Fist, HitPoint.FistF };
        static readonly HitPoint[] Body = { HitPoint.Fist, HitPoint.FistF, HitPoint.KneeN };

        static int Frames(double seconds) => Mathf.RoundToInt((float)(seconds * 60.0));
        static float Metres(double px) => (float)JsUnits.PxToMetres(px);

        /// <summary>JS <c>ai.p.windup</c> for a sub-boss on Normal: the mode's wind-up times the sub-boss role's (1 x 0.98).</summary>
        public static double Windup => JsTables.Difficulty("normal").Windup * JsTables.AiRole("sub").Windup;

        /// <summary>Unity damage per JS damage: Unity's jab over the JS jab.</summary>
        public static float UnityPerJsDamage => KatanaMoves.Build().jab.hits[0].dmg / (float)JsTables.Move("p1").Dmg;

        static int Dmg(double js) => Mathf.Max(1, Mathf.RoundToInt((float)js * UnityPerJsDamage));
        static float PostureMul(double gb, double jsDmg) => (float)(gb / (jsDmg * GuardLeanPerDmg));

        static AbilityDef Def(string id, MoveDef move)
        {
            var js = JsTables.BossAbility(id);
            return new AbilityDef
            {
                id = id, move = move, cooldownFrames = Frames(js.Cd),
                range = new Vector2(Metres(js.Range[0]), Metres(js.Range[1])), chance = (float)js.Chance,
            };
        }

        /// <summary>Registers the four abilities in the <see cref="AbilityCatalog"/> for a Garan of build scale <paramref name="scale"/>. Idempotent.</summary>
        public static void Register(float scale)
        {
            AbilityCatalog.Register(Def(StormFistId, StormFist(JsTables.BossAbility(StormFistId), scale)));
            AbilityCatalog.Register(Def(TsuppariId, Tsuppari()));
            AbilityCatalog.Register(Def(ChargeId, Charge()));
            var clinch = JsTables.FightStyle(Garan.Id).Strings;
            JsStyleString grab = null;
            foreach (var s in clinch) if (s.Seq.Length == 1 && s.Seq[0] == "grab") grab = s;
            AbilityCatalog.Register(new AbilityDef
            {
                id = ClinchId, move = Clinch(Metres(grab.Range[1]), scale), cooldownFrames = 0,
                range = new Vector2(Metres(grab.Range[0]), Metres(grab.Range[1])), chance = ClinchChance,
                when = (self, opp) => opp != null && opp.IsGuarding,      // the JS string's `when`: a guard in front of him
            });
        }

        static Pose P() => new Pose();
        static Pose Guard() => FistGuard(Stance());

        // ---------------------------------------------------------------------------------------------- Storm Fist
        static MoveDef StormFist(JsBossAbility js, float scale)
        {
            int land = Frames(js.Windup * Windup);           // 46: the fists reach the floor
            int slam = land - 1;                             // the first frame of the blow, and of the shock
            int total = land + Frames(StormBreakRecovery);
            var overhead = P().Hip(-0.05f, -0.12f, 8f).Chest(18f).Head(-12f).Feet(0.34f, -0.32f).HandN(0.08f, 2.05f, 80f).HandF(-0.04f, 2.0f, 80f);
            var down = P().Hip(0.12f, -0.42f, -10f).Chest(-48f).Head(24f).Feet(0.46f, -0.32f).HandN(0.98f, 0.14f, -70f).HandF(0.88f, 0.12f, -70f);
            double jsSlam = StormBreakDmg * js.Dmg;
            var shock = JsTables.Projectile("shock");
            double runPx = shock.Speed * shock.Life * js.ShockLife;          // how far the JS shockwave runs (228 px)
            var hazard = new HazardDef
            {
                id = ShockId,
                telegraphFrames = slam - 1,                                   // laid down on frame 1, it strikes with the slam
                strikeFrames = Frames(shock.Life * js.ShockLife),
                size = new Vector2(Metres(runPx + 2 * shock.R), Metres(2 * shock.R)),
                hit = new HitWindow
                {
                    dmg = Dmg(shock.Dmg * js.Shock), level = shock.Unb ? HitLevel.Unblockable : HitLevel.Mid,
                    kb = new Vector2(2.2f, 3.6f), stun = 24, hitstop = 8, shake = 0.3f, knockdown = shock.Kd, heavy = true,
                },
                strikeCue = ShockCue,
            };
            return new MoveDef("garan.stormFist", "Storm Fist", total)
                .Key(8, overhead, Ease.OutQuad)
                .Key(land - 4, P().Hip(-0.06f, -0.1f, 10f).Chest(22f).Head(-14f).Feet(0.34f, -0.32f).HandN(0.04f, 2.1f, 80f).HandF(-0.08f, 2.05f, 80f))
                .Key(land, down, Ease.InCubic)
                .Key(land + 16, P().Hip(0.12f, -0.42f, -10f).Chest(-46f).Head(22f).Feet(0.46f, -0.32f).HandN(0.96f, 0.16f, -70f).HandF(0.86f, 0.14f, -70f))
                .Key(total, Guard(), Ease.InOutQuad)
                .Hit(slam, slam + 4, BothFists, Dmg(jsSlam), HitLevel.Overhead, new Vector2(3.3f, 4.0f), stun: 30, hitstop: 12, shake: 0.55f,
                    knockdown: true, heavy: true, radius: 0.22f, postureMul: PostureMul(StormBreakGb, jsSlam))
                .Armour(0, land)
                .Hazard(1, hazard, Metres(ShockStartPx * scale + runPx / 2))
                .Fx(2, land - 3, FxKind.ChargeDark).Fx(land - 8, land - 8, FxKind.Glint)
                .Fx(land, land, FxKind.Dust).Fx(land, land, FxKind.Shockwave).Fx(slam - 2, land + 1, FxKind.TrailFist);
        }

        // ---------------------------------------------------------------------------------------------- Tsuppari
        static MoveDef Tsuppari()
        {
            int tell = Frames(TsuppariTell * Windup);                         // 29
            int end = Frames(TsuppariTell * Windup + Palms * PalmGap);
            int total = Frames(TsuppariTell * Windup + Palms * PalmGap + TsuppariRecovery);
            var crouch = P().Hip(0f, -0.44f, 2f).Chest(-30f).Head(22f).Feet(0.52f, -0.34f).HandN(0.44f, 0.46f, -80f).HandF(0.36f, 0.44f, -80f);
            var m = new MoveDef("garan.tsuppari", "Tsuppari", total)
                .Key(tell - 6, crouch, Ease.OutQuad)
                .Key(tell - 1, P().Hip(0f, -0.46f, 2f).Chest(-32f).Head(24f).Feet(0.52f, -0.34f).HandN(0.46f, 0.44f, -80f).HandF(0.38f, 0.42f, -80f));
            double jsSmall = PalmDmg, jsLast = LastPalmDmg;
            for (int i = 0; i < Palms; i++)
            {
                int at = Frames(TsuppariTell * Windup + i * PalmGap);
                bool near = i % 2 == 0, last = i == Palms - 1;                   // the JS alternates the jab and the cross hand
                var thrust = P().Hip(0.12f, -0.2f, -10f).Chest(-22f).Head(10f).Feet(0.44f, -0.3f);
                thrust = near ? thrust.HandN(0.92f, 1.3f, 90f).HandF(0.2f, 1.12f, 30f) : thrust.HandF(0.9f, 1.28f, 90f).HandN(0.24f, 1.2f, 30f);
                var chamber = P().Hip(0.06f, -0.22f, -6f).Chest(-16f).Head(8f).Feet(0.4f, -0.3f).HandN(0.4f, 1.24f, 60f).HandF(0.34f, 1.18f, 60f);
                m.Key(at + 2, thrust, Ease.OutExpo).Key(at + 7, chamber, Ease.InOutQuad);
                double js = last ? jsLast : jsSmall;
                m.Hit(at, at + 3, near ? Fist : FarFist, Dmg(js), HitLevel.Mid, last ? new Vector2(4.2f, 0.8f) : new Vector2(1.6f, 0f),
                    stun: last ? 26 : 16, hitstop: last ? 9 : 5, shake: last ? 0.3f : 0.12f, heavy: last, radius: 0.16f, postureMul: PostureMul(PalmGb, js));
                m.Fx(at, at + 3, FxKind.TrailFist);
            }
            return m.Key(total, Guard(), Ease.InOutQuad)
                .Move(tell, end, Metres(PalmWalkPx))
                .Armour(0, end)
                .Fx(tell, tell, FxKind.Dust);
        }

        // ---------------------------------------------------------------------------------------------- Charge
        static MoveDef Charge()
        {
            int tell = Frames(ChargeTell), total = Frames(ChargeDur);
            var set = P().Hip(-0.08f, -0.24f, 10f).Chest(14f).Head(-8f).Feet(0.3f, -0.4f).HandN(0.14f, 1.0f, 20f).HandF(-0.02f, 0.96f, 20f);
            var runA = P().Hip(0.12f, -0.22f, -18f).Chest(-34f).Head(26f).FootN(0.5f, 0.18f).FootF(-0.42f, 0.2f).HandN(0.6f, 1.22f, 0f).HandF(0.5f, 1.1f, 0f);
            var runB = P().Hip(0.12f, -0.18f, -18f).Chest(-34f).Head(26f).FootN(-0.36f, 0.2f).FootF(0.46f, 0.18f).HandN(0.62f, 1.24f, 0f).HandF(0.52f, 1.12f, 0f);
            var m = new MoveDef("garan.charge", "Charge", total)
                .Key(10, set, Ease.OutQuad)
                .Key(tell - 1, P().Hip(-0.1f, -0.26f, 12f).Chest(18f).Head(-10f).Feet(0.3f, -0.42f).HandN(0.1f, 0.98f, 20f).HandF(-0.04f, 0.94f, 20f));
            for (int f = tell + 4, k = 0; f < total; f += 8, k++) m.Key(f, k % 2 == 0 ? runA : runB, Ease.InOutQuad);
            m.Move(0, tell - 1, -Metres(ChargeBackPx))
                .Move(tell, total, Metres(ChargeRunPx))
                .Hit(tell, total, Body, Dmg(ChargeDmg), HitLevel.Mid, new Vector2(5.0f, 3.0f), stun: 34, hitstop: 14, shake: 0.6f,
                    knockdown: true, heavy: true, radius: 0.2f, postureMul: PostureMul(ChargeGb, ChargeDmg))
                .Armour(0, total)
                .Fx(tell, total, FxKind.AfterImage).Fx(tell, tell, FxKind.Dust);
            m.onContact = new MoveDef("garan.chargeStop", "Charge", Frames(ChargeStop))
                .Key(10, P().Hip(-0.04f, -0.24f, 8f).Chest(10f).Head(-4f).Feet(0.46f, -0.3f).HandN(0.5f, 1.1f, 10f).HandF(0.4f, 1.02f, 10f), Ease.OutQuad)
                .Key(Frames(ChargeStop), Guard(), Ease.InOutQuad)
                .Move(0, 8, 2.4f)
                .Fx(0, 0, FxKind.Dust);
            m.wallStagger = Frames(ChargeWallStun);
            return m;
        }

        // ---------------------------------------------------------------------------------------------- Clinch
        static MoveDef Clinch(float reach, float scale)
        {
            var grab = JsTables.Move("grab");
            int from = Frames(grab.T[0]);                        // 4: the arms close
            int to = from + Frames(grab.T[1]) - 1;               // 8: the last frame they can take him
            int release = to + Frames(ThrowCarry);
            int recovery = Frames(grab.T[2]);
            var wide = P().Hip(0.08f, -0.2f, -8f).Chest(-22f).Head(12f).Feet(0.4f, -0.28f).HandN(0.78f, 1.38f, 10f).HandF(0.72f, 1.16f, 10f);
            var clamp = P().Hip(0.12f, -0.22f, -10f).Chest(-26f).Head(14f).Feet(0.42f, -0.28f).HandN(0.52f, 1.26f, -20f).HandF(0.48f, 1.14f, -20f);
            var whiff = new MoveDef("garan.clinchWhiff", "Clinch", to + recovery)
                .Key(from, wide, Ease.OutExpo).Key(to, clamp, Ease.InQuad)
                .Key(to + recovery, Guard(), Ease.InOutQuad)
                .Armour(0, to);
            var m = new MoveDef("garan.clinch", "Clinch", release + recovery)
                .Key(from, wide, Ease.OutExpo).Key(to, clamp, Ease.InQuad)
                .Key(to + 10, P().Hip(0f, -0.32f, -2f).Chest(-6f).Head(-4f).Feet(0.32f, -0.3f).HandN(0.28f, 1.72f).HandF(0.2f, 1.68f), Ease.InOutQuad)
                .Key(to + 20, P().Hip(-0.04f, -0.18f, 10f).Chest(18f).Head(-12f).Feet(0.3f, -0.3f).HandN(-0.08f, 1.96f).HandF(-0.18f, 1.9f), Ease.InOutQuad)
                .Key(release, P().Hip(-0.06f, -0.36f, -12f).Chest(-44f).Head(20f).Feet(0.26f, -0.3f).HandN(-0.52f, 0.7f).HandF(-0.46f, 0.6f), Ease.InQuad)
                .Key(release + recovery, Guard(), Ease.InOutQuad)
                .Move(1, to, 1.6f)
                .Armour(0, release)
                .Fx(release, release, FxKind.Dust).Fx(release, release, FxKind.Shockwave);
            m.isThrow = true;
            m.grabFrame = from;
            m.grabTo = to;
            m.releaseFrame = release;
            m.whiff = whiff;
            m.grabRange = reach;
            m.throwDmg = Dmg(ThrowDmg);
            m.carryFrom = Metres(ThrowReachPx * scale) * Mathf.Cos(0.2f);                 // the JS arc: from 0.2 rad in front...
            m.carryTo = Metres(ThrowReachPx * scale) * Mathf.Cos(Mathf.PI * 0.95f);      // ...over his head to behind him
            m.carryHeight = Metres(ThrowLiftPx * scale);
            m.throwKb = new Vector2(-Metres(ThrowVxPx), Metres(ThrowVyPx));             // the JS throw: back and up (px/s / 115)
            return m;
        }

        // ---------------------------------------------------------------------------------------------- cues
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterCues() => EffectCues.Register(ShockCue, PlayShock);

        /// <summary>Placeholder ground crack: a shockwave and dust the length of the box, red sparks running along the floor.</summary>
        static void PlayShock(Vector2 pos, in CueArgs a)
        {
            var vfx = Vfx.I;
            if (vfx == null) return;
            Color c = a.tint ?? GaranPalette.Storm;
            float half = Mathf.Max(0.4f, a.size.x * 0.5f);
            vfx.Shockwave(pos + Vector2.up * 0.03f, half, c);
            vfx.Dust(pos, Mathf.Max(0.9f, a.size.x * 0.6f), 0f);
            for (int i = -3; i <= 3; i++)
                vfx.Spark(pos + new Vector2(i * half / 3.5f, 0.05f), new Vector2(i * 0.6f, 2.2f + Mathf.Abs(i) * 0.2f), c, 0.06f, 0.35f);
            GameFeel.Shake(0.25f);
        }
    }
}
