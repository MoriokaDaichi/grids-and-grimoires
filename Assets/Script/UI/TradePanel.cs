using UnityEngine;
using UnityEngine.UI;

// トレード画面。TradeManager の交換メニューを一覧し、素材を交換する。
public class TradePanel : MonoBehaviour
{
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private GameObject entryPrefab;
    [SerializeField] private Button closeButton;

    private TradeManager trade;
    private GamePhaseManager phaseManager;
    private PlayerInventory inventory;

    void Awake()
    {
        trade = Object.FindFirstObjectByType<TradeManager>();
        phaseManager = Object.FindFirstObjectByType<GamePhaseManager>();
        inventory = Object.FindFirstObjectByType<PlayerInventory>();
        if (closeButton != null) closeButton.onClick.AddListener(OnClose);
    }

    void OnEnable()
    {
        if (trade != null) trade.OnTraded += Rebuild;
        if (inventory != null) inventory.OnInventoryChanged += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        if (trade != null) trade.OnTraded -= Rebuild;
        if (inventory != null) inventory.OnInventoryChanged -= Rebuild;
    }

    private void OnClose()
    {
        if (phaseManager != null) phaseManager.ReturnToBuild();
    }

    private void Rebuild()
    {
        if (listRoot == null || entryPrefab == null || trade == null) return;

        for (int i = listRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(listRoot.GetChild(i).gameObject);
        }

        foreach (TradeOffer offer in trade.Offers)
        {
            GameObject row = Instantiate(entryPrefab, listRoot, false);
            HideoutEntry entry = row.GetComponent<HideoutEntry>();
            if (entry == null) continue;

            TradeOffer captured = offer;
            entry.Bind(
                offer.label,
                "",
                "交換",
                trade.CanTrade(offer),
                () => trade.TryTrade(captured));
        }
    }
}
