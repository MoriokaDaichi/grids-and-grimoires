using System.Collections.Generic;

// ハイドアウトの5設備。素材で建造し、Lv3まで素材で強化できる。
// ・研究机       … 研究(スキルツリー)を解禁。Lvで研究コスト減＋小ノードのボーナス増。
// ・作業台       … 杖/防具/アクセサリーの製作を解禁。Lvで上位レシピ解禁。
// ・マジックサークル … 手持ちアイテムを捧げて一定時間後にランダムなアイテムを得る。Lvで待ち時間短縮＋高レア率上昇。
// ・錬金釜       … モンスター素材を魔力結晶/エレメントへ変換。Lvで効率上昇。
// ・魔力炉       … 全設備へのエネルギー供給。魔力結晶を入れて消費する。Lvでスロット増＋燃費改善。
public enum FacilityKind
{
    ResearchDesk = 0,
    Workbench = 1,
    MagicCircle = 2,
    AlchemyCauldron = 3,
    ManaFurnace = 4,
}

// アイテムのレア度（マジックサークルの待ち時間・抽選に使う）。
public enum ItemRarity
{
    Common = 0,
    Uncommon = 1,
    Rare = 2,
    Epic = 3,
}

public class FacilityDef
{
    public FacilityKind kind;
    public string name;
    public string blurb;

    // costByStep[0] = 未建造 → Lv1、[1] = Lv1 → Lv2、[2] = Lv2 → Lv3
    public List<List<MaterialCost>> costByStep = new List<List<MaterialCost>>();
}

// 設備の定義と、レベルごとの効果パラメータ（純粋データ。数値は全て仮）。
public static class HideoutCatalog
{
    public const int MaxLevel = 3;

    private static MaterialCost S(int n) { return new MaterialCost { materialType = MaterialType.SmallManaCrystal, amount = n }; }
    private static MaterialCost M(int n) { return new MaterialCost { materialType = MaterialType.MediumManaCrystal, amount = n }; }
    private static MaterialCost L(int n) { return new MaterialCost { materialType = MaterialType.LargeManaCrystal, amount = n }; }
    private static MaterialCost Frag(MagicAttribute a, int n) { return new MaterialCost { materialType = MaterialType.ElementFragment, attribute = a, amount = n }; }
    private static MaterialCost Part(string name, int n) { return new MaterialCost { materialType = MaterialType.SpecialItem, specialItemName = name, amount = n }; }

    private static List<FacilityDef> _all;

    public static IReadOnlyList<FacilityDef> Facilities { get { Ensure(); return _all; } }

    public static FacilityDef Get(FacilityKind kind)
    {
        Ensure();
        foreach (FacilityDef d in _all) if (d.kind == kind) return d;
        return null;
    }

