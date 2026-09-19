using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 런타임에 uGUI 요소를 코드로 생성하는 스크립트들(DayMainUI, FlowerSelectUI 등)이 공유하는
/// 작은 헬퍼 모음. 배경/버튼 등은 이미지 없이 색상만으로 구분한다는 방침에 맞춰
/// 전부 스프라이트 없는 단색 Image로 만든다.
/// </summary>
public static class UIFactory
{
    public static Font DefaultFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    public static RectTransform CreateFullStretchPanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = color;

        return rt;
    }

    public static Text CreateText(Transform parent, string text, int fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var t = go.AddComponent<Text>();
        t.font = DefaultFont;
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        return t;
    }

    public const int DefaultButtonFontSize = 42;

    public static Button CreateButton(Transform parent, string label, Color bgColor, Color textColor, int fontSize = DefaultButtonFontSize)
    {
        var go = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = bgColor;

        var btn = go.GetComponent<Button>();

        CreateText(rt.transform, label, fontSize, textColor);

        return btn;
    }

    // 프로젝트의 기존 씬(01DayMain/03DayPuzzle/02NightPuzzle) Canvas들이 이미 이 기준으로 세팅되어 있다.
    // 새로 Canvas를 만드는 씬(02FlowerSellect, 01NightMain)도 동일한 기준이어야 글자/버튼 크기가 일관된다.
    private static readonly Vector2 ReferenceResolution = new(2960f, 1440f);
    private const float MatchWidthOrHeight = 1f;

    /// <summary>씬에 Canvas가 아직 없을 때 프로젝트의 Input System 설정에 맞는 Canvas+EventSystem을 만든다.</summary>
    public static Canvas EnsureCanvas()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas != null) return canvas;

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = MatchWidthOrHeight;

        EnsureEventSystem();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }
}
