using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ハイドアウトのハブ画面。5設備の建造/強化と、各設備の機能（研究・製作・変換・捧げ・給電）を
// 1つの縦スクロールに動的生成する。行や見出しはコードで組み立て（プレハブ不要）。
// ロジックは HideoutManager / HideoutCatalog / HideoutRules / GearCatalog に委譲する。
public class HideoutHubPanel : MonoBehaviour
{
    [SerializeField] private RectTransform listRoot;   // VerticalLayoutGroup + ContentSizeFitter
    [SerializeField] private TMP_Text fuelText;
    [SerializeField] private Image fuelFill;
    [SerializeField] private Button researchButton;    // 「研究する」→ スキルツリーへ
    [SerializeField] private TMP_Text researchButtonLabel;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_FontAsset font;

    private HideoutManager hideout;
    private PlayerInventory inventory;
    private GamePhaseManager phase;

    private static readonly Color CardBg = new Color(0f, 0f, 0f, 0.32f);
    private static readonly Color OkBtn = new Color(0.26f, 0.46f, 0.34f, 1f);
    private static readonly Color GoBtn = new Color(0.25f, 0.4f, 0.7f, 1f);
    private static readonly Color DimBtn = new Color(0.3f, 0.3f, 0.36f, 1f);

    void Awake()
    {
        hideout = UnityEngine.Object.FindFirstObjectByType<HideoutManager>();
        inventory = UnityEngine.Object.FindFirstObjectByType<PlayerInventory>();
        phase = UnityEngine.Object.FindFirstObjectByType<GamePhaseManager>();
        if (closeButton != null) closeButton.onClick.AddListener(() => { if (phase != null) phase.ReturnToBuild(); });
        if (researchButton != null) researchButton.onClick.AddListener(() => { if (phase != null) phase.GoToResearch(); });
    }

    void OnEnable()
    {
        if (hideout != null) hideout.OnHideoutChanged += Rebuild;
        if (inventory != null) inventory.OnInventoryChanged += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        if (hideout != null) hideout.OnHideoutChanged -= Rebuild;
        if (inventory != null) inventory.OnInventoryChanged -= Rebuild;
    }

    // ---------------------------------------------------------------- 生成

    private void Rebuild()
    {
        if (listRoot == null || hideout == null) return;

        for (int i = listRoot.childCount - 1; i >= 0; i--)
            Destroy(listRoot.GetChild(i).gameObject);

        RefreshFuelBar();

        bool researchReady = hideout.ResearchUnlocked;
        if (researchButton != null) researchButton.interactable = researchReady;
        if (researchButtonLabel != null)
            researchButtonLabel.text = researchReady ? "研究する" : "研究机が必要";

        // enum 順（魔力炉→研究机→錬金釜→作業台→サークル）で並べる
        foreach (FacilityDef def in HideoutCatalog.Facilities)
            BuildFacilityCard(def);
    }

    private void RefreshFuelBar()
    {
        long fuel = hideout.Fuel;
        long cap = hideout.FuelCapacity;
        if (fuelFill != null) fuelFill.fillAmount = cap > 0 ? Mathf.Clamp01((float)fuel / cap) : 0f;
        if (fuelText != null)
        {
            fuelText.text = hideout.IsBuilt(FacilityKind.ManaFurnace)
                ? "魔力炉 燃料 " + fuel + " / " + cap
                : "魔力炉 未建造 — 設備を動かすには魔力炉が要る";
        }
    }

    private void BuildFacilityCard(FacilityDef def)
    {
        int level = hideout.Level(def.kind);

        RectTransform card = Row(listRoot, 0f);
        VerticalLayoutGroup v = card.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 4f; v.padding = new RectOffset(10, 10, 8, 10);
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;
        v.childControlWidth = true; v.childControlHeight = true;
        AddImage(card, CardBg, false);
        card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        string lvLabel = level <= 0 ? "未建造" : "Lv" + level + (level >= HideoutCatalog.MaxLevel ? "（最大）" : "");
        Label(card, def.name + "   " + lvLabel, 22, FontStyles.Bold, new Color(1f, 0.95f, 0.8f, 1f));
        Label(card, def.blurb, 14, FontStyles.Normal, new Color(1f, 1f, 1f, 0.65f));

        // 建造 / 強化
        List<MaterialCost> next = hideout.NextCost(def.kind);
        if (next != null)
        {
            bool afford = inventory != null && inventory.CanAfford(next);
            bool powered = def.kind == FacilityKind.ManaFurnace || hideout.CanPowerFacilityAction();
            string verb = level <= 0 ? "建造" : "Lv" + (level + 1) + "へ強化";
            string reason = !afford ? "（素材不足）" : !powered ? "（魔力炉の電力不足）" : "";
            ActionRow(card, verb + "  " + CostText(next) + reason, verb,
                afford && powered, GoBtn, () => hideout.Advance(def.kind));
        }

        // 設備ごとの機能
        switch (def.kind)
        {
            case FacilityKind.ManaFurnace: BuildFurnaceSection(card, level); break;
            case FacilityKind.ResearchDesk: BuildResearchSection(card, level); break;
            case FacilityKind.AlchemyCauldron: BuildCauldronSection(card, level); break;
            case FacilityKind.Workbench: BuildWorkbenchSection(card, level); break;
            case FacilityKind.MagicCircle: BuildCircleSection(card, level); break;
        }
    }

