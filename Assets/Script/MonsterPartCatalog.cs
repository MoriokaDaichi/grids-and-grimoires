using System.Collections.Generic;

// 敵40体が落とす「モンスター固有ドロップ」の正本（純粋データ・Runtime）。
// EnemyDataGenerator の Part(...) 名とここが1対1で対応する（生成器側で未登録名を警告）。
//
// tier は 1〜5（浅層=1 … 深淵=5。EnemyDataGenerator.OrderWeakToStrong のバンドに対応）。
// attribute は錬金釜/トレーダーで「属性の欠片・エレメント」へ変換するときの対応属性（None は結晶ルートのみ）。
// 数値・分類は全て仮。
public class MonsterPart
{
    public string name;
    public int tier;
    public MagicAttribute attribute;
}

public static class MonsterPartCatalog
{
    private static List<MonsterPart> _all;
    private static Dictionary<string, MonsterPart> _byName;

    public static IReadOnlyList<MonsterPart> All { get { Ensure(); return _all; } }

    public static MonsterPart Get(string name)
    {
        Ensure();
        if (string.IsNullOrEmpty(name)) return null;
        MonsterPart p;
        return _byName.TryGetValue(name, out p) ? p : null;
    }

    public static bool IsKnown(string name) { return Get(name) != null; }

    // 未登録は 0 を返す（呼び出し側で下限1に丸める運用）。
    public static int TierOf(string name)
    {
        MonsterPart p = Get(name);
        return p != null ? p.tier : 0;
    }

    public static MagicAttribute AttributeOf(string name)
    {
        MonsterPart p = Get(name);
        return p != null ? p.attribute : MagicAttribute.None;
    }

    private static MonsterPart P(string name, int tier, MagicAttribute attr)
    {
        return new MonsterPart { name = name, tier = tier, attribute = attr };
    }

    private static void Ensure()
    {
        if (_all != null) return;
        _all = new List<MonsterPart>
        {
            // ---- tier1: 浅層（森の小物）----
            P("スライムゼリー", 1, MagicAttribute.None),
            P("小さな牙",       1, MagicAttribute.None),
            P("薄い翼膜",       1, MagicAttribute.Wind),
            P("大ネズミの尾",   1, MagicAttribute.Dark),
            P("毒針",           1, MagicAttribute.Wind),
            P("胞子嚢",         1, MagicAttribute.None),
            P("ゴブリンの牙",   1, MagicAttribute.Dark),
            P("鋭い爪",         1, MagicAttribute.None),

            // ---- tier2: 森の中型 ----
            P("剛毛",           2, MagicAttribute.None),
            P("錆びた短剣",     2, MagicAttribute.Fire),
            P("蜘蛛の糸",       2, MagicAttribute.None),
            P("毒腺",           2, MagicAttribute.Dark),
            P("鉄の兜",         2, MagicAttribute.None),
            P("風切羽",         2, MagicAttribute.Wind),
            P("竜人の鱗",       2, MagicAttribute.Fire),
            P("若木の枝",       2, MagicAttribute.Wind),
            P("古びた骨",       2, MagicAttribute.Dark),
            P("腐肉",           2, MagicAttribute.None),
            P("番人の樹皮",     2, MagicAttribute.None),
            P("古木の芯",       2, MagicAttribute.Wind),

            // ---- tier3: 遺跡・洞窟 ----
            P("石の破片",       3, MagicAttribute.None),
            P("悪魔の角",       3, MagicAttribute.Fire),
            P("トロルの生皮",   3, MagicAttribute.None),
            P("呪われた指輪",   3, MagicAttribute.Dark),
            P("オーガの牙",     3, MagicAttribute.None),
            P("石化の眼",       3, MagicAttribute.Wind),
            P("魔石の欠片",     3, MagicAttribute.None),
            P("焦げた牙",       3, MagicAttribute.Fire),

            // ---- tier4: 魔性・上位 ----
            P("飛竜の翼膜",     4, MagicAttribute.Wind),
            P("悪夢の霧",       4, MagicAttribute.Dark),
            P("首無しの兜",     4, MagicAttribute.None),
            P("死霊術の書片",   4, MagicAttribute.Dark),
            P("世界樹の若枝",   4, MagicAttribute.Wind),
            P("巨神の核",       4, MagicAttribute.None),
            P("竜のうろこ",     4, MagicAttribute.Fire),

            // ---- tier5: ボス級・深淵 ----
            P("吸血鬼の牙",     5, MagicAttribute.Dark),
            P("混沌の血",       5, MagicAttribute.Fire),
            P("命の宝珠",       5, MagicAttribute.Dark),
            P("ベヒモスの角",   5, MagicAttribute.None),
            P("竜王のうろこ",   5, MagicAttribute.Fire),
            P("竜の心臓",       5, MagicAttribute.None),
            P("深淵の欠片",     5, MagicAttribute.Dark),
            P("混沌の核",       5, MagicAttribute.None),
        };

        _byName = new Dictionary<string, MonsterPart>();
        foreach (MonsterPart p in _all) _byName[p.name] = p;
    }
}
