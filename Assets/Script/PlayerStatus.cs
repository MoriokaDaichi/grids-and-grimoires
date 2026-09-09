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

    // 研究スキルツリーの特性ノード（大ノード）による恒久効果。ResearchManager が
    // 割当時／起動時に加算する（研究ノードは外せないので減算は無い）。検証レポート 2026-09-10 O1。
    // 防御特性（被弾は TakeDamage で 固定→割合→不屈→バリア吸収 の順に適用）:
    public float flatDamageReduction;      // 被弾を常にこの固定量ぶん軽減（Def の素引きに上乗せ）
    public float percentDamageReduction;   // 被弾を割合軽減（0..0.4）
    public float lastStandReduction;       // 現在HP ≤ 最大HP×LastStandThreshold のとき割合軽減（0..0.5）
    public float waveBarrierPercent;       // 各ウェーブ開始時、最大HP×この割合のバリアを張る（0..0.4）
    public float thornsPercent;            // 被弾時、敵の一撃×この割合を攻撃者へ反射（0..0.6。BattleManager が参照）
    private int barrierCurrent;            // 現ウェーブの残バリア
    private const float LastStandThreshold = 0.35f;
    public int BarrierCurrent { get { return barrierCurrent; } }
    // 攻撃特性（BattleManager が攻撃魔法の発動時に参照。数値は全て仮）:
    public float spellPowerPercent;        // 攻撃魔法ダメージ% 加算（damagePercent へ直接足す）
    public float critMultBonus;            // 会心倍率への加算（基礎 1.5 に上乗せ。0..0.5）
    public float castHastePercent;         // 発動間隔の短縮率（0..0.4）
    public int   armorPierce;              // 攻撃魔法が無視する敵防御力
    public float executeBonusPercent;      // HP25%以下の敵へのダメージ% 加算（0..0.6）

    // 現ウェーブでこれまでに受けた累計ダメージ（バースト即死クランプ用。レポート C2/D5）。
    private int damageThisWave;
    // ウェーブ開始時の HP 割合。クランプは「開始時に十分健康だったウェーブ」だけに効かせる
    // （＝満タンからの即死よけであって、削れた run を延命する生存バフではない。再検証4 R1）。
    private float waveStartHpFraction = 1f;
    // 現ウェーブで自然回復（hpRegenPerSecond）で戻した累計（上限は BattleFormula.WaveHealCap。再検証7 R4）。
    private int regenHealedThisWave;

    // 手動振り分け（+ボタン）の累計上限。研究(ApplyResearchDelta)・装備(ApplyGearDelta)の恒久ボーナスとは
    // 独立にカウントする。合計値で判定すると深部ランでは研究ぶんだけで上限に達し、以降タスク報酬で入る
    // ステP（`rewardStatPoints`／大結晶→stP 交換）の HP/Atk 割り当て分が statsPoint を消費しないまま
    // 加算もされず無言で死蔵していた（検証レポート 2026-09-10 N1）。
    private const int MANUAL_HP_MAX = 3000;    // HP は +10/pt なので手動 300pt ぶん
    private const int MANUAL_OTHER_MAX = 300;  // Atk/Def/Spd/Luc は +1/pt

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
        barrierCurrent = 0;
        OnStatusChanged?.Invoke();
        OnManaChanged?.Invoke();
    }

    // 新しいウェーブの開始時に DungeonManager が呼ぶ。バースト即死クランプ・自然回復上限の累計をリセットし、
    // 「聖盾」バリアを張り直す。
    public void BeginWave()
    {
        damageThisWave = 0;
        regenHealedThisWave = 0;
        waveStartHpFraction = hp > 0 ? (float)currentHp / hp : 0f;
        barrierCurrent = waveBarrierPercent > 0f ? Mathf.RoundToInt(hp * waveBarrierPercent) : 0;
        if (barrierCurrent > 0) OnStatusChanged?.Invoke();
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

        // 研究の防御特性による被弾軽減（O1 対策）。固定軽減 → 割合軽減 → 不屈（低HP時）→ 聖盾バリア吸収。
        if (flatDamageReduction > 0f)
            amount = Mathf.Max(0, amount - Mathf.RoundToInt(flatDamageReduction));
        if (percentDamageReduction > 0f)
            amount = Mathf.RoundToInt(amount * (1f - percentDamageReduction));
        if (lastStandReduction > 0f && hp > 0 && currentHp <= hp * LastStandThreshold)
            amount = Mathf.RoundToInt(amount * (1f - lastStandReduction));
        if (barrierCurrent > 0 && amount > 0)
        {
            int absorbed = Mathf.Min(barrierCurrent, amount);
            barrierCurrent -= absorbed;
            amount -= absorbed;
            OnStatusChanged?.Invoke();
        }
        if (amount <= 0) return;

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

    // 研究スキルツリーの特性ノード（大ノード）による恒久効果。ResearchManager が割り当て時／起動時に呼ぶ。
    // amount は「表示値」＝割合系は %ポイント（10 → 10%）。研究ノードは外せないので加算のみ。
    public void ApplyResearchPerk(ResearchPerk perk, float amount)
    {
        switch (perk)
        {
            // 防御特性
            case ResearchPerk.Thorns:      thornsPercent = Mathf.Min(0.6f, thornsPercent + amount / 100f); break;
            case ResearchPerk.FlatWard:    flatDamageReduction += amount; break;
            case ResearchPerk.PercentWard: percentDamageReduction = Mathf.Min(0.4f, percentDamageReduction + amount / 100f); break;
            case ResearchPerk.WaveBarrier: waveBarrierPercent = Mathf.Min(0.4f, waveBarrierPercent + amount / 100f); break;
            case ResearchPerk.LastStand:   lastStandReduction = Mathf.Min(0.5f, lastStandReduction + amount / 100f); break;
            // 攻撃特性
            case ResearchPerk.SpellPower:  spellPowerPercent += amount; break;
            case ResearchPerk.CritPower:   critMultBonus = Mathf.Min(0.5f, critMultBonus + amount / 100f); break;
            case ResearchPerk.CastHaste:   castHastePercent = Mathf.Min(0.4f, castHastePercent + amount / 100f); break;
            case ResearchPerk.ArmorPierce: armorPierce += Mathf.RoundToInt(amount); break;
            case ResearchPerk.Execute:     executeBonusPercent = Mathf.Min(0.6f, executeBonusPercent + amount / 100f); break;
        }
        OnStatusChanged?.Invoke();
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

    // 手動振り分けで各ステータスをまだ伸ばせるか（研究・装備の恒久ボーナスは判定に含めない）。
    // ステ振りUI のボタン活性判定にも使う。
    public bool CanAddStat(string type)
    {
        if (statsPoint <= 0) return false;
        switch (type)
        {
            case "HP":  return manualHp  < MANUAL_HP_MAX;
            case "Atk": return manualAtk < MANUAL_OTHER_MAX;
            case "Def": return manualDef < MANUAL_OTHER_MAX;
            case "Spd": return manualSpd < MANUAL_OTHER_MAX;
            case "Luc": return manualLuc < MANUAL_OTHER_MAX;
            default:    return false;
        }
    }

    public void AddStat(string type)
    {
        if (!CanAddStat(type)) return; // 上限到達・ポイント切れ・未知typeでは statsPoint を消費しない

        switch (type)
        {
            case "HP":  hp += 10; currentHp += 10; manualHp += 10; break;
            case "Atk": atk += 1; manualAtk += 1; break;
            case "Def": def += 1; manualDef += 1; break;
            case "Spd": spd += 1; manualSpd += 1; break;
            case "Luc": luc += 1; manualLuc += 1; break;
        }
        statsPoint--;

        Persist();

        // UIを更新するように通知
        OnStatusChanged?.Invoke();
    }
}