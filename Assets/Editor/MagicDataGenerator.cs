using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 企画書 4章（魔法マスターデータ）・6章（クラフト素材コスト）・8章（グリッド形状データ）に基づき、
// Assets/MasicData 配下の MagicData アセットを一括生成・更新するEditor拡張。
// メニュー: Grimoire > Generate Master Magic Data
//
// 8章の座標は (列, 行) で原点(0,0)が左上・行は下方向に増加する表記だが、
// プロジェクトのグリッド座標系はY上方向が正のため、Shape()では row を反転して変換している。
public static class MagicDataGenerator
{
    private const string OutputFolder = "Assets/MasicData";

    private class AttrInfo
    {
        public MagicAttribute Attribute;
        public string Id;          // 単体系統・属性系アセットのファイル名ベース (Fire/Thunder/Wind/Light/Dark)
        public string AoeBase;     // 全体系統のファイル名ベース (Flame/Bolt/Storm/Shine/Darkness)
        public string[] SingleNames; // 表示名：単体Tier1-3
        public string[] AoeNames;    // 表示名：全体Tier1-3
        public string StatusName;    // 状態異常専用魔法の表示名
        public string BuffName;      // アクティブバフの表示名
        public StatusEffectType Status;
        public BuffStat Buff;
        public Color Color;
        public List<Vector2Int>[] SingleShapes; // 単体Tier1-3の形状
        public List<Vector2Int>[] AoeShapes;    // 全体Tier1-3の形状
        public List<Vector2Int> StatusShape;    // 状態異常専用魔法の形状
    }

    private class Def
    {
        public string FileId;   // アセットファイル名（英語、既存資産との互換のため）
        public string Name;     // magicName（日本語表示名）
        public MagicCategory Category;
        public MagicAttribute Attribute = MagicAttribute.None;
        public MagicRange Range = MagicRange.None;
        public float Interval;
        public int Damage;
        public StatusEffectType StatusEffect = StatusEffectType.None;
        public int StatusChance;
        public BuffStat BuffStat = BuffStat.None;
        public float BuffDuration;
        public int PassiveStage = 1;
        public int SupportRepeatCount = 1;
        public string EffectDescription = "";
        public Color PieceColor = Color.white;
        public List<MaterialCost> Materials = new List<MaterialCost>();
        public List<Vector2Int> Shape = new List<Vector2Int>();
    }

    private static readonly Dictionary<MagicAttribute, string> AttrLabel = new Dictionary<MagicAttribute, string>
    {
        { MagicAttribute.Fire, "炎" },
        { MagicAttribute.Thunder, "雷" },
        { MagicAttribute.Wind, "風" },
        { MagicAttribute.Light, "光" },
        { MagicAttribute.Dark, "闇" },
    };

    // 全属性共通のバフ系アイコン形状（8章「形状デザインの傾向」より、属性を問わず共通パターン）
    private static readonly List<Vector2Int> BuffActiveShape = Shape(0, 0, 1, 0, 2, 0);       // 3マス横一列
    private static readonly List<Vector2Int> PassiveShape = Shape(0, 0);                       // 1マス
    private static readonly List<Vector2Int> AttrBuffShape = Shape(0, 0, 1, 0);                // 2マス横並び
    private static readonly List<Vector2Int> StatusRateBuffShape = Shape(0, 0, 1, -1);         // 2マス斜め

    [MenuItem("Grimoire/Generate Master Magic Data")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            Debug.LogError($"{OutputFolder} フォルダが見つかりません。先にフォルダを作成してください。");
            return;
        }

