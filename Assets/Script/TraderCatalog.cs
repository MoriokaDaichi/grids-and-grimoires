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

// トレーダーの定義（純粋データ）。企画書に交換レート・依頼内容の記載が無いため全て仮。
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

    private static TradeOffer Offer(string label, MaterialCost give, MaterialCost receive, int bonusStat = 0)
    {
        TradeOffer o = new TradeOffer { label = label, bonusStatPoints = bonusStat };
        if (give != null) o.give.Add(give);
        if (receive != null) o.receive.Add(receive);
        return o;
    }

    private static TraderTask Deliver(string id, string trader, string title, List<MaterialCost> items,
        List<MaterialCost> reward, int rewardStat = 0)
    {
        return new TraderTask
        {
            id = id, traderId = trader, title = title, kind = TraderTaskKind.DeliverItems,
            deliverItems = items, rewardItems = reward ?? new List<MaterialCost>(), rewardStatPoints = rewardStat,
        };
    }

    private static TraderTask Kill(string id, string trader, string title, string enemyName, int count,
        List<MaterialCost> reward, int rewardStat = 0)
    {
        return new TraderTask
        {
            id = id, traderId = trader, title = title, kind = TraderTaskKind.DefeatEnemies,
            targetEnemyName = enemyName, targetCount = count,
            rewardItems = reward ?? new List<MaterialCost>(), rewardStatPoints = rewardStat,
        };
    }

    private static TraderTask Depth(string id, string trader, string title, int depth,
        List<MaterialCost> reward, int rewardStat = 0)
    {
        return new TraderTask
        {
            id = id, traderId = trader, title = title, kind = TraderTaskKind.ReachDepth,
            targetCount = depth, rewardItems = reward ?? new List<MaterialCost>(), rewardStatPoints = rewardStat,
        };
    }

    public static List<Trader> BuildTraders()
    {
        var traders = new List<Trader>();

        // --- 両替商 グレン（結晶）---
        var glen = new Trader { id = "glen", name = "両替商 グレン", specialty = TraderSpecialty.Crystals,
            blurb = "魔力結晶なら何でも扱う。価値は等価交換だ。" };
        glen.offers.Add(Offer("小結晶 ×10 → 中結晶 ×1", M(MaterialType.SmallManaCrystal, 10), M(MaterialType.MediumManaCrystal, 1)));
        glen.offers.Add(Offer("中結晶 ×1 → 小結晶 ×10", M(MaterialType.MediumManaCrystal, 1), M(MaterialType.SmallManaCrystal, 10)));
        glen.offers.Add(Offer("中結晶 ×10 → 大結晶 ×1", M(MaterialType.MediumManaCrystal, 10), M(MaterialType.LargeManaCrystal, 1)));
        glen.offers.Add(Offer("大結晶 ×1 → 中結晶 ×10", M(MaterialType.LargeManaCrystal, 1), M(MaterialType.MediumManaCrystal, 10)));
        glen.offers.Add(Offer("大結晶 ×1 → ステータスポイント +1（仮）", M(MaterialType.LargeManaCrystal, 1), null, bonusStat: 1));
        glen.tasks.Add(Deliver("glen_t1", "glen", "小結晶を 15 個かき集める",
            new List<MaterialCost> { M(MaterialType.SmallManaCrystal, 15) },
            new List<MaterialCost> { M(MaterialType.MediumManaCrystal, 3) }));
        glen.tasks.Add(Deliver("glen_t2", "glen", "中結晶を 8 個用立てる",
            new List<MaterialCost> { M(MaterialType.MediumManaCrystal, 8) },
            new List<MaterialCost> { M(MaterialType.LargeManaCrystal, 1) }, rewardStat: 1));
        traders.Add(glen);

        // --- 精霊使い リーゼ（エレメント）---
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
        liese.offers.Add(Offer("番人の樹皮 ×2 → 風エレメントの欠片 ×1", Part("番人の樹皮", 2), Frag(MagicAttribute.Wind, 1)));
        liese.offers.Add(Offer("ゴブリンの牙 ×3 → 闇エレメントの欠片 ×1", Part("ゴブリンの牙", 3), Frag(MagicAttribute.Dark, 1)));
        liese.offers.Add(Offer("大ネズミの尾 ×3 → 闇エレメントの欠片 ×1", Part("大ネズミの尾", 3), Frag(MagicAttribute.Dark, 1)));
        liese.offers.Add(Offer("古木の芯 ×2 → 風エレメント ×1", Part("古木の芯", 2), Elem(MagicAttribute.Wind, 1)));
        liese.tasks.Add(Deliver("liese_t1", "liese", "炎の欠片を 5 つ納める",
            new List<MaterialCost> { Frag(MagicAttribute.Fire, 5) },
            new List<MaterialCost> { Elem(MagicAttribute.Fire, 2) }));
        liese.tasks.Add(Deliver("liese_t2", "liese", "光と闇の欠片を 3 つずつ",
            new List<MaterialCost> { Frag(MagicAttribute.Light, 3), Frag(MagicAttribute.Dark, 3) },
            new List<MaterialCost> { M(MaterialType.MediumManaCrystal, 4) }, rewardStat: 1));
        traders.Add(liese);

        // --- 傭兵ギルド受付 ダグ（戦闘）---
        var dag = new Trader { id = "dag", name = "傭兵ギルド受付 ダグ", specialty = TraderSpecialty.Combat,
            blurb = "討伐依頼を回してる。腕が立つなら報酬は弾む。" };
        dag.offers.Add(Offer("小結晶 ×6 → 風エレメントの欠片 ×1", M(MaterialType.SmallManaCrystal, 6), Frag(MagicAttribute.Wind, 1)));
        // 討伐で得たモンスター素材の買取（結晶化）
        dag.offers.Add(Offer("スライムゼリー ×5 → 小結晶 ×3", Part("スライムゼリー", 5), M(MaterialType.SmallManaCrystal, 3)));
        dag.offers.Add(Offer("ゴブリンの牙 ×4 → 小結晶 ×5", Part("ゴブリンの牙", 4), M(MaterialType.SmallManaCrystal, 5)));
        dag.offers.Add(Offer("大ネズミの尾 ×4 → 小結晶 ×4", Part("大ネズミの尾", 4), M(MaterialType.SmallManaCrystal, 4)));
        dag.offers.Add(Offer("番人の樹皮 ×3 → 中結晶 ×1", Part("番人の樹皮", 3), M(MaterialType.MediumManaCrystal, 1)));
        dag.tasks.Add(Kill("dag_t1", "dag", "魔物を 10 体討伐", null, 10,
            new List<MaterialCost> { M(MaterialType.MediumManaCrystal, 3) }));
        dag.tasks.Add(Kill("dag_t2", "dag", "魔物を 30 体討伐", null, 30,
            new List<MaterialCost> { M(MaterialType.LargeManaCrystal, 1) }, rewardStat: 1));
        dag.tasks.Add(Kill("dag_t3", "dag", "森の番人を 3 体討伐", "森の番人", 3,
            new List<MaterialCost> { Elem(MagicAttribute.Light, 1) }));
        dag.tasks.Add(Deliver("dag_t4", "dag", "ゴブリンの牙を 12 本納める",
            new List<MaterialCost> { Part("ゴブリンの牙", 12) },
            new List<MaterialCost> { M(MaterialType.MediumManaCrystal, 3) }, rewardStat: 1));
        traders.Add(dag);

        // --- 蒐集家 オルカ（深層）---
        var orca = new Trader { id = "orca", name = "蒐集家 オルカ", specialty = TraderSpecialty.Collector,
            blurb = "深層の澱みでしか採れぬものがある。潜れる者を求む。" };
        orca.offers.Add(Offer("大結晶 ×1 → ステータスポイント +2（仮）", M(MaterialType.LargeManaCrystal, 1), null, bonusStat: 2));
        orca.tasks.Add(Depth("orca_t1", "orca", "深度 5 まで到達する", 5,
            new List<MaterialCost> { M(MaterialType.MediumManaCrystal, 5) }));
        orca.tasks.Add(Depth("orca_t2", "orca", "深度 10 まで到達する", 10,
            new List<MaterialCost> { M(MaterialType.LargeManaCrystal, 2) }, rewardStat: 1));
        orca.tasks.Add(Deliver("orca_t3", "orca", "各モンスターの素材を 3 つずつ蒐集する",
            new List<MaterialCost> { Part("スライムゼリー", 3), Part("ゴブリンの牙", 3), Part("大ネズミの尾", 3), Part("番人の樹皮", 3) },
            new List<MaterialCost> { M(MaterialType.LargeManaCrystal, 1) }, rewardStat: 1));
        traders.Add(orca);

        return traders;
    }
}
