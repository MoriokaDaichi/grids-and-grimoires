using TMPro;
using UnityEngine;

// 所持金（MoneyManager.Balance）を1つの TMP_Text に表示する。
// MoneyManager.OnMoneyChanged を購読して再描画する（他UIのイベント購読パターンを踏襲）。
public class MoneyLabel : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private string prefix = "所持金 ";
    [SerializeField] private string suffix = " G";

    private MoneyManager money;

    void Awake()
    {
        if (label == null) label = GetComponent<TMP_Text>();
        money = Object.FindFirstObjectByType<MoneyManager>();
    }

    void OnEnable()
    {
        if (money == null) money = Object.FindFirstObjectByType<MoneyManager>();
        if (money != null) money.OnMoneyChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (money != null) money.OnMoneyChanged -= Refresh;
    }

    private void Refresh()
    {
        if (label == null) return;
        int bal = money != null ? money.Balance : 0;
        label.text = prefix + bal + suffix;
    }
}
