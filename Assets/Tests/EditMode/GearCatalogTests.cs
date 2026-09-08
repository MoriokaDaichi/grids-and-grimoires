using NUnit.Framework;

namespace GridsAndGrimoires.EditModeTests
{
    public class GearCatalogTests
    {
        [Test]
        public void EveryGear_WellFormed_UniqueIds()
        {
            System.Collections.Generic.HashSet<string> ids = new System.Collections.Generic.HashSet<string>();
            foreach (GearDef g in GearCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(g.id));
                Assert.IsFalse(string.IsNullOrEmpty(g.name));
                Assert.IsTrue(ids.Add(g.id), "gear id 重複: " + g.id);
                Assert.GreaterOrEqual(g.tier, 1);
                Assert.LessOrEqual(g.tier, 3);
                Assert.Greater(g.amount, 0f);
                Assert.Greater(g.cost.Count, 0, g.id + " にコストが無い");
            }
        }

        [Test]
        public void AllThreeSlots_HaveGearAtEachTier()
        {
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
            {
                for (int tier = 1; tier <= 3; tier++)
                {
                    bool found = false;
                    foreach (GearDef g in GearCatalog.All)
                        if (g.slot == slot && g.tier == tier) found = true;
                    Assert.IsTrue(found, slot + " の tier" + tier + " が無い");
                }
            }
        }

        [Test]
        public void Craftable_GatedByWorkbenchLevel()
        {
            Assert.AreEqual(0, GearCatalog.Craftable(0).Count);
            Assert.AreEqual(3, GearCatalog.Craftable(1).Count);
            Assert.AreEqual(6, GearCatalog.Craftable(2).Count);
            Assert.AreEqual(GearCatalog.All.Count, GearCatalog.Craftable(3).Count);
        }

        [Test]
        public void Get_ReturnsByIdOrNull()
        {
            Assert.IsNotNull(GearCatalog.Get("wand_oak"));
            Assert.IsNull(GearCatalog.Get("nope"));
        }
    }
}
