using System.Collections.Generic;

// 所持素材の集計ロジック。MonoBehaviour / ファイルIO に依存しないので EditModeテストで検証できる。
// PlayerInventory がこれをラップし、セーブ読み書きとイベント通知を担う。
public class MaterialLedger
{
    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    private readonly Dictionary<string, MaterialCost> samples = new Dictionary<string, MaterialCost>();

    public IEnumerable<KeyValuePair<string, int>> Counts { get { return counts; } }

    public int GetCount(MaterialCost cost)
    {
        int v;
        return counts.TryGetValue(MaterialCatalog.Key(cost), out v) ? v : 0;
    }

    // 集計キーから表示用の MaterialCost（種別情報）を引く
    public MaterialCost Sample(string key)
    {
        MaterialCost mc;
        return samples.TryGetValue(key, out mc) ? mc : null;
    }

    public void Add(IEnumerable<MaterialCost> gained)
    {
        if (gained == null) return;
        foreach (MaterialCost c in gained)
        {
            if (c == null || c.amount <= 0) continue;
            string key = MaterialCatalog.Key(c);
            int cur;
            counts.TryGetValue(key, out cur);
            counts[key] = cur + c.amount;
            if (!samples.ContainsKey(key)) samples[key] = Clone(c);
        }
    }

    // 消費せずに、賄えるかどうかだけ判定する。
    public bool CanAfford(IEnumerable<MaterialCost> cost)
    {
        if (cost == null) return true;
        foreach (KeyValuePair<string, int> kv in Aggregate(cost))
        {
            int have;
            counts.TryGetValue(kv.Key, out have);
            if (have < kv.Value) return false;
        }
        return true;
    }

    // すべて賄えるときだけ消費して true を返す。1つでも足りなければ何も減らさず false。
    public bool TrySpend(IEnumerable<MaterialCost> cost)
    {
        if (cost == null) return true;

        Dictionary<string, int> need = Aggregate(cost);

        foreach (KeyValuePair<string, int> kv in need)
        {
            int have;
            counts.TryGetValue(kv.Key, out have);
            if (have < kv.Value) return false;
        }

        foreach (KeyValuePair<string, int> kv in need)
        {
            counts[kv.Key] -= kv.Value;
            if (counts[kv.Key] <= 0) counts.Remove(kv.Key);
        }
        return true;
    }

    private static Dictionary<string, int> Aggregate(IEnumerable<MaterialCost> cost)
    {
        Dictionary<string, int> need = new Dictionary<string, int>();
        foreach (MaterialCost c in cost)
        {
            if (c == null || c.amount <= 0) continue;
            string key = MaterialCatalog.Key(c);
            int cur;
            need.TryGetValue(key, out cur);
            need[key] = cur + c.amount;
        }
        return need;
    }

    public void LoadFrom(SaveData data)
    {
        counts.Clear();
        samples.Clear();
        if (data == null || data.materials == null) return;

        foreach (MaterialStack s in data.materials)
        {
            if (s == null || s.count <= 0) continue;
            MaterialCost mc = new MaterialCost
            {
                materialType = (MaterialType)s.materialType,
                attribute = (MagicAttribute)s.attribute,
                specialItemName = s.specialItemName,
                amount = 1,
            };
            string key = MaterialCatalog.Key(mc);
            counts[key] = s.count;
            samples[key] = mc;
        }
    }

    public void WriteTo(SaveData data)
    {
        data.materials = new List<MaterialStack>();
        foreach (KeyValuePair<string, int> kv in counts)
        {
            MaterialCost mc;
            if (!samples.TryGetValue(kv.Key, out mc) || mc == null) continue;
            data.materials.Add(new MaterialStack
            {
                materialType = (int)mc.materialType,
                attribute = (int)mc.attribute,
                specialItemName = mc.specialItemName,
                count = kv.Value,
            });
        }
    }

    private static MaterialCost Clone(MaterialCost c)
    {
        return new MaterialCost
        {
            materialType = c.materialType,
            attribute = c.attribute,
            specialItemName = c.specialItemName,
            amount = 1,
        };
    }
}
