using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// シーン内のMagicSpawnerに、Assets/MasicData配下の全MagicDataアセットを
// カテゴリ→属性→範囲→発動間隔の順で並べて登録するEditor拡張。
// メニュー: Grimoire > Populate MagicSpawner List
public static class MagicSpawnerPopulator
{
    private const string DataFolder = "Assets/MasicData";

    [MenuItem("Grimoire/Populate MagicSpawner List")]
    public static void Populate()
    {
        var spawner = Object.FindFirstObjectByType<MagicSpawner>(FindObjectsInactive.Include);
        if (spawner == null)
        {
            Debug.LogError("[Grimoire] 開いているシーンに MagicSpawner が見つかりません。SampleScene を開いた状態で実行してください。");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:MagicData", new[] { DataFolder });
        var list = new List<MagicData>();
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<MagicData>(path);
            if (data != null) list.Add(data);
        }

        list.Sort((a, b) =>
        {
            int c = a.category.CompareTo(b.category);
            if (c != 0) return c;
            c = a.attribute.CompareTo(b.attribute);
            if (c != 0) return c;
            c = a.range.CompareTo(b.range);
            if (c != 0) return c;
            c = a.interval.CompareTo(b.interval);
            if (c != 0) return c;
            return string.Compare(a.magicName, b.magicName, System.StringComparison.Ordinal);
        });

        Undo.RecordObject(spawner, "Populate MagicSpawner List");
        spawner.magicDataList = list;
        EditorUtility.SetDirty(spawner);
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);

        Debug.Log($"[Grimoire] MagicSpawner.magicDataList に {list.Count} 件登録しました。シーンを保存してください（Ctrl+S）。");
    }
}
