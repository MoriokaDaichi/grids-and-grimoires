using UnityEngine;

// 戦闘のダメージ計算式。ScriptableObjectやシーンに依存しない純粋関数として切り出し、
// EditModeテストで検証できるようにしている。数式は BattleManager から逐語で移設したもの。
public static class BattleFormula
{
    // 攻撃魔法1発のダメージ。
    // rawDamage = (魔法固有ダメージ + 実効攻撃力) * (1 + ダメージ強化% / 100)
    // 最終ダメージ = max(1, round(rawDamage) - 敵防御力)
    public static int AttackDamage(int spellDamage, float effectiveAtk, float damagePercent, int enemyDef)
    {
        float rawDamage = (spellDamage + effectiveAtk) * (1f + damagePercent / 100f);
        return Mathf.Max(1, Mathf.RoundToInt(rawDamage) - enemyDef);
    }

    // 敵の通常攻撃1発のダメージ。
    // 最終ダメージ = max(1, round(敵攻撃力 * 攻撃力倍率) - round(実効防御力))
    public static int EnemyAttackDamage(int enemyAtk, float atkMultiplier, float effectiveDef)
    {
        return Mathf.Max(1, Mathf.RoundToInt(enemyAtk * atkMultiplier) - Mathf.RoundToInt(effectiveDef));
    }
}
