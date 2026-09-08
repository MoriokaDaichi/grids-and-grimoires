using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 発動中のアクティブバフ1種を表すインジケータ。BuffIndicator.prefab に付ける。
// 残り時間をラジアルフィルで見せ、0で自壊する（BattleHUDもOnBuffExpiredで破棄するため二重破棄に注意）。
public class BuffIndicator : MonoBehaviour
{
    [SerializeField] private Image radialFill;
    [SerializeField] private TMP_Text label;

    public BuffStat Stat { get; private set; }

    private float duration = 1f;
    private float remaining;
    private bool running;

    public void Bind(BuffStat stat, float dur)
    {
        Stat = stat;
        if (label != null) label.text = ShortLabel(stat);
        Refresh(dur);
    }

    // BattleHUD が OnBuffApplied（更新）を受けたときに残り時間を戻す
    public void Refresh(float dur)
    {
        duration = Mathf.Max(0.01f, dur);
        remaining = duration;
        running = true;
        UpdateFill();
    }

    void Update()
    {
        if (!running) return;

        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            running = false;
            UpdateFill();
            Destroy(gameObject);
            return;
        }
        UpdateFill();
    }

    private void UpdateFill()
    {
        if (radialFill != null) radialFill.fillAmount = Mathf.Clamp01(remaining / duration);
    }

    public static string ShortLabel(BuffStat stat)
    {
        switch (stat)
        {
            case BuffStat.Atk: return "攻";
            case BuffStat.Def: return "防";
            case BuffStat.Spd: return "速";
            case BuffStat.Luc: return "運";
            case BuffStat.Damage: return "威";
            default: return "?";
        }
    }
}
