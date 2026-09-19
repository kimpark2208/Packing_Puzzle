using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮→밤 전환 UI 관리
/// 낮 퍼즐 완료 후 플레이어가 보유한 꽃 중 부족하다고 판단되는 꽃을 요청하는 인터페이스
/// </summary>
public class DayNightTransition : MonoBehaviour
{
    [SerializeField] private VerticalLayoutGroup flowerButtonContainer;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Text titleText;

    private List<int> selectedFlowerIds = new();
    private GameObject flowerButtonPrefab;

    private void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmRequests);
    }

    /// <summary>
    /// 요청 UI 표시
    /// </summary>
    public void ShowFlowerRequestUI()
    {
        // Phase 2: 낮 퍼즐 검증 (색상/꽃 추출, 매출 계산)
        PuzzleValidator.ValidatePuzzle();

        selectedFlowerIds.Clear();

        if (titleText != null)
            titleText.text = "이 꽃이 부족해요";

        // 버튼 컨테이너 초기화
        foreach (Transform child in flowerButtonContainer.transform)
        {
            Destroy(child.gameObject);
        }

        // 보유한 꽃 버튼 생성
        var obtainedFlowers = CurrencyManager.Instance.GetObtainedFlowerIds();
        foreach (int flowerId in obtainedFlowers)
        {
            CreateFlowerButton(flowerId);
        }

        gameObject.SetActive(true);
        Debug.Log($"[DayNightTransition] 꽃 요청 UI 표시 (보유 꽃: {obtainedFlowers.Count}개)");
    }

    private void CreateFlowerButton(int flowerId)
    {
        // 간단한 버튼: 텍스트만 표시 (스프라이트 없음)
        var buttonGo = new GameObject($"FlowerButton_{flowerId}");
        buttonGo.transform.SetParent(flowerButtonContainer.transform, false);

        var button = buttonGo.AddComponent<Button>();
        var layoutElement = buttonGo.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 60;

        // 버튼 텍스트
        var text = buttonGo.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = $"꽃 {flowerId}";
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.black;
        text.fontSize = 18;

        // 버튼 배경
        var image = buttonGo.AddComponent<Image>();
        image.color = new Color(0.8f, 0.8f, 0.8f);

        // 클릭 리스너
        button.onClick.AddListener(() => OnFlowerButtonClicked(flowerId, button));

        Debug.Log($"[DayNightTransition] 꽃 {flowerId} 버튼 생성");
    }

    private void OnFlowerButtonClicked(int flowerId, Button button)
    {
        if (selectedFlowerIds.Contains(flowerId))
        {
            selectedFlowerIds.Remove(flowerId);
            button.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f);
            Debug.Log($"[DayNightTransition] 꽃 {flowerId} 선택 해제");
        }
        else
        {
            selectedFlowerIds.Add(flowerId);
            button.GetComponent<Image>().color = new Color(0.6f, 0.8f, 1.0f);
            Debug.Log($"[DayNightTransition] 꽃 {flowerId} 선택");
        }
    }

    private void OnConfirmRequests()
    {
        Debug.Log($"[DayNightTransition] 요청 확인: {selectedFlowerIds.Count}개 꽃");

        // CurrencyManager에 요청 등록
        foreach (int flowerId in selectedFlowerIds)
        {
            CurrencyManager.Instance.RequestFlowerForTonight(flowerId);
        }

        // 밤 퍼즐 생성 시작
        var requestedFlowerIds = CurrencyManager.Instance.ClearDailyRequestsAndGet();
        EventBus.RaiseDayEnded(requestedFlowerIds);

        // 프로시저럴 밤 퍼즐 생성
        ProceduralNightPuzzleGenerator.GeneratePuzzles(5, requestedFlowerIds);

        gameObject.SetActive(false);
    }
}
