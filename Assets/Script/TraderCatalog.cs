using System.Collections.Generic;

// トレーダーの専門分野。
public enum TraderSpecialty
{
    Crystals,   // 魔力結晶の両替
    Elements,   // エレメント・欠片の精製
    Combat,     // 戦闘報酬・討伐依頼
    Collector,  // 蒐集・深層到達依頼
}

// 1人のトレーダー。得意な素材種と、交換メニュー・タスクを持つ。
public class Trader
{
    public string id;
    public string name;
    public string blurb;
    public TraderSpecialty specialty;
    public List<TradeOffer> offers = new List<TradeOffer>();
    public List<TraderTask> tasks = new List<TraderTask>();
}

// トレーダーの定義（純粋データ）。企画書に交換レート・依頼内容・お金の記載が無いため数値は全て仮。
// tasks は「タスクライン」＝ requires を辿る連鎖。前提タスクを達成するまで次は解放されない（判定は TradeManager）。
public static class TraderCatalog
{
    private static MaterialCost M(MaterialType type, int amount)
    {
        return new MaterialCost { materialType = type, amount = amount };
    }

    private static MaterialCost Frag(MagicAttribute attr, int amount)
    {
        return new MaterialCost { materialType = MaterialType.ElementFragment, attribute = attr, amount = amount };
    }

    private static MaterialCost Elem(MagicAttribute attr, int amount)
    {
        return new MaterialCost { materialType = MaterialType.Element, attribute = attr, amount = amount };
    }

    // モンスター固有のドロップ品（EnemyDataGenerator の Part と対応）。
    private static MaterialCost Part(string name, int amount)
    {
        return new MaterialCost { materialType = MaterialType.SpecialItem, specialItemName = name, amount = amount };
    }

    private static List<MaterialCost> Items(params MaterialCost[] cs)
    {
        return new List<MaterialCost>(cs);
    }

    // --- 交換オファー -------------------------------------------------

    private static TradeOffer Offer(string label, MaterialCost give, MaterialCost receive, int bonusStat = 0)
    {
        TradeOffer o = new TradeOffer { label = label, bonusStatPoints = bonusStat };
        if (give != null) o.give.Add(give);
        if (receive != null) o.receive.Add(receive);
        return o;
    }

    // 素材 → お金（売却）
    private static TradeOffer Sell(string label, MaterialCost give, int gold)
    {
        TradeOffer o = new TradeOffer { label = label, gainMoney = gold };
        if (give != null) o.give.Add(give);
        return o;
    }

    // お金 → 素材／ステータスポイント（買い取り）
    private static TradeOffer Buy(string label, int gold, MaterialCost receive, int bonusStat = 0)
    {
        TradeOffer o = new TradeOffer { label = label, giveMoney = gold, bonusStatPoints = bonusStat };
        if (receive != null) o.receive.Add(receive);
        return o;
    }

    // --- タスク -----------------------------------------------------

    private static TraderTask Deliver(string id, string trader, string title, List<MaterialCost> items,
        List<MaterialCost> reward, int rewardStat = 0, int rewardMoney = 0, int deliverMoney = 0,
        string rewardGear = null, string rewardRecipe = null)
    {
        return new TraderTask
        {
            id = id, traderId = trader, title = title, kind = TraderTaskKind.DeliverItems,
            deliverItems = items, deliverMoney = deliverMoney,
            rewardItems = reward ?? new List<MaterialCost>(), rewardStatPoints = rewardStat,
            rewardMoney = rewardMoney, rewardGearId = rewardGear, rewardRecipeId = rewardRecipe,
        };
    }

    private static TraderTask Kill(string id, string trader, string title, string enemyName, int count,
        List<MaterialCost> reward, int rewardStat = 0, int rewardMoney = 0,
        string rewardGear = null, string rewardRecipe = null)
    {
        return new TraderTask
        {
            id = id, traderId = trader, title = title, kind = TraderTaskKind.DefeatEnemies,
            targetEnemyName = enemyName, targetCount = count,
            rewardItems = reward ?? new List<MaterialCost>(), rewardStatPoints = rewardStat,
            rewardMoney = rewardMoney, rewardGearId = rewardGear, rewardRecipeId = rewardRecipe,
        };
    }

