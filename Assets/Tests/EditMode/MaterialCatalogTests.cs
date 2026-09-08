using NUnit.Framework;

namespace GridsAndGrimoires.EditModeTests
{
    public class MaterialCatalogTests
    {
        private static MaterialCost Part(string name, int amount)
        {
            return new MaterialCost { materialType = MaterialType.SpecialItem, specialItemName = name, amount = amount };
        }

        [Test]
        public void GoldValue_Crystals_MatchCanonicalWorth()
        {
            Assert.AreEqual(1, MaterialCatalog.GoldValue(new MaterialCost { materialType = MaterialType.SmallManaCrystal, amount = 1 }));
            Assert.AreEqual(10, MaterialCatalog.GoldValue(new MaterialCost { materialType = MaterialType.MediumManaCrystal, amount = 3 })); // 個数非依存の単価
            Assert.AreEqual(100, MaterialCatalog.GoldValue(new MaterialCost { materialType = MaterialType.LargeManaCrystal, amount = 1 }));
        }

        [Test]
        public void GoldValue_SpecialItems_ScaleWithTier()
        {
            int t1 = MaterialCatalog.GoldValue(Part("スライムゼリー", 1)); // tier1
            int t3 = MaterialCatalog.GoldValue(Part("オーガの牙", 1));     // tier3
            int t5 = MaterialCatalog.GoldValue(Part("混沌の核", 1));       // tier5
            Assert.Greater(t1, 0);
            Assert.Greater(t3, t1);
            Assert.Greater(t5, t3);
        }

        [Test]
        public void GoldValue_UnknownSpecialItem_FallsBackToTier1()
        {
            Assert.AreEqual(MaterialCatalog.GoldValue(Part("スライムゼリー", 1)),
                            MaterialCatalog.GoldValue(Part("架空の素材", 1)));
        }
    }
}
