using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The placeholder look of a weapon clash (SAN-93, ADR-0007): <c>VFX_Kage_Clash</c> is a flare where the blades met, a ring,
    /// and gold sparks thrown out both ways (the JS game's clash flare, ring and two spark fans), with a steel clang.
    /// The real effect replaces it by registering the same id. Numbers are in clash.json.
    /// </summary>
    public static class ClashCues
    {
        public const string Clash = "VFX_Kage_Clash";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => EffectCues.Register(Clash, PlayClash);

        static void PlayClash(Vector2 pos, in CueArgs a)
        {
            var t = Tuning.Get<ClashTuning>();
            Vfx.I.Star(pos, t.flareSize, Color.white, t.sparkTint);
            Vfx.I.Ring(pos, 0.1f, t.ringRadius, 0.06f, t.sparkTint, 0.3f, true);
            // one fan along the line between the fighters each way, a little upward (cosmetic randomness)
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < t.sparksPerSide; i++)
                {
                    float ang = Random.Range(-70f, 70f);
                    Vector2 dir = KMath.Dir(side > 0 ? ang : 180f - ang);
                    Vfx.I.Spark(pos, dir * Random.Range(t.sparkSpeedMin, t.sparkSpeedMax) + Vector2.up * 1.5f, t.sparkTint, Random.Range(0.025f, 0.05f), t.sparkLife * Random.Range(0.7f, 1.2f));
                }
            KageGame.I?.Audio?.Play(SfxKind.Block, t.soundVolume);
        }
    }
}
