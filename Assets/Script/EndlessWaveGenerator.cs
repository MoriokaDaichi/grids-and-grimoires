using System.Collections.Generic;
using UnityEngine;

// エンドレスダンジョンのウェーブ生成ルール（純粋関数）。深度が上がるほど敵が強く・多くなる。
// 企画書に数値の記載が無いため全て仮バランス。シーン非依存なので EditMode テストで検証できる。
//
// プールは「弱い順」に並んでいる前提。深度に応じて抽選できる範囲を [min, maxExclusive) の窓で絞る：
//   - 浅い深度では最弱の数体しか出ない（StartingChoices）
//   - 深度が進むごとに強い敵が窓に入る（EnemiesUnlockedPerDepth）
//   - さらに深いと最弱の敵が窓から外れていく（WeakCutoffLagDepths / DepthsPerPoolShift）
public static class EndlessWaveGenerator
{
    public const float HpGrowthPerDepth = 0.15f;   // 深度+1 ごとに最大HP +15%
    public const float AtkGrowthPerDepth = 0.10f;  // 深度+1 ごとに攻撃力 +10%
    public const float DefGrowthPerDepth = 0.07f;  // 深度+1 ごとに防御力 +7%
    public const int DepthsPerExtraEnemy = 5;       // 5 深度ごとに同時出現数 +1（同時被弾＝バーストの最大要因なので緩め）
    public const int DepthsPerPoolShift = 3;        // 弱い敵が窓から外れる間隔（深度）

    public const int StartingChoices = 2;            // 深度1 で抽選できる敵数（最弱2体）
    public const float EnemiesUnlockedPerDepth = 1f; // 深度+1 ごとに窓へ入る敵数
    public const int WeakCutoffLagDepths = 7;        // この深度を超えてから最弱の敵が外れ始める
    public const int MinTwoEnemyDepth = 2;           // この深度以降はウェーブ最低2体：debut敵の保証枠＋RNG枠を両立させる

    // 倍率の伸びを深部で寝かせる。浅い深度（膝まで）は素の線形、そこから先は勾配を落とす。
    // 深部は「窓に強い敵が入ってくる」ことで十分に難度が上がるため、掛け算の倍率は青天井にしない。
    // 再検証3周で「深度12〜13 のウェーブが満タンから即死させる」所見（C2）を受けて、膝を 12→10・
    // 勾配を 0.5→0.4 に下げ、中盤〜深部のステータス倍率の伸びをさらに寝かせた（数値は仮）。
    public const int ScalingTaperKneeDepth = 10;    // この深度までは素の線形
    public const float ScalingTaperSlope = 0.4f;    // 膝から先の勾配（0〜1）
    public const float DefMultCap = 2.5f;           // 防御倍率の上限（絶対値は窓の入れ替えで上がる）

    // Atk だけ膝から先をさらに寝かせる。再検証3周（レポート D6）で、プレイヤー Atk が1周で 10→17 しか
    // 伸びないのに敵 Atk は +10%/実効深度で伸び続け、深部ほど「殲滅が遅い→被弾総量が増える／満タンから
    // バーストで即死」が悪化していた。膝までは HP/Def と同じ素の線形なので浅〜中盤の手応えは不変。
    // 再検証4 R1: 0.25 は寝かせすぎ（D5 クランプ＋回復と乗算で深部の被弾圧力が消えた）。0.35 に戻す。
    public const float AtkScalingTaperSlope = 0.35f;

    // 深度 d（0始まり）を、膝から先で勾配を落とした「実効深度」に変換する。
    private static float TaperedDepth(int d) => TaperedDepth(d, ScalingTaperSlope);

    private static float TaperedDepth(int d, float slope)
    {
        if (d <= ScalingTaperKneeDepth) return d;
        return ScalingTaperKneeDepth + (d - ScalingTaperKneeDepth) * slope;
    }

    // 深度 depth（1始まり）の敵ステータス倍率。depth<=1 で等倍。
    public static WaveScaling ScalingFor(int depth)
    {
        int d0 = Mathf.Max(1, depth) - 1;
        float e = TaperedDepth(d0);
        float eAtk = TaperedDepth(d0, AtkScalingTaperSlope);
        return new WaveScaling(
            1f + HpGrowthPerDepth * e,
            1f + AtkGrowthPerDepth * eAtk,
            Mathf.Min(1f + DefGrowthPerDepth * e, DefMultCap));
    }

