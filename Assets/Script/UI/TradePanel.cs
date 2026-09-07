using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// トレード画面。トレーダーごとにタブを分け、各トレーダー内で「交換」「依頼」をサブタブで切り替える。
// 行はコードで組み立て（プレハブ不使用、テキストは折り返して高さ可変）。
public class TradePanel : MonoBehaviour
{
    [Serializable]
    public class TraderIconEntry
    {
        public string traderId;
        public Sprite icon;
    }

    [SerializeField] private RectTransform tabBar;      // トレーダーのタブ（横並び）
    [SerializeField] private RectTransform subTabBar;   // 交換 / 依頼
    [SerializeField] private RectTransform listRoot;    // スクロール内容（Vertical + ContentSizeFitter）
    [SerializeField] private TMP_Text blurbText;        // 選択中トレーダーの説明
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private TraderIconEntry[] traderIcons;

    private TradeManager trade;
    private GamePhaseManager phaseManager;
    private PlayerInventory inventory;

    private int activeTrader;
    private int activeSubTab; // 0 = 交換, 1 = 依頼

    private static readonly Color TabOn = new Color(0.30f, 0.42f, 0.46f, 1f);
    private static readonly Color TabOff = new Color(0.16f, 0.18f, 0.21f, 1f);
    private static readonly Color SubOn = new Color(0.28f, 0.4f, 0.62f, 1f);
    private static readonly Color SubOff = new Color(0.18f, 0.19f, 0.23f, 1f);
    private static readonly Color OkBtn = new Color(0.26f, 0.46f, 0.34f, 1f);
    private static readonly Color DimBtn = new Color(0.30f, 0.30f, 0.36f, 1f);
    private static readonly Color CardBg = new Color(0f, 0f, 0f, 0.30f);

    void Awake()
    {
        trade = UnityEngine.Object.FindFirstObjectByType<TradeManager>();
        phaseManager = UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>();
        inventory = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
        if (closeButton != null) closeButton.onClick.AddListener(() => { if (phaseManager != null) phaseManager.ReturnToBuild(); });
    }

    void OnEnable()
    {
        if (trade != null) { trade.OnTraded += Rebuild; trade.OnTasksChanged += Rebuild; }
        if (inventory != null) inventory.OnInventoryChanged += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        if (trade != null) { trade.OnTraded -= Rebuild; trade.OnTasksChanged -= Rebuild; }
        if (inventory != null) inventory.OnInventoryChanged -= Rebuild;
    }

    private Sprite IconFor(string traderId)
    {
        if (traderIcons == null) return null;
        foreach (TraderIconEntry e in traderIcons)
            if (e != null && e.traderId == traderId) return e.icon;
        return null;
    }

    // ---------------------------------------------------------------- 生成

    private void Rebuild()
    {
        if (trade == null || listRoot == null) return;

        IReadOnlyList<Trader> traders = trade.Traders;
        if (traders.Count == 0) return;
        if (activeTrader >= traders.Count) activeTrader = 0;

        BuildTraderTabs(traders);
        BuildSubTabs();

        Trader t = traders[activeTrader];
        if (blurbText != null) blurbText.text = t.name + " — " + t.blurb;

        for (int i = listRoot.childCount - 1; i >= 0; i--)
            Destroy(listRoot.GetChild(i).gameObject);

        if (activeSubTab == 0) BuildOffers(t);
        else BuildTasks(t);
    }

