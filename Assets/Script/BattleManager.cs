using UnityEngine;
using System.Collections.Generic;

// オートバトルループ：グリッドに配置済みの魔法を各々のinterval間隔で自動発動する。
// 攻撃(単体/全体)、状態異常専用、補助(アッド/デュアル)、アクティブ/パッシブバフに対応。
// 敵は EnemyRoster が管理する複数体。単体魔法は先頭の生存個体、全体(AoE)魔法は生存個体すべてに当たる。
public class BattleManager : MonoBehaviour
{
    // バフの倍率は企画書に数値の記載が無いため仮決め（要バランス調整）
    private const float PassiveStatPercentPerStage = 10f; // パッシブ1段階につき+10%（Lv3で+30%）
    private const float ActiveStatPercent = 25f;           // アクティブバフ発動中は+25%
    private const float PassiveAttrDamagePercent = 15f;    // 属性バフ(パッシブ)1枚につき対応属性の魔法威力+15%
    private const int PassiveStatusRateBonus = 15;          // 状態異常付与率バフ(パッシブ)1枚につき対応属性の付与率+15pt

    // Spd = 発動間隔の短縮（BattleFormula.SpdCastMultiplier）、Luc = 会心発生率（BattleFormula.LucCritChance）。
    // どちらも Spd/Luc バフ(%)が乗る。攻撃魔法は敵の属性耐性倍率（EnemyStatus.ResistanceTo）も掛かる。

    private PlayerStatus playerStatus;
    private EnemyRoster roster;
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
    private bool battleActive;

    // 現在戦闘が進行中か（DungeonManagerが空杖出撃を検知するのに使う）
    public bool BattleActive { get { return battleActive; } }

    // ウェーブ突破後の「脱出／続行」選択待ちなど、外部から戦闘ループを止める。
    // 次の StartBattle() で再開する。
    public void EndBattle()
    {
        battleActive = false;
    }

    // 戦闘UI（BattleHUD）向けの通知。ロジックには影響しない
    public System.Action<MagicData> OnCastFired;          // 魔法が発動した（種別問わず）
    public System.Action<MagicData, int> OnAttackHit;     // 攻撃魔法が敵に命中した (魔法, ダメージ)
    public System.Action<BuffStat, float> OnBuffApplied;  // アクティブバフが発動/更新された (対象, 効果時間)
    public System.Action<BuffStat> OnBuffExpired;         // アクティブバフが切れた
    public System.Action<MagicData> OnManaStarved;        // マナ不足で発動を見送った

    // 補助魔法(アッドスペル/デュアルスペル)が参照する「直前に発動した攻撃魔法」
    private MagicData lastCastAttackSpell;

    private Dictionary<BuffStat, float> passiveStatPercent = new Dictionary<BuffStat, float>();
    private Dictionary<MagicAttribute, float> passiveAttrDamagePercent = new Dictionary<MagicAttribute, float>();
    private Dictionary<MagicAttribute, int> passiveStatusRateBonus = new Dictionary<MagicAttribute, int>();

    void Awake()
    {
        playerStatus = Object.FindFirstObjectByType<PlayerStatus>();
        roster = Object.FindFirstObjectByType<EnemyRoster>();
        gridManager = Object.FindFirstObjectByType<MagicGridManager>();
    }