    // ---------------------------------------------------------------- 魔力炉

    private void BuildFurnaceSection(RectTransform card, int level)
    {
        if (level <= 0) return;
        Label(card, "スロット " + HideoutCatalog.FurnaceSlots(level) + " ／ 稼働1回=" + HideoutCatalog.FurnaceFuelPerAction(level) + " 燃料", 13,
            FontStyles.Normal, new Color(1f, 1f, 1f, 0.6f));

        MaterialType[] crystals = { MaterialType.SmallManaCrystal, MaterialType.MediumManaCrystal, MaterialType.LargeManaCrystal };
        foreach (MaterialType c in crystals)
        {
            MaterialCost one = new MaterialCost { materialType = c, amount = 1 };
            int have = inventory != null ? inventory.GetCount(one) : 0;
            bool can = have > 0 && hideout.Fuel < hideout.FuelCapacity;
            MaterialType cc = c;
            ActionRow(card, MaterialCatalog.DisplayName(one) + " を入れる（+" + MaterialCatalog.Value(c) + " 燃料 / 所持 " + have + "）",
                "投入", can, OkBtn, () => hideout.LoadFuel(cc));
        }
    }

    // ---------------------------------------------------------------- 研究机

    private void BuildResearchSection(RectTransform card, int level)
    {
        if (level <= 0) return;
        int costPct = Mathf.RoundToInt(HideoutCatalog.ResearchCostMult(level) * 100f);
        int bonusPct = Mathf.RoundToInt(HideoutCatalog.ResearchBonusMult(level) * 100f);
        Label(card, "研究コスト " + costPct + "% ／ 小ノードのボーナス " + bonusPct + "%", 13,
            FontStyles.Normal, new Color(0.8f, 0.95f, 0.8f, 1f));
        ActionRow(card, "スキルツリーを開く", "研究する", hideout.CanPowerFacilityAction(), GoBtn,
            () => { if (phase != null) phase.GoToResearch(); });
    }

    // ---------------------------------------------------------------- 錬金釜

    private void BuildCauldronSection(RectTransform card, int level)
    {
        if (level <= 0) return;
        float mult = HideoutCatalog.CauldronYieldMult(level);
        int maxTier = HideoutCatalog.CauldronMaxTier(level);
        Label(card, "変換効率 ×" + mult.ToString("0.0") + "  /  変換可能: tier1〜" + maxTier, 13,
            FontStyles.Normal, new Color(0.9f, 0.85f, 1f, 0.9f));

        // 所持しているモンスター素材を tier 順に列挙する。
        List<MonsterPart> held = new List<MonsterPart>();
        foreach (MonsterPart mp in MonsterPartCatalog.All)
        {
            MaterialCost probe = new MaterialCost { materialType = MaterialType.SpecialItem, specialItemName = mp.name, amount = 1 };
            if (inventory != null && inventory.GetCount(probe) > 0) held.Add(mp);
        }
        held.Sort((a, b) => a.tier != b.tier ? a.tier.CompareTo(b.tier) : string.CompareOrdinal(a.name, b.name));

        if (held.Count == 0)
        {
            Label(card, "変換できるモンスター素材を持っていない。", 12, FontStyles.Italic, new Color(1f, 1f, 1f, 0.5f));
            return;
        }

        foreach (MonsterPart mp in held)
        {
            MaterialCost part = new MaterialCost { materialType = MaterialType.SpecialItem, specialItemName = mp.name, amount = 1 };
            int have = inventory.GetCount(part);
            int times = Mathf.Min(have, 3);

            if (mp.tier > maxTier)
            {
                int needLv = HideoutCatalog.CauldronLevelForTier(mp.tier);
                ActionRow(card, mp.name + "（tier" + mp.tier + " / 所持 " + have + "） … 錬金釜Lv" + needLv + "で解放",
                    "Lv" + needLv, false, DimBtn, null);
                continue;
            }

            List<MaterialCost> outp = HideoutRules.Transmute(part, times, mult, level);
            string outText = outp.Count > 0 ? MaterialCatalog.DisplayName(outp[0]) + " ×" + outp[0].amount : "?";
            bool can = hideout.CanTransmute(part, times);
            MaterialCost captured = part; int t = times;
            ActionRow(card, mp.name + " ×" + times + " → " + outText + "（tier" + mp.tier + " / 所持 " + have + "）", "変換",
                can, OkBtn, () => hideout.Transmute(captured, t));
        }

        // 錬金釜Lv3：属性エレメントの欠片 → エレメント（O6 対策。ギガ全体魔法のゲート解消）。
        if (level >= 3)
        {
            int unit = HideoutRules.ElementRefinePerElement;
            foreach (MagicAttribute attr in new[]
                { MagicAttribute.Fire, MagicAttribute.Thunder, MagicAttribute.Wind, MagicAttribute.Light, MagicAttribute.Dark })
            {
                MaterialCost frag = new MaterialCost { materialType = MaterialType.ElementFragment, attribute = attr, amount = 1 };
                int haveFrag = inventory != null ? inventory.GetCount(frag) : 0;
                if (haveFrag < unit) continue;

                int refineTimes = Mathf.Min(haveFrag - haveFrag % unit, unit * 10); // 5 の倍数、1アクション上限 50個
                int made = refineTimes / unit;
                MaterialCost capturedFrag = frag; int rt = refineTimes;
                ActionRow(card,
                    MaterialCatalog.DisplayName(frag) + " ×" + refineTimes + " → " + AttrElementName(attr) + " ×" + made
                        + "（所持 " + haveFrag + "）",
                    "精製", hideout.CanTransmute(frag, refineTimes), OkBtn, () => hideout.Transmute(capturedFrag, rt));
            }
        }
    }