    // 深度 depth の同時出現数（1〜maxPerWave）。
    // 深度2以降は最低2体：debut敵をこの深度で必ず1体出す（PickIndices の保証枠）ぶん、
    // もう1枠を RNG に残さないと浅層のウェーブが「debut敵1種で固定」になり、最弱スライム等が
    // 抽選から完全に消える（＝そのドロップが枯れる／AoEの価値が消える）。
    public static int EnemyCountFor(int depth, int maxPerWave)
    {
        int d = Mathf.Max(1, depth);
        int count = 1 + (d - 1) / DepthsPerExtraEnemy;
        if (d >= MinTwoEnemyDepth) count = Mathf.Max(count, 2);
        int cap = Mathf.Max(1, maxPerWave);
        return Mathf.Clamp(count, 1, cap);
    }

    // 抽選できるプール範囲の上限（排他）。深度が上がるほど強い敵が窓に入ってくる。
    public static int MaxPoolIndexExclusiveFor(int depth, int poolCount)
    {
        if (poolCount <= 0) return 0;
        int unlocked = StartingChoices + Mathf.FloorToInt((Mathf.Max(1, depth) - 1) * EnemiesUnlockedPerDepth);
        return Mathf.Clamp(unlocked, 1, poolCount);
    }

    // 抽選できるプール範囲の下限。深いほど弱い敵をプール先頭から外していくが、上限より前で止める。
    public static int MinPoolIndexFor(int depth, int poolCount)
    {
        if (poolCount <= 1) return 0;
        int maxExclusive = MaxPoolIndexExclusiveFor(depth, poolCount);
        int floor = (Mathf.Max(1, depth) - WeakCutoffLagDepths) / DepthsPerPoolShift;
        return Mathf.Clamp(floor, 0, Mathf.Max(0, maxExclusive - 1));
    }

    // この深度で「初めて抽選窓に入った」敵のインデックス。窓が広がっていなければ -1。
    // ＝抽選窓の上限が前深度より増えたとき、その増分の先頭（最強）の敵。
    // 新しく開放された敵種のドロップが、その深度を通るたび最低1体は出るよう保証するために使う。
    public static int NewlyOpenedIndexFor(int depth, int poolCount)
    {
        if (poolCount <= 0) return -1;
        int now = MaxPoolIndexExclusiveFor(depth, poolCount);
        int prev = MaxPoolIndexExclusiveFor(Mathf.Max(1, depth) - 1, poolCount);
        if (now <= prev) return -1; // 窓が広がっていない（poolCount でクランプ済み等）
        return now - 1;
    }

    // このウェーブに出す敵の「プール内インデックス」を count 個選ぶ（同じ敵の重複あり）。
    // この深度で新規開放された敵がいれば、1枠をその敵に固定する（新規敵のドロップ導線の保証）。
    public static List<int> PickIndices(int depth, int poolCount, int count, System.Random rng)
    {
        List<int> result = new List<int>();
        if (poolCount <= 0) return result;

        int min = MinPoolIndexFor(depth, poolCount);
        int maxExclusive = MaxPoolIndexExclusiveFor(depth, poolCount);
        int span = Mathf.Max(1, maxExclusive - min);
        int n = Mathf.Max(0, count);
        for (int i = 0; i < n; i++)
        {
            int r = rng != null ? rng.Next(span) : (i % span);
            result.Add(min + r);
        }

        // debut敵の保証は「RNG枠を1つ以上残せるとき」だけ差し込む。1体ウェーブを保証で
        // 潰すと、その深度は debut敵1種で固定になり抽選の多様性が消えるため。
        int guaranteed = NewlyOpenedIndexFor(depth, poolCount);
        if (guaranteed >= min && guaranteed < maxExclusive && result.Count >= 2 && !result.Contains(guaranteed))
            result[result.Count - 1] = guaranteed;

        return result;
    }
}
