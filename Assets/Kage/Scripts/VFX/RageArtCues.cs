using UnityEngine;

namespace Kage
{
    /// <summary>The look of a rage art: its name, kanji and colours (the JS <c>RAGE_ARTS</c> table).</summary>
    public sealed class RageArtLook
    {
        public string name, kanji;
        public Color col, hot, deep;

        /// <summary>Crimson Moon, 紅月: crimson, a pale pink core, a near-black red.</summary>
        public static readonly RageArtLook Crimson = new()
        {
            name = "Crimson Moon", kanji = "紅月",
            col = KMath.Hex("#ff2238"), hot = KMath.Hex("#ffd0d4"), deep = KMath.Hex("#2a0008"),
        };

        /// <summary>The look of the art with this id, or null (Thousand Shadows has no cut-in).</summary>
        public static RageArtLook Of(string artId) => artId == Fighter.CrimsonMoon ? Crimson : null;
    }

    /// <summary>
    /// Crimson Moon's placeholder effects as named cues (ADR-0007, SAN-82). Each is registered from here, so the real
    /// effects replace them by registering the same ids:
    /// <list type="bullet">
    /// <item><c>VFX_Rage_Crimson_Crescent</c> (the JS manifest id): the crimson crescent of a cut or the giant one, a slash
    /// along <c>dir</c> at <c>scale</c>, a ring, sparks and the hit's burst.</item>
    /// <item><c>VFX_Kage_RageCutIn</c>: the cut-in band with the art's name and kanji across the HUD (<c>seconds</c> long).</item>
    /// <item><c>VFX_Kage_CrimsonMoonFinish</c>: the finisher's flash, shockwave and rings. <c>VFX_Kage_CrimsonMoonStep</c>:
    /// the afterimage Kage leaves between cuts. <c>VFX_Kage_CrimsonMoonCharge</c>: crimson motes drawn into the blade.</item>
    /// </list>
    /// </summary>
    public static class RageArtCues
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            EffectCues.Register(Fighter.RageArtCutInCue, PlayCutIn);
            EffectCues.Register(Fighter.CrimsonCrescentCue, PlayCrescent);
            EffectCues.Register(Fighter.CrimsonFinishCue, PlayFinish);
            EffectCues.Register(Fighter.CrimsonStepCue, PlayStep);
            EffectCues.Register(Fighter.CrimsonChargeCue, PlayCharge);
        }

        static void PlayCutIn(Vector2 pos, in CueArgs a)
        {
            var owner = a.owner;
            var hud = owner != null && owner.ctx != null ? owner.ctx.Hud : null;
            var look = RageArtLook.Of(owner != null ? owner.ActiveRageArt : null);
            if (hud == null || look == null) return;
            string weapon = string.IsNullOrEmpty(owner.WeaponId) ? "" : "  ·  " + owner.WeaponId.Replace('_', ' ').ToUpperInvariant();
            hud.CutIn(look.name.ToUpperInvariant(), look.kanji, "RAGE ART" + weapon, look.col, look.hot, a.seconds > 0f ? a.seconds : 0.7f);
        }

        static void PlayCrescent(Vector2 pos, in CueArgs a)
        {
            var look = RageArtLook.Crimson;
            Vector2 d = a.dir.sqrMagnitude > 1e-4f ? a.dir.normalized : Vector2.right;
            float s = a.scale > 0f ? a.scale : 1f;
            Vector2 half = d * (0.95f * s);
            Vfx.I.SlashLine(pos - half, pos + half, look.col, look.hot, 0.45f, 0.14f * s);
            Vfx.I.Ring(pos, 0.1f * s, 0.9f * s, 0.07f * s, look.col, 0.35f, false);
            Vfx.I.Star(pos, 0.5f * s, look.hot, look.col);
            for (int i = 0; i < Mathf.RoundToInt(8 * s); i++)
            {
                float ang = Random.Range(0f, 360f);
                Vfx.I.Spark(pos, KMath.Dir(ang) * Random.Range(1.5f, 4.5f * s), i % 3 == 0 ? look.hot : look.col, 0.05f * s, Random.Range(0.3f, 0.6f));
            }
            if (a.owner != null) Vfx.I.HitBurst(pos, d, 0.9f + 0.3f * s, true, look.col, look.hot, look.deep);
        }

        static void PlayFinish(Vector2 pos, in CueArgs a)
        {
            var look = RageArtLook.Crimson;
            float s = a.scale > 0f ? a.scale : 1f;
            GameFeel.Flash(new Color(1f, 0.25f, 0.25f, 0.45f), 0.25f);
            Vfx.I.Shockwave(new Vector2(pos.x, 0.03f), 1.4f * s, look.col);
            Vfx.I.Ring(pos, 0.2f, 1.8f * s, 0.12f, look.col, 0.5f, false);
            Vfx.I.Ring(pos, 0.1f, 1.1f * s, 0.07f, look.hot, 0.35f, false);
            for (int i = 0; i < 26; i++)
                Vfx.I.Spark(pos, KMath.Dir(Random.Range(0f, 360f)) * Random.Range(2f, 8f) + Vector2.up * 1.0f, i % 3 == 0 ? look.hot : look.col, 0.06f, Random.Range(0.3f, 0.8f));
        }

        static void PlayStep(Vector2 pos, in CueArgs a)
        {
            if (a.owner == null || a.owner.rig == null) return;
            Vfx.I.Ghost(a.owner.rig, RageArtLook.Crimson.col.WithA(0.45f), 0.35f, a.dir * 0.1f);
        }

        static void PlayCharge(Vector2 pos, in CueArgs a)
        {
            var look = RageArtLook.Crimson;
            for (int i = 0; i < 2; i++)
            {
                Vector2 off = KMath.Dir(Random.Range(0f, 360f)) * Random.Range(0.7f, 1.4f);
                Vfx.I.Embers(pos + off, -off * 3f, i == 0 ? look.col : look.hot, Random.Range(0.03f, 0.06f), 0.3f);
            }
        }
    }
}
