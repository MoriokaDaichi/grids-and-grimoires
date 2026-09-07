using UnityEngine;
using System.Collections.Generic;

// ダンジョン1回分の進行を管理する。encountersに並べたEnemyDataを順番に戦わせ、
// 全滅させればクリア、プレイヤーが倒れれば失敗とする。プレイヤーHPは道中持ち越し（波ごとの全回復はしない）。
//
// 出撃は GamePhaseManager.StartSortie() 経由でのみ開始する（シーンロード時に自動開始はしない）。
public class DungeonManager : MonoBehaviour
{
    public List<EnemyData> encounters = new List<EnemyData>();

    [Header("失敗時もドロップを与えるか（既定: 与えない＝死亡は没収）")]
    public bool grantLootOnFailure = false;

    // 進行状況の通知（戦闘UI・フェーズ管理向け）
    public System.Action<int, int, string> OnWaveChanged; // (現在ウェーブ 1始まり, 総数, 敵名)
    public System.Action OnDungeonCleared;
    public System.Action OnDungeonFailed;

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

    // 出撃開始。開始できた（少なくとも1ウェーブ戦闘に入った）場合のみ true。
    public bool StartDungeon()
    {
        if (playerStatus == null || enemyStatus == null || battleManager == null || encounters.Count == 0)
        {
            Debug.LogWarning("DungeonManager: PlayerStatus / EnemyStatus / BattleManager / encounters の設定を確認してください。");
            return false;
        }

        playerStatus.BattleReset();
        waveIndex = 0;
        dungeonActive = true;
        return SpawnNextWave();
    }

    private bool SpawnNextWave()
    {
        if (waveIndex >= encounters.Count)
        {
            dungeonActive = false;
            Debug.Log("ダンジョンクリア！ すべての敵を倒した。");
            OnDungeonCleared?.Invoke();
            return true;
        }

        EnemyData next = encounters[waveIndex];
        waveIndex++;

        enemyStatus.Setup(next);
        battleManager.StartBattle();

        if (!battleManager.BattleActive)
        {
            // 杖に発動可能な魔法が無い＝出撃を成立させられない。呼び出し側(GamePhaseManager)が構築画面に戻す。
            dungeonActive = false;
            Debug.LogWarning("DungeonManager: 杖に発動可能な魔法が無いため出撃を中止しました。");
            return false;
        }

        OnWaveChanged?.Invoke(waveIndex, encounters.Count, next.enemyName);
        Debug.Log($"{next.enemyName} が現れた！（{waveIndex}/{encounters.Count}）");
        return true;
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
        OnDungeonFailed?.Invoke();
    }
}
