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

// セーブデータ全体。Phase 3 以降で unlockedMagicIds / passiveStages / statAllocation を追加予定。
[Serializable]
public class SaveData
{
    public List<MaterialStack> materials = new List<MaterialStack>();
}