    private void BuildTraderTabs(IReadOnlyList<Trader> traders)
    {
        if (tabBar == null) return;
        for (int i = tabBar.childCount - 1; i >= 0; i--)
            Destroy(tabBar.GetChild(i).gameObject);

        for (int i = 0; i < traders.Count; i++)
        {
            Trader tr = traders[i];
            int idx = i;
            bool on = i == activeTrader;

            RectTransform tab = MakeRow(tabBar, 54f);
            HorizontalLayoutGroup h = tab.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 6f; h.padding = new RectOffset(6, 6, 4, 4);
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            h.childAlignment = TextAnchor.MiddleLeft;
            Image bg = AddImage(tab, on ? TabOn : TabOff, true);
            Button b = tab.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => { activeTrader = idx; activeSubTab = 0; Rebuild(); });
            LayoutElement tle = tab.gameObject.AddComponent<LayoutElement>();
            tle.minWidth = 150f; tle.preferredWidth = 190f; tle.minHeight = 50f;

            // 顔アイコン（未設定でも枠は出す。ユーザーが traderIcons に割り当てる）
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(tab, false);
            Image icon = iconGo.AddComponent<Image>();
            Sprite sp = IconFor(tr.id);
            icon.sprite = sp;
            icon.color = sp != null ? Color.white : new Color(0.4f, 0.44f, 0.5f, 1f);
            icon.raycastTarget = false;
            LayoutElement ile = iconGo.AddComponent<LayoutElement>();
            ile.minWidth = 40f; ile.preferredWidth = 40f; ile.minHeight = 40f; ile.preferredHeight = 40f;

            TMP_Text name = Text(tab, tr.name, 15, on ? FontStyles.Bold : FontStyles.Normal, Color.white);
            name.alignment = TextAlignmentOptions.MidlineLeft;
            name.GetComponent<LayoutElement>().flexibleWidth = 1f;
        }
    }

    private void BuildSubTabs()
    {
        if (subTabBar == null) return;
        for (int i = subTabBar.childCount - 1; i >= 0; i--)
            Destroy(subTabBar.GetChild(i).gameObject);

        string[] labels = { "交換", "依頼" };
        for (int i = 0; i < labels.Length; i++)
        {
            int idx = i;
            bool on = i == activeSubTab;
            RectTransform tab = MakeRow(subTabBar, 40f);
            Image bg = AddImage(tab, on ? SubOn : SubOff, true);
            Button b = tab.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            b.onClick.AddListener(() => { activeSubTab = idx; Rebuild(); });
            LayoutElement le = tab.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 130f; le.preferredWidth = 160f; le.minHeight = 38f;
            TMP_Text lbl = Text(tab, labels[i], 17, on ? FontStyles.Bold : FontStyles.Normal, Color.white);
            RectTransform lrt = (RectTransform)lbl.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            lbl.alignment = TextAlignmentOptions.Center;
            UnityEngine.Object.Destroy(lbl.GetComponent<LayoutElement>());
        }
    }

    private void BuildOffers(Trader t)
    {
        if (t.offers.Count == 0) { InfoRow("（交換メニューはありません）"); return; }
        foreach (TradeOffer offer in t.offers)
        {
            TradeOffer captured = offer;
            bool can = trade.CanTrade(offer);
            ActionRow(offer.label, "交換", can, can ? OkBtn : DimBtn, () => trade.TryTrade(captured));
        }
    }

    private void BuildTasks(Trader t)
    {
        if (t.tasks.Count == 0) { InfoRow("（依頼はありません）"); return; }
        foreach (TraderTask task in t.tasks)
        {
            TraderTask captured = task;
            bool completed = trade.IsTaskCompleted(task);
            bool canClaim = trade.CanClaim(task);
            string detail = trade.TaskProgressText(task) + RewardText(task);
            string btn = completed ? "達成済" : (canClaim ? "報酬受取" : "未達成");
            ActionRow("<b>" + task.title + "</b>\n<size=85%>" + detail + "</size>",
                btn, canClaim, canClaim ? OkBtn : DimBtn,
                canClaim ? (Action)(() => trade.ClaimTask(captured)) : null);
        }
    }

    private static string RewardText(TraderTask task)
    {
        if (task == null) return "";
        List<string> parts = new List<string>();
        if (task.rewardItems != null)
            foreach (MaterialCost c in task.rewardItems)
                if (c != null) parts.Add(MaterialCatalog.DisplayName(c) + " ×" + c.amount);
        if (task.rewardStatPoints > 0) parts.Add("ステータスP +" + task.rewardStatPoints);
        return parts.Count > 0 ? "　→ " + string.Join(" / ", parts) : "";
    }

    // ---------------------------------------------------------------- 行ヘルパー

    private void InfoRow(string text)
    {
        RectTransform row = MakeRow(listRoot, 40f);
        AddImage(row, CardBg, false);
        TMP_Text t = Text(row, text, 15, FontStyles.Italic, new Color(1f, 1f, 1f, 0.6f));
        RectTransform rt = (RectTransform)t.transform;
        rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(12f, 4f); rt.offsetMax = new Vector2(-12f, -4f);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        UnityEngine.Object.Destroy(t.GetComponent<LayoutElement>());
    }

    // 折り返すテキスト ＋ 右端ボタン。高さは内容で伸びる。
    private void ActionRow(string text, string buttonLabel, bool interactable, Color btnColor, Action onClick)
    {
        RectTransform row = MakeRow(listRoot, 46f);
        AddImage(row, CardBg, false);
        HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 10f; h.padding = new RectOffset(12, 12, 8, 8);
        h.childControlWidth = true; h.childControlHeight = true;
        h.childForceExpandWidth = false; h.childForceExpandHeight = false;
        h.childAlignment = TextAnchor.MiddleLeft;

        TMP_Text t = Text(row, text, 15, FontStyles.Normal, Color.white);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        LayoutElement tle = t.GetComponent<LayoutElement>();
        tle.flexibleWidth = 1f; tle.minHeight = 30f;

        GameObject btnGo = new GameObject("Btn", typeof(RectTransform));
        btnGo.transform.SetParent(row, false);
        Image bi = btnGo.AddComponent<Image>();
        bi.color = interactable ? btnColor : new Color(btnColor.r, btnColor.g, btnColor.b, 0.4f);
        Button b = btnGo.AddComponent<Button>();
        b.targetGraphic = bi;
        b.interactable = interactable;
        if (interactable && onClick != null) b.onClick.AddListener(() => onClick());
        LayoutElement ble = btnGo.AddComponent<LayoutElement>();
        ble.minWidth = 120f; ble.preferredWidth = 120f; ble.minHeight = 34f;

        GameObject lblGo = new GameObject("Label", typeof(RectTransform));
        lblGo.transform.SetParent(btnGo.transform, false);
        RectTransform lrt = (RectTransform)lblGo.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        TextMeshProUGUI lt = lblGo.AddComponent<TextMeshProUGUI>();
        if (font != null) lt.font = font;
        lt.text = buttonLabel; lt.fontSize = 15; lt.alignment = TextAlignmentOptions.Center;
        lt.color = new Color(1f, 1f, 1f, interactable ? 1f : 0.6f); lt.raycastTarget = false;
    }

    // 親のレイアウトグループ（Vertical/Horizontal）が高さ・幅を測るので、行自体には
    // ContentSizeFitter を付けない（レイアウトグループと競合するため）。最低高さだけ与える。
    private RectTransform MakeRow(RectTransform parent, float minHeight)
    {
        GameObject go = new GameObject("Row", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = minHeight;
        return (RectTransform)go.transform;
    }

    private TMP_Text Text(RectTransform parent, string text, int size, FontStyles style, Color color)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
        t.alignment = TextAlignmentOptions.MidlineLeft; t.raycastTarget = false;
        t.richText = true;
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = size + 8f;
        return t;
    }

    private static Image AddImage(RectTransform rt, Color color, bool raycast)
    {
        Image img = rt.gameObject.GetComponent<Image>();
        if (img == null) img = rt.gameObject.AddComponent<Image>();
        img.color = color; img.raycastTarget = raycast;
        return img;
    }
}
