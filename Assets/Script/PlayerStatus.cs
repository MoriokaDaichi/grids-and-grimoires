using UnityEngine;
using UnityEngine.UI;
using System;

public class PlayerStatus : MonoBehaviour
{
    // 現在のステータス値
    public int hp = 100;
    public int atk = 5;
    public int def = 5;
    public int spd = 5;
    public int luc = 5;

    // 残りステータスポイント
    public int statsPoint = 5;

    // ＋ボタンで手動振り分けした累計量（研究/装備ぶんは含めない）。セーブ対象。
    private int manualHp, manualAtk, manualDef, manualSpd, manualLuc;
    // 起動時の復元が終わるまで Persist を抑止する（Start 前の他システムからの呼び出し対策）。
    private bool allocationsRestored;

    // バトル中の現在HP（hpは振り分けで決まる最大値、こちらは戦闘中に増減する値）
    public int currentHp { get; private set; }

    // マナ（魔力）。魔法を発動するたびに消費し、戦闘中は自然回復する。数値は全て仮（ManaRules）。
    public int maxMana = ManaRules.BaseMaxMana;
    public float manaRegenPerSecond = ManaRules.DefaultRegenPerSecond;
    public float currentMana { get; private set; }

    // サステイン系アクセサリ（GearDef）による恒久ボーナス。装備の着脱で HideoutManager が増減させる。
    public float hpRegenPerSecond;       // 戦闘中、毎秒この量ずつ現在HPを回復
    public float healPerWaveFlat;        // ウェーブ突破時に固定量ずつ現在HPを回復
    public float healPerWavePercent;     // ウェーブ突破時に最大HPのこの割合ぶん回復（0..1）
    private float hpRegenCarry;           // 毎秒回復の端数（1未満を持ち越す）

    // 現ウェーブでこれまでに受けた累計ダメージ（バースト即死クランプ用。レポート C2/D5）。
    private int damageThisWave;
    // ウェーブ開始時の HP 割合。クランプは「開始時に十分健康だったウェーブ」だけに効かせる
    // （＝満タンからの即死よけであって、削れた run を延命する生存バフではない。再検証4 R1）。
    private float waveStartHpFraction = 1f;
    // 現ウェーブで自然回復（hpRegenPerSecond）で戻した累計（上限は BattleFormula.WaveHealCap。再検証7 R4）。
    private int regenHealedThisWave;

    // 上限値
    private const int HP_MAX = 1000;
    private const int OTHER_MAX = 100;

    // UI更新用のイベント（UI側に通知するため）
    public Action OnStatusChanged;
    public Action OnDefeated;
    public Action<int, int> OnDamaged; // (被ダメージ量, 残りcurrentHp)
    public Action<int, int> OnHealed;  // (回復量, 残りcurrentHp) サステイン系アクセサリの回復
    public Action OnManaChanged;       // マナが増減した

    void Awake()
    {
        currentHp = hp;
        currentMana = maxMana;
    }

    // セーブから手動振り分けを復元する。研究(ResearchManager)・装備(HideoutManager)の
    // 恒久ボーナスは各自の Start で別途 ApplyResearchDelta されるため、ここでは触れない。
    // 加算は全て可換なので、それらの Start と本 Start の実行順は問わない。
    void Start()
    {
        PlayerStatAllocation a = PlayerStatSave.Read(SaveManager.Load());
        allocationsRestored = true;
        if (!a.saved) return;

        manualHp = a.hp; manualAtk = a.atk; manualDef = a.def; manualSpd = a.spd; manualLuc = a.luc;
        hp += manualHp;
        atk += manualAtk;
        def += manualDef;
        spd += manualSpd;
        luc += manualLuc;
        statsPoint = a.statsPoint;
        currentHp = hp; // 構築画面。戦闘突入時は BattleReset で改めて全回復する。

        OnStatusChanged?.Invoke();
    }

