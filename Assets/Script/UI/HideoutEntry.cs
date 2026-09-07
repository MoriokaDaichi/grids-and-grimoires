using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 研究画面の1行（魔法名 / コスト / 解放ボタン）。HideoutEntry.prefab に付ける。
public class HideoutEntry : MonoBehaviour
{
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text costLabel;
    [SerializeField] private Button actionButton;
    [SerializeField] private TMP_Text actionLabel;

    public void Bind(string displayName, string costText, bool unlocked, bool canUnlock, Action onUnlockClicked)
    {
        Bind(displayName, costText, unlocked ? "解放済" : "解放", !unlocked && canUnlock, unlocked ? null : onUnlockClicked);
    }

    // 汎用: 研究・トレードなどで使い回す
    public void Bind(string displayName, string detailText, string buttonText, bool interactable, Action onClicked)
    {
        if (nameLabel != null) nameLabel.text = displayName;
        if (costLabel != null) costLabel.text = detailText;

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            if (onClicked != null)
            {
                actionButton.onClick.AddListener(() => onClicked());
            }
            actionButton.interactable = interactable;
        }
        if (actionLabel != null) actionLabel.text = buttonText;
    }
}