    // ウェーブの敵をセットアップした後に DungeonManager が呼ぶ。
    // プレイヤーHP・アクティブバフの残り時間はダンジョン内で持ち越すためリセットしない。
    public void StartBattle()
    {
        if (playerStatus == null || roster == null || gridManager == null)
        {
            Debug.LogWarning("BattleManager: PlayerStatus / EnemyRoster / MagicGridManager がシーン内に見つかりません。");
            return;
        }

        ComputePassiveBonuses();

        // Clear()で使い回すと、ウェーブ全滅→同フレーム内でStartBattle()が再帰的に呼ばれた際に
        // Update()側のforeachが列挙中のリストを書き換えてしまう(InvalidOperationException)ため、新リストへ差し替える
        List<CastState> newCasts = new List<CastState>();
        foreach (MagicData data in gridManager.GetPlacedMagics())
        {
            bool isCastable = data.category == MagicCategory.Attack
                || data.category == MagicCategory.StatusInflict
                || data.category == MagicCategory.BuffActive
                || data.category == MagicCategory.Support;
            if (!isCastable) continue;
            newCasts.Add(new CastState { data = data, timer = data.interval * CastIntervalScale() });
        }
        casts = newCasts;

        foreach (EnemyStatus e in roster.Living)
        {
            if (e != null) e.attackTimer = e.attackInterval * e.AttackIntervalMultiplier;
        }

        battleActive = casts.Count > 0 && roster.AliveCount() > 0;

        if (casts.Count == 0)
        {
            Debug.LogWarning("BattleManager: 攻撃魔法が配置されていないため戦闘を開始できません。");
        }
    }

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

    private float TotalStatPercent(BuffStat stat)
    {
        float percent = GetOrZero(passiveStatPercent, stat);
        foreach (ActiveBuff b in activeBuffs)
        {
            if (b.stat == stat) percent += ActiveStatPercent;
        }
        return percent;
    }

    private int EffectiveSpd()
    {
        return Mathf.RoundToInt(playerStatus.spd * (1f + TotalStatPercent(BuffStat.Spd) / 100f));
    }

    private int EffectiveLuc()
    {
        return Mathf.RoundToInt(playerStatus.luc * (1f + TotalStatPercent(BuffStat.Luc) / 100f));
    }

    private float CastIntervalScale()
    {
        return BattleFormula.SpdCastMultiplier(EffectiveSpd());
    }