    private static void Ensure()
    {
        if (_all != null) return;
        // 建造コストのモンスター素材は「Lv1=tier1（浅層）のみ / Lv2=tier1〜2 / Lv3=上限なし（深層可）」。
        // （HideoutCatalogTests がこの上限を検証する）
        //
        // 再検証2（cold start 5周）で「Lv2 設備が中盤でデッドロック」＝錬金釜Lv2 の建材『古木の芯』が
        // 森の番人（debut 深度19）ドロップで、深度16 の壁に詰まったプレイヤーは永遠に建てられず、
        // 中結晶・tier2 素材の出口が両方閉じる、という所見。対策として Lv2(step1) は
        //   ・モンスター素材の必要個数を ×3→×2
        //   ・深層 debut の素材（古木の芯 等）を深度10〜13 で採れる tier2 素材へ差し替え
        //   ・中結晶の要求量も圧縮（Lv2 到達の前提が中結晶なので）
        // （数値は全て仮）
        _all = new List<FacilityDef>
        {
            new FacilityDef
            {
                kind = FacilityKind.ManaFurnace, name = "魔力炉",
                blurb = "全設備の動力源。魔力結晶を入れて稼働させる。",
                costByStep = new List<List<MaterialCost>>
                {
                    new List<MaterialCost> { S(12), Part("スライムゼリー", 4) },
                    new List<MaterialCost> { M(5), Part("鉄の兜", 2) },
                    new List<MaterialCost> { L(1), M(12), Part("巨神の核", 1) },
                },
            },
            new FacilityDef
            {
                kind = FacilityKind.ResearchDesk, name = "研究机",
                blurb = "研究（スキルツリー）を行う作業場。",
                costByStep = new List<List<MaterialCost>>
                {
                    new List<MaterialCost> { S(20), Part("毒針", 4) },
                    new List<MaterialCost> { M(6), Frag(MagicAttribute.Light, 2), Part("古びた骨", 2) },
                    new List<MaterialCost> { L(2), M(20), Part("世界樹の若枝", 2) },
                },
            },
            new FacilityDef
            {
                kind = FacilityKind.AlchemyCauldron, name = "錬金釜",
                blurb = "モンスター素材を魔力結晶やエレメントに練り直す。",
                costByStep = new List<List<MaterialCost>>
                {
                    // 再検証3 D1／改善ループ通しプレイ：cold-start の結晶収入はタスク報酬の**中結晶**で、
                    // Lv1 建材の小結晶（S16）は glen の「中→小」崩しを踏まないと賄えず、貪欲プレイでは
                    // 錬金釜（＝素材→結晶の“エンジン”）が建たずに詰む。錬金釜Lv1 だけ中結晶払いにして、
                    // 最初のタスク報酬で建てられるようにする（M(2)=20 は S(16)=16 よりむしろ割高＝甘くはしない）。
                    new List<MaterialCost> { M(2), Part("大ネズミの尾", 4) },
                    // 再検証3 D3／再検証5 R3：錬金釜Lv2 は中盤の伸びしろ（tier2+ 変換→エレメント欠片→
                    // 作業台Lv2/研究机Lv2）の起点なのに、cold-start 3周でも建たなかった。tier2 素材の要求を
                    // 2種（剛毛 d8＋蜘蛛の糸 d10）→ 1種（剛毛 d8）に。壁が d10〜11 のプレイヤーが d10 debut の
                    // 蜘蛛の糸 を待たずに建てられる。M(3) は据え置き（大結晶×2＝orca_2 を崩せば届く）。
                    new List<MaterialCost> { M(3), Part("剛毛", 2) },
                    new List<MaterialCost> { L(1), Frag(MagicAttribute.Wind, 3), Part("魔石の欠片", 3) },
                },
            },
            new FacilityDef
            {
                kind = FacilityKind.Workbench, name = "作業台",
                blurb = "杖・防具・アクセサリーを製作する。",
                costByStep = new List<List<MaterialCost>>
                {
                    new List<MaterialCost> { S(24), Part("ゴブリンの牙", 4) },
                    // 再検証3 D7：作業台Lv2＝tier2杖の 5×5 グリッド。建材の『竜人の鱗』は
                    // リザードマン（debut 深度13＝バースト即死帯）待ちで 5周とも建たなかった。
                    // 同 tier2・Fire の『錆びた短剣』（コボルト debut 深度9）へ差し替えて導線を浅くする。
                    new List<MaterialCost> { M(7), Frag(MagicAttribute.Fire, 2), Part("錆びた短剣", 2) },
                    new List<MaterialCost> { L(2), Frag(MagicAttribute.Dark, 3), Part("竜のうろこ", 2) },
                },
            },
            new FacilityDef
            {
                kind = FacilityKind.MagicCircle, name = "マジックサークル",
                blurb = "アイテムを捧げ、時をおいて別のアイテムへ変える。",
                costByStep = new List<List<MaterialCost>>
                {
                    new List<MaterialCost> { S(30), Part("スライムゼリー", 6) },
                    new List<MaterialCost> { M(8), Frag(MagicAttribute.Thunder, 2), Part("風切羽", 2) },
                    new List<MaterialCost> { L(3), Part("古木の芯", 3), Part("命の宝珠", 1) },
                },
            },
        };
    }

