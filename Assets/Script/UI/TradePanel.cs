using UnityEngine;
using UnityEngine.UI;

// トレード画面。複数トレーダーを縦に並べ、各トレーダーの交換メニューと依頼タスクを一覧する。
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
        if (trade != null)
        {
            trade.OnTraded += Rebuild;
            trade.OnTasksChanged += Rebuild;
        }
        if (inventory != null) inventory.OnInventoryChanged += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        if (trade != null)
        {
            trade.OnTraded -= Rebuild;
            trade.OnTasksChanged -= Rebuild;
        }
        if (inventory != null) inventory.OnInventoryChanged -= Rebuild;
    }

    private void OnClose()
    {
        if (phaseManager != null) phaseManager.ReturnToBuild();
    }

    private HideoutEntry NewRow()
    {
        GameObject row = Instantiate(entryPrefab, listRoot, false);
        return row.GetComponent<HideoutEntry>();
    }

    private void Rebuild()
    {
        if (listRoot == null || entryPrefab == null || trade == null) return;

        for (int i = listRoot.childCount - 1; i >= 0; i--)
            Destroy(listRoot.GetChild(i).gameObject);

        foreach (Trader trader in trade.Traders)
        {
            HideoutEntry header = NewRow();
            if (header != null)
                header.Bind("── " + trader.name + " ──", trader.blurb, "", false, null);

            foreach (TradeOffer offer in trader.offers)
            {
                HideoutEntry row = NewRow();
                if (row == null) continue;
                TradeOffer captured = offer;
                row.Bind(offer.label, "交換", "交換", trade.CanTrade(offer), () => trade.TryTrade(captured));
            }

            foreach (TraderTask task in trader.tasks)
            {
                HideoutEntry row = NewRow();
                if (row == null) continue;
                TraderTask captured = task;

                bool completed = trade.IsTaskCompleted(task);
                bool canClaim = trade.CanClaim(task);
                string detail = "[依頼] " + trade.TaskProgressText(task) + RewardText(task);
                string buttonText = completed ? "達成済" : (canClaim ? "報酬受取" : "未達成");

                row.Bind(task.title, detail, buttonText, canClaim, canClaim ? (System.Action)(() => trade.ClaimTask(captured)) : null);
            }
        }
    }

    private static string RewardText(TraderTask task)
    {
        if (task == null) return "";
        System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
        if (task.rewardItems != null)
        {
            foreach (MaterialCost c in task.rewardItems)
                if (c != null) parts.Add(MaterialCatalog.DisplayName(c) + " ×" + c.amount);
        }
        if (task.rewardStatPoints > 0) parts.Add("ステータスP +" + task.rewardStatPoints);
        return parts.Count > 0 ? "　→ " + string.Join(" / ", parts) : "";
    }
}
