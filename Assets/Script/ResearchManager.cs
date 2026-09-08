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

    // 研究机レベルによる小ノードのボーナス倍率（未建造・机なしなら等倍）
    private float BonusMult
    {
        get { return HideoutManager.Instance != null ? HideoutManager.Instance.ResearchBonusMult : 1f; }
    }

    // 研究机が建っていて研究可能か（ハイドアウトがシーンに無ければ常に可）
    public bool ResearchEnabled
    {
        get { return HideoutManager.Instance == null || HideoutManager.Instance.ResearchUnlocked; }
    }

    // 割り当て済みの小ノードぶんのボーナスを PlayerStatus へ一括反映（起動時に1回）
    private void ApplyAllStatNodes()
    {
        if (statsApplied || playerStatus == null) return;
        statsApplied = true;
        float mult = BonusMult;
        foreach (string id in allocated)
        {
            ResearchNodeDef def = ResearchGraph.Get(id);
            if (def != null && !def.isMagic) playerStatus.ApplyResearchDelta(def.stat, def.statAmount * mult);
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
        if (!ResearchEnabled) return false; // 研究机が未建造
        ResearchNodeDef def = ResearchGraph.Get(id);
        if (def == null || IsIdAllocated(id)) return false;
        if (def.parentId != null && !IsIdAllocated(def.parentId)) return false;

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null || !inv.CanAfford(EffectiveCost(def))) return false;

        // 魔力炉の電力（ハイドアウトがある場合のみ）
        if (HideoutManager.Instance != null && !HideoutManager.Instance.CanPowerFacilityAction()) return false;
        return true;
    }

    public bool Allocate(string id)
    {
        if (!CanAllocate(id)) return false;
        ResearchNodeDef def = ResearchGraph.Get(id);

        // 魔力炉の電力を消費（不足なら中止）
        if (HideoutManager.Instance != null && !HideoutManager.Instance.TryConsumeResearchPower()) return false;

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv != null && !inv.TrySpend(EffectiveCost(def))) return false;

        allocated.Add(id);

        if (!def.isMagic && playerStatus != null)
            playerStatus.ApplyResearchDelta(def.stat, def.statAmount * BonusMult);

        Save();
        OnUnlocksChanged?.Invoke();
        Debug.Log($"[研究] ノード「{NodeTitle(id)}」を取得した。");
        return true;
    }

    // 研究机レベルでスケール済みのコスト
    private List<MaterialCost> EffectiveCost(ResearchNodeDef def)
    {
        List<MaterialCost> raw = CostOf(def);
        return HideoutManager.Instance != null ? HideoutManager.Instance.ScaleResearchCost(raw) : raw;
    }

    // ---------------------------------------------------------------- 表示ヘルパー（ビュー用）

    // ノードに載せる短いラベル。大ノードは魔法名の切り出しではなく、系統が一目で分かる
    // コンパクトなタグを生成する（フォントアトラス未収録の漢字も避けられる）。
    public string NodeShortLabel(string id)
    {
        ResearchNodeDef def = ResearchGraph.Get(id);
        if (def == null) return id;
        if (!def.isMagic) return def.shortLabel;

        MagicData md;
        if (!byId.TryGetValue(id, out md) || md == null) return id;

        string attr = AttrKanji(md.attribute);
        string tier = id.StartsWith("Giga") ? "III" : id.StartsWith("Mega") ? "II" : "I";

        switch (md.category)
        {
            case MagicCategory.Attack:
                return attr + (md.range == MagicRange.AoE ? "全" : "") + tier;
            case MagicCategory.StatusInflict:
                return attr + "異常";
            case MagicCategory.BuffActive:
                return attr + "バフ";
            case MagicCategory.BuffPassive:
                if (id.Contains("PassiveLv")) return attr + "P" + id.Substring(id.Length - 1);
                if (id.StartsWith("AttrBuff")) return attr + "強化";
                if (id.StartsWith("StatusRateBuff")) return attr + "付与";
                return attr + "P";
            case MagicCategory.Support:
                return id == "DualSpell" ? "デュアル" : "アッド";
            default:
                return md.magicName.Length > 4 ? md.magicName.Substring(0, 4) : md.magicName;
        }
    }

    public string NodeTitle(string id)
    {
        ResearchNodeDef def = ResearchGraph.Get(id);
        if (def == null) return id;
        if (!def.isMagic) return def.title;
        MagicData md;
        return byId.TryGetValue(id, out md) && md != null ? Sanitize(md.magicName) : id;
    }

    public string NodeDetail(string id)
    {
        ResearchNodeDef def = ResearchGraph.Get(id);
        if (def == null) return "";
        if (!def.isMagic) return def.title;
        MagicData md;
        if (byId.TryGetValue(id, out md) && md != null)
            return Sanitize(string.IsNullOrEmpty(md.effectDescription) ? md.category.ToString() : md.effectDescription);
        return "";
    }

    private static string AttrKanji(MagicAttribute a)
    {
        switch (a)
        {
            case MagicAttribute.Fire: return "炎";
            case MagicAttribute.Thunder: return "雷";
            case MagicAttribute.Wind: return "風";
            case MagicAttribute.Light: return "光";
            case MagicAttribute.Dark: return "闇";
            default: return "無";
        }
    }

    // フォントアトラス未収録の漢字（態/電/復）を読める字に置換する表示専用ヘルパー。
    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Replace("状態異常", "異常").Replace("電磁波", "雷撃波").Replace("回復", "リジェネ");
    }

    public string NodeCostText(string id)
    {
        ResearchNodeDef def = ResearchGraph.Get(id);
        if (def == null) return "-";
        List<MaterialCost> cost = EffectiveCost(def);
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
