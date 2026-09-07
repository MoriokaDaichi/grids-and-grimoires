using NUnit.Framework;
using System.Collections.Generic;

// EndlessWaveGenerator（エンドレスダンジョンのウェーブ生成、純粋関数）の検証。
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
            Assert.Greater(s.defMult, prevDef);
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

    [Test]
    public void EnemyCountFor_GrowsEveryThreeDepths_AndCaps()
    {
        Assert.AreEqual(1, EndlessWaveGenerator.EnemyCountFor(1, 6));
        Assert.AreEqual(1, EndlessWaveGenerator.EnemyCountFor(3, 6));
        Assert.AreEqual(2, EndlessWaveGenerator.EnemyCountFor(4, 6));
        Assert.AreEqual(3, EndlessWaveGenerator.EnemyCountFor(7, 6));
        Assert.AreEqual(6, EndlessWaveGenerator.EnemyCountFor(100, 6)); // maxPerWave でキャップ
        Assert.AreEqual(1, EndlessWaveGenerator.EnemyCountFor(100, 1));
    }

    [Test]
    public void MinPoolIndexFor_AdvancesEveryFourDepths_AndClamps()
    {
        Assert.AreEqual(0, EndlessWaveGenerator.MinPoolIndexFor(1, 4));
        Assert.AreEqual(0, EndlessWaveGenerator.MinPoolIndexFor(4, 4));
        Assert.AreEqual(1, EndlessWaveGenerator.MinPoolIndexFor(5, 4));
        Assert.AreEqual(3, EndlessWaveGenerator.MinPoolIndexFor(100, 4)); // poolCount-1 でクランプ
        Assert.AreEqual(0, EndlessWaveGenerator.MinPoolIndexFor(100, 1));
    }

    [Test]
    public void PickIndices_ReturnsRequestedCount_WithinValidRange()
    {
        System.Random rng = new System.Random(12345);
        List<int> picks = EndlessWaveGenerator.PickIndices(6, 4, 3, rng);
        Assert.AreEqual(3, picks.Count);
        int min = EndlessWaveGenerator.MinPoolIndexFor(6, 4);
        foreach (int i in picks)
        {
            Assert.GreaterOrEqual(i, min);
            Assert.Less(i, 4);
        }
    }

    [Test]
    public void PickIndices_SeededRng_IsDeterministic()
    {
        List<int> a = EndlessWaveGenerator.PickIndices(10, 4, 4, new System.Random(999));
        List<int> b = EndlessWaveGenerator.PickIndices(10, 4, 4, new System.Random(999));
        CollectionAssert.AreEqual(a, b);
    }

    [Test]
    public void PickIndices_EmptyPool_ReturnsEmpty()
    {
        List<int> picks = EndlessWaveGenerator.PickIndices(5, 0, 3, new System.Random(1));
        Assert.AreEqual(0, picks.Count);
    }
}
