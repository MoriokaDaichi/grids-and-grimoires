using System;
using System.Collections.Generic;
using UnityEngine;

// 所持素材の実体。シーンにシングルトンで1つ置く。起動時にセーブを読み込み、変更のたびに保存する。
public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    public Action OnInventoryChanged;

    private readonly MaterialLedger ledger = new MaterialLedger();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        ledger.LoadFrom(SaveManager.Load());
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public int GetCount(MaterialCost cost) { return ledger.GetCount(cost); }
    public MaterialCost Sample(string key) { return ledger.Sample(key); }
    public IEnumerable<KeyValuePair<string, int>> Counts { get { return ledger.Counts; } }

    public void Add(IEnumerable<MaterialCost> gained)
    {
        ledger.Add(gained);
        Persist();
    }

    public bool TrySpend(IEnumerable<MaterialCost> cost)
    {
        bool ok = ledger.TrySpend(cost);
        if (ok) Persist();
        return ok;
    }

    private void Persist()
    {
        SaveData data = new SaveData();
        ledger.WriteTo(data);
        SaveManager.Save(data);
        OnInventoryChanged?.Invoke();
    }
}
