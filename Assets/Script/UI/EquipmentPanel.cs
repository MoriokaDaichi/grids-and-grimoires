using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// キャラクター画面の装備枠（杖 / 防具 / アクセサリー×2）。
// 枠を押すと、その枠に入れられる所有装備の一覧（＋「外す」）をポップアップし、
// 選ぶと HideoutManager.Equip で付け替える。表示は HideoutManager.OnHideoutChanged で更新。
// アクセサリー2枠目はタスク報酬で開放するまで「（未開放）」表示で押しても何も起きない。
public class EquipmentPanel : MonoBehaviour
{
    [System.Serializable]
    public class SlotView
    {
        public EquipSlot slot;
        public Button button;    // 装備枠のクリック受け
        public TMP_Text label;   // 装備中の装備名（空なら「（空）」／未開放なら「（未開放）」）
    }

    [SerializeField] private SlotView[] slots;

    [Header("選択ポップアップ")]
    [SerializeField] private GameObject chooserRoot;      // 既定は非アクティブ
    [SerializeField] private TMP_Text chooserTitle;
    [SerializeField] private RectTransform chooserListRoot;
    [SerializeField] private Button chooserCloseButton;
    [SerializeField] private TMP_FontAsset font;

    private HideoutManager hideout;
    private bool subscribed;

    void Awake()
    {
        if (slots != null)
        {
            foreach (SlotView s in slots)
            {
                if (s == null || s.button == null) continue;
                SlotView captured = s;
                s.button.onClick.AddListener(delegate { OpenChooser(captured.slot); });
            }
        }
        if (chooserCloseButton != null) chooserCloseButton.onClick.AddListener(CloseChooser);
    }

    // OnEnable は HideoutManager.Awake より前に走ることがある（初期化順）。
    // Start でも Bind を呼び、Instance が用意でき次第 購読＋再描画する。
    void OnEnable() { Bind(); }
    void Start() { Bind(); }

    void OnDisable()
    {
        if (hideout != null && subscribed) { hideout.OnHideoutChanged -= RefreshSlots; subscribed = false; }
    }

    private void Bind()
    {
        if (hideout == null) hideout = HideoutManager.Instance;
        if (hideout != null && !subscribed) { hideout.OnHideoutChanged += RefreshSlots; subscribed = true; }
        CloseChooser();
        RefreshSlots();
    }

    private void RefreshSlots()
    {
        if (hideout == null) hideout = HideoutManager.Instance;
        if (slots == null) return;
        foreach (SlotView s in slots)
        {
            if (s == null || s.label == null) continue;
            if (s.slot == EquipSlot.Accessory2 && (hideout == null || !hideout.Accessory2Unlocked))
            {
                s.label.text = "（未開放）";
                continue;
            }
            GearDef g = hideout != null ? hideout.EquippedGear(s.slot) : null;
            s.label.text = g != null ? g.name : "（空）";
        }
    }

    private void OpenChooser(EquipSlot slot)
    {
        if (hideout == null) hideout = HideoutManager.Instance;
        if (hideout == null || chooserRoot == null || chooserListRoot == null) return;
        if (slot == EquipSlot.Accessory2 && !hideout.Accessory2Unlocked) return; // 未開放枠

        GearSlot category = GearCatalog.CategoryOf(slot);
        if (chooserTitle != null) chooserTitle.text = SlotName(slot) + " を選ぶ";

        for (int i = chooserListRoot.childCount - 1; i >= 0; i--)
            Destroy(chooserListRoot.GetChild(i).gameObject);

        string equippedId = hideout.EquippedId(slot);

        // アクセサリーはもう一方の枠に入っているものは選べない（同じ現物は2枠に付けられない）
        string blockedId = null;
        if (GearCatalog.IsAccessorySlot(slot))
        {
            EquipSlot other = slot == EquipSlot.Accessory1 ? EquipSlot.Accessory2 : EquipSlot.Accessory1;
            blockedId = hideout.EquippedId(other);
        }

        AddRow(slot, null, "外す", equippedId == null);
        foreach (string id in hideout.OwnedGearForSlot(category))
        {
            if (id == blockedId) continue;
            GearDef g = GearCatalog.Get(id);
            if (g == null) continue;
            string txt = g.name + "  （T" + g.tier + " / " + StatName(g.stat) + "+" + g.amount.ToString("0.#") + Sustain(g) + "）";
            AddRow(slot, id, txt, id == equippedId);
        }

        chooserRoot.SetActive(true);
    }

    private void AddRow(EquipSlot slot, string id, string text, bool current)
    {
        GameObject row = new GameObject(string.IsNullOrEmpty(id) ? "row_none" : id, typeof(RectTransform));
        row.layer = chooserListRoot.gameObject.layer;
        row.transform.SetParent(chooserListRoot, false);

        Image bg = row.AddComponent<Image>();
        bg.color = current ? new Color(0.25f, 0.4f, 0.7f, 1f) : new Color(1f, 1f, 1f, 0.08f);

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 46f;
        le.minHeight = 46f;

        Button b = row.AddComponent<Button>();
        b.targetGraphic = bg;

        GameObject lblGo = new GameObject("Label", typeof(RectTransform));
        lblGo.layer = row.layer;
        RectTransform lrt = lblGo.GetComponent<RectTransform>();
        lrt.SetParent(row.transform, false);
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(14f, 0f);
        lrt.offsetMax = new Vector2(-14f, 0f);
        TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>();
        if (font != null) lbl.font = font;
        lbl.text = text + (current ? "    ← 装備中" : "");
        lbl.fontSize = 20f;
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
        lbl.color = Color.white;
        lbl.raycastTarget = false;

        string capturedId = id;
        EquipSlot capturedSlot = slot;
        b.onClick.AddListener(delegate
        {
            if (hideout != null) hideout.Equip(capturedSlot, capturedId);
            RefreshSlots();
            CloseChooser();
        });
    }

    private void CloseChooser()
    {
        if (chooserRoot != null) chooserRoot.SetActive(false);
    }

    private static string SlotName(EquipSlot s)
    {
        switch (s)
        {
            case EquipSlot.Wand: return "杖";
            case EquipSlot.Armor: return "防具";
            case EquipSlot.Accessory1: return "アクセサリー";
            case EquipSlot.Accessory2: return "アクセサリー2";
        }
        return s.ToString();
    }

    private static string StatName(ResearchStat s)
    {
        switch (s)
        {
            case ResearchStat.Hp: return "HP";
            case ResearchStat.Atk: return "Atk";
            case ResearchStat.Def: return "Def";
            case ResearchStat.Spd: return "Spd";
            case ResearchStat.Luc: return "Luc";
            case ResearchStat.ManaMax: return "最大MP";
            case ResearchStat.ManaRegen: return "MP回復";
        }
        return s.ToString();
    }

    private static string Sustain(GearDef g)
    {
        if (!g.HasSustain) return "";
        List<string> p = new List<string>();
        if (g.hpRegenPerSecond > 0f) p.Add("HP毎秒+" + g.hpRegenPerSecond.ToString("0.#"));
        if (g.healPerWaveFlat > 0f) p.Add("W回復+" + g.healPerWaveFlat.ToString("0.#"));
        if (g.healPerWavePercent > 0f) p.Add("W回復+" + Mathf.RoundToInt(g.healPerWavePercent * 100f) + "%");
        return " / " + string.Join(",", p.ToArray());
    }
}