        var attrs = new List<AttrInfo>
        {
            new AttrInfo
            {
                Attribute = MagicAttribute.Fire, Id = "Fire", AoeBase = "Flame",
                SingleNames = new[] { "ファイア", "メガファイア", "ギガファイア" },
                AoeNames = new[] { "フレイム", "メガフレイム", "ギガフレイム" },
                StatusName = "火あぶり", BuffName = "攻撃力バフ",
                Status = StatusEffectType.Burn, Buff = BuffStat.Atk,
                Color = new Color(1f, 0f, 0f),
                SingleShapes = new[]
                {
                    Shape(0, 0, 0, -1),
                    Shape(0, 0, 0, -1, 1, -1, 0, -2),
                    Shape(0, 0, 1, 0, 2, 0, 0, -1, 1, -1, 0, -2),
                },
                AoeShapes = new[]
                {
                    Shape(0, 0, 1, 0, 0, -1, 1, -1),
                    Shape(0, 0, 2, 0, 0, -1, 1, -1, 2, -1, 0, -2, 2, -2),
                    Shape(0, 0, 2, 0, 0, -1, 1, -1, 3, -1, 4, -1, 0, -2, 2, -2, 0, -3, 0, -4),
                },
                StatusShape = Shape(1, 0, 0, -1, 2, -1, 1, -2),
            },
            new AttrInfo
            {
                Attribute = MagicAttribute.Thunder, Id = "Thunder", AoeBase = "Bolt",
                SingleNames = new[] { "サンダー", "メガサンダー", "ギガサンダー" },
                AoeNames = new[] { "ボルト", "メガボルト", "ギガボルト" },
                StatusName = "電磁波", BuffName = "防御力バフ",
                Status = StatusEffectType.Shock, Buff = BuffStat.Def,
                Color = new Color(1f, 1f, 0f),
                SingleShapes = new[]
                {
                    Shape(0, 0, 0, -1),
                    Shape(0, 0, 1, 0, 0, -1, 1, -1),
                    Shape(0, 0, 0, -1, 1, -1, 1, -2, 1, -3),
                },
                AoeShapes = new[]
                {
                    Shape(0, 0, 0, -1, 1, -1, 2, -1),
                    Shape(0, 0, 2, 0, 0, -1, 1, -1, 0, -2, 1, -2, 2, -2),
                    Shape(0, 0, 1, 0, 2, 0, 3, 0, 0, -1, 0, -2, 0, -3, 1, -3, 2, -3, 0, -4),
                },
                StatusShape = Shape(0, 0, 1, 0, 1, -1, 1, -2),
            },
            new AttrInfo
            {
                Attribute = MagicAttribute.Wind, Id = "Wind", AoeBase = "Storm",
                SingleNames = new[] { "ウィンド", "メガウィンド", "ギガウィンド" },
                AoeNames = new[] { "ストーム", "メガストーム", "ギガストーム" },
                StatusName = "かまいたち", BuffName = "速さバフ",
                Status = StatusEffectType.Laceration, Buff = BuffStat.Spd,
                Color = new Color(0f, 0.8f, 0.2f),
                SingleShapes = new[]
                {
                    Shape(0, 0, 0, -1),
                    Shape(0, 0, 1, 0, 2, 0, 0, -1),
                    Shape(0, 0, 1, 0, 1, -1, 1, -2, 2, -2, 1, -3),
                },
                AoeShapes = new[]
                {
                    Shape(0, 0, 1, 0, 2, 0, 1, -1),
                    Shape(0, 0, 1, 0, 2, 0, 0, -1, 1, -1, 2, -1, 1, -2),
                    Shape(0, 0, 1, 0, 2, 0, 3, 0, 4, 0, 2, -1, 1, -2, 2, -2, 3, -2, 2, -3),
                },
                StatusShape = Shape(0, 0, 1, -1, 0, -2, 2, -2),
            },
            new AttrInfo
            {
                Attribute = MagicAttribute.Light, Id = "Light", AoeBase = "Shine",
                SingleNames = new[] { "ライト", "メガライト", "ギガライト" },
                AoeNames = new[] { "シャイン", "メガシャイン", "ギガシャイン" },
                StatusName = "目くらまし", BuffName = "運バフ",
                Status = StatusEffectType.Dizzy, Buff = BuffStat.Luc,
                Color = new Color(1f, 0.85f, 0.3f),
                SingleShapes = new[]
                {
                    Shape(0, 0, 0, -1),
                    Shape(0, 0, 1, -1, 2, -1, 0, -2),
                    Shape(0, 0, 2, 0, 0, -1, 1, -1, 0, -2, 1, -3),
                },
                AoeShapes = new[]
                {
                    Shape(0, 0, 1, 0, 1, -1, 2, -1),
                    Shape(0, 0, 1, 0, 0, -1, 1, -1, 0, -2, 1, -2, 2, -2),
                    Shape(2, 0, 3, 0, 2, -1, 0, -2, 1, -2, 2, -2, 3, -2, 1, -3, 0, -4, 1, -4),
                },
                StatusShape = Shape(0, 0, 1, 0, 0, -1, 0, -2),
            },
            new AttrInfo
            {
                Attribute = MagicAttribute.Dark, Id = "Dark", AoeBase = "Darkness",
                SingleNames = new[] { "ダーク", "メガダーク", "ギガダーク" },
                AoeNames = new[] { "ダークネス", "メガダークネス", "ギガダークネス" },
                StatusName = "目隠し", BuffName = "ダメージバフ",
                Status = StatusEffectType.Blind, Buff = BuffStat.Damage,
                Color = new Color(0.5f, 0f, 0.6f),
                SingleShapes = new[]
                {
                    Shape(0, 0, 0, -1),
                    Shape(0, 0, 1, 0, 1, -1, 0, -2),
                    Shape(0, 0, 1, 0, 0, -1, 1, -1, 0, -2, 1, -2),
                },
                AoeShapes = new[]
                {
                    Shape(0, 0, 2, 0, 1, -1, 2, -1),
                    Shape(0, 0, 1, 0, 0, -1, 2, -1, 0, -2, 1, -2, 2, -2),
                    Shape(0, 0, 0, -1, 0, -2, 2, -2, 0, -3, 1, -3, 2, -3, 0, -4, 1, -4, 2, -4),
                },
                StatusShape = Shape(0, 0, 2, 0, 0, -1, 1, -1),
            },
        };

