using NUnit.Framework;

// DungeonEconomy（ダンジョンの入場料など、純粋関数）の検証。
public class DungeonEconomyTests
{
    [Test]
    public void EntryFee_IsPositiveConstant()
    {
        Assert.Greater(DungeonEconomy.EntryFee(), 0);
        Assert.AreEqual(DungeonEconomy.EntryFee(), DungeonEconomy.EntryFee());
        Assert.AreEqual(DungeonEconomy.BaseEntryFee, DungeonEconomy.EntryFee());
    }
}
