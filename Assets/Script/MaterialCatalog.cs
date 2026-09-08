// 素材(MaterialCost)の表示名・価値・集計キーを一元管理する。
// 報酬画面(Phase 1)、素材インベントリ(Phase 2)、トレード(Phase 4)で共有する。
public static class MaterialCatalog
{
    public static string DisplayName(MaterialType type, MagicAttribute attribute, string specialItemName)
    {
        switch (type)
        {
            case MaterialType.SmallManaCrystal: return "小魔力結晶";
            case MaterialType.MediumManaCrystal: return "中魔力結晶";
            case MaterialType.LargeManaCrystal: return "大魔力結晶";
            case MaterialType.ElementFragment: return AttributeLabel(attribute) + "エレメントの欠片";
            case MaterialType.Element: return AttributeLabel(attribute) + "エレメント";
            case MaterialType.SpecialItem: return string.IsNullOrEmpty(specialItemName) ? "特殊アイテム" : specialItemName;
            default: return type.ToString();
        }
    }

    public static string DisplayName(MaterialCost cost)
    {
        return DisplayName(cost.materialType, cost.attribute, cost.specialItemName);
    }

    // 企画書6.6の魔力結晶の価値。欠片・エレメント・固有アイテムの価値はPhase 4で確定する。
    public static int Value(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.SmallManaCrystal: return 1;
            case MaterialType.MediumManaCrystal: return 10;
            case MaterialType.LargeManaCrystal: return 100;
            default: return 0;
        }
    }

    // 素材1個あたりのゴールド概算価値（トレーダーの売却レート基準・仮）。
    // 入場料の自動精算（無料入場ぶんを戦利品から差し引く）などで使う。スタック総額は amount 倍する。
    public static int GoldValue(MaterialCost cost)
    {
        if (cost == null) return 0;
        switch (cost.materialType)
        {
            case MaterialType.SmallManaCrystal: return 1;
            case MaterialType.MediumManaCrystal: return 10;
            case MaterialType.LargeManaCrystal: return 100;
            case MaterialType.ElementFragment: return 8;
            case MaterialType.Element: return 60;
            case MaterialType.SpecialItem:
                switch (MonsterPartCatalog.TierOf(cost.specialItemName))
                {
                    case 2: return 10;
                    case 3: return 20;
                    case 4: return 60;
                    case 5: return 150;
                    default: return 3; // tier1 / 未登録
                }
            default: return 0;
        }
    }

    // 素材を種別ごとに集計するための一意キー
    public static string Key(MaterialCost cost)
    {
        return cost.materialType + ":" + cost.attribute + ":" + cost.specialItemName;
    }

    private static string AttributeLabel(MagicAttribute attribute)
    {
        switch (attribute)
        {
            case MagicAttribute.Fire: return "炎";
            case MagicAttribute.Thunder: return "雷";
            case MagicAttribute.Wind: return "風";
            case MagicAttribute.Light: return "光";
            case MagicAttribute.Dark: return "闇";
            default: return "";
        }
    }
}
