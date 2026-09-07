using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace GridsAndGrimoires.EditModeTests
{
    // MagicGridManager の配置判定まわり（座標変換を含まない部分）を検証する。
    // EditMode では AddComponent で Awake が呼ばれないため、リフレクションで明示的に呼んで
    // grid 配列を確保してからテストする。
    public class GridModelTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<MagicData> createdMagics = new List<MagicData>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            spawned.Clear();
            foreach (MagicData m in createdMagics)
            {
                if (m != null) Object.DestroyImmediate(m);
            }
            createdMagics.Clear();
        }

        private MagicGridManager NewGrid(int width = 5, int height = 5)
        {
            GameObject go = new GameObject("grid");
            spawned.Add(go);
            MagicGridManager m = go.AddComponent<MagicGridManager>();
            m.width = width;
            m.height = height;
            typeof(MagicGridManager)
                .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(m, null);
            return m;
        }

        private MagicData Piece(params Vector2Int[] nodes)
        {
            MagicData m = ScriptableObject.CreateInstance<MagicData>();
            m.shapeNodes = new List<Vector2Int>(nodes);
            createdMagics.Add(m);
            return m;
        }

        private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        [Test]
        public void CanPlace_InsideBounds_True()
        {
            MagicGridManager g = NewGrid();
            MagicData single = Piece(V(0, 0));
            Assert.IsTrue(g.CanPlace(single, V(2, 2)));
        }

        [Test]
        public void CanPlace_EachCorner_True()
        {
            MagicGridManager g = NewGrid();
            MagicData single = Piece(V(0, 0));
            Assert.IsTrue(g.CanPlace(single, V(0, 0)));
            Assert.IsTrue(g.CanPlace(single, V(4, 0)));
            Assert.IsTrue(g.CanPlace(single, V(0, 4)));
            Assert.IsTrue(g.CanPlace(single, V(4, 4)));
        }

        [Test]
        public void CanPlace_OutOfBounds_False()
        {
            MagicGridManager g = NewGrid();
            MagicData single = Piece(V(0, 0));
            Assert.IsFalse(g.CanPlace(single, V(5, 0)));
            Assert.IsFalse(g.CanPlace(single, V(-1, 0)));
            Assert.IsFalse(g.CanPlace(single, V(0, 5)));
            Assert.IsFalse(g.CanPlace(single, V(0, -1)));
        }

        [Test]
        public void CanPlace_OverlapWithRegistered_False()
        {
            MagicGridManager g = NewGrid();
            MagicData domino = Piece(V(0, 0), V(0, 1));
            g.RegisterMagic(domino, V(0, 0)); // (0,0) と (0,1) を占有

            MagicData other = Piece(V(0, 0), V(0, 1));
            Assert.IsFalse(g.CanPlace(other, V(0, 1))); // (0,1),(0,2) を要求 → (0,1) が埋まっている
            Assert.IsTrue(g.CanPlace(other, V(0, 2)));  // (0,2),(0,3) → 空き
        }

        [Test]
        public void RegisterMagic_MarksEveryShapeCell_RemoveMagic_FreesThem()
        {
            MagicGridManager g = NewGrid();
            MagicData lShape = Piece(V(0, 0), V(1, 0), V(0, 1));
            g.RegisterMagic(lShape, V(1, 1)); // (1,1),(2,1),(1,2)

            MagicData single = Piece(V(0, 0));
            Assert.IsFalse(g.CanPlace(single, V(1, 1)));
            Assert.IsFalse(g.CanPlace(single, V(2, 1)));
            Assert.IsFalse(g.CanPlace(single, V(1, 2)));
            Assert.IsTrue(g.CanPlace(single, V(3, 3)));

            g.RemoveMagic(lShape);
            Assert.IsTrue(g.CanPlace(single, V(1, 1)));
            Assert.IsTrue(g.CanPlace(single, V(2, 1)));
            Assert.IsTrue(g.CanPlace(single, V(1, 2)));
        }

        [Test]
        public void GetPlacedMagics_DedupsMultiCellPieces()
        {
            MagicGridManager g = NewGrid();
            MagicData tri = Piece(V(0, 0), V(1, 0), V(2, 0));
            g.RegisterMagic(tri, V(0, 0));
            Assert.AreEqual(1, g.GetPlacedMagics().Count);

            MagicData domino = Piece(V(0, 0), V(0, 1));
            g.RegisterMagic(domino, V(0, 3));
            Assert.AreEqual(2, g.GetPlacedMagics().Count);
        }

        [Test]
        public void ClampToGrid_FarPositive_PushesShapeFullyInside()
        {
            MagicGridManager g = NewGrid();
            // minX=0,maxX=1,minY=-1,maxY=0
            MagicData piece = Piece(V(0, 0), V(1, 0), V(0, -1));

            Vector2Int clamped = g.ClampToGrid(piece, V(10, 10));
            // clampedX = Clamp(10, 0, 5-1-1=3) = 3 ; clampedY = Clamp(10, 1, 5-1-0=4) = 4
            Assert.AreEqual(V(3, 4), clamped);
            foreach (Vector2Int node in piece.shapeNodes)
            {
                Vector2Int cell = clamped + node;
                Assert.IsTrue(cell.x >= 0 && cell.x < g.width && cell.y >= 0 && cell.y < g.height,
                    $"cell {cell} out of bounds");
            }
        }

        [Test]
        public void ClampToGrid_FarNegative_RespectsNegativeNodeOffset()
        {
            MagicGridManager g = NewGrid();
            MagicData piece = Piece(V(0, 0), V(1, 0), V(0, -1));

            Vector2Int clamped = g.ClampToGrid(piece, V(-10, -10));
            // clampedX = Clamp(-10, 0, 3) = 0 ; clampedY = Clamp(-10, 1, 4) = 1（負オフセットぶん下限が上がる）
            Assert.AreEqual(V(0, 1), clamped);
        }
    }
}
