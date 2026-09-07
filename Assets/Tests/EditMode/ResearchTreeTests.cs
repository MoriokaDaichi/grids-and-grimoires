using NUnit.Framework;

// ResearchTree（研究スキルツリーの前提関係）の検証。
public class ResearchTreeTests
{
    [Test]
    public void SingleLine_BaseMegaGiga_ChainsInOrder()
    {
        var p = ResearchTree.Prerequisites;
        Assert.IsFalse(p.ContainsKey("Fire"));          // 根
        Assert.AreEqual("Fire", p["MegaFire"]);
        Assert.AreEqual("MegaFire", p["GigaFire"]);
    }

    [Test]
    public void AoeLine_BaseMegaGiga_ChainsInOrder()
    {
        var p = ResearchTree.Prerequisites;
        Assert.IsFalse(p.ContainsKey("Flame"));         // 根
        Assert.AreEqual("Flame", p["MegaFlame"]);
        Assert.AreEqual("MegaFlame", p["GigaFlame"]);
    }

    [Test]
    public void StatusSpecialization_RequiresMegaSingle()
    {
        Assert.AreEqual("MegaWind", ResearchTree.Prerequisites["StatusLaceration"]);
        Assert.AreEqual("MegaDark", ResearchTree.Prerequisites["StatusBlind"]);
    }

    [Test]
    public void ActiveBuff_RequiresBaseSingle_PassivesChain()
    {
        var p = ResearchTree.Prerequisites;
        Assert.AreEqual("Light", p["BuffLuc"]);
        Assert.AreEqual("BuffLuc", p["BuffLucPassiveLv1"]);
        Assert.AreEqual("BuffLucPassiveLv1", p["BuffLucPassiveLv2"]);
        Assert.AreEqual("BuffLucPassiveLv2", p["BuffLucPassiveLv3"]);
    }

    [Test]
    public void GenericBuffs_RequireTheirMidTierNodes()
    {
        var p = ResearchTree.Prerequisites;
        Assert.AreEqual("MegaThunder", p["AttrBuffThunder"]);
        Assert.AreEqual("StatusShock", p["StatusRateBuffThunder"]);
    }

    [Test]
    public void DualSpell_RequiresAddSpell()
    {
        Assert.AreEqual("AddSpell", ResearchTree.Prerequisites["DualSpell"]);
        Assert.IsFalse(ResearchTree.Prerequisites.ContainsKey("AddSpell"));
    }

    [Test]
    public void Depth_CountsChainLength()
    {
        Assert.AreEqual(0, ResearchTree.Depth("Fire"));
        Assert.AreEqual(1, ResearchTree.Depth("MegaFire"));
        Assert.AreEqual(2, ResearchTree.Depth("GigaFire"));
        Assert.AreEqual(2, ResearchTree.Depth("StatusBurn"));       // Fire→MegaFire→StatusBurn
        Assert.AreEqual(4, ResearchTree.Depth("BuffAtkPassiveLv3")); // Fire→BuffAtk→Lv1→Lv2→Lv3
    }
}
