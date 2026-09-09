using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace GridsAndGrimoires.EditModeTests
{
    // PlayerStatus のうちシーンに依存しない挙動（バースト即死クランプ＝レポート C2/D5）を検証する。
    public class PlayerStatusTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private PlayerStatus NewPlayer(int maxHp)
        {
            GameObject go = new GameObject("player");
            spawned.Add(go);
            PlayerStatus ps = go.AddComponent<PlayerStatus>();
            ps.hp = maxHp;
            typeof(PlayerStatus).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ps, null);
            return ps;
        }

        [Test]
        public void TakeDamage_SingleWave_CannotRemoveMoreThanCapFraction()
        {
            PlayerStatus ps = NewPlayer(200);
            int cap = BattleFormula.WaveDamageCap(200); // = 120

            ps.BeginWave();
            ps.TakeDamage(9999);
            Assert.AreEqual(200 - cap, ps.currentHp, "満タンからの1発では最大HPの上限ぶんしか削れない");

            ps.TakeDamage(9999); // 同ウェーブの追撃は無効化される
            Assert.AreEqual(200 - cap, ps.currentHp, "同ウェーブでは上限を超えて削れない");
        }

        // --- 研究の防御特性ノード（検証レポート 2026-09-10 O1）---

        [Test]
        public void Perk_FlatWard_SubtractsFixedAmountPerHit()
        {
            PlayerStatus ps = NewPlayer(500);
            ps.ApplyResearchPerk(ResearchPerk.FlatWard, 4f);
            ps.BeginWave();
            ps.TakeDamage(30);
            Assert.AreEqual(500 - 26, ps.currentHp, "固定軽減ぶんだけ被弾が減る");
        }

        [Test]
        public void Perk_PercentWard_ScalesDamageDown()
        {
            PlayerStatus ps = NewPlayer(500);
            ps.ApplyResearchPerk(ResearchPerk.PercentWard, 10f); // 10%
            ps.BeginWave();
            ps.TakeDamage(100);
            Assert.AreEqual(500 - 90, ps.currentHp);
        }

        [Test]
        public void Perk_PercentWard_CapsAt40Percent()
        {
            PlayerStatus ps = NewPlayer(1000);
            for (int i = 0; i < 10; i++) ps.ApplyResearchPerk(ResearchPerk.PercentWard, 10f); // 100% 積んでも
            ps.BeginWave();
            ps.TakeDamage(100);
            Assert.AreEqual(1000 - 60, ps.currentHp, "割合軽減は 40% で頭打ち");
        }

        [Test]
        public void Perk_LastStand_OnlyAppliesBelowThreshold()
        {
            PlayerStatus ps = NewPlayer(1000);
            ps.ApplyResearchPerk(ResearchPerk.LastStand, 25f); // HP35%以下で 25% 軽減

            ps.BeginWave();
            ps.TakeDamage(100); // 100% → 閾値より上、素通り
            Assert.AreEqual(900, ps.currentHp);

            ps.BeginWave();
            ps.TakeDamage(600); // → 300 (30% ≤ 35%)。ここまでは軽減対象外
            Assert.AreEqual(300, ps.currentHp);

            ps.BeginWave();
            ps.TakeDamage(100); // 低HP状態なので 25% 軽減 → 75
            Assert.AreEqual(225, ps.currentHp);
        }

        [Test]
        public void Perk_WaveBarrier_AbsorbsThenRefreshesEachWave()
        {
            PlayerStatus ps = NewPlayer(1000);
            ps.ApplyResearchPerk(ResearchPerk.WaveBarrier, 10f); // 最大HPの10% = 100

            ps.BeginWave();
            Assert.AreEqual(100, ps.BarrierCurrent);

            ps.TakeDamage(60); // バリアが吸う
            Assert.AreEqual(1000, ps.currentHp);
            Assert.AreEqual(40, ps.BarrierCurrent);

            ps.TakeDamage(90); // 40 はバリア、残り 50 が HP へ
            Assert.AreEqual(950, ps.currentHp);
            Assert.AreEqual(0, ps.BarrierCurrent);

            ps.BeginWave(); // 次ウェーブで張り直し
            Assert.AreEqual(100, ps.BarrierCurrent);
        }

        [Test]
        public void Perk_BuffsStackOnStartupReapply_AndDoNotBreakClamp()
        {
            PlayerStatus ps = NewPlayer(200);
            ps.ApplyResearchPerk(ResearchPerk.FlatWard, 3f);
            ps.ApplyResearchPerk(ResearchPerk.FlatWard, 3f); // 起動時の再適用を想定して二重加算
            ps.BeginWave();
            ps.TakeDamage(9999);
            // 固定軽減で amount は減るが、9999 なら依然クランプが効く（満タン開始）
            Assert.AreEqual(200 - BattleFormula.WaveDamageCap(200), ps.currentHp);
        }

        [Test]
        public void Perk_OffensiveTraits_AccumulateAndCap()
        {
            PlayerStatus ps = NewPlayer(300);

            ps.ApplyResearchPerk(ResearchPerk.SpellPower, 8f);
            ps.ApplyResearchPerk(ResearchPerk.SpellPower, 12f);
            Assert.AreEqual(20f, ps.spellPowerPercent, 0.001f, "魔力増幅は素の%ポイントで加算");

            for (int i = 0; i < 10; i++) ps.ApplyResearchPerk(ResearchPerk.CritPower, 25f);
            Assert.AreEqual(0.5f, ps.critMultBonus, 0.001f, "痛撃は +0.5 で頭打ち");

            for (int i = 0; i < 10; i++) ps.ApplyResearchPerk(ResearchPerk.CastHaste, 8f);
            Assert.AreEqual(0.4f, ps.castHastePercent, 0.001f, "詠唱加速は 40% で頭打ち");

            ps.ApplyResearchPerk(ResearchPerk.ArmorPierce, 3f);
            ps.ApplyResearchPerk(ResearchPerk.ArmorPierce, 5f);
            Assert.AreEqual(8, ps.armorPierce, "貫通は整数で加算");

            for (int i = 0; i < 10; i++) ps.ApplyResearchPerk(ResearchPerk.Execute, 40f);
            Assert.AreEqual(0.6f, ps.executeBonusPercent, 0.001f, "処刑は 60% で頭打ち");
        }

        [Test]
        public void TakeDamage_ClampResetsEachWave()
        {
            PlayerStatus ps = NewPlayer(200);

            ps.BeginWave();
            ps.TakeDamage(9999); // → 80

            ps.BeginWave();       // 次ウェーブでクランプ枠が復活
            bool defeated = false;
            ps.OnDefeated += () => defeated = true;
            ps.TakeDamage(9999);
            Assert.AreEqual(0, ps.currentHp);
            Assert.IsTrue(defeated, "低HPで次ウェーブに入れば、そのウェーブのバーストで倒れうる");
        }

        [Test]
        public void TakeDamage_LowStartingHp_StillDies_WhenHeadroomExceedsCurrentHp()
        {
            PlayerStatus ps = NewPlayer(100);
            ps.BeginWave();
            ps.TakeDamage(40); // → 60
            ps.BeginWave();
            ps.TakeDamage(80); // 死ねる
            Assert.AreEqual(0, ps.currentHp);
        }

        // 再検証4 R1：ウェーブ開始時に削れていた（HP割合 < WaveClampMinStartFraction）ら
        // クランプは効かない＝そのウェーブのバーストで一気に落ちうる（延命バフにしない）。
        [Test]
        public void TakeDamage_WaveStartedLow_ClampDoesNotEngage()
        {
            PlayerStatus ps = NewPlayer(200); // cap = round(0.85*200) = 170
            // 1ウェーブ目で 50% まで削っておく（クランプ有効な状態から）
            ps.BeginWave();
            ps.TakeDamage(100); // → 100 (50%)
            Assert.AreEqual(100, ps.currentHp);

            // 開始 50% < 0.55 のウェーブでは 170 の上限を無視して一撃で 0 になれる
            ps.BeginWave();
            ps.TakeDamage(9999);
            Assert.AreEqual(0, ps.currentHp);
        }

        [Test]
        public void TakeDamage_WaveStartedHealthy_ClampEngages()
        {
            PlayerStatus ps = NewPlayer(200);
            ps.BeginWave(); // 100% ≥ 0.55
            ps.TakeDamage(9999);
            Assert.AreEqual(200 - BattleFormula.WaveDamageCap(200), ps.currentHp);
            Assert.Greater(ps.currentHp, 0);
        }

        // 再検証7 R4：毎秒回復で1ウェーブに戻せる量は WaveHealCap まで。
        // 検証レポート 2026-09-10 N1：手動振り分けの上限は「合計値」ではなく「手動加算分」で判定する。
        // 研究・装備で合計が旧キャップ(1000/100)を超えていても、+ボタンは効き statsPoint を消費する。
        [Test]
        public void AddStat_CapsOnManualPortion_NotTotalWithResearch()
        {
            PlayerStatus ps = NewPlayer(200);
            ps.statsPoint = 500;
            ps.ApplyResearchDelta(ResearchStat.Hp, 2000f);
            ps.ApplyResearchDelta(ResearchStat.Atk, 500f);
            Assert.Greater(ps.hp, 1000);
            Assert.Greater(ps.atk, 100);

            int hpBefore = ps.hp, atkBefore = ps.atk, spBefore = ps.statsPoint;
            ps.AddStat("HP");
            ps.AddStat("Atk");

            Assert.AreEqual(hpBefore + 10, ps.hp, "研究で合計が旧キャップ超でも手動HPは振れる");
            Assert.AreEqual(atkBefore + 1, ps.atk, "研究で合計が旧キャップ超でも手動Atkは振れる");
            Assert.AreEqual(spBefore - 2, ps.statsPoint, "振れたぶんだけ statsPoint を消費する");
        }

        [Test]
        public void AddStat_DoesNotConsumePoint_WhenManualCapReached()
        {
            PlayerStatus ps = NewPlayer(200);
            ps.statsPoint = 10000;
            for (int i = 0; i < 400; i++) ps.AddStat("Atk"); // 手動 Atk 上限(300)まで振り切る

            Assert.IsFalse(ps.CanAddStat("Atk"), "手動上限に達したら CanAddStat は false");
            int spBefore = ps.statsPoint, atkBefore = ps.atk;
            ps.AddStat("Atk");
            Assert.AreEqual(spBefore, ps.statsPoint, "上限到達後は statsPoint を消費しない");
            Assert.AreEqual(atkBefore, ps.atk, "上限到達後は加算もしない");
        }

        [Test]
        public void AddStat_UnknownType_DoesNotConsumePoint()
        {
            PlayerStatus ps = NewPlayer(200);
            ps.statsPoint = 5;
            ps.AddStat("Mana");
            Assert.AreEqual(5, ps.statsPoint, "未知のtypeでは statsPoint を消費しない");
        }

        [Test]
        public void RegenHealth_IsCappedPerWave()
        {
            PlayerStatus ps = NewPlayer(200);          // WaveHealCap(200) = 100
            ps.hpRegenPerSecond = 50f;
            ps.BeginWave();
            ps.TakeDamage(160);                        // wave start 100% → clamp caps at 170; applied 160 → curHp 40
            Assert.AreEqual(40, ps.currentHp);

            for (int i = 0; i < 20; i++) ps.RegenHealth(1f); // 50/s ×20s = 1000 want, capped at 100
            Assert.AreEqual(40 + BattleFormula.WaveHealCap(200), ps.currentHp, "1ウェーブの回復が上限を超えている");

            ps.BeginWave();                            // 次ウェーブで枠が復活
            for (int i = 0; i < 5; i++) ps.RegenHealth(1f);
            Assert.Greater(ps.currentHp, 40 + BattleFormula.WaveHealCap(200));
        }
    }
}
