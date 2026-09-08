using UnityEngine;
using System.Collections.Generic;

// エンドレスダンジョンの進行を管理する。深度1から始まり、ウェーブを1つ突破するたびに
// 「脱出する / さらに深層へ進む」を選べる（脱出しない限り無限に続く）。深層ほど敵が強く・多くなる。
// プレイヤーHP・マナは道中持ち越し（ウェーブごとの全回復はしない）。
//
// 出撃は GamePhaseManager.StartSortie() 経由でのみ開始する。
public class DungeonManager : MonoBehaviour
{
    [Header("出現する敵プール（Grimoire > Generate Dungeon で生成）。弱い順に並べる。")]
    public List<EnemyData> enemyPool = new List<EnemyData>();

    [Header("旧: 固定ウェーブ / 単体エンカウント（enemyPool が空のときプールとして流用）")]
    public List<EnemyWave> waves = new List<EnemyWave>();
    public List<EnemyData> encounters = new List<EnemyData>();

    [Header("失敗時もドロップを与えるか（既定: 与えない＝死亡は没収）")]
    public bool grantLootOnFailure = false;

    [Header("ウェーブ生成の乱数シード（0でプレイごとにランダム）")]
    public int seed = 0;

    // 進行状況の通知（戦闘UI・フェーズ管理向け）
    public System.Action<int, int, string> OnWaveChanged; // (深度, -1=エンドレス, 表示名)
    public System.Action<int> OnWaveCleared;              // (突破した深度) 脱出/続行の選択待ちに入った
    public System.Action<EnemyData> OnEnemyDefeated;      // 敵1体を撃破した（トレーダーのタスク進捗用）
    public System.Action OnDungeonCleared;                // 脱出で決着（報酬あり）
    public System.Action OnDungeonFailed;                 // プレイヤー敗北で決着（報酬なし）

    private PlayerStatus playerStatus;
    private EnemyRoster roster;
    private BattleManager battleManager;
    private int depth;
    private bool dungeonActive;
    private bool awaitingChoice;
    private System.Random rng;

    private readonly List<EnemyData> currentWaveEnemies = new List<EnemyData>();
    private readonly List<EnemyData> defeatedEnemies = new List<EnemyData>();
    public IReadOnlyList<EnemyData> DefeatedEnemies { get { return defeatedEnemies; } }

    // 今回の潜行で到達した深度
    public int Depth { get { return depth; } }
    // ウェーブ突破後の選択待ちか
    public bool AwaitingChoice { get { return awaitingChoice; } }
    public bool DungeonActive { get { return dungeonActive; } }

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

    // 実効プール（enemyPool が空なら旧 waves / encounters の敵を重複なく寄せ集める）
    private List<EnemyData> EffectivePool()
    {
        if (enemyPool != null && enemyPool.Count > 0) return enemyPool;

        List<EnemyData> pool = new List<EnemyData>();
        if (waves != null)
        {
            foreach (EnemyWave w in waves)
            {
                if (w == null) continue;
                foreach (EnemyData e in w.enemies)
                    if (e != null && !pool.Contains(e)) pool.Add(e);
            }
        }
        if (encounters != null)
        {
            foreach (EnemyData e in encounters)
                if (e != null && !pool.Contains(e)) pool.Add(e);
        }
        return pool;
    }

    public bool StartDungeon()
    {
        if (playerStatus == null || roster == null || battleManager == null || EffectivePool().Count == 0)
        {
            Debug.LogWarning("DungeonManager: PlayerStatus / EnemyRoster / BattleManager / enemyPool の設定を確認してください。");
            return false;
        }

        playerStatus.BattleReset();
        depth = 0;
        dungeonActive = true;
        awaitingChoice = false;
        defeatedEnemies.Clear();
        rng = seed != 0 ? new System.Random(seed) : new System.Random();

        return AdvanceWave();
    }

    private bool AdvanceWave()
    {
        List<EnemyData> pool = EffectivePool();
        depth++;

        WaveScaling scaling = EndlessWaveGenerator.ScalingFor(depth);
        int count = EndlessWaveGenerator.EnemyCountFor(depth, roster.Capacity);
        List<int> indices = EndlessWaveGenerator.PickIndices(depth, pool.Count, count, rng);

        currentWaveEnemies.Clear();
        foreach (int i in indices)
        {
            if (i >= 0 && i < pool.Count && pool[i] != null) currentWaveEnemies.Add(pool[i]);
        }
        if (currentWaveEnemies.Count == 0 && pool.Count > 0) currentWaveEnemies.Add(pool[0]);

        roster.SpawnWave(currentWaveEnemies, scaling);
        battleManager.StartBattle();

        if (!battleManager.BattleActive)
        {
            dungeonActive = false;
            Debug.LogWarning("DungeonManager: 杖に発動可能な魔法が無いため出撃を中止しました。");
            return false;
        }

        string label = DescribeWave(currentWaveEnemies);
        OnWaveChanged?.Invoke(depth, -1, label);
        Debug.Log($"深度 {depth}: {label} が現れた！（HP×{scaling.hpMult:F2} / Atk×{scaling.atkMult:F2}）");
        return true;
    }

    private static string DescribeWave(List<EnemyData> enemies)
    {
        if (enemies == null || enemies.Count == 0) return "敵";
        string first = enemies[0] != null ? enemies[0].enemyName : "敵";
        return enemies.Count > 1 ? first + " ×" + enemies.Count : first;
    }

    // ウェーブ全滅 → 選択待ちに入る（自動で次ウェーブへは進まない）
    private void HandleWaveDefeated()
    {
        if (!dungeonActive || awaitingChoice) return;

        foreach (EnemyData e in currentWaveEnemies)
        {
            if (e == null) continue;
            defeatedEnemies.Add(e);
            OnEnemyDefeated?.Invoke(e);
        }

        awaitingChoice = true;
        battleManager.EndBattle(); // 選択が済むまで戦闘ループを止める
        Debug.Log($"深度 {depth} を突破。脱出するか、さらに深層へ進むか選択。");
        OnWaveCleared?.Invoke(depth);
    }

    // 「深層へ進む」
    public void ContinueDeeper()
    {
        if (!dungeonActive || !awaitingChoice) return;
        awaitingChoice = false;
        AdvanceWave();
    }

    // 「脱出する」（ここまでの戦利品を確定させて帰還）
    public void Escape()
    {
        if (!dungeonActive || !awaitingChoice) return;
        dungeonActive = false;
        awaitingChoice = false;
        Debug.Log($"深度 {depth} から脱出した。");
        OnDungeonCleared?.Invoke();
    }

    private void HandlePlayerDefeated()
    {
        if (!dungeonActive) return;
        dungeonActive = false;
        awaitingChoice = false;
        Debug.Log($"深度 {depth} で力尽きた… 戦利品は失われた。");
        OnDungeonFailed?.Invoke();
    }
}
