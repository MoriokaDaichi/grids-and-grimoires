using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MagicGeneratorButton : MonoBehaviour
{
    public MagicData magicData;
    private MagicSpawner spawner;

    public void Init(MagicData data, MagicSpawner parentSpawner)
    {
        magicData = data;
        spawner = parentSpawner;

        // ボタンの見た目をMagicDataの色などにする（任意）
        if (TryGetComponent<Image>(out Image img))
        {
            img.color = data.pieceColor;
        }

        TMP_Text tmpText = GetComponentInChildren<TMP_Text>();
        if (tmpText != null)
        {
            tmpText.text = data.magicName;
        }
    }

    public void OnClickButton()
    {
        spawner.SpawnPiece(this);
    }
}