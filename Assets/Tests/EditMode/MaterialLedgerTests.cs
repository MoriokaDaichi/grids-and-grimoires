using System.Collections.Generic;
using NUnit.Framework;

namespace GridsAndGrimoires.EditModeTests
{
    public class MaterialLedgerTests
    {
        private static MaterialCost Cost(MaterialType type, int amount)
        {
            return new MaterialCost { materialType = type, amount = amount };
        }

        private static MaterialCost Frag(MagicAttribute attr, int amount)
        {
            return new MaterialCost { materialType = MaterialType.ElementFragment, attribute = attr, amount = amount };
        }

        private static List<MaterialCost> L(params MaterialCost[] c)
        {
            return new List<MaterialCost>(c);
        }

        [Test]
        public void Add_SameMaterialTwice_Stacks()
        {
            MaterialLedger l = new MaterialLedger();
            l.Add(L(Cost(MaterialType.SmallManaCrystal, 2)));
            l.Add(L(Cost(MaterialType.SmallManaCrystal, 3)));
            Assert.AreEqual(5, l.GetCount(Cost(MaterialType.SmallManaCrystal, 1)));
        }

        [Test]
        public void Add_AttributeScopedMaterials_KeptSeparate()
        {
            MaterialLedger l = new MaterialLedger();
            l.Add(L(Frag(MagicAttribute.Wind, 1), Frag(MagicAttribute.Light, 2)));
            Assert.AreEqual(1, l.GetCount(Frag(MagicAttribute.Wind, 1)));
            Assert.AreEqual(2, l.GetCount(Frag(MagicAttribute.Light, 1)));
        }

        [Test]
        public void TrySpend_Enough_DecrementsAndReturnsTrue()
        {
            MaterialLedger l = new MaterialLedger();
            l.Add(L(Cost(MaterialType.SmallManaCrystal, 5)));
            Assert.IsTrue(l.TrySpend(L(Cost(MaterialType.SmallManaCrystal, 3))));
            Assert.AreEqual(2, l.GetCount(Cost(MaterialType.SmallManaCrystal, 1)));
        }

        [Test]
        public void TrySpend_NotEnough_NoChangeReturnsFalse()
        {
            MaterialLedger l = new MaterialLedger();
            l.Add(L(Cost(MaterialType.SmallManaCrystal, 2)));
            Assert.IsFalse(l.TrySpend(L(Cost(MaterialType.SmallManaCrystal, 3))));
            Assert.AreEqual(2, l.GetCount(Cost(MaterialType.SmallManaCrystal, 1)));
        }

        [Test]
        public void TrySpend_MultiCost_OneMissing_NothingConsumed()
        {
            MaterialLedger l = new MaterialLedger();
            l.Add(L(Cost(MaterialType.SmallManaCrystal, 5)));
            Assert.IsFalse(l.TrySpend(L(
                Cost(MaterialType.SmallManaCrystal, 2),
                Cost(MaterialType.MediumManaCrystal, 1))));
            Assert.AreEqual(5, l.GetCount(Cost(MaterialType.SmallManaCrystal, 1)));
        }

        [Test]
        public void TrySpend_ExactAll_RemovesEntry()
        {
            MaterialLedger l = new MaterialLedger();
            l.Add(L(Cost(MaterialType.SmallManaCrystal, 3)));
            Assert.IsTrue(l.TrySpend(L(Cost(MaterialType.SmallManaCrystal, 3))));
            Assert.AreEqual(0, l.GetCount(Cost(MaterialType.SmallManaCrystal, 1)));
        }

        [Test]
        public void TrySpend_Null_ReturnsTrue()
        {
            Assert.IsTrue(new MaterialLedger().TrySpend(null));
        }

        [Test]
        public void SaveRoundTrip_PreservesCounts()
        {
            MaterialLedger src = new MaterialLedger();
            src.Add(L(
                Cost(MaterialType.SmallManaCrystal, 7),
                Cost(MaterialType.MediumManaCrystal, 1),
                Frag(MagicAttribute.Light, 2)));

            SaveData data = new SaveData();
            src.WriteTo(data);

            MaterialLedger dst = new MaterialLedger();
            dst.LoadFrom(data);

            Assert.AreEqual(7, dst.GetCount(Cost(MaterialType.SmallManaCrystal, 1)));
            Assert.AreEqual(1, dst.GetCount(Cost(MaterialType.MediumManaCrystal, 1)));
            Assert.AreEqual(2, dst.GetCount(Frag(MagicAttribute.Light, 1)));
        }
    }
}
