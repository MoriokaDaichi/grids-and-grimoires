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
    // いま装備している装備（枠→装備ID）。所有していても装備中とは限らない。
    private readonly Dictionary<EquipSlot, string> equipped = new Dictionary<EquipSlot, string>();
    private bool accessory2Unlocked;   // 2つ目のアクセサリー枠（トレーダーのタスク報酬で開放）
    private readonly HashSet<string> unlockedRecipes = new HashSet<string>();

    private static readonly EquipSlot[] AllSlots =
        { EquipSlot.Wand, EquipSlot.Armor, EquipSlot.Accessory1, EquipSlot.Accessory2 };

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
        ApplyEquippedGear();
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

    // ---------------------------------------------------------------- 装備（所有の中から枠ごとに1個を選ぶ）

    // 2つ目のアクセサリー枠が開放済みか。
    public bool Accessory2Unlocked { get { return accessory2Unlocked; } }

    // トレーダーのタスク報酬で2つ目のアクセサリー枠を開放する。
    public void UnlockAccessory2Slot()
    {
        if (accessory2Unlocked) return;
        accessory2Unlocked = true;
        Save();
        Debug.Log("[装備] アクセサリーの装備枠を1つ増設した。");
    }

    // 枠に装備中の装備ID（何も装備していなければ null）。
    public string EquippedId(EquipSlot slot)
    {
        string id;
        return equipped.TryGetValue(slot, out id) ? id : null;
    }

    public GearDef EquippedGear(EquipSlot slot) { return GearCatalog.Get(EquippedId(slot)); }

    public bool IsEquipped(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        foreach (string v in equipped.Values) if (v == id) return true;
        return false;
    }

    // そのカテゴリで所有している装備ID（弱→強、tier昇順）。装備選択UI用。
    public List<string> OwnedGearForSlot(GearSlot category)
    {
        List<string> outp = new List<string>();
        foreach (string id in craftedGear)
        {
            GearDef g = GearCatalog.Get(id);
            if (g != null && g.slot == category) outp.Add(id);
        }
        outp.Sort((a, b) =>
        {
            GearDef ga = GearCatalog.Get(a), gb = GearCatalog.Get(b);
            int ta = ga != null ? ga.tier : 0, tb = gb != null ? gb.tier : 0;
            return ta != tb ? ta.CompareTo(tb) : string.CompareOrdinal(a, b);
        });
        return outp;
    }

    // 枠に装備する。id=null なら外す。未開放枠／所有していない／カテゴリ不一致なら false。
    public bool Equip(EquipSlot slot, string id)
    {
        if (slot == EquipSlot.Accessory2 && !accessory2Unlocked) return false;

        if (string.IsNullOrEmpty(id))
        {
            if (EquippedId(slot) == null) return true; // 既に空
        }
        else
        {
            GearDef gear = GearCatalog.Get(id);
            if (gear == null || gear.slot != GearCatalog.CategoryOf(slot) || !HasGear(id)) return false;
            if (EquippedId(slot) == id) return true;
        }

        SwapEquip(slot, id);
        Save();
        Debug.Log("[装備] " + slot + " ← " + (string.IsNullOrEmpty(id) ? "（外す）" : GearCatalog.Get(id).name));
        return true;
    }

    public void Unequip(EquipSlot slot) { Equip(slot, null); }

    // 新しく手に入れた装備をどの枠に入れるか（製作・タスク付与時）。
    private EquipSlot DefaultEquipSlotFor(GearSlot category)
    {
        if (category == GearSlot.Wand) return EquipSlot.Wand;
        if (category == GearSlot.Armor) return EquipSlot.Armor;
        if (!equipped.ContainsKey(EquipSlot.Accessory1)) return EquipSlot.Accessory1;
        if (accessory2Unlocked && !equipped.ContainsKey(EquipSlot.Accessory2)) return EquipSlot.Accessory2;
        return EquipSlot.Accessory1; // 両枠が埋まっていれば1枠目を置換
    }

    // 旧装備のボーナスを外し、新装備のボーナスを付ける（Save はしない）。id 空なら外すだけ。
    private void SwapEquip(EquipSlot slot, string newId)
    {
        // 同じアクセを別のアクセ枠から移してくる場合は、旧枠の割り当て・ボーナスを先に外す
        if (!string.IsNullOrEmpty(newId) && GearCatalog.IsAccessorySlot(slot))
        {
            EquipSlot other = slot == EquipSlot.Accessory1 ? EquipSlot.Accessory2 : EquipSlot.Accessory1;
            string otherId;
            if (equipped.TryGetValue(other, out otherId) && otherId == newId)
            {
                GearDef od = GearCatalog.Get(otherId);
                if (gearApplied && playerStatus != null && od != null) playerStatus.ApplyGearDelta(od, -1);
                equipped.Remove(other);
            }
        }

        string cur;
        if (equipped.TryGetValue(slot, out cur) && cur != newId)
        {
            GearDef curDef = GearCatalog.Get(cur);
            if (gearApplied && playerStatus != null && curDef != null) playerStatus.ApplyGearDelta(curDef, -1);
        }

        if (string.IsNullOrEmpty(newId)) { equipped.Remove(slot); return; }

        equipped[slot] = newId;
        GearDef newDef = GearCatalog.Get(newId);
        if (gearApplied && playerStatus != null && newDef != null) playerStatus.ApplyGearDelta(newDef, +1);
    }

    public bool RecipeUnlocked(string id) { return !string.IsNullOrEmpty(id) && unlockedRecipes.Contains(id); }
    public IEnumerable<string> UnlockedRecipes { get { return unlockedRecipes; } }

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

        // 魔力炉を初めて建てたら初期燃料を注ぐ（cold-start の燃料デッドロック対策。再検証5 R2）。
        if (kind == FacilityKind.ManaFurnace && Level(kind) == 1)
            furnaceFuel = Math.Min(FuelCapacity, furnaceFuel + HideoutCatalog.FurnaceBuildBonusFuel);

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
        int level = Level(FacilityKind.AlchemyCauldron);

        if (part.materialType == MaterialType.ElementFragment)
        {
            // 欠片→エレメントの精製は錬金釜Lv3 以上、かつ精製単位（5個）以上を投入するときのみ（O6 対策）
            if (level < 3 || part.attribute == MagicAttribute.None) return false;
            if (times < HideoutRules.ElementRefinePerElement) return false;
        }
        else if (part.materialType == MaterialType.SpecialItem)
        {
            // 錬金釜のレベルで扱える tier に制限（Lv1:tier1 / Lv2:tier1〜3 / Lv3:tier1〜5）
            if (!HideoutRules.CanCauldronProcess(level, MonsterPartCatalog.TierOf(part.specialItemName))) return false;
        }
        else return false;

        if (!CanPowerFacilityAction()) return false;
        PlayerInventory inv = PlayerInventory.Instance;
        return inv != null && inv.CanAfford(new List<MaterialCost> { TransmuteInput(part, times) });
    }

    public bool Transmute(MaterialCost part, int times)
    {
        if (!CanTransmute(part, times)) return false;
        PlayerInventory inv = PlayerInventory.Instance;
        int level = Level(FacilityKind.AlchemyCauldron);

        if (!inv.TrySpend(new List<MaterialCost> { TransmuteInput(part, times) })) return false;

        List<MaterialCost> yield = HideoutRules.Transmute(part, times, HideoutCatalog.CauldronYieldMult(level), level);
        inv.Add(yield);
        ConsumeFacilityPower();
        Save();
        return true;
    }

    // 変換に投入する素材（モンスター素材 or 属性エレメントの欠片）を times 個ぶんの MaterialCost にする。
    private static MaterialCost TransmuteInput(MaterialCost part, int times)
    {
        return part.materialType == MaterialType.ElementFragment
            ? new MaterialCost { materialType = MaterialType.ElementFragment, attribute = part.attribute, amount = times }
            : new MaterialCost { materialType = MaterialType.SpecialItem, specialItemName = part.specialItemName, amount = times };
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
        if (gear.recipeGated && !RecipeUnlocked(gear.id)) return false;
        if (HasGear(gear.id)) return false;
        if (!CanPowerFacilityAction()) return false;
        PlayerInventory inv = PlayerInventory.Instance;
        return inv != null && inv.CanAfford(gear.cost);
    }

    // トレーダーのタスク報酬でレシピを解禁する。
    public void UnlockRecipe(string gearId)
    {
        if (string.IsNullOrEmpty(gearId)) return;
        if (!unlockedRecipes.Add(gearId)) return;
        Save();
        Debug.Log("[作業台] レシピ「" + gearId + "」を解禁した。");
    }

    // トレーダーのタスク報酬で完成品を直接付与する（コスト・電力チェックなし。Craft のボーナス反映部分と同じ）。
    public void GrantGear(string gearId)
    {
        GearDef gear = GearCatalog.Get(gearId);
        if (gear == null || HasGear(gear.id)) return;

        craftedGear.Add(gear.id);
        SwapEquip(DefaultEquipSlotFor(gear.slot), gear.id);   // 受け取った装備を装備する（同カテゴリの旧装備は所有のまま残る）
        Save();
        Debug.Log("[作業台] " + gear.name + " を受け取った（タスク報酬）。");
    }

    public bool Craft(GearDef gear)
    {
        if (!CanCraft(gear)) return false;
        PlayerInventory inv = PlayerInventory.Instance;
        if (!inv.TrySpend(gear.cost)) return false;

        // 製作した装備を所有に加え、そのまま装備する。同カテゴリの旧装備は所有のまま
        // 残り、キャラクター画面でいつでも付け替えられる。
        craftedGear.Add(gear.id);
        SwapEquip(DefaultEquipSlotFor(gear.slot), gear.id);

        ConsumeFacilityPower();
        Save();
        Debug.Log("[作業台] " + gear.name + " を製作した。");
        return true;
    }

    // 装備中の装備のボーナスを PlayerStatus に反映（起動時に1回。PlayerStatus はセーブされないため）。
    private void ApplyEquippedGear()
    {
        if (gearApplied || playerStatus == null) return;
        gearApplied = true;
        foreach (string id in equipped.Values)
        {
            GearDef g = GearCatalog.Get(id);
            if (g != null) playerStatus.ApplyGearDelta(g, +1);
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

        accessory2Unlocked = d.accessorySlot2Unlocked;

        // 装備中の枠を復元。equippedGearIds は Wand→Armor→Accessory1→Accessory2 の順で書かれた
        // フラットな所有IDリスト（枠が空ならスキップ）。空 or 旧セーブは所有品から移行
        // ＝旧仕様は「1カテゴリ1個・所有＝装備」だったので、所有品をそのまま装備する。
        equipped.Clear();
        List<string> equipSource = (d.equippedGearIds != null && d.equippedGearIds.Count > 0)
            ? d.equippedGearIds
            : new List<string>(craftedGear);
        List<string> accIds = new List<string>();
        foreach (string id in equipSource)
        {
            GearDef g = GearCatalog.Get(id);
            if (g == null || !craftedGear.Contains(id)) continue;
            if (g.slot == GearSlot.Wand) { if (!equipped.ContainsKey(EquipSlot.Wand)) equipped[EquipSlot.Wand] = id; }
            else if (g.slot == GearSlot.Armor) { if (!equipped.ContainsKey(EquipSlot.Armor)) equipped[EquipSlot.Armor] = id; }
            else accIds.Add(id);
        }
        if (accIds.Count > 0) equipped[EquipSlot.Accessory1] = accIds[0];
        if (accIds.Count > 1 && accessory2Unlocked) equipped[EquipSlot.Accessory2] = accIds[1];

        unlockedRecipes.Clear();
        if (d.unlockedGearRecipes != null)
            foreach (string r in d.unlockedGearRecipes) if (!string.IsNullOrEmpty(r)) unlockedRecipes.Add(r);
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
        d.equippedGearIds = new List<string>();
        foreach (EquipSlot s in AllSlots)   // Wand→Armor→Accessory1→Accessory2 の順で並べる
        {
            string id;
            if (equipped.TryGetValue(s, out id) && !string.IsNullOrEmpty(id)) d.equippedGearIds.Add(id);
        }
        d.accessorySlot2Unlocked = accessory2Unlocked;
        d.unlockedGearRecipes = new List<string>(unlockedRecipes);

        SaveManager.Save(d);
        OnHideoutChanged?.Invoke();
    }
}
