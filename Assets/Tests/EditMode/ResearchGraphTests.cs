using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

// ResearchGraph（放射状スキルツリーのノード定義）の構造検証。
public class ResearchGraphTests
{
    [Test]
    public void EveryParentReferenceResolves()
    {
        HashSet<string> ids = new HashSet<string>();
        foreach (ResearchNodeDef d in ResearchGraph.Nodes) ids.Add(d.id);

        foreach (ResearchNodeDef d in ResearchGraph.Nodes)
        {
            if (d.parentId == null) continue;
            Assert.IsTrue(ids.Contains(d.parentId), d.id + " の親 " + d.parentId + " が存在しない");
        }
    }

    [Test]
    public void NoCycles_DepthTerminates()
    {
        foreach (ResearchNodeDef d in ResearchGraph.Nodes)
        {
            int depth = ResearchGraph.Depth(d.id);
            Assert.Less(depth, 20, d.id + " の親チェーンが異常に深い（循環の疑い）");
        }
    }

    [Test]
    public void NodeIdsAreUnique()
    {
        HashSet<string> seen = new HashSet<string>();
        foreach (ResearchNodeDef d in ResearchGraph.Nodes)
            Assert.IsTrue(seen.Add(d.id), "ノードID重複: " + d.id);
    }

    [Test]
    public void TenBaseMagicsAreRoots()
    {
        string[] roots = { "Fire", "Thunder", "Wind", "Light", "Dark", "Flame", "Bolt", "Storm", "Shine", "Darkness" };
        foreach (string id in roots)
        {
            ResearchNodeDef d = ResearchGraph.Get(id);
            Assert.IsNotNull(d, id + " が存在しない");
            Assert.IsTrue(d.isMagic);
            Assert.IsNull(d.parentId, id + " は根であるべき");
        }
    }

    [Test]
    public void AllSixtySevenMagicNodesPresent()
    {
        int magicCount = 0;
        foreach (ResearchNodeDef d in ResearchGraph.Nodes)
            if (d.isMagic) magicCount++;
        Assert.AreEqual(67, magicCount);

        // 代表的なものが含まれる
        foreach (string id in new[] { "GigaFire", "GigaDarkness", "StatusBurn", "BuffLucPassiveLv3", "AttrBuffWind", "StatusRateBuffDark", "AddSpell", "DualSpell" })
            Assert.IsNotNull(ResearchGraph.Get(id), id + " が無い");
    }

    [Test]
    public void EveryStatKindAppearsAsSmallNode()
    {
        HashSet<ResearchStat> found = new HashSet<ResearchStat>();
        foreach (ResearchNodeDef d in ResearchGraph.Nodes)
            if (!d.isMagic) found.Add(d.stat);

        foreach (ResearchStat s in System.Enum.GetValues(typeof(ResearchStat)))
            Assert.IsTrue(found.Contains(s), s + " の小ノードが存在しない");
    }

    [Test]
    public void SmallNodesHaveCostAndLabel()
    {
        foreach (ResearchNodeDef d in ResearchGraph.Nodes)
        {
            if (d.isMagic) continue;
            Assert.Greater(d.cost.Count, 0, d.id + " にコストが無い");
            Assert.IsFalse(string.IsNullOrEmpty(d.shortLabel), d.id + " にラベルが無い");
            Assert.Greater(d.statAmount, 0f, d.id + " の増加量が0");
        }
    }

    [Test]
    public void MagicNodesGatedBehindSmallNodes()
    {
        // メガ系は小ノードを経由してからでないと到達できない
        Assert.AreEqual("node_Fire_a", ResearchGraph.Get("MegaFire").parentId);
        Assert.AreEqual("node_Fire_b", ResearchGraph.Get("MegaFlame").parentId);
        Assert.Greater(ResearchGraph.Depth("MegaFire"), 1);
    }

    [Test]
    public void PrerequisitesCountEqualsNonRootNodeCount()
    {
        int nonRoot = 0;
        foreach (ResearchNodeDef d in ResearchGraph.Nodes)
            if (d.parentId != null) nonRoot++;
        Assert.AreEqual(nonRoot, ResearchGraph.Prerequisites.Count);
    }

    [Test]
    public void Position_RadiusGrowsWithRing()
    {
        ResearchNodeDef ring1 = ResearchGraph.Get("Fire");
        ResearchNodeDef ring3 = ResearchGraph.Get("MegaFire");
        Assert.Greater(ResearchGraph.Position(ring3).magnitude, ResearchGraph.Position(ring1).magnitude);
    }
}
