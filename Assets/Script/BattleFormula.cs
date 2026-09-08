using UnityEngine;

// 戦闘のダメージ計算式。ScriptableObjectやシーンに依存しない純粋関数として切り出し、
// EditModeテストで検証できるようにしている。
public static class BattleFormula
{
    // 会心（クリティカル）倍率
    public const float CritMultiplier = 1.5f;

    // 1ウェーブでプレイヤーが失える最大HPの割合（＝バースト即死のクランプ、レポート C2/D5）。
    // ウェーブ間でしか「脱出」を選べないので、満タン近くから1ウェーブで即死すると脱出判断が働かない。
    // このぶんを超える被弾は無効化する（数値は仮）。
    // 再検証4 R1: 0.6 だと「常に 40% 残る」＋自然回復でフェイルステートが消えたので 0.85 に。
    // さらに WaveClampMinStartFraction 未満で始まったウェーブには効かせない（延命バフ化を防ぐ）。
    public const float WaveDamageCapFraction = 0.85f;
    public const float WaveClampMinStartFraction = 0.55f;

    // 1ウェーブでサステインの「毎秒回復」で戻せる最大HPの割合（再検証7 R4：深部の長いウェーブで
    // 毎秒回復が被弾を上回り続けてフェイルステートが消えるのを防ぐ。ウェーブ突破時の回復は別枠）。
    public const float WaveHealCapFraction = 0.5f;

    public static int WaveHealCap(int maxHp)
    {
        return Mathf.Max(1, Mathf.RoundToInt(maxHp * WaveHealCapFraction));
    }

    // 1ウェーブで許容する累計被ダメージ（最大HP基準）。
    public static int WaveDamageCap(int maxHp)
    {
        return Mathf.Max(1, Mathf.RoundToInt(maxHp * WaveDamageCapFraction));
    }

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
