using Kage.Rules;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// Shiden's abilities (SAN-106, ADR-0008): the three JS ones and the charging thrust the ticket asks for, each an ordinary
    /// <see cref="MoveDef"/> on the spear set (his Vanguard Yari).
    /// <list type="bullet">
    /// <item><b>Vanguard Break</b> (<c>vanguardBreak</c>, JS "a lighter Storm Break"): the yari raised high, armoured, while a ground mark
    /// shows where the shockwave will run; then the spear driven into the ground (a crushing overhead blow that breaks a guard) and the
    /// shockwave, a <see cref="Hazard"/> (ADR-0007) along the floor in front of him. Then a long open recovery: the JS tip is "step back
    /// from the shockwave".</item>
    /// <item><b>Brace</b> (<c>shidenBrace</c>, "KAMAE", 41-styles/shiden.js): he sets the spear low and level (a glint on the point) and holds
    /// it. A Kage who rushes onto the point (a dash, a run or a quick walk at him, a cut started on the way in) within the JS trigger is met
    /// at once by the thrust, a counter-hit critical (a <see cref="MoveDef.Brace"/>). Left alone, he lifts the point and stands open: the JS
    /// "wait it out". It is not a counter window: a blow that doesn't rush in (a kunai, a poke from where Kage stands) lands as usual.</item>
    /// <item><b>Hold the Line</b> (<c>vanguardGuard</c>, phase two, JS "a short guard whose punish can be blocked"): a counter stance
    /// (<see cref="MoveDef.counterAnswer"/>, as Kuroe's Censure) with the shaft raised upright; a blow into it is turned aside and answered by
    /// a stepping thrust; waited out, he drops it and is open. The JS tip: "do not strike into it".</item>
    /// <item><b>The charging thrust</b> (<see cref="ChargeId"/>, the ticket's signature; not a JS ability): the spear set's dash thrust run
    /// at the arena's scale. A long readable wind-up (the spear drawn back low, a glint on the point), then a run with the point out
    /// across most of the arena. It stops on contact; a guard blocks it, a parry turns it, a jump clears the low point and a dash or a roll
    /// gets through; dodged, he runs on and stands open (or meets the wall, staggered).</item>
    /// </list>
    /// <para>
    /// <b>Numbers.</b> As Garan's and Hisame's: cooldown, range and chance from <see cref="JsTables.BossAbility"/>; Vanguard Break's variant
    /// (wind-up, damage, shock and its life) and the JS shock projectile; Hold the Line's variant (stance, damage, unb); the Braced Spear's
    /// <see cref="JsTables.ShidenBrace"/> (hold, reach, trigger, damage, open); the tells scaled by the JS <c>ai.p.windup</c> for a sub-boss
    /// on Normal (<see cref="GaranAbilities.Windup"/>). What the JS writes only in its ability code is a named constant (here, or Storm
    /// Break's and the charge's in <see cref="GaranAbilities"/>). Damage is the JS damage × <see cref="GaranAbilities.UnityPerJsDamage"/>.
    /// Posture is Hisame's rule (<see cref="HisameAbilities.PostureMul"/>): a JS <c>gb</c> takes the same share of Kage's posture as of the
    /// JS guard (out of 100); a blow with no <c>gb</c> leans the JS default, damage × 1.3.
    /// The charging thrust is not in the JS, so it is built from what is: the spear set's own dash thrust (Charging Lunge: its damage and
    /// hit), the JS dash's 900 px/s, the JS charge's tell, cooldown, chance and wall stun (<c>BOSS_ABILITIES.charge</c>), the near end of
    /// the JS style's dash-thrust string (320 px); the run's length is the one Unity number (<see cref="ChargeRunTime"/>).
    /// </para>
    /// </summary>
    public static class ShidenAbilities
    {
        public const string VanguardBreakId = "vanguardBreak", BraceId = "shidenBrace", VanguardGuardId = "vanguardGuard";
        public const string ChargeId = "shiden.chargingThrust";
        public const string ShockId = "shiden.vanguardBreak.shock";
        /// <summary>A violet glint on the point: the brace is set, the charge is coming.</summary>
        public const string ChargeGlintCue = "VFX_Kage_ShidenGlint";
        /// <summary>Violet sparks on the raised yari through Vanguard Break's wind-up (the JS raikonCrackle, his tint).</summary>
        public const string CrackleCue = "VFX_Kage_ShidenCrackle";
        /// <summary>The shockwave where it strikes: the ground crack in his violet.</summary>
        public const string ShockCue = "VFX_Kage_VanguardShock";

        // ---- constants of the JS ability code (not data, so not in the generated tables) ----
        const double BraceSet = 0.22;               // 41-styles/shiden.js shidenBrace: set = 0.22 x ai.p.windup
        const double BraceThrustLive = 0.24, BraceThrustEnd = 0.5;      //   ...the thrust's area is live 0.24 s, the move is over at 0.5 s
        const double BraceKbPx = 560, BraceUpPx = 160, BraceStun = 0.5;  //   ...kb 560, up -160, hs 0.5
        const double BraceLungePx = 520, BraceDamp = 14;                 //   ...the body pushed 520 px/s, damped 14 /s: 520 / 14 px in all
        const double RushRunPx = 140, RushCutPx = 60;   // shidenRushing: walking or running at him faster than 140 px/s, a cut started on the way in faster than 60
        const double GuardLeanPerDmg = 1.3;         // 30-combat.js: a blow without gb leans dmg x 1.3 on a guard
        const double RedlinePunish = 0.1;           // 40-ai.js redlineCounter: the punish waits o.punish || 0.1 s (Hold the Line has none)
        const int CounterAnswerDamage = 12;         // the Unity counter answer the Redline Counter is measured against (Kuroe's Censure)
        const int GuardRaise = 4, GuardDrop = 14;   // Censure's: the shaft is up in 4 frames, dropped in 14 (no window)
        const double DashPx = 900;                  // 20-fighter.js startDash: the dash runs at 900 px/s
        const double ChargeStringPx = 320;          // FIGHT_STYLES.shiden: ['dash', 'weapon'] from 320 px (the generated style string)

        /// <summary>How long the charging thrust runs: Unity tuning, the one number the JS doesn't have (1.2 s at 900 px/s = 9.4 m, most of the 15.2 m arena).</summary>
        public const double ChargeRunTime = 1.2;

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

        /// <summary>Registers the four abilities in the <see cref="AbilityCatalog"/> for a Shiden of build scale <paramref name="scale"/>. Idempotent.</summary>
        public static void Register(float scale)
        {
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
        }

        /// <summary>The near end of the JS style's dash-thrust string (['dash', 'weapon']), from the generated style.</summary>
        static double DashStringNear()
        {
            foreach (var s in JsTables.FightStyle(Shiden.Id).Strings)
                if (s.Seq.Length == 2 && s.Seq[0] == "dash" && s.Seq[1] == "weapon") return s.Range[0];
            return ChargeStringPx;
        }

        /// <summary>
        /// The JS <c>shidenRushing</c>: is Kage coming in on the point? A dash; a walk or a run at him faster than 140 px/s; or a cut
        /// started on the way in (an attack before its first blow, facing him, moving at him faster than 60 px/s).
        /// </summary>
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

        // ---------------------------------------------------------------------------------------------- poses
        static Pose P() => new Pose();

        /// <summary>Both hands on the shaft: the lead hand's wrist at (x, y), the shaft pointing <paramref name="blade"/> degrees.</summary>
        static Pose Hold(Pose p, float x, float y, float blade) => p.HandF(x, y).Blade(blade).Grip();

        /// <summary>His ready stance: the point at Kage's chest (the spear set's stance).</summary>
        static Pose Ready() => Stance();

        // ---------------------------------------------------------------------------------------------- Vanguard Break
        static MoveDef VanguardBreak(JsBossAbility js, float scale)
        {
            int land = Frames(js.Windup * GaranAbilities.Windup);       // 40: the point reaches the floor
            int slam = land - 1;                                        // the first frame of the blow, and of the shock
            int total = land + Frames(GaranAbilities.StormBreakRecovery);
            var overhead = Hold(P().Hip(-0.06f, -0.1f, 8f).Chest(16f).Head(-10f).Feet(0.34f, -0.32f), 0.1f, 1.98f, -24f);
            var higher = Hold(P().Hip(-0.08f, -0.08f, 10f).Chest(20f).Head(-12f).Feet(0.34f, -0.32f), 0.04f, 2.06f, -20f);
            var down = Hold(P().Hip(0.12f, -0.42f, -10f).Chest(-44f).Head(22f).Feet(0.48f, -0.32f), 0.74f, 0.66f, -36f);
            double jsSlam = GaranAbilities.StormBreakDmg * js.Dmg;
            var shock = JsTables.Projectile("shock");
            double runPx = shock.Speed * shock.Life * js.ShockLife;     // how far the JS shockwave runs
            var hazard = new HazardDef
            {
                id = ShockId,
                telegraphFrames = slam - 1,                              // laid down on frame 1, it strikes with the slam
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
                .Key(land + 18, Hold(P().Hip(0.12f, -0.42f, -10f).Chest(-42f).Head(20f).Feet(0.48f, -0.32f), 0.72f, 0.68f, -36f))
                .Key(total, Ready(), Ease.InOutQuad)
                .Hit(slam, slam + 4, new[] { HitPoint.Blade }, Dmg(jsSlam), HitLevel.Overhead, new Vector2(3.3f, 4.0f), stun: 30, hitstop: 12,
                    shake: 0.5f, knockdown: true, heavy: true, radius: 0.18f, postureMul: HisameAbilities.PostureMul(GaranAbilities.StormBreakGb, Dmg(jsSlam)))
                .Armour(0, land)
                .Hazard(1, hazard, Metres(GaranAbilities.ShockStartPx * scale + runPx / 2))
                .Fx(land - 8, land - 8, FxKind.Glint)
                .Fx(land, land, FxKind.Dust).Fx(land, land, FxKind.Shockwave).Fx(slam - 2, land + 1, FxKind.TrailBlade).Sword();
            for (int f = 4; f < land; f += 4) m.Cue(f, CrackleCue);
            return m;
        }

        // ---------------------------------------------------------------------------------------------- Brace
        static MoveDef Brace(JsBrace js, float scale)
        {
            int set = Frames(BraceSet * GaranAbilities.Windup);         // 13: the spear set low and level
            int held = set + Frames(js.Hold) - 1;                       // 87: the last frame a rush sets the thrust off
            int total = held + 1 + Frames(js.Open);                     // 121: then the point lifted, open
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

        /// <summary>The braced thrust: the point driven straight out the JS reach, live 0.24 s; a counter-hit critical.</summary>
        static MoveDef BraceThrust(JsBrace js, float scale)
        {
            int live = Frames(BraceThrustLive), total = Frames(BraceThrustEnd);    // 14, 30
            float lungeM = Metres(BraceLungePx / BraceDamp);                        // the JS body push: 520 px/s damped at 14 /s
            var thrust = Hold(P().Hip(0.18f, -0.22f, -14f).Chest(-28f).Head(14f).Feet(0.52f, -0.34f), 0.86f, 0.92f, 2f);
            var m = new MoveDef("shiden.braceThrust", "Braced Thrust", total)
                .Key(2, thrust, Ease.OutExpo)
                .Key(live, thrust)
                .Key(total, Ready(), Ease.InOutQuad)
                .Move(1, 4, lungeM * 60f / 4f)
                .Hit(0, live, new[] { HitPoint.Blade }, Dmg(js.Dmg), HitLevel.Mid, new Vector2(Metres(BraceKbPx), Metres(BraceUpPx)), stun: Frames(BraceStun),
                    hitstop: 10, shake: 0.3f, heavy: true, radius: 0.09f, postureMul: Lean(js.Dmg))
                .Fx(0, live, FxKind.TrailBlade).Fx(0, 0, FxKind.Dust).Sword();
            m.hits[0].critical = true;      // FIGHT_STYLES.shiden.crit: the spear met a man rushing onto it
            return m;
        }

        // ---------------------------------------------------------------------------------------------- Hold the Line
        static Pose GuardBody() => P().Hip(-0.05f, -0.14f, -2f).Chest(2f).Head(-4f).Feet(0.26f, -0.32f);

        /// <summary>The stance: the shaft comes up upright before him, holds for the JS stance, then drops (Kuroe's Censure with his poses).</summary>
        static MoveDef HoldTheLine(JsBossAbility js)
        {
            int counterTo = GuardRaise + Frames(js.Stance) - 1;
            int total = counterTo + 1 + GuardDrop;
            var m = new MoveDef("shiden.holdTheLine", "Hold the Line", total)
                .Key(GuardRaise, Hold(GuardBody(), 0.34f, 1.12f, 84f), Ease.OutExpo)
                .Key(counterTo, Hold(GuardBody().Hip(-0.05f, -0.15f, -2f), 0.34f, 1.11f, 86f))
                .Key(total, Ready(), Ease.InOutQuad)
                .Fx(GuardRaise, GuardRaise, FxKind.Glint).Sword();
            m.counterFrom = GuardRaise;
            m.counterTo = counterTo;
            m.counterFrontOnly = true;
            m.counterAnswer = HoldTheLinePunish(js);
            return m;
        }

        /// <summary>The punish: the JS delay, then a stepping thrust (0.2 s live, as the JS area), a knockdown; blockable when the JS says so.</summary>
        static MoveDef HoldTheLinePunish(JsBossAbility js)
        {
            int delay = Frames(js.Punish > 0 ? js.Punish : RedlinePunish);
            int live = 12;                                      // JS: the area is live 0.2 s after the delay
            int total = delay + 33;                             // JS: the ability ends 0.55 s after the delay
            var level = js.Unb == true ? HitLevel.Unblockable : HitLevel.Mid;
            int dmg = Mathf.RoundToInt(CounterAnswerDamage * (float)js.Dmg);
            var thrust = Hold(P().Hip(0.2f, -0.2f, -16f).Chest(-28f).Head(14f).Feet(0.52f, -0.34f), 0.84f, 1.0f, 4f);
            return new MoveDef("shiden.holdTheLinePunish", "Hold the Line", total)
                .Key(Mathf.Max(1, delay - 1), Hold(GuardBody().Chest(-4f), 0.02f, 1.0f, 6f), Ease.OutQuad)
                .Key(delay + 3, thrust, Ease.OutExpo)
                .Key(delay + live, thrust)
                .Key(total, Ready(), Ease.InOutQuad)
                .Move(delay, delay + 4, 7.0f)
                .Hit(delay, delay + live, new[] { HitPoint.Blade }, dmg, level, new Vector2(2.8f, 3.2f), stun: 26, hitstop: 9, shake: 0.3f,
                    knockdown: true, heavy: true, radius: 0.09f)
                .Fx(delay - 1, delay + live, FxKind.TrailBlade).Fx(delay, delay + 6, FxKind.AfterImage).Fx(delay, delay, FxKind.Dust).Sword();
        }

        // ---------------------------------------------------------------------------------------------- the charging thrust
        static MoveDef ChargingThrust(out float reach)
        {
            var lunge = SpearMoves.Build(KatanaMoves.Build()).lunge.hits[0];       // the spear set's dash thrust
            int tell = Frames(GaranAbilities.ChargeTell * GaranAbilities.Windup);  // 35: drawn back low
            int run = Frames(ChargeRunTime);                                       // 72
            int end = tell + run - 1;
            int total = end + 1 + 24;                                              // the lunge's recovery, open
            float speed = Metres(DashPx);
            reach = speed * run / 60f;
            var drawn = Hold(P().Hip(-0.14f, -0.34f, -2f).Chest(-16f).Head(14f).FootN(0.4f, 0.13f).FootF(-0.44f, 0.15f, -25f), -0.12f, 0.84f, 2f);
            var deeper = Hold(P().Hip(-0.16f, -0.36f, -2f).Chest(-18f).Head(15f).FootN(0.4f, 0.13f).FootF(-0.46f, 0.15f, -25f), -0.16f, 0.83f, 2f);
            var runA = Hold(P().Hip(0.18f, -0.26f, -18f).Chest(-30f).Head(20f).FootN(0.5f, 0.18f).FootF(-0.42f, 0.2f), 0.74f, 0.86f, 2f);
            var runB = Hold(P().Hip(0.18f, -0.22f, -18f).Chest(-30f).Head(20f).FootN(-0.36f, 0.2f).FootF(0.46f, 0.18f), 0.74f, 0.88f, 2f);
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
                .Cue(tell - 12, ChargeGlintCue)
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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterCues()
        {
            EffectCues.Register(ChargeGlintCue, PlayGlint);
            EffectCues.Register(CrackleCue, PlayCrackle);
            EffectCues.Register(ShockCue, PlayShock);
        }

        /// <summary>A pale violet glint and a star on the point (the JS <c>w.fx.glint(..., '#e4d6ff')</c>).</summary>
        static void PlayGlint(Vector2 pos, in CueArgs a)
        {
            var vfx = Vfx.I;
            if (vfx == null) return;
            Vector2 at = pos;
            if (a.owner != null && a.owner.rig != null) at = a.owner.rig.World(B.Sword, a.owner.BladeTipLocal * 0.95f);
            vfx.Glint(at, ShidenPalette.Pale);
            vfx.Star(at, 0.45f, Color.white, ShidenPalette.Accent);
        }

        static void PlayCrackle(Vector2 pos, in CueArgs a)
        {
            var vfx = Vfx.I;
            if (vfx == null) return;
            Vector2 at = pos;
            if (a.owner != null && a.owner.rig != null) at = a.owner.rig.World(B.Sword, a.owner.BladeTipLocal * 0.9f);
            for (int i = 0; i < 3; i++)
                vfx.Spark(at + new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(-0.1f, 0.15f)), new Vector2(Random.Range(-1f, 1f), Random.Range(0.4f, 1.6f)),
                    i == 0 ? Color.white : ShidenPalette.Accent, 0.045f, 0.22f);
        }

        /// <summary>Placeholder ground crack in his violet: a shockwave and dust the length of the box, sparks running along the floor.</summary>
        static void PlayShock(Vector2 pos, in CueArgs a)
        {
            var vfx = Vfx.I;
            if (vfx == null) return;
            Color c = ShidenPalette.Accent;
            float half = Mathf.Max(0.4f, a.size.x * 0.5f);
            vfx.Shockwave(pos + Vector2.up * 0.03f, half, c);
            vfx.Dust(pos, Mathf.Max(0.9f, a.size.x * 0.6f), 0f);
            for (int i = -3; i <= 3; i++)
                vfx.Spark(pos + new Vector2(i * half / 3.5f, 0.05f), new Vector2(i * 0.6f, 2.2f + Mathf.Abs(i) * 0.2f), i % 2 == 0 ? Color.white : c, 0.06f, 0.35f);
            GameFeel.Shake(0.22f);
        }
    }
}
