using UnityEngine;

// 戦闘のダメージ計算式。ScriptableObjectやシーンに依存しない純粋関数として切り出し、
// EditModeテストで検証できるようにしている。
public static class BattleFormula
{
    // 会心（クリティカル）倍率
    public const float CritMultiplier = 1.5f;

    // 攻撃魔法1発のダメージ（属性倍率なし＝等倍）。
    public static int AttackDamage(int spellDamage, float effectiveAtk, float damagePercent, int enemyDef)
    {
        return AttackDamage(spellDamage, effectiveAtk, damagePercent, enemyDef, 1f);
    }

    // 攻撃魔法1発のダメージ。
    // rawDamage = (魔法固有ダメージ + 実効攻撃力) * (1 + ダメージ強化% / 100) * 属性倍率
    // 最終ダメージ = max(1, round(rawDamage) - 敵防御力)
    public static int AttackDamage(int spellDamage, float effectiveAtk, float damagePercent, int enemyDef, float attributeMultiplier)
    {
        float rawDamage = (spellDamage + effectiveAtk) * (1f + damagePercent / 100f) * attributeMultiplier;
        return Mathf.Max(1, Mathf.RoundToInt(rawDamage) - enemyDef);
    }

    // 敵の通常攻撃1発のダメージ。
    // 最終ダメージ = max(1, round(敵攻撃力 * 攻撃力倍率) - round(実効防御力))
    public static int EnemyAttackDamage(int enemyAtk, float atkMultiplier, float effectiveDef)
    {
        return Mathf.Max(1, Mathf.RoundToInt(enemyAtk * atkMultiplier) - Mathf.RoundToInt(effectiveDef));
    }

    // 素早さによる発動間隔の倍率。基準は spd=5 で 1.0。企画書に数値が無いため仮：
    // 5より1高いごとに -2%（下限0.5）、5より低いと遅くなる（上限1.5）。
    public static float SpdCastMultiplier(int spd)
    {
        return Mathf.Clamp(1f - (spd - 5) * 0.02f, 0.5f, 1.5f);
    }

    // 運による会心発生率(%)。基準は luc=5 で 0%。仮：5より1高いごとに +1%（上限50%）。
    public static int LucCritChance(int luc)
    {
        return Mathf.Clamp(luc - 5, 0, 50);
    }
}
