using UnityEngine;
using UnityEngine.UI;
using System;

public class PlayerStatus : MonoBehaviour
{
    // 現在のステータス値
    public int hp = 100;
    public int atk = 5;
    public int def = 5;
    public int spd = 5;
    public int luc = 5;

    // 残りステータスポイント
    public int statsPoint = 5;

    // バトル中の現在HP（hpは振り分けで決まる最大値、こちらは戦闘中に増減する値）
    public int currentHp { get; private set; }

    // マナ（魔力）。魔法を発動するたびに消費し、戦闘中は自然回復する。数値は全て仮（ManaRules）。
    public int maxMana = ManaRules.BaseMaxMana;
    public float manaRegenPerSecond = ManaRules.DefaultRegenPerSecond;
    public float currentMana { get; private set; }

    // 上限値
    private const int HP_MAX = 1000;
    private const int OTHER_MAX = 100;

    // UI更新用のイベント（UI側に通知するため）
    public Action OnStatusChanged;
    public Action OnDefeated;
    public Action<int, int> OnDamaged; // (被ダメージ量, 残りcurrentHp)
    public Action OnManaChanged;       // マナが増減した

    void Awake()
    {
        currentHp = hp;
        currentMana = maxMana;
    }

    // ダンジョン突入時など、戦闘開始時にHP・マナを全回復してリセットする
    public void BattleReset()
    {
        currentHp = hp;
        currentMana = maxMana;
        OnStatusChanged?.Invoke();
        OnManaChanged?.Invoke();
    }

    // 発動に必要なマナがあるか
    public bool HasMana(int cost)
    {
        return currentMana >= cost;
    }

    // マナを消費する。足りなければ false（消費しない）。
    public bool SpendMana(int cost)
    {
        if (cost <= 0) return true;
        if (currentMana < cost) return false;

        currentMana -= cost;
        OnManaChanged?.Invoke();
        OnStatusChanged?.Invoke();
        return true;
    }

    // 戦闘中の自然回復。BattleManager が毎フレーム呼ぶ。
    public void RegenMana(float deltaTime)
    {
        if (currentMana >= maxMana) return;

        currentMana = Mathf.Min(maxMana, currentMana + ManaRules.RegenAmount(manaRegenPerSecond, deltaTime));
        OnManaChanged?.Invoke();
    }

    public void TakeDamage(int amount)
    {
        if (currentHp <= 0) return;

        currentHp = Mathf.Max(0, currentHp - amount);
        OnStatusChanged?.Invoke();
        OnDamaged?.Invoke(amount, currentHp);

        if (currentHp <= 0) OnDefeated?.Invoke();
    }

    // トレード等でステータスポイントを増やす
    public void AddStatsPoint(int amount)
    {
        if (amount <= 0) return;
        statsPoint += amount;
        OnStatusChanged?.Invoke();
    }

    public void AddStat(string type)
    {
        if (statsPoint <= 0) return;

        switch (type)
        {
            case "HP":
                if (hp < HP_MAX) { hp += 10; statsPoint--; }
                break;
            case "Atk":
                if (atk < OTHER_MAX) { atk += 1; statsPoint--; }
                break;
            case "Def":
                if (def < OTHER_MAX) { def += 1; statsPoint--; }
                break;
            case "Spd":
                if (spd < OTHER_MAX) { spd += 1; statsPoint--; }
                break;
            case "Luc":
                if (luc < OTHER_MAX) { luc += 1; statsPoint--; }
                break;
        }

        // UIを更新するように通知
        OnStatusChanged?.Invoke();
    }
}