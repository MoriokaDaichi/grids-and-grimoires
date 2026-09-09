using UnityEngine;
using UnityEngine.UI;
using TMPro; // TMPを使うために必要

public class StatusUIManager : MonoBehaviour
{
    [Header("--- Reference ---")]
    public PlayerStatus playerStatus;

    [Header("--- Text UI (TMP) ---")]
    public TextMeshProUGUI statsPointText;
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI atkText;
    public TextMeshProUGUI defText;
    public TextMeshProUGUI spdText;
    public TextMeshProUGUI lucText;

    [Header("--- Bar Images (Image Type: Filled) ---")]
    public Image hpBarImage;
    public Image atkBarImage;
    public Image defBarImage;
    public Image spdBarImage;
    public Image lucBarImage;

    [Header("--- Plus Buttons ---")]
    public Button hpButton;
    public Button atkButton;
    public Button defButton;
    public Button spdButton;
    public Button lucButton;

    // バー表示の基準値（実際の振り分け可否は PlayerStatus.CanAddStat が持つ。
    // 研究・装備で合計はこれを超えうるが、その場合バーは満タン表示でよい）。
    private const int HP_MAX = 1000;
    private const int OTHER_MAX = 100;

    void Start()
    {
        if (playerStatus != null)
        {
            playerStatus.OnStatusChanged += UpdateUI;
            UpdateUI();
        }
    }

    public void OnClickPlusButton(string type)
    {
        if (playerStatus != null)
        {
            playerStatus.AddStat(type);
        }
    }

    public void UpdateUI()
    {
        if (playerStatus == null) return;

        // TMPのテキスト更新
        statsPointText.text = playerStatus.statsPoint.ToString();
        hpText.text = playerStatus.hp.ToString();
        atkText.text = playerStatus.atk.ToString();
        defText.text = playerStatus.def.ToString();
        spdText.text = playerStatus.spd.ToString();
        lucText.text = playerStatus.luc.ToString();

        // バーの更新（端の隙間対策として、最大値の時は確実に 1.0f になるよう Mathf.Clamp01 を使用）
        hpBarImage.fillAmount = Mathf.Clamp01((float)playerStatus.hp / HP_MAX);
        atkBarImage.fillAmount = Mathf.Clamp01((float)playerStatus.atk / OTHER_MAX);
        defBarImage.fillAmount = Mathf.Clamp01((float)playerStatus.def / OTHER_MAX);
        spdBarImage.fillAmount = Mathf.Clamp01((float)playerStatus.spd / OTHER_MAX);
        lucBarImage.fillAmount = Mathf.Clamp01((float)playerStatus.luc / OTHER_MAX);

        // ボタンの活性化設定（手動振り分けの上限＝PlayerStatus 側。合計値ではなく手動加算分で判定）
        hpButton.interactable = playerStatus.CanAddStat("HP");
        atkButton.interactable = playerStatus.CanAddStat("Atk");
        defButton.interactable = playerStatus.CanAddStat("Def");
        spdButton.interactable = playerStatus.CanAddStat("Spd");
        lucButton.interactable = playerStatus.CanAddStat("Luc");
    }

    private void OnDestroy()
    {
        if (playerStatus != null)
        {
            playerStatus.OnStatusChanged -= UpdateUI;
        }
    }
}