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
            {
                if (m != null) UnityEngine.Object.DestroyImmediate(m);
            }
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
        public void PrerequisiteId_FollowsSkillTree()
        {
            Assert.AreEqual("BuffAtkPassiveLv1", ResearchRules.PrerequisiteId("BuffAtkPassiveLv2"));
            Assert.AreEqual("BuffAtkPassiveLv2", ResearchRules.PrerequisiteId("BuffAtkPassiveLv3"));
            Assert.AreEqual("BuffAtk", ResearchRules.PrerequisiteId("BuffAtkPassiveLv1"));
            Assert.AreEqual("Fire", ResearchRules.PrerequisiteId("MegaFire"));
            Assert.AreEqual("MegaFire", ResearchRules.PrerequisiteId("GigaFire"));
            Assert.IsNull(ResearchRules.PrerequisiteId("Fire")); // 根
            Assert.IsNull(ResearchRules.PrerequisiteId(null));
        }

        [Test]
        public void IsUnlocked_BaseFreeAlwaysUnlocked()
        {
            HashSet<string> none = new HashSet<string>();
            Assert.IsTrue(ResearchRules.IsUnlocked(Magic("Fire"), none));
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
            // MegaFire は Fire（基本・初期解放）が前提。前提充足時は afford 次第。
            MagicData mega = Magic("MegaFire", Small(3));
            HashSet<string> set = new HashSet<string>();
            Assert.IsTrue(ResearchRules.CanUnlock(mega, set, id => id == "Fire", c => true));
            Assert.IsFalse(ResearchRules.CanUnlock(mega, set, id => id == "Fire", c => false));
        }

        [Test]
        public void CanUnlock_PrereqNotMet_AlwaysFalse()
        {
            // GigaFire は MegaFire が前提。未解放なら afford できても不可。
            MagicData giga = Magic("GigaFire", Small(3));
            HashSet<string> set = new HashSet<string>();
            Assert.IsFalse(ResearchRules.CanUnlock(giga, set, id => false, c => true));
            Assert.IsTrue(ResearchRules.CanUnlock(giga, set, id => id == "MegaFire", c => true));
        }

        [Test]
        public void CanUnlock_PassiveLv2_RequiresLv1()
        {
            MagicData lv2 = Magic("BuffAtkPassiveLv2", Small(1));
            HashSet<string> set = new HashSet<string>();

            // Lv1 未解放 → afford できても不可
            Assert.IsFalse(ResearchRules.CanUnlock(lv2, set, id => false, c => true));

            // Lv1 解放済み → 可
            Assert.IsTrue(ResearchRules.CanUnlock(lv2, set, id => id == "BuffAtkPassiveLv1", c => true));
        }

        [Test]
        public void PrerequisiteMet_RootMagic_AlwaysTrue()
        {
            Assert.IsTrue(ResearchRules.PrerequisiteMet(Magic("Fire"), id => false));
        }
    }
}
