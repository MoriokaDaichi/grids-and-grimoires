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

    [Test]
    public void EffectiveEntryFee_FreeWhenBroke_ChargedOtherwise()
    {
        // お金が枯れると「潜れない」詰みを防ぐセーフティ：基本料金未満なら無料
        Assert.AreEqual(0, DungeonEconomy.EffectiveEntryFee(0));
        Assert.AreEqual(0, DungeonEconomy.EffectiveEntryFee(DungeonEconomy.BaseEntryFee - 1));
        // 払える所持金があれば通常どおり徴収
        Assert.AreEqual(DungeonEconomy.BaseEntryFee, DungeonEconomy.EffectiveEntryFee(DungeonEconomy.BaseEntryFee));
        Assert.AreEqual(DungeonEconomy.BaseEntryFee, DungeonEconomy.EffectiveEntryFee(9999));
    }
}
