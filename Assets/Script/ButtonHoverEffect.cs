using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public GameObject outlineImage;

    public void OnPointerEnter(PointerEventData eventData)
    {
        outlineImage.SetActive(true); // マウスが乗ったら表示
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        outlineImage.SetActive(false); // 離れたら非表示
    }

    public void ButtonAway()
    {
        outlineImage.SetActive(false);
    }
}