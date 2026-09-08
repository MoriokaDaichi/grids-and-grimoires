using NUnit.Framework;
using UnityEngine;

// ManaRules（マナ消費・回復ルール、純粋関数）の検証。数値は仮バランスなので式の意図を固定する。
public class ManaRulesTests
{
    private static MagicData Magic(MagicCategory category, MagicRange range, int damage, int manaCost = 0)
    {
        MagicData md = ScriptableObject.CreateInstance<MagicData>();
        md.category = category;
        md.range = range;
        md.damage = damage;
        md.manaCost = manaCost;
        return md;
    }

    [Test]
    public void CastCost_SingleAttack_ScalesWithDamage()
    {
        // 8 + ceil(damage * 0.5)
        Assert.AreEqual(11, ManaRules.CastCost(Magic(MagicCategory.Attack, MagicRange.Single, 5)));
        Assert.AreEqual(18, ManaRules.CastCost(Magic(MagicCategory.Attack, MagicRange.Single, 20)));
    }

    [Test]
    public void CastCost_AoeAttack_IsPricierThanSingle()
    {
        // 18 + damage
        Assert.AreEqual(21, ManaRules.CastCost(Magic(MagicCategory.Attack, MagicRange.AoE, 3)));
        Assert.AreEqual(30, ManaRules.CastCost(Magic(MagicCategory.Attack, MagicRange.AoE, 12)));

        int single = ManaRules.CastCost(Magic(MagicCategory.Attack, MagicRange.Single, 12));
        int aoe = ManaRules.CastCost(Magic(MagicCategory.Attack, MagicRange.AoE, 12));
        Assert.Greater(aoe, single);
    }

    [Test]
    public void CastCost_FixedCategories()
    {
        Assert.AreEqual(12, ManaRules.CastCost(Magic(MagicCategory.StatusInflict, MagicRange.Single, 0)));
        Assert.AreEqual(20, ManaRules.CastCost(Magic(MagicCategory.Support, MagicRange.None, 0)));
        Assert.AreEqual(25, ManaRules.CastCost(Magic(MagicCategory.BuffActive, MagicRange.None, 0)));
        Assert.AreEqual(0, ManaRules.CastCost(Magic(MagicCategory.BuffPassive, MagicRange.None, 0)));
    }

    [Test]
    public void CastCost_ExplicitValueWins()
    {
        // manaCost > 0 が焼き込まれていれば自動算出せずそれを返す
        MagicData md = Magic(MagicCategory.Attack, MagicRange.Single, 5, manaCost: 99);
        Assert.AreEqual(99, ManaRules.CastCost(md));
    }

    [Test]
    public void CastCost_Null_IsZero()
    {
        Assert.AreEqual(0, ManaRules.CastCost(null));
    }

    [Test]
    public void RegenAmount_IsLinearInTime()
    {
        Assert.AreEqual(4f, ManaRules.RegenAmount(8f, 0.5f), 0.0001f);
        Assert.AreEqual(8f, ManaRules.RegenAmount(8f, 1f), 0.0001f);
    }

    [Test]
    public void RegenAmount_NonPositiveInputs_AreZero()
    {
        Assert.AreEqual(0f, ManaRules.RegenAmount(0f, 1f));
        Assert.AreEqual(0f, ManaRules.RegenAmount(8f, 0f));
        Assert.AreEqual(0f, ManaRules.RegenAmount(8f, -1f));
    }
}
