using UnityEngine;

namespace Kage
{
    /// <summary>
    /// The weapon styles' own named cues (ADR-0007; docs/weapon-styles.md): Iwakudaki's fissure & heavy ground impacts,
    /// and Onikiri's ground-gouging spark drag during windup.
    /// </summary>
    public static class WeaponCues
    {
        public const string OnikiriSparkDragCue = "VFX_Onikiri_SparkDrag";
        public const string HammerGroundImpactCue = "VFX_Hammer_GroundImpact";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            EffectCues.Register(HammerMoves.FissureCue, PlayFissure);
            EffectCues.Register(HammerMoves.FissureHitCue, PlayFissureHit);
            EffectCues.Register(OnikiriSparkDragCue, PlaySparkDrag);
            EffectCues.Register(HammerGroundImpactCue, PlayHammerImpact);
        }

        static void PlayFissure(Vector2 pos, in CueArgs a)
        {
            if (a.follow == null) return;
            var go = new GameObject("FissureTrail");
            go.transform.SetParent(a.follow, false);
            go.AddComponent<FissureTrail>().dir = a.dir.x >= 0f ? 1f : -1f;
        }

        static void PlayFissureHit(Vector2 pos, in CueArgs a)
        {
            if (Vfx.I == null) return;
            Vfx.I.QuakeStomp(new Vector2(pos.x, 0f), 0.9f);
        }

        /// <summary>Onikiri Spark-Drag Uppercut: hot steel friction sparks & dust ripped along the floor under the dragging cleaver tip.</summary>
        static void PlaySparkDrag(Vector2 pos, in CueArgs a)
        {
            if (Vfx.I == null) return;
            float dir = a.dir.x >= 0f ? 1f : -1f;
            Vector2 tip = pos;
            if (a.owner != null && a.owner.rig != null)
                tip = a.owner.rig.World(B.Sword, a.owner.BladeTipLocal);
            Vector2 at = new(tip.x, Mathf.Max(0.04f, Mathf.Min(tip.y, 0.24f)));
            Vfx.I.Dust(new Vector2(at.x, 0.02f), 0.55f, -dir);
            for (int i = 0; i < 6; i++)
            {
                Color c = i < 2 ? Color.white : i < 4 ? new Color(1f, 0.78f, 0.34f, 1f) : new Color(1f, 0.42f, 0.18f, 1f);
                Vector2 vel = new(-dir * Random.Range(2.4f, 5.8f), Random.Range(0.9f, 3.4f));
                Vfx.I.Spark(at, vel, c, Random.Range(0.025f, 0.045f), Random.Range(0.18f, 0.34f));
            }
        }

        /// <summary>Iwakudaki Crushing Overhead Smash / Meteor Sunder: heavy ground-impact dust, floor cracks, shockwave, and earth debris.</summary>
        static void PlayHammerImpact(Vector2 pos, in CueArgs a)
        {
            if (Vfx.I == null) return;
            float dir = a.dir.x >= 0f ? 1f : -1f;
            Vector2 at = a.owner != null ? new Vector2(a.owner.Pos.x + dir * 0.92f, 0.02f) : new Vector2(pos.x, 0.02f);
            Vfx.I.Dust(at, 1.35f, dir);
            Vfx.I.Dust(at, 1.1f, -dir);
            Vfx.I.Shockwave(at, 1.35f, new Color(1f, 0.75f, 0.25f, 1f));
            Vfx.I.GroundCracks(at.x, 4, 0.28f, 0.65f, new Color(1f, 0.71f, 0.12f, 1f), new Color(1f, 0.945f, 0.75f, 1f), 0.85f);
            for (int i = 0; i < 8; i++)
            {
                float sx = Random.Range(-3.2f, 3.2f);
                float sy = Random.Range(2.4f, 6.2f);
                Color c = i % 3 == 0 ? new Color(1f, 0.85f, 0.42f, 1f) : new Color(0.36f, 0.30f, 0.22f, 1f);
                Vfx.I.Spark(at + new Vector2(Random.Range(-0.22f, 0.22f), 0.04f), new Vector2(sx, sy), c, Random.Range(0.04f, 0.075f), Random.Range(0.35f, 0.55f));
            }
        }

        /// <summary>Rides the fissure: every few frames the floor cracks open where it passes, gold light in the crack, rock thrown up.</summary>
        sealed class FissureTrail : MonoBehaviour
        {
            public float dir = 1f;
            float _lastX = float.NaN;

            void Update()
            {
                if (Vfx.I == null) return;
                float x = transform.position.x;
                if (!float.IsNaN(_lastX) && Mathf.Abs(x - _lastX) < 0.28f) return;
                _lastX = x;
                Vfx.I.GroundCracks(x, 2, 0.2f, 0.45f, new Color(1f, 0.71f, 0.12f, 1f), new Color(1f, 0.945f, 0.75f, 1f), 0.7f);
                for (int i = 0; i < 3; i++)
                    Vfx.I.Spark(new Vector2(x + Random.Range(-0.1f, 0.1f), 0.05f), new Vector2(dir * Random.Range(0.2f, 1.2f), Random.Range(2f, 5f)), i == 0 ? new Color(1f, 0.85f, 0.4f, 1f) : new Color(0.3f, 0.24f, 0.17f, 1f), 0.05f, 0.5f);
                Vfx.I.Dust(new Vector2(x, 0f), 0.5f, -dir);
            }
        }
    }
}
