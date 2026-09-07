using System.Collections.Generic;
using UnityEngine;

// エンドレスダンジョンのウェーブ生成ルール（純粋関数）。深度が上がるほど敵が強く・多くなる。
// 企画書に数値の記載が無いため全て仮バランス。シーン非依存なので EditMode テストで検証できる。
public static class EndlessWaveGenerator
{
    public const float HpGrowthPerDepth = 0.18f;   // 深度+1 ごとに最大HP +18%
    public const float AtkGrowthPerDepth = 0.12f;  // 深度+1 ごとに攻撃力 +12%
    public const float DefGrowthPerDepth = 0.08f;  // 深度+1 ごとに防御力 +8%
    public const int DepthsPerExtraEnemy = 3;      // 3 深度ごとに同時出現数 +1
    public const int DepthsPerPoolShift = 4;       // 4 深度ごとに弱い敵をプールから外す

    // 深度 depth（1始まり）の敵ステータス倍率。depth<=1 で等倍。
    public static WaveScaling ScalingFor(int depth)
    {
        int d = Mathf.Max(1, depth) - 1;
        return new WaveScaling(
            1f + HpGrowthPerDepth * d,
            1f + AtkGrowthPerDepth * d,
            1f + DefGrowthPerDepth * d);
    }

    // 深度 depth の同時出現数（1〜maxPerWave）。
    public static int EnemyCountFor(int depth, int maxPerWave)
    {
        int d = Mathf.Max(1, depth);
        int count = 1 + (d - 1) / DepthsPerExtraEnemy;
        int cap = Mathf.Max(1, maxPerWave);
        return Mathf.Clamp(count, 1, cap);
    }

    // 深度が上がるほどプールの先頭（弱い敵）を除外していく。返り値は使用可能な最小インデックス。
    public static int MinPoolIndexFor(int depth, int poolCount)
    {
        if (poolCount <= 1) return 0;
        return Mathf.Clamp((Mathf.Max(1, depth) - 1) / DepthsPerPoolShift, 0, poolCount - 1);
    }

    // このウェーブに出す敵の「プール内インデックス」を count 個選ぶ（同じ敵の重複あり）。
    public static List<int> PickIndices(int depth, int poolCount, int count, System.Random rng)
    {
        List<int> result = new List<int>();
        if (poolCount <= 0) return result;

        int min = MinPoolIndexFor(depth, poolCount);
        int span = poolCount - min;
        int n = Mathf.Max(0, count);
        for (int i = 0; i < n; i++)
        {
            int r = rng != null ? rng.Next(span) : (i % span);
            result.Add(min + r);
        }
        return result;
    }
}