        int[] singleIntervals = { 3, 5, 7 };
        int[] singleDamages = { 5, 10, 20 };
        int[] singleChances = { 0, 10, 20 };
        string[] tierPrefix = { "", "Mega", "Giga" };

        int[] aoeIntervals = { 5, 7, 9 };
        int[] aoeDamages = { 3, 6, 12 };
        int[] aoeChances = { 0, 10, 20 };

        var defs = new List<Def>();

        foreach (var a in attrs)
        {
            // 4.1/4.3/4.5/4.7/4.9: 単体威力系統（Tier1は初期解放、Tier2は小結晶x3、Tier3は中結晶x1 : 6.1節）
            for (int tier = 0; tier < 3; tier++)
            {
                var materials = new List<MaterialCost>();
                if (tier == 1) materials.Add(Mat(MaterialType.SmallManaCrystal, 3));
                if (tier == 2) materials.Add(Mat(MaterialType.MediumManaCrystal, 1));

                defs.Add(new Def
                {
                    FileId = tierPrefix[tier] + a.Id,
                    Name = a.SingleNames[tier],
                    Category = MagicCategory.Attack,
                    Attribute = a.Attribute,
                    Range = MagicRange.Single,
                    Interval = singleIntervals[tier],
                    Damage = singleDamages[tier],
                    StatusEffect = a.Status,
                    StatusChance = singleChances[tier],
                    PieceColor = a.Color,
                    Materials = materials,
                    Shape = a.SingleShapes[tier],
                });
            }

            // 4.2/4.4/4.6/4.8/4.10: 全体威力系統（Tier2は中結晶x5+欠片x1、Tier3は大結晶x10+エレメントx3 : 6.1節）
            // Tier1のコストは資料に明記が無いため、単体Tier1と同様に初期解放として扱っている（要確認）
            for (int tier = 0; tier < 3; tier++)
            {
                var materials = new List<MaterialCost>();
                if (tier == 1)
                {
                    materials.Add(Mat(MaterialType.MediumManaCrystal, 5));
                    materials.Add(MatAttr(MaterialType.ElementFragment, a.Attribute, 1));
                }
                if (tier == 2)
                {
                    materials.Add(Mat(MaterialType.LargeManaCrystal, 10));
                    materials.Add(MatAttr(MaterialType.Element, a.Attribute, 3));
                }

                defs.Add(new Def
                {
                    FileId = tierPrefix[tier] + a.AoeBase,
                    Name = a.AoeNames[tier],
                    Category = MagicCategory.Attack,
                    Attribute = a.Attribute,
                    Range = MagicRange.AoE,
                    Interval = aoeIntervals[tier],
                    Damage = aoeDamages[tier],
                    StatusEffect = a.Status,
                    StatusChance = aoeChances[tier],
                    PieceColor = a.Color,
                    Materials = materials,
                    Shape = a.AoeShapes[tier],
                });
            }

            // 4.11/6.2: 状態異常専用魔法
            defs.Add(new Def
            {
                FileId = "Status" + a.Status,
                Name = a.StatusName,
                Category = MagicCategory.StatusInflict,
                Attribute = a.Attribute,
                Range = MagicRange.Single,
                Interval = 7,
                Damage = 0,
                StatusEffect = a.Status,
                StatusChance = 80,
                PieceColor = a.Color,
                Materials = new List<MaterialCost>
                {
                    Mat(MaterialType.MediumManaCrystal, 2),
                    MatAttr(MaterialType.ElementFragment, a.Attribute, 1),
                },
                Shape = a.StatusShape,
            });

            // 4.13/6.4: バフ魔法（アクティブ、効果時間30秒）
            defs.Add(new Def
            {
                FileId = "Buff" + a.Buff,
                Name = a.BuffName,
                Category = MagicCategory.BuffActive,
                Attribute = a.Attribute,
                Range = MagicRange.None,
                Interval = 15,
                Damage = 0,
                BuffStat = a.Buff,
                BuffDuration = 30,
                EffectDescription = "効果時間30秒",
                PieceColor = a.Color,
                Materials = new List<MaterialCost>
                {
                    Mat(MaterialType.MediumManaCrystal, 3),
                    MatAttr(MaterialType.ElementFragment, a.Attribute, 1),
                },
                Shape = BuffActiveShape,
            });

            // 4.14/6.5: パッシブ（対応ステータスバフ、3段階。段階ごとに1種類の結晶を消費）
            var passiveMaterials = new List<MaterialCost>[]
            {
                new List<MaterialCost> { Mat(MaterialType.SmallManaCrystal, 3) },
                new List<MaterialCost> { Mat(MaterialType.MediumManaCrystal, 1) },
                new List<MaterialCost> { Mat(MaterialType.LargeManaCrystal, 3) },
            };
            for (int stage = 1; stage <= 3; stage++)
            {
                defs.Add(new Def
                {
                    FileId = $"Buff{a.Buff}PassiveLv{stage}",
                    Name = $"{a.BuffName}（パッシブ）Lv{stage}",
                    Category = MagicCategory.BuffPassive,
                    Attribute = a.Attribute,
                    Range = MagicRange.None,
                    BuffStat = a.Buff,
                    PassiveStage = stage,
                    EffectDescription = "常時効果（多段階強化）",
                    PieceColor = a.Color,
                    Materials = passiveMaterials[stage - 1],
                    Shape = PassiveShape,
                });
            }

            // 4.14/6.5: 属性バフ（パッシブ、汎用強化）
            defs.Add(new Def
            {
                FileId = "AttrBuff" + a.Id,
                Name = $"{AttrLabel[a.Attribute]}属性バフ",
                Category = MagicCategory.BuffPassive,
                Attribute = a.Attribute,
                Range = MagicRange.None,
                EffectDescription = "対応属性の魔法威力を強化する（常時効果）",
                PieceColor = a.Color,
                Materials = new List<MaterialCost> { Mat(MaterialType.LargeManaCrystal, 3) },
                Shape = AttrBuffShape,
            });

            // 4.14/6.5: 状態異常付与率バフ（パッシブ、汎用強化）
            defs.Add(new Def
            {
                FileId = "StatusRateBuff" + a.Id,
                Name = $"{AttrLabel[a.Attribute]}状態異常付与率バフ",
                Category = MagicCategory.BuffPassive,
                Attribute = a.Attribute,
                Range = MagicRange.None,
                StatusEffect = a.Status,
                EffectDescription = "対応属性の状態異常付与率を強化する（常時効果）",
                PieceColor = a.Color,
                Materials = new List<MaterialCost> { Mat(MaterialType.LargeManaCrystal, 3) },
                Shape = StatusRateBuffShape,
            });
        }

