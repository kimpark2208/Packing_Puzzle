using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꽃 한 송이를 보여주는 템플릿(트레이 조각 FlowerTemplate, 선택 화면 슬롯 VaseTemplate)의 표시만 담당한다:
/// 아이콘, 속성(역할) 마크, 이름. 템플릿마다 자식 위치가 달라서 이름으로 깊이 찾고,
/// 없는 요소는 건너뛴다. 드래그 입력은 BlockDrag, 선택 개수 같은 화면별 동작은 각 화면이 맡는다.
/// </summary>
public class FlowerPieceView : MonoBehaviour
{
    private Image icon;
    private Image featureMark; // 꽃의 속성(역할) 상징 색으로 칠해지는 마크
    private TMP_Text nameText;

    /// <summary>꽃 아이콘 이미지. 화면별로 선택 상태 등에 맞춰 색을 바꿀 때 쓴다.</summary>
    public Image Icon => icon;

    private void Awake()
    {
        // "FlowerImage" 자식이 있으면(배경 틀 + 꽃 아이콘이 분리된 템플릿) 그쪽에 적용,
        // 없으면(옛 단일 이미지 프리팹) 자기 자신의 Image를 그대로 쓴다.
        icon = FindChild<Image>("FlowerImage");
        if (icon == null) icon = GetComponent<Image>();
        if (icon == null) icon = gameObject.AddComponent<Image>();

        featureMark = FindChild<Image>("FeatureMark_temp");
        nameText = FindChild<TMP_Text>("FlowerName");
    }

    private T FindChild<T>(string childName) where T : Component
    {
        Transform t = GetComponentsInChildren<Transform>(true).FirstOrDefault(c => c.name == childName);
        return t != null ? t.GetComponent<T>() : null;
    }

    /// <param name="sprite">아이콘 스프라이트. null이면 템플릿에 있던 이미지를 그대로 둔다.</param>
    public void Apply(FlowerData flower, Sprite sprite, bool preserveAspect)
    {
        if (sprite != null) icon.sprite = sprite;
        icon.preserveAspect = preserveAspect;
        icon.color = ColorPalette.ToUnityColor(flower.color);

        if (featureMark != null)
        {
            Color mark = ColorPalette.ToRoleColor(flower.flowerRole);
            mark.a = featureMark.color.a; // 투명도는 프리팹에 정해 둔 값을 따른다
            featureMark.color = mark;
        }
        if (nameText != null) nameText.text = flower.flowerName;
    }
}
