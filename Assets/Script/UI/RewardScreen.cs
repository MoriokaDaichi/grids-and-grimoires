using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 報酬画面。有効化されると結果テキストとドロップ一覧を表示する。
// Phase 1 は表示のみ（素材の付与・保存は Phase 2 でここに追加する）。
public class RewardScreen : MonoBehaviour
{
    [SerializeField] private RectTransform dropListRoot;
    [SerializeField] private GameObject dropRowPrefab;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button returnButton;

    private DungeonManager dungeon;
    private GamePhaseManager phaseManager;

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
        if (phaseManager != null) phaseManager.ReturnToBuild();
    }

    private void Populate()
    {
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

        // 全エンカウントのドロップを種別ごとに集計（Phase 2 で「実際に倒した敵」に限定する）
        Dictionary<string, int> totals = new Dictionary<string, int>();
        Dictionary<string, MaterialCost> sample = new Dictionary<string, MaterialCost>();

        foreach (EnemyData enemy in dungeon.encounters)
        {
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
            GameObject row = Instantiate(dropRowPrefab, dropListRoot, false);
            DropRow dropRow = row.GetComponent<DropRow>();
            if (dropRow != null)
            {
                dropRow.Bind(MaterialCatalog.DisplayName(sample[kv.Key]), kv.Value, Color.white);
            }
        }
    }
}
