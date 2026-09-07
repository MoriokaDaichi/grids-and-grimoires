using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace GridsAndGrimoires.EditModeTests
{
    public class ResearchRulesTests
    {
        private readonly List<MagicData> created = new List<MagicData>();

        [TearDown]
        public void TearDown()
        {
            foreach (MagicData m in created)
                if (m != null) UnityEngine.Object.DestroyImmediate(m);
            created.Clear();
        }

        private MagicData Magic(string id, params MaterialCost[] cost)
        {
            MagicData m = ScriptableObject.CreateInstance<MagicData>();
            m.name = id;
            m.magicName = id;
            m.requiredMaterials = new List<MaterialCost>(cost);
            created.Add(m);
            return m;
        }

        private static MaterialCost Small(int n)
        {
            return new MaterialCost { materialType = MaterialType.SmallManaCrystal, amount = n };
        }

        [Test]
        public void IsBaseFree_NoCost_True_WithCost_False()
        {
            Assert.IsTrue(ResearchRules.IsBaseFree(Magic("Fire")));
            Assert.IsFalse(ResearchRules.IsBaseFree(Magic("MegaFire", Small(3))));
        }

        [Test]
        public void PrerequisiteId_FollowsGraph()
        {
            // 大ノードは小ノードを経由する（グラフに準拠）
            Assert.AreEqual("node_Fire_a", ResearchRules.PrerequisiteId("MegaFire"));
            Assert.AreEqual("BuffAtkPassiveLv1", ResearchRules.PrerequisiteId("BuffAtkPassiveLv2"));
            Assert.AreEqual("BuffAtk", ResearchRules.PrerequisiteId("BuffAtkPassiveLv1"));
            Assert.IsNull(ResearchRules.PrerequisiteId("Fire")); // 根
            Assert.IsNull(ResearchRules.PrerequisiteId(null));
        }

        [Test]
        public void IsUnlocked_BaseFreeAlwaysUnlocked()
        {
            Assert.IsTrue(ResearchRules.IsUnlocked(Magic("Fire"), new HashSet<string>()));
        }

        [Test]
        public void IsUnlocked_CostedRequiresSetMembership()
        {
            MagicData mega = Magic("MegaFire", Small(3));
            Assert.IsFalse(ResearchRules.IsUnlocked(mega, new HashSet<string>()));
            Assert.IsTrue(ResearchRules.IsUnlocked(mega, new HashSet<string> { "MegaFire" }));
        }

        [Test]
        public void CanUnlock_AlreadyUnlocked_False()
        {
            MagicData mega = Magic("MegaFire", Small(3));
            HashSet<string> set = new HashSet<string> { "MegaFire" };
            Assert.IsFalse(ResearchRules.CanUnlock(mega, set, id => set.Contains(id), c => true));
        }

        [Test]
        public void CanUnlock_PrereqMet_DependsOnAfford()
        {
            MagicData mega = Magic("MegaFire", Small(3));
            HashSet<string> set = new HashSet<string>();
            string prereq = ResearchRules.PrerequisiteId("MegaFire");
            Assert.IsNotNull(prereq);

            Assert.IsTrue(ResearchRules.CanUnlock(mega, set, id => id == prereq, c => true));
            Assert.IsFalse(ResearchRules.CanUnlock(mega, set, id => id == prereq, c => false));
        }

        [Test]
        public void CanUnlock_PrereqNotMet_AlwaysFalse()
        {
            MagicData giga = Magic("GigaFire", Small(3));
            HashSet<string> set = new HashSet<string>();
            string prereq = ResearchRules.PrerequisiteId("GigaFire");
            Assert.IsNotNull(prereq);

            Assert.IsFalse(ResearchRules.CanUnlock(giga, set, id => false, c => true));
            Assert.IsTrue(ResearchRules.CanUnlock(giga, set, id => id == prereq, c => true));
        }

        [Test]
        public void PrerequisiteMet_RootMagic_AlwaysTrue()
        {
            Assert.IsTrue(ResearchRules.PrerequisiteMet(Magic("Fire"), id => false));
        }
    }
}
