using System.Collections;
using TMPro;
using UnityEngine;

// 戦闘画面に一瞬表示されて上へ流れながら消えるダメージ数字。DamageNumber.prefab に付ける。
[RequireComponent(typeof(RectTransform))]
public class DamageNumber : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private float riseDistance = 60f;
    [SerializeField] private float lifetime = 0.7f;

    public void Play(int amount, Color color)
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = amount.ToString();
            label.color = color;
        }
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        RectTransform rt = (RectTransform)transform;
        Vector2 start = rt.anchoredPosition;
        Vector2 end = start + Vector2.up * riseDistance;
        Color baseColor = label != null ? label.color : Color.white;

        float t = 0f;
        while (t < lifetime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / lifetime);
            rt.anchoredPosition = Vector2.Lerp(start, end, k);
            if (label != null)
            {
                Color c = baseColor;
                c.a = 1f - k;
                label.color = c;
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
