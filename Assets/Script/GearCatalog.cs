using System.Collections.Generic;

// 作業台で製作する装備。スロットは 杖 / 防具 / アクセサリー の3種。
// 所有＝装備中の扱いで、PlayerStatus に恒久ボーナスを与える（1スロット1個まで、上位を作ると下位は置き換え）。
// tier は必要な作業台レベル（1..3）。数値は全て仮。
public enum GearSlot
{
    Wand = 0,
    Armor = 1,
    Accessory = 2,
}

public class GearDef
{
    public string id;
    public string name;
    public GearSlot slot;
    public int tier;              // 1..3（作業台レベル要件）
    public ResearchStat stat;     // 付与ステータス（PlayerStatus.ApplyResearchDelta で流用）
    public float amount;
    public List<MaterialCost> cost = new List<MaterialCost>();
    public bool recipeGated;      // true なら、レシピを解禁（トレーダーのタスク報酬）するまで作業台に出さない
}

public static class GearCatalog
{
    private static MaterialCost S(int n) { return new MaterialCost { materialType = MaterialType.SmallManaCrystal, amount = n }; }
    private static MaterialCost M(int n) { return new MaterialCost { materialType = MaterialType.MediumManaCrystal, amount = n }; }
    private static MaterialCost L(int n) { return new MaterialCost { materialType = MaterialType.LargeManaCrystal, amount = n }; }
    private static MaterialCost Frag(MagicAttribute a, int n) { return new MaterialCost { materialType = MaterialType.ElementFragment, attribute = a, amount = n }; }
    private static MaterialCost Part(string name, int n) { return new MaterialCost { materialType = MaterialType.SpecialItem, specialItemName = name, amount = n }; }

    private static List<GearDef> _all;

    public static IReadOnlyList<GearDef> All { get { Ensure(); return _all; } }

    public static GearDef Get(string id)
    {
        Ensure();
        foreach (GearDef g in _all) if (g.id == id) return g;
        return null;
    }

    // 作業台レベル level で常時製作可能なもの（tier <= level かつ レシピ制でない）。
    public static List<GearDef> Craftable(int workbenchLevel)
    {
        Ensure();
        List<GearDef> outp = new List<GearDef>();
        foreach (GearDef g in _all) if (g.tier <= workbenchLevel && !g.recipeGated) outp.Add(g);
        return outp;
    }

    // 作業台レベル level で製作可能なもの（tier <= level）。レシピ制のものは解禁済みIDに含まれる場合のみ。
    public static List<GearDef> CraftableWithRecipes(int workbenchLevel, IEnumerable<string> unlockedRecipeIds)
    {
        Ensure();
        HashSet<string> unlocked = new HashSet<string>();
        if (unlockedRecipeIds != null)
            foreach (string id in unlockedRecipeIds) if (!string.IsNullOrEmpty(id)) unlocked.Add(id);

        List<GearDef> outp = new List<GearDef>();
        foreach (GearDef g in _all)
        {
            if (g.tier > workbenchLevel) continue;
            if (g.recipeGated && !unlocked.Contains(g.id)) continue;
            outp.Add(g);
        }
        return outp;
    }

    // レシピ制の全装備（トレーダー報酬 rewardRecipeId の候補検証などに使う）。
    public static List<GearDef> RecipeGated()
    {
        Ensure();
        List<GearDef> outp = new List<GearDef>();
        foreach (GearDef g in _all) if (g.recipeGated) outp.Add(g);
        return outp;
    }

    private static GearDef G(string id, string name, GearSlot slot, int tier, ResearchStat stat, float amount, params MaterialCost[] cost)
    {
        return new GearDef { id = id, name = name, slot = slot, tier = tier, stat = stat, amount = amount, cost = new List<MaterialCost>(cost) };
    }

    private static GearDef Gr(string id, string name, GearSlot slot, int tier, ResearchStat stat, float amount, params MaterialCost[] cost)
    {
        return new GearDef { id = id, name = name, slot = slot, tier = tier, stat = stat, amount = amount, cost = new List<MaterialCost>(cost), recipeGated = true };
    }

    private static void Ensure()
    {
        if (_all != null) return;
        _all = new List<GearDef>
        {
            // --- tier1（作業台 Lv1）---
            G("wand_apprentice", "見習いの杖",   GearSlot.Wand,      1, ResearchStat.Atk, 3f, S(15), Part("ゴブリンの牙", 3)),
            G("armor_cloth",     "布の外套",     GearSlot.Armor,     1, ResearchStat.Hp, 20f, S(15), Part("スライムゼリー", 4)),
            G("acc_charm",       "小さな護符",   GearSlot.Accessory, 1, ResearchStat.Luc, 2f, S(15), Part("大ネズミの尾", 3)),

            // --- tier2（作業台 Lv2）---
            G("wand_oak",        "樫の杖",       GearSlot.Wand,      2, ResearchStat.Atk, 6f, M(6), Frag(MagicAttribute.Fire, 2)),
            G("armor_leather",   "堅革の鎧",     GearSlot.Armor,     2, ResearchStat.Def, 5f, M(6), Part("番人の樹皮", 3)),
            G("acc_ring",        "魔力の指輪",   GearSlot.Accessory, 2, ResearchStat.ManaMax, 25f, M(6), Frag(MagicAttribute.Thunder, 2)),

            // --- tier3（作業台 Lv3）---
            G("wand_arch",       "大魔道の杖",   GearSlot.Wand,      3, ResearchStat.Atk, 11f, L(1), Frag(MagicAttribute.Dark, 3)),
            G("armor_plate",     "彫紋の板金",   GearSlot.Armor,     3, ResearchStat.Hp, 55f, L(1), Part("古木の芯", 3)),
            G("acc_amulet",      "賢者の護符",   GearSlot.Accessory, 3, ResearchStat.ManaRegen, 2.5f, L(1), Frag(MagicAttribute.Light, 3)),

            // --- レシピ制（トレーダーのタスク報酬で解禁。作業台Lvは満たしていること）---
            Gr("wand_runed",      "刻印の杖",     GearSlot.Wand,      1, ResearchStat.Atk, 5f,  S(20), Frag(MagicAttribute.Fire, 2), Part("小さな牙", 4)),
            Gr("acc_sigil",       "精霊のシジル", GearSlot.Accessory, 1, ResearchStat.ManaRegen, 1.8f, S(20), Frag(MagicAttribute.Wind, 2), Part("薄い翼膜", 4)),
            Gr("armor_warded",    "護符織りの法衣", GearSlot.Armor,   2, ResearchStat.Hp, 40f, M(6), Frag(MagicAttribute.Light, 2), Part("トロルの生皮", 2)),
            Gr("wand_stormcaller","嵐呼びの杖",   GearSlot.Wand,      3, ResearchStat.Atk, 14f, L(1), Frag(MagicAttribute.Thunder, 3), Part("竜王のうろこ", 1)),
            Gr("armor_aegis",     "深淵のイージス", GearSlot.Armor,   3, ResearchStat.Def, 9f,  L(1), Frag(MagicAttribute.Dark, 3), Part("首無しの兜", 1)),
            Gr("acc_orb",         "賢者の宝珠",   GearSlot.Accessory, 3, ResearchStat.ManaMax, 45f, L(1), Frag(MagicAttribute.Light, 3), Part("命の宝珠", 1)),
        };
    }
}
