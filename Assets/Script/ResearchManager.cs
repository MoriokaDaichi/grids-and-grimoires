using System;
using System.Collections.Generic;
using UnityEngine;

// 研究スキルツリー（放射状）の割り当てを管理する。シーンにシングルトンで1つ。
// ノードは大＝魔法（id は MagicData 名）と小＝ステータス系（id は "node_..."）。
// 親ノードが割り当て済みかつ素材を賄えるノードだけ割り当てられる（ResearchGraph / ResearchRules）。
// 小ノードは割り当て時に PlayerStatus へ恒久ボーナスを加算する。
public class ResearchManager : MonoBehaviour
{
    public static ResearchManager Instance { get; private set; }

    public Action OnUnlocksChanged;

    private readonly HashSet<string> allocated = new HashSet<string>();
    private readonly Dictionary<string, MagicData> byId = new Dictionary<string, MagicData>();
    private List<MagicData> allMagic = new List<MagicData>();
    private PlayerStatus playerStatus;
    private bool statsApplied;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        MagicSpawner spawner = UnityEngine.Object.FindFirstObjectByType<MagicSpawner>();
        if (spawner != null && spawner.magicDataList != null) allMagic = spawner.magicDataList;

        byId.Clear();
        foreach (MagicData md in allMagic)
            if (md != null && !byId.ContainsKey(md.name)) byId[md.name] = md;

