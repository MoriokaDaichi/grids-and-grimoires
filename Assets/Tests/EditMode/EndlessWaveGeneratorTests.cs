using NUnit.Framework;
using System.Collections.Generic;

// EndlessWaveGenerator（エンドレスダンジョンのウェーブ生成、純粋関数）の検証。
// 数値は仮バランスなので、式の意図（浅い深度ほど最弱側だけが抽選される窓）を固定する。
public class EndlessWaveGeneratorTests
{
    [Test]
    public void ScalingFor_Depth1_IsIdentity()
    {
        WaveScaling s = EndlessWaveGenerator.ScalingFor(1);
        Assert.AreEqual(1f, s.hpMult, 0.0001f);
        Assert.AreEqual(1f, s.atkMult, 0.0001f);
        Assert.AreEqual(1f, s.defMult, 0.0001f);
    }

    [Test]
    public void ScalingFor_DepthBelow1_ClampsToIdentity()
    {
        WaveScaling s = EndlessWaveGenerator.ScalingFor(0);
        Assert.AreEqual(1f, s.hpMult, 0.0001f);
    }

    [Test]
    public void ScalingFor_IncreasesMonotonicallyWithDepth()
    {
        float prevHp = 0f, prevAtk = 0f, prevDef = 0f;
        for (int d = 1; d <= 20; d++)
        {
            WaveScaling s = EndlessWaveGenerator.ScalingFor(d);
            Assert.Greater(s.hpMult, prevHp);
            Assert.Greater(s.atkMult, prevAtk);
            Assert.GreaterOrEqual(s.defMult, prevDef); // 深部で DefMultCap に張り付く
            prevHp = s.hpMult; prevAtk = s.atkMult; prevDef = s.defMult;
        }
    }

    [Test]
    public void ScalingFor_Depth2_MatchesGrowthConstants()
    {
        WaveScaling s = EndlessWaveGenerator.ScalingFor(2);
        Assert.AreEqual(1f + EndlessWaveGenerator.HpGrowthPerDepth, s.hpMult, 0.0001f);
        Assert.AreEqual(1f + EndlessWaveGenerator.AtkGrowthPerDepth, s.atkMult, 0.0001f);
    }

    // 膝（ScalingTaperKneeDepth）までは素の線形＝従来式と一致する。浅〜中盤の手応えは変えない。
    [Test]
    public void ScalingFor_UpToTaperKnee_IsPlainLinear()
    {
        for (int depth = 1; depth <= EndlessWaveGenerator.ScalingTaperKneeDepth + 1; depth++)
        {
            int d = depth - 1;
            WaveScaling s = EndlessWaveGenerator.ScalingFor(depth);
            Assert.AreEqual(1f + EndlessWaveGenerator.HpGrowthPerDepth * d, s.hpMult, 0.0001f, "depth " + depth);
            Assert.AreEqual(1f + EndlessWaveGenerator.AtkGrowthPerDepth * d, s.atkMult, 0.0001f, "depth " + depth);
        }
    }

    // 膝から先は勾配が寝る＝各深度の伸び幅が膝前より小さくなる。
    [Test]
    public void ScalingFor_PastTaperKnee_GrowthSlowsButStillRises()
    {
        int knee = EndlessWaveGenerator.ScalingTaperKneeDepth;
        float preKneeStep = EndlessWaveGenerator.ScalingFor(knee + 1).hpMult - EndlessWaveGenerator.ScalingFor(knee).hpMult;

        for (int depth = knee + 2; depth <= 60; depth++)
        {
            float step = EndlessWaveGenerator.ScalingFor(depth).hpMult - EndlessWaveGenerator.ScalingFor(depth - 1).hpMult;
            Assert.Greater(step, 0f, "depth " + depth + " はまだ増加する");
            Assert.Less(step, preKneeStep + 0.0001f, "depth " + depth + " の伸び幅は膝前より小さい");
        }
    }

    [Test]
    public void ScalingFor_DefMult_NeverExceedsCap()
    {
        for (int depth = 1; depth <= 200; depth++)
            Assert.LessOrEqual(EndlessWaveGenerator.ScalingFor(depth).defMult, EndlessWaveGenerator.DefMultCap + 0.0001f);
    }