    void Update()
    {
        if (!battleActive) return;

        // 継続ダメージ等で全滅していたら決着。最終ウェーブだとDungeonManagerがStartBattleを再度呼ばないため、ここで止める
        if (roster.AliveCount() == 0)
        {
            battleActive = false;
            return;
        }

        // マナ／HP の自然回復（戦闘中のみ。HP回復はサステイン系アクセサリを装備しているときだけ効く）
        playerStatus.RegenMana(Time.deltaTime);
        playerStatus.RegenHealth(Time.deltaTime);

        // ループ中にStartBattle()が呼ばれてもcastsは新リストに差し替わるだけなので、スナップショットは安全に回せる
        List<CastState> currentCasts = casts;
        foreach (CastState cast in currentCasts)
        {
            cast.timer -= Time.deltaTime;
            if (cast.timer <= 0f)
            {
                int manaCost = ManaRules.CastCost(cast.data);
                if (!playerStatus.HasMana(manaCost))
                {
                    // マナ不足：発動間隔は消費せず、少し待ってから再試行する
                    cast.timer = ManaRules.StarvedRetryDelay;
                    OnManaStarved?.Invoke(cast.data);
                    continue;
                }
                playerStatus.SpendMana(manaCost);

                ExecuteCast(cast.data);
                if (casts != currentCasts) return; // ウェーブが切り替わった
                if (!battleActive) return;          // 最終ウェーブ全滅 or 敗北
                cast.timer += cast.data.interval * CastIntervalScale();
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

        if (!battleActive) return;

        // 各生存個体が自分のタイマーで反撃する
        int gen = roster.Generation;
        List<EnemyStatus> attackers = new List<EnemyStatus>(roster.Living);
        foreach (EnemyStatus e in attackers)
        {
            if (e == null || e.hp <= 0) continue;
            e.attackTimer -= Time.deltaTime;
            if (e.attackTimer <= 0f)
            {
                EnemyAttackFrom(e);
                if (roster.Generation != gen || !battleActive) return;
                e.attackTimer += e.attackInterval * e.AttackIntervalMultiplier;
            }
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

    // 状態異常専用魔法：ダメージ0、高付与率で状態異常のみ。先頭の生存個体を対象にする。
    // lastCastAttackSpell は更新しない（アッド/デュアルの再発動対象は攻撃魔法だけ）。
    private void CastStatusInflict(MagicData data)
    {
        EnemyStatus target = roster.FirstAlive();
        if (target == null)
        {
            battleActive = false;
            return;
        }
        if (data.statusEffect == StatusEffectType.None) return;

        string targetName = target.enemyName;
        int chance = data.statusEffectChance + GetOrZeroInt(passiveStatusRateBonus, data.attribute);

        if (Random.Range(0, 100) < chance)
        {
            target.ApplyStatusEffect(data.statusEffect);
            Debug.Log($"{data.magicName} が発動！ {targetName} は {StatusEffectLabel(data.statusEffect)} 状態になった！");
        }
        else
        {
            Debug.Log($"{data.magicName} が発動！ しかし {targetName} には効かなかった。");
        }
    }

    private void CastAttack(MagicData data)
    {
        if (roster.AliveCount() == 0)
        {
            battleActive = false;
            return;
        }

        if (data.range == MagicRange.AoE)
        {
            int gen = roster.Generation;
            List<EnemyStatus> targets = new List<EnemyStatus>(roster.Living);
            foreach (EnemyStatus t in targets)
            {
                if (t == null || t.hp <= 0) continue;
                HitOne(data, t);
                if (roster.Generation != gen) return; // ウェーブ切り替わり
            }
        }
        else
        {
            EnemyStatus t = roster.FirstAlive();
            if (t != null) HitOne(data, t);
        }

        lastCastAttackSpell = data;

        if (roster.AliveCount() == 0) battleActive = false;
    }

    // 攻撃魔法1発を1体に当てる。
    private void HitOne(MagicData data, EnemyStatus target)
    {
        if (target == null || target.hp <= 0) return;

        // TakeDamage が撃破→ウェーブ切り替えを連鎖させると target の中身が差し替わるため、先にスナップショット
        string targetName = target.enemyName;
        int hpBefore = target.hp;

        float effectiveAtk = playerStatus.atk * (1f + TotalStatPercent(BuffStat.Atk) / 100f);
        float damagePercent = TotalStatPercent(BuffStat.Damage) + GetOrZero(passiveAttrDamagePercent, data.attribute);
        float resist = target.ResistanceTo(data.attribute);
        int damage = BattleFormula.AttackDamage(data.damage, effectiveAtk, damagePercent, target.def, resist);

        bool crit = Random.Range(0, 100) < BattleFormula.LucCritChance(EffectiveLuc());
        if (crit) damage = Mathf.RoundToInt(damage * BattleFormula.CritMultiplier);

        int hpAfterThisHit = Mathf.Max(0, hpBefore - damage);
        bool willDefeat = hpAfterThisHit <= 0;

        target.TakeDamage(damage);
        OnAttackHit?.Invoke(data, damage);
        string critText = crit ? "（会心！）" : "";
        Debug.Log($"{data.magicName} が発動！ {targetName} に {damage} ダメージ{critText}（残りHP: {hpAfterThisHit}）");

        if (willDefeat)
        {
            Debug.Log($"{targetName} を倒した！");
        }
        else if (data.statusEffect != StatusEffectType.None)
        {
            int chance = data.statusEffectChance + GetOrZeroInt(passiveStatusRateBonus, data.attribute);
            if (Random.Range(0, 100) < chance)
            {
                target.ApplyStatusEffect(data.statusEffect);
                Debug.Log($"{targetName} は {StatusEffectLabel(data.statusEffect)} 状態になった！");
            }
        }
    }

    // アッドスペル(1回)/デュアルスペル(2回)：直前に発動した攻撃魔法をもう一度発動させる
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

    private void EnemyAttackFrom(EnemyStatus e)
    {
        if (e == null || e.hp <= 0) return;

        if (e.IsStunned)
        {
            Debug.Log($"{e.enemyName} は状態異常で動けない！");
            return;
        }

        float effectiveDef = playerStatus.def * (1f + TotalStatPercent(BuffStat.Def) / 100f);
        int damage = BattleFormula.EnemyAttackDamage(e.atk, e.AtkMultiplier, effectiveDef);
        playerStatus.TakeDamage(damage);
        Debug.Log($"{e.enemyName} の攻撃！ プレイヤーに {damage} ダメージ（残りHP: {playerStatus.currentHp}）");

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