    // 手動振り分けの領域だけを SaveData に書き戻す（他システムのフィールドは保つ）。
    private void Persist()
    {
        if (!allocationsRestored) return; // Start で復元し切る前は書かない
        SaveData d = SaveManager.Load();
        PlayerStatSave.Write(d, new PlayerStatAllocation
        {
            statsPoint = statsPoint,
            hp = manualHp, atk = manualAtk, def = manualDef, spd = manualSpd, luc = manualLuc,
        });
        SaveManager.Save(d);
    }

    // ダンジョン突入時など、戦闘開始時にHP・マナを全回復してリセットする
    public void BattleReset()
    {
        currentHp = hp;
        currentMana = maxMana;
        hpRegenCarry = 0f;
        damageThisWave = 0;
        regenHealedThisWave = 0;
        OnStatusChanged?.Invoke();
        OnManaChanged?.Invoke();
    }

    // 新しいウェーブの開始時に DungeonManager が呼ぶ。バースト即死クランプ・自然回復上限の累計をリセットする。
    public void BeginWave()
    {
        damageThisWave = 0;
        regenHealedThisWave = 0;
        waveStartHpFraction = hp > 0 ? (float)currentHp / hp : 0f;
    }

    // 発動に必要なマナがあるか
    public bool HasMana(int cost)
    {
        return currentMana >= cost;
    }

    // マナを消費する。足りなければ false（消費しない）。
    public bool SpendMana(int cost)
    {
        if (cost <= 0) return true;
        if (currentMana < cost) return false;

        currentMana -= cost;
        OnManaChanged?.Invoke();
        OnStatusChanged?.Invoke();
        return true;
    }

    // 戦闘中の自然回復。BattleManager が毎フレーム呼ぶ。
    public void RegenMana(float deltaTime)
    {
        if (currentMana >= maxMana) return;

        currentMana = Mathf.Min(maxMana, currentMana + ManaRules.RegenAmount(manaRegenPerSecond, deltaTime));
        OnManaChanged?.Invoke();
    }

    // 戦闘中の HP 自然回復（サステイン系アクセサリ）。BattleManager が毎フレーム呼ぶ。
    // currentHp は int なので 1 未満は hpRegenCarry に持ち越す。
    // 再検証7 R4：深部の長いウェーブで毎秒回復が被弾を上回りフェイルステートが消えるため、
    // 1ウェーブで戻せる自然回復量を最大HPの WaveHealCapFraction までに制限する
    // （ウェーブ突破時の HealWaveTick はこの上限の対象外＝クリア報酬として別枠）。
    public void RegenHealth(float deltaTime)
    {
        if (hpRegenPerSecond <= 0f || deltaTime <= 0f) return;
        if (currentHp <= 0 || currentHp >= hp) { hpRegenCarry = 0f; return; }
        if (regenHealedThisWave >= BattleFormula.WaveHealCap(hp)) return;

        hpRegenCarry += hpRegenPerSecond * deltaTime;
        int whole = Mathf.FloorToInt(hpRegenCarry);
        if (whole <= 0) return;
        hpRegenCarry -= whole;

        whole = Mathf.Min(whole, BattleFormula.WaveHealCap(hp) - regenHealedThisWave);
        if (whole <= 0) return;

        int before = currentHp;
        currentHp = Mathf.Min(hp, currentHp + whole);
        int healed = currentHp - before;
        if (healed <= 0) return;
        regenHealedThisWave += healed;
        OnStatusChanged?.Invoke();
        OnHealed?.Invoke(healed, currentHp);
    }

    // ウェーブ突破時の HP 回復（サステイン系アクセサリ）。DungeonManager が呼ぶ。
    public void HealWaveTick()
    {
        if (currentHp <= 0 || currentHp >= hp) return;
        int amount = Mathf.RoundToInt(healPerWaveFlat + hp * healPerWavePercent);
        if (amount <= 0) return;

        int before = currentHp;
        currentHp = Mathf.Min(hp, currentHp + amount);
        int healed = currentHp - before;
        if (healed <= 0) return;
        OnStatusChanged?.Invoke();
        OnHealed?.Invoke(healed, currentHp);
    }

