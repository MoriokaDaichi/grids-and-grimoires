using System;
using System.Collections.Generic;

// JSONセーブの1レコード。JsonUtility は Dictionary を扱えないためリストで持つ。
[Serializable]
public class MaterialStack
{
    public int materialType;      // (int)MaterialType
    public int attribute;         // (int)MagicAttribute（欠片/エレメントのみ意味を持つ）
    public string specialItemName; // 固有アイテムのみ
    public int count;
}

// JsonUtility は Dictionary を扱えないため、文字列→整数のペアをリストで持つ。
[Serializable]
public class StringIntPair
{
    public string key;
    public int value;
}

// セーブデータ全体。各システムは「読み込み→自分の領域だけ更新→書き込み」で他システムのフィールドを保つこと。
[Serializable]
public class SaveData
{
    public List<MaterialStack> materials = new List<MaterialStack>();

    // 研究で解放済みの魔法ID（= MagicData のアセット名）。コスト0の魔法は常に解放扱いなので含めない。
    public List<string> unlockedMagicIds = new List<string>();

    // トレーダーのタスク進捗
    public List<string> completedTaskIds = new List<string>();
    public int lifetimeEnemyKills;
    public int bestDungeonDepth;
    public List<StringIntPair> enemyKillCounts = new List<StringIntPair>();
}
