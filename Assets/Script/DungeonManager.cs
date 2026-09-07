using UnityEngine;
using System.Collections.Generic;

// ダンジョン1回分の進行を管理する。waves に並べた EnemyWave（敵グループ）を順番に戦わせ、
// 全ウェーブ突破でクリア、プレイヤーが倒れれば失敗。プレイヤーHPは道中持ち越し（波ごとの全回復はしない）。
//
// 出撃は GamePhaseManager.StartSortie() 経由でのみ開始する（シーンロード時に自動開始はしない）。
public class DungeonManager : MonoBehaviour
{
    [Header("ウェーブ構成（敵グループの並び）。Grimoire > Generate Dungoen で生成")]
    public List<EnemyWave> waves = new List<EnemyWave>();

    [Header("旧: 単体ウェーブ用（waves が空のとき 1体ずつのウェーブとして使う）")]
    public List<EnemyData> encounters = new List<EnemyData>();

    [Header("失敗時もドロップを与えるか（既定: 与えない＝死亡は没収）")]
    public bool grantLootOnFailure = false;

    // 進行状況の通知（戦闘UI・フェーズ管理向け）
    public System.Action<int, int, string> OnWaveChanged; // (現在ウェーブ 1始まり, 総数, 表示名)
    public System.Action OnDungeonCleared;
    public System.Action OnDungeonFailed;

    private PlayerStatus playerStatus;
    private EnemyRoster roster;
    private BattleManager battleManager;
    private int waveIndex;
    private bool dungeonActive;

    private readonly List<EnemyData> defeatedEnemies = new List<EnemyData>();
    public IReadOnlyList<EnemyData> DefeatedEnemies { get { return defeatedEnemies; } }

    void Awake()
    {
        playerStatus = Object.FindFirstObjectByType<PlayerStatus>();
        roster = Object.FindFirstObjectByType<EnemyRoster>();
        battleManager = Object.FindFirstObjectByType<BattleManager>();
    }

    void OnEnable()
    {
        if (playerStatus != null) playerStatus.OnDefeated += HandlePlayerDefeated;
        if (roster != null) roster.OnWaveDefeated += HandleWaveDefeated;
    }

    void OnDisable()
    {
        if (playerStatus != null) playerStatus.OnDefeated -= HandlePlayerDefeated;
        if (roster != null) roster.OnWaveDefeated -= HandleWaveDefeated;
    }

    // 実効ウェーブリスト（waves が無ければ encounters を1体ずつのウェーブに変換）
    private List<EnemyWave> EffectiveWaves()
    {
        if (waves != null && waves.Count > 0) return waves;

        List<EnemyWave> converted = new List<EnemyWave>();
        foreach (EnemyData e in encounters)
        {
            EnemyWave w = new EnemyWave();
            w.enemies.Add(e);
            converted.Add(w);
        }
        return converted;
    }

    public bool StartDungeon()
    {
        if (playerStatus == null || roster == null || battleManager == null || EffectiveWaves().Count == 0)
        {
            Debug.LogWarning("DungeonManager: PlayerStatus / EnemyRoster / BattleManager / waves の設定を確認してください。");
            return false;
        }

        playerStatus.BattleReset();
        waveIndex = 0;
        dungeonActive = true;
        defeatedEnemies.Clear();
        return SpawnNextWave();
    }

    private bool SpawnNextWave()
    {
        List<EnemyWave> ws = EffectiveWaves();

        if (waveIndex >= ws.Count)
        {
            dungeonActive = false;
            Debug.Log("ダンジョンクリア！ すべての敵を倒した。");
            OnDungeonCleared?.Invoke();
            return true;
        }

        EnemyWave wave = ws[waveIndex];
        waveIndex++;

        roster.SpawnWave(wave.enemies);
        battleManager.StartBattle();

        if (!battleManager.BattleActive)
        {
            dungeonActive = false;
            Debug.LogWarning("DungeonManager: 杖に発動可能な魔法が無いため出撃を中止しました。");
            return false;
        }

        string label = DescribeWave(wave);
        OnWaveChanged?.Invoke(waveIndex, ws.Count, label);
        Debug.Log($"{label} が現れた！（{waveIndex}/{ws.Count}）");
        return true;
    }

    private static string DescribeWave(EnemyWave wave)
    {
        if (wave == null || wave.enemies.Count == 0) return "敵";
        string first = wave.enemies[0] != null ? wave.enemies[0].enemyName : "敵";
        return wave.enemies.Count > 1 ? first + " ×" + wave.enemies.Count : first;
    }

    private void HandleWaveDefeated()
    {
        if (!dungeonActive) return;

        // いま表示中のウェーブは EffectiveWaves()[waveIndex - 1]（SpawnNextWaveでインクリメント済み）
        List<EnemyWave> ws = EffectiveWaves();
        int justCleared = waveIndex - 1;
        if (justCleared >= 0 && justCleared < ws.Count)
        {
            foreach (EnemyData e in ws[justCleared].enemies)
            {
                if (e != null) defeatedEnemies.Add(e);
            }
        }

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
