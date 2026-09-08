using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 敵にかかっている状態異常1種を表す小アイコン。StatusEffectIcon.prefab に付ける。
// アート素材が無いため、背景色＋1文字ラベルで表現する。
public class StatusEffectIcon : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;

    public StatusEffectType Type { get; private set; }

    public void Bind(StatusEffectType type)
    {
        Type = type;
        if (label != null) label.text = ShortLabel(type);
        if (background != null) background.color = TintOf(type);
    }

    public static string ShortLabel(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Burn: return "火";
            case StatusEffectType.Shock: return "感";
            case StatusEffectType.Laceration: return "裂";
            case StatusEffectType.Dizzy: return "眩";
            case StatusEffectType.Blind: return "盲";
            default: return "?";
        }
    }

    public static Color TintOf(StatusEffectType type)
    {
        switch (type)
        {
            case StatusEffectType.Burn: return new Color(0.90f, 0.35f, 0.20f);
            case StatusEffectType.Shock: return new Color(0.95f, 0.82f, 0.30f);
            case StatusEffectType.Laceration: return new Color(0.55f, 0.80f, 0.45f);
            case StatusEffectType.Dizzy: return new Color(0.85f, 0.75f, 0.95f);
            case StatusEffectType.Blind: return new Color(0.40f, 0.32f, 0.55f);
            default: return Color.gray;
        }
    }
}
