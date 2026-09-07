using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ウェーブ突破画面。到達深度とここまでの戦利品（脱出時に得られる分）を表示し、
// 「深層へ進む」か「脱出する」かを選ばせる。
public class WaveClearPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text depthText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private RectTransform lootListRoot;
    [SerializeField] private GameObject dropRowPrefab;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button escapeButton;

    private DungeonManager dungeon;
    private GamePhaseManager phaseManager;
    private PlayerStatus player;

    void Awake()
    {
        dungeon = Object.FindFirstObjectByType<DungeonManager>();
        phaseManager = Object.FindFirstObjectByType<GamePhaseManager>();
        player = Object.FindFirstObjectByType<PlayerStatus>();
        if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
        if (escapeButton != null) escapeButton.onClick.AddListener(OnEscape);
    }

    void OnEnable()
    {
        Populate();
    }

    private void OnContinue()
    {
        if (phaseManager != null) phaseManager.ContinueRun();
    }

    private void OnEscape()
    {
        if (phaseManager != null) phaseManager.EscapeRun();
    }

    private void Populate()
    {
        if (dungeon == null) return;

        if (depthText != null) depthText.text = "深度 " + dungeon.Depth + " 突破";

        if (hintText != null)
        {
            string hp = player != null ? Mathf.Max(0, player.currentHp) + "/" + player.hp : "?";
            hintText.text = "進むほど敵は強大に。脱出すれば戦利品を持ち帰れる。（残HP " + hp + "）";
        }

        if (lootListRoot != null)
        {
            for (int i = lootListRoot.childCount - 1; i >= 0; i--)
                Destroy(lootListRoot.GetChild(i).gameObject);
        }
        if (dropRowPrefab == null || lootListRoot == null) return;

        Dictionary<string, int> totals = new Dictionary<string, int>();
        Dictionary<string, MaterialCost> sample = new Dictionary<string, MaterialCost>();
        foreach (EnemyData enemy in dungeon.DefeatedEnemies)
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
            GameObject row = Instantiate(dropRowPrefab, lootListRoot, false);
            DropRow dropRow = row.GetComponent<DropRow>();
            if (dropRow != null) dropRow.Bind(MaterialCatalog.DisplayName(sample[kv.Key]), kv.Value, Color.white);
        }
    }
}
