using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 報酬画面。有効化されると結果テキストと「実際に倒した敵」のドロップ集計を表示する。
// 「帰還」ボタン押下時に PlayerInventory へ付与しセーブする（多重付与しないようガードあり）。
public class RewardScreen : MonoBehaviour
{
    [SerializeField] private RectTransform dropListRoot;
    [SerializeField] private GameObject dropRowPrefab;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button returnButton;

    private DungeonManager dungeon;
    private GamePhaseManager phaseManager;

    private readonly List<MaterialCost> pendingDrops = new List<MaterialCost>();
    private bool granted;

    void Awake()
    {
        dungeon = Object.FindFirstObjectByType<DungeonManager>();
        phaseManager = Object.FindFirstObjectByType<GamePhaseManager>();
        if (returnButton != null) returnButton.onClick.AddListener(OnReturn);
    }

    void OnEnable()
    {
        Populate();
    }

    private void OnReturn()
    {
        if (!granted && pendingDrops.Count > 0)
        {
            PlayerInventory inv = PlayerInventory.Instance;
            if (inv == null) inv = Object.FindFirstObjectByType<PlayerInventory>();
            if (inv != null) inv.Add(pendingDrops);
            granted = true;
        }
        if (phaseManager != null) phaseManager.ReturnToBuild();
    }

    private void Populate()
    {
        granted = false;
        pendingDrops.Clear();

        bool cleared = phaseManager != null && phaseManager.LastRunCleared;

        if (resultText != null)
        {
            resultText.text = cleared ? "ダンジョンクリア！" : "ダンジョンから撤退した…";
        }

        if (dropListRoot != null)
        {
            for (int i = dropListRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(dropListRoot.GetChild(i).gameObject);
            }
        }

        if (dungeon == null || dropRowPrefab == null || dropListRoot == null) return;

        bool grant = cleared || dungeon.grantLootOnFailure;
        if (!grant) return;

        // 実際に倒した敵のドロップだけを種別ごとに集計
        Dictionary<string, int> totals = new Dictionary<string, int>();
        Dictionary<string, MaterialCost> sample = new Dictionary<string, MaterialCost>();

        IReadOnlyList<EnemyData> defeated = dungeon.DefeatedEnemies;
        for (int i = 0; i < defeated.Count; i++)
        {
            EnemyData enemy = defeated[i];
            if (enemy == null || enemy.drops == null) continue;
            foreach (MaterialCost drop in enemy.drops)
            {
                if (drop == null) continue;
                string key = MaterialCatalog.Key(drop);
                totals.TryGetValue(key, out int current);
                totals[key] = current + drop.amount;
                if (!sample.ContainsKey(key)) sample[key] = drop;
            }
        }

        foreach (KeyValuePair<string, int> kv in totals)
        {
            MaterialCost mc = sample[kv.Key];
            pendingDrops.Add(new MaterialCost
            {
                materialType = mc.materialType,
                attribute = mc.attribute,
                specialItemName = mc.specialItemName,
                amount = kv.Value,
            });

            GameObject row = Instantiate(dropRowPrefab, dropListRoot, false);
            DropRow dropRow = row.GetComponent<DropRow>();
            if (dropRow != null)
            {
                dropRow.Bind(MaterialCatalog.DisplayName(mc), kv.Value, Color.white);
            }
        }
    }
}
