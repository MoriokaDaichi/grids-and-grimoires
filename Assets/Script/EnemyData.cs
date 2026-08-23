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
}
