using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 企画書に敵データの数値表が無いため、Tier1魔法（interval3秒/damage5、MagicDataGenerator.cs参照）を基準に
// 最初のダンジョン用の敵4体を仮決めして Assets/EnemyData 配下に生成するEditor拡張。
// メニュー: Grimoire > Generate Enemy Data
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

    private static MaterialCost Mat(MaterialType type, int amount)
    {
        return new MaterialCost { materialType = type, amount = amount };
    }

    private static MaterialCost Frag(MagicAttribute attribute, int amount)
    {
        return new MaterialCost { materialType = MaterialType.ElementFragment, attribute = attribute, amount = amount };
    }

    private static AttributeResistance Res(MagicAttribute attribute, float multiplier)
    {
        return new AttributeResistance { attribute = attribute, multiplier = multiplier };
    }

    [MenuItem("Grimoire/Generate Enemy Data")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets", "EnemyData");
        }

        var defs = new List<Def>
        {
            new Def { FileId = "Slime",       Name = "スライム",     MaxHp = 60,  Atk = 6,  Def_ = 2, AttackInterval = 4f,
                Attribute = MagicAttribute.Wind,
                Resistances = new List<AttributeResistance> { Res(MagicAttribute.Fire, 1.5f), Res(MagicAttribute.Thunder, 0.6f) },
                Drops = new List<MaterialCost> { Mat(MaterialType.SmallManaCrystal, 2) } },
            new Def { FileId = "Goblin",      Name = "ゴブリン",     MaxHp = 90,  Atk = 9,  Def_ = 4, AttackInterval = 3.5f,
                Attribute = MagicAttribute.Dark,
                Resistances = new List<AttributeResistance> { Res(MagicAttribute.Dark, 0.7f), Res(MagicAttribute.Light, 1.4f) },
                Drops = new List<MaterialCost> { Mat(MaterialType.SmallManaCrystal, 3) } },
            new Def { FileId = "GiantRat",    Name = "大ネズミ",     MaxHp = 80,  Atk = 11, Def_ = 3, AttackInterval = 3f,
                Attribute = MagicAttribute.Dark,
                Resistances = new List<AttributeResistance> { Res(MagicAttribute.Wind, 1.3f) },
                Drops = new List<MaterialCost> { Mat(MaterialType.SmallManaCrystal, 2), Frag(MagicAttribute.Wind, 1) } },
            new Def { FileId = "ForestGuard", Name = "森の番人",     MaxHp = 180, Atk = 14, Def_ = 6, AttackInterval = 3f,
                Attribute = MagicAttribute.Wind,
                Resistances = new List<AttributeResistance> { Res(MagicAttribute.Wind, 0.5f), Res(MagicAttribute.Fire, 1.5f), Res(MagicAttribute.Light, 1.3f) },
                Drops = new List<MaterialCost> { Mat(MaterialType.MediumManaCrystal, 1), Frag(MagicAttribute.Light, 1) } },
        };

        int created = 0, updated = 0;
        foreach (var d in defs)
        {
            string path = $"{OutputFolder}/{d.FileId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
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
        Debug.Log($"[Grimoire] 敵データ生成完了: 新規{created}件 / 更新{updated}件（合計{defs.Count}件）。数値は仮バランスです。");
    }
}
