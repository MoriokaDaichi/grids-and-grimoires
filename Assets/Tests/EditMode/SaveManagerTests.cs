using NUnit.Framework;

namespace GridsAndGrimoires.EditModeTests
{
    public class SaveManagerTests
    {
        [Test]
        public void SerializeThenDeserialize_RoundTrips()
        {
            SaveData src = new SaveData();
            src.materials.Add(new MaterialStack { materialType = 0, attribute = 0, specialItemName = "", count = 7 });
            src.materials.Add(new MaterialStack { materialType = 3, attribute = 4, specialItemName = "", count = 2 });

            SaveData dst = SaveManager.Deserialize(SaveManager.Serialize(src));

            Assert.AreEqual(2, dst.materials.Count);
            Assert.AreEqual(7, dst.materials[0].count);
            Assert.AreEqual(3, dst.materials[1].materialType);
            Assert.AreEqual(4, dst.materials[1].attribute);
            Assert.AreEqual(2, dst.materials[1].count);
        }

        [Test]
        public void Deserialize_EmptyOrNull_ReturnsEmptySaveData()
        {
            Assert.IsNotNull(SaveManager.Deserialize(null));
            Assert.IsNotNull(SaveManager.Deserialize(""));
            Assert.AreEqual(0, SaveManager.Deserialize("").materials.Count);
        }

        [Test]
        public void HideoutFields_RoundTrip()
        {
            SaveData src = new SaveData();
            src.facilityLevels.Add(new StringIntPair { key = FacilityKind.ManaFurnace.ToString(), value = 2 });
            src.facilityLevels.Add(new StringIntPair { key = FacilityKind.ResearchDesk.ToString(), value = 1 });
            src.furnaceFuel = 137;
            src.magicCircleBrews.Add(new BrewRecord { startUnixSeconds = 1000.5, hours = 6f, inputRarity = 1, inputLabel = "ゴブリンの牙" });
            src.craftedGearIds.Add("wand_oak");

            SaveData dst = SaveManager.Deserialize(SaveManager.Serialize(src));

            Assert.AreEqual(2, dst.facilityLevels.Count);
            Assert.AreEqual(FacilityKind.ManaFurnace.ToString(), dst.facilityLevels[0].key);
            Assert.AreEqual(2, dst.facilityLevels[0].value);
            Assert.AreEqual(137, dst.furnaceFuel);
            Assert.AreEqual(1, dst.magicCircleBrews.Count);
            Assert.AreEqual(6f, dst.magicCircleBrews[0].hours);
            Assert.AreEqual("ゴブリンの牙", dst.magicCircleBrews[0].inputLabel);
            Assert.AreEqual("wand_oak", dst.craftedGearIds[0]);
        }

        [Test]
        public void NewSaveData_HideoutCollectionsNonNull()
        {
            SaveData d = SaveManager.Deserialize("");
            Assert.IsNotNull(d.facilityLevels);
            Assert.IsNotNull(d.magicCircleBrews);
            Assert.IsNotNull(d.craftedGearIds);
            Assert.IsNotNull(d.unlockedGearRecipes);
            Assert.AreEqual(0, d.furnaceFuel);
        }

        [Test]
        public void MoneyAndRecipeFields_RoundTrip()
        {
            SaveData src = new SaveData();
            src.money = 235;
            src.moneyInitialized = true;
            src.unlockedGearRecipes.Add("wand_runed");
            src.unlockedGearRecipes.Add("armor_aegis");

            SaveData dst = SaveManager.Deserialize(SaveManager.Serialize(src));

            Assert.AreEqual(235, dst.money);
            Assert.IsTrue(dst.moneyInitialized);
            Assert.AreEqual(2, dst.unlockedGearRecipes.Count);
            Assert.Contains("wand_runed", dst.unlockedGearRecipes);
            Assert.Contains("armor_aegis", dst.unlockedGearRecipes);
        }

        [Test]
        public void NewSaveData_MoneyDefaultsToZeroUninitialized()
        {
            SaveData d = SaveManager.Deserialize("");
            Assert.AreEqual(0, d.money);
            Assert.IsFalse(d.moneyInitialized);
        }

        [Test]
        public void RepeatableTaskFields_RoundTrip()
        {
            SaveData src = new SaveData();
            src.repeatableTaskBaselines.Add(new StringIntPair { key = "dag_grind", value = 125 });
            src.repeatableTaskClaims.Add(new StringIntPair { key = "dag_grind", value = 5 });

            SaveData dst = SaveManager.Deserialize(SaveManager.Serialize(src));

            Assert.AreEqual(1, dst.repeatableTaskBaselines.Count);
            Assert.AreEqual("dag_grind", dst.repeatableTaskBaselines[0].key);
            Assert.AreEqual(125, dst.repeatableTaskBaselines[0].value);
            Assert.AreEqual(5, dst.repeatableTaskClaims[0].value);
        }
    }
}
