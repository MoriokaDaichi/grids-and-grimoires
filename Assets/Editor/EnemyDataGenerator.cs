using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 企画書7章の敵データは列見出しのみで数値が無いため、仮バランスで敵40体を生成する Editor 拡張。
// メニュー: Grimoire > Generate Enemy Data
//
// 並びは「弱い順」。EndlessWaveGenerator が深度に応じて抽選できる範囲を先頭側から窓で絞るので、
// この配列の順序がそのまま出現順の目安になる。DungeonGenerator も OrderWeakToStrong を使う。
// 数値・属性・耐性・ドロップはすべて仮。
public static class EnemyDataGenerator
{
    private const string OutputFolder = "Assets/EnemyData";

    private class Def
    {
        public string FileId;
        public string Name;
        public int MaxHp;
        public int Atk;
        public int Def_;
        public float AttackInterval;
        public MagicAttribute Attribute = MagicAttribute.None;
        public List<AttributeResistance> Resistances = new List<AttributeResistance>();
        public List<MaterialCost> Drops = new List<MaterialCost>();
    }

    // そのモンスター固有のドロップ品（牙・毛皮など）。集計・所持は名前で一意になる（MaterialType.SpecialItem）。
    private static MaterialCost Part(string name, int amount)
    {
        return new MaterialCost { materialType = MaterialType.SpecialItem, specialItemName = name, amount = amount };
    }

    private static AttributeResistance Res(MagicAttribute attribute, float multiplier)
    {
        return new AttributeResistance { attribute = attribute, multiplier = multiplier };
    }

    private static Def E(string fileId, string name, int hp, int atk, int def, float interval,
        MagicAttribute attr, AttributeResistance[] res, params MaterialCost[] drops)
    {
        return new Def
        {
            FileId = fileId, Name = name, MaxHp = hp, Atk = atk, Def_ = def, AttackInterval = interval,
            Attribute = attr,
            Resistances = new List<AttributeResistance>(res ?? new AttributeResistance[0]),
            Drops = new List<MaterialCost>(drops),
        };
    }

    private static readonly AttributeResistance[] None = new AttributeResistance[0];

    // 弱い順の正本。DungeonGenerator がプール順に流用する。
    public static readonly string[] OrderWeakToStrong =
    {
        "Slime", "ForestRat", "CaveBat", "GiantRat", "Hornet", "Mushroomling", "Goblin", "Wolf",
        "Boar", "Kobold", "PoisonSpider", "GoblinSoldier", "Harpy", "Lizardman", "TrentSapling", "Skeleton",
        "Zombie", "GargoyleLesser", "Imp", "ForestGuard", "CaveTroll", "Wight", "Ogre", "Basilisk",
        "Gargoyle", "HellHound", "Wyvern", "Nightmare", "Durahan", "LichAcolyte", "AncientTrent", "StoneTitan",
        "YoungDragon", "Vampire", "Demon", "Lich", "Behemoth", "Dragon", "AbyssHerald", "ChaosAvatar",
    };

