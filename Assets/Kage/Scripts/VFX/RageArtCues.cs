using Kage.Rules;
using UnityEngine;

namespace Kage
{
    /// <summary>The look of a rage art: its name, kanji, colours and the finisher's word (the JS <c>RAGE_ARTS</c> table and <c>finish</c> stamps).</summary>
    public sealed class RageArtLook
    {
        public string id, name, kanji;
        /// <summary>The word slammed onto the screen at the finisher (<see cref="Hud.ArtStamp"/>): the JS stamp.</summary>
        public string stamp;
        public Color col, hot, deep;
        /// <summary>A legendary weapon's art (the rainbow tier): its name and word drift in a rainbow, its finisher has a prism ring.</summary>
        public bool legendary;

        static RageArtLook L(string id, string name, string kanji, string stamp, string col, string hot, string deep, bool legendary = false) => new()
        {
            id = id, name = name, kanji = kanji, stamp = stamp, col = KMath.Hex(col), hot = KMath.Hex(hot), deep = KMath.Hex(deep), legendary = legendary,
        };

        /// <summary>Crimson Moon, 紅月 (Kagetsu): crimson, a pale pink core, a near-black red. Finisher: MOONFALL.</summary>
        public static readonly RageArtLook Crimson = L(Fighter.CrimsonMoon, "Crimson Moon", "紅月", "MOONFALL", "#ff2238", "#ffd0d4", "#2a0008");
        /// <summary>Silent Moon, 静月 (Shizuka): violet, a lilac-white core, an ink violet. Finisher: SILENCE.</summary>
        public static readonly RageArtLook Silent = L(Fighter.SilentMoon, "Silent Moon", "静月", "SILENCE", "#a46bff", "#f0e6ff", "#12052a");
        /// <summary>Thousand Swallows, 雷燕 (Tsubame): storm cyan, an ice-white core, a deep sea blue. Finisher: RAIJIN.</summary>
        public static readonly RageArtLook Swallows = L(Fighter.SwallowStorm, "Thousand Swallows", "雷燕", "RAIJIN", "#38c8ff", "#e6f9ff", "#021626");
        /// <summary>Hell Splitter, 鬼斬 (Onikiri): hell orange-red, a molten cream core, a scorched black-red. Finisher: HELL SPLIT.</summary>
        public static readonly RageArtLook Hell = L(Fighter.HellSplitter, "Hell Splitter", "鬼斬", "HELL SPLIT", "#ff3a1c", "#ffd2a0", "#2a0500");
        /// <summary>Rising Water Dragon, 水龍 (Mizuchi): water blue, a foam-white core, an abyss blue. Finisher: MIZUCHI.</summary>
        public static readonly RageArtLook Dragon = L(Fighter.WaterDragon, "Rising Water Dragon", "水龍", "MIZUCHI", "#2f9dff", "#d4f1ff", "#001428");
        /// <summary>Reaper's Requiem, 闇鎌 (Yamigama): reaper violet, a pale orchid core, the dark itself. Finisher: REQUIEM.</summary>
        public static readonly RageArtLook Reaper = L(Fighter.ReaperRequiem, "Reaper's Requiem", "闇鎌", "REQUIEM", "#9a3cff", "#f0c8ff", "#06020c", legendary: true);
        /// <summary>Earthsplitter, 地裂 (Iwakudaki, legendary): gold, a cream-white core, a buried brown-black. Finisher: EARTHSPLITTER.</summary>
        public static readonly RageArtLook Earth = L(Fighter.Earthsplitter, "Earthsplitter", "地裂", "EARTHSPLITTER", "#ffb51f", "#fff1bf", "#1e1200", legendary: true);
        /// <summary>Thousand Shadows, 千影 (basic / shadow art): deep crimson, warm bone core, ink shadow. Finisher: SHADOWFALL.</summary>
        public static readonly RageArtLook Shadows = L(Fighter.ThousandShadows, "Thousand Shadows", "千影", "SHADOWFALL", "#e63242", "#f6e7d0", "#14060a");

        static readonly RageArtLook[] All = { Crimson, Silent, Swallows, Hell, Dragon, Reaper, Earth };

        /// <summary>The look of the art with this id, or null (Thousand Shadows is not in the cinematic table).</summary>
        public static RageArtLook Of(string artId)
        {
            foreach (var l in All) if (l.id == artId) return l;
            return null;
        }

        /// <summary>The HUD presentation look for any rage art, falling back to <see cref="Shadows"/> for Thousand Shadows or unknown ids.</summary>
        public static RageArtLook ForHud(string artId) => Of(artId) ?? Shadows;
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

        /// <summary>Human-readable weapon display name from <see cref="JsTables"/> (never raw snake_case when the weapon is known).</summary>
        public static string WeaponDisplayName(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return "";
            var w = JsTables.Weapon(weaponId);
            if (w != null && !string.IsNullOrEmpty(w.Name)) return w.Name;
            return weaponId.Replace('_', ' ');
        }

        /// <summary>Formats the cut-in subtitle: tier/art prefix + weapon display name (e.g. "LEGENDARY RAGE ART  ·  YAMIGAMA", "BOSS RAGE ART  ·  RAIKŌ NO TACHI").</summary>
        public static string FormatCutInCaption(RageArtLook look, string weaponId)
        {
            if (look == null) return "";
            string prefix;
            if (look.legendary) prefix = "LEGENDARY RAGE ART";
            else if (JsTables.Weapon(weaponId)?.Tier == "boss") prefix = "BOSS RAGE ART";
            else if (look.id == Fighter.ThousandShadows) prefix = "SHADOW ART";
            else prefix = "RAGE ART";
            string weap = WeaponDisplayName(weaponId);
            return string.IsNullOrEmpty(weap) ? prefix : prefix + "  ·  " + weap.ToUpperInvariant();
        }

        /// <summary>Formats the finisher stamp caption without repeating the stamp word (Earthsplitter uses its tier+weapon line; Mizuchi omits redundant weapon name).</summary>
        public static string FormatStampCaption(RageArtLook look, string weaponId)
        {
            if (look == null) return "";
            string art = (look.name ?? "").ToUpperInvariant();
            string stamp = (look.stamp ?? "").ToUpperInvariant();
            if (string.Equals(art, stamp, System.StringComparison.Ordinal))
                return FormatCutInCaption(look, weaponId);
            string weap = WeaponDisplayName(weaponId).ToUpperInvariant();
            if (string.IsNullOrEmpty(weap) || string.Equals(weap, stamp, System.StringComparison.Ordinal))
                return art;
            return art + "  ·  " + weap;
        }

        static void PlayCutIn(Vector2 pos, in CueArgs a)
        {
            var owner = a.owner;
            var hud = owner != null ? (owner.ctx != null && owner.ctx.Hud != null ? owner.ctx.Hud : KageGame.I?.Hud) : null;
            var look = RageArtLook.Of(owner != null ? owner.ActiveRageArt : null);
            if (hud == null || look == null) return;
            string title = look.name.ToUpperInvariant();
            if (hud.CutInShowing && hud.CutInName == title) return;
            hud.CutIn(title, look.kanji, FormatCutInCaption(look, owner.WeaponId), look.col, look.hot, a.seconds > 0f ? a.seconds : 0.7f, look.legendary, look.deep);
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
