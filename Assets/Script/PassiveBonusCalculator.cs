using System.Collections.Generic;

// グリッドに配置中の BuffPassive 魔法を集計する処理を BattleManager から切り出したもの。
// 3種のパッシブ（対応ステータス強化 / 属性威力強化 / 状態異常付与率強化）を、
// どのフィールドが既定値でないかで判別する（buffStat != None が最優先）。
// MonoBehaviour / シーンに依存しないので EditModeテストで検証できる。

public struct PassiveBonuses
{
    public Dictionary<BuffStat, float> StatPercent;          // ステータス種別 → 強化%
    public Dictionary<MagicAttribute, float> AttrDamagePercent; // 属性 → その属性の魔法威力強化%
    public Dictionary<MagicAttribute, int> StatusRateBonus;  // 属性 → その属性の状態異常付与率ボーナスpt
}

public static class PassiveBonusCalculator
{
    public static PassiveBonuses Aggregate(
        IEnumerable<MagicData> placedMagics,
        float statPercentPerStage,
        float attrDamagePercent,
        int statusRateBonus)
    {
        var statPercent = new Dictionary<BuffStat, float>();
        var attrDamage = new Dictionary<MagicAttribute, float>();
        var statusRate = new Dictionary<MagicAttribute, int>();

        foreach (MagicData data in placedMagics)
        {
            if (data.category != MagicCategory.BuffPassive) continue;

            if (data.buffStat != BuffStat.None)
            {
                float percent = data.passiveStage * statPercentPerStage;
                statPercent[data.buffStat] = GetOrZero(statPercent, data.buffStat) + percent;
            }
            else if (data.attribute != MagicAttribute.None && data.statusEffect == StatusEffectType.None)
            {
                // 属性バフ
                attrDamage[data.attribute] = GetOrZero(attrDamage, data.attribute) + attrDamagePercent;
            }
            else if (data.attribute != MagicAttribute.None && data.statusEffect != StatusEffectType.None)
            {
                // 状態異常付与率バフ
                statusRate[data.attribute] = GetOrZeroInt(statusRate, data.attribute) + statusRateBonus;
            }
        }

        return new PassiveBonuses
        {
            StatPercent = statPercent,
            AttrDamagePercent = attrDamage,
            StatusRateBonus = statusRate,
        };
    }

    private static float GetOrZero<TKey>(Dictionary<TKey, float> dict, TKey key)
    {
        return dict.TryGetValue(key, out float v) ? v : 0f;
    }

    private static int GetOrZeroInt<TKey>(Dictionary<TKey, int> dict, TKey key)
    {
        return dict.TryGetValue(key, out int v) ? v : 0;
    }
}
