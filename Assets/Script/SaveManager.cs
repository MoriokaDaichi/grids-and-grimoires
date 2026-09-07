using System.IO;
using UnityEngine;

// 単一JSONファイル（Application.persistentDataPath/gg_save.json）でのセーブ入出力。
// Serialize/Deserialize は純粋関数なので EditModeテストで検証できる。
public static class SaveManager
{
    private static string FilePath { get { return Application.persistentDataPath + "/gg_save.json"; } }

    public static SaveData Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new SaveData();
            return Deserialize(File.ReadAllText(FilePath));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Grimoire] セーブ読み込み失敗: " + e.Message);
            return new SaveData();
        }
    }

    public static void Save(SaveData data)
    {
        try
        {
            File.WriteAllText(FilePath, Serialize(data));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Grimoire] セーブ書き込み失敗: " + e.Message);
        }
    }

    public static void Wipe()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Grimoire] セーブ削除失敗: " + e.Message);
        }
    }

    public static string Serialize(SaveData data)
    {
        return JsonUtility.ToJson(data ?? new SaveData(), true);
    }

    public static SaveData Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json)) return new SaveData();
        SaveData data = JsonUtility.FromJson<SaveData>(json);
        return data ?? new SaveData();
    }
}
