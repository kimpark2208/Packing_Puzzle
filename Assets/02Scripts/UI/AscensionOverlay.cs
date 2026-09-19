using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "성불" 연출 placeholder. 실제 일러스트 대신 단색 배경 + 문구로 대체한다.
/// EventBus.OnAscensionMoment가 발생할 때마다 잠깐 떴다가 사라진다.
/// </summary>
public class AscensionOverlay : MonoBehaviour
{
    private static readonly Color OverlayColor = new(0.15f, 0.12f, 0.25f, 0.92f);
    private const float ShowDuration = 1.4f;

    private RectTransform panel;
    private Text label;
    private Coroutine hideRoutine;

    private void Awake()
    {
        Canvas canvas = UIFactory.EnsureCanvas();
        panel = UIFactory.CreateFullStretchPanel("AscensionOverlay", canvas.transform, OverlayColor);
        panel.SetAsLastSibling();
        label = UIFactory.CreateText(panel, "", 30, Color.white);
        panel.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        EventBus.OnAscensionMoment += Show;
    }

    private void OnDisable()
    {
        EventBus.OnAscensionMoment -= Show;
    }

    private void Show(string message)
    {
        label.text = message;
        panel.SetAsLastSibling();
        panel.gameObject.SetActive(true);

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(ShowDuration);
        panel.gameObject.SetActive(false);
    }
}
