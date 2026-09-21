using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낮 메인(손님 주문) 화면. 손님 캐릭터 자리 + 말풍선(주문 힌트) + 응답 버튼 2개
/// ("다시 여쭤볼게요" / "포장 시작할게요") + 설정 팝업 + 카탈로그 팝업(전화 버튼으로 열림).
/// UI 요소는 전부 씬 하이라키에 미리 배치되어 있고, 이 스크립트는 참조만 들고 로직을 수행한다.
///
/// 첫째 날은 GameFlowController.Start()가 BeginNewDay()를 호출하면서 씬 로드까지 같은 프레임에
/// 동기적으로 발생시키기 때문에, 이 스크립트의 Start()가 CurrentDayOrder를 읽는 시점이
/// GameFlowController보다 먼저일 수도, 나중일 수도 있다(레이스). NightBoardController 때와 동일한
/// 문제라서 같은 해법을 쓴다: GameFlowController.OnSceneLoaded가 Initialize()를 직접 호출해서
/// 데이터를 넣어주고, Start()는 그게 아직 안 됐을 때만 스스로 채우는 폴백 역할만 한다.
/// </summary>
public class DayMainUI : MonoBehaviour
{
    private enum CatalogTab { Artifacts, Wrapper, Furniture }

    private const string RejectLine = "거절할순없으세요";

    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text bubbleText;
    [SerializeField] private Button askButton;
    [SerializeField] private Button acceptButton;

    [Header("설정 팝업")]
    [SerializeField] private Button preferencesButton;
    [SerializeField] private GameObject preferencesPopup;
    [SerializeField] private Button preferencesCloseButton;

    [Header("카탈로그 팝업 (전화 버튼으로 열고 닫기)")]
    [SerializeField] private Button telephoneButton;
    [SerializeField] private GameObject catalogPopup;
    [SerializeField] private GameObject artifactsPage;
    [SerializeField] private GameObject wrapperPage;
    [SerializeField] private GameObject furniturePage;
    [SerializeField] private Button artifactsIndexButton;
    [SerializeField] private Button wrapperIndexButton;
    [SerializeField] private Button furnitureIndexButton;

    private DayPuzzleGenerator.DayOrder order;
    private bool initialized;
    private bool catalogOpen;

    public void Initialize(DayPuzzleGenerator.DayOrder dayOrder)
    {
        if (initialized) return;
        initialized = true;

        order = dayOrder;

        var currency = CurrencyManager.Instance;
        if (moneyText != null) moneyText.text = currency != null ? $"{currency.CurrentMoney}원" : "0원";

        if (askButton != null) askButton.onClick.AddListener(OnRejectClicked);
        if (acceptButton != null) acceptButton.onClick.AddListener(() =>
        {
            if (GameFlowController.Instance != null) GameFlowController.Instance.GoToFlowerSelect();
        });

        if (preferencesButton != null) preferencesButton.onClick.AddListener(OpenPreferences);
        if (preferencesCloseButton != null) preferencesCloseButton.onClick.AddListener(ClosePreferences);
        if (preferencesPopup != null) preferencesPopup.SetActive(false);

        if (telephoneButton != null) telephoneButton.onClick.AddListener(ToggleCatalog);
        if (artifactsIndexButton != null) artifactsIndexButton.onClick.AddListener(() => SwitchCatalogTab(CatalogTab.Artifacts));
        if (wrapperIndexButton != null) wrapperIndexButton.onClick.AddListener(() => SwitchCatalogTab(CatalogTab.Wrapper));
        if (furnitureIndexButton != null) furnitureIndexButton.onClick.AddListener(() => SwitchCatalogTab(CatalogTab.Furniture));
        catalogOpen = false;
        if (catalogPopup != null) catalogPopup.SetActive(false);
        SwitchCatalogTab(CatalogTab.Wrapper);

        RefreshBubbleText();
    }

    private void Start()
    {
        if (!initialized)
        {
            Initialize(GameFlowController.Instance != null ? GameFlowController.Instance.CurrentDayOrder : null);
        }
    }

    private void OnRejectClicked()
    {
        if (bubbleText != null) bubbleText.text = RejectLine;
    }

    private void RefreshBubbleText()
    {
        if (bubbleText == null) return;

        if (order == null)
        {
            bubbleText.text = "손님이 아직 정하지 못한 것 같아요...";
            return;
        }

        bubbleText.text = $"{order.hintColorName}색 포장지에 {order.requirement.description}";
    }

    private void OpenPreferences()
    {
        if (preferencesPopup != null) preferencesPopup.SetActive(true);
    }

    private void ClosePreferences()
    {
        if (preferencesPopup != null) preferencesPopup.SetActive(false);
    }

    private void ToggleCatalog()
    {
        catalogOpen = !catalogOpen;
        if (catalogPopup != null) catalogPopup.SetActive(catalogOpen);
    }

    private void SwitchCatalogTab(CatalogTab tab)
    {
        if (artifactsPage != null) artifactsPage.SetActive(tab == CatalogTab.Artifacts);
        if (wrapperPage != null) wrapperPage.SetActive(tab == CatalogTab.Wrapper);
        if (furniturePage != null) furniturePage.SetActive(tab == CatalogTab.Furniture);
    }
}
