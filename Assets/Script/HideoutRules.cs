using System;
using System.Collections.Generic;

// ハイドアウトの純粋ロジック（MonoBehaviour / ファイルIO 非依存。EditModeテストで検証する）。
// HideoutManager がこれをラップして、所持素材の消費・セーブ・イベント通知を行う。
public static class HideoutRules
{
    // 次の建造/強化に必要な素材。currentLevel は現在のレベル(0..MaxLevel)。最大なら null。
    public static List<MaterialCost> NextCost(FacilityDef def, int currentLevel)
    {
        if (def == null || currentLevel >= HideoutCatalog.MaxLevel || currentLevel < 0) return null;
        if (currentLevel >= def.costByStep.Count) return null;
        return def.costByStep[currentLevel];
    }

    public static bool CanAdvance(FacilityDef def, int currentLevel, Func<IEnumerable<MaterialCost>, bool> canAfford)
    {
        List<MaterialCost> cost = NextCost(def, currentLevel);
        return cost != null && canAfford != null && canAfford(cost);
    }

    // 研究コストのスケール。mult<1 で軽減。各素材 ceil(amount×mult)、最低1。
    public static List<MaterialCost> ScaleCost(IEnumerable<MaterialCost> baseCost, float mult)
    {
        List<MaterialCost> outp = new List<MaterialCost>();
        if (baseCost == null) return outp;
        foreach (MaterialCost c in baseCost)
        {
            if (c == null) continue;
            // float 由来の誤差（0.6f≒0.60000002）で切り上げが1つズレないよう丸めてから ceil する
            double scaled = Math.Round(c.amount * (double)mult, 6);
            int amt = Math.Max(1, (int)Math.Ceiling(scaled));
            outp.Add(new MaterialCost
            {
                materialType = c.materialType,
                attribute = c.attribute,
                specialItemName = c.specialItemName,
                amount = amt,
            });
        }
        return outp;
    }

    // ---------------------------------------------------------------- マジックサークル

    // 素材/アイテムのレア度。
    public static ItemRarity RarityOf(MaterialCost c)
    {
        if (c == null) return ItemRarity.Common;
        switch (c.materialType)
        {
            case MaterialType.SmallManaCrystal: return ItemRarity.Common;
            case MaterialType.MediumManaCrystal: return ItemRarity.Uncommon;
            case MaterialType.LargeManaCrystal: return ItemRarity.Rare;
            case MaterialType.ElementFragment: return ItemRarity.Uncommon;
            case MaterialType.Element: return ItemRarity.Rare;
            case MaterialType.SpecialItem:
                return c.specialItemName == "古木の芯" ? ItemRarity.Uncommon : ItemRarity.Common;
            default: return ItemRarity.Common;
        }
    }

    // 捧げたアイテムのレア度とサークルLvから、完成までの待ち時間（時間）。高レアほど長い。
    public static float BrewHours(ItemRarity inputRarity, int circleLevel)
    {
        float baseHours;
        switch (inputRarity)
        {
            case ItemRarity.Common: baseHours = 4f; break;
            case ItemRarity.Uncommon: baseHours = 6f; break;
            case ItemRarity.Rare: baseHours = 9f; break;
            default: baseHours = 12f; break;
        }
        return baseHours * HideoutCatalog.CircleDurationMult(Math.Max(1, circleLevel));
    }

    public static bool BrewReady(double startUnixSeconds, float hours, double nowUnixSeconds)
    {
        return nowUnixSeconds - startUnixSeconds >= hours * 3600.0;
    }

    public static double BrewRemainingSeconds(double startUnixSeconds, float hours, double nowUnixSeconds)
    {
        double end = startUnixSeconds + hours * 3600.0;
        return Math.Max(0.0, end - nowUnixSeconds);
    }

    // 完成品のレア度（決定論的）。入力レア度＋サークルボーナスを中心に確率で上下する。
    public static ItemRarity RollRarity(int seed, ItemRarity inputRarity, int rarityBonus)
    {
        uint h = (uint)seed * 1664525u + 1013904223u;
        int roll = (int)(h % 100u);
        int tier = (int)inputRarity + Math.Max(0, rarityBonus);

        int result;
        if (roll < 10) result = tier - 1;       // 10% 格下げ
        else if (roll < 70) result = tier;      // 60% 据え置き
        else if (roll < 95) result = tier + 1;  // 25% 格上げ
        else result = tier + 2;                 // 5%  大当たり

        if (result < 0) result = 0;
        if (result > (int)ItemRarity.Epic) result = (int)ItemRarity.Epic;
        return (ItemRarity)result;
    }

    // ---------------------------------------------------------------- 錬金釜

    // モンスター素材 → 結晶/エレメント。yieldMult で産出量が増える（floor、最低1）。
    // 牙/尾/ゼリー → 小結晶×2、番人の樹皮 → 中結晶×1、古木の芯 → 風エレメントの欠片×1（1個あたり）。
    public static List<MaterialCost> Transmute(MaterialCost part, int times, float yieldMult)
    {
        List<MaterialCost> outp = new List<MaterialCost>();
        if (part == null || times <= 0 || part.materialType != MaterialType.SpecialItem) return outp;

        MaterialType outType;
        MagicAttribute outAttr = MagicAttribute.None;
        int per;
        if (part.specialItemName == "番人の樹皮") { outType = MaterialType.MediumManaCrystal; per = 1; }
        else if (part.specialItemName == "古木の芯") { outType = MaterialType.ElementFragment; outAttr = MagicAttribute.Wind; per = 1; }
        else { outType = MaterialType.SmallManaCrystal; per = 2; }

        int amt = Math.Max(1, (int)Math.Floor(per * times * (double)yieldMult));
        outp.Add(new MaterialCost { materialType = outType, attribute = outAttr, amount = amt });
        return outp;
    }

    // ---------------------------------------------------------------- 魔力炉

    // スロット枚数から燃料バッファ上限（大結晶価値=100 換算）。
    public static long FurnaceCapacity(int furnaceLevel)
    {
        return HideoutCatalog.FurnaceSlots(furnaceLevel) * (long)MaterialCatalog.Value(MaterialType.LargeManaCrystal);
    }

    // 燃料があり、かつ furnaceLevel>=1 なら設備アクション可能。
    public static bool CanPowerAction(int furnaceLevel, long fuel)
    {
        return furnaceLevel >= 1 && fuel >= HideoutCatalog.FurnaceFuelPerAction(furnaceLevel);
    }
}
