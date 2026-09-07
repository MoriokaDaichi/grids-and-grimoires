using UnityEngine;
using System.Collections.Generic;

// 最小オートバトルループ：グリッドに配置済みの攻撃魔法(MagicCategory.Attack)を
// 各々のinterval間隔で自動発動し、ダミー敵にdamageと状態異常(statusEffect)を与える。
// バフ(BuffActive/BuffPassive)、補助魔法(Support: アッドスペル/デュアルスペル)も実装済み。
public class BattleManager : MonoBehaviour
{
    // バフの倍率は企画書に数値の記載が無いため仮決め（要バランス調整）
    private const float PassiveStatPercentPerStage = 10f; // パッシブ1段階につき+10%（Lv3で+30%）
    private const float ActiveStatPercent = 25f;           // アクティブバフ発動中は+25%
    private const float PassiveAttrDamagePercent = 15f;    // 属性バフ(パッシブ)1枚につき対応属性の魔法威力+15%
    private const int PassiveStatusRateBonus = 15;          // 状態異常付与率バフ(パッシブ)1枚につき対応属性の付与率+15pt

    // Spd/Lucバフも集計はするが、PlayerStatus.spd/lucそのものが未使用（戦闘計算に登場しない）ため現状は効果なし。
    // 素早さ・運が絡む仕組み（行動速度・クリティカル等）を実装する際にTotalStatPercentを参照すれば有効化できる。

    private PlayerStatus playerStatus;
    private EnemyStatus enemyStatus;
    private MagicGridManager gridManager;

    private class CastState
    {
        public MagicData data;
        public float timer;
    }

    private class ActiveBuff
    {
        public BuffStat stat;
        public float remaining;
    }

    private List<CastState> casts = new List<CastState>();
    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
    private float enemyAttackTimer;
    private bool battleActive;

    // 現在この敵との戦闘が進行中か（DungeonManagerが空杖出撃を検知するのに使う）
    public bool BattleActive { get { return battleActive; } }

    // 戦闘UI（BattleHUD）向けの通知。ロジックには影響しない
    public System.Action<MagicData> OnCastFired;          // 魔法が発動した（種別問わず）
    public System.Action<MagicData, int> OnAttackHit;     // 攻撃魔法が敵に命中した (魔法, ダメージ)
    public System.Action<BuffStat, float> OnBuffApplied;  // アクティブバフが発動/更新された (対象, 効果時間)
    public System.Action<BuffStat> OnBuffExpired;         // アクティブバフが切れた

    // 補助魔法(アッドスペル/デュアルスペル)が「直前に発動した魔法」として参照する対象。
    // Attack魔法が実際に発動したときだけ更新する（補助魔法自身やバフでは更新しない）
    private MagicData lastCastAttackSpell;

    // パッシブバフの集計結果（ダンジョン中は編成固定のため、敵が切り替わるたびに集計し直すだけでよい）
    private Dictionary<BuffStat, float> passiveStatPercent = new Dictionary<BuffStat, float>();
    private Dictionary<MagicAttribute, float> passiveAttrDamagePercent = new Dictionary<MagicAttribute, float>();
    private Dictionary<MagicAttribute, int> passiveStatusRateBonus = new Dictionary<MagicAttribute, int>();

    void Awake()
    {
        playerStatus = Object.FindFirstObjectByType<PlayerStatus>();
        enemyStatus = Object.FindFirstObjectByType<EnemyStatus>();
        gridManager = Object.FindFirstObjectByType<MagicGridManager>();
    }