    public void TakeDamage(int amount)
    {
        if (currentHp <= 0 || amount <= 0) return;

        // バースト即死クランプ（レポート C2/D5、調整 再検証4 R1）：ウェーブ開始時に十分健康だった
        // （HP割合 ≥ WaveClampMinStartFraction）ウェーブに限り、1ウェーブで最大HPの WaveDamageCapFraction
        // を超えるぶんの被弾を無効化する。「脱出」はウェーブ間でしか選べないので満タン近くからの1ウェーブ
        // 即死を防ぐのが目的。既に削れている run はこのクランプで延命しない（attrition で普通に死ねる）。
        int applied = amount;
        if (waveStartHpFraction >= BattleFormula.WaveClampMinStartFraction)
        {
            int allowed = Mathf.Max(0, BattleFormula.WaveDamageCap(hp) - damageThisWave);
            applied = Mathf.Min(amount, allowed);
        }
        damageThisWave += applied;

        currentHp = Mathf.Max(0, currentHp - applied);
        OnStatusChanged?.Invoke();
        OnDamaged?.Invoke(applied, currentHp);

        if (currentHp <= 0) OnDefeated?.Invoke();
    }

    // トレード等でステータスポイントを増やす
    public void AddStatsPoint(int amount)
    {
        if (amount <= 0) return;
        statsPoint += amount;
        Persist();
        OnStatusChanged?.Invoke();
    }

    // 研究スキルツリーの小ノードによる恒久ボーナス。ResearchManager が割り当て時／起動時に呼ぶ。
    public void ApplyResearchDelta(ResearchStat stat, float amount)
    {
        switch (stat)
        {
            case ResearchStat.Hp:
                hp += Mathf.RoundToInt(amount);
                currentHp += Mathf.RoundToInt(amount);
                break;
            case ResearchStat.Atk: atk += Mathf.RoundToInt(amount); break;
            case ResearchStat.Def: def += Mathf.RoundToInt(amount); break;
            case ResearchStat.Spd: spd += Mathf.RoundToInt(amount); break;
            case ResearchStat.Luc: luc += Mathf.RoundToInt(amount); break;
            case ResearchStat.ManaMax:
                maxMana += Mathf.RoundToInt(amount);
                currentMana = Mathf.Min(maxMana, currentMana + amount);
                break;
            case ResearchStat.ManaRegen:
                manaRegenPerSecond += amount;
                break;
        }
        OnStatusChanged?.Invoke();
        OnManaChanged?.Invoke();
    }

    // 製作装備（GearDef）の恒久ボーナス。HideoutManager が着脱時／起動時に sign=+1/-1 で呼ぶ。
    // 主ステータス（stat/amount）に加え、サステイン系の副効果も増減させる。
    public void ApplyGearDelta(GearDef gear, int sign)
    {
        if (gear == null || sign == 0) return;

        if (gear.amount != 0f) ApplyResearchDelta(gear.stat, gear.amount * sign);

        hpRegenPerSecond = Mathf.Max(0f, hpRegenPerSecond + gear.hpRegenPerSecond * sign);
        healPerWaveFlat = Mathf.Max(0f, healPerWaveFlat + gear.healPerWaveFlat * sign);
        healPerWavePercent = Mathf.Max(0f, healPerWavePercent + gear.healPerWavePercent * sign);

        OnStatusChanged?.Invoke();
    }

    public void AddStat(string type)
    {
        if (statsPoint <= 0) return;

        switch (type)
        {
            case "HP":
                if (hp < HP_MAX) { hp += 10; currentHp += 10; manualHp += 10; statsPoint--; }
                break;
            case "Atk":
                if (atk < OTHER_MAX) { atk += 1; manualAtk += 1; statsPoint--; }
                break;
            case "Def":
                if (def < OTHER_MAX) { def += 1; manualDef += 1; statsPoint--; }
                break;
            case "Spd":
                if (spd < OTHER_MAX) { spd += 1; manualSpd += 1; statsPoint--; }
                break;
            case "Luc":
                if (luc < OTHER_MAX) { luc += 1; manualLuc += 1; statsPoint--; }
                break;
        }

        Persist();

        // UIを更新するように通知
        OnStatusChanged?.Invoke();
    }
}