    // ---------------------------------------------------------------- レベル別の効果（level 0 = 未建造）

    // 研究机：研究コスト倍率（1未満で軽減）
    public static float ResearchCostMult(int level)
    {
        switch (level) { case 1: return 0.9f; case 2: return 0.78f; case 3: return 0.6f; default: return 1f; }
    }

    // 研究机：小ノードのボーナス倍率
    public static float ResearchBonusMult(int level)
    {
        switch (level) { case 1: return 1f; case 2: return 1.3f; case 3: return 1.7f; default: return 1f; }
    }

    // マジックサークル：待ち時間の倍率（1未満で短縮）
    public static float CircleDurationMult(int level)
    {
        switch (level) { case 1: return 1f; case 2: return 0.75f; case 3: return 0.5f; default: return 1f; }
    }

    // マジックサークル：抽選レア度への加算
    public static int CircleRarityBonus(int level)
    {
        return level <= 1 ? 0 : level - 1; // Lv1:0 / Lv2:1 / Lv3:2
    }

    // 錬金釜：変換効率（産出量の倍率）
    public static float CauldronYieldMult(int level)
    {
        switch (level) { case 1: return 1f; case 2: return 1.4f; case 3: return 1.9f; default: return 0f; }
    }

    // 錬金釜：変換できるモンスター素材の tier 上限（0 = 未建造）。Lv1:tier1 / Lv2:tier1〜3 / Lv3:tier1〜5
    public static int CauldronMaxTier(int level)
    {
        switch (level) { case 1: return 1; case 2: return 3; case 3: return 5; default: return 0; }
    }

    // その tier のモンスター素材を変換するのに必要な錬金釜レベル（1〜3）。
    public static int CauldronLevelForTier(int tier)
    {
        if (tier <= 1) return 1;
        if (tier <= 3) return 2;
        return 3;
    }

    // 魔力炉：スロット数（＝燃料バッファ上限は slots × 大結晶価値）
    public static int FurnaceSlots(int level)
    {
        return level <= 0 ? 0 : level + 1; // Lv1:2 / Lv2:3 / Lv3:4
    }

    // 魔力炉を初めて建てた（Lv0→Lv1）ときに与える初期燃料。再検証5 R2：
    // 魔力炉＋錬金釜＋作業台＋アクセを建てた直後に $30／小結晶20／燃料0 になり、
    // craft/research/transmute/upgrade が全部止まる「燃料デッドロック」への対策。
    // これだけあれば「錬金釜を建てる→杖を打つ→最初の十数個を変換して結晶を回収」の初動が
    // 一通り回る（＝結晶エンジンが自走に入るまでのブートストラップ分。数値は仮）。
    public const int FurnaceBuildBonusFuel = 25;

    // 魔力炉：設備アクション1回あたりの燃料消費（燃料は魔力結晶の価値で測る）
    // 再検証3（cold start 5周）D1/D4：Lv1=2 だと錬金釜Lv1 の tier1 変換（part+小2→小2）が
    // 完全に燃料中立で、cold-start は有限な序盤タスク報酬の結晶を配分ミスすると回復不能に近かった。
    // Lv1 を 1 に下げると Lv1 錬金釜でも `part + 燃料1 → 小2` ＝純増+1 になり、素材を結晶へ
    // 回して経済を立て直せる。中盤（Lv2 炉）以降の値は不変なので 3周目以降の推移は変わらない。
    // 魔力炉の強化はスロット（燃料バッファ上限）でメリットが残る。
    public static int FurnaceFuelPerAction(int level)
    {
        switch (level) { case 1: return 1; case 2: return 1; case 3: return 1; default: return 0; }
    }

    // 作業台：解禁されるレシピ階層（0 = 未建造）
    public static int WorkbenchTier(int level)
    {
        return level < 0 ? 0 : level;
    }
}