        allocated.Clear();
        SaveData data = SaveManager.Load();
        List<string> stored = data.allocatedResearchNodes != null && data.allocatedResearchNodes.Count > 0
            ? data.allocatedResearchNodes
            : data.unlockedMagicIds; // 旧セーブからの移行
        if (stored != null)
        {
            foreach (string id in stored)
                if (!string.IsNullOrEmpty(id)) allocated.Add(id);
        }
    }

    void Start()
    {
        playerStatus = UnityEngine.Object.FindFirstObjectByType<PlayerStatus>();
        ApplyAllStatNodes();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 割り当て済みの小ノードぶんのボーナスを PlayerStatus へ一括反映（起動時に1回）
    private void ApplyAllStatNodes()
    {
        if (statsApplied || playerStatus == null) return;
        statsApplied = true;
        foreach (string id in allocated)
        {
            ResearchNodeDef def = ResearchGraph.Get(id);
            if (def != null && !def.isMagic) playerStatus.ApplyResearchDelta(def.stat, def.statAmount);
        }
    }

    // ---------------------------------------------------------------- 参照

    public bool IsUnlocked(MagicData md)
    {
        return ResearchRules.IsUnlocked(md, allocated);
    }

    public bool IsIdAllocated(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (allocated.Contains(id)) return true;
        MagicData md;
        return byId.TryGetValue(id, out md) && ResearchRules.IsBaseFree(md); // 根の初期解放魔法
    }

    public ResearchNodeState NodeState(string id)
    {
        ResearchNodeDef def = ResearchGraph.Get(id);
        if (def == null) return ResearchNodeState.Locked;
        if (IsIdAllocated(id)) return ResearchNodeState.Allocated;
        if (def.parentId != null && !IsIdAllocated(def.parentId)) return ResearchNodeState.Locked;
        return CanAllocate(id) ? ResearchNodeState.Allocatable : ResearchNodeState.Locked;
    }

    public bool CanAllocate(string id)
    {
        ResearchNodeDef def = ResearchGraph.Get(id);
        if (def == null || IsIdAllocated(id)) return false;
        if (def.parentId != null && !IsIdAllocated(def.parentId)) return false;

        PlayerInventory inv = PlayerInventory.Instance;
        return inv != null && inv.CanAfford(CostOf(def));
    }

    public bool Allocate(string id)
    {
        if (!CanAllocate(id)) return false;
        ResearchNodeDef def = ResearchGraph.Get(id);

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv != null && !inv.TrySpend(CostOf(def))) return false;

        allocated.Add(id);

        if (!def.isMagic && playerStatus != null)
            playerStatus.ApplyResearchDelta(def.stat, def.statAmount);

        Save();
        OnUnlocksChanged?.Invoke();
        Debug.Log($"[研究] ノード「{NodeTitle(id)}」を取得した。");
        return true;
    }

    // ---------------------------------------------------------------- 表示ヘルパー（ビュー用）

    public string NodeShortLabel(string id)
    {
        ResearchNodeDef def = ResearchGraph.Get(id);
        if (def == null) return id;
        if (!def.isMagic) return def.shortLabel;
        MagicData md;
        if (byId.TryGetValue(id, out md) && md != null)
        {
            string n = md.magicName;
            return n.Length > 5 ? n.Substring(0, 5) : n;
        }
        return id;
    }

    public string NodeTitle(string id)
    {
        ResearchNodeDef def = ResearchGraph.Get(id);
        if (def == null) return id;
        if (!def.isMagic) return def.title;
        MagicData md;
        return byId.TryGetValue(id, out md) && md != null ? md.magicName : id;
    }

    public string NodeDetail(string id)
    {
        ResearchNodeDef def = ResearchGraph.Get(id);
        if (def == null) return "";
        if (!def.isMagic) return def.title;
        MagicData md;
        if (byId.TryGetValue(id, out md) && md != null)
            return string.IsNullOrEmpty(md.effectDescription) ? md.category.ToString() : md.effectDescription;
        return "";
    }

    public string NodeCostText(string id)
    {
        ResearchNodeDef def = ResearchGraph.Get(id);
        if (def == null) return "-";
        List<MaterialCost> cost = CostOf(def);
        if (cost == null || cost.Count == 0) return "初期解放";
        List<string> parts = new List<string>();
        foreach (MaterialCost c in cost)
            if (c != null) parts.Add(MaterialCatalog.DisplayName(c) + " ×" + c.amount);
        return string.Join(" / ", parts);
    }

    public bool IsMagicNode(string id)
    {
        ResearchNodeDef def = ResearchGraph.Get(id);
        return def != null && def.isMagic;
    }

    private List<MaterialCost> CostOf(ResearchNodeDef def)
    {
        if (def == null) return null;
        if (!def.isMagic) return def.cost;
        MagicData md;
        return byId.TryGetValue(def.id, out md) && md != null ? md.requiredMaterials : new List<MaterialCost>();
    }

    // ---------------------------------------------------------------- 旧APIの互換

    public bool IsIdUnlocked(string id) { return IsIdAllocated(id); }

    public List<MagicData> Researchable()
    {
        List<MagicData> list = new List<MagicData>();
        foreach (MagicData md in allMagic)
            if (md != null && !ResearchRules.IsBaseFree(md)) list.Add(md);
        list.Sort(delegate (MagicData a, MagicData b)
        {
            int c = ResearchGraph.Depth(a.name).CompareTo(ResearchGraph.Depth(b.name));
            if (c != 0) return c;
            return string.CompareOrdinal(a.name, b.name);
        });
        return list;
    }

    public bool PrerequisiteMet(MagicData md) { return ResearchRules.PrerequisiteMet(md, IsIdAllocated); }
    public bool CanUnlock(MagicData md) { return md != null && CanAllocate(md.name); }
    public bool Unlock(MagicData md) { return md != null && Allocate(md.name); }

    // ---------------------------------------------------------------- 永続化

    private void Save()
    {
        SaveData data = SaveManager.Load();
        data.allocatedResearchNodes = new List<string>(allocated);

        // 魔法だけの一覧も同期（他システムの互換用）
        List<string> magicIds = new List<string>();
        foreach (string id in allocated)
            if (byId.ContainsKey(id)) magicIds.Add(id);
        data.unlockedMagicIds = magicIds;

        SaveManager.Save(data);
    }
}
