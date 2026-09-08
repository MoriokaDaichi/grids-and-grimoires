using System;
using UnityEngine;

// お金（ゴールド）の実体。シーンにシングルトンで1つ置く。
// 起動時にセーブを読み込み（初回だけ開始所持金を付与）、変更のたびに「Load → 自領域だけ更新 → Save」で保存する。
// トレード（TradeManager）・タスクの納金/報酬・ダンジョン入場料（DungeonManager）が参照する。
public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    // 開始所持金（仮。企画書に記載なし）
    public const int StartingMoney = 80;

    public Action OnMoneyChanged;

    private int balance;
    public int Balance { get { return balance; } }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        SaveData data = SaveManager.Load();
        if (!data.moneyInitialized)
        {
            data.money = StartingMoney;
            data.moneyInitialized = true;
            SaveManager.Save(data);
        }
        balance = data.money;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool CanAfford(int amount) { return amount <= 0 || balance >= amount; }

    public void Add(int amount)
    {
        if (amount <= 0) return;
        balance += amount;
        Persist();
    }

    public bool TrySpend(int amount)
    {
        if (amount <= 0) return true;
        if (balance < amount) return false;
        balance -= amount;
        Persist();
        return true;
    }

    private void Persist()
    {
        // 他システムのフィールドを潰さないよう、読み込んでからお金の領域だけ更新する
        SaveData data = SaveManager.Load();
        data.money = balance;
        data.moneyInitialized = true;
        SaveManager.Save(data);
        OnMoneyChanged?.Invoke();
    }
}
