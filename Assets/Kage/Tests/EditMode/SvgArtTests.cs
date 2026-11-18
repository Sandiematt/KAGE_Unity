using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Kage.Tests
{
    /// <summary>
    /// The colour-trace SVG importer (SAN-71). Seam: <see cref="SvgArt.Parse"/> → <see cref="SvgArt.Cut"/>, a region
    /// polygon in, the traced colours inside it out as triangles. The expected areas are worked out by hand from the
    /// little SVGs below (one compound path per colour, even-odd fill, as in the owner's trace).
    /// </summary>
    public class SvgArtTests
    {
        // a 100 x 100 black canvas with a 40 x 40 red square at (10,10) cut out of it; the red path fills that square
        // and has a 10 x 10 hole at (30,30) that the black path fills again (a black island inside the red)
        const string Trace =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\" width=\"100\" height=\"100\">" +
            "<path d=\"M0 0C33 0 66 0 100 0C100 33 100 66 100 100C66 100 33 100 0 100C0 66 0 33 0 0Z" +
            "M10 10C10 23 10 36 10 50C23 50 36 50 50 50C50 36 50 23 50 10C36 10 23 10 10 10Z" +
            "M30 30C33 30 36 30 40 30C40 33 40 36 40 40C36 40 33 40 30 40C30 36 30 33 30 30Z\" " +
            "fill=\"#010000\" fill-rule=\"evenodd\"/>" +
            "<path d=\"M10 10C23 10 36 10 50 10C50 23 50 36 50 50C36 50 23 50 10 50C10 36 10 23 10 10Z" +
            "M30 30C30 33 30 36 30 40C33 40 36 40 40 40C40 36 40 33 40 30C36 30 33 30 30 30Z\" " +
            "fill=\"#c7181f\" fill-rule=\"evenodd\"/>" +
            "</svg>";

        static readonly Color Black = new Color32(1, 0, 0, 255), Red = new Color32(199, 24, 31, 255);

        static float Area(IEnumerable<SvgPiece> pieces, Color c)
        {
            float a = 0f;
            foreach (var p in pieces)
            {
                if (!Same(p.color, c)) continue;
                for (int i = 0; i < p.triangles.Length; i += 3)
                {
                    Vector2 u = p.vertices[p.triangles[i]], v = p.vertices[p.triangles[i + 1]], w = p.vertices[p.triangles[i + 2]];
                    a += Mathf.Abs((v.x - u.x) * (w.y - u.y) - (w.x - u.x) * (v.y - u.y)) * 0.5f;
                }
            }
            return a;
        }

        static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        static Vector2[] Rect(float x0, float y0, float x1, float y1) => new[] { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) };

        [Test]
        public void ReadsOneLayerPerColourInPaintOrderWithTheCanvasSize()
        {
            var art = SvgArt.Parse(Trace);
            Assert.AreEqual(new Vector2(100, 100), art.Size);
            Assert.AreEqual(2, art.Layers.Count);
            Assert.IsTrue(Same(Black, art.Layers[0]), "first layer is the black");
            Assert.IsTrue(Same(Red, art.Layers[1]), "second layer is the red");
        }

        [Test]
        public void CuttingByARegionKeepsOnlyTheColoursInsideIt()
        {
            var art = SvgArt.Parse(Trace);
            // the left half of the red square, with part of the black island and of the black canvas around it
            var pieces = art.Cut(new[] { Rect(0, 0, 35, 100) });
            // red: x 10..35, y 10..50 = 25 x 40, minus the island x 30..35, y 30..40 = 5 x 10
            Assert.AreEqual(25 * 40 - 5 * 10, Area(pieces, Red), 1f);
            // black: the 35 x 100 strip minus the red there, i.e. the canvas around it plus the island's half
            Assert.AreEqual(35 * 100 - (25 * 40 - 5 * 10), Area(pieces, Black), 1f);
        }

        [Test]
        public void ARegionWithAHoleLeavesTheHoleEmpty()
        {
            var art = SvgArt.Parse(Trace);
            // the whole red square as the region, with a 20 x 20 hole punched in its middle (even-odd)
            var pieces = art.Cut(new[] { Rect(10, 10, 50, 50), Rect(20, 20, 40, 40) });
            // red inside the square minus the punched hole (which also holds the island)
            Assert.AreEqual(40 * 40 - 20 * 20, Area(pieces, Red), 1f);
            Assert.AreEqual(0f, Area(pieces, Black), 1f);
        }

        [Test]
        public void ABackgroundLayerIsPaintedOverTheWholeRegionUnderTheOtherColours()
        {
            var art = SvgArt.Parse(Trace);
            art.backgroundLayer = 0;
            var pieces = art.Cut(new[] { Rect(0, 0, 35, 100) });
            Assert.AreEqual(35 * 100, Area(pieces, Black), 1f, "the whole region in black");
            Assert.AreEqual(25 * 40 - 5 * 10, Area(pieces, Red), 1f, "the red over it, as before");
            Assert.AreEqual(0, pieces[0].layer, "black first, so the red paints over it");
        }

        [Test]
        public void CuttingCellByCellGivesTheSameColoursInSmallTriangles()
        {
            var art = SvgArt.Parse(Trace);
            var pieces = art.Cut(new[] { Rect(0, 0, 35, 100) }, cell: 8f);
            Assert.AreEqual(25 * 40 - 5 * 10, Area(pieces, Red), 1f);
            Assert.AreEqual(35 * 100 - (25 * 40 - 5 * 10), Area(pieces, Black), 1f);
            foreach (var p in pieces)
                for (int i = 0; i < p.triangles.Length; i += 3)
                    for (int k = 0; k < 3; k++)
                        Assert.LessOrEqual((p.vertices[p.triangles[i + k]] - p.vertices[p.triangles[i + (k + 1) % 3]]).magnitude, 8f * 1.415f, "no edge longer than a cell's diagonal");
        }

        [Test]
        public void SeparateIslandsAreAllKept()
        {
            var art = SvgArt.Parse(Trace);
            // two strips of the red square, left and right, away from the island
            var pieces = art.Cut(new[] { Rect(10, 10, 20, 50), Rect(44, 10, 50, 50) });
            Assert.AreEqual(10 * 40 + 6 * 40, Area(pieces, Red), 1f);
        }

        [Test]
        public void CutsAreInSvgPixelsWithYDown()
        {
            var art = SvgArt.Parse(Trace);
            var pieces = art.Cut(new[] { Rect(10, 10, 50, 50) });
            var red = pieces.Where(p => Same(p.color, Red)).SelectMany(p => p.vertices).ToList();
            Assert.IsNotEmpty(red);
            Assert.AreEqual(10f, red.Min(v => v.x), 0.01f);
            Assert.AreEqual(50f, red.Max(v => v.x), 0.01f);
            Assert.AreEqual(10f, red.Min(v => v.y), 0.01f);
            Assert.AreEqual(50f, red.Max(v => v.y), 0.01f);
        }
    }
}