        // 4.12/6.3: 補助魔法（発動制御系）
        defs.Add(new Def
        {
            FileId = "AddSpell",
            Name = "アッドスペル",
            Category = MagicCategory.Support,
            Range = MagicRange.None,
            Interval = 5,
            Damage = 0,
            SupportRepeatCount = 1,
            EffectDescription = "直前に発動した魔法をもう一度発動させる",
            PieceColor = new Color(0.7f, 0.7f, 0.7f),
            Materials = new List<MaterialCost>
            {
                MatSpecial("効率的魔力運用のすすめ", 1),
                Mat(MaterialType.LargeManaCrystal, 10),
            },
            Shape = Shape(0, 0, 1, 0, 1, -1, 2, -1, 1, -2, 2, -2),
        });
        defs.Add(new Def
        {
            FileId = "DualSpell",
            Name = "デュアルスペル",
            Category = MagicCategory.Support,
            Range = MagicRange.None,
            Interval = 10,
            Damage = 0,
            SupportRepeatCount = 2,
            EffectDescription = "直前に発動した魔法を2回発動させる",
            PieceColor = new Color(0.7f, 0.7f, 0.7f),
            Materials = new List<MaterialCost>
            {
                MatSpecial("超効率的魔力運用のすすめ", 1),
                Mat(MaterialType.LargeManaCrystal, 35),
            },
            Shape = Shape(0, 0, 1, 0, 2, 0, 3, 0, 0, -1, 1, -1, 2, -1, 3, -1, 0, -2, 1, -2, 2, -2, 3, -2),
        });

