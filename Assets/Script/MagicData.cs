using UnityEngine;
using System.Collections.Generic;

// 魔法の系統（企画書 4章の分類に対応）
public enum MagicCategory
{
    Attack,        // 通常攻撃魔法（単体/全体威力系統）
    StatusInflict, // 状態異常専用魔法
    Support,       // 補助魔法（発動制御系）
    BuffActive,    // バフ魔法（アクティブ）
    BuffPassive,   // パッシブ魔法（常時効果）
}

public enum MagicAttribute
{
    None,
    Fire,
    Thunder,
    Wind,
    Light,
    Dark,
}

public enum MagicRange
{
    None,   // 対象を取らない（補助/バフ等）
    Single, // 単体対象
    AoE,    // 全体対象
}

public enum StatusEffectType
{
    None,
    Burn,       // 火傷
    Shock,      // 感電
    Laceration, // 裂傷
    Dizzy,      // 眩暈
    Blind,      // 盲目
}

// バフの対象ステータス（PlayerStatusのHP/Atk/Def/Spd/Lucに加え、企画書のダメージバフ用にDamageを含む）
public enum BuffStat
{
    None,
    Atk,
    Def,
    Spd,
    Luc,
    Damage,
}

public enum MaterialType
{
    SmallManaCrystal,  // 小魔力結晶
    MediumManaCrystal, // 中魔力結晶
    LargeManaCrystal,  // 大魔力結晶
    ElementFragment,   // {属性}エレメントの欠片
    Element,           // {属性}エレメント
    SpecialItem,       // 固有アイテム（例: 効率的魔力運用のすすめ）
}

[System.Serializable]
public class MaterialCost
{
    public MaterialType materialType;
    public MagicAttribute attribute;    // materialTypeがElementFragment/Elementの場合のみ使用
    public string specialItemName;      // materialTypeがSpecialItemの場合のみ使用
    public int amount = 1;
}

[CreateAssetMenu(fileName = "NewMagicData", menuName = "Grimoire/MagicData")]
public class MagicData : ScriptableObject
{
    [Header("基本情報")]
    public string magicName;        // 魔法名
    public MagicCategory category = MagicCategory.Attack;
    public MagicAttribute attribute = MagicAttribute.None;
    public MagicRange range = MagicRange.Single;
    public float interval;          // 発動間隔（秒）
    public int damage;              // ダメージ

    [Header("状態異常")]
    public StatusEffectType statusEffect = StatusEffectType.None;
    [Range(0, 100)] public int statusEffectChance; // 付与率（％）

    [Header("バフ設定 (category が BuffActive / BuffPassive の場合のみ使用)")]
    public BuffStat buffStat = BuffStat.None;
    public float buffDuration;      // アクティブバフの効果時間（秒）。パッシブは0
    [Range(1, 3)] public int passiveStage = 1; // パッシブの強化段階

    [Header("補助魔法設定 (category が Support の場合のみ使用)")]
    public int supportRepeatCount = 1; // 直前に発動した魔法を何回追加発動させるか（アッドスペル=1, デュアルスペル=2）

    [Header("説明文（UI表示用）")]
    [TextArea] public string effectDescription;

    [Header("パズル形状 (0,0を中心とした相対座標)")]
    public List<Vector2Int> shapeNodes = new List<Vector2Int>();

    [Header("表示設定")]
    public Color pieceColor = Color.white; // ピースの色（属性ごとに変えると見やすい）

    [Header("解放に必要な素材")]
    public List<MaterialCost> requiredMaterials = new List<MaterialCost>();
}
