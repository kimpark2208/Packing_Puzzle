using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 포장지 템플릿 안의 칸 하나. SlotArea 아래에 에디터에서 직접 배치해 두고(오브젝트 이름 = position),
/// WrapperBoardController가 프리셋에 따라 태그와 색을 정한다. 채워지면 하이라이트(Image)의 투명도를 바꿔 표시한다.
/// </summary>
public class WrapperSlot : MonoBehaviour
{
    public enum Quadrant { TopRight, TopLeft, BottomLeft, BottomRight, Center }

    public WrapperRegionPosition position; // 에디터에서 지정한다 (오브젝트 이름과 같게)
    public FlowerData.FlowerRole tag;      // 프리셋이 정한다

    public int ringIndex => position.Ring() - 1; // -1 = 중앙

    public Quadrant quadrant
    {
        get
        {
            if (position == WrapperRegionPosition.Center) return Quadrant.Center;
            bool upper = ((int)position - 1) % 2 == 0;
            if (position.IsFaceA()) return upper ? Quadrant.TopLeft : Quadrant.BottomLeft;
            return upper ? Quadrant.TopRight : Quadrant.BottomRight;
        }
    }

    private readonly List<FlowerData> placed = new();

    public IReadOnlyList<FlowerData> PlacedFlowers => placed;
    public int FilledCount => placed.Count;
    public bool IsFull => placed.Count > 0; // 칸당 꽃 한 송이

    private Image background;
    private Outline hoverOutline; // 에디터에서 칸마다 붙여 둔다(평소엔 꺼짐). 놓을 칸을 흰 테두리로 맥박치게 해서 손가락 근처에서도 보이게 한다

    private const float PulseSpeed = 8f;

    private void Awake()
    {
        background = GetComponent<Image>();
        hoverOutline = GetComponent<Outline>();
        if (hoverOutline != null) hoverOutline.enabled = false;
    }

    private void Update()
    {
        if (hoverOutline == null || !hoverOutline.enabled) return;

        Color c = hoverOutline.effectColor;
        c.a = Mathf.Lerp(0.55f, 1f, (Mathf.Sin(Time.unscaledTime * PulseSpeed) + 1f) * 0.5f);
        hoverOutline.effectColor = c;
    }

    /// <summary>드래그 중인 꽃을 놓을 수 있는 칸으로 가리킬 때 테두리를 켠다.</summary>
    public void SetHover(bool on)
    {
        if (hoverOutline != null) hoverOutline.enabled = on;
    }

    /// <summary>하이라이트 색을 지정한다(태그 색 + 투명도).</summary>
    public void SetHighlightColor(Color color)
    {
        if (background != null) background.color = color;
    }

    /// <summary>하이라이트 투명도만 바꾼다(낮음=빈 영역, 중간=호버 미리보기, 1=확정 채움).</summary>
    public void SetHighlightAlpha(float alpha)
    {
        if (background == null) return;
        Color c = background.color;
        c.a = alpha;
        background.color = c;
    }

    /// <summary>꽃을 기록하고 자리를 채운 것으로 표시한다.</summary>
    public void Fill(FlowerData flower)
    {
        placed.Add(flower);
    }

    /// <summary>드래그해온 꽃이 이 슬롯에 들어갈 수 있는지 확인. 자리가 비어 있고 꽃의 역할(속성)이 이 칸의 태그와 같아야 한다.</summary>
    public bool CanAccept(FlowerData flower)
    {
        return !IsFull && flower != null && flower.flowerRole == tag;
    }

    public void Clear()
    {
        placed.Clear();
    }
}
