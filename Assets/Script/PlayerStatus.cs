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

    // 上限値
    private const int HP_MAX = 1000;
    private const int OTHER_MAX = 100;

    // UI更新用のイベント（UI側に通知するため）
    public Action OnStatusChanged;
    public Action OnDefeated;

    void Awake()
    {
        currentHp = hp;
    }

    // ダンジョン突入時など、戦闘開始時にHPを全回復してリセットする
    public void BattleReset()
    {
        currentHp = hp;
        OnStatusChanged?.Invoke();
    }

    public void TakeDamage(int amount)
    {
        if (currentHp <= 0) return;

        currentHp = Mathf.Max(0, currentHp - amount);
        OnStatusChanged?.Invoke();

        if (currentHp <= 0) OnDefeated?.Invoke();
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