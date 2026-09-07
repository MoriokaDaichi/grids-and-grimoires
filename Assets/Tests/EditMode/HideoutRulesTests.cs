using System.Collections.Generic;
using NUnit.Framework;

namespace GridsAndGrimoires.EditModeTests
{
    public class HideoutRulesTests
    {
        private static MaterialCost Small(int n) { return new MaterialCost { materialType = MaterialType.SmallManaCrystal, amount = n }; }
        private static MaterialCost Part(string name, int n) { return new MaterialCost { materialType = MaterialType.SpecialItem, specialItemName = name, amount = n }; }

        [Test]
        public void NextCost_NullWhenMaxed()
        {
            FacilityDef d = HideoutCatalog.Get(FacilityKind.ResearchDesk);
            Assert.IsNotNull(HideoutRules.NextCost(d, 0));
            Assert.IsNotNull(HideoutRules.NextCost(d, 2));
            Assert.IsNull(HideoutRules.NextCost(d, 3));
            Assert.IsNull(HideoutRules.NextCost(d, -1));
        }

        [Test]
        public void CanAdvance_RequiresAffordAndNotMaxed()
        {
            FacilityDef d = HideoutCatalog.Get(FacilityKind.Workbench);
            Assert.IsTrue(HideoutRules.CanAdvance(d, 0, _ => true));
            Assert.IsFalse(HideoutRules.CanAdvance(d, 0, _ => false));
            Assert.IsFalse(HideoutRules.CanAdvance(d, 3, _ => true));
        }

        [Test]
        public void ScaleCost_CeilsAndFloorsAtOne()
        {
            List<MaterialCost> baseCost = new List<MaterialCost> { Small(10), Part("ゴブリンの牙", 3) };
            List<MaterialCost> scaled = HideoutRules.ScaleCost(baseCost, 0.6f);
            Assert.AreEqual(6, scaled[0].amount);          // ceil(6.0)
            Assert.AreEqual(2, scaled[1].amount);          // ceil(1.8) = 2
            Assert.AreEqual("ゴブリンの牙", scaled[1].specialItemName);

            // 極端に小さい倍率でも最低1
            Assert.AreEqual(1, HideoutRules.ScaleCost(new List<MaterialCost> { Small(3) }, 0.01f)[0].amount);
            // 等倍は不変
            Assert.AreEqual(10, HideoutRules.ScaleCost(baseCost, 1f)[0].amount);
        }

        [Test]
        public void RarityOf_MapsMaterialsSensibly()
        {
            Assert.AreEqual(ItemRarity.Common, HideoutRules.RarityOf(Small(1)));
            Assert.AreEqual(ItemRarity.Uncommon, HideoutRules.RarityOf(new MaterialCost { materialType = MaterialType.MediumManaCrystal, amount = 1 }));
            Assert.AreEqual(ItemRarity.Rare, HideoutRules.RarityOf(new MaterialCost { materialType = MaterialType.LargeManaCrystal, amount = 1 }));
            Assert.AreEqual(ItemRarity.Common, HideoutRules.RarityOf(Part("ゴブリンの牙", 1)));
            Assert.AreEqual(ItemRarity.Uncommon, HideoutRules.RarityOf(Part("古木の芯", 1)));
        }

        [Test]
        public void BrewHours_HigherRarityLonger_HigherLevelShorter()
        {
            Assert.Less(HideoutRules.BrewHours(ItemRarity.Common, 1), HideoutRules.BrewHours(ItemRarity.Rare, 1));
            Assert.Less(HideoutRules.BrewHours(ItemRarity.Rare, 1), HideoutRules.BrewHours(ItemRarity.Epic, 1));
            Assert.Greater(HideoutRules.BrewHours(ItemRarity.Rare, 1), HideoutRules.BrewHours(ItemRarity.Rare, 3));

            // Common Lv1 = 4時間
            Assert.AreEqual(4f, HideoutRules.BrewHours(ItemRarity.Common, 1), 0.001f);
        }