    private static TraderTask Depth(string id, string trader, string title, int depth,
        List<MaterialCost> reward, int rewardStat = 0, int rewardMoney = 0,
        string rewardGear = null, string rewardRecipe = null)
    {
        return new TraderTask
        {
            id = id, traderId = trader, title = title, kind = TraderTaskKind.ReachDepth,
            targetCount = depth, rewardItems = reward ?? new List<MaterialCost>(), rewardStatPoints = rewardStat,
            rewardMoney = rewardMoney, rewardGearId = rewardGear, rewardRecipeId = rewardRecipe,
        };
    }

    private static TraderTask CraftGear(string id, string trader, string title, GearSlot slot, int minTier,
        List<MaterialCost> reward, int rewardStat = 0, int rewardMoney = 0)
    {
        return new TraderTask
        {
            id = id, traderId = trader, title = title, kind = TraderTaskKind.CraftGear,
            targetGearSlot = slot, targetCount = minTier,
            rewardItems = reward ?? new List<MaterialCost>(), rewardStatPoints = rewardStat, rewardMoney = rewardMoney,
        };
    }

    // 連鎖にする：定義順で直前タスクの id を requires に埋め、トレーダーへ追加する。
    private static void Chain(Trader trader, params TraderTask[] tasks)
    {
        string prev = null;
        foreach (TraderTask t in tasks)
        {
            t.requires = prev;
            trader.tasks.Add(t);
            prev = t.id;
        }
    }

