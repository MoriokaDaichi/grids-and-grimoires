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
        if (nameLabel != null) nameLabel.text = displayName;
        if (costLabel != null) costLabel.text = costText;

        if (actionButton != null)
        {
            actionButton.onClick.RemoveAllListeners();
            if (onUnlockClicked != null)
            {
                actionButton.onClick.AddListener(() => onUnlockClicked());
            }
            actionButton.interactable = !unlocked && canUnlock;
        }
        if (actionLabel != null) actionLabel.text = unlocked ? "解放済" : "解放";
    }
}