    private static string AttrElementName(MagicAttribute attr)
    {
        return MaterialCatalog.DisplayName(new MaterialCost { materialType = MaterialType.Element, attribute = attr, amount = 1 });
    }

    // ---------------------------------------------------------------- 作業台

    private void BuildWorkbenchSection(RectTransform card, int level)
    {
        if (level <= 0) return;
        // 通常品＋レシピ制（tier<=level）を並べ、レシピ未取得はグレー表示にする。
        List<GearDef> list = new List<GearDef>(GearCatalog.Craftable(level));
        foreach (GearDef rg in GearCatalog.RecipeGated())
            if (rg.tier <= level) list.Add(rg);
        foreach (GearDef g in list)
        {
            bool owned = hideout.HasGear(g.id);
            bool lockedRecipe = g.recipeGated && !hideout.RecipeUnlocked(g.id);
            bool can = hideout.CanCraft(g);
            string line = g.name + "（" + SlotName(g.slot) + " / " + GearEffectText(g) + "）  " + CostText(g.cost);
            if (owned) line += "  （所持済）";
            else if (lockedRecipe) line += "  （レシピ未取得）";
            GearDef captured = g;
            string btn = owned ? "所持" : (lockedRecipe ? "未解放" : "製作");
            ActionRow(card, line, btn, can, (owned || lockedRecipe) ? DimBtn : OkBtn,
                () => hideout.Craft(captured));
        }
    }

    private static string SlotName(GearSlot s)
    {
        switch (s) { case GearSlot.Wand: return "杖"; case GearSlot.Armor: return "防具"; default: return "アクセ"; }
    }

    // 装備の効果を1行に要約（主ステータス＋サステイン系の副効果）。
    private static string GearEffectText(GearDef g)
    {
        List<string> parts = new List<string>();
        if (g.amount != 0f) parts.Add(ResearchGraph.Label(g.stat) + "+" + g.amount.ToString("0.#"));
        if (g.hpRegenPerSecond > 0f) parts.Add("HP毎秒+" + g.hpRegenPerSecond.ToString("0.#"));
        if (g.healPerWaveFlat > 0f) parts.Add("ウェーブ回復+" + g.healPerWaveFlat.ToString("0.#"));
        if (g.healPerWavePercent > 0f) parts.Add("ウェーブ回復+" + Mathf.RoundToInt(g.healPerWavePercent * 100f) + "%");
        return parts.Count > 0 ? string.Join(", ", parts) : "—";
    }

    // ---------------------------------------------------------------- マジックサークル

