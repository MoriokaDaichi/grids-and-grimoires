using System;
using System.Collections.Generic;
using UnityEngine;

// ハイドアウトの設備（建造・強化・稼働）を管理する。シーンにシングルトンで1つ。
// 起動時にセーブから設備レベル・魔力炉の燃料・マジックサークルの進行・製作済み装備を読み込み、
// 変更のたびに「Load → 自領域だけ更新 → Save」で保存する。
//
// エネルギー: 魔力炉(ManaFurnace)以外の設備アクション（建造/強化/研究割当/製作/変換/捧げ/受取）は
// 魔力炉が Lv1 以上かつ燃料があるときのみ実行できる。魔力炉自体の建造/強化は電力不要。
public class HideoutManager : MonoBehaviour
{
    public static HideoutManager Instance { get; private set; }

    public Action OnHideoutChanged;

    private readonly Dictionary<FacilityKind, int> levels = new Dictionary<FacilityKind, int>();
    private long furnaceFuel;
    private readonly List<BrewRecord> brews = new List<BrewRecord>();
    private readonly HashSet<string> craftedGear = new HashSet<string>();

    private PlayerStatus playerStatus;
    private bool gearApplied;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Load();
    }

    void Start()
    {
        playerStatus = UnityEngine.Object.FindFirstObjectByType<PlayerStatus>();
        ApplyOwnedGear();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- 参照

    public int Level(FacilityKind kind)
    {
        int v;
        return levels.TryGetValue(kind, out v) ? v : 0;
    }

    public bool IsBuilt(FacilityKind kind) { return Level(kind) >= 1; }

    public int FurnaceLevel { get { return Level(FacilityKind.ManaFurnace); } }
    public long Fuel { get { return furnaceFuel; } }
    public long FuelCapacity { get { return HideoutRules.FurnaceCapacity(FurnaceLevel); } }

    public bool ResearchUnlocked { get { return IsBuilt(FacilityKind.ResearchDesk); } }
    public float ResearchCostMult { get { return HideoutCatalog.ResearchCostMult(Level(FacilityKind.ResearchDesk)); } }
    public float ResearchBonusMult { get { return HideoutCatalog.ResearchBonusMult(Level(FacilityKind.ResearchDesk)); } }

    public IReadOnlyList<BrewRecord> Brews { get { return brews; } }
    public bool HasGear(string id) { return craftedGear.Contains(id); }
    public IEnumerable<string> OwnedGear { get { return craftedGear; } }

    // ---------------------------------------------------------------- 電力

    // 魔力炉以外の設備アクションが今すぐ実行できるか（副作用なし）。
    public bool CanPowerFacilityAction()
    {
        return HideoutRules.CanPowerAction(FurnaceLevel, furnaceFuel);
    }

    // 実際に燃料を1アクション分消費する。呼び出し側が事前に CanPowerFacilityAction を確認していること。
    private void ConsumeFacilityPower()
    {
        furnaceFuel = Math.Max(0, furnaceFuel - HideoutCatalog.FurnaceFuelPerAction(FurnaceLevel));
    }

    // 魔力炉に魔力結晶を1つ投入して燃料に変える（インベントリから消費）。
    public bool LoadFuel(MaterialType crystal)
    {
        if (!IsBuilt(FacilityKind.ManaFurnace)) return false;
        int value = MaterialCatalog.Value(crystal);
        if (value <= 0) return false; // 結晶以外は不可
        if (furnaceFuel >= FuelCapacity) return false;

        PlayerInventory inv = PlayerInventory.Instance;
        MaterialCost one = new MaterialCost { materialType = crystal, amount = 1 };
        if (inv == null || !inv.TrySpend(new List<MaterialCost> { one })) return false;

        furnaceFuel = Math.Min(FuelCapacity, furnaceFuel + value);
        Save();
        return true;
    }

    // ---------------------------------------------------------------- 建造・強化

    public List<MaterialCost> NextCost(FacilityKind kind)
    {
        return HideoutRules.NextCost(HideoutCatalog.Get(kind), Level(kind));
    }

    public bool CanAdvance(FacilityKind kind)
    {
        List<MaterialCost> cost = NextCost(kind);
        if (cost == null) return false;

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null || !inv.CanAfford(cost)) return false;

        // 魔力炉自体は電力不要。それ以外は魔力炉の稼働が必要。
        if (kind != FacilityKind.ManaFurnace && !CanPowerFacilityAction()) return false;
        return true;
    }

    public bool Advance(FacilityKind kind)
    {
        if (!CanAdvance(kind)) return false;
        List<MaterialCost> cost = NextCost(kind);

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null || !inv.TrySpend(cost)) return false;

        if (kind != FacilityKind.ManaFurnace) ConsumeFacilityPower();
        levels[kind] = Level(kind) + 1;
        Save();
        Debug.Log("[ハイドアウト] " + HideoutCatalog.Get(kind).name + " を Lv" + Level(kind) + " にした。");
        return true;
    }

    // ---------------------------------------------------------------- 研究机フック（ResearchManager から呼ぶ）

    // 研究コストを机レベルでスケールする（未建造なら等倍）。
    public List<MaterialCost> ScaleResearchCost(IEnumerable<MaterialCost> baseCost)
    {
        if (!ResearchUnlocked) return new List<MaterialCost>(baseCost ?? new List<MaterialCost>());
        return HideoutRules.ScaleCost(baseCost, ResearchCostMult);
    }

    // 研究の割り当て1回ぶんの電力を消費する。電力不足なら false（呼び出し側は割当を中止）。
    public bool TryConsumeResearchPower()
    {
        if (!CanPowerFacilityAction()) return false;
        ConsumeFacilityPower();
        Save();
        return true;
    }

    // ---------------------------------------------------------------- 錬金釜

    // 錬金釜が変換できるモンスター素材の tier 上限（0 = 未建造）。
    public int CauldronMaxTier { get { return HideoutCatalog.CauldronMaxTier(Level(FacilityKind.AlchemyCauldron)); } }

    public bool CanTransmute(MaterialCost part, int times)
    {
        if (!IsBuilt(FacilityKind.AlchemyCauldron) || part == null || times <= 0) return false;
        if (part.materialType != MaterialType.SpecialItem) return false;
        // 錬金釜のレベルで扱える tier に制限（Lv1:tier1 / Lv2:tier1〜3 / Lv3:tier1〜5）
        if (!HideoutRules.CanCauldronProcess(Level(FacilityKind.AlchemyCauldron), MonsterPartCatalog.TierOf(part.specialItemName))) return false;
        if (!CanPowerFacilityAction()) return false;
        PlayerInventory inv = PlayerInventory.Instance;
        MaterialCost need = new MaterialCost { materialType = MaterialType.SpecialItem, specialItemName = part.specialItemName, amount = times };
        return inv != null && inv.CanAfford(new List<MaterialCost> { need });
    }

    public bool Transmute(MaterialCost part, int times)
    {
        if (!CanTransmute(part, times)) return false;
        PlayerInventory inv = PlayerInventory.Instance;

        MaterialCost need = new MaterialCost { materialType = MaterialType.SpecialItem, specialItemName = part.specialItemName, amount = times };
        if (!inv.TrySpend(new List<MaterialCost> { need })) return false;

        List<MaterialCost> yield = HideoutRules.Transmute(part, times, HideoutCatalog.CauldronYieldMult(Level(FacilityKind.AlchemyCauldron)));
        inv.Add(yield);
        ConsumeFacilityPower();
        Save();
        return true;
    }

    // ---------------------------------------------------------------- マジックサークル

    public bool CanSacrifice(MaterialCost item)
    {
        if (!IsBuilt(FacilityKind.MagicCircle) || item == null) return false;
        if (!CanPowerFacilityAction()) return false;
        PlayerInventory inv = PlayerInventory.Instance;
        MaterialCost one = Clone(item, 1);
        return inv != null && inv.CanAfford(new List<MaterialCost> { one });
    }

    public bool Sacrifice(MaterialCost item)
    {
        if (!CanSacrifice(item)) return false;
        PlayerInventory inv = PlayerInventory.Instance;
        MaterialCost one = Clone(item, 1);
        if (!inv.TrySpend(new List<MaterialCost> { one })) return false;

        ItemRarity r = HideoutRules.RarityOf(item);
        int lvl = Level(FacilityKind.MagicCircle);
        brews.Add(new BrewRecord
        {
            startUnixSeconds = NowUnix(),
            hours = HideoutRules.BrewHours(r, lvl),
            inputRarity = (int)r,
            inputLabel = MaterialCatalog.DisplayName(item),
        });
        ConsumeFacilityPower();
        Save();
        return true;
    }

    public bool BrewReady(int index)
    {
        if (index < 0 || index >= brews.Count) return false;
        BrewRecord b = brews[index];
        return HideoutRules.BrewReady(b.startUnixSeconds, b.hours, NowUnix());
    }

    public double BrewRemainingSeconds(int index)
    {
        if (index < 0 || index >= brews.Count) return 0;
        BrewRecord b = brews[index];
        return HideoutRules.BrewRemainingSeconds(b.startUnixSeconds, b.hours, NowUnix());
    }

    // 完成した捧げものを受け取る。受取不可なら false。
    public bool ClaimBrew(int index, out List<MaterialCost> reward)
    {
        reward = null;
        if (!BrewReady(index)) return false;

        BrewRecord b = brews[index];
        int bonus = HideoutCatalog.CircleRarityBonus(Level(FacilityKind.MagicCircle));
        int seed = unchecked((int)(b.startUnixSeconds) ^ (b.inputRarity * 92821) ^ (index * 40503));
        ItemRarity outRarity = HideoutRules.RollRarity(seed, (ItemRarity)b.inputRarity, bonus);
        MaterialCost result = HideoutRules.CircleReward(outRarity, seed);

        reward = new List<MaterialCost> { result };
        PlayerInventory inv = PlayerInventory.Instance;
        if (inv != null) inv.Add(reward);

        brews.RemoveAt(index);
        Save();
        Debug.Log("[マジックサークル] " + b.inputLabel + " → " + MaterialCatalog.DisplayName(result) + " ×" + result.amount + "（" + outRarity + "）");
        return true;
    }

    // ---------------------------------------------------------------- 作業台

    public bool CanCraft(GearDef gear)
    {
        if (gear == null) return false;
        if (gear.tier > Level(FacilityKind.Workbench)) return false;
        if (HasGear(gear.id)) return false;
        if (!CanPowerFacilityAction()) return false;
        PlayerInventory inv = PlayerInventory.Instance;
        return inv != null && inv.CanAfford(gear.cost);
    }

    public bool Craft(GearDef gear)
    {
        if (!CanCraft(gear)) return false;
        PlayerInventory inv = PlayerInventory.Instance;
        if (!inv.TrySpend(gear.cost)) return false;

        // 同じスロットの旧装備は置き換え（ボーナスを外す）
        List<string> toRemove = new List<string>();
        foreach (string ownedId in craftedGear)
        {
            GearDef o = GearCatalog.Get(ownedId);
            if (o != null && o.slot == gear.slot) toRemove.Add(ownedId);
        }
        foreach (string id in toRemove)
        {
            craftedGear.Remove(id);
            GearDef o = GearCatalog.Get(id);
            if (o != null && playerStatus != null) playerStatus.ApplyResearchDelta(o.stat, -o.amount);
        }

        craftedGear.Add(gear.id);
        if (playerStatus != null) playerStatus.ApplyResearchDelta(gear.stat, gear.amount);

        ConsumeFacilityPower();
        Save();
        Debug.Log("[作業台] " + gear.name + " を製作した。");
        return true;
    }

    // 所持している装備のボーナスを PlayerStatus に反映（起動時に1回。PlayerStatus はセーブされないため）。
    private void ApplyOwnedGear()
    {
        if (gearApplied || playerStatus == null) return;
        gearApplied = true;
        foreach (string id in craftedGear)
        {
            GearDef g = GearCatalog.Get(id);
            if (g != null) playerStatus.ApplyResearchDelta(g.stat, g.amount);
        }
    }

    // ---------------------------------------------------------------- 永続化

    private static double NowUnix()
    {
        return (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    }

    private static MaterialCost Clone(MaterialCost c, int amount)
    {
        return new MaterialCost
        {
            materialType = c.materialType,
            attribute = c.attribute,
            specialItemName = c.specialItemName,
            amount = amount,
        };
    }

    private void Load()
    {
        SaveData d = SaveManager.Load();

        levels.Clear();
        if (d.facilityLevels != null)
        {
            foreach (StringIntPair p in d.facilityLevels)
            {
                FacilityKind k;
                if (!string.IsNullOrEmpty(p.key) && Enum.TryParse(p.key, out k))
                    levels[k] = Mathf.Clamp(p.value, 0, HideoutCatalog.MaxLevel);
            }
        }
        furnaceFuel = d.furnaceFuel;
        brews.Clear();
        if (d.magicCircleBrews != null) brews.AddRange(d.magicCircleBrews);
        craftedGear.Clear();
        if (d.craftedGearIds != null)
            foreach (string g in d.craftedGearIds) if (!string.IsNullOrEmpty(g)) craftedGear.Add(g);
    }

    private void Save()
    {
        SaveData d = SaveManager.Load();

        d.facilityLevels = new List<StringIntPair>();
        foreach (KeyValuePair<FacilityKind, int> kv in levels)
            d.facilityLevels.Add(new StringIntPair { key = kv.Key.ToString(), value = kv.Value });
        d.furnaceFuel = furnaceFuel;
        d.magicCircleBrews = new List<BrewRecord>(brews);
        d.craftedGearIds = new List<string>(craftedGear);

        SaveManager.Save(d);
        OnHideoutChanged?.Invoke();
    }
}
