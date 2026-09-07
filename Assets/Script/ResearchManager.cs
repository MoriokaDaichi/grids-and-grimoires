using System;
using System.Collections.Generic;
using UnityEngine;

// 研究（スキルツリー）による魔法の解放を管理する。シーンにシングルトンで1つ。
// v1 は「素材を払って解放するフラットなリスト」（企画書の巨大分岐ツリーは後続）。
// パッシブのみ Lv1→Lv2→Lv3 の段階前提がある（ResearchRules）。
public class ResearchManager : MonoBehaviour
{
    public static ResearchManager Instance { get; private set; }

    public Action OnUnlocksChanged;

    private readonly HashSet<string> unlocked = new HashSet<string>();
    private readonly Dictionary<string, MagicData> byId = new Dictionary<string, MagicData>();
    private List<MagicData> allMagic = new List<MagicData>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        MagicSpawner spawner = UnityEngine.Object.FindFirstObjectByType<MagicSpawner>();
        if (spawner != null && spawner.magicDataList != null)
        {
            allMagic = spawner.magicDataList;
        }
        byId.Clear();
        foreach (MagicData md in allMagic)
        {
            if (md != null && !byId.ContainsKey(md.name)) byId[md.name] = md;
        }

        unlocked.Clear();
        foreach (string id in SaveManager.Load().unlockedMagicIds)
        {
            if (!string.IsNullOrEmpty(id)) unlocked.Add(id);
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public bool IsUnlocked(MagicData md)
    {
        return ResearchRules.IsUnlocked(md, unlocked);
    }

    public bool IsIdUnlocked(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (unlocked.Contains(id)) return true;
        MagicData md;
        return byId.TryGetValue(id, out md) && ResearchRules.IsBaseFree(md);
    }

    // 研究対象（コスト0でない魔法）を、属性→カテゴリ→名前 の順で返す
    public List<MagicData> Researchable()
    {
        List<MagicData> list = new List<MagicData>();
        foreach (MagicData md in allMagic)
        {
            if (md != null && !ResearchRules.IsBaseFree(md)) list.Add(md);
        }
        list.Sort(delegate (MagicData a, MagicData b)
        {
            int c = a.attribute.CompareTo(b.attribute);
            if (c != 0) return c;
            c = a.category.CompareTo(b.category);
            if (c != 0) return c;
            return string.CompareOrdinal(a.name, b.name);
        });
        return list;
    }

    public bool CanUnlock(MagicData md)
    {
        PlayerInventory inv = PlayerInventory.Instance;
        Func<IEnumerable<MaterialCost>, bool> afford =
            inv != null ? (Func<IEnumerable<MaterialCost>, bool>)inv.CanAfford : (c => false);
        return ResearchRules.CanUnlock(md, unlocked, IsIdUnlocked, afford);
    }

    public bool Unlock(MagicData md)
    {
        if (!CanUnlock(md)) return false;

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv != null && !inv.TrySpend(md.requiredMaterials)) return false;

        unlocked.Add(md.name);
        Save();
        OnUnlocksChanged?.Invoke();
        Debug.Log($"[研究] {md.magicName} を解放した。");
        return true;
    }

    private void Save()
    {
        SaveData data = SaveManager.Load();
        data.unlockedMagicIds = new List<string>(unlocked);
        SaveManager.Save(data);
    }
}
