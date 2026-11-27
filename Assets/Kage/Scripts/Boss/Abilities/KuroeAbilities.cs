using System.Collections.Generic;
using Kage.Rules;
using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// Kuroe's abilities (SAN-103 + Bōjutsu calligraphy ink &amp; Raikon's training, ADR-0008):
    /// <list type="bullet">
    /// <item><b>Censure</b> (<c>censure</c>, slot 1): the JS counter stance ("a brief, weaker Redline Counter whose punish can be blocked"),
    /// now sealed with a sumi-gold glint and answered by an Ichimonji calligraphy ink slash.</item>
    /// <item><b>Ichimonji · Nidan</b> (<c>kuroe.sumiNidan</c>): Bōjutsu <i>kesa-uchi</i> downward brush cut into a <i>gyaku-kesa</i> rising
    /// ink slash. In Phase 2 ("Redacted"), the second cut paints a delayed ink-and-crimson-lightning floor line (Raikon's Kesa-Giri lesson).</item>
    /// <item><b>Sumi Barai</b> (<c>kuroe.sumiBarai</c>): Bōjutsu low ankle sweep that stains the floor with black ink and chains on contact into
    /// a <i>Shōmen-uchi</i> overhead brush smash (echoing Raikon's Low Sweep → Rising Cut).</item>
    /// <item><b>Bokuseki Wave</b> (<c>kuroe.bokuseki</c>): a horizontal Bleach/Court-Squad calligraphy cut that launches a flying crescent of
    /// void-black sumi ink edged in censor gold.</item>
    /// <item><b>Thunder Redaction</b> (<c>kuroe.thunderRedaction</c>, Phase 2): taught by Raikon — Kuroe lifts the ink brush skyward to paint a
    /// tracking Censor Seal under Kage that locks and erupts with a sub-boss crimson-ink lightning bolt.</item>
    /// </list>
    /// </summary>
    public static class KuroeAbilities
    {
        public const string CensureId = "censure";
        public const string SumiNidanId = "kuroe.sumiNidan";
        public const string SumiBaraiId = "kuroe.sumiBarai";
        public const string BokusekiWaveId = "kuroe.bokuseki";
        public const string ThunderRedactionId = "kuroe.thunderRedaction";
        public const string InkWaveProjectileId = "kuroe.inkWave";

        public static readonly string[] ExtraIds = { SumiNidanId, SumiBaraiId, BokusekiWaveId, ThunderRedactionId };

        // ---- effect cues (ADR-0007) ----
        public const string SealGlintCue = "VFX_Kage_KuroeSealGlint";
        public const string InkArcDownCue = "VFX_Kage_KuroeInkArcDown";
        public const string InkArcUpCue = "VFX_Kage_KuroeInkArcUp";
        public const string InkPunishCue = "VFX_Kage_KuroeInkPunish";
        public const string InkSweepCue = "VFX_Kage_KuroeInkSweep";
        public const string InkSlamCue = "VFX_Kage_KuroeInkSlam";
        public const string InkSoakCue = "VFX_Kage_KuroeInkSoak";
        public const string InkWaveFlightCue = "VFX_Kage_KuroeInkWave";
        public const string InkWaveHitCue = "VFX_Kage_KuroeInkBurst";
        public const string InkLineMarkCue = "VFX_Kage_KuroeInkLineMark";
        public const string InkLineStrikeCue = "VFX_Kage_KuroeInkLineStrike";
        public const string RedactionMarkCue = "VFX_Kage_KuroeRedactionMark";
        public const string RedactionBoltCue = "VFX_Kage_KuroeRedactionBolt";

        public static readonly string[] CueIds =
        {
            SealGlintCue, InkArcDownCue, InkArcUpCue, InkPunishCue, InkSweepCue, InkSlamCue, InkSoakCue,
            InkWaveFlightCue, InkWaveHitCue, InkLineMarkCue, InkLineStrikeCue, RedactionMarkCue, RedactionBoltCue,
        };

        const int CounterAnswerDamage = 12;
        const int RaiseFrames = 4;
        const int DropFrames = 14;

        public static MoveDef SumiNidanMove, SumiNidanStormMove, SumiBaraiMove, ShomenDropMove, BokusekiMove, ThunderRedactionMove;
        public static HazardDef InkSparkLine, RedactionSeal;
        public static ProjectileDef InkWave;

        static int Frames(double seconds) => Mathf.RoundToInt((float)(seconds * 60.0));
        static int Dmg(double js) => Mathf.Max(1, Mathf.RoundToInt((float)js * GaranAbilities.UnityPerJsDamage));
        static int PhaseOf(Fighter f) => f?.Phases != null ? f.Phases.Index : 0;

        /// <summary>Registers Kuroe's abilities in the <see cref="AbilityCatalog"/>. Idempotent.</summary>
        public static void Register()
        {
            RegisterCues();
            BuildMoves();

            var js = JsTables.BossAbility(CensureId);
            AbilityCatalog.Register(new AbilityDef
            {
                id = CensureId,
                move = Censure(js),
                cooldownFrames = Frames(js.Cd),
                range = new Vector2((float)JsUnits.PxToMetres(js.Range[0]), (float)JsUnits.PxToMetres(js.Range[1])),
                chance = (float)js.Chance,
            });

            var nidan = Reg(SumiNidanId, SumiNidanMove, 5.5f, 0.85f, 2.35f, 0.45f);
            nidan.move.variantFor = f => PhaseOf(f) >= 1 ? SumiNidanStormMove : null;
            Reg(SumiBaraiId, SumiBaraiMove, 6.5f, 0.7f, 2.05f, 0.4f);
            Reg(BokusekiWaveId, BokusekiMove, 6.5f, 2.25f, 6.6f, 0.5f);
            Reg(ThunderRedactionId, ThunderRedactionMove, 8.0f, 2.4f, 7.8f, 0.5f);
            Patterns();
        }

        /// <summary>
        /// Kuroe's strings (sub-boss I, <see cref="SubBossTier.Kuroe"/>: the rarest chains and the longest beat between links, so every link is
        /// a readable tell). The ink sweep drives Kage back into the wave's range; the wave is followed in with the two-cut or, in Phase 2, sealed
        /// where he stands; the Nidan's rising cut opens him for the Redaction.
        /// </summary>
        static void Patterns()
        {
            var t = SubBossTier.Kuroe;
            t.Chain(SumiBaraiId, BokusekiWaveId, SumiNidanId);
            t.Chain(BokusekiWaveId, ThunderRedactionId, SumiNidanId);
            t.Chain(SumiNidanId, ThunderRedactionId, SumiBaraiId);
            t.Chain(ThunderRedactionId, BokusekiWaveId);
        }

        static AbilityDef Reg(string id, MoveDef m, float cdSeconds, float near, float far, float chance)
        {
            var d = new AbilityDef { id = id, move = m, cooldownFrames = Frames(cdSeconds), range = new Vector2(near, far), chance = chance };
            AbilityCatalog.Register(d);
            return d;
        }

        static Pose P() => new Pose();
        static Pose Hold(Pose p, float x, float y, float blade) => p.HandF(x, y).Blade(blade).Grip();
        static Pose GuardBody() => P().Hip(-0.05f, -0.12f, -2f).Chest(4f).Head(-6f).Feet(0.22f, -0.3f);
        static Pose Lunge(float hx = 0.12f, float hy = -0.14f) => P().Hip(hx, hy, -12f).Chest(-22f).Head(12f).Feet(0.42f, -0.28f);

        static void BuildMoves()
        {
            if (SumiNidanMove != null) return;

            InkSparkLine = new HazardDef
            {
                id = "kuroe.sumiNidan.inkLine",
                telegraphFrames = 22,
                strikeFrames = 5,
                size = new Vector2(2.2f, 0.55f),
                hit = new HitWindow { dmg = Dmg(5), level = HitLevel.Mid, kb = new Vector2(1.6f, 2.8f), stun = 22, hitstop = 6, shake = 0.18f },
                telegraphCue = InkLineMarkCue,
                strikeCue = InkLineStrikeCue,
            };

            RedactionSeal = new HazardDef
            {
                id = "kuroe.thunderRedaction.seal",
                telegraphFrames = 34,
                followFrames = 12,
                strikeFrames = 4,
                size = new Vector2(1.15f, 3.8f),
                hit = new HitWindow { dmg = Dmg(8), level = HitLevel.Mid, kb = new Vector2(2.0f, 3.6f), stun = 24, hitstop = 9, shake = 0.32f, knockdown = true, heavy = true },
                telegraphCue = RedactionMarkCue,
                strikeCue = RedactionBoltCue,
            };

            InkWave = new ProjectileDef
            {
                id = InkWaveProjectileId,
                speed = 6.2f,
                radius = 0.28f,
                lifeFrames = 78,
                maxHits = 1,
                hit = new HitWindow { dmg = Dmg(6), level = HitLevel.Mid, kb = new Vector2(2.4f, 0.6f), stun = 20, hitstop = 6, shake = 0.18f },
                flightCue = InkWaveFlightCue,
                hitCue = InkWaveHitCue,
                endCue = InkWaveHitCue,
            };

            SumiNidanMove = BuildSumiNidan(false);
            SumiNidanStormMove = BuildSumiNidan(true);

            // Sumi Barai → Shōmen Drop: Bōjutsu low brush sweep that chains on contact into an overhead calligraphy smash
            ShomenDropMove = new MoveDef("kuroe.shomenDrop", "Shōmen Ink Smash", 40)
                .Key(8, Hold(P().Hip(-0.02f, -0.06f, 6f).Chest(14f).Head(-10f).Feet(0.28f, -0.3f), 0.08f, 1.82f, 108f), Ease.OutQuad)
                .Key(15, Hold(P().Hip(0.14f, -0.34f, -12f).Chest(-38f).Head(20f).Feet(0.46f, -0.32f), 0.68f, 0.72f, -32f), Ease.InCubic)
                .Key(22, Hold(P().Hip(0.14f, -0.35f, -12f).Chest(-36f).Head(18f).Feet(0.46f, -0.32f), 0.66f, 0.74f, -34f))
                .Key(40, Stance(), Ease.InOutQuad)
                .Move(10, 15, 2.2f)
                .Hit(14, 18, new[] { HitPoint.Blade }, Dmg(8), HitLevel.Overhead, new Vector2(2.8f, 2.2f), stun: 24, hitstop: 8, shake: 0.28f, knockdown: true, heavy: true, radius: 0.14f)
                .Cue(4, SealGlintCue).Cue(15, InkSlamCue)
                .Fx(13, 19, FxKind.TrailBlade).Fx(15, 15, FxKind.Dust)
                .Impulse(15, new Vector2(2.4f, -2.0f))
                .Sword();

            SumiBaraiMove = new MoveDef(SumiBaraiId, "Sumi Barai", 48)
                .Key(10, Hold(P().Hip(-0.06f, -0.36f, 2f).Chest(-22f).Head(14f).FootN(0.26f, 0.13f).FootF(-0.3f, 0.15f, -25f), -0.08f, 0.72f, 174f), Ease.OutQuad)
                .Key(19, Hold(P().Hip(-0.08f, -0.38f, 4f).Chest(-24f).Head(16f).FootN(0.26f, 0.13f).FootF(-0.3f, 0.15f, -25f), -0.12f, 0.68f, 176f))
                .Key(23, Hold(P().Hip(0.08f, -0.38f, -8f).Chest(-30f).Head(18f).FootN(0.38f, 0.13f).FootF(-0.3f, 0.15f, -25f), 0.62f, 0.56f, 4f), Ease.OutExpo)
                .Key(29, Hold(P().Hip(0.08f, -0.38f, -8f).Chest(-30f).Head(18f).FootN(0.38f, 0.13f).FootF(-0.3f, 0.15f, -25f), 0.58f, 0.58f, 6f))
                .Key(48, Stance(), Ease.InOutQuad)
                .Move(17, 23, 3.2f)
                .Hit(20, 24, new[] { HitPoint.Blade }, Dmg(5), HitLevel.Low, new Vector2(1.1f, 0f), stun: 22, hitstop: 6, shake: 0.16f, radius: 0.16f)
                .Cue(8, SealGlintCue).Cue(21, InkSweepCue)
                .Fx(19, 25, FxKind.TrailBlade)
                .Impulse(21, new Vector2(2.0f, -0.6f))
                .Sword();
            SumiBaraiMove.onContact = ShomenDropMove;

            // Bokuseki Wave: Bleach/Ichimonji horizontal brush slash launching an ink crescent wave
            BokusekiMove = new MoveDef(BokusekiWaveId, "Bokuseki Wave", 54)
                .Key(10, Hold(P().Hip(-0.08f, -0.16f, 8f).Chest(12f).Head(-6f).Feet(0.24f, -0.32f), -0.16f, 1.02f, -162f), Ease.OutQuad)
                .Key(22, Hold(P().Hip(-0.1f, -0.18f, 10f).Chest(14f).Head(-8f).Feet(0.24f, -0.32f), -0.2f, 1.0f, -166f))
                .Key(26, Hold(Lunge(), 0.64f, 1.08f, 6f), Ease.OutExpo)
                .Key(33, Hold(Lunge(), 0.6f, 1.14f, 18f))
                .Key(54, Stance(), Ease.InOutQuad)
                .Hit(24, 28, new[] { HitPoint.Blade }, Dmg(7), HitLevel.Mid, new Vector2(2.6f, 0.8f), stun: 22, hitstop: 7, shake: 0.22f, radius: 0.14f)
                .Throw(25, InkWave, new Vector2(0.85f, 0.95f))
                .Cue(6, InkSoakCue).Cue(14, InkSoakCue).Cue(18, SealGlintCue).Cue(25, InkArcDownCue)
                .Fx(23, 29, FxKind.TrailBlade)
                .Impulse(25, new Vector2(2.6f, 0.5f))
                .Sword();

            // Thunder Redaction (Phase 2): Kuroe calls Raikon's lightning down into an inked censor seal under Kage
            ThunderRedactionMove = new MoveDef(ThunderRedactionId, "Thunder Redaction", 62)
                .Key(10, Hold(P().Hip(-0.02f, -0.04f, 4f).Chest(12f).Head(-14f).Feet(0.26f, -0.3f), 0.1f, 1.82f, 96f), Ease.OutQuad)
                .Key(32, Hold(P().Hip(-0.02f, -0.04f, 6f).Chest(14f).Head(-16f).Feet(0.26f, -0.3f), 0.08f, 1.86f, 94f))
                .Key(37, Hold(Lunge(0.1f, -0.22f), 0.56f, 0.86f, -34f), Ease.InCubic)
                .Key(46, Hold(Lunge(0.1f, -0.22f), 0.54f, 0.88f, -36f))
                .Key(62, Stance(), Ease.InOutQuad)
                .HazardOnTarget(2, RedactionSeal)
                .Cue(4, InkSoakCue).Cue(12, InkSoakCue).Cue(22, SealGlintCue).Cue(36, InkSlamCue)
                .Impulse(36, new Vector2(2.2f, -2.4f))
                .Sword();
        }

        static MoveDef BuildSumiNidan(bool storm)
        {
            var m = new MoveDef(storm ? "kuroe.sumiNidan.storm" : SumiNidanId, "Ichimonji · Nidan", 62)
                .Key(8, Hold(P().Hip(-0.04f, -0.1f, 6f).Chest(10f).Head(-6f).Feet(0.24f, -0.3f), 0.02f, 1.56f, 132f), Ease.OutQuad)
                .Key(17, Hold(P().Hip(-0.06f, -0.12f, 8f).Chest(12f).Head(-6f).Feet(0.24f, -0.3f), 0.0f, 1.6f, 136f))
                .Key(21, Hold(Lunge(), 0.62f, 0.92f, -26f), Ease.OutExpo)
                .Key(28, Hold(Lunge(), 0.54f, 0.82f, -40f), Ease.InOutQuad)
                .Key(36, Hold(Lunge(0.14f, -0.1f), 0.6f, 1.44f, 52f), Ease.OutExpo)
                .Key(43, Hold(Lunge(0.14f, -0.1f), 0.56f, 1.48f, 58f))
                .Key(62, Stance(), Ease.InOutQuad)
                .Move(16, 21, 2.8f).Move(31, 36, 2.2f)
                .Hit(19, 23, new[] { HitPoint.Blade }, Dmg(6), HitLevel.Mid, new Vector2(1.4f, 0.3f), stun: 22, hitstop: 6, shake: 0.18f, radius: 0.14f)
                .Hit(34, 38, new[] { HitPoint.Blade }, Dmg(8), HitLevel.Mid, new Vector2(3.0f, 1.4f), stun: 24, hitstop: 8, shake: 0.25f, heavy: true, radius: 0.15f)
                .Cue(7, SealGlintCue).Cue(20, InkArcDownCue).Cue(35, InkArcUpCue)
                .Fx(18, 24, FxKind.TrailBlade).Fx(33, 39, FxKind.TrailBlade)
                .Impulse(20, new Vector2(2.0f, -1.2f)).Impulse(35, new Vector2(1.8f, 2.0f))
                .Sword();
            if (storm) m.HazardAlso(35, InkSparkLine, 1.55f);
            return m;
        }

        /// <summary>The stance: the brush-staff comes up crosswise, holds for the JS stance, then drops.</summary>
        static MoveDef Censure(JsBossAbility js)
        {
            int hold = Frames(js.Stance);
            int counterTo = RaiseFrames + hold - 1;
            int total = counterTo + 1 + DropFrames;
            var m = new MoveDef("kuroe.censure", "Censure", total)
                .Key(RaiseFrames, Hold(GuardBody(), 0.3f, 1.36f, 118f), Ease.OutExpo)
                .Key(counterTo, Hold(GuardBody().Hip(-0.05f, -0.13f, -2f), 0.3f, 1.35f, 120f))
                .Key(total, Stance(), Ease.InOutQuad)
                .Cue(RaiseFrames, SealGlintCue)
                .Fx(RaiseFrames, RaiseFrames, FxKind.Glint).Sword();
            m.counterFrom = RaiseFrames;
            m.counterTo = counterTo;
            m.counterFrontOnly = true;
            m.counterAnswer = Punish(js);
            return m;
        }

        /// <summary>The punish: the JS delay, then a dashing diagonal Ichimonji ink slash, a knockdown; blockable when the JS says so.</summary>
        static MoveDef Punish(JsBossAbility js)
        {
            int delay = Frames(js.Punish);
            int live = 12;
            int total = delay + 33;
            var level = js.Unb == true ? HitLevel.Unblockable : HitLevel.Mid;
            int dmg = Mathf.RoundToInt(CounterAnswerDamage * (float)js.Dmg);
            return new MoveDef("kuroe.censurePunish", "Censure", total)
                .Key(Mathf.Max(1, delay - 2), Hold(GuardBody().Chest(10f).Head(-8f), 0.14f, 1.56f, 128f), Ease.OutQuad)
                .Key(delay + 3, Hold(P().Hip(0.2f, -0.18f, -16f).Chest(-28f).Head(16f).Feet(0.5f, -0.32f), 0.72f, 0.98f, -14f), Ease.OutExpo)
                .Key(delay + live, Hold(P().Hip(0.2f, -0.18f, -16f).Chest(-28f).Head(16f).Feet(0.5f, -0.32f), 0.7f, 0.96f, -18f))
                .Key(total, Stance(), Ease.InOutQuad)
                .Move(delay, delay + 4, 7.0f)
                .Hit(delay, delay + live, new[] { HitPoint.Blade }, dmg, level, new Vector2(2.6f, 3.4f), stun: 26, hitstop: 9, shake: 0.3f,
                    knockdown: true, heavy: true, radius: 0.09f)
                .Cue(delay + 1, InkPunishCue)
                .Fx(delay - 1, delay + live, FxKind.TrailBlade).Fx(delay, delay + 6, FxKind.AfterImage).Fx(delay, delay, FxKind.Dust)
                .Impulse(delay, new Vector2(3.0f, -1.5f))
                .Sword();
        }

        // ---------------------------------------------------------------------------------------------- cues
        // The look itself is in Vfx.Kuroe.cs (docs/act1-subbosses-vfx.md); her brush trail and her impacts come from KuroeLook on every move.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void RegisterCues()
        {
            EffectCues.Register(SealGlintCue, (Vector2 p, in CueArgs a) => Vfx.I?.KuroeNibGlint(Tip(a, p, 0.95f)));
            EffectCues.Register(InkArcDownCue, (Vector2 p, in CueArgs a) => PlayFling(p, a, true));
            EffectCues.Register(InkArcUpCue, (Vector2 p, in CueArgs a) => PlayFling(p, a, false));
            EffectCues.Register(InkPunishCue, PlayInkPunish);
            EffectCues.Register(InkSweepCue, PlayInkSweep);
            EffectCues.Register(InkSlamCue, PlayInkSlam);
            EffectCues.Register(InkSoakCue, (Vector2 p, in CueArgs a) => Vfx.I?.KuroeInkGather(Tip(a, p, 0.95f), StormPhase(a)));
            EffectCues.Register(InkWaveFlightCue, InkWaveLook.Play);
            EffectCues.Register(InkWaveHitCue, (Vector2 p, in CueArgs a) => Vfx.I?.KuroeInkHit(p, a.dir, 1.25f, 0f, false));
            EffectCues.Register(InkLineMarkCue, (Vector2 p, in CueArgs a) => Vfx.I?.KuroeInkLineMark(p - Vector2.right * a.size.x * 0.5f, p + Vector2.right * a.size.x * 0.5f, a.seconds));
            EffectCues.Register(InkLineStrikeCue, (Vector2 p, in CueArgs a) => Vfx.I?.KuroeInkLineStrike(p - Vector2.right * a.size.x * 0.5f, p + Vector2.right * a.size.x * 0.5f));
            EffectCues.Register(RedactionMarkCue, KuroeRedactionMark.Play);
            EffectCues.Register(RedactionBoltCue, (Vector2 p, in CueArgs a) => Vfx.I?.KuroeRedactionStrike(p, Mathf.Max(0.45f, a.size.x * 0.5f)));
        }

        static Vector2 Tip(in CueArgs a, Vector2 fallback, float k = 0.95f)
            => a.owner != null && a.owner.rig != null ? a.owner.rig.World(B.Sword, a.owner.BladeTipLocal * k) : fallback;

        static bool StormPhase(in CueArgs a) => PhaseOf(a.owner) >= 1;

        /// <summary>Ink flung off the tip on the cut, along the swing: down-and-forward for the kesa cut, up-and-forward for the rising cut.</summary>
        static void PlayFling(Vector2 pos, in CueArgs a, bool downward)
        {
            var v = Vfx.I;
            if (v == null) return;
            float face = a.owner != null ? a.owner.facing : 1f;
            v.KuroeInkFling(Tip(a, pos), new Vector2(face, downward ? -0.75f : 0.85f), StormPhase(a));
        }

        static void PlayInkPunish(Vector2 pos, in CueArgs a)
        {
            var v = Vfx.I;
            if (v == null) return;
            float face = a.owner != null ? a.owner.facing : 1f;
            Vector2 c = a.owner != null ? a.owner.Pos + new Vector2(face * 0.4f, 1.0f) : pos;
            v.KuroeIchimonji(c + new Vector2(-face * 0.7f, 0.7f), c + new Vector2(face * 1.5f, -0.5f), StormPhase(a));
        }

        static void PlayInkSweep(Vector2 pos, in CueArgs a)
        {
            var v = Vfx.I;
            if (v == null) return;
            float face = a.owner != null ? a.owner.facing : 1f;
            Vector2 foot = a.owner != null ? a.owner.Pos : pos;
            v.KuroeFloorStroke(foot - Vector2.right * (face * 0.35f), foot + Vector2.right * (face * 1.5f));
        }

        static void PlayInkSlam(Vector2 pos, in CueArgs a)
        {
            var v = Vfx.I;
            if (v == null) return;
            Vector2 at = new(Tip(a, pos).x, a.owner != null ? a.owner.Pos.y : pos.y);
            v.KuroeInkSlam(at, 1f, StormPhase(a));
        }
    }
}