    // 次の敵との戦闘を開始する。DungeonManagerが敵を切り替えるたびに呼ぶ想定
    // （プレイヤーHP・アクティブバフの残り時間はダンジョン内で持ち越すため、ここではリセットしない）
    public void StartBattle()
    {
        if (playerStatus == null || enemyStatus == null || gridManager == null)
        {
            Debug.LogWarning("BattleManager: PlayerStatus / EnemyStatus / MagicGridManager がシーン内に見つかりません。");
            return;
        }

        ComputePassiveBonuses();

        // Clear()で使い回すと、敵撃破→DungeonManagerが同フレーム内でStartBattle()を再帰的に呼んだ際に
        // Update()側のforeachが列挙中のリストを書き換えてしまう(InvalidOperationException)ため、
        // 新しいリストへの差し替えにしている
        List<CastState> newCasts = new List<CastState>();
        foreach (MagicData data in gridManager.GetPlacedMagics())
        {
            bool isCastable = data.category == MagicCategory.Attack
                || data.category == MagicCategory.StatusInflict
                || data.category == MagicCategory.BuffActive
                || data.category == MagicCategory.Support;
            if (!isCastable) continue;
            newCasts.Add(new CastState { data = data, timer = data.interval });
        }
        casts = newCasts;

        enemyAttackTimer = enemyStatus.attackInterval * enemyStatus.AttackIntervalMultiplier;
        battleActive = casts.Count > 0;

        if (!battleActive)
        {
            Debug.LogWarning("BattleManager: 攻撃魔法が配置されていないため戦闘を開始できません。");
        }
    }

    // グリッドに配置中のBuffPassiveの魔法を集計する（対応ステータスの強化 / 属性威力強化 / 状態異常付与率強化の3種）
    private void ComputePassiveBonuses()
    {
        PassiveBonuses bonuses = PassiveBonusCalculator.Aggregate(
            gridManager.GetPlacedMagics(),
            PassiveStatPercentPerStage,
            PassiveAttrDamagePercent,
            PassiveStatusRateBonus);

        passiveStatPercent = bonuses.StatPercent;
        passiveAttrDamagePercent = bonuses.AttrDamagePercent;
        passiveStatusRateBonus = bonuses.StatusRateBonus;

        LogPassiveSummary();
    }

    // パッシブは発動ログが無く画面上で効いているか分からないため、敵が切り替わるたびに集計結果を1行にまとめて出す
    private void LogPassiveSummary()
    {
        List<string> parts = new List<string>();

        foreach (KeyValuePair<BuffStat, float> kv in passiveStatPercent)
        {
            if (kv.Value != 0f) parts.Add($"{BuffStatLabel(kv.Key)}+{kv.Value}%");
        }
        foreach (KeyValuePair<MagicAttribute, float> kv in passiveAttrDamagePercent)
        {
            if (kv.Value != 0f) parts.Add($"{AttributeLabel(kv.Key)}属性威力+{kv.Value}%");
        }
        foreach (KeyValuePair<MagicAttribute, int> kv in passiveStatusRateBonus)
        {
            if (kv.Value != 0) parts.Add($"{AttributeLabel(kv.Key)}状態異常付与率+{kv.Value}pt");
        }

        string summary = parts.Count > 0 ? string.Join(" / ", parts) : "なし";
        Debug.Log($"パッシブ効果: {summary}");
    }

    private static float GetOrZero(Dictionary<BuffStat, float> dict, BuffStat key)
    {
        return dict.TryGetValue(key, out float v) ? v : 0f;
    }

    private static float GetOrZero(Dictionary<MagicAttribute, float> dict, MagicAttribute key)
    {
        return dict.TryGetValue(key, out float v) ? v : 0f;
    }

    private static int GetOrZeroInt(Dictionary<MagicAttribute, int> dict, MagicAttribute key)
    {
        return dict.TryGetValue(key, out int v) ? v : 0;
    }

    // 指定ステータスの合計強化率（パッシブ＋発動中のアクティブバフ）を%で返す
    private float TotalStatPercent(BuffStat stat)
    {
        float percent = GetOrZero(passiveStatPercent, stat);
        foreach (ActiveBuff b in activeBuffs)
        {
            if (b.stat == stat) percent += ActiveStatPercent;
        }
        return percent;
    }