        [Test]
        public void BrewReady_And_Remaining_TimeMath()
        {
            double start = 1_000_000.0;
            float hours = 5f; // = 18000 秒
            Assert.IsFalse(HideoutRules.BrewReady(start, hours, start + 17999));
            Assert.IsTrue(HideoutRules.BrewReady(start, hours, start + 18000));
            Assert.AreEqual(1.0, HideoutRules.BrewRemainingSeconds(start, hours, start + 17999), 0.001);
            Assert.AreEqual(0.0, HideoutRules.BrewRemainingSeconds(start, hours, start + 999999));
        }

        [Test]
        public void RollRarity_StaysInRange_AndBonusRaisesAverage()
        {
            int sumNoBonus = 0, sumBonus = 0;
            for (int seed = 0; seed < 400; seed++)
            {
                ItemRarity a = HideoutRules.RollRarity(seed, ItemRarity.Common, 0);
                ItemRarity b = HideoutRules.RollRarity(seed, ItemRarity.Common, 2);
                Assert.GreaterOrEqual((int)a, 0);
                Assert.LessOrEqual((int)a, (int)ItemRarity.Epic);
                Assert.LessOrEqual((int)b, (int)ItemRarity.Epic);
                sumNoBonus += (int)a;
                sumBonus += (int)b;
            }
            Assert.Greater(sumBonus, sumNoBonus, "机ボーナスで平均レア度が上がるべき");
        }

        [Test]
        public void Transmute_YieldScalesWithMultiplier_AndRoutesByPart()
        {
            List<MaterialCost> lo = HideoutRules.Transmute(Part("ゴブリンの牙", 3), 3, 1f);
            Assert.AreEqual(MaterialType.SmallManaCrystal, lo[0].materialType);
            Assert.AreEqual(6, lo[0].amount); // 2 × 3

            List<MaterialCost> hi = HideoutRules.Transmute(Part("ゴブリンの牙", 3), 3, 1.9f);
            Assert.Greater(hi[0].amount, lo[0].amount);

            List<MaterialCost> bark = HideoutRules.Transmute(Part("番人の樹皮", 1), 2, 1f);
            Assert.AreEqual(MaterialType.MediumManaCrystal, bark[0].materialType);

            List<MaterialCost> core = HideoutRules.Transmute(Part("古木の芯", 1), 1, 1f);
            Assert.AreEqual(MaterialType.ElementFragment, core[0].materialType);
            Assert.AreEqual(MagicAttribute.Wind, core[0].attribute);

            // 非モンスター素材は変換不可
            Assert.AreEqual(0, HideoutRules.Transmute(Small(5), 1, 1f).Count);
        }

        [Test]
        public void CircleReward_NonNull_AmountPositive_ValueTrendsUpWithRarity()
        {
            long commonVal = 0, epicVal = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                MaterialCost c = HideoutRules.CircleReward(ItemRarity.Common, seed);
                MaterialCost e = HideoutRules.CircleReward(ItemRarity.Epic, seed);
                Assert.IsNotNull(c);
                Assert.Greater(c.amount, 0);
                Assert.Greater(e.amount, 0);
                commonVal += MaterialCatalog.Value(c.materialType) * c.amount + 1; // +1 で結晶以外(価値0)も比較可能に
                epicVal += MaterialCatalog.Value(e.materialType) * e.amount + 1;
            }
            Assert.Greater(epicVal, commonVal, "高レアほど報酬の価値が高いはず");
        }

        [Test]
        public void Furnace_CapacityAndPowerGate()
        {
            Assert.AreEqual(0, HideoutRules.FurnaceCapacity(0));
            Assert.AreEqual(200, HideoutRules.FurnaceCapacity(1)); // 2 スロット × 100
            Assert.Less(HideoutRules.FurnaceCapacity(1), HideoutRules.FurnaceCapacity(3));

            Assert.IsFalse(HideoutRules.CanPowerAction(0, 999));  // 未建造
            Assert.IsFalse(HideoutRules.CanPowerAction(1, 1));    // 燃料不足（Lv1 は 2 必要）
            Assert.IsTrue(HideoutRules.CanPowerAction(1, 2));
        }
    }
}
