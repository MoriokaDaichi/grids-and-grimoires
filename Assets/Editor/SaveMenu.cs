using UnityEditor;
using UnityEngine;

public static class SaveMenu
{
    [MenuItem("Grimoire/Wipe Save")]
    public static void Wipe()
    {
        SaveManager.Wipe();
        Debug.Log("[Grimoire] セーブデータを削除しました: " + Application.persistentDataPath + "/gg_save.json");
    }
}
