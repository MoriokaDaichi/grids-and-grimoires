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

// 装備を挿す「枠」。カテゴリ(GearSlot)は同じでもアクセサリーは2枠ある。
// Accessory2 はトレーダーのタスク報酬で開放するまで使えない。
public enum EquipSlot
{
    Wand = 0,
    Armor = 1,
    Accessory1 = 2,
    Accessory2 = 3,
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

    // --- サステイン系の副効果（0 は無効）。主に「アクセ」枠。数値は全て仮。---
    public float hpRegenPerSecond;    // 戦闘中、毎秒このぶん現在HPを自然回復する
    public float healPerWaveFlat;     // ウェーブ突破時に固定値ぶん現在HPを回復する
    public float healPerWavePercent;  // ウェーブ突破時に最大HPのこの割合ぶん回復する（0..1）

    public bool HasSustain
    {
        get { return hpRegenPerSecond > 0f || healPerWaveFlat > 0f || healPerWavePercent > 0f; }
    }
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

    // 装備枠 → 装備カテゴリ。
    public static GearSlot CategoryOf(EquipSlot s)
    {
        if (s == EquipSlot.Wand) return GearSlot.Wand;
        if (s == EquipSlot.Armor) return GearSlot.Armor;
        return GearSlot.Accessory;
    }

