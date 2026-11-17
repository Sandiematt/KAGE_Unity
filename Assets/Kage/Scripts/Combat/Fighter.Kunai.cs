using UnityEngine;
using static Kage.PoseLib;

namespace Kage
{
    /// <summary>
    /// The kunai (SAN-88): Special + Down throws a knife from a per-fight count (ADR-0004: the rage art
    /// comes first, see <see cref="TrySpecialDown"/>). It is the generic <see cref="Projectile"/> with the
    /// JS numbers (6 damage of a 232 hp player = 3 of 100, 1250 px/s, radius 10 px, 1.2 s) and cue
    /// <c>VFX_Takashi_Kunai</c>, the closest JS effect id.
    /// </summary>
    public partial class Fighter
    {
        /// <summary>Kunai a fighter starts a fight with (the JS player's 5). Character data and gear perks set it.</summary>
        public int kunaiStart = 5;
        /// <summary>False for characters that never throw (a character flag, glossary "CanThrowKunai").</summary>
        public bool canThrowKunai = true;

        int _kunaiLeft = -1;        // -1: not thrown any yet this fight, so the full starting count

        /// <summary>Kunai left this fight, for the HUD.</summary>
        public int KunaiCount => _kunaiLeft < 0 ? kunaiStart : _kunaiLeft;
        /// <summary>The count a fight starts with, for the HUD ("3/5").</summary>
        public int KunaiMax => kunaiStart;

        /// <summary>Back to a full count: call at the start of a fight (not a round).</summary>
        public void ResetKunai() => _kunaiLeft = -1;
        /// <summary>Sets the count directly (tests, pickups).</summary>
        public void SetKunaiCount(int n) => _kunaiLeft = Mathf.Max(0, n);

        public const string KunaiCue = "VFX_Takashi_Kunai";

        public static readonly ProjectileDef KunaiProjectile = new()
        {
            id = "kunai",
            hit = new HitWindow { dmg = 3, level = HitLevel.Mid, kb = new Vector2(0.7f, 0.2f), stun = 12, hitstop = 3, shake = 0.05f, radius = 0.09f },
            speed = 10.87f,                  // 1250 px/s
            radius = 0.09f,                  // 10 px
            lifeFrames = 72,                 // 1.2 s
            maxHits = 1,
            flightCue = KunaiCue,
        };

        /// <summary>The throw: 0.1 s wind-up, the knife leaves at frame 6, 0.18 s recovery (JS t: 0.1 / 0.05 / 0.18).</summary>
        static readonly MoveDef KunaiThrow = BuildKunaiThrow();

        static MoveDef BuildKunaiThrow()
        {
            var m = new MoveDef("kunai", "Kunai", 20)
                .Key(5, Stance().Chest(6f).HandN(-0.05f, 1.3f, 20f))
                .Key(8, Stance().Chest(-14f).HandN(0.6f, 1.2f, 0f), Ease.OutExpo)
                .Key(20, Stance(), Ease.InOutQuad);
            m.projectileFrame = 6;
            return m;
        }

        bool IsKunaiThrow(MoveDef m) => m == KunaiThrow;

        void ThrowKunai()
        {
            if (ctx == null) return;
            var p = new Vector2(Pos.x + facing * 0.26f, 1.15f);     // JS: 30 px ahead, 132 px up
            Projectile.Spawn(ctx, this, KunaiProjectile, p, facing);
            KageGame.I?.Audio?.Play(SfxKind.Slash, 0.5f);
        }

        /// <summary>Starts a throw if there is one to throw. Spends the kunai when the throw starts, as the JS game does.</summary>
        bool StartKunaiThrow()
        {
            if (!canThrowKunai || KunaiCount <= 0) return false;
            _kunaiLeft = KunaiCount - 1;
            StartMove(KunaiThrow);
            return true;
        }
    }

    /// <summary>The kunai's look as an effect cue (ADR-0007): a glint where it leaves the hand.</summary>
    public static class KunaiCues
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => EffectCues.Register(Fighter.KunaiCue, Play);

        static void Play(Vector2 pos, in CueArgs a)
        {
            if (a.owner != null) Vfx.I.Glint(pos, a.owner.fxCore);
        }
    }
}
