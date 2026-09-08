using System.Collections.Generic;
using NUnit.Framework;

namespace GridsAndGrimoires.EditModeTests
{
    // EnemyStatus から切り出した状態異常タイマーの挙動を固定する（仮バランス値も含めて記録）。
    public class StatusEffectControllerTests
    {
        private StatusEffectController controller;
        private List<StatusEffectController.StatusTickResult> ticks;
        private List<StatusEffectType> expired;

        [SetUp]
        public void SetUp()
        {
            controller = new StatusEffectController();
            ticks = new List<StatusEffectController.StatusTickResult>();
            expired = new List<StatusEffectType>();
        }

        private void Tick(float dt)
        {
            ticks.Clear();
            expired.Clear();
            controller.Tick(dt, ticks, expired);
        }

        [Test]
        public void Burn_TicksThreeDamageEachSecond_ExpiresAfterFiveSeconds()
        {
            Assert.IsTrue(controller.Apply(StatusEffectType.Burn));

            int totalTicks = 0;
            for (int second = 1; second <= 5; second++)
            {
                Tick(1f);
                Assert.AreEqual(1, ticks.Count, $"{second}秒目のtick数");
                Assert.AreEqual(StatusEffectType.Burn, ticks[0].Type);
                Assert.AreEqual(3, ticks[0].Damage);
                totalTicks += ticks.Count;
            }

            Assert.AreEqual(5, totalTicks);
            Assert.Contains(StatusEffectType.Burn, expired);
            Assert.IsFalse(controller.HasAny);
        }

        [Test]
        public void Laceration_TicksTwoDamage_ForEightSeconds()
        {
            controller.Apply(StatusEffectType.Laceration);

            int totalTicks = 0;
            for (int second = 1; second <= 8; second++)
            {
                Tick(1f);
                Assert.AreEqual(1, ticks.Count);
                Assert.AreEqual(2, ticks[0].Damage);
                totalTicks += ticks.Count;
            }

            Assert.AreEqual(8, totalTicks);
            Assert.Contains(StatusEffectType.Laceration, expired);
        }

        [Test]
        public void Burn_Reapply_RefreshesDuration_DoesNotStack()
        {
            controller.Apply(StatusEffectType.Burn);
            Tick(1f);
            Tick(1f); // 残り 3 秒

            Assert.IsFalse(controller.Apply(StatusEffectType.Burn), "再付与はリフレッシュ扱いで false");

            // リフレッシュ後、さらに5秒ぶん。stackしていれば1回のTickで2件出るはず。
            for (int second = 1; second <= 5; second++)
            {
                Tick(1f);
                Assert.AreEqual(1, ticks.Count, "1本ぶんのtickのみ（重ね掛けされていない）");
            }
            Assert.Contains(StatusEffectType.Burn, expired);
            Assert.IsFalse(controller.HasAny);
        }

        [Test]
        public void Shock_StunsForThreeSeconds_NoDamageTicks()
        {
            controller.Apply(StatusEffectType.Shock);
            Assert.IsTrue(controller.IsStunned);

            Tick(1f);
            Tick(1f);
            Assert.IsTrue(controller.IsStunned);
            Assert.AreEqual(0, ticks.Count);

            Tick(1f); // 3秒経過
            Assert.IsFalse(controller.IsStunned);
            Assert.Contains(StatusEffectType.Shock, expired);
        }

        [Test]
        public void Dizzy_LengthensAttackInterval_ThenClears()
        {
            controller.Apply(StatusEffectType.Dizzy);
            Assert.AreEqual(1.5f, controller.AttackIntervalMultiplier);

            Tick(5f);
            Assert.AreEqual(1f, controller.AttackIntervalMultiplier);
            Assert.Contains(StatusEffectType.Dizzy, expired);
        }

        [Test]
        public void Blind_HalvesAtk_ThenClears()
        {
            controller.Apply(StatusEffectType.Blind);
            Assert.AreEqual(0.5f, controller.AtkMultiplier);

            Tick(5f);
            Assert.AreEqual(1f, controller.AtkMultiplier);
            Assert.Contains(StatusEffectType.Blind, expired);
        }

        [Test]
        public void Apply_None_ReturnsFalse_AddsNothing()
        {
            Assert.IsFalse(controller.Apply(StatusEffectType.None));
            Assert.IsFalse(controller.HasAny);
        }

        [Test]
        public void Reset_ClearsAllEffects()
        {
            controller.Apply(StatusEffectType.Burn);
            controller.Apply(StatusEffectType.Shock);
            Assert.IsTrue(controller.HasAny);

            controller.Reset();
            Assert.IsFalse(controller.HasAny);
            Assert.IsFalse(controller.IsStunned);
        }
    }
}
