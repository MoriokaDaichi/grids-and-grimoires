using NUnit.Framework;

namespace GridsAndGrimoires.EditModeTests
{
    // BattleFormula の数式が BattleManager から移設した内容と一致することを固定する。
    // Mathf.RoundToInt は「偶数丸め（banker's rounding）」である点も明示的に記録しておく。
    public class BattleFormulaTests
    {
        [Test]
        public void AttackDamage_NoBuff_SubtractsEnemyDef()
        {
            // raw = (10 + 5) * 1.0 = 15 → 15 - 2 = 13
            Assert.AreEqual(13, BattleFormula.AttackDamage(10, 5f, 0f, 2));
        }

        [Test]
        public void AttackDamage_WithDamagePercent_AppliesBeforeDefSubtraction()
        {
            // raw = (10 + 5) * 1.25 = 18.75 → RoundToInt = 19 → 19 - 2 = 17
            Assert.AreEqual(17, BattleFormula.AttackDamage(10, 5f, 25f, 2));
        }

        [Test]
        public void AttackDamage_DefExceedsRaw_ClampsToOne()
        {
            Assert.AreEqual(1, BattleFormula.AttackDamage(1, 0f, 0f, 100));
        }

        [Test]
        public void AttackDamage_HalfValues_UseBankersRounding()
        {
            // raw = 2.5 → RoundToInt(2.5) = 2（最近接偶数）
            Assert.AreEqual(2, BattleFormula.AttackDamage(0, 2.5f, 0f, 0));
            // raw = 3.5 → RoundToInt(3.5) = 4（最近接偶数）
            Assert.AreEqual(4, BattleFormula.AttackDamage(0, 3.5f, 0f, 0));
        }

        [Test]
        public void EnemyAttackDamage_DefEqualsAtk_ClampsToOne()
        {
            Assert.AreEqual(1, BattleFormula.EnemyAttackDamage(6, 1f, 5f));
        }

        [Test]
        public void EnemyAttackDamage_Basic()
        {
            Assert.AreEqual(9, BattleFormula.EnemyAttackDamage(14, 1f, 5f));
        }

        [Test]
        public void EnemyAttackDamage_BlindHalvesAtk_ThenClamps()
        {
            // RoundToInt(6 * 0.5) - RoundToInt(5) = 3 - 5 = -2 → 1
            Assert.AreEqual(1, BattleFormula.EnemyAttackDamage(6, 0.5f, 5f));
        }

        [Test]
        public void EnemyAttackDamage_RoundsEachOperandIndependently()
        {
            // RoundToInt(7 * 0.5 = 3.5) - RoundToInt(1.5) = 4 - 2 = 2
            Assert.AreEqual(2, BattleFormula.EnemyAttackDamage(7, 0.5f, 1.5f));
        }
    }
}
