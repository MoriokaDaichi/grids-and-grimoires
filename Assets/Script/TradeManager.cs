using System;
using System.Collections.Generic;
using UnityEngine;

// 複数トレーダーとの取引・タスクを管理する。シーンにシングルトンで1つ。
// トレーダー定義は TraderCatalog（専門分野ごとの交換メニュー＋タスク）。
// 実消費・付与は PlayerInventory / PlayerStatus 経由。タスク進捗は SaveData に永続化。
public class TradeManager : MonoBehaviour
{
    public static TradeManager Instance { get; private set; }

    public Action OnTraded;
    public Action OnTasksChanged;

    private readonly List<Trader> traders = TraderCatalog.BuildTraders();
    public IReadOnlyList<Trader> Traders { get { return traders; } }

    // 全トレーダーの交換メニューを平坦化（旧APIの互換用）
    public IEnumerable<TradeOffer> Offers
    {
        get
        {
            foreach (Trader t in traders)
                foreach (TradeOffer o in t.offers)
                    yield return o;
        }
    }

    // --- タスク進捗（SaveData に永続化）---
    private readonly HashSet<string> completedTaskIds = new HashSet<string>();
    private readonly Dictionary<string, int> killsByName = new Dictionary<string, int>();
    private int lifetimeKills;
    private int bestDepth;

