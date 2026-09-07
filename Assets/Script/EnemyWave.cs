using System;
using System.Collections.Generic;

// ダンジョンの1ウェーブ（同時に出現する敵の集まり）。DungeonManager.waves に並べる。
[Serializable]
public class EnemyWave
{
    public List<EnemyData> enemies = new List<EnemyData>();
}
