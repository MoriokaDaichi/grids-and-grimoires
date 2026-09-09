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
            if (d.isMagic || d.IsPerk) continue;
            Assert.Greater(d.cost.Count, 0, d.id + " にコストが無い");
            Assert.IsFalse(string.IsNullOrEmpty(d.shortLabel), d.id + " にラベルが無い");
            Assert.Greater(d.statAmount, 0f, d.id + " の増加量が0");
        }
    }

    // 検証レポート 2026-09-10 O1：研究の深部フロンティアが Atk 一色でグラスキャノン化するため、
    // 属性ラインごとに防御特性（perk_*_r5..r7）＋攻撃特性（atr_*_r5..r7）の大ノード列を挿した。
    [Test]
    public void PerkNodesAreWellFormed()
    {
        HashSet<ResearchPerk> kinds = new HashSet<ResearchPerk>();
        int count = 0;
        foreach (ResearchNodeDef d in ResearchGraph.Nodes)
        {
            if (!d.IsPerk) continue;
            count++;
            Assert.IsFalse(d.isMagic, d.id + " は魔法サブセットに入ってはいけない");
            Assert.Greater(d.cost.Count, 0, d.id + " にコストが無い");
            Assert.Greater(d.perkAmount, 0f, d.id + " の効果量が0");
            Assert.IsFalse(string.IsNullOrEmpty(d.shortLabel), d.id + " にラベルが無い");
            Assert.IsFalse(string.IsNullOrEmpty(d.detail), d.id + " に説明が無い");
            Assert.IsNotNull(ResearchGraph.Get(d.parentId), d.id + " の親が存在しない");
            kinds.Add(d.perk);
        }
        Assert.AreEqual(30, count, "特性ノードは 5ライン×(防御3tier + 攻撃3tier) ＝ 30");
        // 防御5種＋攻撃5種の全10種が出る
        Assert.AreEqual(10, kinds.Count, "特性の種類が10種そろっていない");
    }

    // ユーザー要求「Def や Luc も Atk と同じくらい伸びるように小ノードを増やす」。
    // 中央帯の放射カラム（node_col_*）は Atk / Def / Luc / Hp を各20ノード、いずれも小結晶だけで到達可。
    [Test]
    public void StatColumns_GiveAtkDefLucHpEqualReach()
    {
        int Reachable(ResearchStat want)
        {
            int n = 0;
            foreach (ResearchNodeDef d in ResearchGraph.Nodes)
            {
                if (d.isMagic || d.IsPerk || d.stat != want || !d.id.StartsWith("node_col_")) continue;
                bool ok = true;
                string cur = d.id;
                int guard = 0;
                while (cur != null && guard++ < 32)
                {
                    ResearchNodeDef nd = ResearchGraph.Get(cur);
                    if (nd == null) { ok = false; break; }
                    if (!nd.isMagic)
                        foreach (MaterialCost c in nd.cost)
                            if (c != null && c.materialType != MaterialType.SmallManaCrystal) { ok = false; break; }
                    if (!ok) break;
                    cur = nd.parentId;
                }
                if (ok) n++;
            }
            return n;
        }
        Assert.AreEqual(20, Reachable(ResearchStat.Atk), "Atk カラムが 20 ノードない");
        Assert.AreEqual(20, Reachable(ResearchStat.Def), "Def カラムが 20 ノードない");
        Assert.AreEqual(20, Reachable(ResearchStat.Luc), "Luc カラムが 20 ノードない");
        Assert.AreEqual(20, Reachable(ResearchStat.Hp), "Hp カラムが 20 ノードない");
    }

    // 再検証3〜5 D6：min-max プレイでも Atk が伸びるよう、小結晶だけで（＝中/大結晶ゲート無しで）
    // たどれる Atk 小ノードが十分な本数あること。親チェーンを根までさかのぼり、通り道の小ノードが
    // 全て小結晶コストのみなら「小結晶だけで到達可能」とみなす。
    [Test]
    public void EnoughAtkStatNodes_ReachableWithSmallCrystalsOnly()
    {
        int reachable = 0;
        foreach (ResearchNodeDef d in ResearchGraph.Nodes)
        {
            if (d.isMagic || d.stat != ResearchStat.Atk) continue;

            bool ok = true;
            string cur = d.id;
            int guard = 0;
            while (cur != null && guard++ < 32)
            {
                ResearchNodeDef nd = ResearchGraph.Get(cur);
                if (nd == null) { ok = false; break; }
                if (!nd.isMagic)
                {
                    foreach (MaterialCost c in nd.cost)
                        if (c != null && c.materialType != MaterialType.SmallManaCrystal) { ok = false; break; }
                }
                if (!ok) break;
                cur = nd.parentId;
            }
            if (ok) reachable++;
        }
        Assert.GreaterOrEqual(reachable, 6,
            "小結晶だけで到達できる Atk 小ノードが少なすぎる（D6：min-max で Atk が伸びない）");
    }

    // 検証レポート 2026-09-10 O1：深部フロンティアが「Atk カラム＋マナ扇」ばかりで、壁（累積被弾 > 回復）に
    // 対して研究で Def/Hp を伸ばせない。108° の扇をマナ主体 → 防御主体（主 Def / 副 Hp）へ付け替えた結果、
    // 外周（ring6-7）の小ノードでは Def+Hp がマナ（ManaMax+ManaRegen）を十分上回ること。
    [Test]
    public void DeepFrontier_HasMoreDefHpThanMana()
    {
        int defHp = 0, mana = 0;
        foreach (ResearchNodeDef d in ResearchGraph.Nodes)
        {
            if (d.isMagic || d.IsPerk || d.ring < 6) continue;
            if (d.stat == ResearchStat.Def || d.stat == ResearchStat.Hp) defHp++;
            else if (d.stat == ResearchStat.ManaMax || d.stat == ResearchStat.ManaRegen) mana++;
        }
        Assert.Greater(defHp, mana * 3, "外周の Def/Hp 小ノードがマナ扇に埋もれている（O1）");

        // 108° の扇（Bulwark）はマナを一切含まず Def/Hp のみ
        bool hasBulwark = false;
        foreach (ResearchNodeDef d in ResearchGraph.Nodes)
        {
            if (!d.id.StartsWith("nsp_Bulwark_")) continue;
            hasBulwark = true;
            Assert.IsTrue(d.stat == ResearchStat.Def || d.stat == ResearchStat.Hp, d.id + " が Def/Hp でない");
        }
        Assert.IsTrue(hasBulwark, "Bulwark 扇が無い");
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

    // ノードの描画半径（ResearchNodeWidget: 大=150 / 小=92 → 半径 75 / 46）。
    // 防御特性ノード（IsPerk）は魔法と同じ大サイズで描画される。
    private static float NodeRadius(ResearchNodeDef d) { return (d.isMagic || d.IsPerk) ? 75f : 46f; }

    [Test]
    public void NoTwoNodesOverlap()
    {
        List<ResearchNodeDef> ns = new List<ResearchNodeDef>(ResearchGraph.Nodes);
        for (int i = 0; i < ns.Count; i++)
        {
            Vector2 pi = ResearchGraph.Position(ns[i]);
            for (int j = i + 1; j < ns.Count; j++)
            {
                float dist = Vector2.Distance(pi, ResearchGraph.Position(ns[j]));
                float need = NodeRadius(ns[i]) + NodeRadius(ns[j]);
                Assert.GreaterOrEqual(dist, need,
                    ns[i].id + " と " + ns[j].id + " が重なっている (dist=" + dist.ToString("F0") + " / need=" + need.ToString("F0") + ")");
            }
        }
    }

    [Test]
    public void AllNodesFitInsideContentBounds()
    {
        // ResearchTreeView.content は 10000² （中心から ±5000）。余裕をもって半径 3000 以内に収める。
        foreach (ResearchNodeDef d in ResearchGraph.Nodes)
            Assert.Less(ResearchGraph.Position(d).magnitude, 3000f, d.id + " が円盤の外に出ている");
    }

    [Test]
    public void NodesInSameRingShareRadius()
    {
        Dictionary<int, float> radiusByRing = new Dictionary<int, float>();
        foreach (ResearchNodeDef d in ResearchGraph.Nodes)
        {
            float r = ResearchGraph.Position(d).magnitude;
            if (radiusByRing.TryGetValue(d.ring, out float known))
                Assert.AreEqual(known, r, 0.5f, d.id + " の半径が同 ring の他ノードと一致しない");
            else
                radiusByRing[d.ring] = r;
        }
    }
}
