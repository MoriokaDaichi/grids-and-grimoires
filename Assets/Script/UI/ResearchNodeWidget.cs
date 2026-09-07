using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// スキルツリーの1ノード。大ノード（魔法）は大きく、小ノード（ステータス系）は小さく表示する。
public class ResearchNodeWidget : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button button;

    private static readonly Color Allocated = new Color(0.30f, 0.72f, 0.40f, 1f);
    private static readonly Color Allocatable = new Color(0.92f, 0.78f, 0.32f, 1f);
    private static readonly Color Locked = new Color(0.32f, 0.34f, 0.40f, 1f);

    public string Id { get; private set; }

    public void Setup(string id, string shortLabel, bool large, Action<string> onClick)
    {
        Id = id;
        RectTransform rt = (RectTransform)transform;
        float size = large ? 108f : 60f;
        rt.sizeDelta = new Vector2(size, size);

        if (label != null)
        {
            label.text = shortLabel;
            label.fontSize = large ? 15f : 10.5f;
        }
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            string captured = id;
            button.onClick.AddListener(() => onClick?.Invoke(captured));
        }
    }

    public void SetState(ResearchNodeState state, bool selected)
    {
        Color c = state == ResearchNodeState.Allocated ? Allocated
            : state == ResearchNodeState.Allocatable ? Allocatable
            : Locked;
        if (selected) c = Color.Lerp(c, Color.white, 0.45f);
        if (background != null) background.color = c;
        transform.localScale = selected ? Vector3.one * 1.18f : Vector3.one;
    }
}
