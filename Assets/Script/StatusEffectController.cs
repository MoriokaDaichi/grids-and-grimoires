using System.Collections.Generic;

// 敵にかかっている状態異常のタイマー処理を EnemyStatus から切り出したもの。
// MonoBehaviour / シーンに依存せず、ダメージ適用やログ出力も行わない
// （「このtickで何が起きたか」をデータで返すだけ）。EditModeテストで検証できるようにするため。
//
// 効果内容は企画書に数値の記載が無いため仮決め（5種で役割が被らないようにしている）。
// 火傷/裂傷=継続ダメージ、感電=スタン（反撃が発動しない）、眩暈=行動間隔延長、盲目=攻撃力ダウン
public class StatusEffectController
{
    private const float BurnDuration = 5f;
    private const float BurnTickInterval = 1f;
    private const int BurnTickDamage = 3;

    private const float LacerationDuration = 8f;
    private const float LacerationTickInterval = 1f;
    private const int LacerationTickDamage = 2;

    private const float ShockDuration = 3f;
    private const float DizzyDuration = 5f;
    private const float DizzyAttackIntervalMultiplier = 1.5f;
    private const float BlindDuration = 5f;
    private const float BlindAtkMultiplier = 0.5f;

    private class ActiveEffect
    {
        public StatusEffectType type;
        public float remaining;
        public float tickTimer;
    }

    // Tick() が返す「この経過ぶんで発生した継続ダメージ」1件
    public struct StatusTickResult
    {
        public StatusEffectType Type;
        public int Damage;
    }

    private readonly List<ActiveEffect> activeEffects = new List<ActiveEffect>();

    public bool IsStunned { get { return HasEffect(StatusEffectType.Shock); } }
    public float AttackIntervalMultiplier { get { return HasEffect(StatusEffectType.Dizzy) ? DizzyAttackIntervalMultiplier : 1f; } }
    public float AtkMultiplier { get { return HasEffect(StatusEffectType.Blind) ? BlindAtkMultiplier : 1f; } }

    public bool HasAny { get { return activeEffects.Count > 0; } }

    // 戦闘（敵）の切り替え時に全解除する
    public void Reset()
    {
        activeEffects.Clear();
    }

    // 状態異常を付与する。既にかかっている場合は効果時間をリフレッシュする（重ね掛けはしない）。
    // 新規付与のときだけ true を返す（呼び出し側が通知イベントを出す判断に使う）。
    public bool Apply(StatusEffectType type)
    {
        if (type == StatusEffectType.None) return false;

        foreach (ActiveEffect e in activeEffects)
        {
            if (e.type == type)
            {
                e.remaining = DurationOf(type);
                return false;
            }
        }

        activeEffects.Add(new ActiveEffect
        {
            type = type,
            remaining = DurationOf(type),
            tickTimer = TickIntervalOf(type),
        });
        return true;
    }

    // 経過時間ぶんタイマーを進める。継続ダメージのtickは ticks に、期限切れした種別は expired に積む。
    // ダメージ適用・ログ・イベント発火は行わない（呼び出し側の責務）。
    public void Tick(float deltaTime, List<StatusTickResult> ticks, List<StatusEffectType> expired)
    {
        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveEffect e = activeEffects[i];
            e.remaining -= deltaTime;

            if (IsDamageOverTime(e.type))
            {
                e.tickTimer -= deltaTime;
                if (e.tickTimer <= 0f)
                {
                    e.tickTimer += TickIntervalOf(e.type);
                    ticks.Add(new StatusTickResult { Type = e.type, Damage = TickDamageOf(e.type) });
                }
            }

            if (e.remaining <= 0f)
            {
                activeEffects.RemoveAt(i);
                expired.Add(e.type);
            }
        }
    }

    private bool HasEffect(StatusEffectType type)
    {
        foreach (ActiveEffect e in activeEffects)
        {
            if (e.type == type) return true;
        }
        return false;
    }

    private static bool IsDamageOverTime(StatusEffectType type)
    {
        return type == StatusEffectType.Burn || type == StatusEffectType.Laceration;
    }

    private static float DurationOf(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Burn: return BurnDuration;
            case StatusEffectType.Laceration: return LacerationDuration;
            case StatusEffectType.Shock: return ShockDuration;
            case StatusEffectType.Dizzy: return DizzyDuration;
            case StatusEffectType.Blind: return BlindDuration;
            default: return 0f;
        }
    }

    private static float TickIntervalOf(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Burn: return BurnTickInterval;
            case StatusEffectType.Laceration: return LacerationTickInterval;
            default: return 0f;
        }
    }

    private static int TickDamageOf(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Burn: return BurnTickDamage;
            case StatusEffectType.Laceration: return LacerationTickDamage;
            default: return 0;
        }
    }
}
