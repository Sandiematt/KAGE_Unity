using System.Collections.Generic;
using UnityEngine;

namespace Kage
{
    /// <summary>Bones of the 2D fighter skeleton. N = near side (towards camera), F = far side.</summary>
    public enum B
    {
        Root, Hips, Chest, Neck, Head,
        ArmNU, ArmNL, HandN,          // near arm: free fist (the cuffed arm in the reference)
        ArmFU, ArmFL, HandF, Sword,   // far arm: katana arm
        LegNU, LegNL, FootN,          // near / lead leg
        LegFU, LegFL, FootF,          // far / rear leg
        CoatN, CoatF,                 // coat panels (spring driven)
        COUNT
    }

    public enum Limb { ArmN, ArmF, LegN, LegF }

    public enum PartRole { Body, Trim, TrimDeep, Metal, Eye, Hair, Boot, Detail }
    public enum SoftKind { None, Hair, Coat, Chain }

    /// <summary>How a <see cref="SoftKind.Chain"/> part moves (<see cref="SoftChains"/>): Raikon's cape, ponytail, tassels and fur (SAN-71).</summary>
    public struct ChainFeel
    {
        public float stiffRoot, stiffTip;   // pull back to the drawn shape per substep, at the root and at the tip (0..1)
        public float drag;                  // air drag: how much the part trails the body's motion and the breeze
        public float gravity;               // m/s^2 pulling the free end down
        public float breeze;                // m/s of ambient wind blowing behind the fighter
        public static ChainFeel Default => new() { stiffRoot = 0.3f, stiffTip = 0.06f, drag = 2f, gravity = 5f, breeze = 0.8f };
    }

    public class PartDef
    {
        public string name;
        public B bone;
        public Vector2[] pts;          // bind-space polygon (units, facing right)
        public Mesh mesh;              // optional custom mesh (bind space), overrides pts
        public Color color;
        public PartRole role;
        public int order;              // depth slot within the fighter
        /// <summary>
        /// -1 draws the part just under the other parts of its depth slot (above the slot below). Two overlapping parts in one
        /// slot have the same sorting order, and Unity then draws them in whatever order it picks, which can change from one
        /// render to the next in a session (SAN-114: the habaki and the tsuba).
        /// </summary>
        public int underSlot;
        public bool rim = true;
        public float rimGrow = 0.0045f;
        public Color? rimColor;
        public float glow = 1f;        // HDR multiplier (eyes, blade edge)
        public bool swordLayer;        // moves to the front during sword techniques
        public Vector2? jigglePivot;   // hair spikes: own pivot with spring motion
        public float jiggle;
        public List<(Vector2[] pts, Color col)> polys;   // many polygons painted in order into one mesh (SVG groups)
        public Vector2[] rimPts;       // a polys part's silhouette for the world-fixed back-light rim (SAN-112 cel parts; null: no rim)
        public SoftKind soft;          // deformed every frame by HairField / CoatCloth
        public int clothLayer;         // coat: 0 = front flap cloth, 1 = tail cloth (side + back panels)
        public List<Vector2[]> chains; // SoftKind.Chain: spring chains (bind space, root first) the mesh is bound to
        public ChainFeel chainFeel;
    }

    /// <summary>An extra scarf tail (armour visuals, SAN-85): a short strip on the scarf's knot that streams like the two main tails.</summary>
    public struct ScarfStrand
    {
        public Vector2 anchor;          // bind space, carried by the chest
        public float length, width, phase;
        public Color front, back;
    }

    public class HurtDef
    {
        public B bone; public Vector2 local; public float radius; public HitZone zone;
        public HurtDef(B b, Vector2 l, float r, HitZone z) { bone = b; local = l; radius = r; zone = z; }
    }

    public enum HitZone { Head, Body, Legs }

    /// <summary>Complete description of a fighter's look and skeleton, in bind (reference) pose.</summary>
    public class RigDef
    {
        public string name;
        public readonly Vector2[] pivot = new Vector2[(int)B.COUNT];
        public readonly B[] parent = new B[(int)B.COUNT];
        public readonly List<PartDef> parts = new();
        public readonly List<HurtDef> hurt = new();
        public Color rimColor = Palette.Rim;
        public Color bodyColor = Palette.Body;
        public bool scarf = true;
        public Color scarfColor = Palette.Trim, scarfBack = Palette.TrimDeep;
        public Vector2 scarfAnchorA, scarfAnchorB;   // bind space, carried by the chest
        public Color scarfEdge = Palette.TrimBright; // the lit edge of the scarf's ribbons
        public readonly List<ScarfStrand> scarfStrands = new();   // extra tails beside the two main ones (armour looks)
        public float bladeBindAngle;                  // world angle of the blade in bind pose
        public Vector2 bladeTipLocal;                 // sword-bone local tip
        public Vector2 bladeBaseLocal;                // sword-bone local blade base (guard)
        public Vector2 fistLocal, hiltLocal, footTipLocal, heelLocal;
        public Vector2 hairCentre;                    // bind space: skull centre the hair field rotates about
        public Vector2 coatF0, coatF1, coatB0, coatB1; // bind space: front and back coat ropes (belt -> hem)
        public bool softCoat;
        public bool coatTail;                          // a second, looser cloth patch for the coat tails
        public Vector2 tailF0, tailF1, tailB0, tailB1;  // bind space: tail patch ropes (belt -> hem)

        public Vector2 BindLocal(B b) => b == B.Root ? Vector2.zero : pivot[(int)b] - pivot[(int)parent[(int)b]];
        public float Len(B a, B b) => (pivot[(int)b] - pivot[(int)a]).magnitude;
    }
}
