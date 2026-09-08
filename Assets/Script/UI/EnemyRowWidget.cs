using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 戦闘HUDの敵1体ぶんの簡易行（名前＋HPバー＋HP数値）。表示専用でロジックは持たない。
// BattleHUD が roster.Living の各個体に1つ割り当て、毎フレーム値を流し込む。
public class EnemyRowWidget : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpText;

    private static readonly Color RowAlive = new Color(1f, 1f, 1f, 0.10f);
    private static readonly Color RowDead = new Color(1f, 1f, 1f, 0.03f);
    private static readonly Color RowPrimary = new Color(0.95f, 0.80f, 0.45f, 0.22f); // 代表個体の強調
    private static readonly Color HpHigh = new Color(0.85f, 0.30f, 0.30f, 1f);
    private static readonly Color HpLow = new Color(0.55f, 0.16f, 0.16f, 1f);

    public void Set(string enemyName, int hp, int maxHp, bool isPrimary)
    {
        bool alive = hp > 0;
        float ratio = maxHp > 0 ? Mathf.Clamp01((float)hp / maxHp) : 0f;

        if (nameText != null)
        {
            nameText.text = enemyName;
            nameText.alpha = alive ? 1f : 0.4f;
        }
        if (hpFill != null)
        {
            hpFill.fillAmount = ratio;
            hpFill.color = ratio > 0.3f ? HpHigh : HpLow;
        }
        if (hpText != null)
        {
            hpText.text = alive ? Mathf.Max(0, hp) + " / " + maxHp : "撃破";
            hpText.alpha = alive ? 1f : 0.4f;
        }
        if (background != null)
            background.color = !alive ? RowDead : (isPrimary ? RowPrimary : RowAlive);
    }
}
