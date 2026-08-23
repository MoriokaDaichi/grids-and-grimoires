using UnityEngine;
using System.Collections.Generic;

// ダンジョン1回分の進行を管理する。encountersに並べたEnemyDataを順番に戦わせ、
// 全滅させればクリア、プレイヤーが倒れれば失敗とする。プレイヤーHPは道中持ち越し（波ごとの全回復はしない）。
public class DungeonManager : MonoBehaviour
{
    public List<EnemyData> encounters = new List<EnemyData>();

    private PlayerStatus playerStatus;
    private EnemyStatus enemyStatus;
    private BattleManager battleManager;
    private int waveIndex;
    private bool dungeonActive;

    void Awake()
    {
        playerStatus = Object.FindFirstObjectByType<PlayerStatus>();
        enemyStatus = Object.FindFirstObjectByType<EnemyStatus>();
        battleManager = Object.FindFirstObjectByType<BattleManager>();
    }

    void Start()
    {
        StartDungeon();
    }

    void OnEnable()
    {
        if (playerStatus != null) playerStatus.OnDefeated += HandlePlayerDefeated;
        if (enemyStatus != null) enemyStatus.OnDefeated += HandleEnemyDefeated;
    }

    void OnDisable()
    {
        if (playerStatus != null) playerStatus.OnDefeated -= HandlePlayerDefeated;
        if (enemyStatus != null) enemyStatus.OnDefeated -= HandleEnemyDefeated;
    }

    public void StartDungeon()
    {
        if (playerStatus == null || enemyStatus == null || battleManager == null || encounters.Count == 0)
        {
            Debug.LogWarning("DungeonManager: PlayerStatus / EnemyStatus / BattleManager / encounters の設定を確認してください。");
            return;
        }

        playerStatus.BattleReset();
        waveIndex = 0;
        dungeonActive = true;
        SpawnNextWave();
    }

    private void SpawnNextWave()
    {
        if (waveIndex >= encounters.Count)
        {
            dungeonActive = false;
            Debug.Log("ダンジョンクリア！ すべての敵を倒した。");
            return;
        }

        EnemyData next = encounters[waveIndex];
        waveIndex++;

        enemyStatus.Setup(next);
        battleManager.StartBattle();
        Debug.Log($"{next.enemyName} が現れた！（{waveIndex}/{encounters.Count}）");
    }

    private void HandleEnemyDefeated()
    {
        if (!dungeonActive) return;
        SpawnNextWave();
    }

    private void HandlePlayerDefeated()
    {
        if (!dungeonActive) return;
        dungeonActive = false;
        Debug.Log("ダンジョン失敗… 拠点まで撤退した。");
    }
}
