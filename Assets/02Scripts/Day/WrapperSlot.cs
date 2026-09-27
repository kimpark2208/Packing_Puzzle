using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 포장지 템플릿 안의 영역(칸) 하나. WrapperBoardController가 절차적으로 생성하고,
/// 채워지면 하이라이트(배경 스프라이트)의 투명도를 바꿔 표시한다.
/// </summary>
public class WrapperSlot : MonoBehaviour
{
    public enum Quadrant { TopRight, TopLeft, BottomLeft, BottomRight, Center }

    public int requiredCount = 1;

    // 동심원 링 + 4분할 모델에서 쓰는 위치 정보.
    public int ringIndex = -1; // -1 = 중앙
    public Quadrant quadrant = Quadrant.Center;

    private readonly List<BlockData> placed = new();

    public IReadOnlyList<BlockData> PlacedFlowers => placed;
    public int FilledCount => placed.Count;
    public bool IsFull => placed.Count >= requiredCount;

    private Image background;

    private void Awake()
    {
        background = GetComponent<Image>();
    }

    /// <summary>동심원 4분할 모델용: 이 슬롯 모양에 맞게 절차적으로 만든 하이라이트 스프라이트를 지정한다.</summary>
    public void SetHighlightSprite(Sprite sprite, Color color)
    {
        if (background == null) background = gameObject.AddComponent<Image>();
        background.sprite = sprite;
        background.color = color;
        background.raycastTarget = false;
    }

    /// <summary>하이라이트 투명도만 바꾼다(0=안 보임, 중간값=호버 미리보기, 1=확정 채움).</summary>
    public void SetHighlightAlpha(float alpha)
    {
        if (background == null) return;
        Color c = background.color;
        c.a = alpha;
        background.color = c;
    }

    /// <summary>태그 무시 버전: 꽃을 기록만 하고 자리를 채운 것으로 표시한다.</summary>
    public void Fill(BlockData flower)
    {
        placed.Add(flower);
    }

    /// <summary>드래그해온 꽃이 이 슬롯에 들어갈 수 있는지 확인. (지금은 태그 구분 없이 자리만 남았으면 OK)</summary>
    public bool CanAccept(BlockData flower)
    {
        return !IsFull && flower != null;
    }

    public void Clear()
    {
        placed.Clear();
    }
}