    public static bool IsAccessorySlot(EquipSlot s)
    {
        return s == EquipSlot.Accessory1 || s == EquipSlot.Accessory2;
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

    // サステイン系アクセサリ。主ステータス（stat/amount）に加えて HP 回復系の副効果を持つ。
    // regen=毎秒回復 / healFlat=ウェーブ突破時の固定回復 / healPct=ウェーブ突破時の最大HP割合回復。
    private static GearDef Ga(string id, string name, int tier, ResearchStat stat, float amount,
        float regen, float healFlat, float healPct, params MaterialCost[] cost)
    {
        return new GearDef
        {
            id = id, name = name, slot = GearSlot.Accessory, tier = tier, stat = stat, amount = amount,
            cost = new List<MaterialCost>(cost),
            hpRegenPerSecond = regen, healPerWaveFlat = healFlat, healPerWavePercent = healPct,
        };
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
            // 検証レポート 2026-09-10 O7：樫の杖＝tier2杖＝5×5 グリッドの入口。建材の『炎欠片×2』は
            // 釜Lv2 前は自作できず（tier2 属性パーツの変換に釜Lv2 が要る）リーゼからの購入頼み＝細い faucet。
            // 5×5 到達を属性フラグの購入に縛らないよう中結晶のみに（釜Lv2 以降は炎フラグは余る＝O7）。
            G("wand_oak",        "樫の杖",       GearSlot.Wand,      2, ResearchStat.Atk, 6f, M(8)),
            G("armor_leather",   "堅革の鎧",     GearSlot.Armor,     2, ResearchStat.Def, 5f, M(6), Part("番人の樹皮", 3)),
            G("acc_ring",        "魔力の指輪",   GearSlot.Accessory, 2, ResearchStat.ManaMax, 25f, M(6), Frag(MagicAttribute.Thunder, 2)),

            // --- tier3（作業台 Lv3）---
            G("wand_arch",       "大魔道の杖",   GearSlot.Wand,      3, ResearchStat.Atk, 11f, L(1), Frag(MagicAttribute.Dark, 3)),
            // 深部設計（サイクル13）：建材『古木の芯』は森の番人（debut 深度19）ドロップで、
            // 作業台Lv3 到達直後には採れないことが多い。d13 debut の『竜人の鱗』へ差し替え。
            G("armor_plate",     "彫紋の板金",   GearSlot.Armor,     3, ResearchStat.Hp, 55f, L(1), Part("竜人の鱗", 3)),
            G("acc_amulet",      "賢者の護符",   GearSlot.Accessory, 3, ResearchStat.ManaRegen, 2.5f, L(1), Frag(MagicAttribute.Light, 3)),

            // --- サステイン系アクセサリ（ウェーブ間でHPが回復しない壁への対策。作業台Lvで解禁）---
            // tier1
            Ga("acc_salve_pendant",  "癒しのペンダント", 1, ResearchStat.Hp,  10f, 0f,   15f, 0f,    S(15), Part("スライムゼリー", 4)),
            Ga("acc_mending_band",   "繕いの腕輪",       1, ResearchStat.Hp,  10f, 1.5f, 0f,   0f,    S(15), Part("薄い翼膜", 4)),
            Ga("acc_verdant_charm",  "若葉の護符",       1, ResearchStat.Def, 1f,  0f,   0f,   0.06f, S(15), Part("胞子嚢", 4)),
            // tier2
            Ga("acc_lifewell_ring",  "命脈の指輪",       2, ResearchStat.Hp,  20f, 0f,   35f,  0f,    M(6), Frag(MagicAttribute.Light, 2)),
            Ga("acc_regen_torc",     "再生のトルク",     2, ResearchStat.Hp,  15f, 3.5f, 0f,   0f,    M(6), Part("番人の樹皮", 3)),
            Ga("acc_bloodstone",     "血石の首飾り",     2, ResearchStat.Atk, 3f,  0f,   0f,   0.10f, M(6), Frag(MagicAttribute.Dark, 2)),
            Ga("acc_heartbeat_stone","鼓動の護石",       2, ResearchStat.Def, 2f,  2f,   20f,  0f,    M(6), Frag(MagicAttribute.Wind, 2)),
            // tier3
            Ga("acc_phoenix_charm",  "不死鳥の護符",     3, ResearchStat.Hp,  30f, 0f,   0f,   0.20f, L(1), Frag(MagicAttribute.Fire, 3)),
            Ga("acc_font_of_life",   "生命の泉",         3, ResearchStat.Hp,  25f, 6f,   0f,   0f,    L(1), Part("古木の芯", 3)),
            Ga("acc_eternal_locket", "永生のロケット",   3, ResearchStat.ManaRegen, 1.5f, 3f, 40f, 0.08f, L(1), Frag(MagicAttribute.Light, 3)),

            // --- レシピ制（トレーダーのタスク報酬で解禁。作業台Lvは満たしていること）---
            Gr("wand_runed",      "刻印の杖",     GearSlot.Wand,      1, ResearchStat.Atk, 5f,  S(20), Frag(MagicAttribute.Fire, 2), Part("小さな牙", 4)),
            Gr("acc_sigil",       "精霊のシジル", GearSlot.Accessory, 1, ResearchStat.ManaRegen, 1.8f, S(20), Frag(MagicAttribute.Wind, 2), Part("薄い翼膜", 4)),
            Gr("armor_warded",    "護符織りの法衣", GearSlot.Armor,   2, ResearchStat.Hp, 40f, M(6), Frag(MagicAttribute.Light, 2), Part("トロルの生皮", 2)),
            Gr("wand_stormcaller","嵐呼びの杖",   GearSlot.Wand,      3, ResearchStat.Atk, 14f, L(1), Frag(MagicAttribute.Thunder, 3), Part("竜王のうろこ", 1)),
            Gr("armor_aegis",     "深淵のイージス", GearSlot.Armor,   3, ResearchStat.Def, 9f,  L(1), Frag(MagicAttribute.Dark, 3), Part("首無しの兜", 1)),
            Gr("acc_orb",         "賢者の宝珠",   GearSlot.Accessory, 3, ResearchStat.ManaMax, 45f, L(1), Frag(MagicAttribute.Light, 3), Part("命の宝珠", 1)),

            // 深部設計（サイクル13 案B）：d13〜15 帯で採れる素材（竜人の鱗）で作れる tier3 装備を追加。
            // 「全投資でも壁 d20」＝投資軸が Atk/HP しか無く tier3 が丸ごとデッド、への対策。
            // レシピはオルカの深部到達タスク（d18／d22）で解禁。
            Gr("wand_dragoon",  "竜騎の杖",     GearSlot.Wand,   3, ResearchStat.Atk, 16f, L(1), Frag(MagicAttribute.Fire, 3),  Part("竜人の鱗", 2)),
            Gr("armor_scale",   "竜鱗の胸甲",   GearSlot.Armor,  3, ResearchStat.Hp,  60f, L(1), Frag(MagicAttribute.Dark, 3),  Part("竜人の鱗", 3)),
        };
    }
}
