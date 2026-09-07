using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 研究(隠れ家)画面。研究対象の魔法を一覧し、素材を払って解放する。
// v1 はフラットなリスト（企画書の巨大分岐ツリーは後続）。
public class HideoutPanel : MonoBehaviour
{
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private GameObject entryPrefab;
    [SerializeField] private Button closeButton;

    private ResearchManager research;
    private GamePhaseManager phaseManager;
    private PlayerInventory inventory;

    void Awake()
    {
        research = Object.FindFirstObjectByType<ResearchManager>();
        phaseManager = Object.FindFirstObjectByType<GamePhaseManager>();
        inventory = Object.FindFirstObjectByType<PlayerInventory>();
        if (closeButton != null) closeButton.onClick.AddListener(OnClose);
    }

    void OnEnable()
    {
        if (research != null) research.OnUnlocksChanged += Rebuild;
        if (inventory != null) inventory.OnInventoryChanged += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        if (research != null) research.OnUnlocksChanged -= Rebuild;
        if (inventory != null) inventory.OnInventoryChanged -= Rebuild;
    }

    private void OnClose()
    {
        if (phaseManager != null) phaseManager.ReturnToBuild();
    }

    private void Rebuild()
    {
        if (listRoot == null || entryPrefab == null || research == null) return;

        for (int i = listRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(listRoot.GetChild(i).gameObject);
        }

        foreach (MagicData md in research.Researchable())
        {
            GameObject row = Instantiate(entryPrefab, listRoot, false);
            HideoutEntry entry = row.GetComponent<HideoutEntry>();
            if (entry == null) continue;

            MagicData captured = md;
            entry.Bind(
                md.magicName,
                CostText(md),
                research.IsUnlocked(md),
                research.CanUnlock(md),
                () => research.Unlock(captured));
        }
    }

    private static string CostText(MagicData md)
    {
        if (md.requiredMaterials == null || md.requiredMaterials.Count == 0) return "-";
        List<string> parts = new List<string>();
        foreach (MaterialCost c in md.requiredMaterials)
        {
            if (c == null) continue;
            parts.Add(MaterialCatalog.DisplayName(c) + " ×" + c.amount);
        }
        return string.Join(" / ", parts);
    }
}
