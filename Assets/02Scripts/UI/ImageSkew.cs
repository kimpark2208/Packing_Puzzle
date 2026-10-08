using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 이미지/TMP 글자를 평행사변형 꼴로 기울이는(전단, Skew) 효과. 가로선만 기울고 세로선은 그대로 선다.
/// 각도가 음수면 오른쪽이 내려가고 양수면 올라간다. 요소의 가로 중앙을 기준으로 기울어서 위치는 그대로다.
/// 기본 각도는 DefaultAngle 하나를 모든 요소가 따르고, 요소마다 overrideAngle을 켜면 따로 줄 수 있다.
/// Image는 메쉬 효과(ModifyMesh)로, TMP는 메쉬 효과가 안 먹혀서 텍스트 메쉬 갱신 시점(OnPreRenderText)에 처리한다.
/// </summary>
[RequireComponent(typeof(Graphic))]
public class ImageSkew : BaseMeshEffect
{
    /// <summary>모든 요소에 공통으로 적용되는 기본 기울기(도). 여기를 바꾸면 오버라이드 안 한 요소 전부에 반영된다.</summary>
    public const float DefaultAngle = -3.5f;

    [SerializeField] private bool overrideAngle;
    [SerializeField, Range(-30f, 30f)] private float angle = DefaultAngle;

    private TMP_Text tmp;

    private float Slope => Mathf.Tan((overrideAngle ? angle : DefaultAngle) * Mathf.Deg2Rad);

    protected override void OnEnable()
    {
        tmp = GetComponent<TMP_Text>();
        if (tmp != null) tmp.OnPreRenderText += SkewText;
        base.OnEnable();
    }

    protected override void OnDisable()
    {
        if (tmp != null) tmp.OnPreRenderText -= SkewText;
        base.OnDisable();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || tmp != null) return;

        float slope = Slope;
        float centerX = graphic.rectTransform.rect.center.x;
        var v = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            v.position = Shear(v.position, centerX, slope);
            vh.SetUIVertex(v, i);
        }
    }

    private void SkewText(TMP_TextInfo info)
    {
        float slope = Slope;
        float centerX = tmp.rectTransform.rect.center.x;
        foreach (TMP_MeshInfo mesh in info.meshInfo)
        {
            for (int i = 0; i < mesh.vertexCount; i++) mesh.vertices[i] = Shear(mesh.vertices[i], centerX, slope);
        }
    }

    private static Vector3 Shear(Vector3 p, float centerX, float slope)
    {
        p.y += (p.x - centerX) * slope;
        return p;
    }
}
