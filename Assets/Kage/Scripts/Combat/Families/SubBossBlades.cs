using System.Collections.Generic;
using UnityEngine;
using static Kage.SubBossDrawing;

namespace Kage
{
    internal static class SubBossBlades
    {
        public const float GreatswordLength = 1.30f;

        // Local +x follows the hit line, from guard to tip. Both cutting edges meet on that line.
        static BladePen Sword(FighterRig rig, string name, float length, Vector2 axis)
        {
            var origin = KMath.Rot(rig.def.bladeBaseLocal,-rig.def.bladeBindAngle);
            return new BladePen(rig,name,B.Sword,origin,axis,length/100f);
        }

        public static void Greatsword(FighterRig rig)
        {
            var dir = KMath.Rot(rig.def.bladeTipLocal-rig.def.bladeBaseLocal,-rig.def.bladeBindAngle).normalized;
            var p = Sword(rig,"StormGreatsword",GreatswordLength,dir);
            var spine = new List<Vector2> { new(0,-5),new(10,-9),new(27,-11),new(25,-6),
                new(44,-10),new(42,-5),new(64,-9),new(62,-4),new(85,-7),new(100,0) };
            var edge = new List<Vector2> { new(0,5),new(12,8),new(45,10),new(72,9),new(87,6),new(100,0) };
            var blade = new List<Vector2>(spine);
            for(int i=edge.Count-2;i>=0;i--) blade.Add(edge[i]);
            p.Fill("Blade",0,Ink("586273"),PartRole.Metal,blade,true);
            p.Fill("Bevel",1,Ink("BDCAD0"),PartRole.Metal,new Vector2[]{new(0,2),new(44,5),new(73,4),new(100,0),
                new(87,6),new(72,9),new(45,10),new(12,8),new(0,5)});
            p.Fill("StormVein",2,GaranPalette.Storm,PartRole.Trim,new Vector2[]{new(4,-2),new(34,-5),new(29,-1),
                new(58,-4),new(54,-1),new(80,-3),new(48,2),new(51,-1),new(24,2),new(28,-2)});
            p.Grip("Leather",1,-38,0,2.4f,Ink("412F36"),Ink("B18D65"),6);
            p.Fill("Guard",2,Ink("977254"),PartRole.Metal,new Vector2[]{new(-4,-12),new(5,-16),
                new(1,-6),new(2,6),new(6,14),new(-3,11),new(-5,3)},true);
        }

        public static void LieutenantKatana(FighterRig rig)
        {
            var p = Sword(rig,"LieutenantKatana",SpearMoves.BladeLength,Vector2.right);
            CurvedSteel(p,2.0f,Ink("716E89"),Ink("E3DBF6"));
            p.Grip("Tsuka",1,-47,-1,1.5f,Ink("292331"),Ink("A88AB9"),10);
            p.Ellipse("Tsuba",2,Ink("A39AAE"),PartRole.Metal,-0.7f,0,1.9f,5.1f,true);
            p.Fill("Habaki",3,Ink("BEA379"),PartRole.Metal,new Vector2[]{new(0,-1.7f),new(4,-1.6f),new(4,1.7f),new(0,2)});
        }

        public static void RainBlade(BladePen p)
        {
            CurvedSteel(p,4.3f,Ink("53828D"),Ink("D3F4EE"));
            p.Grip("Grip",1,-33,-2,3,Ink("253940"),Ink("7FB6B3"),4);
            p.Ellipse("Guard",2,Ink("98C2C3"),PartRole.Metal,-1,0,2.4f,6.5f,true);
        }

        static void CurvedSteel(BladePen p,float width,Color flat,Color bright)
        {
            var outline = new List<Vector2>();
            var bevel = new List<Vector2>();
            const int segments=18;
            for(int i=0;i<=segments;i++)
            {
                float t=i/(float)segments;
                float bow=width*Mathf.Sin(Mathf.PI*t);
                float w=width*(1-t*t);
                outline.Add(new Vector2(t*100,bow-w));
                bevel.Add(new Vector2(t*100,bow+0.1f*w));
            }
            for(int i=segments-1;i>=0;i--)
            {
                float t=i/(float)segments;
                var edge=new Vector2(t*100,width*(Mathf.Sin(Mathf.PI*t)+(1-t*t)*Mathf.Clamp01((1-t)*6)));
                outline.Add(edge); bevel.Add(edge);
            }
            p.Fill("Blade",0,flat,PartRole.Metal,outline,true);
            p.Fill("Hamon",1,bright,PartRole.Metal,bevel);
        }
    }
}
