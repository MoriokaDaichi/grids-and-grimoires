using System;
using NUnit.Framework;

namespace GridsAndGrimoires.EditModeTests
{
    // HideoutCatalog（5設備の定義とレベル別効果）の構造検証。
    public class HideoutCatalogTests
    {
        [Test]
        public void FiveFacilities_AllKindsPresent()
        {
            Assert.AreEqual(5, HideoutCatalog.Facilities.Count);
            foreach (FacilityKind k in Enum.GetValues(typeof(FacilityKind)))
                Assert.IsNotNull(HideoutCatalog.Get(k), k + " が無い");
        }

        [Test]
        public void EveryFacility_HasNameBlurbAndThreeCostSteps()
        {
            foreach (FacilityDef d in HideoutCatalog.Facilities)
            {
                Assert.IsFalse(string.IsNullOrEmpty(d.name), d.kind + " に名前が無い");
                Assert.IsFalse(string.IsNullOrEmpty(d.blurb), d.kind + " に説明が無い");
                Assert.AreEqual(HideoutCatalog.MaxLevel, d.costByStep.Count, d.kind + " のコスト段数が3でない");
                foreach (var step in d.costByStep)
                    Assert.Greater(step.Count, 0, d.kind + " の建造/強化コストが空");
            }
        }

        [Test]
        public void MaxLevelIsThree()
        {
            Assert.AreEqual(3, HideoutCatalog.MaxLevel);
        }

        [Test]
        public void ResearchDesk_CostGoesDown_BonusGoesUp_WithLevel()
        {
            Assert.Greater(HideoutCatalog.ResearchCostMult(1), HideoutCatalog.ResearchCostMult(2));
            Assert.Greater(HideoutCatalog.ResearchCostMult(2), HideoutCatalog.ResearchCostMult(3));
            Assert.LessOrEqual(HideoutCatalog.ResearchCostMult(3), 1f);

            Assert.LessOrEqual(HideoutCatalog.ResearchBonusMult(1), HideoutCatalog.ResearchBonusMult(2));
            Assert.Less(HideoutCatalog.ResearchBonusMult(2), HideoutCatalog.ResearchBonusMult(3));

            // 未建造は等倍
            Assert.AreEqual(1f, HideoutCatalog.ResearchCostMult(0));
            Assert.AreEqual(1f, HideoutCatalog.ResearchBonusMult(0));
        }

        [Test]
        public void MagicCircle_WaitShrinks_RarityBonusGrows_WithLevel()
        {
            Assert.GreaterOrEqual(HideoutCatalog.CircleDurationMult(1), HideoutCatalog.CircleDurationMult(2));
            Assert.Greater(HideoutCatalog.CircleDurationMult(2), HideoutCatalog.CircleDurationMult(3));
            Assert.AreEqual(0, HideoutCatalog.CircleRarityBonus(1));
            Assert.Less(HideoutCatalog.CircleRarityBonus(1), HideoutCatalog.CircleRarityBonus(3));
        }

        [Test]
        public void Cauldron_EfficiencyGrows_WithLevel()
        {
            Assert.Less(HideoutCatalog.CauldronYieldMult(1), HideoutCatalog.CauldronYieldMult(2));
            Assert.Less(HideoutCatalog.CauldronYieldMult(2), HideoutCatalog.CauldronYieldMult(3));
            Assert.AreEqual(0f, HideoutCatalog.CauldronYieldMult(0));
        }

        [Test]
        public void Furnace_SlotsGrow_FuelPerActionShrinks_WithLevel()
        {
            Assert.AreEqual(0, HideoutCatalog.FurnaceSlots(0));
            Assert.AreEqual(2, HideoutCatalog.FurnaceSlots(1));
            Assert.Less(HideoutCatalog.FurnaceSlots(1), HideoutCatalog.FurnaceSlots(3));
            Assert.GreaterOrEqual(HideoutCatalog.FurnaceFuelPerAction(1), HideoutCatalog.FurnaceFuelPerAction(3));
            Assert.AreEqual(0, HideoutCatalog.FurnaceFuelPerAction(0));
        }

        [Test]
        public void Workbench_TierEqualsLevel()
        {
            Assert.AreEqual(0, HideoutCatalog.WorkbenchTier(0));
            Assert.AreEqual(3, HideoutCatalog.WorkbenchTier(3));
        }
    }
}
