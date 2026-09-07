using System.Collections.Generic;
using UnityEngine;

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

    [Header("撃破時のドロップ（企画書7章の敵表はヘッダのみのため数量は仮）")]
    public List<MaterialCost> drops = new List<MaterialCost>();

    [Header("戦闘画面の見た目（未設定可・グレー矩形で代替）")]
    public Sprite sprite;
}