    [Test]
    public void EnemyCountFor_GrowsEveryFiveDepths_AndCaps()
    {
        Assert.AreEqual(1, EndlessWaveGenerator.EnemyCountFor(1, 6));
        Assert.AreEqual(1, EndlessWaveGenerator.EnemyCountFor(5, 6));
        Assert.AreEqual(2, EndlessWaveGenerator.EnemyCountFor(6, 6));
        Assert.AreEqual(3, EndlessWaveGenerator.EnemyCountFor(11, 6));
        Assert.AreEqual(6, EndlessWaveGenerator.EnemyCountFor(100, 6)); // maxPerWave でキャップ
        Assert.AreEqual(1, EndlessWaveGenerator.EnemyCountFor(100, 1));
    }

    [Test]
    public void MaxPoolIndexExclusiveFor_OpensUpWithDepth()
    {
        Assert.AreEqual(2, EndlessWaveGenerator.MaxPoolIndexExclusiveFor(1, 40));  // 深度1は最弱2体だけ
        Assert.AreEqual(3, EndlessWaveGenerator.MaxPoolIndexExclusiveFor(2, 40));
        Assert.AreEqual(11, EndlessWaveGenerator.MaxPoolIndexExclusiveFor(10, 40));
        Assert.AreEqual(40, EndlessWaveGenerator.MaxPoolIndexExclusiveFor(100, 40)); // poolCount でクランプ
        Assert.AreEqual(3, EndlessWaveGenerator.MaxPoolIndexExclusiveFor(50, 3));    // 小さいプールもクランプ
        Assert.AreEqual(0, EndlessWaveGenerator.MaxPoolIndexExclusiveFor(1, 0));
    }

    [Test]
    public void MinPoolIndexFor_LagsBehindThenAdvances_AndStaysBelowMax()
    {
        Assert.AreEqual(0, EndlessWaveGenerator.MinPoolIndexFor(1, 40));
        Assert.AreEqual(0, EndlessWaveGenerator.MinPoolIndexFor(7, 40));   // WeakCutoffLagDepths まで据え置き
        Assert.AreEqual(2, EndlessWaveGenerator.MinPoolIndexFor(13, 40));  // (13-7)/3
        Assert.AreEqual(11, EndlessWaveGenerator.MinPoolIndexFor(40, 40)); // (40-7)/3
        Assert.AreEqual(0, EndlessWaveGenerator.MinPoolIndexFor(100, 1));  // 単体プールは常に0

        // 下限は必ず上限より前
        for (int d = 1; d <= 60; d++)
        {
            int min = EndlessWaveGenerator.MinPoolIndexFor(d, 40);
            int maxExclusive = EndlessWaveGenerator.MaxPoolIndexExclusiveFor(d, 40);
            Assert.Less(min, maxExclusive, $"depth {d}: min {min} < maxExclusive {maxExclusive}");
        }
    }

    [Test]
    public void PickIndices_ReturnsRequestedCount_WithinWindow()
    {
        System.Random rng = new System.Random(12345);
        List<int> picks = EndlessWaveGenerator.PickIndices(6, 40, 3, rng);
        Assert.AreEqual(3, picks.Count);
        int min = EndlessWaveGenerator.MinPoolIndexFor(6, 40);
        int maxExclusive = EndlessWaveGenerator.MaxPoolIndexExclusiveFor(6, 40);
        foreach (int i in picks)
        {
            Assert.GreaterOrEqual(i, min);
            Assert.Less(i, maxExclusive);
        }
    }

    [Test]
    public void PickIndices_ShallowDepth_OnlyPicksWeakest()
    {
        System.Random rng = new System.Random(7);
        List<int> picks = EndlessWaveGenerator.PickIndices(1, 40, 20, rng);
        Assert.AreEqual(20, picks.Count);
        foreach (int i in picks) Assert.Less(i, 2); // 深度1は最弱2体のみ
    }

    [Test]
    public void PickIndices_SeededRng_IsDeterministic()
    {
        List<int> a = EndlessWaveGenerator.PickIndices(10, 40, 4, new System.Random(999));
        List<int> b = EndlessWaveGenerator.PickIndices(10, 40, 4, new System.Random(999));
        CollectionAssert.AreEqual(a, b);
    }

    [Test]
    public void PickIndices_EmptyPool_ReturnsEmpty()
    {
        List<int> picks = EndlessWaveGenerator.PickIndices(5, 0, 3, new System.Random(1));
        Assert.AreEqual(0, picks.Count);
    }
}
