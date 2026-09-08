using System.Collections.Generic;

// トレーダーの交換メニュー。企画書に交換レートの記載が無いため、
// 魔力結晶は価値保存（企画書6.6の 1/10/100）、欠片3→エレメント1（クラフトコストの×3から）を仮定。
// 「ステータスアップ：魔力結晶を…」の途中で切れた記述は、大結晶1→ステータスポイント+1 と仮実装（要確認）。
public class TradeOffer
{
    public string label;
    public List<MaterialCost> give = new List<MaterialCost>();
    public List<MaterialCost> receive = new List<MaterialCost>();
    public int bonusStatPoints;
    public int giveMoney;   // 交換にお金を支払う（お金コストのオファー）
    public int gainMoney;   // 交換でお金を受け取る（素材の売却）
}

public static class TradeCatalog
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

    public static List<TradeOffer> StandardOffers()
    {
        List<TradeOffer> offers = new List<TradeOffer>();

        offers.Add(Crystal("小結晶 ×10 → 中結晶 ×1", M(MaterialType.SmallManaCrystal, 10), M(MaterialType.MediumManaCrystal, 1)));
        offers.Add(Crystal("中結晶 ×1 → 小結晶 ×10", M(MaterialType.MediumManaCrystal, 1), M(MaterialType.SmallManaCrystal, 10)));
        offers.Add(Crystal("中結晶 ×10 → 大結晶 ×1", M(MaterialType.MediumManaCrystal, 10), M(MaterialType.LargeManaCrystal, 1)));
        offers.Add(Crystal("大結晶 ×1 → 中結晶 ×10", M(MaterialType.LargeManaCrystal, 1), M(MaterialType.MediumManaCrystal, 10)));

        MagicAttribute[] attrs = { MagicAttribute.Fire, MagicAttribute.Thunder, MagicAttribute.Wind, MagicAttribute.Light, MagicAttribute.Dark };
        string[] names = { "炎", "雷", "風", "光", "闇" };
        for (int i = 0; i < attrs.Length; i++)
        {
            TradeOffer o = new TradeOffer { label = names[i] + "エレメントの欠片 ×3 → " + names[i] + "エレメント ×1" };
            o.give.Add(Frag(attrs[i], 3));
            o.receive.Add(Elem(attrs[i], 1));
            offers.Add(o);
        }

        TradeOffer stat = new TradeOffer { label = "大結晶 ×1 → ステータスポイント +1（仮）", bonusStatPoints = 1 };
        stat.give.Add(M(MaterialType.LargeManaCrystal, 1));
        offers.Add(stat);

        return offers;
    }

    private static TradeOffer Crystal(string label, MaterialCost give, MaterialCost receive)
    {
        TradeOffer o = new TradeOffer { label = label };
        o.give.Add(give);
        o.receive.Add(receive);
        return o;
    }

    // 交換内容の価値（魔力結晶換算）。give と receive で一致していれば価値保存。
    public static int Value(IEnumerable<MaterialCost> materials)
    {
        int sum = 0;
        foreach (MaterialCost c in materials)
        {
            if (c == null) continue;
            sum += MaterialCatalog.Value(c.materialType) * c.amount;
        }
        return sum;
    }
}
