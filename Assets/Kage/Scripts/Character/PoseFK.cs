using UnityEngine;

namespace Kage
{
    /// <summary>
    /// Forward kinematics of a <see cref="Pose"/> on a <see cref="RigDef"/> without any GameObjects (SAN-118): where every
    /// bone's pivot lands and how it is turned, in root space, facing right. It follows <see cref="FighterRig.Apply"/>
    /// exactly, so <see cref="Point"/> is what <see cref="FighterRig.World"/> gives for an unflipped, unscaled rig at the
    /// origin. Pose checks (grip, edge, kamae) run on it in EditMode.
    /// </summary>
    public sealed class PoseFK
    {
        public readonly RigDef def;
        /// <summary>Bone pivots, root space.</summary>
        public readonly Vector2[] pos = new Vector2[(int)B.COUNT];
        /// <summary>Bone world angles, degrees.</summary>
        public readonly float[] rot = new float[(int)B.COUNT];
        bool _edgeUp;

        public PoseFK(RigDef d) { def = d; }

        public PoseFK Solve(Pose p)
        {
            p.Resolve(def);
            pos[0] = def.pivot[0];
            rot[0] = 0f;
            for (int i = 1; i < (int)B.COUNT; i++)
            {
                var b = (B)i;
                int par = (int)def.parent[i];
                Vector2 local = def.BindLocal(b);
                if (b == B.Hips) local += p.hip;
                else if (b == B.Sword && p.shaftSlide != 0f) local += KMath.Dir(p.a[(int)B.Sword] + def.bladeBindAngle) * p.shaftSlide;
                pos[i] = pos[par] + KMath.Rot(local, rot[par]);
                rot[i] = rot[par] + (b == B.CoatN || b == B.CoatF ? 0f : p.a[i]);
            }
            _edgeUp = p.edgeUp;
            return this;
        }

        /// <summary>A bone-local point (bind-space offset from the bone's pivot) in root space; the sword turned over when the pose says so.</summary>
        public Vector2 Point(B b, Vector2 local)
        {
            if (b == B.Sword && _edgeUp) local = Pose.TurnOver(local, def.bladeBindAngle);
            return pos[(int)b] + KMath.Rot(local, rot[(int)b]);
        }

        /// <summary>A point on the katana's axis, <paramref name="along"/> metres from the sword pivot (negative: towards the pommel).</summary>
        public Vector2 OnBlade(float along) => Point(B.Sword, KMath.Rot(new Vector2(along, 0f), def.bladeBindAngle));

        /// <summary>The blade's direction, root-space degrees (0 forward, 90 up).</summary>
        public float BladeAngle => rot[(int)B.Sword] + def.bladeBindAngle;

        /// <summary>The way the cutting edge faces, a unit vector: the blade turned clockwise, or anticlockwise when it is turned over (<see cref="Pose.EdgeUp"/>).</summary>
        public Vector2 EdgeNormal => KMath.Dir(BladeAngle + (_edgeUp ? 90f : -90f));

        /// <summary>The near fist's centre (the rear hand on the handle).</summary>
        public Vector2 NearFist => Point(B.HandN, def.fistLocal);

        /// <summary>How far the near fist's centre is from the katana's handle (pommel 0.17 behind the sword pivot to the guard 0.03 ahead), metres.</summary>
        public float NearFistToHandle()
        {
            Vector2 a = OnBlade(-0.17f), b = OnBlade(0.03f), f = NearFist;
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(f - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - f).magnitude;
        }
    }
}
