using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GridsAndGrimoires.EditModeTests
{
    // BattleManager.ComputePassiveBonuses から移設したパッシブ集計ロジックを固定する。
    // 3種のパッシブは「どのフィールドが既定値でないか」で判別され、buffStat != None が最優先。
    public class PassiveBonusCalculatorTests
    {
        // BattleManager が使っている仮の係数
        private const float StatPercentPerStage = 10f;
        private const float AttrDamagePercent = 15f;
        private const int StatusRateBonus = 15;

        private readonly List<MagicData> created = new List<MagicData>();

        [TearDown]
        public void TearDown()
        {
            foreach (MagicData m in created)
            {
                if (m != null) Object.DestroyImmediate(m);
            }
            created.Clear();
        }

        private MagicData Passive(BuffStat buffStat, int stage, MagicAttribute attr, StatusEffectType status)
        {
            MagicData m = ScriptableObject.CreateInstance<MagicData>();
            m.category = MagicCategory.BuffPassive;
            m.buffStat = buffStat;
            m.passiveStage = stage;
            m.attribute = attr;
            m.statusEffect = status;
            created.Add(m);
            return m;
        }

        private PassiveBonuses Aggregate(params MagicData[] magics)
        {
            return PassiveBonusCalculator.Aggregate(magics, StatPercentPerStage, AttrDamagePercent, StatusRateBonus);
        }

        [Test]
        public void StatPassives_StackByStageTimesPerStage()
        {
            PassiveBonuses b = Aggregate(
                Passive(BuffStat.Atk, 2, MagicAttribute.None, StatusEffectType.None),
                Passive(BuffStat.Atk, 2, MagicAttribute.None, StatusEffectType.None));

            Assert.AreEqual(40f, b.StatPercent[BuffStat.Atk]);
            Assert.AreEqual(0, b.AttrDamagePercent.Count);
            Assert.AreEqual(0, b.StatusRateBonus.Count);
        }

        [Test]
        public void AttributeBuff_NoStatusNoStat_CountsAsAttrDamage()
        {
            PassiveBonuses b = Aggregate(
                Passive(BuffStat.None, 1, MagicAttribute.Fire, StatusEffectType.None));

            Assert.AreEqual(15f, b.AttrDamagePercent[MagicAttribute.Fire]);
            Assert.AreEqual(0, b.StatPercent.Count);
            Assert.AreEqual(0, b.StatusRateBonus.Count);
        }

        [Test]
        public void StatusRateBuff_AttributePlusStatus_CountsAsStatusRate()
        {
            PassiveBonuses b = Aggregate(
                Passive(BuffStat.None, 1, MagicAttribute.Fire, StatusEffectType.Burn));

            Assert.AreEqual(15, b.StatusRateBonus[MagicAttribute.Fire]);
            Assert.AreEqual(0, b.StatPercent.Count);
            Assert.AreEqual(0, b.AttrDamagePercent.Count);
        }

        [Test]
        public void BuffStatWins_OverAttributeClassification()
        {
            // buffStat と attribute の両方がセットされていても stat 扱い（else if の優先順）
            PassiveBonuses b = Aggregate(
                Passive(BuffStat.Atk, 1, MagicAttribute.Fire, StatusEffectType.None));

            Assert.AreEqual(10f, b.StatPercent[BuffStat.Atk]);
            Assert.AreEqual(0, b.AttrDamagePercent.Count);
        }

        [Test]
        public void NonPassiveCategory_Ignored()
        {
            MagicData attack = ScriptableObject.CreateInstance<MagicData>();
            attack.category = MagicCategory.Attack;
            attack.buffStat = BuffStat.Atk;
            attack.passiveStage = 3;
            created.Add(attack);

            PassiveBonuses b = Aggregate(attack);

            Assert.AreEqual(0, b.StatPercent.Count);
            Assert.AreEqual(0, b.AttrDamagePercent.Count);
            Assert.AreEqual(0, b.StatusRateBonus.Count);
        }

        [Test]
        public void Empty_AllDictionariesEmpty()
        {
            PassiveBonuses b = Aggregate();

            Assert.AreEqual(0, b.StatPercent.Count);
            Assert.AreEqual(0, b.AttrDamagePercent.Count);
            Assert.AreEqual(0, b.StatusRateBonus.Count);
        }
    }
}
