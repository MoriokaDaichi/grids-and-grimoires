using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 報酬画面/インベントリの素材1行。DropRow.prefab に付ける。
public class DropRow : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text amountLabel;

    public void Bind(string displayName, int amount, Color iconColor)
    {
        if (nameLabel != null) nameLabel.text = displayName;
        if (amountLabel != null) amountLabel.text = "×" + amount;
        if (icon != null) icon.color = iconColor;
    }
}
