using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 所持素材の一覧（構築画面に常設）。PlayerInventory.OnInventoryChanged を購読して再描画する。
public class InventoryPanel : MonoBehaviour
{
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private GameObject rowPrefab;
    [SerializeField] private TMP_Text emptyLabel;

    private PlayerInventory inventory;

    void Awake()
    {
        inventory = Object.FindFirstObjectByType<PlayerInventory>();
    }

    void OnEnable()
    {
        if (inventory == null) inventory = Object.FindFirstObjectByType<PlayerInventory>();
        if (inventory != null) inventory.OnInventoryChanged += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        if (inventory != null) inventory.OnInventoryChanged -= Rebuild;
    }

    private void Rebuild()
    {
        if (listRoot == null) return;

        for (int i = listRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(listRoot.GetChild(i).gameObject);
        }

        int shown = 0;
        if (inventory != null && rowPrefab != null)
        {
            foreach (KeyValuePair<string, int> kv in inventory.Counts)
            {
                if (kv.Value <= 0) continue;
                MaterialCost mc = inventory.Sample(kv.Key);
                if (mc == null) continue;

                GameObject row = Instantiate(rowPrefab, listRoot, false);
                DropRow dropRow = row.GetComponent<DropRow>();
                if (dropRow != null) dropRow.Bind(MaterialCatalog.DisplayName(mc), kv.Value, Color.white);
                shown++;
            }
        }

        if (emptyLabel != null) emptyLabel.gameObject.SetActive(shown == 0);
    }
}