    private void BuildCircleSection(RectTransform card, int level)
    {
        if (level <= 0) return;
        int durPct = Mathf.RoundToInt(HideoutCatalog.CircleDurationMult(level) * 100f);
        Label(card, "待ち時間 " + durPct + "% ／ 高レア加算 +" + HideoutCatalog.CircleRarityBonus(level), 13,
            FontStyles.Normal, new Color(1f, 0.9f, 0.75f, 0.9f));

        // 進行中の捧げもの
        IReadOnlyList<BrewRecord> brews = hideout.Brews;
        for (int i = 0; i < brews.Count; i++)
        {
            BrewRecord b = brews[i];
            bool ready = hideout.BrewReady(i);
            string status = ready ? "完成！" : FormatRemaining(hideout.BrewRemainingSeconds(i));
            int idx = i;
            ActionRow(card, "祭壇: " + b.inputLabel + " → …（" + status + "）", "受取", ready, OkBtn,
                () => { List<MaterialCost> r; hideout.ClaimBrew(idx, out r); });
        }

        // 捧げるアイテムの候補（所持素材から）
        if (inventory != null)
        {
            Label(card, "捧げる —", 13, FontStyles.Normal, new Color(1f, 1f, 1f, 0.55f));
            foreach (KeyValuePair<string, int> kv in inventory.Counts)
            {
                if (kv.Value <= 0) continue;
                MaterialCost sample = inventory.Sample(kv.Key);
                if (sample == null) continue;
                MaterialCost one = new MaterialCost { materialType = sample.materialType, attribute = sample.attribute, specialItemName = sample.specialItemName, amount = 1 };
                float hrs = HideoutRules.BrewHours(HideoutRules.RarityOf(one), level);
                bool can = hideout.CanSacrifice(one);
                MaterialCost captured = one;
                ActionRow(card, MaterialCatalog.DisplayName(one) + "（所持 " + kv.Value + " / 約" + hrs.ToString("0.#") + "時間）",
                    "捧げる", can, GoBtn, () => hideout.Sacrifice(captured));
            }
        }
    }

    private static string FormatRemaining(double seconds)
    {
        if (seconds <= 0) return "まもなく";
        int h = (int)(seconds / 3600);
        int m = (int)((seconds % 3600) / 60);
        return "残り " + (h > 0 ? h + "時間" : "") + m + "分";
    }

    // ---------------------------------------------------------------- 部品ヘルパー

    private static string CostText(List<MaterialCost> cost)
    {
        if (cost == null || cost.Count == 0) return "無料";
        List<string> parts = new List<string>();
        foreach (MaterialCost c in cost) if (c != null) parts.Add(MaterialCatalog.DisplayName(c) + "×" + c.amount);
        return string.Join(" / ", parts);
    }

    private RectTransform Row(RectTransform parent, float minHeight)
    {
        GameObject go = new GameObject("Row", typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        LayoutElement le = go.AddComponent<LayoutElement>();
        if (minHeight > 0f) le.minHeight = minHeight;
        return rt;
    }

    private TMP_Text Label(RectTransform parent, string text, int size, FontStyles style, Color color)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = size; t.fontStyle = style; t.color = color;
        t.alignment = TextAlignmentOptions.MidlineLeft; t.raycastTarget = false;
        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = size + 8f;
        return t;
    }

    // 「説明テキスト ＋ 右端ボタン」の行
    private void ActionRow(RectTransform parent, string text, string buttonLabel, bool interactable, Color btnColor, Action onClick)
    {
        RectTransform row = new GameObject("ActionRow", typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(parent, false);
        HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 8f; h.childForceExpandWidth = false; h.childForceExpandHeight = true;
        h.childControlWidth = true; h.childControlHeight = true; h.childAlignment = TextAnchor.MiddleLeft;
        row.gameObject.AddComponent<LayoutElement>().minHeight = 34f;

        TMP_Text t = Label(row, text, 14, FontStyles.Normal, Color.white);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        LayoutElement tle = t.GetComponent<LayoutElement>();
        tle.flexibleWidth = 1f; tle.minHeight = 30f;

        GameObject btnGo = new GameObject("Btn", typeof(RectTransform));
        btnGo.transform.SetParent(row, false);
        Image bi = btnGo.AddComponent<Image>();
        bi.color = interactable ? btnColor : new Color(btnColor.r, btnColor.g, btnColor.b, 0.35f);
        Button b = btnGo.AddComponent<Button>();
        b.targetGraphic = bi;
        b.interactable = interactable;
        if (interactable && onClick != null) b.onClick.AddListener(() => onClick());
        LayoutElement ble = btnGo.AddComponent<LayoutElement>();
        ble.minWidth = 120f; ble.preferredWidth = 120f; ble.minHeight = 32f;

        GameObject lblGo = new GameObject("Label", typeof(RectTransform));
        lblGo.transform.SetParent(btnGo.transform, false);
        RectTransform lrt = (RectTransform)lblGo.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
        TextMeshProUGUI lt = lblGo.AddComponent<TextMeshProUGUI>();
        if (font != null) lt.font = font;
        lt.text = buttonLabel; lt.fontSize = 16; lt.alignment = TextAlignmentOptions.Center;
        lt.color = new Color(1f, 1f, 1f, interactable ? 1f : 0.6f); lt.raycastTarget = false;
    }

    private static Image AddImage(RectTransform rt, Color color, bool raycast)
    {
        Image img = rt.gameObject.GetComponent<Image>();
        if (img == null) img = rt.gameObject.AddComponent<Image>();
        img.color = color; img.raycastTarget = raycast;
        return img;
    }
}
