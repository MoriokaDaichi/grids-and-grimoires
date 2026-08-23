using UnityEngine;

public class PanelSwitcher : MonoBehaviour
{
    [Header("閉じるパネル（複数選択可）")]
    public GameObject[] panelsToClose;

    [Header("開くパネル（複数選択可）")]
    public GameObject[] panelsToOpen;

    /// <summary>
    /// パネルを切り替えるメソッド
    /// </summary>
    public void SwitchPanel()
    {
        // 閉じるパネルをすべて非表示にする
        if (panelsToClose != null)
        {
            foreach (GameObject panel in panelsToClose)
            {
                if (panel != null)
                {
                    panel.SetActive(false);
                }
            }
        }

        // 開くパネルをすべて表示にする
        if (panelsToOpen != null)
        {
            foreach (GameObject panel in panelsToOpen)
            {
                if (panel != null)
                {
                    panel.SetActive(true);
                }
            }
        }
    }
}