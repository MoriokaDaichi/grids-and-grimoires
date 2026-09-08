using NUnit.Framework;

namespace GridsAndGrimoires.EditModeTests
{
    public class PlayerStatSaveTests
    {
        [Test]
        public void Read_FreshSaveData_ReturnsNotSaved()
        {
            PlayerStatAllocation a = PlayerStatSave.Read(new SaveData());

            Assert.IsFalse(a.saved);
            Assert.AreEqual(0, a.statsPoint);
            Assert.AreEqual(0, a.hp);
        }

        [Test]
        public void Read_NullSaveData_ReturnsNotSaved()
        {
            Assert.IsFalse(PlayerStatSave.Read(null).saved);
        }

        [Test]
        public void WriteTo_ThenRead_RoundTrips()
        {
            SaveData d = new SaveData();
            PlayerStatSave.Write(d, new PlayerStatAllocation
            {
                statsPoint = 4, hp = 30, atk = 2, def = 1, spd = 5, luc = 3,
            });

            PlayerStatAllocation back = PlayerStatSave.Read(d);

            Assert.IsTrue(back.saved);
            Assert.AreEqual(4, back.statsPoint);
            Assert.AreEqual(30, back.hp);
            Assert.AreEqual(2, back.atk);
            Assert.AreEqual(1, back.def);
            Assert.AreEqual(5, back.spd);
            Assert.AreEqual(3, back.luc);
        }

        [Test]
        public void WriteTo_SurvivesJsonRoundTrip()
        {
            SaveData d = new SaveData { money = 77 };
            PlayerStatSave.Write(d, new PlayerStatAllocation { statsPoint = 6, hp = 20, luc = 4 });

            SaveData restored = SaveManager.Deserialize(SaveManager.Serialize(d));
            PlayerStatAllocation a = PlayerStatSave.Read(restored);

            Assert.IsTrue(a.saved);
            Assert.AreEqual(6, a.statsPoint);
            Assert.AreEqual(20, a.hp);
            Assert.AreEqual(4, a.luc);
            Assert.AreEqual(77, restored.money); // 他フィールドを潰さない
        }

        [Test]
        public void WriteTo_DoesNotTouchUnrelatedFields()
        {
            SaveData d = new SaveData();
            d.craftedGearIds.Add("wand_oak");
            d.furnaceFuel = 123;
            d.allocatedResearchNodes.Add("node_atk_1");

            PlayerStatSave.Write(d, new PlayerStatAllocation { statsPoint = 1, atk = 1 });

            Assert.AreEqual(1, d.craftedGearIds.Count);
            Assert.AreEqual(123, d.furnaceFuel);
            Assert.AreEqual(1, d.allocatedResearchNodes.Count);
        }
    }
}
