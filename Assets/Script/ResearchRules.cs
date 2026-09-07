using System;
using System.Collections.Generic;

// 研究解放の判定ロジック（純粋関数）。MonoBehaviour / インベントリ実体に依存しないので
// EditModeテストで検証できる。ResearchManager がこれを実インベントリと結線する。
public static class ResearchRules
{
    // コスト0の魔法（各属性のTier1: ファイア/フレイム系など）は最初から解放扱い。
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

    // パッシブの段階前提: "〜PassiveLv2" は "〜PassiveLv1"、"〜PassiveLv3" は "〜PassiveLv2" が前提。
    // それ以外は前提なし（null）。
    public static string PrerequisiteId(string magicId)
    {
        if (string.IsNullOrEmpty(magicId)) return null;
        if (magicId.EndsWith("PassiveLv2")) return magicId.Substring(0, magicId.Length - 1) + "1";
        if (magicId.EndsWith("PassiveLv3")) return magicId.Substring(0, magicId.Length - 1) + "2";
        return null;
    }

    // 解放可能か: 未解放 かつ 前提を満たす かつ コストを賄える。
    public static bool CanUnlock(
        MagicData md,
        ICollection<string> unlockedIds,
        Func<string, bool> isIdUnlocked,
        Func<IEnumerable<MaterialCost>, bool> canAfford)
    {
        if (md == null) return false;
        if (IsUnlocked(md, unlockedIds)) return false;

        string prereq = PrerequisiteId(md.name);
        if (prereq != null && (isIdUnlocked == null || !isIdUnlocked(prereq))) return false;

        return canAfford != null && canAfford(md.requiredMaterials);
    }
}
