using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 開いているシーンの DungeonManager.waves に、複数敵を含む仮のウェーブ構成を設定する。
// メニュー: Grimoire > Generate Dungeon
public static class DungeonGenerator
{
    private const string EnemyDir = "Assets/EnemyData";

    [MenuItem("Grimoire/Generate Dungeon")]
    public static void Generate()
    {
        DungeonManager dm = Object.FindFirstObjectByType<DungeonManager>();
        if (dm == null)
        {
            Debug.LogError("[Grimoire] DungeonManager がシーンに見つかりません。");
            return;
        }

        EnemyData slime = Load("Slime");
        EnemyData goblin = Load("Goblin");
        EnemyData rat = Load("GiantRat");
        EnemyData guard = Load("ForestGuard");
        if (slime == null || goblin == null || rat == null || guard == null)
        {
            Debug.LogError("[Grimoire] Assets/EnemyData の敵アセットが揃っていません。先に Generate Enemy Data を実行してください。");
            return;
        }

        SerializedObject so = new SerializedObject(dm);
        SerializedProperty waves = so.FindProperty("waves");
        waves.arraySize = 0;

        AddWave(waves, slime);
        AddWave(waves, goblin, goblin);
        AddWave(waves, rat, slime);
        AddWave(waves, goblin, rat, slime);
        AddWave(waves, guard);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(dm.gameObject.scene);
        Debug.Log("[Grimoire] ダンジョン（5ウェーブ・敵グループ入り・仮バランス）を生成しました。シーンを保存してください。");
    }

    private static void AddWave(SerializedProperty waves, params EnemyData[] enemies)
    {
        int wi = waves.arraySize;
        waves.arraySize = wi + 1;
        SerializedProperty enemyList = waves.GetArrayElementAtIndex(wi).FindPropertyRelative("enemies");
        enemyList.arraySize = enemies.Length;
        for (int i = 0; i < enemies.Length; i++)
        {
            enemyList.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
        }
    }

    private static EnemyData Load(string fileId)
    {
        return AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyDir + "/" + fileId + ".asset");
    }
}