    void Update()
    {
        if (!battleActive) return;

        // EnemyStatus側の継続ダメージ(火傷/裂傷)は自身のUpdate()で独立に敵を倒しうる。
        // 最終ウェーブでの撃破だとDungeonManagerが次のSetup()/StartBattle()を呼ばないため、
        // battleActiveをここで検知して止めないと、死んだ敵がいつまでも反撃してしまう
        if (enemyStatus.hp <= 0)
        {
            battleActive = false;
            return;
        }

        // このフレームで列挙するリストをスナップショットしておく。
        // ループ中にStartBattle()が呼ばれてもcastsは新しいリストに差し替わるだけなので、
        // currentCastsの列挙自体は安全に最後まで回せる
        List<CastState> currentCasts = casts;
        foreach (CastState cast in currentCasts)
        {
            cast.timer -= Time.deltaTime;
            if (cast.timer <= 0f)
            {
                ExecuteCast(cast.data);
                // 敵撃破により次の戦闘へ切り替わっていたら、古いキャスト情報の処理はここで打ち切る
                if (casts != currentCasts) return;
                // 最終ウェーブでの撃破はcastsが差し替わらないため、上と別にここでも決着を検知して打ち切る
                if (!battleActive) return;
                cast.timer += cast.data.interval;
            }
        }

        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            activeBuffs[i].remaining -= Time.deltaTime;
            if (activeBuffs[i].remaining <= 0f)
            {
                BuffStat expiredStat = activeBuffs[i].stat;
                activeBuffs.RemoveAt(i);
                OnBuffExpired?.Invoke(expiredStat);
            }
        }

        if (!battleActive) return; // このフレームで倒し切って戦闘が終わっていたら、敵の反撃は処理しない

