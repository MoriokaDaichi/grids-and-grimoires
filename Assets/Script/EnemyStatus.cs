using UnityEngine;
using System;
using System.Collections.Generic;

// 最小オートバトル用の仮ダミー敵。企画書に敵データ数値の記載が無いため、
// Tier1単体魔法（interval 3秒 / damage 5、MagicDataGenerator.cs参照）を基準にバランスを仮決めしている。
// 正式な敵データテーブルが決まり次第、このデフォルト値は差し替える想定。
//
// 状態異常のタイマー処理は StatusEffectController に切り出してある（EditModeテストのため）。
public class EnemyStatus : MonoBehaviour
{
    public string enemyName = "スライム";
    public int maxHp = 60;
    public int atk = 6;
    public int def = 2;
    public float attackInterval = 4f;
    public MagicAttribute attribute = MagicAttribute.None;
    public Sprite sprite;

    // BattleManager が管理するこの敵の次回攻撃までの残り時間
    public float attackTimer;

    public int hp { get; private set; }

    // このインスタンスの元になった EnemyData（トレーダーのタスク進捗などで種類を参照する）
    public EnemyData source { get; private set; }

    // 属性ごとの被ダメージ倍率（Setupで EnemyData から構築）。未登録の属性は 1.0。
    private readonly Dictionary<MagicAttribute, float> resistances = new Dictionary<MagicAttribute, float>();

    public Action OnStatusChanged;
    public Action OnDefeated;

    // 戦闘UI（BattleHUD）向けの通知。ロジックには影響しない
    public Action<int, int> OnDamaged;            // (被ダメージ量, 残りHP)
    public Action<StatusEffectType> OnStatusApplied;   // 新規に状態異常が付与された
    public Action<StatusEffectType> OnStatusExpired;   // 状態異常が期限切れで解除された
    public Action<StatusEffectType, int> OnStatusTick; // 継続ダメージのtick (種別, ダメージ)

    private readonly StatusEffectController statusEffects = new StatusEffectController();

    // Tick() の結果受け取り用（毎フレーム使い回す）
    private readonly List<StatusEffectController.StatusTickResult> tickBuffer = new List<StatusEffectController.StatusTickResult>();
    private readonly List<StatusEffectType> expiredBuffer = new List<StatusEffectType>();

    // 継続ダメージのtick適用中に撃破→次の敵のSetup()が連鎖すると、この敵の状態が次の敵のものに
    // 差し替わる。世代番号を進めておき、tick適用ループの途中で切り替わったら残りを打ち切る
    // （旧実装で activeEffects の参照差し替えを検知していたのと同じ目的）。
    private int battleGeneration;

    public bool IsStunned { get { return statusEffects.IsStunned; } }
    public float AttackIntervalMultiplier { get { return statusEffects.AttackIntervalMultiplier; } }
    public float AtkMultiplier { get { return statusEffects.AtkMultiplier; } }

    void Awake()
    {
        hp = maxHp;
    }

    // EnemyDataの数値を反映してHPを全回復する（ダンジョンの次の敵へ切り替える際に使用）
    public void Setup(EnemyData data)
    {
        Setup(data, WaveScaling.None);
    }

    // 深度スケーリング倍率を掛けて反映する（エンドレスダンジョン用）
    public void Setup(EnemyData data, WaveScaling scaling)
    {
        source = data;
        enemyName = data.enemyName;
        maxHp = Mathf.Max(1, Mathf.RoundToInt(data.maxHp * scaling.hpMult));
        atk = Mathf.Max(0, Mathf.RoundToInt(data.atk * scaling.atkMult));
        def = Mathf.Max(0, Mathf.RoundToInt(data.def * scaling.defMult));
        attackInterval = data.attackInterval;
        attribute = data.attribute;
        sprite = data.sprite;

        resistances.Clear();
        if (data.resistances != null)
        {
            foreach (AttributeResistance r in data.resistances)
            {
                if (r != null) resistances[r.attribute] = r.multiplier;
            }
        }

        ResetBattle();
    }

    // 指定属性の魔法で受けるダメージ倍率。未設定なら等倍。
    public float ResistanceTo(MagicAttribute attr)
    {
        float m;
        return resistances.TryGetValue(attr, out m) ? m : 1f;
    }

    public void ResetBattle()
    {
        hp = maxHp;
        statusEffects.Reset();
        battleGeneration++;
        OnStatusChanged?.Invoke();
    }

    public void TakeDamage(int amount)
    {
        if (hp <= 0) return;

        hp = Mathf.Max(0, hp - amount);
        OnStatusChanged?.Invoke();
        OnDamaged?.Invoke(amount, hp);

        if (hp <= 0) OnDefeated?.Invoke();
    }

    // 攻撃魔法の状態異常付与時に呼ぶ。既にかかっている場合は効果時間をリフレッシュする（重ね掛けはしない）
    public void ApplyStatusEffect(StatusEffectType type)
    {
        if (type == StatusEffectType.None || hp <= 0) return;

        if (statusEffects.Apply(type))
        {
            OnStatusChanged?.Invoke();
            OnStatusApplied?.Invoke(type);
        }
    }

    void Update()
    {
        if (hp <= 0 || !statusEffects.HasAny) return;

        tickBuffer.Clear();
        expiredBuffer.Clear();
        statusEffects.Tick(Time.deltaTime, tickBuffer, expiredBuffer);

        int gen = battleGeneration;
        foreach (StatusEffectController.StatusTickResult tick in tickBuffer)
        {
            // 直前のtickでの撃破が次の敵への切り替えを連鎖させていたら、残りのtickは適用しない
            if (hp <= 0 || battleGeneration != gen) break;

            // TakeDamageが撃破→次の敵のSetup()を連鎖させると、この呼び出しの後でenemyName/hpが
            // 次の敵のものに差し替わってしまうため、ログに使う値は呼び出し前にスナップショットしておく
            string name = enemyName;
            int tickDamage = tick.Damage;
            int hpAfterTick = Mathf.Max(0, hp - tickDamage);

            TakeDamage(tickDamage);
            OnStatusTick?.Invoke(tick.Type, tickDamage);
            Debug.Log($"{name} は{StatusEffectLabel(tick.Type)}のダメージ！ {tickDamage} ダメージ（残りHP: {hpAfterTick}）");
            if (hpAfterTick <= 0) Debug.Log($"{name} を倒した！");
        }

        // 次の敵へ切り替わっていたら、この敵向けの期限切れ通知は出さない（新しい敵の状態はリセット済み）
        if (battleGeneration != gen) return;
        foreach (StatusEffectType expiredType in expiredBuffer)
        {
            OnStatusExpired?.Invoke(expiredType);
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
