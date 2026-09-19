using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "성불" 연출 placeholder. 실제 일러스트 대신 단색 배경 + 문구로 대체한다.
/// EventBus.OnAscensionMoment가 발생할 때마다 잠깐 떴다가 사라진다.
/// 패널은 하이라키에 미리 배치되어 있고 평소엔 비활성 상태다.
/// </summary>
public class AscensionOverlay : MonoBehaviour
{
    private const float ShowDuration = 1.4f;

    [SerializeField] private GameObject panel;
    [SerializeField] private Text label;

    private Coroutine hideRoutine;

    private void Awake()
    {
        panel.SetActive(false);
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
        panel.transform.SetAsLastSibling();
        panel.SetActive(true);

        if (hideRoutine != null) StopCoroutine(hideRoutine);
        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(ShowDuration);
        panel.SetActive(false);
    }
}
