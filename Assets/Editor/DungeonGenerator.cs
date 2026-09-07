using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 開いているシーンの DungeonManager.enemyPool に、エンドレスダンジョン用の敵プールを設定する。
// 弱い順に並べる（EndlessWaveGenerator が深度に応じて先頭から除外していく）。
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

        // 弱い → 強い の順
        string[] order = { "Slime", "GiantRat", "Goblin", "ForestGuard" };
        EnemyData[] enemies = new EnemyData[order.Length];
        for (int i = 0; i < order.Length; i++)
        {
            enemies[i] = Load(order[i]);
            if (enemies[i] == null)
            {
                Debug.LogError($"[Grimoire] {order[i]}.asset が見つかりません。先に Generate Enemy Data を実行してください。");
                return;
            }
        }

        SerializedObject so = new SerializedObject(dm);

        // 旧・固定ウェーブはクリア（エンドレス経路を使う）
        SerializedProperty waves = so.FindProperty("waves");
        if (waves != null) waves.arraySize = 0;

        SerializedProperty pool = so.FindProperty("enemyPool");
        pool.arraySize = enemies.Length;
        for (int i = 0; i < enemies.Length; i++)
        {
            pool.GetArrayElementAtIndex(i).objectReferenceValue = enemies[i];
        }

        SerializedProperty seed = so.FindProperty("seed");
        if (seed != null) seed.intValue = 0; // プレイごとにランダム

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(dm.gameObject.scene);
        Debug.Log("[Grimoire] エンドレスダンジョンの敵プール（スライム/大ネズミ/ゴブリン/森の番人・仮バランス）を設定しました。シーンを保存してください。");
    }

    private static EnemyData Load(string fileId)
    {
        return AssetDatabase.LoadAssetAtPath<EnemyData>(EnemyDir + "/" + fileId + ".asset");
    }
}
