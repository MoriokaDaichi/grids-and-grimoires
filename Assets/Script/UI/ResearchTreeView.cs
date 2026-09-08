using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 放射状スキルツリーの表示・操作。ResearchGraph のノード定義から実行時にノードとエッジを生成し、
// ドラッグでパン／ホイールでズーム、ノードクリックで詳細表示、取得ボタンで割り当てる。
// このコンポーネントは Viewport（マスクされた表示領域）に付く。
public class ResearchTreeView : MonoBehaviour, IDragHandler, IScrollHandler
{
    [SerializeField] private RectTransform content;    // パン／ズームするレイヤ（Viewportの子）
    [SerializeField] private RectTransform edgeLayer;  // content の子。エッジ用
    [SerializeField] private RectTransform nodeLayer;  // content の子。ノード用
    [SerializeField] private GameObject nodeWidgetPrefab;

    [SerializeField] private TMP_Text detailTitle;
    [SerializeField] private TMP_Text detailBody;
    [SerializeField] private Button allocateButton;
    [SerializeField] private TMP_Text allocateLabel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text legendText;

    private ResearchManager research;
    private PlayerInventory inventory;
    private GamePhaseManager phaseManager;

    private readonly Dictionary<string, ResearchNodeWidget> widgets = new Dictionary<string, ResearchNodeWidget>();
    private string selectedId;
    private bool built;

    private const float MinZoom = 0.08f;
    private const float MaxZoom = 1.3f;

    void Awake()
    {
        research = Object.FindFirstObjectByType<ResearchManager>();
        inventory = Object.FindFirstObjectByType<PlayerInventory>();
        phaseManager = Object.FindFirstObjectByType<GamePhaseManager>();

        if (allocateButton != null) allocateButton.onClick.AddListener(OnAllocateClicked);
        if (closeButton != null) closeButton.onClick.AddListener(OnClose);
    }

    void OnEnable()
    {
        if (research != null) research.OnUnlocksChanged += RefreshAll;
        if (inventory != null) inventory.OnInventoryChanged += RefreshAll;

        BuildOnce();
        if (content != null)
        {
            // 円盤が半径 ≈2680 に収まるので、開いた時点でノードのラベルが読める倍率にする。
            content.localScale = Vector3.one * 0.22f;
            content.anchoredPosition = Vector2.zero;
        }
        if (legendText != null)
            legendText.text = "金=取得可（クリックで選択、もう一度クリックで取得） / 緑=取得済 / 灰=前提なし    ドラッグで移動・ホイールで拡大縮小";
        Select(null);
        RefreshAll();
    }

    void OnDisable()
    {
        if (research != null) research.OnUnlocksChanged -= RefreshAll;
        if (inventory != null) inventory.OnInventoryChanged -= RefreshAll;
    }

    // ---------------------------------------------------------------- 生成

    private void BuildOnce()
    {
        if (built || nodeWidgetPrefab == null || nodeLayer == null) return;
        built = true;

        foreach (ResearchNodeDef def in ResearchGraph.Nodes)
        {
            GameObject go = Instantiate(nodeWidgetPrefab, nodeLayer, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = ResearchGraph.Position(def);

            ResearchNodeWidget w = go.GetComponent<ResearchNodeWidget>();
            if (w == null) continue;
            w.Setup(def.id, research != null ? research.NodeShortLabel(def.id) : def.id, def.isMagic, OnNodeClicked);
            widgets[def.id] = w;
        }

        if (edgeLayer != null)
        {
            foreach (ResearchNodeDef def in ResearchGraph.Nodes)
            {
                if (def.parentId == null) continue;
                ResearchNodeDef parent = ResearchGraph.Get(def.parentId);
                if (parent == null) continue;
                CreateEdge(ResearchGraph.Position(parent), ResearchGraph.Position(def));
            }
        }
    }

    private void CreateEdge(Vector2 from, Vector2 to)
    {
        GameObject go = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(edgeLayer, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Vector2 dir = to - from;
        rt.anchoredPosition = from + dir * 0.5f;
        rt.sizeDelta = new Vector2(dir.magnitude, 11f);
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        Image img = go.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.22f);
        img.raycastTarget = false;
        rt.SetAsFirstSibling();
    }

    // ---------------------------------------------------------------- 操作

    private void OnNodeClicked(string id)
    {
        // 1回目のクリックで選択＋詳細表示、選択済みの取得可能ノードをもう一度クリックで取得。
        if (id == selectedId && research != null && research.CanAllocate(id))
        {
            research.Allocate(id); // 成功すれば OnUnlocksChanged → RefreshAll
            return;
        }
        Select(id);
    }

    private void Select(string id)
    {
        selectedId = id;
        RefreshDetail();
        RefreshStates();
    }

    private void OnAllocateClicked()
    {
        if (selectedId == null || research == null) return;
        research.Allocate(selectedId); // 成功すれば OnUnlocksChanged → RefreshAll
    }

    private void OnClose()
    {
        if (phaseManager != null) phaseManager.CloseResearch();
    }

    private void RefreshAll()
    {
        RefreshStates();
        RefreshDetail();
    }

    private void RefreshStates()
    {
        foreach (KeyValuePair<string, ResearchNodeWidget> kv in widgets)
        {
            if (kv.Value == null || research == null) continue;
            kv.Value.SetState(research.NodeState(kv.Key), kv.Key == selectedId);
        }
    }

    private void RefreshDetail()
    {
        if (research == null) return;

        if (selectedId == null)
        {
            if (detailTitle != null) detailTitle.text = "ノードを選択";
            if (detailBody != null) detailBody.text = "";
            if (allocateButton != null) allocateButton.interactable = false;
            if (allocateLabel != null) allocateLabel.text = "取得";
            return;
        }

        ResearchNodeState state = research.NodeState(selectedId);
        if (detailTitle != null) detailTitle.text = research.NodeTitle(selectedId);
        if (detailBody != null)
        {
            string stateText = state == ResearchNodeState.Allocated ? "取得済み"
                : state == ResearchNodeState.Allocatable ? "取得できます（もう一度クリック、または下の[取得]ボタン）"
                : "前提ノードが未取得、または素材が足りません";
            detailBody.text = research.NodeDetail(selectedId)
                + "\n\nコスト: " + research.NodeCostText(selectedId)
                + "\n\n" + stateText;
        }
        if (allocateButton != null) allocateButton.interactable = research.CanAllocate(selectedId);
        if (allocateLabel != null) allocateLabel.text = state == ResearchNodeState.Allocated ? "取得済" : "取得";
    }

    // ---------------------------------------------------------------- パン／ズーム

    public void OnDrag(PointerEventData eventData)
    {
        if (content == null) return;
        content.anchoredPosition += eventData.delta;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (content == null) return;
        float factor = 1f + Mathf.Clamp(eventData.scrollDelta.y, -3f, 3f) * 0.08f;
        float z = Mathf.Clamp(content.localScale.x * factor, MinZoom, MaxZoom);
        content.localScale = new Vector3(z, z, 1f);
    }
}
