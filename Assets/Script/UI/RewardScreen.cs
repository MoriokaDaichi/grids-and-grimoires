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

    // 無料入場ぶんの入場料を戦利品から自動精算した記録（Populate で計算、OnReturn でお金に反映）。
    private int feeOwed;        // 精算対象の入場料
    private int feeRecovered;   // 自動売却で回収したゴールド
    private string feeSoldText; // 売却した素材の内訳（表示用）

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
        if (!granted)
        {
            // 無料入場ぶんの入場料をお金に反映（自動売却ぶんを足してから料金を引く＝実質は戦利品で相殺）。
            if (feeOwed > 0)
            {
                MoneyManager money = MoneyManager.Instance;
                if (money != null)
                {
                    if (feeRecovered > 0) money.Add(feeRecovered);
                    money.TrySpend(Mathf.Min(feeOwed, feeRecovered));
                }
                if (dungeon != null) dungeon.SettleEntryFee();
                Debug.Log($"[Reward] 後払いの入場料 {feeOwed}G を戦利品から精算（売却 {feeRecovered}G 分：{feeSoldText}）。");
            }

            if (pendingDrops.Count > 0)
            {
                PlayerInventory inv = PlayerInventory.Instance;
                if (inv == null) inv = Object.FindFirstObjectByType<PlayerInventory>();
                if (inv != null) inv.Add(pendingDrops);
            }
            granted = true;
        }
        if (phaseManager != null) phaseManager.ReturnToBuild();
    }

    private void Populate()
    {
        granted = false;
        pendingDrops.Clear();
        feeOwed = 0;
        feeRecovered = 0;
        feeSoldText = null;

        bool cleared = phaseManager != null && phaseManager.LastRunCleared;

        if (resultText != null)
        {
            int reached = dungeon != null ? dungeon.Depth : 0;
            resultText.text = cleared
                ? "深度 " + reached + " まで到達して脱出した！"
                : "深度 " + reached + " で力尽きた…";
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
        }

        // 無料入場したぶんの入場料は、戦利品を安い順に自動売却して差し引く。
        SettleEntryFeeFromLoot();

        foreach (MaterialCost mc in pendingDrops)
        {
            GameObject row = Instantiate(dropRowPrefab, dropListRoot, false);
            DropRow dropRow = row.GetComponent<DropRow>();
            if (dropRow != null)
            {
                dropRow.Bind(MaterialCatalog.DisplayName(mc), mc.amount, Color.white);
            }
        }

        if (feeOwed > 0)
        {
            GameObject row = Instantiate(dropRowPrefab, dropListRoot, false);
            DropRow dropRow = row.GetComponent<DropRow>();
            if (dropRow != null)
            {
                string label = string.IsNullOrEmpty(feeSoldText)
                    ? "入場料の精算（後払い " + feeOwed + "G）"
                    : "入場料の精算：" + feeSoldText + " を売却（-" + feeOwed + "G）";
                dropRow.BindNote(label, new Color(1f, 0.75f, 0.55f, 1f));
            }
        }
    }

    // 無料入場ぶんの入場料（dungeon.EntryFeeOwed）を、pendingDrops から安い順に1個ずつ
    // 自動売却して回収する。pendingDrops を直接減らし、回収額・売却内訳を記録する。
    private void SettleEntryFeeFromLoot()
    {
        if (dungeon == null) return;
        int owed = dungeon.EntryFeeOwed;
        if (owed <= 0 || pendingDrops.Count == 0) return;

        Dictionary<string, int> soldCounts = new Dictionary<string, int>();
        Dictionary<string, MaterialCost> soldSample = new Dictionary<string, MaterialCost>();
        int recovered = 0;

        while (recovered < owed)
        {
            int pick = -1;
            int pickUnit = int.MaxValue;
            for (int i = 0; i < pendingDrops.Count; i++)
            {
                if (pendingDrops[i].amount <= 0) continue;
                int unit = Mathf.Max(1, MaterialCatalog.GoldValue(pendingDrops[i]));
                if (unit < pickUnit) { pickUnit = unit; pick = i; }
            }
            if (pick < 0) break; // 売るものが尽きた

            MaterialCost mc = pendingDrops[pick];
            mc.amount -= 1;
            recovered += pickUnit;

            string key = MaterialCatalog.Key(mc);
            soldCounts.TryGetValue(key, out int c);
            soldCounts[key] = c + 1;
            if (!soldSample.ContainsKey(key)) soldSample[key] = mc;
        }

        pendingDrops.RemoveAll(d => d.amount <= 0);

        feeOwed = owed;
        feeRecovered = recovered;

        List<string> parts = new List<string>();
        foreach (KeyValuePair<string, int> kv in soldCounts)
            parts.Add(MaterialCatalog.DisplayName(soldSample[kv.Key]) + " ×" + kv.Value);
        feeSoldText = parts.Count > 0 ? string.Join("、", parts) : null;
    }
}
