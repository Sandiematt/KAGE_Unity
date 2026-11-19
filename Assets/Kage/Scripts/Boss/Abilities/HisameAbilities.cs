using Kage.Rules;
using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Hisame's abilities (SAN-105, ADR-0008): the three JS ones, each an ordinary <see cref="MoveDef"/> on the twin blades set (her Mist Fans).
    /// <list type="bullet">
    /// <item><b>Lightning Cut</b> (<c>lightningCut</c>, JS "Thunder Swing cut down to its first stroke, a short dashing lightning cut with no
    /// ground wave"): she drops low with both fans drawn back, red sparks crackling on them and a red glint before the release (armoured,
    /// the JS super armour), then dashes straight through Kage with one cut. No wave. Then she skids to a stop, open: the JS tip is "block
    /// or jump it, then strike as she skids to a stop".</item>
    /// <item><b>Kocho</b> (<c>hisameKocho</c>, the butterfly step, the ticket's "mist step"): her answer to being chased. The fans spread,
    /// she springs in an arc over Kage's head and lands at his back (beside him on her own side when a wall leaves no room there), facing
    /// him; invulnerable from the start to the landing. A glint on the fan as she lands, and the cut a beat later (0.2 s: time to turn and
    /// guard). Her AI uses it only on a Kage walking, running or dashing at her (<see cref="AbilityDef.when"/>), as the JS does.</item>
    /// <item><b>Smoke Bomb</b> (<c>smokeBomb</c>, phase two): she lobs a smoke bomb (the JS thrown weapon <c>smoke</c>, a projectile,
    /// ADR-0007); it bursts into smoke where it lands, or on Kage.</item>
    /// </list>
    /// <para>
    /// <b>Numbers.</b> As Garan's (<see cref="GaranAbilities"/>, which this file shares its two conversions with): cooldown, range and chance
    /// from <see cref="JsTables.BossAbility"/>; Lightning Cut's variant (wind-up, damage, reach, dash); the twin blades' first cut that ends
    /// Kocho (<see cref="JsTables.Move"/> <c>tw1</c>); the smoke projectile (<see cref="JsTables.Projectile"/>); the tells scaled by the JS
    /// <c>ai.p.windup</c> for a sub-boss on Normal (<see cref="GaranAbilities.Windup"/>). What the JS writes only in its ability code is a
    /// named constant below with its file. Damage is the JS damage × <see cref="GaranAbilities.UnityPerJsDamage"/>; posture is
    /// <c>postureMul = gb / (JS damage × 1.3)</c>.
    /// </para>
    /// </summary>
    public static class HisameAbilities
    {
        public const string LightningCutId = "lightningCut", KochoId = "hisameKocho", SmokeBombId = "smokeBomb";
        public const string SmokeProjectileId = "hisame.smokeBomb";
        /// <summary>Her glint on the fan before Kocho's cut (the JS <c>w.fx.glint(..., '#9fd4ff')</c>; not in the JS VFX manifest).</summary>
        public const string KochoGlintCue = "VFX_Kage_HisameGlint";
        /// <summary>Red sparks on the fans through Lightning Cut's tell (the JS <c>raikonCrackle</c>), and the red glint before the release.</summary>
        public const string CrackleCue = "VFX_Kage_LightningCrackle", CutGlintCue = "VFX_Kage_LightningGlint";
        /// <summary>The smoke bomb in flight.</summary>
        public const string SmokeBombCue = "VFX_Kage_SmokeBomb";
        /// <summary>The JS manifest's toon smoke burst: where the bomb bursts, and where Kocho lands.</summary>
        public const string SmokeBurstCue = "VFX_Anime_Smoke_Burst";

        // ---- constants of the JS ability code (not data, so not in the generated tables) ----
        const double SwingDmg = 15, SwingGb = 70, SwingKbPx = 420, SwingUpPx = 240, SwingStun = 0.5;   // 40-ai.js raikonRelease: 15 x the variant's dmg, gb 70
        const double SwingRecovery = 0.6;           // 40-ai.js thunderSwing: the ability lasts the wind-up + 0.6 s
        const double DashTime = 0.16, CutDelay = 0.06, GlintLead = 0.2;   //   ...the dash runs 0.16 s, the cut lands 0.06 s into it, the glint 0.2 s before
        const double KochoWindup = 0.14, KochoArc = 0.34;                 // 41-styles/hisame.js hisameKocho: W = 0.14 x windup, E = 0.34
        const double KochoHeightPx = 150, KochoPastPx = 110, KochoRoomPx = 60, KochoMarginPx = 30;   //   ...arc 150 px x scale high, 110 px x scale past him
        const double KochoCutStartup = 0.2;         //   ...tw1 with its wind-up held to at least 0.2 s after the glint
        const double ChasedPx = 280, ChasedSpeedPx = 120;                 // 41-styles/hisame.js hisameChased: within 280 px, coming faster than 120 px/s
        const double SmokeThrowTime = 0.5, SmokeReleaseAt = 0.18;         // 40-ai.js smokeBomb: a 0.5 s throw, the bomb leaves at 0.18 s
        const double SmokeVyPx = 520, SmokeVxMinPx = 380, SmokeVxMaxPx = 560;   // 30-combat.js WeaponSystem.spawn('smoke'): vy -520, vx rand(380, 560)
        const double GuardLeanPerDmg = 1.3;         // 30-combat.js: a blow without gb leans dmg x 1.3 on a guard

        /// <summary>
        /// How far ahead of her the fans' tips are as they cut through (the far hand 0.62 m ahead in the pose, plus a fan): the cut's hit
        /// radius is what is left of the JS cut's reach (160 px x scale) past them.
        /// </summary>
        const float ThroughReach = 0.62f + TwinMoves.BladeLength;

        /// <summary>Where the bomb leaves her hand (metres ahead, up): Unity tuning, the throwing hand at the release.</summary>
        static readonly Vector2 SmokeFrom = new(0.3f, 1.3f);

        static readonly HitPoint[] Fans = { HitPoint.Blade, HitPoint.BladeN };
        static readonly HitPoint[] NearFan = { HitPoint.BladeN };

        static int Frames(double seconds) => Mathf.RoundToInt((float)(seconds * 60.0));
        static float Metres(double px) => (float)JsUnits.PxToMetres(px);
        static int Dmg(double js) => Mathf.Max(1, Mathf.RoundToInt((float)js * GaranAbilities.UnityPerJsDamage));
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

        /// <summary>
        /// The JS <c>hisameChased</c>: Kage is coming after her, on the ground within 280 px, walking or running at her faster than
        /// 120 px/s, or dashing in. Kocho waits for it.
        /// </summary>
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

        /// <summary>Registers the three abilities in the <see cref="AbilityCatalog"/> for a Hisame of build scale <paramref name="scale"/>. Idempotent.</summary>
        public static void Register(float scale)
        {
            AbilityCatalog.Register(Def(LightningCutId, LightningCut(JsTables.BossAbility(LightningCutId), scale)));
            var kocho = Def(KochoId, Kocho(scale));
            kocho.when = Chased;
            AbilityCatalog.Register(kocho);
            AbilityCatalog.Register(Def(SmokeBombId, SmokeBomb()));
        }

        // ---------------------------------------------------------------------------------------------- poses
        static Pose T() => new Pose().Dual();

        /// <summary>The twin blades' ready stance (TwinMoves): far fan up, near fan low and forward.</summary>
        static Pose Rest() => T().Hip(0f, -0.08f, -4f).Chest(-8f).Head(5f).Feet(0.24f, -0.26f)
            .HandN(0.44f, 1.01f).BladeN(10f).HandF(0.27f, 0.97f).Blade(34f);

        // ---------------------------------------------------------------------------------------------- Lightning Cut
        static MoveDef LightningCut(JsBossAbility js, float scale)
        {
            int tell = Frames(js.Windup * GaranAbilities.Windup);     // 33: 0.56 x Normal 1 x the sub role's 0.98
            int dash = Frames(DashTime);                               // 10
            int cut = tell + Frames(CutDelay);                         // 37: the cut lands mid-dash
            int total = tell + Frames(SwingRecovery);                  // 69
            double jsDmg = SwingDmg * js.Dmg;                          // 9
            var low = T().Hip(-0.06f, -0.44f, 10f).Chest(-30f).Head(22f).Feet(0.52f, -0.34f)
                .HandN(-0.18f, 0.6f).BladeN(-160f).HandF(-0.26f, 0.66f).Blade(-172f);
            var lower = T().Hip(-0.08f, -0.48f, 12f).Chest(-34f).Head(24f).Feet(0.54f, -0.34f)
                .HandN(-0.24f, 0.56f).BladeN(-165f).HandF(-0.32f, 0.62f).Blade(-176f);
            var through = T().Hip(0.18f, -0.32f, -20f).Chest(-42f).Head(26f).Feet(0.56f, -0.3f)
                .HandN(0.68f, 0.84f).BladeN(-6f).HandF(0.62f, 0.92f).Blade(6f);
            var skid = T().Hip(0.08f, -0.36f, -10f).Chest(-30f).Head(18f).Feet(0.6f, -0.32f)
                .HandN(0.56f, 0.8f).BladeN(-30f).HandF(0.2f, 1.0f).Blade(40f);
            var m = new MoveDef("hisame.lightningCut", "Lightning Cut", total)
                .Key(10, low, Ease.OutQuad)
                .Key(tell - 1, lower)
                .Key(tell + 3, through, Ease.OutExpo)
                .Key(tell + 14, skid, Ease.OutQuad)
                .Key(total, Rest(), Ease.InOutQuad)
                .Move(tell, tell + dash - 1, Metres(js.Dash))          // 900 px/s for 0.16 s: straight through him
                .Hit(cut - 1, cut + 3, Fans, Dmg(jsDmg), HitLevel.Mid, new Vector2(Metres(SwingKbPx), Metres(SwingUpPx)), stun: Frames(SwingStun),
                    hitstop: 10, shake: 0.25f, heavy: true, radius: Mathf.Max(0.08f, Metres(js.Reach * scale) - ThroughReach), postureMul: PostureMul(SwingGb, jsDmg))
                .Armour(0, tell - 1)                                   // the JS super armour, off at the release
                .NoPush(tell, tell + dash + 2)
                .Fx(tell, tell + dash, FxKind.AfterImage).Fx(tell, tell, FxKind.Dust).Fx(cut - 1, cut + 4, FxKind.TrailBlade)
                .Fx(cut - 1, cut + 4, FxKind.TrailBladeN).Fx(cut, cut, FxKind.SlashLine).Fx(tell + dash, tell + dash, FxKind.Dust)
                .Cue(tell - Frames(GlintLead), CutGlintCue).Sword();
            for (int f = 3; f < tell; f += 3) m.Cue(f, CrackleCue);
            return m;
        }

        // ---------------------------------------------------------------------------------------------- Kocho
        static MoveDef Kocho(float scale)
        {
            var tw1 = JsTables.Move("tw1");
            int wind = Frames(KochoWindup * GaranAbilities.Windup);    // 8: the fans spread
            int land = wind + Frames(KochoArc);                        // 28: down at his back
            int cut = land + Frames(System.Math.Max(tw1.T[0], KochoCutStartup));   // 40: the glint on landing, the cut 0.2 s later
            int active = Frames(tw1.T[1]);                             // 3
            int total = cut + active + Frames(tw1.T[2]);               // 51
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
                .Invuln(0, land)                                       // the JS invuln = W + E: the whole step, not the cut
                .NoPush(wind, land + 2)
                .Hit(cut, cut + active - 1, NearFan, Dmg(tw1.Dmg), HitLevel.Mid, new Vector2(0.78f, 0f), stun: 16, hitstop: 4, shake: 0.06f, radius: 0.08f)
                .Fx(wind, land, FxKind.AfterImage).Fx(wind, wind, FxKind.Dust).Fx(cut - 1, cut + active + 1, FxKind.TrailBladeN)
                .Cue(wind, SmokeBurstCue).Cue(land, SmokeBurstCue).Cue(land, KochoGlintCue).Sword();
        }

        // ---------------------------------------------------------------------------------------------- Smoke Bomb
        /// <summary>The JS <c>smoke</c> thrown weapon as a projectile (ADR-0007): a lob that falls under the JS gravity and bursts where it lands.</summary>
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
            int total = Frames(SmokeThrowTime), release = Frames(SmokeReleaseAt);      // 30, 11
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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterCues()
        {
            EffectCues.Register(KochoGlintCue, PlayGlint);
            EffectCues.Register(CrackleCue, PlayCrackle);
            EffectCues.Register(CutGlintCue, PlayCutGlint);
            EffectCues.Register(SmokeBombCue, SmokeBombLook.Play);
            EffectCues.Register(SmokeBurstCue, PlaySmokeBurst);
        }

        static readonly Color StormRed = KMath.Hex("#FF6A52"), GlintRed = KMath.Hex("#FF4A3C");

        /// <summary>A pale blue glint and a small star on the fan: the cut is coming.</summary>
        static void PlayGlint(Vector2 pos, in CueArgs a)
        {
            var vfx = Vfx.I;
            if (vfx == null) return;
            vfx.Glint(pos + Vector2.up * 0.12f, HisamePalette.Pale);
            vfx.Star(pos + Vector2.up * 0.12f, 0.42f, Color.white, HisamePalette.Accent);
        }

        /// <summary>Red sparks on the fans (the JS crackle: Raikon's lightning in her fans).</summary>
        static void PlayCrackle(Vector2 pos, in CueArgs a)
        {
            var vfx = Vfx.I;
            if (vfx == null) return;
            for (int i = 0; i < 3; i++)
                vfx.Spark(pos + new Vector2(Random.Range(-0.18f, 0.18f), Random.Range(-0.1f, 0.2f)), new Vector2(Random.Range(-1f, 1f), Random.Range(0.4f, 1.6f)),
                    i == 0 ? Color.white : StormRed, 0.045f, 0.22f);
        }

        static void PlayCutGlint(Vector2 pos, in CueArgs a)
        {
            var vfx = Vfx.I;
            if (vfx == null) return;
            vfx.Glint(pos, GlintRed);
            vfx.Star(pos, 0.5f, Color.white, GlintRed);
        }

        /// <summary>Placeholder toon smoke: grey-blue puffs blooming and rising.</summary>
        static void PlaySmokeBurst(Vector2 pos, in CueArgs a)
        {
            var vfx = Vfx.I;
            if (vfx == null) return;
            var ground = new Vector2(pos.x, Mathf.Max(0.05f, pos.y));
            for (int i = 0; i < 9; i++)
            {
                float k = (i - 4) / 4f;
                vfx.Puff(ground + new Vector2(k * 0.35f, 0.1f + Mathf.Abs(k) * 0.05f), new Vector2(k * 0.9f, 0.5f + (1f - Mathf.Abs(k)) * 0.6f),
                    i % 2 == 0 ? HisamePalette.Smoke : HisamePalette.SmokeDark, 0.34f + (1f - Mathf.Abs(k)) * 0.12f, 0.75f);
            }
            vfx.Dust(ground, 0.8f, 0f);
        }
    }

    /// <summary>The smoke bomb in flight (a flight cue riding the projectile): a black ball with a pale rim, a burning fuse and a thin smoke trail.</summary>
    public sealed class SmokeBombLook : MonoBehaviour
    {
        Transform _proj;
        float _trail;

        public static void Play(Vector2 pos, in CueArgs a)
        {
            if (a.follow == null || Vfx.I == null) return;
            var go = new GameObject("SmokeBombLook");
            go.transform.SetParent(a.follow, false);
            go.transform.localPosition = Vector3.zero;
            go.AddComponent<SmokeBombLook>().Init(a.follow);
        }

        void Init(Transform proj)
        {
            _proj = proj;
            int o = Vfx.Order + 30;
            MeshKit.MakeRenderer("Rim", transform, MeshKit.Disc(Vector2.zero, 0.105f, HisamePalette.Pale, HisamePalette.Pale), MeshKit.Alpha, o);
            MeshKit.MakeRenderer("Ball", transform, MeshKit.Disc(Vector2.zero, 0.09f, HisamePalette.RainLit, HisamePalette.RainDeep), MeshKit.Alpha, o + 1);
            MeshKit.MakeRenderer("Fuse", transform, MeshKit.Star(6, 0.07f, 0.025f, Color.white, KMath.Hex("#FFB25A")), Vfx.I.AddHDR(2.4f), o + 2)
                .transform.localPosition = new Vector3(0.05f, 0.11f, 0f);
        }

        void Update()
        {
            if (_proj == null || Vfx.I == null) return;
            _trail += Time.deltaTime;
            if (_trail < 0.035f) return;
            _trail = 0f;
            Vfx.I.Puff((Vector2)_proj.position, new Vector2(0f, 0.3f), HisamePalette.Smoke, 0.1f, 0.4f);
        }
    }
}
