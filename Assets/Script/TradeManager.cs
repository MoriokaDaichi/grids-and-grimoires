using System;
using System.Collections.Generic;
using UnityEngine;

// トレーダーとの素材交換。シーンにシングルトンで1つ。
// 交換メニューは TradeCatalog（固定の変換群）。実消費・付与は PlayerInventory 経由。
public class TradeManager : MonoBehaviour
{
    public static TradeManager Instance { get; private set; }

    public Action OnTraded;

    private readonly List<TradeOffer> offers = TradeCatalog.StandardOffers();
    public IReadOnlyList<TradeOffer> Offers { get { return offers; } }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool CanTrade(TradeOffer offer)
    {
        if (offer == null) return false;
        PlayerInventory inv = PlayerInventory.Instance;
        return inv != null && inv.CanAfford(offer.give);
    }

    public bool TryTrade(TradeOffer offer)
    {
        if (!CanTrade(offer)) return false;

        PlayerInventory inv = PlayerInventory.Instance;
        if (!inv.TrySpend(offer.give)) return false;

        if (offer.receive != null && offer.receive.Count > 0)
        {
            inv.Add(offer.receive);
        }

        if (offer.bonusStatPoints > 0)
        {
            PlayerStatus ps = UnityEngine.Object.FindFirstObjectByType<PlayerStatus>();
            if (ps != null) ps.AddStatsPoint(offer.bonusStatPoints);
        }

        OnTraded?.Invoke();
        Debug.Log($"[トレード] {offer.label} を交換した。");
        return true;
    }
}