    private static List<Def> BuildDefs()
    {
        return new List<Def>
        {
            // ---- 浅層：森の小物（深度1〜、2×2グリッドで戦える強さ）----
            E("Slime",        "スライム",       28,  3, 0, 4.2f, MagicAttribute.None,    None,                                                   Part("スライムゼリー", 2)),
            E("ForestRat",    "森ネズミ",       24,  3, 0, 3.8f, MagicAttribute.None,    None,                                                   Part("小さな牙", 2)),
            E("CaveBat",      "洞窟コウモリ",   34,  4, 0, 3.4f, MagicAttribute.Wind,    new[] { Res(MagicAttribute.Wind, 0.7f) },                Part("薄い翼膜", 2)),
            E("GiantRat",     "大ネズミ",       48,  5, 1, 3.6f, MagicAttribute.Dark,    new[] { Res(MagicAttribute.Wind, 1.3f) },                Part("大ネズミの尾", 2)),
            E("Hornet",       "大スズメバチ",   42,  6, 0, 2.9f, MagicAttribute.Wind,    new[] { Res(MagicAttribute.Wind, 0.6f), Res(MagicAttribute.Fire, 1.5f) }, Part("毒針", 2)),
            E("Mushroomling", "キノコ人間",     70,  5, 2, 4.2f, MagicAttribute.None,    new[] { Res(MagicAttribute.Fire, 1.6f) },                Part("胞子嚢", 2)),

            // ---- 森の中型（深度6〜）----
            E("Goblin",       "ゴブリン",       88,  8, 3, 3.5f, MagicAttribute.Dark,    new[] { Res(MagicAttribute.Dark, 0.7f), Res(MagicAttribute.Light, 1.4f) }, Part("ゴブリンの牙", 2)),
            E("Wolf",         "森オオカミ",     80, 10, 2, 2.7f, MagicAttribute.None,    None,                                                   Part("鋭い爪", 2)),
            E("Boar",         "荒れイノシシ",  120,  9, 4, 3.4f, MagicAttribute.None,    None,                                                   Part("剛毛", 2)),
            E("Kobold",       "コボルト",       98, 11, 3, 3.0f, MagicAttribute.Fire,    new[] { Res(MagicAttribute.Fire, 0.7f) },                Part("錆びた短剣", 2)),
            E("PoisonSpider", "毒グモ",        105, 12, 3, 3.0f, MagicAttribute.Dark,    None,                                                   Part("蜘蛛の糸", 2), Part("毒腺", 1)),
            E("GoblinSoldier","ゴブリン戦士",  160, 14, 6, 3.2f, MagicAttribute.Dark,    new[] { Res(MagicAttribute.Dark, 0.6f), Res(MagicAttribute.Light, 1.3f) }, Part("鉄の兜", 2)),
            E("Harpy",        "ハーピー",      135, 15, 4, 2.5f, MagicAttribute.Wind,    new[] { Res(MagicAttribute.Wind, 0.5f), Res(MagicAttribute.Thunder, 1.4f) }, Part("風切羽", 2)),
            E("Lizardman",    "リザードマン",  175, 16, 7, 3.0f, MagicAttribute.Fire,    new[] { Res(MagicAttribute.Fire, 0.6f), Res(MagicAttribute.Wind, 1.3f) }, Part("竜人の鱗", 2)),
            E("TrentSapling", "トレントの苗",  200, 12, 9, 3.6f, MagicAttribute.Wind,    new[] { Res(MagicAttribute.Wind, 0.5f), Res(MagicAttribute.Fire, 1.8f) }, Part("若木の枝", 2)),

            // ---- 遺跡・洞窟（深度13〜）----
            E("Skeleton",     "スケルトン",    180, 17, 5, 2.7f, MagicAttribute.Dark,    new[] { Res(MagicAttribute.Dark, 0.5f), Res(MagicAttribute.Light, 1.6f), Res(MagicAttribute.Wind, 1.2f) }, Part("古びた骨", 2)),
            E("Zombie",       "ゾンビ",        260, 15, 6, 3.6f, MagicAttribute.Dark,    new[] { Res(MagicAttribute.Dark, 0.4f), Res(MagicAttribute.Fire, 1.5f), Res(MagicAttribute.Light, 1.5f) }, Part("腐肉", 2)),
            E("GargoyleLesser","小ガーゴイル", 230, 19, 13, 3.2f, MagicAttribute.None,   new[] { Res(MagicAttribute.Thunder, 0.7f) },             Part("石の破片", 2)),
            E("Imp",          "インプ",        210, 22, 6, 2.5f, MagicAttribute.Fire,    new[] { Res(MagicAttribute.Fire, 0.4f), Res(MagicAttribute.Light, 1.4f) }, Part("悪魔の角", 2)),
            E("ForestGuard",  "森の番人",      320, 21, 10, 3.0f, MagicAttribute.Wind,   new[] { Res(MagicAttribute.Wind, 0.5f), Res(MagicAttribute.Fire, 1.5f), Res(MagicAttribute.Light, 1.3f) }, Part("番人の樹皮", 2), Part("古木の芯", 1)),
            E("CaveTroll",    "洞窟トロル",    460, 25, 11, 3.2f, MagicAttribute.None,   new[] { Res(MagicAttribute.Fire, 1.4f) },                Part("トロルの生皮", 2)),
            E("Wight",        "ワイト",        360, 27, 9, 2.7f, MagicAttribute.Dark,    new[] { Res(MagicAttribute.Dark, 0.4f), Res(MagicAttribute.Light, 1.7f) }, Part("呪われた指輪", 1)),
            E("Ogre",         "オーガ",        540, 31, 12, 3.2f, MagicAttribute.None,   None,                                                   Part("オーガの牙", 2)),
            E("Basilisk",     "バジリスク",    400, 29, 11, 2.8f, MagicAttribute.Wind,   new[] { Res(MagicAttribute.Wind, 0.5f), Res(MagicAttribute.Light, 1.4f) }, Part("石化の眼", 1)),

            // ---- 魔性・上位（深度21〜）----
            E("Gargoyle",     "ガーゴイル",    470, 28, 17, 3.0f, MagicAttribute.None,   new[] { Res(MagicAttribute.Thunder, 0.6f), Res(MagicAttribute.Wind, 0.8f) }, Part("魔石の欠片", 2)),
            E("HellHound",    "ヘルハウンド",  420, 34, 10, 2.3f, MagicAttribute.Fire,   new[] { Res(MagicAttribute.Fire, 0.3f), Res(MagicAttribute.Thunder, 1.4f) }, Part("焦げた牙", 2)),
            E("Wyvern",       "ワイバーン",    650, 37, 15, 2.8f, MagicAttribute.Wind,   new[] { Res(MagicAttribute.Wind, 0.4f), Res(MagicAttribute.Thunder, 1.5f) }, Part("飛竜の翼膜", 2)),
            E("Nightmare",    "ナイトメア",    580, 39, 12, 2.5f, MagicAttribute.Dark,   new[] { Res(MagicAttribute.Dark, 0.3f), Res(MagicAttribute.Light, 1.6f) }, Part("悪夢の霧", 1)),
            E("Durahan",      "デュラハン",    720, 41, 19, 3.0f, MagicAttribute.Dark,   new[] { Res(MagicAttribute.Dark, 0.4f), Res(MagicAttribute.Light, 1.5f) }, Part("首無しの兜", 1)),
            E("LichAcolyte",  "リッチの従者",  660, 43, 13, 2.7f, MagicAttribute.Dark,   new[] { Res(MagicAttribute.Dark, 0.3f), Res(MagicAttribute.Fire, 1.4f), Res(MagicAttribute.Light, 1.5f) }, Part("死霊術の書片", 1)),
            E("AncientTrent", "エンシェントトレント", 1150, 41, 23, 3.4f, MagicAttribute.Wind, new[] { Res(MagicAttribute.Wind, 0.4f), Res(MagicAttribute.Fire, 1.9f), Res(MagicAttribute.Thunder, 1.2f) }, Part("世界樹の若枝", 1)),
            E("StoneTitan",   "石の巨神",     1350, 47, 31, 3.4f, MagicAttribute.None,   new[] { Res(MagicAttribute.Thunder, 0.6f), Res(MagicAttribute.Fire, 0.9f) }, Part("巨神の核", 1)),

            // ---- ボス級・深淵（深度31〜）----
            E("YoungDragon",  "幼竜",          980, 53, 20, 2.8f, MagicAttribute.Fire,   new[] { Res(MagicAttribute.Fire, 0.3f), Res(MagicAttribute.Wind, 1.3f) }, Part("竜のうろこ", 2)),
            E("Vampire",      "ヴァンパイア",  900, 56, 18, 2.3f, MagicAttribute.Dark,   new[] { Res(MagicAttribute.Dark, 0.2f), Res(MagicAttribute.Light, 1.9f) }, Part("吸血鬼の牙", 1)),
            E("Demon",        "デーモン",     1250, 61, 25, 2.8f, MagicAttribute.Fire,   new[] { Res(MagicAttribute.Fire, 0.3f), Res(MagicAttribute.Dark, 0.6f), Res(MagicAttribute.Light, 1.5f) }, Part("混沌の血", 1)),
            E("Lich",         "リッチ",       1080, 59, 21, 2.6f, MagicAttribute.Dark,   new[] { Res(MagicAttribute.Dark, 0.2f), Res(MagicAttribute.Fire, 1.4f), Res(MagicAttribute.Light, 1.7f) }, Part("命の宝珠", 1)),
            E("Behemoth",     "ベヒモス",     1950, 67, 29, 3.0f, MagicAttribute.None,   None,                                                   Part("ベヒモスの角", 1)),
            E("Dragon",       "ドラゴン",     1750, 73, 31, 2.7f, MagicAttribute.Fire,   new[] { Res(MagicAttribute.Fire, 0.25f), Res(MagicAttribute.Thunder, 1.3f) }, Part("竜王のうろこ", 1), Part("竜の心臓", 1)),
            E("AbyssHerald",  "深淵の使者",   1550, 82, 27, 2.3f, MagicAttribute.Dark,   new[] { Res(MagicAttribute.Dark, 0.2f), Res(MagicAttribute.Light, 1.8f) }, Part("深淵の欠片", 1)),
            E("ChaosAvatar",  "混沌の化身",   2500, 92, 35, 2.6f, MagicAttribute.None,   new[] { Res(MagicAttribute.Fire, 0.5f), Res(MagicAttribute.Thunder, 0.5f), Res(MagicAttribute.Wind, 0.5f), Res(MagicAttribute.Light, 0.5f), Res(MagicAttribute.Dark, 0.5f) }, Part("混沌の核", 1)),
        };
    }

