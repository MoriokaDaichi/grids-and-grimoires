using UnityEngine;

// マナ（魔力）の消費・回復ルール。企画書に数値の記載が無いため全て仮バランス。
// ScriptableObject/シーンに依存しない純粋関数として切り出し、EditModeテストで検証できるようにしている。
public static class ManaRules
{
    // プレイヤーの基準最大マナ（仮）
    public const int BaseMaxMana = 100;

    // 毎秒のマナ自然回復量（仮）
    public const float DefaultRegenPerSecond = 8f;

    // マナ不足で発動を見送ったときの再試行間隔（秒）
    public const float StarvedRetryDelay = 0.2f;

    // 魔法1回の発動に必要なマナ（仮）。category / range / damage から算出する。
    // ・単体攻撃: 8 + ceil(damage * 0.5)
    // ・全体攻撃(AoE): 18 + damage（範囲攻撃は割高）
    // ・状態異常専用: 12
    // ・補助(アッド/デュアル): 20
    // ・アクティブバフ: 25
    // ・パッシブ: 0（常時効果なので消費しない）
    public static int CastCost(MagicData data)
    {
        if (data == null) return 0;

        // 生成時に確定値が書き込まれていればそれを使う（0 のときだけ自動算出）
        if (data.manaCost > 0) return data.manaCost;

        switch (data.category)
        {
            case MagicCategory.Attack:
                return data.range == MagicRange.AoE
                    ? 18 + Mathf.Max(0, data.damage)
                    : 8 + Mathf.CeilToInt(Mathf.Max(0, data.damage) * 0.5f);
            case MagicCategory.StatusInflict:
                return 12;
            case MagicCategory.Support:
                return 20;
            case MagicCategory.BuffActive:
                return 25;
            default:
                return 0;
        }
    }

    // deltaTime 秒ぶんのマナ回復量。
    public static float RegenAmount(float perSecond, float deltaTime)
    {
        if (perSecond <= 0f || deltaTime <= 0f) return 0f;
        return perSecond * deltaTime;
    }
}
