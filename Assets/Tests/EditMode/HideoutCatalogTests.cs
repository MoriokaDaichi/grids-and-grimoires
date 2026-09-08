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
        public void Cauldron_TierGate_OpensWithLevel()
        {
            Assert.AreEqual(0, HideoutCatalog.CauldronMaxTier(0)); // 未建造
            Assert.AreEqual(1, HideoutCatalog.CauldronMaxTier(1)); // Lv1 → tier1
            Assert.AreEqual(3, HideoutCatalog.CauldronMaxTier(2)); // Lv2 → tier1〜3
            Assert.AreEqual(5, HideoutCatalog.CauldronMaxTier(3)); // Lv3 → tier1〜5

            Assert.AreEqual(1, HideoutCatalog.CauldronLevelForTier(1));
            Assert.AreEqual(2, HideoutCatalog.CauldronLevelForTier(2));
            Assert.AreEqual(2, HideoutCatalog.CauldronLevelForTier(3));
            Assert.AreEqual(3, HideoutCatalog.CauldronLevelForTier(4));
            Assert.AreEqual(3, HideoutCatalog.CauldronLevelForTier(5));
        }

        [Test]
        public void Furnace_SlotsGrow_FuelPerActionStaysLean_WithLevel()
        {
            Assert.AreEqual(0, HideoutCatalog.FurnaceSlots(0));
            Assert.AreEqual(2, HideoutCatalog.FurnaceSlots(1));
            Assert.Less(HideoutCatalog.FurnaceSlots(1), HideoutCatalog.FurnaceSlots(3));

            // 再検証3 D1：Lv1 の燃費を 2→1 に下げ、Lv1 錬金釜でも tier1 変換が純増になるようにした。
            // 建造済みレベルはどれも 1 燃料/アクション（強化のメリットはスロット＝バッファ上限に寄せる）。
            Assert.AreEqual(0, HideoutCatalog.FurnaceFuelPerAction(0));
            Assert.AreEqual(1, HideoutCatalog.FurnaceFuelPerAction(1));
            Assert.GreaterOrEqual(HideoutCatalog.FurnaceFuelPerAction(1), HideoutCatalog.FurnaceFuelPerAction(3));

            // Lv1 錬金釜（yieldMult=1.0）× Lv1 魔力炉 で tier1 素材変換が純増になる。
            var outp = HideoutRules.Transmute(
                new MaterialCost { materialType = MaterialType.SpecialItem, specialItemName = "スライムゼリー", amount = 1 },
                1, HideoutCatalog.CauldronYieldMult(1));
            Assert.AreEqual(MaterialType.SmallManaCrystal, outp[0].materialType);
            Assert.Greater(outp[0].amount, HideoutCatalog.FurnaceFuelPerAction(1),
                "Lv1 錬金釜×Lv1 魔力炉 で tier1 変換が燃料中立以下（cold-start の詰みが残る）");
        }

        [Test]
        public void Workbench_TierEqualsLevel()
        {
            Assert.AreEqual(0, HideoutCatalog.WorkbenchTier(0));
            Assert.AreEqual(3, HideoutCatalog.WorkbenchTier(3));
        }

        [Test]
        public void EverySpecialItemInCosts_IsKnownMonsterPart()
        {
            foreach (FacilityDef d in HideoutCatalog.Facilities)
                foreach (var step in d.costByStep)
                    foreach (MaterialCost c in step)
                        if (c != null && c.materialType == MaterialType.SpecialItem)
                            Assert.IsTrue(MonsterPartCatalog.IsKnown(c.specialItemName),
                                d.kind + " のコストに未登録の固有アイテム「" + c.specialItemName + "」");
        }

        [Test]
        public void Lv2BuildSteps_UseSmallMonsterPartStacks()
        {
            // 再検証2 R2/R3：Lv2 設備の連鎖ゲートを緩めるため、Lv2(step1) のモンスター素材は
            // 必要個数 ≤ 2（farm 1回で賄える量）に抑える。深層 debut の素材で足踏みさせない。
            foreach (FacilityDef d in HideoutCatalog.Facilities)
            {
                foreach (MaterialCost c in d.costByStep[1])
                {
                    if (c == null || c.materialType != MaterialType.SpecialItem) continue;
                    Assert.LessOrEqual(c.amount, 2,
                        d.kind + " Lv2 のコスト「" + c.specialItemName + "」×" + c.amount + " が多すぎる（Lv2 は ≤2）");
                }
            }
        }

        [Test]
        public void FurnaceBuildBonusFuel_IsPositive_AndFitsLv1Buffer()
        {
            // 再検証5 R2：魔力炉初建造時の初期燃料。cold-start の燃料デッドロックを破れる量で、
            // かつ Lv1 の燃料バッファ（FurnaceSlots(1) × 大結晶価値 = 200）に収まること。
            Assert.Greater(HideoutCatalog.FurnaceBuildBonusFuel, HideoutCatalog.FurnaceFuelPerAction(1),
                "1アクション分すら無いと意味がない");
            long lv1Buffer = HideoutCatalog.FurnaceSlots(1) * (long)MaterialCatalog.Value(MaterialType.LargeManaCrystal);
            Assert.LessOrEqual(HideoutCatalog.FurnaceBuildBonusFuel, lv1Buffer);
        }

        [Test]
        public void AlchemyCauldronLv1_IsPayableInMediumCrystals_ForColdStart()
        {
            // 再検証3 D1／改善ループ通しプレイ：cold-start の結晶収入はタスク報酬の中結晶。
            // 錬金釜（素材→結晶のエンジン）が小結晶ゲートで建たないと貪欲プレイは詰む。
            // Lv1(step0) は小結晶を要求せず、中結晶で賄えること。
            var step0 = HideoutCatalog.Get(FacilityKind.AlchemyCauldron).costByStep[0];
            bool hasMedium = false;
            foreach (MaterialCost c in step0)
            {
                if (c == null) continue;
                Assert.AreNotEqual(MaterialType.SmallManaCrystal, c.materialType,
                    "錬金釜Lv1 がまだ小結晶を要求している（cold-start の D1 詰みが残る）");
                if (c.materialType == MaterialType.MediumManaCrystal) hasMedium = true;
            }
            Assert.IsTrue(hasMedium, "錬金釜Lv1 に中結晶コストが無い");
        }

        [Test]
        public void AlchemyCauldronLv2_DoesNotRequireDeepDebutPart()
        {
            // 『古木の芯』は森の番人（debut 深度19）ドロップ。深度16 前後で詰まるプレイヤーが
            // 錬金釜Lv2 を建てられず中結晶／tier2 素材の出口が両方閉じるデッドロックの元だった。
            foreach (MaterialCost c in HideoutCatalog.Get(FacilityKind.AlchemyCauldron).costByStep[1])
                if (c != null && c.materialType == MaterialType.SpecialItem)
                    Assert.AreNotEqual("古木の芯", c.specialItemName,
                        "錬金釜Lv2 が深層 debut の『古木の芯』を要求している（再検証2 R2）");
        }

        [Test]
        public void AlchemyCauldronLv2_MediumCrystalCost_StaysModest()
        {
            // 再検証3 D3：中結晶の中盤 faucet が無いので Lv2 の中結晶要求は farm＋わずかな両替で
            // 賄える量（≤3）に抑える。
            foreach (MaterialCost c in HideoutCatalog.Get(FacilityKind.AlchemyCauldron).costByStep[1])
                if (c != null && c.materialType == MaterialType.MediumManaCrystal)
                    Assert.LessOrEqual(c.amount, 3,
                        "錬金釜Lv2 の中結晶 ×" + c.amount + " が多すぎる（再検証3 D3：≤3）");
        }

        [Test]
        public void WorkbenchLv2_DoesNotRequireBurstBandDebutPart()
        {
            // 再検証3 D7：作業台Lv2 は tier2 杖＝5×5 グリッドのゲート。建材が『竜人の鱗』
            // （リザードマン debut 深度13＝バースト即死帯）だと 5×5 に届かないまま詰む。
            foreach (MaterialCost c in HideoutCatalog.Get(FacilityKind.Workbench).costByStep[1])
                if (c != null && c.materialType == MaterialType.SpecialItem)
                    Assert.AreNotEqual("竜人の鱗", c.specialItemName,
                        "作業台Lv2 が深度13帯 debut の『竜人の鱗』を要求している（再検証3 D7）");
        }

        [Test]
        public void BuildCosts_StayWithinDepthBudget_PerStep()
        {
            // Lv1(step0)=tier1のみ / Lv2(step1)=tier1〜2 / Lv3(step2)=上限なし（深層素材OK）。
            int[] maxTierByStep = { 1, 2, 5 };
            foreach (FacilityDef d in HideoutCatalog.Facilities)
            {
                for (int step = 0; step < d.costByStep.Count; step++)
                {
                    int maxTier = step < maxTierByStep.Length ? maxTierByStep[step] : 5;
                    foreach (MaterialCost c in d.costByStep[step])
                    {
                        if (c == null || c.materialType != MaterialType.SpecialItem) continue;
                        int tier = MonsterPartCatalog.TierOf(c.specialItemName);
                        Assert.LessOrEqual(tier, maxTier,
                            d.kind + " Lv" + (step + 1) + " のコスト「" + c.specialItemName + "」が tier" + tier + "（上限 tier" + maxTier + "）");
                    }
                }
            }
        }
    }
}