        enemyAttackTimer -= Time.deltaTime;
        if (enemyAttackTimer <= 0f)
        {
            EnemyAttack();
            enemyAttackTimer += enemyStatus.attackInterval * enemyStatus.AttackIntervalMultiplier;
        }
    }

    private void ExecuteCast(MagicData data)
    {
        OnCastFired?.Invoke(data);

        if (data.category == MagicCategory.Attack) CastAttack(data);
        else if (data.category == MagicCategory.StatusInflict) CastStatusInflict(data);
        else if (data.category == MagicCategory.BuffActive) CastBuff(data);
        else if (data.category == MagicCategory.Support) CastSupport(data);
    }

    // 状態異常専用魔法(火あぶり/電磁波/かまいたち/目くらまし/目隠し)：
    // ダメージは無く、高い付与率(企画書8章では80%)で対応する状態異常のみを与える。
    // lastCastAttackSpell は更新しない ＝ アッド/デュアルスペルの再発動対象は攻撃魔法だけに限る。
    private void CastStatusInflict(MagicData data)
    {
        if (enemyStatus.hp <= 0)
        {
            battleActive = false;
            return;
        }

        if (data.statusEffect == StatusEffectType.None) return;

        string targetName = enemyStatus.enemyName;
        int chance = data.statusEffectChance + GetOrZeroInt(passiveStatusRateBonus, data.attribute);

        if (Random.Range(0, 100) < chance)
        {
            enemyStatus.ApplyStatusEffect(data.statusEffect);
            Debug.Log($"{data.magicName} が発動！ {targetName} は {StatusEffectLabel(data.statusEffect)} 状態になった！");
        }
        else
        {
            Debug.Log($"{data.magicName} が発動！ しかし {targetName} には効かなかった。");
        }
    }

    private void CastAttack(MagicData data)
    {
        // 同フレーム内で既に(火傷等の継続ダメージや他のキャストで)倒れている対象には追撃しない。
        // ここを素通りすると、既に0HPの相手に「発動！を倒した！」が二重に出てしまう
        if (enemyStatus.hp <= 0)
        {
            battleActive = false;
            return;
        }

        // 撃破がダンジョンの次の敵へのSetup()を連鎖させると、この呼び出しの後でenemyStatusの
        // 中身（名前・HP）が次の敵のものに差し替わってしまう。ログ表示・状態異常付与の対象を
        // 誤らないよう、ダメージを与える前に対象の情報をスナップショットしておく
        string targetName = enemyStatus.enemyName;
        int hpBefore = enemyStatus.hp;

        float effectiveAtk = playerStatus.atk * (1f + TotalStatPercent(BuffStat.Atk) / 100f);
        float damagePercent = TotalStatPercent(BuffStat.Damage) + GetOrZero(passiveAttrDamagePercent, data.attribute);
        int damage = BattleFormula.AttackDamage(data.damage, effectiveAtk, damagePercent, enemyStatus.def);

        int hpAfterThisHit = Mathf.Max(0, hpBefore - damage);
        bool willDefeat = hpAfterThisHit <= 0;

        enemyStatus.TakeDamage(damage);
        OnAttackHit?.Invoke(data, damage);
        Debug.Log($"{data.magicName} が発動！ {targetName} に {damage} ダメージ（残りHP: {hpAfterThisHit}）");

        if (willDefeat)
        {
            Debug.Log($"{targetName} を倒した！");
        }
        else if (data.statusEffect != StatusEffectType.None)
        {
            int chance = data.statusEffectChance + GetOrZeroInt(passiveStatusRateBonus, data.attribute);
            if (Random.Range(0, 100) < chance)
            {
                enemyStatus.ApplyStatusEffect(data.statusEffect);
                Debug.Log($"{targetName} は {StatusEffectLabel(data.statusEffect)} 状態になった！");
            }
        }

        lastCastAttackSpell = data; // 補助魔法(アッドスペル/デュアルスペル)が参照する「直前に発動した魔法」を更新

        if (enemyStatus.hp <= 0)
        {
            battleActive = false;
        }
    }

    // アッドスペル(1回)/デュアルスペル(2回)：直前に発動した攻撃魔法をもう一度(もしくは2回)発動させる
    private void CastSupport(MagicData data)
    {
        if (lastCastAttackSpell == null)
        {
            Debug.Log($"{data.magicName} が発動！ しかし直前に発動した魔法が無く不発だった。");
            return;
        }

        MagicData repeatTarget = lastCastAttackSpell;
        List<CastState> castsSnapshot = casts;

        Debug.Log($"{data.magicName} が発動！ {repeatTarget.magicName} を{data.supportRepeatCount}回追加発動！");

        for (int i = 0; i < data.supportRepeatCount; i++)
        {
            CastAttack(repeatTarget);
            // 追加発動が撃破→次の敵への切り替えや戦闘終了を引き起こしていたら、残りの追加発動は行わない
            if (casts != castsSnapshot || !battleActive) return;
        }
    }

    private void CastBuff(MagicData data)
    {
        bool refreshed = false;
        foreach (ActiveBuff b in activeBuffs)
        {
            if (b.stat == data.buffStat)
            {
                b.remaining = data.buffDuration;
                refreshed = true;
                break;
            }
        }
        if (!refreshed) activeBuffs.Add(new ActiveBuff { stat = data.buffStat, remaining = data.buffDuration });

        OnBuffApplied?.Invoke(data.buffStat, data.buffDuration);
        Debug.Log($"{data.magicName} が発動！ {BuffStatLabel(data.buffStat)}を強化（残り{data.buffDuration}秒）");
    }

    private void EnemyAttack()
    {
        if (enemyStatus.IsStunned)
        {
            Debug.Log($"{enemyStatus.enemyName} は状態異常で動けない！");
            return;
        }

        float effectiveDef = playerStatus.def * (1f + TotalStatPercent(BuffStat.Def) / 100f);
        int damage = BattleFormula.EnemyAttackDamage(enemyStatus.atk, enemyStatus.AtkMultiplier, effectiveDef);
        playerStatus.TakeDamage(damage);
        Debug.Log($"{enemyStatus.enemyName} の攻撃！ プレイヤーに {damage} ダメージ（残りHP: {playerStatus.currentHp}）");

        if (playerStatus.currentHp <= 0)
        {
            battleActive = false;
            Debug.Log("プレイヤーは倒れた… 敗北。");
        }
    }

    private static string StatusEffectLabel(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Burn: return "火傷";
            case StatusEffectType.Shock: return "感電";
            case StatusEffectType.Laceration: return "裂傷";
            case StatusEffectType.Dizzy: return "眩暈";
            case StatusEffectType.Blind: return "盲目";
            default: return type.ToString();
        }
    }

    private static string BuffStatLabel(BuffStat stat)
    {
        switch (stat)
        {
            case BuffStat.Atk: return "攻撃力";
            case BuffStat.Def: return "防御力";
            case BuffStat.Spd: return "素早さ";
            case BuffStat.Luc: return "運";
            case BuffStat.Damage: return "魔法威力";
            default: return stat.ToString();
        }
    }

    private static string AttributeLabel(MagicAttribute attribute)
    {
        switch (attribute)
        {
            case MagicAttribute.Fire: return "炎";
            case MagicAttribute.Thunder: return "雷";
            case MagicAttribute.Wind: return "風";
            case MagicAttribute.Light: return "光";
            case MagicAttribute.Dark: return "闇";
            default: return attribute.ToString();
        }
    }
}
