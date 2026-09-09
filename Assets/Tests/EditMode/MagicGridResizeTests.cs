using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace GridsAndGrimoires.EditModeTests
{
    // 杖の tier に応じたグリッドのリサイズ（MagicGridManager.WandTierToSize / SetGridSize）を検証する。
    // 見た目（RectTransform / GridLayoutGroup / セル背景）はシーン依存なので、ここでは
    // 論理サイズと配置判定のみを対象にする。
    public class MagicGridResizeTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<MagicData> createdMagics = new List<MagicData>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
            foreach (MagicData m in createdMagics) if (m != null) Object.DestroyImmediate(m);
            createdMagics.Clear();
        }

        private MagicGridManager NewGrid()
        {
            GameObject go = new GameObject("grid");
            spawned.Add(go);
            MagicGridManager m = go.AddComponent<MagicGridManager>();
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
        public void WandTierToSize_MapsTierToSideLength()
        {
            MagicGridManager g = NewGrid();
            Assert.AreEqual(3, g.WandTierToSize(0)); // 未製作＝3×3（基本＋1枚は置ける）
            Assert.AreEqual(4, g.WandTierToSize(1)); // 作業台Lv1の杖＝4×4（Mega＋AoE＋単体が両立）
            Assert.AreEqual(5, g.WandTierToSize(2)); // 作業台Lv2の杖＝5×5
            Assert.AreEqual(6, g.WandTierToSize(3)); // 作業台Lv3の杖＝6×6（深部設計 D：AoE throughput）
            Assert.AreEqual(6, g.WandTierToSize(9)); // 上限クランプ
        }

        [Test]
        public void SetGridSize_ShrinksLogicalBounds()
        {
            MagicGridManager g = NewGrid();
            g.SetGridSize(2, 2);
            Assert.AreEqual(2, g.width);
            Assert.AreEqual(2, g.height);

            MagicData single = Piece(V(0, 0));
            Assert.IsTrue(g.CanPlace(single, V(1, 1)));
            Assert.IsFalse(g.CanPlace(single, V(2, 0)));
            Assert.IsFalse(g.CanPlace(single, V(0, 2)));
        }

        [Test]
        public void SetGridSize_GrowsLogicalBounds()
        {
            MagicGridManager g = NewGrid();
            g.SetGridSize(2, 2);
            g.SetGridSize(5, 5);
            Assert.AreEqual(5, g.width);
            Assert.AreEqual(5, g.height);

            MagicData single = Piece(V(0, 0));
            Assert.IsTrue(g.CanPlace(single, V(4, 4)));
        }

        [Test]
        public void SetGridSize_ClearsPlacedPieces()
        {
            MagicGridManager g = NewGrid();
            MagicData domino = Piece(V(0, 0), V(0, 1));
            g.RegisterMagic(domino, V(0, 0));
            Assert.AreEqual(1, g.GetPlacedMagics().Count);

            g.SetGridSize(3, 3); // サイズ変更で盤面はリセットされる
            Assert.AreEqual(0, g.GetPlacedMagics().Count);
        }

        [Test]
        public void SetGridSize_SameSize_KeepsPlacedPieces()
        {
            MagicGridManager g = NewGrid();
            g.SetGridSize(3, 3);
            MagicData single = Piece(V(0, 0));
            g.RegisterMagic(single, V(1, 1));

            g.SetGridSize(3, 3); // 同じサイズならリセットしない
            Assert.AreEqual(1, g.GetPlacedMagics().Count);
        }
    }
}
