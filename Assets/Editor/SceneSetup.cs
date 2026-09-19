using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using UnityEditor;

/// <summary>
/// 씬 자동 구성 (Editor 스크립트)
/// Unity 메뉴에서 실행: Tools > Setup Packing Puzzle Scene
/// </summary>
public class SceneSetup
{
    [MenuItem("Tools/Setup Packing Puzzle Scene")]
    public static void SetupScene()
    {
        Debug.Log("[SceneSetup] 씬 구성 시작...");

        // 기존 Canvas 찾기 또는 생성
        Canvas mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            var canvasGo = new GameObject("Canvas");
            mainCanvas = canvasGo.AddComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var canvasScaler = canvasGo.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            Debug.Log("[SceneSetup] Canvas 생성");
        }

        // ========== ShopUI 구성 ==========
        SetupShopUI(mainCanvas);

        // ========== DayNightTransition 구성 ==========
        SetupDayNightTransition(mainCanvas);

        // Scene 저장
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("[SceneSetup] 씬 구성 완료 및 저장!");
    }

    private static void SetupShopUI(Canvas mainCanvas)
    {
        // ShopUI 컨테이너
        var shopUiGo = new GameObject("ShopUI");
        shopUiGo.transform.SetParent(mainCanvas.transform, false);

        var shopRect = shopUiGo.AddComponent<RectTransform>();
        shopRect.anchorMin = Vector2.zero;
        shopRect.anchorMax = Vector2.one;
        shopRect.offsetMin = Vector2.zero;
        shopRect.offsetMax = Vector2.zero;

        var shopImage = shopUiGo.AddComponent<Image>();
        shopImage.color = Color.white;

        var shopLayout = shopUiGo.AddComponent<VerticalLayoutGroup>();
        shopLayout.padding = new RectOffset(10, 10, 10, 10);
        shopLayout.spacing = 10;
        shopLayout.childForceExpandHeight = false;

        // 금액 텍스트
        var moneyGo = new GameObject("MoneyText");
        moneyGo.transform.SetParent(shopUiGo.transform, false);
        var moneyLayout = moneyGo.AddComponent<LayoutElement>();
        moneyLayout.preferredHeight = 40;

        var moneyText = moneyGo.AddComponent<Text>();
        moneyText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        moneyText.text = "금액: 0원";
        moneyText.alignment = TextAnchor.MiddleLeft;
        moneyText.color = Color.black;
        moneyText.fontSize = 20;

        var moneyBg = moneyGo.AddComponent<Image>();
        moneyBg.color = new Color(0.95f, 0.95f, 0.95f);

        // 탭 버튼 컨테이너
        var tabContainerGo = new GameObject("TabButtonContainer");
        tabContainerGo.transform.SetParent(shopUiGo.transform, false);
        var tabLayout = tabContainerGo.AddComponent<LayoutElement>();
        tabLayout.preferredHeight = 60;

        var tabHGroup = tabContainerGo.AddComponent<HorizontalLayoutGroup>();
        tabHGroup.padding = new RectOffset(5, 5, 5, 5);
        tabHGroup.spacing = 5;
        tabHGroup.childForceExpandHeight = true;
        tabHGroup.childForceExpandWidth = true;

        var tabBg = tabContainerGo.AddComponent<Image>();
        tabBg.color = new Color(0.85f, 0.85f, 0.85f);

        // 아이템 컨테이너
        var itemContainerGo = new GameObject("ItemContainer");
        itemContainerGo.transform.SetParent(shopUiGo.transform, false);

        var itemScroll = itemContainerGo.AddComponent<ScrollRect>();
        var itemContent = new GameObject("Content");
        itemContent.transform.SetParent(itemContainerGo.transform, false);

        var itemVGroup = itemContent.AddComponent<VerticalLayoutGroup>();
        itemVGroup.padding = new RectOffset(10, 10, 10, 10);
        itemVGroup.spacing = 10;
        itemVGroup.childForceExpandHeight = false;

        var itemContentLayout = itemContent.AddComponent<LayoutElement>();
        itemContentLayout.flexibleWidth = 1;

        itemScroll.content = itemContent.GetComponent<RectTransform>();
        itemScroll.vertical = true;
        itemScroll.horizontal = false;

        var itemBg = itemContainerGo.AddComponent<Image>();
        itemBg.color = Color.white;

        // ShopUI 스크립트 추가 및 연결
        var shopUI = shopUiGo.AddComponent<ShopUI>();
        shopUI.GetType().GetField("itemContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(shopUI, itemVGroup);
        shopUI.GetType().GetField("tabButtonContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(shopUI, tabHGroup);
        shopUI.GetType().GetField("moneyText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(shopUI, moneyText);

        Debug.Log("[SceneSetup] ShopUI 생성 완료");
    }

    private static void SetupDayNightTransition(Canvas mainCanvas)
    {
        // DayNightTransition 컨테이너
        var dayNightGo = new GameObject("DayNightTransition");
        dayNightGo.transform.SetParent(mainCanvas.transform, false);

        var dayNightRect = dayNightGo.AddComponent<RectTransform>();
        dayNightRect.anchorMin = Vector2.zero;
        dayNightRect.anchorMax = Vector2.one;
        dayNightRect.offsetMin = Vector2.zero;
        dayNightRect.offsetMax = Vector2.zero;

        var dayNightImage = dayNightGo.AddComponent<Image>();
        dayNightImage.color = new Color(0, 0, 0, 0.8f);

        var dayNightLayout = dayNightGo.AddComponent<VerticalLayoutGroup>();
        dayNightLayout.padding = new RectOffset(20, 20, 20, 20);
        dayNightLayout.spacing = 15;
        dayNightLayout.childForceExpandHeight = false;

        // 제목
        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(dayNightGo.transform, false);
        var titleLayout = titleGo.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 50;

        var titleText = titleGo.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        titleText.text = "이 꽃이 부족해요";
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.fontSize = 28;

        // 꽃 버튼 컨테이너
        var flowerContainerGo = new GameObject("FlowerButtonContainer");
        flowerContainerGo.transform.SetParent(dayNightGo.transform, false);

        var flowerScroll = flowerContainerGo.AddComponent<ScrollRect>();
        var flowerContent = new GameObject("Content");
        flowerContent.transform.SetParent(flowerContainerGo.transform, false);

        var flowerVGroup = flowerContent.AddComponent<VerticalLayoutGroup>();
        flowerVGroup.padding = new RectOffset(10, 10, 10, 10);
        flowerVGroup.spacing = 10;
        flowerVGroup.childForceExpandHeight = false;

        var flowerContentLayout = flowerContent.AddComponent<LayoutElement>();
        flowerContentLayout.flexibleWidth = 1;

        flowerScroll.content = flowerContent.GetComponent<RectTransform>();
        flowerScroll.vertical = true;
        flowerScroll.horizontal = false;

        var flowerBg = flowerContainerGo.AddComponent<Image>();
        flowerBg.color = new Color(0.3f, 0.3f, 0.3f);

        var flowerContainerLayout = flowerContainerGo.AddComponent<LayoutElement>();
        flowerContainerLayout.preferredHeight = 300;

        // Confirm 버튼
        var confirmGo = new GameObject("ConfirmButton");
        confirmGo.transform.SetParent(dayNightGo.transform, false);

        var confirmButton = confirmGo.AddComponent<Button>();
        var confirmLayout = confirmGo.AddComponent<LayoutElement>();
        confirmLayout.preferredHeight = 60;

        var confirmText = confirmGo.AddComponent<Text>();
        confirmText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        confirmText.text = "확인";
        confirmText.alignment = TextAnchor.MiddleCenter;
        confirmText.color = Color.black;
        confirmText.fontSize = 22;

        var confirmBg = confirmGo.AddComponent<Image>();
        confirmBg.color = new Color(0.8f, 0.9f, 0.8f);

        // DayNightTransition 스크립트 추가 및 연결
        var dayNightTransition = dayNightGo.AddComponent<DayNightTransition>();
        dayNightTransition.GetType().GetField("flowerButtonContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(dayNightTransition, flowerVGroup);
        dayNightTransition.GetType().GetField("confirmButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(dayNightTransition, confirmButton);
        dayNightTransition.GetType().GetField("titleText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(dayNightTransition, titleText);

        dayNightGo.SetActive(false);

        Debug.Log("[SceneSetup] DayNightTransition 생성 완료");
    }
}
