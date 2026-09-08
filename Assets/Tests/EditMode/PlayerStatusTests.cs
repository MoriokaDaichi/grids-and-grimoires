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
