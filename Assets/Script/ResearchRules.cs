using System;
using System.Collections.Generic;

// 研究解放の判定ロジック（純粋関数）。MonoBehaviour / インベントリ実体に依存しないので
// EditModeテストで検証できる。ResearchManager がこれを実インベントリと結線する。
// 前提関係は ResearchGraph（放射状スキルツリー）が既定。
public static class ResearchRules
{
    // コスト0の魔法（各属性の単体/全体Tier1）は最初から解放扱い＝木の根。
    public static bool IsBaseFree(MagicData md)
    {
        return md != null && (md.requiredMaterials == null || md.requiredMaterials.Count == 0);
    }

    public static bool IsUnlocked(MagicData md, ICollection<string> unlockedIds)
    {
        if (md == null) return false;
        if (IsBaseFree(md)) return true;
        return unlockedIds != null && unlockedIds.Contains(md.name);
    }

    // 前提の魔法ID（既定のスキルツリー）。前提が無ければ null。
    public static string PrerequisiteId(string magicId)
    {
        return PrerequisiteId(magicId, ResearchGraph.Prerequisites);
    }

    public static string PrerequisiteId(string magicId, IReadOnlyDictionary<string, string> prereqs)
    {
        if (string.IsNullOrEmpty(magicId) || prereqs == null) return null;
        return prereqs.TryGetValue(magicId, out string p) ? p : null;
    }

    // 前提が満たされているか（前提が無ければ常に true）。
    public static bool PrerequisiteMet(MagicData md, Func<string, bool> isIdUnlocked)
    {
        return PrerequisiteMet(md, isIdUnlocked, ResearchGraph.Prerequisites);
    }

    public static bool PrerequisiteMet(MagicData md, Func<string, bool> isIdUnlocked, IReadOnlyDictionary<string, string> prereqs)
    {
        if (md == null) return false;
        string prereq = PrerequisiteId(md.name, prereqs);
        if (prereq == null) return true;
        return isIdUnlocked != null && isIdUnlocked(prereq);
    }

    // 解放可能か: 未解放 かつ 前提を満たす かつ コストを賄える。
    public static bool CanUnlock(
        MagicData md,
        ICollection<string> unlockedIds,
        Func<string, bool> isIdUnlocked,
        Func<IEnumerable<MaterialCost>, bool> canAfford)
    {
        return CanUnlock(md, unlockedIds, isIdUnlocked, canAfford, ResearchGraph.Prerequisites);
    }

    public static bool CanUnlock(
        MagicData md,
        ICollection<string> unlockedIds,
        Func<string, bool> isIdUnlocked,
        Func<IEnumerable<MaterialCost>, bool> canAfford,
        IReadOnlyDictionary<string, string> prereqs)
    {
        if (md == null) return false;
        if (IsUnlocked(md, unlockedIds)) return false;
        if (!PrerequisiteMet(md, isIdUnlocked, prereqs)) return false;

        return canAfford != null && canAfford(md.requiredMaterials);
    }
}
