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
    }
}
