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

    // 無料入場したぶんの未精算入場料（>0 なら帰還時に戦利品から差し引く）。
    private int entryFeeOwed;
    public int EntryFeeOwed { get { return entryFeeOwed; } }

    // 未精算の入場料を確定し、額を返してクリアする（RewardScreen が帰還時に1度だけ呼ぶ）。
    public int SettleEntryFee()
    {
        int owed = entryFeeOwed;
        entryFeeOwed = 0;
        return owed;
    }

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

    // 入場料を払える状態か。所持金が基本料金未満なら無料扱いになるので、事実上つねに true
    // （MoneyManager がシーンに無い場合も true）。
    public bool CanAffordEntry()
    {
        MoneyManager money = MoneyManager.Instance;
        if (money == null) return true;
        return money.CanAfford(DungeonEconomy.EffectiveEntryFee(money.Balance));
    }

    public bool StartDungeon()
    {
        if (playerStatus == null || roster == null || battleManager == null || EffectivePool().Count == 0)
        {
            Debug.LogWarning("DungeonManager: PlayerStatus / EnemyRoster / BattleManager / enemyPool の設定を確認してください。");
            return false;
        }

        // ダンジョン入場料（少額固定・仮）。MoneyManager が無ければ無料。
        // 所持金が基本料金に満たないときは無料（お金が枯れて潜れなくなる詰みのセーフティ）。
        // 出撃が確定するまで（AdvanceWave 成功まで）は徴収しない＝空杖などで中断しても取られない。
        MoneyManager money = MoneyManager.Instance;
        int fee = money != null ? DungeonEconomy.EffectiveEntryFee(money.Balance) : 0;
        if (money != null && !money.CanAfford(fee))
        {
            Debug.LogWarning($"DungeonManager: 入場料 {fee}G が足りません。");
            return false;
        }

        // 所持金が乏しくて無料入場したぶんは、帰還時に戦利品から差し引く（RewardScreen が精算）。
        entryFeeOwed = 0;
        if (money != null && fee == 0 && money.Balance < DungeonEconomy.BaseEntryFee)
        {
            entryFeeOwed = DungeonEconomy.BaseEntryFee;
            Debug.Log($"DungeonManager: 所持金が乏しいため入場料を後払いにした（{entryFeeOwed}G を帰還時に戦利品から精算）。");
        }

        playerStatus.BattleReset();
        depth = 0;
        dungeonActive = true;
        awaitingChoice = false;
        defeatedEnemies.Clear();
        rng = seed != 0 ? new System.Random(seed) : new System.Random();

        if (!AdvanceWave())
        {
            dungeonActive = false;
            return false;
        }

        // 潜行が始まった。ここで入場料を徴収する。
        if (money != null) money.TrySpend(fee);
        return true;
    }

    private bool AdvanceWave()
    {
        List<EnemyData> pool = EffectivePool();
        depth++;

        // バースト即死クランプの累計をウェーブ開始でリセット（レポート C2/D5）。
        if (playerStatus != null) playerStatus.BeginWave();

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

        // サステイン系アクセサリによるウェーブ間回復（無装備なら何も起きない）。
        if (playerStatus != null) playerStatus.HealWaveTick();

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
