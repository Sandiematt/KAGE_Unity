using Kage.Rules;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Hisame's abilities (SAN-105 + Tessenjutsu war-fan martial arts &amp; Raikon's training, ADR-0008):
    /// <list type="bullet">
    /// <item><b>Lightning Cut</b> (<c>lightningCut</c>), <b>Kocho</b> (<c>hisameKocho</c>), and <b>Smoke Bomb</b> (<c>smokeBomb</c>): her three
    /// SAN-105 slots, upgraded with craft-layered rain-wind crescents, crimson fan arcs, and multi-layered mist bursts.</item>
    /// <item><b>Kasumi Fan Dance</b> (<c>hisame.kasumiMai</c>): Tessenjutsu closed-fan wrist snap → Kasumi temple fan slash → a stepping
    /// twin-fan cross-sweep, body upright (a light courier echo of Raikon's Tempest Wheel).</item>
    /// <item><b>Shigure Fan Gust</b> (<c>hisame.rainGust</c>): a nimble spacing step that snaps both Mist Fans open and launches a fast
    /// rain-wind crescent across mid-range.</item>
    /// <item><b>Inazuma Mist Step</b> (<c>hisame.inazumaStep</c>, Phase 2): taught by Raikon — an electrified crouch and mist-ghost dash
    /// through Kage that leaves a delayed rain-and-crimson-lightning slash hanging where she passed (a single-dash Blink Flurry).</item>
    /// </list>
    /// </summary>
    public static class HisameAbilities
    {
        public const string LightningCutId = "lightningCut", KochoId = "hisameKocho", SmokeBombId = "smokeBomb";
        public const string KasumiMaiId = "hisame.kasumiMai", RainGustId = "hisame.rainGust", InazumaStepId = "hisame.inazumaStep";
        public const string SmokeProjectileId = "hisame.smokeBomb", RainGustProjectileId = "hisame.rainGust.blade";

        public static readonly string[] ExtraIds = { KasumiMaiId, RainGustId, InazumaStepId };

        // ---- effect cues ----
        public const string KochoGlintCue = "VFX_Kage_HisameGlint";
        public const string CrackleCue = "VFX_Kage_LightningCrackle", CutGlintCue = "VFX_Kage_LightningGlint";
        public const string SmokeBombCue = "VFX_Kage_SmokeBomb";
        public const string SmokeBurstCue = "VFX_Anime_Smoke_Burst";
        public const string KochoSmokeCue = "VFX_Kage_KochoSmoke";
        public const string FanSlashCue = "VFX_Kage_HisameFanSlash";
        public const string StormFanSlashCue = "VFX_Kage_HisameStormFanSlash";
        public const string FanWheelCue = "VFX_Kage_HisameFanWheel";
        public const string RainGhostCue = "VFX_Kage_HisameRainGhost";
        public const string RainGustFlightCue = "VFX_Kage_HisameRainGust";
        public const string RainGustHitCue = "VFX_Kage_HisameRainGustHit";
        public const string InazumaMarkCue = "VFX_Kage_HisameInazumaMark";
        public const string InazumaDetonateCue = "VFX_Kage_HisameInazumaDetonate";
        public const string AtemiCue = "VFX_Kage_HisameAtemi";

        public static readonly string[] CueIds =
        {
            KochoGlintCue, CrackleCue, CutGlintCue, SmokeBombCue, SmokeBurstCue, KochoSmokeCue,
            FanSlashCue, StormFanSlashCue, FanWheelCue, RainGhostCue, RainGustFlightCue, RainGustHitCue,
            InazumaMarkCue, InazumaDetonateCue, AtemiCue,
        };

        // ---- constants of the JS ability code ----
        const double SwingDmg = 15, SwingGb = 70, SwingKbPx = 420, SwingUpPx = 240, SwingStun = 0.5;
        const double SwingRecovery = 0.6;
        const double DashTime = 0.16, CutDelay = 0.06, GlintLead = 0.2;
        const double KochoWindup = 0.14, KochoArc = 0.34;
        const double KochoHeightPx = 150, KochoPastPx = 110, KochoRoomPx = 60, KochoMarginPx = 30;
        const double KochoCutStartup = 0.2;
        const double ChasedPx = 280, ChasedSpeedPx = 120;
        const double SmokeThrowTime = 0.5, SmokeReleaseAt = 0.18;
        const double SmokeVyPx = 520, SmokeVxMinPx = 380, SmokeVxMaxPx = 560;
        const double JsGuardMax = 100;

        static readonly Vector2 SmokeFrom = new(0.3f, 1.3f);

        static readonly HitPoint[] Fans = { HitPoint.Blade, HitPoint.BladeN };
        static readonly HitPoint[] NearFan = { HitPoint.BladeN };
        static readonly HitPoint[] FarFan = { HitPoint.Blade };

        public static MoveDef KasumiMaiMove, RainGustMove, InazumaStepMove;
        public static ProjectileDef RainGustBlade;
        public static HazardDef InazumaDelayedSlash;

        static int Frames(double seconds) => Mathf.RoundToInt((float)(seconds * 60.0));
        static float Metres(double px) => (float)JsUnits.PxToMetres(px);
        static int Dmg(double js) => Mathf.Max(1, Mathf.RoundToInt((float)js * GaranAbilities.UnityPerJsDamage));

        public static float PostureMul(double gb, int unityDmg) =>
            (float)(gb / JsGuardMax * new CharacterDef().postureMax / (unityDmg * Tuning.Get<GuardTuning>().blockPostureFactor));

        static AbilityDef Def(string id, MoveDef move)
        {
            var js = JsTables.BossAbility(id);
            return new AbilityDef
            {
                id = id, move = move, cooldownFrames = Frames(js.Cd),
                range = new Vector2(Metres(js.Range[0]), Metres(js.Range[1])), chance = (float)js.Chance,
            };
        }

        public static bool Chased(Fighter self, Fighter kage)
        {
            if (self == null || kage == null || !kage.Grounded) return false;
            float dx = self.Pos.x - kage.Pos.x;
            if (Mathf.Abs(dx) >= Metres(ChasedPx)) return false;
            float vx = kage.LogicVelocity.x;
            bool toward = Mathf.Sign(vx) == Mathf.Sign(dx);
            if (kage.state == FState.Dash) return toward;
            return (kage.state == FState.Walk || kage.state == FState.Run) && toward && Mathf.Abs(vx) > Metres(ChasedSpeedPx);
        }

        /// <summary>Registers Hisame's abilities in the <see cref="AbilityCatalog"/> for build scale <paramref name="scale"/>. Idempotent.</summary>
        public static void Register(float scale)
        {
            RegisterCues();
            AbilityCatalog.Register(Def(LightningCutId, LightningCut(JsTables.BossAbility(LightningCutId))));
            var kocho = Def(KochoId, Kocho(scale));
            kocho.when = Chased;
            AbilityCatalog.Register(kocho);
            AbilityCatalog.Register(Def(SmokeBombId, SmokeBomb()));

            BuildMartialKit();
            Reg(KasumiMaiId, KasumiMaiMove, 5.2f, 0.4f, 2.0f, 0.45f);
            Reg(RainGustId, RainGustMove, 6.0f, 2.0f, 5.8f, 0.45f);
            Reg(InazumaStepId, InazumaStepMove, 7.0f, 1.1f, 4.2f, 0.45f);
            Patterns();
        }

        /// <summary>
        /// Hisame's strings (sub-boss III, <see cref="SubBossTier.Hisame"/>): hit-and-run. The fan gust is chased by the dash cuts; the Lightning
        /// Cut lands her beside Kage for the fan dance, or vanishes behind a smoke bomb; the fan dance's cross-sweep throws her clear for another gust; a
        /// smoke bomb covers a dash in.
        /// </summary>
        static void Patterns()
        {
            var t = SubBossTier.Hisame;
            t.Chain(RainGustId, InazumaStepId, LightningCutId);
            t.Chain(LightningCutId, KasumiMaiId, SmokeBombId);
            t.Chain(KasumiMaiId, RainGustId, InazumaStepId);
            t.Chain(SmokeBombId, LightningCutId, InazumaStepId);
            t.Chain(InazumaStepId, KasumiMaiId, RainGustId);
            t.Chain(KochoId, KasumiMaiId, LightningCutId);
        }

        static void Reg(string id, MoveDef m, float cd, float near, float far, float chance)
        {
            AbilityCatalog.Register(new AbilityDef { id = id, move = m, cooldownFrames = Frames(cd), range = new Vector2(near, far), chance = chance });
        }

        static Pose T() => new Pose().Dual();

        static Pose Rest() => T().Hip(0f, -0.08f, -4f).Chest(-8f).Head(5f).Feet(0.24f, -0.26f)
            .HandN(0.44f, 1.01f).BladeN(10f).HandF(0.27f, 0.97f).Blade(34f);

        static void BuildMartialKit()
        {
            if (KasumiMaiMove != null) return;

            RainGustBlade = new ProjectileDef
            {
                id = RainGustProjectileId,
                speed = 7.4f,
                radius = 0.24f,
                lifeFrames = 70,
                maxHits = 1,
                hit = new HitWindow { dmg = Dmg(5), level = HitLevel.Mid, kb = new Vector2(1.9f, 0.5f), stun = 18, hitstop = 5, shake = 0.14f },
                flightCue = RainGustFlightCue,
                hitCue = RainGustHitCue,
                endCue = RainGustHitCue,
            };

            InazumaDelayedSlash = new HazardDef
            {
                id = "hisame.inazumaStep.slash",
                telegraphFrames = 22,
                strikeFrames = 4,
                size = new Vector2(1.25f, 1.6f),
                hit = new HitWindow { dmg = Dmg(5), level = HitLevel.Mid, kb = new Vector2(1.4f, 2.6f), stun = 20, hitstop = 6, shake = 0.18f },
                telegraphCue = InazumaMarkCue,
                strikeCue = InazumaDetonateCue,
            };

            // Tessenjutsu Kasumi Fan Dance: closed-fan atemi -> open-fan Kasumi temple slash -> both fans gathered high, then swept
            // forward together through Kage on a step-in (a grounded cross-sweep: no whole-body spin)
            KasumiMaiMove = new MoveDef(KasumiMaiId, "Kasumi Fan Dance", 68)
                .Key(7, T().Hip(0.02f, -0.14f, -4f).Chest(-10f).Head(6f).Feet(0.28f, -0.26f).HandF(0.18f, 1.18f).Blade(90f).HandN(0.28f, 0.98f).BladeN(20f), Ease.OutQuad)
                .Key(15, T().Hip(0.1f, -0.12f, -12f).Chest(-20f).Head(10f).Feet(0.38f, -0.24f).HandF(0.62f, 1.16f).Blade(6f).HandN(0.2f, 1.04f).BladeN(50f), Ease.OutExpo)
                .Key(22, T().Hip(0.06f, -0.14f, -6f).Chest(-10f).Head(6f).Feet(0.34f, -0.26f).HandN(0.12f, 1.38f).BladeN(132f).HandF(0.34f, 0.96f).Blade(-20f), Ease.InOutQuad)
                .Key(28, T().Hip(0.12f, -0.1f, -14f).Chest(-24f).Head(12f).Feet(0.42f, -0.26f).HandN(0.68f, 1.24f).BladeN(-12f).HandF(0.18f, 1.06f).Blade(70f), Ease.OutExpo)
                .Key(36, T().Hip(-0.02f, -0.16f, 4f).Chest(4f).Head(-2f).Feet(0.3f, -0.28f).HandN(0.1f, 1.34f).BladeN(140f).HandF(-0.3f, 1.28f).Blade(160f), Ease.InOutQuad)
                .Key(42, T().Hip(0.1f, -0.14f, -12f).Chest(-22f).Head(10f).Feet(0.4f, -0.26f).HandN(0.66f, 1.16f).BladeN(-6f).HandF(0.6f, 1.04f).Blade(-14f), Ease.OutExpo)
                .Key(46, T().Hip(0.1f, -0.15f, -12f).Chest(-22f).Head(10f).Feet(0.4f, -0.26f).HandN(0.62f, 1.0f).BladeN(-30f).HandF(0.56f, 0.92f).Blade(-36f))
                .Key(50, T().Hip(0.06f, -0.16f, -8f).Chest(-12f).Head(6f).Feet(0.36f, -0.28f).HandN(0.5f, 1.06f).BladeN(-20f).HandF(0.42f, 1.0f).Blade(-24f))
                .Key(68, Rest(), Ease.InOutQuad)
                .Move(11, 16, 2.8f).Move(24, 29, 2.2f).Move(38, 49, 2.4f)
                .Hit(14, 17, FarFan, Dmg(4), HitLevel.Mid, new Vector2(0.9f, 0f), stun: 18, hitstop: 4, shake: 0.1f, radius: 0.09f)
                .Hit(26, 30, NearFan, Dmg(5), HitLevel.Mid, new Vector2(1.2f, 0.3f), stun: 20, hitstop: 5, shake: 0.14f, radius: 0.1f)
                .Hit(40, 48, Fans, Dmg(7), HitLevel.Mid, new Vector2(3.2f, 1.1f), stun: 24, hitstop: 8, shake: 0.24f, heavy: true, radius: 0.24f)
                .Cue(15, AtemiCue).Cue(27, FanSlashCue).Cue(41, FanWheelCue).Cue(46, FanWheelCue)
                .Fx(13, 18, FxKind.TrailBlade).Fx(25, 31, FxKind.TrailBladeN).Fx(39, 49, FxKind.TrailBlade).Fx(39, 49, FxKind.TrailBladeN)
                .Impulse(39, new Vector2(-3.0f, 1.6f)).Sword();

            // Shigure Fan Gust: agile step back while snapping both Mist Fans open to send a rain-wind crescent
            RainGustMove = new MoveDef(RainGustId, "Shigure Fan Gust", 42)
                .Key(8, T().Hip(-0.08f, -0.18f, 6f).Chest(10f).Head(-4f).Feet(0.26f, -0.32f).HandN(-0.14f, 1.32f).BladeN(142f).HandF(-0.18f, 1.22f).Blade(156f), Ease.OutQuad)
                .Key(16, T().Hip(0.08f, -0.14f, -12f).Chest(-20f).Head(10f).Feet(0.36f, -0.28f).HandN(0.64f, 1.14f).BladeN(-4f).HandF(0.58f, 1.06f).Blade(8f), Ease.OutExpo)
                .Key(23, T().Hip(0.06f, -0.14f, -10f).Chest(-18f).Head(8f).Feet(0.34f, -0.28f).HandN(0.6f, 1.12f).BladeN(-8f).HandF(0.54f, 1.04f).Blade(4f))
                .Key(42, Rest(), Ease.InOutQuad)
                .Move(2, 10, -2.4f)
                .Throw(15, RainGustBlade, new Vector2(0.55f, 1.05f))
                .Cue(6, KochoGlintCue).Cue(15, FanSlashCue)
                .Fx(14, 19, FxKind.TrailBlade).Fx(14, 19, FxKind.TrailBladeN)
                .Impulse(15, new Vector2(2.4f, 0.8f)).Sword();

            // Inazuma Mist Step (Phase 2): Raikon-trained blink dash leaving a delayed electrified rain-slash
            InazumaStepMove = new MoveDef(InazumaStepId, "Inazuma Mist Step", 58)
                .Key(9, T().Hip(-0.06f, -0.4f, 8f).Chest(-26f).Head(18f).Feet(0.48f, -0.34f).HandN(-0.18f, 0.64f).BladeN(-158f).HandF(-0.24f, 0.7f).Blade(-170f), Ease.OutQuad)
                .Key(23, T().Hip(-0.08f, -0.44f, 10f).Chest(-30f).Head(22f).Feet(0.5f, -0.34f).HandN(-0.22f, 0.6f).BladeN(-164f).HandF(-0.28f, 0.66f).Blade(-174f))
                .Key(28, T().Hip(0.16f, -0.28f, -18f).Chest(-36f).Head(22f).Feet(0.52f, -0.3f).HandN(0.66f, 0.9f).BladeN(-4f).HandF(0.6f, 0.98f).Blade(8f), Ease.OutExpo)
                .Key(36, T().Hip(0.08f, -0.3f, -10f).Chest(-24f).Head(14f).Feet(0.54f, -0.3f).HandN(0.54f, 0.86f).BladeN(-24f).HandF(0.24f, 1.02f).Blade(36f), Ease.OutQuad)
                .Key(58, Rest(), Ease.InOutQuad)
                .Move(24, 33, 8.2f)
                .NoPush(23, 35)
                .Hit(25, 32, Fans, Dmg(6), HitLevel.Mid, new Vector2(2.2f, 0.8f), stun: 22, hitstop: 7, shake: 0.2f, radius: 0.22f)
                .HazardOnTarget(25, InazumaDelayedSlash)
                .Cue(6, CrackleCue).Cue(12, CrackleCue).Cue(16, CutGlintCue)
                .Cue(24, RainGhostCue).Cue(27, RainGhostCue).Cue(30, RainGhostCue).Cue(28, StormFanSlashCue)
                .Fx(24, 34, FxKind.TrailBlade).Fx(24, 34, FxKind.TrailBladeN).Fx(24, 24, FxKind.Dust)
                .Impulse(24, new Vector2(-5.5f, 1.4f)).Sword();
        }

        // ---------------------------------------------------------------------------------------------- Lightning Cut
        static MoveDef LightningCut(JsBossAbility js)
        {
            int tell = Frames(js.Windup * GaranAbilities.Windup);
            int dash = Frames(DashTime);
            int cut = tell + Frames(CutDelay);
            int total = tell + Frames(SwingRecovery);
            double jsDmg = SwingDmg * js.Dmg;
            var low = T().Hip(-0.06f, -0.44f, 10f).Chest(-30f).Head(22f).Feet(0.52f, -0.34f)
                .HandN(-0.18f, 0.6f).BladeN(-160f).HandF(-0.26f, 0.66f).Blade(-172f);
            var lower = T().Hip(-0.08f, -0.48f, 12f).Chest(-34f).Head(24f).Feet(0.54f, -0.34f)
                .HandN(-0.24f, 0.56f).BladeN(-165f).HandF(-0.32f, 0.62f).Blade(-176f);
            var through = T().Hip(0.18f, -0.32f, -20f).Chest(-42f).Head(26f).Feet(0.56f, -0.3f)
                .HandN(0.68f, 0.62f).BladeN(-9f).HandF(0.62f, 0.69f).Blade(1f);
            var skid = T().Hip(0.08f, -0.36f, -10f).Chest(-30f).Head(18f).Feet(0.6f, -0.32f)
                .HandN(0.56f, 0.8f).BladeN(-30f).HandF(0.2f, 1.0f).Blade(40f);
            var m = new MoveDef("hisame.lightningCut", "Lightning Cut", total)
                .Key(10, low, Ease.OutQuad)
                .Key(tell - 1, lower)
                .Key(tell + 3, through, Ease.OutExpo)
                .Key(tell + 14, skid, Ease.OutQuad)
                .Key(total, Rest(), Ease.InOutQuad)
                .Move(tell, tell + dash - 1, Metres(js.Dash))
                .Hit(cut - 1, cut + 3, Fans, Dmg(jsDmg), HitLevel.Mid, new Vector2(Metres(SwingKbPx), Metres(SwingUpPx)), stun: Frames(SwingStun),
                    hitstop: 10, shake: 0.25f, heavy: true, radius: 0.10f, postureMul: PostureMul(SwingGb, Dmg(jsDmg)))
                .Armour(0, tell - 1)
                .NoPush(tell, tell + dash + 2)
                .Fx(tell, tell + dash, FxKind.AfterImage).Fx(tell, tell, FxKind.Dust).Fx(cut - 1, cut + 4, FxKind.TrailBlade)
                .Fx(cut - 1, cut + 4, FxKind.TrailBladeN).Fx(cut, cut, FxKind.SlashLine).Fx(tell + dash, tell + dash, FxKind.Dust)
                .Cue(tell - Frames(GlintLead), CutGlintCue).Cue(cut, StormFanSlashCue)
                .Impulse(tell, new Vector2(-4.8f, 1.2f)).Sword();
            for (int f = 3; f < tell; f += 3) m.Cue(f, CrackleCue);
            return m;
        }

        // ---------------------------------------------------------------------------------------------- Kocho
        static MoveDef Kocho(float scale)
        {
            var tw1 = JsTables.Move("tw1");
            int wind = Frames(KochoWindup * GaranAbilities.Windup);
            int land = wind + Frames(KochoArc);
            int cut = land + Frames(System.Math.Max(tw1.T[0], KochoCutStartup));
            int active = Frames(tw1.T[1]);
            int total = cut + active + Frames(tw1.T[2]);
            var spread = T().Hip(0f, -0.3f, 4f).Chest(-8f).Head(6f).Feet(0.36f, -0.3f)
                .HandN(0.3f, 1.36f).BladeN(120f).HandF(-0.34f, 1.24f).Blade(160f);
            var air = T().Hip(0f, 0f, 0f).Chest(8f).Head(-4f).LegN(100f, -130f).LegF(84f, -120f)
                .HandN(0.4f, 1.56f).BladeN(70f).HandF(-0.4f, 1.5f).Blade(150f);
            var landing = T().Hip(0f, -0.34f, -4f).Chest(-16f).Head(10f).Feet(0.36f, -0.3f)
                .HandN(0.14f, 1.12f).BladeN(100f).HandF(0.12f, 0.96f).Blade(50f);
            var raised = T().Hip(0f, -0.14f, -2f).Chest(4f).Head(2f).Feet(0.28f, -0.26f)
                .HandN(0.16f, 1.32f).BladeN(118f).HandF(0.2f, 1.0f).Blade(40f);
            var strike = T().Hip(0.1f, -0.1f, -10f).Chest(-22f).Head(10f).Feet(0.36f, -0.24f)
                .HandN(0.64f, 1.16f).BladeN(-6f).HandF(0.24f, 1.08f).Blade(70f);
            return new MoveDef("hisame.kocho", "Kocho", total)
                .Key(wind, spread, Ease.OutQuad)
                .Key(wind + 6, air, Ease.OutQuad)
                .Key(land - 3, air)
                .Key(land, landing, Ease.InQuad)
                .Key(cut - 3, raised, Ease.InOutQuad)
                .Key(cut + 1, strike, Ease.OutExpo)
                .Key(cut + active + 1, strike)
                .Key(total, Rest(), Ease.InOutQuad)
                .Vault(wind, land, Metres(KochoPastPx * scale), Metres(KochoHeightPx * scale), Metres(KochoRoomPx * scale), Metres(KochoMarginPx))
                .Invuln(0, land)
                .NoPush(wind, land + 2)
                .Hit(cut, cut + active - 1, NearFan, Dmg(tw1.Dmg), HitLevel.Mid, new Vector2(0.78f, 0f), stun: 16, hitstop: 4, shake: 0.06f, radius: 0.08f)
                .Fx(wind, land, FxKind.AfterImage).Fx(wind, wind, FxKind.Dust).Fx(cut - 1, cut + active + 1, FxKind.TrailBladeN)
                .Cue(land, KochoSmokeCue).Cue(land, KochoGlintCue).Cue(cut, FanSlashCue)
                .Impulse(wind, new Vector2(-2.2f, 2.5f)).Sword();
        }

        // ---------------------------------------------------------------------------------------------- Smoke Bomb
        public static ProjectileDef SmokeBombProjectile()
        {
            var p = JsTables.Projectile("smoke");
            return new ProjectileDef
            {
                id = SmokeProjectileId,
                hit = new HitWindow { dmg = Dmg(p.Dmg), level = HitLevel.Mid, kb = new Vector2(Metres(p.Kb), 0f), stun = Frames(p.Hs), hitstop = 4, shake = 0.08f },
                speed = Metres(SmokeVxMinPx), speedMax = Metres(SmokeVxMaxPx), launchVy = Metres(SmokeVyPx),
                gravityScale = Metres(p.Grav) / -Physics2D.gravity.y,
                radius = Metres(p.R), lifeFrames = Frames(p.Life), maxHits = 1,
                flightCue = SmokeBombCue, hitCue = SmokeBurstCue, endCue = SmokeBurstCue,
            };
        }

        static MoveDef SmokeBomb()
        {
            int total = Frames(SmokeThrowTime), release = Frames(SmokeReleaseAt);
            var back = T().Hip(-0.04f, -0.12f, 6f).Chest(8f).Head(-2f).Feet(0.3f, -0.28f)
                .HandN(-0.12f, 1.42f).BladeN(150f).HandF(0.3f, 0.92f).Blade(20f);
            var fling = T().Hip(0.08f, -0.12f, -10f).Chest(-18f).Head(8f).Feet(0.36f, -0.26f)
                .HandN(0.62f, 1.3f).BladeN(30f).HandF(0.22f, 0.96f).Blade(30f);
            return new MoveDef("hisame.smokeBomb", "Smoke Bomb", total)
                .Key(release - 3, back, Ease.OutQuad)
                .Key(release + 2, fling, Ease.OutExpo)
                .Key(total, Rest(), Ease.InOutQuad)
                .Throw(release, SmokeBombProjectile(), SmokeFrom);
        }

        // ---------------------------------------------------------------------------------------------- cues
        // The look itself is in Vfx.Hisame.cs (docs/act1-subbosses-vfx.md); her fan winds and rain cuts come from HisameLook on every move.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RegisterCues()
        {
            EffectCues.Register(KochoGlintCue, (Vector2 p, in CueArgs a) => Vfx.I?.HisameGlint(p + Vector2.up * 0.12f, HisamePalette.Accent, 0.85f));
            EffectCues.Register(CrackleCue, (Vector2 p, in CueArgs a) => Vfx.I?.HisameCrackle(p));
            EffectCues.Register(CutGlintCue, (Vector2 p, in CueArgs a) => Vfx.I?.HisameGlint(p, GlintRed, 1.1f));
            EffectCues.Register(SmokeBombCue, SmokeBombLook.Play);
            EffectCues.Register(SmokeBurstCue, (Vector2 p, in CueArgs a) => Vfx.I?.HisameMistScreen(p, 1.05f, Downpour(a)));
            EffectCues.Register(KochoSmokeCue, (Vector2 p, in CueArgs a) => { if (a.owner != null) Vfx.I?.HisameLanding(a.owner.Pos, a.owner.facing); });
            EffectCues.Register(FanSlashCue, (Vector2 p, in CueArgs a) => PlayGust(p, a, false));
            EffectCues.Register(StormFanSlashCue, (Vector2 p, in CueArgs a) => PlayGust(p, a, true));
            EffectCues.Register(FanWheelCue, PlayFanWheel);
            EffectCues.Register(RainGhostCue, PlayRainGhost);
            EffectCues.Register(RainGustFlightCue, RainGustLook.Play);
            EffectCues.Register(RainGustHitCue, (Vector2 p, in CueArgs a) => Vfx.I?.HisameRainCut(p, a.dir.sqrMagnitude > 0f ? a.dir : Vector2.right, 1.1f, false));
            EffectCues.Register(InazumaMarkCue, (Vector2 p, in CueArgs a) => Vfx.I?.HisameSuspendedRain(p + new Vector2(-0.55f, 0.35f), p + new Vector2(0.55f, 1.4f), a.seconds));
            EffectCues.Register(InazumaDetonateCue, (Vector2 p, in CueArgs a) => Vfx.I?.HisameRainCleave(p + new Vector2(-0.55f, 0.35f), p + new Vector2(0.55f, 1.4f)));
            EffectCues.Register(AtemiCue, PlayAtemi);
        }

        static readonly Color GlintRed = KMath.Hex("#FF4A3C");

        static float Facing(in CueArgs a) => a.owner != null ? a.owner.facing : (a.dir.x >= 0f ? 1f : -1f);
        static bool Downpour(in CueArgs a) => a.owner?.Phases != null && a.owner.Phases.Index >= 1;

        /// <summary>A fan's gust leaves her at chest height and travels: level for the storm cuts, tilted down for the plain fan slash.</summary>
        static void PlayGust(Vector2 pos, in CueArgs a, bool storm)
        {
            var v = Vfx.I;
            if (v == null) return;
            float face = Facing(a);
            Vector2 c = a.owner != null ? a.owner.Pos + new Vector2(face * 0.35f, 1.0f) : pos;
            v.HisameGust(c, face, storm ? -4f : -16f, storm || Downpour(a));
        }

        static void PlayFanWheel(Vector2 pos, in CueArgs a)
        {
            var v = Vfx.I;
            if (v == null) return;
            v.HisameVortex(a.owner != null ? a.owner.Pos + Vector2.up * 1.0f : pos, Facing(a), Downpour(a));
        }

        static void PlayRainGhost(Vector2 pos, in CueArgs a)
        {
            if (a.owner?.rig == null) return;
            Vfx.I?.HisameWaterGhost(a.owner);
        }

        static void PlayAtemi(Vector2 pos, in CueArgs a)
        {
            var v = Vfx.I;
            if (v == null) return;
            Vector2 tip = a.owner != null && a.owner.rig != null ? a.owner.rig.World(B.Sword, a.owner.BladeTipLocal * 0.95f) : pos;
            v.HisameAtemi(tip, Facing(a));
        }
    }
}
