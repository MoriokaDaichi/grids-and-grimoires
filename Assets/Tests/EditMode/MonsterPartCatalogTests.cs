using System.Collections.Generic;
using NUnit.Framework;

namespace GridsAndGrimoires.EditModeTests
{
    // MonsterPartCatalog（敵40体のドロップの正本）の構造検証。
    public class MonsterPartCatalogTests
    {
        [Test]
        public void All_WellFormed_UniqueNames_TierInRange()
        {
            HashSet<string> names = new HashSet<string>();
            Assert.GreaterOrEqual(MonsterPartCatalog.All.Count, 40, "敵40体ぶんのドロップが登録されているはず");

            foreach (MonsterPart p in MonsterPartCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(p.name));
                Assert.IsTrue(names.Add(p.name), "ドロップ名が重複: " + p.name);
                Assert.GreaterOrEqual(p.tier, 1);
                Assert.LessOrEqual(p.tier, 5);
            }
        }

        [Test]
        public void EveryTier_HasAtLeastOnePart()
        {
            HashSet<int> tiers = new HashSet<int>();
            foreach (MonsterPart p in MonsterPartCatalog.All) tiers.Add(p.tier);
            for (int t = 1; t <= 5; t++) Assert.Contains(t, new List<int>(tiers), "tier" + t + " のドロップが無い");
        }

        [Test]
        public void Get_And_TierOf_And_AttributeOf()
        {
            Assert.IsTrue(MonsterPartCatalog.IsKnown("ゴブリンの牙"));
            Assert.AreEqual(1, MonsterPartCatalog.TierOf("ゴブリンの牙"));
            Assert.AreEqual(2, MonsterPartCatalog.TierOf("古木の芯"));
            Assert.AreEqual(MagicAttribute.Wind, MonsterPartCatalog.AttributeOf("古木の芯"));
            Assert.AreEqual(5, MonsterPartCatalog.TierOf("混沌の核"));

            // 未登録
            Assert.IsFalse(MonsterPartCatalog.IsKnown("存在しない素材"));
            Assert.AreEqual(0, MonsterPartCatalog.TierOf("存在しない素材"));
            Assert.AreEqual(MagicAttribute.None, MonsterPartCatalog.AttributeOf("存在しない素材"));
            Assert.IsNull(MonsterPartCatalog.Get(null));
        }

        [Test]
        public void KeepsLegacyPartsForCompatibility()
        {
            foreach (string legacy in new[] { "スライムゼリー", "ゴブリンの牙", "大ネズミの尾", "番人の樹皮", "古木の芯" })
                Assert.IsTrue(MonsterPartCatalog.IsKnown(legacy), legacy + " が失われている");
        }
    }
}
