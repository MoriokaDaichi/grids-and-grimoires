using UnityEngine;
using System;
using System.Collections.Generic;

// 最小オートバトル用の仮ダミー敵。企画書に敵データ数値の記載が無いため、
// Tier1単体魔法（interval 3秒 / damage 5、MagicDataGenerator.cs参照）を基準にバランスを仮決めしている。
// 正式な敵データテーブルが決まり次第、このデフォルト値は差し替える想定。
public class EnemyStatus : MonoBehaviour
{
    public string enemyName = "スライム";
    public int maxHp = 60;
    public int atk = 6;
    public int def = 2;
    public float attackInterval = 4f;

    public int hp { get; private set; }

    public Action OnStatusChanged;
    public Action OnDefeated;

    // 状態異常の効果内容も企画書に数値の記載が無いため仮決め（5種で役割が被らないようにしている）。
    // 火傷/裂傷=継続ダメージ、感電=スタン（反撃が発動しない）、眩暈=行動間隔延長、盲目=攻撃力ダウン
    private class ActiveEffect
    {
        public StatusEffectType type;
        public float remaining;
        public float tickTimer;
    }

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

    private List<ActiveEffect> activeEffects = new List<ActiveEffect>();

    public bool IsStunned { get { return HasEffect(StatusEffectType.Shock); } }
    public float AttackIntervalMultiplier { get { return HasEffect(StatusEffectType.Dizzy) ? DizzyAttackIntervalMultiplier : 1f; } }
    public float AtkMultiplier { get { return HasEffect(StatusEffectType.Blind) ? BlindAtkMultiplier : 1f; } }

    void Awake()
    {
        hp = maxHp;
    }

    // EnemyDataの数値を反映してHPを全回復する（ダンジョンの次の敵へ切り替える際に使用）
    public void Setup(EnemyData data)
    {
        enemyName = data.enemyName;
        maxHp = data.maxHp;
        atk = data.atk;
        def = data.def;
        attackInterval = data.attackInterval;
        ResetBattle();
    }

    public void ResetBattle()
    {
        hp = maxHp;
        activeEffects = new List<ActiveEffect>(); // Clear()で使い回すとUpdate()側の列挙中に書き換わる恐れがあるため差し替え
        OnStatusChanged?.Invoke();
    }

    public void TakeDamage(int amount)
    {
        if (hp <= 0) return;

        hp = Mathf.Max(0, hp - amount);
        OnStatusChanged?.Invoke();

        if (hp <= 0) OnDefeated?.Invoke();
    }

    // 攻撃魔法の状態異常付与時に呼ぶ。既にかかっている場合は効果時間をリフレッシュする（重ね掛けはしない）
    public void ApplyStatusEffect(StatusEffectType type)
    {
        if (type == StatusEffectType.None || hp <= 0) return;

        foreach (ActiveEffect e in activeEffects)
        {
            if (e.type == type)
            {
                e.remaining = DurationOf(type);
                return;
            }
        }

        activeEffects.Add(new ActiveEffect { type = type, remaining = DurationOf(type), tickTimer = TickIntervalOf(type) });
        OnStatusChanged?.Invoke();
    }

    void Update()
    {
        if (hp <= 0 || activeEffects.Count == 0) return;

        // TakeDamage経由で撃破→次の敵のSetup()が連鎖すると、activeEffectsが新しいリストに差し替わる。
        // その場合はこのフレームの残りの列挙を打ち切る（BattleManagerと同じ理由でCollection was modifiedを避けるため）
        List<ActiveEffect> current = activeEffects;
        for (int i = current.Count - 1; i >= 0; i--)
        {
            ActiveEffect e = current[i];
            e.remaining -= Time.deltaTime;

            if (IsDamageOverTime(e.type))
            {
                e.tickTimer -= Time.deltaTime;
                if (e.tickTimer <= 0f)
                {
                    e.tickTimer += TickIntervalOf(e.type);

                    // TakeDamageが撃破→次の敵のSetup()を連鎖させると、この呼び出しの後でenemyName/hpが
                    // 次の敵のものに差し替わってしまうため、ログに使う値は呼び出し前にスナップショットしておく
                    string name = enemyName;
                    int tickDamage = TickDamageOf(e.type);
                    int hpAfterTick = Mathf.Max(0, hp - tickDamage);

                    TakeDamage(tickDamage);
                    Debug.Log($"{name} は{StatusEffectLabel(e.type)}のダメージ！ {tickDamage} ダメージ（残りHP: {hpAfterTick}）");
                    if (hpAfterTick <= 0) Debug.Log($"{name} を倒した！");

                    if (activeEffects != current) return;
                }
            }

            if (e.remaining <= 0f) current.RemoveAt(i);
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

    private static string StatusEffectLabel(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Burn: return "火傷";
            case StatusEffectType.Laceration: return "裂傷";
            default: return type.ToString();
        }
    }
}
