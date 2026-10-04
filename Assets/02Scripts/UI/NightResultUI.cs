using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 밤 퍼즐 결과 팝업(ResultPopup). 퍼즐을 완료하면(스킵 디버그 포함) 획득한 꽃을 "꽃이름: n개" 형식으로 보여 주고,
/// 확인을 누르면 밤 메인의 다음 손님으로 넘어간다. 팝업은 씬에 미리 배치되어 있고 평소엔 비활성 상태다.
/// </summary>
public class NightResultUI : MonoBehaviour
{
    [SerializeField] private GameObject resultPopup;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Button confirmButton;

    private void Start()
    {
        if (resultPopup != null) resultPopup.SetActive(false);
        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
    }

    /// <param name="gains">꽃 이름 → 늘어난 수량</param>
    public void Show(IEnumerable<KeyValuePair<string, int>> gains)
    {
        var lines = new List<string> { "획득한 꽃", "" };
        foreach (var g in gains) lines.Add($"{g.Key}: {g.Value}개");

        if (resultText != null) resultText.text = string.Join("\n", lines);
        if (resultPopup != null) resultPopup.SetActive(true);
    }

    private void OnConfirm()
    {
        if (resultPopup != null) resultPopup.SetActive(false);
        if (GameFlowController.Instance != null) GameFlowController.Instance.ContinueNightAfterResult();
    }
}
