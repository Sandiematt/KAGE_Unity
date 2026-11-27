using Kage.Rules;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// Shiden's abilities (SAN-106 + Hōzōin-ryū sōjutsu martial arts &amp; Raikon's lieutenant lightning, ADR-0008):
    /// <list type="bullet">
    /// <item><b>Vanguard Break</b> (<c>vanguardBreak</c>), <b>Brace</b> (<c>shidenBrace</c>), <b>Hold the Line</b> (<c>vanguardGuard</c>), and
    /// <b>Charging Thrust</b> (<c>shiden.chargingThrust</c>): his four SAN-106 slots, upgraded with 40:1 Hōzōin-ryū spear pierce streaks and
    /// violet-crimson lieutenant lightning strikes.</item>
    /// <item><b>Maki-Otoshi · Ren-Tsuki</b> (<c>shiden.makiOtoshi</c>): textbook Hōzōin-ryū Omote kata — a winding downward beat of the shaft
    /// to knock down the foe's guard, a snapping probe jab, and a body-weighted piston <i>Ō-Tsuki</i> down the centerline.</item>
    /// <item><b>Suriage · Ō-Nagi</b> (<c>shiden.sojutsuSweep</c>): classical sōjutsu low shin sweep into a rising diagonal yari fang trailing
    /// violet-crimson static (echoing Raikon's Low Sweep → Rising Cut &amp; Sky-Splitter).</item>
    /// <item><b>Vanguard Raijin Arc</b> (<c>shiden.raijinLance</c>, Phase 2): as Raikon's closest disciple, Shiden raises the Vanguard Yari
    /// in an armoured lightning call and slashes out a ground-skimming violet-crimson spark wave (an apprentice Raijin Arc).</item>
    /// </list>
    /// </summary>
    public static class ShidenAbilities
    {
        public const string VanguardBreakId = "vanguardBreak", BraceId = "shidenBrace", VanguardGuardId = "vanguardGuard";
        public const string ChargeId = "shiden.chargingThrust";
        public const string MakiOtoshiId = "shiden.makiOtoshi", SojutsuSweepId = "shiden.sojutsuSweep", RaijinLanceId = "shiden.raijinLance";
        public const string ShockId = "shiden.vanguardBreak.shock", RaijinWaveId = "shiden.raijinWave";

        public static readonly string[] ExtraIds = { MakiOtoshiId, SojutsuSweepId, RaijinLanceId };

        // ---- effect cues ----
        public const string ChargeGlintCue = "VFX_Kage_ShidenGlint";
        public const string CrackleCue = "VFX_Kage_ShidenCrackle";
        public const string ShockCue = "VFX_Kage_VanguardShock";
        public const string SpearPierceCue = "VFX_Kage_ShidenSpearPierce";
        public const string StormPierceCue = "VFX_Kage_ShidenStormPierce";
        public const string RisingFangCue = "VFX_Kage_ShidenRisingFang";
        public const string RaijinWaveFlightCue = "VFX_Kage_ShidenRaijinWave";
        public const string MakiBeatCue = "VFX_Kage_ShidenMakiBeat";
        public const string LowSweepCue = "VFX_Kage_ShidenLowSweep";

        public static readonly string[] CueIds =
        {
            ChargeGlintCue, CrackleCue, ShockCue, SpearPierceCue, StormPierceCue, RisingFangCue, RaijinWaveFlightCue, MakiBeatCue, LowSweepCue,
        };

        // ---- constants of the JS ability code ----
        const double BraceSet = 0.22;
        const double BraceThrustLive = 0.24, BraceThrustEnd = 0.5;
        const double BraceKbPx = 560, BraceUpPx = 160, BraceStun = 0.5;
        const double BraceLungePx = 520, BraceDamp = 14;
        const double RushRunPx = 140, RushCutPx = 60;
        const double GuardLeanPerDmg = 1.3;
        const double RedlinePunish = 0.1;
        const int CounterAnswerDamage = 12;
        const int GuardRaise = 4, GuardDrop = 14;
        const double DashPx = 900;
        const double ChargeStringPx = 320;

        public const double ChargeRunTime = 1.2;

        public static MoveDef MakiOtoshiMove, SojutsuSweepMove, RaijinLanceMove;
        public static ProjectileDef RaijinWave;

        static int Frames(double seconds) => Mathf.RoundToInt((float)(seconds * 60.0));
        static float Metres(double px) => (float)JsUnits.PxToMetres(px);
        static int Dmg(double js) => Mathf.Max(1, Mathf.RoundToInt((float)js * GaranAbilities.UnityPerJsDamage));
        static float Lean(double jsDmg) => HisameAbilities.PostureMul(jsDmg * GuardLeanPerDmg, Dmg(jsDmg));

        static AbilityDef Def(string id, MoveDef move)
        {
            var js = JsTables.BossAbility(id);
            return new AbilityDef
            {
                id = id, move = move, cooldownFrames = Frames(js.Cd),
                range = new Vector2(Metres(js.Range[0]), Metres(js.Range[1])), chance = (float)js.Chance,
            };
        }

        /// <summary>Registers Shiden's abilities in the <see cref="AbilityCatalog"/> for build scale <paramref name="scale"/>. Idempotent.</summary>
        public static void Register(float scale)
        {
            RegisterCues();
            AbilityCatalog.Register(Def(VanguardBreakId, VanguardBreak(JsTables.BossAbility(VanguardBreakId), scale)));
            AbilityCatalog.Register(Def(BraceId, Brace(JsTables.ShidenBrace, scale)));
            AbilityCatalog.Register(Def(VanguardGuardId, HoldTheLine(JsTables.BossAbility(VanguardGuardId))));
            var charge = JsTables.BossAbility(GaranAbilities.ChargeId);
            var move = ChargingThrust(out float reach);
            AbilityCatalog.Register(new AbilityDef
            {
                id = ChargeId, move = move, cooldownFrames = Frames(charge.Cd), chance = (float)charge.Chance,
                range = new Vector2(Metres(DashStringNear()), reach),
            });

            BuildMartialKit();
            Reg(MakiOtoshiId, MakiOtoshiMove, 5.5f, 0.7f, 2.5f, 0.45f);
            Reg(SojutsuSweepId, SojutsuSweepMove, 6.0f, 0.8f, 2.4f, 0.42f);
            Reg(RaijinLanceId, RaijinLanceMove, 6.8f, 2.2f, 6.8f, 0.45f);
            Patterns();
        }

        /// <summary>
        /// Shiden's strings (sub-boss IV, <see cref="SubBossTier.Shiden"/>: the most frequent chains on the shortest beat, the hardest of Act I's
        /// lieutenants). The bind-and-thrust kata flows into the low sweep or the overhead Vanguard Break; the sweep's rising fang opens Kage for the
        /// Raijin wave or the charging thrust; whatever lands, the next link is already coming.
        /// </summary>
        static void Patterns()
        {
            var t = SubBossTier.Shiden;
            t.Chain(MakiOtoshiId, SojutsuSweepId, VanguardBreakId);
            t.Chain(SojutsuSweepId, RaijinLanceId, ChargeId, MakiOtoshiId);
            t.Chain(VanguardBreakId, MakiOtoshiId, RaijinLanceId);
            t.Chain(RaijinLanceId, ChargeId, MakiOtoshiId);
            t.Chain(ChargeId, SojutsuSweepId, MakiOtoshiId);
        }

        static void Reg(string id, MoveDef m, float cd, float near, float far, float chance)
        {
            AbilityCatalog.Register(new AbilityDef { id = id, move = m, cooldownFrames = Frames(cd), range = new Vector2(near, far), chance = chance });
        }

        static double DashStringNear()
        {
            foreach (var s in JsTables.FightStyle(Shiden.Id).Strings)
                if (s.Seq.Length == 2 && s.Seq[0] == "dash" && s.Seq[1] == "weapon") return s.Range[0];
            return ChargeStringPx;
        }

        public static bool Rushing(Fighter self, Fighter kage)
        {
            if (self == null || kage == null) return false;
            float toward = Mathf.Sign(self.Pos.x - kage.Pos.x);
            if (toward == 0f) toward = 1f;
            if (kage.state == FState.Dash) return true;
            float vx = kage.LogicVelocity.x;
            if ((kage.state == FState.Walk || kage.state == FState.Run) && Mathf.Sign(vx) == toward && Mathf.Abs(vx) > Metres(RushRunPx)) return true;
            if (kage.state != FState.Attack || kage.move == null) return false;
            int first = int.MaxValue;
            foreach (var w in kage.move.hits) first = Mathf.Min(first, w.from);
            return kage.moveFrame < first && kage.facing == toward && Mathf.Sign(vx) == toward && Mathf.Abs(vx) > Metres(RushCutPx);
        }

        static Pose P() => new Pose();
        static Pose Hold(Pose p, float x, float y, float blade) => p.HandF(x, y).Blade(blade).Grip();
        static Pose Ready() => Stance();

        static void BuildMartialKit()
        {
            if (MakiOtoshiMove != null) return;

            RaijinWave = new ProjectileDef
            {
                id = RaijinWaveId,
                speed = 6.2f,
                groundWave = true,
                radius = 0.25f,
                lifeFrames = 85,
                maxHits = 1,
                hit = new HitWindow { dmg = Dmg(7), level = HitLevel.Mid, kb = new Vector2(2.8f, 1.8f), stun = 22, hitstop = 7, shake = 0.22f, heavy = true },
                flightCue = RaijinWaveFlightCue,
            };

            // Hōzōin-ryū Maki-Otoshi · Ren-Tsuki: circular winding beat -> snapping probe thrust -> body-weighted piston Ō-Tsuki
            MakiOtoshiMove = new MoveDef(MakiOtoshiId, "Maki-Otoshi · Ren-Tsuki", 72)
                .Key(9, Hold(P().Hip(-0.04f, -0.1f, 4f).Chest(10f).Head(-6f).Feet(0.28f, -0.32f), 0.14f, 1.54f, 52f), Ease.OutQuad)
                .Key(17, Hold(P().Hip(0.1f, -0.22f, -10f).Chest(-20f).Head(10f).Feet(0.42f, -0.32f), 0.56f, 0.84f, -22f), Ease.OutExpo)
                .Key(24, Hold(P().Hip(0.02f, -0.18f, -4f).Chest(-8f).Head(6f).Feet(0.36f, -0.32f), 0.18f, 0.98f, 4f), Ease.InOutQuad)
                .Key(30, Hold(P().Hip(0.14f, -0.2f, -12f).Chest(-22f).Head(12f).Feet(0.46f, -0.32f), 0.72f, 1.0f, 3f), Ease.OutExpo)
                .Key(37, Hold(P().Hip(0.0f, -0.22f, -2f).Chest(-6f).Head(4f).Feet(0.38f, -0.34f), 0.08f, 0.94f, 2f), Ease.InOutQuad)
                .Key(44, Hold(P().Hip(0.22f, -0.24f, -16f).Chest(-30f).Head(16f).Feet(0.54f, -0.34f), 0.88f, 0.98f, 2f), Ease.OutExpo)
                .Key(51, Hold(P().Hip(0.2f, -0.24f, -16f).Chest(-28f).Head(14f).Feet(0.54f, -0.34f), 0.86f, 0.98f, 2f))
                .Key(72, Ready(), Ease.InOutQuad)
                .Move(13, 18, 2.2f).Move(27, 31, 1.8f).Move(41, 46, 3.6f)
                .Hit(16, 20, new[] { HitPoint.Blade }, Dmg(5), HitLevel.Mid, new Vector2(1.1f, -0.4f), stun: 20, hitstop: 5, shake: 0.14f, radius: 0.11f, postureMul: 1.6f)
                .Hit(29, 33, new[] { HitPoint.Blade }, Dmg(5), HitLevel.Mid, new Vector2(1.2f, 0f), stun: 20, hitstop: 5, shake: 0.14f, radius: 0.09f)
                .Hit(43, 49, new[] { HitPoint.Blade }, Dmg(8), HitLevel.Mid, new Vector2(3.8f, 1.2f), stun: 26, hitstop: 9, shake: 0.3f, heavy: true, radius: 0.1f)
                .Cue(6, ChargeGlintCue).Cue(17, MakiBeatCue).Cue(30, SpearPierceCue).Cue(44, StormPierceCue)
                .Fx(15, 21, FxKind.TrailBlade).Fx(28, 34, FxKind.TrailBlade).Fx(42, 50, FxKind.TrailBlade).Fx(43, 43, FxKind.Dust)
                .Impulse(44, new Vector2(3.0f, 0.4f)).Sword();

            // Suriage · Ō-Nagi: low yari shin sweep pivoting smoothly into an electrified rising diagonal fang
            SojutsuSweepMove = new MoveDef(SojutsuSweepId, "Suriage · Ō-Nagi", 60)
                .Key(10, Hold(P().Hip(-0.08f, -0.36f, 2f).Chest(-22f).Head(14f).FootN(0.28f, 0.13f).FootF(-0.32f, 0.15f, -25f), -0.1f, 0.72f, 174f), Ease.OutQuad)
                .Key(19, Hold(P().Hip(-0.1f, -0.38f, 4f).Chest(-24f).Head(16f).FootN(0.28f, 0.13f).FootF(-0.32f, 0.15f, -25f), -0.14f, 0.68f, 176f))
                .Key(23, Hold(P().Hip(0.08f, -0.38f, -8f).Chest(-30f).Head(18f).FootN(0.4f, 0.13f).FootF(-0.32f, 0.15f, -25f), 0.62f, 0.58f, 4f), Ease.OutExpo)
                .Key(30, Hold(P().Hip(0.06f, -0.26f, -4f).Chest(-14f).Head(8f).Feet(0.38f, -0.3f), 0.38f, 0.74f, -12f), Ease.InOutQuad)
                .Key(37, Hold(P().Hip(0.14f, -0.08f, -4f).Chest(8f).Head(-8f).Feet(0.44f, -0.28f), 0.6f, 1.52f, 54f), Ease.OutExpo)
                .Key(43, Hold(P().Hip(0.14f, -0.08f, -4f).Chest(8f).Head(-8f).Feet(0.44f, -0.28f), 0.56f, 1.56f, 58f))
                .Key(60, Ready(), Ease.InOutQuad)
                .Move(17, 23, 2.8f).Move(32, 37, 2.0f)
                .Hit(20, 24, new[] { HitPoint.Blade }, Dmg(6), HitLevel.Low, new Vector2(1.2f, 0f), stun: 22, hitstop: 6, shake: 0.16f, radius: 0.16f)
                .Hit(35, 40, new[] { HitPoint.Blade }, Dmg(8), HitLevel.Mid, new Vector2(3.0f, 2.4f), stun: 26, hitstop: 8, shake: 0.26f, heavy: true, radius: 0.15f)
                .Cue(8, ChargeGlintCue).Cue(21, LowSweepCue).Cue(36, RisingFangCue)
                .Fx(19, 25, FxKind.TrailBlade).Fx(34, 41, FxKind.TrailBlade)
                .Impulse(36, new Vector2(2.0f, 2.4f)).Sword();

            // Vanguard Raijin Arc (Phase 2): Shiden's disciplined sub-boss inheritance of Raikon's Raijin Arc
            RaijinLanceMove = new MoveDef(RaijinLanceId, "Vanguard Raijin Arc", 60)
                .Key(10, Hold(P().Hip(-0.04f, -0.06f, 6f).Chest(14f).Head(-12f).Feet(0.3f, -0.32f), 0.08f, 1.88f, 96f), Ease.OutQuad)
                .Key(26, Hold(P().Hip(-0.06f, -0.04f, 8f).Chest(18f).Head(-14f).Feet(0.3f, -0.32f), 0.04f, 1.94f, 94f))
                .Key(30, Hold(P().Hip(0.14f, -0.34f, -12f).Chest(-36f).Head(18f).Feet(0.48f, -0.32f), 0.64f, 0.78f, -24f), Ease.InCubic)
                .Key(40, Hold(P().Hip(0.14f, -0.34f, -12f).Chest(-34f).Head(16f).Feet(0.48f, -0.32f), 0.62f, 0.8f, -26f))
                .Key(60, Ready(), Ease.InOutQuad)
                .Armour(0, 26)
                .Hit(28, 32, new[] { HitPoint.Blade }, Dmg(9), HitLevel.Mid, new Vector2(2.8f, 1.4f), stun: 24, hitstop: 9, shake: 0.34f, heavy: true, radius: 0.14f)
                .Throw(29, RaijinWave, new Vector2(0.95f, RaijinWave.radius))
                .Cue(6, CrackleCue).Cue(12, CrackleCue).Cue(18, CrackleCue).Cue(22, ChargeGlintCue).Cue(29, StormPierceCue)
                .Fx(27, 33, FxKind.TrailBlade).Fx(29, 29, FxKind.Dust).Fx(29, 29, FxKind.Shockwave)
                .Impulse(29, new Vector2(2.8f, -2.2f)).Sword();
        }

        // ---------------------------------------------------------------------------------------------- Vanguard Break
        static MoveDef VanguardBreak(JsBossAbility js, float scale)
        {
            int land = Frames(js.Windup * GaranAbilities.Windup);
            int slam = land;
            int total = land + Frames(GaranAbilities.StormBreakRecovery);
            var overhead = Hold(P().Hip(-0.06f, -0.1f, 8f).Chest(16f).Head(-10f).Feet(0.34f, -0.32f), 0.1f, 1.98f, -24f);
            var higher = Hold(P().Hip(-0.08f, -0.08f, 10f).Chest(20f).Head(-12f).Feet(0.34f, -0.32f), 0.04f, 2.06f, -20f);
            var down = Hold(P().Hip(0.12f, -0.42f, -10f).Chest(-44f).Head(22f).Feet(0.48f, -0.32f), 0.6f, 0.7f, -44f);
            double jsSlam = GaranAbilities.StormBreakDmg * js.Dmg;
            var shock = JsTables.Projectile("shock");
            double runPx = shock.Speed * shock.Life * js.ShockLife;
            var hazard = new HazardDef
            {
                id = ShockId,
                telegraphFrames = slam - 1,
                strikeFrames = Frames(shock.Life * js.ShockLife),
                size = new Vector2(Metres(runPx + 2 * shock.R), Metres(2 * shock.R)),
                hit = new HitWindow
                {
                    dmg = Dmg(shock.Dmg * js.Shock), level = shock.Unb ? HitLevel.Unblockable : HitLevel.Mid,
                    kb = new Vector2(2.2f, 3.6f), stun = 24, hitstop = 8, shake = 0.3f, knockdown = shock.Kd, heavy = true,
                },
                strikeCue = ShockCue,
            };
            var m = new MoveDef("shiden.vanguardBreak", "Vanguard Break", total)
                .Key(8, overhead, Ease.OutQuad)
                .Key(land - 4, higher)
                .Key(land, down, Ease.InCubic)
                .Key(land + 18, Hold(P().Hip(0.12f, -0.42f, -10f).Chest(-42f).Head(20f).Feet(0.48f, -0.32f), 0.58f, 0.72f, -44f))
                .Key(total, Ready(), Ease.InOutQuad)
                .Hit(slam, slam + 4, new[] { HitPoint.Blade }, Dmg(jsSlam), HitLevel.Overhead, new Vector2(3.3f, 4.0f), stun: 30, hitstop: 12,
                    shake: 0.5f, knockdown: true, heavy: true, radius: 0.1f, postureMul: HisameAbilities.PostureMul(GaranAbilities.StormBreakGb, Dmg(jsSlam)))
                .Armour(0, land)
                .Hazard(1, hazard, Metres(GaranAbilities.ShockStartPx * scale + runPx / 2))
                .Fx(land - 8, land - 8, FxKind.Glint)
                .Fx(land, land, FxKind.Dust).Fx(land, land, FxKind.Shockwave).Fx(slam - 2, land + 1, FxKind.TrailBlade)
                .Impulse(land, new Vector2(2.6f, -2.5f)).Sword();
            for (int f = 4; f < land; f += 4) m.Cue(f, CrackleCue);
            return m;
        }

        // ---------------------------------------------------------------------------------------------- Brace
        static MoveDef Brace(JsBrace js, float scale)
        {
            int set = Frames(BraceSet * GaranAbilities.Windup);
            int held = set + Frames(js.Hold) - 1;
            int total = held + 1 + Frames(js.Open);
            var low = Hold(P().Hip(-0.08f, -0.32f, -4f).Chest(-18f).Head(12f).Feet(0.46f, -0.38f), 0.36f, 0.86f, 2f);
            var lower = Hold(P().Hip(-0.09f, -0.33f, -4f).Chest(-19f).Head(12f).Feet(0.46f, -0.38f), 0.35f, 0.85f, 2f);
            var lifted = Hold(P().Hip(-0.02f, -0.08f, 2f).Chest(6f).Head(-4f).Feet(0.26f, -0.28f), 0.22f, 1.26f, 62f);
            return new MoveDef("shiden.brace", "Brace", total)
                .Key(set, low, Ease.OutExpo)
                .Key(held, lower)
                .Key(held + 10, lifted, Ease.OutQuad)
                .Key(total, Ready(), Ease.InOutQuad)
                .Brace(set, held, Metres(js.Trigger * scale), BraceThrust(js, scale), Rushing)
                .Cue(set, ChargeGlintCue).Sword();
        }

        static MoveDef BraceThrust(JsBrace js, float scale)
        {
            int live = Frames(BraceThrustLive), total = Frames(BraceThrustEnd);
            float lungeM = Metres(BraceLungePx / BraceDamp);
            var thrust = Hold(P().Hip(0.18f, -0.22f, -14f).Chest(-28f).Head(14f).Feet(0.52f, -0.34f), 0.86f, 0.92f, 2f);
            var m = new MoveDef("shiden.braceThrust", "Braced Thrust", total)
                .Key(2, thrust, Ease.OutExpo)
                .Key(live, thrust)
                .Key(total, Ready(), Ease.InOutQuad)
                .Move(1, 4, lungeM * 60f / 4f)
                .Hit(0, live, new[] { HitPoint.Blade }, Dmg(js.Dmg), HitLevel.Mid, new Vector2(Metres(BraceKbPx), Metres(BraceUpPx)), stun: Frames(BraceStun),
                    hitstop: 10, shake: 0.3f, heavy: true, radius: 0.09f, postureMul: Lean(js.Dmg))
                .Cue(2, SpearPierceCue)
                .Fx(0, live, FxKind.TrailBlade).Fx(0, 0, FxKind.Dust).Sword();
            m.hits[0].critical = true;
            return m;
        }

        // ---------------------------------------------------------------------------------------------- Hold the Line
        static Pose GuardBody() => P().Hip(-0.05f, -0.14f, -2f).Chest(2f).Head(-4f).Feet(0.26f, -0.32f);

        static MoveDef HoldTheLine(JsBossAbility js)
        {
            int counterTo = GuardRaise + Frames(js.Stance) - 1;
            int total = counterTo + 1 + GuardDrop;
            var m = new MoveDef("shiden.holdTheLine", "Hold the Line", total)
                .Key(GuardRaise, Hold(GuardBody(), 0.34f, 1.12f, 84f), Ease.OutExpo)
                .Key(counterTo, Hold(GuardBody().Hip(-0.05f, -0.15f, -2f), 0.34f, 1.11f, 86f))
                .Key(total, Ready(), Ease.InOutQuad)
                .Cue(GuardRaise, ChargeGlintCue)
                .Fx(GuardRaise, GuardRaise, FxKind.Glint).Sword();
            m.counterFrom = GuardRaise;
            m.counterTo = counterTo;
            m.counterFrontOnly = true;
            m.counterAnswer = HoldTheLinePunish(js);
            return m;
        }

        static MoveDef HoldTheLinePunish(JsBossAbility js)
        {
            int delay = Frames(js.Punish > 0 ? js.Punish : RedlinePunish);
            int live = 12;
            int total = delay + 33;
            var level = js.Unb == true ? HitLevel.Unblockable : HitLevel.Mid;
            int dmg = Mathf.RoundToInt(CounterAnswerDamage * (float)js.Dmg);
            var thrust = Hold(P().Hip(0.2f, -0.2f, -16f).Chest(-28f).Head(14f).Feet(0.52f, -0.34f), 0.78f, 0.84f, -4f);
            return new MoveDef("shiden.holdTheLinePunish", "Hold the Line", total)
                .Key(Mathf.Max(1, delay - 1), Hold(GuardBody().Chest(-4f), 0.02f, 0.88f, 1f), Ease.OutQuad)
                .Key(delay + 3, thrust, Ease.OutExpo)
                .Key(delay + live, thrust)
                .Key(total, Ready(), Ease.InOutQuad)
                .Move(delay, delay + 4, 7.0f)
                .Hit(delay, delay + live, new[] { HitPoint.Blade }, dmg, level, new Vector2(2.8f, 3.2f), stun: 26, hitstop: 9, shake: 0.3f,
                    knockdown: true, heavy: true, radius: 0.09f)
                .Cue(delay + 2, StormPierceCue)
                .Fx(delay - 1, delay + live, FxKind.TrailBlade).Fx(delay, delay + 6, FxKind.AfterImage).Fx(delay, delay, FxKind.Dust).Sword();
        }

        // ---------------------------------------------------------------------------------------------- the charging thrust
        static MoveDef ChargingThrust(out float reach)
        {
            var lunge = SpearMoves.Build(KatanaMoves.Build()).lunge.hits[0];
            int tell = Frames(GaranAbilities.ChargeTell * GaranAbilities.Windup);
            int run = Frames(ChargeRunTime);
            int end = tell + run - 1;
            int total = end + 1 + 24;
            float speed = Metres(DashPx);
            reach = speed * run / 60f;
            var drawn = Hold(P().Hip(-0.14f, -0.34f, -2f).Chest(-16f).Head(14f).FootN(0.4f, 0.13f).FootF(-0.44f, 0.15f, -25f), -0.12f, 0.84f, 2f);
            var deeper = Hold(P().Hip(-0.16f, -0.36f, -2f).Chest(-18f).Head(15f).FootN(0.4f, 0.13f).FootF(-0.46f, 0.15f, -25f), -0.16f, 0.83f, 2f);
            var runA = Hold(P().Hip(0.18f, -0.26f, -18f).Chest(-30f).Head(20f).FootN(0.5f, 0.18f).FootF(-0.42f, 0.2f), 0.74f, 0.74f, 0f);
            var runB = Hold(P().Hip(0.18f, -0.22f, -18f).Chest(-30f).Head(20f).FootN(-0.36f, 0.2f).FootF(0.46f, 0.18f), 0.74f, 0.76f, 0f);
            var m = new MoveDef("shiden.chargingThrust", "Charging Thrust", total)
                .Key(10, drawn, Ease.OutQuad)
                .Key(tell - 1, deeper);
            for (int f = tell + 3, k = 0; f < end; f += 8, k++) m.Key(f, k % 2 == 0 ? runA : runB, Ease.InOutQuad);
            m.Key(end, runA)
                .Key(total, Ready(), Ease.InOutQuad)
                .Move(tell, end, speed)
                .Hit(tell, end, new[] { HitPoint.Blade }, lunge.dmg, lunge.level, new Vector2(3.2f, 2.6f), stun: lunge.stun, hitstop: lunge.hitstop,
                    shake: lunge.shake, knockdown: true, heavy: true, radius: lunge.radius, postureMul: lunge.postureMul)
                .NoPush(tell, end + 2)
                .Cue(tell - 12, ChargeGlintCue).Cue(tell + 1, SpearPierceCue)
                .Fx(tell, end, FxKind.AfterImage).Fx(tell, tell, FxKind.Dust).Fx(tell, end, FxKind.TrailBlade).Sword();
            m.onContact = new MoveDef("shiden.chargeStop", "Charging Thrust", 30)
                .Key(8, Hold(P().Hip(0.04f, -0.26f, -6f).Chest(-14f).Head(8f).Feet(0.48f, -0.32f), 0.6f, 0.94f, 6f), Ease.OutQuad)
                .Key(30, Ready(), Ease.InOutQuad)
                .Move(0, 6, 2.0f)
                .Fx(0, 0, FxKind.Dust).Sword();
            m.wallStagger = Frames(GaranAbilities.ChargeWallStun);
            return m;
        }

        // ---------------------------------------------------------------------------------------------- cues
        // The look itself is in Vfx.Shiden.cs (docs/act1-subbosses-vfx.md); his needle trail and piercing impacts come from ShidenLook on every move.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RegisterCues()
        {
            EffectCues.Register(ChargeGlintCue, (Vector2 p, in CueArgs a) => Vfx.I?.ShidenGlint(Tip(a, p, 0.95f)));
            EffectCues.Register(CrackleCue, (Vector2 p, in CueArgs a) => Vfx.I?.ShidenCrackle(Tip(a, p, 0.9f), HoldTheLine(a)));
            EffectCues.Register(ShockCue, PlayShock);
            EffectCues.Register(SpearPierceCue, (Vector2 p, in CueArgs a) => PlayPierce(p, a, false));
            EffectCues.Register(StormPierceCue, (Vector2 p, in CueArgs a) => PlayPierce(p, a, true));
            EffectCues.Register(RisingFangCue, PlayRisingFang);
            EffectCues.Register(RaijinWaveFlightCue, VanguardWaveLook.Play);
            EffectCues.Register(MakiBeatCue, (Vector2 p, in CueArgs a) => Vfx.I?.ShidenBindCross(Tip(a, p, 0.85f), Facing(a)));
            EffectCues.Register(LowSweepCue, (Vector2 p, in CueArgs a) => Vfx.I?.ShidenLowSweep(a.owner != null ? a.owner.Pos : p, Facing(a)));
        }

        static Vector2 Tip(in CueArgs a, Vector2 fallback, float k)
            => a.owner != null && a.owner.rig != null ? a.owner.rig.World(B.Sword, a.owner.BladeTipLocal * k) : fallback;
        static float Facing(in CueArgs a) => a.owner != null ? a.owner.facing : (a.dir.x >= 0f ? 1f : -1f);
        static bool HoldTheLine(in CueArgs a) => a.owner?.Phases != null && a.owner.Phases.Index >= 1;

        /// <summary>The Vanguard Break shock: strikes march away from him across the hazard.</summary>
        static void PlayShock(Vector2 pos, in CueArgs a)
        {
            var v = Vfx.I;
            if (v == null) return;
            float face = Facing(a);
            float half = Mathf.Max(0.4f, a.size.x * 0.5f);
            Vector2 g = new(pos.x, 0.02f);
            v.ShidenStrikeRow(g - Vector2.right * (face * half * 0.8f), g + Vector2.right * (face * half * 0.8f), HoldTheLine(a));
        }

        static void PlayPierce(Vector2 pos, in CueArgs a, bool storm)
        {
            var v = Vfx.I;
            if (v == null) return;
            if (a.owner != null && a.owner.rig != null)
                v.ShidenMachRelease(Tip(a, pos, 0.25f), Tip(a, pos, 1f), storm || HoldTheLine(a));
            else
                v.ShidenMachRelease(pos - Vector2.right * 0.6f, pos + Vector2.right * 0.6f, storm);
        }

        static void PlayRisingFang(Vector2 pos, in CueArgs a)
        {
            var v = Vfx.I;
            if (v == null) return;
            float face = Facing(a);
            Vector2 lo = (a.owner != null ? a.owner.Pos : pos) + new Vector2(face * 0.35f, 0.25f);
            v.ShidenRisingFang(lo, lo + new Vector2(face * 0.95f, 1.35f), HoldTheLine(a));
        }
    }
}