    private DungeonManager dungeon;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        dungeon = UnityEngine.Object.FindFirstObjectByType<DungeonManager>();
        LoadProgress();
    }

    void OnEnable()
    {
        if (dungeon != null)
        {
            dungeon.OnEnemyDefeated += HandleEnemyDefeated;
            dungeon.OnWaveChanged += HandleWaveChanged;
        }
    }

    void OnDisable()
    {
        if (dungeon != null)
        {
            dungeon.OnEnemyDefeated -= HandleEnemyDefeated;
            dungeon.OnWaveChanged -= HandleWaveChanged;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- 交換

    public bool CanTrade(TradeOffer offer)
    {
        if (offer == null) return false;
        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null || !inv.CanAfford(offer.give)) return false;
        // お金が絡むオファーは MoneyManager がシーンに無ければ成立させない（払い/受取が消える事故を防ぐ）
        if (offer.giveMoney > 0 || offer.gainMoney > 0)
        {
            MoneyManager money = MoneyManager.Instance;
            if (money == null) return false;
            if (offer.giveMoney > 0 && !money.CanAfford(offer.giveMoney)) return false;
        }
        return true;
    }

    public bool TryTrade(TradeOffer offer)
    {
        if (!CanTrade(offer)) return false;

        PlayerInventory inv = PlayerInventory.Instance;
        MoneyManager money = MoneyManager.Instance;

        // 素材とお金の両方が払えることを確かめてから消費する
        if (offer.giveMoney > 0 && (money == null || !money.TrySpend(offer.giveMoney))) return false;
        if (!inv.TrySpend(offer.give))
        {
            if (offer.giveMoney > 0 && money != null) money.Add(offer.giveMoney); // 返金
            return false;
        }

        if (offer.receive != null && offer.receive.Count > 0) inv.Add(offer.receive);
        if (offer.gainMoney > 0 && money != null) money.Add(offer.gainMoney);

        if (offer.bonusStatPoints > 0)
        {
            PlayerStatus ps = UnityEngine.Object.FindFirstObjectByType<PlayerStatus>();
            if (ps != null) ps.AddStatsPoint(offer.bonusStatPoints);
        }

        OnTraded?.Invoke();
        Debug.Log($"[トレード] {offer.label} を交換した。");
        return true;
    }

    // ---------------------------------------------------------------- タスク

    public TaskProgress BuildProgress()
    {
        return new TaskProgress
        {
            enemiesDefeated = lifetimeKills,
            bestDepth = bestDepth,
            money = MoneyManager.Instance != null ? MoneyManager.Instance.Balance : 0,
            enemyKills = KillsOf,
            inventoryCount = InventoryCountOf,
            ownedGearTier = OwnedGearTierOf,
        };
    }

    // 所持中の装備のうち、そのスロットの最上位 tier（無ければ 0）。CraftGear タスクの判定用。
    // 装備所持は HideoutManager から都度導出する（在庫数と同じく永続化はしない）。
    private static int OwnedGearTierOf(GearSlot slot)
    {
        HideoutManager h = HideoutManager.Instance;
        if (h == null) return 0;
        int best = 0;
        foreach (string id in h.OwnedGear)
        {
            GearDef g = GearCatalog.Get(id);
            if (g != null && g.slot == slot && g.tier > best) best = g.tier;
        }
        return best;
    }

    // 前提タスク（requires）が無い、または達成済みなら解放されている。
    public bool IsTaskUnlocked(TraderTask task)
    {
        if (task == null) return false;
        return string.IsNullOrEmpty(task.requires) || completedTaskIds.Contains(task.requires);
    }

    // 画面に出すタスク: 解放済みは全部＋「次の未解放（最初の1件）」だけ。連鎖の先が見えるように。
    public List<TraderTask> VisibleTasks(Trader trader)
    {
        List<TraderTask> outp = new List<TraderTask>();
        if (trader == null) return outp;
        bool shownNextLocked = false;
        foreach (TraderTask t in trader.tasks)
        {
            if (IsTaskUnlocked(t)) { outp.Add(t); continue; }
            if (!shownNextLocked) { outp.Add(t); shownNextLocked = true; }
        }
        return outp;
    }

    public TraderTask FindTask(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (Trader tr in traders)
            foreach (TraderTask t in tr.tasks)
                if (t.id == id) return t;
        return null;
    }

    private int KillsOf(string enemyName)
    {
        if (string.IsNullOrEmpty(enemyName)) return 0;
        return killsByName.TryGetValue(enemyName, out int n) ? n : 0;
    }

    private static int InventoryCountOf(MaterialCost c)
    {
        PlayerInventory inv = PlayerInventory.Instance;
        return inv != null && c != null ? inv.GetCount(c) : 0;
    }

    public bool IsTaskCompleted(TraderTask task)
    {
        return task != null && completedTaskIds.Contains(task.id);
    }

    public string TaskProgressText(TraderTask task)
    {
        return TaskRules.ProgressText(task, BuildProgress());
    }

    public bool CanClaim(TraderTask task)
    {
        if (task == null || completedTaskIds.Contains(task.id)) return false;
        if (!IsTaskUnlocked(task)) return false;
        return TaskRules.IsComplete(task, BuildProgress());
    }

    public bool ClaimTask(TraderTask task)
    {
        if (!CanClaim(task)) return false;

        PlayerInventory inv = PlayerInventory.Instance;
        MoneyManager money = MoneyManager.Instance;

        // 納品タスクは受取と引き換えに素材（＋納金）を消費する。両方払えることを先に確認する。
        if (task.kind == TraderTaskKind.DeliverItems)
        {
            if (inv == null || !inv.CanAfford(task.deliverItems)) return false;
            if (task.deliverMoney > 0 && (money == null || !money.CanAfford(task.deliverMoney))) return false;

            if (!inv.TrySpend(task.deliverItems)) return false;
            if (task.deliverMoney > 0 && money != null && !money.TrySpend(task.deliverMoney))
            {
                inv.Add(task.deliverItems); // 返却
                return false;
            }
        }

        if (inv != null && task.rewardItems != null && task.rewardItems.Count > 0) inv.Add(task.rewardItems);
        if (task.rewardMoney > 0 && money != null) money.Add(task.rewardMoney);

        if (task.rewardStatPoints > 0)
        {
            PlayerStatus ps = UnityEngine.Object.FindFirstObjectByType<PlayerStatus>();
            if (ps != null) ps.AddStatsPoint(task.rewardStatPoints);
        }

        if (!string.IsNullOrEmpty(task.rewardRecipeId) && HideoutManager.Instance != null)
            HideoutManager.Instance.UnlockRecipe(task.rewardRecipeId);
        if (!string.IsNullOrEmpty(task.rewardGearId) && HideoutManager.Instance != null)
            HideoutManager.Instance.GrantGear(task.rewardGearId);

        completedTaskIds.Add(task.id);
        SaveProgress();
        OnTasksChanged?.Invoke();
        Debug.Log($"[タスク] 「{task.title}」を達成した。");
        return true;
    }

    // ---------------------------------------------------------------- 進捗フック

    private void HandleEnemyDefeated(EnemyData enemy)
    {
        if (enemy == null) return;
        lifetimeKills++;
        string name = enemy.enemyName;
        killsByName[name] = (killsByName.TryGetValue(name, out int n) ? n : 0) + 1;
        SaveProgress();
        OnTasksChanged?.Invoke();
    }

    private void HandleWaveChanged(int depth, int total, string label)
    {
        if (depth <= bestDepth) return;
        bestDepth = depth;
        SaveProgress();
        OnTasksChanged?.Invoke();
    }

    // ---------------------------------------------------------------- 永続化

    private void LoadProgress()
    {
        SaveData data = SaveManager.Load();
        completedTaskIds.Clear();
        foreach (string id in data.completedTaskIds)
            if (!string.IsNullOrEmpty(id)) completedTaskIds.Add(id);

        killsByName.Clear();
        foreach (StringIntPair kv in data.enemyKillCounts)
            if (kv != null && !string.IsNullOrEmpty(kv.key)) killsByName[kv.key] = kv.value;

        lifetimeKills = data.lifetimeEnemyKills;
        bestDepth = data.bestDungeonDepth;
    }

    private void SaveProgress()
    {
        SaveData data = SaveManager.Load();
        data.completedTaskIds = new List<string>(completedTaskIds);
        data.lifetimeEnemyKills = lifetimeKills;
        data.bestDungeonDepth = bestDepth;

        data.enemyKillCounts = new List<StringIntPair>();
        foreach (KeyValuePair<string, int> kv in killsByName)
            data.enemyKillCounts.Add(new StringIntPair { key = kv.Key, value = kv.Value });

        SaveManager.Save(data);
    }
}
