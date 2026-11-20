using System.Collections.Generic;
using Kage.Rules;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// Raikon's four abilities (SAN-107, ADR-0008), each an ordinary <see cref="MoveDef"/> on the tachi set (the Raikō no Tachi he fights with).
    /// <list type="bullet">
    /// <item><b>Thunder Swing</b> (<c>thunderSwing</c>): a long readable wind-up (armoured, red sparks on the blade, a glint 0.2 s before the
    /// release), then one wide heavy cut and a lightning wave racing along the floor (a ground-wave <see cref="Projectile"/>, unblockable, knocks
    /// down): jump it or dash through it. The cut itself can be blocked. From the last phase on (<see cref="Fighter.SecondWaveDelay"/>) the blade
    /// is drawn back again and a second cut and wave follow after the JS delay, "punishing a dodge made too early".</item>
    /// <item><b>Storm Break</b> (<c>stormBreak</c>): the tachi raised overhead (armoured, a glint), then driven into the ground: a crushing blow
    /// that breaks a guard, and a shock area in front of him, a <see cref="Hazard"/> laid with the wind-up so the ground mark shows. Then an open
    /// recovery: the JS tip for Storm Break is to step out of the shock and hit the opening.</item>
    /// <item><b>Skyfall</b> (<c>skyfall</c>): he raises the tachi to the sky; a red mark opens on the floor under Kage and follows him while the
    /// storm gathers, then locks; half a second later a bolt strikes the mark. A <see cref="Hazard"/> that <i>follows</i> its target for the
    /// charge (<see cref="HazardDef.followFrames"/>) and then stands: step off the locked mark in time. The AI calls it only from far away
    /// (JS range 460 px and up), his answer to a Kage who keeps his distance.</item>
    /// <item><b>Redline Counter</b> (<c>redlineCounter</c>, phase two on): the 41-styles/raikon.js martial stance: a low red guard for a long beat
    /// (1.5 s, 1.25 s in the last phase). The stance settles for 0.24 s before it can catch anything. A melee blow from the front is turned
    /// aside and answered at once by an unblockable critical cut. Left alone the stance is spent: he drops it, loses a large share of posture
    /// and stands open. The JS tip: never attack into Redline; bait it out and hit the spent stance.</item>
    /// </list>
    /// <para>
    /// <b>Numbers.</b> As the sub-bosses': cooldown, range and chance from <see cref="JsTables.BossAbility"/> (the Raikon-style overrides are
    /// already in the generated row), Skyfall's from <see cref="JsTables.RaikonSkyfall"/>, Redline's stance from
    /// <see cref="JsTables.RaikonRedline"/>, the Thunder Swing wave and the Storm Break shock from the JS projectiles, the tells scaled by the JS
    /// <c>ai.p.windup</c> for a boss on Normal (<see cref="Windup"/>). What 40-ai.js writes only in its code is a named constant here. Damage is the
    /// JS damage × <see cref="GaranAbilities.UnityPerJsDamage"/>. Posture is Hisame's rule (<see cref="HisameAbilities.PostureMul"/>): a JS
    /// <c>gb</c> takes the same share of Kage's posture as of the JS guard (out of 100).
    /// </para>
    /// </summary>
    public static class RaikonAbilities
    {
        public const string ThunderSwingId = "thunderSwing", StormBreakId = "stormBreak", SkyfallId = "skyfall", RedlineId = "redlineCounter";
        public const string WaveId = "raikon.thunderSwing.wave", ShockId = "raikon.stormBreak.shock", BoltId = "raikon.skyfall";
        public const string MoveSwing = "raikon.thunderSwing", MoveSwing2 = "raikon.thunderSwing2", MoveBreak = "raikon.stormBreak", MoveSkyfall = "raikon.skyfall";
        public const string MoveRedline = "raikon.redlineCounter", MoveRedlineShort = "raikon.redlineCounterShort", MovePunish = "raikon.redlinePunish";

        // ---- cues: the JS VFX manifest ids (ADR-0007) where the JS has the effect, VFX_Kage_ for the rest ----
        /// <summary>Red motes and arcs pulled onto the blade (the JS raikonCrackle): through every wind-up.</summary>
        public const string BladeChargeCue = "VFX_Raikon_Blade_Charge";
        /// <summary>The red glint just before the release (the JS raikonGlint; the manifest's Blade Charge ends in it, so a cue of its own for the beat).</summary>
        public const string GlintCue = "VFX_Kage_RaikonGlint";
        /// <summary>The lightning wave racing along the floor (rides the projectile).</summary>
        public const string ThunderWaveCue = "VFX_Raikon_Thunder_Wave";
        /// <summary>The bolt that strikes Skyfall's mark.</summary>
        public const string SkyStrikeCue = "VFX_Raikon_Sky_Strike";
        /// <summary>The crescent of red lightning in front of him while the Redline stance stands.</summary>
        public const string RedlineGuardCue = "VFX_Raikon_Redline_Guard";
        /// <summary>The storm round him: played as he calls Skyfall.</summary>
        public const string StormMantleCue = "VFX_Raikon_Storm_Mantle";

        // ---- constants of the JS ability code (40-ai.js; not data, so not in the generated tables) ----
        const double SwingWindup = 0.66, SwingRecovery = 0.6, SwingReachPx = 210, SwingDmg = 15, SwingGb = 70;       // thunderSwing: W = 0.66 x tell; raikonRelease: 15 dmg, gb 70, reach 210
        const double SwingKbPx = 420, SwingUpPx = 240, SwingStun = 0.5;                                              //   ...kb 420, up -240, hs 0.5
        const double WaveOffsetPx = 110;                                                                             //   ...the wave starts 110 px x scale in front
        const double GlintLead = 0.2, SecondCharge = 0.2;                                                            //   ...the glint 0.2 s before the release; the second charge starts 0.2 s after the first release
        const double BreakWindup = 0.58, BreakRecovery = 0.75, BreakDmg = 19, BreakGb = 110, BreakReachPx = 200;     // stormBreak: W = 0.58 x tell, + 0.75 s; 19 dmg, gb 110, strike width 200
        const double BreakKbPx = 380, BreakUpPx = 460, BreakStun = 0.6, BreakShock = 0.9, BreakShockLife = 0.6;      //   ...kb 380, up -460, hs 0.6; the shock projectile x 0.9 damage, life x 0.6
        const double BreakGlintLead = 0.18, ShockStartPx = 120;                                                      //   ...glint 0.18 s before; the shock starts 120 px x scale in front
        const double SkyfallRecovery = 0.6, SkyfallHeightPx = 490, SkyfallKbPx = 260, SkyfallUpPx = 460, SkyfallStun = 0.6, SkyfallGb = 120;   // skyfall: dur = W + F + 0.6; area 490 tall; kb 260, up -460, hs 0.6, gb 120
        const int SkyfallStrikeFrames = 3;                                                                           // 38-skyfall.js: the contact is 3 frames
        const double GuardLeanPerDmg = 1.3;                                                                          // 30-combat.js: a blow without gb leans dmg x 1.3 on a guard
        const double PunishLive = 0.2, PunishTail = 0.55, PunishDashPx = 1100;                                       // redlineCounter: live 0.2 s after the delay; over 0.55 s after it; vx 1100
        const double PunishKbPx = 520, PunishUpPx = 380, PunishStun = 0.55;                                          //   ...kb 520, up -380, hs 0.55

        static float _scale = 1.24f;

        static int Frames(double seconds) => Mathf.RoundToInt((float)(seconds * 60.0));
        static float Metres(double px) => (float)JsUnits.PxToMetres(px);
        static int Dmg(double js) => Mathf.Max(1, Mathf.RoundToInt((float)js * GaranAbilities.UnityPerJsDamage));

        /// <summary>JS <c>ai.p.windup</c> for a boss on Normal: the mode's wind-up times the boss role's (1 x 0.93).</summary>
        public static double Windup => JsTables.Difficulty("normal").Windup * JsTables.AiRole("boss").Windup;

        static AbilityDef Def(string id, MoveDef move)
        {
            var js = JsTables.BossAbility(id);
            return new AbilityDef
            {
                id = id, move = move, cooldownFrames = Frames(js.Cd),
                range = new Vector2(Metres(js.Range[0]), Metres(js.Range[1])), chance = (float)js.Chance,
            };
        }

        /// <summary>Registers the four abilities in the <see cref="AbilityCatalog"/> for a Raikon of build scale <paramref name="scale"/>. Idempotent.</summary>
        public static void Register(float scale)
        {
            _scale = scale;
            SwingSecond.Clear();
            _redlineShort = null;
            _punish = null;
            var swing = ThunderSwing(0);
            swing.variantFor = f => SecondWaveSwing(f.SecondWaveDelay);
            AbilityCatalog.Register(Def(ThunderSwingId, swing));
            AbilityCatalog.Register(Def(StormBreakId, StormBreak()));
            AbilityCatalog.Register(Def(SkyfallId, Skyfall()));
            var redline = Redline(0);
            redline.variantFor = f => RedlineForPhase(f.Phases != null ? f.Phases.Index : 0);
            var d = Def(RedlineId, redline);
            AbilityCatalog.Register(d);
        }

        // ---------------------------------------------------------------------------------------------- poses
        static Pose P() => new Pose();

        /// <summary>Both hands on the hilt: the lead hand's wrist at (x, y), the blade pointing <paramref name="blade"/> degrees.</summary>
        static Pose Hold(Pose p, float x, float y, float blade) => p.HandF(x, y).Blade(blade).Grip();

        static Pose Lunge(float hx = 0.1f, float hy = -0.12f, float feetN = 0.4f, float feetF = -0.26f) => P().Hip(hx, hy, -12f).Chest(-24f).Head(12f).Feet(feetN, feetF);

        /// <summary>The wide cut's wind-up: the blade drawn back low behind the hip (the tachi's Broad Cut).</summary>
        static Pose SwingBack() => Hold(P().Hip(-0.1f, -0.18f, 8f).Chest(10f).Head(-4f).Feet(0.22f, -0.32f), -0.18f, 1.05f, -164f);
        static Pose SwingBackDeep() => Hold(P().Hip(-0.12f, -0.2f, 10f).Chest(12f).Head(-6f).Feet(0.22f, -0.32f), -0.2f, 1.07f, -166f);
        static Pose SwingThrough() => Hold(Lunge(), 0.5f, 1.12f, 4f);
        static Pose SwingEnd() => Hold(Lunge(), 0.46f, 1.24f, 22f);

        /// <summary>Tonbo: the blade raised high and upright by the shoulder (the tachi's Thunder Fall).</summary>
        static Pose Overhead() => Jodan(P().Hip(0f, -0.06f, 4f).Chest(10f).Head(-8f).Feet(0.28f, -0.3f)).HandF(0.04f, 1.68f).Blade(112f);
        static Pose OverheadHigh() => Jodan(P().Hip(-0.02f, -0.04f, 6f).Chest(14f).Head(-12f).Feet(0.3f, -0.3f)).HandF(0.02f, 1.78f).Blade(106f);
        static Pose Slam() => Hold(P().Hip(0.12f, -0.42f, -10f).Chest(-44f).Head(22f).Feet(0.48f, -0.32f), 0.6f, 0.7f, -44f);

        /// <summary>The call to the sky: the tachi lifted at arm's length, the point to the clouds.</summary>
        static Pose CallSky() => Hold(P().Hip(-0.02f, -0.04f, 4f).Chest(12f).Head(-16f).Feet(0.28f, -0.3f), 0.08f, 1.8f, 98f);
        static Pose CallDown() => Hold(P().Hip(0.1f, -0.2f, -12f).Chest(-30f).Head(16f).Feet(0.44f, -0.28f), 0.5f, 0.9f, -40f);

        /// <summary>The Redline guard: low, the blade across the body (the tachi's Redline Guard, a little lower).</summary>
        static Pose RedGuard() => Hold(P().Hip(-0.03f, -0.16f, -2f).Chest(-4f).Head(-4f).Feet(0.22f, -0.3f), 0.28f, 1.16f, 80f);
        static Pose RedGuardHold() => Hold(P().Hip(-0.03f, -0.17f, -2f).Chest(-4f).Head(-4f).Feet(0.22f, -0.3f), 0.28f, 1.15f, 82f);
        static Pose Spent() => Hold(P().Hip(-0.04f, -0.22f, 4f).Chest(10f).Head(10f).Feet(0.22f, -0.3f), 0.2f, 0.8f, 40f);
        static Pose Ready() => Stance();

        // ---------------------------------------------------------------------------------------------- Thunder Swing
        static readonly Dictionary<int, MoveDef> SwingSecond = new();

        /// <summary>The variant a fighter in a second-wave phase starts instead (the delay is the phase's <see cref="PhaseDef.secondWave"/>); null in the early phases.</summary>
        public static MoveDef SecondWaveSwing(float secondWaveDelay)
        {
            if (secondWaveDelay <= 0f) return null;
            int delay = Frames(secondWaveDelay * System.Math.Max(1.0, Windup));         // the JS: secondWave x max(1, tell)
            if (!SwingSecond.TryGetValue(delay, out var m)) SwingSecond[delay] = m = ThunderSwing(delay);
            return m;
        }

        static ProjectileDef WaveDef()
        {
            var w = JsTables.Projectile("thunderwave");
            return new ProjectileDef
            {
                id = WaveId, speed = Metres(w.Speed), groundWave = true, radius = Metres(w.R), lifeFrames = Frames(w.Life), maxHits = 1,
                hit = new HitWindow
                {
                    dmg = Dmg(w.Dmg), level = w.Unb ? HitLevel.Unblockable : HitLevel.Mid, kb = new Vector2(Metres(w.Kb), Metres(-w.Up)),
                    stun = Frames(w.Hs), hitstop = 8, shake = 0.3f, knockdown = w.Kd, heavy = true,
                },
                flightCue = ThunderWaveCue,
            };
        }

        /// <summary>The wide cut's reach: the JS area is 210 px wide from 10 px behind him, x his scale; the blade line (the painted blade) reaches less, so the hit is a fat arc.</summary>
        public static float SwingRadius(float scale) => Mathf.Max(0.09f, 0.3f);

        /// <summary>Thunder Swing; <paramref name="secondDelay"/> &gt; 0 frames adds the late second cut and wave (the last phase).</summary>
        static MoveDef ThunderSwing(int secondDelay)
        {
            var js = JsTables.BossAbility(ThunderSwingId);
            int w = Frames(SwingWindup * Windup);                       // 37: the release
            int release = secondDelay > 0 ? w + secondDelay : w;        // the last release
            int total = release + Frames(SwingRecovery);
            var wave = WaveDef();
            var from = new Vector2(Metres(WaveOffsetPx * _scale), wave.radius);
            var m = new MoveDef(secondDelay > 0 ? MoveSwing2 : MoveSwing, "Thunder Swing", total)
                .Key(10, SwingBack(), Ease.OutQuad)
                .Key(w - 1, SwingBackDeep())
                .Key(w + 2, SwingThrough(), Ease.OutExpo)
                .Key(w + 9, SwingEnd());
            AddCut(m, w, wave, from);
            if (secondDelay > 0)
            {
                int back = w + Frames(SecondCharge);                    // the blade drawn back again
                m.Key(back, SwingBack(), Ease.InOutQuad)
                    .Key(release - 1, SwingBackDeep())
                    .Key(release + 2, SwingThrough(), Ease.OutExpo)
                    .Key(release + 9, SwingEnd());
                AddCut(m, release, wave, from);
                for (int f = back + 2; f < release; f += 3) m.Cue(f, BladeChargeCue);
                m.Cue(release - Frames(GlintLead), GlintCue);
            }
            m.Key(total, Ready(), Ease.InOutQuad)
                .Armour(0, w - 1)                                       // the JS super armour, off at the release (and not back on for the second)
                .Cue(w - Frames(GlintLead), GlintCue)
                .Fx(w - 2, w + 8, FxKind.TrailBlade).Sword();
            for (int f = 3; f < w; f += 3) m.Cue(f, BladeChargeCue);
            return m;
        }

        /// <summary>One release: the cut live for 5 frames and the wave thrown.</summary>
        static void AddCut(MoveDef m, int at, ProjectileDef wave, Vector2 from)
        {
            int dmg = Dmg(SwingDmg);
            m.Hit(at, at + 4, new[] { HitPoint.Blade }, dmg, HitLevel.Mid, new Vector2(Metres(SwingKbPx), Metres(SwingUpPx)), stun: Frames(SwingStun), hitstop: 10,
                    shake: 0.4f, heavy: true, radius: SwingRadius(_scale), postureMul: HisameAbilities.PostureMul(SwingGb, dmg))
                .Fx(at, at, FxKind.Dust).Fx(at, at, FxKind.Shockwave);
            m.hits[m.hits.Count - 1].fixedDamage = true;                // the JS damage, not times the tachi's 0.78
            if (m.projectile == null) m.Throw(at, wave, from);
            else m.ThrowAlso(at, wave, from);
        }

        // ---------------------------------------------------------------------------------------------- Storm Break
        static MoveDef StormBreak()
        {
            var js = JsTables.BossAbility(StormBreakId);
            int land = Frames(BreakWindup * Windup);                    // 32: the blade reaches the floor
            int slam = land;
            int total = land + Frames(BreakRecovery);
            int dmg = Dmg(BreakDmg);
            var shock = JsTables.Projectile("shock");
            double runPx = shock.Speed * shock.Life * BreakShockLife;   // how far the JS shockwave runs
            var hazard = new HazardDef
            {
                id = ShockId,
                telegraphFrames = slam - 1,                              // laid down on frame 1, it strikes with the slam
                strikeFrames = Frames(shock.Life * BreakShockLife),
                size = new Vector2(Metres(runPx + 2 * shock.R), Metres(2 * shock.R)),
                hit = new HitWindow
                {
                    dmg = Dmg(shock.Dmg * BreakShock), level = shock.Unb ? HitLevel.Unblockable : HitLevel.Mid,
                    kb = new Vector2(Metres(shock.Kb), Metres(-shock.Up)), stun = Frames(shock.Hs), hitstop = 8, shake = 0.3f, knockdown = shock.Kd, heavy = true,
                },
                strikeCue = GaranAbilities.ShockCue,
            };
            var m = new MoveDef(MoveBreak, "Storm Break", total)
                .Key(8, Overhead(), Ease.OutQuad)
                .Key(land - 4, OverheadHigh())
                .Key(land, Slam(), Ease.InCubic)
                .Key(land + 18, Slam())
                .Key(total, Ready(), Ease.InOutQuad)
                .Hit(slam, slam + 4, new[] { HitPoint.Blade }, dmg, HitLevel.Overhead, new Vector2(Metres(BreakKbPx), Metres(BreakUpPx)), stun: Frames(BreakStun),
                    hitstop: 12, shake: 0.5f, knockdown: true, heavy: true, radius: SwingRadius(_scale), postureMul: HisameAbilities.PostureMul(BreakGb, dmg))
                .Armour(0, land)
                .Hazard(1, hazard, Metres(ShockStartPx * _scale + runPx / 2))
                .Cue(land - Frames(BreakGlintLead), GlintCue)
                .Fx(land, land, FxKind.Dust).Fx(land, land, FxKind.Shockwave).Fx(slam - 2, land + 1, FxKind.TrailBlade).Sword();
            m.hits[0].fixedDamage = true;
            for (int f = 3; f < land; f += 3) m.Cue(f, BladeChargeCue);
            return m;
        }

        // ---------------------------------------------------------------------------------------------- Skyfall
        static MoveDef Skyfall()
        {
            var s = JsTables.RaikonSkyfall;
            int charge = Frames(s.Charge * Windup);                     // 28: the mark follows him
            int fall = Frames(s.Fall);                                  // 30: from the lock to the bolt
            int strike = charge + fall;                                 // 58: the bolt lands on the mark
            int total = strike + Frames(SkyfallRecovery);
            int dmg = Dmg(s.Dmg);
            var hazard = new HazardDef
            {
                id = BoltId,
                telegraphFrames = strike - 1,                            // laid on frame 1 under Kage; the bolt strikes on frame `strike`
                followFrames = charge - 1,                               // the mark follows him until it locks
                strikeFrames = SkyfallStrikeFrames,
                size = new Vector2(Metres(s.Width), Metres(SkyfallHeightPx)),
                hit = new HitWindow
                {
                    dmg = dmg, level = HitLevel.Mid, kb = new Vector2(Metres(SkyfallKbPx), Metres(SkyfallUpPx)), stun = Frames(SkyfallStun), hitstop = 12, shake = 0.6f,
                    knockdown = true, heavy = true, postureMul = HisameAbilities.PostureMul(SkyfallGb, dmg),
                },
                telegraphCue = "VFX_Kage_HazardTelegraph",
                strikeCue = SkyStrikeCue,
            };
            var m = new MoveDef(MoveSkyfall, "Skyfall", total)
                .Key(10, CallSky(), Ease.OutQuad)
                .Key(strike - 6, CallSky())
                .Key(strike - 2, CallDown(), Ease.InCubic)
                .Key(strike + 14, CallDown())
                .Key(total, Ready(), Ease.InOutQuad)
                .HazardOnTarget(1, hazard)
                .Armour(0, strike)
                .Cue(2, StormMantleCue)
                .Sword();
            for (int f = 3; f < strike; f += 3) m.Cue(f, BladeChargeCue);
            return m;
        }

        // ---------------------------------------------------------------------------------------------- Redline Counter
        /// <summary>The shorter stance of the last phase (JS <c>stance[1]</c>, 1.25 s) from phase index 2 on; null earlier (the 1.5 s stance).</summary>
        public static MoveDef RedlineForPhase(int phaseIndex) => phaseIndex >= 2 ? _redlineShort ??= Redline(1) : null;
        static MoveDef _redlineShort;

        /// <summary>The counter stance; <paramref name="stanceIndex"/> 0 holds it the JS stance of phase two (1.5 s), 1 that of the last phase (1.25 s).</summary>
        static MoveDef Redline(int stanceIndex)
        {
            var r = JsTables.RaikonRedline;
            int stance = Frames(r.Stance[stanceIndex]);
            int settle = Frames(r.Settle);
            int counterTo = stance - 1;
            int spent = Frames(r.Spent);
            int total = stance + spent;
            var m = new MoveDef(stanceIndex == 0 ? MoveRedline : MoveRedlineShort, "Redline Counter", total)
                .Key(6, RedGuard(), Ease.OutExpo)
                .Key(counterTo, RedGuardHold())
                .Key(stance + 4, Spent(), Ease.OutQuad)
                .Key(total, Ready(), Ease.InOutQuad)
                .Cue(1, RedlineGuardCue)
                .SelfPosture(stance, (float)(r.SpentPosture / 100.0))      // the stance is spent: a large share of his posture goes
                .Sword();
            for (int f = 4; f < stance; f += 4) m.Cue(f, BladeChargeCue);
            m.counterFrom = settle;                                          // the stance settles before it counters: an earlier blow lands as a plain hit
            m.counterTo = counterTo;
            m.counterFrontOnly = true;
            m.counterAnswer = RedlinePunish();
            return m;
        }

        static MoveDef _punish;

        /// <summary>The punish: the JS delay (0.1 x tell), then a diagonal cut that dashes in, unblockable and a critical, knocks down.</summary>
        static MoveDef RedlinePunish()
        {
            if (_punish != null) return _punish;
            var r = JsTables.RaikonRedline;
            int delay = Frames(r.Punish * Windup);                              // 6
            int live = Frames(PunishLive);                                      // 12
            int total = delay + Frames(PunishTail);
            var kesaBack = Hold(Lunge(0.02f, -0.08f, 0.3f, -0.2f).Chest(-6f), 0.04f, 1.44f, 146f);
            var cut = Hold(Lunge(0.1f, -0.14f, 0.4f, -0.28f), 0.5f, 1.1f, -8f);
            var cutEnd = Hold(Lunge(0.1f, -0.14f, 0.4f, -0.28f), 0.46f, 1.0f, -22f);
            _punish = new MoveDef(MovePunish, "Redline Counter", total)
                .Key(Mathf.Max(1, delay - 1), kesaBack, Ease.OutQuad)
                .Key(delay + 3, cut, Ease.OutExpo)
                .Key(delay + live, cutEnd)
                .Key(total, Ready(), Ease.InOutQuad)
                .Move(delay, delay + 5, 8f)
                .Hit(delay, delay + live, new[] { HitPoint.Blade }, Dmg(r.Dmg), HitLevel.Unblockable, new Vector2(Metres(PunishKbPx), Metres(PunishUpPx)),
                    stun: Frames(PunishStun), hitstop: 12, shake: 0.4f, knockdown: true, heavy: true, radius: SwingRadius(_scale))
                .Fx(delay - 1, delay + live, FxKind.TrailBlade).Fx(delay, delay + 6, FxKind.AfterImage).Fx(delay, delay, FxKind.Dust).Sword();
            _punish.hits[0].fixedDamage = true;
            _punish.hits[0].critical = true;                                    // FIGHT_STYLES.raikon.crit: the Redline punish
            return _punish;
        }

        // ---------------------------------------------------------------------------------------------- cues
        static readonly Color StormRed = KMath.Hex("#FF3B2F");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterCues()
        {
            EffectCues.Register(BladeChargeCue, PlayCharge);
            EffectCues.Register(GlintCue, PlayGlint);
            EffectCues.Register(ThunderWaveCue, ThunderWaveLook.Play);
            EffectCues.Register(SkyStrikeCue, PlaySkyStrike);
            EffectCues.Register(RedlineGuardCue, PlayRedlineGuard);
            EffectCues.Register(StormMantleCue, PlayMantle);
        }

        static Vector2 Tip(in CueArgs a, Vector2 fallback, float k)
        {
            if (a.owner != null && a.owner.rig != null) return a.owner.rig.World(B.Sword, a.owner.BladeTipLocal * k);
            return fallback;
        }

        /// <summary>Red sparks gathering on the blade (the JS raikonCrackle).</summary>
        static void PlayCharge(Vector2 pos, in CueArgs a)
        {
            var vfx = Vfx.I;
            if (vfx == null) return;
            Vector2 at = Tip(a, pos, Random.Range(0.55f, 1f));
            for (int i = 0; i < 3; i++)
                vfx.Spark(at + new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(-0.1f, 0.15f)), new Vector2(Random.Range(-1f, 1f), Random.Range(0.4f, 1.8f)),
                    i == 0 ? Color.white : StormRed, 0.05f, 0.24f);
        }

        /// <summary>A red glint and a star on the point (the JS raikonGlint).</summary>
        static void PlayGlint(Vector2 pos, in CueArgs a)
        {
            var vfx = Vfx.I;
            if (vfx == null) return;
            Vector2 at = Tip(a, pos, 0.95f);
            vfx.Glint(at, StormRed);
            vfx.Star(at, 0.5f, Color.white, StormRed);
        }

        /// <summary>The bolt that falls on Skyfall's mark: a white-cored red line from the sky, a flash, a shockwave and sparks.</summary>
        static void PlaySkyStrike(Vector2 pos, in CueArgs a)
        {
            var vfx = Vfx.I;
            if (vfx == null) return;
            var top = pos + new Vector2(0.25f, 6.5f);
            vfx.SlashLine(top, pos, StormRed, Color.white, 0.32f, 0.16f);
            vfx.SlashLine(top + new Vector2(-0.5f, -0.5f), pos + new Vector2(0.15f, 0.2f), StormRed, Color.white, 0.22f, 0.08f);
            vfx.Shockwave(pos + Vector2.up * 0.03f, Mathf.Max(0.8f, a.size.x), StormRed);
            vfx.Star(pos + Vector2.up * 0.3f, 0.9f, Color.white, StormRed);
            vfx.Dust(pos, 1.2f, 0f);
            for (int i = -3; i <= 3; i++)
                vfx.Spark(pos + new Vector2(i * 0.12f, 0.05f), new Vector2(i * 0.7f, 2.6f + Mathf.Abs(i) * 0.3f), i % 2 == 0 ? Color.white : StormRed, 0.06f, 0.4f);
            GameFeel.Shake(0.5f);
        }

        /// <summary>A crescent of red lightning in front of him, standing a beat (the JS Redline guard ring).</summary>
        static void PlayRedlineGuard(Vector2 pos, in CueArgs a)
        {
            var vfx = Vfx.I;
            if (vfx == null || a.owner == null) return;
            var c = a.owner.Pos + new Vector2(a.owner.facing * 0.5f, 1.1f);
            vfx.Ring(c, 0.5f, 1.15f, 0.07f, StormRed, 0.5f, false, 1.4f);
            vfx.Star(c + Vector2.right * a.owner.facing * 0.55f, 0.55f, Color.white, StormRed);
            for (int i = 0; i < 6; i++)
                vfx.Spark(c + new Vector2(a.owner.facing * 0.55f, (i - 2.5f) * 0.22f), new Vector2(a.owner.facing * Random.Range(0.2f, 0.8f), Random.Range(-0.5f, 0.8f)),
                    i % 2 == 0 ? Color.white : StormRed, 0.05f, 0.3f);
        }

        /// <summary>The storm gathering round his body: arcs and embers.</summary>
        static void PlayMantle(Vector2 pos, in CueArgs a)
        {
            var vfx = Vfx.I;
            if (vfx == null || a.owner == null) return;
            var c = a.owner.Pos + Vector2.up * 1.1f;
            for (int i = 0; i < 10; i++)
            {
                float ang = i * 36f * Mathf.Deg2Rad;
                vfx.Spark(c + new Vector2(Mathf.Cos(ang) * 0.5f, Mathf.Sin(ang) * 0.8f), new Vector2(Mathf.Cos(ang) * 0.8f, 0.8f + Mathf.Sin(ang)), i % 2 == 0 ? Color.white : StormRed, 0.055f, 0.5f);
            }
            vfx.Ring(a.owner.Pos + Vector2.up * 0.05f, 0.4f, 1.3f, 0.05f, StormRed, 0.55f, false, 0.3f);
        }
    }

    /// <summary>The lightning wave in flight (a flight cue riding the projectile): a low red-and-white streak along the floor with sparks thrown off behind it.</summary>
    public sealed class ThunderWaveLook : MonoBehaviour
    {
        Transform _proj;
        float _spark;
        Vector2 _last;

        public static void Play(Vector2 pos, in CueArgs a)
        {
            if (a.follow == null || Vfx.I == null) return;
            var go = new GameObject("ThunderWaveLook");
            go.transform.SetParent(a.follow, false);
            go.transform.localPosition = Vector3.zero;
            go.AddComponent<ThunderWaveLook>().Init(a.follow, a.dir.x >= 0f ? 1f : -1f);
        }

        void Init(Transform proj, float dir)
        {
            _proj = proj;
            int o = Vfx.Order + 30;
            var red = KMath.Hex("#FF3B2F");
            MeshKit.MakeRenderer("Flare", transform, MeshKit.Disc(Vector2.zero, 0.3f, red, new Color(red.r, red.g, red.b, 0f), 24, 1.6f, 1.1f), Vfx.I.AddHDR(1.4f), o);
            MeshKit.MakeRenderer("Core", transform, MeshKit.Disc(Vector2.zero, 0.12f, Color.white, red, 20, 2.4f, 1f), Vfx.I.AddHDR(2.2f), o + 1);
            MeshKit.MakeRenderer("Tip", transform, MeshKit.Star(5, 0.34f, 0.07f, Color.white, red), Vfx.I.AddHDR(1.8f), o + 2).transform.localPosition = new Vector3(0.18f * dir, 0.08f, 0f);
        }

        void Update()
        {
            if (_proj == null || Vfx.I == null) return;
            _spark += Time.deltaTime;
            if (_spark < 0.03f) return;
            _spark = 0f;
            Vector2 p = _proj.position;
            Vfx.I.Spark(p + new Vector2(Random.Range(-0.1f, 0.1f), Random.Range(0f, 0.3f)), new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(1f, 2.6f)), Random.value < 0.4f ? Color.white : KMath.Hex("#FF3B2F"), 0.05f, 0.3f);
            if (Random.value < 0.4f) Vfx.I.Puff(p + Vector2.down * 0.2f, new Vector2(0f, 0.3f), new Color(0.2f, 0.05f, 0.05f, 0.5f), 0.16f, 0.4f);
            _last = p;
        }
    }
}