    public static List<Trader> BuildTraders()
    {
        var traders = new List<Trader>();

        // ================================================ 両替商 グレン（結晶）
        var glen = new Trader { id = "glen", name = "両替商 グレン", specialty = TraderSpecialty.Crystals,
            blurb = "魔力結晶なら何でも扱う。価値は等価交換だ。お金への両替もやってるぞ。" };
        glen.offers.Add(Offer("小結晶 ×10 → 中結晶 ×1", M(MaterialType.SmallManaCrystal, 10), M(MaterialType.MediumManaCrystal, 1)));
        glen.offers.Add(Offer("中結晶 ×1 → 小結晶 ×10", M(MaterialType.MediumManaCrystal, 1), M(MaterialType.SmallManaCrystal, 10)));
        glen.offers.Add(Offer("中結晶 ×10 → 大結晶 ×1", M(MaterialType.MediumManaCrystal, 10), M(MaterialType.LargeManaCrystal, 1)));
        glen.offers.Add(Offer("大結晶 ×1 → 中結晶 ×10", M(MaterialType.LargeManaCrystal, 1), M(MaterialType.MediumManaCrystal, 10)));
        // 結晶の売却（→ お金）
        glen.offers.Add(Sell("小結晶 ×10 → 12 G", M(MaterialType.SmallManaCrystal, 10), 12));
        glen.offers.Add(Sell("中結晶 ×5 → 55 G", M(MaterialType.MediumManaCrystal, 5), 55));
        glen.offers.Add(Sell("大結晶 ×1 → 120 G", M(MaterialType.LargeManaCrystal, 1), 120));
        // お金で買う
        glen.offers.Add(Buy("60 G → 中結晶 ×1", 60, M(MaterialType.MediumManaCrystal, 1)));
        glen.offers.Add(Buy("150 G → ステータスポイント +1（仮）", 150, null, bonusStat: 1));
        Chain(glen,
            Deliver("glen_1", "glen", "小結晶を 15 個かき集める",
                Items(M(MaterialType.SmallManaCrystal, 15)), Items(M(MaterialType.MediumManaCrystal, 3)), rewardMoney: 30),
            Deliver("glen_2", "glen", "中結晶を 8 個用立てる",
                Items(M(MaterialType.MediumManaCrystal, 8)), Items(M(MaterialType.LargeManaCrystal, 1)), rewardStat: 1),
            // 再検証2 R5：序盤の中結晶供給が glen_1（小15→中3・1回）だけで、研究/設備Lv2 が中結晶枯れで止まる。
            // 結晶トレーダーの浅いタスクにもう1本、中結晶を出す（現物中心でお金も細いので少額の現金も付ける）。
            Deliver("glen_3", "glen", "小結晶 ×30 と手数料 20 G を預ける",
                Items(M(MaterialType.SmallManaCrystal, 30)), Items(M(MaterialType.MediumManaCrystal, 3)), deliverMoney: 20, rewardMoney: 70),
            Deliver("glen_4", "glen", "中結晶を 12 個回す",
                Items(M(MaterialType.MediumManaCrystal, 12)), null, rewardStat: 1, rewardMoney: 90),
            Deliver("glen_5", "glen", "大結晶 ×2 と 40 G で精霊細工の権利を買う",
                Items(M(MaterialType.LargeManaCrystal, 2)), null, deliverMoney: 40, rewardRecipe: "acc_sigil"),
            Deliver("glen_6", "glen", "中結晶を 20 個そろえる",
                Items(M(MaterialType.MediumManaCrystal, 20)), null, rewardMoney: 160),
            Deliver("glen_7", "glen", "大結晶を 3 個そろえる",
                Items(M(MaterialType.LargeManaCrystal, 3)), null, rewardStat: 2, rewardMoney: 120),
            Deliver("glen_8", "glen", "大結晶 ×5 と 100 G で上得意の証を得る",
                Items(M(MaterialType.LargeManaCrystal, 5)), null, deliverMoney: 100, rewardMoney: 200, rewardGear: "acc_ring"));
        traders.Add(glen);

        // ================================================ 精霊使い リーゼ（エレメント）
        var liese = new Trader { id = "liese", name = "精霊使い リーゼ", specialty = TraderSpecialty.Elements,
            blurb = "欠片を束ねてエレメントに。属性の相談も乗るわ。" };
        MagicAttribute[] attrs = { MagicAttribute.Fire, MagicAttribute.Thunder, MagicAttribute.Wind, MagicAttribute.Light, MagicAttribute.Dark };
        string[] an = { "炎", "雷", "風", "光", "闇" };
        for (int i = 0; i < attrs.Length; i++)
        {
            liese.offers.Add(Offer(an[i] + "エレメントの欠片 ×3 → " + an[i] + "エレメント ×1", Frag(attrs[i], 3), Elem(attrs[i], 1)));
        }
        // モンスター素材から属性の欠片・エレメントを精製する（敵の属性に対応）
        liese.offers.Add(Offer("スライムゼリー ×4 → 風エレメントの欠片 ×1", Part("スライムゼリー", 4), Frag(MagicAttribute.Wind, 1)));
        liese.offers.Add(Offer("ゴブリンの牙 ×3 → 闇エレメントの欠片 ×1", Part("ゴブリンの牙", 3), Frag(MagicAttribute.Dark, 1)));
        liese.offers.Add(Offer("古木の芯 ×2 → 風エレメント ×1", Part("古木の芯", 2), Elem(MagicAttribute.Wind, 1)));
        liese.offers.Add(Offer("錆びた短剣 ×4 → 炎エレメントの欠片 ×1", Part("錆びた短剣", 4), Frag(MagicAttribute.Fire, 1)));
        liese.offers.Add(Offer("竜人の鱗 ×3 → 炎エレメントの欠片 ×1", Part("竜人の鱗", 3), Frag(MagicAttribute.Fire, 1)));
        liese.offers.Add(Offer("風切羽 ×3 → 風エレメントの欠片 ×1", Part("風切羽", 3), Frag(MagicAttribute.Wind, 1)));
        liese.offers.Add(Offer("竜のうろこ ×2 → 炎エレメント ×1", Part("竜のうろこ", 2), Elem(MagicAttribute.Fire, 1)));
        liese.offers.Add(Offer("深淵の欠片 ×1 → 闇エレメント ×1", Part("深淵の欠片", 1), Elem(MagicAttribute.Dark, 1)));
        // 欠片・エレメントの売却（→ お金）
        liese.offers.Add(Sell("エレメントの欠片 ×3（炎）→ 24 G", Frag(MagicAttribute.Fire, 3), 24));
        liese.offers.Add(Sell("闇エレメントの欠片 ×3 → 24 G", Frag(MagicAttribute.Dark, 3), 24));
        liese.offers.Add(Sell("炎エレメント ×1 → 60 G", Elem(MagicAttribute.Fire, 1), 60));
        liese.offers.Add(Buy("50 G → 風エレメントの欠片 ×1", 50, Frag(MagicAttribute.Wind, 1)));
        Chain(liese,
            Deliver("liese_1", "liese", "炎の欠片を 5 つ納める",
                Items(Frag(MagicAttribute.Fire, 5)), Items(Elem(MagicAttribute.Fire, 2))),
            Deliver("liese_2", "liese", "光と闇の欠片を 3 つずつ",
                Items(Frag(MagicAttribute.Light, 3), Frag(MagicAttribute.Dark, 3)),
                Items(M(MaterialType.MediumManaCrystal, 4)), rewardStat: 1),
            Deliver("liese_3", "liese", "炎系の中位素材を束ねる（竜人の鱗/悪魔の角/焦げた牙 ×3）",
                Items(Part("竜人の鱗", 3), Part("悪魔の角", 3), Part("焦げた牙", 3)),
                Items(Elem(MagicAttribute.Fire, 2)), rewardStat: 1),
            Deliver("liese_4", "liese", "風系の素材 と 30 G を工房に回す（風切羽 ×4 / 石化の眼 ×2）",
                Items(Part("風切羽", 4), Part("石化の眼", 2)), null, deliverMoney: 30, rewardMoney: 80),
            Deliver("liese_5", "liese", "全属性の欠片を 4 つずつ集める",
                Items(Frag(MagicAttribute.Fire, 4), Frag(MagicAttribute.Thunder, 4), Frag(MagicAttribute.Wind, 4),
                      Frag(MagicAttribute.Light, 4), Frag(MagicAttribute.Dark, 4)),
                null, rewardRecipe: "armor_warded"),
            Deliver("liese_6", "liese", "深層の若枝と欠片（世界樹の若枝 ×2 / 深淵の欠片 ×1）",
                Items(Part("世界樹の若枝", 2), Part("深淵の欠片", 1)),
                Items(Elem(MagicAttribute.Dark, 2)), rewardMoney: 100),
            Deliver("liese_7", "liese", "炎と風のエレメントを 2 つずつ",
                Items(Elem(MagicAttribute.Fire, 2), Elem(MagicAttribute.Wind, 2)), null, rewardStat: 2),
            Deliver("liese_8", "liese", "五大エレメント各 1 と 120 G を捧げる",
                Items(Elem(MagicAttribute.Fire, 1), Elem(MagicAttribute.Thunder, 1), Elem(MagicAttribute.Wind, 1),
                      Elem(MagicAttribute.Light, 1), Elem(MagicAttribute.Dark, 1)),
                null, deliverMoney: 120, rewardMoney: 120, rewardGear: "acc_ring"));
        traders.Add(liese);

        // ================================================ 傭兵ギルド受付 ダグ（戦闘）
        var dag = new Trader { id = "dag", name = "傭兵ギルド受付 ダグ", specialty = TraderSpecialty.Combat,
            blurb = "討伐依頼を回してる。腕が立つなら報酬は弾む。素材の買取もやるぞ。" };
        dag.offers.Add(Offer("小結晶 ×6 → 風エレメントの欠片 ×1", M(MaterialType.SmallManaCrystal, 6), Frag(MagicAttribute.Wind, 1)));
        // 討伐で得たモンスター素材の買取（→ お金）
        dag.offers.Add(Sell("スライムゼリー ×5 → 10 G", Part("スライムゼリー", 5), 10));
        dag.offers.Add(Sell("ゴブリンの牙 ×4 → 16 G", Part("ゴブリンの牙", 4), 16));
        dag.offers.Add(Sell("大ネズミの尾 ×4 → 14 G", Part("大ネズミの尾", 4), 14));
        // tier2 の中位素材（剛毛/蜘蛛の糸/鉄の兜/毒腺/錆びた短剣/風切羽/若木の枝/腐肉）は
        // 錬金釜Lv1 で変換できず売り先も無く「塩漬け」になる（再検証2 R4）。深度10前後を周回するほど
        // 積み上がるので、討伐報酬の買取口をまとめて開ける（レートは仮、tier2 ≒ 6 G/個）。
        dag.offers.Add(Sell("剛毛 ×3 → 18 G", Part("剛毛", 3), 18));
        dag.offers.Add(Sell("蜘蛛の糸 ×3 → 18 G", Part("蜘蛛の糸", 3), 18));
        dag.offers.Add(Sell("鉄の兜 ×3 → 18 G", Part("鉄の兜", 3), 18));
        dag.offers.Add(Sell("毒腺 ×3 → 18 G", Part("毒腺", 3), 18));
        dag.offers.Add(Sell("錆びた短剣 ×3 → 18 G", Part("錆びた短剣", 3), 18));
        dag.offers.Add(Sell("風切羽 ×3 → 18 G", Part("風切羽", 3), 18));
        dag.offers.Add(Sell("若木の枝 ×3 → 18 G", Part("若木の枝", 3), 18));
        dag.offers.Add(Sell("腐肉 ×3 → 18 G", Part("腐肉", 3), 18));
        dag.offers.Add(Sell("番人の樹皮 ×3 → 30 G", Part("番人の樹皮", 3), 30));
        dag.offers.Add(Sell("オーガの牙 ×2 → 45 G", Part("オーガの牙", 2), 45));
        dag.offers.Add(Sell("トロルの生皮 ×2 → 40 G", Part("トロルの生皮", 2), 40));
        dag.offers.Add(Sell("巨神の核 ×1 → 110 G", Part("巨神の核", 1), 110));
        dag.offers.Add(Sell("ベヒモスの角 ×1 → 180 G", Part("ベヒモスの角", 1), 180));
        dag.offers.Add(Sell("竜王のうろこ ×1 → 180 G", Part("竜王のうろこ", 1), 180));
        dag.offers.Add(Buy("80 G → 中結晶 ×1", 80, M(MaterialType.MediumManaCrystal, 1)));
        Chain(dag,
            // 再検証3 D2：「まず杖」導線。研究より先にグリッドを広げる動機づけが無く、cold-start で
            // 有限な結晶を研究へ全振り→杖が買えず 3×3 のまま火力が伸びない、という詰み方をしていた。
            // ダグの連鎖の先頭に「杖を打て」を置き、報酬に中結晶×3（D3 の中盤 faucet も兼ねる）。
            // 見習いの杖でよい（targetCount=1＝最低tier1）。作業台Lv1 が前提なので序盤の一里塚になる。
            CraftGear("dag_wand", "dag", "作業台で杖を打つ（見習いの杖でよい）", GearSlot.Wand, 1,
                Items(M(MaterialType.MediumManaCrystal, 3))),
            Kill("dag_1", "dag", "魔物を 10 体討伐", null, 10, Items(M(MaterialType.MediumManaCrystal, 4)), rewardMoney: 40),
            Kill("dag_2", "dag", "魔物を 30 体討伐", null, 30, Items(M(MaterialType.LargeManaCrystal, 1)), rewardStat: 1),
            Kill("dag_3", "dag", "森の番人を 3 体討伐", "森の番人", 3, Items(Elem(MagicAttribute.Light, 1)), rewardMoney: 50),
            Deliver("dag_4", "dag", "ゴブリンの牙を 12 本納める",
                Items(Part("ゴブリンの牙", 12)), Items(M(MaterialType.MediumManaCrystal, 3)), rewardStat: 1),
            Kill("dag_5", "dag", "スケルトンを 8 体討伐", "スケルトン", 8, Items(Frag(MagicAttribute.Dark, 2)), rewardMoney: 70),
            Kill("dag_6", "dag", "オーガを 5 体討伐", "オーガ", 5, null, rewardStat: 1, rewardRecipe: "wand_runed"),
            Kill("dag_7", "dag", "魔物を 80 体討伐", null, 80, null, rewardStat: 1, rewardMoney: 160),
            Kill("dag_8", "dag", "ドラゴンを 3 体討伐", "ドラゴン", 3, Items(Elem(MagicAttribute.Fire, 2)), rewardMoney: 120),
            Kill("dag_9", "dag", "ドラゴンを 8 体討伐", "ドラゴン", 8, null, rewardStat: 2, rewardGear: "wand_stormcaller"));
        traders.Add(dag);

        // ================================================ 蒐集家 オルカ（深層）
        var orca = new Trader { id = "orca", name = "蒐集家 オルカ", specialty = TraderSpecialty.Collector,
            blurb = "深層の澱みでしか採れぬものがある。潜れる者を求む。" };
        // 深層でしか採れぬ素材の買取（蒐集家価格）
        orca.offers.Add(Sell("死霊術の書片 ×1 → 130 G", Part("死霊術の書片", 1), 130));
        orca.offers.Add(Sell("首無しの兜 ×1 → 120 G", Part("首無しの兜", 1), 120));
        orca.offers.Add(Sell("混沌の核 ×1 → 260 G", Part("混沌の核", 1), 260));
        orca.offers.Add(Sell("竜の心臓 ×1 → 240 G", Part("竜の心臓", 1), 240));
        orca.offers.Add(Buy("200 G → ステータスポイント +2（仮）", 200, null, bonusStat: 2));
        orca.offers.Add(Buy("120 G → 大結晶 ×1", 120, M(MaterialType.LargeManaCrystal, 1)));
        Chain(orca,
            Depth("orca_1", "orca", "深度 5 まで到達する", 5, Items(M(MaterialType.MediumManaCrystal, 5)), rewardMoney: 40),
            Depth("orca_2", "orca", "深度 10 まで到達する", 10, Items(M(MaterialType.LargeManaCrystal, 2)), rewardStat: 1),
            // 「ウェーブ間 HP 無回復＋深部バーストで満タンから即死」の壁（C2）に届く直前で、
            // tier2 サステイン（再生のトルク＝毎秒 +3.5）を作業台Lv2 を待たず直接渡す＝入手性の底上げ。
            Depth("orca_2b", "orca", "深度 12 まで到達する", 12, null, rewardGear: "acc_regen_torc"),
            Deliver("orca_3", "orca", "浅層の素材を 3 つずつ蒐集する",
                Items(Part("スライムゼリー", 3), Part("ゴブリンの牙", 3), Part("大ネズミの尾", 3), Part("番人の樹皮", 3)),
                Items(M(MaterialType.LargeManaCrystal, 1)), rewardStat: 1),
            Depth("orca_4", "orca", "深度 15 まで到達する", 15, null, rewardStat: 1, rewardMoney: 120),
            Deliver("orca_5", "orca", "遺跡の遺物 と 50 G を納める（石の破片/古びた骨/腐肉 ×5）",
                Items(Part("石の破片", 5), Part("古びた骨", 5), Part("腐肉", 5)), null, deliverMoney: 50, rewardMoney: 150),
            Depth("orca_6", "orca", "深度 20 まで到達する", 20, null, rewardRecipe: "armor_aegis"),
            Deliver("orca_7", "orca", "深淵の証と 80 G を捧げる（竜の心臓/深淵の欠片/混沌の核 ×1）",
                Items(Part("竜の心臓", 1), Part("深淵の欠片", 1), Part("混沌の核", 1)),
                Items(Elem(MagicAttribute.Dark, 3)), rewardStat: 2, deliverMoney: 80),
            Depth("orca_8", "orca", "深度 25 まで到達する", 25, null, rewardStat: 2, rewardMoney: 250),
            Depth("orca_9", "orca", "深度 30 まで到達する", 30, null, rewardStat: 3, rewardGear: "acc_orb"));
        traders.Add(orca);

        return traders;
    }
}
