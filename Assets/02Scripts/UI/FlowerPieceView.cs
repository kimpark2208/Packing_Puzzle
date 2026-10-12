using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꽃 한 송이를 보여주는 템플릿(트레이 조각 FlowerTemplate, 선택 화면 슬롯 VaseTemplate)의 표시만 담당한다:
/// 아이콘(들), 아이콘 외곽선(역할 색), 이름. 템플릿마다 자식 위치가 달라서 이름으로 깊이 찾고,
/// 없는 요소는 건너뛴다. 드래그 입력은 BlockDrag, 선택 개수 같은 화면별 동작은 각 화면이 맡는다.
/// 꽃 아이콘은 이름이 "FlowerImage"로 시작하는 이미지 전부다(화병은 "FlowerImage (1)", "FlowerImage (2)"처럼 꽃을 여러 송이 둘 수 있고, 모두 같은 그림과 색을 입는다).
/// </summary>
public class FlowerPieceView : MonoBehaviour
{
    private const string IconNamePrefix = "FlowerImage";

    private readonly List<Image> icons = new();
    private TMP_Text nameText;

    /// <summary>꽃 아이콘 이미지들(하나뿐이면 그 하나). 화면별로 색을 바꿀 때 모두에 적용한다.</summary>
    public IReadOnlyList<Image> Icons
    {
        get
        {
            Resolve();
            return icons;
        }
    }

    /// <summary>꽃이 한꺼번에 움직이는 단위(꽃이 여럿이면 그 꽃들의 부모, 하나면 그 아이콘). 선택하면 위로 올라올 때 이것을 옮긴다.</summary>
    public RectTransform LiftTarget => Icons.Count > 1 ? (RectTransform)Icons[0].transform.parent : Icons[0].rectTransform;

    /// <summary>템플릿 복제본에 이 뷰가 이미 있으면 그것을, 없으면 붙여서 돌려준다.</summary>
    public static FlowerPieceView GetOrAdd(GameObject target)
    {
        return target.TryGetComponent(out FlowerPieceView view) ? view : target.AddComponent<FlowerPieceView>();
    }

    private void Awake()
    {
        Resolve();
    }

    /// <summary>자식을 찾아 둔다. 꺼진 부모 아래에서 만들어져 Awake가 아직 돌지 않았어도(예: 퍼즐을 완성해 버켓이 꺼진 뒤) Apply가 안전하도록 쓰는 쪽에서도 부른다.</summary>
    private void Resolve()
    {
        if (icons.Count > 0) return;

        // "FlowerImage" 자식이 있으면(배경 틀 + 꽃 아이콘이 분리된 템플릿) 그쪽에 적용,
        // 없으면(옛 단일 이미지 프리팹) 자기 자신의 Image를 그대로 쓴다.
        icons.AddRange(GetComponentsInChildren<Image>(true).Where(i => i.name.StartsWith(IconNamePrefix)));
        if (icons.Count == 0) icons.Add(TryGetComponent(out Image own) ? own : gameObject.AddComponent<Image>());

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        nameText = texts.FirstOrDefault(t => t.name == "FlowerName");
    }

    /// <param name="sprite">아이콘 스프라이트. null이면 템플릿에 있던 이미지를 그대로 둔다.</param>
    public void Apply(FlowerData flower, Sprite sprite, bool preserveAspect)
    {
        Resolve();
        Color roleColor = ColorPalette.ToRoleColor(flower.flowerRole);
        foreach (Image icon in icons)
        {
            if (sprite != null) icon.sprite = sprite;
            icon.preserveAspect = preserveAspect;
            icon.color = ColorPalette.ToUnityColor(flower.color);

            if (icon.TryGetComponent(out Outline outline)) // 꽃 아이콘의 외곽선(있으면 꽃의 역할 상징 색으로 칠해진다)
            {
                Color outlineColor = roleColor;
                outlineColor.a = outline.effectColor.a; // 투명도는 프리팹에 정해 둔 값을 따른다
                outline.effectColor = outlineColor;
            }
        }
        if (nameText != null) nameText.text = flower.flowerName;
    }

    /// <summary>꽃 아이콘 전부의 색을 바꾼다(재고 없음 같은 화면별 상태 표시용).</summary>
    public void SetIconColor(Color color)
    {
        foreach (Image icon in Icons) icon.color = color;
    }
}
