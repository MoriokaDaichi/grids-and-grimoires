using System;
using System.Collections.Generic;
using UnityEngine;

// 属性ごとの被ダメージ倍率。multiplier < 1 で軽減、> 1 で弱点。未指定の属性は 1.0。
[Serializable]
public class AttributeResistance
{
    public MagicAttribute attribute;
    [Range(0f, 3f)] public float multiplier = 1f;
}

// ダンジョンの敵1体分のマスターデータ。企画書に数値表が無いため、暫定バランスとして仮決めしたもの。
// Grimoire > Generate Enemy Data (Assets/Editor/EnemyDataGenerator.cs) で一括生成する。
[CreateAssetMenu(fileName = "NewEnemyData", menuName = "Grimoire/EnemyData")]
public class EnemyData : ScriptableObject
{
    public string enemyName;
    public int maxHp;
    public int atk;
    public int def;
    public float attackInterval;

    [Header("属性（表示・将来の相性用）と被ダメージ倍率（企画書7章にデータが無いため仮）")]
    public MagicAttribute attribute = MagicAttribute.None;
    public List<AttributeResistance> resistances = new List<AttributeResistance>();

    [Header("撃破時のドロップ（企画書7章の敵表はヘッダのみのため数量は仮）")]
    public List<MaterialCost> drops = new List<MaterialCost>();

    [Header("戦闘画面の見た目（未設定可・グレー矩形で代替）")]
    public Sprite sprite;
}
