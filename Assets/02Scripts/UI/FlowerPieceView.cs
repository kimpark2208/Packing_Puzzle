using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 꽃 한 송이를 보여주는 템플릿(트레이 조각 FlowerTemplate, 선택 화면 슬롯 VaseTemplate)의 표시만 담당한다:
/// 아이콘, 아이콘 외곽선(역할 색), 이름. 템플릿마다 자식 위치가 달라서 이름으로 깊이 찾고,
/// 없는 요소는 건너뛴다. 드래그 입력은 BlockDrag, 선택 개수 같은 화면별 동작은 각 화면이 맡는다.
/// </summary>
public class FlowerPieceView : MonoBehaviour
{
    private Image icon;
    private Outline outline; // 꽃 아이콘의 외곽선(있으면 꽃의 역할 상징 색으로 칠해진다)
    private TMP_Text nameText;

    /// <summary>꽃 아이콘 이미지. 화면별로 선택 상태 등에 맞춰 색을 바꿀 때 쓴다.</summary>
    public Image Icon
    {
        get
        {
            Resolve();
            return icon;
        }
    }

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
        if (icon != null) return;

        // "FlowerImage" 자식이 있으면(배경 틀 + 꽃 아이콘이 분리된 템플릿) 그쪽에 적용,
        // 없으면(옛 단일 이미지 프리팹) 자기 자신의 Image를 그대로 쓴다.
        icon = FindChild<Image>("FlowerImage");
        if (icon == null) icon = GetComponent<Image>();
        if (icon == null) icon = gameObject.AddComponent<Image>();

        outline = icon.GetComponent<Outline>();
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
        Resolve();
        if (sprite != null) icon.sprite = sprite;
        icon.preserveAspect = preserveAspect;
        icon.color = ColorPalette.ToUnityColor(flower.color);

        if (outline != null)
        {
            Color roleColor = ColorPalette.ToRoleColor(flower.flowerRole);
            roleColor.a = outline.effectColor.a; // 투명도는 프리팹에 정해 둔 값을 따른다
            outline.effectColor = roleColor;
        }
        if (nameText != null) nameText.text = flower.flowerName;
    }
}
