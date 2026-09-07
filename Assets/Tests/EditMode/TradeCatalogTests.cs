using System.Collections.Generic;
using NUnit.Framework;

namespace GridsAndGrimoires.EditModeTests
{
    public class TradeCatalogTests
    {
        [Test]
        public void StandardOffers_HasExpectedCount()
        {
            // 結晶変換4 + 欠片→エレメント5 + ステータスポイント1
            Assert.AreEqual(10, TradeCatalog.StandardOffers().Count);
        }

        [Test]
        public void CrystalSwaps_AreValuePreserving()
        {
            List<TradeOffer> offers = TradeCatalog.StandardOffers();
            for (int i = 0; i < 4; i++)
            {
                int give = TradeCatalog.Value(offers[i].give);
                int receive = TradeCatalog.Value(offers[i].receive);
                Assert.AreEqual(give, receive, "offer[" + i + "] '" + offers[i].label + "' の価値が保存されていない");
                Assert.Greater(give, 0);
            }
        }

        [Test]
        public void FragmentOffers_ThreeFragmentsToOneElement()
        {
            List<TradeOffer> offers = TradeCatalog.StandardOffers();
            int fragOffers = 0;
            foreach (TradeOffer o in offers)
            {
                if (o.give.Count == 1 && o.give[0].materialType == MaterialType.ElementFragment)
                {
                    fragOffers++;
                    Assert.AreEqual(3, o.give[0].amount);
                    Assert.AreEqual(1, o.receive.Count);
                    Assert.AreEqual(MaterialType.Element, o.receive[0].materialType);
                    Assert.AreEqual(o.give[0].attribute, o.receive[0].attribute);
                }
            }
            Assert.AreEqual(5, fragOffers);
        }

        [Test]
        public void StatPointOffer_CostsLargeCrystal()
        {
            List<TradeOffer> offers = TradeCatalog.StandardOffers();
            TradeOffer stat = offers.Find(o => o.bonusStatPoints > 0);
            Assert.IsNotNull(stat);
            Assert.AreEqual(1, stat.bonusStatPoints);
            Assert.AreEqual(1, stat.give.Count);
            Assert.AreEqual(MaterialType.LargeManaCrystal, stat.give[0].materialType);
        }
    }
}