        int created = 0, updated = 0;
        foreach (var d in defs)
        {
            string path = $"{OutputFolder}/{d.FileId}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<MagicData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MagicData>();
                AssetDatabase.CreateAsset(asset, path);
                created++;
            }
            else
            {
                updated++;
            }

            asset.magicName = d.Name;
            asset.category = d.Category;
            asset.attribute = d.Attribute;
            asset.range = d.Range;
            asset.interval = d.Interval;
            asset.damage = d.Damage;

            // マナコストは ManaRules を単一の真実源とし、確定値をアセットへ焼き込む（0 に戻してから再算出）
            asset.manaCost = 0;
            asset.manaCost = ManaRules.CastCost(asset);
            asset.statusEffect = d.StatusEffect;
            asset.statusEffectChance = d.StatusChance;
            asset.buffStat = d.BuffStat;
            asset.buffDuration = d.BuffDuration;
            asset.passiveStage = d.PassiveStage;
            asset.supportRepeatCount = d.SupportRepeatCount;
            asset.effectDescription = d.EffectDescription;
            asset.pieceColor = d.PieceColor;
            asset.requiredMaterials = d.Materials;
            asset.shapeNodes = new List<Vector2Int>(d.Shape); // 8章の形状データで上書き

            EditorUtility.SetDirty(asset);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Grimoire] 魔法マスターデータ生成完了: 新規{created}件 / 更新{updated}件（合計{defs.Count}件）。" +
                  "形状データ（shapeNodes）も8章の内容で設定済みです。");
    }

    private static List<Vector2Int> Shape(params int[] xy)
    {
        var list = new List<Vector2Int>();
        for (int i = 0; i < xy.Length; i += 2)
        {
            list.Add(new Vector2Int(xy[i], xy[i + 1]));
        }
        return list;
    }

    private static MaterialCost Mat(MaterialType type, int amount)
    {
        return new MaterialCost { materialType = type, amount = amount };
    }

    private static MaterialCost MatAttr(MaterialType type, MagicAttribute attribute, int amount)
    {
        return new MaterialCost { materialType = type, attribute = attribute, amount = amount };
    }

    private static MaterialCost MatSpecial(string name, int amount)
    {
        return new MaterialCost { materialType = MaterialType.SpecialItem, specialItemName = name, amount = amount };
    }
}