    [MenuItem("Grimoire/Generate Enemy Data")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets", "EnemyData");
        }

        List<Def> defs = BuildDefs();

        // 生成順と OrderWeakToStrong の整合性チェック（取りこぼし防止）。
        if (defs.Count != OrderWeakToStrong.Length)
        {
            Debug.LogWarning($"[Grimoire] 敵定義数 {defs.Count} と OrderWeakToStrong {OrderWeakToStrong.Length} が不一致です。");
        }

        // ドロップ名が MonsterPartCatalog（トレーダー変換・錬金釜・ハイドアウトコストが参照する正本）に
        // 登録されているか確認する。未登録だと変換ルートが tier1 相当にフォールバックしてしまう。
        foreach (Def d in defs)
        {
            foreach (MaterialCost drop in d.Drops)
            {
                if (drop.materialType == MaterialType.SpecialItem && !MonsterPartCatalog.IsKnown(drop.specialItemName))
                    Debug.LogWarning($"[Grimoire] ドロップ「{drop.specialItemName}」（{d.Name}）が MonsterPartCatalog に未登録です。");
            }
        }

        int created = 0, updated = 0;
        foreach (Def d in defs)
        {
            string path = $"{OutputFolder}/{d.FileId}.asset";
            EnemyData asset = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(asset, path);
                created++;
            }
            else
            {
                updated++;
            }

            asset.enemyName = d.Name;
            asset.maxHp = d.MaxHp;
            asset.atk = d.Atk;
            asset.def = d.Def_;
            asset.attackInterval = d.AttackInterval;
            asset.attribute = d.Attribute;
            asset.resistances = d.Resistances;
            asset.drops = d.Drops;

            EditorUtility.SetDirty(asset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Grimoire] 敵データ生成完了: 新規{created}件 / 更新{updated}件（合計{defs.Count}件）。数値は仮バランスです。" +
                  " この後 Grimoire > Generate Dungeon でプールを更新してください。");
    }
}
